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

        // 기본 메인 패스 (URP 2D 멀티패스 무시 버그 방지를 위한 단일 패스)
        Pass
        {
            Name "Main"
            ZWrite Off
            ZTest LEqual 

            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment CustomFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            sampler2D _ColorPart;
            float _OriginalRate;

            fixed4 CustomFrag(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture (IN.texcoord);
                fixed4 maskCol = tex2D(_ColorPart, IN.texcoord);

                // 마스크 타겟색
                fixed3 camouflageTarget = maskCol.rgb * IN.color.rgb;

                // [핵심 강화] 아티스트님의 마스크 이미지가 유니티에서 알파값으로 완벽히 뚫리지 않았을 경우를 대비한 2중 보호 로직
                // 마스크의 투명도(a)가 낮거나, 어둡게 칠해진(r) 부분은 무조건 0(보호 구역: 눈, 외곽선)으로 간주합니다.
                // 투명하게 지웠든, 까맣게 칠했든 무조건 눈을 원본 색으로 지켜냅니다!
                float isBody = (maskCol.a < 0.1 || maskCol.r < 0.1) ? 0.0 : maskCol.a;

                // 의태 시: isBody가 0이면 눈/외곽선이므로 c.rgb(원본), 몸통은 타겟색
                fixed3 fullyCamouflaged = lerp(c.rgb, camouflageTarget, isBody);
                
                // 평상시: 무조건 원본
                fixed3 unCamouflaged = c.rgb; 
                
                // 애니메이션에 따른 보간
                fixed3 finalRGB = lerp(fullyCamouflaged, unCamouflaged, _OriginalRate);
                
                // SpriteRenderer의 원본 알파 유지 및 추가 페이드 효과
                c.a *= IN.color.a;

                c.rgb = finalRGB * c.a; 
                return c;
            }
            ENDCG
        }
    }
}
