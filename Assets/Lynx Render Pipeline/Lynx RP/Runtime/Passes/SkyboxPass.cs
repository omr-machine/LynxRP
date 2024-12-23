using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace LynxRP
{
    public class SkyboxPass
    {
        static readonly ProfilingSampler sampler = new("Skybox");

        RendererListHandle list;

        void Render(RenderGraphContext context)
        {
            context.cmd.DrawRendererList(list);
            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();   
        }

        public static void Record(
            RenderGraph renderGraph, 
            Camera camera,
            in CameraRendererTextures textures
        )
        {
            if (camera.clearFlags == CameraClearFlags.Skybox)
            {
                using RenderGraphBuilder builder = renderGraph.AddRenderPass(
                    sampler.name, out SkyboxPass pass, sampler
                );

                pass.list = builder.UseRendererList(
                    renderGraph.CreateSkyboxRendererList(camera)
                );
                builder.ReadWriteTexture(textures.colorAttachment);
                builder.ReadTexture(textures.depthAttachment);

                builder.AllowPassCulling(false);
                builder.SetRenderFunc<SkyboxPass>(
                    static (pass, context) => pass.Render(context)
                );
            }
        }
    }
}
