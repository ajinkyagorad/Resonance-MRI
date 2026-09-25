// Shared shading for the lit shaders (0.8.3 theme): a soft key light with wrap-around, a sky/ground ambient, a
// Blinn-Phong highlight and a cool rim, a clean studio look for a few ALU operations per pixel.
#ifndef RES_LIGHTING_INCLUDED
#define RES_LIGHTING_INCLUDED
float4 _ResLightDir;
static const float3 ResSky = float3(0.62, 0.68, 0.76);
static const float3 ResGround = float3(0.22, 0.21, 0.21);
static const float3 ResRimTint = float3(0.78, 0.86, 0.96);

// Diffuse terms only (ambient + wrapped key), for emissive surfaces that add their own light.
float ResDiffuse(float3 n)
{
    float3 L = normalize(_ResLightDir.xyz + float3(0, 1e-5, 0));
    return saturate((dot(n, L) + 0.35) / 1.35);
}

float3 ResAmbient(float3 n) { return lerp(ResGround, ResSky, n.y * 0.5 + 0.5); }

float ResSpecular(float3 n, float3 v, float gloss)
{
    float3 L = normalize(_ResLightDir.xyz + float3(0, 1e-5, 0));
    return pow(saturate(dot(n, normalize(L + v))), gloss);
}

float ResFresnel(float3 n, float3 v, float p) { return pow(1.0 - saturate(abs(dot(n, v))), p); }

// albedo under the key and ambient, plus a highlight (spec) and a rim (rim); n and v normalised, v toward the eye.
float3 ResShade(float3 albedo, float3 n, float3 v, float gloss, float spec, float rim)
{
    float3 lit = albedo * (0.55 * ResAmbient(n) + 0.72 * ResDiffuse(n));
    return lit + ResSpecular(n, v, gloss) * spec + ResFresnel(n, v, 3.0) * rim * ResRimTint;
}
#endif
