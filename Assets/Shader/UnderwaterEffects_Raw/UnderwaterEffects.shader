Shader "Hidden/UnderwaterEffects_FullScreen"
{
    Properties
    {
        [HideInInspector] _MainTex ("Base (RGB)", 2D) = "white" {}
        _color ("Fog Color", Color) = (0, 0.5, 0.8, 1)
        _dis ("Fog Distance", Float) = 20
        _alpha ("Fog Alpha", Range(0, 1)) = 0.5
        
        [Header(Refraction)]
        _refraction ("Refraction Intensity", Float) = 0.1
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _normalUV ("Normal UV", Vector) = (1, 1, 0.2, 0.1)

        [Header(Light Rays)]
        _RayColor ("Ray Color", Color) = (1, 1, 1, 0.2)
        _RayIntensity ("Ray Intensity", Range(0, 1)) = 0.3
        _RaySpeed ("Ray Speed", Float) = 0.1
        _RayScale ("Ray Scale", Float) = 2.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "UnderwaterFullScreenPass"
            ZTest Always ZWrite Off Cull Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes {
                uint vertexID : SV_VertexID;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _color;
                float _dis;
                float _alpha;
                float _refraction;
                float4 _normalUV;

                float4 _RayColor;
                float _RayIntensity;
                float _RaySpeed;
                float _RayScale;
            CBUFFER_END

            // 가짜 노이즈 함수 (빛 줄기용)
            float PseudoNoise(float2 uv) {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
            }

            Varyings vert (Attributes input) {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            half4 frag (Varyings input) : SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 1. 굴절 효과
                float2 normalUV = input.uv * _normalUV.xy + _normalUV.zw * _Time.y;
                float3 normalSample = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUV));
                float2 offset = normalSample.xy * _refraction * 0.05;

                // 2. 화면 샘플링
                float2 uv = input.uv + offset;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);

                // 3. 깊이 정보
                float rawDepth = SampleSceneDepth(uv);
                float depth = Linear01Depth(rawDepth, _ZBufferParams);

                // 4. 광원 레이 (Light Rays) 계산
                // 수직으로 길게 늘어뜨린 노이즈를 좌우로 스크롤
                float rayMask = 0;
                float2 rayUV = input.uv;
                rayUV.x *= _RayScale;
                rayUV.y = 0.5; // 수직으로 고정하여 줄기 형태 만듦
                
                // 두 층의 노이즈를 섞어서 부드러운 움직임 생성
                rayMask += PseudoNoise(float2(rayUV.x + _Time.y * _RaySpeed, 0.5));
                rayMask += PseudoNoise(float2(rayUV.x - _Time.y * _RaySpeed * 0.7, 0.8));
                rayMask *= 0.5;
                
                // 빛 줄기 선명도 조절 (pow)
                rayMask = pow(rayMask, 4.0) * _RayIntensity;
                
                // 아래쪽으로 갈수록 흐려지게 (그라데이션)
                rayMask *= saturate(1.0 - input.uv.y);
                
                // 깊이에 따른 가림 처리 (가까운 물체 뒤에는 레이가 안 나타나게)
                // 만약 아주 먼 배경(depth ~ 1)이면 레이가 잘 보이게 함
                rayMask *= saturate(depth * 5.0);

                // 5. 최종 합성
                float fogFactor = saturate(depth / max(0.001, _dis * 0.1));
                float finalFog = saturate(fogFactor * _alpha);
                
                // 안개 입히기
                half4 finalCol = lerp(col, _color, finalFog);
                
                // 광원 레이 추가 (Additive 방식)
                finalCol.rgb += _RayColor.rgb * rayMask * _RayColor.a;

                return finalCol;
            }
            ENDHLSL
        }
    }
}