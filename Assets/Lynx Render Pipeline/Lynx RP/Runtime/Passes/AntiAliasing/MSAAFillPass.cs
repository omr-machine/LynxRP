using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace LynxRP
{
    public class MSAAFillPass
    {
        static readonly ProfilingSampler sampler = new("MSAA Fill");

        static readonly int 
            msaaDebugID = Shader.PropertyToID("_DebugMSAAFillColor"),
            msaaDebugDepthID = Shader.PropertyToID("_DebugMSAAFillDepth"),
            msaaFillStencilID = Shader.PropertyToID("_MSAAFillStencil"),
            msaaFillColorAttachmentID = Shader.PropertyToID("_MSAAFillColorAttachment"),
            msaaFillDepthAttachmentID = Shader.PropertyToID("_MSAAFillDepthAttachment"),
            msaaFillTexelSizeID = Shader.PropertyToID("_MSAAFillTexelSize");

        TextureHandle msaaFill, msaaFillDepth, msaaStencil;

        TextureHandle colorAttachment, depthAttachment;

        Shader msaaFillShader;

        Material msaaFillMaterial;

        int width, height;

        void Render(UnsafeGraphContext context)
        {
            UnsafeCommandBuffer buffer = context.cmd;
            
            int fillMSAAWidth = width * 2;
            int fillMSAAHeight = height * 2;
            buffer.SetGlobalVector(msaaFillTexelSizeID, new Vector4(
                1f / fillMSAAWidth, 1f / fillMSAAHeight, fillMSAAWidth, fillMSAAHeight
            ));

            buffer.SetGlobalTexture(msaaFillColorAttachmentID, colorAttachment);
            buffer.SetGlobalTexture(msaaFillDepthAttachmentID, depthAttachment);
            buffer.SetGlobalTexture(msaaFillStencilID, msaaStencil);

            buffer.SetRenderTarget
            (
                msaaFill, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                msaaFillDepth, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
            buffer.DrawProcedural(
                Matrix4x4.identity, msaaFillMaterial, 0, MeshTopology.Triangles, 3
            );
            buffer.SetGlobalTexture(msaaDebugID, msaaFill);
            buffer.SetGlobalTexture(msaaDebugDepthID, msaaFillDepth);

            buffer.SetRenderTarget(
                colorAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                depthAttachment, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
        }


        public static void Record(
            RenderGraph renderGraph,
            Vector2Int attachmentSize,
            Shader msaaFillShader,
            in MSAARenderTextures msaaTextures,
            in CameraRendererTextures textures
        )
        {
            using IUnsafeRenderGraphBuilder builder =
                renderGraph.AddUnsafePass(sampler.name, out MSAAFillPass pass, sampler);

            pass.colorAttachment = textures.colorAttachment;
            pass.depthAttachment = textures.depthAttachment;

            builder.UseTexture(pass.colorAttachment);
            builder.UseTexture(pass.depthAttachment);

            pass.msaaFillShader = msaaFillShader;
            if (pass.msaaFillShader != null)
            {
                pass.msaaFillMaterial = new Material(pass.msaaFillShader);
                pass.msaaFillMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            pass.msaaStencil = msaaTextures.msaaStencil;

            pass.width = attachmentSize.x;
            pass.height = attachmentSize.y;
            pass.msaaFill = msaaTextures.msaaFill;
            pass.msaaFillDepth = msaaTextures.msaaFillDepth;

            builder.UseTexture(pass.msaaStencil, AccessFlags.ReadWrite);
            builder.UseTexture(pass.msaaFill, AccessFlags.WriteAll);
            builder.UseTexture(pass.msaaFillDepth, AccessFlags.WriteAll);

            builder.AllowPassCulling(false);
            builder.SetRenderFunc<MSAAFillPass>(
                static (pass, context) => pass.Render(context)
            );
        }
    }
}
