// Design 0.9 concept: a frosted, warm smoked glass panel (strip, plots, cards, tags). One quad with an analytic rounded
// rectangle: a tinted fill whose opacity rises from the rim (the room shows through the edge) to the text area, a soft
// vertical light gradient, a top sheen, fine grain, a lit rim that is brightest along the top left (light from above),
// and a faint light-only glow outside that lifts the panel off the room (it never darkens the room).
// UV0.xy: position on the panel in metres from its centre.
Shader "Concept/Glass"
{
    Properties
    {
        _Half ("Half size (m)", Vector) = (0.2, 0.1, 0, 0)
        _Radius ("Corner radius (m)", Float) = 0.028
        _Tint ("Tint", Color) = (0.110, 0.090, 0.078, 1)
        _AlphaCentre ("Alpha (text area)", Float) = 0.80
        _AlphaRim ("Alpha (rim)", Float) = 0.62
        _EdgeFade ("Rim-to-centre distance (m)", Float) = 0.035
        _RimColor ("Rim light", Color) = (1.0, 0.957, 0.902, 1)
        _RimWidth ("Rim width (m)", Float) = 0.0022
        _RimTop ("Rim brightness top", Float) = 0.55
        _RimBottom ("Rim brightness bottom", Float) = 0.16
        _Sheen ("Top sheen", Float) = 0.06
        _Grain ("Grain", Float) = 0.02
        _Glow ("Outer glow", Float) = 0.05
        _GlowWidth ("Outer glow width (m)", Float) = 0.012
        _Accent ("Focus accent", Color) = (1,1,1,1)
        _Focus ("Focus", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Half, _Tint, _RimColor, _Accent;
            float _Radius, _AlphaCentre, _AlphaRim, _EdgeFade, _RimWidth, _RimTop, _RimBottom, _Sheen, _Grain, _Glow, _GlowWidth, _Focus;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 p : TEXCOORD0; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.p = v.uv; return o; }
            float hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float4 frag (v2f i) : SV_Target
            {
                float2 p = i.p, h = _Half.xy;
                float2 q = abs(p) - (h - _Radius);
                float d = length(max(q, 0)) + min(max(q.x, q.y), 0) - _Radius;   // < 0 inside
                float aa = max(fwidth(d), 1e-6);
                float cover = saturate(0.5 - d / aa);
                // Outward direction of the nearest edge (for the directional rim light).
                float2 g = (q.x > 0 || q.y > 0) ? sign(p) * max(q, 0) / max(length(max(q, 0)), 1e-6) : (q.x > q.y ? float2(sign(p.x), 0) : float2(0, sign(p.y)));
                float lightSide = saturate(dot(g, normalize(float2(-0.45, 1.0))) * 0.5 + 0.5);
                float inner = max(-d, 0);
                float vy = saturate(p.y / h.y * 0.5 + 0.5);
                float a = lerp(_AlphaRim, _AlphaCentre, smoothstep(0, _EdgeFade, inner));
                float3 tint = _Tint.rgb * lerp(0.78, 1.30, vy);
                tint *= 1 + _Grain * (hash(floor(p * 5200)) - 0.5) * 2;
                float3 rgb = tint * a;
                rgb += _RimColor.rgb * _Sheen * smoothstep(0.62, 1.0, vy) * smoothstep(0.0, 0.01, inner);
                float rim = exp(-inner / _RimWidth) + 0.25 * exp(-inner / (_RimWidth * 5));
                float3 rimCol = lerp(_RimColor.rgb, _Accent.rgb, _Focus * 0.85);
                float rimI = lerp(_RimBottom, _RimTop, lightSide) * (1 + 1.4 * _Focus);
                rgb += rimCol * rim * rimI;
                a = saturate(a + rim * rimI * 0.25);
                float3 glow = lerp(_RimColor.rgb, _Accent.rgb, _Focus) * (_Glow * (1 + 2.5 * _Focus)) * exp(-max(d, 0) / _GlowWidth) * (1 - cover);
                return float4(rgb * cover + glow, a * cover);
            }
            ENDCG
        }
    }
}
