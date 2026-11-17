// Text (TextMesh) that is lit by its surroundings instead of glowing: the font's alpha, tinted by the vertex
// colour, lit by the light probes (the baked mall lighting) and the main light. So a sign in a dark corridor
// is dark, and a notice under a tube is readable.
Shader "AfterHours/LitText"
{
    Properties
    {
        _MainTex ("Font Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Floor ("Minimum light", Float) = 0.02
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -1, -1
        Pass
        {
            Name "LitText"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Floor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; };

            Varyings vert (Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(float3(0, 0, -1));   // text faces the reader (-z)
                o.color = i.color;
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * i.color.a * _Color.a;
                clip(a - 0.01);
                float3 n = normalize(i.normalWS);
                float3 toEye = GetWorldSpaceViewDir(i.positionWS);
                if (dot(n, toEye) < 0) n = -n;
                half3 light = SampleSH(n);
                Light m = GetMainLight();
                light += m.color * saturate(dot(n, m.direction)) * m.distanceAttenuation;
                light = max(light, _Floor);
                return half4(i.color.rgb * _Color.rgb * light, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
