using System;
using System.Collections.Generic;
using Nebulytic.Resonance.Sim;
using UnityEngine;
using UnityEngine.XR;

namespace Nebulytic.Resonance
{
    /// <summary>
    /// Application root. Owns the simulation, the dashboard items (scanner, acquired data, proton grid, received signal,
    /// pulse sequence, proton close-up, strip), the pointer, the lesson clock and navigation. Every frame, one physical time t
    /// is computed from the lesson clock and every view reads the simulation at that same t (on the headset the spins are
    /// evaluated by a worker one frame behind). The narration runs on by itself from step to step (0.8.3); A and B jump.
    /// </summary>
    public sealed class App : MonoBehaviour
    {
        public OVRCameraRig Rig; public Camera HeadCamera; public Controls Controls; public Environment Environment;
        public Simulation Sim { get; private set; }
        public Attention Attention { get; private set; }
        public ScannerView Scanner { get; private set; }
        public CubeView Cube { get; private set; }
        public PlotsView Plots { get; private set; }
        public CloseUpView CloseUp { get; private set; }
        public Strip Strip { get; private set; }
        public Performance Perf { get; private set; }
        /// <summary>Frame-time readout on the strip (off by default; the strip's "ms" icon or F toggles it).</summary>
        public bool ShowFrameTimes { get; private set; }
        public bool Muted { get; private set; }
        /// <summary>Main-thread cost of each part of the last frames (ms, smoothed) and the spin worker's cost.</summary>
        public readonly FrameCost Cost = new FrameCost();
        public Lesson Lesson { get; private set; }
        public Protocol Protocol { get; private set; } = new Protocol();
        public HandSpecimen Specimen { get; private set; }
        public Transform World { get; private set; }
        public bool Ready { get; private set; }
        public bool Desktop => Application.platform != RuntimePlatform.Android && !XRSettings.isDeviceActive;
        public Vector3 Eye => HeadCamera ? HeadCamera.transform.position : Vector3.up * 1.6f;
        public List<string> AnatomyNames { get; } = new List<string>();
        public string VoiceName { get; private set; } = "am_michael";

        // Lesson clock.
        public int StepIndex { get; private set; }
        public double Tau { get; private set; }
        public bool Playing { get; private set; }
        public bool FixedStep; // headless validation: advance by exactly 1/72 s per frame, no audio
        public double T { get; private set; }
        public bool Lab { get; private set; }
        public LessonCue Cue { get; private set; }
        public bool Overview { get; private set; } = true;
        public string Status { get; private set; }
        public bool Stalled { get; private set; }
        /// <summary>
        /// The displayed carrier phase: the lab frame turns with it (grid, scanner, close-up). One turn per 2 s of display
        /// time while playing; during carrier-speed cues it follows the physical carrier (plus a fixed offset, so it never
        /// jumps).
        /// </summary>
        public double DisplayCarrier { get; private set; }
        double carrierOffset; bool wasLab;

        // Layout.
        Quaternion dashboard = Quaternion.identity; Vector3 layoutEye; bool laidOut; float settle; float settleStart;
        /// <summary>
        /// World rotation shared by the scanner and the proton block (0.8.4: any 3-D rotation, from a grab with the full
        /// controller orientation or a desktop drag), so the block and its outline in the scanner always stay aligned.
        /// </summary>
        public Quaternion SharedRotation { get; private set; } = Quaternion.identity;
        /// <summary>Yaw of the shared rotation about the vertical, relative to the layout (degrees; kept for the stick and tests).</summary>
        public float SharedYaw { get; private set; }
        readonly Dictionary<Transform, Vector3> homeScale = new Dictionary<Transform, Vector3>();
        public Vector3 DashboardCentre => laidOut ? 0.5f * (Scanner.Root.position + Cube.Root.position) : Vector3.forward + Vector3.up * 1.5f;
        public IEnumerable<Transform> Items { get { yield return Scanner.Root; yield return Plots.Data; yield return Cube.Root; yield return Plots.Signal; yield return Plots.Sequence; yield return CloseUp.Root; yield return Strip.Root; } }
        public Vector3 StripRestPoint => Strip != null ? Strip.Root.TransformPoint(new Vector3(-0.30f, 0.03f, -0.01f)) : Vector3.zero;

