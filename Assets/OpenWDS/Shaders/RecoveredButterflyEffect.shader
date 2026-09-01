Shader "OpenWDS/Recovered/SplitEffect/ButterflyEffect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct VertexInput
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct VertexOutput
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float4 projectedPosition : TEXCOORD1;
            };

            VertexOutput vert(VertexInput input)
            {
                VertexOutput output;
                output.position = UnityObjectToClipPos(input.position);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
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
                fixed4 color = tex2D(_MainTex, input.uv) * input.color;
                color.a *= depthFade;
                return color;
            }
            ENDCG
        }
    }
}
