using UnityEngine;

namespace Nebulytic.Resonance
{
    /// <summary>Marks a collider as a grabbable dashboard item ("scanner", "cube", "micro", "console", "strip") or the region frame.</summary>
    public sealed class Grabbable : MonoBehaviour { public string Kind; }
}
