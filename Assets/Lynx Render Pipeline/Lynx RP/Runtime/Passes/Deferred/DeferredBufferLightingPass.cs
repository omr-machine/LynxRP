using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace LynxRP
{
    public class DeferredBufferLightingPass
    {
        static readonly ProfilingSampler sampler = new("Deferred Buffer Lighting");

        static readonly int 
            albedoID = Shader.PropertyToID("_DeferredBufferAlbedo"),
            positionID = Shader.PropertyToID("_DeferredBufferPosition"),
            normalID = Shader.PropertyToID("_DeferredBufferNormal"),
            normalInterpolatedID = Shader.PropertyToID("_DeferredBufferNormalInterpolated"),
            ormID = Shader.PropertyToID("_DeferredBufferOrm"),
            emissionsID = Shader.PropertyToID("_DeferredBufferEmission"),
            extrasID = Shader.PropertyToID("_DeferredBufferExtras"),
            depthID = Shader.PropertyToID("_DeferredBufferDepth");

        static readonly int 
            debugPositionID = Shader.PropertyToID("_DebugADeferredPositionBuffer"),
            debugNormalID = Shader.PropertyToID("_DebugADeferredNormalBuffer"),
            debugNormalInterpolatedID = Shader.PropertyToID("_DebugADeferredNormalInterpolatedBuffer"),
            debugOrmID = Shader.PropertyToID("_DebugADeferredOrmdBuffer"),
            debugEmissionID = Shader.PropertyToID("_DebugADeferredEmissiondBuffer"),
            debugExtrasID = Shader.PropertyToID("_DebugADeferredExtrasdBuffer");

        static ShaderTagId[] shaderTagId =
        {
            new("CustomLitDeferredLighting"),
            new("CustomUnlitDeferredLighting")
        };

        TextureHandle 
            positionBuffer, 
            normalBuffer, normalInterpolatedBuffer, 
            ormBuffer, lightingBuffer, extrasBuffer;

        TextureHandle albedoBuffer, depthBuffer;

        Material deferredMaterial;

        void Render(UnsafeGraphContext context)
        {
            UnsafeCommandBuffer buffer = context.cmd;

            buffer.SetGlobalTexture(debugPositionID, positionBuffer);
            buffer.SetGlobalTexture(debugNormalID, normalBuffer);
            buffer.SetGlobalTexture(debugNormalInterpolatedID, normalInterpolatedBuffer);
            buffer.SetGlobalTexture(debugOrmID, ormBuffer);
            buffer.SetGlobalTexture(debugEmissionID, lightingBuffer);
            buffer.SetGlobalTexture(debugExtrasID, extrasBuffer);

            buffer.SetGlobalTexture(emissionsID, lightingBuffer);

            buffer.SetRenderTarget(
                lightingBuffer, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                depthBuffer, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );

            buffer.SetGlobalTexture(albedoID, albedoBuffer);
            buffer.SetGlobalTexture(positionID, positionBuffer);
            buffer.SetGlobalTexture(normalID, normalBuffer);
            buffer.SetGlobalTexture(normalInterpolatedID, normalInterpolatedBuffer);
            buffer.SetGlobalTexture(ormID, ormBuffer);
            buffer.SetGlobalTexture(extrasID, extrasBuffer);

            buffer.SetGlobalTexture(depthID, depthBuffer);

            buffer.DrawProcedural(
                Matrix4x4.identity, deferredMaterial, 2, MeshTopology.Triangles, 3
            );
        }

        public static void Record(
            RenderGraph renderGraph,
            Shader deferredShader,
            in CameraRendererTextures textures,
            in DeferredRenderTextures deferredTextures
            
        )
        {
            using IUnsafeRenderGraphBuilder builder =
                renderGraph.AddUnsafePass(sampler.name, out DeferredBufferLightingPass pass, sampler);

            if (deferredShader != null)
            {
                pass.deferredMaterial = new Material(deferredShader);
                pass.deferredMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            pass.positionBuffer = deferredTextures.positionBuffer;
            pass.normalBuffer = deferredTextures.normalBuffer;
            pass.normalInterpolatedBuffer = deferredTextures.normalInterpolatedBuffer;
            pass.ormBuffer = deferredTextures.ormBuffer;
            pass.extrasBuffer = deferredTextures.extrasBuffer;
            builder.UseTexture(pass.positionBuffer);
            builder.UseTexture(pass.normalBuffer);
            builder.UseTexture(pass.normalInterpolatedBuffer);
            builder.UseTexture(pass.ormBuffer);
            builder.UseTexture(pass.extrasBuffer);
            
            pass.depthBuffer = textures.depthAttachment;
            pass.albedoBuffer = textures.colorAttachment;
            builder.UseTexture(pass.depthBuffer, AccessFlags.ReadWrite);
            builder.UseTexture(pass.albedoBuffer);

            pass.lightingBuffer = deferredTextures.lightingBuffer;
            builder.UseTexture(pass.lightingBuffer, AccessFlags.ReadWrite);

            builder.AllowPassCulling(false);
            builder.SetRenderFunc<DeferredBufferLightingPass>(
                static (pass, context) => pass.Render(context)
            );
        }
    }
}
