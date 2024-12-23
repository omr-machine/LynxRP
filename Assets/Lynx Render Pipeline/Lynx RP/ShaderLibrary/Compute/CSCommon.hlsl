#ifndef CUSTOM_CS_COMMON_INCLUDED
#define CUSTOM_CS_COMMON_INCLUDED
#include <HLSLSupport.cginc>
#include <UnityShaderVariables.cginc>

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

float4 _FrustumCornersVS[8];
float4 _FrustumCornersWS[8];
float4 _FrustumPlanesWS[6];

bool CheckForNaNOrInfinity(float3 a, float number)
{
    if (!(a.x < 0.f || a.x > 0.f || a.x == 0.f))
        return true;
    if (!(a.y < 0.f || a.y > 0.f || a.y == 0.f))
        return true;
    if (!(a.z < 0.f || a.z > 0.f || a.z == 0.f))
        return true;
    // 1/0 infinity
    if (a.x >= number || a.y >= number || a.z >= number)
        return true;

    if (a.x <= -number || a.y <= -number || a.x <= -number)
        return true;

    return false;
}

float getNaNSqrt(float value) {
	return sqrt(value);
}

float invLerp(float from, float to, float value) {
    return (value - from) / (to - from);
}

float remap(float origFrom, float origTo, float targetFrom, float targetTo, float value){
    float rel = invLerp(origFrom, origTo, value);
    return lerp(targetFrom, targetTo, rel);
}

// float Remap(float origFrom, float origTo, float targetFrom, float targetTo, float value)
// {
//     return lerp(targetFrom, targetTo, (value - origFrom) / (origTo - origFrom));
// }

#endif