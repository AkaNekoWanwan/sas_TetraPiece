Shader "Custom/UIAlphaSolidForOutline"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _FillColor("Fill Color", Color) = (1,1,1,1)
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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile __ UNITY_UI_CLIP_RECT UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            // === 自前で定義 ===
            float4 _ClipRect;

            bool UnityRectContains(float2 pos, float4 clipRect)
            {
                return (pos.x >= clipRect.x && pos.x <= clipRect.z &&
                        pos.y >= clipRect.y && pos.y <= clipRect.w);
            }

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;

                #ifdef UNITY_UI_CLIP_RECT
                float4 worldPosition : TEXCOORD1;
                #endif

                float4 screenPos : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _FillColor;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;

                #ifdef UNITY_UI_CLIP_RECT
                o.worldPosition = v.vertex;
                #endif

                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float alpha = tex2D(_MainTex, i.uv).a;
                if (alpha < 0.001) discard;

                #ifdef UNITY_UI_CLIP_RECT
                if (!UnityRectContains(i.screenPos.xy, _ClipRect))
                    discard;
                #endif

                return fixed4(_FillColor.rgb, _FillColor.a * alpha) * i.color;
            }

            ENDCG
        }
    }
}
