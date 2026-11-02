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
        TextureHandle debugHiZRTH;
        RTHandle debugHiZRTHandle;

        void CopyDepth(ref UnsafeCommandBuffer buffer, ref RenderTargetIdentifier target, bool clearTarget)
        {
            buffer.SetRenderTarget(target, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            if (clearTarget)
                buffer.ClearRenderTarget(true, true, Color.black);
    
            buffer.DrawProcedural(
                Matrix4x4.identity, copyMaterial, 0, MeshTopology.Triangles, 3
            );
        }

        void CopyToRenderTexture(ref UnsafeCommandBuffer buffer, ref RenderTexture renderTexture)
        {
            renderTexture.Release();
            renderTexture.width = attachmentSize.x; renderTexture.height = attachmentSize.y;
            renderTexture.Create();
            RenderTargetIdentifier target = new RenderTargetIdentifier(renderTexture);
            CopyDepth(ref buffer, ref target, true);
        }

        void Render(UnsafeGraphContext context)
        {
            UnsafeCommandBuffer buffer = context.cmd;
            RenderTargetIdentifier hiZBufferRTI = new RenderTargetIdentifier(hiZInterFrameRT);
            
            if (!debug)
            {
                buffer.SetGlobalTexture(sourceID, depthAttachment);
                CopyDepth(ref buffer, ref hiZBufferRTI, true);
                CopyToRenderTexture(ref buffer, ref debugHiZRT);
            }
            else
            {
                buffer.SetGlobalTexture(sourceID, debugHiZRTH);
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
            using IUnsafeRenderGraphBuilder builder =
                renderGraph.AddUnsafePass(sampler.name, out HiZCopyPass pass, sampler);
            
            pass.debugHiZRT = debugHiZRT;
            pass.attachmentSize = attachmentSize;
            pass.camera = camera;

            pass.debug = debug;
            
            pass.copyMaterial = copyMaterial;
            
            pass.csHiZShader = csHiZShader;
            
            pass.hiZInterFrameRT = hiZData.hiZInterFrameRT;
            pass.hiZCurrFrameDataRT = hiZData.hiZPrevFrameDataRT;
            
            pass.depthAttachment = textures.depthAttachment;
            pass.colorAttachment = textures.colorAttachment;

            builder.UseTexture(pass.hiZInterFrameRT, AccessFlags.WriteAll);
            builder.UseTexture(pass.hiZCurrFrameDataRT, AccessFlags.WriteAll);

            RenderTargetInfo rtInfo = new RenderTargetInfo
            {
                width = debugHiZRT.width,
                height = debugHiZRT.height,
                volumeDepth = debugHiZRT.volumeDepth,
                msaaSamples = debugHiZRT.antiAliasing,
                format = debugHiZRT.graphicsFormat
            };

            pass.debugHiZRTHandle ??= RTHandles.Alloc(debugHiZRT);
            pass.debugHiZRTH = renderGraph.ImportTexture(pass.debugHiZRTHandle, rtInfo);

            builder.UseTexture(pass.debugHiZRTH, AccessFlags.WriteAll);
            
            builder.UseTexture(pass.depthAttachment);
            builder.UseTexture(pass.colorAttachment);
            
            builder.AllowPassCulling(false);
            builder.SetRenderFunc<HiZCopyPass>(
                static (pass, context) => pass.Render(context)
            );
        }
    }
}
