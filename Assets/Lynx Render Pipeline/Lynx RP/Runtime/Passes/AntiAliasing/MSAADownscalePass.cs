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

        void Render(UnsafeGraphContext context)
        {
            UnsafeCommandBuffer buffer = context.cmd;

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
        }

        public static void Record(
            RenderGraph renderGraph,
            Shader msaaDownscaleShader,
            Vector2Int attachmentSize,
            in MSAARenderTextures msaaTextures,
            in CameraRendererTextures textures
        )
        {
            using IUnsafeRenderGraphBuilder builder =
                renderGraph.AddUnsafePass(sampler.name, out MSAADownscalePass pass, sampler);
            
            var desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
            {
                colorFormat = SystemInfo.GetGraphicsFormat(UnityEngine.Experimental.Rendering.DefaultFormat.HDR),
                name = "Half Texture",
            };
            pass.halfTexture = renderGraph.CreateTexture(desc);
            desc.width = attachmentSize.x / 2; desc.height = attachmentSize.y / 2;
            pass.quarterTexture = renderGraph.CreateTexture(desc);
            desc.width = attachmentSize.x / 4; desc.height = attachmentSize.y / 4;
            pass.eighthTexture = renderGraph.CreateTexture(desc);
            desc.width = attachmentSize.x / 8; desc.height = attachmentSize.y / 8;
            pass.sixteenthTexture = renderGraph.CreateTexture(desc);

            builder.UseTexture(pass.halfTexture, AccessFlags.WriteAll);
            builder.UseTexture(pass.quarterTexture, AccessFlags.WriteAll);
            builder.UseTexture(pass.eighthTexture, AccessFlags.WriteAll);
            builder.UseTexture(pass.sixteenthTexture, AccessFlags.WriteAll);

            pass.msaaDownscaleShader = msaaDownscaleShader;
            if (pass.msaaDownscaleShader != null)
            {
                pass.msaaDownscaleMaterial = new Material(pass.msaaDownscaleShader);
                pass.msaaDownscaleMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            pass.msaaFillColor = msaaTextures.msaaFill;
            pass.msaaFillDepth = msaaTextures.msaaFillDepth;
            pass.msaaBlurColor = msaaTextures.msaaBlur;
            pass.msaaBlurDepth = msaaTextures.msaaBlurDepth;

            builder.UseTexture(pass.msaaFillColor);
            builder.UseTexture(pass.msaaFillDepth);
            builder.UseTexture(pass.msaaBlurColor, AccessFlags.WriteAll);
            builder.UseTexture(pass.msaaBlurDepth, AccessFlags.WriteAll);

            pass.colorAttachment = textures.colorAttachment;
            pass.depthAttachment = textures.depthAttachment;

            builder.UseTexture(pass.colorAttachment, AccessFlags.ReadWrite);
            builder.UseTexture(pass.depthAttachment, AccessFlags.ReadWrite);
            
            builder.AllowPassCulling(false);
            builder.SetRenderFunc<MSAADownscalePass>(
                static (pass, context) => pass.Render(context)
            );
        }
    }
}
