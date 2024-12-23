using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace LynxRP
{
    [CreateAssetMenu(menuName = "Rendering/Lynx Render Pipeline")]
    public partial class LynxRenderPipelineAsset : RenderPipelineAsset
    {
        [SerializeField]
        LynxRenderPipelineSettings settings;

        public override Type pipelineType => typeof(LynxRenderPipeline);

        public override string renderPipelineShaderTag => string.Empty;

        protected override RenderPipeline CreatePipeline() =>
            new LynxRenderPipeline(settings);
    }
}

