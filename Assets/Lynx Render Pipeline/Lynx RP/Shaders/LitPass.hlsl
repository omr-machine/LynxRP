#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED

#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"
#include "../ShaderLibrary/GI.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"

struct Attributes
{
    float3 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 baseUV : TEXCOORD0;
    GI_ATTRIBUTE_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS_SS : SV_POSITION;
    float3 positionWS : VAR_POSITION;
    float3 normalWS : VAR_NORMAL;
    #if defined(_NORMAL_MAP)
        float4 tangentWS : VAR_TANGENT;
    #endif
    float2 baseUV : VAR_BASE_UV;
    #if defined(_DETAIL_MAP)
        float2 detailUV : VAR_DETAIL_UV;
    #endif
    GI_VARYINGS_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings LitPassVertex (Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    TRANSFER_GI_DATA(input, output);
    output.positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS_SS = TransformWorldToHClip(output.positionWS);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    #if defined(_NORMAL_MAP)
        output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
    #endif

    output.baseUV = TransformBaseUV(input.baseUV);
    #if defined(_DETAIL_MAP)
        output.detailUV = TransformDetailUV(input.baseUV);
    #endif
    return output;
}

float4 LitPassFragment (Varyings input) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(input);
    // ClipLOD(input.positionCS.xy, unity_LODFade[0]);

    InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);
    // return float4(config.fragment.depth.xxx / 20.0, 1.0);
    ClipLOD(config.fragment, unity_LODFade.x);
    #if defined(_MASK_MAP)
        config.useMask = true;
    #endif
    #if defined(_DETAIL_MAP)
        config.detailUV = input.detailUV;
        config.useDetail = true;
    #endif

    float4 base = GetBase(config);
    #if defined(_CLIPPING)
        clip(base.a - GetCutoff(config));
    #endif
    
    Surface surface;
    surface.position = input.positionWS;
    #if defined(_NORMAL_MAP)
        surface.normal = NormalTangentToWorld(GetNormalTS(config), input.normalWS, input.tangentWS);
        surface.interpolatedNormal = input.normalWS;
    #else
        surface.normal = normalize(input.normalWS);
        surface.interpolatedNormal = surface.normal;
    #endif
    surface.viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
    surface.depth = -TransformWorldToView(input.positionWS).z;
    surface.color = base.rgb;
    surface.alpha = base.a;
    surface.metallic = GetMetallic(config);
	surface.smoothness = GetSmoothness(config);
    surface.occlusion = GetOcclusion(config);
    surface.fresnelStrength = GetFresnel(config);
    surface.dither = InterleavedGradientNoise(config.fragment.positionSS, 0);
	surface.renderingLayerMask = asuint(unity_RenderingLayer.x);

	#if defined(_PREMULTIPLY_ALPHA)
		BRDF brdf = GetBRDF(surface, true);
	#else
		BRDF brdf = GetBRDF(surface);
	#endif
    GI gi = GetGI(GI_FRAGMENT_DATA(input), surface, brdf);
    float3 color = GetLighting(config.fragment, surface, brdf, gi);
    color += GetEmission(config);
    return float4(color, GetFinalAlpha(surface.alpha));
}

/////////////////////////////////////////////////////////////////////////////////
//                         Deferred Pass                                       //
/////////////////////////////////////////////////////////////////////////////////

struct MRTBufferLit
{
    float4 albedo : SV_TARGET0;
    float4 position : SV_TARGET1;
    float4 normal : SV_TARGET2;
    float4 normalInterpolated : SV_TARGET3;
    float4 orm : SV_TARGET4;
    float4 lighting : SV_TARGET5;
    float4 extras : SV_TARGET6;
};

MRTBufferLit LitDeferredBufferPassFragment (Varyings input)
{
    MRTBufferLit mrtBuffer;
    UNITY_SETUP_INSTANCE_ID(input);

    InputConfig config = GetInputConfig(input.positionCS_SS, input.baseUV);
    ClipLOD(config.fragment, unity_LODFade.x);
    #if defined(_MASK_MAP)
        config.useMask = true;
    #endif
    #if defined(_DETAIL_MAP)
        config.detailUV = input.detailUV;
        config.useDetail = true;
    #endif

    float4 base = GetBase(config);
    #if defined(_CLIPPING)
        clip(base.a - GetCutoff(config));
    #endif

    float3 normal;
    float3 normalInterpolated;
    #if defined(_NORMAL_MAP)
        normal = NormalTangentToWorld(GetNormalTS(config), input.normalWS, input.tangentWS);
        normalInterpolated = input.normalWS;
    #else
        normal = normalize(input.normalWS);
        normalInterpolated = normal;
    #endif
    float occlusion = GetOcclusion(config);
    float smoothness = GetSmoothness(config);
    float metallic = GetMetallic(config);
    float fresnel = GetFresnel(config);

    float3 color = base.rgb;
    float alpha = GetFinalAlpha(base.a);

    float3 emission = GetEmission(config);

    float dither = InterleavedGradientNoise(config.fragment.positionSS, 0);
    
    float2 objectUV = config.baseUV;
    float2 lightMapUV = GI_FRAGMENT_DATA(input);

    mrtBuffer.albedo = float4(color, alpha);
    mrtBuffer.position = float4(input.positionWS, dither);
    mrtBuffer.normal = float4(normal, 0.0);
    mrtBuffer.normalInterpolated = float4(normalInterpolated, 0.0);
    mrtBuffer.orm = float4(occlusion, smoothness, metallic, fresnel);
    mrtBuffer.lighting = float4(emission, 0.0);
    mrtBuffer.extras = float4(lightMapUV.x, lightMapUV.y, objectUV.x, objectUV.y);

    return mrtBuffer;
}

