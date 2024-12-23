#ifndef CUSTOM_SPACE_TRANSFORMATIONS_INCLUDED
#define CUSTOM_SPACE_TRANSFORMATIONS_INCLUDED

void GetFarClipNDC(out float farClip)
{
    farClip = 1.0;
    if (UNITY_NEAR_CLIP_VALUE != 0) // Reversed Z
    {
        farClip = 0.0;
    }
}

void GetNDCMinMax(out float3 min, out float3 max)
{
    float farClip; GetFarClipNDC(farClip);
    min = float3(-1, -1, UNITY_NEAR_CLIP_VALUE);
    max = float3(1, 1, farClip);
	// positionWS = NDCToWorld(clipMin, -_NearPlane) + _CameraPosition * 2;
}

bool PositionSSInBounds(float3 positionSS)
{
	bool inBounds =
		1 >= positionSS.z && positionSS.z >= 0.0 &&
		0 <  positionSS.x && positionSS.x <  1 &&
		0 <  positionSS.y && positionSS.y <  1;
	return inBounds;
}

float LinearizeDepth(float depth, float near, float far) {
	float depthLinear;
	if (UNITY_NEAR_CLIP_VALUE != 0)
		depthLinear = (near * far) / (far - depth * (far - near));
	else
		depthLinear = (2.0 * near * far) / (far + near - depth * (far - near));
	
	return depthLinear;
}

float3 WorldToView(float3 positionWS)
{
	float3 positionVS = mul(_MatrixV, float4(positionWS.xyz, 1.0)).xyz; // - _CameraPosition
	return positionVS;
}

float3 ViewToWorld(float3 positionVS)
{
	float3 positionWS = mul(_MatrixVInv, float4(positionVS, 1.0)).xyz;
	return positionWS;
}

float4 ViewToHClip(float3 positionVS)
{
	float4 positionCS = mul(_MatrixP, float4(positionVS.xyz, 1.0));
	positionCS.y *= _ProjectionParams.x * -1;
	return positionCS;
}

float3 HClipToView(float4 positionCS)
{
	positionCS.y *= _ProjectionParams.x * -1;
	float3 positionVS = mul(_MatrixPInv, positionCS).xyz;
	return positionVS;
}

float3 HClipToNDC(float4 positionCS)
{
	float3 positionNDC = positionCS.xyz;
	positionNDC /= positionCS.w;
	return positionNDC;
}

float4 NDCToHClip(float3 positionNDC, float w)
{
	
    positionNDC *= w;
	float4 positionCS = float4(positionNDC, w);
    return positionCS;
}

float3 NDCToScreen(float3 positionNDC)
{ 
	float3 positionSS;
    positionSS.x = (positionNDC.x + 1.0) * 0.5;
    positionSS.y = (positionNDC.y + 1.0) * 0.5;
    positionSS.z = positionNDC.z;
	return positionSS;
}

float3 ScreenToNDC(float3 positionSS)
{
	float3 positionNDC;
	positionNDC.x = (positionSS.x * 2.0) - 1.0;;
	positionNDC.y = (positionSS.y * 2.0) - 1.0;;
	positionNDC.z = positionSS.z;
	return positionNDC;
}

float3 WorldToNDC(float3 positionWS, float4x4 matVP)
{
    float4 positionCS = mul(matVP, float4(positionWS, 1.0));
    positionCS.y *= _ProjectionParams.x * -1;
    float3 positionNDC = positionCS.xyz;
    positionNDC /= positionCS.w;
    return positionNDC;
}

float3 NDCToWorld(float3 positionNDC, float4x4 matVPI, float w)
{
    positionNDC *= w;
	float4 positionCS = float4(positionNDC, w);
	positionCS.y *= _ProjectionParams.x * -1;
	float3 positionWS = mul(matVPI, positionCS).xyz;
	return positionWS;
}

float3 HClipToWorld(float4 positionCS)
{
	// float4x4 matVPI = mul(unity_CameraInvProjection, unity_MatrixInvV);
	float3 positionWS = mul(_MatrixVPInv, positionCS).xyz;
	return positionWS;
}

#endif

/*
Transformation Pipeline:
World → [View Matrix] → View → [Projection Matrix] → Clip/Homogeneous → [Perspective Divide] → NDC → [Viewport Transform] → Screen Space


- World position × View matrix = View space/Camera space/Local Space
- View space × Projection matrix = Clip space/Homogeneous coordinates
- Clip space after perspective divide (xyz/w) = NDC space
- NDC Space after multiply with screen pixels = Screen space


1. View space is the camera's local space
2. When you multiply by just VP matrix, you get clip space coordinates - the W component is still present and perspective divide hasn't happened yet
3. Clip space coordinates are in homogeneous coordinates - meaning they include the w component which is used for the perspective divide
4. Clip space W is the depth
5. To get to NDC, you need to perform the perspective divide (dividing x,y,z by w)
6. So NDC Space positions are going to be (x/w, y/w, z/w, 1), with the 1 still there


Ranges:
- Clip space: x,y,z coordinates can be outside [-w,w] range
- NDC (after perspective divide): coordinates are normalized to [-1,1] range
- View space: coordinates are relative to camera position/orientation

- The viewport transform converts from NDC [-1,1] range to screen/pixel coordinates
*/