        AudioSource narration; string playingClip; int lastCueKey = -1; D3? cubeCentre; bool pendingRestart; int frame; Transform chevron;
        /// <summary>The spoken sentence shown as subtitles on the strip (default on; the strip's CC chip or C toggles it).</summary>
        public bool Subtitles { get; private set; } = true;
        LessonData lessonData;

        void Start()
        {
#if RESONANCE_STORE && UNITY_ANDROID && !UNITY_EDITOR
            StoreGate.Check(this, Initialize);
#else
            Initialize();
#endif
        }

        void Initialize()
        {
            Application.targetFrameRate = 72; QualitySettings.vSyncCount = 0;
#if UNITY_ANDROID && !UNITY_EDITOR
            // The rebuild and acquisition run on one low-priority thread, never on the cores the frame needs (0.8.2).
            Work.MaxThreads = 1;
#endif
            Mats.Init();
            var tables = FieldTables.Load(Resources.Load<TextAsset>("Resonance/FieldTables").bytes);
            var labels = HandLabels.Load(Resources.Load<TextAsset>("Anatomy/HandLabels").bytes);
            var manifest = Resources.Load<TextAsset>("Anatomy/AnatomyManifest");
            foreach (var part in manifest.text.Split('"'))
                if (part.EndsWith(".obj") && (part.StartsWith("Bone_") || part.StartsWith("Muscle_") || part == "Skin.obj")) AnatomyNames.Add(part.Substring(0, part.Length - 4));
            Sim = new Simulation(tables, labels) { AddNoise = true };
            Specimen = new HandSpecimen(labels, Simulation.DefaultPose(labels));
            World = new GameObject("Dashboard").transform; World.SetParent(transform, false); World.gameObject.SetActive(false);
            Attention = new Attention(this);
            Scanner = new ScannerView(this, World); Cube = new CubeView(this, World); Plots = new PlotsView(this, World); CloseUp = new CloseUpView(this, World); Strip = new Strip(this, World);
            Spatial = new SpatialTools(this);
            Perf = GetComponent<Performance>() ?? UnityEngine.Object.FindAnyObjectByType<Performance>();
            // The pointer (0.8.4): a white chevron with a dark casing (reads over a bright room), turned to face the eye.
            var pointer = new GameObject("Pointer").transform; pointer.SetParent(World, false);
            var chev = new LineBuilder(); chev.Segment(new Vector3(-0.016f, 0, 0), new Vector3(0, 0.02f, 0), 0.0065f, Color.white); chev.Segment(new Vector3(0.016f, 0, 0), new Vector3(0, 0.02f, 0), 0.0065f, Color.white);
            var chevMat = Mats.Line(Look.SCAF, false, 0.36f); chevMat.SetFloat("_ScaleWidth", 0);
            chevron = Mats.Object("Chevron", pointer, chev.Commit(new Mesh { name = "Chevron" }), chevMat).transform;
            Attention.Pointer = pointer;
            foreach (var t in Items) homeScale[t] = t.localScale;
            foreach(Transform t in World) if(t!=Cube.Root && t!=Strip.Root && t!=Attention.Pointer)teachingHidden.Add(t);
            narration = gameObject.AddComponent<AudioSource>(); narration.spatialBlend = 0; narration.volume = 0.9f; narration.playOnAwake = false;
            Lesson = new Lesson(); lessonData = Lesson.LoadData(VoiceName);
            if (Controls) Controls.App = this;
            if (Environment) { Environment.App = this; Environment.Initialize(); }
            if (Desktop)
            {
                if (Rig) { Rig.enabled = false; var m = Rig.GetComponent<OVRManager>(); if (m) m.enabled = false; }
                HeadCamera.transform.SetPositionAndRotation(new Vector3(0, 1.60f, 0), Quaternion.Euler(8, 0, 0));
                HeadCamera.fieldOfView = 76;
            }
            Status = "Preparing the simulation…";
            Sim.Request(Protocol, Specimen, null);
            settleStart = Time.realtimeSinceStartup;
            Ready = true;
        }

