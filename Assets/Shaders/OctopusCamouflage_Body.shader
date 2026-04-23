Shader "HideAndInk/OctopusCamouflage_Body"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _ColorPart ("Color Part/Mask (A=Mask)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OriginalRate ("Original Rate", Range(0,1)) = 1.0

        [Header(Caustics)]
        _CausticsTex("윤슬 텍스처 (Caustics Map)", 2D) = "black" {}
        [HDR] _CausticsColor("윤슬 색상 (밝기)", Color) = (1, 1, 1, 1)
        _CausticsSpeed("윤슬 흐름 속도 (X, Y)", Vector) = (0.5, 0.5, 0, 0)
        _CausticsScale("윤슬 크기 (스케일)", Float) = 2.0
        
        [Header(Caustics Masking)]
        _MaskHeight("윤슬 맺히는 높이 기준점 (로컬 Y)", Range(-5.0, 5.0)) = 0.5
        _MaskSoftness("윤슬 경계면 부드러움", Range(0.01, 0.5)) = 0.2
        _CausticsIntensity("윤슬 전체 강도", Range(0.0, 5.0)) = 1.0

        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha 

        Pass
        {
            Name "Main"
            ZWrite Off
            ZTest LEqual // 앞에 있을 때만 정상적으로 그립니다. (X-Ray 가림 판정용)

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog // URP 전역 포그 지원

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
                float4 _Color;
                float4 _RendererColor;
                float4 _Flip;
                float _OriginalRate;
                
                float4 _CausticsColor;
                float4 _CausticsSpeed;
                float _CausticsScale;
                float _MaskHeight;
                float _MaskSoftness;
                float _CausticsIntensity;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            TEXTURE2D(_ColorPart);
            SAMPLER(sampler_ColorPart);

            TEXTURE2D(_CausticsTex);
            SAMPLER(sampler_CausticsTex);

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // 유니티 SpriteRenderer 고유 기능 (FlipX, FlipY 지원)
                float3 posOS = input.positionOS.xyz;
                posOS.xy *= _Flip.xy;

                output.positionHCS = TransformObjectToHClip(posOS);
                output.uv = input.uv;
                // 유니티 SpriteRenderer 고유 기능 (Inspector Color 연동)
                output.color = input.color * _Color * _RendererColor;
                output.localPos = posOS.xy;
                
                // URP 포그 거리 팩터 연산
                output.fold = ComputeFogFactor(output.positionHCS.z);
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 maskCol = SAMPLE_TEXTURE2D(_ColorPart, sampler_ColorPart, input.uv);

                // 1. 기존 의태(Camouflage) 로직: 마스크 판별 및 색상 블렌딩
                bool isEye = (maskCol.b > 0.5 && maskCol.r < 0.5);
                bool isBody = (maskCol.a >= 0.05 && !isEye);

                if (isBody)
                {
                    half3 targetColor = input.color.rgb;
                    half3 camouRGB = c.rgb * targetColor;
                    c.rgb = lerp(camouRGB, c.rgb, _OriginalRate);
                }

                // 2. 투명도 적용 및 프리멀티플라이드 알파 (Blend One OneMinusSrcAlpha 대응)
                c.a *= input.color.a;
                c.rgb *= c.a; 

                // 3. 코스틱스(Caustics) 투사 (실제 이미지가 있는 부분에만 적용)
                if (c.a > 0.01)
                {
                    float2 causticsUV = input.localPos * _CausticsScale;
                    causticsUV += _Time.y * _CausticsSpeed.xy; 
                    
                    half4 causticsSample = SAMPLE_TEXTURE2D(_CausticsTex, sampler_CausticsTex, causticsUV);
                    half causticsLight = max(causticsSample.r, causticsSample.g); 
                    
                    // 발 밑(로컬 Y축 하단)부터 맺히도록 마스킹
                    half causticsMask = smoothstep(_MaskHeight - _MaskSoftness, _MaskHeight + _MaskSoftness, input.localPos.y);
                    
                    half3 causticsEffect = causticsLight * _CausticsColor.rgb * causticsMask * _CausticsIntensity;
                    
                    // 프리멀티플라이드 연산 규칙에 맞춰 알파를 곱해서 원본에 더함
                    c.rgb += causticsEffect * c.a;
                }

                // 4. URP 포그 연산 (프리멀티플라이드 알파 전용 커스텀 포그)
                // MixFog 함수 대신 커스텀 연산을 하므로, 씬 포그가 비활성화된 경우를 대비해 매크로로 감싸줍니다.
#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                half3 pmaFogColor = unity_FogColor.rgb * c.a;
                c.rgb = lerp(pmaFogColor, c.rgb, input.fold);
#endif

                return c;
            }
            ENDHLSL
        }
    }
}
