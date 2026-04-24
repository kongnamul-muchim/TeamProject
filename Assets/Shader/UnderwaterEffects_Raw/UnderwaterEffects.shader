Shader "Hidden/UnderwaterEffects_FullScreen"
{
    Properties
    {
        [HideInInspector] _MainTex ("Base (RGB)", 2D) = "white" {}
        _color ("Fog Color", Color) = (0, 0.5, 0.8, 1)
        _dis ("Fog Distance", Float) = 20
        _alpha ("Fog Alpha", Range(0, 1)) = 0.5
        _refraction ("Refraction Intensity", Float) = 0.1
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
            // 유니티 표준 깊이 텍스처 라이브러리 포함
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float2 uv : TEXCOORD0; float4 positionCS : SV_POSITION; };

            TEXTURE2D_X(_BlitTexture); SAMPLER(sampler_BlitTexture);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _color; float _dis; float _alpha; float _refraction; float4 _normalUV;
            CBUFFER_END

            Varyings vert (Attributes input) {
                Varyings output;
                output.positionCS = float4(input.positionOS.xyz, 1.0);
                output.uv = input.uv;
                return output;
            }

            half4 frag (Varyings input) : SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 1. 노멀맵 기반 굴절
                float3 normalSample = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv * _normalUV.xy + _normalUV.zw * _Time.y));
                float2 offset = normalSample.xy * _refraction * 0.02;

                // 2. 화면 컬러 샘플링
                float2 uv = input.uv + offset;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);
                
                // 3. 유니티 표준 방식으로 깊이 값 샘플링
                float rawDepth = SampleSceneDepth(uv);
                float depth = Linear01Depth(rawDepth, _ZBufferParams);
                
                // 4. 안개 계산 (검은 화면 방지를 위해 saturate 및 max 처리)
                float fogFactor = saturate(depth / max(0.01, _dis * 0.1));
                float finalFog = saturate(fogFactor * _alpha);

                return lerp(col, _color, finalFog);
            }
            ENDHLSL
        }
    }
}