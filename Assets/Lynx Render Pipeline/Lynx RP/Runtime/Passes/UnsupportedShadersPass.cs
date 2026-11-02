
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;

namespace LynxRP
{
    public class UnsupportedShadersPass
    {
        #if UNITY_EDITOR
            static readonly ProfilingSampler sampler = new("Unsupported Shaders");

            static ShaderTagId[] shaderTagIDs = {
                new ShaderTagId("Always"),
                new ShaderTagId("ForwardBase"),
                new ShaderTagId("PrepassBase"),
                new ShaderTagId("Vertex"),
                new ShaderTagId("VertexLMRGBM"),
                new ShaderTagId("VertexLM")
            };

            static Material errorMaterial;

            RendererListHandle list;

            void Render(UnsafeGraphContext context) 
            {
                context.cmd.DrawRendererList(list);
            }

        #endif

        [Conditional("UNITY_EDITOR")]
        public static void Record(
            RenderGraph renderGraph, Camera camera, CullingResults cullingResults, in CameraRendererTextures textures
        )
        {
            #if UNITY_EDITOR
                using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(
                    sampler.name, out UnsupportedShadersPass pass, sampler
                );
                if (errorMaterial == null)
                {
                    errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
                }

                pass.list = renderGraph.CreateRendererList(
                    new RendererListDesc(shaderTagIDs, cullingResults, camera)
                    {
                        overrideMaterial = errorMaterial,
                        renderQueueRange = RenderQueueRange.all
                    }
                );
                builder.UseRendererList(pass.list);
                builder.UseTexture(textures.colorAttachment, AccessFlags.ReadWrite);
                builder.UseTexture(textures.depthAttachment, AccessFlags.ReadWrite);
                
                builder.SetRenderFunc<UnsupportedShadersPass>(
                    static (pass, context) => pass.Render(context)
                );
            #endif
        }
    }
}
