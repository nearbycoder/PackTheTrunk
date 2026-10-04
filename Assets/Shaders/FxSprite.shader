// Camera-facing procedural sprites for little bits of juice: dust puffs, sparkles, confetti.
// _Shape: 0 = soft puff, 1 = four-point sparkle, 2 = confetti square, 3 = ring.
Shader "PTT/FxSprite"
{
    Properties
    {
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        _Shape("Shape", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Shape;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Tint)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _Tint);
                if (tint.a <= 0) tint = float4(1, 1, 1, 1);
                float2 p = i.uv * 2.0 - 1.0;
                float r = length(p);
                float a;
                if (_Shape < 0.5) a = saturate(1.0 - r) * saturate(1.0 - r) ;
                else if (_Shape < 1.5) a = saturate(1.0 - (abs(p.x) * abs(p.y) * 14.0 + r * 0.8));
                else if (_Shape < 2.5) a = 1.0;
                else a = saturate(1.0 - abs(r - 0.8) * 8.0);
                half4 c = _BaseColor * tint;
                return half4(c.rgb, c.a * a);
            }
            ENDHLSL
        }
    }
}
