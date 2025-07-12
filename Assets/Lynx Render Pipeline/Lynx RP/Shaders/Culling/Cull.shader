Shader "Custom RP/Other/Cull" 
{
    Properties 
    {
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
    }

    SubShader 
    {
        HLSLINCLUDE
        #include "../../ShaderLibrary/Common.hlsl"
		#include "CullInput.hlsl"
        ENDHLSL
        Pass 
        {
            Name "Cull Test"

            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma target 5.0
            #pragma require geometry
            // #pragma multi_compile _CULL_PASS_GEOMETRY_FLIPY
            #pragma multi_compile_instancing
            #pragma vertex CullPassVertex
            // #pragma geometry CullPassGeometry
            #pragma fragment CullPassFragment
            #include "CullPass.hlsl"
            #include "CullPassGeometry.hlsl"
            ENDHLSL
        }

        Pass 
        {
            Name "Cull Test Line"

            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma target 5.0
            #pragma multi_compile_instancing
            #pragma vertex CullLinePassVertex
            #pragma fragment CullLinePassFragment
            #include "CullPass.hlsl"
            ENDHLSL
        }

        Pass 
        {
            Name "Cull Test Point"

            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 5.0
            #pragma multi_compile_instancing
            #pragma vertex CullPointPassVertex
            #pragma fragment CullPointPassFragment
            #include "CullPass.hlsl"
            ENDHLSL
        }

        Pass 
        {
            Name "GPUDriven Test"

            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma target 5.0
            #pragma multi_compile_instancing
            #pragma vertex LitGPUDrivenPassVertex
            #pragma fragment LitGPUDrivenPassFragment
            #include "CullPass.hlsl"
            ENDHLSL
        }
    }
}