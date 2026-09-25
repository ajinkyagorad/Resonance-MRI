using System.Collections.Generic;
using Nebulytic.Resonance.Sim;
using TMPro;
using UnityEngine;

namespace Nebulytic.Resonance
{
    /// <summary>
    /// The plots (0.8.4, USER-REVIEW-0.8.3: "opaque and bright, thick lines, a solid dark backing and large labels", at
    /// least 0.5.0's quality). Each stands on a solid dark rounded panel whose rim lights when the narration names it:
    ///   sequence - the coil currents over time, one thick trace per coil in its colour (RF envelope, Gz, Gy, Gx) and the ADC
    ///              window, with the present moment as a line through all rows and live values; the window pages forward
    ///              like an oscilloscope, so the traces are rebuilt only when it moves;
    ///   signal   - the received samples of the current readout as a 3-D line in time, I and Q (a helix while the phase
    ///              turns), coloured by its phase like the protons, with its I and Q shadows and the RF voltage v(t) above
    ///              (carrier drawn slowed); built once per readout and revealed as samples arrive (shader clip), joined to
    ///              the receiver on the scanner's base;
    ///   data     - k-space as a solid relief (height and colour = log |S|, as in 0.5.0), revealed sample by sample, beside
    ///              the reconstructed image and the stacked slices as a 3-D image turned like the scanner.
    /// The k-space labels and axes appear only once k-space holds a measured row: nothing names a quantity that is not shown.
    /// </summary>
    public sealed class PlotsView
    {
        readonly App app;
        public readonly Transform Data, Signal, Sequence;
        readonly LineBuilder lb = new LineBuilder();
        readonly Material seqRim, sigRim, dataRim;
        // Sequence.
        readonly Mesh seqMesh = new Mesh { name = "Sequence traces" }; readonly Material seqMat; Transform seqCursor;
        readonly TextMeshPro[] seqValues = new TextMeshPro[5]; readonly TextMeshPro seqSpanLabel;
        double seqT0 = double.NaN, seqSpan = 1.2e-3; int seqRevision = -1; float nextValues;
        public const float SeqW = 0.215f, SeqX0 = -0.135f, SeqAmp = 0.0135f;
        static Vector3 SeqRow(int k) => new Vector3(0, 0.058f - 0.037f * k, 0);
        // Signal.
        readonly Mesh sigMesh = new Mesh { name = "Signal lines" }; readonly Material sigMat; readonly Transform sigTip, sigLink; readonly Renderer sigLinkR, sigTipR;
        readonly TextMeshPro sigValue; double[] sigTimes; double[] sigRe = new double[0], sigIm = new double[0]; double sigRef; int sigBlock = -2, sigRevision = -1, sigCount; bool sigEmpty; float shownReveal = float.NaN;
        public const float SigW = 0.28f, SigA = 0.034f; static readonly Vector3 SigO = new Vector3(-0.125f, -0.042f, 0);
        // k-space relief.
        readonly Mesh kMesh = new Mesh { name = "k-space relief" }; readonly Material kMat; readonly Transform kRow, kTilt; readonly Renderer kRowR;
        int kSlice = -1, kRevision = -1, kRows = -1; float nextK; float kShownReveal = float.NaN; readonly TextMeshPro kStatus; readonly GameObject kLabels;
        Vector3[] kVerts; Vector2[] kUv; int[] kTris; int kN = -1; bool kTrisSet;
        public const float KW = 0.17f, KH = 0.048f; float nextStatus;
        // Image and 3-D image.
        readonly Renderer imageQuad; readonly Transform reconBox; readonly List<Renderer> reconLayers = new List<Renderer>(); Texture2D imageTex;
        readonly TextMeshPro imageLabel;
        double imageReference; int shownRevision = -1, lastSlice = -1; readonly Dictionary<int, int> sliceRows = new Dictionary<int, int>(); int[] zRank = new int[0];
        public Element eSeq, eSig, eKspace, eKrow, eImage, eRecon;

        public void InvalidateSignal() { sigRevision = -1; }

