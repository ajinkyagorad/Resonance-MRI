// Coil conductors. Brightness = body (floor + emphasis + current) + moving dashes whose direction is the conventional
// current direction along the path (UV.x = arc length).
// Two render modes, chosen per material (Mats.Conductor):
//  - solid (thin tubes: gradient coils, birdcage): opaque, lit and emissive, crisp over any passthrough room;
//  - light (magnet packs, large surfaces): additive light that never writes alpha, so the room behind stays at full
//    brightness (Quest composites the eye buffer as premultiplied: out = rgb + room * (1 - a)).
Shader "Resonance/Conductor"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _Opacity ("Context opacity", Range(0,1)) = 1
        _Body ("Body", Float) = 0.15
        _Flow ("Flow", Float) = 0
        _Offset ("Dash offset", Float) = 0
        _FlowSpeed ("Dash speed (arc length per second)", Float) = 0
        _DashLen ("Dash length", Float) = 0.05
        _Rim ("Rim", Float) = 0
        _Turns ("Turn pitch (UV.y units, 0 = none)", Float) = 0
        _Solid ("Solid (1) or light (0)", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendA ("Src alpha", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendA ("Dst alpha", Float) = 1
        _ZWrite ("ZWrite", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendA] [_DstBlendA]
            ZWrite [_ZWrite]
            Cull [_Cull]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "ResLighting.cginc"
            float4 _Color; float _Opacity, _Body, _Flow, _Offset, _FlowSpeed, _DashLen, _Rim, _Turns, _Solid;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float3 v : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata i)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_OUTPUT(v2f, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(i.vertex); o.uv = i.uv;
                o.n = UnityObjectToWorldNormal(i.normal); o.v = WorldSpaceViewDir(i.vertex);
                return o;
            }
            float4 frag (v2f i) : SV_Target
            {
                float ph = frac((i.uv.x - _Offset - _Time.y * _FlowSpeed) / _DashLen); // dashes move in the shader: no per-frame updates
                float dash = smoothstep(0.0, 0.08, ph) * (1.0 - smoothstep(0.30, 0.40, ph));
                float3 n = normalize(i.n), v = normalize(i.v);
                float fres = pow(1.0 - saturate(abs(dot(n, v))), 2.0);
                float turns = _Turns > 0 ? 0.65 + 0.35 * smoothstep(0.15, 0.35, abs(frac(i.uv.y / _Turns) - 0.5)) : 1.0;
                float e = (_Body + _Flow * dash) * turns + _Rim * fres;
                if (i.uv.y < -0.5) e *= 0.68; // the magnet's cut faces (UV.y = -1) read as sections (0.8.4)
                if (_Solid > 0.5)
                {
                    // Lit tube: shading keeps it three-dimensional; emission keeps its colour readable at any room brightness;
                    // a highlight and a cool rim give it a clean, modern finish.
                    float3 c = _Color.rgb * e * (0.62 + 0.38 * ResDiffuse(n)) + ResSpecular(n, v, 56) * 0.28 + fres * 0.10 * ResRimTint;
                    return float4(saturate(c) * _Opacity, _Opacity);
                }
                // Light-only shell (magnet packs): faint faces, brighter silhouettes, like tinted glass lit from the edge.
                return float4(_Color.rgb * (e * 0.45 + fres * (0.20 + 0.8 * _Rim)), 0);
            }
            ENDCG
        }
    }
}
