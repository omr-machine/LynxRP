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

        void Render(UnsafeGraphContext context)
        {
            UnsafeCommandBuffer buffer = context.cmd;

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
        }

        public static void Record(
            RenderGraph renderGraph,
            in CameraRendererTextures textures,
            in DeferredRenderTextures deferredTextures,
            Material copyMaterial
        )
        {
            using IUnsafeRenderGraphBuilder builder =
                renderGraph.AddUnsafePass(sampler.name, out DeferredBufferCopyPass pass, sampler);

            pass.copyMaterial = copyMaterial;

            pass.colorAttachment = textures.colorAttachment;
            pass.depthAttachment = textures.depthAttachment;
            builder.UseTexture(pass.colorAttachment, AccessFlags.WriteAll);
            builder.UseTexture(pass.depthAttachment);

            pass.lightingBuffer = deferredTextures.lightingBuffer;
            builder.UseTexture(pass.lightingBuffer);

            builder.AllowPassCulling(false);
            builder.SetRenderFunc<DeferredBufferCopyPass>(
                static (pass, context) => pass.Render(context)
            );
        }
    }
}
