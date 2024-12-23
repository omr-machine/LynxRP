using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace LynxRP
{
    [Serializable]
    public class CameraSettings
    {
        [Flags]
        public enum CullSettings 
        { 
            None = 0, Frustum = 1, Orientation = 2, Small = 4, HiZ = 8
        }

        public CullSettings cullSettings = CullSettings.None;
        
        public bool copyColor = true, copyDepth = true;

        // [RenderingLayerMaskField]
        [HideInInspector, Obsolete("Use newRenderingLayerMask instead.")]
        public int renderingLayerMask = -1;

        public RenderingLayerMask newRenderingLayerMask = -1;

        public bool maskLights = false;

        public enum RenderScaleMode { Inherit, Multiply, Override }

        public RenderScaleMode renderScaleMode = RenderScaleMode.Inherit;

        [Range(CameraRenderer.renderScaleMin, CameraRenderer.renderScaleMax)]
        public float renderScale = 1f;

        public bool overridePostFX = false;

        public PostFXSettings postFXSettings = default;

        public bool allowFXAA = false;

        public bool allowMSAA = false;

        public bool keepAlpha = false;

        [Serializable]
        public struct FinalBlendMode
        {
            public BlendMode source, destination;
        }

        public FinalBlendMode finalBlendMode = new FinalBlendMode
        {
            source = BlendMode.One,
            destination = BlendMode.Zero
        };

        public float GetRenderScale (float scale)
        {
            return
                renderScaleMode == RenderScaleMode.Inherit ? scale :
                renderScaleMode == RenderScaleMode.Override ? renderScale :
                scale * renderScale;
        }

        public CullSettings CullSettingsMax()
            {
                return (
                    CullSettings.Frustum | CullSettings.Orientation | 
                    CullSettings.Small   | CullSettings.HiZ
                );
            }
    }
}
