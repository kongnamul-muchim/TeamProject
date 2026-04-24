Shader "Hidden/UnderwaterEffects_FullScreen"
{
    Properties
    {
        _color ("Fog Color", Color) = (0, 0.5, 0.8, 1)
        _dis ("Fog Distance", Float) = 20
        _alpha ("Fog Alpha", Range(0, 1)) = 0.5
        _refraction ("Refraction Intensity", Float) = 0.1
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _normalUV ("Normal UV Tiling/Speed", Vector) = (1, 1, 0.2, 0.1)
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
            
            // 유니티 공식 Blit 라이브러리 포함
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes {
                uint vertexID : SV_VertexID;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            // 공식 Full Screen Pass 전용 소스 텍스처
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
            CBUFFER_END

            Varyings vert (Attributes input) {
                Varyings output;
                // 유니티 공식 풀스크린 삼각형 생성 방식
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            half4 frag (Varyings input) : SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 1. 굴절 효과 (노멀맵)
                float2 normalUV = input.uv * _normalUV.xy + _normalUV.zw * _Time.y;
                float3 normalSample = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUV));
                float2 offset = normalSample.xy * _refraction * 0.1;

                // 2. 화면 샘플링 (굴절 적용)
                float2 uv = input.uv + offset;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);

                // 3. 수중 포그 (깊이 기반)
                float rawDepth = SampleSceneDepth(uv);
                float depth = Linear01Depth(rawDepth, _ZBufferParams);
                
                // 안개 강도 계산
                float fogFactor = saturate(depth / (_dis * 0.1));
                float finalFog = saturate(fogFactor * _alpha);

                return lerp(col, _color, finalFog);
            }
            ENDHLSL
        }
    }
}