// The k-space relief (0.8.4, after 0.5.0's surface): an opaque, lit surface whose height is log |S| of each measured
// sample, coloured by height through the same navy-violet-white map as the images. UV.x = height share (0-1), UV.y = the
// sample's acquisition time: samples later than _Reveal are clipped, so the surface grows as the receiver measures.
Shader "Resonance/Relief"
{
    Properties { _Reveal ("Reveal up to time", Float) = 1000000 _Bright ("Brightness", Float) = 1 }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "ResLighting.cginc"
            float _Reveal, _Bright;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 v : TEXCOORD1; float2 uv : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata i)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_OUTPUT(v2f, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(i.vertex); o.n = UnityObjectToWorldNormal(i.normal); o.v = WorldSpaceViewDir(i.vertex); o.uv = i.uv;
                return o;
            }
            float3 Map(float v)
            {
                float3 c0 = float3(0.0110, 0.0180, 0.0600), c1 = float3(0.0452, 0.0168, 0.4564), c2 = float3(0.2623, 0.1070, 1.0),
                       c3 = float3(0.6724, 0.5520, 1.0), c4 = float3(1, 1, 1);
                if (v < 0.25) return lerp(c0, c1, v / 0.25);
                if (v < 0.55) return lerp(c1, c2, (v - 0.25) / 0.3);
                if (v < 0.8) return lerp(c2, c3, (v - 0.55) / 0.25);
                return lerp(c3, c4, (v - 0.8) / 0.2);
            }
            float4 frag (v2f i) : SV_Target
            {
                if (i.uv.y > _Reveal) discard;
                float3 n = normalize(i.n), v = normalize(i.v); if (dot(n, v) < 0) n = -n;
                float3 c = Map(saturate(i.uv.x)) * (0.62 + 0.38 * ResDiffuse(n)) * _Bright + ResSpecular(n, v, 40) * 0.18;
                return float4(saturate(c), 1);
            }
            ENDCG
        }
    }
}
