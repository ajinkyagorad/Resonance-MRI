// Validation only (editor): counts shaded fragments per pixel. Every fragment of every draw adds 1/32 to red.
Shader "Hidden/Resonance/Overdraw"
{
    SubShader
    {
        Pass
        {
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata i) { v2f o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_OUTPUT(v2f, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.pos = UnityObjectToClipPos(i.vertex); return o; }
            float4 frag (v2f i) : SV_Target { return float4(1.0 / 32.0, 0, 0, 0); }
            ENDCG
        }
    }
}
