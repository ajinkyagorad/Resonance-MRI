// Design 0.9 concept: unlit textured or flat quads (images, colour chips, icons) with rounded corners (UV space) and
// premultiplied output. _Radius is in UV units of the shorter side; _Aspect = width / height of the quad.
Shader "Concept/Unlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Colour", Color) = (1,1,1,1)
        _Alpha ("Alpha", Float) = 1
        _Radius ("Corner radius (UV)", Float) = 0.06
        _Aspect ("Aspect", Float) = 1
        _Disc ("Draw a disc", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _Color; float _Alpha, _Radius, _Aspect, _Disc;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }
            float4 frag (v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * float2(_Aspect, 1);
                float2 h = float2(_Aspect, 1) * 0.5;
                float rad = _Disc > 0.5 ? 0.5 : _Radius;
                float2 q = abs(p) - (h - rad);
                float d = length(max(q, 0)) + min(max(q.x, q.y), 0) - rad;
                float cover = saturate(0.5 - d / max(fwidth(d), 1e-6));
                float4 c = tex2D(_MainTex, i.uv) * _Color * i.color;
                float a = c.a * _Alpha * cover;
                return float4(c.rgb * a, a);
            }
            ENDCG
        }
    }
}
