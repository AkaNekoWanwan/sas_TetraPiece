Shader "Sprites/InnerOutlineByAlpha"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [HDR] _StrokeColor ("Stroke Color", Color) = (1,1,1,1)
        _StrokeWidthPx ("Stroke Width (px)", Range(0,16)) = 3
        _AlphaThreshold ("Alpha Threshold", Range(0,1)) = 0.25
        _EdgeSoftnessPx ("Edge Softness (px)", Range(0,4)) = 0.0
        _Overlay ("Overlay Strength", Range(0,1)) = 1.0 // 0=無効,1=強く着色
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
                float4 pos  : SV_POSITION;
                float2 uv   : TEXCOORD0;
                fixed4 color: COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;

            fixed4 _StrokeColor;
            float  _StrokeWidthPx;
            float  _AlphaThreshold;
            float  _EdgeSoftnessPx;
            float  _Overlay;

            v2f vert (appdata v){
                v2f o;
                o.pos   = UnityObjectToClipPos(v.vertex);
                o.uv    = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            inline float SampleA(float2 uv, fixed4 tint){
                return (tex2D(_MainTex, uv) * tint).a;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 元スプライト
                fixed4 src = tex2D(_MainTex, i.uv) * i.color;
                float aCenter = src.a;

                // 中身（=内側）のみ対象
                if (aCenter < _AlphaThreshold){
                    return src; // 透明 or 外側はそのまま
                }

                // 境界までの内向き“最短距離”を近似（サンプル探索）
                float2 texel = _MainTex_TexelSize.xy;
                float  r     = max(_StrokeWidthPx, 0.0);

                // 8方向
                float2 dirs[8] = {
                    float2( 1, 0), float2(-1, 0), float2(0,  1), float2(0, -1),
                    float2( 1, 1), float2(-1, 1), float2(1, -1), float2(-1,-1)
                };

                // 距離初期値＝充分に遠い
                float minHit = 1e9;

                // サンプル数（太さに対する探索解像度）
                const int STEPS = 6;

                [unroll]
                for (int d=0; d<8; d++)
                {
                    float2 dir = normalize(dirs[d]);

                    [unroll]
                    for (int s=1; s<=STEPS; s++)
                    {
                        float t = (r * (s / (float)STEPS));  // px
                        float2 uv = i.uv + dir * t * texel;

                        // “内側から外側へ”進んで、アルファが閾値未満になった最初の位置を境界とみなす
                        float a = SampleA(uv, i.color);
                        if (a < _AlphaThreshold)
                        {
                            minHit = min(minHit, t);
                            break; // この方向はヒットしたので次へ
                        }
                    }
                }

                // 内側の境界付近だけストローク表示
                float strokeAlpha = 0.0;
                if (minHit <= r)
                {
                    // 硬い縁：1 - dist/r
                    float base = 1.0 - saturate(minHit / max(r, 1e-5));

                    // 端を少しだけ滑らかに
                    float soft = _EdgeSoftnessPx;
                    if (soft > 0.0)
                    {
                        // r の外側に向けてやわらかく減衰
                        float k0 = 0.0;
                        float k1 = saturate(r / max(r + soft, 1e-5));
                        strokeAlpha = smoothstep(k0, k1, base);
                    }
                    else
                    {
                        strokeAlpha = base;
                    }
                }

                // 色を“上から着色”する（アルファは元のまま）
                fixed4 outCol = src;
                if (strokeAlpha > 0.0 && _Overlay > 0.0)
                {
                    float t = strokeAlpha * _Overlay * _StrokeColor.a;
                    outCol.rgb = lerp(outCol.rgb, _StrokeColor.rgb, t);
                    // 透明度は元スプライト優先：内側線なのでカバー範囲を増やさない
                    outCol.a   = src.a;
                }

                return outCol;
            }
            ENDCG
        }
    }
    FallBack "Transparent"
}
