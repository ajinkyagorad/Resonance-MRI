// Lit meshes drawn with GPU instancing (needles, dots, arrows). Per-instance tint carries colour x brightness in rgb and
// opacity in a. Two materials use it (Mats.Needle): opaque for referenced spins, and see-through for the others, which
// fade by becoming transparent (never darker than the room behind them).
Shader "Resonance/Needle"
{
    Properties
    {
        _VertexTint ("Vertex tint", Float) = 0
        _Tint ("Tint", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendA ("Src alpha", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendA ("Dst alpha", Float) = 0
        _ZWrite ("ZWrite", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendA] [_DstBlendA]
            ZWrite [_ZWrite]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Tint)
            UNITY_INSTANCING_BUFFER_END(Props)
            #include "ResLighting.cginc"
            float _VertexTint;
            struct appdata { float4 colour : COLOR; float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 v : TEXCOORD1; float4 tint : COLOR; float along : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata i)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_OUTPUT(v2f, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(i.vertex); o.n = UnityObjectToWorldNormal(i.normal); o.v = WorldSpaceViewDir(i.vertex);
                o.tint = lerp(UNITY_ACCESS_INSTANCED_PROP(Props, _Tint),i.colour,_VertexTint);
                o.along = saturate(i.vertex.y + 0.5); // 0 at the tail, 1 at the tip: brightness shows direction
                return o;
            }
            float4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // 0.8.4: the tint is the proton's own colour (phase as hue, tip as brightness), so the lighting varies it only
                // gently; a darker silhouette edge keeps each needle distinct over a bright room.
                float3 n = normalize(i.n), v = normalize(i.v);
                float3 c = i.tint.rgb * (0.70 + 0.30 * ResDiffuse(n)) * (0.80 + 0.20 * i.along) + ResSpecular(n, v, 48) * 0.30 * i.tint.a;
                c *= 1.0 - 0.45 * ResFresnel(n, v, 2.5);
                return float4(saturate(c), i.tint.a);
            }
            ENDCG
        }
    }
}