        // ------------------------------------------------------------------------------------------------ layout

        bool HeadSettled(float dt)
        {
            if (Desktop) return true;
            if (Time.realtimeSinceStartup - settleStart > 4) return true;
            bool tracked = OVRPlugin.GetNodePositionTracked(OVRPlugin.Node.EyeCenter) && HeadCamera.transform.position.y > 0.3f;
            float angular = 0;
            var dev = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (dev.isValid && dev.TryGetFeatureValue(CommonUsages.deviceAngularVelocity, out Vector3 w)) angular = w.magnitude * Mathf.Rad2Deg;
            if (tracked && angular < 30) settle += dt; else settle = 0;
            return settle >= 0.3f;
        }

        public void Layout()
        {
            teachingFocused=false; foreach(var t in teachingHidden) if(t)t.gameObject.SetActive(true);
            Vector3 eye = Eye; if (eye.y <= 0.3f) eye.y = 1.60f;
            Vector3 fwd = HeadCamera.transform.forward, fh = Vector3.ProjectOnPlane(fwd, Vector3.up);
            if (fh.sqrMagnitude < 0.04f) fh = dashboard * Vector3.forward;
            dashboard = Quaternion.LookRotation(fh.normalized, Vector3.up); layoutEye = eye; SharedYaw = 0;
            SharedRotation = Frames.PhysicsRotation(dashboard, Look.PhysicsYaw);
            Scanner.Root.position = Slot(Look.ScannerAz, Look.ScannerEl, Look.ScannerDist);
            Cube.Root.position = Slot(Look.CubeAz, Look.CubeEl, Look.CubeDist);
            Plots.Data.position = Slot(Look.DataAz, Look.DataEl, Look.DataDist);
            Plots.Signal.position = Slot(Look.SignalAz, Look.SignalEl, Look.SignalDist);
            Plots.Sequence.position = Slot(Look.SequenceAz, Look.SequenceEl, Look.SequenceDist);
            CloseUp.Root.position = Slot(Look.CloseAz, Look.CloseEl, Look.CloseDist);
            Strip.Root.position = Slot(Look.StripAz, Look.StripEl, Look.StripDist);
            ApplyYaw(); Cube.Root.rotation = SharedRotation;
            foreach (var kv in homeScale) kv.Key.localScale = kv.Value;
            // Plots, close-up and strip face the eye squarely (0.8.4: yaw and pitch, so the panels below eye level tilt back
            // toward you and their text is not foreshortened).
            foreach (var t in new[] { Plots.Data, Plots.Signal, Plots.Sequence, CloseUp.Root, Strip.Root })
            {
                Vector3 d = t.position - eye;
                t.rotation = Quaternion.LookRotation(d.sqrMagnitude > 1e-6f ? d : Vector3.forward, Vector3.up);
            }
            Strip.Root.rotation = Quaternion.LookRotation(Strip.Root.position - eye, Vector3.up);
            laidOut = true; World.gameObject.SetActive(true);
        }

        public Vector3 Slot(float az, float el, float d)
        {
            float a = az * Mathf.Deg2Rad, e = el * Mathf.Deg2Rad;
            return layoutEye + dashboard * (d * new Vector3(Mathf.Cos(e) * Mathf.Sin(a), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Cos(a)));
        }

        void ApplyYaw() { Scanner.Root.rotation = SharedRotation; if (SyncTissue) Cube.Root.rotation = SharedRotation; }

        public void YawShared(float degrees) { SharedYaw += degrees; SharedRotation = Quaternion.AngleAxis(degrees, Vector3.up) * SharedRotation; ApplyYaw(); }

        /// <summary>Sets the shared rotation of the scanner and the block (each turns about its own centre).</summary>
        public void SetShared(Quaternion rotation) { SharedRotation = rotation; ApplyYaw(); }

        /// <summary>Turns the scanner and the block together by a world-space rotation.</summary>
        public void RotateShared(Quaternion delta) { SharedRotation = delta * SharedRotation; ApplyYaw(); }

