Shader "OpenWDS/SplitEffect/ParticleAdditive"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }
        Pass
        {
            Blend SrcAlpha One, SrcAlpha One
            ZWrite Off
            Cull Off
            Fog { Mode Off }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4x4 unity_ObjectToWorld;
            float4x4 unity_MatrixVP;
            float4 _MainTex_ST;
            Texture2D<float4> _MainTex;
            SamplerState sampler_MainTex;

            struct VertexInput
            {
                float4 position : POSITION;
                float4 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct VertexOutput
            {
                float4 position : SV_POSITION;
                float3 uv : TEXCOORD0;
                float4 color : TEXCOORD1;
            };

            VertexOutput vert(VertexInput input)
            {
                VertexOutput output;
                output.position = mul(unity_MatrixVP,
                    mul(unity_ObjectToWorld, input.position));
                output.uv.xy = input.uv.xy * _MainTex_ST.xy + _MainTex_ST.zw;
                output.uv.z = input.uv.z;
                output.color = input.color;
                return output;
            }

            float4 frag(VertexOutput input) : SV_Target
            {
                float4 color = _MainTex.Sample(sampler_MainTex, input.uv.xy) *
                    input.color * (input.uv.z * 0.5 + 1.0);
                clip(color.a - 0.001);
                return color;
            }
            ENDHLSL
        }
    }
}
