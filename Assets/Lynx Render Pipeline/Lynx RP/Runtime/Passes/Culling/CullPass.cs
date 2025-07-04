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
    public class CullPass
    {
        static readonly ProfilingSampler samplerCull = new("Cull Pass");
        
        private static readonly int
            indexSizeId = Shader.PropertyToID("_IndexSize"),
            numGroupsId = Shader.PropertyToID("_NumOfGroups");
        
        private static readonly int 
            offsetSizesBufferId = Shader.PropertyToID("_OffsetSizesBuffer"),
            matricesBufferId = Shader.PropertyToID("_MatricesBuffer"),
            aabbBufferId = Shader.PropertyToID("_AABBBuffer"); 

        private static readonly int
            indexBufferId = Shader.PropertyToID("_IndexBuffer"),
            triangleBufferId = Shader.PropertyToID("_TriangleBuffer"),
            bboxBufferId = Shader.PropertyToID("_BBoxBuffer"),
            quadBufferId = Shader.PropertyToID("_QuadBuffer");

        private static readonly int
            voteBufferId = Shader.PropertyToID("_VoteBuffer"),
            scanBufferId = Shader.PropertyToID("_ScanBuffer"),
            scanSumBufferId = Shader.PropertyToID("_ScanSumBuffer"),
            groupSumBufferId = Shader.PropertyToID("_GroupSumBuffer"),
            groupScanBufferId = Shader.PropertyToID("_GroupScanBuffer");
        
        private static readonly int
            argsBufferId = Shader.PropertyToID("_ArgsBuffer"),
            argsLineBufferId = Shader.PropertyToID("_ArgsLineBuffer"),
            argsQuadBufferId = Shader.PropertyToID("_ArgsQuadBuffer");

        private static readonly int
            vertexPassBufferId = Shader.PropertyToID("_VertexPassBuffer"),
            bboxPassBufferId = Shader.PropertyToID("_BBoxPassBuffer"),
            quadPassBufferId = Shader.PropertyToID("_QuadPassBuffer");
        
        private static readonly int
            hiZBufferId = Shader.PropertyToID("_HiZTexture"),
            cullDebugId = Shader.PropertyToID("_CullDebugTexture");
        
        bool skipPass, cullHiZ;
        
        int indexCount, triCount, triCountPadded;
        int voteGroups, scanGroups, sumGroups, compactGroups;
        const int numThreadsXMax = 256;
        const int numThreadsXMaxGroups = 32;

	    // int hiZMipLevelMax;
        
        Camera camera;
        Vector2Int resolution;
        
        Material cullMaterial;
        
        InterFrameData.MeshJobsData meshData;
        
        ComputeShader csCullShader, csCompactShader, csTransformPositionShader;

        BufferHandle offsetSizesBuffer, matricesBuffer, aabbBuffer;
        
        BufferHandle 
            indexBuffer, vertexPassBuffer, triangleBuffer,
			bboxBuffer, bboxPassBuffer,
			quadBuffer, quadPassBuffer,
			voteBuffer, scanBuffer, groupSumBuffer, groupScanBuffer, scanSumBuffer,
			argsBuffer, argsLineBuffer, argsQuadBuffer;

        TextureHandle hiZBuffer;
        TextureHandle cullDebug;

        BufferHandle hiZDebugPointBuffer;
        
        void DebugVertexBuffer(CommandBuffer commandBuffer, ComputeBuffer computeBuffer)
        {
            NativeArray<Vertex> tempVertices = new NativeArray<Vertex>(indexCount, Allocator.Persistent);
            var vertices = tempVertices;
            commandBuffer.RequestAsyncReadbackIntoNativeArray(ref tempVertices, computeBuffer, request =>
            {
                if (request.hasError)
                {
                    Debug.LogError("AsyncGPUReadback request failed.");
                }
                else
                {
                    for (var index = 0; index < vertices.Length; index++)
                    {
                        var t = vertices[index];
                        Debug.Log(index + ": " + t.uv + ", " + vertices[index].uv);
                    }
                }
            });
            // tempVertices.Dispose();
        }
        
        void DebugUintBuffer(CommandBuffer commandBuffer, ComputeBuffer computeBuffer)
        {
            NativeArray<uint> tempArray = new NativeArray<uint>(indexCount, Allocator.Persistent);
            var elements = tempArray;
            commandBuffer.RequestAsyncReadbackIntoNativeArray(ref tempArray, computeBuffer, request =>
            {
                if (request.hasError)
                {
                    Debug.LogError("AsyncGPUReadback request failed.");
                }
                else
                {
                    Debug.Log(elements.ToSeparatedString(" "));
                }
            });
        }
        
        void DebugLineBuffer(CommandBuffer commandBuffer, ComputeBuffer computeBuffer)
        {
            NativeArray<Line> lineArray = new NativeArray<Line>(indexCount, Allocator.Persistent);
            var lines = lineArray;
            commandBuffer.RequestAsyncReadbackIntoNativeArray(ref lineArray, computeBuffer, request =>
            {
                if (request.hasError)
                {
                    Debug.LogError("AsyncGPUReadback request failed.");
                }
                else
                {
                    for (var index = 0; index < lines.Length / 2; index++)
                    {
                        var t = lines[index];
                        var e = lines[index + 1];
                        Debug.Log(index + ": " + t.position + ", " + e.position);
                    }
                }
            });
        }
        
        void DebugPointBuffer(CommandBuffer commandBuffer, ComputeBuffer computeBuffer)
        {
            NativeArray<Line> pointArray = new NativeArray<Line>(indexCount, Allocator.Persistent);
            var point = pointArray;
            commandBuffer.RequestAsyncReadbackIntoNativeArray(ref pointArray, computeBuffer, request =>
            {
                if (request.hasError)
                {
                    Debug.LogError("AsyncGPUReadback request failed.");
                }
                else
                {
                    for (var index = 0; index < point.Length; index++)
                    {
                        var t = point[index];
                        Debug.Log(index + ": " + t.position + ", " + t.color);
                    }
                }
            });
        }
        
        void SetGroups(uint triBufferCount)
        {
            voteGroups = Mathf.CeilToInt(triBufferCount / (float)numThreadsXMax);
            scanGroups = Mathf.CeilToInt(triBufferCount/2f / numThreadsXMax/2f);
            sumGroups = Mathf.CeilToInt(scanGroups/2f / numThreadsXMaxGroups/2f);
            compactGroups = voteGroups;

	        // Debug.Log(voteGroups + " " + scanGroups + " " + sumGroups);
        }
        
        void Render(RenderGraphContext context)
        {
            CommandBuffer buffer = context.cmd;

            if (skipPass)
                return;
            
            // buffer.SetBufferData(
            //     indexBuffer, jobs.outputArray, 0, 0, indexCount
            // );
            buffer.SetBufferData(
                indexBuffer, meshData.meshBufferDefault, 0, 0, indexCount
            );

            int matricesCount = meshData.objCount;
            buffer.SetBufferData(
                offsetSizesBuffer, meshData.finalOffsetSizes, 0, 0, matricesCount * 2
            );
            buffer.SetBufferData(
                matricesBuffer, meshData.finalMatrices, 0, 0, matricesCount
            );
            buffer.SetBufferData(
                aabbBuffer, meshData.BBs, 0, 0, matricesCount
            );
            
            int indexCount2 = Mathf.CeilToInt(matricesCount / (float)numThreadsXMax);
            buffer.SetComputeBufferParam(csTransformPositionShader, 0, indexBufferId, indexBuffer);
            buffer.SetComputeBufferParam(csTransformPositionShader, 0, offsetSizesBufferId, offsetSizesBuffer);
            buffer.SetComputeBufferParam(csTransformPositionShader, 0, matricesBufferId, matricesBuffer);
            buffer.SetComputeIntParam(csTransformPositionShader, indexSizeId, matricesCount);
            buffer.DispatchCompute(csTransformPositionShader, 0, indexCount2, 1, 1);

            buffer.SetComputeBufferParam(csTransformPositionShader, 1, voteBufferId, voteBuffer);
            buffer.SetComputeBufferParam(csTransformPositionShader, 1, offsetSizesBufferId, offsetSizesBuffer);
            buffer.SetComputeBufferParam(csTransformPositionShader, 1, aabbBufferId, aabbBuffer);
            
            buffer.SetComputeBufferParam(csTransformPositionShader, 1, indexBufferId, indexBuffer);

            buffer.SetComputeIntParam(csTransformPositionShader, indexSizeId, matricesCount);
            buffer.DispatchCompute(csTransformPositionShader, 1, indexCount2, 1, 1);
            
            uint[] args = {0, 1, 0, 0};
            buffer.SetBufferData(argsBuffer, args, 0, 0, 4);
            buffer.SetBufferData(argsLineBuffer, args, 0, 0, 4);
            buffer.SetBufferData(argsQuadBuffer, args, 0, 0, 4);
            
            buffer.SetComputeIntParam(csCullShader, indexSizeId, triCount);
            
            buffer.SetComputeBufferParam(csCullShader, 0, indexBufferId, indexBuffer);
            buffer.SetComputeBufferParam(csCullShader, 0, triangleBufferId, triangleBuffer);
            buffer.SetComputeIntParam(csCompactShader, indexSizeId, triCount);

            uint clearGroupX = (uint)Mathf.CeilToInt(resolution.x / 16f);
            uint clearGroupY = (uint)Mathf.CeilToInt(resolution.y / 16f);
            int clearGroupMax = (int)Mathf.Max(clearGroupX, clearGroupY);

            buffer.SetComputeTextureParam(csCullShader, 4, cullDebugId, cullDebug);
            buffer.DispatchCompute(csCullShader, 4, clearGroupMax, clearGroupMax, 1);
            
            buffer.SetComputeBufferParam(csCullShader, 0, indexBufferId, indexBuffer);
            buffer.SetComputeBufferParam(csCullShader, 0, triangleBufferId, triangleBuffer);
            buffer.SetComputeBufferParam(csCullShader, 0, bboxBufferId, bboxBuffer);
            buffer.SetComputeBufferParam(csCullShader, 0, quadBufferId, quadBuffer);
            buffer.DispatchCompute(csCullShader, 0, voteGroups, 1, 1);
            
            buffer.SetComputeBufferParam(csCullShader, 1, triangleBufferId, triangleBuffer);
            buffer.SetComputeBufferParam(csCullShader, 1, voteBufferId, voteBuffer);
            buffer.SetComputeBufferParam(csCullShader, 1, bboxBufferId, bboxBuffer);
            buffer.SetComputeBufferParam(csCullShader, 1, quadBufferId, quadBuffer);
            buffer.SetComputeTextureParam(csCullShader, 1, cullDebugId, cullDebug);
            if (cullHiZ)
				buffer.SetComputeTextureParam(csCullShader, 1, hiZBufferId, hiZBuffer);
            buffer.DispatchCompute(csCullShader, 1, voteGroups, 1, 1);
            
            // uint[] tempArray =
            // {
	           //  0, 0, 1, 0, 1, 1, 0, 1, 
	           //  0, 0, 0, 0, 0, 0, 0, 0
            // };
            // buffer.SetBufferData(
	           //  voteBuffer, tempArray,
	           //  0, 0, tempArray.Length
            // );
            
            // buffer.SetComputeBufferParam(csCompactShader, 0, voteBufferId, voteBuffer);
            // buffer.SetComputeBufferParam(csCompactShader, 0, scanBufferId, scanBuffer);
            // buffer.SetComputeBufferParam(csCompactShader, 0, groupSumBufferId, groupSumBuffer);
            // buffer.DispatchCompute(csCompactShader, 0, scanGroups, 1, 1);
            
            // buffer.SetComputeIntParam(csCompactShader, numGroupsId, sumGroups);
            // buffer.SetComputeBufferParam(csCompactShader, 1, groupSumBufferId, groupSumBuffer);
            // buffer.SetComputeBufferParam(csCompactShader, 1, groupScanBufferId, groupScanBuffer);
            // buffer.DispatchCompute(csCompactShader, 1, sumGroups, 1, 1);
            
            // buffer.SetComputeBufferParam(csCompactShader, 2, groupScanBufferId, groupScanBuffer);
            // buffer.SetComputeBufferParam(csCompactShader, 2, voteBufferId, voteBuffer);
            // buffer.SetComputeBufferParam(csCompactShader, 2, scanBufferId, scanBuffer);
            // buffer.SetComputeBufferParam(csCompactShader, 2, scanSumBufferId, scanSumBuffer);
            // buffer.SetComputeBufferParam(csCompactShader, 2, triangleBufferId, triangleBuffer);
            // buffer.SetComputeBufferParam(csCompactShader, 2, argsBufferId, argsBuffer);
            // buffer.SetComputeBufferParam(csCompactShader, 2, vertexPassBufferId, vertexPassBuffer);
            // buffer.DispatchCompute(csCompactShader, 2, compactGroups, 1, 1);

            buffer.SetComputeBufferParam(csCullShader, 2, triangleBufferId, triangleBuffer);
			buffer.SetComputeBufferParam(csCullShader, 2, vertexPassBufferId, vertexPassBuffer);
			buffer.SetComputeBufferParam(csCullShader, 2, argsBufferId, argsBuffer);
			buffer.SetComputeBufferParam(csCullShader, 2, voteBufferId, voteBuffer);
			buffer.SetComputeBufferParam(csCullShader, 2, bboxBufferId, bboxBuffer);
			buffer.SetComputeBufferParam(csCullShader, 2, bboxPassBufferId, bboxPassBuffer);
			buffer.SetComputeBufferParam(csCullShader, 2, argsLineBufferId, argsLineBuffer);
			buffer.DispatchCompute(csCullShader, 2, compactGroups, 1, 1);
			
			int groups = Mathf.CeilToInt(indexCount / (float)numThreadsXMax);
			buffer.SetComputeBufferParam(csCullShader, 3, quadBufferId, quadBuffer);
			buffer.SetComputeBufferParam(csCullShader, 3, quadPassBufferId, quadPassBuffer);
			buffer.SetComputeBufferParam(csCullShader, 3, argsQuadBufferId, argsQuadBuffer);
			buffer.DispatchCompute(csCullShader, 3, groups, 1, 1);
			
			// buffer.SetComputeBufferParam(csCompactShader, 3, voteBufferId, voteBuffer);
			// buffer.SetComputeBufferParam(csCompactShader, 3, scanSumBufferId, scanSumBuffer);
			// buffer.DispatchCompute(csCompactShader, 3, voteGroups, 1, 1);
            
            buffer.SetGlobalBuffer(vertexPassBufferId, vertexPassBuffer);
            buffer.SetGlobalBuffer(bboxPassBufferId, bboxPassBuffer);
            buffer.SetGlobalBuffer(quadPassBufferId, quadPassBuffer);
            buffer.SetGlobalTexture(cullDebugId, cullDebug);
            if (cullHiZ)
				buffer.SetGlobalTexture(hiZBufferId, hiZBuffer);

            // DebugVertexBuffer(buffer, vertexPassBuffer);
            // DebugUintBuffer(buffer, scanSumBuffer);
            // DebugUintBuffer(buffer, argsQuadBuffer);
            // DebugLineBuffer(buffer, bboxPassBuffer);
            // DebugPointBuffer(buffer, quadPassBuffer);
            
            buffer.DrawProceduralIndirect(
                Matrix4x4.identity,
                cullMaterial, 0,
                MeshTopology.Triangles, argsBuffer);
            
            // buffer.DrawProceduralIndirect(
	           //  Matrix4x4.identity,
	           //  cullMaterial, 1,
	           //  MeshTopology.Lines, argsLineBuffer);
            
            buffer.DrawProceduralIndirect(
                Matrix4x4.identity,
                cullMaterial, 2,
                MeshTopology.Points, argsQuadBuffer);

            if (cullHiZ)
            {
				// buffer.SetGlobalBuffer(quadPassBufferId, hiZDebugPointBuffer);
				// buffer.DrawProcedural(
				// 	Matrix4x4.identity,
				// 	cullMaterial, 2,
				// 	MeshTopology.Points, resolution.x * resolution.y);
            }
            
            context.renderContext.ExecuteCommandBuffer(buffer);
            buffer.Clear();
            
            context.renderContext.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        public static void Record(
            RenderGraph renderGraph,
            Vector2Int attachmentSize,
            Camera camera,
            CameraSettings cameraSettings,
            in HiZData hiZData,
            in CameraRendererTextures textures,
            ComputeShader csCullShader,
            ComputeShader csCompactShader,
            ComputeShader csTransformPositionShader,
            Shader cullShader,
            ref InterFrameData.MeshJobsData meshData
        )
        {
            ProfilingSampler sampler = samplerCull;

            using RenderGraphBuilder builder = renderGraph.AddRenderPass(
                sampler.name, out CullPass pass, sampler
            );

            if (cullShader != null)
            {
                pass.cullMaterial = new Material(cullShader);
                pass.cullMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            
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
            pass.SetGroups((uint)pass.triCountPadded);
            
            pass.csCullShader = csCullShader;
            pass.csCompactShader = csCompactShader;
            pass.csTransformPositionShader = csTransformPositionShader;

            var descN = new BufferDesc
            {
                name = "Offset And Sizes Buffer",
                count = meshData.objCount * 2,
                stride = 4,
                target = GraphicsBuffer.Target.Structured
            };
            pass.offsetSizesBuffer = builder.WriteBuffer(renderGraph.CreateBuffer(descN));
            
            descN.name = "Matrices Buffer";
            descN.count = meshData.objCount;
            descN.stride = (4 * 4) * 4;
            pass.matricesBuffer = builder.WriteBuffer(renderGraph.CreateBuffer(descN));

            descN.name = "AABB Buffer";
            descN.count = meshData.objCount;
            descN.stride = 2 * (4 * 3) + 4;
            pass.aabbBuffer = builder.WriteBuffer(renderGraph.CreateBuffer(descN));
            
            var descT = new BufferDesc
            {
                name = "Index Buffer",
                count = pass.indexCount,
                stride = (4 * 3 * 2) + (4 * 4) + (4 * 2),
                target = GraphicsBuffer.Target.Structured
            };
            pass.indexBuffer = builder.WriteBuffer(renderGraph.CreateBuffer(descT));
            
            descT.name = "Vertex Pass Buffer";
            pass.vertexPassBuffer = builder.WriteBuffer(renderGraph.CreateBuffer(descT));
            
            descT.name = "Triangle Buffer"; 
            descT.count = pass.triCountPadded;
            descT.stride = 3 * ((4 * 3 * 2) + (4 * 4) + (4 * 2));
            pass.triangleBuffer = builder.WriteBuffer(renderGraph.CreateBuffer(descT));
            
            descT.name = "Vote Buffer";
            descT.stride = 4;
            pass.voteBuffer = builder.WriteBuffer(renderGraph.CreateBuffer(descT));
            
            descT.name = "BBox Buffer";
            descT.stride = 2 * (4 * 3) + 4;
            pass.bboxBuffer = builder.WriteBuffer(renderGraph.CreateBuffer(descT));
            descT.name = "BBox Pass Buffer";
            descT.stride = 2 * (4 * 3);
            pass.bboxPassBuffer = builder.WriteBuffer(renderGraph.CreateBuffer(descT));
            
            descT.name = "Quad Buffer";
            descT.count = pass.indexCount;
            pass.quadBuffer = builder.WriteBuffer(renderGraph.CreateBuffer(descT));
            descT.name = "Quad Pass Buffer";
            pass.quadPassBuffer = builder.WriteBuffer(renderGraph.CreateBuffer(descT));
            
            descT.name = "Scan Buffer";
            descT.count = pass.triCountPadded;
            pass.scanBuffer = builder.WriteBuffer(renderGraph.CreateBuffer(descT));
            descT.name = "Scan Sum Buffer";
            pass.scanSumBuffer = builder.WriteBuffer(renderGraph.CreateBuffer(descT));
            descT.name = "Group Sum Buffer";
            descT.count = pass.scanGroups;
            pass.groupSumBuffer = builder.WriteBuffer(renderGraph.CreateBuffer(descT));
            descT.name = "Group Scan Buffer";
            pass.groupScanBuffer = builder.WriteBuffer(renderGraph.CreateBuffer(descT));

            descT.name = "Args Buffer";
            descT.count = 1;
            descT.stride = 4 * 4;
            descT.target = GraphicsBuffer.Target.IndirectArguments;
            pass.argsBuffer = builder.WriteBuffer(renderGraph.CreateBuffer(descT));
            
            descT.name = "Args Line Buffer";
            pass.argsLineBuffer = builder.WriteBuffer(renderGraph.CreateBuffer(descT));
            descT.name = "Args Quad Buffer";
            pass.argsQuadBuffer = builder.WriteBuffer(renderGraph.CreateBuffer(descT));
            
            var desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
            {
                colorFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.HDR),
                name = "Cull Debug Texture",
                enableRandomWrite = true
            };
            desc.clearColor = Color.black;
            pass.cullDebug = builder.WriteTexture(renderGraph.CreateTexture(desc));

            pass.camera = Camera.main;
            pass.resolution = attachmentSize;

            pass.cullHiZ = (cameraSettings.cullSettings & CameraSettings.CullSettings.HiZ) != 0;
            if (pass.cullHiZ)
            {
				pass.hiZBuffer = builder.ReadTexture(hiZData.hiZDepthRT);
				pass.hiZDebugPointBuffer = builder.ReadBuffer(hiZData.pointBuffer);
            }
            
            builder.ReadWriteTexture(textures.colorAttachment);
            builder.ReadWriteTexture(textures.depthAttachment);

            builder.SetRenderFunc<CullPass>(
                static (pass, context) => pass.Render(context)
            );
        }
    }
}

