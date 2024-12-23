using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace LynxRP
{
    public class HiZPass
    {
        static readonly ProfilingSampler sampler = new("Hierarchical Z");

        static readonly int depthID = Shader.PropertyToID("_HiZDepth");

        private static readonly int
            sourceMipID = Shader.PropertyToID("_SourceMip"),
            destMipID = Shader.PropertyToID("_DestMip"),
            sourceMipParamsID = Shader.PropertyToID("_SourceMipParams"),
            destMipParamsID = Shader.PropertyToID("_DestMipParams"),
            mipLevelID = Shader.PropertyToID("_MipLevel");

        private static readonly int
            mipLevelMaxId = Shader.PropertyToID("_MipLevelMax");


        private static readonly int
            debugDepthProjectedID = Shader.PropertyToID("_DebugHiZDepthProjected");

        TextureHandle colorAttachment, depthAttachment;
        TextureHandle hiZDepthRT;
        
        ComputeShader csHiZShader;

        Material hiZMaterial;
        
        Vector2Int attachmentSize;

        int mipLevelMax;

        const uint numThreadsMax = 16;

        void Render(RenderGraphContext context)
        {
            CommandBuffer buffer = context.cmd;
            
            // buffer.SetGlobalTexture(depthID, depthAttachment);
            buffer.SetGlobalTexture(debugDepthProjectedID, hiZDepthRT);

            buffer.SetRenderTarget(hiZDepthRT, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);

            // buffer.DrawProcedural(
            //     Matrix4x4.identity, hiZMaterial, 0, MeshTopology.Triangles, 3
            // );
            
            Vector4 mipParams = new(
                attachmentSize.x, attachmentSize.y,
                1.0f / attachmentSize.x, 1.0f / attachmentSize.y
            );

            mipLevelMax = (int)math.floor(math.max(math.log2(attachmentSize.x), math.log2(attachmentSize.y)));
            for(int mipLevel = 0; mipLevel < mipLevelMax;)
            {
                buffer.SetComputeVectorParam(csHiZShader, sourceMipParamsID, mipParams);
                buffer.SetComputeTextureParam(csHiZShader, 0, sourceMipID, hiZDepthRT, mipLevel);
                
                mipParams.x = Mathf.FloorToInt(mipParams.x / 2.0f); 
                mipParams.y = Mathf.FloorToInt(mipParams.y / 2.0f);
                
                mipParams.x = Mathf.Max(mipParams.x, 1);
                mipParams.y = Mathf.Max(mipParams.y, 1);
                
                mipParams.z = 1.0f / mipParams.x; 
                mipParams.w = 1.0f / mipParams.y;
                
                // Debug.Log("mipx " + mipParams.x + " " + mipParams.y);
                // Debug.Log("mipz " + mipParams.z + " " + mipParams.w);
                buffer.SetComputeVectorParam(csHiZShader, destMipParamsID, mipParams);
                
                mipLevel++;
                buffer.SetComputeTextureParam(csHiZShader, 0, destMipID, hiZDepthRT, mipLevel);
                buffer.SetComputeIntParam(csHiZShader, mipLevelID, mipLevel);
                
                int workGroupsX = Mathf.CeilToInt(mipParams.x / numThreadsMax);
                int workGroupsY = Mathf.CeilToInt(mipParams.y / numThreadsMax);
                buffer.DispatchCompute(csHiZShader, 0, workGroupsX, workGroupsY, 1);
            }

            buffer.SetGlobalInt(mipLevelMaxId, mipLevelMax);

            buffer.SetRenderTarget(
                colorAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                depthAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );

            context.renderContext.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        public static HiZData Record(
            RenderGraph renderGraph,
            Vector2Int attachmentSize,
            Shader hiZShader,
            ComputeShader csHiZShader,
            in HiZData hiZData,
            in CameraRendererTextures textures
        )
        {
            using RenderGraphBuilder builder =
                renderGraph.AddRenderPass(sampler.name, out HiZPass pass, sampler);

            pass.attachmentSize = attachmentSize;
            
            if (hiZShader != null)
            {
                pass.hiZMaterial = new Material(hiZShader);
                pass.hiZMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            
            pass.csHiZShader = csHiZShader;
            pass.hiZDepthRT = builder.ReadWriteTexture(hiZData.hiZDepthRT);
            
            pass.depthAttachment = builder.ReadTexture(textures.depthAttachment);
            pass.colorAttachment = builder.ReadTexture(textures.colorAttachment);

            builder.AllowPassCulling(false);
            builder.SetRenderFunc<HiZPass>(
                static (pass, context) => pass.Render(context)
            );
            
            return new HiZData(hiZData.hiZDepthRT, pass.mipLevelMax, hiZData.hiZInterFrameRT, hiZData.hiZPrevFrameDataRT, hiZData.pointBuffer);
        }
    }
}