TEXTURE2D(_DeferredBufferAlbedo);
TEXTURE2D(_DeferredBufferPosition);
TEXTURE2D(_DeferredBufferNormal);
TEXTURE2D(_DeferredBufferNormalInterpolated);
TEXTURE2D(_DeferredBufferOrm);
TEXTURE2D(_DeferredBufferEmission);
TEXTURE2D(_DeferredBufferExtras);
TEXTURE2D(_DeferredBufferDepth);

struct VaryingsDeferredLighting
{
    float4 positionCS : SV_POSITION;
    float2 screenUV : VAR_SCREEN_UV;
};

VaryingsDeferredLighting LitDeferredBufferLightingPassVertex (uint vertexID : SV_VertexID)
{
    VaryingsDeferredLighting output;
    output.positionCS = float4(
        vertexID <= 1 ? -1.0 : 3.0,
        vertexID == 1 ? 3.0 : -1.0,
        0.0, 1.0
    );
    output.screenUV = float2(
        vertexID <= 1 ? 0.0 : 2.0,
        vertexID == 1 ? 2.0 : 0.0
    );

    if (_ProjectionParams.x <= 0.0)
    {
        output.screenUV.y = 1.0 - output.screenUV.y;
    }
    return output;
}

float4 LitDeferredBufferLightingPassFragment (VaryingsDeferredLighting input) : SV_TARGET
{   
    // float2 screenUV = GetScreenUV(input.positionCS_SS);
    float2 screenUV = input.screenUV;

    Fragment fragment = GetFragment(input.positionCS);
    
    float4 albedo = SAMPLE_TEXTURE2D_LOD(_DeferredBufferAlbedo, sampler_point_clamp, screenUV, 0);
    float4 position = SAMPLE_TEXTURE2D_LOD(_DeferredBufferPosition, sampler_point_clamp, screenUV, 0);
    float4 normal = SAMPLE_TEXTURE2D_LOD(_DeferredBufferNormal, sampler_point_clamp, screenUV, 0);
    float4 normalInterpolated = SAMPLE_TEXTURE2D_LOD(_DeferredBufferNormalInterpolated, sampler_point_clamp, screenUV, 0);
    float4 orm = SAMPLE_TEXTURE2D_LOD(_DeferredBufferOrm, sampler_point_clamp, screenUV, 0);
    float4 emission = SAMPLE_TEXTURE2D_LOD(_DeferredBufferEmission, sampler_point_clamp, screenUV, 0);
    float4 extras = SAMPLE_TEXTURE2D_LOD(_DeferredBufferExtras, sampler_point_clamp, screenUV, 0);
    
    // depth = SAMPLE_DEPTH_TEXTURE_LOD(_DeferredBufferDepth, sampler_point_clamp, input.screenUV, 0);
    
    float2 objectUV = float2(extras.z, extras.w);

    if (emission.w == 1.0)
    {
        return albedo;
    }

    Surface surface;
    surface.position = position.xyz;
    surface.normal = normal.xyz;
    surface.interpolatedNormal = normalInterpolated.xyz;
    surface.viewDirection = normalize(_WorldSpaceCameraPos - position.xyz);
    surface.depth = -TransformWorldToView(position.xyz).z;
    surface.color = albedo.rgb;
    // surface.color = float3(0.0, 0.0, 0.0);
    surface.alpha = albedo.a;
    surface.metallic = orm.z;
	surface.smoothness = orm.y;
    surface.occlusion = orm.x;
    surface.fresnelStrength = orm.w;
    surface.dither = position.w;
	surface.renderingLayerMask = asuint(unity_RenderingLayer.x);

    float2 lightMapUV = extras.xy;

	#if defined(_PREMULTIPLY_ALPHA)
		BRDF brdf = GetBRDF(surface, true);
	#else
		BRDF brdf = GetBRDF(surface);
	#endif
    GI gi = GetGI(lightMapUV, surface, brdf);
    float3 color = GetLighting(fragment, surface, brdf, gi);
    color += emission.rgb;
    // return float4(surface.interpolatedNormal, 0.0);
    // color -= albedo.rgb;
    return float4(color, surface.alpha);
}

#endif