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
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:vertInstancingSetup

            // The original ParticleSystemRenderer streams are
            // Position, Normal, Color, UV, AnimFrame and Custom1XY. Unity's
            // particle instancing contract requires the instanced fields to
            // follow transform in their renderer-stream order.
            #define UNITY_PARTICLE_INSTANCE_DATA ButterflyParticleInstanceData
            struct ButterflyParticleInstanceData
            {
                float3x4 transform;
                uint color;
                float animFrame;
                float2 custom1;
            };

            #include "UnityCG.cginc"
            #include "UnityStandardParticleInstancing.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct VertexInput
            {
                float4 position : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
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
                UNITY_SETUP_INSTANCE_ID(input);

                VertexOutput output;
                output.color = input.color;

#if defined(UNITY_PARTICLE_INSTANCING_ENABLED)
                vertInstancingColor(output.color);
                vertInstancingUVs(input.uv, output.uv);

                UNITY_PARTICLE_INSTANCE_DATA particle =
                    unity_ParticleInstanceData[unity_InstanceID];

                // Original Vulkan/GLES vertex program:
                // theta = Custom1.y * vertex.x * 7.5, then rotate X/Z.
                float theta = particle.custom1.y * input.position.x * 7.5;
                float sine;
                float cosine;
                sincos(theta, sine, cosine);
                float originalX = input.position.x;
                float originalZ = input.position.z;
                input.position.x = cosine * originalX + sine * originalZ;
                input.position.z = -sine * originalX + cosine * originalZ;

                // The original shader clamps the particle/mesh RGB first,
                // then applies Custom1.x + 1 as its authored brightness.
                output.color.rgb =
                    min(output.color.rgb, 1.0) * (particle.custom1.x + 1.0);
#else
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
#endif

                output.position = UnityObjectToClipPos(input.position);
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
