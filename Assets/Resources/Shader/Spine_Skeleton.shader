Shader "Spine/Skeleton" {
	Properties {
		_Cutoff ("Shadow alpha cutoff", Range(0, 1)) = 0.1
		[NoScaleOffset] _MainTex ("Main Texture", 2D) = "black" {}
		[Toggle(_STRAIGHT_ALPHA_INPUT)] _StraightAlphaInput ("Straight Alpha Texture", Float) = 0
		[HideInInspector] _StencilRef ("Stencil Reference", Float) = 1
		[Enum(UnityEngine.Rendering.CompareFunction)] [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
		[HideInInspector] _OutlineWidth ("Outline Width", Range(0, 8)) = 3
		[HideInInspector] _OutlineColor ("Outline Color", Vector) = (1,1,0,1)
		[HideInInspector] _OutlineReferenceTexWidth ("Reference Texture Width", Float) = 1024
		[HideInInspector] _ThresholdEnd ("Outline Threshold", Range(0, 1)) = 0.25
		[HideInInspector] _OutlineSmoothness ("Outline Smoothness", Range(0, 1)) = 1
		[MaterialToggle(_USE8NEIGHBOURHOOD_ON)] [HideInInspector] _Use8Neighbourhood ("Sample 8 Neighbours", Float) = 1
		[HideInInspector] _OutlineMipLevel ("Outline Mip Level", Range(0, 3)) = 0
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float4 _MainTex_ST;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Vertex_Stage_Output
			{
				float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.uv = (input.uv.xy * _MainTex_ST.xy) + _MainTex_ST.zw;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			Texture2D<float4> _MainTex;
			SamplerState sampler_MainTex;

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				return _MainTex.Sample(sampler_MainTex, input.uv.xy);
			}

			ENDHLSL
		}
	}
	//CustomEditor "SpineShaderWithOutlineGUI"
}