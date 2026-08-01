Shader "Effect/Alpha" {
	Properties {
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Color ("Tint", Vector) = (1,1,1,1)
		[HideInInspector] _TextureSampleAdd ("Texture Sample Add", Vector) = (0,0,0,0)
	}
	SubShader{
		Tags {
			"Queue"="Transparent"
			"RenderType"="Transparent"
			"CanUseSpriteAtlas"="True"
			"IgnoreProjector"="True"
			"PreviewType"="Plane"
		}

		Pass
		{
			Name "Default"
			Blend One OneMinusSrcAlpha
			ZTest Off
			ZWrite Off
			Cull Off

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float4 _MainTex_ST;
			float4 _Color;
			float4 _TextureSampleAdd;
			Texture2D<float4> _MainTex;
			SamplerState sampler_MainTex;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
			};

			struct Vertex_Stage_Output
			{
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.uv = (input.uv.xy * _MainTex_ST.xy) + _MainTex_ST.zw;
				output.color = input.color * _Color;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				// The original GLES3 program quantizes animated sprite alpha to 8-bit,
				// then emits premultiplied RGB for Blend One OneMinusSrcAlpha.
				float alpha = round(input.color.a * 255.0) / 255.0;
				float4 sampled = _MainTex.Sample(sampler_MainTex, input.uv.xy) + _TextureSampleAdd;
				float4 combined = sampled * float4(input.color.rgb, alpha);
				return float4(combined.rgb * combined.a, combined.a);
			}

			ENDHLSL
		}
	}
}
