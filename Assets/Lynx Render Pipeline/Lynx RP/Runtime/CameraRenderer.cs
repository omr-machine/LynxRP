using LynxRP;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace LynxRP
{
    public class CameraRenderer
    {
        public const float renderScaleMin = 0.05f, renderScaleMax = 2f;

        static readonly CameraSettings defaultCameraSettings = new();

        readonly PostFXStack postFXStack = new();

        readonly Material material;

        public CameraRenderer(Shader shader, Shader cameraDebuggerShader)
        {
            material = CoreUtils.CreateEngineMaterial(shader);
            CameraDebugger.Initialize(cameraDebuggerShader);
        }

        public void Dispose() 
        {
            CoreUtils.Destroy(material);
            CameraDebugger.Cleanup();
        }

        public void Render(
            RenderGraph renderGraph,
            ScriptableRenderContext context, 
            Camera camera, 
            LynxRenderPipelineSettings settings,
            ref InterFrameData.MeshJobsData meshData
        )
        {
            CameraBufferSettings bufferSettings = settings.cameraBuffer;
            PostFXSettings postFXSettings = settings.postFXSettings;
            ShadowSettings shadowSettings = settings.shadows;
            
            ProfilingSampler cameraSampler;
            CameraSettings cameraSettings;
            if (camera.TryGetComponent(out LynxRenderPipelineCamera crpCamera))
            {
                cameraSampler = crpCamera.Sampler;
                cameraSettings = crpCamera.Settings;
            }
            else
            {
                cameraSampler = ProfilingSampler.Get(camera.cameraType);
                cameraSettings = defaultCameraSettings;
            }

            #if UNITY_EDITOR
            #pragma warning disable 0618
                if (cameraSettings.renderingLayerMask != 0)
                {
                    // Migrate camera settings to new rendering layer mask.
                    cameraSettings.newRenderingLayerMask = (uint)cameraSettings.renderingLayerMask;
                    cameraSettings.renderingLayerMask = 0;
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                        UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene()
                    );
                }
            #pragma warning restore 0618
            #endif

            bool useColorTexture, useDepthTexture;
            if (camera.cameraType == CameraType.Reflection)
            {
                useColorTexture = bufferSettings.copyColorReflection;
                useDepthTexture = bufferSettings.copyDepthReflection;
            }
            else {
                useColorTexture = bufferSettings.copyColor && cameraSettings.copyColor;
                useDepthTexture = bufferSettings.copyDepth && cameraSettings.copyDepth;
            }

            bool debugCulling = false;
            if (camera.cameraType == CameraType.SceneView)
            {
                #if UNITY_EDITOR
                    debugCulling = UnityEditor.SceneView.currentDrawingSceneView.cameraSettings.occlusionCulling;
                #endif
            }

            if (cameraSettings.overridePostFX)
            {
                postFXSettings = cameraSettings.postFXSettings;
            }
            bool hasActivePostFX =
                postFXSettings != null && PostFXSettings.AreApplicableTo(camera);

            float renderScale = cameraSettings.GetRenderScale(bufferSettings.renderScale);
            bool useScaledRendering = renderScale < 0.99f || renderScale > 1.01f;

            #if UNITY_EDITOR
                if (camera.cameraType == CameraType.SceneView)
                {
                    ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
                    useScaledRendering = false;
                }
            #endif

            if (!camera.TryGetCullingParameters(
                out ScriptableCullingParameters scriptableCullingParameters
            ))
            {
                return;
            }
            scriptableCullingParameters.shadowDistance =
                Mathf.Min(shadowSettings.maxDistance, camera.farClipPlane);
            CullingResults cullingResults = context.Cull(ref scriptableCullingParameters);

            bufferSettings.allowHDR &= camera.allowHDR;
            Vector2Int bufferSize = default;
            if (useScaledRendering)
            {
                renderScale = Mathf.Clamp(renderScale, renderScaleMin, renderScaleMax);
                bufferSize.x = (int)(camera.pixelWidth * renderScale);
                bufferSize.y = (int)(camera.pixelHeight * renderScale);
            }
            else 
            {
                bufferSize.x = camera.pixelWidth;
                bufferSize.y = camera.pixelHeight;
            }

            bufferSettings.fxaa.enabled &= cameraSettings.allowFXAA;
            bufferSettings.msaa.enabled &= cameraSettings.allowMSAA;
            bool applyMSAA = bufferSettings.msaa.enabled;
            CameraBufferSettings.MSAA.Type msaaType = bufferSettings.msaa.msaaType;

            // DrawGizmosBeforeFX();
            // DrawGizmosAfterFX();

            var renderGraphParameters = new RenderGraphParameters
            {
                commandBuffer = CommandBufferPool.Get(),
                currentFrameIndex = Time.frameCount,
                executionName = cameraSampler.name,
                rendererListCulling = true,
                scriptableRenderContext = context
            };

            renderGraph.BeginRecording(renderGraphParameters);
            using (new RenderGraphProfilingScope(renderGraph, cameraSampler))
            {
                uint pipelineType = (uint)settings.pipelineType;
                if (pipelineType == 0)
                {
                    PassesForward(
                        ref context, ref renderGraph, debugCulling,
                        camera, cullingResults, bufferSize, settings, bufferSettings,
                        postFXSettings, shadowSettings, cameraSettings, hasActivePostFX,
                        useColorTexture, useDepthTexture,
                        applyMSAA, msaaType
                    );
                }
                else if(pipelineType == 1)
                {
                    PassesDeferred(
                        ref context, ref renderGraph, debugCulling,
                        camera, cullingResults, bufferSize, settings, bufferSettings,
                        postFXSettings, shadowSettings, cameraSettings, hasActivePostFX,
                        useColorTexture, useDepthTexture
                    );
                }
                else if(pipelineType == 2)
                {
                    PassesGPUDriven(
                        ref context, ref renderGraph, debugCulling,
                        camera, cullingResults, bufferSize, settings, bufferSettings,
                        postFXSettings, shadowSettings, cameraSettings, hasActivePostFX,
                        useColorTexture, useDepthTexture,
                        ref meshData
                    );
                }
            }
            renderGraph.EndRecordingAndExecute();

            context.ExecuteCommandBuffer(renderGraphParameters.commandBuffer);
            context.Submit();

            CommandBufferPool.Release(renderGraphParameters.commandBuffer);
        }

        void PassesForward(
            ref ScriptableRenderContext context, ref RenderGraph renderGraph, bool debugCulling,
            Camera camera, CullingResults cullingResults, Vector2Int bufferSize, 
            LynxRenderPipelineSettings settings, CameraBufferSettings bufferSettings,
            PostFXSettings postFXSettings, ShadowSettings shadowSettings, CameraSettings cameraSettings, 
            bool hasActivePostFX, bool useColorTexture, bool useDepthTexture, 
            bool applyMSAA, CameraBufferSettings.MSAA.Type msaaType
        )
        {
            LightResources lightResources = LightingPass.Record(
                renderGraph, cullingResults, bufferSize,
                settings.forwardPlus, shadowSettings, 
                cameraSettings.maskLights ? cameraSettings.newRenderingLayerMask : -1,
                context
            );

            MSAASamples msaaSamples = MSAASamples.None;
            if (applyMSAA && msaaType == CameraBufferSettings.MSAA.Type.Hardwware)
            {
                msaaSamples = bufferSettings.msaa.msaaSamples;
            }
            CameraRendererTextures textures = SetupPass.Record(
                renderGraph, 
                useColorTexture, useDepthTexture, 
                bufferSettings.allowHDR, bufferSize, camera,
                msaaSamples, (int)cameraSettings.cullSettings, debugCulling
            );

            MSAARenderTextures msaaTextures = default;
            if (applyMSAA && msaaType == CameraBufferSettings.MSAA.Type.Manual)
            {
                msaaTextures = MSAAStencilPass.Record(
                    renderGraph, camera, cullingResults, bufferSize,
                    cameraSettings.newRenderingLayerMask,
                    bufferSettings.msaa.msaaStencilShader, textures
                );
            }

            HiZData hiZData = new HiZData();
            bool hiZCull = (cameraSettings.cullSettings & CameraSettings.CullSettings.HiZ) == CameraSettings.CullSettings.HiZ;
            if (hiZCull || debugCulling)
            {
                PassesHiZSetup(ref renderGraph, ref textures, ref hiZData, bufferSize, settings);
            }

            GeometryPass.Record(
                renderGraph, camera, cullingResults,
                cameraSettings.newRenderingLayerMask, true,
                textures, lightResources
            );

            if (hiZCull || debugCulling)
            {
                HiZCopyPass.Record(
                    renderGraph, bufferSize, camera, debugCulling, material,
                    settings.csHiZShader, textures, hiZData, settings.debugHiZRT

                );
            }

            if (applyMSAA && msaaType == CameraBufferSettings.MSAA.Type.Manual)
            {
                MSAAFillPass.Record(
                    renderGraph, bufferSize, bufferSettings.msaa.msaaFillShader, msaaTextures, textures
                );

                MSAADownscalePass.Record(
                    renderGraph, bufferSettings.msaa.msaaDownscaleShader, bufferSize, msaaTextures, textures
                );
            }

            SkyboxPass.Record(renderGraph, camera, textures);

            var copier = new CameraRendererCopier(
                material, camera, cameraSettings.finalBlendMode
            );
            CopyAttachmentsPass.Record(
                renderGraph, useColorTexture, useDepthTexture, copier, textures
            );

            GeometryPass.Record(
                renderGraph, camera, cullingResults,
                cameraSettings.newRenderingLayerMask, false,
                textures, lightResources
            );

            PassesPostProcess(
                renderGraph, camera, cullingResults, bufferSize,
                settings, bufferSettings, postFXSettings, cameraSettings,
                hasActivePostFX,
                textures, copier, lightResources
            );
        }

        void PassesDeferred(
            ref ScriptableRenderContext context, ref RenderGraph renderGraph, bool debugCulling,
            Camera camera, CullingResults cullingResults, Vector2Int bufferSize,
            LynxRenderPipelineSettings settings, CameraBufferSettings bufferSettings,
            PostFXSettings postFXSettings, ShadowSettings shadowSettings, CameraSettings cameraSettings,
            bool hasActivePostFX, bool useColorTexture, bool useDepthTexture
        )
        {
            LightResources lightResources = LightingPass.Record(
                renderGraph, cullingResults, bufferSize,
                settings.forwardPlus, shadowSettings,
                cameraSettings.maskLights ? cameraSettings.newRenderingLayerMask : -1,
                context
            );

            MSAASamples msaaSamples = MSAASamples.None;
            CameraRendererTextures textures = SetupPass.Record(
                renderGraph, 
                useColorTexture, useDepthTexture, 
                bufferSettings.allowHDR, bufferSize, camera,
                msaaSamples, (int)cameraSettings.cullSettings, debugCulling
            );

            DeferredRenderTextures deferredTextures = SetupDeferredPass.Record(
                renderGraph, bufferSettings.allowHDR, bufferSize, camera, textures
            );

            HiZData hiZData = new HiZData();
            bool hiZCull = (cameraSettings.cullSettings & CameraSettings.CullSettings.HiZ) == CameraSettings.CullSettings.HiZ;
            if (hiZCull || debugCulling)
            {
                PassesHiZSetup(ref renderGraph, ref textures, ref hiZData, bufferSize, settings);
            }

            DeferredBufferPass.Record(
                renderGraph, camera, cullingResults,
                cameraSettings.newRenderingLayerMask, textures, deferredTextures, lightResources
            );

            if (hiZCull || debugCulling)
            {
                HiZCopyPass.Record(
                    renderGraph, bufferSize, camera, debugCulling, material,
                    settings.csHiZShader, textures, hiZData, settings.debugHiZRT

                );
            }

            DeferredBufferLightingPass.Record(
                renderGraph, settings.deferredShader, textures, deferredTextures
            );

            DeferredBufferCopyPass.Record(
                renderGraph, textures, deferredTextures, material
            );

            SkyboxPass.Record(renderGraph, camera, textures);

            var copier = new CameraRendererCopier(
                material, camera, cameraSettings.finalBlendMode
            );
            CopyAttachmentsPass.Record(
                renderGraph, useColorTexture, useDepthTexture, copier, textures
            );

            GeometryPass.Record(
                renderGraph, camera, cullingResults,
                cameraSettings.newRenderingLayerMask, false,
                textures, lightResources
            );

            PassesPostProcess(
                renderGraph, camera, cullingResults, bufferSize,
                settings, bufferSettings, postFXSettings, cameraSettings,
                hasActivePostFX, 
                textures, copier, lightResources
            );
        }
        
        void PassesGPUDriven(
            ref ScriptableRenderContext context, ref RenderGraph renderGraph, bool debugCulling,
            Camera camera, CullingResults cullingResults, Vector2Int bufferSize,
            LynxRenderPipelineSettings settings, CameraBufferSettings bufferSettings,
            PostFXSettings postFXSettings, ShadowSettings shadowSettings, CameraSettings cameraSettings,
            bool hasActivePostFX, bool useColorTexture, bool useDepthTexture,
            ref InterFrameData.MeshJobsData meshData
        )
        {

            LightResources lightResources = LightingPass.Record(
                renderGraph, cullingResults, bufferSize,
                settings.forwardPlus, shadowSettings,
                cameraSettings.maskLights ? cameraSettings.newRenderingLayerMask : -1,
                context
            );

            MSAASamples msaaSamples = MSAASamples.None;
            CameraRendererTextures textures = SetupPass.Record(
                renderGraph,
                useColorTexture, useDepthTexture,
                bufferSettings.allowHDR, bufferSize, camera,
                msaaSamples, (int)cameraSettings.cullSettings, debugCulling
            );

            HiZData hiZData = new HiZData();
            bool hiZCull = (cameraSettings.cullSettings & CameraSettings.CullSettings.HiZ) == CameraSettings.CullSettings.HiZ;
            if (hiZCull || debugCulling)
            {
                PassesHiZSetup(ref renderGraph, ref textures, ref hiZData, bufferSize, settings);
            }

            CullPass.Record(
                renderGraph, bufferSize, camera,
                cameraSettings, hiZData, textures,
                settings.csCullShader, settings.csCompactShader, settings.csTransformPositionShader,
                settings.cullShader,
                ref meshData
            );

            // GeometryPass.Record(
            //     renderGraph, camera, cullingResults,
            //     cameraSettings.newRenderingLayerMask, true,
            //     textures, lightResources
            // );

            if (hiZCull || debugCulling)
            {
                HiZCopyPass.Record(
                    renderGraph, bufferSize, camera, debugCulling, material, 
                    settings.csHiZShader, textures, hiZData, settings.debugHiZRT
                    
                );
            }

            // SkyboxPass.Record(renderGraph, camera, textures);

            var copier = new CameraRendererCopier(
                material, camera, cameraSettings.finalBlendMode
            );
            
            PassesPostProcess(
                renderGraph, camera, cullingResults, bufferSize,
                settings, bufferSettings, postFXSettings, cameraSettings,
                hasActivePostFX,
                textures, copier, lightResources
            );
        }

        void PassesHiZSetup(
            ref RenderGraph renderGraph, ref CameraRendererTextures textures, ref HiZData hiZData,
            Vector2Int bufferSize, LynxRenderPipelineSettings settings
        )
        {
            hiZData = SetupHiZPass.Record(
                renderGraph, bufferSize, textures
            );
            
            HiZReprojectPass.Record(
                renderGraph, bufferSize, 
                settings.csHiZShader, hiZData
            );

            hiZData = HiZPass.Record(
                renderGraph, bufferSize, 
                settings.hiZShader, settings.csHiZShader, 
                hiZData, textures
            );
        }

        void PassesPostProcess(
            RenderGraph renderGraph, Camera camera, CullingResults cullingResults,
            Vector2Int bufferSize, 
            LynxRenderPipelineSettings settings, CameraBufferSettings bufferSettings,
            PostFXSettings postFXSettings, CameraSettings cameraSettings,
            bool hasActivePostFX, 
            CameraRendererTextures textures, CameraRendererCopier copier,
            LightResources lightResources
        )
        {
            UnsupportedShadersPass.Record(renderGraph, camera, cullingResults);

            if (hasActivePostFX)
            {
                postFXStack.BufferSettings = bufferSettings;
                postFXStack.BufferSize = bufferSize;
                postFXStack.Camera = camera;
                postFXStack.FinalBlendMode = cameraSettings.finalBlendMode;
                postFXStack.Settings = postFXSettings;
                PostFXPass.Record(
                    renderGraph, postFXStack, (int)settings.colorLUTResolution,
                    cameraSettings.keepAlpha, textures
                );
            }
            else
            {
                FinalPass.Record(renderGraph, copier, textures);
            }

            DebugPass.Record(renderGraph, camera, lightResources, textures);

            GizmosPass.Record(renderGraph, copier, textures);
        }
    }
}

