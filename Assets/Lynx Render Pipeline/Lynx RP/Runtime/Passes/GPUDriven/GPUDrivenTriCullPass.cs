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

        void Render(RenderGraphContext context)
        {
            CommandBuffer buffer = context.cmd;

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

            context.renderContext.ExecuteCommandBuffer(buffer);
            buffer.Clear();
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

            using RenderGraphBuilder builder = renderGraph.AddRenderPass(
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

            pass.indexBuffer = builder.WriteBuffer(gpuDrivenData.indexBuffer);
            pass.voteBuffer = builder.WriteBuffer(gpuDrivenData.voteBuffer);

            pass.triangleBuffer = builder.WriteBuffer(gpuDrivenData.triangleBuffer);
            pass.bboxBuffer = builder.WriteBuffer(gpuDrivenData.bboxBuffer);
            pass.quadBuffer = builder.WriteBuffer(gpuDrivenData.quadBuffer);

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
            }

            builder.ReadWriteTexture(textures.colorAttachment);
            builder.ReadWriteTexture(textures.depthAttachment);

            builder.SetRenderFunc<GPUDrivenTriCullPass>(
                static (pass, context) => pass.Render(context)
            );
        }
    }
}