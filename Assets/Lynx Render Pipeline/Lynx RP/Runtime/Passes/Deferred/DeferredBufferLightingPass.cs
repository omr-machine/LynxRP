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
            new ShaderTagId("CustomLitDeferredLighting"),
            new ShaderTagId("CustomUnlitDeferredLighting")
        };

        TextureHandle 
            positionBuffer, 
            normalBuffer, normalInterpolatedBuffer, 
            ormBuffer, lightingBuffer, extrasBuffer;

        TextureHandle albedoBuffer, depthBuffer;

        Material deferredMaterial;

        void Render(RenderGraphContext context)
        {
            CommandBuffer buffer = context.cmd;

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


            context.renderContext.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }

        public static void Record(
            RenderGraph renderGraph,
            Shader deferredShader,
            in CameraRendererTextures textures,
            in DeferredRenderTextures deferredTextures
            
        )
        {
            using RenderGraphBuilder builder =
                renderGraph.AddRenderPass(sampler.name, out DeferredBufferLightingPass pass, sampler);

            if (deferredShader != null)
            {
                pass.deferredMaterial = new Material(deferredShader);
                pass.deferredMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            pass.positionBuffer = builder.ReadTexture(deferredTextures.positionBuffer);
            pass.normalBuffer = builder.ReadTexture(deferredTextures.normalBuffer);
            pass.normalInterpolatedBuffer = builder.ReadTexture(deferredTextures.normalInterpolatedBuffer);
            pass.ormBuffer = builder.ReadTexture(deferredTextures.ormBuffer);
            pass.extrasBuffer = builder.ReadTexture(deferredTextures.extrasBuffer);
            
            pass.depthBuffer = builder.ReadWriteTexture(textures.depthAttachment);
            pass.albedoBuffer = builder.ReadTexture(textures.colorAttachment);

            pass.lightingBuffer = builder.ReadWriteTexture(deferredTextures.lightingBuffer);

            builder.AllowPassCulling(false);
            builder.SetRenderFunc<DeferredBufferLightingPass>(
                static (pass, context) => pass.Render(context)
            );
        }
    }
}
