Shader "Custom RP/Other/MSAAFill"
{
    Properties 
    {
    }

    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite On

        HLSLINCLUDE
            #include "../../ShaderLibrary/Common.hlsl"
            #include "MSAAFillPasses.hlsl"
        ENDHLSL
        Pass
        {
            Name "MSAA Fill Color And Depth"

            // Blend [_FinalSrcBlend] [_FinalDstBlend]

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment ColorPassFragment
            ENDHLSL
        }
    }
}
