using UnityEngine;
using UnityEngine.Rendering;

namespace Nebulytic.Resonance
{
    /// <summary>Material factory for the shaders; also sets the global light direction.</summary>
    public static class Mats
    {
        static Shader Find(string name)
        {
            var s = Shader.Find(name);
            if (s == null) throw new System.Exception("Shader missing: " + name);
            return s;
        }

        public static void Init()
        {
            Shader.SetGlobalVector("_ResLightDir", new Vector4(-0.35f, 0.85f, -0.4f, 0).normalized);
        }

        /// <summary>Conductor material: solid (opaque lit emissive tube) or light (additive, never writes alpha).</summary>
        public static Material Conductor(Color c, bool solid)
        {
            var m = new Material(Find("Resonance/Conductor")); m.SetColor("_Color", c);
            if (solid) { Blend(m, BlendMode.One, BlendMode.OneMinusSrcAlpha, BlendMode.One, BlendMode.OneMinusSrcAlpha, false); m.SetFloat("_Solid", 1); m.renderQueue = 3001; }
            else { Blend(m, BlendMode.One, BlendMode.One, BlendMode.Zero, BlendMode.One, false); m.SetFloat("_Solid", 0); m.renderQueue = 3000; }
            return m;
        }

        static void Blend(Material m, BlendMode src, BlendMode dst, BlendMode srcA, BlendMode dstA, bool zwrite)
        {
            m.SetFloat("_SrcBlend", (float)src); m.SetFloat("_DstBlend", (float)dst); m.SetFloat("_SrcBlendA", (float)srcA); m.SetFloat("_DstBlendA", (float)dstA);
            m.SetFloat("_ZWrite", zwrite ? 1 : 0);
        }
        /// <summary>A volume. smooth: a smooth field integrated along each pixel's chord with two samples (no raymarch);
        /// otherwise a raymarch of the given steps (fields with thin structure).</summary>
        public static Material Volume(Color c, Texture tex, float gain, int mode, float steps, bool smooth = false)
        {
            var m = new Material(Find("Resonance/Volume")); m.SetColor("_Color", c); m.SetTexture("_Vol", tex); m.SetFloat("_Gain", gain); m.SetFloat("_Mode", mode); m.SetFloat("_Steps", steps); m.SetFloat("_Quad", smooth ? 1 : 0);
            // Emission modes add light and never write alpha; the matter modes (3 tissue, 4 scalar) composite premultiplied.
            if (mode >= 3) { Blend(m, BlendMode.One, BlendMode.OneMinusSrcAlpha, BlendMode.One, BlendMode.OneMinusSrcAlpha, false); m.renderQueue = 2998; }
            else Blend(m, BlendMode.One, BlendMode.One, BlendMode.Zero, BlendMode.One, false);
            return m;
        }
        /// <summary>Instanced lit meshes: opaque, or see-through (premultiplied over the room; faded spins).</summary>
        public static Material Needle(bool seeThrough = false)
        {
            var m = new Material(Find("Resonance/Needle")); m.enableInstancing = true;
            if (seeThrough) { Blend(m, BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, BlendMode.One, BlendMode.OneMinusSrcAlpha, false); m.renderQueue = 3002; }
            else { Blend(m, BlendMode.One, BlendMode.Zero, BlendMode.One, BlendMode.Zero, true); m.renderQueue = 2000; }
            return m;
        }
        /// <summary>
        /// 3-D lines (Line3D): opaque (plots, axes, frames, outlines) or light only (never covers the room). casing: the share
        /// of each half-width drawn as a dark edge (0.8.4), so white and coloured lines read over a bright room. overAll: drawn
        /// over everything (the block's outline inside the hand).
        /// </summary>
        public static Material Line(Color c, bool light = false, float casing = 0, bool overAll = false)
        {
            var m = new Material(Find("Resonance/Line3D")); m.SetColor("_Color", c);
            if (light) { Blend(m, BlendMode.One, BlendMode.One, BlendMode.Zero, BlendMode.One, false); m.renderQueue = 3005; }
            else { Blend(m, BlendMode.One, BlendMode.Zero, BlendMode.One, BlendMode.Zero, true); m.renderQueue = 2010; }
            if (casing > 0 && !light) { m.SetFloat("_Casing", casing); m.SetColor("_CasingColor", Look.INK); }
            if (overAll) { m.SetFloat("_ZTest", (float)CompareFunction.Always); m.SetFloat("_ZWrite", 0); m.renderQueue = 3060; }
            return m;
        }
        /// <summary>A reconstructed image through the violet-to-white map (0.8.4): opaque square, or transparent where dark.</summary>
        public static Material Image(Texture tex, bool opaque)
        {
            var m = new Material(Find("Resonance/Image")); if (tex) m.SetTexture("_MainTex", tex); m.SetFloat("_Opaque", opaque ? 1 : 0);
            m.renderQueue = opaque ? 3001 : 3003; return m;
        }
        /// <summary>An opaque, unlit backing plate (0.8.4: plots, close-up, strip).</summary>
        public static Material Plate(Color c) { var m = new Material(Find("Resonance/Plate")); m.SetColor("_Color", c); return m; }
        /// <summary>The k-space relief surface (0.8.4).</summary>
        public static Material Relief() => new Material(Find("Resonance/Relief"));
        /// <summary>A dark rounded backing plate with a rim, facing -Z, its face at local z = zFace (behind content at z = 0).</summary>
        public static void Backing(Transform parent, float w, float h, Vector3 centre, float zFace = 0.012f)
        {
            float r = Mathf.Min(0.022f, 0.12f * Mathf.Min(w, h));
            Object("Backing", parent, MeshKit.RoundedPanel(w, h, r, 0.006f, 8), Plate(Look.PANEL), centre + new Vector3(0, 0, zFace));
            Object("Backing rim", parent, MeshKit.RoundedPanel(w + 0.006f, h + 0.006f, r + 0.003f, 0.004f, 8), Plate(Look.PANEL_EDGE), centre + new Vector3(0, 0, zFace + 0.003f));
        }
        public static Material Solid(Color c) { var m = new Material(Find("Resonance/Solid")); m.SetColor("_Color", c); m.SetColor("_Glow", Color.black); return m; }
        public static Material Glass(Color c, float edge = 0.5f) { var m = new Material(Find("Resonance/Glass")); m.SetColor("_Color", c); m.SetFloat("_Edge", edge); return m; }
        public static Material Flat(Color c, Texture tex = null) { var m = new Material(Find("Resonance/Flat")); m.SetColor("_Color", c); if (tex) m.SetTexture("_MainTex", tex); return m; }
        public static Material Glow(Color c, Texture tex) { var m = new Material(Find("Resonance/Glow")); m.SetColor("_Color", c); m.SetTexture("_MainTex", tex); return m; }

        public static GameObject Object(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos = default, Quaternion? rot = null, Vector3? scale = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localRotation = rot ?? Quaternion.identity; go.transform.localScale = scale ?? Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            return go;
        }
    }
}
