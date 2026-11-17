// A CRT security monitor showing a live camera feed: night-gain greyscale with a faint green cast, sensor
// noise, scanlines, a slow rolling interference bar, line jitter and a dark vignette. _Signal 0 = snow.
Shader "AfterHours/CCTVScreen"
{
    Properties
    {
        _BaseMap ("Feed", 2D) = "black" {}
        _Level ("Brightness", Float) = 1
        _Signal ("Signal", Float) = 1
        _Seed ("Seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Level;
                float _Signal;
                float _Seed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            float hash (float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            half4 frag (Varyings i) : SV_Target
            {
                float t = _Time.y;
                float2 uv = i.uv;
                float bar = frac(uv.y * 0.6 - t * 0.06 + _Seed);
                float roll = smoothstep(0.0, 0.05, bar) * smoothstep(0.14, 0.05, bar);
                float line_ = floor(uv.y * 144.0);
                uv.x += (hash(float2(line_, floor(t * 12.0) + _Seed)) - 0.5) * 0.003 + roll * 0.008;
                float3 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                float l = dot(c, float3(0.299, 0.587, 0.114));
                l = pow(saturate(l * 2.2 + 0.035), 0.75);                       // night gain
                l += (hash(floor(i.uv * float2(240.0, 180.0)) + frac(t * 7.31) * 97.0) - 0.5) * 0.16;
                l *= 0.86 + 0.14 * sin(i.uv.y * 180.0 * 6.2831853);              // scanlines
                l += roll * 0.07;
                float2 d = i.uv - 0.5;
                l *= saturate(1.0 - dot(d, d) * 1.5);
                float snow = hash(floor(i.uv * float2(160.0, 120.0)) + floor(t * 25.0) * 0.371 + _Seed);
                l = lerp(snow * 0.65, l, saturate(_Signal));
                float3 col = saturate(l) * float3(0.8, 0.95, 0.86);
                return half4(col * _Level, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
