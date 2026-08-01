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
			float4 _TimeParameters; float ShutterMaskPositionZ; int IsTouchMask;
			float TouchMaskPositionZ; float TouchMaskThresfold; int _IsScratch;

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
				float4 world=mul(unity_ObjectToWorld,input.pos); output.worldZ=world.z; output.pos=mul(unity_MatrixVP,world);
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
				float x=input.uv.x; float2 a=saturate((x.xx-float2(.7,.3))/float2(.3,-.3)); a=a*a*(3-2*a); float edge=min((1-min(pow(a.x+a.y,.08),1))*200,1); float touch=IsTouchMask!=0?smoothstep(0,TouchMaskThresfold,input.worldZ-TouchMaskPositionZ):1; float2 w=saturate((x.xx-float2(.25,.75))/float2(.75,-.75)); w=w*w*(3-2*w); float inner=min(pow(w.x+w.y,1.2),1); float2 n=saturate((x.xx-float2(.3495,.6505))/float2(.6505,-.6505)); n=n*n*(3-2*n); float narrow=min(pow(n.x+n.y,.08),1); float alpha=edge*touch*saturate(inner*.5+.3+(IsTouchMask!=0?sin(_TimeParameters.x*30)*.3*(1-narrow):0)); if(input.worldZ>=ShutterMaskPositionZ) alpha=0; bool s=_IsScratch!=0; float3 b=s?float3(.706069827,.26666671,.952941179):float3(.265174389,.831479371,.952830195); float3 h=s?(IsTouchMask!=0?float3(.97012198,.713725507,1):float3(.933603287,.619607925,1)):(IsTouchMask!=0?float3(.71226418,1,.90442729):float3(.617924571,1,.906408608)); return float4((b+h*(inner*.5))*input.color.rgb,alpha);
			}

			ENDHLSL
		}
	}
	Fallback "Hidden/Shader Graph/FallbackError"
	//CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
}
