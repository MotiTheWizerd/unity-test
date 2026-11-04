// Forge's sky, drawn as the skybox: a gradient from the horizon up to the zenith (and down to the ground below the
// horizon), the sun's disk, a soft glow around it and a warm haze along the horizon on the sun's side. Every color
// and the sun's direction are set by Forge.World.SkyView from the SkyProfile, so the same shader draws dawn, noon
// and dusk. The horizon color is also the fog color, so fogged land and sea melt into the sky.
Shader "Forge/Sky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.18, 0.42, 0.85, 1)
        _HorizonColor ("Horizon", Color) = (0.70, 0.82, 0.95, 1)
        _GroundColor ("Ground (below the horizon)", Color) = (0.36, 0.38, 0.40, 1)
        _GradientPower ("Horizon band (lower = thinner)", Range(0.1, 2)) = 0.45
        _SunDirection ("Direction to the sun", Vector) = (0, 0.77, -0.64, 0)
        _SunColor ("Sun", Color) = (1, 0.96, 0.9, 1)
        _SunSize ("Sun radius (degrees)", Range(0.1, 5)) = 0.7
        _SunGlow ("Sun glow", Range(0, 2)) = 0.35
        _SunGlowPower ("Sun glow tightness", Range(1, 2000)) = 600
        _HorizonHaze ("Haze toward the sun", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Sky"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                float _GradientPower;
                float4 _SunDirection;
                half4 _SunColor;
                float _SunSize;
                half _SunGlow;
                float _SunGlowPower;
                half _HorizonHaze;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                // The skybox mesh is centered on the camera, so each vertex is a view direction.
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float up = direction.y;

                half3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(up), _GradientPower));
                half3 ground = lerp(_HorizonColor.rgb, _GroundColor.rgb, saturate(-up * 6));
                half3 color = up >= 0 ? sky : ground;

                float toSun = dot(direction, normalize(_SunDirection.xyz));
                float edge = cos(radians(_SunSize));
                half disk = smoothstep(edge, lerp(edge, 1, 0.3), toSun);
                half glow = pow(saturate(toSun), _SunGlowPower) * _SunGlow;
                half haze = pow(saturate(toSun), 6) * (1 - saturate(abs(up) * 3)) * _HorizonHaze;
                color += _SunColor.rgb * (disk * 20 + glow + haze);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
