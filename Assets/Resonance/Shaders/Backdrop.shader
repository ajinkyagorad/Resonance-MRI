// The proton block's far walls (0.8.4): the box is built facing inward, so only the walls behind the protons are drawn,
// whatever the block's orientation. Opaque dark navy with a faint millimetre grid (_Grid = grid period in object units),
// so the coloured protons stand out over a bright room and the grid gives the scale. While a gradient runs, the walls glow in
// the gradient's colour, brighter toward the stronger field (_Ramp: object-space direction over the block's length, _RampColor).
Shader "Resonance/Backdrop"
{
    Properties { _Color ("Color", Color) = (0.04,0.063,0.14,1) _Line ("Grid colour", Color) = (0.13,0.2,0.4,1) _Grid ("Grid period", Vector) = (0.2,0.2,0.1,0) _Ramp ("Ramp direction / length", Vector) = (0,0,0,0) _RampColor ("Ramp colour", Color) = (0,0,0,1) }
    SubShader
    {
        Tags { "Queue"="Geometry-10" "RenderType"="Opaque" }
        Pass
        {
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            float4 _Color, _Line, _Grid, _Ramp, _RampColor;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 obj : TEXCOORD0; float3 n : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata i)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_OUTPUT(v2f, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(i.vertex); o.obj = i.vertex.xyz; o.n = abs(i.normal);
                return o;
            }
            float4 frag (v2f i) : SV_Target
            {
                // Grid lines on the two axes lying in this wall.
                float3 g = abs(frac(i.obj / _Grid.xyz + 0.5) - 0.5) / fwidth(i.obj / _Grid.xyz);
                float3 lines = 1.0 - saturate(g - 0.5);
                float l = max(max(lines.x * (1 - i.n.x), lines.y * (1 - i.n.y)), lines.z * (1 - i.n.z));
                float3 c = _Color.rgb;
                if (dot(_Ramp.xyz, _Ramp.xyz) > 0) { float u = saturate(0.5 + dot(i.obj, _Ramp.xyz)); c = lerp(c, _RampColor.rgb * 0.7, 0.3 * u * u); }
                return float4(lerp(c, _Line.rgb, l), 1);
            }
            ENDCG
        }
    }
}
