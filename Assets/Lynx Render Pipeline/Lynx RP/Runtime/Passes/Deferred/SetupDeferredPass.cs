
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

        void ClearMRTs(ref UnsafeCommandBuffer cmd)
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

        void Render(UnsafeGraphContext context)
        {
            UnsafeCommandBuffer cmd = context.cmd;
            cmd.SetupCameraProperties(camera);

            bufferColorIDs[0] = albedoBuffer;
            bufferColorIDs[1] = positionBuffer;
            bufferColorIDs[2] = normalBuffer;
            bufferColorIDs[3] = normalInterpolatedBuffer;
            bufferColorIDs[4] = ormBuffer;
            bufferColorIDs[5] = lightingBuffer;
            bufferColorIDs[6] = extrasBuffer;

            ClearMRTs(ref cmd);
            cmd.SetRenderTarget(bufferColorIDs, depthBuffer);
        }

        public static DeferredRenderTextures Record(
            RenderGraph renderGraph, 
            bool useHDR,
            Vector2Int attachmentSize,
            Camera camera,
            in CameraRendererTextures textures
        )
        {
            using IUnsafeRenderGraphBuilder builder =
                renderGraph.AddUnsafePass(sampler.name, out SetupDeferredPass pass, sampler);
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
            positionBuffer = pass.positionBuffer = renderGraph.CreateTexture(desc);
            builder.UseTexture(positionBuffer, AccessFlags.WriteAll);

            desc.name = "Normal Buffer";
            normalBuffer = pass.normalBuffer = renderGraph.CreateTexture(desc);
            desc.name = "Normal Interpolated Buffer";
            normalInterpolatedBuffer = pass.normalInterpolatedBuffer = renderGraph.CreateTexture(desc);
            builder.UseTexture(normalBuffer, AccessFlags.WriteAll);
            builder.UseTexture(normalInterpolatedBuffer, AccessFlags.WriteAll);

            desc.name = "ORM Buffer";
            ormBuffer = pass.ormBuffer = renderGraph.CreateTexture(desc);
            desc.name = "Lighting Buffer";
            lightingBuffer = pass.lightingBuffer = renderGraph.CreateTexture(desc);
            desc.name = "Extras Buffer";
            extrasBuffer = pass.extrasBuffer = renderGraph.CreateTexture(desc);
            builder.UseTexture(ormBuffer, AccessFlags.WriteAll);
            builder.UseTexture(lightingBuffer, AccessFlags.WriteAll);
            builder.UseTexture(extrasBuffer, AccessFlags.WriteAll);

            pass.depthBuffer = textures.colorAttachment;
            pass.albedoBuffer = textures.depthAttachment;

            builder.UseTexture(pass.depthBuffer);
            builder.UseTexture(pass.albedoBuffer);
            
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
