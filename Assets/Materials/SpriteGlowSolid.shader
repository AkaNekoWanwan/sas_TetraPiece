Shader "Custom/SpriteGlowSolid"
{
    Properties
    {
        _Color ("Glow Color", Color) = (1,1,1,1)
        _Intensity ("Glow Intensity", Range(0, 5)) = 1
        _Thickness ("Glow Thickness", Range(0,0.2)) = 0.05
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
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
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Intensity;
            float _Thickness;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 元スプライトのアルファを参照
                fixed4 tex = tex2D(_MainTex, i.uv);
                float alpha = tex.a;

                // 外側にぼかしを追加（簡易的な「グロー」）
                float glow = smoothstep(0.0, _Thickness, alpha);
                fixed4 col = _Color * _Intensity;
                col.a *= glow;

                return col;
            }
            ENDCG
        }
    }
}
