using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nebulytic.Resonance
{
    /// <summary>
    /// Builds meshes for the Line3D shader (0.8.3): every segment is a quad of four vertices that the shader turns towards
    /// the eye, so a mesh is built once and never rebuilt when the view moves. Keys per point let the shader reveal a line
    /// up to a value (a time) without rebuilding it.
    /// </summary>
    public sealed class LineBuilder
    {
        readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
        readonly List<Vector4> uv = new List<Vector4>(), field = new List<Vector4>();
        readonly List<Color> c = new List<Color>(); readonly List<int> t = new List<int>();
        Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity; float maxWidth;

        public int Segments => t.Count / 6;

        public void Segment(Vector3 a, Vector3 b, float width, Color col, float s0 = 0, float s1 = 0, float key0 = -1e30f, float key1 = -1e30f, Vector4 f0 = default, Vector4 f1 = default)
        {
            Vector3 d = b - a; if (d.sqrMagnitude < 1e-14f) return;
            int k = v.Count;
            v.Add(a); v.Add(a); v.Add(b); v.Add(b);
            n.Add(d); n.Add(d); n.Add(d); n.Add(d);
            uv.Add(new Vector4(-1, width, s0, key0)); uv.Add(new Vector4(1, width, s0, key0)); uv.Add(new Vector4(-1, width, s1, key1)); uv.Add(new Vector4(1, width, s1, key1));
            field.Add(f0); field.Add(f0); field.Add(f1); field.Add(f1);
            c.Add(col); c.Add(col); c.Add(col); c.Add(col);
            t.Add(k); t.Add(k + 2); t.Add(k + 1); t.Add(k + 1); t.Add(k + 2); t.Add(k + 3);
            min = Vector3.Min(min, Vector3.Min(a, b)); max = Vector3.Max(max, Vector3.Max(a, b)); maxWidth = Mathf.Max(maxWidth, width);
        }

        /// <summary>A segment whose ribbon width changes from w0 at a to w1 at b (0.8.4: tapered arrows that face the eye).</summary>
        public void Taper(Vector3 a, Vector3 b, float w0, float w1, Color col)
        {
            Vector3 d = b - a; if (d.sqrMagnitude < 1e-14f) return;
            int k = v.Count;
            v.Add(a); v.Add(a); v.Add(b); v.Add(b);
            n.Add(d); n.Add(d); n.Add(d); n.Add(d);
            uv.Add(new Vector4(-1, w0, 0, -1e30f)); uv.Add(new Vector4(1, w0, 0, -1e30f)); uv.Add(new Vector4(-1, w1, 0, -1e30f)); uv.Add(new Vector4(1, w1, 0, -1e30f));
            field.Add(default); field.Add(default); field.Add(default); field.Add(default);
            c.Add(col); c.Add(col); c.Add(col); c.Add(col);
            t.Add(k); t.Add(k + 2); t.Add(k + 1); t.Add(k + 1); t.Add(k + 2); t.Add(k + 3);
            min = Vector3.Min(min, Vector3.Min(a, b)); max = Vector3.Max(max, Vector3.Max(a, b)); maxWidth = Mathf.Max(maxWidth, Mathf.Max(w0, w1));
        }

        /// <summary>A polyline through pts. keys (optional): reveal key per point; fields (optional): per-point field data.</summary>
        public void Polyline(IList<Vector3> pts, float width, Color col, IList<float> keys = null, IList<Vector4> fields = null)
        {
            float s = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                float step = Vector3.Distance(pts[i - 1], pts[i]);
                Segment(pts[i - 1], pts[i], width, col, s, s + step, keys != null ? keys[i - 1] : -1e30f, keys != null ? keys[i] : -1e30f,
                    fields != null ? fields[i - 1] : default, fields != null ? fields[i] : default);
                s += step;
            }
        }

        /// <summary>A circle of radius r in the plane through centre spanned by u and w (unit vectors).</summary>
        public void Circle(Vector3 centre, Vector3 u, Vector3 w, float r, float width, Color col, int segments = 48)
        {
            Vector3 prev = centre + u * r;
            for (int k = 1; k <= segments; k++)
            {
                float a = k * 2 * Mathf.PI / segments; Vector3 p = centre + (u * Mathf.Cos(a) + w * Mathf.Sin(a)) * r;
                Segment(prev, p, width, col); prev = p;
            }
        }

        /// <summary>An arrow from a to b with a two-stroke head.</summary>
        public void Arrow(Vector3 a, Vector3 b, float width, Color col, float head = 0.18f)
        {
            Segment(a, b, width, col);
            Vector3 d = b - a; float len = d.magnitude; if (len < 1e-6f) return; d /= len;
            Vector3 side = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 back = b - d * len * head;
            Segment(b, back + side * len * head * 0.45f, width, col); Segment(b, back - side * len * head * 0.45f, width, col);
        }

        public Mesh Commit(Mesh m)
        {
            m.Clear();
            m.indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetUVs(1, field); m.SetColors(c); m.SetTriangles(t, 0);
            m.bounds = v.Count > 0 ? new Bounds((min + max) / 2, max - min + Vector3.one * (maxWidth + 0.002f)) : new Bounds(Vector3.zero, Vector3.one * 0.01f);
            Clear();
            return m;
        }

        public void Clear() { v.Clear(); n.Clear(); uv.Clear(); field.Clear(); c.Clear(); t.Clear(); min = Vector3.positiveInfinity; max = Vector3.negativeInfinity; maxWidth = 0; }
    }
}
