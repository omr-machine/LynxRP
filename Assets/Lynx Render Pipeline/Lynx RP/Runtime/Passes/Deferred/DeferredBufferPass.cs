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
            new("CustomLitDeferred"),
            new("CustomUnlitDeferred")
        };

        RendererListHandle list;

        TextureHandle 
            positionBuffer, 
            normalBuffer, normalInterpolatedBuffer, 
            ormBuffer, lightingBuffer, extrasBuffer;

        TextureHandle albedoBuffer, depthBuffer;

        RenderTargetIdentifier[] bufferColorIDs = new RenderTargetIdentifier[7];

        void Render(UnsafeGraphContext context)
        {
            UnsafeCommandBuffer buffer = context.cmd;

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
            using IUnsafeRenderGraphBuilder builder =
                renderGraph.AddUnsafePass(sampler.name, out DeferredBufferPass pass, sampler);

            pass.list = renderGraph.CreateRendererList(
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
            );
            builder.UseRendererList(pass.list);

            pass.positionBuffer = deferredTextures.positionBuffer;
            pass.normalBuffer = deferredTextures.normalBuffer;
            pass.normalInterpolatedBuffer = deferredTextures.normalInterpolatedBuffer;
            pass.ormBuffer = deferredTextures.ormBuffer;
            pass.extrasBuffer = deferredTextures.extrasBuffer;
            pass.lightingBuffer = deferredTextures.lightingBuffer ;
            builder.UseTexture(pass.positionBuffer, AccessFlags.WriteAll);
            builder.UseTexture(pass.normalBuffer, AccessFlags.WriteAll);
            builder.UseTexture(pass.normalInterpolatedBuffer, AccessFlags.WriteAll);
            builder.UseTexture(pass.ormBuffer, AccessFlags.WriteAll);
            builder.UseTexture(pass.extrasBuffer, AccessFlags.WriteAll);
            builder.UseTexture(pass.lightingBuffer, AccessFlags.WriteAll);
            
            pass.albedoBuffer = textures.colorAttachment;
            pass.depthBuffer = textures.depthAttachment;
            builder.UseTexture(pass.albedoBuffer, AccessFlags.WriteAll);
            builder.UseTexture(pass.depthBuffer, AccessFlags.WriteAll);

            builder.UseBuffer(lightData.directionalLightDataBuffer);
            builder.UseBuffer(lightData.otherLightDataBuffer);
            // if (lightData.tilesBuffer.IsValid())
            {
                builder.UseBuffer(lightData.tilesBuffer);
            }
            builder.UseTexture(lightData.shadowResources.directionalAtlas);
            builder.UseTexture(lightData.shadowResources.otherAtlas);
            builder.UseBuffer(
                lightData.shadowResources.directionalShadowCascadesBuffer
            );
            builder.UseBuffer(
                lightData.shadowResources.directionalShadowMatricesBuffer
            );
            builder.UseBuffer(
                lightData.shadowResources.otherShadowDataBuffer
            );

            builder.AllowPassCulling(false);
            builder.SetRenderFunc<DeferredBufferPass>(
                static (pass, context) => pass.Render(context)
            );
        }
    }
}
