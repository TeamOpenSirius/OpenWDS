// Generated from original sharedassets1.assets Shader PathID 11.
Shader "OpenWDS/StarActCutinOutline" {
Properties {
[NoScaleOffset]_MainTex ("MainTex", 2D) = "white" {}
_ColorCount ("ColorCount", Float) = 5.0
_Color1 ("Color1", Color) = (1.0, 1.0, 1.0, 1.0)
_Color2 ("Color2", Color) = (0.007843137718737125, 1.0, 0.0, 1.0)
_Color3 ("Color3", Color) = (1.0, 0.0, 0.0, 1.0)
_Color4 ("Color4", Color) = (1.0, 0.9294118285179138, 0.0, 1.0)
_Color5 ("Color5", Color) = (0.0, 0.1607843041419983, 1.0, 1.0)
[NoScaleOffset]_SampleTexture2D_bbfb1da80e77405096e81d753830b9c8_Texture_1_Texture2D ("Texture2D", 2D) = "white" {}
[HideInInspector]_QueueOffset ("_QueueOffset", Float) = 0.0
[HideInInspector]_QueueControl ("_QueueControl", Float) = -1.0
[HideInInspector][NoScaleOffset]unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
[HideInInspector][NoScaleOffset]unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
[HideInInspector][NoScaleOffset]unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
}
SubShader {
Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
Pass {
Name "Universal Forward"
Tags { "LightMode"="UniversalForward" }
Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
ZTest LEqual
ZWrite Off
Cull Back
HLSLPROGRAM
#pragma target 5.0
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
CBUFFER_START(UnityPerMaterial)
float4 _SampleTexture2D_bbfb1da80e77405096e81d753830b9c8_Texture_1_Texture2D_TexelSize;
float4 _MainTex_TexelSize;
float4 _Color1;
float4 _Color2;
float4 _Color3;
float _ColorCount;
float4 _Color4;
float4 _Color5;
CBUFFER_END



static float4 gl_Position;
static float3 vert_11;
static float3 vert_83;
static float4 vert_96;
static float4 vert_98;
static float4 vert_100;
static float4 vert_101;
static float3 vert_103;
static float3 vert_133;

struct vert_Input
{
    float3 vert_11 : POSITION;
    float3 vert_103 : NORMAL;
    float4 vert_98 : TEXCOORD0;
    float4 vert_101 : COLOR;
};

struct vert_Output
{
    float4 vert_96 : TEXCOORD0;
    float4 vert_100 : TEXCOORD1;
    float3 vert_83 : TEXCOORD2;
    float3 vert_133 : TEXCOORD3;
    float4 gl_Position : SV_Position;
};

static float3 vert_9;
static float4 vert_58;
static float vert_124;

void vert_main()
{
    vert_9 = vert_11.yyy * transpose(UNITY_MATRIX_M)[1].xyz;
    vert_9 = (transpose(UNITY_MATRIX_M)[0].xyz * vert_11.xxx) + vert_9;
    vert_9 = (transpose(UNITY_MATRIX_M)[2].xyz * vert_11.zzz) + vert_9;
    vert_9 += transpose(UNITY_MATRIX_M)[3].xyz;
    vert_58 = vert_9.yyyy * transpose(UNITY_MATRIX_VP)[1];
    vert_58 = (transpose(UNITY_MATRIX_VP)[0] * vert_9.xxxx) + vert_58;
    vert_58 = (transpose(UNITY_MATRIX_VP)[2] * vert_9.zzzz) + vert_58;
    vert_83 = vert_9;
    gl_Position = vert_58 + transpose(UNITY_MATRIX_VP)[3];
    vert_96 = vert_98;
    vert_100 = vert_101;
    vert_9.x = dot(vert_103, transpose(UNITY_MATRIX_I_M)[0].xyz);
    vert_9.y = dot(vert_103, transpose(UNITY_MATRIX_I_M)[1].xyz);
    vert_9.z = dot(vert_103, transpose(UNITY_MATRIX_I_M)[2].xyz);
    vert_124 = dot(vert_9, vert_9);
    vert_124 = max(vert_124, 1.1754943508222875079687365372222e-38f);
    vert_124 = rsqrt(vert_124);
    vert_133 = vert_124.xxx * vert_9;
    
}

vert_Output vert(vert_Input stage_input)
{
    vert_11 = stage_input.vert_11;
    vert_98 = stage_input.vert_98;
    vert_101 = stage_input.vert_101;
    vert_103 = stage_input.vert_103;
    vert_main();
    vert_Output stage_output;
    stage_output.gl_Position = gl_Position;
    stage_output.vert_83 = vert_83;
    stage_output.vert_96 = vert_96;
    stage_output.vert_100 = vert_100;
    stage_output.vert_133 = vert_133;
    return stage_output;
}



Texture2D<float4> _MainTex;
SamplerState sampler_MainTex;
Texture2D<float4> _SampleTexture2D_bbfb1da80e77405096e81d753830b9c8_Texture_1_Texture2D;
SamplerState sampler_SampleTexture2D_bbfb1da80e77405096e81d753830b9c8_Texture_1_Texture2D;

static float4 gl_FragCoord;
static float4 frag_33;
static float4 frag_51;
static float4 frag_108;

struct frag_Input
{
    float4 frag_33 : TEXCOORD0;
    float4 frag_51 : TEXCOORD1;
    float4 gl_FragCoord : SV_Position;
};

struct frag_Output
{
    float4 frag_108 : SV_Target0;
};

static float frag_27;
static float3 frag_49;
static float frag_56;
static float frag_63;
static float frag_67;
static bool frag_98;
static float3 frag_144;
static float3 frag_173;
static float frag_240;
static float frag_244;
static float3 frag_265;
static float frag_271;
static float frag_279;
static float3 frag_299;

void frag_main()
{
    float4 frag_9 = float4(gl_FragCoord.xyz, 1.0f / gl_FragCoord.w);
    frag_27 = _MainTex.SampleBias(sampler_MainTex, frag_33.xy, _GlobalMipBias.x).w;
    frag_49.x = frag_27 * frag_51.w;
    frag_56 = (frag_27 * frag_51.w) + (-0.00999999977648258209228515625f);
    frag_63 = ddx_coarse(frag_49.x);
    frag_67 = ddy_coarse(frag_49.x);
    frag_63 = abs(frag_67) + abs(frag_63);
    frag_67 = ((-frag_63) * 0.5f) + frag_56;
    frag_63 = max(frag_63, 9.9999997473787516355514526367188e-05f);
    frag_63 = frag_67 / frag_63;
    frag_63 += 1.0f;
    frag_63 = clamp(frag_63, 0.0f, 1.0f);
    frag_67 = frag_63 + (-9.9999997473787516355514526367188e-05f);
    frag_98 = _AlphaToMaskAvailable != 0.0f;
    frag_56 = frag_98 ? frag_67 : frag_56;
    float frag_111;
    if (frag_98)
    {
        frag_111 = frag_63;
    }
    else
    {
        frag_111 = frag_49.x;
    }
    frag_108.w = frag_111;
    frag_98 = frag_56 < 0.0f;
    if ((int(frag_98) * (-1)) != 0)
    {
        discard;
    }
    frag_98 = _ProjectionParams.x < 0.0f;
    frag_49.x = (-frag_9.y) + _ScaledScreenParams.y;
    float frag_146;
    if (frag_98)
    {
        frag_146 = frag_49.x;
    }
    else
    {
        frag_146 = frag_9.y;
    }
    frag_144.y = frag_146;
    frag_144.x = frag_9.x;
    float2 frag_165 = frag_144.xy / _ScaledScreenParams.xy;
    frag_144 = float3(frag_165.x, frag_165.y, frag_144.z);
    frag_49.x = (-frag_144.y) + 1.0f;
    frag_173.x = (frag_144.x * 0.839999973773956298828125f) + 1.28999996185302734375f;
    frag_144.x = frag_49.x * 1.14999997615814208984375f;
    frag_173.y = (_TimeParameters.x * 0.37999999523162841796875f) + frag_144.x;
    frag_27 = _SampleTexture2D_bbfb1da80e77405096e81d753830b9c8_Texture_1_Texture2D.SampleBias(sampler_SampleTexture2D_bbfb1da80e77405096e81d753830b9c8_Texture_1_Texture2D, frag_173.xy, _GlobalMipBias.x).x;
    frag_144.x = frag_27 + (-0.569999992847442626953125f);
    frag_144.x = (frag_144.x * (-2.75f)) + 0.4900000095367431640625f;
    frag_144.x = clamp(frag_144.x, 0.0f, 1.0f);
    frag_49.x = round(_ColorCount);
    frag_49.x = max(frag_49.x, 0.0f);
    frag_49.x = min(frag_49.x, 5.0f);
    frag_49.x += (-1.0f);
    frag_240 = 1.0f / frag_49.x;
    frag_244 = (-frag_240) + frag_144.x;
    frag_244 = frag_49.x * frag_244;
    frag_244 = clamp(frag_244, 0.0f, 1.0f);
    frag_173.x = (frag_244 * (-2.0f)) + 3.0f;
    frag_244 *= frag_244;
    frag_265.x = frag_244 * frag_173.x;
    frag_271 = frag_49.x * frag_144.x;
    frag_271 = clamp(frag_271, 0.0f, 1.0f);
    frag_279 = (frag_271 * (-2.0f)) + 3.0f;
    frag_271 *= frag_271;
    frag_265.x = (frag_279 * frag_271) + (-frag_265.x);
    frag_271 = ((-frag_279) * frag_271) + 1.0f;
    frag_299 = frag_265.xxx * _Color2.xyz;
    frag_265 = (_Color1.xyz * frag_271.xxx) + frag_299;
    frag_299.x = ((-frag_240) * 2.0f) + frag_144.x;
    frag_144.x = ((-frag_240) * 3.0f) + frag_144.x;
    frag_144.x = frag_49.x * frag_144.x;
    frag_144.x = clamp(frag_144.x, 0.0f, 1.0f);
    frag_49.x *= frag_299.x;
    frag_49.x = clamp(frag_49.x, 0.0f, 1.0f);
    frag_240 = (frag_49.x * (-2.0f)) + 3.0f;
    frag_49.x *= frag_49.x;
    frag_299.x = frag_49.x * frag_240;
    frag_244 = (frag_173.x * frag_244) + (-frag_299.x);
    frag_173 = (_Color3.xyz * frag_244.xxx) + frag_265;
    frag_244 = (frag_144.x * (-2.0f)) + 3.0f;
    frag_144.x *= frag_144.x;
    frag_144.x *= frag_244;
    frag_49.x = (frag_240 * frag_49.x) + (-frag_144.x);
    frag_49 = (_Color4.xyz * frag_49.xxx) + frag_173;
    frag_144 = (_Color5.xyz * frag_144.xxx) + frag_49;
    frag_144 *= 1.5f.xxx;
    frag_108 = float4(frag_144.x, frag_144.y, frag_144.z, frag_108.w);
}

frag_Output frag(frag_Input stage_input)
{
    gl_FragCoord = stage_input.gl_FragCoord;
    gl_FragCoord.w = 1.0 / gl_FragCoord.w;
    frag_33 = stage_input.frag_33;
    frag_51 = stage_input.frag_51;
    frag_main();
    frag_Output stage_output;
    stage_output.frag_108 = frag_108;
    return stage_output;
}

ENDHLSL
}
Pass {
Name "DepthNormalsOnly"
Tags { "LightMode"="DepthNormalsOnly" }
Blend One Zero, One Zero
ZTest LEqual
ZWrite On
Cull Back
HLSLPROGRAM
#pragma target 5.0
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"




static float4 gl_Position;
static float3 vert_12;
static float4 vert_103;
static float4 vert_105;
static float4 vert_107;
static float4 vert_108;
static float3 vert_110;
static float3 vert_143;

struct vert_Input
{
    float3 vert_12 : POSITION;
    float3 vert_110 : NORMAL;
    float4 vert_105 : TEXCOORD0;
    float4 vert_108 : COLOR;
};

struct vert_Output
{
    float4 vert_103 : TEXCOORD0;
    float4 vert_107 : TEXCOORD1;
    float3 vert_143 : TEXCOORD2;
    float4 gl_Position : SV_Position;
};

static float4 vert_9;
static float4 vert_68;
static float vert_131;

void vert_main()
{
    float3 vert_33 = vert_12.yyy * transpose(UNITY_MATRIX_M)[1].xyz;
    vert_9 = float4(vert_33.x, vert_33.y, vert_33.z, vert_9.w);
    float3 vert_44 = (transpose(UNITY_MATRIX_M)[0].xyz * vert_12.xxx) + vert_9.xyz;
    vert_9 = float4(vert_44.x, vert_44.y, vert_44.z, vert_9.w);
    float3 vert_56 = (transpose(UNITY_MATRIX_M)[2].xyz * vert_12.zzz) + vert_9.xyz;
    vert_9 = float4(vert_56.x, vert_56.y, vert_56.z, vert_9.w);
    float3 vert_65 = vert_9.xyz + transpose(UNITY_MATRIX_M)[3].xyz;
    vert_9 = float4(vert_65.x, vert_65.y, vert_65.z, vert_9.w);
    vert_68 = vert_9.yyyy * transpose(UNITY_MATRIX_VP)[1];
    vert_68 = (transpose(UNITY_MATRIX_VP)[0] * vert_9.xxxx) + vert_68;
    vert_9 = (transpose(UNITY_MATRIX_VP)[2] * vert_9.zzzz) + vert_68;
    gl_Position = vert_9 + transpose(UNITY_MATRIX_VP)[3];
    vert_103 = vert_105;
    vert_107 = vert_108;
    vert_9.x = dot(vert_110, transpose(UNITY_MATRIX_I_M)[0].xyz);
    vert_9.y = dot(vert_110, transpose(UNITY_MATRIX_I_M)[1].xyz);
    vert_9.z = dot(vert_110, transpose(UNITY_MATRIX_I_M)[2].xyz);
    vert_131 = dot(vert_9.xyz, vert_9.xyz);
    vert_131 = max(vert_131, 1.1754943508222875079687365372222e-38f);
    vert_131 = rsqrt(vert_131);
    vert_143 = vert_131.xxx * vert_9.xyz;
    
}

vert_Output vert(vert_Input stage_input)
{
    vert_12 = stage_input.vert_12;
    vert_105 = stage_input.vert_105;
    vert_108 = stage_input.vert_108;
    vert_110 = stage_input.vert_110;
    vert_main();
    vert_Output stage_output;
    stage_output.gl_Position = gl_Position;
    stage_output.vert_103 = vert_103;
    stage_output.vert_107 = vert_107;
    stage_output.vert_143 = vert_143;
    return stage_output;
}


Texture2D<float4> _MainTex;
SamplerState sampler_MainTex;

static float4 frag_16;
static float4 frag_37;
static float3 frag_62;
static float4 frag_76;

struct frag_Input
{
    float4 frag_16 : TEXCOORD0;
    float4 frag_37 : TEXCOORD1;
    float3 frag_62 : TEXCOORD2;
};

struct frag_Output
{
    float4 frag_76 : SV_Target0;
};

static float frag_8;
static float3 frag_35;
static bool frag_47;

void frag_main()
{
    frag_8 = _MainTex.SampleBias(sampler_MainTex, frag_16.xy, _GlobalMipBias.x).w;
    frag_35.x = (frag_8 * frag_37.w) + (-0.00999999977648258209228515625f);
    frag_47 = frag_35.x < 0.0f;
    if ((int(frag_47) * (-1)) != 0)
    {
        discard;
    }
    frag_35.x = dot(frag_62, frag_62);
    frag_35.x = rsqrt(frag_35.x);
    frag_35 = frag_35.xxx * frag_62;
    frag_76 = float4(frag_35.x, frag_35.y, frag_35.z, frag_76.w);
    frag_76.w = 0.0f;
}

frag_Output frag(frag_Input stage_input)
{
    frag_16 = stage_input.frag_16;
    frag_37 = stage_input.frag_37;
    frag_62 = stage_input.frag_62;
    frag_main();
    frag_Output stage_output;
    stage_output.frag_76 = frag_76;
    return stage_output;
}

ENDHLSL
}
}
Fallback Off
}
