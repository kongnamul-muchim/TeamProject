Shader "Custom/PatternTransition"
{
    Properties
    {
        _MainTex ("Screen Texture", 2D) = "white" {} 
        _TransitionTex ("Transition Mask (R)", 2D) = "white" {} 
        _TileTex ("Pattern Tile (RGBA)", 2D) = "white" {} 
        
        _Progress ("Progress", Range(0, 1)) = 0.0
        _Width ("Transition Width", Range(0, 1)) = 0.5
        _TilePixelSize ("Tile Size (Pixels)", Float) = 32.0
        _TileGrownScale ("Max Scale", Range(0, 16)) = 2.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            sampler2D _TransitionTex;
            sampler2D _TileTex;

            float _Progress;
            float _Width;
            float _TilePixelSize;
            float _TileGrownScale;

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionHCS);
                return output;
            }

            float4 frag (Varyings i) : SV_Target
            {
                // 1. 타일 UV 계산 (fract -> frac로 수정)
                float2 pixelPos = i.screenPos.xy / i.screenPos.w * _ScreenParams.xy;
                float2 tile_uv = frac(pixelPos / _TilePixelSize);
                
                // 2. 트랜지션 로직
                float part = _Progress * (1.0 + 2.0 * _Width) - _Width;
                float transition = tex2D(_TransitionTex, i.uv).r;
                
                float window = saturate(_Width + (part - transition) / _Width);
                
                // 3. 타일 크기 조절
                tile_uv = tile_uv * 2.0 - 1.0;
                tile_uv /= (window * _TileGrownScale + 0.0001);
                tile_uv = (tile_uv + 1.0) * 0.5;

                // 4. 결과 출력
                float4 tileColor = tex2D(_TileTex, tile_uv);
                
                // 타일 경계 밖 처리 (frac 특성상 반복되는 것을 방지)
                if(tile_uv.x < 0.0 || tile_uv.x > 1.0 || tile_uv.y < 0.0 || tile_uv.y > 1.0) {
                    tileColor.a = 0;
                }

                // 배경 투명 처리: 타일 패턴만 보이도록 alpha 계산
                float alpha = tileColor.a * window;
                return float4(tileColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}