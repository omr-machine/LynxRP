using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

namespace LynxRP
{
    public class MSAAStencilPass
    {
        static readonly ProfilingSampler sampler = new("MSAA Stencil");

        static readonly int msaaDebugID = Shader.PropertyToID("_DebugMSAA2");

        static ShaderTagId[] shaderTagId = 
        {
            new("MSAAStencil"),
            new("CustomLit"),
            new("SRPDefaultUnlit")
        };

        RendererListHandle list;

        TextureHandle msaaStencil;

        TextureHandle colorAttachment, depthAttachment;

        Shader msaaStencilShader;

        int width, height;

        void Render(UnsafeGraphContext context)
        {
            UnsafeCommandBuffer buffer = context.cmd;

            buffer.SetRenderTarget(msaaStencil, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            buffer.DrawRendererList(list);

            buffer.SetGlobalTexture(msaaDebugID, msaaStencil);
            // buffer.ClearRenderTarget(true, true, Color.black);

            buffer.SetRenderTarget(
                colorAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                depthAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
        }

        public static MSAARenderTextures Record(
            RenderGraph renderGraph,
            Camera camera,
            CullingResults cullingResults,
            Vector2Int attachmentSize,
            int renderingLayerMask,
            Shader msaaStencilShader,
            in CameraRendererTextures textures
        )
        {
            using IUnsafeRenderGraphBuilder builder =
                renderGraph.AddUnsafePass(sampler.name, out MSAAStencilPass pass, sampler);

            pass.colorAttachment = textures.colorAttachment;
            pass.depthAttachment = textures.depthAttachment;
            pass.msaaStencilShader = msaaStencilShader;

            builder.UseTexture(pass.colorAttachment);
            builder.UseTexture(pass.depthAttachment);
            
            pass.list = renderGraph.CreateRendererList(
                new RendererListDesc(shaderTagId, cullingResults, camera)
                    {
                        sortingCriteria = SortingCriteria.CommonOpaque,
                        // rendererConfiguration = PerObjectData.None,
                        renderQueueRange = RenderQueueRange.opaque,
                        renderingLayerMask = (uint)renderingLayerMask,
                        // overrideMaterial = pass.msaaStencilMaterial,
                        overrideShader = pass.msaaStencilShader
                    }
            );
            builder.UseRendererList(pass.list);

            TextureHandle msaaStencil;
            TextureHandle msaaFill = default, msaaBlur = default, msaaDownscale = default;
            TextureHandle msaaFillDepth = default, msaaBlurDepth = default, msaaDownscaleDepth = default;

            pass.width = attachmentSize.x;
            pass.height = attachmentSize.y;
            var desc = new TextureDesc(pass.width * 2, pass.height * 2)
            {
                // colorFormat = GraphicsFormat.R8_SInt,
                colorFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.HDR),
                name = "MSAA Stencil",
            };
            msaaStencil = pass.msaaStencil = renderGraph.CreateTexture(desc);
            builder.UseTexture(msaaStencil, AccessFlags.WriteAll); // also do pass.msaaStencil?

            desc.name = "MSAA Fill";
            msaaFill = renderGraph.CreateTexture(desc);
            desc.name = "MSAA Blur";
            msaaBlur = renderGraph.CreateTexture(desc);
            desc.name = "MSAA Downscale";
            msaaDownscale = renderGraph.CreateTexture(desc);

            desc.depthBufferBits = DepthBits.Depth32;
            desc.name = "Fill Depth";
            msaaFillDepth = renderGraph.CreateTexture(desc);
            desc.name = "Blur Depth";
            msaaBlurDepth = renderGraph.CreateTexture(desc);
            desc.name = "Downscale Depth";
            msaaDownscaleDepth = renderGraph.CreateTexture(desc);


            builder.AllowPassCulling(false);
            builder.SetRenderFunc<MSAAStencilPass>(
                static (pass, context) => pass.Render(context)
            );

            return new MSAARenderTextures(msaaStencil, msaaFill, msaaFillDepth, msaaBlur, msaaBlurDepth, msaaDownscale, msaaDownscaleDepth);
        }
    }
}