        public void ScaleItem(Transform t, float factor)
        {
            if (!homeScale.TryGetValue(t, out var home)) return;
            float ratio = t.localScale.x / home.x * factor;
            if (ratio > 0.5f && ratio < 2f) t.localScale *= factor;
        }

        public void Recenter()
        {
            Controls?.ReleaseAll();
            Layout();
        }

        void BillboardStrip(float dt)
        {
            var eye = Eye; var want = Quaternion.LookRotation(Strip.Root.position - eye, Vector3.up);
            float angle = Quaternion.Angle(Strip.Root.rotation, want);
            if (angle > 6 || stripSettling) { stripSettling = angle > 0.5f; Strip.Root.rotation = Quaternion.Slerp(Strip.Root.rotation, want, 1 - Mathf.Exp(-dt / 0.25f)); }
        }
        bool stripSettling;

        // ------------------------------------------------------------------------------------------------ lesson navigation

        LessonStep CurrentStep => Lesson.Steps.Count > 0 ? Lesson.Steps[StepIndex] : null;
        /// <summary>The current step shows repetitions only at their echoes (strobe).</summary>
        public bool Strobing => !Overview && CurrentStep != null && CurrentStep.StrobeT != null;

        void RebuildLesson(bool keepCue)
        {
            int cueIndex = Cue != null && keepCue ? Cue.Index : 0;
            Lesson.Build(lessonData, VoiceName, Sim.State.Program, Protocol.IsDefault, r => Attention.IsKnownRef(r, Attention.Elements));
            var step = CurrentStep;
            Tau = keepCue && step != null && cueIndex < step.Cues.Count ? step.Cues[cueIndex].Start : 0;
            Strip.SetTicks(step); lastCueKey = -1; StopAudio();
        }

        public void GoToStep(int index, bool play)
        {
            StepIndex = Mathf.Clamp(index, 0, Lesson.Steps.Count - 1); Tau = 0; Playing = play; Overview = !play;
            Strip.SetTicks(CurrentStep); lastCueKey = -1; StopAudio();
        }

        public void Next()
        {
            if (Lesson.Steps.Count == 0) return;
            if (StepIndex < Lesson.Steps.Count - 1) GoToStep(StepIndex + 1, true);
        }

        public void Previous()
        {
            if (Lesson.Steps.Count == 0) return;
            GoToStep(Mathf.Max(0, StepIndex - 1), true);
        }

        public void TogglePlay()
        {
            var step = CurrentStep; if (step == null) return;
            if (!Playing && Tau >= step.Len - 1e-6) { if (StepIndex < Lesson.Steps.Count - 1) Next(); else GoToStep(0, true); return; }
            Playing = !Playing; if (Playing) Overview = false;
            if (Playing) narration.UnPause(); else narration.Pause();
        }

        /// <summary>Selects the proton under the ray in the grid (pointer and trigger, or a click). Returns true on a hit.</summary>
        public bool SelectProton(Ray ray)
        {
            int i = Cube.PickProton(ray); if (i < 0) return false;
            Cube.Select(i); return true;
        }

        public void Pause() { if (Playing) { Playing = false; narration.Pause(); } }

        public bool SyncTissue = false, ShowTissue = false, PhaseColour = false, EnsembleMoments = true, ShowFields = true;
        public SpatialTools Spatial;
        readonly List<Transform> teachingHidden = new List<Transform>(); bool teachingFocused; Vector3 beforeTeachingPosition,beforeTeachingScale; Quaternion beforeTeachingRotation;
        void FocusTeaching()
        {
            if(Demonstrating==teachingFocused)return;
            teachingFocused=Demonstrating;
            if(teachingFocused) { beforeTeachingPosition=Cube.Root.position; beforeTeachingScale=Cube.Root.localScale; beforeTeachingRotation=Cube.Root.rotation; Cube.Root.rotation=dashboard*Quaternion.Euler(15,45,0); Cube.Root.position=Slot(0,-3,0.95f); Cube.Root.localScale=beforeTeachingScale*1.45f; }
            else { Cube.Root.position=beforeTeachingPosition; Cube.Root.localScale=beforeTeachingScale; Cube.Root.rotation=beforeTeachingRotation; }
            foreach(var t in teachingHidden) if(t)t.gameObject.SetActive(!teachingFocused);
        }

