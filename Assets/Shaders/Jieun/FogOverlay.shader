// 2D Fog Overlay (Unity URP Conversion)
// Original: https://godotshaders.com/shader/2d-fog-overlay-2/
// Author: HexagonNico - CC0 License
//
// 2D 탑다운 게임용 안개 오버레이 셰이더.
// RawImage에 ShaderMaterial로 적용하여 사용.
// 노이즈 텍스처가 천천히 이동하며 안개 효과를 만듭니다.
//
// 사용 방법:
// 1. Canvas 아래에 RawImage 생성
// 2. 이 셰이더가 적용된 머티리얼 할당
// 3. Noise Texture에 Perlin/Simplex 노이즈 텍스처 할당
// 4. FogOverlayController로 제어

Shader "Custom/FogOverlay"
{
    Properties
    {
        [HideInInspector] _MainTex ("Main Texture", 2D) = "white" {}
        _NoiseTexture ("Noise Texture", 2D) = "white" {}
        _Density ("Fog Density", Range(0, 1)) = 0.25
        _Speed ("Fog Speed (X, Y)", Vector) = (0.02, 0.01, 0, 0)
        _FogColor ("Fog Color", Color) = (0.7, 0.7, 0.75, 1.0)
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
                float _Density;
                float2 _Speed;
                float4 _FogColor;
            CBUFFER_END

            // RawImage/Canvas 호환성
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // 노이즈 텍스처
            TEXTURE2D(_NoiseTexture);
            SAMPLER(sampler_NoiseTexture);

            float4 _NoiseTexture_ST;

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

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings i) : SV_Target
            {
                // 안개가 천천히 이동하도록 UV에 시간 오프셋 추가
                float2 uv = i.uv + _Speed * _Time.y;

                // 노이즈 텍스처 샘플링 (타일링 적용)
                float2 tiledUv = TRANSFORM_TEX(uv, _NoiseTexture);
                float noise = SAMPLE_TEXTURE2D(_NoiseTexture, sampler_NoiseTexture, tiledUv).r;

                // 노이즈를 (0,1) → (-1,1) 범위로 변환 후 다시 (0,1)로 클램프
                // 밀도가 낮은 영역은 투명하게, 높은 영역은 불투명하게
                float fog = clamp(noise * 2.0 - 1.0, 0.0, 1.0);

                // 안개 밀도 적용
                float alpha = fog * _Density;

                // 최종 색상: 안개 색상 × 계산된 알파
                return float4(_FogColor.rgb, _FogColor.a * alpha);
            }
            ENDHLSL
        }
    }
}