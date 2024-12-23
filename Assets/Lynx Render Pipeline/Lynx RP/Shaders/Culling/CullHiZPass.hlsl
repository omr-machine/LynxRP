#ifndef CUSTOM_UNLIT_PASS_INCLUDED
#define CUSTOM_UNLIT_PASS_INCLUDED

struct Varyings 
{
	float4 positionCS_SS : SV_POSITION;
	float2 screenUV : VAR_SCREEN_UV;
};

float4 _InvSize;
// float4 _CameraBufferSize;

Texture2D<float4> _HiZDepth;

Varyings HiZPassVertex (uint vertexID : SV_VertexID)
{
	Varyings output;
	output.positionCS_SS = float4(
		vertexID <= 1 ? -1.0 : 3.0,
		vertexID == 1 ? 3.0 : -1.0,
		0.0, 1.0
	);
	output.screenUV = float2(
		vertexID <= 1 ? 0.0 : 2.0,
		vertexID == 1 ? 2.0 : 0.0
	);

	if (_ProjectionParams.x < 0.0)
	{
		output.screenUV.y = 1.0 - output.screenUV.y;
	}
	return output;
}

float HiZReduce(float2 uv, float2 invSize)
{
	float2 uvLB = float2(-0.25, -0.25);
	float2 uvLT = float2(-0.25, 0.25);
	float2 uvRB = float2(0.25, -0.25);
	float2 uvRT = float2(0.25, 0.25);

	float depthLB = SAMPLE_TEXTURE2D_LOD(_HiZDepth, sampler_point_clamp, uvLB, 1).x;
	float depthLT = SAMPLE_TEXTURE2D_LOD(_HiZDepth, sampler_point_clamp, uvLT, 1).x;
	float depthRB = SAMPLE_TEXTURE2D_LOD(_HiZDepth, sampler_point_clamp, uvRB, 1).x;
	float depthRT = SAMPLE_TEXTURE2D_LOD(_HiZDepth, sampler_point_clamp, uvRT, 1).x;

	float depth;
	#if defined(UNITY_REVERSED_Z)
		depth = min(min(depthLB, depthLT), min(depthRB, depthRT));
	#else
		depth = max(max(depthLB, depthLT), max(depthRB, depthRT));
	#endif
	
	return depth;
}

float4 HiZPassFragment (Varyings input) : SV_TARGET
{
	float2 invSize = _InvSize.xy;
	float2 screenUV = input.screenUV;

	float depth = HiZReduce(screenUV, invSize);

	return float4(depth, 0.0, 0.0, 1.0);
}

#endif