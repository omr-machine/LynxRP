#ifndef CUSTOM_CSCULL_DRAW_INCLUDED
#define CUSTOM_CSCULL_DRAW_INCLUDED

void DrawTriangle(Triangle triangleToDraw, bool culled)
{
    Vertex vertices[3];
    vertices[0] = triangleToDraw.v1;
    vertices[1] = triangleToDraw.v2;
    vertices[2] = triangleToDraw.v3;
    
    uint index;
    InterlockedAdd(_ArgsBuffer[0], 1 * VerticesInTri, index);

    if (culled)
    {
        vertices[0].position.x = getNaNSqrt(-1.0);
    }
    
    _VertexPassBuffer[index] = vertices[0];
    _VertexPassBuffer[index + 1] = vertices[1];
    _VertexPassBuffer[index + 2] = vertices[2];
}

void DrawLine(AABBox lineToDraw, bool culled)
{
    float3 minCorner = lineToDraw.minCorner;
    float3 maxCorner = lineToDraw.maxCorner;
    float length = lineToDraw.length;
    float3 otherCorner = float3(minCorner.x, minCorner.y + length, minCorner.z);
    float3 colorStart = float3(1, 0, 0);
    float3 colorEnd = float3(0, 0, 1);
    if (culled)
        colorStart = float3(1, 1, 0);
    
    uint indexLine;
    InterlockedAdd(_ArgsLineBuffer[0], 1 * 2, indexLine);
    
    _BBoxPassBuffer[indexLine].position = minCorner;
    _BBoxPassBuffer[indexLine].color = colorStart;
    _BBoxPassBuffer[indexLine + 1].position = otherCorner;//maxCorner;
    _BBoxPassBuffer[indexLine + 1].color = colorEnd;
}

void DrawPoint()
{
    uint indexQuad;
    InterlockedAdd(_ArgsQuadBuffer[0], 1, indexQuad);
    _QuadPassBuffer[indexQuad].position = _QuadBuffer[indexQuad].position;
    // if (_ProjectionParams.z == 1001)
    // {
    //     _QuadPassBuffer[indexQuad].color = float3(0, 1, 0);
    // }
    // else
    // {
    //     _QuadPassBuffer[indexQuad].color = float3(0, 0, 1);
    // }
    _QuadPassBuffer[indexQuad].color = _QuadBuffer[indexQuad].color;
}

#endif