        public string Demo => !Overview && Cue != null ? Cue.Data.demo : null;
        public bool Demonstrating => !string.IsNullOrEmpty(Demo);
        public double DemoProgress => Cue == null ? 0 : Math.Max(0,Math.Min(1,(Tau-Cue.Start)/Math.Max(1,Cue.Voice)));


        public void Action(string action)
        {
            switch (action)
            {
                case "sync": SyncTissue = !SyncTissue; if (SyncTissue) Cube.Root.rotation = SharedRotation; break;
                case "tissue": ShowTissue = !ShowTissue; break;
                case "phase": PhaseColour = !PhaseColour; Plots.InvalidateSignal(); break;
                case "moments": EnsembleMoments = !EnsembleMoments; break;
                case "fields": ShowFields = !ShowFields; break;
                case "lesson": GoToStep(0, true); break;
                case "fullscan": GoToStep(9, true); break;
                case "receiver": GoToStep(3, true); break;
                case "prev": Previous(); break;
                case "next": Next(); break;
                case "play": TogglePlay(); break;
                case "voice":
                    VoiceName = VoiceName == "am_michael" ? "bm_george" : "am_michael";
                    lessonData = Lesson.LoadData(VoiceName); if (Sim.State != null) RebuildLesson(true); break;
                case "mode": Environment?.Toggle(); break;
                case "credits": Strip.ToggleCredits(); break;
                case "perf": ShowFrameTimes = !ShowFrameTimes; break;
                case "mute": Muted = !Muted; narration.mute = Muted; break;
                case "subtitles": Subtitles = !Subtitles; break;
            }
        }

        /// <summary>Seek used by validation and navigation: step and lesson time, applied immediately.</summary>
        public void Seek(int step, double tau, bool playing)
        {
            StepIndex = Mathf.Clamp(step, 0, Lesson.Steps.Count - 1); Tau = tau; Playing = playing; Overview = false;
            Strip.SetTicks(CurrentStep); lastCueKey = -1; StopAudio();
        }

        // ------------------------------------------------------------------------------------------------ region

        public void DragRegion(Vector3 world)
        {
            var s = Frames.ToP(Scanner.Physics.InverseTransformPoint(world));
            double r = Math.Sqrt(s.X * s.X + s.Y * s.Y), lim = 0.072 - 0.008 * Math.Sqrt(2);
            if (r > lim) { s = new D3(s.X * lim / r, s.Y * lim / r, s.Z); }
            regionDrag = new D3(Math.Round(s.X / 0.001) * 0.001, Math.Round(s.Y / 0.001) * 0.001, Protocol.SliceZ);
            Scanner.PreviewRegion(regionDrag.Value);
        }
        D3? regionDrag;

        public void CommitRegion()
        {
            if (!regionDrag.HasValue) return;
            cubeCentre = regionDrag; regionDrag = null; RequestRebuild();
        }

        /// <summary>Recomputes the simulation once the region has rested for 0.3 s (one rebuild per move).</summary>
        void RequestRebuild()
        {
            Pause(); Status = "Recalculating…"; pendingRestart = true;
            rebuildAt = Time.unscaledTime + 0.3f;
        }
        float rebuildAt = -1; int kicks;

        void StopAudio() { narration.Stop(); playingClip = null; }

        // ------------------------------------------------------------------------------------------------ frame

