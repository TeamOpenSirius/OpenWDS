// sharedassets3 Shader 18, Universal Forward GLES program 1 (no keywords).
Shader "OpenWDS/ResultSpotlight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _ExclusionTexture ("Exclusion", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One, One One
            ZWrite Off
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_ExclusionTexture); SAMPLER(sampler_ExclusionTexture);
            struct Attributes { float3 position : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float4 screen : TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.position = TransformObjectToHClip(input.position);
                output.screen = ComputeScreenPos(output.position);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float radius = saturate((0.5 - length(input.uv - 0.5)) * 5.00000048);
                float falloff = min(radius * radius * (3.0 - 2.0 * radius), 1.0);
                float2 screen = input.screen.xy / input.screen.w + float2(0.03, 0.05);
                float exclusion = saturate(SAMPLE_TEXTURE2D(_ExclusionTexture, sampler_ExclusionTexture, screen).r);
                float alpha = (falloff - exclusion) * input.color.a;
                // Offline URP uses one sample: retail _AlphaToMaskAvailable == 0 branch.
                clip(alpha - 0.01);
                return half4(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb * input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
