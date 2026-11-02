using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace LynxRP
{
    public class DebugPass
    {
        static readonly ProfilingSampler sampler = new("Debug");

        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        public static void Record(
            RenderGraph renderGraph,
            Camera camera,
            in LightResources lightData,
            in CameraRendererTextures textures
        )
        {
            if (
                CameraDebugger.IsActive &&
                camera.cameraType <= CameraType.SceneView
            )
            {
                using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(
                    sampler.name, out DebugPass pass, sampler
                );
                builder.UseBuffer(lightData.tilesBuffer);

                CameraDebugger.depthBuffer = textures.depthAttachment;
                CameraDebugger.colorBuffer = textures.colorAttachment;
                
                builder.UseTexture(CameraDebugger.depthBuffer);
                builder.UseTexture(CameraDebugger.colorBuffer);
                
                builder.SetRenderFunc<DebugPass>(
                    static (pass, context) => CameraDebugger.Render(context)
                );
            }
        }
    }
}
