#ifndef CUSTOM_CSCULL_INPUT_INCLUDED
#define CUSTOM_CSCULL_INPUT_INCLUDED
#include <HLSLSupport.cginc>

#define NumThreadsXMax 256
#define NumThreadsXMaxGroups 32

#define VerticesInTri 3

uint _IndexSize;
uint _NumOfGroups;

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

RWStructuredBuffer<Triangle> _TriangleBuffer;

RWStructuredBuffer<uint> _VoteBuffer;
RWStructuredBuffer<uint> _ScanBuffer;
RWStructuredBuffer<uint> _ScanSumBuffer;
RWStructuredBuffer<uint> _GroupSumBuffer;
RWStructuredBuffer<uint> _GroupScanBuffer;

RWStructuredBuffer<uint> _ArgsBuffer;

RWStructuredBuffer<Vertex> _VertexPassBuffer;


#endif