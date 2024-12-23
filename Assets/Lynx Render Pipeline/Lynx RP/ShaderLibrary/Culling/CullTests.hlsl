#ifndef CUSTOM_CULL_TESTS_INCLUDED
#define CUSTOM_CULL_TESTS_INCLUDED

int _MipLevelMax;
float4 _ScreenParamsCull;

Texture2D<float4> _HiZTexture;
SamplerState point_Clamp_HiZTexture;// MyPointClampSampler;

//=============================================================================//
//                                HiZ Culling                                  //
//=============================================================================//
void GetMipColor(int level, out float4 color)
{
    if (level == 0)
        color = float4(1.0, 0.0, 0.0, 1.0); // red
    else if (level == 1)
        color = float4(0.03, 0.0, 0.0, 1.0); // red dark
    else if (level == 2)
        color = float4(0.0, 1.0, 0.0, 1.0); // green
    else if (level == 3)
        color = float4(0.0, 0.03, 0.0, 1.0); // green dark
    else if (level == 4)
        color = float4(0.0, 0.0, 1.0, 1.0); // blue
    else if (level == 5)
        color = float4(0.0, 0.0, 0.03, 1.0); // blue dark
    else if (level == 6)
        color = float4(0.0, 1.0, 1.0, 1.0); // cyan
    else if (level == 7)
        color = float4(0.0, 0.03, 0.03, 1.0); // cyan dark
    else if (level == 8)
        color = float4(1.0, 1.0, 0.0, 1.0); // yellow
    else if (level > 8)
        color = float4(0.03, 0.03, 0.0, 1.0); // yellow dark
    else
        color = float4(1.0, 1.0, 1.0, 1.0);
}



void CullHiZ(AABBox bboxSS, uint2 screenParams, uint mipLevelMax, out bool cull, out float level)
{
    float bboxDepth = max(bboxSS.maxCorner.z, bboxSS.minCorner.z);
    float2 minPos = bboxSS.minCorner.xy;
    float2 maxPos = bboxSS.maxCorner.xy;
    
    bboxDepth = clamp(bboxDepth, 0.0, 1.0);
    minPos = clamp(minPos, 0, 1.0);
    maxPos = clamp(maxPos, 0, 1.0);
    
    minPos *= screenParams;
    maxPos *= screenParams;

    // minPos = floor(minPos);
    // maxPos = ceil(maxPos);

    float2 sizeSS = float2(maxPos.x - minPos.x, maxPos.y - minPos.y);

    level = ceil(log2(max(sizeSS.x, sizeSS.y)));
    level = clamp(level, 0, mipLevelMax);
    
    float4 uvBBox = float4(
        minPos.x / screenParams.x, minPos.y / screenParams.y,
        maxPos.x / screenParams.x, maxPos.y / screenParams.y
    );
    float4 depth = float4(
        _HiZTexture.SampleLevel(point_Clamp_HiZTexture, uvBBox.xy, level).x, // LB
        _HiZTexture.SampleLevel(point_Clamp_HiZTexture, uvBBox.xw, level).x, // LT
        _HiZTexture.SampleLevel(point_Clamp_HiZTexture, uvBBox.zy, level).x, // RB
        _HiZTexture.SampleLevel(point_Clamp_HiZTexture, uvBBox.zw, level).x  // RT
        // _HiZTexture.mips[level][uvBBox.xy].x,
        // _HiZTexture.mips[level][uvBBox.xw].x,
        // _HiZTexture.mips[level][uvBBox.zy].x,
        // _HiZTexture.mips[level][uvBBox.zw].x
    );
    float sampledDepth = min(min(depth.x, depth.y), min(depth.z, depth.w)) - 0.000015;// - 0.006;
    // sampledDepth = depth.w;
    // if (level == 0)
    // {
    //     sampledDepth = 1.0;
    // }

    uint inView = sampledDepth <= bboxDepth; // HiZ sample is behind the bbox
    cull = !inView;
}

