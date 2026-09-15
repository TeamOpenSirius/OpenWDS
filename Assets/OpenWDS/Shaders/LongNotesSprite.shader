Shader "Shader Graphs/LongNotesSprite"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("MainTex", 2D) = "white" {}
        ShutterMaskPositionZ ("ShutterMaskPositionZ", Float) = 0
        [ToggleUI] IsTouchMask ("IsTouchMask", Integer) = 0
        TouchMaskPositionZ ("TouchMaskPositionZ", Float) = 0
        TouchMaskThresfold ("TouchMaskThresfold", Float) = 1
        [ToggleUI] _IsScratch ("IsScratch", Integer) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Unlit"
        }

        Pass
        {
            Name "Universal Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float ShutterMaskPositionZ;
                int IsTouchMask;
                float TouchMaskPositionZ;
                float TouchMaskThresfold;
                int _IsScratch;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                float4 color : COLOR;
                float worldZ : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.color = input.color;
                output.worldZ = positionWS.z;
                return output;
            }

            float ExactSmoothStep(float edge0, float edge1, float value)
            {
                float t = saturate((value - edge0) / (edge1 - edge0));
                return t * t * (3.0 - 2.0 * t);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Literal port of GLES3 program 2, shader pathID
                // 3337198859166160120. Constants and branch order are retained.
                float edgePair =
                    ExactSmoothStep(0.699999988, 1.0, input.uv.x) +
                    ExactSmoothStep(0.300000012, 0.0, input.uv.x);
                float edgeAlpha = min((1.0 - min(pow(edgePair, 0.0800000057), 1.0)) * 200.0, 1.0);

                bool touched = IsTouchMask != 0;
                float touchMask = 1.0;
                if (touched)
                {
                    touchMask = ExactSmoothStep(
                        0.0,
                        TouchMaskThresfold,
                        input.worldZ - TouchMaskPositionZ);
                }

                float widePair =
                    ExactSmoothStep(0.25, 1.0, input.uv.x) +
                    ExactSmoothStep(0.75, 0.0, input.uv.x);
                float narrowPair =
                    ExactSmoothStep(0.3495, 1.0, input.uv.x) +
                    ExactSmoothStep(0.6505, 0.0, input.uv.x);
                float wide = min(pow(widePair, 1.19999993), 1.0);
                float narrow = min(pow(narrowPair, 0.0800000057), 1.0);
                float pulse = touched
                    ? sin(_TimeParameters.x * 30.0) * 0.300000012 * (1.0 - narrow)
                    : 0.0;
                float intensity = saturate(pulse + wide * 0.5 + 0.300000012);
                float alpha = edgeAlpha * touchMask * intensity;
                if (input.worldZ >= ShutterMaskPositionZ)
                    alpha = 0.0;

                bool scratch = _IsScratch != 0;
                float3 baseColor = scratch
                    ? float3(0.706069827, 0.26666671, 0.952941179)
                    : float3(0.265174389, 0.831479371, 0.952830195);
                float3 touchedColor = scratch
                    ? float3(0.97012198, 0.713725507, 1.0)
                    : float3(0.71226418, 1.0, 0.90442729);
                float3 idleColor = scratch
                    ? float3(0.933603287, 0.619607925, 1.0)
                    : float3(0.617924571, 1.0, 0.906408608);
                float3 rgb = baseColor + (touched ? touchedColor : idleColor) * (wide * 0.5);
                rgb *= input.color.rgb;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
