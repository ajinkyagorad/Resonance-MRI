using System.Collections.Generic;
using UnityEngine;

namespace Nebulytic.Resonance
{
    /// <summary>Procedural meshes. Every mesh in the app except the anatomy and the controllers is generated here.</summary>
    public static class MeshKit
    {
        static Mesh Build(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            // Unity front faces are clockwise as seen from the front, i.e. cross(v1 - v0, v2 - v0) points toward the viewer.
            // Orient every triangle to agree with the outward vertex normals supplied by the generator.
            for (int i = 0; i + 2 < t.Count; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                Vector3 face = Vector3.Cross(b - a, c - a), avg = n[t[i]] + n[t[i + 1]] + n[t[i + 2]];
                if (Vector3.Dot(face, avg) < 0) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
            }
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v); m.SetNormals(n); if (uv != null) m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Tube of the given radius along a polyline. UV.x = arc length (same units as the points), UV.y = angle/2pi.</summary>
        public static Mesh Tube(IList<Vector3> pts, float radius, int sides = 8, string name = "Tube")
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            float arc = 0; Vector3 prevNormal = Vector3.zero;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 dir = i == 0 ? pts[1] - pts[0] : i == pts.Count - 1 ? pts[i] - pts[i - 1] : (pts[i + 1] - pts[i]).normalized + (pts[i] - pts[i - 1]).normalized;
                dir.Normalize();
                if (i > 0) arc += Vector3.Distance(pts[i], pts[i - 1]);
                Vector3 nrm = i == 0 ? Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized : Vector3.ProjectOnPlane(prevNormal, dir).normalized;
                if (nrm.sqrMagnitude < 1e-6f) nrm = Vector3.Cross(dir, Vector3.up).normalized;
                prevNormal = nrm; Vector3 bin = Vector3.Cross(dir, nrm);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s * 2 * Mathf.PI / sides; Vector3 o = nrm * Mathf.Cos(a) + bin * Mathf.Sin(a);
                    v.Add(pts[i] + o * radius); n.Add(o); uv.Add(new Vector2(arc, s / (float)sides));
                }
                if (i > 0)
                {
                    int b0 = (i - 1) * (sides + 1), b1 = i * (sides + 1);
                    for (int s = 0; s < sides; s++) { t.Add(b0 + s); t.Add(b1 + s); t.Add(b0 + s + 1); t.Add(b0 + s + 1); t.Add(b1 + s); t.Add(b1 + s + 1); }
                }
            }
            return Build(name, v, n, uv, t);
        }

        /// <summary>
        /// Annular winding pack about the z axis (rIn..rOut, z0..z1) in S coordinates. UV.x = arc length along the winding
        /// direction (+phi in P, which is -phi seen in S), UV.y = z.
        /// </summary>
        /// <summary>Winding pack: outer cylinder, end caps and (optionally) the inner cylinder.</summary>
        public static Mesh Pack(float rIn, float rOut, float z0, float z1, int segments = 96, bool inner = true)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            void Ring(float r, float za, float zb, bool outward)
            {
                int b = v.Count;
                for (int s = 0; s <= segments; s++)
                {
                    float phi = s * 2 * Mathf.PI / segments; // physical azimuth; S x = -cos
                    Vector3 radial = new Vector3(-Mathf.Cos(phi), Mathf.Sin(phi), 0);
                    v.Add(radial * r + new Vector3(0, 0, za)); v.Add(radial * r + new Vector3(0, 0, zb));
                    Vector3 nn = outward ? radial : -radial; n.Add(nn); n.Add(nn);
                    uv.Add(new Vector2(phi * r, za)); uv.Add(new Vector2(phi * r, zb));
                }
                for (int s = 0; s < segments; s++)
                {
                    int a = b + 2 * s;
                    if (outward) { t.Add(a); t.Add(a + 2); t.Add(a + 1); t.Add(a + 1); t.Add(a + 2); t.Add(a + 3); }
                    else { t.Add(a); t.Add(a + 1); t.Add(a + 2); t.Add(a + 1); t.Add(a + 3); t.Add(a + 2); }
                }
            }
            void Cap(float z, bool plusZ)
            {
                int b = v.Count;
                for (int s = 0; s <= segments; s++)
                {
                    float phi = s * 2 * Mathf.PI / segments; Vector3 radial = new Vector3(-Mathf.Cos(phi), Mathf.Sin(phi), 0);
                    v.Add(radial * rIn + new Vector3(0, 0, z)); v.Add(radial * rOut + new Vector3(0, 0, z));
                    Vector3 nn = plusZ ? Vector3.forward : Vector3.back; n.Add(nn); n.Add(nn);
                    uv.Add(new Vector2(phi * rIn, z)); uv.Add(new Vector2(phi * rOut, z));
                }
                for (int s = 0; s < segments; s++)
                {
                    int a = b + 2 * s;
                    if (plusZ) { t.Add(a); t.Add(a + 1); t.Add(a + 2); t.Add(a + 1); t.Add(a + 3); t.Add(a + 2); }
                    else { t.Add(a); t.Add(a + 2); t.Add(a + 1); t.Add(a + 1); t.Add(a + 2); t.Add(a + 3); }
                }
            }
            Ring(rOut, z0, z1, true); if (inner) Ring(rIn, z0, z1, false); Cap(z1, true); Cap(z0, false);
            return Build("Pack", v, n, uv, t);
        }

        /// <summary>Needle along +Y, pivot at the centre, unit length: shaft cylinder then cone head (26 triangles at 6/8 sides).</summary>
        public static Mesh Needle(float shaftRadius = 0.05f, float headRadius = 0.13f, float headStart = 0.15f, int shaftSegments = 6, int headSegments = 8)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            int s1 = shaftSegments, s2 = headSegments;
            for (int s = 0; s < s1; s++)
            {
                float a0 = s * 2 * Mathf.PI / s1, a1 = (s + 1) * 2 * Mathf.PI / s1;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                int b = v.Count;
                v.Add(d0 * shaftRadius + Vector3.up * -0.5f); v.Add(d1 * shaftRadius + Vector3.up * -0.5f); v.Add(d0 * shaftRadius + Vector3.up * headStart); v.Add(d1 * shaftRadius + Vector3.up * headStart);
                n.Add(d0); n.Add(d1); n.Add(d0); n.Add(d1);
                t.Add(b); t.Add(b + 2); t.Add(b + 1); t.Add(b + 1); t.Add(b + 2); t.Add(b + 3);
            }
            float slope = headRadius / (0.5f - headStart);
            for (int s = 0; s < s2; s++)
            {
                float a0 = s * 2 * Mathf.PI / s2, a1 = (s + 1) * 2 * Mathf.PI / s2, am = 0.5f * (a0 + a1);
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)), dm = new Vector3(Mathf.Cos(am), 0, Mathf.Sin(am));
                int b = v.Count;
                v.Add(d0 * headRadius + Vector3.up * headStart); v.Add(d1 * headRadius + Vector3.up * headStart); v.Add(Vector3.up * 0.5f);
                n.Add((d0 + Vector3.up * slope).normalized); n.Add((d1 + Vector3.up * slope).normalized); n.Add((dm + Vector3.up * slope).normalized);
                t.Add(b); t.Add(b + 2); t.Add(b + 1);
                int c = v.Count; // head base
                v.Add(d0 * headRadius + Vector3.up * headStart); v.Add(d1 * headRadius + Vector3.up * headStart); v.Add(Vector3.up * headStart);
                n.Add(Vector3.down); n.Add(Vector3.down); n.Add(Vector3.down);
                t.Add(c); t.Add(c + 1); t.Add(c + 2);
            }
            return Build("Needle", v, n, null, t);
        }

        public static Mesh Octahedron(float r = 0.5f)
        {
            var dirs = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            int[,] faces = { { 2, 0, 4 }, { 2, 4, 1 }, { 2, 1, 5 }, { 2, 5, 0 }, { 3, 4, 0 }, { 3, 1, 4 }, { 3, 5, 1 }, { 3, 0, 5 } };
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            for (int f = 0; f < 8; f++)
            {
                Vector3 a = dirs[faces[f, 0]] * r, b = dirs[faces[f, 1]] * r, c = dirs[faces[f, 2]] * r, nn = Vector3.Cross(b - a, c - a).normalized;
                int k = v.Count; v.Add(a); v.Add(b); v.Add(c); n.Add(nn); n.Add(nn); n.Add(nn); t.Add(k); t.Add(k + 1); t.Add(k + 2);
            }
            return Build("Dot", v, n, null, t);
        }

        public static Mesh Box(Vector3 size) => BoxAt(Vector3.zero, size);

        static void AddBox(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 c, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var nn in normals)
            {
                Vector3 a = new Vector3(nn.y, nn.z, nn.x), b = Vector3.Cross(nn, a);
                int k = v.Count;
                Vector3 center = c + Vector3.Scale(nn, h);
                Vector3 ha = Vector3.Scale(a, h), hb = Vector3.Scale(b, h);
                v.Add(center - ha - hb); v.Add(center + ha - hb); v.Add(center + ha + hb); v.Add(center - ha + hb);
                for (int i = 0; i < 4; i++) n.Add(nn);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                t.Add(k); t.Add(k + 2); t.Add(k + 1); t.Add(k); t.Add(k + 3); t.Add(k + 2);
            }
        }

        public static Mesh BoxAt(Vector3 centre, Vector3 size)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            AddBox(v, n, uv, t, centre, size);
            return Build("Box", v, n, uv, t);
        }

        /// <summary>
        /// A box whose faces point inward (0.8.4): drawn with back-face culling, only the walls behind its contents are seen,
        /// from any direction (the proton block's backdrop).
        /// </summary>
        public static Mesh InwardBox(Vector3 size)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            AddBox(v, n, uv, t, Vector3.zero, size);
            for (int i = 0; i < n.Count; i++) n[i] = -n[i];
            return Build("Inward box", v, n, uv, t);
        }

        /// <summary>
        /// Winding pack about the z axis (rIn..rOut, z0..z1) in S coordinates with a cutaway (0.8.4): the physical azimuths
        /// from cutFrom to cutTo (degrees, P frame) are left open and closed by two radial faces, so the coils inside show.
        /// UV.x = arc length along the winding direction, UV.y = z; the cut faces carry UV.y = -1 (the shader shades them).
        /// </summary>
        public static Mesh PackCut(float rIn, float rOut, float z0, float z1, float cutFrom, float cutTo, int segments = 72)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            float a0 = cutTo * Mathf.Deg2Rad, span = (360 - (cutTo - cutFrom)) * Mathf.Deg2Rad;
            Vector3 Radial(float phi) => new Vector3(-Mathf.Cos(phi), Mathf.Sin(phi), 0); // physical azimuth; S x = -cos
            void Ring(float r, bool outward)
            {
                int b = v.Count;
                for (int s = 0; s <= segments; s++)
                {
                    float phi = a0 + span * s / segments; Vector3 rad = Radial(phi);
                    v.Add(rad * r + new Vector3(0, 0, z0)); v.Add(rad * r + new Vector3(0, 0, z1));
                    Vector3 nn = outward ? rad : -rad; n.Add(nn); n.Add(nn); uv.Add(new Vector2(phi * r, z0)); uv.Add(new Vector2(phi * r, z1));
                }
                for (int s = 0; s < segments; s++) { int a = b + 2 * s; t.Add(a); t.Add(a + 2); t.Add(a + 1); t.Add(a + 1); t.Add(a + 2); t.Add(a + 3); }
            }
            void Cap(float z, Vector3 nn)
            {
                int b = v.Count;
                for (int s = 0; s <= segments; s++)
                {
                    float phi = a0 + span * s / segments; Vector3 rad = Radial(phi);
                    v.Add(rad * rIn + new Vector3(0, 0, z)); v.Add(rad * rOut + new Vector3(0, 0, z)); n.Add(nn); n.Add(nn); uv.Add(new Vector2(phi * rIn, z)); uv.Add(new Vector2(phi * rOut, z));
                }
                for (int s = 0; s < segments; s++) { int a = b + 2 * s; t.Add(a); t.Add(a + 1); t.Add(a + 2); t.Add(a + 1); t.Add(a + 3); t.Add(a + 2); }
            }
            void CutFace(float phi, float sign)
            {
                Vector3 rad = Radial(phi), tang = new Vector3(Mathf.Sin(phi), Mathf.Cos(phi), 0) * sign; // S-frame tangent
                int k = v.Count;
                v.Add(rad * rIn + new Vector3(0, 0, z0)); v.Add(rad * rOut + new Vector3(0, 0, z0)); v.Add(rad * rOut + new Vector3(0, 0, z1)); v.Add(rad * rIn + new Vector3(0, 0, z1));
                for (int j = 0; j < 4; j++) { n.Add(tang); uv.Add(new Vector2(0, -1)); }
                t.Add(k); t.Add(k + 1); t.Add(k + 2); t.Add(k); t.Add(k + 2); t.Add(k + 3);
            }
            Ring(rOut, true); Ring(rIn, false); Cap(z1, Vector3.forward); Cap(z0, Vector3.back);
            // The two cut faces look into the cutaway: at phi = cutTo (start of the drawn arc, facing decreasing phi) and at
            // phi = cutFrom + 360 (its end, facing increasing phi).
            CutFace(a0, -1); CutFace(a0 + span, 1);
            return Build("Pack (cutaway)", v, n, uv, t);
        }

        /// <summary>The 12 edges of an axis-aligned box as square bars of thickness w.</summary>
        public static Mesh Frame(Vector3 size, float w)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            Vector3 h = size * 0.5f;
            for (int a = 0; a < 3; a++)
                for (int s1 = -1; s1 <= 1; s1 += 2)
                    for (int s2 = -1; s2 <= 1; s2 += 2)
                    {
                        Vector3 c = Vector3.zero, sz = Vector3.one * w;
                        int b = (a + 1) % 3, d = (a + 2) % 3;
                        c[b] = s1 * h[b]; c[d] = s2 * h[d]; sz[a] = size[a] + w;
                        AddBox(v, n, uv, t, c, sz);
                    }
            return Build("Frame", v, n, uv, t);
        }

        /// <summary>Cylinder along +Y from 0 to height.</summary>
        public static Mesh Cylinder(float radius, float height, int sides = 24)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int s = 0; s <= sides; s++)
            {
                float a = s * 2 * Mathf.PI / sides; Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(d * radius); v.Add(d * radius + Vector3.up * height); n.Add(d); n.Add(d); uv.Add(new Vector2(s / (float)sides, 0)); uv.Add(new Vector2(s / (float)sides, 1));
            }
            for (int s = 0; s < sides; s++) { int a = 2 * s; t.Add(a); t.Add(a + 1); t.Add(a + 2); t.Add(a + 1); t.Add(a + 3); t.Add(a + 2); }
            foreach (float y in new[] { 0f, height })
            {
                int c = v.Count; v.Add(Vector3.up * y); n.Add(y > 0 ? Vector3.up : Vector3.down); uv.Add(Vector2.one * 0.5f);
                for (int s = 0; s <= sides; s++) { float a = s * 2 * Mathf.PI / sides; v.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius + Vector3.up * y); n.Add(y > 0 ? Vector3.up : Vector3.down); uv.Add(Vector2.one * 0.5f); }
                for (int s = 0; s < sides; s++) { if (y > 0) { t.Add(c); t.Add(c + s + 2); t.Add(c + s + 1); } else { t.Add(c); t.Add(c + s + 1); t.Add(c + s + 2); } }
            }
            return Build("Cylinder", v, n, uv, t);
        }

        /// <summary>Torus in the XZ plane (ring of radius R, tube radius r).</summary>
        public static Mesh Torus(float R, float r, int seg = 48, int sides = 8)
        {
            var pts = new List<Vector3>();
            for (int s = 0; s <= seg; s++) { float a = s * 2 * Mathf.PI / seg; pts.Add(new Vector3(Mathf.Cos(a) * R, 0, Mathf.Sin(a) * R)); }
            return Tube(pts, r, sides, "Ring");
        }

        /// <summary>Cone along +Y from base (y = 0, radius r) to apex (y = h).</summary>
        public static Mesh Cone(float r, float h, int sides = 16)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            float slope = r / h;
            for (int s = 0; s < sides; s++)
            {
                float a0 = s * 2 * Mathf.PI / sides, a1 = (s + 1) * 2 * Mathf.PI / sides;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                int b = v.Count;
                v.Add(d0 * r); v.Add(d1 * r); v.Add(Vector3.up * h);
                n.Add((d0 + Vector3.up * slope).normalized); n.Add((d1 + Vector3.up * slope).normalized); n.Add(((d0 + d1) * 0.5f + Vector3.up * slope).normalized);
                t.Add(b); t.Add(b + 2); t.Add(b + 1);
                int c = v.Count; v.Add(d0 * r); v.Add(d1 * r); v.Add(Vector3.zero); n.Add(Vector3.down); n.Add(Vector3.down); n.Add(Vector3.down);
                t.Add(c); t.Add(c + 1); t.Add(c + 2);
            }
            return Build("Cone", v, n, null, t);
        }

        /// <summary>Flat rounded rectangle in the XY plane facing -Z (towards a viewer looking along +Z), with thickness d behind it.</summary>
        public static Mesh RoundedPanel(float w, float h, float radius, float depth = 0.004f, int arc = 6)
        {
            var outline = new List<Vector2>();
            Vector2[] centres = { new Vector2(w / 2 - radius, h / 2 - radius), new Vector2(-w / 2 + radius, h / 2 - radius), new Vector2(-w / 2 + radius, -h / 2 + radius), new Vector2(w / 2 - radius, -h / 2 + radius) };
            for (int c = 0; c < 4; c++)
                for (int k = 0; k <= arc; k++) { float a = (c * 90 + k * 90f / arc) * Mathf.Deg2Rad; outline.Add(centres[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius); }
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            int cf = v.Count; v.Add(Vector3.zero); n.Add(Vector3.back); uv.Add(new Vector2(0.5f, 0.5f));
            foreach (var p in outline) { v.Add(new Vector3(p.x, p.y, 0)); n.Add(Vector3.back); uv.Add(new Vector2(p.x / w + 0.5f, p.y / h + 0.5f)); }
            for (int i = 0; i < outline.Count; i++) { t.Add(cf); t.Add(cf + 1 + (i + 1) % outline.Count); t.Add(cf + 1 + i); }
            int cb = v.Count; v.Add(new Vector3(0, 0, depth)); n.Add(Vector3.forward); uv.Add(new Vector2(0.5f, 0.5f));
            foreach (var p in outline) { v.Add(new Vector3(p.x, p.y, depth)); n.Add(Vector3.forward); uv.Add(new Vector2(p.x / w + 0.5f, p.y / h + 0.5f)); }
            for (int i = 0; i < outline.Count; i++) { t.Add(cb); t.Add(cb + 1 + i); t.Add(cb + 1 + (i + 1) % outline.Count); }
            for (int i = 0; i < outline.Count; i++)
            {
                var p0 = outline[i]; var p1 = outline[(i + 1) % outline.Count]; var e = (p1 - p0).normalized; var nn = new Vector3(e.y, -e.x, 0);
                int k = v.Count; v.Add(new Vector3(p0.x, p0.y, 0)); v.Add(new Vector3(p1.x, p1.y, 0)); v.Add(new Vector3(p1.x, p1.y, depth)); v.Add(new Vector3(p0.x, p0.y, depth));
                for (int j = 0; j < 4; j++) { n.Add(nn); uv.Add(Vector2.zero); }
                t.Add(k); t.Add(k + 2); t.Add(k + 1); t.Add(k); t.Add(k + 3); t.Add(k + 2);
            }
            return Build("Panel", v, n, uv, t);
        }

        /// <summary>Quad in the XY plane facing -Z, size w x h, UV 0..1.</summary>
        /// <summary>UV sphere of radius r centred at the origin (atoms in the microscope, the microscope's glass).</summary>
        public static Mesh Sphere(float r, int lon = 16, int lat = 10)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int j = 0; j <= lat; j++)
                for (int i = 0; i <= lon; i++)
                {
                    float th = Mathf.PI * j / lat, ph = 2 * Mathf.PI * i / lon;
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    v.Add(d * r); n.Add(d); uv.Add(new Vector2(i / (float)lon, j / (float)lat));
                }
            for (int j = 0; j < lat; j++)
                for (int i = 0; i < lon; i++)
                {
                    int a = j * (lon + 1) + i, b = a + lon + 1;
                    if (j > 0) { t.Add(a); t.Add(b); t.Add(a + 1); }
                    if (j < lat - 1) { t.Add(a + 1); t.Add(b); t.Add(b + 1); }
                }
            return Build("Sphere", v, n, uv, t);
        }

        /// <summary>Open truncated cone from a circle of radius r0 at the origin to radius r1 at distance len along +Z.</summary>
        public static Mesh Frustum(float r0, float r1, float len, int sides = 24)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= sides; i++)
            {
                float a = 2 * Mathf.PI * i / sides; var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                v.Add(d * r0); v.Add(d * r1 + Vector3.forward * len);
                var nn = (d * len - Vector3.forward * (r1 - r0)).normalized; n.Add(nn); n.Add(nn);
                uv.Add(new Vector2(i / (float)sides, 0)); uv.Add(new Vector2(i / (float)sides, 1));
            }
            for (int i = 0; i < sides; i++) { int a = 2 * i; t.Add(a); t.Add(a + 1); t.Add(a + 2); t.Add(a + 1); t.Add(a + 3); t.Add(a + 2); }
            return Build("Frustum", v, n, uv, t);
        }

        public static Mesh Quad(float w, float h)
        {
            var v = new List<Vector3> { new Vector3(-w / 2, -h / 2, 0), new Vector3(w / 2, -h / 2, 0), new Vector3(w / 2, h / 2, 0), new Vector3(-w / 2, h / 2, 0) };
            var n = new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            var uv = new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            var t = new List<int> { 0, 2, 1, 0, 3, 2 };
            return Build("Quad", v, n, uv, t);
        }
    }
}
