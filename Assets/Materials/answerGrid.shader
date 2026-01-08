Shader "Custom/answerGrid"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Fill Color", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width (Pixels)", Float) = 5.0
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

        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineWidth;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                fixed4 base = tex2D(_MainTex, uv);
                
                // 早期リターン：完全に透明ならスキップ（モバイル最適化）
                if (base.a < 0.01) return fixed4(0, 0, 0, 0);
                
                // ピクセル固定の太さをUV単位に変換
                float2 p = _MainTex_TexelSize.xy * _OutlineWidth;
                
                // 8方向のアルファ値をサンプリング（UV範囲外は0とみなす）
                // モバイル最適化：条件分岐を減らすため、saturate後に範囲外判定を兼ねる
                float2 uv_up    = uv + float2(0, p.y);
                float2 uv_down  = uv - float2(0, p.y);
                float2 uv_left  = uv - float2(p.x, 0);
                float2 uv_right = uv + float2(p.x, 0);
                
                float a_up    = (uv_up.x >= 0 && uv_up.x <= 1 && uv_up.y >= 0 && uv_up.y <= 1)       ? tex2D(_MainTex, uv_up).a : 0;
                float a_down  = (uv_down.x >= 0 && uv_down.x <= 1 && uv_down.y >= 0 && uv_down.y <= 1)   ? tex2D(_MainTex, uv_down).a : 0;
                float a_left  = (uv_left.x >= 0 && uv_left.x <= 1 && uv_left.y >= 0 && uv_left.y <= 1)   ? tex2D(_MainTex, uv_left).a : 0;
                float a_right = (uv_right.x >= 0 && uv_right.x <= 1 && uv_right.y >= 0 && uv_right.y <= 1) ? tex2D(_MainTex, uv_right).a : 0;
                
                // 対角線（斜め）- 必要に応じてコメントアウトでさらに軽量化可能
                float2 uv_ur = uv + p;
                float2 uv_ul = uv + float2(-p.x, p.y);
                float2 uv_dr = uv + float2(p.x, -p.y);
                float2 uv_dl = uv - p;
                
                float a_ur = (uv_ur.x >= 0 && uv_ur.x <= 1 && uv_ur.y >= 0 && uv_ur.y <= 1) ? tex2D(_MainTex, uv_ur).a : 0;
                float a_ul = (uv_ul.x >= 0 && uv_ul.x <= 1 && uv_ul.y >= 0 && uv_ul.y <= 1) ? tex2D(_MainTex, uv_ul).a : 0;
                float a_dr = (uv_dr.x >= 0 && uv_dr.x <= 1 && uv_dr.y >= 0 && uv_dr.y <= 1) ? tex2D(_MainTex, uv_dr).a : 0;
                float a_dl = (uv_dl.x >= 0 && uv_dl.x <= 1 && uv_dl.y >= 0 && uv_dl.y <= 1) ? tex2D(_MainTex, uv_dl).a : 0;

                // 最小アルファ値を計算（min関数をネストで効率化）
                float minAlpha = min(min(a_up, a_down), min(a_left, a_right));
                minAlpha = min(minAlpha, min(min(a_ur, a_ul), min(a_dr, a_dl)));

                // ジャギー緩和：smoothstepで境界を滑らかに
                float edgeFactor = smoothstep(0.05, 1, minAlpha * base.a);
                
                // 極薄い中間色の層を作成（境界付近で両色を混ぜた色を使用）
                // edgeFactorが0.3～0.7の範囲で中間色を適用
                float midLayerStrength = smoothstep(0.1, 0.5, edgeFactor) * (1.0 - smoothstep(0.5, 0.9, edgeFactor));
                fixed3 midColor = (_OutlineColor.rgb + IN.color.rgb) * 0.5; // アウトラインと内側の中間色
                
                // 3段階の色ブレンド：アウトライン → 中間層 → 内側
                fixed3 finalRGB = lerp(_OutlineColor.rgb, IN.color.rgb, edgeFactor);
                finalRGB = lerp(finalRGB, midColor, midLayerStrength * 0.9); // 0.6は中間層の強度（調整可能）
                
                float finalAlpha = base.a * IN.color.a;
                return fixed4(finalRGB, finalAlpha);
            }
        ENDCG
        }
    }
}