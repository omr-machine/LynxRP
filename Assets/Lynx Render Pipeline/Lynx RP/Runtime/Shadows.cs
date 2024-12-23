using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using Unity.Collections;

namespace LynxRP
{
    public partial class Shadows
    {
        const int maxShadowedDirLightCount = 4, maxShadowedOtherLightCount = 16;
        const int maxCascades = 4;
        const int maxTilesPerLight = 6;

        static readonly GlobalKeyword[] filterQualityKeywords = {
            GlobalKeyword.Create("_SHADOW_FILTER_LOW"),
            GlobalKeyword.Create("_SHADOW_FILTER_MEDIUM"),
            GlobalKeyword.Create("_SHADOW_FILTER_HIGH"),
        };

        static readonly GlobalKeyword[] cascadeBlendKeywords = {
            GlobalKeyword.Create("_CASCADE_BLEND_SOFT"),
            GlobalKeyword.Create("_CASCADE_BLEND_DITHER"),
        };

        static readonly GlobalKeyword[] shadowMaskKeywords = {
            GlobalKeyword.Create("_SHADOW_MASK_ALWAYS"),
            GlobalKeyword.Create("_SHADOW_MASK_DISTANCE"),
        };

        static readonly int
            dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
            dirShadowCascadesId = Shader.PropertyToID("_DirectionalShadowCascades"),
            dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
            otherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas"),
            otherShadowDataId = Shader.PropertyToID("_OtherShadowData"),
            cascadeCountId = Shader.PropertyToID("_CascadeCount"),
            shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
            shadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade"),
            shadowPancakingId = Shader.PropertyToID("_ShadowPancaking");

        static readonly DirectionalShadowCascade[] dirShadowCascades =
            new DirectionalShadowCascade[maxCascades]; 

        static readonly Matrix4x4[]
            dirShadowMatrices = new Matrix4x4[maxShadowedDirLightCount * maxCascades];

        static readonly OtherShadowData[] otherShadowData =
            new OtherShadowData[maxShadowedOtherLightCount];

        struct ShadowedDirectionalLight
        {
            public int visibleLightIndex;
            public float slopeScaleBias;
            public float nearPlaneOffset;
        }

        struct ShadowedOtherLight
        {
            public int visibleLightIndex;
            public float slopeScaleBias;
            public float normalBias;
            public bool isPoint;
        }

        readonly ShadowedDirectionalLight[] shadowedDirectionalLights =
            new ShadowedDirectionalLight[maxShadowedDirLightCount];

        readonly ShadowedOtherLight[] shadowedOtherLights =
            new ShadowedOtherLight[maxShadowedOtherLightCount];

        int shadowedDirLightCount, shadowedOtherLightCount;
        
        CommandBuffer buffer;

        CullingResults cullingResults;
        ShadowSettings settings;

        bool useShadowMask;

        Vector4 atlasSizes;

        TextureHandle directionalAtlas, otherAtlas;

        BufferHandle
            directionalShadowCascadesBuffer,
            directionalShadowMatricesBuffer,
            otherShadowDataBuffer;

        NativeArray<LightShadowCasterCullingInfo> cullingInfoPerLight;
        NativeArray<ShadowSplitData> shadowSplitDataPerLight;

        struct RenderInfo
        {
            public RendererListHandle handle;

            public Matrix4x4 view, projection;
        }

        RenderInfo[]
            directionalRenderInfo = new RenderInfo[maxShadowedDirLightCount * maxCascades],
            otherRenderInfo = new RenderInfo[maxShadowedOtherLightCount * maxTilesPerLight];

        int directionalSplit, directionalTileSize;
        int otherSplit, otherTileSize;

        public void Setup(
            CullingResults cullingResults, ShadowSettings settings
        )
        {
            this.cullingResults = cullingResults;
            this.settings = settings;

            shadowedDirLightCount = shadowedOtherLightCount = 0;

            useShadowMask = false;

            cullingInfoPerLight = new NativeArray<LightShadowCasterCullingInfo>(
                cullingResults.visibleLights.Length, Allocator.Temp
            );
            shadowSplitDataPerLight = new NativeArray<ShadowSplitData>(
                cullingInfoPerLight.Length * maxTilesPerLight,
                Allocator.Temp, NativeArrayOptions.UninitializedMemory
            );
        }

