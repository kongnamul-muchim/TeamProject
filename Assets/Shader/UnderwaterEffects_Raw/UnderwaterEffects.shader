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

            // Blitter API 전용 텍스처 선언
            TEXTURE2D(_BlitTexture); SAMPLER(sampler_BlitTexture);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_CameraDepthTexture); SAMPLER(sampler_CameraDepthTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _color; float _dis; float _alpha; float _refraction; float4 _normalUV;
            CBUFFER_END

            Varyings vert (Attributes input) {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag (Varyings input) : SV_Target {
                // 노멀맵 기반 굴절 오프셋 계산
                float3 normalSample = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv * _normalUV.xy + _normalUV.zw * _Time.y));
                float2 offset = normalSample.xy * _refraction * 0.05;

                // 굴절이 적용된 화면 컬러 샘플링
                half4 col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, input.uv + offset);
                
                // 깊이값 샘플링 및 거리 계산
                float rawDepth = SAMPLE_TEXTURE2D(_CameraDepthTexture, sampler_CameraDepthTexture, input.uv + offset).r;
                float depth = Linear01Depth(rawDepth, _ZBufferParams);
                
                // 포그 강도 (Distance와 Alpha 기반)
                float fog = saturate(smoothstep(0, _dis * 0.05, depth) + _alpha);

                return lerp(col, _color, fog);
            }
            ENDHLSL
        }
    }
}