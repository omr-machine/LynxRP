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

        InterFrameData interFrameData;

        readonly RenderGraph renderGraph = new("Custom SRP Render Graph");

        public LynxRenderPipeline(LynxRenderPipelineSettings settings)
        {
            this.settings = settings;
            GraphicsSettings.useScriptableRenderPipelineBatching = settings.useSRPBatcher;
            GraphicsSettings.lightsUseLinearIntensity = true;

            InitializeForEditor();
            renderer = new CameraRenderer(settings.cameraRendererShader, settings.cameraDebuggerShader);

            interFrameData = new InterFrameData();
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras) { }

        protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (settings.pipelineType == LynxRenderPipelineSettings.PipelineType.GPUDriven)
            {
                interFrameData.UpdateVertexBuffer();
                interFrameData.DebugInstanceIDs();
                // interFrameData.DebugInstanceMatrices();
                interFrameData.DebugFinalList();
                interFrameData.DebugFinalMatrices();

                // interFrameData.JobsMeshes();
                // interFrameData.meshData.handle.Complete();
            }

            for (int i = 0; i < cameras.Count; i++)
            {
                renderer.Render(renderGraph, context, cameras[i], settings, ref interFrameData.meshData);
            }
            renderGraph.EndFrame();

            // interFrameData.JobsMeshesDispose();
        }

        protected override void Dispose (bool disposing)
        {
            base.Dispose(disposing);
            DisposeForEditor();
            renderer.Dispose();

            if (settings.pipelineType == LynxRenderPipelineSettings.PipelineType.GPUDriven)
            {
                interFrameData.Dispose(); 
            }
        }
    }
}