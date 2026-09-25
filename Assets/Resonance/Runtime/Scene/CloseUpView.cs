using Nebulytic.Resonance.Sim;
using TMPro;
using UnityEngine;

namespace Nebulytic.Resonance
{
    /// <summary>
    /// The close-up of the selected proton (after 0.5.0's resonance microscope): the proton picked in the block (pointer and
    /// trigger, or a click), magnified, in two views side by side with B0 vertical:
    ///   lab frame      - the proton precesses about B0 at the displayed carrier; B1 turns with it while RF is on;
    ///   rotating frame - the frame turning with the RF: B1 holds still and the tip is a simple rotation.
    /// Each view shows the proton's moment (mu, with its spin ring) in the proton's colour, the circle its tip precesses on,
    /// the path of the tip, its isochromat's net magnetization M, B0 and B1, and x, y, z in the standard colours.
    /// 0.8.4: a solid dark backing, larger, and in the rotating frame the precession circle carries the phase colours: a
    /// proton's colour is the colour of the circle where its magnetization points (the colour code shown in the figure).
    /// Two lines below give the proton's tissue, position, frequency offset and magnetization. Everything is read from the
    /// same simulation state as the block and the scanner.
    /// </summary>
    public sealed class CloseUpView
    {
        readonly App app; public readonly Transform Root;
        readonly Transform[] frames = new Transform[2];
        readonly Transform[] mu = new Transform[2], spin = new Transform[2], net = new Transform[2], b1 = new Transform[2], cone = new Transform[2];
        readonly Renderer[] muR = new Renderer[2], spinR = new Renderer[2];
        readonly TextMeshPro[] frameLabels = new TextMeshPro[2], muLabels = new TextMeshPro[2], mLabels = new TextMeshPro[2], b1Labels = new TextMeshPro[2], b0Labels = new TextMeshPro[2];
        readonly TextMeshPro readout, readout2, freq; float nextReadout; int shownProton = -2, shownCue = -2; double shownT;
        readonly Material rim;
        // Tip path: the last 48 tip positions of each view, drawn as fading instanced dots in the proton's colour.
        const int Trail = 48; readonly Vector3[,] trail = new Vector3[2, Trail]; int trailHead, trailCount; float trailClock;
        readonly Matrix4x4[] trailMats = new Matrix4x4[2 * Trail]; readonly Vector4[] trailTints = new Vector4[2 * Trail];
        readonly MaterialPropertyBlock trailBlock = new MaterialPropertyBlock(); readonly Mesh dot; readonly Material dotMat;
        readonly Transform link; readonly Renderer linkRenderer;
        public Element eClose, eLab, eRot;
        public const float Unit = 0.098f; // length of an equilibrium moment in the close-up (m)
        public const float Width = 0.52f, Height = 0.47f, CentreY = -0.02f;
        /// <summary>Depth of the flat text: just in front of the backing's face (0.11), so it does not float in front of the
        /// panel and shift against it with the viewer's height (parallax).</summary>
        const float TextZ = 0.1f;
        /// <summary>P frame to each view's local frame: B0 (z) up, x and y seen obliquely from slightly above.</summary>
        static readonly Quaternion FrameRot = Quaternion.Euler(16, 0, 0) * Quaternion.Euler(0, 35, 0) * Quaternion.Euler(-90, 0, 0);

