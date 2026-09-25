// Solid backing plates (0.8.4): the plots, the close-up and the strip sit on an opaque, unlit dark panel with a rim, so
// thin lines and text read over a bright room. Opaque and depth-writing, so nothing behind a plate shows through it.
Shader "Resonance/Plate"
{
    Properties { _Color ("Color", Color) = (0.04,0.063,0.14,1) }
    SubShader
    {
        Tags { "Queue"="Geometry-5" "RenderType"="Opaque" }
        Pass
        {
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            float4 _Color;
            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata i)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_OUTPUT(v2f, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(i.vertex); return o;
            }
            float4 frag (v2f i) : SV_Target { return float4(_Color.rgb, 1); }
            ENDCG
        }
    }
}
