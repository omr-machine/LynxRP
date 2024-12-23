using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace LynxRP
{
    public class DeferredBufferCopyPass
    {
        static readonly ProfilingSampler sampler = new("Deferred Buffer Copy");

        static readonly int sourceID = Shader.PropertyToID("_SourceTexture");
        
        static readonly int copyDebugID = Shader.PropertyToID("_DebugAlbedoBuffer");

        TextureHandle colorAttachment, depthAttachment;

        TextureHandle lightingBuffer;

        Material copyMaterial;

        void Render(RenderGraphContext context)
        {
            CommandBuffer buffer = context.cmd;

            buffer.SetGlobalTexture(copyDebugID, lightingBuffer);
            // buffer.SetGlobalTexture(copyDebugID, depthBuffer);

            buffer.SetGlobalTexture(sourceID, lightingBuffer);
            buffer.SetRenderTarget(
                colorAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                depthAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
            buffer.DrawProcedural(
                Matrix4x4.identity, copyMaterial, 0, MeshTopology.Triangles, 3
            );

            context.renderContext.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        public static void Record(
            RenderGraph renderGraph,
            in CameraRendererTextures textures,
            in DeferredRenderTextures deferredTextures,
            Material copyMaterial
        )
        {
            using RenderGraphBuilder builder =
                renderGraph.AddRenderPass(sampler.name, out DeferredBufferCopyPass pass, sampler);

            pass.copyMaterial = copyMaterial;

            pass.colorAttachment = builder.WriteTexture(textures.colorAttachment);
            pass.depthAttachment = builder.ReadTexture(textures.depthAttachment);

            pass.lightingBuffer = builder.ReadTexture(deferredTextures.lightingBuffer);

            builder.AllowPassCulling(false);
            builder.SetRenderFunc<DeferredBufferCopyPass>(
                static (pass, context) => pass.Render(context)
            );
        }
    }
}
