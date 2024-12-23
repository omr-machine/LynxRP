using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace LynxRP
{
    public static class CameraDebugger
    {
        enum PipelineBuffer {

        }
        const string panelNameForward = "Forward+";
        const string panelNamePipelineBuffers = "Pipeline Buffers";
        const string panelNameCulling = "Culling";

        static readonly int opacityID = Shader.PropertyToID("_DebugOpacity");

        static readonly int
            depthID = Shader.PropertyToID("_DebugDepthBuffer"),
            colorID = Shader.PropertyToID("_DebugColorBuffer");
        
        static readonly int hiZMipLevelId = Shader.PropertyToID("_DebugHiZMipLevel");

        static Material material;

        static bool showTiles;

        static bool showDepth, showStencil, showColor;

        static bool showAlbedo, showMRT;

        static bool showHiZ, showHiZDepthDifference;

        static float opacity = 0.5f;
        
        static int hiZMipLevel;
        
        public static TextureHandle depthBuffer, stencilBuffer, colorBuffer;

        public static bool IsActive =>
            (showTiles && opacity > 0f) ||
            showDepth || showStencil || showColor || showAlbedo || showMRT ||
            showHiZ || showHiZDepthDifference;

        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        public static void Initialize(Shader shader)
        {
            material = CoreUtils.CreateEngineMaterial(shader);
            DebugManager.instance.GetPanel(panelNameForward, true).children.Add(
            new DebugUI.FloatField
            {
                displayName = "Opacity",
                tooltip = "Opacity of the debug overlay.",
                min = static () => 0f,
                max = static () => 1f,
                getter = static () => opacity,
                setter = static value => opacity = value
            },
                new DebugUI.BoolField
                {
                    displayName = "Show Tiles",
                    tooltip = "Whether the debug overlay is shown.",
                    getter = static () => showTiles,
                    setter = static value => showTiles = value
                }
            );

            DebugManager.instance.GetPanel(panelNamePipelineBuffers, true).children.Add(
                new DebugUI.BoolField
                {
                    displayName = "Depth",
                    tooltip = "Depth Buffer",
                    getter = static () => showDepth,
                    setter = static value => showDepth = value
                },
                    new DebugUI.BoolField
                    {
                        displayName = "Stencil",
                        tooltip = "Stencil Buffer",
                        getter = static () => showStencil,
                        setter = static value => showStencil = value
                    },
                        new DebugUI.BoolField
                        {
                            displayName = "Color",
                            tooltip = "Color Buffer",
                            getter = static () => showColor,
                            setter = static value => showColor = value
                        },
                            new DebugUI.BoolField
                            {
                                displayName = "Deferred Albedo",
                                tooltip = "Albedo Buffer",
                                getter = static () => showAlbedo,
                                setter = static value => showAlbedo = value
                            },
                                new DebugUI.BoolField
                                {
                                    displayName = "Deferred MRTs",
                                    tooltip = "MRT Buffer",
                                    getter = static () => showMRT,
                                    setter = static value => showMRT = value
                                }
            );
            
            DebugManager.instance.GetPanel(panelNameCulling, true).children.Add(
                new DebugUI.BoolField
                {
                    displayName = "Show Hi Z Buffer",
                    tooltip = "Whether the debug overlay is shown.",
                    getter = static () => showHiZ,
                    setter = static value => showHiZ = value
                },
                    new DebugUI.IntField()
                    {
                        displayName = "Hi Z Mip Level",
                        tooltip = "Whether the debug overlay is shown.",
                        min = static () => 0,
                        max = static () => 15,
                        getter = static () => hiZMipLevel,
                        setter = static value => hiZMipLevel = value
                    },
                    new DebugUI.BoolField
                    {
                        displayName = "Show Difference Between Depth and Projected Depth",
                        tooltip = "Whether the debug overlay is shown.",
                        getter = static () => showHiZDepthDifference,
                        setter = static value => showHiZDepthDifference = value
                    }
            );
        }

        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        public static void Cleanup()
        {
            CoreUtils.Destroy(material);
            DebugManager.instance.RemovePanel(panelNameForward);
            DebugManager.instance.RemovePanel(panelNamePipelineBuffers);
            DebugManager.instance.RemovePanel(panelNameCulling);
        }

        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        public static void Render(RenderGraphContext context)
        {
            CommandBuffer buffer = context.cmd;
            int shaderPass = 0;
            buffer.SetGlobalFloat(opacityID, opacity);
            if (showDepth)
            {
                buffer.SetGlobalTexture(depthID, depthBuffer);
                shaderPass = 1;
            }
            else if (showStencil)
            {
                shaderPass = 2;
            }
            else if (showColor)
            {
                buffer.SetGlobalTexture(colorID, colorBuffer);
                shaderPass = 3;
            }
            else if (showAlbedo)
            {
                shaderPass = 4;
            }
            
            else if (showMRT)
            {
                shaderPass = 5;
            }

            else if (showHiZ)
            {
                shaderPass = 6;
                hiZMipLevel = Math.Max(0, hiZMipLevel);
                buffer.SetGlobalInteger(hiZMipLevelId, hiZMipLevel);
                buffer.SetGlobalTexture(colorID, colorBuffer);
            }
            else if (showHiZDepthDifference)
            {
                shaderPass = 7;
            }
            buffer.DrawProcedural(
                Matrix4x4.identity, material, shaderPass, MeshTopology.Triangles, 3
            );
            context.renderContext.ExecuteCommandBuffer(buffer);
            buffer.Clear();
        }
    }
}
