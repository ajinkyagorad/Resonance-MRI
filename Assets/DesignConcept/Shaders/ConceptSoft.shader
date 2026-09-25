// Design 0.9 concept: soft-lit smooth surfaces (porcelain, satin coils, pearl bones, needles, translucent skin).
// One light rig: a warm key from the upper left front with wrap-around (no hard terminator), a faint fill, a warm sky /
// soft floor hemisphere, a gentle highlight and a light rim. Premultiplied output: rgb * a, a (opaque when _Alpha = 1).
// _AlphaEdge raises the opacity toward grazing angles (glassy skin). _Band* light a soft band (the excited slab) in world
// space. _Emission adds the focus glow.
Shader "Concept/Soft"
{
    Properties
    {
        _Color ("Albedo", Color) = (1,1,1,1)
        _EmissionColor ("Emission colour", Color) = (1,1,1,1)
        _Emission ("Emission", Float) = 0
        _Alpha ("Alpha facing", Float) = 1
        _AlphaEdge ("Alpha grazing", Float) = 1
        _Gloss ("Gloss", Float) = 28
        _Spec ("Highlight", Float) = 0.22
        _Rim ("Rim", Float) = 0.30
        _Wrap ("Wrap", Float) = 0.65
        _Vertex ("Use vertex colour", Float) = 0
        _Band ("Band plane (world normal, offset)", Vector) = (0,0,1,0)
        _BandHalf ("Band half thickness (m)", Float) = 0
        _BandSoft ("Band softness (m)", Float) = 0.004
        _BandColor ("Band colour", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendA ("Src alpha", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendA ("Dst alpha", Float) = 0
        _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" "IgnoreProjector"="True" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendA] [_DstBlendA]
            ZWrite [_ZWrite]
            Cull [_Cull]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color, _EmissionColor, _Band, _BandColor;
            float _Emission, _Alpha, _AlphaEdge, _Gloss, _Spec, _Rim, _Wrap, _Vertex, _BandHalf, _BandSoft;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 wp : TEXCOORD1; float4 color : COLOR; };
            v2f vert (appdata v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.n = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz; o.color = v.color; return o;
            }
            float4 frag (v2f i, float face : VFACE) : SV_Target
            {
                float3 n = normalize(i.n) * (face > 0 ? 1 : -1);
                float3 v = normalize(_WorldSpaceCameraPos.xyz - i.wp);
                float3 albedo = _Color.rgb * (_Vertex > 0.5 ? i.color.rgb : float3(1, 1, 1));
                float3 key = normalize(float3(-0.42, 0.80, -0.44)), fill = normalize(float3(0.65, 0.15, -0.55));
                float dk = saturate((dot(n, key) + _Wrap) / (1 + _Wrap)); dk *= dk * (3 - 2 * dk);
                float df = saturate((dot(n, fill) + 0.5) / 1.5);
                float3 sky = float3(1.00, 0.96, 0.92), ground = float3(0.62, 0.55, 0.52);
                float3 amb = lerp(ground, sky, n.y * 0.5 + 0.5);
                float3 lit = albedo * (0.46 * amb + 0.62 * dk * float3(1.0, 0.97, 0.93) + 0.14 * df);
                float spec = pow(saturate(dot(n, normalize(key + v))), _Gloss) * _Spec;
                float fres = pow(1 - saturate(dot(n, v)), 3);
                // Highlights and the rim are light on the surface: added after the body's opacity (glass keeps its glints).
                float3 light = spec * float3(1.0, 0.97, 0.94) + fres * _Rim * float3(1.0, 0.96, 0.92);
                lit += _EmissionColor.rgb * _Emission;
                float a = lerp(_Alpha, _AlphaEdge, pow(1 - saturate(dot(n, v)), 2));
                if (_BandHalf > 0)
                {
                    float d = abs(dot(i.wp, _Band.xyz) - _Band.w);
                    float b = 1 - smoothstep(_BandHalf, _BandHalf + _BandSoft, d);
                    float halo = exp(-max(0, d - _BandHalf) / (_BandSoft * 2.5)) * 0.35;
                    lit = lerp(lit, _BandColor.rgb * 1.15, b * 0.85);
                    light += _BandColor.rgb * halo * 0.5;
                    a = max(a, b * 0.9);
                }
                return float4(lit * a + light, a);
            }
            ENDCG
        }
    }
}
