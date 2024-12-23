using UnityEngine;
using UnityEngine.Rendering;

namespace LynxRP
{
    public class PostFXStack
    {
        public enum Pass
        {
            BloomAdd,
            BloomHorizontal,
            BloomPrefilter,
            BloomPrefilterFireflies,
            BloomScatter,
            BloomScatterFinal,
            BloomVertical,
            Copy,
            ColorGradingNone,
            ColorGradingACES,
            ColorGradingNeutral,
            ColorGradingReinhard,
            ApplyColorGrading,
            ApplyColorGradingWithLuma,
            FinalRescale,
            FXAA,
            FXAAWithLuma
        }

        public static readonly int
            fxSourceId = Shader.PropertyToID("_PostFXSource"),
            fxSource2Id = Shader.PropertyToID("_PostFXSource2"),
            finalSrcBlendId = Shader.PropertyToID("_FinalSrcBlend"),
            finalDstBlendId = Shader.PropertyToID("_FinalDstBlend");

        static readonly Rect fullViewRect = new Rect(0f, 0f, 1f, 1f);

        public CameraBufferSettings BufferSettings { get; set; }
        
        public Vector2Int BufferSize { get; set; }

        public Camera Camera { get; set; }
        
        public CameraSettings.FinalBlendMode FinalBlendMode { get; set; }
        
        public PostFXSettings Settings { get; set; }

        public void Draw(CommandBuffer buffer, RenderTargetIdentifier to, Pass pass)
        {
            buffer.SetRenderTarget(
                to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
            buffer.DrawProcedural(
                Matrix4x4.identity, Settings.Material, (int)pass,
                MeshTopology.Triangles, 3
            );
        }

        public void Draw(
            CommandBuffer buffer,
            RenderTargetIdentifier from, 
            RenderTargetIdentifier to, 
            Pass pass
        )
        {
            buffer.SetGlobalTexture(fxSourceId, from);
            buffer.SetRenderTarget(
                to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store
            );
            buffer.DrawProcedural(
                Matrix4x4.identity, Settings.Material, (int)pass,
                MeshTopology.Triangles, 3
            );
        }

        public void DrawFinal(
            CommandBuffer buffer,
            RenderTargetIdentifier from, 
            Pass pass
        )
        {
            buffer.SetGlobalFloat(finalSrcBlendId, (float)FinalBlendMode.source);
            buffer.SetGlobalFloat(finalDstBlendId, (float)FinalBlendMode.destination);
            buffer.SetGlobalTexture(fxSourceId, from);
            buffer.SetRenderTarget(
                BuiltinRenderTextureType.CameraTarget,
                FinalBlendMode.destination == BlendMode.Zero && 
                Camera.rect == fullViewRect ?
                    RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load,
                RenderBufferStoreAction.Store
            );
            buffer.SetViewport(Camera.pixelRect);
            buffer.DrawProcedural(
                Matrix4x4.identity, Settings.Material, (int)pass, MeshTopology.Triangles, 3
            );
        }
    }
}
