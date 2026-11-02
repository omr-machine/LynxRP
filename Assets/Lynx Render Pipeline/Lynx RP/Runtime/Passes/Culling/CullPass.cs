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
        
        void DebugVertexBuffer(UnsafeCommandBuffer commandBuffer, ComputeBuffer computeBuffer)
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
        
        void DebugUintBuffer(UnsafeCommandBuffer commandBuffer, ComputeBuffer computeBuffer)
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
        
        void DebugLineBuffer(UnsafeCommandBuffer commandBuffer, ComputeBuffer computeBuffer)
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
        
        void DebugPointBuffer(UnsafeCommandBuffer commandBuffer, ComputeBuffer computeBuffer)
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

        void InitPipelineState(ref UnsafeCommandBuffer buffer)
        {
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
        }

        void TriCulling(ref UnsafeCommandBuffer buffer)
        {
            LynxRenderPipelineCamera lynxRenderPipelineCamera = camera.GetComponent<LynxRenderPipelineCamera>();
            bool skipTexture = (uint)lynxRenderPipelineCamera.Settings.cullSettings > 7;
            LocalKeyword localKeyword = new(csCullShader, "_HIZ_ON");
            buffer.SetKeyword(csCullShader,localKeyword, skipTexture);

            buffer.SetComputeIntParam(csCullShader, indexSizeId, triCount);

            buffer.SetComputeBufferParam(csCullShader, 0, indexBufferId, indexBuffer);
            buffer.SetComputeBufferParam(csCullShader, 0, triangleBufferId, triangleBuffer);

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
        }

        void PrepareIndirects(ref UnsafeCommandBuffer buffer)
        {
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

            buffer.SetGlobalBuffer(vertexPassBufferId, vertexPassBuffer);
            buffer.SetGlobalBuffer(bboxPassBufferId, bboxPassBuffer);
            buffer.SetGlobalBuffer(quadPassBufferId, quadPassBuffer);
            buffer.SetGlobalTexture(cullDebugId, cullDebug);
            if (cullHiZ)
                buffer.SetGlobalTexture(hiZBufferId, hiZBuffer);
        }
        
        void Render(UnsafeGraphContext context)
        {
            UnsafeCommandBuffer buffer = context.cmd;

            if (skipPass)
                return;
            
            buffer.SetBufferData(
                indexBuffer, meshData.meshBufferDefault, 0, 0, indexCount
            );

            InitPipelineState(ref buffer);
            
            uint[] args = {0, 1, 0, 0};
            buffer.SetBufferData(argsBuffer, args, 0, 0, 4);
            buffer.SetBufferData(argsLineBuffer, args, 0, 0, 4);
            buffer.SetBufferData(argsQuadBuffer, args, 0, 0, 4);
            
            TriCulling(ref buffer);
            PrepareIndirects(ref buffer);
            
            buffer.DrawProceduralIndirect(
                Matrix4x4.identity,
                cullMaterial, 3,
                MeshTopology.Triangles, argsBuffer);
            
            // buffer.DrawProceduralIndirect(
	           //  Matrix4x4.identity,
	           //  cullMaterial, 1,
	           //  MeshTopology.Lines, argsLineBuffer);
            
            // buffer.DrawProceduralIndirect(
            //     Matrix4x4.identity,
            //     cullMaterial, 2,
            //     MeshTopology.Points, argsQuadBuffer);

            if (cullHiZ)
            {
				// buffer.SetGlobalBuffer(quadPassBufferId, hiZDebugPointBuffer);
				// buffer.DrawProcedural(
				// 	Matrix4x4.identity,
				// 	cullMaterial, 2,
				// 	MeshTopology.Points, resolution.x * resolution.y);
            }

            // DebugVertexBuffer(buffer, vertexPassBuffer);
            // DebugUintBuffer(buffer, scanSumBuffer);
            // DebugUintBuffer(buffer, argsQuadBuffer);
            // DebugLineBuffer(buffer, bboxPassBuffer);
            // DebugPointBuffer(buffer, quadPassBuffer);
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

            using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(
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
            pass.offsetSizesBuffer = renderGraph.CreateBuffer(descN);
            builder.UseBuffer(pass.offsetSizesBuffer, AccessFlags.WriteAll);
            
            descN.name = "Matrices Buffer";
            descN.count = meshData.objCount;
            descN.stride = (4 * 4) * 4;
            pass.matricesBuffer = renderGraph.CreateBuffer(descN);
            builder.UseBuffer(pass.matricesBuffer, AccessFlags.WriteAll);

            descN.name = "AABB Buffer";
            descN.count = meshData.objCount;
            descN.stride = 2 * (4 * 3) + 4;
            pass.aabbBuffer = renderGraph.CreateBuffer(descN);
            builder.UseBuffer(pass.aabbBuffer, AccessFlags.WriteAll);
            
            var descT = new BufferDesc
            {
                name = "Index Buffer",
                count = pass.indexCount,
                stride = (4 * 3 * 2) + (4 * 4) + (4 * 2),
                target = GraphicsBuffer.Target.Structured
            };
            pass.indexBuffer = renderGraph.CreateBuffer(descT);
            builder.UseBuffer(pass.indexBuffer, AccessFlags.WriteAll);
            
            descT.name = "Vertex Pass Buffer";
            pass.vertexPassBuffer = renderGraph.CreateBuffer(descT);
            builder.UseBuffer(pass.vertexPassBuffer, AccessFlags.WriteAll);
            
            descT.name = "Triangle Buffer"; 
            descT.count = pass.triCountPadded;
            descT.stride = 3 * ((4 * 3 * 2) + (4 * 4) + (4 * 2));
            pass.triangleBuffer = renderGraph.CreateBuffer(descT);
            builder.UseBuffer(pass.triangleBuffer, AccessFlags.WriteAll);
            
            descT.name = "Vote Buffer";
            descT.stride = 4;
            pass.voteBuffer = renderGraph.CreateBuffer(descT);
            builder.UseBuffer(pass.voteBuffer, AccessFlags.WriteAll);
            
            descT.name = "BBox Buffer";
            descT.stride = 2 * (4 * 3) + 4;
            pass.bboxBuffer = renderGraph.CreateBuffer(descT);
            descT.name = "BBox Pass Buffer";
            descT.stride = 2 * (4 * 3);
            pass.bboxPassBuffer = renderGraph.CreateBuffer(descT);
            builder.UseBuffer(pass.bboxBuffer, AccessFlags.WriteAll);
            builder.UseBuffer(pass.bboxPassBuffer, AccessFlags.WriteAll);
            
            descT.name = "Quad Buffer";
            descT.count = pass.indexCount;
            pass.quadBuffer = renderGraph.CreateBuffer(descT);
            descT.name = "Quad Pass Buffer";
            pass.quadPassBuffer = renderGraph.CreateBuffer(descT);
            builder.UseBuffer(pass.quadBuffer, AccessFlags.WriteAll);
            builder.UseBuffer(pass.quadPassBuffer, AccessFlags.WriteAll);
            
            descT.name = "Scan Buffer";
            descT.count = pass.triCountPadded;
            pass.scanBuffer = renderGraph.CreateBuffer(descT);
            descT.name = "Scan Sum Buffer";
            pass.scanSumBuffer = renderGraph.CreateBuffer(descT);
            descT.name = "Group Sum Buffer";
            descT.count = pass.scanGroups;
            pass.groupSumBuffer = renderGraph.CreateBuffer(descT);
            descT.name = "Group Scan Buffer";
            pass.groupScanBuffer = renderGraph.CreateBuffer(descT);
            builder.UseBuffer(pass.scanBuffer, AccessFlags.WriteAll);
            builder.UseBuffer(pass.scanSumBuffer, AccessFlags.WriteAll);
            builder.UseBuffer(pass.groupSumBuffer, AccessFlags.WriteAll);
            builder.UseBuffer(pass.groupScanBuffer, AccessFlags.WriteAll);

            descT.name = "Args Buffer";
            descT.count = 1;
            descT.stride = 4 * 4;
            descT.target = GraphicsBuffer.Target.IndirectArguments;
            pass.argsBuffer = renderGraph.CreateBuffer(descT);
            builder.UseBuffer(pass.argsBuffer, AccessFlags.WriteAll);
            
            descT.name = "Args Line Buffer";
            pass.argsLineBuffer = renderGraph.CreateBuffer(descT);
            descT.name = "Args Quad Buffer";
            pass.argsQuadBuffer = renderGraph.CreateBuffer(descT);
            builder.UseBuffer(pass.argsLineBuffer, AccessFlags.WriteAll);
            builder.UseBuffer(pass.argsQuadBuffer, AccessFlags.WriteAll);
            
            var desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
            {
                colorFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.HDR),
                name = "Cull Debug Texture",
                enableRandomWrite = true
            };
            desc.clearColor = Color.black;
            pass.cullDebug = renderGraph.CreateTexture(desc);
            builder.UseTexture(pass.cullDebug, AccessFlags.WriteAll);

            pass.camera = Camera.main;
            pass.resolution = attachmentSize;

            pass.cullHiZ = (cameraSettings.cullSettings & CameraSettings.CullSettings.HiZ) != 0;
            if (pass.cullHiZ)
            {
				pass.hiZBuffer = hiZData.hiZDepthRT;
				pass.hiZDebugPointBuffer = hiZData.pointBuffer;
                builder.UseTexture(pass.hiZBuffer);
                builder.UseBuffer(pass.hiZDebugPointBuffer);
            }
            
            builder.UseTexture(textures.colorAttachment, AccessFlags.ReadWrite);
            builder.UseTexture(textures.depthAttachment, AccessFlags.ReadWrite);

            builder.SetRenderFunc<CullPass>(
                static (pass, context) => pass.Render(context)
            );
        }
    }
}