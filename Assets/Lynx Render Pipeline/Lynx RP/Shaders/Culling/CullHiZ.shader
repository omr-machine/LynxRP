Shader "Custom RP/Other/Cull HiZ" 
{
    SubShader 
    {
        HLSLINCLUDE
        #include "../../ShaderLibrary/Common.hlsl"
        ENDHLSL
        Pass 
        {
            Name "Cull HiZ"
            
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 4.5
            #pragma enable_d3d11_debug_symbols
            #pragma vertex HiZPassVertex
            #pragma fragment HiZPassFragment
            #include "CullHiZPass.hlsl"
            ENDHLSL
        }
    }
}