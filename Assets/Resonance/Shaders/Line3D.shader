// 3-D lines drawn as ribbons of constant width that always face the eye (0.8.3): the plots in the scene, the B0 field
// lines, axes, frames and outlines. Each segment is four vertices carrying the segment's direction; the vertex shader turns
// the ribbon towards the eye (per eye in single-pass stereo), so nothing is rebuilt when the view moves.
//   POSITION  the segment end (object space)          NORMAL     the segment direction (object space)
//   TEXCOORD0 x side (-1 or +1), y width (m at unit scale), z distance along the line (dashes), w reveal key
//   TEXCOORD1 field lines only: |B| deviation per unit current of the magnet, Gx, Gy and Gz (T/A x 1e4)
//   COLOR     colour
// _Reveal clips everything whose key is beyond it (signal and k-space revealed as samples arrive, no rebuild).
// _Casing (0.8.4): the outer share of each half-width is drawn in _CasingColor, a dark edge that keeps white and coloured
// lines readable over a bright room (opaque lines only; light-only lines cannot darken).
// _Mode 1: colour from the field deviation dB = dot(field, _Currents): s = 0.5 + 0.5 tanh(dB / _W), from a deep shade of the
// colour (weak field) through the colour to a near-white highlight (strong field), so a gradient reads as a ramp.
// _Dash and _Flow draw markers moving along the line.
Shader "Resonance/Line3D"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Reveal ("Reveal up to key", Float) = 1000000
        _Mode ("Mode", Float) = 0
        _Currents ("Scaled currents", Vector) = (0,0,0,0)
        _W ("Field width", Float) = 0.0006
        _Dash ("Dash period (m)", Float) = 0
        _Flow ("Dash speed (periods per s)", Float) = 0
        _Base ("Brightness between dashes", Float) = 1
        _ScaleWidth ("Width follows the object's scale", Float) = 1
        _Casing ("Casing share of the half-width", Float) = 0
        _CasingColor ("Casing colour", Color) = (0.024,0.04,0.094,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendA ("Src alpha", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendA ("Dst alpha", Float) = 0
        _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }
    SubShader
    {
        Tags { "Queue"="Geometry+20" "RenderType"="Opaque" "IgnoreProjector"="True" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendA] [_DstBlendA]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            float4 _Color, _Currents, _CasingColor; float _Reveal, _Mode, _W, _Dash, _Flow, _Base, _ScaleWidth, _Casing;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 uv : TEXCOORD0; float4 field : TEXCOORD1; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float3 d : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_OUTPUT(v2f, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 wd = mul((float3x3)unity_ObjectToWorld, v.normal);
                float scale = length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x));
                float3 side = cross(wd, _WorldSpaceCameraPos.xyz - wp);
                float len = length(side);
                side = len > 1e-12 ? side / len : float3(0, 1, 0);
                wp += side * (v.uv.x * 0.5 * v.uv.y * (_ScaleWidth > 0.5 ? scale : 1));
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1));
                float3 c = v.color.rgb * _Color.rgb;
                if (_Mode > 0.5)
                {
                    float s = 0.5 + 0.5 * tanh(dot(v.field, _Currents) / _W);
                    // Deep shade (weak) -> the colour (uniform field) -> near white (strong).
                    c = s < 0.5 ? c * lerp(0.28, 1.0, s * 2.0) : lerp(c, float3(1, 1, 1) * max(max(c.r, c.g), c.b), (s - 0.5) * 1.3);
                }
                o.color = float4(c, 1);
                o.d = float3(v.uv.z, v.uv.w, v.uv.x);
                return o;
            }
            float4 frag (v2f i) : SV_Target
            {
                if (i.d.y > _Reveal) discard;
                float3 c = i.color.rgb;
                if (_Dash > 0)
                {
                    float ph = frac(i.d.x / _Dash - _Time.y * _Flow);
                    c *= lerp(_Base, 1.0, smoothstep(0.0, 0.12, ph) * (1.0 - smoothstep(0.3, 0.45, ph)));
                }
                if (_Casing > 0) c = lerp(c, _CasingColor.rgb, smoothstep(1.0 - _Casing - 0.08, 1.0 - _Casing + 0.02, abs(i.d.z)));
                return float4(c, 1); // opaque lines write alpha 1; additive lines (Zero One) leave alpha untouched
            }
            ENDCG
        }
    }
}
