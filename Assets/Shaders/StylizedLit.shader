// TasteJump's UNIFY shader: one stylized look laid over every prop/platform material so the mixed
// asset packs (Kenney / NatureKit / SpaceKit / FreeNature / SimpleNature) read as ONE art style
// instead of "bought separately". Ramped half-lambert lighting (soft two-tone), cool night shadow
// tint, a moon-blue rim light, per-pixel point lights (the glow beacons), received shadows and fog.
// Property names match URP/Lit (_BaseMap/_BaseColor) so existing MaterialPropertyBlock zone tints
// keep working unchanged. Modeled on the proven TasteJump/VertexColorLit pattern.
Shader "TasteJump/StylizedLit"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _VertexColor ("Use Vertex Color", Range(0,1)) = 0
        _ShadowTint ("Shadow Tint", Color) = (0.45, 0.5, 0.72, 1)
        _RampMin ("Ramp Min", Range(0,1)) = 0.22
        _RampMax ("Ramp Max", Range(0,1)) = 0.62
        _RimColor ("Rim Color", Color) = (0.45, 0.6, 1.0, 1)
        _RimStrength ("Rim Strength", Range(0,2)) = 0.35
        _RimPower ("Rim Power", Range(1,8)) = 3.0
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float4 color       : COLOR;
                float  fogFactor   : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _VertexColor;
                float4 _ShadowTint;
                float  _RampMin;
                float  _RampMax;
                float4 _RimColor;
                float  _RimStrength;
                float  _RimPower;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);

                // Albedo: texture x tint, optionally x vertex color (packs without vertex colors
                // read undefined COLOR data, so it's opt-in per material via _VertexColor).
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 vcol = lerp(half3(1, 1, 1), IN.color.rgb, _VertexColor);
                half3 albedo = tex.rgb * _BaseColor.rgb * vcol;

                // Main light with received shadows, shaped by a soft two-tone ramp.
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float nl = dot(n, mainLight.direction) * 0.5 + 0.5; // half-lambert
                float ramp = smoothstep(_RampMin, _RampMax, nl * mainLight.shadowAttenuation);
                half3 lighting = lerp(_ShadowTint.rgb, half3(1, 1, 1), ramp) * mainLight.color.rgb;
                lighting += SampleSH(n); // ambient

                // Per-pixel point lights (glow crystals / endless beacons), softly ramped too.
                #ifdef _ADDITIONAL_LIGHTS
                uint lightCount = GetAdditionalLightsCount();
                for (uint li = 0u; li < lightCount; li++)
                {
                    Light l = GetAdditionalLight(li, IN.positionWS);
                    float anl = saturate(dot(n, l.direction)) * 0.85 + 0.15;
                    lighting += l.color * l.distanceAttenuation * anl;
                }
                #endif

                half3 color = albedo * lighting;

                // Moon-blue rim: the stylized "premium" edge that ties every prop into the night.
                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float fres = pow(1.0 - saturate(dot(n, viewDir)), _RimPower);
                color += _RimColor.rgb * (fres * _RimStrength * saturate(ramp + 0.4));

                color = MixFog(color, IN.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // Shadow casting (same proven pass as TasteJump/VertexColorLit).
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
                OUT.positionHCS = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, _LightDirection));
                return OUT;
            }

            half4 shadowFrag (SVary IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // Depth prepass so depth-based effects (SSAO) see these objects.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DAttr { float4 positionOS : POSITION; };
            struct DVary { float4 positionHCS : SV_POSITION; };

            DVary depthVert (DAttr IN)
            {
                DVary OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 depthFrag (DVary IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
