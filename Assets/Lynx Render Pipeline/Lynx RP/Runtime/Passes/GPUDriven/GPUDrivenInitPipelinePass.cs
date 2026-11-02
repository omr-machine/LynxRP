using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace LynxRP
{
    using static MeshDefinitions;
    public class GPUDrivenInitPipelinePass
    {
        static readonly ProfilingSampler samplerCull = new("Pipeline Init State Pass");
        
        private static readonly int
            indexSizeId = Shader.PropertyToID("_IndexSize");
        
        private static readonly int 
            offsetSizesBufferId = Shader.PropertyToID("_OffsetSizesBuffer"),
            matricesBufferId = Shader.PropertyToID("_MatricesBuffer"),
            aabbBufferId = Shader.PropertyToID("_AABBBuffer"); 

        private static readonly int
            indexBufferId = Shader.PropertyToID("_IndexBuffer"),
            voteBufferId = Shader.PropertyToID("_VoteBuffer");
        
        bool skipPass;

        int indexCount, triCount, triCountPadded;
        const int numThreadsXMax = 256;
        const int numThreadsXMaxGroups = 32;
        
        InterFrameData.MeshJobsData meshData;
        
        ComputeShader csTransformPositionShader;

        BufferHandle indexBuffer, voteBuffer;
        
        BufferHandle offsetSizesBuffer, matricesBuffer, aabbBuffer;
        
        void Render(UnsafeGraphContext context)
        {
            UnsafeCommandBuffer buffer = context.cmd;

            if (skipPass)
                return;
            
            int matricesCount = meshData.objCount;

            buffer.SetBufferData(
                indexBuffer, meshData.meshBufferDefault, 0, 0, indexCount
            );
            buffer.SetBufferData(
                offsetSizesBuffer, meshData.finalOffsetSizes, 0, 0, matricesCount * 2
            );
            buffer.SetBufferData(
                matricesBuffer, meshData.finalMatrices, 0, 0, matricesCount
            );
            buffer.SetBufferData(
                aabbBuffer, meshData.BBs, 0, 0, matricesCount
            );

            int voteGroups = Mathf.CeilToInt(matricesCount / (float)numThreadsXMax);
            buffer.SetComputeIntParam(csTransformPositionShader, indexSizeId, matricesCount);
            
            buffer.SetComputeBufferParam(csTransformPositionShader, 0, indexBufferId, indexBuffer);
            buffer.SetComputeBufferParam(csTransformPositionShader, 0, offsetSizesBufferId, offsetSizesBuffer);
            buffer.SetComputeBufferParam(csTransformPositionShader, 0, matricesBufferId, matricesBuffer);
            buffer.DispatchCompute(csTransformPositionShader, 0, voteGroups, 1, 1);

            buffer.SetComputeBufferParam(csTransformPositionShader, 1, indexBufferId, indexBuffer);
            buffer.SetComputeBufferParam(csTransformPositionShader, 1, voteBufferId, voteBuffer);
            buffer.SetComputeBufferParam(csTransformPositionShader, 1, offsetSizesBufferId, offsetSizesBuffer);
            buffer.SetComputeBufferParam(csTransformPositionShader, 1, aabbBufferId, aabbBuffer);
            buffer.DispatchCompute(csTransformPositionShader, 1, voteGroups, 1, 1);
        }

        public static void Record(
            RenderGraph renderGraph,
            in CameraRendererTextures textures,
            ComputeShader csTransformPositionShader,
            ref InterFrameData.MeshJobsData meshData,
            in GPUDrivenData gpuDrivenData
        )
        {
            ProfilingSampler sampler = samplerCull;

            using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(
                sampler.name, out GPUDrivenInitPipelinePass pass, sampler
            );
            
            pass.skipPass = false;

            pass.indexCount = meshData.indexCount;
            pass.triCount = meshData.triCount;
            if (pass.indexCount == 0 || pass.triCount == 0)
            {
                pass.indexCount = 3;
                pass.triCount = 1;
                pass.skipPass = true;
            }
            pass.meshData = meshData;

            pass.triCountPadded = (int)NextPowerOfTwo((uint)pass.triCount);
            
            pass.csTransformPositionShader = csTransformPositionShader;

            pass.indexBuffer = gpuDrivenData.indexBuffer;
            pass.voteBuffer = gpuDrivenData.voteBuffer;
            builder.UseBuffer(pass.indexBuffer, AccessFlags.WriteAll);
            builder.UseBuffer(pass.voteBuffer, AccessFlags.WriteAll);

            pass.offsetSizesBuffer = gpuDrivenData.offsetSizesBuffer;
            pass.matricesBuffer = gpuDrivenData.matricesBuffer;
            pass.aabbBuffer = gpuDrivenData.aabbBuffer;
            builder.UseBuffer(pass.offsetSizesBuffer, AccessFlags.WriteAll);
            builder.UseBuffer(pass.matricesBuffer, AccessFlags.WriteAll);
            builder.UseBuffer(pass.aabbBuffer, AccessFlags.WriteAll);
            
            builder.UseTexture(textures.colorAttachment, AccessFlags.ReadWrite);
            builder.UseTexture(textures.depthAttachment, AccessFlags.ReadWrite);

            builder.SetRenderFunc<GPUDrivenInitPipelinePass>(
                static (pass, context) => pass.Render(context)
            );
        }
    }
}