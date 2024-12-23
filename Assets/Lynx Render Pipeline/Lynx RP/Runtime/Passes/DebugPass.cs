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
                using RenderGraphBuilder builder = renderGraph.AddRenderPass(
                    sampler.name, out DebugPass pass, sampler
                );
                builder.ReadBuffer(lightData.tilesBuffer);

                CameraDebugger.colorBuffer = builder.ReadTexture(textures.colorAttachment);
                CameraDebugger.depthBuffer = builder.ReadTexture(textures.depthAttachment);
                
                builder.SetRenderFunc<DebugPass>(
                    static (pass, context) => CameraDebugger.Render(context)
                );
            }
        }
    }
}
