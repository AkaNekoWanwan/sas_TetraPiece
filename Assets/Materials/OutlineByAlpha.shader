Shader "Sprites/OutlineByAlpha"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [HDR] _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineThickness ("Outline Thickness (px)", Range(0,16)) = 2
        _AlphaThreshold ("Alpha Threshold", Range(0,1)) = 0.2
        _Softness ("Edge Softness (px)", Range(0,4)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

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
                fixed4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize; // x=1/width, y=1/height
            fixed4 _Color;

            fixed4 _OutlineColor;
            float  _OutlineThickness;   // px
            float  _AlphaThreshold;
            float  _Softness;           // px

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            // アルファ取得（SpriteRendererのTintも反映）
            inline float SampleAlpha(float2 uv, fixed4 tint)
            {
                fixed4 c = tex2D(_MainTex, uv) * tint;
                return c.a;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 src = tex2D(_MainTex, i.uv) * i.color;
                float aCenter = src.a;

                // すでに中身（非透明）ならそのまま描画
                if (aCenter >= _AlphaThreshold)
                {
                    return src;
                }

                // アウトライン判定：近傍に“中身”があれば縁
                // 太さ(px) を UV オフセットに変換
                float2 px = float2(_MainTex_TexelSize.x, _MainTex_TexelSize.y);
                float r = max(_OutlineThickness, 0.0);

                // サンプル方向（8方向 + 斜め）
                float2 dirs[8] = {
                    float2( 1, 0), float2(-1, 0), float2(0,  1), float2(0, -1),
                    float2( 1, 1), float2(-1, 1), float2(1, -1), float2(-1,-1)
                };

                // 距離に応じたソフトネス（0ならくっきり）
                float softness = max(_Softness, 0.0);

                // 最短距離風スコア（0=遠い, 1=境界）
                float maxEdge = 0.0;

                // 何段か内挿して“太さ”分を見る（等間隔サンプル）
                // サンプル数を控えめにして軽量化
                const int STEPS = 6;
                [unroll]
                for (int d = 0; d < 8; d++)
                {
                    float2 dir = normalize(dirs[d]);
                    [unroll]
                    for (int s = 1; s <= STEPS; s++)
                    {
                        float t = (r * (s / (float)STEPS)); // px
                        float2 uv = i.uv + dir * t * px;
                        float aN = SampleAlpha(uv, i.color);

                        if (aN >= _AlphaThreshold)
                        {
                            // 近いほどエッジスコアが高い
                            float dist = t; // px単位
                            float edge = 1.0 - saturate((dist) / max(r, 1e-5));
                            maxEdge = max(maxEdge, edge);
                            break; // この方向はヒットしたので次の方向へ
                        }
                    }
                }

                if (maxEdge > 0.0)
                {
                    // ソフトネス（px）でフェード
                    float alpha = maxEdge;

                    if (softness > 0.0)
                    {
                        // 太さの外側に向けてソフトに減衰（擬似的）
                        // ここでは maxEdge 自体をやや丸める
                        alpha = smoothstep(0.0, 1.0, maxEdge * (r / max(r + softness, 1e-5)));
                    }

                    fixed4 ocol = _OutlineColor;
                    ocol.a *= alpha;
                    return ocol;
                }

                // どこにも中身が近くにない → 完全に透明
                return fixed4(0,0,0,0);
            }
            ENDCG
        }
    }
    FallBack "Transparent"
}
