using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace LynxRP
{
    public class HiZCopyPass
    {
        static readonly ProfilingSampler sampler = new("Hierarchical Z Copy");

        static readonly int 
            sourceID = Shader.PropertyToID("_SourceTexture"),
            storeFrameDataID = Shader.PropertyToID("_FrameData");

        Camera camera;
        
        TextureHandle colorAttachment, depthAttachment;
        
        TextureHandle hiZInterFrameRT, hiZCurrFrameDataRT;

        ComputeShader csHiZShader;

        Material copyMaterial;

        private bool debug;
        Vector2Int attachmentSize;
        RenderTexture debugHiZRT;

        void CopyDepth(ref CommandBuffer buffer, ref RenderTargetIdentifier target, bool clearTarget)
        {
            buffer.SetRenderTarget(target, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            if (clearTarget)
                buffer.ClearRenderTarget(true, true, Color.black);
    
            buffer.DrawProcedural(
                Matrix4x4.identity, copyMaterial, 0, MeshTopology.Triangles, 3
            );
        }

        void CopyToRenderTexture(ref CommandBuffer buffer, ref RenderTexture renderTexture)
        {
            renderTexture.Release();
            renderTexture.width = attachmentSize.x; renderTexture.height = attachmentSize.y;
            renderTexture.Create();
            RenderTargetIdentifier target = new RenderTargetIdentifier(renderTexture);
            CopyDepth(ref buffer, ref target, true);
        }

        void Render(RenderGraphContext context)
        {
            CommandBuffer buffer = context.cmd;
            RenderTargetIdentifier hiZBufferRTI = new RenderTargetIdentifier(hiZInterFrameRT);
            
            if (!debug)
            {
                buffer.SetGlobalTexture(sourceID, depthAttachment);
                CopyDepth(ref buffer, ref hiZBufferRTI, true);
                CopyToRenderTexture(ref buffer, ref debugHiZRT);
            }
            else
            {
                buffer.SetGlobalTexture(sourceID, debugHiZRT);
                CopyDepth(ref buffer, ref hiZBufferRTI, false);
            }

            if (camera.cameraType != CameraType.SceneView)
            {
                buffer.SetComputeTextureParam(csHiZShader, 1, storeFrameDataID, hiZCurrFrameDataRT);
                buffer.DispatchCompute(csHiZShader, 1, 1, 1, 1);
            }
            
            buffer.SetRenderTarget(
                colorAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                depthAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );

            context.renderContext.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        public static void Record(
            RenderGraph renderGraph,
            Vector2Int attachmentSize,
            Camera camera,
            bool debug,
            Material copyMaterial,
            ComputeShader csHiZShader,
            in CameraRendererTextures textures,
            in HiZData hiZData,
            RenderTexture debugHiZRT
            
        )
        {
            using RenderGraphBuilder builder =
                renderGraph.AddRenderPass(sampler.name, out HiZCopyPass pass, sampler);
            
            pass.debugHiZRT = debugHiZRT;
            pass.attachmentSize = attachmentSize;
            pass.camera = camera;

            pass.debug = debug;
            
            pass.copyMaterial = copyMaterial;
            
            pass.csHiZShader = csHiZShader;
            
            pass.hiZInterFrameRT = builder.WriteTexture(hiZData.hiZInterFrameRT);
            pass.hiZCurrFrameDataRT = builder.WriteTexture(hiZData.hiZPrevFrameDataRT);
            
            pass.depthAttachment = builder.ReadTexture(textures.depthAttachment);
            pass.colorAttachment = builder.ReadTexture(textures.colorAttachment);

            builder.AllowPassCulling(false);
            builder.SetRenderFunc<HiZCopyPass>(
                static (pass, context) => pass.Render(context)
            );
        }
    }
}
