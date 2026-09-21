Shader "UI/MaskAdditive" {
	Properties {
		[PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		_StencilComp ("Stencil Comparison", Float) = 8
		_Stencil ("Stencil ID", Float) = 0
		_StencilOp ("Stencil Operation", Float) = 0
		_StencilWriteMask ("Stencil Write Mask", Float) = 255
		_StencilReadMask ("Stencil Read Mask", Float) = 255
		_ColorMask ("Color Mask", Float) = 15
		_ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
	}

	SubShader {
		Tags {
			"Queue"="Transparent"
			"RenderType"="Transparent"
			"IgnoreProjector"="True"
			"PreviewType"="Plane"
			"CanUseSpriteAtlas"="True"
		}
		Stencil {
			Ref [_Stencil]
			Comp [_StencilComp]
			Pass [_StencilOp]
			ReadMask [_StencilReadMask]
			WriteMask [_StencilWriteMask]
		}
		Cull Off
		Lighting Off
		ZWrite Off
		ZTest [unity_GUIZTestMode]
		Blend One One
		ColorMask [_ColorMask]

		Pass {
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float4 _Color;
			float4 _ClipRect;

			struct appdata {
				float4 vertex : POSITION;
				float4 color : COLOR;
				float2 uv : TEXCOORD0;
			};

			struct v2f {
				float4 vertex : SV_POSITION;
				float4 color : COLOR;
				float2 uv : TEXCOORD0;
				float4 localPosition : TEXCOORD1;
			};

			v2f vert(appdata input) {
				v2f output;
				output.vertex = UnityObjectToClipPos(input.vertex);
				output.color = input.color * _Color;
				output.uv = TRANSFORM_TEX(input.uv, _MainTex);
				output.localPosition = input.vertex;
				return output;
			}

			float4 frag(v2f input) : SV_Target {
				float inside = step(_ClipRect.x, input.localPosition.x) *
					step(_ClipRect.y, input.localPosition.y) *
					step(input.localPosition.x, _ClipRect.z) *
					step(input.localPosition.y, _ClipRect.w);
				float4 sampled = tex2D(_MainTex, input.uv) * input.color;
				float alpha = inside * sampled.a;
				// Original GLES3 program: clip before additive premultiplied output.
                clip(alpha - 0.01);
                return float4(sampled.rgb * alpha, alpha);
			}
			ENDHLSL
		}
	}
}
