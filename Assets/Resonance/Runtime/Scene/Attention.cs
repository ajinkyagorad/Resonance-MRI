using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nebulytic.Resonance
{
    /// <summary>One emphasizable thing in the scene. Brightness only ever depends on E; geometry never does.</summary>
    public sealed class Element
    {
        public string Key; public string[] Groups; public float E = 1, Target = 1; public Func<Vector3> Anchor;
        public bool Matches(string r)
        {
            if (Key == r) return true;
            foreach (var g in Groups) if (g == r) return true;
            return false;
        }
    }

    /// <summary>
    /// Referent registry, emphasis and the single pointer (SPEC 6). A cue's refs select the referents; everything else
    /// sits at its context level. Before the first cue and between steps everything is at full emphasis (overview).
    /// </summary>
    public sealed class Attention
    {
        readonly App app; readonly List<Element> elements = new List<Element>(); readonly Dictionary<string, Element> byKey = new Dictionary<string, Element>();
        public string[] Refs { get; private set; }
        public Transform Pointer; Vector3 pointerFrom, pointerTo; float flight = 1; string pointerRef; public Vector3 PointerTarget;
        public readonly Dictionary<string, Func<string, Vector3?>> Resolvers = new Dictionary<string, Func<string, Vector3?>>();
        public IReadOnlyList<Element> Elements => elements;

        public Attention(App app) { this.app = app; }

        public Element Register(string key, Func<Vector3> anchor, params string[] groups)
        {
            var e = new Element { Key = key, Groups = groups ?? new string[0], Anchor = anchor };
            elements.Add(e); byKey[key] = e; return e;
        }

        public static bool IsKnownRef(string r, IEnumerable<Element> elements)
        {
            if (r.StartsWith("spins.")) return true;
            foreach (var e in elements) if (e.Matches(r)) return true;
            return false;
        }

        /// <summary>Null refs = overview (everything emphasized).</summary>
        public void SetRefs(string[] refs)
        {
            Refs = refs;
            foreach (var e in elements)
            {
                if (refs == null) { e.Target = 1; continue; }
                bool hit = false; foreach (var r in refs) if (e.Matches(r)) { hit = true; break; }
                e.Target = hit ? 1 : 0;
            }
            string primary = refs != null && refs.Length > 0 ? refs[0] : null;
            if (primary != pointerRef) { pointerRef = primary; pointerFrom = Pointer ? Pointer.position : Vector3.zero; flight = 0; }
        }

        public bool Referenced(string r)
        {
            if (Refs == null) return false;
            foreach (var x in Refs) if (x == r) return true;
            return false;
        }

        public float E(string key) => byKey.TryGetValue(key, out var e) ? e.E : 1;

        public Vector3? AnchorOf(string r)
        {
            foreach (var kv in Resolvers) if (r.StartsWith(kv.Key)) { var p = kv.Value(r); if (p.HasValue) return p; }
            // The element that owns the key is the one the sentence names (the scanner's axes, not the cube's copies);
            // group members light up with it. Without an owner, point at the nearest member.
            if (byKey.TryGetValue(r, out var owner) && owner.Anchor != null) return owner.Anchor();
            Vector3 eye = app.Eye; Vector3? best = null; float bd = float.MaxValue;
            foreach (var e in elements)
                if (e.Matches(r) && e.Anchor != null)
                {
                    var p = e.Anchor(); float d = Vector3.Distance(p, eye);
                    if (d < bd) { bd = d; best = p; }
                }
            return best;
        }

        public void Tick(float dt, bool instant = false)
        {
            float k = instant ? 1 : 1 - Mathf.Exp(-dt / Look.EmphasisTime);
            foreach (var e in elements) e.E += (e.Target - e.E) * k;
            if (!Pointer) return;
            Vector3 eye = app.Eye;
            Vector3 target;
            var a = pointerRef != null ? AnchorOf(pointerRef) : null;
            if (a.HasValue)
            {
                // Approach from above and toward the eye so the chevron is seen from the side, pointing down at the referent.
                Vector3 h = Vector3.ProjectOnPlane(eye - a.Value, Vector3.up).normalized;
                target = a.Value + h * 0.03f + Vector3.up * 0.065f;
            }
            else target = app.StripRestPoint;
            PointerTarget = target;
            flight = instant ? 1 : Mathf.Min(1, flight + dt / 0.45f);
            float s = flight * flight * (3 - 2 * flight);
            Vector3 mid = 0.5f * (pointerFrom + target) + Vector3.up * 0.10f;
            Vector3 pos = flight >= 1 ? target : (1 - s) * (1 - s) * pointerFrom + 2 * (1 - s) * s * mid + s * s * target;
            Pointer.position = pos;
            Vector3 aim = a.HasValue ? a.Value - pos : (app.StripRestPoint + Vector3.down * 0.05f - pos);
            if (aim.sqrMagnitude > 1e-8f) Pointer.rotation = Quaternion.FromToRotation(Vector3.up, aim.normalized);
            if (flight >= 1) pointerFrom = pos;
        }
    }
}
