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

            // 【重要】端の判定を考慮したサンプリング
            float GetAlphaSafe(float2 uv)
            {
                // UVが0-1の範囲外なら、そこは「透明（図形の外）」とみなす
                if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) return 0;
                return tex2D(_MainTex, uv).a;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 base = tex2D(_MainTex, IN.texcoord);
                
                // ピクセル固定の太さをUV単位に変換
                float2 p = _MainTex_TexelSize.xy * _OutlineWidth;
                
                // 8方向をチェック。1つでも「透明（または画面端の外）」があればそこはアウトライン
                float a_up    = GetAlphaSafe(IN.texcoord + float2(0, p.y));
                float a_down  = GetAlphaSafe(IN.texcoord - float2(0, p.y));
                float a_left  = GetAlphaSafe(IN.texcoord - float2(p.x, 0));
                float a_right = GetAlphaSafe(IN.texcoord + float2(p.x, 0));
                float a_ur    = GetAlphaSafe(IN.texcoord + float2(p.x, p.y));
                float a_ul    = GetAlphaSafe(IN.texcoord + float2(-p.x, p.y));
                float a_dr    = GetAlphaSafe(IN.texcoord + float2(p.x, -p.y));
                float a_dl    = GetAlphaSafe(IN.texcoord + float2(-p.x, -p.y));

                // 最小のアルファ値を取る（＝周囲に透明があれば0に近づく）
                float minAlpha = min(a_up, min(a_down, min(a_left, min(a_right, min(a_ur, min(a_ul, min(a_dr, a_dl)))))));

                fixed4 finalColor;
                
                // アルファが0.1以上（図形の範囲内）かつ、
                // 周囲がすべて不透明（minAlpha > 0.1）なら「内側の色」
                if (base.a > 0.1 && minAlpha > 0.1)
                {
                    finalColor.rgb = IN.color.rgb;
                    finalColor.a = base.a * IN.color.a;
                }
                else
                {
                    // それ以外（図形の端から指定ピクセル内）は「アウトラインの色」
                    finalColor.rgb = _OutlineColor.rgb;
                    finalColor.a = base.a * IN.color.a * _OutlineColor.a;
                }

                return finalColor;
            }
        ENDCG
        }
    }
}