        void Update()
        {
            if (!Ready) return;
            long c0 = Cost.Start();
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f); frame++;
            if (rebuildAt >= 0 && Time.unscaledTime >= rebuildAt) { rebuildAt = -1; Sim.Request(Protocol, Specimen, cubeCentre); }
            if (Sim.Poll())
            {
                bool first = Lesson.Steps.Count == 0;
                if (first) { cubeCentre = Sim.State.CubeCentre; }
                RebuildLesson(!first && pendingRestart);
                if (first) GoToStep(0, false); // the narration starts by itself (0.8.3)
                pendingRestart = false; Status = null;
            }
            if (Sim.State == null) return;
            if (!laidOut) { if (HeadSettled(dt)) Layout(); else return; }
            if (Sim.State.Error != null) Status = "Simulation error";
            AdvanceClock(dt);
            ComputeTime();
            // Validation steps time exactly and evaluates spins inline; on the headset a worker evaluates them one frame behind.
            Sim.Synchronous = FixedStep;
            long c1 = Cost.Lap(c0, ref Cost.Clock);
            Sim.Update(T, evaluateHand: FixedStep || frame % 3 == 0 || Overview);
            bool fresh = Sim.SpinsReady;
            long c2 = Cost.Lap(c1, ref Cost.Sim);
            Attention.SetRefs(Overview || Cue == null ? null : Cue.Data.refs);
            Attention.Tick(dt, FixedStep && instantEmphasis);
            if (chevron)
            {
                // Shown only while a sentence names something; at rest it would be a stray mark.
                bool named = !Demonstrating && !Overview && Cue != null && Cue.Data.refs != null && Cue.Data.refs.Length > 0;
                if (chevron.gameObject.activeSelf != named) chevron.gameObject.SetActive(named);
                // The chevron points along the pointer's axis, faces the eye, and bobs gently toward its referent.
                var ptr = Attention.Pointer; Vector3 aim = ptr.up, toEye = Vector3.ProjectOnPlane(Eye - ptr.position, aim);
                if (toEye.sqrMagnitude > 1e-8f) chevron.rotation = Quaternion.LookRotation(toEye, aim);
                chevron.localPosition = Vector3.up * (0.004f * Mathf.Sin(Time.unscaledTime * 5f));
            }
            var step = CurrentStep;
            double speed = Cue != null && Cue.D > 0 && !Overview ? Cue.D : 0;
            long c3 = Cost.Lap(c2, ref Cost.Attention);
            AdvanceCarrier(dt);
            if(!Demonstrating) Scanner.Tick(dt, Playing, DisplayCarrier, fresh);
            long c4 = Cost.Lap(c3, ref Cost.Scanner);
            Cube.Tick(dt, DisplayCarrier, FixedStep && instantEmphasis, Playing, fresh);
            if(!Demonstrating) CloseUp.Tick(dt, DisplayCarrier, Playing);
            long c5 = Cost.Lap(c4, ref Cost.Cube);
            long c6 = c5;
            Plots.Tick(dt, speed);
            Spatial.Tick();
            // Foundations use the same narration clock and the same volume location.
            FocusTeaching();
            Scanner.Root.gameObject.SetActive(!Demonstrating);
            Plots.Data.gameObject.SetActive(!Demonstrating); Plots.Signal.gameObject.SetActive(!Demonstrating); Plots.Sequence.gameObject.SetActive(!Demonstrating);
            if (Demonstrating) CloseUp.HideForExample();

            Labels.Face(Eye);
            long c7 = Cost.Lap(c6, ref Cost.Console);
            Strip.Tick(StepIndex, Lesson.Steps.Count, step, Cue, Tau, Playing, Overview, Status ?? (Stalled ? "Measuring…" : null), ClockTime(), speed, Lab);
            BillboardStrip(dt);
            long c8 = Cost.Lap(c7, ref Cost.Strip);
            // Request the next spin evaluation now that this frame's views have read the last one.
            // The hand set (the scanner's glow) is refreshed every third evaluation, 24 times a second at 72 Hz (0.8.2).
            if (!Sim.Synchronous && Sim.SpinsIdle) { bool hand = Overview || ++kicks % 3 == 0; Sim.Kick(T, hand); }
            Cost.Lap(c8, ref Cost.Kick);
            Cost.End(c0, Sim.SpinMs, Sim.SpinHandMs);
        }

        void OnDestroy() { Sim?.StopWorker(); Spatial?.Dispose(); }

        public bool instantEmphasis;

