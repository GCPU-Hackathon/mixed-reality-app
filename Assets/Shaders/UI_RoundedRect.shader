Shader "UI/RoundedRectURP"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Radius ("Corner Radius (px)", Float) = 24
        _Feather ("Edge Softness (px)", Float) = 1.5
        _BorderWidth ("Border Width (px)", Float) = 0
        _BorderColor ("Border Color", Color) = (1,1,1,0)

        [HideInInspector]_MainTex ("Sprite", 2D) = "white" {}
        _RectSize ("Rect Size (px)", Vector) = (100,100,0,0)
    }

    SubShader
    {
        Tags{
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "CanUseSpriteAtlas"="True"
        }

        ZWrite Off
        ZTest Always
        Cull Off
        Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "RoundedRectURP_XR"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM

            //---------------------------------
            // Pragmas
            //---------------------------------
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag

            // SRP Batcher / instancing
            #pragma multi_compile_instancing

            // XR stereo variants
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO STEREO_MULTIVIEW STEREO_INSTANCING
            #pragma multi_compile _ XR_VIEW

            // On n'a pas besoin de fog/lighting ici.

            //---------------------------------
            // Includes
            //---------------------------------
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // sur certaines versions URP, UnityInput.hlsl existe, mais Core.hlsl suffit
            // #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"


            //---------------------------------
            // Structs
            //---------------------------------
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;

                UNITY_VERTEX_OUTPUT_STEREO
            };

            //---------------------------------
            // Uniforms / Material properties
            //---------------------------------
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _BorderColor;
                float4 _RectSize;    // xy = size px
                float  _Radius;
                float  _Feather;
                float  _BorderWidth;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            //---------------------------------
            // Helpers
            //---------------------------------

            // SDF d'un rectangle aux coins arrondis
            float sdRoundBox(float2 p, float2 halfSize, float r)
            {
                float2 q = abs(p) - (halfSize - r);
                float2 maxq = max(q, 0);
                float outsideDist = length(maxq);
                float insideDist = min(max(q.x, q.y), 0);
                return outsideDist + insideDist - r;
            }

            //---------------------------------
            // Vertex
            //---------------------------------
            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // Convert object space position -> clip space.
                // Core.hlsl fournit TransformObjectToHClip() directement dans beaucoup de versions URP.
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);

                OUT.uv    = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            //---------------------------------
            // Fragment
            //---------------------------------
            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float2 size = _RectSize.xy;

                // UV 0..1 -> coords pixels (origine au centre)
                float2 p = (IN.uv * size) - size * 0.5;

                // distance arrondie externe
                float dOuter = sdRoundBox(p, size * 0.5, _Radius);

                // alpha externe adoucie
                float alphaOuter = saturate(1.0 - smoothstep(0.0, _Feather, dOuter));

                // couleur de base : _Color * alpha vertex (i.color.a vient du Graphic/TMP/UI)
                float4 baseCol = _Color;
                baseCol.a *= IN.color.a;

                // BORDURE ?
                if (_BorderWidth > 0.0)
                {
                    float2 innerHalf   = (size * 0.5) - _BorderWidth;
                    float  innerRadius = max(_Radius - _BorderWidth, 0.0);

                    float dInner = sdRoundBox(p, innerHalf, innerRadius);

                    float edgeOuter  = saturate(1.0 - smoothstep(0.0, _Feather, dOuter));
                    float edgeInner  = saturate(1.0 - smoothstep(0.0, _Feather, -dInner));
                    float borderMask = edgeOuter * edgeInner;

                    float4 fillCol   = baseCol;
                    float4 borderCol = _BorderColor;

                    float3 rgbMix = lerp(fillCol.rgb, borderCol.rgb, borderMask);

                    float aFill   = fillCol.a    * alphaOuter;
                    float aBorder = borderCol.a  * borderMask;
                    float finalA  = max(aFill, aBorder);

                    return half4(rgbMix, finalA);
                }
                else
                {
                    float4 col = baseCol;
                    // si tu veux que Image.color (le tint Unity UI) module vraiment le RGB aussi :
                    col.rgb *= IN.color.rgb;
                    col.a   *= alphaOuter;
                    return half4(col.rgb, col.a);
                }
            }

            ENDHLSL
        }
    }

    FallBack Off
}
