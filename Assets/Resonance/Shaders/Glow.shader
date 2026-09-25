// Additive textured emission (reconstructed image slabs): colour x texture luminance.
Shader "Resonance/Glow"
{
    Properties { _MainTex ("Texture", 2D) = "black" {} _Color ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend One One, Zero One
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _Color;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata i)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_OUTPUT(v2f, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(i.vertex); o.uv = i.uv; return o;
            }
            float4 frag (v2f i) : SV_Target
            {
                float3 c = _Color.rgb * tex2D(_MainTex, i.uv).r;
                return float4(c, 0); // light only: never covers the room (premultiplied eye layer)
            }
            ENDCG
        }
    }
}
