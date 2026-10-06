// Placement preview: a glowing hologram of where the held item will land.
// Fresnel-bright edges, faint body, drifting scan lines and a slow pulse. With _Hatch on (the
// item won't fit there) it gets bold diagonal stripes, so the two states differ without colour.
Shader "PTT/Ghost"
{
    Properties
    {
        [MainColor] _BaseColor("Color", Color) = (0.35, 1, 0.55, 0.4)
        _Hatch("Hatch", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Ghost"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Hatch;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half fresnel = pow(1.0 - saturate(dot(n, v)), 2.5);
                half scan = smoothstep(0.7, 1.0, frac(i.positionWS.y * 6.0 - _Time.y * 1.2)) * 0.35;
                half pulse = 0.85 + 0.15 * sin(_Time.y * 5.0);
                half alpha = saturate((_BaseColor.a * 0.55 + fresnel * 0.75 + scan * _BaseColor.a) * pulse);
                half3 color = _BaseColor.rgb * (1.1 + fresnel * 1.4 + scan);
                half stripe = step(0.5, frac((i.positionWS.x + i.positionWS.y - i.positionWS.z) * 2.5));
                alpha = lerp(alpha, lerp(alpha * 0.2, saturate(alpha * 1.6 + 0.3), stripe), _Hatch);
                color = lerp(color, color * lerp(0.45, 1.1, stripe), _Hatch);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