//=============================================================================//
//                                Frustum Culling                              //
//=============================================================================//
void BoxInFrustum(float4 mPlanes[6], AABBox bbox, float4 corners[8], out bool cull)
{
    float3 mMin = bbox.minCorner;
    float3 mMax = bbox.maxCorner;

    bool cullFrustum = true;
    UNITY_UNROLL
    for (int i = 0; i < 6; i++)
    {
        int inView = 0;
        inView += dot(mPlanes[i], float4(mMin.x, mMin.y, mMin.z, 1.0f)) > 0.0 ? 1 : 0;
        inView += dot(mPlanes[i], float4(mMax.x, mMin.y, mMin.z, 1.0f)) > 0.0 ? 1 : 0;
        inView += dot(mPlanes[i], float4(mMin.x, mMax.y, mMin.z, 1.0f)) > 0.0 ? 1 : 0;
        inView += dot(mPlanes[i], float4(mMin.x, mMin.y, mMax.z, 1.0f)) > 0.0 ? 1 : 0;
        inView += dot(mPlanes[i], float4(mMax.x, mMax.y, mMin.z, 1.0f)) > 0.0 ? 1 : 0;
        inView += dot(mPlanes[i], float4(mMax.x, mMin.y, mMax.z, 1.0f)) > 0.0 ? 1 : 0;
        inView += dot(mPlanes[i], float4(mMin.x, mMax.y, mMax.z, 1.0f)) > 0.0 ? 1 : 0;
        inView += dot(mPlanes[i], float4(mMax.x, mMax.y, mMax.z, 1.0f)) > 0.0 ? 1 : 0;
        if (inView == 8)
        {
            cullFrustum = false;
            break;
        }
    }
    
    bool inView = false;
    if (cullFrustum == true)
    {
        inView = true;
        UNITY_UNROLL
        for (int p = 0; p < 1; p++)
        {
            int outV;
            outV = 0; for (uint j = 0; j < 8; j++) outV += (corners[j].x > mMax.x) ? 1 : 0; if (outV == 8 ) { inView = false; break; }
            outV = 0; for (uint k = 0; k < 8; k++) outV += (corners[k].x < mMin.x) ? 1 : 0; if (outV == 8 ) { inView = false; break; }
            outV = 0; for (uint l = 0; l < 8; l++) outV += (corners[l].y > mMax.y) ? 1 : 0; if (outV == 8 ) { inView = false; break; }
            outV = 0; for (uint m = 0; m < 8; m++) outV += (corners[m].y < mMin.y) ? 1 : 0; if (outV == 8 ) { inView = false; break; }
            outV = 0; for (uint n = 0; n < 8; n++) outV += (corners[n].z > mMax.z) ? 1 : 0; if (outV == 8 ) { inView = false; break; }
            outV = 0; for (uint o = 0; o < 8; o++) outV += (corners[o].z < mMin.z) ? 1 : 0; if (outV == 8 ) { inView = false; break; }
        }
    }
    
    cull = !inView;
}

uint IsCameraOutsideObjBounds(float3 pos, float3 minPos, float3 maxPos)
{
    float boundsSize = distance(maxPos, minPos);
    return ((distance(pos, maxPos) > boundsSize)
            + (distance(pos, minPos) > boundsSize));
}

uint IsVisibleAfterFrustumCulling(float4 positionCS)
{
    // -w <= x <= w, -w <= y <= w, 0 <= z <= w
    // positionCS.w *= -1;
    return (
        positionCS.z > positionCS.w || 
        -positionCS.w > positionCS.x || positionCS.x > positionCS.w || 
        -positionCS.w > positionCS.y || positionCS.y > positionCS.w
        ) ? 0 : 1;
}

void CullFrustum (AABBox bbox, out bool cull)
{
    float3 minPos = bbox.minCorner;
    float3 maxPos = bbox.maxCorner;

    float4 boxCorners[8]; GetCornersAABB(bbox, boxCorners);
    
    uint isInFrustum = 0;
    [unroll]
    for (int i = 0; i < 8; i++)
    {
        float4 positionCS = mul(_MatrixVP, boxCorners[i]);
        isInFrustum = saturate(isInFrustum + IsVisibleAfterFrustumCulling(positionCS));
    }

    uint isOutsideBounds = 1;
    if (IsCameraOutsideObjBounds(_CameraPosition, minPos, maxPos))
    {
        isOutsideBounds *= isInFrustum; // Do we pass the frustum culling...?
    }

    cull = !isOutsideBounds;
}

//=============================================================================//
//                            Small Primitive Culling                          //
//=============================================================================//
void CullTriSmall(AABBox bboxSS, uint2 screenParams, out bool cull)
{
    float2 minPos = bboxSS.minCorner.xy;
    float2 maxPos = bboxSS.maxCorner.xy;
    minPos *= screenParams;
    maxPos *= screenParams;

    bool small = any( round(minPos) == round(maxPos) );
    cull = small;
}

//=============================================================================//
//                              Orientation Culling                            //
//=============================================================================//
bool CullOrientationBase(float4 positions[3])
{
    float det = determinant(float3x3(positions[0].xyw, positions[1].xyw, positions[2].xyw));
    bool cull = det <= 0.0;
    return cull;
}

void CullOrientation(float3 positions[3], out bool cull)
{
    float4 positionsVS[3];
    uint verticesInTri = 3;
    UNITY_UNROLL
    for (uint i = 0; i < verticesInTri; i++)
    {
        positionsVS[i] = mul(_MatrixV, float4(positions[i], 1.0));
        positionsVS[i] = float4(positionsVS[i].xyz, positionsVS[i].z);
    }
    
    cull = CullOrientationBase(positionsVS);
}

#endif
