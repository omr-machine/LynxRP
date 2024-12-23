
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace LynxRP
{
    public class SetupDeferredPass
    {
        static readonly ProfilingSampler sampler = new("Setup Deferred");

        TextureHandle
            positionBuffer,
            normalBuffer, normalInterpolatedBuffer,
            ormBuffer, lightingBuffer, extrasBuffer;

        TextureHandle albedoBuffer, depthBuffer;

        RenderTargetIdentifier[] bufferColorIDs = new RenderTargetIdentifier[7];

        Camera camera;

        CameraClearFlags clearFlags;

        void ClearMRTs(ref CommandBuffer cmd)
        {
            for (int i = 1; i < bufferColorIDs.Length; i++)
            {
                cmd.SetRenderTarget(bufferColorIDs[i]);
                cmd.ClearRenderTarget(
                    false,
                    clearFlags <= CameraClearFlags.Color,
                    clearFlags == CameraClearFlags.Color ?
                    camera.backgroundColor.linear : Color.clear
                );
            }
        }

        void Render(RenderGraphContext context)
        {
            context.renderContext.SetupCameraProperties(camera);
            CommandBuffer cmd = context.cmd;

            bufferColorIDs[0] = albedoBuffer;
            bufferColorIDs[1] = positionBuffer;
            bufferColorIDs[2] = normalBuffer;
            bufferColorIDs[3] = normalInterpolatedBuffer;
            bufferColorIDs[4] = ormBuffer;
            bufferColorIDs[5] = lightingBuffer;
            bufferColorIDs[6] = extrasBuffer;

            ClearMRTs(ref cmd);
            cmd.SetRenderTarget(bufferColorIDs, depthBuffer);

            context.renderContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        public static DeferredRenderTextures Record(
            RenderGraph renderGraph, 
            bool useHDR,
            Vector2Int attachmentSize,
            Camera camera,
            in CameraRendererTextures textures
        )
        {
            using RenderGraphBuilder builder =
                renderGraph.AddRenderPass(sampler.name, out SetupDeferredPass pass, sampler);
            pass.camera = camera;
            pass.clearFlags = camera.clearFlags;
            
            TextureHandle 
                positionBuffer, normalBuffer, normalInterpolatedBuffer,
                ormBuffer, lightingBuffer, extrasBuffer;

            if (pass.clearFlags > CameraClearFlags.Color)
            {
                pass.clearFlags = CameraClearFlags.Color;
            }
            var desc = new TextureDesc(attachmentSize.x, attachmentSize.y)
            {
                colorFormat = SystemInfo.GetGraphicsFormat(
                    useHDR ? DefaultFormat.HDR : DefaultFormat.LDR
                ),
                name = "Position Buffer"
            };
            positionBuffer = pass.positionBuffer = builder.WriteTexture(renderGraph.CreateTexture(desc));

            desc.name = "Normal Buffer";
            normalBuffer = pass.normalBuffer = builder.WriteTexture(renderGraph.CreateTexture(desc));
            desc.name = "Normal Interpolated Buffer";
            normalInterpolatedBuffer = pass.normalInterpolatedBuffer = builder.WriteTexture(renderGraph.CreateTexture(desc));

            desc.name = "ORM Buffer";
            ormBuffer = pass.ormBuffer = builder.WriteTexture(renderGraph.CreateTexture(desc));
            desc.name = "Lighting Buffer";
            lightingBuffer = pass.lightingBuffer = builder.WriteTexture(renderGraph.CreateTexture(desc));
            desc.name = "Extras Buffer";
            extrasBuffer = pass.extrasBuffer = builder.WriteTexture(renderGraph.CreateTexture(desc));

            pass.depthBuffer = builder.ReadTexture(textures.colorAttachment);
            pass.albedoBuffer = builder.ReadTexture(textures.depthAttachment);
            
            builder.AllowPassCulling(false);
            builder.SetRenderFunc<SetupDeferredPass>(
                static (pass, context) => pass.Render(context)
            );

            return new DeferredRenderTextures(
                positionBuffer,
                normalBuffer, normalInterpolatedBuffer,
                ormBuffer, lightingBuffer, extrasBuffer
            );
        }
    }
}
