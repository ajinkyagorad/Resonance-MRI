// Volume in a unit box (object space [-0.5, 0.5]^3 maps to the 3-D texture).
// Smooth fields (_Quad = 1: the |B| haze, B1, the gradient ramp in the spin grid) are not raymarched (0.8.2): each pixel
// finds its chord through the emitting region analytically (the bore cylinder for mode 1) and integrates it with the
// two-point Gauss-Legendre rule, two samples instead of 8-12. Only fields with thin structure (the hand glow's slab, the
// 3-D reconstruction) keep a short raymarch of _Steps samples.
// Emission modes are light only (Blend One One, Zero One: never covers the passthrough room):
//   0: scalar emission = tex.r * gain (hand glow, B1 haze, 3-D reconstruction).
//   1: field magnitude deviation dB = dot(tex, _Currents), v = 0.5 + 0.5 tanh(dB / W), emission v^2, masked to the bore.
//   2: the gradient's field change across the spin cube, dB/W = dot(p, _Grad), stronger side only, soft box edges.
// Matter modes (material blend One OneMinusSrcAlpha, premultiplied):
//   3: tissue colour and opacity per unit length from an RGBA texture, composited front to back.
//   4: (0.8.4) a scalar tex.r that both colours (_Color2 at low values to _Color at high) and makes the volume opaque in
//      proportion (_Gain per unit length), front to back: the hand's tipped slab and the 3-D image read over a bright room.
Shader "Resonance/Volume"
{
    Properties
    {
        _Vol ("Volume", 3D) = "" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Color2 ("Colour at low values (mode 4)", Color) = (0,0,0,1)
        _Gain ("Gain", Float) = 1
        _Currents ("Scaled currents", Vector) = (0,0,0,0)
        _Width ("Width", Float) = 1
        _Radius ("Mask radius", Float) = 0.5
        _Steps ("Steps", Float) = 24
        _Quad ("Smooth field: two-point chord integral instead of a raymarch", Float) = 0
        _Mode ("Mode", Float) = 0
        _Grad ("Gradient over W, object units", Vector) = (0,0,0,0)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendA ("Src alpha", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendA ("Dst alpha", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendA] [_DstBlendA]
            ZWrite Off
            Cull Back
            ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler3D _Vol; float4 _Color, _Color2; float _Gain, _Width, _Radius, _Steps, _Mode, _Quad; float4 _Currents, _Grad;
            // Emission per unit length at object point p for the emission modes 0-2.
            float Emission(float3 p)
            {
                if (_Mode > 1.5)
                {
                    float v = saturate(tanh(dot(p, _Grad.xyz)));
                    float3 q = abs(p);
                    return v * v * (1.0 - smoothstep(0.44, 0.5, q.x)) * (1.0 - smoothstep(0.44, 0.5, q.y)) * (1.0 - smoothstep(0.44, 0.5, q.z));
                }
                float4 s = tex3Dlod(_Vol, float4(p + 0.5, 0));
                if (_Mode > 0.5)
                {
                    float v = 0.5 + 0.5 * tanh(dot(s, _Currents) / _Width);
                    return v * v * (1.0 - smoothstep(_Radius * 0.92, _Radius, length(p.xy)));
                }
                return s.r;
            }
            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 obj : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_OUTPUT(v2f, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex); o.obj = v.vertex.xyz;
                return o;
            }
            float4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 camObj = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                float3 dir = normalize(i.obj - camObj);
                float3 inv = 1.0 / (dir + (abs(dir) < 1e-6) * 1e-6);
                float3 t1 = (-0.5 - i.obj) * inv, t2 = (0.5 - i.obj) * inv;
                float3 tm = max(t1, t2);
                float tExit = max(0, min(min(tm.x, tm.y), tm.z));
                int n = (int)_Steps;
                float dt = tExit / n;
                float worldPerObj = length(mul((float3x3)unity_ObjectToWorld, dir));
                if (_Quad > 0.5 && _Mode < 2.5)
                {
                    float ta = 0, tb = tExit;
                    if (_Mode > 0.5 && _Mode < 1.5)
                    {
                        // Clip to the bore: |xy| < R, |z| < 0.46 (where the old mask faded out).
                        float qa = dot(dir.xy, dir.xy), qb = dot(i.obj.xy, dir.xy), qc = dot(i.obj.xy, i.obj.xy) - _Radius * _Radius;
                        if (qa > 1e-8)
                        {
                            float disc = qb * qb - qa * qc; if (disc <= 0) return 0;
                            float sq = sqrt(disc); ta = max(ta, (-qb - sq) / qa); tb = min(tb, (-qb + sq) / qa);
                        }
                        else if (qc > 0) return 0;
                        if (abs(dir.z) > 1e-6) { float z1 = (-0.46 - i.obj.z) / dir.z, z2 = (0.46 - i.obj.z) / dir.z; ta = max(ta, min(z1, z2)); tb = min(tb, max(z1, z2)); }
                        else if (abs(i.obj.z) > 0.46) return 0;
                        if (tb <= ta) return 0;
                    }
                    float h = 0.5 * (tb - ta), m = 0.5 * (ta + tb), o = h * 0.57735027;
                    float f = Emission(i.obj + dir * (m - o)) + Emission(i.obj + dir * (m + o));
                    return float4(_Color.rgb * (_Gain * f * h * worldPerObj), 0); // light only
                }
                float jitter = frac(sin(dot(i.pos.xy, float2(12.9898, 78.233))) * 43758.5453);
                if (_Mode > 3.5)
                {
                    float3 col = 0; float a = 0;
                    [loop] for (int k = 0; k < 48; k++)
                    {
                        if (k >= n || a > 0.97) break;
                        float3 p = i.obj + dir * (dt * (k + jitter));
                        float s = tex3Dlod(_Vol, float4(p + 0.5, 0)).r;
                        float ak = saturate(s * _Gain * dt * worldPerObj);
                        col += (1 - a) * ak * lerp(_Color2.rgb, _Color.rgb, s); a += (1 - a) * ak;
                    }
                    return float4(col, a);
                }
                if (_Mode > 2.5)
                {
                    // Tissue: front-to-back compositing; opacity per metre in tex.a times the gain.
                    float3 col = 0; float a = 0;
                    [loop] for (int k = 0; k < 48; k++)
                    {
                        if (k >= n || a > 0.97) break;
                        float3 p = i.obj + dir * (dt * (k + jitter));
                        float4 s = tex3Dlod(_Vol, float4(p + 0.5, 0));
                        float ak = saturate(s.a * _Gain * dt * worldPerObj);
                        col += (1 - a) * ak * s.rgb; a += (1 - a) * ak;
                    }
                    return float4(col, a);
                }
                float acc = 0;
                [loop] for (int k = 0; k < 64; k++)
                {
                    if (k >= n) break;
                    float3 p = i.obj + dir * (dt * (k + jitter));
                    acc += Emission(p) * (_Mode > 0.5 && _Mode < 1.5 ? 1.0 - smoothstep(0.42, 0.5, abs(p.z)) : 1.0);
                }
                float3 c = _Color.rgb * (_Gain * acc * dt * worldPerObj);
                return float4(c, 0); // light only: never covers the room (premultiplied eye layer)
            }
            ENDCG
        }
    }
}
