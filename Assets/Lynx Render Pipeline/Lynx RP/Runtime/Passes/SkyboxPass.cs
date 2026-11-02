using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace LynxRP
{
    public class SkyboxPass
    {
        static readonly ProfilingSampler sampler = new("Skybox");

        RendererListHandle list;

        void Render(UnsafeGraphContext context)
        {
            context.cmd.DrawRendererList(list);
        }

        public static void Record(
            RenderGraph renderGraph, 
            Camera camera,
            in CameraRendererTextures textures
        )
        {
            if (camera.clearFlags == CameraClearFlags.Skybox)
            {
                using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(
                    sampler.name, out SkyboxPass pass, sampler
                );

                pass.list = renderGraph.CreateSkyboxRendererList(camera);
                builder.UseRendererList(pass.list);
                
                builder.UseTexture(textures.colorAttachment, AccessFlags.ReadWrite);
                builder.UseTexture(textures.depthAttachment);

                builder.AllowPassCulling(false);
                builder.SetRenderFunc<SkyboxPass>(
                    static (pass, context) => pass.Render(context)
                );
            }
        }
    }
}
