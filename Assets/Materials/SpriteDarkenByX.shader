Shader "Custom/SpriteDarken_Right_SafeAlpha"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _Color   ("Tint", Color) = (1,1,1,1)
        _MaxDarkness ("Max Darkness (0..1)", Range(0,1)) = 0.5
        _StartX      ("Start X (0..1)", Range(0,1)) = 0.0
        _Power       ("Falloff Power", Range(0.1, 8)) = 1.0
        _MinAlpha    ("Min Alpha Clamp", Range(0,1)) = 0.001 // 極端な落ち込み対策
    }
    SubShader
    {
        Tags{
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "CanUseSpriteAtlas"="True"
        }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            half _MaxDarkness, _StartX, _Power, _MinAlpha;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };
            struct v2f {
                float2 uv      : TEXCOORD0;
                fixed4 color   : COLOR;
                float4 pos     : SV_POSITION;
                float4 sp      : TEXCOORD1; // screen pos
            };

            v2f vert(appdata v){
                v2f o;
                o.pos   = UnityObjectToClipPos(v.vertex);
                o.uv    = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                o.sp    = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;

                // 画面X [0,1]
                half sx = saturate(i.sp.x / i.sp.w);
                // 右端へ向かうほど 0→1
                half t = saturate((sx - _StartX) / max(1e-4h, (1.0h - _StartX)));
                t = pow(t, _Power);

                // 暗さ係数
                half dark = 1.0h - (_MaxDarkness * t);
                c.rgb *= dark;

                // αは保持（最低でも _MinAlpha にクランプ）
                c.a = max(c.a, _MinAlpha);

                return c;
            }
            ENDCG
        }
    }
}
