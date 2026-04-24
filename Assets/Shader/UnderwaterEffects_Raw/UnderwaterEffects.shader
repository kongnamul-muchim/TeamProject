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

        [Header(God Rays)]
        _RayColor ("Ray Color", Color) = (1, 1, 1, 0.3)
        _RayIntensity ("Ray Intensity", Range(0, 2)) = 0.5
        _RaySpeed ("Ray Speed", Float) = 0.2
        _RayScale ("Ray Scale", Float) = 5.0
        _RayTilt ("Ray Tilt (Angle)", Range(-1, 1)) = 0.3
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
                float _RayTilt;
            CBUFFER_END

            Varyings vert (Attributes input) {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            // 고품질 줄기 생성을 위한 노이즈 함수
            float GradientNoise(float x) {
                return (sin(x) + sin(x * 2.3) + sin(x * 5.7) + sin(x * 11.3)) * 0.25;
            }

            half4 frag (Varyings input) : SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // 1. 굴절
                float2 normalUV = input.uv * _normalUV.xy + _normalUV.zw * _Time.y;
                float3 normalSample = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUV));
                float2 offset = normalSample.xy * _refraction * 0.05;

                // 2. 화면 샘플링
                float2 uv = input.uv + offset;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);

                // 3. 깊이
                float rawDepth = SampleSceneDepth(uv);
                float depth = Linear01Depth(rawDepth, _ZBufferParams);

                // 4. 스크린샷 스타일 Godrays 계산
                // UV를 각도(Tilt)에 따라 변형
                float rayCoord = input.uv.x + (input.uv.y * _RayTilt);
                rayCoord *= _RayScale;

                float time = _Time.y * _RaySpeed;
                
                // 여러 겹의 파동을 섞어 뚜렷한 줄기 형성
                float rays = 0;
                rays += GradientNoise(rayCoord + time);
                rays += GradientNoise(rayCoord * 0.6 - time * 0.7) * 0.5;
                
                // 줄기를 날카롭게 (Saturate & Power)
                rays = saturate(rays);
                rays = pow(rays, 3.0) * _RayIntensity;

                // 미세한 깜빡임(Flicker) 추가
                float flicker = 1.0 + 0.1 * sin(_Time.y * 2.0);
                rays *= flicker;

                // 아래로 갈수록 흐려짐 (스크린샷처럼 부드럽게)
                float falloff = saturate(1.1 - input.uv.y);
                rays *= pow(falloff, 1.5);
                
                // 깊이 가림
                rays *= saturate(depth * 10.0);

                // 5. 최종 합성
                float fogFactor = saturate(depth / max(0.001, _dis * 0.1));
                float finalFog = saturate(fogFactor * _alpha);
                
                half4 finalCol = lerp(col, _color, finalFog);
                
                // Additive 합성
                finalCol.rgb += _RayColor.rgb * rays * _RayColor.a;

                return finalCol;
            }
            ENDHLSL
        }
    }
}