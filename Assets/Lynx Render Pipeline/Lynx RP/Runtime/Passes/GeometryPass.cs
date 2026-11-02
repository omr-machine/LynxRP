using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

namespace LynxRP
{
    public class GeometryPass
    {
        static readonly ProfilingSampler
            samplerOpaque = new("Opaque Geometry"),
            samplerTransparent = new("Transparent Geometry");

        static readonly ShaderTagId[] shaderTagIDs = {
                new ShaderTagId("SRPDefaultUnlit"),
                new ShaderTagId("CustomLit")
            };

        RendererListHandle list;

        void Render(UnsafeGraphContext context)
        {
            context.cmd.DrawRendererList(list);
        }

        public static void Record(
            RenderGraph renderGraph, 
            Camera camera, 
            CullingResults cullingResults,
            uint renderingLayerMask, 
            bool opaque,
            in CameraRendererTextures textures,
            in LightResources lightData
        )
        {
            ProfilingSampler sampler = opaque ? samplerOpaque : samplerTransparent;

            using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(
                sampler.name, out GeometryPass pass, sampler
            );
            
            //PerObjectData perObjectData2 = (PerObjectData)0b_1110_0001_1110;
            pass.list = renderGraph.CreateRendererList(
                new RendererListDesc(shaderTagIDs, cullingResults, camera)
                {
                    sortingCriteria = opaque ?
                        SortingCriteria.CommonOpaque : SortingCriteria.CommonTransparent,
                    rendererConfiguration = 
                        PerObjectData.ReflectionProbes |
                        PerObjectData.Lightmaps |
                        PerObjectData.ShadowMask |
                        PerObjectData.LightProbe |
                        PerObjectData.OcclusionProbe |
                        PerObjectData.LightProbeProxyVolume |
                        PerObjectData.OcclusionProbeProxyVolume,// |
                        //PerObjectData.LightData | PerObjectData.LightIndices, // lightsPerObject
                    // perObjectData = perObjectData2
                    renderQueueRange = opaque ?
                        RenderQueueRange.opaque : RenderQueueRange.transparent,
                    renderingLayerMask = renderingLayerMask
                }
            );
            builder.UseRendererList(pass.list);

            builder.UseTexture(textures.colorAttachment, AccessFlags.ReadWrite);
            builder.UseTexture(textures.depthAttachment, AccessFlags.ReadWrite);
            if(!opaque)
            {
                if (textures.colorCopy.IsValid())
                {
                    builder.UseTexture(textures.colorCopy);
                }
                if (textures.depthCopy.IsValid())
                {
                    builder.UseTexture(textures.depthCopy);
                }
            }
            
            builder.UseBuffer(lightData.directionalLightDataBuffer);
            builder.UseBuffer(lightData.otherLightDataBuffer);
            // if (lightData.tilesBuffer.IsValid())
            {
                builder.UseBuffer(lightData.tilesBuffer);
            }
            builder.UseTexture(lightData.shadowResources.directionalAtlas);
            builder.UseTexture(lightData.shadowResources.otherAtlas);
            builder.UseBuffer(
                lightData.shadowResources.directionalShadowCascadesBuffer
            );
            builder.UseBuffer(
                lightData.shadowResources.directionalShadowMatricesBuffer
            );
            builder.UseBuffer(
                lightData.shadowResources.otherShadowDataBuffer
            );

            builder.SetRenderFunc<GeometryPass>(
                static (pass, context) => pass.Render(context)
            );
        }
    }
}
