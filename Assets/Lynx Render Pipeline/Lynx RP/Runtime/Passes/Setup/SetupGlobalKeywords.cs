
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;

namespace LynxRP
{
    partial class SetupPass
    {
        static readonly int 
            attachmentSizeID = Shader.PropertyToID("_CameraBufferSize"),
            scaledScreenParmsID = Shader.PropertyToID("_ScaledScreenParams");

        private static readonly int
            matrixMId = Shader.PropertyToID("_MatrixM"),
            matrixVId = Shader.PropertyToID("_MatrixV"),
            matrixVInvId = Shader.PropertyToID("_MatrixVInv"),
            matrixPId = Shader.PropertyToID("_MatrixP"),
            matrixPInvId = Shader.PropertyToID("_MatrixPInv"),
            matrixVPId = Shader.PropertyToID("_MatrixVP"),
            matrixVPInvId = Shader.PropertyToID("_MatrixVPInv"),
            nearPlaneId = Shader.PropertyToID("_NearPlane"),
            farPlaneId = Shader.PropertyToID("_FarPlane"),
            cameraPositionId = Shader.PropertyToID("_CameraPosition");

        private static readonly int
            frustumCornersVSId = Shader.PropertyToID("_FrustumCornersVS"),
            frustumCornersWSId = Shader.PropertyToID("_FrustumCornersWS"),
            frustumPlanesWSId = Shader.PropertyToID("_FrustumPlanesWS");

        private static readonly int
            screenParamsId = Shader.PropertyToID("_ScreenParamsCull"),
            cullFrustumId = Shader.PropertyToID("_TriCullFrustum"),
            cullOrientationId = Shader.PropertyToID("_TriCullOrientation"),
            cullSmallId = Shader.PropertyToID("_TriCullSmall"),
            cullHiZId = Shader.PropertyToID("_TriCullHiZ");

        struct CameraMatrices
        {
            public Matrix4x4 matM, matV, matGLP, matVP;
            public Matrix4x4 matGLV, matGLVP;
        }

        CameraMatrices matrices = new();

        void GetFrustumCorners(ref Vector4[] frustumCorners, ref Matrix4x4 mat)
        {
            Rect rect = new(0, 0, 1, 1);
            Vector3[] frustumCornersNear = new Vector3[4];
            Vector3[] frustumCornersFar = new Vector3[4];
            camera.CalculateFrustumCorners(rect, camera.nearClipPlane, Camera.MonoOrStereoscopicEye.Mono, frustumCornersNear);
            camera.CalculateFrustumCorners(rect, camera.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, frustumCornersFar);
            frustumCorners[0] = frustumCornersNear[0]; frustumCorners[1] = frustumCornersNear[1];
            frustumCorners[2] = frustumCornersNear[2]; frustumCorners[3] = frustumCornersNear[3];
            frustumCorners[4] = frustumCornersFar[0];  frustumCorners[5] = frustumCornersFar[1];
            frustumCorners[6] = frustumCornersFar[2];  frustumCorners[7] = frustumCornersFar[3];

            DebugNDC(ref mat, ref frustumCornersNear, ref frustumCornersFar);
        }

        void GetFrustumPlanes(ref Vector4[] frustumPlanes, ref Matrix4x4 mat)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(mat);
            int[] indexes = { 4, 5, 0, 1, 2, 3 };
            for (int i = 0; i < 6; i++)
            {
                int k = indexes[i];
                frustumPlanes[i] = new float4(planes[k].normal, planes[k].distance);
            }
        }

        void SetTargetKeywords(CommandBuffer buffer, Vector2Int attachmentSize)
        {
            buffer.SetGlobalVector(attachmentSizeID, new Vector4(
                1f / attachmentSize.x, 1f / attachmentSize.y,
                attachmentSize.x, attachmentSize.y
            ));
            buffer.SetGlobalVector(scaledScreenParmsID, new Vector4(
                attachmentSize.x, attachmentSize.y,
                1.0f + 1.0f / attachmentSize.x, 1.0f + 1.0f / attachmentSize.y
            ));
        }

        void SetCameraKeywords(CommandBuffer buffer, Camera camera)
        {
            buffer.SetGlobalVector(cameraPositionId, camera.transform.position);
            buffer.SetGlobalFloat(nearPlaneId, camera.nearClipPlane);
            buffer.SetGlobalFloat(farPlaneId, camera.farClipPlane);
        }

