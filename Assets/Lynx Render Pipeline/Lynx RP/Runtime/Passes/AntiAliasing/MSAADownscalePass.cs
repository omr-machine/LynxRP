using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace LynxRP
{
    public class MSAADownscalePass
    {
        static readonly ProfilingSampler sampler = new("MSAA Downscale");

        static readonly int 
            msaaBlurColorID = Shader.PropertyToID("_MSAABlurColor"),
            msaaBlurDepthID = Shader.PropertyToID("_MSAABlurDepth"),
            msaaDownscaleColorID = Shader.PropertyToID("_MSAADownscaleColor"),
            msaaDownscaleDepthID = Shader.PropertyToID("_MSAADownscaleDepth"),
            msaaBlurOffsetID = Shader.PropertyToID("_BlurOffset");

        TextureHandle 
            msaaFillColor, msaaFillDepth,
            msaaBlurColor, msaaBlurDepth;

        TextureHandle halfTexture, quarterTexture, eighthTexture, sixteenthTexture;

        TextureHandle colorAttachment, depthAttachment;

        Shader msaaDownscaleShader;

        Material msaaDownscaleMaterial;

        float blurOffset = 3.0f; //0.01f; //0 - 10

        void Render(RenderGraphContext context)
        {
            CommandBuffer buffer = context.cmd;

            buffer.SetGlobalFloat(msaaBlurOffsetID, blurOffset);

            buffer.SetGlobalTexture(msaaBlurColorID, msaaFillColor);
            buffer.SetRenderTarget(halfTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            buffer.DrawProcedural(Matrix4x4.identity, msaaDownscaleMaterial, 0, MeshTopology.Triangles, 3);

            buffer.SetGlobalTexture(msaaBlurColorID, halfTexture);
            buffer.SetRenderTarget(quarterTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            buffer.DrawProcedural(Matrix4x4.identity, msaaDownscaleMaterial, 0, MeshTopology.Triangles, 3);

            // buffer.SetGlobalTexture(msaaBlurColorID, quarterTexture);
            // buffer.SetRenderTarget(eighthTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            // buffer.DrawProcedural(Matrix4x4.identity, msaaDownscaleMaterial, 0, MeshTopology.Triangles, 3);

            // buffer.SetGlobalTexture(msaaBlurColorID, eighthTexture);
            // buffer.SetRenderTarget(quarterTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            // buffer.DrawProcedural(Matrix4x4.identity, msaaDownscaleMaterial, 1, MeshTopology.Triangles, 3);

            buffer.SetGlobalTexture(msaaBlurColorID, quarterTexture);
            buffer.SetRenderTarget(halfTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            buffer.DrawProcedural(Matrix4x4.identity, msaaDownscaleMaterial, 1, MeshTopology.Triangles, 3);

            buffer.SetGlobalTexture(msaaBlurColorID, halfTexture);
            buffer.SetRenderTarget(msaaFillColor, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            buffer.DrawProcedural(Matrix4x4.identity, msaaDownscaleMaterial, 1, MeshTopology.Triangles, 3);

            buffer.SetGlobalTexture(msaaDownscaleColorID, msaaFillColor);
            buffer.SetRenderTarget(
                colorAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                depthAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
                );

            buffer.DrawProcedural(Matrix4x4.identity, msaaDownscaleMaterial, 2, MeshTopology.Triangles, 3);

            context.renderContext.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        public static void Record(
            RenderGraph renderGraph,
            Shader msaaDownscaleShader,
            Vector2Int attachmentSize,
            in MSAARenderTextures msaaTextures,
            in CameraRendererTextures textures
        )
        {
            using RenderGraphBuilder builder =
                renderGraph.AddRenderPass(sampler.name, out MSAADownscalePass pass, sampler);
            
            var desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
            {
                colorFormat = SystemInfo.GetGraphicsFormat(UnityEngine.Experimental.Rendering.DefaultFormat.HDR),
                name = "Half Texture",
            };
            pass.halfTexture = builder.WriteTexture(renderGraph.CreateTexture(desc));
            desc.width = attachmentSize.x / 2; desc.height = attachmentSize.y / 2;
            pass.quarterTexture = builder.WriteTexture(renderGraph.CreateTexture(desc));
            desc.width = attachmentSize.x / 4; desc.height = attachmentSize.y / 4;
            pass.eighthTexture = builder.WriteTexture(renderGraph.CreateTexture(desc));
            desc.width = attachmentSize.x / 8; desc.height = attachmentSize.y / 8;
            pass.sixteenthTexture = builder.WriteTexture(renderGraph.CreateTexture(desc));

            pass.msaaDownscaleShader = msaaDownscaleShader;
            if (pass.msaaDownscaleShader != null)
            {
                pass.msaaDownscaleMaterial = new Material(pass.msaaDownscaleShader);
                pass.msaaDownscaleMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            pass.msaaFillColor = builder.ReadTexture(msaaTextures.msaaFill);
            pass.msaaFillDepth = builder.ReadTexture(msaaTextures.msaaFillDepth);
            pass.msaaBlurColor = builder.WriteTexture(msaaTextures.msaaBlur);
            pass.msaaBlurDepth = builder.WriteTexture(msaaTextures.msaaBlurDepth);

            pass.colorAttachment = builder.ReadWriteTexture(textures.colorAttachment);
            pass.depthAttachment = builder.ReadWriteTexture(textures.depthAttachment);
            
            builder.AllowPassCulling(false);
            builder.SetRenderFunc<MSAADownscalePass>(
                static (pass, context) => pass.Render(context)
            );
        }
    }
}
