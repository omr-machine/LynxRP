#ifndef MSAA_DOWNSCALE_PASSES_INCLUDED
#define MSAA_DOWNSCALE_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

TEXTURE2D(_MSAABlurColor);
TEXTURE2D(_MSAABlurDepth);
TEXTURE2D(_MSAADownscaleColor);
TEXTURE2D(_MSAADownscaleDepth);
float4 _MSAAFillTexelSize;
float4 _MSAABlurColor_TexelSize;
float _BlurOffset;

struct Varyings 
{
    float4 positionCS : SV_POSITION;
    float2 screenUV : VAR_SCREEN_UV;
};

struct VaryingsDownscale
{
    float4 positionCS : SV_POSITION;
    float2 screenUV0 : VAR_SCREEN_UV0;
    float4 screenUV1 : VAR_SCREEN_UV1;
    float4 screenUV2 : VAR_SCREEN_UV2;
};

struct VaryingsUpscale
{
    float4 positionCS : SV_POSITION;
    float2 screenUV0 : VAR_SCREEN_UV0;
    float4 screenUV1 : VAR_SCREEN_UV1;
    float4 screenUV2 : VAR_SCREEN_UV2;
    float4 screenUV3 : VAR_SCREEN_UV3;
    float4 screenUV4 : VAR_SCREEN_UV4;
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

VaryingsDownscale BlurDownscalePassVertex (const uint vertexID : SV_VertexID)
{
    VaryingsDownscale output;
    output.positionCS = float4(
        vertexID <= 1 ? -1.0 : 3.0,
        vertexID == 1 ? 3.0 : -1.0,
        0.0, 1.0
    );
    output.screenUV0 = float2(
        vertexID <= 1 ? 0.0 : 2.0,
        vertexID == 1 ? 2.0 : 0.0
    );

    if (_ProjectionParams.x <= 0.0)
    {
        output.screenUV0.y = 1.0 - output.screenUV0.y;
    }

    const float2 uv = output.screenUV0;
    const float2 halfPixel = _MSAABlurColor_TexelSize.xy * 0.5;
    const float2 offset = float2(1.0 + _BlurOffset, 1.0 + _BlurOffset);

    output.screenUV1.xy = uv - halfPixel * offset;
    output.screenUV1.zw = uv + halfPixel * offset;

    output.screenUV2.xy = uv - float2(halfPixel.x, -halfPixel.y) * offset;
    output.screenUV2.zw = uv + float2(halfPixel.x, -halfPixel.y) * offset;
    return output;
}

VaryingsUpscale BlurUpscalePassVertex (const uint vertexID : SV_VertexID)
{
    VaryingsUpscale output;
    output.positionCS = float4(
        vertexID <= 1 ? -1.0 : 3.0,
        vertexID == 1 ? 3.0 : -1.0,
        0.0, 1.0
    );
    output.screenUV0 = float2(
        vertexID <= 1 ? 0.0 : 2.0,
        vertexID == 1 ? 2.0 : 0.0
    );

    if (_ProjectionParams.x <= 0.0)
    {
        output.screenUV0.y = 1.0 - output.screenUV0.y;
    }

    const float2 uv = output.screenUV0;
    const float2 halfPixel = _MSAABlurColor_TexelSize.xy * 0.5;
    const float2 offset = float2(1.0 + _BlurOffset, 1.0 + _BlurOffset);

    output.screenUV1.xy = uv + float2(-halfPixel.x * 2.0, 0.0) * offset;
    output.screenUV1.zw = uv + float2(-halfPixel.x, halfPixel.y) * offset;

    output.screenUV2.xy = uv + float2(0.0, halfPixel.y * 2.0) * offset;
    output.screenUV2.zw = uv + halfPixel * offset;
    
    output.screenUV3.xy = uv + float2(halfPixel.x * 2.0, 0.0) * offset;
    output.screenUV3.zw = uv + float2(halfPixel.x, -halfPixel.y) * offset;
    
    output.screenUV4.xy = uv + float2(0.0, -halfPixel.y * 2.0) * offset;
    output.screenUV4.zw = uv - halfPixel * offset;

    return output;
}

float4 GetSource (float2 screenUV)
{
    return SAMPLE_TEXTURE2D_LOD(_MSAABlurColor, sampler_linear_clamp, screenUV, 0);
}

float4 BlurDownscalePassFragment (const VaryingsDownscale input) : SV_TARGET
{
    float4 sum = GetSource(input.screenUV0) * 4.0;
    sum += GetSource(input.screenUV1.xy);
    sum += GetSource(input.screenUV1.zw);
    sum += GetSource(input.screenUV2.xy);
    sum += GetSource(input.screenUV2.zw);
    return sum * 0.125;
}

float4 BlurUpscalePassFragment (const VaryingsUpscale input) : SV_TARGET
{
    float4 sum = GetSource(input.screenUV1.xy);
    sum += GetSource(input.screenUV1.zw) * 2.0;
    sum += GetSource(input.screenUV2.xy);
    sum += GetSource(input.screenUV2.zw) * 2.0;
    sum += GetSource(input.screenUV3.xy);
    sum += GetSource(input.screenUV3.zw) * 2.0;
    sum += GetSource(input.screenUV4.xy);
    sum += GetSource(input.screenUV4.zw) * 2.0;
    return sum * 0.0833;
}

float4 DownscalePassFragment (Varyings input) : SV_TARGET
{
    float4 color = SAMPLE_TEXTURE2D_LOD(_MSAADownscaleColor, sampler_point_clamp, input.screenUV, 0);
    return color;
    // return float4(input.screenUV.x, input.screenUV.y, 0.0, 1.0);
}
#endif