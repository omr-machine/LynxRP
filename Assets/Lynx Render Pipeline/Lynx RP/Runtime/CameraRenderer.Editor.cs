using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace LynxRP
{
    // partial class CameraRenderer
    // {
        // partial void DrawGizmosBeforeFX();
        // partial void DrawGizmosAfterFX();

#if UNITY_EDITOR

        // partial void DrawGizmosBeforeFX()
        // {
        //     if (Handles.ShouldRenderGizmos())
        //     {
        //         if (useIntermediateBuffer)
        //         {
        //             Draw(depthAttachmentId, BuiltinRenderTextureType.CameraTarget, true);
        //             ExecuteBuffer();
        //         }
        //         context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
        //     }
        // }

        // partial void DrawGizmosAfterFX()
        // {
        //     if (Handles.ShouldRenderGizmos())
        //     {
        //         if (postFXStack.IsActive)
        //         {
        //             Draw(depthAttachmentId, BuiltinRenderTextureType.CameraTarget, true);
        //             ExecuteBuffer();
        //         }
        //         context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        //     }
        // }

#endif
    // }

}

