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
    public class GPUDrivenIndirectDrawPass
    {
        static readonly ProfilingSampler samplerCull = new("Pipeline Indirect Draw Pass");

        private static readonly int
            indexSizeId = Shader.PropertyToID("_IndexSize");

        private static readonly int
            triangleBufferId = Shader.PropertyToID("_TriangleBuffer"),
            bboxBufferId = Shader.PropertyToID("_BBoxBuffer"),
            quadBufferId = Shader.PropertyToID("_QuadBuffer");

        private static readonly int
            voteBufferId = Shader.PropertyToID("_VoteBuffer");

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
        const int numThreadsXMax = 256;
        const int numThreadsXMaxGroups = 32;

        // int hiZMipLevelMax;

        Camera camera;
        Vector2Int resolution;

        Material cullMaterial;

        ComputeShader csCullShader;

        BufferHandle indexBuffer, voteBuffer;

        BufferHandle triangleBuffer, bboxBuffer, quadBuffer;

        BufferHandle vertexPassBuffer, bboxPassBuffer, quadPassBuffer;

        BufferHandle argsBuffer, argsLineBuffer, argsQuadBuffer;

        BufferHandle hiZDebugPointBuffer;

        void DebugVertexBuffer(CommandBuffer commandBuffer, ComputeBuffer computeBuffer)
        {
            NativeArray<Vertex> tempVertices = new(indexCount, Allocator.Persistent);
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
            NativeArray<uint> tempArray = new(indexCount, Allocator.Persistent);
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
            NativeArray<Line> lineArray = new(indexCount, Allocator.Persistent);
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
            NativeArray<Line> pointArray = new(indexCount, Allocator.Persistent);
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

        void Render(RenderGraphContext context)
        {
            CommandBuffer buffer = context.cmd;

            if (skipPass)
                return;

            int compactGroups = Mathf.CeilToInt(triCountPadded / (float)numThreadsXMax);
            buffer.SetComputeIntParam(csCullShader, indexSizeId, triCount);
            buffer.SetComputeBufferParam(csCullShader, 2, triangleBufferId, triangleBuffer);
            buffer.SetComputeBufferParam(csCullShader, 2, vertexPassBufferId, vertexPassBuffer);
            buffer.SetComputeBufferParam(csCullShader, 2, argsBufferId, argsBuffer);
            buffer.SetComputeBufferParam(csCullShader, 2, voteBufferId, voteBuffer);
            buffer.SetComputeBufferParam(csCullShader, 2, bboxBufferId, bboxBuffer);
            buffer.SetComputeBufferParam(csCullShader, 2, bboxPassBufferId, bboxPassBuffer);
            buffer.SetComputeBufferParam(csCullShader, 2, argsLineBufferId, argsLineBuffer);
            Debug.Log("Compact groups: " + compactGroups);
            // buffer.DispatchCompute(csCullShader, 2, compactGroups, 1, 1);

            int groups = Mathf.CeilToInt(indexCount / (float)numThreadsXMax);
            buffer.SetComputeBufferParam(csCullShader, 3, quadBufferId, quadBuffer);
            buffer.SetComputeBufferParam(csCullShader, 3, quadPassBufferId, quadPassBuffer);
            buffer.SetComputeBufferParam(csCullShader, 3, argsQuadBufferId, argsQuadBuffer);
            // buffer.DispatchCompute(csCullShader, 3, groups, 1, 1);

            // buffer.SetGlobalBuffer(vertexPassBufferId, vertexPassBuffer);
            // buffer.SetGlobalBuffer(bboxPassBufferId, bboxPassBuffer);
            buffer.SetGlobalBuffer(quadPassBufferId, quadPassBuffer);

            // buffer.DrawProceduralIndirect(
            //     Matrix4x4.identity,
            //     cullMaterial, 0,
            //     MeshTopology.Triangles, argsBuffer);

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

            // DebugVertexBuffer(buffer, vertexPassBuffer);
            // DebugUintBuffer(buffer, scanSumBuffer);
            // DebugUintBuffer(buffer, argsQuadBuffer);
            // DebugLineBuffer(buffer, bboxPassBuffer);
            // DebugPointBuffer(buffer, quadPassBuffer);

            context.renderContext.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        public static void Record(
            RenderGraph renderGraph,
            CameraSettings cameraSettings,
            in HiZData hiZData,
            in CameraRendererTextures textures,
            ComputeShader csCullShader,
            Shader cullShader,
            int indexCount, int triCount,
            in GPUDrivenData gpuDrivenData
        )
        {
            ProfilingSampler sampler = samplerCull;

            using RenderGraphBuilder builder = renderGraph.AddRenderPass(
                sampler.name, out GPUDrivenIndirectDrawPass pass, sampler
            );

            if (cullShader != null)
            {
                pass.cullMaterial = new Material(cullShader);
                pass.cullMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            pass.skipPass = false;

            pass.indexCount = indexCount;
            pass.triCount = triCount;
            if (pass.indexCount == 0 || pass.triCount == 0)
            {
                pass.indexCount = 3;
                pass.triCount = 1;
                pass.skipPass = true;
            }

            pass.triCountPadded = (int)NextPowerOfTwo((uint)pass.triCount);

            pass.csCullShader = csCullShader;

            pass.indexBuffer = builder.ReadBuffer(gpuDrivenData.indexBuffer);
            pass.voteBuffer = builder.ReadBuffer(gpuDrivenData.voteBuffer);

            pass.triangleBuffer = builder.ReadBuffer(gpuDrivenData.triangleBuffer);
            pass.bboxBuffer = builder.ReadBuffer(gpuDrivenData.bboxBuffer);
            pass.quadBuffer = builder.ReadBuffer(gpuDrivenData.quadBuffer);

            pass.vertexPassBuffer = builder.WriteBuffer(gpuDrivenData.vertexPassBuffer);
            pass.bboxPassBuffer = builder.WriteBuffer(gpuDrivenData.bboxPassBuffer);
            pass.quadPassBuffer = builder.WriteBuffer(gpuDrivenData.quadPassBuffer);

            pass.argsBuffer = builder.WriteBuffer(gpuDrivenData.argsBuffer);
            pass.argsLineBuffer = builder.WriteBuffer(gpuDrivenData.argsLineBuffer);
            pass.argsQuadBuffer = builder.WriteBuffer(gpuDrivenData.argsQuadBuffer);

            pass.camera = Camera.main;

            pass.cullHiZ = (cameraSettings.cullSettings & CameraSettings.CullSettings.HiZ) != 0;
            if (pass.cullHiZ)
            {
                pass.hiZDebugPointBuffer = builder.ReadBuffer(hiZData.pointBuffer);
            }

            builder.ReadWriteTexture(textures.colorAttachment);
            builder.ReadWriteTexture(textures.depthAttachment);

            builder.SetRenderFunc<GPUDrivenIndirectDrawPass>(
                static (pass, context) => pass.Render(context)
            );
        }
    }
}