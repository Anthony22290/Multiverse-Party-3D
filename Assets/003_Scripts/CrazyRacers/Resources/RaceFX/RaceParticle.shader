// Unlit particle shader for Carreras Locas: vertex colour x soft texture, blend set per material.
Shader "RaceFX/Particle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Boost ("Brightness", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off Cull Off
        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial) float4 _MainTex_ST; float _Boost; CBUFFER_END
            struct A { float4 pos : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            V vert (A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.color = i.color; o.uv = TRANSFORM_TEX(i.uv, _MainTex); return o; }
            half4 frag (V i) : SV_Target
            {
                half4 t = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                t.rgb *= _Boost;
                return t;
            }
            ENDHLSL
        }
    }
}
