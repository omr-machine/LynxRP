#ifndef CUSTOM_CSCULL_INPUT_INCLUDED
#define CUSTOM_CSCULL_INPUT_INCLUDED

#define NumThreadsXMax 256
#define NumThreadsXMaxGroups 32

uint _IndexSize;

int _TriCullFrustum;
int _TriCullOrientation;
int _TriCullSmall;
int _TriCullHiZ;

RWStructuredBuffer<Vertex> _IndexBuffer;
RWStructuredBuffer<Triangle> _TriangleBuffer;

RWStructuredBuffer<AABBox> _BBoxBuffer;
RWStructuredBuffer<Point> _QuadBuffer;

RWStructuredBuffer<uint> _VoteBuffer;

RWStructuredBuffer<uint> _ArgsBuffer;
RWStructuredBuffer<uint> _ArgsLineBuffer;
RWStructuredBuffer<uint> _ArgsQuadBuffer;

RWStructuredBuffer<Vertex> _VertexPassBuffer;
RWStructuredBuffer<Point> _BBoxPassBuffer;
RWStructuredBuffer<Point> _QuadPassBuffer;

RWTexture2D<float4> _CullDebugTexture;

#endif