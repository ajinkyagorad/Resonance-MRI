using Nebulytic.Resonance.Sim;
using UnityEngine;

namespace Nebulytic.Resonance
{
    /// <summary>
    /// The only place where physical (right-handed) coordinates meet Unity's left-handed ones. Every physics item's local
    /// frame S is the physical frame P with x negated: S = (-x, y, z). Never apply Unity cross products or rotations to
    /// physical vectors before converting them.
    /// </summary>
    public static class Frames
    {
        public static Vector3 ToS(D3 p) => new Vector3((float)-p.X, (float)p.Y, (float)p.Z);
        public static Vector3 ToS(double x, double y, double z) => new Vector3((float)-x, (float)y, (float)z);
        public static D3 ToP(Vector3 s) => new D3(-s.x, s.y, s.z);
        /// <summary>A physical size (all components positive) in S: the same numbers.</summary>
        public static Vector3 ToSAbs(Vector3 size) => new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));

        /// <summary>Rotation of every physics item relative to the dashboard: yaw about up.</summary>
        public static Quaternion PhysicsRotation(Quaternion dashboard, float yaw) => dashboard * Quaternion.Euler(0, yaw, 0);
    }
}
