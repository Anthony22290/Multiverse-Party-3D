// Animated chevrons for boost pads (procedural, no texture). Arrows point to +V of the face UVs.
Shader "RaceFX/BoostChevrons"
{
    Properties
    {
        _ColorA ("Arrow", Color) = (1, 0.55, 0.05, 1)
        _ColorB ("Base", Color) = (1, 0.9, 0.2, 1)
        _Count ("Arrows", Float) = 3
        _Speed ("Speed", Float) = 1.6
        _Glow ("Glow", Float) = 1.4
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Chevrons"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) float4 _ColorA, _ColorB; float _Count, _Speed, _Glow; CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert (A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = i.uv; return o; }
            half4 frag (V i) : SV_Target
            {
                float band = frac(i.uv.y * _Count - abs(i.uv.x - 0.5) * 1.4 - _Time.y * _Speed);
                float arrow = smoothstep(0.42, 0.47, band) * (1 - smoothstep(0.78, 0.83, band));
                float edge = smoothstep(0.42, 0.5, abs(i.uv.x - 0.5));
                float3 c = lerp(_ColorB.rgb, _ColorA.rgb * _Glow, arrow);
                c = lerp(c, _ColorA.rgb * 0.6, edge);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
