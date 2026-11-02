
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace LynxRP
{
    public partial class SetupPass
    {
        static readonly ProfilingSampler sampler = new("Setup");

        TextureHandle colorAttachment, depthAttachment;

        Vector2Int attachmentSize;

        Camera camera;

        CameraClearFlags clearFlags;
        
        int cullSettings;

        bool debugCull;

        void Render(UnsafeGraphContext context)
        {
            UnsafeCommandBuffer cmd = context.cmd;
            cmd.SetupCameraProperties(camera);

            cmd.SetRenderTarget(
                colorAttachment,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                depthAttachment,
                RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
            
            cmd.ClearRenderTarget(
                clearFlags <= CameraClearFlags.Depth,
                clearFlags <= CameraClearFlags.Color,
                clearFlags == CameraClearFlags.Color ?
                    camera.backgroundColor.linear : Color.clear
            );

            Camera keywordCamera = camera;
            if (camera.cameraType == CameraType.SceneView)
            {
                keywordCamera = Camera.main;
                if (keywordCamera.TryGetComponent(out LynxRenderPipelineCamera crpCamera) && debugCull)
                    cullSettings = (int)crpCamera.Settings.cullSettings;
            }
            SetKeywords(cmd, keywordCamera, attachmentSize, cullSettings);
        }

        public static CameraRendererTextures Record(
            RenderGraph renderGraph, 
            bool copyColor,
            bool copyDepth,
            bool useHDR,
            Vector2Int attachmentSize,
            Camera camera,
            MSAASamples msaaSamples,
            int cullSettings,
            bool debugCull
        )
        {
            using IUnsafeRenderGraphBuilder builder =
                renderGraph.AddUnsafePass(sampler.name, out SetupPass pass, sampler);
            
            pass.attachmentSize = attachmentSize;
            pass.camera = camera;
            pass.clearFlags = camera.clearFlags;
            pass.cullSettings = cullSettings;
            pass.debugCull = debugCull;
            
            TextureHandle colorCopy = default, depthCopy = default;

            if (pass.clearFlags > CameraClearFlags.Color)
            {
                pass.clearFlags = CameraClearFlags.Color;
            }

            var desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
            {
                colorFormat = SystemInfo.GetGraphicsFormat(
                    useHDR ? DefaultFormat.HDR : DefaultFormat.LDR
                ),
                name = "Color Attachment"
            };
            desc.msaaSamples = msaaSamples;

            TextureHandle colorAttachment = pass.colorAttachment = 
                renderGraph.CreateTexture(desc);
            builder.UseTexture(colorAttachment, AccessFlags.WriteAll);

            if (copyColor)
            {
                desc.name = "Color Copy";
                colorCopy = renderGraph.CreateTexture(desc);
            }

            desc.depthBufferBits = DepthBits.Depth32;
            desc.name = "Depth Attachment";

            TextureHandle depthAttachment = pass.depthAttachment = 
                renderGraph.CreateTexture(desc);
            builder.UseTexture(depthAttachment, AccessFlags.WriteAll);

            if (copyDepth)
            {
                desc.name = "Depth Copy";
                depthCopy = renderGraph.CreateTexture(desc);
            }
            
            builder.AllowPassCulling(false);
            builder.SetRenderFunc<SetupPass>(
                static (pass, context) => pass.Render(context)
            );

            return new CameraRendererTextures(
                colorAttachment, depthAttachment, colorCopy, depthCopy
            );
        }
    }
}
