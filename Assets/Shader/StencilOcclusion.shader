Shader "Custom/XRaySilhouette"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        
        [Header(Silhouette Settings)]
        _OutlineColor ("Silhouette Color", Color) = (0, 0, 0, 1)
        _OutlineAlpha ("Silhouette Alpha", Range(0, 1)) = 0.4
        
        [Header(Rim Light Settings)]
        _RimPower ("Rim Power", Range(0.1, 5)) = 2.0
        _RimIntensity ("Rim Intensity", Range(0, 1)) = 0.5
    }
    
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
        
        // ============== Pass 1: Normal Rendering ==============
        Pass
        {
            Name "NormalPass"
            Tags { "LightMode" = "ForwardBase" }
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };
            
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv);
            }
            ENDCG
        }
        
        // ============== Pass 2: X-Ray Silhouette (가려졌을 때) ==============
        Pass
        {
            Name "XRayPass"
            Tags { "LightMode" = "ForwardBase" }
            
            ZWrite Off
            ZTest Greater
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            fixed4 _OutlineColor;
            float _OutlineAlpha;
            float _RimPower;
            float _RimIntensity;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };
            
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                // Rim Light 계산 (외곽선 강조)
                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);
                float rim = 1.0 - saturate(dot(viewDir, normal));
                rim = pow(rim, _RimPower) * _RimIntensity;
                
                // 실루엣 색상에 림라이트 blending
                fixed4 silhouette = _OutlineColor;
                silhouette.a = _OutlineAlpha;
                
                // 림라이트가 있으면 약간 bright하게
                float3 finalColor = lerp(silhouette.rgb, silhouette.rgb + rim, _RimIntensity);
                
                return fixed4(finalColor, silhouette.a);
            }
            ENDCG
        }
    }
    
    FallBack "Diffuse"
}
