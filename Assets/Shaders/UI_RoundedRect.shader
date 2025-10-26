Shader "UI/RoundedRect"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Radius ("Corner Radius (px)", Float) = 24
        _Feather ("Edge Softness (px)", Float) = 1.5
        _BorderWidth ("Border Width (px)", Float) = 0
        _BorderColor ("Border Color", Color) = (1,1,1,0)
        // _MainTex is needed for UI batching (even if unused)
        [HideInInspector]_MainTex ("Sprite", 2D) = "white" {}
        // Sync depuis script (taille rect en pixels)
        _RectSize ("Rect Size (px)", Vector) = (100,100,0,0)
    }

    SubShader
    {
        Tags{ "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "RoundedRect"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;   // 0..1 over the rect
                float4 color : COLOR;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            float4 _Color;
            float4 _BorderColor;
            float  _Radius;
            float  _Feather;
            float  _BorderWidth;
            float4 _RectSize; // (w,h,0,0)

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;   // UI Image génère 0..1
                o.color = v.color * _Color;
                return o;
            }

            // distance signée d’un rectangle arrondi (SDF) centré, en pixels
            float sdRoundBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - (b - r);
                return length(max(q, 0)) + min(max(q.x, q.y), 0) - r;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Convertit UV (0..1) en coords pixels, origine au centre
                float2 size = _RectSize.xy;
                float2 p = (i.uv * size) - size * 0.5;

                // SDF pour le bord extérieur
                float dOuter = sdRoundBox(p, size * 0.5, _Radius);

                // Lissage bord externe
                float alphaOuter = saturate(1.0 - smoothstep(0.0, _Feather, dOuter));

                // Gérer le contour
                float alpha = alphaOuter;
                if (_BorderWidth > 0.0)
                {
                    float dInner = sdRoundBox(p, size * 0.5 - _BorderWidth, max(_Radius - _BorderWidth, 0.0));
                    float edgeOuter = saturate(1.0 - smoothstep(0.0, _Feather, dOuter));
                    float edgeInner = saturate(1.0 - smoothstep(0.0, _Feather, -dInner));
                    float borderMask = edgeOuter * edgeInner;

                    // Couleur de fond + bord
                    float4 fillCol = i.color;
                    float4 borderCol = _BorderColor;
                    float3 rgb = lerp(fillCol.rgb, borderCol.rgb, borderMask);
                    float a = max(fillCol.a * alphaOuter, borderCol.a * borderMask);
                    return float4(rgb, a);
                }

                return float4(i.color.rgb, i.color.a * alpha);
            }
            ENDCG
        }
    }
}
