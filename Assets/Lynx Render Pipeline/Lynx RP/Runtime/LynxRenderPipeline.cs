using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace LynxRP
{
    public partial class LynxRenderPipeline : RenderPipeline
    {
        readonly CameraRenderer renderer;

        readonly LynxRenderPipelineSettings settings;

        readonly RenderGraph renderGraph = new("Custom SRP Render Graph");

        public LynxRenderPipeline(LynxRenderPipelineSettings settings)
        {
            this.settings = settings;
            GraphicsSettings.useScriptableRenderPipelineBatching = settings.useSRPBatcher;
            GraphicsSettings.lightsUseLinearIntensity = true;

            InitializeForEditor();
            renderer = new CameraRenderer(settings.cameraRendererShader, settings.cameraDebuggerShader);
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras) { }

        protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
        {
            for (int i = 0; i < cameras.Count; i++)
            {
                renderer.Render(renderGraph, context, cameras[i], settings);
            }
            renderGraph.EndFrame();
        }

        protected override void Dispose (bool disposing)
        {
            base.Dispose(disposing);
            DisposeForEditor();
            renderer.Dispose();
        }
    }
}