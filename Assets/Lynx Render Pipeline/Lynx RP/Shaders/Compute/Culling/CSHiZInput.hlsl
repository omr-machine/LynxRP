#ifndef CUSTOM_CS_HIZ_INPUT_INCLUDED
#define CUSTOM_CS_HIZ_INPUT_INCLUDED

int _MipLevel;

float4 _SourceMipParams;
float4 _DestMipParams;
float4 _PrevFrameDepthParams;

RWStructuredBuffer<Point> _HiZReprojectionPointBuffer;

Texture2D<float4> _SourceMip;
Texture2D<float4> _PrevFrameDepth;

RWTexture2D<float4> _DestMip;
RWTexture2D<float4> _FrameData;
RWTexture2D<float4> _ProjectedDepthTexture;

SamplerState point_Clamp_SourceMip;
SamplerState linear_Clamp_SourceMip;

void StoreFrameMatrix(float4x4 mat, uint mx, uint my)
{
    uint2 m00; uint2 m01; uint2 m02; uint2 m03;
    uint2 m10; uint2 m11; uint2 m12; uint2 m13;
    uint2 m20; uint2 m21; uint2 m22; uint2 m23;
    uint2 m30; uint2 m31; uint2 m32; uint2 m33;

    float4 mv00; float4 mv01; float4 mv02; float4 mv03;
    float4 mv10; float4 mv11; float4 mv12; float4 mv13;
    float4 mv20; float4 mv21; float4 mv22; float4 mv23;
    float4 mv30; float4 mv31; float4 mv32; float4 mv33;
    
    m00 = uint2(mx, my);   m01 = uint2(mx+1, my);   m02 = uint2(mx+2, my);   m03 = uint2(mx+3, my);
    m10 = uint2(mx, my+1); m11 = uint2(mx+1, my+1); m12 = uint2(mx+2, my+1); m13 = uint2(mx+3, my+1);
    m20 = uint2(mx, my+2); m21 = uint2(mx+1, my+2); m22 = uint2(mx+2, my+2); m23 = uint2(mx+3, my+2); 
    m30 = uint2(mx, my+3); m31 = uint2(mx+1, my+3); m32 = uint2(mx+2, my+3); m33 = uint2(mx+3, my+3);

    mv00 = mat[0][0]; mv01 = mat[0][1]; mv02 = mat[0][2]; mv03 = mat[0][3];
    mv10 = mat[1][0]; mv11 = mat[1][1]; mv12 = mat[1][2]; mv13 = mat[1][3];
    mv20 = mat[2][0]; mv21 = mat[2][1]; mv22 = mat[2][2]; mv23 = mat[2][3];
    mv30 = mat[3][0]; mv31 = mat[3][1]; mv32 = mat[3][2]; mv33 = mat[3][3];
        
    _FrameData[m00] = mv00; _FrameData[m01] = mv01; _FrameData[m02] = mv02; _FrameData[m03] = mv03;
    _FrameData[m10] = mv10; _FrameData[m11] = mv11; _FrameData[m12] = mv12; _FrameData[m13] = mv13;
    _FrameData[m20] = mv20; _FrameData[m21] = mv21; _FrameData[m22] = mv22; _FrameData[m23] = mv23;
    _FrameData[m30] = mv30; _FrameData[m31] = mv31; _FrameData[m32] = mv32; _FrameData[m33] = mv33;
}

float4x4 ReadFrameMatrix(uint mx, uint my)
{
    float4x4 mat;
    uint2 m00; uint2 m01; uint2 m02; uint2 m03;
    uint2 m10; uint2 m11; uint2 m12; uint2 m13;
    uint2 m20; uint2 m21; uint2 m22; uint2 m23;
    uint2 m30; uint2 m31; uint2 m32; uint2 m33;

    float4 mv00; float4 mv01; float4 mv02; float4 mv03;
    float4 mv10; float4 mv11; float4 mv12; float4 mv13;
    float4 mv20; float4 mv21; float4 mv22; float4 mv23;
    float4 mv30; float4 mv31; float4 mv32; float4 mv33;
    
    m00 = uint2(mx, my);   m01 = uint2(mx+1, my);   m02 = uint2(mx+2, my);   m03 = uint2(mx+3, my);
    m10 = uint2(mx, my+1); m11 = uint2(mx+1, my+1); m12 = uint2(mx+2, my+1); m13 = uint2(mx+3, my+1);
    m20 = uint2(mx, my+2); m21 = uint2(mx+1, my+2); m22 = uint2(mx+2, my+2); m23 = uint2(mx+3, my+2); 
    m30 = uint2(mx, my+3); m31 = uint2(mx+1, my+3); m32 = uint2(mx+2, my+3); m33 = uint2(mx+3, my+3);
    
    mv00 = _FrameData[m00]; mv01 = _FrameData[m01]; mv02 = _FrameData[m02]; mv03 = _FrameData[m03];
    mv10 = _FrameData[m10]; mv11 = _FrameData[m11]; mv12 = _FrameData[m12]; mv13 = _FrameData[m13];
    mv20 = _FrameData[m20]; mv21 = _FrameData[m21]; mv22 = _FrameData[m22]; mv23 = _FrameData[m23];
    mv30 = _FrameData[m30]; mv31 = _FrameData[m31]; mv32 = _FrameData[m32]; mv33 = _FrameData[m33];

    mat[0][0] = mv00; mat[0][1] = mv01; mat[0][2] = mv02; mat[0][3] = mv03;
    mat[1][0] = mv10; mat[1][1] = mv11; mat[1][2] = mv12; mat[1][3] = mv13;
    mat[2][0] = mv20; mat[2][1] = mv21; mat[2][2] = mv22; mat[2][3] = mv23;
    mat[3][0] = mv30; mat[3][1] = mv31; mat[3][2] = mv32; mat[3][3] = mv33;
    return mat;
}

void StoreFrameExtras(uint mx, uint my, float3 extras)
{
    uint2 m00; uint2 m01; uint2 m02;
    m00 = uint2(mx, my); m01 = uint2(mx+1, my); m02 = uint2(mx+2, my);
    
    _FrameData[m00] = extras.x; _FrameData[m01] = extras.y; _FrameData[m02] = extras.z;
}

float3 ReadFrameExtras(uint mx, uint my)
{
    float3 extras;
    uint2 m00; uint2 m01; uint2 m02;
    m00 = uint2(mx, my); m01 = uint2(mx+1, my); m02 = uint2(mx+2, my);
    
    extras.x = _FrameData[m00]; extras.y = _FrameData[m01]; extras.z = _FrameData[m02];
    return extras;
}

#endif
