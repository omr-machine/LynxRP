using LynxRP;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

using static Unity.Mathematics.math;

namespace LynxRP
{
    public partial class LightingPass
    {
        static readonly ProfilingSampler sampler = new("Lighting");

        const int 
            maxDirectionalLightCount = 4, maxOtherLightCount = 128;

        static readonly int
            directionalLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
            directionalLightDataId = Shader.PropertyToID("_DirectionalLightData"),
            otherLightCountId = Shader.PropertyToID("_OtherLightCount"),
            otherLightDataId = Shader.PropertyToID("_OtherLightData"),
            tilesId = Shader.PropertyToID("_ForwardPlusTiles"),
            tileSettingsId = Shader.PropertyToID("_ForwardPlusTileSettings");

        static readonly DirectionalLightData[] directionalLightData =
            new DirectionalLightData[maxDirectionalLightCount];

        static readonly OtherLightData[] otherLightData =
            new OtherLightData[maxOtherLightCount];

        BufferHandle directionalLightDataBuffer, otherLightDataBuffer, tilesBuffer;

        CullingResults cullingResults;

        readonly Shadows shadows = new();

        int directionalLightCount, otherLightCount;

        NativeArray<int> tileData;

        NativeArray<float4> lightBounds;
        
        JobHandle forwardPlusJobHandle;

        Vector2 screenUVToTileCoordinates;

        Vector2Int tileCount;

        int maxLightsPerTile, tileDataSize, maxTileDataSize;

        int TileCount => tileCount.x * tileCount.y;

        void Setup(
            CullingResults cullingResults, 
            Vector2Int attachmentSize,
            ForwardPlusSettings forwardPlusSettings,
            ShadowSettings shadowSettings,
            int renderingLayerMask
        )
        {
            this.cullingResults = cullingResults;
            shadows.Setup(cullingResults, shadowSettings);

            maxLightsPerTile = forwardPlusSettings.maxLightsPerTile <= 0 ?
                31 : forwardPlusSettings.maxLightsPerTile;
            maxTileDataSize = maxLightsPerTile + 1;

            lightBounds = new NativeArray<float4>(
                maxOtherLightCount, Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory
            );

            float tileScreenPixelSize = forwardPlusSettings.tileSize <= 0 ?
                64f : (float)forwardPlusSettings.tileSize;
            screenUVToTileCoordinates.x =
                attachmentSize.x / tileScreenPixelSize;
            screenUVToTileCoordinates.y =
                attachmentSize.y / tileScreenPixelSize;
            tileCount.x = Mathf.CeilToInt(screenUVToTileCoordinates.x);
            tileCount.y = Mathf.CeilToInt(screenUVToTileCoordinates.y);
            

            SetupLights(renderingLayerMask);
        }

        void SetupForwardPlus(int lightIndex, ref VisibleLight visibleLight)
        {
            Rect r = visibleLight.screenRect;
            lightBounds[lightIndex] = float4(r.xMin, r.yMin, r.xMax, r.yMax);
        }

        void SetupLights(int renderingLayerMask)
        {
            NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;

            int requiredMaxLightsPerTile = Mathf.Min(
                maxLightsPerTile, visibleLights.Length
            );
            tileDataSize = requiredMaxLightsPerTile + 1;

            directionalLightCount = otherLightCount = 0;
            int i;
            for (i = 0; i < visibleLights.Length; i++)
            {
                VisibleLight visibleLight = visibleLights[i];
                Light light = visibleLight.light;
                if ((visibleLight.light.renderingLayerMask & renderingLayerMask) != 0)
                {
                    switch (visibleLight.lightType)
                    {
                        case LightType.Directional:
                            if (directionalLightCount < maxDirectionalLightCount)
                            {
                                directionalLightData[directionalLightCount++] =
                                    new DirectionalLightData(
                                        ref visibleLight,
                                        shadows.ReserveDirectionalShadows(light, i)
                                    );
                            }
                            break;
                        case LightType.Point:
                            if (otherLightCount < maxOtherLightCount)
                            {
                                SetupForwardPlus(otherLightCount, ref visibleLight);
                                otherLightData[otherLightCount++] =
                                    OtherLightData.CreatePointLight(
                                        ref visibleLight, 
                                        shadows.ReserveOtherShadows(light, i)
                                    );
                            }
                            break;
                        case LightType.Spot:
                            if (otherLightCount < maxOtherLightCount)
                            {
                                SetupForwardPlus(otherLightCount, ref visibleLight);
                                otherLightData[otherLightCount++] =
                                    OtherLightData.CreateSpotLight(
                                        ref visibleLight,
                                        shadows.ReserveOtherShadows(light, i)
                                    );
                            }
                            break;
                    }
                }
            }

            tileData = new NativeArray<int>(
                TileCount * tileDataSize, Allocator.TempJob
            );
            forwardPlusJobHandle = new ForwardPlusTilesJob
            {
                lightBounds = lightBounds,
                tileData = tileData,
                otherLightCount = otherLightCount,
                tileScreenUVSize = float2(
                    1f / screenUVToTileCoordinates.x,
                    1f / screenUVToTileCoordinates.y
                ),
                maxLightsPerTile = requiredMaxLightsPerTile,
                tilesPerRow = tileCount.x,
                tileDataSize = tileDataSize
            }.ScheduleParallel(TileCount, tileCount.x, default);
            
        }