/*
   200
   256
   
   numthreadsMax 64
   Groups 4
   
   temp[2 * 64] = vote[2 * 256]
   
   
   7
   8
   
   numthreadsMax 4
   Groups 2
   
   temp[2 * 4] = vote[2 * 8]
   
   upsweep 
   
   d0  0 0 1 0 1 1 0 1
   d1  0 0 1 1 1 2 0 1
   d2  0 0 1 1 1 2 0 3
   d3  0 0 1 1 1 2 0 4
   
   downsweep
   
   d-1 0 0 1 1 1 2 0 4
   d0  0 0 1 1 1 2 0 0
   d1  0 0 1 0 1 2 0 1
   d2  0 0 1 0 1 1 0 3
   d3  0 0 0 1 1 2 3 3
   
   4 6 7
   
   upsweep 
   
   d0  0 0 1 0 1 1 0 0?
   d1  0 0 1 1 1 2 0 0
   d2  0 0 1 1 1 2 0 2
   d3  0 0 1 1 1 2 0 3
   
   downsweep
   
   d-1 0 0 1 1 1 2 0 0
   d0  0 0 1 1 1 2 0 0
   d1  0 0 1 0 1 2 0 1
   d2  0 0 1 0 1 1 0 3
   d3  0 0 0 1 1 2 3 3
   
   
   numthreadsMax 4/2 = 2
   Groups 2
   
   0 0 1 0 1 1 0 1
   
   =================================
   group 0 numthread 0 idx 0
   0 0 temp 0 f f f
   1 1 temp 0 0 f f
   
   d = 2 offset = 1
   Sync1
   temp 0 0 1 0
   0 < 2
   0, 1
   temp[1] += temp[0]
   
   d = 1 offset = 2
   Sync2
   temp 0 0 1 1
   0 < 1
   1, 3
   temp[3] += temp[1]
   
   d = 0 offset = 4
   
   0 == 0
   group[0] = temp[4-1]
   group = 1 f
   temp[4-1] = 0
   temp 0 0 1 0
   
   ###########################
   d = 1 offset = 2
   Sync3
   temp 0 0 1 0
   0 < 1
   1, 3
   t = temp[1]
   temp[1] = temp[3]
   temp[3] += t
   
   d = 2 offset = 1
   Sync4
   temp 0 0 0 1
   0 < 2
   0, 1
   t = temp[0]
   temp[0] = temp[1]
   temp[1] += t
   
   d = 4 offset = 0
   
   Sync5
   temp 0 0 0 1
   
   scan[0] = temp[0]
   scan[1] = temp[1]
   
   =================================
   group 0 numthread 1 idx 1
   2 2 temp f f 1 f
   3 3 temp f f 1 0
   
   d = 2 offset = 1
   Sync1
   temp 0 0 1 0
   1 < 2
   2, 3
   temp[3] += temp[2]
   
   d = 1 offset = 2
   Sync2
   temp 0 0 1 1
   1 < 1
   
   d = 0 offset = 4
   
   1 == 0
   
   ###########################
   d = 1 offset = 2
   Sync3
   temp 0 0 1 0
   1 < 1
   
   d = 2 offset = 1
   1 < 2
   2, 3
   t = temp[2]
   temp[2] = temp[3]
   temp[3] += t
   
   d = 4 offset = 0
   
   Sync4
   temp 0 0 0 1
   
   Sync5
   
   scan[2] = temp[2]
   scan[3] = temp[3]
   
   
   =================================
   =================================
   group 1 numthread 0 idx 2
   0 4 temp 1 f f f 
   1 5 temp 1 1 f f 
   
   d = 2 offset = 1
   Sync1
   temp 1 1 0 1
   0 < 2
   0, 1
   temp[1] += temp[0]
   
   d = 1 offset = 2
   Sync2
   temp 1 2 0 1
   0 < 1
   1, 3
   temp[3] += temp[1]
   
   d = 0 offset = 4
   
   0 == 0
   group[1] = temp[4-1]
   group = 1 1
   temp[4-1] = 0
   temp 1 2 0 0
   
   ###########################
   d = 1 offset = 2
   Sync 3
   temp 1 2 0 0
   0 < 1
   1, 3
   t = temp[1]
   temp[1] = temp[3]
   temp[3] += t
   
   d = 2 offset 1
   Sync 4
   temp 1 0 0 2
   0 < 2
   0, 1
   t = temp[0]
   temp[0] = temp[1]
   temp[1] += t
   
   d = 4 offset = 0
   Sync5
   temp 0 1 2 2
   
   scan[4] = temp[0]
   scan[5] = temp[1]
   
   
   =================================
   group 1 numthread 1 idx 3
   2 6 temp f f 0 f
   3 7 temp f f 0 1
   
   d = 2 offset = 1
   Sync1
   temp 1 1 0 1
   1 < 2
   2, 3
   temp[3] += temp[2]
   
   d = 1 offset = 2
   Sync2
   temp 1 2 0 1
   1 < 1
   
   d = 0 offset = 4
   
   1 == 0
   
   ###########################
   d = 1 offset = 2
   Sync 3
   temp 1 2 0 0
   1 < 1
   
   d = 2 offset = 1
   Sync4
   temp 1 0 0 2
   1 < 2
   2, 3
   t = temp[2]
   temp[2] = temp[3]
   temp[3] += t
   
   d = 4 offset = 0
   Sync5
   temp 0 1 2 2
   
   scan[6] = temp[2]
   scan[7] = temp[3]
   
   
   
   =================================
   =================================
   scan 0 0 0 1 0 1 2 2
   group 1 1
   
   
   =================================
   =================================
   numthreadsMax 2/2 = 1
   Groups 1
   
   
   group 0 numthread 0 idx 0
   temp[0]
   temp = 1 1
   
   d = 1 offset = 1
   Sync1
   temp = 1 1
   0 < 1
   0, 1
   temp[1] += temp[0]
   
   d = 0 offset = 2
   
   Sync2
   temp = 1 2
   
   0 == 0
   temp = 1 0
   
   temp = 0 1
   
   
   =================================
   =================================
   numthreadsMax 4
   Groups 2
   
   elem 2 3 4 5 6 7 8 9
   scan 0 0 0 1 0 1 2 2
   group 0 1
   vote 0 0 1 0 1 1 0 1
   eOut f f f f f f f f
   
   =================================
   group 0 numthread 0 idx 0
   sum = 0
   vote[0] = 0
   0 == 1
   
   =================================
   group 0 numthread 1 idx 1
   sum = 0
   vote[1] = 0
   0 == 1
   
   =================================
   group 0 numthread 2 idx 2
   sum = 0
   vote[2] = 1
   1 == 1
   scan[2] + 0 = 0
   eOut[0] = elem[0] = 4
   
   =================================
   group 0 numthread 3 idx 3
   sum = 0
   vote[3] = 0
   0 == 1
   
   =================================
   group 1 numthread 0 idx 4
   sum = 1
   vote[4] = 1
   1 == 1
   scan[4] + 1 = 1
   eOut[1] = elem[4] = 6
   
   =================================
   group 1 numthread 1 idx 5
   sum = 1
   vote[5] = 1
   1 == 1
   scan[5] + 1 = 2
   eOut[2] = elem[5] = 7
   
   =================================
   group 1 numthread 2 idx 6
   sum = 1
   vote[6] = 0
   0 == 1
   
   =================================
   group 1 numthread 3 idx 7
   sum = 1
   vote[7] = 1
   1 == 1
   scan[7] + 1 = 3
   eOut[3] = elem[7] = 9
   
   
   =================================
   =================================
   
   
   
   scan 0 0 0 1 0+1 1+1 2+1 2+1
   scan 0 0 0 1 1 2 3 3
   d3   0 0 0 1 1 2 3 3
   
   elem 2 3 4 5 6 7 8 9
   vote 0 0 1 0 1 1 0 1
   eOut 4 6 7 9 f f f f
   
   voteGroups    = aLength / numThreadMax
   scanGroups    = aLength/2 / numThreadMax/2
   sumGroups     = scanGroups/2 / numThreadMax
   compactGroups = aLength / numThreadMax
   
   groupshared temp = 2 * numThreadMax of scanGroups
   groupshared grouptemp = 2 * numThreadMax of sumGroups
   
   aLength       = 8192
   voteGroups    = 8192 / 256 = 32
   scanGroups    = 4096 / 128 = 32
   sumGroups     = 16 / 4 = 4
   compactGroups = 8192 / 256 = 32
   
   aLength       = 32768
   voteGroups    = 32768 / 256 = 128
   scanGroups    = 16384 / 128 = 128
   sumGroups     = 64 / 32 = 2
   compactGroups = 32768 / 256 = 128
   
   aLength       = 1048576
   voteGroups    = 1048576 / 256 = 4096
   scanGroups    = 524288 / 128 = 4096
   sumGroups     = 2048 / 128 = 16
   compactGroups = 1048576 / 256 = 4096
   
   16 for sumGroup numThreads is a good number. or maybe just 64
 */