        /// <summary>Physical time shown on the strip: time since the current excitation began (0 before any).</summary>
        public double ClockTime()
        {
            var prog = Sim.State?.Program; if (prog == null) return T;
            int b = prog.BlockAt(T); if (b < 0) return 0;
            var blk = prog.Blocks[b];
            return T >= blk.RfStart ? T - blk.RfStart : 0;
        }
        public static double CarrierPhase(double t, double fref) => Constants.TwoPi * (t * fref - Math.Floor(t * fref));
        double CarrierPhase(double t) => Sim.State != null ? CarrierPhase(t, Sim.State.P.Fref) : 0;

        void AdvanceCarrier(float dt)
        {
            if (Lab)
            {
                if (!wasLab) carrierOffset = DisplayCarrier - CarrierPhase(T);
                DisplayCarrier = CarrierPhase(T) + carrierOffset;
            }
            else if (Playing) DisplayCarrier += (FixedStep ? 1.0 / 72.0 : dt) * Constants.TwoPi / Lesson.CarrierTurnSeconds;
            DisplayCarrier = Math.IEEERemainder(DisplayCarrier, Constants.TwoPi);
            wasLab = Lab;
        }

        void AdvanceClock(float dt)
        {
            var step = CurrentStep; if (step == null) return;
            Stalled = false;
            if (Playing)
            {
                // Stall: never show k-space or images beyond what the acquisition has computed.
                if (NeedsRows(step, Tau + dt) && !RowsReady(step, Tau + dt)) { Stalled = true; return; }
                Tau += FixedStep ? 1.0 / 72.0 : dt;
                if (!FixedStep && narration.isPlaying && Cue != null && playingClip == Cue.Clip)
                {
                    double ta = Cue.Start + Cue.Lead + narration.time;
                    double e = ta - Tau; Tau = Math.Abs(e) > 0.030 ? ta : Tau + 0.1 * e;
                }
                if (Tau >= step.Len)
                {
                    // The narration flows on to the next step by itself; it stops only after the last one.
                    if (StepIndex < Lesson.Steps.Count - 1 && !FixedStep) { GoToStep(StepIndex + 1, true); return; }
                    Tau = step.Len; Playing = false; Overview = true; StopAudio();
                }
            }
        }

        bool NeedsRows(LessonStep step, double tau) => step.StrobeT != null || (Cue != null && Sim.State.Program.BlockAt(Cue.T1) >= 0 && Sim.State.Program.Blocks[Sim.State.Program.BlockAt(Cue.T1)].Selective);

        bool RowsReady(LessonStep step, double tau)
        {
            var s = Sim.State; double t = Lesson.PhysicalAt(step, Math.Min(tau, step.Len), out _, out _);
            int b = s.Program.BlockAt(t); if (b < 0) return true;
            var blk = s.Program.Blocks[b]; if (!blk.Selective) return true;
            var acq = s.Slices[blk.Slice]; int order = s.Program.SliceBlocks[blk.Slice].IndexOf(b);
            return acq.Done || acq.RowsComputed > order;
        }

        void ComputeTime()
        {
            var step = CurrentStep; if (step == null) return;
            T = Lesson.PhysicalAt(step, Tau, out var cue, out bool lab);
            if (Overview && Tau <= 0) { T = step.Cues[0].Cut ? (StepIndex > 0 ? Lesson.Steps[StepIndex - 1].ExitT : 0) : step.Cues[0].T0; lab = false; }
            Cue = cue; Lab = lab && !Overview;
            // Narration audio for the current cue.
            int key = StepIndex * 1000 + cue.Index;
            if (Playing && !FixedStep && key != lastCueKey && Tau >= cue.Start + cue.Lead)
            {
                lastCueKey = key; StopAudio();
                if (!string.IsNullOrEmpty(cue.Clip))
                {
                    var clip = Resources.Load<AudioClip>(cue.Clip);
                    if (clip) { narration.clip = clip; narration.time = Mathf.Clamp((float)(Tau - cue.Start - cue.Lead), 0, clip.length - 0.01f); narration.Play(); playingClip = cue.Clip; }
                }
            }
        }
    }
}
