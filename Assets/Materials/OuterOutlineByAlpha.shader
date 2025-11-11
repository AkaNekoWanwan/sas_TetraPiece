Shader "Sprites/OutlineSimple_Texel8Dir"
{
   
   Properties
    {
        [PerRendererData] [NoScaleOffset] _MainTex ("Sprite", 2D) = "white" {}

        [HDR] _OutlineColor ("Fill + Outline Color", Color) = (0,0,0,1)
        _OutlineSize ("Outline Size (texels)", Float) = 2
        _AlphaThreshold ("Alpha Threshold", Range(0,1)) = 0.25
    }

    SubShader
    {
        Tags{
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
            #pragma target 2.0
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
                float4 pos    : SV_POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _OutlineColor;
            float  _OutlineSize;
            float  _AlphaThreshold;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos   = UnityObjectToClipPos(v.vertex);
                o.uv    = v.uv;
                o.color = v.color; // ここではTint不要
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 src = tex2D(_MainTex, i.uv) * i.color;

                // 本体を塗りつぶし色で描画
                if (src.a >= _AlphaThreshold)
                {
                    fixed4 col = _OutlineColor;
                    col.a = src.a; // 元の透過度は維持
                    return col;
                }

                // アウトラインチェック
                float s = _OutlineSize;
                float2 du = _MainTex_TexelSize.xy;

                float2 off[8] = {
                    float2( s*du.x,  0.0     ),
                    float2(-s*du.x,  0.0     ),
                    float2( 0.0,     s*du.y  ),
                    float2( 0.0,    -s*du.y  ),
                    float2( s*du.x,  s*du.y  ),
                    float2(-s*du.x,  s*du.y  ),
                    float2( s*du.x, -s*du.y  ),
                    float2(-s*du.x, -s*du.y  )
                };

                float maxA = 0.0;
                [unroll]
                for (int k=0; k<8; k++)
                {
                    float a = (tex2D(_MainTex, i.uv + off[k]) * i.color).a;
                    maxA = max(maxA, a);
                }

                if (maxA >= _AlphaThreshold)
                {
                    fixed4 col = _OutlineColor;
                    return col;
                }

                // 完全透明
                return 0;
            }
            ENDCG
        }
    }
    FallBack "Transparent"
}