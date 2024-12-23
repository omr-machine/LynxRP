#ifndef CUSTOM_CSCULL_TESTS_INCLUDED
#define CUSTOM_CSCULL_TESTS_INCLUDED

bool CullFrustumSS(float3 positionSS)
{
    bool cull = true;
    if (PositionSSInBounds(positionSS))
    {
        cull = false;
    }
    return cull;
}

void TriWorldToView(float3 positionsWS[3], out float3 positionsVS[3])
{
    positionsVS[0] = WorldToView(positionsWS[0]);
    positionsVS[1] = WorldToView(positionsWS[1]);
    positionsVS[2] = WorldToView(positionsWS[2]);
}

void TriViewToHClip(float3 positionsVS[3], out float4 positionsCS[3])
{
    positionsCS[0] = ViewToHClip(positionsVS[0]);
    positionsCS[1] = ViewToHClip(positionsVS[1]);
    positionsCS[2] = ViewToHClip(positionsVS[2]);
}

void TriHClipToNDC(float4 positionsCS[3], out float3 positionsNDC[3])
{
    positionsNDC[0] = HClipToNDC(positionsCS[0]);
    positionsNDC[1] = HClipToNDC(positionsCS[1]);
    positionsNDC[2] = HClipToNDC(positionsCS[2]);
}

void TriNDCToScreen(float3 positionsNDC[3], out float3 positionsSS[3])
{
    positionsSS[0] = NDCToScreen(positionsNDC[0]);
    positionsSS[1] = NDCToScreen(positionsNDC[1]);
    positionsSS[2] = NDCToScreen(positionsNDC[2]);
}

void DebugTextureFillTriCenter(float3 positionsSS[3], float4 color, bool cull)
{
    float3 triCenter = (positionsSS[0] + positionsSS[1] + positionsSS[2]) / 3.0;
    if (!CullFrustumSS(float3(triCenter.xy, 0.0)))
    {
        uint2 coord = uint2(triCenter.x * _ScreenParams.x, triCenter.y * _ScreenParams.y);
        if (!cull)
            _CullDebugTexture[coord] = color;
        else
            _CullDebugTexture[coord] = float4(1, 0, 0, 0);
    }
}

void DebugTextureFillPositions(float3 positionsSS[3], float4 color, bool cull)
{
    UNITY_UNROLL
    for (int i = 0; i < VerticesInTri; ++i)
    {
        if (!CullFrustumSS(positionsSS[i]))
        {
            uint2 coord = uint2(positionsSS[i].x * _ScreenParams.x, positionsSS[i].y * _ScreenParams.y);
            if (!cull)
                _CullDebugTexture[coord] = color;
            else
                _CullDebugTexture[coord] = float4(1, 0, 0, 0);
        }
    }
}

uint CullTests (uint idx, float3 positionsWS[3])
{
    float3 triCenterWS = (positionsWS[0] + positionsWS[1] + positionsWS[2]) / 3.0;
    float3 positionsVS[3]; TriWorldToView(positionsWS, positionsVS);
    float4 positionsCS[3]; TriViewToHClip(positionsVS, positionsCS);
    float3 positionsNDC[3]; TriHClipToNDC(positionsCS, positionsNDC);
    float3 positionsSS[3]; TriNDCToScreen(positionsNDC, positionsSS);

    AABBox bboxWS; GetAABBox(positionsWS, bboxWS);
    AABBox bboxVS; GetAABBox(positionsVS, bboxVS);
    AABBox bboxNDC; GetAABBox(positionsNDC, bboxNDC);
    AABBox bboxSS; GetAABBox(positionsSS, bboxSS);

    float level = 0;
    uint triCull = 0;
    if (triCull == 0 && (_TriCullOrientation > 0))
    {
        CullOrientation(positionsWS, triCull); // CullOrientationBase(positionsCS, triCull);
    }
         
    if (triCull == 0 && _TriCullFrustum)
    {
        // CullFrustum(bboxWS, triCull);
    }
    
    if (triCull == 0 && _TriCullFrustum)
    {
        BoxInFrustum(_FrustumPlanesWS, bboxWS, _FrustumCornersWS, triCull);
    }
    
    if (triCull == 0 && _TriCullSmall)
    {
        CullTriSmall(bboxSS, _ScreenParamsCull.xy, triCull);
    }
    
    if (triCull == 0 && _TriCullHiZ)
    {
        CullHiZ(bboxSS, _ScreenParamsCull.xy, _MipLevelMax, triCull, level);
    }

    float4 debugColorBbox = float4(1, 1, 0, 0);
    float4 debugColorPoint = float4(1, 0, 0, 0);
    if (triCull)
    {
        debugColorBbox = float4(1, 1, 0, 0);
        debugColorPoint = float4(0, 0, 1, 0);
    }
    
    // GetMipColor(level, debugColorPoint);
    // DebugTextureFillTriCenter(positionsSS, debugColorBbox, triCull);
    // DebugTextureFillPositions(positionsSS, debugColorBbox, cull2);
    
    uint pointIndexStart = idx * VerticesInTri;
    
    UNITY_UNROLL
    for (uint i = 0; i < VerticesInTri; i++)
    {
        float3 positionWS = positionsWS[i]; //bboxCorners[i];
        _QuadBuffer[pointIndexStart + i].position = positionWS;
        _QuadBuffer[pointIndexStart + i].color = debugColorPoint;
    }

    AABBox bboxDebug;
    bboxDebug.minCorner = bboxWS.minCorner;
    bboxDebug.maxCorner = bboxWS.maxCorner;
    bboxDebug.length = bboxNDC.length;
    _BBoxBuffer[idx] = bboxDebug;

    return triCull;
}

#endif
