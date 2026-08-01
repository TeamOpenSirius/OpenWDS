Shader "OpenWDS/Recovered/SplitEffect/SplitEffectSyuriken"
{
    Properties
    {
        [HideInInspector] _SrcBlend ("Source Blend", Float) = 5
        [HideInInspector] _DstBlend ("Destination Blend", Float) = 1
        [HideInInspector] _ZWrite ("Z Write", Float) = 0
        [HideInInspector] _ZTest ("Z Test", Float) = 4
        [HideInInspector] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float maxColor : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.uv = input.uv;
                output.color = input.color;
                output.maxColor = max(input.color.r, max(input.color.g, input.color.b));
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Exact scalar graph recovered from the Vulkan Universal Forward
                // program (path ID 777288701855927481, program 0, streams 0/1).
                float4 outer = input.uv.xxyy +
                    float4(-0.425000012, -0.574999988, -1.02500010, -1.17499995);
                outer *= float4(1.73913050, -1.73913050, 1.73913050, -1.73913050);
                outer = saturate(outer);
                outer = outer * outer * (3.0 - 2.0 * outer);
                float2 outerPair = 1.0 - (outer.wy + outer.zx);
                float colorMask = 1.0 - outerPair.x * outerPair.y;
                colorMask = 1.0 - min(pow(abs(colorMask), 4.0), 1.0);

                float4 inner = input.uv.xxyx +
                    float4(-0.25, -0.75, -0.25, -0.544999957);
                inner *= float4(1.33333337, -1.33333337, 1.33333337, 2.19780207);
                inner = saturate(inner);
                inner = inner * inner * (3.0 - 2.0 * inner);

                float innerPair = 1.0 - (inner.y + inner.x);
                float innerReverse = 1.0 - inner.z;
                float checker = 1.0 - innerReverse * innerPair;
                checker = 1.0 - min(pow(abs(checker), 1200.0), 1.0);
                colorMask *= checker;

                float3 rgb = lerp(input.color.rgb, input.maxColor.xxx, colorMask);

                float localY = TransformWorldToObject(input.positionWS).y;
                float worldYScale = length(GetObjectToWorldMatrix()[1].xyz);
                float pulse = min(sin((localY - 20.0 * _TimeParameters.x) * worldYScale) + 1.7, 1.0);

                float edge = saturate((input.uv.x - 0.455000013) * -2.19780207);
                edge = edge * edge * (3.0 - 2.0 * edge);
                edge = pow(edge + inner.w, 0.4);
                float tail = (1.0 - edge) * (1.0 - min(pow(inner.z, 80.0), 1.0));
                float alpha = (pulse * tail + colorMask) * input.color.a;
                return float4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
