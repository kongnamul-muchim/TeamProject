Shader "Paro222/UnderwaterEffects"
{
    Properties
    {
        [HideInInspector] _MainTex ("Base (RGB)", 2D) = "white" {}
        _color ("Fog Color", Color) = (1, 0, 0, 1)
        _dis ("Distance", Float) = 10
        _alpha ("Alpha", Range(0, 1)) = 1
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

            // Render Graph / Blitter API 전용 텍스처 선언
            TEXTURE2D(_BlitTexture); SAMPLER(sampler_BlitTexture);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_CameraDepthTexture); SAMPLER(sampler_CameraDepthTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _color; float _dis; float _alpha; float _refraction; float4 _normalUV;
            CBUFFER_END

            Varyings vert (Attributes input) {
                Varyings output;
                // Blitter API가 제공하는 풀스크린 삼각형 좌표를 그대로 사용
                output.positionCS = float4(input.positionOS.xyz, 1.0);
                output.uv = input.uv;
                return output;
            }

            half4 frag (Varyings input) : SV_Target {
                // 노멀맵 기반 굴절 오프셋 계산
                float3 normalSample = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv * _normalUV.xy + _normalUV.zw * _Time.y));
                float2 offset = normalSample.xy * _refraction * 0.05;

                // 굴절이 적용된 화면 컬러 샘플링 (_BlitTexture 사용)
                half4 col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.uv + offset);
                
                // 깊이값 샘플링 및 거리 계산
                float rawDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, input.uv + offset).r;
                float depth = Linear01Depth(rawDepth, _ZBufferParams);
                
                // 포그 강도 개선: 거리에 따른 감쇄와 알파값을 결합
                // _dis가 클수록 멀리서 안개가 시작됩니다.
                float fogFactor = saturate(depth / (_dis * 0.1));
                float finalFog = saturate(fogFactor * _alpha);

                return lerp(col, _color, finalFog);
            }
            ENDHLSL
        }
    }
}