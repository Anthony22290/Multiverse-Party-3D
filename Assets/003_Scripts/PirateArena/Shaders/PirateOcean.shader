Shader "Multiverse/PirateOcean"
{
    Properties
    {
        _DeepColor ("Deep water", Color) = (0.02,0.27,0.42,1)
        _ShallowColor ("Turquoise", Color) = (0.04,0.65,0.72,1)
        _FoamColor ("Foam", Color) = (0.67,0.94,0.9,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor, _ShallowColor, _FoamColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 world : TEXCOORD0;
                half fog : TEXCOORD1;
            };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                world.y += sin(world.x * 0.19 + _Time.y * 0.65) * 0.06
                    + cos(world.z * 0.23 - _Time.y * 0.4) * 0.045;
                o.world = world;
                o.positionCS = TransformWorldToHClip(world);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.world.xz;
                float wave = sin(p.x * 0.55 + p.y * 0.19 + _Time.y * 0.7)
                    * cos(p.y * 0.4 - p.x * 0.17 - _Time.y * 0.5);
                half3 color = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(wave * 0.3 + 0.57));
                float stroke = sin(p.x * 1.3 + p.y * 0.3 + sin(p.y * 0.7) + _Time.y * 0.45);
                float breakup = sin(p.y * 1.8 - p.x * 0.4 + _Time.y * 0.3);
                float foam = smoothstep(0.965, 1.0, stroke) * smoothstep(0.2, 0.8, breakup);
                color = lerp(color, _FoamColor.rgb, foam * 0.55);
                Light sun = GetMainLight();
                float3 normal = normalize(float3(-cos(p.x * 0.5 + _Time.y) * 0.08, 1, sin(p.y * 0.4) * 0.08));
                float3 view = GetWorldSpaceNormalizeViewDir(input.world);
                float highlight = pow(saturate(dot(normal, normalize(sun.direction + view))), 80);
                color += sun.color * highlight * 0.18;
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
    }
}
