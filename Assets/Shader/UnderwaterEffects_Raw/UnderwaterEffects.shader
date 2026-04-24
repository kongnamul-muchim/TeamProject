Shader "Paro222/UnderwaterEffects"
{
    Properties
    {
        [HideInInspector] _MainTex ("Base (RGB)", 2D) = "white" {}
        _color ("Fog Color", Color) = (0, 1, 1, 1)
        _alpha ("Alpha", Range(0, 1)) = 0.5
        _refraction ("Refraction", Float) = 1
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _normalUV ("Normal UV", Vector) = (1, 1, 0.2, 0.1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float2 uv : TEXCOORD0; float4 positionCS : SV_POSITION; };

            TEXTURE2D(_BlitTexture); SAMPLER(sampler_BlitTexture);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _color; float _alpha; float _refraction; float4 _normalUV;
            CBUFFER_END

            Varyings vert (Attributes input) {
                Varyings output;
                output.positionCS = float4(input.positionOS.xyz, 1.0);
                output.uv = input.uv;
                return output;
            }

            half4 frag (Varyings input) : SV_Target {
                // 1. 노멀맵 기반 굴절 계산 (최소화)
                float3 normalSample = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv * _normalUV.xy + _normalUV.zw * _Time.y));
                float2 offset = normalSample.xy * _refraction * 0.01;

                // 2. 화면 샘플링
                half4 col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.uv + offset);
                
                // 3. 작동 확인용 컬러 블렌딩
                return lerp(col, _color, _alpha * 0.5);
            }
            ENDHLSL
        }
    }
}