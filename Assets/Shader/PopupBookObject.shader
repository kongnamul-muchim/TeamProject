Shader "HideAndInk/PopupBook_Caustics"
{
    Properties
    {
        [Header(Base Options)]
        [MainColor] _BaseColor("Base Color 틴트 (에메랄드빛)", Color) = (1, 1, 1, 1)
        [PerRendererData] _MainTex("Base Texture (스프라이트)", 2D) = "white" {}
        _Cutoff("알파 컷아웃(투명 자르기)", Range(0.0, 1.0)) = 0.5
        
        [Header(Caustics)]
        _CausticsTex("윤슬 텍스처 (Caustics Map)", 2D) = "black" {}
        [HDR] _CausticsColor("윤슬 색상 (밝기)", Color) = (1, 1, 1, 1)
        _CausticsSpeed("윤슬 흐름 속도 (X, Y)", Vector) = (0.5, 0.5, 0, 0)
        _CausticsScale("윤슬 크기 (스케일)", Float) = 2.0
        
        [Header(Caustics Masking)]
        _MaskHeight("윤슬 맺히는 높이 기준점 (로컬 Y)", Range(-5.0, 5.0)) = 0.5
        _MaskSoftness("윤슬 경계면 부드러움", Range(0.01, 0.5)) = 0.2
        _CausticsIntensity("윤슬 전체 강도", Range(0.0, 5.0)) = 1.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="TransparentCutout" 
            "RenderPipeline"="UniversalPipeline" 
            "Queue"="AlphaTest"
        }
        
        // ============================================
        // PASS 1: Main Forward Rendering (포그 대응 가능)
        // ============================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            ZWrite On // 그림자와 포그를 위한 깊이 기록 켜기!
            Cull Off  // 빌보드가 양면으로 보이도록 처리

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float fold          : TEXCOORD1;
                float4 color        : COLOR;
                float2 localPos     : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Cutoff;
                
                float4 _CausticsColor;
                float4 _CausticsSpeed;
                float _CausticsScale;
                float _MaskHeight;
                float _MaskSoftness;
                float _CausticsIntensity;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            TEXTURE2D(_CausticsTex);
            SAMPLER(sampler_CausticsTex);

            Varyings vert(Attributes input)
            {
                Varyings output;
                // 기본 월드 변환 및 투영 변환
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                output.localPos = input.positionOS.xy;
                
                // 포그 계산을 위한 거리 변수에 저장 (뷰 스페이스 깊이 안 쓰고 URP 내장 포그 매크로 사용)
                output.fold = ComputeFogFactor(output.positionHCS.z);
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. 기본 색상 및 컷아웃 (스프라이트 고유 컬러 포함)
                half4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _BaseColor * input.color;
                clip(baseColor.a - _Cutoff); // 컷아웃 (가장자리 자르기)

                // 2. 윤슬 (Caustics) 애니메이션 생성 (아틀라스 방지를 위해 오브젝트 로컬 좌표 사용)
                float2 causticsUV = input.localPos * _CausticsScale;
                causticsUV += _Time.y * _CausticsSpeed.xy; // 스크롤 애니메이션
                
                // 보통 노멀맵 형식의 파란 윤슬 텍스처를 흑백 밝기로 치환하여 사용하거나, 흑백 노이즈를 사용.
                half4 causticsSample = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, causticsUV);
                half causticsLight = max(causticsSample.r, causticsSample.g); // 대략적인 밝기 추출
                
                // 3. 윤슬 마스킹 (UV가 아닌 '로컬 Y 좌표'를 기준으로 마스킹)
                half mask = smoothstep(_MaskHeight - _MaskSoftness, _MaskHeight + _MaskSoftness, input.localPos.y);
                
                // 4. 빛 최종 합성 (더하기 블렌드)
                half3 causticsEffect = causticsLight * _CausticsColor.rgb * mask * _CausticsIntensity;
                half3 finalRGB = baseColor.rgb + causticsEffect;

                // 5. URP 포그 적용
                finalRGB = MixFog(finalRGB, input.fold);

                return half4(finalRGB, baseColor.a);
            }
            ENDHLSL
        }

        // ============================================
        // PASS 2: Shadow Caster (팝업북 얕은 그림자 생성)
        // ============================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Cutoff;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                clip(baseColor.a - _Cutoff); // 투명한 곳은 그림자를 던지지 않음!
                
                return 0; // 그림자 패스는 컬러 반환이 필요 없음
            }
            ENDHLSL
        }

        // ============================================
        // PASS 3: Depth Only (포그 및 후처리 거리 연산용)
        // ============================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0 // 컬러 버퍼에는 안 그림
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Cutoff;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                clip(baseColor.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