        void SetCameraMatrixKeywords(CommandBuffer buffer, Camera camera) 
        {
            matrices.matM    = camera.transform.localToWorldMatrix;
            matrices.matV    = camera.transform.worldToLocalMatrix;
            matrices.matGLV  = camera.worldToCameraMatrix;
            matrices.matGLP  = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
            matrices.matVP   = matrices.matGLP * matrices.matV;
            matrices.matGLVP = matrices.matGLP * matrices.matGLV;

            buffer.SetGlobalMatrix(matrixMId, matrices.matM);
            buffer.SetGlobalMatrix(matrixVId, matrices.matGLV);
            buffer.SetGlobalMatrix(matrixVInvId, matrices.matGLV.inverse);
            buffer.SetGlobalMatrix(matrixPId, matrices.matGLP);
            buffer.SetGlobalMatrix(matrixPInvId, matrices.matGLP.inverse);
            buffer.SetGlobalMatrix(matrixVPId, matrices.matGLVP);
            buffer.SetGlobalMatrix(matrixVPInvId, matrices.matGLVP.inverse);
        }

        void SetFrustumKeywords(CommandBuffer buffer, Camera camera)
        {
            Vector4[] frustumCornersVS = new Vector4[8];
            Vector4[] frustumPlanes = new Vector4[6];

            GetFrustumCorners(ref frustumCornersVS, ref matrices.matGLP);
            GetFrustumPlanes(ref frustumPlanes, ref matrices.matVP);

            Vector4[] frustumCornersWS = new Vector4[8];
            for (int i = 0; i < frustumCornersVS.Length; ++i)
            {
                frustumCornersWS[i] = camera.transform.TransformPoint(frustumCornersVS[i]);
            }

            buffer.SetGlobalVectorArray(frustumCornersVSId, frustumCornersVS);
            buffer.SetGlobalVectorArray(frustumCornersWSId, frustumCornersWS);
            buffer.
            
            SetGlobalVectorArray(frustumPlanesWSId, frustumPlanes);
        }

        public void SetCullKeywords(CommandBuffer buffer, Vector2Int attachmentSize, int cullSettings)
        {
            Vector4 screenParams = new Vector4(
                attachmentSize.x, attachmentSize.y,
                1.0f / attachmentSize.x, 1.0f / attachmentSize.y
            );
            buffer.SetGlobalVector(screenParamsId, screenParams);

            int triCullFrustum     = (cullSettings & (int)CameraSettings.CullSettings.Frustum)     != 0 ? 1 : 0;
            int triCullOrientation = (cullSettings & (int)CameraSettings.CullSettings.Orientation) != 0 ? 1 : 0;
            int triCullSmall       = (cullSettings & (int)CameraSettings.CullSettings.Small)       != 0 ? 1 : 0;
            int triCullHiZ         = (cullSettings & (int)CameraSettings.CullSettings.HiZ)         != 0 ? 1 : 0;
            buffer.SetGlobalInt(cullFrustumId, triCullFrustum);
            buffer.SetGlobalInt(cullOrientationId, triCullOrientation);
            buffer.SetGlobalInt(cullSmallId, triCullSmall);
            buffer.SetGlobalInt(cullHiZId, triCullHiZ);
        }

        void SetKeywords(CommandBuffer buffer, Camera camera, Vector2Int attachmentSize, int cullSettings)
        {
            SetTargetKeywords(buffer, attachmentSize);
            SetCameraKeywords(buffer, camera);
            SetCameraMatrixKeywords(buffer, camera);
            SetFrustumKeywords(buffer, camera);
            SetCullKeywords(buffer, attachmentSize, cullSettings);
        }

        void DebugNDC(ref Matrix4x4 P, ref Vector3[] frustumCornersNear, ref Vector3[] frustumCornersFar)
        {
            IterateCorners(frustumCornersNear, P);
            // IterateCorners(frustumCornersFar, P);

            static void IterateCorners(Vector3[] frustumCorners, Matrix4x4 mat)
            {
                foreach (var frustumCorner in frustumCorners)
                {
                    float3 corner = frustumCorner;
                    float4 posCS = new float4(corner, 1);
                    posCS = mat * posCS;
                    float3 posNDC = posCS.xyz / posCS.w;
                    posNDC.xy *= -1;
                    // Debug.Log(posNDC);
                    float4 posNDC4 = posCS * 0.5f;
                    posNDC4.xy = new float2(posNDC4.x, posNDC4.y * -1) + posNDC4.w;
                    posNDC4.zw = posCS.zw;
                    float3 posSS = posNDC4.xyz / posNDC4.w;
                    // Debug.Log(posSS);
                }
            }
        }
    }
}
