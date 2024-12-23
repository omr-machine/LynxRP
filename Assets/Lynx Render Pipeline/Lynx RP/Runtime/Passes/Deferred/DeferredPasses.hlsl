#ifndef DEFERRED_PASSES_INCLUDED
#define DEFERRED_PASSES_INCLUDED

TEXTURE2D(_DeferredBufferDepth);

struct Attributes
{
    float3 positionOS : POSITION;
    float4 color : COLOR;
    float4 baseUV : TEXCOORD0;
};

struct Varyings 
{
    float4 positionCS_SS : SV_POSITION;
    float3 positionWS : VAR_POSITION_WS;
    float2 baseUV : VAR_SCREEN_UV;
};

Varyings DefaultPassVertex (Attributes input)
{
    Varyings output;
    float3 positionWS = TransformObjectToWorld(input.positionOS);
    output.positionCS_SS = TransformWorldToHClip(positionWS);
    output.positionWS = positionWS;
    output.baseUV = input.baseUV.xy;
    return output;
}

struct MRTBuffer 
{
    float4 albedo : SV_TARGET0;
    float4 normal : SV_TARGET1;
    float4 orm : SV_TARGET2;
};

MRTBuffer DefaultPassFragment (Varyings input)
{
    MRTBuffer mrtBuffer;
    mrtBuffer.albedo = float4(0.0, 0.0, 1.0, 1.0);
    mrtBuffer.normal = float4(input.positionWS.x, input.positionWS.y, input.positionWS.z, 1.0);
    mrtBuffer.orm = float4(input.baseUV.x, input.baseUV.y, 0.0, 1.0);
    // depth = 1.0;

    return mrtBuffer;
}

#include "../../../ShaderLibrary/Surface.hlsl"
#include "../../../ShaderLibrary/Shadows.hlsl"
#include "../../../ShaderLibrary/Light.hlsl"
#include "../../../ShaderLibrary/BRDF.hlsl"
#include "../../../ShaderLibrary/GI.hlsl"
#include "../../../ShaderLibrary/Lighting.hlsl"

TEXTURE2D(_DeferredBufferAlbedo);
TEXTURE2D(_DeferredBufferPosition);
TEXTURE2D(_DeferredBufferNormal);
TEXTURE2D(_DeferredBufferNormalInterpolated);
TEXTURE2D(_DeferredBufferOrm);
TEXTURE2D(_DeferredBufferEmission);
TEXTURE2D(_DeferredBufferExtras);
// TEXTURE2D(_DeferredBufferDepth);

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

    Surface surface;
    surface.position = position.xyz;
    surface.normal = normal.xyz;
    surface.interpolatedNormal = normalInterpolated.xyz;
    surface.viewDirection = normalize(_WorldSpaceCameraPos - position);
    surface.depth = -TransformWorldToView(position).z;
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
    return float4(color, surface.alpha);
}

#endif