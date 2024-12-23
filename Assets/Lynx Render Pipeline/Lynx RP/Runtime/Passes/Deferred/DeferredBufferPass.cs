using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

namespace LynxRP
{
    public class DeferredBufferPass
    {
        static readonly ProfilingSampler sampler = new("Deferred Buffer");

        static readonly int 
            albedoDebugID = Shader.PropertyToID("_DebugAlbedoBuffer");

        static ShaderTagId[] shaderTagId =
        {
            new ShaderTagId("CustomLitDeferred"),
            new ShaderTagId("CustomUnlitDeferred")
        };

        RendererListHandle list;

        TextureHandle 
            positionBuffer, 
            normalBuffer, normalInterpolatedBuffer, 
            ormBuffer, lightingBuffer, extrasBuffer;

        TextureHandle albedoBuffer, depthBuffer;

        RenderTargetIdentifier[] bufferColorIDs = new RenderTargetIdentifier[7];

        void Render(RenderGraphContext context)
        {
            CommandBuffer buffer = context.cmd;

            bufferColorIDs[0] = albedoBuffer;
            bufferColorIDs[1] = positionBuffer;
            bufferColorIDs[2] = normalBuffer;
            bufferColorIDs[3] = normalInterpolatedBuffer;
            bufferColorIDs[4] = ormBuffer;
            bufferColorIDs[5] = lightingBuffer;
            bufferColorIDs[6] = extrasBuffer;

            buffer.SetRenderTarget(
                bufferColorIDs, depthBuffer
            );

            buffer.DrawRendererList(list);

            context.renderContext.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        public static void Record(
            RenderGraph renderGraph,
            Camera camera,
            CullingResults cullingResults,
            int renderingLayerMask,
            in CameraRendererTextures textures,
            in DeferredRenderTextures deferredTextures,
            in LightResources lightData
        )
        {
            using RenderGraphBuilder builder =
                renderGraph.AddRenderPass(sampler.name, out DeferredBufferPass pass, sampler);

            pass.list = builder.UseRendererList(renderGraph.CreateRendererList(
                new RendererListDesc(shaderTagId, cullingResults, camera)
                {
                    sortingCriteria = SortingCriteria.CommonOpaque,
                    rendererConfiguration =
                        PerObjectData.ReflectionProbes |
                        PerObjectData.Lightmaps |
                        PerObjectData.ShadowMask |
                        PerObjectData.LightProbe |
                        PerObjectData.OcclusionProbe |
                        PerObjectData.LightProbeProxyVolume |
                        PerObjectData.OcclusionProbeProxyVolume |
                        PerObjectData.LightData | PerObjectData.LightIndices,
                    renderQueueRange = RenderQueueRange.opaque,
                    renderingLayerMask = (uint)renderingLayerMask
                }
            ));

            pass.positionBuffer = builder.WriteTexture(deferredTextures.positionBuffer);
            pass.normalBuffer = builder.WriteTexture(deferredTextures.normalBuffer);
            pass.normalInterpolatedBuffer = builder.WriteTexture(deferredTextures.normalInterpolatedBuffer);
            pass.ormBuffer = builder.WriteTexture(deferredTextures.ormBuffer);
            pass.extrasBuffer = builder.WriteTexture(deferredTextures.extrasBuffer);
            pass.lightingBuffer = builder.WriteTexture(deferredTextures.lightingBuffer);
            
            pass.albedoBuffer = builder.WriteTexture(textures.colorAttachment);
            pass.depthBuffer = builder.WriteTexture(textures.depthAttachment);

            builder.ReadBuffer(lightData.directionalLightDataBuffer);
            builder.ReadBuffer(lightData.otherLightDataBuffer);
            // if (lightData.tilesBuffer.IsValid())
            {
                builder.ReadBuffer(lightData.tilesBuffer);
            }
            builder.ReadTexture(lightData.shadowResources.directionalAtlas);
            builder.ReadTexture(lightData.shadowResources.otherAtlas);
            builder.ReadBuffer(
                lightData.shadowResources.directionalShadowCascadesBuffer
            );
            builder.ReadBuffer(
                lightData.shadowResources.directionalShadowMatricesBuffer
            );
            builder.ReadBuffer(
                lightData.shadowResources.otherShadowDataBuffer
            );

            builder.AllowPassCulling(false);
            builder.SetRenderFunc<DeferredBufferPass>(
                static (pass, context) => pass.Render(context)
            );
        }
    }
}
