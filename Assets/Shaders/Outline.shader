// Full-screen ink outline from depth and normal discontinuities. Used by a Full Screen Pass
// renderer feature (Depth + Normal requirements). Lines are a darkened version of the scene
// colour rather than black, and fade out with distance so the background stays soft.
Shader "PTT/Outline"
{
    Properties
    {
        _Thickness("Thickness (px)", Float) = 1.2
        _DepthSensitivity("Depth Sensitivity", Float) = 14
        _NormalSensitivity("Normal Sensitivity", Float) = 2.2
        _Strength("Strength", Range(0, 1)) = 0.75
        _FadeStart("Fade Start", Float) = 35
        _FadeEnd("Fade End", Float) = 90
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "Outline"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float _Thickness;
            float _DepthSensitivity;
            float _NormalSensitivity;
            float _Strength;
            float _FadeStart;
            float _FadeEnd;

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float rawDepth = SampleSceneDepth(uv);
                float depth = LinearEyeDepth(rawDepth, _ZBufferParams);
                if (depth > _FadeEnd) return color;

                float3 normal = SampleSceneNormals(uv);
                float2 px = _ScreenSize.zw * _Thickness;
                float2 offsets[4] = { float2(px.x, 0), float2(-px.x, 0), float2(0, px.y), float2(0, -px.y) };

                float edge = 0;
                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    float d = LinearEyeDepth(SampleSceneDepth(uv + offsets[k]), _ZBufferParams);
                    edge = max(edge, saturate((abs(d - depth) / depth - 0.02) * _DepthSensitivity));
                    float3 n = SampleSceneNormals(uv + offsets[k]);
                    // Only real creases count: ignore the small angles between facets of curved meshes.
                    edge = max(edge, saturate((1.0 - dot(n, normal) - 0.32) * _NormalSensitivity));
                }

                edge *= 1.0 - saturate((depth - _FadeStart) / (_FadeEnd - _FadeStart));
                half3 ink = color.rgb * 0.18 + half3(0.04, 0.03, 0.08);
                return half4(lerp(color.rgb, ink, edge * _Strength), color.a);
            }
            ENDHLSL
        }
    }
}
