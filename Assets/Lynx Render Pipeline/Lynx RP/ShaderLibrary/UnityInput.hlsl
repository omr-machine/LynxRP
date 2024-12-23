#ifndef CUSTOM_UNITY_INPUT_INCLUDED
#define CUSTOM_UNITY_INPUT_INCLUDED

CBUFFER_START(UnityPerDraw)
    float4x4 unity_ObjectToWorld;
    float4x4 unity_WorldToObject;
    float4 unity_LODFade;
    real4 unity_WorldTransformParams;

	float4 unity_RenderingLayer;

	// real4 unity_LightData;
	// real4 unity_LightIndices[2];

	float4 unity_ProbesOcclusion;

	float4 unity_SpecCube0_HDR;

	float4 unity_LightmapST;
	float4 unity_DynamicLightmapST;

	float4 unity_SHAr;
	float4 unity_SHAg;
	float4 unity_SHAb;
	float4 unity_SHBr;
	float4 unity_SHBg;
	float4 unity_SHBb;
	float4 unity_SHC;

	float4 unity_ProbeVolumeParams;
	float4x4 unity_ProbeVolumeWorldToObject;
	float4 unity_ProbeVolumeSizeInv;
	float4 unity_ProbeVolumeMin;
    
CBUFFER_END

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 unity_MatrixInvV;
float4x4 unity_prev_MatrixM;
float4x4 unity_prev_MatrixIM;
float4x4 glstate_matrix_projection;

float4x4 unity_CameraInvProjection;

float3 _WorldSpaceCameraPos;

float4 unity_OrthoParams;
float4 _ProjectionParams;
float4 _ScreenParams;
float4 _ScaledScreenParams; // ??
float4 _ZBufferParams;

float4 _FrustumCornersWS[8];
float4 _FrustumPlanesWS[6];

float4x4 _MatrixM;
float4x4 _MatrixV;
float4x4 _MatrixVInv;
float4x4 _MatrixP;
float4x4 _MatrixPInv;
float4x4 _MatrixVP;
float4x4 _MatrixVPInv;

float3 _CameraPosition;
float _NearPlane;
float _FarPlane;

#endif

// https://github.com/phi-lira/CustomSRP/blob/master/ShaderLibrary/ShaderVariables.hlsl