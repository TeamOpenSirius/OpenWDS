// Generated from frozen original GPU programs by recover_home_theatre_shaders.py
Shader "OpenWDS/HomeTheatre/Performance" {
Properties {
[NoScaleOffset]_MainTex ("_MainTex", 2D) = "white" {}
[HideInInspector]_CastShadows ("_CastShadows", Float) = 0.0
[HideInInspector]_Surface ("_Surface", Float) = 1.0
[HideInInspector]_Blend ("_Blend", Float) = 0.0
[HideInInspector]_AlphaClip ("_AlphaClip", Float) = 0.0
[HideInInspector]_SrcBlend ("_SrcBlend", Float) = 1.0
[HideInInspector]_DstBlend ("_DstBlend", Float) = 0.0
[ToggleUI][HideInInspector]_ZWrite ("_ZWrite", Float) = 0.0
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
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"



static float4 gl_Position;
static float3 vert_11;
static float4 vert_94;
static float4 vert_96;
static float4 vert_98;
static float4 vert_99;
static float4 vert_101;
static float4 vert_102;
static float4 vert_104;
static float4 vert_105;
static float3 vert_108;
static float3 vert_116;
static float3 vert_148;
static float3 vert_163;

struct vert_Input
{
    float3 vert_11 : POSITION;
    float3 vert_116 : NORMAL;
    float4 vert_96 : TEXCOORD0;
    float4 vert_99 : TEXCOORD1;
    float4 vert_102 : TEXCOORD2;
    float4 vert_105 : COLOR;
};

struct vert_Output
{
    float4 vert_94 : TEXCOORD0;
    float4 vert_98 : TEXCOORD1;
    float4 vert_101 : TEXCOORD2;
    float4 vert_104 : TEXCOORD3;
    float3 vert_108 : TEXCOORD4;
    float3 vert_148 : TEXCOORD5;
    float3 vert_163 : TEXCOORD6;
    float4 gl_Position : SV_Position;
};

static float3 vert_9;
static float4 vert_58;
static float vert_137;
static bool vert_156;

void vert_main()
{
    vert_9 = vert_11.yyy * transpose(UNITY_MATRIX_M)[1].xyz;
    vert_9 = (transpose(UNITY_MATRIX_M)[0].xyz * vert_11.xxx) + vert_9;
    vert_9 = (transpose(UNITY_MATRIX_M)[2].xyz * vert_11.zzz) + vert_9;
    vert_9 += transpose(UNITY_MATRIX_M)[3].xyz;
    vert_58 = vert_9.yyyy * transpose(UNITY_MATRIX_VP)[1];
    vert_58 = (transpose(UNITY_MATRIX_VP)[0] * vert_9.xxxx) + vert_58;
    vert_58 = (transpose(UNITY_MATRIX_VP)[2] * vert_9.zzzz) + vert_58;
    gl_Position = vert_58 + transpose(UNITY_MATRIX_VP)[3];
    vert_94 = vert_96;
    vert_98 = vert_99;
    vert_101 = vert_102;
    vert_104 = vert_105;
    vert_108 = vert_9;
    vert_9 = (-vert_9) + _WorldSpaceCameraPos;
    vert_58.x = dot(vert_116, transpose(UNITY_MATRIX_I_M)[0].xyz);
    vert_58.y = dot(vert_116, transpose(UNITY_MATRIX_I_M)[1].xyz);
    vert_58.z = dot(vert_116, transpose(UNITY_MATRIX_I_M)[2].xyz);
    vert_137 = dot(vert_58.xyz, vert_58.xyz);
    vert_137 = max(vert_137, 1.1754943508222875079687365372222e-38f);
    vert_137 = rsqrt(vert_137);
    vert_148 = vert_137.xxx * vert_58.xyz;
    vert_156 = unity_OrthoParams.w == 0.0f;
    float vert_166;
    if (vert_156)
    {
        vert_166 = vert_9.x;
    }
    else
    {
        vert_166 = transpose(UNITY_MATRIX_V)[0].z;
    }
    vert_163.x = vert_166;
    float vert_178;
    if (vert_156)
    {
        vert_178 = vert_9.y;
    }
    else
    {
        vert_178 = transpose(UNITY_MATRIX_V)[1].z;
    }
    vert_163.y = vert_178;
    float vert_189;
    if (vert_156)
    {
        vert_189 = vert_9.z;
    }
    else
    {
        vert_189 = transpose(UNITY_MATRIX_V)[2].z;
    }
    vert_163.z = vert_189;

}

vert_Output vert(vert_Input stage_input)
{
    vert_11 = stage_input.vert_11;
    vert_96 = stage_input.vert_96;
    vert_99 = stage_input.vert_99;
    vert_102 = stage_input.vert_102;
    vert_105 = stage_input.vert_105;
    vert_116 = stage_input.vert_116;
    vert_main();
    vert_Output stage_output;
    stage_output.gl_Position = gl_Position;
    stage_output.vert_94 = vert_94;
    stage_output.vert_98 = vert_98;
    stage_output.vert_101 = vert_101;
    stage_output.vert_104 = vert_104;
    stage_output.vert_108 = vert_108;
    stage_output.vert_148 = vert_148;
    stage_output.vert_163 = vert_163;
    return stage_output;
}


Texture2D<float4> _MainTex;
SamplerState sampler_MainTex;
Texture2D<float4> _CameraDepthTexture;
SamplerState sampler_CameraDepthTexture;

static float3 frag_11;
static float3 frag_33;
static float3 frag_45;
static float4 frag_117;
static float4 frag_125;
static float4 frag_132;
static float4 frag_137;
static float4 frag_383;

struct frag_Input
{
    float4 frag_117 : TEXCOORD0;
    float4 frag_132 : TEXCOORD1;
    float4 frag_137 : TEXCOORD2;
    float4 frag_125 : TEXCOORD3;
    float3 frag_45 : TEXCOORD4;
    float3 frag_11 : TEXCOORD5;
    float3 frag_33 : TEXCOORD6;
};

struct frag_Output
{
    float4 frag_383 : SV_Target0;
};

static float3 frag_9;
static float frag_32;
static float3 frag_39;
static float3 frag_44;
static float4 frag_90;
static float4 frag_131;
static float frag_148;
static bool frag_178;
static float frag_185;
static float4 frag_207;
static bool frag_370;

void frag_main()
{
    frag_9.x = dot(frag_11, frag_11);
    frag_9.x = sqrt(frag_9.x);
    frag_9.x = 1.0f / frag_9.x;
    frag_9 = frag_9.xxx * frag_11;
    frag_32 = dot(frag_33, frag_33);
    frag_32 = rsqrt(frag_32);
    frag_39 = frag_32.xxx * frag_33;
    frag_44 = frag_45.yyy * transpose(UNITY_MATRIX_VP)[1].xyw;
    frag_44 = (transpose(UNITY_MATRIX_VP)[0].xyw * frag_45.xxx) + frag_44;
    frag_44 = (transpose(UNITY_MATRIX_VP)[2].xyw * frag_45.zzz) + frag_44;
    frag_44 += transpose(UNITY_MATRIX_VP)[3].xyw;
    float2 frag_95 = frag_44.xz * 0.5f.xx;
    frag_90 = float4(frag_95.x, frag_90.y, frag_95.y, frag_90.w);
    frag_32 = frag_44.y * _ProjectionParams.x;
    frag_90.w = frag_32 * 0.5f;
    float2 frag_113 = frag_90.zz + frag_90.xw;
    frag_44 = float3(frag_113.x, frag_113.y, frag_44.z);
    frag_32 = frag_117.z + 1.0f;
    float3 frag_128 = frag_32.xxx * frag_125.xyz;
    frag_90 = float4(frag_128.x, frag_128.y, frag_128.z, frag_90.w);
    frag_131 = float4(frag_132.zw.x, frag_132.zw.y, frag_131.z, frag_131.w);
    frag_131.z = frag_137.x;
    float3 frag_145 = frag_90.xyz * frag_131.xyz;
    frag_90 = float4(frag_145.x, frag_145.y, frag_145.z, frag_90.w);
    frag_148 = _MainTex.SampleBias(sampler_MainTex, frag_117.xy, _GlobalMipBias.x).x;
    frag_32 = frag_148 * frag_125.w;
    float2 frag_173 = frag_44.xy / frag_44.zz;
    frag_44 = float3(frag_173.x, frag_173.y, frag_44.z);
    frag_178 = unity_OrthoParams.w == 1.0f;
    if (frag_178)
    {
        frag_185 = _CameraDepthTexture.SampleBias(sampler_CameraDepthTexture, frag_44.xy, _GlobalMipBias.x).x;
        float2 frag_204 = (frag_44.xy * 2.0f.xx) + (-1.0f).xx;
        frag_131 = float4(frag_204.x, frag_204.y, frag_131.z, frag_131.w);
        frag_207 = frag_131.yyyy * transpose(UNITY_MATRIX_I_VP)[1];
        frag_131 = (transpose(UNITY_MATRIX_I_VP)[0] * frag_131.xxxx) + frag_207;
        frag_131 = (transpose(UNITY_MATRIX_I_VP)[2] * frag_185.xxxx) + frag_131;
        frag_131 += transpose(UNITY_MATRIX_I_VP)[3];
        float3 frag_237 = frag_131.xyz / frag_131.www;
        frag_131 = float4(frag_237.x, frag_237.y, frag_237.z, frag_131.w);
        frag_185 = frag_131.y * transpose(UNITY_MATRIX_V)[1].z;
        frag_185 = (transpose(UNITY_MATRIX_V)[0].z * frag_131.x) + frag_185;
        frag_185 = (transpose(UNITY_MATRIX_V)[2].z * frag_131.z) + frag_185;
        frag_185 += transpose(UNITY_MATRIX_V)[3].z;
        frag_185 = abs(frag_185);
    }
    else
    {
        frag_44.x = _CameraDepthTexture.SampleBias(sampler_CameraDepthTexture, frag_44.xy, _GlobalMipBias.x).x;
        frag_44.x = (_ZBufferParams.z * frag_44.x) + _ZBufferParams.w;
        frag_185 = 1.0f / frag_44.x;
    }
    frag_185 = (-frag_44.z) + frag_185;
    frag_44.x = 1.0f / frag_117.w;
    frag_185 *= frag_44.x;
    frag_185 = clamp(frag_185, 0.0f, 1.0f);
    frag_44.x = (frag_185 * (-2.0f)) + 3.0f;
    frag_185 *= frag_185;
    frag_185 *= frag_44.x;
    frag_32 *= frag_185;
    frag_185 = dot(frag_9, frag_9);
    frag_185 = rsqrt(frag_185);
    frag_9 *= frag_185.xxx;
    frag_9.x = dot(frag_9, frag_39);
    frag_9.x = clamp(frag_9.x, 0.0f, 1.0f);
    frag_9.x = (-frag_9.x) + 1.0f;
    frag_9.x = (frag_132.y * (-frag_9.x)) + 1.0f;
    frag_9.x *= frag_32;
    frag_90.w = frag_9.x * frag_137.y;
    frag_9.x = (frag_9.x * frag_137.y) + (-0.001000000047497451305389404296875f);
    frag_370 = frag_9.x < 0.0f;
    if ((int(frag_370) * (-1)) != 0)
    {
        discard;
    }
    frag_383 = frag_90;
}

frag_Output frag(frag_Input stage_input)
{
    frag_11 = stage_input.frag_11;
    frag_33 = stage_input.frag_33;
    frag_45 = stage_input.frag_45;
    frag_117 = stage_input.frag_117;
    frag_125 = stage_input.frag_125;
    frag_132 = stage_input.frag_132;
    frag_137 = stage_input.frag_137;
    frag_main();
    frag_Output stage_output;
    stage_output.frag_383 = frag_383;
    return stage_output;
}

ENDHLSL
} } }
