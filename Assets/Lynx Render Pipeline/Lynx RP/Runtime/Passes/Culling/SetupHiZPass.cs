using LynxRP;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace LynxRP
{
    public class SetupHiZPass
    {
        static readonly ProfilingSampler sampler = new("Setup Hierarchical Z");
        
        TextureHandle hiZDepthRT;
        TextureHandle colorAttachment, depthAttachment;

        void Render(RenderGraphContext context)
        {
            CommandBuffer buffer = context.cmd;
            
            buffer.SetRenderTarget(hiZDepthRT);
            buffer.ClearRenderTarget(true, true, Color.black);
            
            buffer.SetRenderTarget(
                colorAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                depthAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
            context.renderContext.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        public static HiZData Record(
            RenderGraph renderGraph,
            Vector2Int attachmentSize,
            in CameraRendererTextures textures
        )
        {
            using RenderGraphBuilder builder =
                renderGraph.AddRenderPass(sampler.name, out SetupHiZPass pass, sampler);
            
            var desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
            {
                name = "HiZ Texture",
                // colorFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.HDR),
                format = GraphicsFormat.R32_SFloat,
                filterMode = FilterMode.Point,
                enableRandomWrite = true,
                useMipMap = true,
                autoGenerateMips = false
            };
            TextureHandle hiZDepthRT = builder.WriteTexture(renderGraph.CreateTexture(desc));

            desc.name = "HiZ Interframe Texture";
            desc.useMipMap = false;
            TextureHandle hiZInterFrameRT = renderGraph.CreateTexture(desc);
            
            desc.name = "HiZ Previous Frame Data";
            desc.width = 8;
            desc.height = 8; 
            desc.format = GraphicsFormat.R32_SFloat;
            TextureHandle hiZPrevFrameDataRT = renderGraph.CreateTexture(desc);
            
            BufferDesc bufferDesc = new BufferDesc
            {
                name = "Debug HiZ Reprojection Point Buffer",
                count = attachmentSize.x * attachmentSize.y,
                stride = 4 * 3 * 2,
                target = GraphicsBuffer.Target.Structured
            };
            
            BufferHandle pointBuffer =renderGraph.CreateBuffer(bufferDesc);
            
            pass.hiZDepthRT = hiZDepthRT;
            
            pass.colorAttachment = textures.colorAttachment;
            pass.depthAttachment = textures.depthAttachment;

            builder.AllowPassCulling(false);
            builder.SetRenderFunc<SetupHiZPass>(
                static (pass, context) => pass.Render(context)
            );

            return new HiZData(hiZDepthRT, 0, hiZInterFrameRT, hiZPrevFrameDataRT, pointBuffer);
        }
    }
}
