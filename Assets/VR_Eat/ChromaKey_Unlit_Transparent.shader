Shader "ChromaKey/Unlit/Transparent_FromChroma"
{
    Properties
    {
        [Header(Material)] 
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _ChromaColor ("Chroma Key Color", Color) = (0,1,0,1)
        _Threshold ("Threshold", Range(0,1)) = 0.1
        _Smoothness ("Smoothness", Range(0,1)) = 0.08
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Culling", Float) = 2
        [Toggle] [KeyEnum(Off, On)] _ZWrite ("ZWrite", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Cull [_Cull]
            ZWrite [_ZWrite]
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _ChromaColor;
            float _Threshold;
            float _Smoothness;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float3 rgb2hsv(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));

                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // HSVで色差を計算
                float3 texHSV = rgb2hsv(col.rgb);
                float3 keyHSV = rgb2hsv(_ChromaColor.rgb);
                
                // 色相と彩度の差を重視した色差計算
                float hueDiff = abs(texHSV.x - keyHSV.x);
                hueDiff = min(hueDiff, 1.0 - hueDiff); // 色相は循環するため
                float satDiff = abs(texHSV.y - keyHSV.y);
                
                // 色差スコアの計算（色相の差を重視）
                float colorDiff = hueDiff * 2.0 + satDiff;
                
                // スムーズなアルファ値の計算
                float alpha = smoothstep(_Threshold, _Threshold + _Smoothness, colorDiff);
                
                // 最終カラーの計算
                fixed4 finalColor = col * _Color;
                finalColor.a = alpha;
                
                return finalColor;
            }
            ENDHLSL
        }
    }

    Fallback "Unlit/Transparent"
}
