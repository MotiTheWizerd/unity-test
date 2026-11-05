// Forge's far land: the coarse tiles past the loaded chunks. Each vertex carries the color its ground looks like from
// afar (a ground layer's map color, linear); the sun's light, the sky's ambient and the fog do the rest, as on the near
// land, so the two meet without a change of tone. No textures: at a kilometre they would only shimmer.
Shader "Forge/FarTerrain"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "FarTerrain"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                half4 color : COLOR;
                half fog : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = position.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color * _Tint;
                output.fog = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                Light sun = GetMainLight();
                half3 light = sun.color * saturate(dot(normal, sun.direction)) + SampleSH(normal);
                half3 color = input.color.rgb * light;
                return half4(MixFog(color, input.fog), 1);
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
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 Vert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half Frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
