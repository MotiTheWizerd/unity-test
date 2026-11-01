// Draws editor gizmos on top of the world (no depth test), so terrain and
// objects never hide a handle. Unlit with a fixed light direction, so shapes
// still read as 3D. Color comes from _BaseColor, set per renderer through
// MaterialPropertyBlocks by Forge.EditorApp.Gizmos.Gizmo.
Shader "Forge/GizmoOverlay"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Overlay" "Queue" = "Overlay" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "GizmoOverlay"
            ZTest Always
            ZWrite Off
            Cull Back

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
                return half4(_BaseColor.rgb * (0.6h + 0.4h * light), 1.0h);
            }
            ENDHLSL
        }
    }
}
