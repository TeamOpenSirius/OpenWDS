Shader "OpenWDS/SplitEffect/ParticleTrailAdditive"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _AddtiveIntencity ("Intencity", Range(0, 10)) = 1
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

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _AddtiveIntencity;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct VertexInput
            {
                float4 position : POSITION;
                float4 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct VertexOutput
            {
                float4 position : SV_POSITION;
                float3 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float4 projectedPosition : TEXCOORD1;
            };

            VertexOutput vert(VertexInput input)
            {
                VertexOutput output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = float3(TRANSFORM_TEX(input.uv.xy, _MainTex), input.uv.z);
                output.color = input.color;
                output.projectedPosition = ComputeScreenPos(output.position);
                output.projectedPosition.z =
                    -UnityObjectToViewPos(input.position).z;
                return output;
            }

            fixed4 frag(VertexOutput input) : SV_Target
            {
                float sceneDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(
                    _CameraDepthTexture, UNITY_PROJ_COORD(input.projectedPosition)));
                float depthFade = saturate(sceneDepth - input.projectedPosition.z);
                fixed4 color = tex2D(_MainTex, input.uv.xy) * input.color *
                    _AddtiveIntencity;
                color.a *= depthFade;
                clip(color.a - 0.01);
                return color;
            }
            ENDCG
        }
    }
}
