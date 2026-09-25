using System.Collections.Generic;
using Nebulytic.Resonance.Sim;
using UnityEngine;

namespace Nebulytic.Resonance
{
    /// <summary>
    /// The scanner (SPEC 5.3): the five coil systems drawn from the simulation's conductors, the B0 field lines (traced
    /// from the magnet's computed field, shaded by the local |B| so a gradient reads as a ramp, markers flowing along +z)
    /// and the rotating B1 arrows, the hand's tipped magnetization, the anatomy, the proton block's outline, the selected
    /// slab's outline, the reconstructed image slabs, and the base with the receiver chain and the signal arrow.
    /// 0.8.4 (USER-REVIEW-0.8.3): the magnet is a solid cyan winding set with a cutaway facing the viewer, so the coils
    /// inside show; coils are thicker, in the colour of their axis; the field lines are opaque; the block's outline is drawn
    /// over everything at the block's true size and orientation, and four zoom lines join it to the magnified block; the
    /// tipped slab and the images are premultiplied (they read over a bright room); the base is a saturated blue; labels
    /// are about twice as large. Physics geometry lives under Physics (S frame, metres).
    /// </summary>
    public sealed class ScannerView
    {
        sealed class Part
        {
            public Renderer R; public MaterialPropertyBlock B = new MaterialPropertyBlock(); public float Sign = 1; public int Index;
            // Last values sent (updated only on change); dash motion runs in the shader: offset + time x speed.
            public float LastBody = -1, LastFlow = -1, LastRim = -1, Speed, Off;
            public bool Changed(float body, float flow, float rim) => Mathf.Abs(body - LastBody) > 2e-3f || Mathf.Abs(flow - LastFlow) > 2e-3f || Mathf.Abs(rim - LastRim) > 2e-3f;
        }
        sealed class CoilVisual { public CoilId Id; public Element E; public readonly List<Part> Parts = new List<Part>(); public float Offset; public readonly List<Vector3> Anchors = new List<Vector3>(); }

        readonly App app;
        public readonly Transform Root, Physics;
        readonly CoilVisual[] coils = new CoilVisual[5];
        readonly TMPro.TextMeshPro[] coilLabels = new TMPro.TextMeshPro[4];
        Renderer receiveCoil; Element eReceive; readonly MaterialPropertyBlock receiveProperties = new MaterialPropertyBlock();
        Renderer fieldLines, glow; Material fieldMat; Texture3D glowTex; byte[] glowData; int glowNx, glowNy, glowNz, glowRevision = -1;
        readonly Mesh fieldMesh = new Mesh { name = "B0 field lines" };
        // B1 arrows: 3 x 3 x 5 samples inside the birdcage, co-rotating B1 per ampere at each.
        const int B1N = 45; readonly Vector3[] b1At = new Vector3[B1N]; readonly double[] b1Re = new double[B1N], b1Im = new double[B1N];
        readonly Matrix4x4[] b1Mats = new Matrix4x4[B1N]; readonly Vector4[] b1Tints = new Vector4[B1N]; readonly MaterialPropertyBlock b1Block = new MaterialPropertyBlock();
        Mesh b1Mesh; Material b1Mat; int b1Count;
        // Selected slab outline and the field/frequency labels along the bore.
        readonly Mesh slabMesh = new Mesh { name = "Selected slab outline" }; Renderer slabOutline; double slabZ = double.NaN, slabDz;
        readonly TMPro.TextMeshPro[] zLabels = new TMPro.TextMeshPro[3]; float nextLabels;
        volatile float glowPeak; // largest tipped fraction in the hand (worker), to skip the glow's raymarch when nothing is tipped
        Transform specimen, region, glowBox; readonly List<Renderer> bones = new List<Renderer>(); Renderer skin; Material muscleMat;
        readonly List<Renderer> slabRenderers = new List<Renderer>(); readonly List<Texture2D> slabTex = new List<Texture2D>();
        Transform signalArrow; Renderer signalRenderer; Renderer[] rxBlocks = new Renderer[3]; readonly TMPro.TextMeshPro[] rxLabels = new TMPro.TextMeshPro[3]; TMPro.TextMeshPro mainLabel;
        readonly List<Renderer> axis = new List<Renderer>(); Renderer regionRenderer; TMPro.TextMeshPro slabLabel, b0Label;
        // Zoom lines: from the block's outline in the hand to the magnified block (four corners of facing faces).
        readonly Transform[] zoom = new Transform[4]; Material zoomMat;
        /// <summary>Label sizes inside Physics (scaled by ScannerScale): about 0.8 degrees at the scanner's distance.</summary>
        const float LocalLabel = 0.40f, LocalValue = 0.34f;
        /// <summary>The magnet's cutaway (physical azimuths, degrees): the sector facing the default view.</summary>
        public const float CutFrom = 125, CutTo = 235;
        Element eField, eB1, eGlow, eHand, eRegion, eSignal, eSlabs; readonly Element[] eAxis = new Element[3], eRx = new Element[3];

        Vector3 SlabAnchor()
        {
            // Top edge of the visible image slabs, in the middle of the stack.
            var s = app.Sim.State; double z = 0; int n = 0;
            if (s != null) foreach (var r in slabRenderers) if (r && r.transform.parent.gameObject.activeSelf) { z += Frames.ToP(r.transform.parent.localPosition).Z; n++; }
            return W(Frames.ToS(0, 0.03, n > 0 ? z / n : 0));
        }
        int shownRevision = -1;
        /// <summary>Where the receiver chain's data leaves the plinth (world): the link to the signal plots starts here.</summary>
        public Vector3 ReceiverPort => W(Frames.ToS(-0.20, -0.25, 0.28));

        public ScannerView(App app, Transform parent)
        {
            this.app = app;
            Root = new GameObject("Scanner").transform; Root.SetParent(parent, false);
            Physics = new GameObject("Physics (S frame)").transform; Physics.SetParent(Root, false); Physics.localScale = Vector3.one * Look.ScannerScale;
            BuildCoils(); BuildFields(); BuildPlinth(); BuildTriad(); BuildZoom(parent);
            app.Sim.AfterHand = FillGlow; // runs on the spin worker (or inline when synchronous)
            // Referents that depend on the simulation state exist from the start (their anchors follow the state).
            eGlow = app.Attention.Register("hand.glow", () => GlowAnchor(), "scanner", "spins");
            eHand = app.Attention.Register("hand", () => W(Frames.ToS(0.0, 0.03, -0.12)), "scanner");
            eRegion = app.Attention.Register("region", () => region ? region.position : Root.position, "scanner", "region");
            var cl = Root.gameObject.AddComponent<BoxCollider>(); cl.center = Physics.localScale.x * new Vector3(0, -0.1f, 0); cl.size = Physics.localScale.x * new Vector3(0.6f, 0.75f, 0.8f); cl.isTrigger = true;
            Root.gameObject.AddComponent<Grabbable>().Kind = "scanner";
        }

