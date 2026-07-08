// Simple URP-compatible lit shader that COLORS a mesh by its VERTEX COLORS (× an optional tint).
// The new nature pack (Quaternius-style) ships no textures - its greens/browns live in vertex colors,
// which the stock URP/Lit shader ignores (so those models would render flat grey). This shader reads
// them and applies basic main-light + ambient shading so the trees/bushes look right and lit.
Shader "TasteJump/VertexColorLit"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float4 color       : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                Light mainLight = GetMainLight();
                float ndotl = saturate(dot(n, mainLight.direction)) * 0.85 + 0.15; // soft half-lambert
                half3 ambient = SampleSH(n);
                half3 lighting = mainLight.color.rgb * ndotl + ambient;
                half3 albedo = IN.color.rgb * _Tint.rgb;
                return half4(albedo * lighting, 1.0);
            }
            ENDHLSL
        }

        // Shadow casting so the new trees/props still drop shadows like the rest of the world.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct SAttr { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct SVary { float4 positionHCS : SV_POSITION; };

            SVary shadowVert (SAttr IN)
            {
                SVary OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(IN.normalOS);
                float4 pos = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, _LightDirection));
                OUT.positionHCS = pos;
                return OUT;
            }

            half4 shadowFrag (SVary IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
