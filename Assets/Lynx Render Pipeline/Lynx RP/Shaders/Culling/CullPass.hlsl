#ifndef CUSTOM_CULL_PASS_INCLUDED
#define CUSTOM_CULL_PASS_INCLUDED

struct Varyings
{
    float4 positionCS_SS : SV_POSITION;
    float3 positionWS : VAR_POSITION;
    float3 normalWS : VAR_NORMAL;
	float4 color : VAR_COLOR;
    float2 baseUV : VAR_BASE_UV;
	uint baseId : VAR_ID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Vertex
{
	float3 position;
	float3 normal;
	float4 color;
	float2 baseUV;
};

StructuredBuffer<Vertex> _VertexPassBuffer;

Varyings CullPassVertex (uint id : SV_VertexID)
{
	Varyings output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	output.positionWS = _VertexPassBuffer[id].position;
	output.positionCS_SS = TransformWorldToHClip(output.positionWS);
	output.normalWS = _VertexPassBuffer[id].normal;
	output.color = _VertexPassBuffer[id].color;
	output.baseUV = _VertexPassBuffer[id].baseUV;
	output.baseId = id;
	
	return output;
}

TEXTURE2D(Result);
TEXTURE2D(_CullDebugTexture);

float4 CullPassFragment (Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
	Fragment fragment = GetFragment(input.positionCS_SS);

	float3 color = input.normalWS;// _VertexPassBuffer[input.baseId].normal;
	color = color * 0.5 + 0.5;
	color *= 0.1;
	
	float4 vOverlay = SAMPLE_TEXTURE2D_LOD(_CullDebugTexture, sampler_point_clamp, fragment.screenUV, 0);
	color += vOverlay.xyz * 2;
	
    return float4(color.rgb, input.color.a);
}

//------------------------------------------------------------
// Line
struct Line
{
	float3 position;
	float3 color;
};

StructuredBuffer<Line> _BBoxPassBuffer;

struct VaryingsLine
{
	float4 positionCS_SS : SV_POSITION;
	float3 positionWS : VAR_POSITION;
	float4 color : VAR_COLOR;
	uint baseId : VAR_ID;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};


VaryingsLine CullLinePassVertex (uint id : SV_VertexID)
{
	VaryingsLine output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	output.positionCS_SS = float4(_BBoxPassBuffer[id].position, 1.0);
	output.positionWS = _BBoxPassBuffer[id].position;
	output.positionCS_SS = TransformWorldToHClip(output.positionWS);
	output.color = float4(_BBoxPassBuffer[id].color, 1.0);
	output.baseId = id;
	
	return output;
}

float4 CullLinePassFragment (VaryingsLine input) : SV_TARGET
{
	UNITY_SETUP_INSTANCE_ID(input);
	Fragment fragment = GetFragment(input.positionCS_SS);

	float3 color = input.color.rgb;
	color = color * 0.5 + 0.5;
	color *= color;
	float4 vOverlay = SAMPLE_TEXTURE2D_LOD(_CullDebugTexture, sampler_point_clamp, fragment.screenUV, 0);
	color += vOverlay.rgb * 2;
	// color = vOverlay.rgb;

	// color = float3(1, 1, 1);
	
	return float4(color.rgb, input.color.a);
}

//------------------------------------------------------------
// Point
StructuredBuffer<Line> _QuadPassBuffer;

struct VaryingsPoint
{
	float4 positionCS_SS : SV_POSITION;
	float3 positionWS : VAR_POSITION;
	float4 color : VAR_COLOR;
	uint baseId : VAR_ID;
	float size: PSIZE;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

VaryingsPoint CullPointPassVertex (uint id : SV_VertexID)
{
	VaryingsPoint output;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_TRANSFER_INSTANCE_ID(input, output);
	output.positionCS_SS = float4(_QuadPassBuffer[id].position, 1.0);
	output.positionWS = _QuadPassBuffer[id].position;
	output.positionCS_SS = TransformWorldToHClip(output.positionWS);
	output.color = float4(_QuadPassBuffer[id].color, 1.0);
	// output.color = float4(1, 0, 0, 1);
	output.size = 4;
	output.baseId = id;
	
	return output;
}

float4 CullPointPassFragment (VaryingsPoint input) : SV_TARGET
{
	UNITY_SETUP_INSTANCE_ID(input);
	Fragment fragment = GetFragment(input.positionCS_SS);

	float3 color = input.color.rgb;
	color *= 5;
	// color = float3(1, 0, 0);
	
	return float4(color.rgb, input.color.a);
}

#endif