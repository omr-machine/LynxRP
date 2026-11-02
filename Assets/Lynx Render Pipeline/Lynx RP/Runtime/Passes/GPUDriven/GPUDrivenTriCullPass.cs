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
    public class GPUDrivenTriCullPass
    {
        static readonly ProfilingSampler samplerCull = new("Pipeline TriCull Pass");

        private static readonly int
            indexSizeId = Shader.PropertyToID("_IndexSize");

        private static readonly int
            indexBufferId = Shader.PropertyToID("_IndexBuffer"),
            voteBufferId = Shader.PropertyToID("_VoteBuffer");

        private static readonly int
            triangleBufferId = Shader.PropertyToID("_TriangleBuffer"),
            bboxBufferId = Shader.PropertyToID("_BBoxBuffer"),
            quadBufferId = Shader.PropertyToID("_QuadBuffer");

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

        ComputeShader csCullShader;

        BufferHandle indexBuffer, voteBuffer;

        BufferHandle triangleBuffer, bboxBuffer, quadBuffer;

        TextureHandle hiZBuffer;
        TextureHandle cullDebug;

        void SetGroups(uint triBufferCount)
        {
            voteGroups = Mathf.CeilToInt(triBufferCount / (float)numThreadsXMax);
            scanGroups = Mathf.CeilToInt(triBufferCount / 2f / numThreadsXMax / 2f);
            sumGroups = Mathf.CeilToInt(scanGroups / 2f / numThreadsXMaxGroups / 2f);
            compactGroups = voteGroups;

            // Debug.Log(voteGroups + " " + scanGroups + " " + sumGroups);
        }

        void Render(UnsafeGraphContext context)
        {
            UnsafeCommandBuffer buffer = context.cmd;

            if (skipPass)
                return;

            LynxRenderPipelineCamera lynxRenderPipelineCamera = camera.GetComponent<LynxRenderPipelineCamera>();
            bool skipTexture = (uint)lynxRenderPipelineCamera.Settings.cullSettings > 7;
            LocalKeyword localKeyword = new(csCullShader, "_HIZ_ON");
            buffer.SetKeyword(csCullShader, localKeyword, skipTexture);

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

            buffer.SetGlobalTexture(cullDebugId, cullDebug);
            if (cullHiZ)
                buffer.SetGlobalTexture(hiZBufferId, hiZBuffer);
        }

        public static void Record(
            RenderGraph renderGraph,
            Vector2Int attachmentSize,
            CameraSettings cameraSettings,
            in HiZData hiZData,
            in CameraRendererTextures textures,
            ComputeShader csCullShader,
            int indexCount, int triCount,
            in GPUDrivenData gpuDrivenData
        )
        {
            ProfilingSampler sampler = samplerCull;

            using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(
                sampler.name, out GPUDrivenTriCullPass pass, sampler
            );

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
            pass.SetGroups((uint)pass.triCountPadded);

            pass.csCullShader = csCullShader;

            pass.indexBuffer = gpuDrivenData.indexBuffer;
            pass.voteBuffer = gpuDrivenData.voteBuffer;
            builder.UseBuffer(pass.indexBuffer, AccessFlags.WriteAll);
            builder.UseBuffer(pass.voteBuffer, AccessFlags.WriteAll);

            pass.triangleBuffer = gpuDrivenData.triangleBuffer;
            pass.bboxBuffer = gpuDrivenData.bboxBuffer;
            pass.quadBuffer = gpuDrivenData.quadBuffer;
            builder.UseBuffer(pass.triangleBuffer, AccessFlags.WriteAll);
            builder.UseBuffer(pass.bboxBuffer, AccessFlags.WriteAll);
            builder.UseBuffer(pass.quadBuffer, AccessFlags.WriteAll);

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
                builder.UseTexture(pass.hiZBuffer);
            }

            builder.UseTexture(textures.colorAttachment, AccessFlags.ReadWrite);
            builder.UseTexture(textures.depthAttachment, AccessFlags.ReadWrite);

            builder.SetRenderFunc<GPUDrivenTriCullPass>(
                static (pass, context) => pass.Render(context)
            );
        }
    }
}