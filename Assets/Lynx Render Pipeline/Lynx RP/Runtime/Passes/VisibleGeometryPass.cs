
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace LynxRP
{
    public class VisibleGeometryPass
    {
        // static readonly ProfilingSampler sampler = new("Visible Geometry");
        
        // CameraRenderer renderer;

        // bool useDynamicBatching, useGPUInstancing;

        // int renderingLayerMask;

        // void Render(RenderGraphContext context) => renderer.DrawVisibleGeometry(
        //     useDynamicBatching, useGPUInstancing, renderingLayerMask
        // );

        // public static void Record(
        //     RenderGraph renderGraph, CameraRenderer renderer,
        //     bool useDynamicBatching, bool useGPUInstancing,
        //     int renderingLayerMask
        // )
        // {
        //     using RenderGraphBuilder builder =
        //         renderGraph.AddRenderPass(sampler.name, out VisibleGeometryPass pass, sampler);
        //     pass.renderer = renderer;
        //     pass.useDynamicBatching = useDynamicBatching;
        //     pass.useGPUInstancing = useGPUInstancing;
        //     pass.renderingLayerMask = renderingLayerMask;
        //     builder.SetRenderFunc<VisibleGeometryPass>(
        //         (pass, context) => pass.Render(context)
        //     );
        // }
    }
}