        public CloseUpView(App app, Transform parent)
        {
            this.app = app;
            Root = new GameObject("Proton close-up").transform; Root.SetParent(parent, false);
            rim = Mats.Line(Look.TEXT2);
            BuildMolecules();
            Labels.Make(Root, "Hydrogen magnetic moment", new Vector3(-Width / 2 + 0.025f, 0.29f, TextZ), Look.TitleSize, Look.TEXT, TextAlignmentOptions.Left, 0.4f, false);
            var axisMat = Mats.Line(Color.white, false, 0.3f); var b0Mat = Mats.Line(Look.B0, false, 0.25f);
            for (int f = 0; f < 2; f++)
            {
                var fr = new GameObject(f == 0 ? "Lab frame" : "Rotating frame").transform; fr.SetParent(Root, false);
                fr.localPosition = new Vector3(f == 0 ? -0.125f : 0.125f, -0.03f, 0); fr.localRotation = FrameRot; frames[f] = fr;
                // The selected hydrogen is the origin of the moment, physically linked to a water molecule.
                var oxygen = new Vector3(-0.025f, -0.019f, 0); var otherH = new Vector3(-0.049f, 0, 0);
                var bondMat = Mats.Solid(new Color(0.75f, 0.82f, 0.86f));
                Mats.Object("Selected hydrogen nucleus", fr, MeshKit.Sphere(0.006f, 16, 10), Mats.Solid(new Color(0.62f, 0.91f, 1)));
                Mats.Object("Water oxygen", fr, MeshKit.Sphere(0.014f, 16, 10), Mats.Solid(new Color(0.9f, 0.22f, 0.25f)), oxygen);
                Mats.Object("Other water hydrogen", fr, MeshKit.Sphere(0.009f, 12, 8), bondMat, otherH);
                Mats.Object("Water bonds", fr, MeshKit.Tube(new System.Collections.Generic.List<Vector3> { Vector3.zero, oxygen, otherH }, 0.002f, 8), bondMat);
                // Axes (P frame drawn in S: x negated) in the standard colours, and B0 along z.
                var lb = new LineBuilder(); float a = 0.85f * Unit;
                lb.Arrow(Vector3.zero, Frames.ToS(a, 0, 0), Look.AxisWidth, Look.AX, 0.14f); lb.Arrow(Vector3.zero, Frames.ToS(0, a, 0), Look.AxisWidth, Look.AY, 0.14f);
                lb.Segment(Frames.ToS(-a * 0.6f, 0, 0), Vector3.zero, 0.0016f, Look.AX); lb.Segment(Frames.ToS(0, -a * 0.6f, 0), Vector3.zero, 0.0016f, Look.AY);
                lb.Arrow(Frames.ToS(0.014f, 0, -0.3f * Unit), Frames.ToS(0.014f, 0, 1.25f * Unit), 0.0018f, Look.AZ, 0.1f);
                Mats.Object("Axes", fr, lb.Commit(new Mesh { name = "Close-up axes" }), axisMat);
                lb.Arrow(Frames.ToS(0, 0, -0.45f * Unit), Frames.ToS(0, 0, 1.45f * Unit), 0.0042f, Color.white, 0.09f);
                Mats.Object("B0", fr, lb.Commit(new Mesh { name = "Close-up B0" }), b0Mat);
                Labels.Make(fr, f == 0 ? "x" : "x′", Frames.ToS(a * 1.28f, 0, -0.02f), Look.LabelSize, Look.AX); // below the transverse plane
                Labels.Make(fr, f == 0 ? "y" : "y′", Frames.ToS(0, a * 1.3f, 0.016f), Look.LabelSize, Look.AY);  // above it
                Labels.Make(fr, "z", Frames.ToS(0.03f, 0, 1.2f * Unit), Look.LabelSize, Look.AZ);
                b0Labels[f] = Labels.Make(fr, "B₀", Frames.ToS(0, 0.055f, 1.68f * Unit), Look.LabelSize, Look.B0);
                // The precession circle (unit circle in the transverse plane, placed and scaled per frame): white in the lab
                // frame, the phase colours in the rotating frame.
                var circle = new LineBuilder();
                for (int k = 0; k < 72; k++)
                {
                    double p0 = 2 * System.Math.PI * k / 72, p1 = 2 * System.Math.PI * (k + 1) / 72;
                    var c = f == 1 ? Look.Phase(0.5 * (p0 + p1), 1f) : Look.TEXT2;
                    circle.Segment(Frames.ToS(System.Math.Cos(p0), System.Math.Sin(p0), 0), Frames.ToS(System.Math.Cos(p1), System.Math.Sin(p1), 0), f == 1 ? 0.0045f : 0.0024f, c);
                }
                var cm = Mats.Line(Color.white, false, 0.25f); cm.SetFloat("_ScaleWidth", 0);
                var cgo = Mats.Object("Precession circle", fr, circle.Commit(new Mesh { name = "Unit circle" }), cm); cone[f] = cgo.transform;
                // mu (one proton's moment, in its colour) with its spin ring; M (the isochromat's net magnetization, white); B1.
                mu[f] = Mats.Object("mu", fr, MeshKit.Needle(0.045f, 0.11f, 0.2f), Mats.Glass(new Color(Look.MAG.r,Look.MAG.g,Look.MAG.b,0.40f))).transform; muR[f] = mu[f].GetComponent<Renderer>();
                spin[f] = Mats.Object("Spin ring", fr, MeshKit.Torus(1f, 0.08f, 40, 6), muR[f].sharedMaterial).transform; spinR[f] = spin[f].GetComponent<Renderer>(); spin[f].gameObject.SetActive(false);
                Mats.Object("Spin bead", spin[f], MeshKit.Sphere(0.2f, 10, 6), muR[f].sharedMaterial, new Vector3(1, 0, 0));
                net[f] = Mats.Object("M", fr, MeshKit.Needle(0.07f, 0.16f, 0.22f), Mats.Solid(Look.MAG)).transform;
                b1[f] = Mats.Object("B1", fr, MeshKit.Needle(0.05f, 0.12f, 0.2f), Mats.Solid(Look.RF)).transform;
                muLabels[f] = Labels.Make(fr, "μ", Vector3.zero, Look.LabelSize, Look.TEXT); mLabels[f] = Labels.Make(fr, "M", Vector3.zero, Look.LabelSize, Look.MAG);
                b1Labels[f] = Labels.Make(fr, "B₁", Vector3.zero, Look.LabelSize, Look.RF);
                frameLabels[f] = Labels.Make(Root, f == 0 ? "lab frame" : "rotating frame", new Vector3(fr.localPosition.x, -0.14f, TextZ), Look.LabelSize, Look.TEXT2, TextAlignmentOptions.Center, 0.3f, false);
            }
            freq = Labels.Make(Root, "", new Vector3(-0.125f, -0.166f, TextZ), Look.ValueSize, Look.B0, TextAlignmentOptions.Center, 0.3f, false);
            readout = Labels.Make(Root, "", new Vector3(0, -0.193f, TextZ), Look.ValueSize, Look.TEXT, TextAlignmentOptions.Center, 0.5f, false);
            readout2 = Labels.Make(Root, "", new Vector3(0, -0.219f, TextZ), Look.ValueSize, Look.TEXT, TextAlignmentOptions.Center, 0.5f, false);
            dot = MeshKit.Sphere(0.5f, 8, 5); dotMat = Mats.Needle(true);
            // A line from the selected proton in the block to the close-up.
            var ll = new LineBuilder(); ll.Segment(Vector3.zero, Vector3.forward, 0.0022f, Color.white);
            var lm = Mats.Line(Look.SCAF, false, 0.35f); lm.SetFloat("_ScaleWidth", 0);
            link = Mats.Object("Zoom link", parent, ll.Commit(new Mesh { name = "Unit segment" }), lm).transform; linkRenderer = link.GetComponent<Renderer>();
            eClose = app.Attention.Register("closeup", () => Root.TransformPoint(new Vector3(0, CentreY + Height / 2 - 0.01f, 0)), "closeup");
            eLab = app.Attention.Register("closeup.lab", () => frames[0].position + Root.up * 0.14f, "closeup");
            eRot = app.Attention.Register("closeup.rot", () => frames[1].position + Root.up * 0.14f, "closeup");
            var col = Root.gameObject.AddComponent<BoxCollider>(); col.size = new Vector3(Width, Height, 0.2f); col.center = new Vector3(0, CentreY, 0); col.isTrigger = true;
            Root.gameObject.AddComponent<Grabbable>().Kind = "closeup";
        }

