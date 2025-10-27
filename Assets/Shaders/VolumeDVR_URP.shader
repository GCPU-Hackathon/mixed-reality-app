Shader "Custom/VolumeDVR_URP"
{
    Properties
    {
        _VolumeTex    ("VolumeTex", 3D) = "" {}
        _TFTex        ("Transfer LUT", 2D) = "" {}
        _LabelCtrlTex ("Label Ctrl Tex", 2D) = "" {}

        _SampleCount  ("Sample Count", Range(32, 1024)) = 256

        _Brightness   ("Brightness", Range(0,5)) = 1.5
        _AlphaScale   ("Alpha Scale", Range(0,2)) = 0.5

        _LightDir        ("Light Dir (world)", Vector) = (0,0,1,0)
        _LightIntensity  ("Light Intensity", Range(0,5)) = 1.0
        _Ambient         ("Ambient", Range(0,1)) = 0.2

        _IsLabelMap ("Is LabelMap (1/0)", Int) = 1
        _P1         ("P1 (cont)", Float) = 0.0
        _P99        ("P99 (cont)", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalRenderPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Cull Front         // même que ton shader actuel
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "RaymarchVolume"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM

            // ===== PRAGMAS IMPORTANTES POUR URP XR =====
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            // instancing + multiview/single-pass stereo
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO STEREO_MULTIVIEW_INSTANCING_ON

            // URP core includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ===== TEXTURES / SAMPLERS =====
            TEXTURE3D(_VolumeTex);
            SAMPLER(sampler_VolumeTex);

            TEXTURE2D(_TFTex);
            SAMPLER(sampler_TFTex);

            TEXTURE2D(_LabelCtrlTex);
            SAMPLER(sampler_LabelCtrlTex);

            // ===== UNIFORMS =====
            int     _SampleCount;
            float   _Brightness;
            float   _AlphaScale;

            float4  _LightDir;
            float   _LightIntensity;
            float   _Ambient;

            int     _IsLabelMap;
            float   _P1;
            float   _P99;

            // Unity fournit toujours :
            // float3 _WorldSpaceCameraPos;
            // float4x4 unity_ObjectToWorld;
            // float4x4 unity_WorldToObject;

            // ===== VERTEX INPUT/OUTPUT =====
            struct Attributes
            {
                float3 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos   : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ===== VERTEX SHADER =====
            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // URP helper: object space -> homogenous clip space
                o.positionCS = TransformObjectToHClip(float4(v.positionOS, 1.0));

                // world position
                o.worldPos   = mul(unity_ObjectToWorld, float4(v.positionOS,1)).xyz;

                return o;
            }

            // ===== UTILS =====
            float rand(float2 co)
            {
                return frac(sin(dot(co.xy, float2(12.9898,78.233))) * 43758.5453);
            }

            // Intersection rayon / cube en OBJ space (cube [-0.5,+0.5]^3)
            bool RayBoxIntersect(float3 rayOrigin, float3 rayDir, out float tEnter, out float tExit)
            {
                float3 boxMin = float3(-0.5, -0.5, -0.5);
                float3 boxMax = float3( 0.5,  0.5,  0.5);

                float3 t0 = (boxMin - rayOrigin) / rayDir;
                float3 t1 = (boxMax - rayOrigin) / rayDir;

                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);

                tEnter = max(max(tmin.x, tmin.y), tmin.z);
                tExit  = min(min(tmax.x, tmax.y), tmax.z);

                return (tExit > max(tEnter, 0.0));
            }

            // estimation de la normale volume pour l'éclairage
            float3 EstimateNormal(float3 pObj)
            {
                float eps = 0.002;

                float3 uvw_xp = (pObj + float3(+eps,0,0)) + 0.5;
                float3 uvw_xm = (pObj + float3(-eps,0,0)) + 0.5;
                float3 uvw_yp = (pObj + float3(0,+eps,0)) + 0.5;
                float3 uvw_ym = (pObj + float3(0,-eps,0)) + 0.5;
                float3 uvw_zp = (pObj + float3(0,0,+eps)) + 0.5;
                float3 uvw_zm = (pObj + float3(0,0,-eps)) + 0.5;

                float vxp = SAMPLE_TEXTURE3D(_VolumeTex, sampler_VolumeTex, uvw_xp).r;
                float vxm = SAMPLE_TEXTURE3D(_VolumeTex, sampler_VolumeTex, uvw_xm).r;
                float vyp = SAMPLE_TEXTURE3D(_VolumeTex, sampler_VolumeTex, uvw_yp).r;
                float vym = SAMPLE_TEXTURE3D(_VolumeTex, sampler_VolumeTex, uvw_ym).r;
                float vzp = SAMPLE_TEXTURE3D(_VolumeTex, sampler_VolumeTex, uvw_zp).r;
                float vzm = SAMPLE_TEXTURE3D(_VolumeTex, sampler_VolumeTex, uvw_zm).r;

                float3 grad = float3(vxp - vxm, vyp - vym, vzp - vzm);
                float len2 = max(dot(grad, grad), 1e-6);
                return grad / sqrt(len2);
            }

            // LUT pour obtenir couleur/alpha depuis voxel
            float4 SampleTF(float val)
            {
                if (_IsLabelMap == 1)
                {
                    // label discret (0..255)
                    float idx = round(val);
                    idx = clamp(idx, 0.0, 255.0);

                    float u = idx / 255.0;

                    float4 tfBase = SAMPLE_TEXTURE2D(_TFTex, sampler_TFTex, float2(u,0.5));
                    float4 ctrl   = SAMPLE_TEXTURE2D(_LabelCtrlTex, sampler_LabelCtrlTex, float2(u,0.5));

                    float3 rgb = tfBase.rgb * ctrl.rgb;
                    float  a   = tfBase.a   * ctrl.a;

                    return float4(rgb, a);
                }
                else
                {
                    // intensité continue (CT/IRM normalisé entre P1 et P99)
                    float normVal = (val - _P1) / max((_P99 - _P1), 1e-6);
                    normVal = saturate(normVal);

                    float4 tf = SAMPLE_TEXTURE2D(_TFTex, sampler_TFTex, float2(normVal,0.5));
                    return tf;
                }
            }

            // ===== FRAGMENT SHADER =====
            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                // cam pos en OBJ space
                float3 camPosObj  = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos,1)).xyz;

                // pixel position en OBJ space
                float3 fragPosObj = mul(unity_WorldToObject, float4(i.worldPos,1)).xyz;

                // direction rayon dans OBJ space
                float3 rayDirObj  = normalize(fragPosObj - camPosObj);

                // intersecte le cube [-0.5..+0.5]
                float tEnter, tExit;
                if (!RayBoxIntersect(camPosObj, rayDirObj, tEnter, tExit))
                    return half4(0,0,0,0);

                float t = max(tEnter, 0.0);
                float rayLength = max(0.0, tExit - t);
                if (rayLength < 0.0001)
                    return half4(0,0,0,0);

                float dt = rayLength / (float)_SampleCount;

                // jitter pour éviter le banding
                t += rand(i.positionCS.xy) * dt;

                float3 accRGB = float3(0,0,0);
                float  accA   = 0.0;

                // direction de la lumière dans OBJ space (pour lambert)
                float3 Lobj = mul((float3x3)unity_WorldToObject, normalize(_LightDir.xyz));

                const int MAX_STEPS = 2048;
                [loop]
                for (int step = 0; step < MAX_STEPS; step++)
                {
                    if (t > tExit) break;
                    if (accA >= 0.9999) break;

                    float3 pObj = camPosObj + rayDirObj * t;

                    // coord volume 0..1
                    float3 uvw = pObj + 0.5;

                    // échantillon voxel
                    float voxelVal = SAMPLE_TEXTURE3D(_VolumeTex, sampler_VolumeTex, uvw).r;

                    // couleur / alpha (via LUT)
                    float4 tf = SampleTF(voxelVal);

                    float aSample = tf.a * _AlphaScale;

                    if (aSample > 0.001)
                    {
                        // shading volumique simple (Lambert)
                        float3 N = EstimateNormal(pObj);
                        float lambert = saturate(dot(normalize(N), normalize(Lobj)));
                        float lighting = _Ambient + _LightIntensity * lambert;

                        float3 litColor = tf.rgb * lighting;

                        float remain = 1.0 - accA;
                        accRGB += remain * litColor * aSample;
                        accA   += remain * aSample;
                    }

                    t += dt;
                }

                accRGB *= _Brightness;
                return half4(accRGB, accA);
            }

            ENDHLSL
        }
    }
}
