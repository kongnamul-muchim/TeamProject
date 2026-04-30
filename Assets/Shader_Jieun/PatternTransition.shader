// Fractal Noise Scene Transition (Unity URP Conversion)
// Original: https://godotshaders.com/shader/fractal-noise-scene-transition/
// Copyright Gerardo Montaño 2025 - MIT License
//
// Godot 캔버스 아이템 셰이더를 Unity URP로 변환.
// PatternTransitionController와 함께 사용.
// _Progress: 0 = 투명, 0.5 = 완전 덮임, 1.0 = 다시 투명
//
// _TransitionImage가 None이면 _Color(단색)로 채움.
// _TransitionImage가 설정되면 텍스처 × _Color(틴트)로 채움.
//   - 이미지 그대로 보려면 _Color를 흰색(255,255,255)으로 설정

Shader "Custom/FractalNoiseTransition"
{
    Properties
    {
        [HideInInspector] _MainTex ("Main Texture", 2D) = "white" {}
        _Progress ("Progress", Range(0, 1)) = 0.0
        _Speed ("Animation Speed", Float) = 0.1
        _Pixelation ("Pixelation", Vector) = (2.0, 2.0, 0, 0)
        _Zoom ("Zoom", Float) = 2.0
        _Color ("Transition Color (Tint)", Color) = (0.0, 0.0, 0.0, 1.0)
        _TransitionImage ("Transition Image", 2D) = "white" {}
        _SoftEdge ("Soft Edge Width", Range(0.01, 0.5)) = 0.1
        _Seed ("Seed", Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "PreviewType"="Plane" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Progress;
                float _Speed;
                float2 _Pixelation;
                float _Zoom;
                float4 _Color;
                float _SoftEdge;
                float _Seed;
            CBUFFER_END

            // RawImage/Canvas 호환성
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // 트랜지션 이미지 (None이면 Unity 기본 흰색 텍스처 사용)
            TEXTURE2D(_TransitionImage);
            SAMPLER(sampler_TransitionImage);

            // FBM 회전 행렬 (Godot: mat2(vec2(0.80,-0.60), vec2(0.60,0.80)))
            static const float2x2 _FbmRot = float2x2(0.80, 0.60, -0.60, 0.80);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            // --- 노이즈 함수들 ---

            float rand(float2 n)
            {
                return frac(sin(dot(n, float2(12.9898 + _Seed, 4.1414 - _Seed)))
                            * (43758.5453 + _Seed * 1000.0));
            }

            float noise(float2 p)
            {
                float2 ip = floor(p);
                float2 u = frac(p);
                u = u * u * (3.0 - 2.0 * u);

                float res = lerp(
                    lerp(rand(ip), rand(ip + float2(1.0, 0.0)), u.x),
                    lerp(rand(ip + float2(0.0, 1.0)), rand(ip + float2(1.0, 1.0)), u.x),
                    u.y);

                return res * res;
            }

            float fbm(float2 p)
            {
                float iTime = _Time.y * _Speed - _Seed;
                float f = 0.0;

                f += 0.500000 * noise(p + iTime);  p = mul(_FbmRot, p) * 2.02;
                f += 0.031250 * noise(p);           p = mul(_FbmRot, p) * 2.01;
                f += 0.250000 * noise(p);           p = mul(_FbmRot, p) * 2.03;
                f += 0.125000 * noise(p);           p = mul(_FbmRot, p) * 2.01;
                f += 0.062500 * noise(p);           p = mul(_FbmRot, p) * 2.04;
                f += 0.015625 * noise(p + sin(iTime));

                return f / 0.96875;
            }

            float pattern(float2 p)
            {
                return fbm(p + fbm(p + fbm(p)));
            }

            // --- 컬러맵 ---

            float4 colormap(float x, float2 uv)
            {
                // _Progress에 따라 대각선 마스크 계산
                // progress 0→0.5: 화면이 덮임, 0.5→1.0: 화면이 걷힘
                float bgThreshold = abs(1.0 - _Progress * 2.0) - 0.5;

                x *= max(0.0, min(-abs(_Progress * 4.0 - uv.x - uv.y - 1.0) + 1.0, 1.0) * 2.0);

                // 항상 텍스처 샘플링 (None이면 Unity 기본 흰색 반환)
                // 텍스처 × _Color = 최종 색상
                //   - 텍스처 없음: 흰색(1,1,1,1) × 검은색(0,0,0,1) = 검은색
                //   - 텍스처 있음 + 흰색 틴트: 텍스처 원본 색상
                //   - 텍스처 있음 + 색상 틴트: 텍스처 × 색상
                float4 fillColor = SAMPLE_TEXTURE2D(_TransitionImage, sampler_TransitionImage, uv) * _Color;

                // smoothstep으로 배경→채움 전이를 부드럽게 블렌딩
                // if-else 하드 컷 대신 _SoftEdge 범위만큼 자연스럽게 스무딩
                float alpha = smoothstep(bgThreshold - _SoftEdge, bgThreshold + _SoftEdge, x);

                return float4(fillColor.rgb, fillColor.a * alpha);
            }

            // --- 버텍스/프래그먼트 ---

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings i) : SV_Target
            {
                // 픽셀화: 화면 해상도 / 픽셀화 크기로 그리드 스냅
                float2 modifier = _ScreenParams.xy / _Pixelation;
                float2 uv = floor(i.uv * modifier) / modifier;

                float shade = pattern(uv * _Zoom);
                return colormap(shade, uv);
            }
            ENDHLSL
        }
    }
}