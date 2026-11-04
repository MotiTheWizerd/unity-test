// The first look of Forge's water: a flat, see-through surface whose color deepens with the water under it (read
// from the camera depth texture), ripples from a few scrolling waves in the normals (no textures, no moving
// vertices), sky reflection by fresnel, the sun's glint, and a soft foam line where it meets the shore.
// Presentation only: the water's data and its depth query live in Forge.Editing.Water. A better water (waves,
// refraction, caustics, or an Asset Store water) replaces this material without touching either.
Shader "Forge/Water"
{
    Properties
    {
        _ShallowColor ("Shallow color", Color) = (0.22, 0.58, 0.62, 0.45)
        _DeepColor ("Deep color", Color) = (0.02, 0.13, 0.22, 0.92)
        _DepthRange ("Meters to full depth color", Float) = 6
        _RippleScale ("Ripple size (waves per meter)", Float) = 0.35
        _RippleStrength ("Ripple strength", Range(0, 1)) = 0.25
        _RippleSpeed ("Ripple speed", Float) = 0.8
        _Glint ("Sun glint", Range(0, 4)) = 1.5
        _GlintSharpness ("Sun glint sharpness", Float) = 320
        _FoamColor ("Shore foam color", Color) = (0.92, 0.96, 1, 0.8)
        _FoamWidth ("Shore foam width (m)", Float) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            // Seen from below too, when the camera dives.
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthRange;
                float _RippleScale;
                half _RippleStrength;
                float _RippleSpeed;
                half _Glint;
                float _GlintSharpness;
                half4 _FoamColor;
                float _FoamWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.screenPos = ComputeScreenPos(output.positionHCS);
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                return output;
            }

            // Slope of the water surface at p: four sine waves in unrelated directions and sizes, so no pattern lines
            // up; their derivatives are exact, so the normal needs no extra samples.
            float2 RippleSlope(float2 p, float time)
            {
                const float2 directions[4] = { float2(0.94, 0.34), float2(-0.52, 0.85), float2(0.17, -0.98), float2(-0.81, -0.59) };
                const float frequencies[4] = { 1.0, 1.73, 2.61, 4.07 };
                const float amplitudes[4] = { 1.0, 0.62, 0.38, 0.21 };

                float2 slope = 0;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float k = frequencies[i] * _RippleScale;
                    float phase = dot(directions[i], p) * k + time * _RippleSpeed * sqrt(frequencies[i]);
                    slope += amplitudes[i] * k * cos(phase) * directions[i];
                }

                return slope;
            }

            half4 Frag(Varyings input, bool facingUp : SV_IsFrontFace) : SV_Target
            {
                // How much water the view ray crosses before it hits what is behind (the sea floor, a shore).
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float behind = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float thickness = max(0, behind - input.screenPos.w);
                half depthBlend = saturate(thickness / _DepthRange);

                float2 slope = RippleSlope(input.positionWS.xz, _Time.y) * _RippleStrength;
                float3 normal = normalize(float3(-slope.x, 1, -slope.y));
                if (!facingUp)
                    normal = -normal;

                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(1 - saturate(dot(normal, view)), 5);
                Light sun = GetMainLight();

                half4 body = lerp(_ShallowColor, _DeepColor, depthBlend);
                half3 lit = body.rgb * (0.35 + 0.65 * saturate(dot(normal, sun.direction))) * sun.color + body.rgb * SampleSH(normal) * 0.5;
                // Rippled normals at a low angle can reflect below the horizon, where the sky probe is dark ground
                // color, which showed as dark blots; water reflects the sky there, so mirror the ray back up.
                float3 reflected = reflect(-view, normal);
                reflected.y = abs(reflected.y);
                half3 sky = GlossyEnvironmentReflection(reflected, 0.05, 1);
                half3 glint = sun.color * _Glint * pow(saturate(dot(normal, normalize(sun.direction + view))), _GlintSharpness);
                half foam = (1 - saturate(thickness / max(_FoamWidth, 0.001))) * _FoamColor.a;

                half3 color = lerp(lit, sky, fresnel * 0.6) + glint;
                // Foam is lit like everything else, so it doesn't glow white at night.
                half3 foamLit = _FoamColor.rgb * (sun.color * saturate(dot(normal, sun.direction)) + SampleSH(normal));
                color = lerp(color, foamLit, foam);
                half alpha = saturate(max(max(body.a, fresnel), foam) + dot(glint, 0.33));
                // The water fades out exactly at the shore line, so it meets the land without a hard edge.
                alpha *= saturate(thickness / 0.05);

                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
