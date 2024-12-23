#ifndef CUSTOM_CULL_PASS_GEOMETRY_INCLUDED
#define CUSTOM_CULL_PASS_GEOMETRY_INCLUDED

#include "../../ShaderLibrary/BoundingShapes.hlsl"
#include "../../ShaderLibrary/SpaceTransformations.hlsl"
#include "../../ShaderLibrary//Culling/CullTests.hlsl"

int _TriCullFrustum;
int _TriCullOrientation;
int _TriCullSmall;
int _TriCullHiZ;

[maxvertexcount(3)]
void CullPassGeometry(triangle Varyings input[3], inout TriangleStream<Varyings> triStream)
{
    // UNITY_SETUP_INSTANCE_ID(input);
    // UNITY_TRANSFER_INSTANCE_ID(input, output);

    int zWrite = INPUT_PROP(_ZWrite);
    #if defined(_CLIPPING)
        zWrite = 0;
    #endif
    
    int shadows = 0;
    #if defined(_CULL_SHADOWS)
        shadows = 1;
    #endif

    float3 positionsWS[3];
    positionsWS[0] = input[0].positionWS;
    positionsWS[1] = input[1].positionWS;
    positionsWS[2] = input[2].positionWS;
    AABBox bboxWS; GetAABBox(positionsWS, bboxWS);


    uint triCull = 0;
    if (_TriCullOrientation && zWrite != 0)
    {
        CullOrientation(positionsWS, triCull);
    }
    
    if (triCull == 0 && _TriCullFrustum)
    {
        BoxInFrustum(_FrustumPlanesWS, bboxWS, _FrustumCornersWS, triCull);
    }
	
    float3 positionsSS[3];
    positionsSS[0] = NDCToScreen(WorldToNDC(input[0].positionWS, _MatrixVP));
    positionsSS[1] = NDCToScreen(WorldToNDC(input[1].positionWS, _MatrixVP));
    positionsSS[2] = NDCToScreen(WorldToNDC(input[2].positionWS, _MatrixVP));
    AABBox bboxSS; GetAABBox(positionsSS, bboxSS);
    
    if (triCull == 0 && _TriCullSmall && zWrite != 0)
    {
        CullTriSmall(bboxSS, _ScreenParamsCull.xy, triCull);
    }
    
    float level = 0;
    if (triCull == 0 && _TriCullHiZ)
    {
        CullHiZ(bboxSS, _ScreenParamsCull.xy, _MipLevelMax, triCull, level);
    }
    
    // if (triCull == 0)
    // {
    //     for (int i = 0; i < 3; i++)
    //     {
    //         #if defined(_CULL_PASS_GEOMETRY_FLIPY)
    //             input[i].positionCS_SS.y *= _ProjectionParams.x; // b to t: -1 to 1 -> 1 to -1
    //         #endif
    //         triStream.Append(input[i]);
    //     }
    // }
    
        UNITY_UNROLL
        for (int i = 0; i < 3; i++)
        {
            #if defined(_CULL_PASS_GEOMETRY_FLIPY)
            input[i].positionCS_SS.y *= _ProjectionParams.x; // b to t: -1 to 1 -> 1 to -1
            #endif
            if (triCull)
            {
                input[i].positionWS = float3(getNaNSqrt(-1), 0, 0);
                input[i].positionCS_SS = float4(getNaNSqrt(-1), 0, 0, 0);
            }
            triStream.Append(input[i]);
        }
    triStream.RestartStrip();
}

#endif