Shader "HideAndInk/OctopusCamouflage_Body"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _ColorPart ("Color Part/Mask (A=Mask)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
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
            ZTest LEqual // 앞에 있을 때만 정상적으로 그립니다.

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

                bool isEye = (maskCol.b > 0.5 && maskCol.r < 0.5);
                bool isBody = (maskCol.a >= 0.05 && !isEye);

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