        readonly System.Collections.Generic.List<Transform> molecules = new System.Collections.Generic.List<Transform>();
        readonly System.Collections.Generic.List<Vector3> moleculeHomes = new System.Collections.Generic.List<Vector3>();
        double molecularClock;
        void BuildMolecules()
        {
            // A symbolic water cluster occupies real depth. Atom radii and separations are magnified for teaching.
            var oxygen = Mats.Solid(new Color(0.91f, 0.23f, 0.27f));
            var hydrogen = Mats.Solid(new Color(0.72f, 0.88f, 0.97f));
            var bond = Mats.Solid(new Color(0.79f, 0.84f, 0.88f));
            var om = MeshKit.Sphere(0.013f, 16, 10); var hm = MeshKit.Sphere(0.009f, 12, 8);
            for (int j = 0; j < 18; j++)
            {
                var molecule = new GameObject("Water molecule (schematic)").transform; molecule.SetParent(Root, false);
                molecule.localPosition = new Vector3(-0.18f + (j % 6) * 0.072f, 0.13f + (j / 6) * 0.028f, -0.08f + (j % 3) * 0.065f);
                molecule.localRotation = Quaternion.Euler(j * 47, j * 71, j * 29);
                molecules.Add(molecule); moleculeHomes.Add(molecule.localPosition);
                Mats.Object("Oxygen", molecule, om, oxygen);
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var h = new Vector3(sign * 0.023f, 0.018f, 0);
                    Mats.Object("Hydrogen", molecule, hm, hydrogen, h);
                    Mats.Object("O-H bond", molecule, MeshKit.Tube(new System.Collections.Generic.List<Vector3> { Vector3.zero, h }, 0.002f, 6), bond);
                }
            }
            Labels.Make(Root, "H₂O · slowed thermal motion", new Vector3(0, 0.25f, 0), Look.ValueSize, Look.TEXT);
        }

