#ifndef CUSTOM_PRIMITIVES_INCLUDED
#define CUSTOM_PRIMITIVES_INCLUDED

#define VerticesInTri 3

struct Vertex
{
    float3 position;
    float3 normal;
    float4 color;
    float2 baseUV;
};

struct Triangle
{
    Vertex v1;
    Vertex v2;
    Vertex v3;
};

struct Point
{
    float3 position;
    float3 color;
};

#endif