        public PlotsView(App app, Transform parent)
        {
            this.app = app; var att = app.Attention;
            // ---------------------------------------------------------------- sequence
            Sequence = new GameObject("Pulse sequence").transform; Sequence.SetParent(parent, false);
            seqRim = Backing(Sequence, 0.42f, 0.275f, new Vector3(0, -0.006f, 0));
            Labels.Make(Sequence, "Pulse sequence", new Vector3(-0.19f, 0.104f, 0), Look.TitleSize, Look.TEXT, TextAlignmentOptions.Left, 0.3f, false);
            seqMat = Mats.Line(Color.white);
            Mats.Object("Sequence traces", Sequence, seqMesh, seqMat);
            string[] names = { "RF", "Gz", "Gy", "Gx", "ADC" }; Color[] cols = { Look.RF, Look.GZ, Look.GY, Look.GX, Look.MAG };
            var baseB = new LineBuilder();
            for (int k = 0; k < 5; k++)
            {
                var r = SeqRow(k);
                baseB.Segment(r + new Vector3(SeqX0, 0, 0.002f), r + new Vector3(SeqX0 + SeqW, 0, 0.002f), 0.0012f, Look.TRACK);
                Labels.Make(Sequence, names[k], r + new Vector3(-0.17f, 0, 0), Look.LabelSize, cols[k], TextAlignmentOptions.Center, 0.3f, false);
                seqValues[k] = Labels.Make(Sequence, "", r + new Vector3(0.15f, 0, 0), Look.ValueSize, Look.TEXT, TextAlignmentOptions.Center, 0.1f, false);
            }
            Mats.Object("Sequence baselines", Sequence, baseB.Commit(new Mesh { name = "Sequence baselines" }), Mats.Line(Color.white));
            var cur = new LineBuilder(); cur.Segment(SeqRow(0) + new Vector3(0, 0.022f, -0.002f), SeqRow(4) + new Vector3(0, -0.018f, -0.002f), 0.0026f, Color.white);
            seqCursor = Mats.Object("Now", Sequence, cur.Commit(new Mesh { name = "Sequence cursor" }), Mats.Line(Look.MAG, false, 0.35f)).transform;
            seqSpanLabel = Labels.Make(Sequence, "", SeqRow(4) + new Vector3(SeqX0 + SeqW / 2, -0.031f, 0), Look.NoteSize, Look.TEXT2, TextAlignmentOptions.Center, 0.3f, false);
            eSeq = att.Register("sequence", () => Sequence.TransformPoint(new Vector3(0, 0.135f, 0)), "plots", "data");
            AddGrab(Sequence, new Vector3(0.42f, 0.275f, 0.05f), "sequence");

            // ---------------------------------------------------------------- signal
            Signal = new GameObject("Received signal").transform; Signal.SetParent(parent, false);
            sigRim = Backing(Signal, 0.42f, 0.30f, new Vector3(0, -0.005f, 0), 0.06f);
            Labels.Make(Signal, "Received signal", new Vector3(-0.19f, 0.117f, 0.05f), Look.TitleSize, Look.TEXT, TextAlignmentOptions.Left, 0.3f, false);
            var sigTilt = new GameObject("Signal axes").transform; sigTilt.SetParent(Signal, false); sigTilt.localRotation = Quaternion.Euler(10, -20, 0);
            sigMat = Mats.Line(Color.white, false, 0.25f);
            Mats.Object("Signal lines", sigTilt, sigMesh, sigMat);
            var ax = new LineBuilder(); Vector3 o = SigO;
            ax.Arrow(o, o + new Vector3(SigW + 0.025f, 0, 0), Look.AxisWidth, Color.white, 0.05f);
            ax.Arrow(o, o + new Vector3(0, SigA * 1.3f, 0), Look.AxisWidth, Color.white, 0.14f); ax.Segment(o, o - new Vector3(0, SigA * 1.1f, 0), 0.0014f, Look.TRACK);
            ax.Arrow(o, o + new Vector3(0, 0, -SigA * 1.3f), Look.AxisWidth, Color.white, 0.14f); ax.Segment(o, o + new Vector3(0, 0, SigA * 1.1f), 0.0014f, Look.TRACK);
            ax.Segment(new Vector3(o.x, 0.062f, 0), new Vector3(o.x + SigW, 0.062f, 0), 0.0012f, Look.TRACK);
            Mats.Object("Signal axes", sigTilt, ax.Commit(new Mesh { name = "Signal axes" }), Mats.Line(Color.white, false, 0.3f));
            Labels.Make(sigTilt, "t", o + new Vector3(SigW + 0.045f, 0, 0), Look.LabelSize, Look.TEXT);
            Labels.Make(sigTilt, "I", o + new Vector3(0, SigA * 1.3f + 0.02f, 0), Look.LabelSize, Look.TEXT);
            Labels.Make(sigTilt, "Q", o + new Vector3(0, 0, -SigA * 1.3f - 0.02f), Look.LabelSize, Look.TEXT);
            Labels.Make(sigTilt, "v(t)", new Vector3(o.x - 0.04f, 0.062f, 0), Look.LabelSize, Look.RF);
            sigTip = Mats.Object("Latest sample", sigTilt, MeshKit.Sphere(0.0065f, 12, 8), Mats.Solid(Look.MAG)).transform; sigTipR = sigTip.GetComponent<Renderer>(); sigTip.gameObject.SetActive(false);
            sigValue = Labels.Make(Signal, "", new Vector3(0, -0.12f, 0.05f), Look.ValueSize, Look.TEXT, TextAlignmentOptions.Center, 0.4f, false);
            var ll = new LineBuilder(); ll.Segment(Vector3.zero, Vector3.forward, 0.0028f, Color.white);
            var lm = Mats.Line(Look.MAG, false, 0.35f); lm.SetFloat("_ScaleWidth", 0);
            sigLink = Mats.Object("Receiver link", parent, ll.Commit(new Mesh { name = "Unit segment" }), lm).transform; sigLinkR = sigLink.GetComponent<Renderer>();
            eSig = att.Register("signal.trace", () => Signal.TransformPoint(new Vector3(0, 0.15f, 0)), "plots", "data", "signal");
            AddGrab(Signal, new Vector3(0.42f, 0.30f, 0.14f), "signal");

            // ---------------------------------------------------------------- data: k-space relief, image, 3-D image
            Data = new GameObject("Acquired data").transform; Data.SetParent(parent, false);
            dataRim = Backing(Data, 0.48f, 0.30f, new Vector3(0, -0.005f, 0), 0.075f);
            Labels.Make(Data, "Measured data and image", new Vector3(-0.22f, 0.117f, 0.065f), Look.TitleSize, Look.TEXT, TextAlignmentOptions.Left, 0.4f, false);
            kTilt = new GameObject("k-space").transform; kTilt.SetParent(Data, false); kTilt.localPosition = new Vector3(-0.115f, -0.02f, 0); kTilt.localRotation = Quaternion.Euler(-40, 0, 0);
            kMat = Mats.Relief(); Mats.Object("k-space relief", kTilt, kMesh, kMat);
            // Axes and names of k-space: shown once a row is measured.
            kLabels = new GameObject("k-space labels"); kLabels.transform.SetParent(kTilt, false);
            var kFrame = new LineBuilder(); float h = KW / 2;
            kFrame.Arrow(new Vector3(-h, 0, -h - 0.012f), new Vector3(h + 0.02f, 0, -h - 0.012f), Look.AxisWidth, Color.white, 0.08f);
            kFrame.Arrow(new Vector3(-h - 0.012f, 0, -h), new Vector3(-h - 0.012f, 0, h + 0.02f), Look.AxisWidth, Color.white, 0.08f);
            kFrame.Arrow(new Vector3(-h, 0, -h), new Vector3(-h, KH * 1.35f, -h), Look.AxisWidth, Color.white, 0.15f);
            Labels.Make(kLabels.transform, "log |S|", new Vector3(-h, KH * 1.65f, -h), Look.NoteSize, Look.TEXT);
            Mats.Object("k-space axes", kLabels.transform, kFrame.Commit(new Mesh { name = "k-space axes" }), Mats.Line(Color.white, false, 0.3f));
            Labels.Make(kLabels.transform, "kₓ", new Vector3(h + 0.04f, 0, -h - 0.012f), Look.LabelSize, Look.AX);
            Labels.Make(kLabels.transform, "kᵧ", new Vector3(-h - 0.012f, 0, h + 0.045f), Look.LabelSize, Look.AY);
            // Where k-space will grow: its base square, faint, from the start (it carries no name until a row is measured).
            var kBase = new LineBuilder();
            kBase.Segment(new Vector3(-h, 0, -h), new Vector3(h, 0, -h), 0.0016f, Color.white); kBase.Segment(new Vector3(h, 0, -h), new Vector3(h, 0, h), 0.0016f, Color.white);
            kBase.Segment(new Vector3(h, 0, h), new Vector3(-h, 0, h), 0.0016f, Color.white); kBase.Segment(new Vector3(-h, 0, h), new Vector3(-h, 0, -h), 0.0016f, Color.white);
            Mats.Object("k-space base", kTilt, kBase.Commit(new Mesh { name = "k-space base" }), Mats.Line(Look.TRACK * 1.8f));
            var rowB = new LineBuilder(); rowB.Segment(new Vector3(-h, 0.004f, 0), new Vector3(h, 0.004f, 0), 0.0034f, Color.white);
            kRow = Mats.Object("Row being read", kTilt, rowB.Commit(new Mesh { name = "k-space row" }), Mats.Line(Look.MAG, false, 0.35f)).transform; kRowR = kRow.GetComponent<Renderer>();
            kStatus = Labels.Make(Data, "", new Vector3(-0.115f, -0.128f, 0.065f), Look.NoteSize, Look.TEXT2, TextAlignmentOptions.Center, 0.3f, false);
            kLabels.SetActive(false);
            // The image: opaque, through the violet-to-white map.
            imageQuad = Mats.Object("Image", Data, MeshKit.Quad(0.1f, 0.1f), Mats.Image(null, true), new Vector3(0.06f, 0.005f, 0.066f)).GetComponent<Renderer>();
            imageLabel = Labels.Make(Data, "image", new Vector3(0.06f, -0.09f, 0.065f), Look.NoteSize, Look.TEXT2, TextAlignmentOptions.Center, 0.14f, false);
            {
                // The image's frame, visible before the first row arrives.
                var fb = new LineBuilder(); const float q = 0.0515f; var c0 = new Vector3(0.06f, 0.005f, 0.065f);
                fb.Segment(c0 + new Vector3(-q, -q, 0), c0 + new Vector3(q, -q, 0), 0.0016f, Color.white); fb.Segment(c0 + new Vector3(q, -q, 0), c0 + new Vector3(q, q, 0), 0.0016f, Color.white);
                fb.Segment(c0 + new Vector3(q, q, 0), c0 + new Vector3(-q, q, 0), 0.0016f, Color.white); fb.Segment(c0 + new Vector3(-q, q, 0), c0 + new Vector3(-q, -q, 0), 0.0016f, Color.white);
                Mats.Object("Image frame", Data, fb.Commit(new Mesh { name = "Image frame" }), Mats.Line(Look.TRACK * 1.8f));
            }
            // The 3-D image: the stacked slices in a box turned like the scanner (transparent where dark), with white edges.
            var disp = new GameObject("3-D image").transform; disp.SetParent(Data, false); disp.localPosition = new Vector3(0.172f, 0.005f, -0.03f);
            reconBox = new GameObject("Volume").transform; reconBox.SetParent(disp, false);
            Labels.Make(Data, "3-D image", new Vector3(0.172f, -0.09f, 0.065f), Look.NoteSize, Look.TEXT2, TextAlignmentOptions.Center, 0.14f, false);
            eKspace = att.Register("kspace", () => kTilt.position + Data.up * 0.09f, "plots", "data");
            eKrow = att.Register("kspace.row", () => kRow.position + Data.up * 0.03f, "plots", "data");
            eImage = att.Register("image", () => imageQuad.transform.position + Data.up * 0.09f, "plots", "data");
            eRecon = att.Register("recon", () => disp.position + Data.up * 0.06f, "plots", "data", "image.slabs");
            AddGrab(Data, new Vector3(0.48f, 0.30f, 0.16f), "data");
        }

