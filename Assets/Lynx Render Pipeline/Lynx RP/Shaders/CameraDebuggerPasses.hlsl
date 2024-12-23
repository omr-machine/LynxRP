#ifndef CAMERA_DEBUGGER_PASSES_INCLUDED
#define CAMERA_DEBUGGER_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Debug.hlsl"

float _DebugOpacity;
TEXTURE2D(_DebugDepthBuffer);
TEXTURE2D(_DebugStencilBuffer);
TEXTURE2D(_DebugColorBuffer);
TEXTURE2D(_DebugMSAA2);
TEXTURE2D(_DebugMSAAFillColor);
TEXTURE2D(_DebugMSAAFillDepth);

struct Varyings 
{
	float4 positionCS_SS : SV_POSITION;
	float2 screenUV : VAR_SCREEN_UV;
};

Varyings DefaultPassVertex (uint vertexID : SV_VertexID)
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

float4 ForwardPlusTilesPassFragment (Varyings input) : SV_TARGET
{
    ForwardPlusTile tile = GetForwardPlusTile(input.screenUV);
    float3 color;
    if (tile.IsMinimumEdgePixel(input.screenUV))
    {
        color = 1.0;
    }
    else
    {
        color = OverlayHeatMap(
			input.screenUV * _CameraBufferSize.zw, tile.GetScreenSize(),
			tile.GetLightCount(), tile.GetMaxLightsPerTile(), 1.0
        ).rgb;
    }
	return float4(color, _DebugOpacity);
}

float4 DepthPassFragment (Varyings input) : SV_TARGET
{
    float depth = SAMPLE_DEPTH_TEXTURE_LOD(_DebugDepthBuffer, sampler_point_clamp, input.screenUV, 0);
	return float4(depth, depth, depth, 1.0); 
    // return float4(config.fragment.depth.xxx / 20.0, 1.0); // debug depth buffer
}

float4 StencilPassFragment (Varyings input) : SV_TARGET
{
    // float4 stencil = SAMPLE_DEPTH_TEXTURE_LOD(_DebugStencilBuffer, sampler_point_clamp, input.screenUV, 0);
    // float4 stencil = SAMPLE_TEXTURE2D_LOD(_DebugMSAA2, sampler_point_clamp, input.screenUV, 0);
    // float4 stencil = SAMPLE_TEXTURE2D_LOD(_DebugMSAAFillColor, sampler_point_clamp, input.screenUV, 0);
    float4 stencil = SAMPLE_DEPTH_TEXTURE_LOD(_DebugMSAAFillDepth, sampler_point_clamp, input.screenUV, 0);

	// return float4(stencil, stencil, stencil, 1.0);
    return float4(stencil.r, stencil.g, stencil.b, 1.0);

    // return stencil;
    // return float4(1.0, 0.0, 0.0, 1.0);


}

float4 ColorPassFragment (Varyings input) : SV_TARGET
{
    return SAMPLE_TEXTURE2D_LOD(_DebugColorBuffer, sampler_linear_clamp, input.screenUV, 0);
    // return GetBufferColor(config.fragment, 0.05); // debug color buffer
}

TEXTURE2D(_DebugAlbedoBuffer);

float4 DeferredAlbedoPassFragment (Varyings input) : SV_TARGET
{
    return SAMPLE_TEXTURE2D_LOD(_DebugAlbedoBuffer, sampler_linear_clamp, input.screenUV, 0);
}

TEXTURE2D(_DebugADeferredPositionBuffer);
TEXTURE2D(_DebugADeferredNormalBuffer);
TEXTURE2D(_DebugADeferredNormalInterpolatedBuffer);
TEXTURE2D(_DebugADeferredOrmdBuffer);
TEXTURE2D(_DebugADeferredEmissiondBuffer);
TEXTURE2D(_DebugADeferredExtrasdBuffer);

float4 DeferredMRTPassFragment (Varyings input) : SV_TARGET
{
    float4 mrtColor = SAMPLE_TEXTURE2D_LOD(_DebugADeferredOrmdBuffer, sampler_linear_clamp, input.screenUV, 0);
    mrtColor.rgb = mrtColor.rgb;
    return float4(mrtColor.rgb, 1);
}

TEXTURE2D(_HiZTexture);
int _DebugHiZMipLevel;

float4 HiZPassFragment (Varyings input) : SV_TARGET
{
    float4 depth = SAMPLE_TEXTURE2D_LOD(_HiZTexture, sampler_point_clamp, input.screenUV, _DebugHiZMipLevel);
    float4 color = SAMPLE_TEXTURE2D_LOD(_DebugColorBuffer, sampler_linear_clamp, input.screenUV, 0) * 0.1;
    float colorMax = max(max(color.x, color.y), color.z);
    return float4(depth.x, colorMax, colorMax, 1);
    // return float4(depth.x, depth.y, 0, 1);
}

TEXTURE2D(_DebugHiZDepthPrevFrame);
TEXTURE2D(_DebugHiZDepthProjected);

float4 HiZDepthDifferencePassFragment (Varyings input) : SV_TARGET
{
    float4 depthPrev = SAMPLE_TEXTURE2D_LOD(_DebugHiZDepthPrevFrame, sampler_point_clamp, input.screenUV, 0);
    float4 depthProjected = SAMPLE_TEXTURE2D_LOD(_DebugHiZDepthProjected, sampler_point_clamp, input.screenUV, 0);
    float depthDifference = abs(depthPrev.x - depthProjected.x) * 10.0;
    // return float4(depthProjected.x, depthPrev.x, depthDifference, 1.0);
    return float4(depthDifference, depthDifference, 0.0, 1.0);
}

#endif