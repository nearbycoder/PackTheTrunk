// Late-summer afternoon sky: zenith-to-horizon gradient, warm glow toward the sun,
// a soft sun disc and slow drifting puffy clouds.
Shader "PTT/Sky"
{
    Properties
    {
        _Zenith("Zenith", Color) = (0.36, 0.6, 0.92, 1)
        _Horizon("Horizon", Color) = (0.98, 0.86, 0.72, 1)
        _Ground("Ground", Color) = (0.55, 0.62, 0.5, 1)
        _SunColor("Sun Glow", Color) = (1, 0.82, 0.6, 1)
        _CloudColor("Clouds", Color) = (1, 0.97, 0.94, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith, _Horizon, _Ground, _SunColor, _CloudColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }
            float fbm(float2 p)
            {
                float s = 0, a = 0.5;
                for (int k = 0; k < 5; k++) { s += noise(p) * a; p *= 2.03; a *= 0.5; }
                return s;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                half3 sky = h >= 0 ? lerp(_Horizon.rgb, _Zenith.rgb, pow(saturate(h), 0.55)) : lerp(_Horizon.rgb, _Ground.rgb, pow(saturate(-h), 0.35));

                Light sun = GetMainLight();
                float sd = saturate(dot(d, sun.direction));
                sky += _SunColor.rgb * (pow(sd, 6.0) * 0.35 + pow(sd, 64.0) * 0.6);
                sky += half3(1, 0.95, 0.85) * smoothstep(0.9993, 0.9996, sd) * 2.0;

                if (h > 0.02)
                {
                    float2 uv = d.xz / (h + 0.18) * 1.6 + float2(_Time.y * 0.004, 0);
                    float c = smoothstep(0.52, 0.78, fbm(uv));
                    c *= smoothstep(0.02, 0.2, h);
                    half3 cloud = lerp(_CloudColor.rgb * 0.85, _CloudColor.rgb, saturate(fbm(uv * 1.7 + 3.0)));
                    sky = lerp(sky, cloud, c * 0.85);
                }
                return half4(sky, 1);
            }
            ENDHLSL
        }
    }
}
