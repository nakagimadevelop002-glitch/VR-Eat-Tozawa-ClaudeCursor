Shader "ChromaKeyToAlphaBlit"
{
    Properties{
        _MainTex("Source", 2D) = "white" {}
        _ChromaColor("Chroma", Color) = (0,1,0,1)
        _Threshold("Threshold", Range(0,1)) = 0.20
    }
        SubShader{
            Tags{ "RenderType" = "Opaque" } ZWrite Off ZTest Always Cull Off
            Blend One Zero   // ← 前フレームにブレンドしない（完全上書き）
            Pass{
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #include "UnityCG.cginc"
                sampler2D _MainTex; float4 _ChromaColor; float _Threshold;

                struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
                v2f vert(float4 v:POSITION, float2 uv : TEXCOORD0) { v2f o; o.pos = UnityObjectToClipPos(v); o.uv = uv; return o; }

                fixed4 frag(v2f i) :SV_Target{
                    fixed4 s = tex2D(_MainTex, i.uv);

                // キー色に近いほど 0、遠いほど 1
                float d = distance(s.rgb, _ChromaColor.rgb);
                float a = smoothstep(_Threshold - 0.05, _Threshold + 0.05, d);

                // プレマルチ（縁の色かぶり低減）
                s.rgb *= a;

                return fixed4(s.rgb, a);
            }
            ENDCG
        }
        }
}
