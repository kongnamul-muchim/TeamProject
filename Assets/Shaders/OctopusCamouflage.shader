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
            "Queue"="Transparent+100"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha 

        // =========================================
        // Pass 1: 일반 렌더링 (장애물 앞에 있을 때)
        // GPU의 ZTest LEqual이 하드웨어에서 가려짐을 판정합니다.
        // 장애물보다 앞에 있는 픽셀만 그려집니다.
        // =========================================
        Pass
        {
            Name "Main"
            ZWrite Off
            ZTest LEqual

            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment CamouFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            sampler2D _ColorPart;
            float _OriginalRate;

            fixed4 CamouFrag(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord);
                fixed4 maskCol = tex2D(_ColorPart, IN.texcoord);

                // 마스크 구역 판정
                // 외곽선: 마스크가 투명 (Alpha < 0.05)
                // 눈: 마스크가 새파란 색 (Blue > 0.5, Red < 0.5)
                // 몸통: 그 외 모든 불투명 영역
                bool isEye = (maskCol.b > 0.5 && maskCol.r < 0.5);
                bool isBody = (maskCol.a >= 0.05 && !isEye);

                // 몸통만 의태 적용 (순수 Multiply 기반)
                if (isBody)
                {
                    fixed3 targetColor = IN.color.rgb;
                    fixed3 camouRGB = c.rgb * targetColor;
                    c.rgb = lerp(camouRGB, c.rgb, _OriginalRate);
                }

                // 스프라이트 렌더러의 투명도 적용
                c.a *= IN.color.a;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }

        // =========================================
        // Pass 2: X-Ray 렌더링 (장애물 뒤에 있을 때)
        // GPU의 ZTest Greater가 하드웨어에서 가려짐을 판정합니다.
        // 장애물보다 뒤에 있는 픽셀만 그려집니다.
        // 그 중에서 마스크의 '외곽선' 영역만 출력하고 나머지는 discard합니다.
        // =========================================
        Pass
        {
            Name "XRay"
            Tags { "LightMode" = "XRayPass" }
            ZWrite Off
            ZTest Greater

            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment XRayFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            sampler2D _ColorPart;

            fixed4 XRayFrag(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord);
                fixed4 maskCol = tex2D(_ColorPart, IN.texcoord);

                // 외곽선 판정: 마스크의 투명한 부분이 외곽선
                bool isOutline = maskCol.a < 0.05;

                // 외곽선이 아니거나, 메인 텍스처에 실제 그림이 없는 부분은 버림
                // (캐릭터 바깥의 빈 공간이 그려지는 것을 방지)
                if (!isOutline || c.a < 0.01)
                {
                    discard;
                }

                // 외곽선은 원본 색상 그대로 출력
                c.a *= IN.color.a;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
