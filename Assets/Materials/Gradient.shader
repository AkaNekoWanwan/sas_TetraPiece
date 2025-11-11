Shader "UI/Gradient"
{
    Properties
    {
        _Color1 ("Color 1", Color) = (1, 1, 1, 1)
        _Color2 ("Color 2", Color) = (0, 0, 0, 1)
        _Angle  ("Gradient Angle", Range(0, 360)) = 0
        _MainTex ("Texture", 2D) = "white" {}

        _Gamma     ("Curve (Gamma)", Range(0.1, 5)) = 1
        _Mid       ("Midpoint Shift", Range(-0.5, 0.5)) = 0
        _Contrast  ("Contrast", Range(0, 2)) = 1
        _Invert    ("Invert (0/1)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color1;
            float4 _Color2;
            float  _Angle;
            float  _Gamma;
            float  _Mid;
            float  _Contrast;
            float  _Invert;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 方向ベクトル
                float rad = radians(_Angle);
                float2 dir = float2(cos(rad), sin(rad));

                // [0,1]^2 の四隅を投影して最小/最大を求め、0..1に正規化
                float d00 = dot(float2(0,0), dir);
                float d10 = dot(float2(1,0), dir);
                float d01 = dot(float2(0,1), dir);
                float d11 = dot(float2(1,1), dir);
                float dmin = min(min(d00, d10), min(d01, d11));
                float dmax = max(max(d00, d10), max(d01, d11));
                float denom = max(1e-5, dmax - dmin);

                float t = (dot(i.uv, dir) - dmin) / denom;   // 0..1

                // 中心シフト & コントラスト
                t = (t - 0.5 + _Mid) * _Contrast + 0.5;

                // 範囲クリップ
                t = saturate(t);

                // 曲線（ガンマ）
                t = pow(t, _Gamma);

                // 反転オプション
                if (_Invert > 0.5) t = 1.0 - t;

                // 補間してテクスチャと乗算（アルファ/形状を維持）
                fixed4 grad = lerp(_Color1, _Color2, t);
                fixed4 tex  = tex2D(_MainTex, i.uv);
                return grad * tex;
            }
            ENDCG
        }
    }
}
