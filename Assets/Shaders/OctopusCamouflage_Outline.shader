Shader "HideAndInk/OctopusCamouflage_Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _ColorPart ("Color Part/Mask (A=Mask)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

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
            Name "XRayOutline"
            ZWrite Off
            ZTest Greater // 장애물 뒤에 가려졌을 때'만' 화면에 그립니다.

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

                // 마스크 구역 판정: 외곽선은 순수하게 '마스크 텍스처에서 투명한 곳(알파 < 0.05)'만 인정합니다.
                bool isOutline = (maskCol.a < 0.05);

                // 외곽선이 아닌 몸통(알파가 있는 곳)이나 스프라이트 바깥의 진짜 빈 공간은 모조리 날려버립니다.
                if (!isOutline || c.a < 0.01)
                {
                    discard;
                }

                // 외곽선 통과: 원본 색상을 그대로 유지하여 그립니다. (의태 영향 안 받음)
                c.a *= IN.color.a;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
