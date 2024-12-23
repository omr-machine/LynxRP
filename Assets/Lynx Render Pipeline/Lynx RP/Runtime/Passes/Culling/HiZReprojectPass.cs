using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace LynxRP
{
    public class HiZReprojectPass
    {
        static readonly ProfilingSampler sampler = new("Hierarchical Z Reproject");

        private static readonly int
            prevFrameDataID = Shader.PropertyToID("_FrameData"),
            prevFrameDepthID = Shader.PropertyToID("_PrevFrameDepth"),
            prevFrameDepthParamsID = Shader.PropertyToID("_PrevFrameDepthParams"),
            projectedDepthID = Shader.PropertyToID("_ProjectedDepthTexture"),
            pointBufferID = Shader.PropertyToID("_HiZReprojectionPointBuffer");

        private static readonly int
            debugDepthPrevID = Shader.PropertyToID("_DebugHiZDepthPrevFrame");

        Vector2Int attachmentSize;
        
        ComputeShader csHiZShader;
        
        TextureHandle hiZDepthRT, hiZInterFrameRT, hiZPrevFrameDataRT;
        
        BufferHandle pointBuffer;
        
        const uint numThreadsMax = 16;

        void Render(RenderGraphContext context)
        {
            CommandBuffer buffer = context.cmd;
            
            buffer.SetGlobalTexture(debugDepthPrevID, hiZInterFrameRT);
            
            Vector4 mipParams = new(
                attachmentSize.x, attachmentSize.y,
                1.0f / attachmentSize.x, 1.0f / attachmentSize.y
            );

            buffer.SetComputeVectorParam(csHiZShader, prevFrameDepthParamsID, mipParams);
            buffer.SetComputeTextureParam(csHiZShader, 2, prevFrameDataID, hiZPrevFrameDataRT);
            buffer.SetComputeTextureParam(csHiZShader, 2, prevFrameDepthID, hiZInterFrameRT);
            buffer.SetComputeTextureParam(csHiZShader, 2, projectedDepthID, hiZDepthRT, 0);
            buffer.SetComputeBufferParam(csHiZShader, 2,pointBufferID, pointBuffer);
            
            int workGroupsX = Mathf.CeilToInt(mipParams.x / numThreadsMax);
            int workGroupsY = Mathf.CeilToInt(mipParams.y / numThreadsMax);
            
            buffer.DispatchCompute(csHiZShader, 2, workGroupsX, workGroupsY, 1);
            
            context.renderContext.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        public static void Record(
            RenderGraph renderGraph,
            Vector2Int attachmentSize,
            ComputeShader csHiZShader,
            in HiZData hiZData
        )
        {
            using RenderGraphBuilder builder =
                renderGraph.AddRenderPass(sampler.name, out HiZReprojectPass pass, sampler);
            
            pass.csHiZShader = csHiZShader;
            pass.attachmentSize = attachmentSize;

            pass.hiZDepthRT = builder.WriteTexture(hiZData.hiZDepthRT);
            pass.hiZInterFrameRT = builder.WriteTexture(hiZData.hiZInterFrameRT);
            pass.hiZPrevFrameDataRT = builder.WriteTexture(hiZData.hiZPrevFrameDataRT);
            
            pass.pointBuffer = builder.WriteBuffer(hiZData.pointBuffer);

            builder.AllowPassCulling(false);
            builder.SetRenderFunc<HiZReprojectPass>(
                static (pass, context) => pass.Render(context)
            );
        }
    }
}
