Shader "Custom/SpriteDarkenGradient_ScreenX_NoScaleOffset"
{
    Properties
    {
        [PerRendererData] [NoScaleOffset] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color     ("Tint", Color) = (1,1,1,1)

        _Darkness  ("Max Darkness (0-1)", Range(0,1)) = 0.6
        _GradStart ("Gradient Start X (0-1)", Range(0,1)) = 0.60
        _GradEnd   ("Gradient End X (0-1)",   Range(0,1)) = 1.00
        _Curve     ("Curve (>=1)", Range(1,8)) = 1.8
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;

            float _Darkness;
            float _GradStart;
            float _GradEnd;
            float _Curve;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos       : SV_POSITION;
                float2 uv        : TEXCOORD0;
                fixed4 color     : COLOR;
                float4 screenPos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;                 // ★ そのまま使う（Tiling/Offset 無視）
                o.color = v.color * _Color;
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
  float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float normX = saturate(screenUV.x);
      

                // グラデーション
                float t = 0.0;
                if (_GradEnd > _GradStart)
                    t = saturate((normX - _GradStart) / max(1e-5, (_GradEnd - _GradStart)));
                t = pow(t, _Curve);

                // 暗くする（右端で _Darkness）
                float darkFactor = saturate(1.0 - _Darkness * t);
                col.rgb *= darkFactor;

                return col;
            }
            ENDCG
        }
    }

    FallBack "Sprites/Default"
}
