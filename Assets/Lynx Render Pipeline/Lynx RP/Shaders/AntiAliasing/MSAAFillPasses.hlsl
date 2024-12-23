#ifndef MSAA_FILL_PASSES_INCLUDED
#define MSAA_FILL_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

TEXTURE2D(_MSAAFillColorAttachment);
TEXTURE2D(_MSAAFillDepthAttachment);
TEXTURE2D(_MSAAFillStencil);
float4 _MSAAFillTexelSize;

struct Varyings 
{
    float4 positionCS : SV_POSITION;
    float2 screenUV : VAR_SCREEN_UV;
};

Varyings DefaultPassVertex (uint vertexID : SV_VertexID)
{
    Varyings output;
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

struct Offsets
{
    float2 lD;
    float2 lU;
    float2 rD;
    float2 rU;
};

struct Samples
{
    float4 lD;
    float4 lU;
    float4 rD;
    float4 rU;
};

float4 ColorPassFragment (Varyings input, out float depth : SV_Depth) : SV_TARGET
{
    float2 subtractOneUV = input.screenUV - _MSAAFillTexelSize.xy;
    Offsets offsets;
    offsets.lD = float2(subtractOneUV.x, subtractOneUV.y);
    offsets.lU = float2(subtractOneUV.x, input.screenUV.y);
    offsets.rD = float2(input.screenUV.x, subtractOneUV.y);
    offsets.rU = float2(input.screenUV.x, input.screenUV.y);
    
    Samples samples;
    samples.lD = SAMPLE_TEXTURE2D_LOD(_MSAAFillStencil, sampler_point_clamp, offsets.lD, 0);
    samples.lU = SAMPLE_TEXTURE2D_LOD(_MSAAFillStencil, sampler_point_clamp, offsets.lU, 0);
    samples.rD = SAMPLE_TEXTURE2D_LOD(_MSAAFillStencil, sampler_point_clamp, offsets.rD, 0);
    samples.rU = SAMPLE_TEXTURE2D_LOD(_MSAAFillStencil, sampler_point_clamp, offsets.rU, 0);
    
    float4 mask = SAMPLE_TEXTURE2D_LOD(_MSAAFillStencil, sampler_point_clamp, input.screenUV, 0);
    float4 color = SAMPLE_TEXTURE2D_LOD(_MSAAFillColorAttachment, sampler_point_clamp, input.screenUV, 0);
    depth = SAMPLE_DEPTH_TEXTURE_LOD(_MSAAFillDepthAttachment, sampler_point_clamp, input.screenUV, 0) * mask.r;

    return color * mask;
    // return float4(input.screenUV.x, input.screenUV.y, 0.0, 1.0);
}
#endif