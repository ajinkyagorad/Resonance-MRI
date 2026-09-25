using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nebulytic.Resonance
{
    /// <summary>Input is accepted only after a neutral release; any loss of availability disarms it.</summary>
    public sealed class ReleaseGate
    {
        public bool Armed { get; private set; }
        public void Reset() { Armed = false; }
        public bool Sample(bool available, bool neutral)
        {
            if (!available) { Armed = false; return false; }
            if (!Armed) { if (neutral) Armed = true; return false; }
            return true;
        }
    }

    /// <summary>
    /// Controls (SPEC 8): right A next, right B previous, stick flick previous/next, left X pause, left stick click
    /// recenter, left Y help (opt-in labels beside the physical controls), grip to hold an item: it follows the controller's
    /// full position and orientation (0.8.4: free rotation about all three axes; the scanner and the proton block turn
    /// together, so the block and its outline stay aligned); stick-x also turns the held scanner or block about the
    /// vertical, stick-y scales. Trigger on a proton of the block shows its close-up, strip icons by trigger. Hands: pinch =
    /// trigger and grip. Desktop: arrows, space, R, V, M; left-drag moves an item, right-drag on an item turns it freely
    /// (right-drag elsewhere orbits the view), Q/E roll it; click a proton. Official Touch Plus models animate with the
    /// physical controls. The narration runs on by itself; A and B only jump.
    /// Focus loss releases everything, hides controllers, hands, help and rays, and pauses.
    /// </summary>
    public sealed class Controls : MonoBehaviour
    {
        public App App; public Transform LeftModel, RightModel; public OVRHand LeftHand, RightHand;
        public bool HelpVisible { get; private set; }

        sealed class Side
        {
            public readonly ReleaseGate Gate = new ReleaseGate(); public Transform Anchor, Model; public OVRHand Hand; public Animator Animator;
            public bool Trigger, Grip, One, Two, Stick, Flick, HandMode; public Transform Held; public string HeldKind; public Vector3 Offset; public Quaternion Rotation, GrabAim, GrabShared; public float HeldYaw;
            public readonly List<TextMeshPro> Labels = new List<TextMeshPro>(); public readonly List<Transform> Bones = new List<Transform>();
            public Transform Ray; public Renderer[] HandRenderers; public float KnobRepeat;
        }

        readonly Side[] sides = { new Side(), new Side() };
        bool initialized, hadFocus; readonly RaycastHit[] hits = new RaycastHit[32];
        Transform desktopHeld; string desktopKind; Vector3 desktopOffset; float desktopDistance; Vector2 lastMouse; Transform desktopTurning; string desktopTurnKind;

        public static Transform FindControl(Transform root, int side, int key)
        {
            string[] names = { side == 0 ? "left_b_trigger_front" : "right_b_trigger_front", side == 0 ? "left_b_trigger_grip" : "right_b_trigger_grip", side == 0 ? "left_b_thumbstick" : "right_b_thumbstick", side == 0 ? "b_button_x" : "b_button_a", side == 0 ? "b_button_y" : "b_button_b" };
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == names[key]) return t;
            return null;
        }

        void Init()
        {
            var font = Resources.Load<TMP_FontAsset>("AtlasSansSDF");
            for (int i = 0; i < 2; i++)
            {
                var s = sides[i]; s.Model = i == 0 ? LeftModel : RightModel; s.Hand = i == 0 ? LeftHand : RightHand;
                s.Anchor = i == 0 ? App.Rig.leftControllerAnchor : App.Rig.rightControllerAnchor;
                s.Animator = s.Model ? s.Model.GetComponentInChildren<Animator>(true) : null;
                s.HandRenderers = s.Hand ? s.Hand.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
                var ray = Mats.Object("Selection ray", transform, MeshKit.Cylinder(0.0012f, 1, 6), Mats.Flat(new Color(Look.SCAF.r, Look.SCAF.g, Look.SCAF.b, 0.75f)));
                s.Ray = ray.transform; ray.SetActive(false);
                for (int k = 0; k < 5; k++)
                {
                    s.Bones.Add(s.Model ? FindControl(s.Model, i, k) : null);
                    var go = new GameObject("Help label"); go.transform.SetParent(transform, false);
                    var t = go.AddComponent<TextMeshPro>(); t.font = font; t.fontSharedMaterial = Labels.Outlined; t.fontSize = 0.085f; t.color = Look.TEXT; t.alignment = TextAlignmentOptions.Center;
                    t.rectTransform.sizeDelta = new Vector2(0.12f, 0.02f); go.SetActive(false); s.Labels.Add(t);
                }
            }
            OVRManager.InputFocusLost += FocusLost;
            initialized = true;
        }

        public void ToggleHelp() { HelpVisible = !HelpVisible; }

        public void ReleaseAll()
        {
            desktopHeld = null;
            foreach (var s in sides)
            {
                if (s.HeldKind == "region" && s.Held) App.CommitRegion();
                s.Held = null; s.HeldKind = null; s.Gate.Reset(); s.Trigger = s.Grip = s.One = s.Two = s.Stick = s.Flick = false;
                if (s.Ray) s.Ray.gameObject.SetActive(false);
            }
        }

        void FocusLost()
        {
            ReleaseAll(); foreach (var s in sides) Hide(s);
            if (App && App.Ready) { App.Pause(); if (!App.Desktop) App.Environment.RestoreMR(); }
        }

        void Hide(Side s)
        {
            if (s.Model) s.Model.gameObject.SetActive(false);
            if (s.HandRenderers != null) foreach (var r in s.HandRenderers) if (r) r.forceRenderingOff = true;
            foreach (var l in s.Labels) l.gameObject.SetActive(false);
            if (s.Ray) s.Ray.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!App || !App.Ready) return;
            if (!initialized) Init();
            bool focus = Application.isFocused && (App.Desktop || OVRManager.hasInputFocus);
            if (!focus) { if (hadFocus) FocusLost(); foreach (var s in sides) Hide(s); hadFocus = false; return; }
            hadFocus = true;
            if (App.Desktop) { foreach (var s in sides) Hide(s); DesktopUpdate(); return; }
            for (int i = 0; i < 2; i++) UpdateSide(i);
        }

        public static void AnimateController(Animator animator, OVRInput.Controller c)
        {
            if (!animator) return;
            animator.SetFloat("Button 1", OVRInput.Get(OVRInput.Button.One, c) ? 1 : 0); animator.SetFloat("Button 2", OVRInput.Get(OVRInput.Button.Two, c) ? 1 : 0);
            animator.SetFloat("Button 3", OVRInput.Get(OVRInput.Button.Start, c) ? 1 : 0);
            animator.SetFloat("Trigger", OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, c)); animator.SetFloat("Grip", OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, c));
            var stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, c); animator.SetFloat("Joy X", stick.x); animator.SetFloat("Joy Y", stick.y);
        }

        void UpdateSide(int i)
        {
            var s = sides[i]; var c = i == 0 ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
            bool controller = OVRInput.IsControllerConnected(c) && OVRInput.GetControllerPositionTracked(c) && OVRInput.GetControllerOrientationTracked(c);
            bool hand = !controller && s.Hand && s.Hand.IsTracked && s.Hand.IsDataHighConfidence;
            if (hand != s.HandMode) { ReleaseSide(s); s.HandMode = hand; }
            if (!controller && !hand) { ReleaseSide(s); Hide(s); return; }
            if (s.Model) s.Model.gameObject.SetActive(controller);
            foreach (var r in s.HandRenderers) if (r) r.forceRenderingOff = !hand;
            bool pinch = hand && s.Hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
            float trigger = controller ? OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, c) : pinch ? 1 : 0;
            float grip = controller ? OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, c) : pinch ? 1 : 0;
            bool one = controller && OVRInput.Get(OVRInput.Button.One, c), two = controller && OVRInput.Get(OVRInput.Button.Two, c);
            var stick = controller ? OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, c) : Vector2.zero;
            bool stickPress = controller && OVRInput.Get(OVRInput.Button.PrimaryThumbstick, c);
            AnimateController(s.Animator, c);
            Transform aim = hand ? s.Hand.PointerPose : s.Anchor;
            if (!aim) { ReleaseSide(s); Hide(s); return; }
            bool ready = s.Gate.Sample(true, trigger < 0.15f && grip < 0.15f && !one && !two && !stickPress && stick.sqrMagnitude < 0.01f);
            var ray = new Ray(aim.position, aim.forward);
            var target = Pick(ray, out Vector3 point);
            bool t = trigger > 0.65f, g = grip > 0.65f;
            if (ready)
            {
                // Trigger: strip icon, or a proton of the grid (its close-up is shown).
                if (t && !s.Trigger && target)
                {
                    var btn = target.GetComponent<StripButton>();
                    if (btn) { App.Action(btn.Action); Haptic(c); }
                    else if (App.SelectProton(ray)) Haptic(c);
                }
                // Grip: grab an item or the region frame.
                if (g && !s.Grip && target && !(hand && target.GetComponent<StripButton>()))
                {
                    var grab = target.GetComponentInParent<Grabbable>();
                    if (grab && grab.transform != sides[1 - i].Held)
                    {
                        s.Held = grab.transform; s.HeldKind = grab.Kind; s.HeldYaw = App.SharedYaw;
                        s.Offset = Quaternion.Inverse(aim.rotation) * (grab.transform.position - aim.position);
                        s.Rotation = Quaternion.Inverse(aim.rotation) * grab.transform.rotation; s.GrabAim = aim.rotation; s.GrabShared = App.SharedRotation; Haptic(c);
                    }
                }
                if (!g && s.Held) { if (s.HeldKind == "region") App.CommitRegion(); s.Held = null; s.HeldKind = null; }
                if (s.Held)
                {
                    Vector3 pos = aim.position + aim.rotation * s.Offset;
                    if (s.HeldKind == "region") App.DragRegion(pos);
                    else
                    {
                        s.Held.position = pos;
                        // The held item follows the controller's full orientation. The scanner and the block share one rotation:
                        // turning either turns both (each about its own centre). Stick x adds a turn about the vertical.
                        if (Linked(s.HeldKind))
                        {
                            if (Mathf.Abs(stick.x) > 0.15f) s.GrabShared = Quaternion.AngleAxis(stick.x * 60 * Time.deltaTime, Vector3.up) * s.GrabShared;
                            App.SetShared(aim.rotation * Quaternion.Inverse(s.GrabAim) * s.GrabShared);
                        }
                        else if (s.HeldKind != "strip") s.Held.rotation = aim.rotation * s.Rotation; // the strip keeps facing the eye
                        if (Mathf.Abs(stick.y) > 0.15f) App.ScaleItem(s.Held, Mathf.Exp(stick.y * Time.deltaTime * 0.6f));
                    }
                }
                // Buttons: no pointing needed.
                if (one && !s.One) { if (i == 1) App.Next(); else App.TogglePlay(); Haptic(c); }
                if (two && !s.Two) { if (i == 1) App.Previous(); else ToggleHelp(); Haptic(c); }
                if (stickPress && !s.Stick && !s.Held && i == 0 && stick.sqrMagnitude < 0.09f) App.Recenter();
                if (!s.Held)
                {
                    if (Mathf.Abs(stick.x) > 0.7f && Mathf.Abs(stick.x) > Mathf.Abs(stick.y) * 1.5f && !s.Flick) { s.Flick = true; if (stick.x > 0) App.Next(); else App.Previous(); Haptic(c); }
                    else if (Mathf.Abs(stick.x) < 0.3f) s.Flick = false;
                }
            }
            s.Trigger = t; s.Grip = g; s.One = one; s.Two = two; s.Stick = stickPress;
            bool show = target && !s.Held && ready;
            s.Ray.gameObject.SetActive(show);
            if (show)
            {
                float len = Vector3.Distance(aim.position, point);
                s.Ray.position = aim.position; s.Ray.rotation = Quaternion.FromToRotation(Vector3.up, aim.forward); s.Ray.localScale = new Vector3(1, len, 1);
            }
            UpdateHelp(s, i, controller && ready);
        }

        bool Linked(string kind) => kind == "scanner" || kind == "cube" && App.SyncTissue;

        /// <summary>Turns an item by a world rotation about its own centre (the scanner and the block together).</summary>
        void Turn(Transform t, string kind, Quaternion delta)
        {
            if (Linked(kind)) App.RotateShared(delta);
            else t.rotation = delta * t.rotation;
        }

        void ReleaseSide(Side s)
        {
            if (s.HeldKind == "region" && s.Held) App.CommitRegion();
            s.Held = null; s.HeldKind = null; s.Gate.Reset(); s.Trigger = s.Grip = s.One = s.Two = false;
        }

        void UpdateHelp(Side s, int i, bool usable)
        {
            string[] actions = { "Select", s.Held ? "Release" : "Move", i == 0 ? "Click: recenter" : "◂ ▸ step", i == 0 ? (App.Playing ? "Pause" : "Play") : "Next step", i == 0 ? "Help" : "Previous step" };
            Vector3[] offset = { new Vector3(0, 0.008f, 0.034f), new Vector3(i == 0 ? -0.042f : 0.042f, -0.018f, 0), new Vector3(i == 0 ? 0.036f : -0.036f, 0.022f, 0.014f), new Vector3(i == 0 ? -0.043f : 0.043f, 0.004f, -0.018f), new Vector3(i == 0 ? -0.043f : 0.043f, 0.018f, 0.012f) };
            for (int k = 0; k < 5; k++)
            {
                var label = s.Labels[k]; bool show = HelpVisible && usable && s.Bones[k];
                label.gameObject.SetActive(show); if (!show) continue;
                label.text = actions[k];
                label.transform.position = s.Bones[k].position + s.Anchor.TransformVector(offset[k]);
                label.transform.rotation = Quaternion.LookRotation(label.transform.position - App.HeadCamera.transform.position, App.HeadCamera.transform.up);
            }
        }

        /// <summary>Picks strip icons first, then the region frame, then items.</summary>
        public Collider Pick(Ray ray, out Vector3 point)
        {
            int count = Physics.RaycastNonAlloc(ray, hits, 5, ~0, QueryTriggerInteraction.Collide);
            Collider chosen = null; float score = float.MaxValue; point = ray.GetPoint(2);
            for (int i = 0; i < count; i++)
            {
                var h = hits[i]; var c = h.collider;
                bool icon = c.GetComponent<StripButton>();
                var grab = c.GetComponent<Grabbable>();
                if (!icon && !grab) continue;
                float rank = h.distance + (icon ? -0.3f : grab.Kind == "region" ? -0.2f : 0);
                if (rank < score) { score = rank; chosen = c; point = h.point; }
            }
            return chosen;
        }

        void Haptic(OVRInput.Controller c) { OVRInput.SetControllerVibration(0.25f, 0.18f, c); StartCoroutine(StopHaptic(c)); }
        System.Collections.IEnumerator StopHaptic(OVRInput.Controller c) { yield return new WaitForSeconds(0.035f); OVRInput.SetControllerVibration(0, 0, c); }

        void DesktopUpdate()
        {
            var mouse = Mouse.current; var key = Keyboard.current; if (mouse == null || key == null) return;
            var pos = mouse.position.ReadValue(); var ray = App.HeadCamera.ScreenPointToRay(pos); var target = Pick(ray, out var point);
            if (mouse.leftButton.wasPressedThisFrame && target)
            {
                var btn = target.GetComponent<StripButton>();
                if (btn) App.Action(btn.Action);
                else if (App.SelectProton(ray)) { } // a click on a proton shows it in the close-up
                else
                {
                    var grab = target.GetComponentInParent<Grabbable>();
                    if (grab) { desktopHeld = grab.transform; desktopKind = grab.Kind; desktopDistance = Vector3.Distance(ray.origin, point); desktopOffset = desktopHeld.position - point; }
                }
            }
            if (!mouse.leftButton.isPressed && desktopHeld) { if (desktopKind == "region") App.CommitRegion(); desktopHeld = null; }
            if (desktopHeld)
            {
                var p = ray.GetPoint(desktopDistance) + desktopOffset;
                if (desktopKind == "region") App.DragRegion(p); else desktopHeld.position = p;
            }
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                var cam = App.HeadCamera.transform; cam.position += cam.forward * Mathf.Clamp(scroll * 0.0008f, -0.2f, 0.2f);
            }
            // Right-drag on an item turns it freely (horizontal: about the view's up, vertical: about the view's right);
            // right-drag elsewhere orbits the view about the dashboard.
            if (mouse.rightButton.wasPressedThisFrame)
            {
                var grab = target ? target.GetComponentInParent<Grabbable>() : null;
                desktopTurning = grab && grab.Kind != "region" && grab.Kind != "strip" ? grab.transform : null; desktopTurnKind = grab ? grab.Kind : null;
            }
            if (!mouse.rightButton.isPressed) desktopTurning = null;
            if (mouse.rightButton.isPressed)
            {
                Vector2 d = pos - lastMouse; var cam = App.HeadCamera.transform;
                if (desktopTurning) Turn(desktopTurning, desktopTurnKind, Quaternion.AngleAxis(-d.x * 0.35f, cam.up) * Quaternion.AngleAxis(d.y * 0.35f, cam.right));
                else { var pivot = App.DashboardCentre; cam.RotateAround(pivot, Vector3.up, d.x * 0.15f); cam.RotateAround(pivot, cam.right, -d.y * 0.12f); cam.LookAt(pivot); }
            }
            // Q/E roll the item under the mouse (or held) about the line of sight: the third axis.
            var rollTarget = desktopHeld ? desktopHeld : desktopTurning ? desktopTurning : target ? target.GetComponentInParent<Grabbable>()?.transform : null;
            string rollKind = rollTarget ? rollTarget.GetComponent<Grabbable>()?.Kind : null;
            if (rollTarget && rollKind != "region" && rollKind != "strip")
            {
                float roll = (key.qKey.isPressed ? 1 : 0) - (key.eKey.isPressed ? 1 : 0);
                if (roll != 0) Turn(rollTarget, rollKind, Quaternion.AngleAxis(roll * 60 * Time.deltaTime, App.HeadCamera.transform.forward));
            }
            lastMouse = pos;
            if (key.spaceKey.wasPressedThisFrame || key.xKey.wasPressedThisFrame) App.TogglePlay();
            if (key.rightArrowKey.wasPressedThisFrame || key.pageDownKey.wasPressedThisFrame || key.aKey.wasPressedThisFrame) App.Next();
            if (key.leftArrowKey.wasPressedThisFrame || key.pageUpKey.wasPressedThisFrame || key.bKey.wasPressedThisFrame) App.Previous();
            if (key.rKey.wasPressedThisFrame) App.Recenter();
            if (key.vKey.wasPressedThisFrame) App.Action("mode");
            if (key.mKey.wasPressedThisFrame) App.Action("voice");
            if (key.hKey.wasPressedThisFrame) ToggleHelp();
            if (key.fKey.wasPressedThisFrame) App.Action("perf");
            if (key.cKey.wasPressedThisFrame) App.Action("subtitles");
        }

        void OnApplicationFocus(bool focus) { if (!focus && initialized) FocusLost(); }
        void OnDisable() { ReleaseAll(); }
        void OnDestroy() { if (initialized) OVRManager.InputFocusLost -= FocusLost; }
    }
}
