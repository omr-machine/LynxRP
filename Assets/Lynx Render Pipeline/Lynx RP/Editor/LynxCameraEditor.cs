using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace LynxRP
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(Camera))]
    [SupportedOnRenderPipeline(typeof(LynxRenderPipelineAsset))]
    public class LynxCameraEditor : Editor { }
}

