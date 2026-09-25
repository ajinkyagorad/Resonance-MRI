// Translucent lit surfaces (skin, formers, cradle) with a fresnel edge.
Shader "Resonance/Glass"
{
    Properties { _Color ("Color", Color) = (1,1,1,0.2) _Edge ("Edge", Float) = 0.5 }
    SubShader
    {
        Tags { "Queue"="Transparent+2" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "ResLighting.cginc"
            float4 _Color; float _Edge;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 v : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata i)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_OUTPUT(v2f, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(i.vertex); o.n = UnityObjectToWorldNormal(i.normal); o.v = WorldSpaceViewDir(i.vertex);
                return o;
            }
            float4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.n), v = normalize(i.v);
                float fres = ResFresnel(n, v, 2.0);
                float a = saturate(_Color.a * (1.0 + _Edge * 2.0 * fres));
                return float4(ResShade(_Color.rgb, n, v, 40, 0.18, 0.0), a);
            }
            ENDCG
        }
    }
}
