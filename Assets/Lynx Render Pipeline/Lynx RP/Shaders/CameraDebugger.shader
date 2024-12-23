Shader "Hidden/Custom RP/Camera Debugger" 
{
	
	SubShader 
	{
		Cull Off
		ZTest Always
		ZWrite Off
		
		HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		#include "CameraDebuggerPasses.hlsl"
		ENDHLSL

		Pass 
		{
			Name "Forward+ Tiles"

			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
				#pragma target 4.5
				#pragma vertex DefaultPassVertex
				#pragma fragment ForwardPlusTilesPassFragment
			ENDHLSL
		}

		Pass 
		{
			Name "Depth"

			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
				#pragma target 4.5
				#pragma vertex DefaultPassVertex
				#pragma fragment DepthPassFragment
			ENDHLSL
		}

		Pass 
		{
			Name "Stencil"

			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
				#pragma target 4.5
				#pragma vertex DefaultPassVertex
				#pragma fragment StencilPassFragment
			ENDHLSL
		}

		Pass 
		{
			Name "Color"

			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
				#pragma target 4.5
				#pragma vertex DefaultPassVertex
				#pragma fragment ColorPassFragment
			ENDHLSL
		}

		Pass 
		{
			Name "Deferred Albedo Debug"

			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
				#pragma target 4.5
				#pragma vertex DefaultPassVertex
				#pragma fragment DeferredAlbedoPassFragment
			ENDHLSL
		}

		Pass 
		{
			Name "Deferred MRT Debug"

			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
				#pragma target 4.5
				#pragma vertex DefaultPassVertex
				#pragma fragment DeferredMRTPassFragment
			ENDHLSL
		}

		Pass 
		{
			Name "Culling Hi Z Debug"

			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
				#pragma target 4.5
				#pragma vertex DefaultPassVertex
				#pragma fragment HiZPassFragment
			ENDHLSL
		}

		Pass 
		{
			Name "Culling Hi Z Depth Difference Debug"

			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
				#pragma target 4.5
				#pragma vertex DefaultPassVertex
				#pragma fragment HiZDepthDifferencePassFragment
			ENDHLSL
		}
	}
}