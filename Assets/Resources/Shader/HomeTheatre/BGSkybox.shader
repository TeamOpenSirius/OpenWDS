// Generated from frozen original GPU programs by recover_home_theatre_shaders.py
Shader "OpenWDS/HomeTheatre/BGSkybox" {
Properties {
[NoScaleOffset]_BaseTexture ("BaseTexture", 2D) = "white" {}
_BaseTextureColor ("BaseTextureColor", Color) = (0.800000011920929, 0.800000011920929, 0.800000011920929, 1.0)
_AlphaThreshould ("AlphaThreshould", Range(0.0, 1.0)) = 0.5
_ambientColor ("ambientColor", Color) = (1.0, 1.0, 1.0, 1.0)
_ambientIntensity ("ambientIntensity", Range(0.0, 1.0)) = 1.0
_DirLightAffect ("DirLightAffect", Range(0.0, 1.0)) = 1.0
_PrimaryLightDirection ("PrimaryLightDirection", Vector) = (0.0, 0.0, -1.0, 0.0)
_PrimaryLightColor ("PrimaryLightColor", Vector) = (1.0, 1.0, 1.0, 0.0)
_PrimaryLightIntensity ("PrimaryLightIntensity", Float) = 1.0
[HideInInspector]_CastShadows ("_CastShadows", Float) = 0.0
[HideInInspector]_Surface ("_Surface", Float) = 0.0
[HideInInspector]_Blend ("_Blend", Float) = 0.0
[HideInInspector]_AlphaClip ("_AlphaClip", Float) = 0.0
[HideInInspector]_SrcBlend ("_SrcBlend", Float) = 1.0
[HideInInspector]_DstBlend ("_DstBlend", Float) = 0.0
[ToggleUI][HideInInspector]_ZWrite ("_ZWrite", Float) = 1.0
[HideInInspector]_ZWriteControl ("_ZWriteControl", Float) = 0.0
[HideInInspector]_ZTest ("_ZTest", Float) = 4.0
[HideInInspector]_Cull ("_Cull", Float) = 2.0
[HideInInspector]_QueueOffset ("_QueueOffset", Float) = 0.0
[HideInInspector]_QueueControl ("_QueueControl", Float) = -1.0
[HideInInspector][NoScaleOffset]unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
[HideInInspector][NoScaleOffset]unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
[HideInInspector][NoScaleOffset]unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
}
SubShader { Tags { "RenderPipeline"="UniversalPipeline" }
Pass { Tags { "LightMode"="UniversalForward" }
Blend [_SrcBlend] [_DstBlend]
ZTest [_ZTest]
ZWrite [_ZWrite]
Cull [_Cull]
HLSLPROGRAM
#pragma target 4.5
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_local _ _ALPHATEST_ON
#pragma multi_compile_local _ _SURFACE_TYPE_TRANSPARENT
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_BaseTexture); SAMPLER(sampler_BaseTexture);
float4 _BaseTextureColor, _ambientColor;
float3 _PrimaryLightDirection, _PrimaryLightColor;
float _PrimaryLightIntensity, _ambientIntensity, _AlphaThreshould, _DirLightAffect;
struct Input { float3 p:POSITION; float3 n:NORMAL; float4 uv:TEXCOORD0; };
struct Output { float4 p:SV_POSITION; float2 uv:TEXCOORD0; float light:TEXCOORD1; float3 ambient:TEXCOORD2; };
Output vert(Input i) { Output o; o.p=TransformObjectToHClip(i.p); o.uv=i.uv.xy;
 o.light=dot(_PrimaryLightDirection,TransformObjectToWorldNormal(i.n));
 o.ambient=saturate(_PrimaryLightIntensity)*_ambientIntensity*_ambientColor.rgb; return o; }
float4 frag(Output i):SV_Target { float4 t=SAMPLE_TEXTURE2D_BIAS(_BaseTexture,sampler_BaseTexture,i.uv,_GlobalMipBias.x);
#if defined(_ALPHATEST_ON)
 clip(t.a-_AlphaThreshould);
#endif
 float3 rgb=(saturate(_DirLightAffect)*_PrimaryLightColor+i.ambient)*t.rgb*_BaseTextureColor.rgb;
#if defined(_SURFACE_TYPE_TRANSPARENT) || defined(_ALPHATEST_ON)
 return float4(rgb,t.a);
#else
 return float4(rgb,1);
#endif
}

ENDHLSL
} } }
