// Stylized lit shader for everything in Pack The Trunk.
// Soft two-band diffuse with cool-tinted shadows, stepped highlights, a gentle rim light,
// metal reflections, optional world-space grain, SSAO, fog and full URP shadow support.
Shader "PTT/Toon"
{
    Properties
    {
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _Smoothness("Smoothness", Range(0, 1)) = 0.3
        _Metallic("Metallic", Range(0, 1)) = 0
        [HDR] _EmissionColor("Emission", Color) = (0, 0, 0, 1)
        _Grain("Grain", Range(0, 1)) = 0
        _ShadowTint("Shadow Tint", Color) = (0.58, 0.64, 0.9, 1)
        _RimStrength("Rim Strength", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Smoothness;
            half _Metallic;
            half4 _EmissionColor;
            half _Grain;
            half4 _ShadowTint;
            half _RimStrength;
        CBUFFER_END

        // Graphics fidelity: 0 Low (one cheap grain octave), 1 Medium / High, 2 Ultra (finer surface detail).
        half _PttDetail;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                float2 uv : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            float hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            // Smooth 3D value noise: no visible cells, unlike raw hashing.
            float vnoise(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                float3 u = f * f * (3.0 - 2.0 * f);
                float n000 = hash13(i), n100 = hash13(i + float3(1, 0, 0));
                float n010 = hash13(i + float3(0, 1, 0)), n110 = hash13(i + float3(1, 1, 0));
                float n001 = hash13(i + float3(0, 0, 1)), n101 = hash13(i + float3(1, 0, 1));
                float n011 = hash13(i + float3(0, 1, 1)), n111 = hash13(i + float3(1, 1, 1));
                return lerp(lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y),
                            lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y), u.z);
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(i.normalWS);
                half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 albedo = _BaseColor.rgb * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                if (_Grain > 0)
                {
                    float g;
                    if (_PttDetail < 0.5)
                        g = vnoise(i.positionWS * 2.2);
                    else
                    {
                        // Fade fine detail with distance so it never shimmers.
                        float fine = 1.0 - saturate(length(i.positionWS - _WorldSpaceCameraPos) / 45.0);
                        g = vnoise(i.positionWS * 9.0) * 0.45 * fine + vnoise(i.positionWS * 2.2) * 0.35 + vnoise(i.positionWS * 0.45) * 0.2;
                    }
                    albedo *= 1.0 + (g - 0.5) * _Grain;
                }

                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half nl = dot(n, light.direction);
                half ramp = smoothstep(-0.12, 0.3, nl);
                half shadow = smoothstep(0.2, 0.8, light.shadowAttenuation);
                half lit = ramp * shadow;

                half ao = 1.0;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                    ao = aoFactor.indirectAmbientOcclusion;
                    lit *= aoFactor.directAmbientOcclusion;
                #endif

                half3 ambient = SampleSH(n) * ao;
                half3 shade = light.color * _ShadowTint.rgb * 0.32;
                half3 diffuse = albedo * (light.color * lit + shade * (1.0 - lit) + ambient * _ShadowTint.rgb * 0.85);

                // Stepped highlight: crisp but small, stronger on glossy things.
                half3 h = SafeNormalize(light.direction + v);
                half nh = saturate(dot(n, h));
                // Stepped highlight with screen-space AA so it doesn't sparkle on curved meshes.
                half specRaw = pow(nh, exp2(_Smoothness * 8.0 + 2.0));
                half aa = max(fwidth(specRaw) * 1.5, 0.03);
                half spec = smoothstep(0.5 - aa, 0.5 + aa, specRaw) * _Smoothness * lit * 0.85;
                half3 specColor = lerp(half3(1, 1, 1), albedo * 1.6, _Metallic);

                // Metals pick up the environment.
                half3 refl = GlossyEnvironmentReflection(reflect(-v, n), 1.0 - _Smoothness, ao);
                diffuse = lerp(diffuse, albedo * (refl * 0.9 + light.color * lit * 0.5 + 0.08), _Metallic * 0.75);

                half fresnel = pow(1.0 - saturate(dot(n, v)), 4.0);
                half3 rim = fresnel * _RimStrength * lerp(light.color, albedo + 0.3, 0.5) * (0.35 + 0.65 * lit);

                half3 color = diffuse + specColor * spec + rim + _EmissionColor.rgb;
                color = MixFog(color, i.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = cs;
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half frag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(n);
                    float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
                    return half4(PackFloat2To888(remapped), 0.0);
                #else
                    return half4(n, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
