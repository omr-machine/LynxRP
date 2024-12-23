#ifndef CUSTOM_BOUNDING_SHAPES_INCLUDED
#define CUSTOM_BOUNDING_SHAPES_INCLUDED

struct AABBox
{
    float3 minCorner;
    float3 maxCorner;
    float length;
};

// float GetNaN()
// {
//     return float NaN = 0.0f / 0.0f;
// }

bool AABBOverlap(float3 minA, float3 maxA, float3 minB, float3 maxB) {
    return (minA.x <= maxB.x && minB.x <= maxA.x) 
        && (minA.y <= maxB.y && minB.y <= maxA.y) 
        && (minA.z <= maxB.z && minB.z <= maxA.z);
}

bool AABBOverlap(float2 minA, float2 maxA, float2 minB, float2 maxB) {
    return (minA.x <= maxB.x && minB.x <= maxA.x)
        && (minA.y <= maxB.y && minB.y <= maxA.y);
}

bool RectIntersectRect(float2 bottomLeft0, float2 topRight0, float2 bottomLeft1, float2 topRight1)
{
    return !(any(topRight0.x < bottomLeft1.x || topRight0.y < bottomLeft1.y)
        || any(bottomLeft0.x > topRight1.x || bottomLeft0.y > topRight1.y));
}

void GetCornersAABB(AABBox bbox, out float4 corners[8])
{
    float3 minPos = bbox.minCorner;
    float3 maxPos = bbox.maxCorner;
    corners[0] = float4(minPos.x, minPos.y, minPos.z, 1.0);
    corners[1] = float4(minPos.x, minPos.y, maxPos.z, 1.0);
    corners[2] = float4(minPos.x, maxPos.y, minPos.z, 1.0);
    corners[3] = float4(minPos.x, maxPos.y, maxPos.z, 1.0);
    corners[4] = float4(maxPos.x, minPos.y, minPos.z, 1.0);
    corners[5] = float4(maxPos.x, minPos.y, maxPos.z, 1.0);
    corners[6] = float4(maxPos.x, maxPos.y, minPos.z, 1.0);
    corners[7] = float4(maxPos.x, maxPos.y, maxPos.z, 1.0);
}

void GetAABBox(float3 positions[3], out AABBox bbox)
{
    bbox.minCorner = min(positions[0], min(positions[1], positions[2]));
    bbox.maxCorner = max(positions[0], max(positions[1], positions[2]));
    bbox.length = distance(bbox.maxCorner, bbox.minCorner);
}

#endif