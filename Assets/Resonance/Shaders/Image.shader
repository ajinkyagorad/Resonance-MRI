// Reconstructed MRI images (0.8.4): the intensity (R8 texture) through a violet-to-white colour map instead of grey, over a
// dark navy base. Premultiplied output (Blend One OneMinusSrcAlpha): _Opaque 1 draws the whole square (the image on a
// backing); _Opaque 0 makes dark pixels transparent and tissue opaque, so the image slabs in the bore and the 3-D stack
// read over a bright room without covering it with a dark square. _Gain scales the intensity before the map.
Shader "Resonance/Image"
{
    Properties { _MainTex ("Intensity", 2D) = "black" {} _Gain ("Gain", Float) = 1 _Opaque ("Opaque", Float) = 1 _Fade ("Fade", Float) = 1 }
    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex; float _Gain, _Opaque, _Fade;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata i)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_OUTPUT(v2f, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(i.vertex); o.uv = i.uv; return o;
            }
            // Navy -> indigo -> violet -> lavender -> white (colour stops in sRGB, converted to linear by the pipeline's
            // linear colour space: the stops below are already linear approximations).
            float3 Map(float v)
            {
                float3 c0 = float3(0.0030, 0.0052, 0.0176), c1 = float3(0.0452, 0.0168, 0.4564), c2 = float3(0.2623, 0.1070, 1.0),
                       c3 = float3(0.6724, 0.5520, 1.0), c4 = float3(1, 1, 1);
                if (v < 0.3) return lerp(c0, c1, v / 0.3);
                if (v < 0.6) return lerp(c1, c2, (v - 0.3) / 0.3);
                if (v < 0.85) return lerp(c2, c3, (v - 0.6) / 0.25);
                return lerp(c3, c4, (v - 0.85) / 0.15);
            }
            float4 frag (v2f i) : SV_Target
            {
                float v = saturate(tex2D(_MainTex, i.uv).r * _Gain);
                float a = (_Opaque > 0.5 ? 1.0 : smoothstep(0.04, 0.22, v)) * _Fade;
                return float4(Map(v) * a, a);
            }
            ENDCG
        }
    }
}
