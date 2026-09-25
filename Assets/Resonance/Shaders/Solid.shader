// Lit opaque single meshes (plinth, stands, knobs, bones, receiver blocks). Emission adds a highlight in _Glow colour.
Shader "Resonance/Solid"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) _Glow ("Glow", Color) = (0,0,0,0) }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "ResLighting.cginc"
            float4 _Color, _Glow;
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
                float rim = ResFresnel(n, v, 2.0);
                return float4(ResShade(_Color.rgb, n, v, 48, 0.22, 0.28) + _Glow.rgb * (0.35 + 0.65 * rim), 1);
            }
            ENDCG
        }
    }
}
