// Forge's grass: tufts drawn straight from a buffer (GrassView), one draw per chunk, no game objects. Each instance is a
// tuft (where, how tall, which way, its color); the mesh is one unit-high tuft of blades whose UV.y runs root to tip.
// The wind sways the tips, the blades shrink into the ground towards the fade distance so grass never pops, and the
// light is the far land's: the sun, the sky's ambient and the fog, with normals pointing up so a tuft shades as a mass.
Shader "Forge/Grass"
{
    Properties
    {
        _RootShade ("Root shade", Range(0, 1)) = 0.45
        _TipLight ("Tip light", Range(1, 2)) = 1.1
        _WindStrength ("Wind strength (m per m of height)", Range(0, 0.5)) = 0.12
        _WindSpeed ("Wind speed", Range(0, 4)) = 1.3
        _FadeStart ("Fade start (m)", Float) = 30
        _FadeEnd ("Fade end (m)", Float) = 45
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half _RootShade;
            half _TipLight;
            float _WindStrength;
            float _WindSpeed;
            float _FadeStart;
            float _FadeEnd;
        CBUFFER_END

        // Must match GrassView.Tuft: 32 bytes.
        struct Tuft
        {
            float3 position; // chunk-local, meters
            float height;
            float3 color;    // linear
            float yaw;
        };

        StructuredBuffer<Tuft> _Tufts;
        float4x4 _ChunkToWorld; // chunk-local meters → the scene (the world root's transform, moved to the chunk)

        // The blade vertex in the scene, and how far up the blade it is (0 root, 1 tip).
        float3 TuftVertex(float3 positionOS, float tip, uint instance, out Tuft tuft)
        {
            tuft = _Tufts[instance];
            float3 root = mul(_ChunkToWorld, float4(tuft.position, 1)).xyz;

            // Shrink into the ground with distance, so the edge of the grass is a soft fade, not a line.
            float fade = 1 - saturate((distance(root, _WorldSpaceCameraPos) - _FadeStart) / max(_FadeEnd - _FadeStart, 0.001));
            float height = tuft.height * fade;

            float s, c;
            sincos(tuft.yaw, s, c);
            float3 local = float3(positionOS.x * c - positionOS.z * s, positionOS.y, positionOS.x * s + positionOS.z * c) * height;
            float3 world = root + mul((float3x3)_ChunkToWorld, local);

            // Tips sway most; the phase drifts across the field so neighbours move together, like gusts.
            float phase = dot(root.xz, float2(0.31, 0.17)) + _Time.y * _WindSpeed;
            float2 sway = float2(sin(phase), sin(phase * 0.73 + 1.7)) * (_WindStrength * height * tip * tip);
            world.xz += sway;
            return world;
        }
        ENDHLSL

        Pass
        {
            Name "Grass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half3 color : COLOR;
                half fog : TEXCOORD0;
            };

            Varyings Vert(Attributes input, uint instance : SV_InstanceID)
            {
                Tuft tuft;
                float3 world = TuftVertex(input.positionOS.xyz, input.uv.y, instance, tuft);

                Varyings output;
                output.positionHCS = TransformWorldToHClip(world);
                output.color = tuft.color * lerp(_RootShade, _TipLight, input.uv.y);
                output.fog = ComputeFogFactor(output.positionHCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                const half3 up = half3(0, 1, 0);
                Light sun = GetMainLight();
                half3 light = sun.color * saturate(dot(up, sun.direction) * 0.75 + 0.25) + SampleSH(up);
                return half4(MixFog(input.color * light, input.fog), 1);
            }
            ENDHLSL
        }

        // Depth for the camera's depth texture, which the water reads to color itself by its depth.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Vert(float4 positionOS : POSITION, float2 uv : TEXCOORD0, uint instance : SV_InstanceID) : SV_POSITION
            {
                Tuft tuft;
                return TransformWorldToHClip(TuftVertex(positionOS.xyz, uv.y, instance, tuft));
            }

            half Frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
