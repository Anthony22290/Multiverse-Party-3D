// Glossy translucent shell with a bright rim. _Rainbow = 1 cycles the hue over time and height
// (item boxes); 0 uses _Color (shield bubble, pulse orb).
Shader "RaceFX/Fresnel"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 0.9, 1, 1)
        _Rainbow ("Rainbow", Range(0,1)) = 0
        _Alpha ("Body alpha", Range(0,1)) = 0.35
        _Rim ("Rim strength", Float) = 2
        _RimPower ("Rim power", Float) = 2.5
        _Speed ("Hue speed", Float) = 0.35
        _Pulse ("Pulse", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Back
        Pass
        {
            Name "Fresnel"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) float4 _Color; float _Rainbow, _Alpha, _Rim, _RimPower, _Speed, _Pulse; CBUFFER_END
            struct A { float4 pos : POSITION; float3 normal : NORMAL; };
            struct V { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 w : TEXCOORD1; };
            V vert (A i) { V o; o.w = TransformObjectToWorld(i.pos.xyz); o.pos = TransformWorldToHClip(o.w); o.n = TransformObjectToWorldNormal(i.normal); return o; }
            float3 Hue(float h) { return saturate(abs(frac(h + float3(0, 2.0/3, 1.0/3)) * 6 - 3) - 1); }
            half4 frag (V i) : SV_Target
            {
                float3 n = normalize(i.n); float3 v = normalize(GetWorldSpaceViewDir(i.w));
                float f = pow(1 - saturate(dot(n, v)), _RimPower);
                float3 c = lerp(_Color.rgb, Hue(_Time.y * _Speed + i.w.y * 0.25 + dot(n, float3(0.15, 0.1, 0.2))), _Rainbow);
                float glint = pow(saturate(dot(reflect(-v, n), normalize(float3(0.3, 1, 0.2)))), 24);
                float pulse = 1 + _Pulse * 0.35 * sin(_Time.y * 9);
                float3 col = c * (0.75 + f * _Rim) * pulse + glint;
                return half4(col, saturate((_Alpha + f * 0.8) * _Color.a));
            }
            ENDHLSL
        }
    }
}
