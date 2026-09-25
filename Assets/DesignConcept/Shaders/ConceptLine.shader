// Design 0.9 concept: soft luminous lines (plot traces, field lines, frames, leaders). LineBuilder ribbons that face the
// eye; the ribbon is drawn _Widen times wider than the line, so the core is anti-aliased and surrounded by a soft glow
// (light only) and, optionally, a soft warm-dark halo (for lines over a bright room; never a hard casing).
//   TEXCOORD0 x side (-1..1), y width (m), z arc length, w reveal key; COLOR colour; _Reveal clips keys beyond it.
//   _Fade: alpha fades toward the ends of a line given by its arc length (z) in [0, _FadeLen] and beyond _FadeEnd.
Shader "Concept/Line"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Widen ("Ribbon width / line width", Float) = 3
        _CoreAlpha ("Core alpha", Float) = 1
        _Glow ("Glow", Float) = 0.35
        _Halo ("Dark halo", Float) = 0
        _HaloColor ("Halo colour", Color) = (0.07, 0.05, 0.04, 1)
        _Reveal ("Reveal up to key", Float) = 1000000
        _Future ("Alpha beyond the reveal", Float) = 0
        _FadeEnd ("Fade after arc length (m)", Float) = 1000
        _FadeLen ("Fade length (m)", Float) = 0.1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color, _HaloColor; float _Widen, _CoreAlpha, _Glow, _Halo, _Reveal, _Future, _FadeEnd, _FadeLen;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float4 d : TEXCOORD0; };
            v2f vert (appdata v)
            {
                v2f o;
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 wd = mul((float3x3)unity_ObjectToWorld, v.normal);
                float scale = length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x));
                float3 side = cross(wd, _WorldSpaceCameraPos.xyz - wp);
                float len = length(side); side = len > 1e-12 ? side / len : float3(0, 1, 0);
                wp += side * (v.uv.x * 0.5 * v.uv.y * scale * _Widen);
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1));
                o.color = v.color * _Color;
                o.d = float4(v.uv.x * _Widen, v.uv.w, v.uv.z, 0);
                return o;
            }
            float4 frag (v2f i) : SV_Target
            {
                float r = abs(i.d.x);                  // 0 at the centre, 1 at the line's edge, _Widen at the ribbon's edge
                float aa = max(fwidth(r), 1e-4);
                float core = saturate((1 - r) / aa + 0.5);
                float glow = _Glow * exp(-1.6 * max(r - 0.6, 0) * max(r - 0.6, 0)) * (1 - smoothstep(_Widen * 0.8, _Widen, r));
                float halo = _Halo * (1 - smoothstep(1.0, _Widen, r)) * (1 - core);
                float k = i.d.z > _FadeEnd ? saturate(1 - (i.d.z - _FadeEnd) / _FadeLen) : 1;
                float future = i.d.y > _Reveal ? _Future : 1;
                float3 c = i.color.rgb;
                float ca = core * _CoreAlpha * i.color.a;
                float3 rgb = c * ca + c * glow * (1 - core) + _HaloColor.rgb * halo * (1 - ca);
                float a = ca + halo * (1 - ca);
                return float4(rgb, a) * k * future;
            }
            ENDCG
        }
    }
}
