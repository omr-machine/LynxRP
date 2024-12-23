#ifndef CUSTOM_UNLIT_PASS_INCLUDED
#define CUSTOM_UNLIT_PASS_INCLUDED

struct Attributes
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 color : COLOR;
    #if defined(_FLIPBOOK_BLENDING)
        float4 baseUV : TEXCOORD0;
        float flipbookBlend : TEXCOORD1;
    #else
        float2 baseUV : TEXCOORD0;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS_SS : SV_POSITION;
    float3 positionWS : VAR_POSITION;
    float3 normalWS : VAR_NORMAL;
	#if defined(_VERTEX_COLORS)
		float4 color : VAR_COLOR;
	#endif
    float2 baseUV : VAR_BASE_UV;
    #if defined(_FLIPBOOK_BLENDING)
        float3 flipbookUVB : VAR_FLIPBOOK;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings UnlitPassVertex (Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    output.positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS_SS = TransformWorldToHClip(output.positionWS);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);

	#if defined(_VERTEX_COLORS)
		output.color = input.color;
	#endif
    output.baseUV.xy = TransformBaseUV(input.baseUV.xy);
	#if defined(_FLIPBOOK_BLENDING)
		output.flipbookUVB.xy = TransformBaseUV(input.baseUV.zw);
		output.flipbookUVB.z = input.flipbookBlend;
	#endif
    return output;
}

float4 UnlitPassFragment (Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);
    // return float4(config.fragment.depth.xxx / 20.0, 1.0); // debug depth buffer
    // return GetBufferColor(config.fragment, 0.05); // debug color buffer
    #if defined(_VERTEX_COLORS)
        config.color = input.color;
    #endif
    #if defined(_FLIPBOOK_BLENDING)
		config.flipbookUVB = input.flipbookUVB;
		config.flipbookBlending = true;
    #endif
	#if defined(_NEAR_FADE)
		config.nearFade = true;
	#endif
    #if defined(_SOFT_PARTICLES)
        config.softParticles = true;
    #endif
    
    float4 base = GetBase(config);

    #if defined(_CLIPPING)
        clip(base.a - GetCutoff(config));
    #endif
	#if defined(_DISTORTION)
		float2 distortion = GetDistortion(config) * base.a;
		base.rgb = lerp(
			GetBufferColor(config.fragment, distortion).rgb, base.rgb,
			saturate(base.a - GetDistortionBlend(config))
		);
	#endif
    
    return float4(base.rgb, GetFinalAlpha(base.a));
}

struct MRTBufferUnlit
{
    float4 albedo : SV_TARGET0;
    float4 position : SV_TARGET1;
    float4 normal : SV_TARGET2;
    float4 normalInterpolated : SV_TARGET3;
    float4 orm : SV_TARGET4;
    float4 lighting : SV_TARGET5;
    float4 extras : SV_TARGET6;
};

MRTBufferUnlit UnlitDeferredBufferPassFragment (Varyings input)
{
    MRTBufferUnlit mrtBuffer;
    UNITY_SETUP_INSTANCE_ID(input);
    InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);

    #if defined(_VERTEX_COLORS)
        config.color = input.color;
    #endif
    #if defined(_FLIPBOOK_BLENDING)
		config.flipbookUVB = input.flipbookUVB;
		config.flipbookBlending = true;
    #endif
	#if defined(_NEAR_FADE)
		config.nearFade = true;
	#endif
    #if defined(_SOFT_PARTICLES)
        config.softParticles = true;
    #endif
    
    float4 base = GetBase(config);

    #if defined(_CLIPPING)
        clip(base.a - GetCutoff(config));
    #endif
	#if defined(_DISTORTION)
		float2 distortion = GetDistortion(config) * base.a;
		base.rgb = lerp(
			GetBufferColor(config.fragment, distortion).rgb, base.rgb,
			saturate(base.a - GetDistortionBlend(config))
		);
	#endif

    // float3 normal = input.normalWS;
    float3 normal = normalize(input.normalWS);

    mrtBuffer.albedo = float4(base.rgb, GetFinalAlpha(base.a));
    mrtBuffer.position = float4(input.positionWS, 0.0);
    mrtBuffer.normal = float4(normal, 0.0);
    mrtBuffer.normalInterpolated = float4(normal, 0.0);
    mrtBuffer.orm = float4(0.0, 0.0, 0.0, 0.0);
    mrtBuffer.lighting = float4(0.0, 0.0, 0.0, 1.0);
    mrtBuffer.extras = float4(0.0, 0.0, 0.0, 0.0);

    return mrtBuffer;
}

float4 UnlitDeferredBufferLightingPassFragment () : SV_TARGET
{
    return float4(0.0, 0.0, 0.0, 0.0);
}

#endif