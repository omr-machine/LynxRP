using System;
using UnityEditor;
using UnityEngine;

namespace LynxRP
{
    [CreateAssetMenu(menuName = "Rendering/Custom Post FX Settings")]
    public class PostFXSettings : ScriptableObject
    {
        [SerializeField]
        Shader shader = default;

        [NonSerialized]
        Material material;

        [Serializable]
        public struct BloomSettings
        {
            public bool ignoreRenderScale;
            
            [Range(0f, 16f)]
            public int maxIterations;

            [Min(1f)]
            public int downscaleLimit;

            public bool bicubicUpsampling;

            [Min(0f)]
            public float threshold;

            [Range(0f, 1f)]
            public float thresholdKnee;

            [Min(0f)]
            public float intensity;
            
            public bool fadeFireflies;

            public enum Mode { Additive, Scattering }

            public Mode mode;

            [Range(0.05f, 0.95f)]
            public float scatter;
        }

        [Serializable]
        public struct ToneMappingSettings
        {
            public enum Mode { None, ACES, Neutral, Reinhard }

            public Mode mode;
        }

        [Serializable]
        public struct ColorAdjustmentsSettings {
            public float postExposure;

            [Range(-100f, 100f)]
            public float contrast;

            [ColorUsage(false, true)]
            public Color colorFilter;

            [Range(-180f, 180f)]
            public float hueShift;

            [Range(-100f, 100f)]
            public float saturation;
        }

        [Serializable]
        public struct WhiteBalanceSettings
        {
            [Range(-100f, 100f)]
            public float temperature, tint;
        }

        [Serializable]
        public struct SplitToningSettings
        {
            [ColorUsage(false)]
            public Color shadows, highlights;

            [Range(-100f, 100f)]
            public float balance;
        }

        [Serializable]
        public struct ChannelMixerSettings
        {

            public Vector3 red, green, blue;
        }

        [Serializable]
        public struct ShadowsMidtonesHighlightsSettings
        {

            [ColorUsage(false, true)]
            public Color shadows, midtones, highlights;

            [Range(0f, 2f)]
            public float shadowsStart, shadowsEnd, highlightsStart, highLightsEnd;
        }

        [SerializeField]
        BloomSettings bloom = new()
        {
            scatter = 0.7f
        };

        [SerializeField]
        ToneMappingSettings toneMapping = default;

        [SerializeField]
        ColorAdjustmentsSettings colorAdjustments = new()
        {
            colorFilter = Color.white
        };

        [SerializeField]
        WhiteBalanceSettings whiteBalance = default;

        [SerializeField]
        SplitToningSettings splitToning = new()
        {
            shadows = Color.gray,
            highlights = Color.gray
        };

        [SerializeField]
        ChannelMixerSettings channelMixer = new()
        {
            red = Vector3.right,
            green = Vector3.up,
            blue = Vector3.forward
        };

        [SerializeField]
        ShadowsMidtonesHighlightsSettings shadowsMidtonesHighlights = new()
        {
            shadows = Color.white,
            midtones = Color.white,
            highlights = Color.white,
            shadowsEnd = 0.3f,
            highlightsStart = 0.55f,
            highLightsEnd = 1f
        };

        public BloomSettings Bloom => bloom;

        public ToneMappingSettings ToneMapping => toneMapping;
        
        public ColorAdjustmentsSettings ColorAdjustments => colorAdjustments;

        public WhiteBalanceSettings WhiteBalance => whiteBalance;

        public SplitToningSettings SplitToning => splitToning;

        public ChannelMixerSettings ChannelMixer => channelMixer;


        public ShadowsMidtonesHighlightsSettings ShadowsMidtonesHighlights =>
            shadowsMidtonesHighlights;

        public Material Material
        {
            get 
            {
                if (material == null && shader != null)
                {
                    material = new Material(shader);
                    material.hideFlags = HideFlags.HideAndDontSave;
                }
                return material;
            }
        }

        public static bool AreApplicableTo(Camera camera)
        {
            #if UNITY_EDITOR
                if (camera.cameraType == CameraType.SceneView &&
                    !SceneView.currentDrawingSceneView.sceneViewState.showImageEffects
                )
                {
                    return false;
                }
            #endif
            return camera.cameraType <= CameraType.SceneView;
        }
    }    
}
