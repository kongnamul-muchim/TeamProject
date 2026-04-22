Shader "HideAndInk/OctopusCamouflage"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _ColorPart ("Color Part/Mask (A=Mask)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        // 의태 진행도 (0 = 완전 의태, 1 = 기본)
        _OriginalRate ("Original Rate", Range(0,1)) = 1.0

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
            ZTest Always 

            CGPROGRAM
            #pragma vertex CustomSpriteVert
            #pragma fragment CustomFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            sampler2D _ColorPart;
            float _OriginalRate;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct v2f_custom
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f_custom CustomSpriteVert(appdata_t IN)
            {
                v2f_custom OUT;
                UNITY_SETUP_INSTANCE_ID (IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO (OUT);

                OUT.vertex = UnityFlipSprite(IN.vertex, _Flip);
                OUT.vertex = UnityObjectToClipPos(OUT.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _RendererColor;

                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap (OUT.vertex);
                #endif

                OUT.screenPos = ComputeScreenPos(OUT.vertex);

                return OUT;
            }

            fixed4 CustomFrag(v2f_custom IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // --- 1. 깊이 비교 (X-Ray 판정 원초적 해킹) ---
                float2 screenUV = IN.vertex.xy / _ScreenParams.xy;
                float sceneRawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUV);
                float currentRawDepth = IN.vertex.z;

                bool isObscured;
                #if defined(UNITY_REVERSED_Z)
                    isObscured = currentRawDepth < sceneRawDepth - 0.001; 
                #else
                    isObscured = currentRawDepth > sceneRawDepth + 0.001;
                #endif

                // --- 2. 기본 색상 및 마스크 샘플링 ---
                fixed4 c = SampleSpriteTexture (IN.texcoord);
                fixed4 maskCol = tex2D(_ColorPart, IN.texcoord);

                // --- 3. 마스크 구역 판정 ---
                float maskLum = dot(maskCol.rgb, fixed3(0.299, 0.587, 0.114));
                bool isEye = (maskCol.b > 0.5 && maskCol.r < 0.5); 
                
                bool isOutline = (maskCol.a < 0.05) || (maskLum < 0.05 && !isEye);
                bool isBody = (maskCol.a >= 0.05 && !isEye && maskLum >= 0.05);

                // --- 4. 장애물에 가려진 상태일 때의 처리 ---
                if (isObscured)
                {
                    if (isOutline)
                    {
                        fixed4 xRayCol = c * IN.color;
                        xRayCol.rgb *= xRayCol.a;
                        return xRayCol;
                    }
                    else
                    {
                        discard; 
                    }
                }

                // --- 5. 가려지지 않았을 때의 색상 연산 ---
                if (isBody)
                {
                    fixed3 targetColor = IN.color.rgb; 
                    fixed3 camouRGB = c.rgb * targetColor;
                    c.rgb = lerp(camouRGB, c.rgb, _OriginalRate);
                }
                
                c.a *= IN.color.a;
                c.rgb *= c.a; 
                
                return c;
            }
            ENDCG
        }
    }
}
