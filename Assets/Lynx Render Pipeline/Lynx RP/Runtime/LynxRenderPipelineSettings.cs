using UnityEngine;

namespace LynxRP
{
    [System.Serializable]
    public class LynxRenderPipelineSettings 
    {
        public enum PipelineType { Forward = 0, Deferred = 1, GPUDriven = 2 }

        public PipelineType pipelineType = PipelineType.Forward;

        public CameraBufferSettings cameraBuffer = new()
        {
            allowHDR = true,
            renderScale = 1f,
            fxaa = new()
            {
                fixedThreshold = 0.0833f,
                relativeThreshold = 0.166f,
                subpixelBlending = 0.75f
            }
        };

        public bool useSRPBatcher = true;

        public ForwardPlusSettings forwardPlus;

        public ShadowSettings shadows;

        public PostFXSettings postFXSettings;

        public enum ColorLUTResolution { _16 = 16, _32 = 32, _64 = 64 }

        public ColorLUTResolution colorLUTResolution = ColorLUTResolution._32;
        
        public Shader cameraRendererShader, cameraDebuggerShader;

        public Shader deferredShader;
        
        public ComputeShader csCullShader, csCompactShader, csHiZShader;
        public Shader cullShader, hiZShader;
        
        public RenderTexture debugHiZRT;
    }
}
