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

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float2 uv : TEXCOORD0; float4 positionCS : SV_POSITION; };

            TEXTURE2D_X(_BlitTexture); SAMPLER(sampler_BlitTexture);
            TEXTURE2D_X(_CameraDepthTexture); SAMPLER(sampler_CameraDepthTexture);
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

                float3 normalSample = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv * _normalUV.xy + _normalUV.zw * _Time.y));
                float2 offset = normalSample.xy * _refraction * 0.02;

                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, input.uv + offset);
                
                float rawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, input.uv + offset).r;
                float depth = Linear01Depth(rawDepth, _ZBufferParams);
                
                float fogFactor = saturate(depth / (_dis * 0.1));
                float finalFog = saturate(fogFactor * _alpha);

                return lerp(col, _color, finalFog);
            }
            ENDHLSL
        }
    }
}