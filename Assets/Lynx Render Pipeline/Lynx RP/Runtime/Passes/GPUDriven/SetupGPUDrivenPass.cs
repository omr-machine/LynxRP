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
    public class SetupGPUDrivenPass
    {
        static readonly ProfilingSampler samplerCull = new("Pipeline Init State Pass");
        
        private static readonly int
            indexSizeId = Shader.PropertyToID("_IndexSize");
        
        private static readonly int
            indexBufferId = Shader.PropertyToID("_IndexBuffer"),
            voteBufferId = Shader.PropertyToID("_VoteBuffer");

        private static readonly int 
            offsetSizesBufferId = Shader.PropertyToID("_OffsetSizesBuffer"),
            matricesBufferId = Shader.PropertyToID("_MatricesBuffer"),
            aabbBufferId = Shader.PropertyToID("_AABBBuffer");

        private static readonly int
            triangleBufferId = Shader.PropertyToID("_TriangleBuffer"),
            bboxBufferId = Shader.PropertyToID("_BBoxBuffer"),
            quadBufferId = Shader.PropertyToID("_QuadBuffer");

        private static readonly int
            argsBufferId = Shader.PropertyToID("_ArgsBuffer"),
            argsLineBufferId = Shader.PropertyToID("_ArgsLineBuffer"),
            argsQuadBufferId = Shader.PropertyToID("_ArgsQuadBuffer");

        private static readonly int
            vertexPassBufferId = Shader.PropertyToID("_VertexPassBuffer"),
            bboxPassBufferId = Shader.PropertyToID("_BBoxPassBuffer"),
            quadPassBufferId = Shader.PropertyToID("_QuadPassBuffer");

        int indexCount, triCount, triCountPadded;
        int voteGroups, scanGroups, sumGroups, compactGroups;

        const int numThreadsXMax = 256;
        const int numThreadsXMaxGroups = 32;
        
        InterFrameData.MeshJobsData meshData;
        
        ComputeShader csTransformPositionShader;

        BufferHandle indexBuffer, voteBuffer;

        BufferHandle offsetSizesBuffer, matricesBuffer, aabbBuffer;
        
        BufferHandle triangleBuffer, bboxBuffer, quadBuffer;

        BufferHandle vertexPassBuffer, bboxPassBuffer, quadPassBuffer;
        BufferHandle argsBuffer, argsLineBuffer, argsQuadBuffer;

        void SetGroups(uint triBufferCount)
        {
            voteGroups = Mathf.CeilToInt(triBufferCount / (float)numThreadsXMax);
            scanGroups = Mathf.CeilToInt(triBufferCount / 2f / numThreadsXMax / 2f);
            sumGroups = Mathf.CeilToInt(scanGroups / 2f / numThreadsXMaxGroups / 2f);
            compactGroups = voteGroups;

            // Debug.Log(voteGroups + " " + scanGroups + " " + sumGroups);
        }
        
        void Render(RenderGraphContext context)
        {
            CommandBuffer buffer = context.cmd;

            context.renderContext.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        public static GPUDrivenData Record(
            RenderGraph renderGraph,
            in CameraRendererTextures textures,
            ComputeShader csTransformPositionShader,
            ref InterFrameData.MeshJobsData meshData
        )
        {
            ProfilingSampler sampler = samplerCull;

            using RenderGraphBuilder builder = renderGraph.AddRenderPass(
                sampler.name, out SetupGPUDrivenPass pass, sampler
            );

            pass.indexCount = meshData.indexCount;
            pass.triCount = meshData.triCount;
            if (pass.indexCount == 0 || pass.triCount == 0)
            {
                pass.indexCount = 3;
                pass.triCount = 1;
            }
            pass.meshData = meshData;

            pass.triCountPadded = (int)NextPowerOfTwo((uint)pass.triCount);
            
            pass.csTransformPositionShader = csTransformPositionShader;

            var descT = new BufferDesc
            {
                name = "Index Buffer",
                count = pass.indexCount,
                stride = (4 * 3 * 2) + (4 * 4) + (4 * 2),
                target = GraphicsBuffer.Target.Structured
            };
            pass.indexBuffer = renderGraph.CreateBuffer(descT);

            descT.name = "Vote Buffer";
            descT.count = pass.triCountPadded;
            descT.stride = 4;
            pass.voteBuffer = renderGraph.CreateBuffer(descT);

            descT.name = "Offset And Sizes Buffer";
            descT.count = meshData.objCount * 2;
            descT.stride = 4;
            pass.offsetSizesBuffer = renderGraph.CreateBuffer(descT);
            
            descT.name = "Matrices Buffer";
            descT.count = meshData.objCount;
            descT.stride = (4 * 4) * 4;
            pass.matricesBuffer = renderGraph.CreateBuffer(descT);

            descT.name = "AABB Buffer";
            descT.stride = 2 * (4 * 3) + 4;
            pass.aabbBuffer = renderGraph.CreateBuffer(descT);

            descT.name = "Triangle Buffer";
            descT.count = pass.triCountPadded;
            descT.stride = 3 * ((4 * 3 * 2) + (4 * 4) + (4 * 2));
            pass.triangleBuffer = renderGraph.CreateBuffer(descT);

            descT.name = "BBox Buffer";
            descT.stride = 2 * (4 * 3) + 4;
            pass.bboxBuffer = renderGraph.CreateBuffer(descT);

            descT.name = "Quad Buffer";
            descT.count = pass.indexCount;
            descT.stride = 2 * (4 * 3);
            pass.quadBuffer = renderGraph.CreateBuffer(descT);

            descT.name = "Vertex Pass Buffer";
            descT.count = pass.indexCount;
            descT.stride = (4 * 3 * 2) + (4 * 4) + (4 * 2);
            pass.vertexPassBuffer = renderGraph.CreateBuffer(descT);

            descT.name = "BBox Pass Buffer";
            descT.count = pass.triCountPadded;
            descT.stride = 2 * (4 * 3);
            pass.bboxPassBuffer = renderGraph.CreateBuffer(descT);

            descT.name = "Quad Pass Buffer";
            descT.count = pass.indexCount;
            pass.quadPassBuffer = renderGraph.CreateBuffer(descT);

            descT.name = "Args Buffer";
            descT.count = 1;
            descT.stride = 4 * 4;
            descT.target = GraphicsBuffer.Target.IndirectArguments;
            pass.argsBuffer = renderGraph.CreateBuffer(descT);

            descT.name = "Args Line Buffer";
            pass.argsLineBuffer = renderGraph.CreateBuffer(descT);
            descT.name = "Args Quad Buffer";
            pass.argsQuadBuffer = renderGraph.CreateBuffer(descT);
            
            builder.ReadWriteTexture(textures.colorAttachment);
            builder.ReadWriteTexture(textures.depthAttachment);

            builder.SetRenderFunc<SetupGPUDrivenPass>(
                static (pass, context) => pass.Render(context)
            );

            return new GPUDrivenData(
                pass.indexBuffer,
                pass.voteBuffer,

                pass.offsetSizesBuffer,
                pass.matricesBuffer,
                pass.aabbBuffer,
                
                pass.triangleBuffer,
                pass.bboxBuffer,
                pass.quadBuffer,
                
                pass.vertexPassBuffer,
                pass.bboxPassBuffer,
                pass.quadPassBuffer,
                
                pass.argsBuffer,
                pass.argsLineBuffer,
                pass.argsQuadBuffer
            );
        }
    }
}