using Nebulytic.Resonance.Sim;
using UnityEngine;

namespace Nebulytic.Resonance
{
    /// <summary>Class-sampled tissue crop and selected slab in exactly the block's physical frame.
    /// Fine fibre texture is illustrative; anatomical class boundaries come from the same specimen as the spins.</summary>
    public sealed class TissueDetail
    {
        readonly App app; readonly Transform volume, slab;
        Transform fibres; readonly Transform parent; readonly Material material;
        Texture3D texture;
        const int N = 32, NZ = 64;
        public TissueDetail(App app, Transform parent)
        {
            this.app = app; this.parent = parent;
            material = Mats.Volume(Color.white, null, 3, 3, 24);
            volume = Mats.Object("Sampled tissue volume", parent, MeshKit.Box(Vector3.one), material, Vector3.zero, null, CubeView.DisplaySize).transform;
            var c = Look.GZ; c.a = 0.14f;
            slab = Mats.Object("Selected tissue slab", parent, MeshKit.Box(Vector3.one), Mats.Glass(c, 0.5f)).transform;
            slab.gameObject.SetActive(false);
        }
        public void Rebuild(SimState s)
        {
            if (fibres)
            {
                foreach (var r in fibres.GetComponentsInChildren<MeshRenderer>()) { Object.Destroy(r.sharedMaterial); Object.Destroy(r.GetComponent<MeshFilter>().sharedMesh); }
                Object.Destroy(fibres.gameObject);
            }
            fibres = new GameObject("Tissue fibre bundles (illustrative)").transform; fibres.SetParent(parent, false);
            var sets = new System.Collections.Generic.Dictionary<TissueClass, System.Collections.Generic.List<CombineInstance>>();
            Vector3 size = CubeView.DisplaySize;
            for (int iz = 0; iz < 8; iz++) for (int ix = 0; ix < 7; ix++)
            {
                float x0 = size.x * ((ix + 0.5f) / 7 - 0.5f), z0 = size.z * ((iz + 0.5f) / 8 - 0.5f);
                var points = new System.Collections.Generic.List<Vector3>();
                var physical = s.CubeCentre + Frames.ToP(new Vector3(x0, 0, z0) / Look.BlockMagnification);
                var kind = app.Specimen.ClassAt(physical); if (kind == TissueClass.Air) continue;
                for (int k = 0; k <= 12; k++)
                {
                    float y = size.y * (k / 12f - 0.5f) * 0.91f;
                    points.Add(new Vector3(x0 + 0.006f * Mathf.Sin(18 * y + iz), y, z0 + 0.009f * Mathf.Sin(12 * y + ix)));
                }
                if (!sets.TryGetValue(kind, out var list)) sets[kind] = list = new System.Collections.Generic.List<CombineInstance>();
                list.Add(new CombineInstance { mesh = MeshKit.Tube(points, 0.008f, 6), transform = Matrix4x4.identity });
            }
            foreach (var pair in sets)
            {
                var mesh = new Mesh { name = "Tissue fibres" }; mesh.CombineMeshes(pair.Value.ToArray(), true, true);
                foreach (var part in pair.Value) Object.Destroy(part.mesh);
                Color c = pair.Key == TissueClass.Fat || pair.Key == TissueClass.Tendon ? new Color(0.96f, 0.78f, 0.53f, 0.30f) : new Color(0.86f, 0.40f, 0.37f, 0.23f);
                Mats.Object(pair.Key + " fibre illustration", fibres, mesh, Mats.Glass(c, 0.35f));
            }
            var data = new Color32[N * N * NZ];
            for (int z = 0; z < NZ; z++) for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                var u = new Vector3((x + 0.5f) / N - 0.5f, (y + 0.5f) / N - 0.5f, (z + 0.5f) / NZ - 0.5f);
                var physical = s.CubeCentre + Frames.ToP(Vector3.Scale(u, CubeView.DisplaySize) / Look.BlockMagnification);
                var kind = app.Specimen.ClassAt(physical);
                Color c = kind switch {
                    TissueClass.Muscle => new Color(0.78f, 0.30f, 0.29f),
                    TissueClass.Fat => new Color(0.95f, 0.77f, 0.49f),
                    TissueClass.Marrow => new Color(0.73f, 0.39f, 0.31f),
                    TissueClass.Cortex => new Color(0.96f, 0.92f, 0.83f),
                    TissueClass.Tendon => new Color(0.84f, 0.81f, 0.71f),
                    TissueClass.Skin => new Color(0.85f, 0.60f, 0.48f),
                    _ => new Color(0.76f, 0.43f, 0.39f) };
                // Object-space fibre detail, stable under head and volume movement.
                float fibre = 0.5f + 0.5f * Mathf.Sin(95 * u.x + 4 * Mathf.Sin(9 * u.z) + 18 * u.y);
                c *= 0.78f + 0.22f * fibre;
                var q = new Vector3(Mathf.Abs(u.x), Mathf.Abs(u.y), Mathf.Abs(u.z)) - Vector3.one * 0.41f;
                float rounded = Vector3.Max(q, Vector3.zero).magnitude;
                c.a = kind == TissueClass.Air ? 0 : (0.22f + 0.6f * fibre * fibre) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.06f, 0.09f, rounded)));
                data[(z * N + y) * N + x] = c;
            }
            if (texture) Object.Destroy(texture);
            texture = new Texture3D(N, N, NZ, TextureFormat.RGBA32, false) { name = "Sampled tissue classes with illustrative fibres", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(data); texture.Apply(false, true); material.SetTexture("_Vol", texture);
        }
        public void Tick(SimState s, Simulation sim)
        {
            volume.gameObject.SetActive((app.ShowTissue && !app.Demonstrating)); if (fibres) fibres.gameObject.SetActive((app.ShowTissue && !app.Demonstrating));
            int bi = sim.BlockIndex; bool on = bi >= 0 && s.Program.Blocks[bi].Selective;
            float z = (float)((bi >= 0 && s.Program.Blocks[bi].Selective ? s.Program.SliceZ[s.Program.Blocks[bi].Slice] : s.P.SliceZ) - s.CubeCentre.Z) * Look.BlockMagnification;
            float dz = (float)s.P.SliceThickness * Look.BlockMagnification;
            float half = CubeView.DisplaySize.z * 0.5f;
            float lo = Mathf.Max(-half, z - dz * 0.5f), hi = Mathf.Min(half, z + dz * 0.5f);
            on &= hi > lo; if (slab.gameObject.activeSelf != on) slab.gameObject.SetActive(on);
            if (on) { slab.localPosition = new Vector3(0, 0, (lo + hi) * 0.5f); slab.localScale = new Vector3(CubeView.DisplaySize.x * 1.04f, CubeView.DisplaySize.y * 1.04f, hi - lo); }
        }
    }
}
