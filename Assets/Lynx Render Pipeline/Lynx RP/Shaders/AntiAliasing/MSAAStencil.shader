Shader "Custom RP/Other/MSAAStencil"
{
    Properties 
    {
    }

    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off

        HLSLINCLUDE
            #include "../../ShaderLibrary/Common.hlsl"
        ENDHLSL
        Pass
        {
            Name "MSAA Stencil"

            HLSLPROGRAM
                #pragma target 3.5
                // #pragma multi_compile_instancing
                #pragma shader_feature _CLIPPING
                #pragma vertex UnlitPassVertex
                #pragma fragment UnlitPassFragment

                struct Attributes
                {
                    float3 positionOS : POSITION;
                };

                struct Varyings
                {
                    float4 positionCS_SS : SV_POSITION;
                };

                Varyings UnlitPassVertex (Attributes input)
                {
                    Varyings output;
                    float3 positionWS = TransformObjectToWorld(input.positionOS);
                    output.positionCS_SS = TransformWorldToHClip(positionWS);

                    return output;
                }

                float4 UnlitPassFragment (Varyings input) : SV_TARGET
                {       
                    // return color;
                    return float4(1.0, 1.0, 1.0, 1.0);
                }
            
            ENDHLSL
        }
    }
}
