Shader "Custom RP/Other/Deferred/Deferred"
{
    Properties 
    {
    }

    SubShader
    {
        ZWrite On

        HLSLINCLUDE
            #include "../../../ShaderLibrary/Common.hlsl"
            #include "DeferredPasses.hlsl"
        ENDHLSL
        Pass
        {
            Name "Deferred Buffers"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment DefaultPassFragment
            ENDHLSL
        }

        Pass 
        {
            Name "Deferred Buffer Lighting Lit"


            // Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha
            Cull Off
            ZTest Always
            ZWrite Off

            HLSLPROGRAM
            #pragma target 4.5
            // #define _RECEIVE_SHADOWS float _receiveShadows = 1
            // #define _PREMULTIPLY_ALPHA float _premultiplyAlpha = 1
            #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
			#pragma multi_compile _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7
            #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
			#pragma multi_compile _ _SHADOW_MASK_ALWAYS _SHADOW_MASK_DISTANCE
            #pragma multi_compile_instancing
            #pragma vertex LitDeferredBufferLightingPassVertex
            #pragma fragment LitDeferredBufferLightingPassFragment
            #include "DeferredPasses.hlsl"
            ENDHLSL
        }

        Pass 
        {
            Name "Deferred Draw Test"


            Cull Off
            ZTest Always
            ZWrite Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex LitDeferredBufferLightingPassVertex
            #pragma fragment LitDeferredBufferLightingPassFragment
            #include "DeferredPasses.hlsl"
            ENDHLSL
        }
    }
}