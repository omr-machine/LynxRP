Shader "Custom RP/Other/MSAADownscale"
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
            #include "MSAADownscalePasses.hlsl"
        ENDHLSL
        Pass
        {
            Name "MSAA Blur Downscale"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex BlurDownscalePassVertex
                #pragma fragment BlurDownscalePassFragment
            ENDHLSL
        }
        Pass
        {
            Name "MSAA Blur Upscale"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex BlurUpscalePassVertex
                #pragma fragment BlurUpscalePassFragment
            ENDHLSL
        }

        Pass
        {
            Name "MSAA Downscale"

            HLSLPROGRAM
                #pragma target 3.5
                #pragma vertex DefaultPassVertex
                #pragma fragment DownscalePassFragment
            ENDHLSL
        }
    }
}

// https://github.com/Baedrick/Dual-Kawase-Blur-Demo
// https://community.arm.com/cfs-file/__key/communityserver-blogs-components-weblogfiles/00-00-00-20-66/siggraph2015_2D00_mmg_2D00_marius_2D00_notes.pdf