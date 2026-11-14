// Vertex-coloured terrain for the race island (sand / grass / jungle / rock painted per vertex),
// lit by the main light with soft wrap, ambient SH and received shadows, plus subtle grain.
Shader "RaceFX/Terrain"
{
    Properties { _Grain ("Grain", Range(0,0.3)) = 0.08 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial) float _Grain; CBUFFER_END
            struct A { float4 pos : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct V { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 w : TEXCOORD1; float4 color : COLOR; float fog : TEXCOORD2; };
            V vert (A i)
            {
                V o; o.w = TransformObjectToWorld(i.pos.xyz); o.pos = TransformWorldToHClip(o.w);
                o.n = TransformObjectToWorldNormal(i.normal); o.color = i.color; o.fog = ComputeFogFactor(o.pos.z); return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            half4 frag (V i) : SV_Target
            {
                float3 n = normalize(i.n);
                Light l = GetMainLight(TransformWorldToShadowCoord(i.w));
                float wrap = saturate(dot(n, l.direction) * 0.8 + 0.2);
                float3 lit = l.color * wrap * lerp(0.4, 1, l.shadowAttenuation) * 0.85 + SampleSH(n) * 0.45;
                float grain = (Hash(floor(i.w.xz * 1.5)) - 0.5) * _Grain;
                float3 c = i.color.rgb * (1 + grain) * lit;
                c = MixFog(c, i.fog);
                return half4(c, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 pos : POSITION; };
            float4 vert (A i) : SV_POSITION { return TransformObjectToHClip(i.pos.xyz); }
            half frag () : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
