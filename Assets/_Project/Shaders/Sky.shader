// Forge's sky, drawn as the skybox: a gradient from the horizon up to the zenith, the sun's disk, a soft glow around
// it and a warm haze along the horizon on the sun's side; by night the stars (turning around the pole star) and up to
// four moons, each face lit from the sun's direction so its phase shows as it truly falls. Every color, the sun, the
// moons and the stars' turn are set by Forge.World.SkyView from the SkyProfile and the SkyState, so the same shader
// draws dawn, noon, dusk and night. The horizon color is also the fog color, so fogged land and sea melt into the sky;
// below the horizon the sky is that color too, so looking down past the land's far edge (from high up) shows only
// haze, never a band where the land stops.
Shader "Forge/Sky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.18, 0.42, 0.85, 1)
        _HorizonColor ("Horizon", Color) = (0.70, 0.82, 0.95, 1)
        _GradientPower ("Horizon band (lower = thinner)", Range(0.1, 2)) = 0.45
        _SunDirection ("Direction to the sun", Vector) = (0, 0.77, -0.64, 0)
        _SunColor ("Sun", Color) = (1, 0.96, 0.9, 1)
        _SunSize ("Sun radius (degrees)", Range(0.1, 5)) = 0.7
        _SunGlow ("Sun glow", Range(0, 2)) = 0.35
        _SunGlowPower ("Sun glow tightness", Range(1, 2000)) = 600
        _HorizonHaze ("Haze toward the sun", Range(0, 1)) = 0.25
        _MoonColor ("Moon", Color) = (0.92, 0.9, 0.84, 1)
        _MoonHalo ("Moon halo", Range(0, 1)) = 0.12
        _MoonEarthshine ("Moon's dark side", Range(0, 0.2)) = 0.03
        _StarBrightness ("Stars", Range(0, 5)) = 0
        _StarDensity ("Star density", Range(20, 400)) = 180
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

            // Matches Forge.Data.Sky.Moon.MaxPerSky.
            #define MAX_MOONS 4

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                float _GradientPower;
                float4 _SunDirection;
                half4 _SunColor;
                float _SunSize;
                half _SunGlow;
                float _SunGlowPower;
                half _HorizonHaze;
                half4 _MoonColor;
                half _MoonHalo;
                half _MoonEarthshine;
                half _StarBrightness;
                float _StarDensity;
                float _MoonCount;
                float4x4 _StarRotation;
            CBUFFER_END

            // Per moon: the direction toward it; and its size (degrees across), lit share, and a seed for its markings.
            float4 _MoonDirections[MAX_MOONS];
            float4 _MoonShapes[MAX_MOONS];

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

            float Hash12(float2 p)
            {
                float3 p3 = frac(p.xyx * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float3 Hash33(float3 p)
            {
                p = frac(p * float3(0.1031, 0.1030, 0.0973));
                p += dot(p, p.yxz + 33.33);
                return frac((p.xxy + p.yxx) * p.zyx);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash12(i), Hash12(i + float2(1, 0)), f.x), lerp(Hash12(i + float2(0, 1)), Hash12(i + 1), f.x), f.y);
            }

            // One layer of stars: a few cells of a grid around the camera hold a star each, drawn about a pixel wide.
            half3 StarLayer(float3 direction, float density, float chance, float pixel, half strength)
            {
                float3 cell = floor(direction * density);
                if (Hash13(cell) > chance)
                    return 0;

                float3 jitter = Hash33(cell + 17.0);
                float3 star = normalize((cell + 0.2 + 0.6 * jitter) / density);
                float shape = saturate(1 - length(direction - star) / (pixel * 1.3));
                half brightness = 0.15 + 0.85 * pow(Hash13(cell + 5.0), 4);
                half twinkle = 0.8 + 0.2 * sin(_Time.y * (1.5 + 3 * jitter.x) + jitter.y * 40);
                half3 tint = lerp(half3(0.75, 0.85, 1.0), half3(1.0, 0.88, 0.72), jitter.z);
                return tint * shape * shape * brightness * twinkle * strength;
            }

            // Grey markings across a moon's face, p within the unit disk.
            half MoonMarkings(float2 p, float seed)
            {
                float n = Noise(p * 2.2 + seed) * 0.6 + Noise(p * 5.5 + seed) * 0.3 + Noise(p * 13 + seed) * 0.1;
                return lerp(0.62, 1.0, smoothstep(0.35, 0.65, n));
            }

            // Draws one moon over the sky: its face lit from the sun (so the phase follows from where the two stand),
            // a faint dark side that hides the stars, and a halo as strong as its lit share.
            void DrawMoon(float3 direction, float3 toSun, float3 toMoon, float4 shape, float pixel, inout half3 color, inout half starsSeen)
            {
                float radius = radians(shape.x * 0.5);
                float toward = dot(direction, toMoon);
                color += _MoonColor.rgb * pow(saturate(toward), 2 / (9 * radius * radius)) * shape.y * _MoonHalo;

                float3 right = normalize(cross(abs(toMoon.y) > 0.99 ? float3(1, 0, 0) : float3(0, 1, 0), toMoon));
                float3 up = cross(toMoon, right);
                float2 p = float2(dot(direction, right), dot(direction, up)) / sin(radius);
                float r = length(p);
                if (toward <= 0 || r > 1.05)
                    return;

                half disk = 1 - smoothstep(1 - pixel / radius, 1, r);
                float3 normal = p.x * right + p.y * up - sqrt(saturate(1 - r * r)) * toMoon;
                half lit = smoothstep(-0.05, 0.15, dot(normal, toSun));
                half3 face = _MoonColor.rgb * MoonMarkings(p, shape.z) * (lit + _MoonEarthshine);
                color += face * disk;
                starsSeen *= 1 - disk;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float up = direction.y;
                float pixel = max(length(fwidth(direction)), 1e-5);

                // Below the horizon the gradient stays at the horizon (fog) color.
                half3 color = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(up), _GradientPower));

                float3 toSun = normalize(_SunDirection.xyz);
                float toward = dot(direction, toSun);
                float edge = cos(radians(_SunSize));
                half disk = smoothstep(edge, lerp(edge, 1, 0.3), toward);
                half glow = pow(saturate(toward), _SunGlowPower) * _SunGlow;
                half haze = pow(saturate(toward), 6) * (1 - saturate(abs(up) * 3)) * _HorizonHaze;
                color += _SunColor.rgb * (disk * 20 + glow + haze);

                // Nothing in the sky shows below the horizon, and it fades into the haze just above it.
                half aboveHorizon = smoothstep(0, 0.08, up);
                half starsSeen = aboveHorizon;
                half3 moons = 0;
                for (int i = 0; i < MAX_MOONS; i++)
                {
                    if (i < (int)_MoonCount)
                        DrawMoon(direction, toSun, _MoonDirections[i].xyz, _MoonShapes[i], pixel, moons, starsSeen);
                }
                color += moons * smoothstep(-0.005, 0.02, up);

                if (_StarBrightness > 0)
                {
                    float3 fixedSky = mul((float3x3)_StarRotation, direction);
                    half3 stars = StarLayer(fixedSky, _StarDensity, 0.025, pixel, 0.6)
                                + StarLayer(fixedSky, _StarDensity * 0.3, 0.05, pixel, 1.6);
                    color += stars * _StarBrightness * starsSeen;
                }
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