        /// <summary>Per frame, after the block (which chose the proton and holds the thermal direction and colour).</summary>
        public void HideForExample() { Root.gameObject.SetActive(false); linkRenderer.enabled=false; }

        public void Tick(float dt, double carrier, bool playing)
        {
            if (playing) molecularClock += dt;
            for (int j = 0; j < molecules.Count; j++)
            {
                float t = (float)molecularClock, q = j * 1.73f;
                molecules[j].localPosition = moleculeHomes[j] + 0.008f * new Vector3(Mathf.Sin(t*1.7f+q),Mathf.Sin(t*2.1f+q*2),Mathf.Cos(t*1.3f+q));
                molecules[j].localRotation = Quaternion.Euler(j*47+t*17,j*71+t*23,j*29+t*13);
            }
            var sim = app.Sim; var s = sim.State; var grid = app.Cube; int i = grid.Selected;
            bool ok = s != null && i >= 0 && s.CubeEval.X != null && (sim.Synchronous || sim.EvalRevision == s.Revision);
            Root.gameObject.SetActive(ok); linkRenderer.enabled = ok;
            if (!ok) return;
            if (i != shownProton) { shownProton = i; trailCount = 0; nextReadout = 0; }
            // A jump in the lesson (a new cue, a seek, a cut) starts a new tip path: the old one would show where the proton was
            // before the jump, not how it moved.
            int cueKey = app.Cue != null ? app.StepIndex * 1000 + app.Cue.Index : -1;
            if (cueKey != shownCue || System.Math.Abs(app.T - shownT) > 0.05) { shownCue = cueKey; trailCount = 0; }
            shownT = app.T;
            // Lab frame: the carrier turns everything transverse; rotating frame: carrier 0.
            Vector3 m = grid.NetM(i); float mPerp = new Vector2(m.x, m.y).magnitude;
            Vector3 dLab = grid.MomentDir(i, carrier), dRot = grid.MomentDir(i, 0); // P frame, unit
            Color pc = grid.ProtonColour(i); pc.a=app.EnsembleMoments?0.78f:0.40f;
            muR[0].sharedMaterial.SetColor("_Color", pc); muR[1].sharedMaterial.SetColor("_Color", pc);
            if (playing) trailClock += dt;
            if (trailClock >= 1f / 16 || trailCount == 0) { trailClock = 0; RecordTip(Frames.ToS(dLab.x, dLab.y, dLab.z) * Unit, Frames.ToS(dRot.x, dRot.y, dRot.z) * Unit); }
            for (int f = 0; f < 2; f++)
            {
                double c = f == 0 ? carrier : 0;
                Vector3 d = f == 0 ? dLab : dRot;
                Vector3 ds = Frames.ToS(d.x, d.y, d.z);
                Place(mu[f], ds, Unit * (app.EnsembleMoments ? Mathf.Clamp01(m.magnitude) : 1), app.EnsembleMoments?0.85f:1.25f);
                Labels.Set(muLabels[f], app.EnsembleMoments ? "M" : "μ");
                // The spin ring turns about mu, a little over halfway up it.
                spin[f].localPosition = ds * (0.55f * Unit); spin[f].localRotation = Quaternion.FromToRotation(Vector3.up, ds) * Quaternion.Euler(0, grid.SpinAngle(i) * Mathf.Rad2Deg, 0);
                spin[f].localScale = Vector3.one * (0.17f * Unit);
                // The circle the tip precesses on: height cos(theta), radius sin(theta).
                cone[f].localPosition = Frames.ToS(0, 0, d.z * Unit); cone[f].localScale = Vector3.one * Mathf.Max(1e-4f, new Vector2(d.x, d.y).magnitude * Unit);
                // M in the same frame.
                double cs = System.Math.Cos(-c), sn = System.Math.Sin(-c);
                Vector3 mf = new Vector3((float)(m.x * cs - m.y * sn), (float)(m.x * sn + m.y * cs), m.z);
                float ml = mf.magnitude; net[f].gameObject.SetActive(!app.EnsembleMoments && ml > 0.03f);
                if (ml > 0.03f) Place(net[f], Frames.ToS(mf.x / ml, mf.y / ml, mf.z / ml), Unit * Mathf.Min(1.15f, ml) * 1.12f, 0.55f); // slim, a little longer than mu
                // B1 while RF drives: A b1 at the proton, turned by the carrier in the lab frame.
                bool rf = sim.RfOn; b1[f].gameObject.SetActive(rf); b1Labels[f].gameObject.SetActive(rf);
                if (rf)
                {
                    s.Tables.Sample(s.Cube.Pos[i], out FieldSample fs);
                    double bre = sim.RfRe * fs.B1Re - sim.RfIm * fs.B1Im, bim = sim.RfRe * fs.B1Im + sim.RfIm * fs.B1Re;
                    double lx = bre * cs - bim * sn, ly = bre * sn + bim * cs, bm = System.Math.Sqrt(lx * lx + ly * ly);
                    if (bm > 1e-12)
                    {
                        Vector3 bd = Frames.ToS(lx / bm, ly / bm, 0); float len = Unit * Mathf.Min(1.1f, (float)(bm / (0.75 * Scanner.B1Iso)));
                        Place(b1[f], bd, Mathf.Max(0.25f * Unit, len), 1.2f); b1Labels[f].transform.localPosition = bd * (Mathf.Max(0.25f * Unit, len) + 0.03f);
                        if (Time.unscaledTime >= nextReadout || app.FixedStep) Labels.Set(b1Labels[f], $"B₁ {bm * 1e6:0.0} µT");
                    }
                }
                muLabels[f].transform.localPosition = ds * (Unit + 0.02f);
                mLabels[f].gameObject.SetActive(!app.EnsembleMoments && ml > 0.03f); mLabels[f].transform.localPosition = Frames.ToS(mf.x, mf.y, mf.z) * Unit * 0.55f + Frames.ToS(0.02f, 0.02f, 0);
            }
            DrawTrail(pc);
            // Emphasis: the whole close-up's rim lights when referenced; the frame named has the bright label.
            for (int f = 0; f < 2; f++) frames[f].rotation = app.Cube.Root.rotation;
            float named = app.Attention.Refs != null ? Mathf.Max(eClose.E, Mathf.Max(eLab.E, eRot.E)) : 0;
            rim.SetColor("_Color", Color.Lerp(Look.PANEL_EDGE, Look.TEXT2, named));
            frameLabels[0].color = Color.Lerp(Look.TEXT2, Look.TEXT, Mathf.Max(eLab.E, eClose.E)); frameLabels[1].color = Color.Lerp(Look.TEXT2, Look.TEXT, Mathf.Max(eRot.E, eClose.E));
            // Link from the proton in the block to the close-up.
            Vector3 a = grid.CellWorld(i), b = Root.TransformPoint(new Vector3(-Width / 2, 0.05f, 0)); Vector3 ab = b - a;
            if (ab.sqrMagnitude > 1e-6f) { link.position = a; link.rotation = Quaternion.LookRotation(ab); link.localScale = new Vector3(1, 1, ab.magnitude); }
            if (Time.unscaledTime >= nextReadout || app.FixedStep) { nextReadout = Time.unscaledTime + 0.2f; UpdateReadout(s, i, m, mPerp); }
        }