        static Material Backing(Transform t, float w, float h, Vector3 centre, float zFace = 0.012f)
        {
            // A short freestanding focus underline; no backing surface.
            var lb = new LineBuilder();
            lb.Segment(centre + new Vector3(-w * 0.45f, h * 0.5f, 0), centre + new Vector3(-w * 0.27f, h * 0.5f, 0), 0.003f, Color.white);
            var mat = Mats.Line(Look.TEXT2, false, 0.22f);
            Mats.Object("Focus underline", t, lb.Commit(new Mesh { name = "Focus underline" }), mat);
            return mat;
        }

        static void AddGrab(Transform t, Vector3 size, string kind)
        {
            var col = t.gameObject.AddComponent<BoxCollider>(); col.size = size; col.isTrigger = true;
            t.gameObject.AddComponent<Grabbable>().Kind = kind;
        }

        void OnState(SimState s)
        {
            shownRevision = s.Revision; imageReference = 0; int n = s.P.Matrix;
            if (imageTex == null || imageTex.width != n)
            {
                imageTex = new Texture2D(n, n, TextureFormat.R8, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Image" };
                imageQuad.sharedMaterial.SetTexture("_MainTex", imageTex);
            }
            imageTex.SetPixelData(new byte[n * n], 0); imageTex.Apply(false);
            int nz = s.Slices.Length;
            // Slices are stored in acquisition order (the lesson's slice first); the 3-D image stacks them by position.
            zRank = new int[nz];
            for (int k = 0; k < nz; k++) { int r = 0; for (int j = 0; j < nz; j++) if (s.Slices[j].Z < s.Slices[k].Z) r++; zRank[k] = r; }
            foreach (var r in reconLayers) Object.Destroy(r.gameObject);
            reconLayers.Clear();
            foreach (Transform c in reconBox) Object.Destroy(c.gameObject);
            double zmin = double.MaxValue, zmax = double.MinValue; foreach (var a in s.Slices) { zmin = System.Math.Min(zmin, a.Z); zmax = System.Math.Max(zmax, a.Z); }
            float zs = (float)(zmax - zmin); float scale = 0.10f / Mathf.Max((float)Protocol.Fov, zs);
            // Layers share the scanner's slab textures (the same reconstruction), one quad per slice at its position.
            for (int k = 0; k < nz; k++)
            {
                var q = Mats.Object("Slice " + k, reconBox, MeshKit.Quad((float)Protocol.Fov * scale, (float)Protocol.Fov * scale), Mats.Image(app.Scanner.SlabTexture(k), false), Frames.ToS(0, 0, (s.Slices[k].Z - 0.5 * (zmin + zmax)) * scale));
                reconLayers.Add(q.GetComponent<Renderer>());
            }
            var edges = new LineBuilder(); Vector3 hb = new Vector3((float)Protocol.Fov * scale, (float)Protocol.Fov * scale, zs * scale + 0.004f) / 2;
            for (int a = 0; a < 3; a++)
                for (int s1 = -1; s1 <= 1; s1 += 2)
                    for (int s2 = -1; s2 <= 1; s2 += 2)
                    {
                        int b = (a + 1) % 3, d = (a + 2) % 3; Vector3 p = Vector3.zero, q; p[b] = s1 * hb[b]; p[d] = s2 * hb[d]; q = p; p[a] = -hb[a]; q[a] = hb[a];
                        edges.Segment(p, q, 0.0016f, Color.white);
                    }
            Mats.Object("Edges", reconBox, edges.Commit(new Mesh { name = "3-D image edges" }), Mats.Line(Look.TEXT2, false, 0.3f));
            sliceRows.Clear(); lastSlice = -1; kSlice = -1; kN = -1;
        }

        /// <summary>Per frame (main thread): plots from the simulation at physical time t.</summary>
        public void Tick(float dt, double speed)
        {
            var sim = app.Sim; var s = sim.State; if (s == null) return;
            if (s.Revision != shownRevision) OnState(s);
            double t = sim.T;
            BuildSequence(s, t, speed);
            BuildSignal(s, t);
            int slice = UpdateImages(s, t);
            BuildKspace(s, t, slice);
            reconBox.rotation = app.Scanner.Physics.rotation;
            // Emphasis: the named plot's rim lights; the traces themselves stay at full brightness.
            seqRim.SetColor("_Color", Color.Lerp(Look.TEXT2, Look.TEXT, Named(eSeq)));
            sigRim.SetColor("_Color", Color.Lerp(Look.TEXT2, Look.TEXT, Named(eSig)));
            float ed = Mathf.Max(Mathf.Max(Named(eKspace), Named(eKrow)), Mathf.Max(Named(eImage), Named(eRecon)));
            dataRim.SetColor("_Color", Color.Lerp(Look.TEXT2, Look.TEXT, ed));
            // Receiver link: from the base's receiver to the signal plot, bright while samples are taken.
            Vector3 a = app.Scanner.ReceiverPort, b = Signal.TransformPoint(new Vector3(-0.212f, 0.02f, 0.05f)); Vector3 ab = b - a;
            if (ab.sqrMagnitude > 1e-6f) { sigLink.position = a; sigLink.rotation = Quaternion.LookRotation(ab); sigLink.localScale = new Vector3(1, 1, ab.magnitude); }
            sigLinkR.sharedMaterial.SetColor("_Color", sim.SignalOn && AdcOn(s.Program, t) ? Look.MAG : Look.TRACK * 1.6f);
            if (Time.unscaledTime >= nextValues || app.FixedStep) { nextValues = Time.unscaledTime + 0.15f; UpdateValues(s, sim, t); }
        }

        /// <summary>1 while a sentence names something (outside the overview), else 0: a rim lights only while its plot is named.</summary>
        float Named(Element e) => app.Attention.Refs != null ? e.E : 0;

        // ------------------------------------------------------------------------------------------------ sequence

        void BuildSequence(SimState s, double now, double speed)
        {
            var prog = s.Program; double t0 = seqT0, span = seqSpan;
            int bi = app.Sim.BlockIndex;
            if (app.Strobing && bi >= 0) { var blk = prog.Blocks[bi]; t0 = blk.RfStart - 0.3e-3; span = blk.End - blk.RfStart + 0.6e-3; }
            else
            {
                // About 12 s of display time at the lesson's slowing, in octave steps, paged forward when the present nears the end.
                double target = speed > 0 ? System.Math.Max(40e-6, System.Math.Min(0.14, 12.0 / speed)) : span;
                if (System.Math.Abs(System.Math.Log(target / span, 2)) > 0.75) { span = System.Math.Pow(2, System.Math.Round(System.Math.Log(target, 2))); t0 = double.NaN; }
                if (double.IsNaN(t0) || now < t0 || now > t0 + 0.85 * span) t0 = now - 0.15 * span;
            }
            if (t0 != seqT0 || span != seqSpan || s.Revision != seqRevision)
            {
                seqT0 = t0; seqSpan = span; seqRevision = s.Revision;
                const int N = 220; const double imax = 70, aNorm = 1.0 / 0.75; // currents on +/-70 A; RF drive on 0.75 A
                Color[] cols = { Look.RF, Look.GZ, Look.GY, Look.GX, Look.MAG };
                var prev = new Vector3[5];
                for (int k = 0; k <= N; k++)
                {
                    double tt = t0 + span * k / N; float x = SeqX0 + SeqW * k / N;
                    prog.CurrentsAt(tt, out double ix, out double iy, out double iz);
                    prog.RfAt(tt, out double re, out double im); double a = System.Math.Sqrt(re * re + im * im);
                    float[] v = { (float)System.Math.Min(1, a * aNorm), (float)Clamp1(iz / imax), (float)Clamp1(iy / imax), (float)Clamp1(ix / imax), AdcOn(prog, tt) ? 0.8f : 0 };
                    for (int c = 0; c < 5; c++)
                    {
                        var p = SeqRow(c) + new Vector3(x, v[c] * SeqAmp, 0);
                        if (k > 0) lb.Segment(prev[c], p, Look.TraceWidth, cols[c]);
                        prev[c] = p;
                    }
                }
                lb.Commit(seqMesh);
                Labels.Set(seqSpanLabel, $"t {Duration(t0)} … {Duration(t0 + span)}");
            }
            float f = Mathf.Clamp01((float)((now - seqT0) / seqSpan));
            seqCursor.localPosition = new Vector3(SeqX0 + SeqW * f, 0, 0);
        }

        static double Clamp1(double v) => System.Math.Max(-1, System.Math.Min(1, v));
        static string Duration(double t) => System.Math.Abs(t) < 1e-3 ? $"{t * 1e6:0} µs" : System.Math.Abs(t) < 1 ? $"{t * 1e3:0.00} ms" : $"{t:0.000} s";

        public static bool AdcOn(SeqProgram prog, double t)
        {
            int b = prog.BlockAt(t); if (b < 0) return false;
            var a = prog.Blocks[b].Adc; if (a.Length == 0) return false;
            double dwell = a.Length > 1 ? a[1] - a[0] : 0;
            return t >= a[0] - dwell / 2 && t <= a[a.Length - 1] + dwell / 2;
        }

        void UpdateValues(SimState s, Simulation sim, double t)
        {
            double a = System.Math.Sqrt(sim.RfRe * sim.RfRe + sim.RfIm * sim.RfIm);
            Labels.Set(seqValues[0], sim.RfOn ? $"{a:0.00} A" : "off");
            Labels.Set(seqValues[1], G(sim.Iz * Scanner.EtaZ));
            Labels.Set(seqValues[2], G(sim.Iy * Scanner.EtaY));
            Labels.Set(seqValues[3], G(sim.Ix * Scanner.EtaX));
            Labels.Set(seqValues[4], AdcOn(s.Program, t) ? "on" : "off");
            if (sigTimes != null && sigCount > 0)
            {
                int j = LastSample(t);
                if (j >= 0) Labels.Set(sigValue, $"I {sigRe[j] / sigRef:+0.00;-0.00;0.00}   Q {sigIm[j] / sigRef:+0.00;-0.00;0.00}   sample {j + 1} of {sigTimes.Length}");
                else Labels.Set(sigValue, $"{sigTimes.Length} samples to come");
            }
            else Labels.Set(sigValue, "no samples yet");
        }

        static string G(double tpm) => System.Math.Abs(tpm) < 5e-5 ? "0" : $"{tpm * 1e3:+0.0;-0.0} mT/m";

        // ------------------------------------------------------------------------------------------------ signal

        int LastSample(double now)
        {
            if (sigTimes == null || sigTimes.Length == 0 || now < sigTimes[0]) return -1;
            int lo = 0, hi = sigTimes.Length - 1, j = 0;
            while (lo <= hi) { int mid = (lo + hi) >> 1; if (sigTimes[mid] <= now) { j = mid; lo = mid + 1; } else hi = mid - 1; }
            return j;
        }

        void BuildSignal(SimState s, double now)
        {
            int bi = app.Sim.BlockIndex;
            if (bi != sigBlock || s.Revision != sigRevision)
            {
                double[] re = null, im = null; double reference = 0; bool ready = true;
                var blk = bi >= 0 ? s.Program.Blocks[bi] : null;
                if (blk != null && blk.Adc.Length > 0)
                {
                    if (!blk.Selective && s.HandRecords.TryGetValue(bi, out var rec)) { re = rec.Re; im = rec.Im; reference = rec.Reference; }
                    else if (blk.Selective && blk.Row >= 0)
                    {
                        var acq = s.Slices[blk.Slice]; int n = acq.N;
                        ready = !double.IsPositiveInfinity(acq.RowTime[blk.Row]); // computed in the background after a change
                        if (ready)
                        {
                            if (sigRe.Length != n) { sigRe = new double[n]; sigIm = new double[n]; }
                            for (int j = 0; j < n; j++) { int k = blk.Row * n + j; sigRe[j] = acq.KRe[k]; sigIm[j] = acq.KIm[k]; }
                            re = sigRe; im = sigIm; reference = acq.Reference;
                        }
                    }
                }
                if (ready) { sigBlock = bi; sigRevision = s.Revision; }
                sigTimes = null; sigCount = 0;
                if (re != null && reference > 0)
                {
                    if (!ReferenceEquals(re, sigRe)) { if (sigRe.Length != re.Length) { sigRe = new double[re.Length]; sigIm = new double[re.Length]; } System.Array.Copy(re, sigRe, re.Length); System.Array.Copy(im, sigIm, im.Length); }
                    sigRef = reference; sigTimes = blk.Adc; sigCount = re.Length;
                    int m = re.Length, step = System.Math.Max(1, m / 220); float sc = SigA / 1.05f; Vector3 o = SigO;
                    var adc = blk.Adc; Vector3 pv = default, pc = default, pi = default, pq = default; bool have = false;
                    const int carrierCycles = 12, sub = 6;
                    var shadow = Look.TEXT3;
                    for (int j = 0; j < m; j += step)
                    {
                        float x = o.x + SigW * j / System.Math.Max(1, m - 1);
                        float yi = Mathf.Clamp((float)(sigRe[j] / reference) * sc, -SigA * 1.2f, SigA * 1.2f), zq = Mathf.Clamp((float)(sigIm[j] / reference) * sc, -SigA * 1.2f, SigA * 1.2f);
                        // Q toward the viewer (-z): the I-Q plane is seen as the dial on the scanner shows it.
                        var p = new Vector3(x, o.y + yi, -zq); var shadowI = new Vector3(x, o.y + yi, SigA * 1.35f); var shadowQ = new Vector3(x, o.y - SigA * 1.35f, -zq);
                        float key = (float)(adc[j] - adc[0]);
                        if (have)
                        {
                            float k0 = (float)(adc[j - step] - adc[0]);
                            // The trace in the protons' colours: hue = the signal's phase, brightness = its size.
                            double mag = System.Math.Sqrt(sigRe[j] * sigRe[j] + sigIm[j] * sigIm[j]) / reference;
                            var col = app.PhaseColour ? Look.Phase(System.Math.Atan2(sigIm[j], sigRe[j]), 0.55f + 0.45f * Mathf.Clamp01((float)mag * 2)) : Look.GX;
                            lb.Segment(pc, p, Look.TraceWidth * 1.15f, col, 0, 0, k0, key);
                            lb.Segment(pi, shadowI, 0.0022f, shadow, 0, 0, k0, key);
                            lb.Segment(pq, shadowQ, 0.0022f, shadow, 0, 0, k0, key);
                            // v(t): I cos(wt) - Q sin(wt) with the carrier slowed to 12 cycles over the readout.
                            for (int q = 1; q <= sub; q++)
                            {
                                float u = q / (float)sub; int jj = j - step; double tt = (jj + u * step) / (double)System.Math.Max(1, m - 1);
                                double ii = Lerp(sigRe[jj], sigRe[j], u) / reference, qq = Lerp(sigIm[jj], sigIm[j], u) / reference, w = 2 * System.Math.PI * carrierCycles * tt;
                                float xv = o.x + SigW * (float)tt, v = (float)(ii * System.Math.Cos(w) - qq * System.Math.Sin(w)) * sc * 0.5f;
                                var pn = new Vector3(xv, 0.062f + Mathf.Clamp(v, -0.022f, 0.022f), 0);
                                lb.Segment(pv, pn, 0.0034f, Look.RF, 0, 0, Mathf.Lerp(k0, key, u - 1f / sub), Mathf.Lerp(k0, key, u));
                                pv = pn;
                            }
                        }
                        else pv = new Vector3(x, 0.062f + (float)(sigRe[j] / reference) * sc * 0.5f, 0);
                        pc = p; pi = shadowI; pq = shadowQ; have = true;
                    }
                    lb.Commit(sigMesh); sigEmpty = false;
                }
                else if (!sigEmpty) { lb.Commit(sigMesh); sigEmpty = true; }
                shownReveal = float.NaN;
            }
            int last = LastSample(now);
            float reveal = last >= 0 ? (float)(sigTimes[last] - sigTimes[0]) + 1e-9f : -1;
            if (reveal != shownReveal) { sigMat.SetFloat("_Reveal", reveal); shownReveal = reveal; }
            if (last >= 0)
            {
                sigTip.gameObject.SetActive(true);
                float x = SigO.x + SigW * last / System.Math.Max(1, sigTimes.Length - 1), sc = SigA / 1.05f;
                sigTip.localPosition = new Vector3(x, SigO.y + Mathf.Clamp((float)(sigRe[last] / sigRef) * sc, -SigA * 1.2f, SigA * 1.2f), -Mathf.Clamp((float)(sigIm[last] / sigRef) * sc, -SigA * 1.2f, SigA * 1.2f));
                sigTipR.sharedMaterial.SetColor("_Color", app.PhaseColour ? Look.Phase(System.Math.Atan2(sigIm[last], sigRe[last]), 1f) : Look.GX);
            }
            else if (sigTip.gameObject.activeSelf) sigTip.gameObject.SetActive(false);
        }

        static double Lerp(double a, double b, float u) => a + (b - a) * u;

        // ------------------------------------------------------------------------------------------------ k-space

        /// <summary>
        /// k-space of the shown slice as a solid relief: kx across, ky in depth, height and colour log |S|. The surface's
        /// heights are updated when the slice changes or the background acquisition has computed more rows (at most twice a
        /// second); rows not yet computed are clipped, and samples are revealed up to now by the shader.
        /// </summary>
        void BuildKspace(SimState s, double t, int slice)
        {
            var acq = s.Slices[slice]; int n = acq.N;
            bool changed = slice != kSlice || s.Revision != kRevision || acq.RowsComputed != kRows && (Time.unscaledTime >= nextK || app.FixedStep);
            if (changed)
            {
                kSlice = slice; kRevision = s.Revision; kRows = acq.RowsComputed; nextK = Time.unscaledTime + 0.5f;
                // One strip per row (0.8.4): a row reads on its own as soon as it is measured, and the measured rows together
                // form the relief. Each sample has a front and a back vertex, one row-spacing apart.
                if (kN != n)
                {
                    kN = n; kVerts = new Vector3[2 * n * n]; kUv = new Vector2[2 * n * n]; var tris = new List<int>();
                    for (int p = 0; p < n; p++)
                        for (int j = 0; j < n - 1; j++)
                        {
                            int a = 2 * (p * n + j); // a: front of sample j, a + 1: its back; a + 2, a + 3: sample j + 1
                            tris.Add(a); tris.Add(a + 1); tris.Add(a + 2); tris.Add(a + 2); tris.Add(a + 1); tris.Add(a + 3);
                        }
                    kTris = tris.ToArray(); kMesh.Clear(); kMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; kTrisSet = false;
                }
                double max = System.Math.Max(1e-30, acq.MaxAbs); float h = KW / 2, dz = KW / n;
                for (int p = 0; p < n; p++)
                {
                    bool have = !double.IsPositiveInfinity(acq.RowTime[p]);
                    float z = -h + KW * (p + 0.5f) / n;
                    for (int j = 0; j < n; j++)
                    {
                        int k = p * n + j; double mag = have ? System.Math.Sqrt(acq.KRe[k] * acq.KRe[k] + acq.KIm[k] * acq.KIm[k]) / max : 0;
                        float u = (float)System.Math.Max(0, System.Math.Log10(1 + 1000 * mag) / 3), x = h - KW * (j + 0.5f) / n;
                        var key = new Vector2(u, have ? (float)acq.SampleTime[k] : 1e30f);
                        kVerts[2 * k] = new Vector3(x, KH * u, z - 0.5f * dz); kVerts[2 * k + 1] = new Vector3(x, KH * u, z + 0.5f * dz);
                        kUv[2 * k] = key; kUv[2 * k + 1] = key;
                    }
                }
                kMesh.vertices = kVerts; kMesh.uv = kUv; if (!kTrisSet) { kMesh.triangles = kTris; kTrisSet = true; }
                kMesh.RecalculateNormals(); kMesh.RecalculateBounds();
                kShownReveal = float.NaN;
            }
            float reveal = (float)t;
            if (reveal != kShownReveal) { kMat.SetFloat("_Reveal", reveal); kShownReveal = reveal; }
            // The row being read, highlighted while its readout runs.
            int blk = app.Sim.BlockIndex;
            bool reading = blk >= 0 && s.Program.Blocks[blk].Selective && s.Program.Blocks[blk].Slice == slice && s.Program.Blocks[blk].Row >= 0 && AdcOn(s.Program, t);
            if (kRowR.enabled != reading) kRowR.enabled = reading;
            if (reading) kRow.localPosition = new Vector3(0, KH * 1.08f, -KW / 2 + KW * (s.Program.Blocks[blk].Row + 0.5f) / n); // a cursor above the row
            if (Time.unscaledTime >= nextStatus || app.FixedStep)
            {
                nextStatus = Time.unscaledTime + 0.25f; int rows = 0; for (int p = 0; p < n; p++) if (acq.RowTime[p] <= t) rows++;
                bool any = rows > 0 || reading;
                if (kLabels.activeSelf != any) kLabels.SetActive(any);
                Labels.Set(kStatus, any ? $"k-space · {rows} of {n} rows" : "");
                Labels.Set(imageLabel, rows > 0 ? $"image\nslice {slice + 1} of {s.Slices.Length}" : "image");
            }
        }

        // ------------------------------------------------------------------------------------------------ images

        /// <summary>Reconstructs each slice when its row count changes; the scanner's slabs, the image and the 3-D image share it. Returns the shown slice.</summary>
        int UpdateImages(SimState s, double t)
        {
            int n = s.P.Matrix;
            int blk = app.Sim.BlockIndex, slice = 0;
            if (blk >= 0 && s.Program.Blocks[blk].Slice >= 0) slice = s.Program.Blocks[blk].Slice;
            else for (int k = s.Slices.Length - 1; k >= 0; k--) if (s.Slices[k].RowTime[n / 2] <= t) { slice = k; break; }
            for (int k = 0; k < s.Slices.Length; k++)
            {
                var a = s.Slices[k]; int rows = 0; for (int p = 0; p < n; p++) if (a.RowTime[p] <= t) rows++;
                bool any = a.RowTime[n / 2] <= t;
                app.Scanner.SetSlabVisible(k, any);
                if (k < reconLayers.Count && reconLayers[k].enabled != any) reconLayers[k].enabled = any;
                sliceRows.TryGetValue(k, out int had);
                if (rows == had && !(k == slice && slice != lastSlice)) continue;
                sliceRows[k] = rows;
                var bytes = new byte[n * n];
                if (rows > 0)
                {
                    var img = a.Reconstruct(t, out _); double max = 1e-30; foreach (var val in img) if (val > max) max = val;
                    // One reference across the hand preserves receive sensitivity and weak-signal regions.
                    if (k == 0) imageReference = System.Math.Max(imageReference, max);
                    max = System.Math.Max(1e-30, imageReference);
                    for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) bytes[y * n + (n - 1 - x)] = (byte)(255 * System.Math.Pow(System.Math.Min(1, img[y * n + x] / max), 0.8));
                }
                app.Scanner.SetSlabImage(k, bytes);
                if (k == slice) { imageTex.SetPixelData(bytes, 0); imageTex.Apply(false); }
            }
            lastSlice = slice;
            return slice;
        }
    }
}