        void Render(RenderGraphContext context)
        {
            CommandBuffer buffer = context.cmd;

            buffer.SetGlobalInt(directionalLightCountId, directionalLightCount);
            // if (directionalLightCount > 0) { }
            buffer.SetBufferData(
                directionalLightDataBuffer, directionalLightData,
                0, 0, directionalLightCount
            );
            buffer.SetGlobalBuffer(directionalLightDataId, directionalLightDataBuffer);

            buffer.SetGlobalInt(otherLightCountId, otherLightCount);
            // if (otherLightCount > 0) { }
            buffer.SetBufferData(
                otherLightDataBuffer, otherLightData, 
                0, 0, otherLightCount
            );
            buffer.SetGlobalBuffer(otherLightDataId, otherLightDataBuffer);

            shadows.Render(context);

            forwardPlusJobHandle.Complete();
            buffer.SetBufferData(
                tilesBuffer, tileData, 0, 0, tileData.Length
            );
            buffer.SetGlobalBuffer(tilesId, tilesBuffer);
            buffer.SetGlobalVector(tileSettingsId, new Vector4(
                screenUVToTileCoordinates.x, screenUVToTileCoordinates.y,
                tileCount.x.ReinterpretAsFloat(),
                tileDataSize.ReinterpretAsFloat()
            ));
            context.renderContext.ExecuteCommandBuffer(buffer);
            buffer.Clear();
            lightBounds.Dispose();
            tileData.Dispose();
        }

        public static LightResources Record
        (
            RenderGraph renderGraph,
            CullingResults cullingResults, 
            Vector2Int attachmentSize,
            ForwardPlusSettings forwardPlusSettings,
            ShadowSettings shadowSettings,
            int renderingLayerMask,
            ScriptableRenderContext context
        )
        {
            using RenderGraphBuilder builder = renderGraph.AddRenderPass(
                sampler.name, out LightingPass pass, sampler
            );
            pass.Setup(
                cullingResults, attachmentSize, 
                forwardPlusSettings,shadowSettings,
                renderingLayerMask
            );
            pass.directionalLightDataBuffer = builder.WriteBuffer(
                renderGraph.CreateBuffer(
                    new BufferDesc
                    {
                        name = "Directional Light Data",
                        count = maxDirectionalLightCount,
                        stride = DirectionalLightData.stride,
                        target = GraphicsBuffer.Target.Structured
                    }
                )
            );
            pass.otherLightDataBuffer = builder.WriteBuffer(
                renderGraph.CreateBuffer(
                    new BufferDesc
                    {
                        name = "Other Light Data",
                        count = maxOtherLightCount,
                        stride = OtherLightData.stride,
                        target = GraphicsBuffer.Target.Structured
                    }
                )
            );
            pass.tilesBuffer = builder.WriteBuffer(
                renderGraph.CreateBuffer(
                    new BufferDesc
                    {
                        name = "Forward+ Tiles",
                        count = pass.TileCount * pass.maxTileDataSize,
                        stride = 4,
                        target = GraphicsBuffer.Target.Structured
                    }
                )
            );
            
            builder.SetRenderFunc<LightingPass>(
                static (pass, context) => pass.Render(context)
            );
            builder.AllowPassCulling(false);
            return new LightResources(
                pass.directionalLightDataBuffer,
                pass.otherLightDataBuffer,
                pass.tilesBuffer,
                pass.shadows.GetResources(renderGraph, builder, context)
            );
        }
    }
} 
