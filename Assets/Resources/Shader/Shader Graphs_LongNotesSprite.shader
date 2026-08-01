Shader "Shader Graphs/LongNotesSprite" {
	Properties {
		[NoScaleOffset] _MainTex ("MainTex", 2D) = "white" {}
		ShutterMaskPositionZ ("ShutterMaskPositionZ", Float) = 0
		[ToggleUI] IsTouchMask ("IsTouchMask", Integer) = 0
		TouchMaskPositionZ ("TouchMaskPositionZ", Float) = 0
		TouchMaskThresfold ("TouchMaskThresfold", Float) = 1
		[ToggleUI] _IsScratch ("IsScratch", Integer) = 0
		[HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
		[HideInInspector] _QueueControl ("_QueueControl", Float) = -1
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
		LOD 200

		Pass
		{
			Name "LongNotes"
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Cull Off
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;
			float4 _MainTex_ST;
			float4 _TimeParameters;
			float ShutterMaskPositionZ;
			int IsTouchMask;
			float TouchMaskPositionZ;
			float TouchMaskThresfold;
			int _IsScratch;

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
				float worldZ : TEXCOORD1;
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.uv = (input.uv.xy * _MainTex_ST.xy) + _MainTex_ST.zw;
				output.color = input.color;
				float4 world = mul(unity_ObjectToWorld, input.pos);
				output.worldZ = world.z;
				output.pos = mul(unity_MatrixVP, world);
				return output;
			}

			Texture2D<float4> _MainTex;
			SamplerState sampler_MainTex;

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
				float4 color : COLOR;
				float worldZ : TEXCOORD1;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				float x = input.uv.x;
				float2 outer = saturate(
					(x.xx - float2(0.7, 0.3)) / float2(0.3, -0.3));
				outer = outer * outer * (3.0 - 2.0 * outer);
				float outerProfile = min(pow(outer.x + outer.y, 0.08), 1.0);
				float edgeAlpha = min((1.0 - outerProfile) * 200.0, 1.0);

				float touchFactor = IsTouchMask != 0.0
					? smoothstep(
						0.0,
						TouchMaskThresfold,
						input.worldZ - TouchMaskPositionZ)
					: 1.0;

				float2 innerWide = saturate(
					(x.xx - float2(0.25, 0.75)) / float2(0.75, -0.75));
				innerWide = innerWide * innerWide * (3.0 - 2.0 * innerWide);
				float innerProfile = min(pow(innerWide.x + innerWide.y, 1.2), 1.0);

				float2 innerNarrow = saturate(
					(x.xx - float2(0.3495, 0.6505)) /
					float2(0.6505, -0.6505));
				innerNarrow = innerNarrow * innerNarrow * (3.0 - 2.0 * innerNarrow);
				float narrowProfile = min(pow(innerNarrow.x + innerNarrow.y, 0.08), 1.0);

				float pulse = IsTouchMask != 0.0
					? sin(_TimeParameters.x * 30.0) * 0.3 * (1.0 - narrowProfile)
					: 0.0;
				float brightness = saturate(innerProfile * 0.5 + 0.3 + pulse);
				float alpha = edgeAlpha * touchFactor * brightness;
				if (input.worldZ >= ShutterMaskPositionZ) alpha = 0.0;

				bool scratch = _IsScratch != 0.0;
				float3 baseColor = scratch
					? float3(0.706069827, 0.26666671, 0.952941179)
					: float3(0.265174389, 0.831479371, 0.952830195);
				float3 highColor = scratch
					? (IsTouchMask != 0.0
						? float3(0.97012198, 0.713725507, 1.0)
						: float3(0.933603287, 0.619607925, 1.0))
					: (IsTouchMask != 0.0
						? float3(0.71226418, 1.0, 0.90442729)
						: float3(0.617924571, 1.0, 0.906408608));
				float3 rgb =
					(baseColor + highColor * (innerProfile * 0.5)) * input.color.rgb;
				return float4(rgb, alpha);
			}

			ENDHLSL
		}
	}
	Fallback "Hidden/Shader Graph/FallbackError"
	//CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
}
