Shader "Hidden/Custom RP/Camera Renderer" 
{
	
	SubShader 
	{
		Cull Off
		ZTest Always
		ZWrite Off
		
		HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		#include "CameraRendererPasses.hlsl"
		ENDHLSL

		Pass 
		{
			Name "Copy"

			// ZWrite Off

			Blend [_CameraSrcBlend] [_CameraDstBlend]

			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment CopyPassFragment
			ENDHLSL
		}

		Pass
		{
			Name "Copy Depth"

			ColorMask 0
			ZWrite On

			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment CopyDepthPassFragment
			ENDHLSL
		}

		Pass 
		{
			Name "Copy Invert"

			// ZWrite Off

			Blend [_CameraSrcBlend] [_CameraDstBlend]

			HLSLPROGRAM
				#pragma target 3.5
				#pragma vertex DefaultPassVertex
				#pragma fragment CopyInvertPassFragment
			ENDHLSL
		}
	}
}