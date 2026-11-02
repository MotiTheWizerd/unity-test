// See-through preview of an object about to be placed. Depth-tested (so the
// ground and other objects hide what is behind them) but writes no depth, so
// it never hides the world. Unlit with a fixed light direction, so shapes still
// read as 3D. Color and opacity come from _BaseColor.
Shader "Forge/PlacementGhost"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.45, 0.85, 1, 0.45)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "PlacementGhost"
            ZTest LEqual
            ZWrite Off
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half light = saturate(dot(normalize(input.normalWS), normalize(half3(0.4h, 1.0h, 0.3h))));
                return half4(_BaseColor.rgb * (0.6h + 0.4h * light), _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