        static void Place(Transform t, Vector3 dirS, float length, float thickness)
        {
            t.localRotation = Quaternion.FromToRotation(Vector3.up, dirS); t.localPosition = dirS * (length * 0.5f);
            t.localScale = new Vector3(Unit * 0.65f * thickness, length, Unit * 0.65f * thickness);
        }

        void RecordTip(Vector3 labTip, Vector3 rotTip)
        {
            trail[0, trailHead] = labTip; trail[1, trailHead] = rotTip;
            trailHead = (trailHead + 1) % Trail; trailCount = Mathf.Min(Trail, trailCount + 1);
        }

        void DrawTrail(Color c)
        {
            int n = 0;
            for (int f = 0; f < 2; f++)
            {
                Matrix4x4 F = frames[f].localToWorldMatrix;
                for (int k = 0; k < trailCount; k++)
                {
                    int idx = (trailHead - 1 - k + Trail) % Trail; float age = k / (float)Trail;
                    trailMats[n] = F * Matrix4x4.TRS(trail[f, idx], Quaternion.identity, Vector3.one * 0.0065f / Mathf.Max(1e-4f, frames[f].lossyScale.x));
                    trailTints[n] = new Vector4(c.r, c.g, c.b, (1 - age) * 0.9f); n++;
                }
            }
            trailDrawn = n; if (n == 0) return;
            trailBlock.SetVectorArray("_Tint", trailTints);
            var rp = new RenderParams(dotMat) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false, layer = Root.gameObject.layer, matProps = trailBlock, worldBounds = new Bounds(Root.position, Vector3.one * 0.7f) };
            Graphics.RenderMeshInstanced(rp, dot, 0, trailMats, n);
        }
        int trailDrawn;

        /// <summary>Re-submits the tip path for one camera (validation captures).</summary>
        public void Submit(Camera cam)
        {
            if (trailDrawn == 0 || !Root.gameObject.activeInHierarchy) return;
            var rp = new RenderParams(dotMat) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false, layer = Root.gameObject.layer, matProps = trailBlock, camera = cam, worldBounds = new Bounds(Root.position, Vector3.one * 0.7f) };
            Graphics.RenderMeshInstanced(rp, dot, 0, trailMats, trailDrawn);
        }

        void UpdateReadout(SimState s, int i, Vector3 m, float mPerp)
        {
            var c = s.Cube; var off = c.Pos[i] - s.CubeCentre;
            string tissue = c.Cls[i] switch { TissueClass.Muscle => "muscle", TissueClass.Fat => "fat", TissueClass.Marrow => "marrow", TissueClass.Cortex => "cortical bone", TissueClass.Tendon => "tendon", TissueClass.Skin => "skin", TissueClass.Soft => "soft tissue", _ => "tissue" };
            var sim = app.Sim; double df = c.DeltaF(i, new Currents(s.P.MagnetCurrent, sim.Ix, sim.Iy, sim.Iz)); // from the carrier, now (Hz)
            double phase = System.Math.Atan2(m.y, m.x) * 180 / System.Math.PI;
            string f = System.Math.Abs(df) >= 1000 ? $"{df / 1e3:+0.00;-0.00} kHz" : $"{df:+0;-0;0} Hz";
            Labels.Set(readout, $"{tissue} · x {off.X * 1e3:+0.0;-0.0;0.0}  y {off.Y * 1e3:+0.0;-0.0;0.0}  z {off.Z * 1e3:+0.0;-0.0;0.0} mm");
            Labels.Set(readout2, $"Δf {f} · Mz {m.z:+0.00;-0.00;0.00} · M⊥ {mPerp:0.00}" + (mPerp > 0.03f ? $" · phase {phase:+0;-0;0}°" : ""));
            Labels.Set(freq, $"f₀ {s.P.Fref / 1e6:0.00} MHz (slowed)");
            for (int k = 0; k < 2; k++) Labels.Set(b0Labels[k], $"B₀ {s.P.B0:0.00} T");
        }
    }
}