        Vector3 W(Vector3 s) => Physics.TransformPoint(s);

        // ------------------------------------------------------------------------------------------------ coils

        void BuildCoils()
        {
            var att = app.Attention;
            for (int c = 0; c < 5; c++) coils[c] = new CoilVisual { Id = (CoilId)c };
            // Magnet packs (0.8.4): solid, lit, cyan winding blocks with a cutaway toward the default view, so the gradient coils,
            // the RF coil and the hand inside show; the cut faces read as sections. Thin conductors: solid lit tubes.
            var b0 = coils[0]; var mat0 = Mats.Conductor(new Color(0.72f, 0.81f, 0.88f), true);
            mat0.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back); mat0.SetFloat("_Turns", 0);
            foreach (var p in Scanner.Packs)
            {
                // Sparse representative winding bundles expose the bore. Pack bounds and the field model are unchanged.
                var bundles = new List<CombineInstance>();
                for (int turn = 0; turn < 6; turn++)
                {
                    var points = new List<Vector3>();
                    double z = p.ZCentre + p.Length * ((turn + 0.5) / 6.0 - 0.5);
                    for (int j = 0; j <= 64; j++) { double a = j * System.Math.PI / 32; points.Add(Frames.ToS(p.ROut * System.Math.Cos(a), p.ROut * System.Math.Sin(a), z)); }
                    bundles.Add(new CombineInstance { mesh = MeshKit.Tube(points, 0.0018f, 6), transform = Matrix4x4.identity });
                }
                var mesh = new Mesh { name = "Main magnet winding bundles" }; mesh.CombineMeshes(bundles.ToArray(), true, true);
                foreach (var bundle in bundles) Object.Destroy(bundle.mesh);
                var go = Mats.Object("B0 pack", Physics, mesh, mat0);
                b0.Parts.Add(new Part { R = go.GetComponent<Renderer>(), Sign = p.Sense });
                for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4; b0.Anchors.Add(Frames.ToS(p.ROut * Mathf.Cos(a), p.ROut * Mathf.Sin(a), p.ZCentre)); }
            }
            foreach (var cd in Scanner.GradientPaths)
            {
                var cv = coils[(int)cd.Coil];
                var pts = new List<Vector3>(); foreach (var p in cd.Points) pts.Add(Frames.ToS(p));
                var go = Mats.Object(cd.Coil + " winding", Physics, MeshKit.Tube(pts, 0.0032f, 10), MatFor(cd.Coil));
                cv.Parts.Add(new Part { R = go.GetComponent<Renderer>(), Sign = (float)cd.Sign });
                for (int k = 0; k < pts.Count; k += 4) cv.Anchors.Add(pts[k]);
            }
            var rf = coils[4];
            foreach (var cd in Scanner.BirdcageParts)
            {
                var pts = new List<Vector3>(); foreach (var p in cd.Points) pts.Add(Frames.ToS(p));
                var go = Mats.Object("Birdcage part " + cd.Part, Physics, MeshKit.Tube(pts, 0.0028f, 10), MatFor(CoilId.Rf));
                rf.Parts.Add(new Part { R = go.GetComponent<Renderer>(), Index = cd.Part });
                rf.Anchors.Add(pts[pts.Count / 2]);
            }
            coilLabels[0] = Labels.Make(Physics, "Main magnet · B₀", Frames.ToS(0, 0.32, -0.22), LocalLabel, Look.TEXT);
            coilLabels[1] = Labels.Make(Physics, "Gx · Gy · Gz · spatial encoding", Frames.ToS(0, 0.26, 0.24), LocalValue, Look.TEXT);
            coilLabels[2] = Labels.Make(Physics, "RF transmit · excite", Frames.ToS(0, -0.19, 0.20), LocalValue, Look.RF);
            var rxPoints = new List<Vector3>(); foreach (var point in Scanner.ReceivePath()) rxPoints.Add(Frames.ToS(point));
            receiveCoil = Mats.Object("Receive surface coil", Physics, MeshKit.Tube(rxPoints, 0.004f, 10), Mats.Conductor(new Color(1f, 0.81f, 0.38f), true)).GetComponent<Renderer>();
            eReceive = app.Attention.Register("coil.rx", () => W(Frames.ToS(Scanner.ReceiveRadius, Scanner.ReceiveY, 0)), "scanner", "coils", "rx");
            coilLabels[3] = Labels.Make(Physics, "Receive · sense", Frames.ToS(-0.11, 0.10, 0), LocalValue, new Color(1f, 0.86f, 0.55f));
            string[] keys = { "coil.b0", "coil.gx", "coil.gy", "coil.gz", "coil.rf" };
            for (int c = 0; c < 5; c++)
            {
                var cv = coils[c];
                cv.E = c >= 1 && c <= 3 ? att.Register(keys[c], () => NearestAnchor(cv.Anchors), "scanner", "coils", "gradients")
                                        : att.Register(keys[c], () => NearestAnchor(cv.Anchors), "scanner", "coils");
            }
        }

        readonly Dictionary<CoilId, Material> coilMats = new Dictionary<CoilId, Material>();
        Material MatFor(CoilId c) { if (!coilMats.TryGetValue(c, out var m)) coilMats[c] = m = Mats.Conductor(Look.Coil(c), true); return m; }

        Vector3 NearestAnchor(List<Vector3> anchors)
        {
            Vector3 eye = app.Eye, best = Root.position; float bd = float.MaxValue;
            foreach (var a in anchors) { var w = W(a); float d = (w - eye).sqrMagnitude; if (d < bd) { bd = d; best = w; } }
            return best;
        }

        // ------------------------------------------------------------------------------------------------ volumes

        /// <summary>
        /// B0 field lines: traced along the magnet's computed field from 19 points of the z = 0 plane (the axis, 6 at 35 mm, 12
        /// at 70 mm) to |z| = 160 mm. Each vertex carries its |B| deviation per unit current of the magnet and of each gradient
        /// coil (the tables the old haze used), so the shader lights the lines from the live currents: a gradient is a
        /// brightness ramp, with no rebuild. B1 arrows: the co-rotating field at 45 points in the birdcage.
        /// </summary>
        void BuildFields()
        {
            var t = app.Sim.Tables; var att = app.Attention;
            var starts = new List<D3> { new D3(0, 0, 0) };
            for (int k = 0; k < 6; k++) { double a = k * System.Math.PI / 3 + System.Math.PI / 6; starts.Add(new D3(0.070 * System.Math.Cos(a), 0.070 * System.Math.Sin(a), 0)); }
            var lb = new LineBuilder(); var pts = new List<Vector3>(); var fs = new List<Vector4>();
            foreach (var s0 in starts)
            {
                pts.Clear(); fs.Clear();
                foreach (var q in TraceFieldLine(s0, FieldTables.HazeZ)) { pts.Add(Frames.ToS(q)); fs.Add(HazeAt(t, q) * 1e4f); }
                lb.Polyline(pts, 0.0042f, Color.white, null, fs);
            }
            // Opaque (0.8.4), with a dark casing: additive lines vanished over a bright room.
            fieldMat = Mats.Line(Look.B0, false, 0.3f); fieldMat.SetFloat("_Mode", 1); fieldMat.SetFloat("_W", Look.LineFieldWidth);
            fieldMat.SetFloat("_Dash", 0.03f); fieldMat.SetFloat("_Flow", 0.4f); fieldMat.SetFloat("_Base", 0.6f);
            fieldLines = Mats.Object("B0 field lines", Physics, lb.Commit(fieldMesh), fieldMat).GetComponent<Renderer>();
            eField = att.Register("field.b", () => W(Frames.ToS(-0.07, 0.07, 0)), "scanner", "fields");
            int n = 0;
            for (int iz = 0; iz < 5; iz++) for (int iy = -1; iy <= 1; iy++) for (int ix = -1; ix <= 1; ix++)
                    {
                        var q = new D3(0.06 * ix, 0.06 * iy, -0.20 + 0.10 * iz); t.Sample(q, out FieldSample f);
                        b1At[n] = Frames.ToS(q); b1Re[n] = f.B1Re; b1Im[n] = f.B1Im; n++;
                    }
            b1Mesh = MeshKit.Needle(0.07f, 0.17f, 0.2f); b1Mat = Mats.Needle(false);
            eB1 = att.Register("field.b1", () => W(Frames.ToS(-0.04, 0.04, -0.04)), "scanner", "fields");
        }

        /// <summary>Points along the magnet's field line through p0 (P frame), from z = -zMax to +zMax, 4 mm apart.</summary>
        static List<D3> TraceFieldLine(D3 p0, double zMax)
        {
            var fwd = new List<D3>(); var back = new List<D3>();
            foreach (int dir in new[] { 1, -1 })
            {
                var list = dir > 0 ? fwd : back; D3 p = p0;
                for (int k = 0; k < 200 && System.Math.Abs(p.Z) < zMax; k++)
                {
                    double rho = System.Math.Sqrt(p.X * p.X + p.Y * p.Y);
                    Scanner.MagnetField(rho, p.Z, out double br, out double bz);
                    D3 b = Scanner.Cylindrical(br, bz, p); double m = b.Norm; if (m <= 0) break;
                    p = p + b / m * (0.004 * dir); list.Add(p);
                }
            }
            back.Reverse(); back.Add(p0); back.AddRange(fwd);
            return back;
        }

        /// <summary>|B| deviation per ampere of the magnet, and b0-hat . G per ampere of Gx, Gy, Gz at p (trilinear, P frame).</summary>
        static Vector4 HazeAt(FieldTables t, D3 p)
        {
            int nx = FieldTables.HazeNx, nz = FieldTables.HazeNz; double r = FieldTables.HazeR, h = FieldTables.HazeZ;
            double fx = (p.X + r) / (2 * r) * (nx - 1), fy = (p.Y + r) / (2 * r) * (nx - 1), fz = (p.Z + h) / (2 * h) * (nz - 1);
            fx = System.Math.Max(0, System.Math.Min(nx - 1.001, fx)); fy = System.Math.Max(0, System.Math.Min(nx - 1.001, fy)); fz = System.Math.Max(0, System.Math.Min(nz - 1.001, fz));
            int ix = (int)fx, iy = (int)fy, iz = (int)fz; double ux = fx - ix, uy = fy - iy, uz = fz - iz;
            var acc = Vector4.zero;
            for (int c = 0; c < 8; c++)
            {
                int dx = c & 1, dy = (c >> 1) & 1, dz = c >> 2;
                double w = (dx == 1 ? ux : 1 - ux) * (dy == 1 ? uy : 1 - uy) * (dz == 1 ? uz : 1 - uz);
                int k = (((iz + dz) * nx + iy + dy) * nx + ix + dx) * 4;
                acc += (float)w * new Vector4(t.Haze[k], t.Haze[k + 1], t.Haze[k + 2], t.Haze[k + 3]);
            }
            return acc;
        }

        /// <summary>Rebuild everything that depends on the specimen, the protocol or the cube region.</summary>
        public void OnState(SimState s)
        {
            shownRevision = s.Revision;
            BuildGlow(s); BuildSpecimen(s); BuildRegion(s); BuildSlabs(s);
        }

        void BuildGlow(SimState s)
        {
            var h = s.Hand; glowNx = h.Nx; glowNy = h.Ny; glowNz = h.Nz;
            if (glowTex == null || glowTex.width != glowNx || glowTex.height != glowNy || glowTex.depth != glowNz)
            {
                glowTex = new Texture3D(glowNx, glowNy, glowNz, TextureFormat.R8, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Hand magnetization" };
            }
            glowData = new byte[glowNx * glowNy * glowNz]; glowRevision = s.Revision;
            if (glowBox == null)
            {
                // Premultiplied (0.8.4): the tipped slab becomes an opaque violet-to-white layer, visible over a bright room.
                var go = Mats.Object("Hand magnetization glow", Physics, MeshKit.Box(Vector3.one), Mats.Volume(Look.MAG, glowTex, 60, 4, 12));
                go.GetComponent<Renderer>().sharedMaterial.SetColor("_Color2", Look.Phase(System.Math.PI / 2, 1f));
                glowBox = go.transform; glow = go.GetComponent<Renderer>();
            }
            glow.sharedMaterial.SetTexture("_Vol", glowTex);
            double c = h.CellX;
            D3 lo = h.LatticeOrigin - new D3(c / 2, c / 2, c / 2), hi = h.LatticeOrigin + new D3((h.Nx - 0.5) * c, (h.Ny - 0.5) * c, (h.Nz - 0.5) * c);
            Vector3 a = Frames.ToS(lo), b = Frames.ToS(hi);
            glowBox.localPosition = 0.5f * (a + b); glowBox.localScale = new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), Mathf.Abs(b.z - a.z));
        }

        Vector3 GlowAnchor()
        {
            var s = app.Sim.State; if (s == null) return Root.position;
            return W(Frames.ToS(s.CubeCentre.X, s.CubeCentre.Y + 0.03, s.P.SliceZ));
        }

        void BuildSpecimen(SimState s)
        {
            if (specimen == null)
            {
                specimen = new GameObject("Hand anatomy").transform; specimen.SetParent(Physics, false);
                var boneMat = Mats.Solid(Look.BONE); var skinMat = Mats.Glass(Look.SKIN, 0.6f);
                // Muscles (0.8.3, as in 0.5.0): a translucent layer between the skin and the bones.
                muscleMat = Mats.Glass(new Color(Look.MUSCLE.r, Look.MUSCLE.g, Look.MUSCLE.b, 0.22f), 0.5f);
                foreach (var name in app.AnatomyNames)
                {
                    var src = Resources.Load<GameObject>("Anatomy/" + name);
                    if (src == null) continue;
                    var mf = src.GetComponentInChildren<MeshFilter>(); if (mf == null) continue;
                    bool isSkin = name == "Skin", isMuscle = name.StartsWith("Muscle_");
                    var go = Mats.Object(name, specimen, mf.sharedMesh, isSkin ? skinMat : isMuscle ? muscleMat : new Material(boneMat));
                    if (isSkin) skin = go.GetComponent<Renderer>(); else if (!isMuscle) bones.Add(go.GetComponent<Renderer>());
                }
            }
            if (s.Specimen is HandSpecimen hs)
            {
                D3 c = hs.Pose.ObjectAtIsocentre; Vector3 off = Frames.ToS(hs.Pose.Offset);
                specimen.localPosition = off - new Vector3((float)c.X, (float)c.Y, (float)-c.Z);
                specimen.localScale = new Vector3(1, 1, -1);
                specimen.gameObject.SetActive(true);
            }
            else specimen.gameObject.SetActive(false);
        }

        void BuildRegion(SimState s)
        {
            if (region == null)
            {
                // The proton block's outline (0.8.4): its twelve edges at the block's true size and orientation, white with a
                // dark casing and drawn over the skin, coils and magnet, which would hide a box a few millimetres across.
                Vector3 h = Frames.ToSAbs(Frames.ToS(Layouts.GridSize)) / 2; var lb = new LineBuilder();
                for (int a = 0; a < 3; a++)
                    for (int s1 = -1; s1 <= 1; s1 += 2)
                        for (int s2 = -1; s2 <= 1; s2 += 2)
                        {
                            int b = (a + 1) % 3, d = (a + 2) % 3; Vector3 p = Vector3.zero, q; p[b] = s1 * h[b]; p[d] = s2 * h[d]; q = p; p[a] = -h[a]; q[a] = h[a];
                            lb.Segment(p, q, 0.0019f, Color.white);
                        }
                var go = Mats.Object("Block outline", Physics, lb.Commit(new Mesh { name = "Block outline" }), Mats.Line(Look.SCAF, false, 0.36f, true));
                region = go.transform; regionRenderer = go.GetComponent<Renderer>();
                var col = go.AddComponent<BoxCollider>(); col.size = Vector3.one * 0.03f; col.isTrigger = true;
                go.AddComponent<Grabbable>().Kind = "region";
            }
            region.localPosition = Frames.ToS(s.CubeCentre);
        }

        /// <summary>Moves the region frame while it is being dragged (the simulation rebuilds on release).</summary>
        public void PreviewRegion(D3 centre) { if (region) region.localPosition = Frames.ToS(centre); }

        void BuildSlabs(SimState s)
        {
            foreach (var r in slabRenderers) Object.Destroy(r.transform.parent.gameObject);
            slabRenderers.Clear(); slabTex.Clear();
            int n = s.P.Matrix;
            for (int k = 0; k < s.Slices.Length; k++)
            {
                var tex = new Texture2D(n, n, TextureFormat.R8, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Image slice " + k };
                tex.SetPixelData(new byte[n * n], 0); tex.Apply(false);
                var go = new GameObject("Image slab " + k); go.transform.SetParent(Physics, false); go.transform.localPosition = Frames.ToS(0, 0, s.Slices[k].Z);
                var mat = Mats.Image(tex, false); // tissue opaque, dark pixels clear: reads over a bright room (0.8.4)
                Mats.Object("Layer", go.transform, MeshKit.Quad((float)Protocol.Fov, (float)Protocol.Fov), mat, Vector3.zero);
                go.SetActive(false); slabRenderers.Add(go.GetComponentInChildren<Renderer>()); slabTex.Add(tex);
            }
        }

        // ------------------------------------------------------------------------------------------------ plinth, receiver, signal

        GameObject Solid(string name, Transform parent, Mesh mesh, Color c, Vector3 pos, Quaternion? rot = null, Vector3? scale = null) =>
            Mats.Object(name, parent, mesh, Mats.Solid(c), pos, rot, scale);

        void BuildPlinth()
        {
            var st = Look.STRUCT;
            // The base (0.8.4: a saturated blue, not grey): rounded slabs for the plinth and the magnet cradles.
            // Open suspended apparatus: passthrough remains visible below the coils.
            foreach (float z in new[] { -0.26f, 0.26f }) Solid("Magnet cradle", Physics, MeshKit.RoundedPanel(0.40f, 0.10f, 0.025f, 0.07f, 6), st, Frames.ToS(0, -0.24, z), Quaternion.Euler(90, 0, 0));
            Solid("Hand tray", Physics, MeshKit.Box(new Vector3(0.13f, 0.004f, 0.46f)), Look.STRUCT * 1.25f, Frames.ToS(0, -0.056, 0.08));
            // Receiver chain on the plinth top, on the viewer's side (-x), spread along z so each block can carry its label:
            // ADC package, mixer can, six-plate filter.
            float y = -0.31f, x = -0.20f; double zAdc = -0.24, zMix = 0.0, zFilt = 0.24;
            var adc = Mats.Object("ADC", Physics, MeshKit.Box(new Vector3(0.07f, 0.035f, 0.07f)), Mats.Glass(new Color(0.7f,0.86f,1f,0.20f)), Frames.ToS(x, y + 0.01, zAdc));
            Solid("ADC sampling die", adc.transform, MeshKit.Box(new Vector3(0.035f,0.004f,0.035f)), Look.GX, Vector3.zero);
            for (int k = 0; k < 6; k++) Solid("ADC pin", adc.transform, MeshKit.Box(new Vector3(0.004f, 0.006f, 0.008f)), Look.TRACK, new Vector3(0.038f, -0.007f, -0.027f + 0.011f * k));
            var mix = Mats.Object("Mixer", Physics, MeshKit.Cylinder(0.025f, 0.05f), Mats.Glass(new Color(0.7f,0.86f,1f,0.20f)), Frames.ToS(x, y, zMix));
            Solid("Digital downconversion core", mix.transform, MeshKit.Box(new Vector3(0.025f,0.025f,0.025f)), Look.GY, Vector3.zero);
            var filt = new GameObject("Filter").transform; filt.SetParent(Physics, false); filt.localPosition = Frames.ToS(x, y, zFilt);
            var plate = Mats.Glass(new Color(0.7f,0.86f,1f,0.20f)); // one material: the whole filter lights when referenced
            for (int k = 0; k < 6; k++) Mats.Object("Filter stage", filt, MeshKit.Box(new Vector3(0.06f, 0.006f, 0.06f)), plate, new Vector3(0, 0.006f + 0.0095f * k, 0));
            rxBlocks[0] = adc.GetComponent<Renderer>(); rxBlocks[1] = mix.GetComponent<Renderer>(); rxBlocks[2] = filt.GetChild(5).GetComponent<Renderer>();
            string[] rk = { "rx.adc", "rx.mix", "rx.filter" };
            for (int k = 0; k < 3; k++) { var tr = k == 2 ? filt : rxBlocks[k].transform; eRx[k] = app.Attention.Register(rk[k], () => tr.position + Vector3.up * 0.02f, "scanner", "rx"); }
            // What each block does, its name and rate above it: shown while the narration names that block (the receiver is
            // introduced in step 1 and used in step 4), so its numbers are on screen exactly when they are spoken.
            rxLabels[0] = Labels.Make(Physics, "ADC\n122.88 MS/s", Frames.ToS(x, y + 0.13, zAdc), LocalValue, Look.TEXT);
            rxLabels[1] = Labels.Make(Physics, "mixer\ncos · sin", Frames.ToS(x, y + 0.13, zMix), LocalValue, Look.TEXT);
            rxLabels[2] = Labels.Make(Physics, "filter\n32 kS/s", Frames.ToS(x, y + 0.13, zFilt), LocalValue, Look.TEXT);
            // Cable (RF colour: it carries the coil's voltage) from the birdcage's +z end ring out of the bore, down the magnet's
            // end and back along the plinth, beside the chain (away from the viewer), into the ADC.
            var cable = new List<Vector3> { Frames.ToS(-Scanner.ReceiveRadius, Scanner.ReceiveY, 0), Frames.ToS(-0.09, Scanner.ReceiveY, 0.40), Frames.ToS(-0.10, -0.20, 0.405), Frames.ToS(-0.12, -0.302, 0.37), Frames.ToS(-0.13, -0.302, zAdc + 0.05), Frames.ToS(-0.165, -0.302, zAdc) };
            Solid("Receive cable", Physics, MeshKit.Tube(cable, 0.005f, 8), Look.RF * 0.8f, Vector3.zero);
            // Signal arrow on a dark dial past the magnet's +z end, in the transverse (x-y) plane the tipped spins turn in; the
            // arrow sits on the face's -z side, the side the default head pose sees. Its colour is the signal's phase.
            var dial = new GameObject("Signal dial").transform; dial.SetParent(Physics, false); dial.localPosition = Frames.ToS(-0.19, -0.20, 0.42);
            Solid("Dial post", Physics, MeshKit.Cylinder(0.009f, 0.05f, 12), st, Frames.ToS(-0.19, -0.31, 0.425));
            // Receiver phasor is freestanding in the transverse plane.
            Mats.Object("Dial ring", dial, MeshKit.Torus(0.088f, 0.0035f, 64, 6), Mats.Solid(Look.TEXT2), new Vector3(0, 0, -0.001f), Quaternion.Euler(90, 0, 0));
            var arrowHolder = new GameObject("Signal plane").transform; arrowHolder.SetParent(dial, false); arrowHolder.localPosition = new Vector3(0, 0, -0.008f);
            signalArrow = Mats.Object("Signal arrow", arrowHolder, MeshKit.Needle(0.04f, 0.1f, 0.2f), Mats.Solid(Look.MAG)).transform;
            signalRenderer = signalArrow.GetComponent<Renderer>();
            eSignal = app.Attention.Register("signal", () => dial.TransformPoint(new Vector3(0, 0.1f, 0)), "scanner", "signal");
            Labels.Make(dial, "I", new Vector3(-0.115f, 0, -0.01f), LocalValue, Look.TEXT); // P +x is S -x: the arrow's I axis
            Labels.Make(dial, "Q", new Vector3(0, 0.115f, -0.01f), LocalValue, Look.TEXT);
            Labels.Make(dial, "signal", new Vector3(0, -0.125f, -0.01f), LocalValue, Look.TEXT2);
            eSlabs = app.Attention.Register("image.slabs", () => SlabAnchor(), "scanner", "image");
            // The selected slab's outline (while a slice-selective repetition runs) and the field/frequency labels along the bore
            // (while the z gradient runs), above the magnet where nothing covers them.
            var outlineMat = Mats.Line(Look.RF, false, 0.3f);
            slabOutline = Mats.Object("Selected slab outline", Physics, slabMesh, outlineMat).GetComponent<Renderer>(); slabOutline.enabled = false;
            slabLabel = Labels.Make(Physics, "", Vector3.zero, LocalValue, Look.RF); slabLabel.gameObject.SetActive(false);
            for (int k = 0; k < 3; k++) { zLabels[k] = Labels.Make(Physics, "", Frames.ToS(0, 0.42, -0.20 + 0.20 * k), LocalValue, Look.TEXT, TMPro.TextAlignmentOptions.Center, 0.2f); zLabels[k].gameObject.SetActive(false); }
        }

        void BuildTriad()
        {
            // x, y, z in the standard colours at a plinth corner; B0 named beside the field lines where they leave the bore.
            var o = Vector3.zero;
            D3[] dirs = { new D3(1, 0, 0), new D3(0, 1, 0), new D3(0, 0, 1) };
            string[] keys = { "axis.x", "axis.y", "axis.z" };
            for (int k = 0; k < 3; k++)
            {
                var d = Frames.ToS(dirs[k]);
                var go = Mats.Object("Axis " + keys[k], Physics, MeshKit.Needle(0.03f, 0.08f, 0.22f), Mats.Solid(Look.Axis(k)), o + d * 0.075f, Quaternion.FromToRotation(Vector3.up, d), Vector3.one * 0.15f);
                axis.Add(go.GetComponent<Renderer>());
                var tr = go.transform;
                eAxis[k] = app.Attention.Register(keys[k], () => tr.position + tr.up * 0.04f, "scanner", "axes");
                Labels.Make(Physics, k == 0 ? "x" : k == 1 ? "y" : "z", o + d * 0.18f, LocalLabel, Look.Axis(k));
            }
            b0Label = Labels.Make(Physics, "B₀", Frames.ToS(0, 0.11, -0.24), LocalValue, Look.B0); // above the magnet's -z end
            mainLabel = Labels.Make(Physics, "", Frames.ToS(0, 0.33, 0.10), LocalValue, Look.B0);
        }

        /// <summary>Four zoom lines from the block's outline in the hand to the magnified block, drawn over everything.</summary>
        void BuildZoom(Transform parent)
        {
            var lb = new LineBuilder(); lb.Segment(Vector3.zero, Vector3.forward, 0.0022f, Color.white);
            var mesh = lb.Commit(new Mesh { name = "Unit segment" });
            zoomMat = Mats.Line(Look.SCAF, false, 0.36f, true); zoomMat.SetFloat("_ScaleWidth", 0);
            for (int k = 0; k < 4; k++) zoom[k] = Mats.Object("Zoom line", parent, mesh, zoomMat).transform;
        }

        /// <summary>
        /// Places the zoom lines: the face of the outline that faces the block and the block's face that faces the outline
        /// (both have the same orientation), corner to matching corner.
        /// </summary>
        void TickZoom()
        {
            var cube = app.Cube; bool on = region != null && cube != null && region.gameObject.activeInHierarchy;
            for (int k = 0; k < 4; k++) if (zoom[k].gameObject.activeSelf != on) zoom[k].gameObject.SetActive(on);
            if (!on) return;
            Vector3 hs = Vector3.Scale(Frames.ToSAbs(Frames.ToS(Layouts.GridSize)) / 2, Physics.lossyScale), hb = Vector3.Scale(CubeView.DisplaySize / 2, cube.Root.lossyScale);
            Vector3 o = region.position, c = cube.Root.position, u = (c - o).normalized;
            Quaternion r = cube.Root.rotation; Vector3[] ax = { r * Vector3.right, r * Vector3.up, r * Vector3.forward };
            int n = 0; float best = -1; for (int k = 0; k < 3; k++) { float dd = Mathf.Abs(Vector3.Dot(ax[k], u)); if (dd > best) { best = dd; n = k; } }
            float sign = Mathf.Sign(Vector3.Dot(ax[n], u)); int b = (n + 1) % 3, d = (n + 2) % 3;
            for (int k = 0; k < 4; k++)
            {
                float sb = (k & 1) == 0 ? -1 : 1, sd = (k & 2) == 0 ? -1 : 1;
                Vector3 from = o + ax[n] * (sign * hs[n]) + ax[b] * (sb * hs[b]) + ax[d] * (sd * hs[d]);
                Vector3 to = c - ax[n] * (sign * hb[n]) + ax[b] * (sb * hb[b]) + ax[d] * (sd * hb[d]);
                Vector3 ab = to - from; if (ab.sqrMagnitude < 1e-8f) continue;
                zoom[k].position = from; zoom[k].rotation = Quaternion.LookRotation(ab); zoom[k].localScale = new Vector3(1, 1, ab.magnitude);
            }
        }

        // ------------------------------------------------------------------------------------------------ per frame

        /// <summary>Update every visual from the simulation at the current physical time (Sim.Update already done).</summary>
        /// <summary>carrier: the displayed carrier phase (the lab frame turns with it; App.DisplayCarrier).</summary>
        public void Tick(float dt, bool playing, double carrier, bool fresh = true)
        {
            var sim = app.Sim; var s = sim.State; if (s == null) return;
            if (s.Revision != shownRevision) OnState(s);
            var p = s.P;
            // Coils.
            double iB0 = p.MagnetCurrent, iMaxB0 = Protocol.MaxB0 / Scanner.BIso;
            TickCoil(coils[0], (float)(iB0 / iMaxB0), 1, dt, playing);
            TickCoil(coils[1], (float)(sim.Ix / 100), Mathf.Sign((float)sim.Ix), dt, playing);
            TickCoil(coils[2], (float)(sim.Iy / 100), Mathf.Sign((float)sim.Iy), dt, playing);
            TickCoil(coils[3], (float)(sim.Iz / 100), Mathf.Sign((float)sim.Iz), dt, playing);
            TickBirdcage(carrier);
            receiveProperties.SetFloat("_Body", 0.45f + 0.45f * eReceive.E);
            receiveProperties.SetFloat("_Opacity", app.Overview ? 0.20f : 0.10f + 0.90f * eReceive.E);
            coilLabels[0].gameObject.SetActive(app.Overview || coils[0].E.E>0.4f);
            coilLabels[1].gameObject.SetActive(app.Overview || Mathf.Max(coils[1].E.E,Mathf.Max(coils[2].E.E,coils[3].E.E))>0.4f);
            coilLabels[2].gameObject.SetActive(app.Overview || coils[4].E.E>0.4f);
            coilLabels[3].gameObject.SetActive(app.Overview || eReceive.E>0.4f);
            receiveProperties.SetFloat("_Rim", 0.12f + 0.2f * eReceive.E);
            receiveCoil.SetPropertyBlock(receiveProperties);
            // Field lines: dB = dot(field x 1e4, I x 1e-4) in the shader; brightness follows emphasis and the field strength.
            float e = eField.E;
            fieldLines.enabled = app.ShowFields;
            fieldMat.SetVector("_Currents", new Vector4((float)(iB0 * 1e-4), (float)(sim.Ix * 1e-4), (float)(sim.Iy * 1e-4), (float)(sim.Iz * 1e-4)));
            fieldMat.SetColor("_Color", Look.B0 * ((0.7f + 0.3f * e) * Mathf.Clamp((float)p.B0, 0.5f, 1.2f)));
            TickB1(carrier);
            TickSlabAndLabels(s, sim);
            Labels.Set(b0Label, $"B₀ {p.B0:0.00} T");
            // The magnet's current: named in the introduction, so shown while the magnet is named.
            bool magnetNamed = app.Attention.Refs != null && coils[0].E.E > 0.5f; if (mainLabel.gameObject.activeSelf != magnetNamed) mainLabel.gameObject.SetActive(magnetNamed);
            if (magnetNamed) Labels.Set(mainLabel, $"main magnet · {p.MagnetCurrent:0} A");
            // Hand glow.
            // Hand glow: the worker filled the data with the last hand evaluation; upload it only then.
            if (fresh && sim.HandFresh && glowRevision == s.Revision) { glowTex.SetPixelData(glowData, 0); glowTex.Apply(false); sim.HandFresh = false; }
            glow.enabled = glowPeak > 0.02f; // only when some tissue is tipped
            glow.sharedMaterial.SetFloat("_Gain", 180f * (0.5f + 0.5f * eGlow.E));
            // Anatomy: faint unless referenced.
            float eh = eHand.E;
            foreach (var r in bones) r.sharedMaterial.SetColor("_Color", Look.BONE * (0.55f + 0.45f * eh));
            if (skin) { var sc = Look.SKIN; sc.a = 0.16f + 0.20f * eh; skin.sharedMaterial.SetColor("_Color", sc); }
            if (muscleMat) { var mc = Look.MUSCLE; mc.a = 0.30f + 0.18f * eh; muscleMat.SetColor("_Color", mc); }
            TickZoom(); // the outline and zoom lines stay pure white (a dimmed white would be grey)
            // Axes, receiver blocks.
            for (int k = 0; k < 3; k++) axis[k].sharedMaterial.SetColor("_Glow", Look.Axis(k) * (0.15f + 0.45f * eAxis[k].E));
            for (int k = 0; k < 3; k++)
            {
                if (rxBlocks[k].sharedMaterial.HasProperty("_Glow")) rxBlocks[k].sharedMaterial.SetColor("_Glow", Look.TEXT2 * (0.6f * eRx[k].E * (s.Program != null ? 1 : 0)));
                bool named = app.Attention.Refs != null && eRx[k].E > 0.5f; if (rxLabels[k].gameObject.activeSelf != named) rxLabels[k].gameObject.SetActive(named);
            }
            // Signal arrow: the most recent receiver sample, in the transverse plane of the scanner frame.
            float mag = sim.SignalOn ? (float)System.Math.Sqrt(sim.SignalRe * sim.SignalRe + sim.SignalIm * sim.SignalIm) : 0;
            if (mag > 1e-3f)
            {
                signalArrow.gameObject.SetActive(true);
                Vector3 d = Frames.ToS(sim.SignalRe, sim.SignalIm, 0).normalized;
                float len = 0.075f * Mathf.Min(1.2f, mag);
                signalArrow.localRotation = Quaternion.FromToRotation(Vector3.up, d);
                signalArrow.localPosition = d * (len * 0.5f); signalArrow.localScale = new Vector3(0.13f, len, 0.13f);
            }
            else signalArrow.gameObject.SetActive(false);
            // Its colour is the signal's phase (the same colours as the protons: the signal's phase is their shared phase).
            var sigCol = mag > 1e-3f ? Look.Phase(System.Math.Atan2(sim.SignalIm, sim.SignalRe), 1f) : Look.MAG;
            signalRenderer.sharedMaterial.SetColor("_Color", sigCol); signalRenderer.sharedMaterial.SetColor("_Glow", sigCol * (0.2f + 0.3f * eSignal.E));
            foreach (var r in slabRenderers) r.sharedMaterial.SetFloat("_Fade", 0.75f + 0.25f * eSlabs.E);
        }

        /// <summary>B1 arrows: A(t) b1(r) at each sample, turned by the displayed carrier (the lab frame), drawn while RF is on.</summary>
        void TickB1(double carrier)
        {
            var sim = app.Sim; b1Count = 0; if (!sim.RfOn || !app.ShowFields) return;
            double c = System.Math.Cos(-carrier), sn = System.Math.Sin(-carrier), full = 0.75 * Scanner.B1Iso; int n = 0; float e = eB1.E;
            Matrix4x4 P = Physics.localToWorldMatrix;
            for (int k = 0; k < B1N; k++)
            {
                double bx = sim.RfRe * b1Re[k] - sim.RfIm * b1Im[k], by = sim.RfRe * b1Im[k] + sim.RfIm * b1Re[k];
                double lx = bx * c - by * sn, ly = bx * sn + by * c, m = System.Math.Sqrt(lx * lx + ly * ly);
                if (m < 0.02 * full) continue;
                Vector3 d = Frames.ToS(lx / m, ly / m, 0); float len = 0.045f * Mathf.Min(1.3f, (float)(m / full));
                var rot = Quaternion.FromToRotation(Vector3.up, d);
                b1Mats[n] = P * Matrix4x4.TRS(b1At[k] + d * (len * 0.5f), rot, new Vector3(0.06f, len, 0.06f));
                b1Tints[n] = new Vector4(Look.RF.r, Look.RF.g, Look.RF.b, 1) * (0.7f + 0.5f * e); b1Tints[n].w = 1; n++;
            }
            b1Count = n; if (n == 0) return;
            b1Block.SetVectorArray("_Tint", b1Tints);
            var rp = new RenderParams(b1Mat) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false, layer = Root.gameObject.layer, matProps = b1Block, worldBounds = new Bounds(Physics.position, Vector3.one * 0.6f) };
            Graphics.RenderMeshInstanced(rp, b1Mesh, 0, b1Mats, n);
        }

        /// <summary>Re-submits the B1 arrows for one camera (validation captures).</summary>
        public void Submit(Camera cam)
        {
            if (b1Count == 0) return;
            var rp = new RenderParams(b1Mat) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false, layer = Root.gameObject.layer, matProps = b1Block, camera = cam, worldBounds = new Bounds(Physics.position, Vector3.one * 0.6f) };
            Graphics.RenderMeshInstanced(rp, b1Mesh, 0, b1Mats, b1Count);
        }

        /// <summary>The selected slab's outline while a slice-selective repetition runs; |B| and frequency labels along z while Gz runs.</summary>
        void TickSlabAndLabels(SimState s, Simulation sim)
        {
            int bi = sim.BlockIndex; bool selective = bi >= 0 && s.Program.Blocks[bi].Selective;
            double z = s.P.SliceZ, dz = s.P.SliceThickness;
            if (selective && (z != slabZ || dz != slabDz))
            {
                slabZ = z; slabDz = dz; var lb = new LineBuilder();
                foreach (double zz in new[] { z - dz / 2, z + dz / 2 })
                {
                    Vector3 a = Frames.ToS(-0.075, -0.05, zz), b = Frames.ToS(0.075, -0.05, zz), c = Frames.ToS(0.075, 0.05, zz), d = Frames.ToS(-0.075, 0.05, zz);
                    lb.Segment(a, b, 0.0032f, Color.white); lb.Segment(b, c, 0.0032f, Color.white); lb.Segment(c, d, 0.0032f, Color.white); lb.Segment(d, a, 0.0032f, Color.white);
                }
                lb.Commit(slabMesh);
            }
            if (slabOutline.enabled != selective) slabOutline.enabled = selective;
            if (slabLabel.gameObject.activeSelf != selective) slabLabel.gameObject.SetActive(selective);
            if (selective) { slabLabel.transform.localPosition = Frames.ToS(-0.31, -0.36, z); Labels.Set(slabLabel, $"slab {dz * 1e3:0.0} mm"); } // before the base's front face, never hidden
            bool gz = System.Math.Abs(sim.Iz) > 0.5;
            for (int k = 0; k < 3; k++) if (zLabels[k].gameObject.activeSelf != gz) zLabels[k].gameObject.SetActive(gz);
            if (!gz || Time.unscaledTime < nextLabels && !app.FixedStep) return;
            nextLabels = Time.unscaledTime + 0.2f;
            var t = s.Tables;
            for (int k = 0; k < 3; k++)
            {
                double zz = -0.20 + 0.20 * k; var f = HazeAt(t, new D3(0, 0, zz));
                double dB = f.x * s.P.MagnetCurrent + f.y * sim.Ix + f.z * sim.Iy + f.w * sim.Iz; // T
                Labels.Set(zLabels[k], $"{Constants.GammaBar * dB / 1e3:+0.0;-0.0;0.0} kHz");
            }
        }

        void TickCoil(CoilVisual cv, float i, float sign, float dt, bool playing)
        {
            float e = app.Overview ? 0.15f : cv.E.E, ia = Mathf.Min(1, Mathf.Abs(i));
            // The magnet's packs are large surfaces: same rules, lower gains so the always-on magnet does not swamp the scene.
            bool pack = cv.Id == CoilId.B0;
            // Packs (0.8.4): solid cyan blocks, brighter when named, no moving markers (the magnet's current never changes);
            // tubes are solid, emissive, never dim below the floor.
            float body = 0.45f + 0.45f * e;
            float flow = 0.45f * ia * e;
            float speed = playing ? sign * 0.08f / Look.ScannerScale * Mathf.Sqrt(ia) : 0, now = Time.timeSinceLevelLoad, rim = 0.15f * e;
            foreach (var part in cv.Parts)
            {
                float ps = part.Sign * speed;
                if (!part.Changed(body, flow, rim) && Mathf.Abs(ps - part.Speed) < 1e-5f) continue; // nothing changed: no update
                part.Off += now * (part.Speed - ps); part.Speed = ps; // keeps the dashes where they are when the speed changes
                part.LastBody = body; part.LastFlow = flow; part.LastRim = rim;
                part.B.SetFloat("_Opacity", 0.08f + 0.92f * e); part.B.SetFloat("_Body", body); part.B.SetFloat("_Flow", flow); part.B.SetFloat("_Rim", rim);
                part.B.SetFloat("_Offset", part.Off); part.B.SetFloat("_FlowSpeed", ps); part.B.SetFloat("_DashLen", 0.05f);
                part.R.SetPropertyBlock(part.B);
            }
        }

        void TickBirdcage(double carrier)
        {
            var sim = app.Sim; var cv = coils[4]; float e = app.Overview ? 0.15f : cv.E.E;
            double amp = System.Math.Sqrt(sim.RfRe * sim.RfRe + sim.RfIm * sim.RfIm), arg = System.Math.Atan2(sim.RfIm, sim.RfRe);
            // Rung n carries |A| sin(phi_n + theta), theta = carrier phase - arg A (the displayed carrier: the lab frame).
            double theta = carrier - arg;
            double sp = amp * System.Math.Cos(theta), cp = amp * System.Math.Sin(theta);
            // Receiving (T/R switch in receive): the rungs glow with the instantaneous induced EMF.
            float recv = 0; // the dedicated surface loop receives; the transmit birdcage is idle
            foreach (var part in cv.Parts)
            {
                double cur = sim.RfOn ? Scanner.PartCurrent(part.Index, sp, cp) : 0;
                float i = Mathf.Min(1, Mathf.Abs((float)cur) / 0.75f);
                float body = Look.CoilFloor + 0.30f * e + 0.45f * i * (0.4f + 0.6f * e) + 0.25f * recv * (0.3f + 0.7f * e), rim = 0.15f * e;
                float flow = 0.4f * i * e;
                float speed = app.Playing ? Mathf.Sign((float)cur) * 0.08f * i : 0;
                if (!part.Changed(body, flow, rim) && Mathf.Abs(speed - part.Speed) < 1e-5f) continue;
                part.Off += Time.timeSinceLevelLoad * (part.Speed - speed); part.Speed = speed;
                part.LastBody = body; part.LastFlow = flow; part.LastRim = rim;
                part.B.SetFloat("_Opacity", 0.08f + 0.92f * e); part.B.SetFloat("_Body", body); part.B.SetFloat("_Flow", flow); part.B.SetFloat("_Rim", rim); part.B.SetFloat("_Offset", part.Off); part.B.SetFloat("_FlowSpeed", speed); part.B.SetFloat("_DashLen", 0.05f);
                part.R.SetPropertyBlock(part.B);
            }
        }

        /// <summary>
        /// Fills the glow texture data from the hand evaluation (runs on the spin worker, or inline when synchronous; the
        /// main thread uploads it only when the worker is idle). Local copies guard against a state change meanwhile.
        /// </summary>
        void FillGlow(SimState s)
        {
            var data = glowData; int nx = glowNx, ny = glowNy;
            var ev = s.HandEval; var h = s.Hand; if (data == null || ev == null || s.Revision != glowRevision || data.Length != h.Nx * h.Ny * h.Nz) return;
            float peak = 0;
            for (int i = 0; i < h.N; i++)
            {
                int l = h.Lattice[i], ix = l % h.Nx, rest = l / h.Nx, iy = rest % h.Ny, iz = rest / h.Ny;
                int dst = (iz * ny + iy) * nx + (nx - 1 - ix);
                double perp = System.Math.Sqrt(ev.X[i] * ev.X[i] + ev.Y[i] * ev.Y[i]);
                double v = perp; // tipped magnetization only: the glow is opaque where it is dense (0.8.4), so M_z must not haze the hand
                data[dst] = (byte)System.Math.Min(255, System.Math.Round(v * 255));
                if (perp > peak) peak = (float)perp;
            }
            glowPeak = peak;
        }

        /// <summary>Shows or hides the image slab of slice k (the console decides from the acquisition).</summary>
        public void SetSlabVisible(int k, bool on) { if (k < slabRenderers.Count) { var go = slabRenderers[k].transform.parent.gameObject; if (go.activeSelf != on) go.SetActive(on); } }

        /// <summary>The reconstructed image of slice k (shared with the 3-D image in the data plot).</summary>
        public Texture2D SlabTexture(int k) => k >= 0 && k < slabTex.Count ? slabTex[k] : null;

        /// <summary>Sets the reconstructed image of slice k (bytes, n x n, x already flipped into the S frame).</summary>
        public void SetSlabImage(int k, byte[] bytes) { if (k < slabTex.Count) { slabTex[k].SetPixelData(bytes, 0); slabTex[k].Apply(false); } }
    }
}
