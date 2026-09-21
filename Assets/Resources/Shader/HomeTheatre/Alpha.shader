// Generated from frozen original GPU programs by recover_home_theatre_shaders.py
Shader "OpenWDS/HomeTheatre/Alpha" {
Properties {
[NoScaleOffset]_MainTex ("_MainTex", 2D) = "white" {}
[ToggleUI]_SoftParticle ("SoftParticle", Float) = 0.0
_Tiling_XY ("Tiling_XY", Vector) = (0.0, 0.0, 0.0, 0.0)
_Offset_XY ("Offset_XY", Vector) = (0.0, 0.0, 0.0, 0.0)
_Near ("Near", Float) = 0.0
_Far ("Far", Float) = 1.0
[HideInInspector]_CastShadows ("_CastShadows", Float) = 0.0
[HideInInspector]_Surface ("_Surface", Float) = 1.0
[HideInInspector]_Blend ("_Blend", Float) = 0.0
[HideInInspector]_AlphaClip ("_AlphaClip", Float) = 1.0
[HideInInspector]_SrcBlend ("_SrcBlend", Float) = 1.0
[HideInInspector]_DstBlend ("_DstBlend", Float) = 0.0
[ToggleUI][HideInInspector]_ZWrite ("_ZWrite", Float) = 0.0
[HideInInspector]_ZWriteControl ("_ZWriteControl", Float) = 0.0
[HideInInspector]_ZTest ("_ZTest", Float) = 4.0
[HideInInspector]_Cull ("_Cull", Float) = 0.0
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
float4 _MainTex_TexelSize; float _Near, _Far, _SoftParticle; float2 _Tiling_XY, _Offset_XY;



static float4 gl_Position;
static float3 vert_11;
static float4 vert_94;
static float4 vert_96;
static float4 vert_98;
static float4 vert_99;
static float3 vert_102;
static float3 vert_110;
static float3 vert_142;
static float3 vert_157;

struct vert_Input
{
    float3 vert_11 : POSITION;
    float3 vert_110 : NORMAL;
    float4 vert_96 : TEXCOORD0;
    float4 vert_99 : COLOR;
};

struct vert_Output
{
    float4 vert_94 : TEXCOORD0;
    float4 vert_98 : TEXCOORD1;
    float3 vert_102 : TEXCOORD2;
    float3 vert_142 : TEXCOORD3;
    float3 vert_157 : TEXCOORD4;
    float4 gl_Position : SV_Position;
};

static float3 vert_9;
static float4 vert_58;
static float vert_131;
static bool vert_150;

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
    vert_102 = vert_9;
    vert_9 = (-vert_9) + _WorldSpaceCameraPos;
    vert_58.x = dot(vert_110, transpose(UNITY_MATRIX_I_M)[0].xyz);
    vert_58.y = dot(vert_110, transpose(UNITY_MATRIX_I_M)[1].xyz);
    vert_58.z = dot(vert_110, transpose(UNITY_MATRIX_I_M)[2].xyz);
    vert_131 = dot(vert_58.xyz, vert_58.xyz);
    vert_131 = max(vert_131, 1.1754943508222875079687365372222e-38f);
    vert_131 = rsqrt(vert_131);
    vert_142 = vert_131.xxx * vert_58.xyz;
    vert_150 = unity_OrthoParams.w == 0.0f;
    float vert_160;
    if (vert_150)
    {
        vert_160 = vert_9.x;
    }
    else
    {
        vert_160 = transpose(UNITY_MATRIX_V)[0].z;
    }
    vert_157.x = vert_160;
    float vert_172;
    if (vert_150)
    {
        vert_172 = vert_9.y;
    }
    else
    {
        vert_172 = transpose(UNITY_MATRIX_V)[1].z;
    }
    vert_157.y = vert_172;
    float vert_183;
    if (vert_150)
    {
        vert_183 = vert_9.z;
    }
    else
    {
        vert_183 = transpose(UNITY_MATRIX_V)[2].z;
    }
    vert_157.z = vert_183;

}

vert_Output vert(vert_Input stage_input)
{
    vert_11 = stage_input.vert_11;
    vert_96 = stage_input.vert_96;
    vert_99 = stage_input.vert_99;
    vert_110 = stage_input.vert_110;
    vert_main();
    vert_Output stage_output;
    stage_output.gl_Position = gl_Position;
    stage_output.vert_94 = vert_94;
    stage_output.vert_98 = vert_98;
    stage_output.vert_102 = vert_102;
    stage_output.vert_142 = vert_142;
    stage_output.vert_157 = vert_157;
    return stage_output;
}



Texture2D<float4> _MainTex;
SamplerState sampler_MainTex;
Texture2D<float4> _CameraDepthTexture;
SamplerState sampler_CameraDepthTexture;

static float3 frag_11;
static float4 frag_88;
static float4 frag_124;
static float4 frag_334;

struct frag_Input
{
    float4 frag_88 : TEXCOORD0;
    float4 frag_124 : TEXCOORD1;
    float3 frag_11 : TEXCOORD2;
};

struct frag_Output
{
    float4 frag_334 : SV_Target0;
};

static float3 frag_9;
static float4 frag_57;
static float4 frag_107;
static float frag_127;
static float4 frag_134;
static bool frag_151;
static float4 frag_179;
static float frag_266;
static bool frag_309;

void frag_main()
{
    frag_9 = frag_11.yyy * transpose(UNITY_MATRIX_VP)[1].xyw;
    frag_9 = (transpose(UNITY_MATRIX_VP)[0].xyw * frag_11.xxx) + frag_9;
    frag_9 = (transpose(UNITY_MATRIX_VP)[2].xyw * frag_11.zzz) + frag_9;
    frag_9 += transpose(UNITY_MATRIX_VP)[3].xyw;
    float2 frag_62 = frag_9.xz * 0.5f.xx;
    frag_57 = float4(frag_62.x, frag_57.y, frag_62.y, frag_57.w);
    frag_9.x = frag_9.y * _ProjectionParams.x;
    frag_57.w = frag_9.x * 0.5f;
    float2 frag_84 = frag_57.zz + frag_57.xw;
    frag_9 = float3(frag_84.x, frag_84.y, frag_9.z);
    float2 frag_104 = (frag_88.xy * _Tiling_XY) + float2(_Offset_XY.x, _Offset_XY.y);
    frag_57 = float4(frag_104.x, frag_104.y, frag_57.z, frag_57.w);
    frag_107 = _MainTex.SampleBias(sampler_MainTex, frag_57.xy, _GlobalMipBias.x);
    frag_57 = frag_107 * frag_124;
    frag_127 = frag_88.z + 1.0f;
    float3 frag_139 = frag_127.xxx * frag_57.xyz;
    frag_134 = float4(frag_139.x, frag_139.y, frag_139.z, frag_134.w);
    float2 frag_146 = frag_9.xy / frag_9.zz;
    frag_9 = float3(frag_146.x, frag_146.y, frag_9.z);
    frag_151 = unity_OrthoParams.w == 1.0f;
    if (frag_151)
    {
        frag_127 = _CameraDepthTexture.SampleBias(sampler_CameraDepthTexture, frag_9.xy, _GlobalMipBias.x).x;
        float2 frag_176 = (frag_9.xy * 2.0f.xx) + (-1.0f).xx;
        frag_57 = float4(frag_176.x, frag_176.y, frag_57.z, frag_57.w);
        frag_179 = frag_57.yyyy * transpose(UNITY_MATRIX_I_VP)[1];
        frag_179 = (transpose(UNITY_MATRIX_I_VP)[0] * frag_57.xxxx) + frag_179;
        frag_179 = (transpose(UNITY_MATRIX_I_VP)[2] * frag_127.xxxx) + frag_179;
        frag_179 += transpose(UNITY_MATRIX_I_VP)[3];
        float3 frag_209 = frag_179.xyz / frag_179.www;
        frag_57 = float4(frag_209.x, frag_209.y, frag_209.z, frag_57.w);
        frag_127 = frag_57.y * transpose(UNITY_MATRIX_V)[1].z;
        frag_127 = (transpose(UNITY_MATRIX_V)[0].z * frag_57.x) + frag_127;
        frag_127 = (transpose(UNITY_MATRIX_V)[2].z * frag_57.z) + frag_127;
        frag_127 += transpose(UNITY_MATRIX_V)[3].z;
        frag_127 = abs(frag_127);
    }
    else
    {
        frag_9.x = _CameraDepthTexture.SampleBias(sampler_CameraDepthTexture, frag_9.xy, _GlobalMipBias.x).x;
        frag_9.x = (_ZBufferParams.z * frag_9.x) + _ZBufferParams.w;
        frag_127 = 1.0f / frag_9.x;
    }
    frag_9.x = (-frag_9.z) + frag_127;
    frag_266 = (-_Near) + _Far;
    frag_9.x += (-_Near);
    frag_266 = 1.0f / frag_266;
    frag_9.x = frag_266 * frag_9.x;
    frag_9.x = clamp(frag_9.x, 0.0f, 1.0f);
    frag_266 = (frag_9.x * (-2.0f)) + 3.0f;
    frag_9.x *= frag_9.x;
    frag_9.x *= frag_266;
    float4 frag_313 = _SoftParticle.xxxx;
    frag_309 = any(bool4(0.0f.xxxx.x != frag_313.x, 0.0f.xxxx.y != frag_313.y, 0.0f.xxxx.z != frag_313.z, 0.0f.xxxx.w != frag_313.w));
    float frag_319;
    if (frag_309)
    {
        frag_319 = frag_9.x;
    }
    else
    {
        frag_319 = 1.0f;
    }
    frag_9.x = frag_319;
    frag_134.w = frag_9.x * frag_57.w;
    frag_334 = frag_134;
}

frag_Output frag(frag_Input stage_input)
{
    frag_11 = stage_input.frag_11;
    frag_88 = stage_input.frag_88;
    frag_124 = stage_input.frag_124;
    frag_main();
    frag_Output stage_output;
    stage_output.frag_334 = frag_334;
    return stage_output;
}

ENDHLSL
} } }
