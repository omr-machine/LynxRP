using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LynxRP
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(Light))]
    [SupportedOnRenderPipeline(typeof(LynxRenderPipelineAsset))]
    public class LynxLightEditor : LightEditor
    {
        
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (
                !settings.lightType.hasMultipleDifferentValues && 
                (LightType)settings.lightType.enumValueIndex == LightType.Spot
            )
            {
                settings.DrawInnerAndOuterSpotAngle();
                // settings.ApplyModifiedProperties();
            }

            settings.ApplyModifiedProperties();

            var light = target as Light;
            if (light.cullingMask != -1)
            {
                EditorGUILayout.HelpBox(
                    "Culling Mask only affects shadows.", MessageType.Warning
                );
            }
        }
    }
}