        public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex)
        {
            if (
                shadowedDirLightCount < maxShadowedDirLightCount &&
                light.shadows != LightShadows.None && light.shadowStrength > 0f
            )
            {
                float maskChannel = -1;
                LightBakingOutput lightBaking = light.bakingOutput;
                if (
                    lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                    lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
                )
                {
                    useShadowMask = true;
                    maskChannel = lightBaking.occlusionMaskChannel;
                }

                if (!cullingResults.GetShadowCasterBounds(
                    visibleLightIndex, out Bounds b
                ))
                {
                    return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
                }

                shadowedDirectionalLights[shadowedDirLightCount] =
                    new ShadowedDirectionalLight
                    {
                        visibleLightIndex = visibleLightIndex,
                        slopeScaleBias = light.shadowBias,
                        nearPlaneOffset = light.shadowNearPlane
                    };
                return new Vector4(
                    light.shadowStrength,
                    settings.directional.cascadeCount * shadowedDirLightCount++,
                    light.shadowNormalBias, maskChannel
                );
            }
            return new Vector4(0f, 0f, 0f, -1f);
        }
        
        public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex)
        {
            if (light.shadows == LightShadows.None || light.shadowStrength <= 0f)
            {
                return new Vector4(0f, 0f, 0f, -1f);
            }

            float maskChannel = -1f;
            LightBakingOutput lightBaking = light.bakingOutput;
            if (
                lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
                lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask
            )
            {
                useShadowMask = true;
                maskChannel = lightBaking.occlusionMaskChannel;
            }

            bool isPoint = light.type == LightType.Point;
            int newLightCount = shadowedOtherLightCount + (isPoint ? 6 : 1);
            if (
                newLightCount > maxShadowedOtherLightCount ||
                !cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)
            )
            {
                return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
            }

            shadowedOtherLights[shadowedOtherLightCount] = new ShadowedOtherLight
            {
                visibleLightIndex = visibleLightIndex,
                slopeScaleBias = light.shadowBias,
                normalBias = light.shadowNormalBias,
                isPoint = isPoint
            };

            Vector4 data = new Vector4(
                light.shadowStrength, shadowedOtherLightCount,
                isPoint ? 1f : 0f, maskChannel
            );
            shadowedOtherLightCount = newLightCount;
            return data;
        }

        public ShadowResources GetResources(
            RenderGraph renderGraph,
            RenderGraphBuilder builder,
            ScriptableRenderContext context
        )
        {
            int atlasSize = (int)settings.directional.atlasSize;
            var desc = new TextureDesc(atlasSize, atlasSize)
            {
                depthBufferBits = DepthBits.Depth32,
                isShadowMap = true,
                name = "Directional Shadow Atlas"
            };
            directionalAtlas = shadowedDirLightCount > 0 ?
                builder.WriteTexture(renderGraph.CreateTexture(desc)) :
                renderGraph.defaultResources.defaultShadowTexture;

            directionalShadowCascadesBuffer = builder.WriteBuffer(
                renderGraph.CreateBuffer(
                    new BufferDesc
                    {
                        name = "Shadow Cascades",
                        stride = DirectionalShadowCascade.stride,
                        count = maxCascades,
                        target = GraphicsBuffer.Target.Structured
                    }
                )
            );

            directionalShadowMatricesBuffer = builder.WriteBuffer(
                renderGraph.CreateBuffer(
                    new BufferDesc
                    {
                        name = "Directional Shadow Matrices",
                        stride = 4 * 16,
                        count = maxShadowedDirLightCount * maxCascades,
                        target = GraphicsBuffer.Target.Structured
                    }
                )
            );

            atlasSize = (int)settings.other.atlasSize;
            desc.width = desc.height = atlasSize;
            desc.name = "Other Shadow Atlas";
            otherAtlas = shadowedOtherLightCount > 0 ?
                    builder.WriteTexture(renderGraph.CreateTexture(desc)) :
                    renderGraph.defaultResources.defaultShadowTexture;

            otherShadowDataBuffer = builder.WriteBuffer(
                renderGraph.CreateBuffer(
                    new BufferDesc
                    {
                        name = "Other Shadow Data",
                        stride = OtherShadowData.stride,
                        count = maxShadowedOtherLightCount,
                        target = GraphicsBuffer.Target.Structured
                    }
                )
            );

            BuildRendererLists(renderGraph, builder, context);

            return new ShadowResources(
                directionalAtlas,
                otherAtlas,
                directionalShadowCascadesBuffer,
                directionalShadowMatricesBuffer,
                otherShadowDataBuffer
            );
        }

        void BuildRendererLists
        (
            RenderGraph renderGraph, RenderGraphBuilder builder, ScriptableRenderContext context)
        {
            if (shadowedDirLightCount > 0)
            {
                int atlasSize = (int)settings.directional.atlasSize;
                int tiles = shadowedDirLightCount * settings.directional.cascadeCount;
                directionalSplit = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
                directionalTileSize = atlasSize / directionalSplit;

                for (int i = 0; i < shadowedDirLightCount; i++)
                {
                    BuildDirectionalRendererList(i, renderGraph, builder);
                }
            }

            if (shadowedOtherLightCount > 0)
            {
                int atlasSize = (int)settings.other.atlasSize;
                int tiles = shadowedOtherLightCount;
                otherSplit = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
                otherTileSize = atlasSize / otherSplit;

                for (int i = 0; i < shadowedOtherLightCount;)
                {
                    if (shadowedOtherLights[i].isPoint)
                    {
                        BuildPointShadowsRendererList(i, renderGraph, builder);
                        i += 6;
                    }
                    else
                    {
                        BuildSpotShadowsRendererList(i, renderGraph, builder);
                        i += 1;
                    }
                }
            }

            if (shadowedDirLightCount + shadowedOtherLightCount > 0)
            {
                context.CullShadowCasters(
                    cullingResults,
                    new ShadowCastersCullingInfos
                    {
                        perLightInfos = cullingInfoPerLight,
                        splitBuffer = shadowSplitDataPerLight
                    });
            }
        }

        void BuildDirectionalRendererList(
            int index, RenderGraph renderGraph, RenderGraphBuilder builder)
        {
            ShadowedDirectionalLight light = shadowedDirectionalLights[index];
            var shadowSettings = new ShadowDrawingSettings(
                cullingResults, light.visibleLightIndex)
            {
                useRenderingLayerMaskTest = true
            };

            int cascadeCount = settings.directional.cascadeCount;
            Vector3 ratios = settings.directional.CascadeRatios;
            float cullingFactor = Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);
            int splitOffset = light.visibleLightIndex * maxTilesPerLight;
            for (int i = 0; i < cascadeCount; i++)
            {
                ref RenderInfo info = ref directionalRenderInfo[index * maxCascades + i];
                cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                    light.visibleLightIndex, i, cascadeCount, ratios,
                    directionalTileSize, light.nearPlaneOffset, out info.view,
                    out info.projection, out ShadowSplitData splitData);
                splitData.shadowCascadeBlendCullingFactor = cullingFactor;
                shadowSplitDataPerLight[splitOffset + i] = splitData;
                if (index == 0)
                {
                    dirShadowCascades[i] = new DirectionalShadowCascade(
                        splitData.cullingSphere,
                        directionalTileSize, settings.DirectionalFilterSize);
                }
                info.handle = builder.UseRendererList(
                    renderGraph.CreateShadowRendererList(ref shadowSettings));

                // Debug.Log(projectionMatrix);
                // Debug.Log(viewMatrix);
                // Debug.Log(dirShadowMatrices[index]);
                // Debug.Log("@@@@@@@@@");
            }

            cullingInfoPerLight[light.visibleLightIndex] =
                new LightShadowCasterCullingInfo
                {
                    projectionType = BatchCullingProjectionType.Orthographic,
                    splitRange = new RangeInt(splitOffset, cascadeCount)
                };
        }

        void BuildSpotShadowsRendererList(
            int index, RenderGraph renderGraph, RenderGraphBuilder builder)
        {
            ShadowedOtherLight light = shadowedOtherLights[index];
            var shadowSettings = new ShadowDrawingSettings(
                cullingResults, light.visibleLightIndex)
            {
                useRenderingLayerMaskTest = true
            };
            ref RenderInfo info = ref otherRenderInfo[index * maxTilesPerLight];
            cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, out info.view, out info.projection,
                out ShadowSplitData splitData);

            int splitOffset = light.visibleLightIndex * maxTilesPerLight;
            shadowSplitDataPerLight[splitOffset] = splitData;
            info.handle = builder.UseRendererList(
                renderGraph.CreateShadowRendererList(ref shadowSettings));
            cullingInfoPerLight[light.visibleLightIndex] =
                new LightShadowCasterCullingInfo
                {
                    projectionType = BatchCullingProjectionType.Perspective,
                    splitRange = new RangeInt(splitOffset, 1)
                };
        }

        void BuildPointShadowsRendererList(
            int index, RenderGraph renderGraph, RenderGraphBuilder builder)
        {
            ShadowedOtherLight light = shadowedOtherLights[index];
            var shadowSettings = new ShadowDrawingSettings(
                cullingResults, light.visibleLightIndex)
            {
                useRenderingLayerMaskTest = true
            };
            float texelSize = 2f / otherTileSize;
            float filterSize = texelSize * settings.OtherFilterSize;
            float bias = light.normalBias * filterSize * 1.4142136f;
            float fovBias = Mathf.Atan(1f + bias + filterSize) * Mathf.Rad2Deg * 2f - 90f;
            int splitOffset = light.visibleLightIndex * maxTilesPerLight;
            for (int i = 0; i < 6; i++)
            {
                ref RenderInfo info = ref otherRenderInfo[index * maxTilesPerLight + i];
                cullingResults.ComputePointShadowMatricesAndCullingPrimitives(
                    light.visibleLightIndex, (CubemapFace)i, fovBias,
                    out info.view, out info.projection,
                    out ShadowSplitData splitData);
                shadowSplitDataPerLight[splitOffset + i] = splitData;
                info.handle = builder.UseRendererList(
                    renderGraph.CreateShadowRendererList(ref shadowSettings));
            }

            cullingInfoPerLight[light.visibleLightIndex] =
                new LightShadowCasterCullingInfo
                {
                    projectionType = BatchCullingProjectionType.Perspective,
                    splitRange = new RangeInt(splitOffset, 6)
                };
        }

        public void Render(RenderGraphContext context)
        {
            buffer = context.cmd;

            if (shadowedDirLightCount > 0)
            {
                RenderDirectionalShadows();
            }
            if (shadowedOtherLightCount > 0)
            {
                RenderOtherShadows();
            }
            SetKeywords(filterQualityKeywords, (int)settings.filterQuality - 1);

            buffer.SetGlobalDepthBias(0f, 0f);
            buffer.SetGlobalBuffer(dirShadowCascadesId, directionalShadowCascadesBuffer);
            buffer.SetGlobalBuffer(dirShadowMatricesId, directionalShadowMatricesBuffer);
            buffer.SetGlobalBuffer(otherShadowDataId, otherShadowDataBuffer);
            buffer.SetGlobalTexture(dirShadowAtlasId, directionalAtlas);
            buffer.SetGlobalTexture(otherShadowAtlasId, otherAtlas);

            SetKeywords(shadowMaskKeywords, useShadowMask ?
                QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 :
                -1
            );

            buffer.SetGlobalInt(
                cascadeCountId,
                shadowedDirLightCount > 0 ? settings.directional.cascadeCount : 0
            );
            float f = 1f - settings.directional.cascadeFade;
            buffer.SetGlobalVector(
                shadowDistanceFadeId, new Vector4(
                    1f / settings.maxDistance, 1f / settings.distanceFade, 1f / (1f - f * f)
                )
            );

            buffer.SetGlobalVector(shadowAtlasSizeId, atlasSizes);

            context.renderContext.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        void RenderDirectionalShadows()
        {
            int atlasSize = (int)settings.directional.atlasSize;
            atlasSizes.x = atlasSize;
            atlasSizes.y = 1f / atlasSize;
            buffer.BeginSample("Directional Shadows");
            buffer.SetRenderTarget(
                directionalAtlas, 
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
            buffer.ClearRenderTarget(true, false, Color.clear);
            buffer.SetGlobalFloat(shadowPancakingId, 1f);

            for (int i = 0; i < shadowedDirLightCount; i++)
            {
                RenderDirectionalShadows(i);
            }

            buffer.SetBufferData(
                directionalShadowCascadesBuffer, dirShadowCascades,
                0, 0, settings.directional.cascadeCount
            );
            buffer.SetBufferData(
                directionalShadowMatricesBuffer, dirShadowMatrices,
                0, 0, shadowedDirLightCount * settings.directional.cascadeCount
            );

            SetKeywords(cascadeBlendKeywords, (int)settings.directional.cascadeBlend - 1);
            buffer.EndSample("Directional Shadows");
        }

        void RenderOtherShadows()
        {
            int atlasSize = (int)settings.other.atlasSize;
            atlasSizes.z = atlasSize;
            atlasSizes.w = 1f / atlasSize;
            buffer.BeginSample("Other Shadows");
            buffer.SetRenderTarget(
                otherAtlas, 
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
            buffer.ClearRenderTarget(true, false, Color.clear);
            buffer.SetGlobalFloat(shadowPancakingId, 0f);

            for (int i = 0; i < shadowedOtherLightCount;)
            {
                if (shadowedOtherLights[i].isPoint)
                {
                    RenderPointShadows(i);
                    i += 6;
                }
                else 
                {
                    RenderSpotShadows(i);
                    i += 1;
                }
            }

            buffer.SetBufferData(
                otherShadowDataBuffer, otherShadowData,
                0, 0, shadowedOtherLightCount
            );

            buffer.EndSample("Other Shadows");
        }

        void RenderDirectionalShadows(int index)
        {
            int cascadeCount = settings.directional.cascadeCount;
            int tileOffset = index * cascadeCount;

            float tileScale = 1f / directionalSplit;
            buffer.SetGlobalDepthBias(0f, shadowedDirectionalLights[index].slopeScaleBias);

            for (int i = 0; i < cascadeCount; i++)
            {
                RenderInfo info = directionalRenderInfo[index * maxCascades + i];
                int tileIndex = tileOffset + i;
                dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
                    info.projection * info.view,
                    SetTileViewport(tileIndex, directionalSplit, directionalTileSize), tileScale
                );

                buffer.SetViewProjectionMatrices(info.view, info.projection);
                buffer.DrawRendererList(info.handle);

                // Debug.Log(projectionMatrix);
                // Debug.Log(viewMatrix);
                // Debug.Log(dirShadowMatrices[index]);
                // Debug.Log("@@@@@@@@@");
            }
        }

        void RenderSpotShadows(int index)
        {
            ShadowedOtherLight light = shadowedOtherLights[index];

            RenderInfo info = otherRenderInfo[index * maxTilesPerLight];
            float texelSize = 2f / (otherTileSize * info.projection.m00); // projection scale
            float filterSize = texelSize * settings.OtherFilterSize;
            float bias = light.normalBias * filterSize * 1.4142136f;
            
            Vector2 offset = SetTileViewport(index, otherSplit, otherTileSize);
            float tileScale = 1f / otherSplit;

            otherShadowData[index] = new OtherShadowData(
                offset, tileScale, bias, atlasSizes.w * 0.5f,
                ConvertToAtlasMatrix(info.projection * info.view, offset, tileScale)
            );
            buffer.SetViewProjectionMatrices(info.view, info.projection);
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            buffer.DrawRendererList(info.handle);
        }

        void RenderPointShadows(int index)
        {
            ShadowedOtherLight light = shadowedOtherLights[index];

            float texelSize = 2f / otherTileSize;
            float filterSize = texelSize * settings.OtherFilterSize;
            float bias = light.normalBias * filterSize * 1.4142136f;
            float tileScale = 1f / otherSplit;
            buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);

            for (int i = 0; i < 6; i++)
            {
                RenderInfo info = otherRenderInfo[index * maxTilesPerLight + i];
                info.view.m11 = -info.view.m11;
                info.view.m12 = -info.view.m12;
                info.view.m13 = -info.view.m13;

                int tileIndex = index + i;
                Vector2 offset = SetTileViewport(tileIndex, otherSplit, otherTileSize);

                otherShadowData[tileIndex] = new OtherShadowData(
                    offset, tileScale, bias, atlasSizes.w * 0.5f,
                    ConvertToAtlasMatrix(info.projection * info.view, offset, tileScale)
                );

                buffer.SetViewProjectionMatrices(info.view, info.projection);
                buffer.DrawRendererList(info.handle);
            }
        }

        Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, float scale)
        {
            if (SystemInfo.usesReversedZBuffer)
            {
                m.m20 = -m.m20;
                m.m21 = -m.m21;
                m.m22 = -m.m22;
                m.m23 = -m.m23;
            }

            m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
            m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
            m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
            m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
            m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
            m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
            m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
            m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
            m.m20 = 0.5f * (m.m20 + m.m30);
            m.m21 = 0.5f * (m.m21 + m.m31);
            m.m22 = 0.5f * (m.m22 + m.m32);
            m.m23 = 0.5f * (m.m23 + m.m33);
            return m;
        }

        Vector2 SetTileViewport(int index, int split, float tileSize)
        {
            Vector2 offset = new Vector2(index % split, index / split);
            buffer.SetViewport(new Rect(
                offset.x * tileSize, offset.y * tileSize, tileSize, tileSize
            ));

            return offset;
        }

        void SetKeywords(GlobalKeyword[] keywords, int enabledIndex)
        {
            for (int i = 0; i < keywords.Length; i++)
            {
                buffer.SetKeyword(keywords[i], i == enabledIndex);
            }
        }
    }
}
