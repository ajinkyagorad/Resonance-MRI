using System.Collections;
using UnityEngine;

namespace Nebulytic.Resonance
{
    /// <summary>
    /// Mixed reality (default) and opaque VR. MR suppresses the boundary through the contextual Boundary API while
    /// passthrough is actually shown; VR first restores the boundary and waits for the system to confirm it, and stays in
    /// MR if that fails. Focus loss or pause returns to MR. Never switches layout.
    /// </summary>
    public sealed class Environment : MonoBehaviour
    {
        public App App; public bool VirtualReality { get; private set; } public string TransitionStatus = "Passthrough"; public GameObject Room { get; private set; }
        OVRManager manager; OVRPassthroughLayer layer; Coroutine transition; int token;

        public void Initialize()
        {
            manager = App.Rig ? App.Rig.GetComponent<OVRManager>() : null; layer = App.Rig ? App.Rig.GetComponent<OVRPassthroughLayer>() : null;
            if (!App.Desktop)
            {
                // The eye layer is composited as premultiplied alpha over passthrough: out = rgb + room * (1 - a).
                // Light-only effects write no alpha, so the room stays at full brightness behind them.
                OVRManager.eyeFovPremultipliedAlphaModeEnabled = true;
            }
            // VR studio (only in VR; 0.8.4: no grey): a soft navy gradient dome, darker above and lighter toward the horizon,
            // over a matching floor, so content is judged on a calm dark-blue ground rather than black or grey.
            Room = new GameObject("VR room");
            var grad = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Studio gradient" };
            Color top = Look.Hex(0x17212A), horizon = Look.Hex(0x455C67), bottom = Look.Hex(0x202D35);
            for (int k = 0; k < 64; k++)
            {
                float v = k / 63f; // the sphere's v runs from the top (0) to the bottom (1)
                grad.SetPixel(0, k, v < 0.5f ? Color.Lerp(top, horizon, Mathf.SmoothStep(0, 1, v / 0.5f)) : Color.Lerp(horizon, bottom, Mathf.SmoothStep(0, 1, (v - 0.5f) / 0.5f)));
            }
            grad.Apply(false, true);
            var dome = Mats.Object("Studio dome", Room.transform, MeshKit.Sphere(12, 32, 16), Mats.Flat(Color.white, grad));
            dome.GetComponent<Renderer>().sharedMaterial.renderQueue = 1000;
            Mats.Object("Floor", Room.transform, MeshKit.Cylinder(4, 0.02f, 64), Mats.Solid(Look.Hex(0x293942)), new Vector3(0, -0.02f, 0));
            Apply(App.Desktop);
        }

        public void Toggle()
        {
            if (VirtualReality || transition != null) { token++; if (transition != null) StopCoroutine(transition); transition = null; Apply(false); }
            else transition = StartCoroutine(EnterVR(++token));
        }

        IEnumerator EnterVR(int id)
        {
            App.Controls.ReleaseAll(); TransitionStatus = "Restoring VR boundary";
            if (manager) manager.shouldBoundaryVisibilityBeSuppressed = false;
            float end = Time.realtimeSinceStartup + 8;
            while (Time.realtimeSinceStartup < end && id == token)
            {
                if (App.Desktop) { Apply(true); transition = null; yield break; }
                var result = OVRPlugin.GetBoundaryVisibility(out var state);
                if (result == OVRPlugin.Result.Success && state == OVRPlugin.BoundaryVisibility.NotSuppressed) { Apply(true); transition = null; yield break; }
                yield return null;
            }
            if (id == token) { Apply(false); TransitionStatus = "VR boundary unavailable: passthrough retained"; transition = null; }
        }

        void Apply(bool vr)
        {
            VirtualReality = vr; if (Room) Room.SetActive(vr); TransitionStatus = vr ? "VR" : "Passthrough";
            if (manager) { manager.isInsightPassthroughEnabled = true; manager.shouldBoundaryVisibilityBeSuppressed = !vr; }
            // Passthrough is shown exactly as the headset captures it: no colour map, no brightness, contrast or saturation
            // change, no edge rendering, full opacity. (0.8.0 dimmed and desaturated it here.)
            if (layer && !App.Desktop) { layer.enabled = true; layer.DisableColorMap(); layer.edgeRenderingEnabled = false; layer.textureOpacity = vr ? 0 : 1; }
            App.HeadCamera.clearFlags = CameraClearFlags.SolidColor;
            App.HeadCamera.backgroundColor = vr ? Look.Hex(0x17212A) : Color.clear;
        }

        void OnApplicationPause(bool pause) { if (pause && App && App.Ready && !App.Desktop) RestoreMR(); }
        void OnApplicationFocus(bool focus) { if (!focus && App && App.Ready && !App.Desktop) RestoreMR(); }
        public void RestoreMR() { token++; if (transition != null) StopCoroutine(transition); transition = null; Apply(false); }
        void OnDestroy() { if (manager) manager.shouldBoundaryVisibilityBeSuppressed = false; }
    }
}
