using System.Collections.Generic;
using Nebulytic.Resonance.Sim;
using TMPro;
using UnityEngine;

namespace Nebulytic.Resonance
{
    /// <summary>A persistent 6x6x8 lattice with dynamic net-moment meshes, plus cue-driven ideal Bloch examples.</summary>
    public sealed class CubeView
    {
        readonly App app;
        readonly Mesh visibleMesh = new Mesh { name="Visible moment lattice" };
        readonly Mesh phaseMesh = new Mesh { name="Transverse phase projections" };
        Vector3[] phasePoints,phaseNormals; Color[] phaseColours;
        readonly Mesh latticeMesh = new Mesh { name="Persistent sample lattice" };
        Vector3[] meshPoints,meshNormals,needlePoints,needleNormals; Color[] meshColours; int[] meshIndices,needleIndices;
        readonly TextMeshPro heading,example,sourceA,sourceB,sumCaption; readonly Transform sumVector;
        public bool ConventionalGrid => visibleMesh.vertexCount>0;

        readonly TissueDetail tissue;
        public readonly Transform Root;
        readonly Mesh needle, ring; readonly Material opaque, faded;
        Matrix4x4[] matrices = new Matrix4x4[0], ringMats = new Matrix4x4[0]; Vector4[] tints = new Vector4[0]; float[] emph = new float[0]; byte[] kind = new byte[0];
        Vector3[] localPos = new Vector3[0]; bool[] member = new bool[0]; string memberRef; int memberRevision = -1;
        readonly List<MaterialPropertyBlock> blocks = new List<MaterialPropertyBlock>();
        readonly Matrix4x4[] batch = new Matrix4x4[1023]; readonly Vector4[] batchTint = new Vector4[1023];
        readonly Transform b1Vector; readonly Renderer b1Renderer, frameRenderer; readonly Renderer[] axis = new Renderer[4];
        readonly Element eFrame, eB1, eField; readonly Element[] eAxis = new Element[3];
        // Gradient arrow (0.8.4): a flat tapered arrow that always faces the eye, along the block edge nearest the eye, three
        // labels, and the far walls glowing in the gradient's colour toward the stronger field.
        readonly Transform gradArrow, gradRod; readonly Renderer gradRodR; readonly Material wallMat; readonly TextMeshPro gradName, gradLow, gradHigh;
        int gradAxis = -1; float nextGradLabels; Vector3 gradMid;
        Vector3[] biasDir = new Vector3[0], isoDir = new Vector3[0]; bool[] present = new bool[0]; float[] spinPhase = new float[0];
        double[] phaseNow = new double[0], tipNow = new double[0];
        readonly Transform halo; readonly Renderer haloRenderer;
        /// <summary>Cells that show a proton (every tissue cell; air cells are empty).</summary>
        public int PresentCount { get; private set; }
        public bool Present(int i) => i >= 0 && i < present.Length && present[i];
        /// <summary>The selected proton (the close-up shows it); -1 before the first state.</summary>
        public int Selected { get; private set; } = -1;
        int revision = -1; double colX; Vector3[] worldPos = new Vector3[0];
        public int DrawnNeedles { get; private set; }
        /// <summary>Displayed size of the block: its physical size times one magnification.</summary>
        public static Vector3 DisplaySize => Frames.ToSAbs(Frames.ToS(Layouts.GridSize) * Look.BlockMagnification);
        /// <summary>Displayed mean spacing between protons (m).</summary>
        public static float Spacing => (float)Layouts.ProtonSpacing * Look.BlockMagnification;
        public Vector3 CellLocal(int i) => i >= 0 && i < localPos.Length ? localPos[i] : Vector3.zero;
        public Vector3 CellWorld(int i) => i >= 0 && i < worldPos.Length ? worldPos[i] : Root.position;
        /// <summary>Triangles of one proton (needle and ring), for the frame-cost report.</summary>
        public int NeedleTriangles => (int)(needle.GetIndexCount(0) / 3);
        /// <summary>The axis (0 x, 1 y, 2 z) along which the shown gradient runs, or -1.</summary>
        public int GradientAxis => gradAxis;
        public Renderer GradientRod => gradRodR;

        public CubeView(App app, Transform parent)
        {
            this.app = app;
            Root = new GameObject("Proton block").transform; Root.SetParent(parent, false);
            needle = MeshKit.Needle(0.07f, 0.17f, 0.20f,12,16); ring = SpinRing(); opaque = Mats.Needle(false); faded = Mats.Needle(true);
            var gridMat=Mats.Needle(true); gridMat.enableInstancing=false; gridMat.SetFloat("_VertexTint",1);
            visibleMesh.MarkDynamic(); Mats.Object("Visible moments",Root,visibleMesh,gridMat);
            phaseMesh.MarkDynamic(); var phaseMat=Mats.Needle(true); phaseMat.enableInstancing=false; phaseMat.SetFloat("_VertexTint",1); phaseMat.renderQueue=3003;
            Mats.Object("Transverse phase projections",Root,phaseMesh,phaseMat);
            Mats.Object("Persistent lattice",Root,latticeMesh,Mats.Line(Look.Hex(0x7FA6B9),false,0.25f));
            needlePoints=needle.vertices; needleNormals=needle.normals; needleIndices=needle.triangles;
            Vector3 size = DisplaySize, half = size / 2; float mm = 0.001f * Look.BlockMagnification;
            // Far walls: dark, with a millimetre grid; drawn from inside, so they are always the walls behind the protons.
            wallMat = new Material(Shader.Find("Resonance/Backdrop"));
            wallMat.SetColor("_Color", Look.PANEL); wallMat.SetColor("_Line", Look.TRACK); wallMat.SetVector("_Grid", new Vector4(mm, mm, mm, 0));
            tissue = new TissueDetail(app, Root);
            // Twelve white edges with a dark casing.
            var fb = new LineBuilder();
            for (int a = 0; a < 3; a++)
                for (int s1 = -1; s1 <= 1; s1 += 2)
                    for (int s2 = -1; s2 <= 1; s2 += 2)
                    {
                        int b = (a + 1) % 3, d = (a + 2) % 3; Vector3 p = Vector3.zero, q; p[b] = s1 * half[b]; p[d] = s2 * half[d]; q = p; p[a] = -half[a]; q[a] = half[a];
                        // Short corner ticks mark the sampled volume without a cage of edges.
                        fb.Segment(p, Vector3.Lerp(p, q, 0.07f), 0.0016f, Color.white);
                        fb.Segment(Vector3.Lerp(p, q, 0.93f), q, 0.0016f, Color.white);
                    }
            frameRenderer = Mats.Object("Block edges", Root, fb.Commit(new Mesh { name = "Block edges" }), Mats.Line(Look.SCAF, false, 0.34f)).GetComponent<Renderer>();
            eFrame = app.Attention.Register("cube.frame", () => NearestCorner(), "cube", "region");
            b1Vector = Mats.Object("B1 vector", Root, MeshKit.Needle(0.035f, 0.085f, 0.24f), Mats.Solid(Look.RF)).transform;
            b1Renderer = b1Vector.GetComponent<Renderer>();
            eB1 = app.Attention.Register("cube.b1", () => b1Vector.position + b1Vector.up * b1Vector.localScale.y * 0.5f, "field.b1");
            // The gradient arrow (hidden while no gradient runs).
            gradArrow = new GameObject("Gradient arrow").transform; gradArrow.SetParent(Root, false);
            {
                // A unit arrow along +Z (scaled to the block's length): the shaft widens from 5 to 20 mm, then a 40 mm head.
                var ab = new LineBuilder(); const int n = 8;
                for (int k = 0; k < n; k++) { float z0 = 0.84f * k / n, z1 = 0.84f * (k + 1) / n; ab.Taper(new Vector3(0, 0, z0), new Vector3(0, 0, z1), 0.005f + 0.015f * z0 / 0.84f, 0.005f + 0.015f * z1 / 0.84f, Color.white); }
                ab.Taper(new Vector3(0, 0, 0.84f), new Vector3(0, 0, 1f), 0.042f, 0.0015f, Color.white);
                var am = Mats.Line(Look.GX, false, 0.22f); am.SetFloat("_ScaleWidth", 0);
                gradRod = Mats.Object("Arrow", gradArrow, ab.Commit(new Mesh { name = "Gradient arrow" }), am).transform; gradRodR = gradRod.GetComponent<Renderer>();
            }
            gradName = Labels.Make(gradArrow, "", Vector3.zero, Look.LabelSize, Look.GX);
            gradLow = Labels.Make(gradArrow, "", Vector3.zero, Look.ValueSize, Look.TEXT);
            gradHigh = Labels.Make(gradArrow, "", Vector3.zero, Look.ValueSize, Look.TEXT);
            gradArrow.gameObject.SetActive(false);
            eField = app.Attention.Register("cube.field", () => Root.TransformPoint(gradMid), "field.b", "gradient.arrow");
            // x, y, z triad (standard colours) on the corner nearest the default view (physical -x, -y, -z), with B0 beside z.
            var corner = Vector3.zero;
            D3[] dirs = { new D3(1, 0, 0), new D3(0, 1, 0), new D3(0, 0, 1) }; string[] keys = { "axis.x", "axis.y", "axis.z" }; string[] names = { "x", "y", "z" };
            for (int k = 0; k < 3; k++)
            {
                var d = Frames.ToS(dirs[k]);
                var go = Mats.Object("Block axis " + keys[k], Root, MeshKit.Needle(0.03f, 0.08f, 0.22f), Mats.Solid(Look.Axis(k)), corner + d * 0.045f, Quaternion.FromToRotation(Vector3.up, d), Vector3.one * 0.09f);
                axis[k] = go.GetComponent<Renderer>(); var tr = go.transform;
                eAxis[k] = app.Attention.Register("cube." + keys[k], () => tr.position + tr.up * 0.045f, keys[k], "axes");
                Labels.Make(Root, names[k], corner + d * 0.11f, Look.LabelSize, Look.Axis(k));
            }
            {
                // B0 along the block's top edge on the viewer's side (physical -x, +y), clear of the triad.
                var dz = Frames.ToS(0, 0, 1); var at = Vector3.Scale(Frames.ToS(-1, 1, 0), half) + Frames.ToS(-0.03, 0.035, 0);
                var go = Mats.Object("Block B0", Root, MeshKit.Needle(0.035f, 0.09f, 0.2f), Mats.Solid(Look.B0), at, Quaternion.FromToRotation(Vector3.up, dz), new Vector3(0.12f, 0.2f, 0.12f));
                axis[3] = go.GetComponent<Renderer>();
                Labels.Make(Root, "B₀", at + dz * 0.13f, Look.LabelSize, Look.B0);
            }
            heading = Labels.Make(Root, "6 × 6 × 8 tissue moments · z is the long axis", new Vector3(0, half.y + 0.18f, 0), Look.ValueSize, Look.TEXT, TextAlignmentOptions.Center, 0.6f);
            sumVector=Mats.Object("Summed transverse signal",Root,MeshKit.Needle(0.055f,0.15f,0.2f),Mats.Solid(Look.Hex(0xF5C76C))).transform;
            sumCaption=Labels.Make(Root,"Signal sum · I/Q",new Vector3(-DisplaySize.x/2-0.09f,-0.07f,0),Look.ValueSize,Look.TEXT);
            sourceA=Labels.Make(Root,"A",Frames.ToS(Layouts.BlockX/12,-Layouts.BlockY/4,Layouts.BlockZ/16)*Look.BlockMagnification+Vector3.up*0.025f,Look.ValueSize,Look.TEXT);
            sourceB=Labels.Make(Root,"B",Frames.ToS(Layouts.BlockX/12,Layouts.BlockY/4,Layouts.BlockZ/16)*Look.BlockMagnification+Vector3.up*0.025f,Look.ValueSize,Look.TEXT);
            example=Labels.Make(Root,"",new Vector3(0,half.y+0.13f,0),Look.ValueSize,Look.TEXT,TextAlignmentOptions.Center,0.7f);
            // The selected proton's halo.
            var focus=new LineBuilder();
            for(int j=0;j<48;j++) if(j%12<9) { float a=j*Mathf.PI/24,b=(j+1)*Mathf.PI/24; focus.Segment(new Vector3(Mathf.Cos(a)*0.5f,0,Mathf.Sin(a)*0.5f),new Vector3(Mathf.Cos(b)*0.5f,0,Mathf.Sin(b)*0.5f),0.0018f,Color.white); }
            var focusMat=Mats.Line(Look.TEXT,false,0.38f,true); focusMat.SetFloat("_ScaleWidth",0);
            halo = Mats.Object("Selected proton halo", Root, focus.Commit(new Mesh {name="Open focus halo"}),focusMat).transform; haloRenderer = halo.GetComponent<Renderer>(); haloRenderer.enabled = false;
            var col = Root.gameObject.AddComponent<BoxCollider>(); col.size = size; col.isTrigger = true;
            Root.gameObject.AddComponent<Grabbable>().Kind = "cube";
            app.Attention.Resolvers["spins."] = SpinAnchor;
        }

        /// <summary>The spin ring: a ring about the needle's axis (mesh +Y) with one bead, so its turning is visible.</summary>
        static Mesh SpinRing()
        {
            var ci = new[] {
                new CombineInstance { mesh = MeshKit.Torus(1f, 0.11f, 24, 5), transform = Matrix4x4.identity },
                new CombineInstance { mesh = MeshKit.Sphere(0.26f, 8, 5), transform = Matrix4x4.Translate(new Vector3(1, 0, 0)) } };
            var m = new Mesh { name = "Spin ring" }; m.CombineMeshes(ci, true, true); m.RecalculateBounds(); return m;
        }

        Vector3 NearestCorner()
        {
            Vector3 eye = app.Eye, best = Root.position; float bd = float.MaxValue; Vector3 h = DisplaySize / 2;
            for (int k = 0; k < 8; k++)
            {
                var c = Root.TransformPoint(new Vector3((k & 1) == 0 ? -h.x : h.x, (k & 2) == 0 ? -h.y : h.y, (k & 4) == 0 ? -h.z : h.z));
                float d = (c - eye).sqrMagnitude; if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        void OnState(SimState s)
        {
            revision = s.Revision; var cube = s.Cube; int n = cube.N;
            if (matrices.Length != n)
            {
                matrices = new Matrix4x4[n]; ringMats = new Matrix4x4[n]; tints = new Vector4[n]; emph = new float[n]; kind = new byte[n]; localPos = new Vector3[n]; member = new bool[n];
                biasDir = new Vector3[n]; isoDir = new Vector3[n]; present = new bool[n]; spinPhase = new float[n]; worldPos = new Vector3[n]; phaseNow = new double[n]; tipNow = new double[n];
                for (int i = 0; i < n; i++) emph[i] = 1;
            }
            // Display positions: the block's physical offsets times one magnification.
            for (int i = 0; i < n; i++) localPos[i] = Frames.ToS(cube.Pos[i] - s.CubeCentre) * Look.BlockMagnification;
            tissue.Rebuild(s);
            var grid=new LineBuilder();
            for(int i=0;i<n;i++) { var q=localPos[i]; float h=0.0025f; grid.Segment(q-Vector3.right*h,q+Vector3.right*h,0.002f,Color.white); grid.Segment(q-Vector3.up*h,q+Vector3.up*h,0.002f,Color.white); }
            // Sparse depth rails preserve the volume's structure without enclosing it in opaque walls.
            for(int y=0;y<6;y++) for(int x=0;x<6;x++) if(x==0||x==5||y==0||y==5) { var q=Frames.ToS((x+0.5)/6*Layouts.BlockX-Layouts.BlockX/2,(y+0.5)/6*Layouts.BlockY-Layouts.BlockY/2,-Layouts.BlockZ*0.4375)*Look.BlockMagnification; grid.Segment(q,q+Vector3.forward*(float)(Layouts.BlockZ*0.875*Look.BlockMagnification),0.0007f,Color.white); }
            grid.Commit(latticeMesh);

            memberRevision = -1;
            // "Spins sharing an x": the 0.7 mm thick x-slab of the band that holds the most mobile protons.
            double half = s.P.BandwidthEff / 2; int bestCount = -1; colX = s.CubeCentre.X;
            for (int j = 0; j < n; j++)
            {
                if (cube.Pd[j] <= 0 || System.Math.Abs(s.CubeDfSlice[j]) > half) continue;
                int c = 0; for (int i = 0; i < n; i++) if (cube.Pd[i] > 0 && System.Math.Abs(s.CubeDfSlice[i]) <= half && System.Math.Abs(cube.Pos[i].X - cube.Pos[j].X) <= 0.00035) c++;
                if (c > bestCount) { bestCount = c; colX = cube.Pos[j].X; }
            }
            // Thermal directions (P frame), deterministic per proton: Boltzmann-shaped around +z with kappa = 4 (mean cos 0.75,
            // so the bias and the band's common tip read at a glance; the real alignment at 1 T is 3.4 per million), and a
            // fully random direction for disorder.
            const double kappa = 4.0; int shown = 0;
            for (int i = 0; i < n; i++)
            {
                uint h = Hash((uint)i * 2654435761u + 12345u);
                double u1 = Unit(ref h), u2 = Unit(ref h), u3 = Unit(ref h), u4 = Unit(ref h), u5 = Unit(ref h);
                double c = 1 + System.Math.Log(u1 + (1 - u1) * System.Math.Exp(-2 * kappa)) / kappa, sn = System.Math.Sqrt(System.Math.Max(0, 1 - c * c)), ph = 2 * System.Math.PI * u2;
                biasDir[i] = new Vector3((float)(sn * System.Math.Cos(ph)), (float)(sn * System.Math.Sin(ph)), (float)c);
                double ci = 2 * u3 - 1, si = System.Math.Sqrt(System.Math.Max(0, 1 - ci * ci)), pi = 2 * System.Math.PI * u4;
                isoDir[i] = new Vector3((float)(si * System.Math.Cos(pi)), (float)(si * System.Math.Sin(pi)), (float)ci);
                spinPhase[i] = (float)(2 * System.Math.PI * u5);
                present[i] = cube.Pd[i] > 0; if (present[i]) shown++;
            }
            PresentCount = shown;
            if (Selected < 0 || Selected >= n || !present[Selected]) Selected = DefaultSelection(s);
        }

        /// <summary>A water-rich proton in the RF band's core, nearest the block's centre.</summary>
        int DefaultSelection(SimState s)
        {
            var c = s.Cube; int best = -1; double score = double.MaxValue;
            for (int i = 0; i < c.N; i++)
            {
                if (c.Pd[i] <= 0 || c.Shift[i] != 0 || c.R2[i] > 1 / 0.005) continue;
                double sc = localPos[i].sqrMagnitude + 0.000001 * System.Math.Abs(s.CubeDfSlice[i]) / System.Math.Max(1, s.P.BandwidthEff);
                if (sc < score) { score = sc; best = i; }
            }
            if (best < 0) for (int i = 0; i < c.N; i++) if (c.Pd[i] > 0) { best = i; break; }
            return best;
        }

        static uint Hash(uint x) { x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16; return x; }
        static double Unit(ref uint h) { h = Hash(h + 0x9e3779b9u); return (h + 0.5) / 4294967296.0; }
        static double SmoothStep(double a, double b, double x) { double t = System.Math.Max(0, System.Math.Min(1, (x - a) / (b - a))); return t * t * (3 - 2 * t); }

        /// <summary>Membership of isochromat i in a spin-set referent.</summary>
        public bool InSet(SimState s, int i, string r)
        {
            var c = s.Cube; if (c.Pd[i] <= 0) return false;
            double half = s.P.BandwidthEff / 2, df = s.CubeDfSlice[i];
            switch (r)
            {
                case "spins.all": return true;
                case "spins.band": return System.Math.Abs(df) <= half;
                case "spins.off": return System.Math.Abs(df) > half;
                case "spins.col": return System.Math.Abs(df) <= half && System.Math.Abs(c.Pos[i].X - colX) <= 0.00035;
            }
            if (r.StartsWith("spins.kind:"))
            {
                var cls = c.Cls[i]; string k = r.Substring(11);
                if (k == "fat") return cls == TissueClass.Fat || cls == TissueClass.Marrow;
                if (k == "muscle") return cls == TissueClass.Muscle || cls == TissueClass.Soft;
                if (k == "cortex") return cls == TissueClass.Cortex;
                if (k == "tendon") return cls == TissueClass.Tendon;
                if (k == "skin") return cls == TissueClass.Skin;
            }
            return false;
        }

        Vector3? SpinAnchor(string r)
        {
            var s = app.Sim.State; if (s == null || matrices.Length == 0) return null;
            bool cached = r == memberRef && memberRevision == s.Revision;
            Vector3 eye = app.Eye; float bd = float.MaxValue; Vector3? best = null;
            for (int i = 0; i < s.Cube.N; i++)
            {
                if (!present[i] || (cached ? !member[i] : !InSet(s, i, r))) continue;
                float d = (worldPos[i] - eye).sqrMagnitude; if (d < bd) { bd = d; best = worldPos[i]; }
            }
            return best;
        }

        /// <summary>
        /// Direction of proton i's moment (P frame) from its isochromat's state: tip theta of the net magnetization from +z,
        /// azimuth from its transverse direction when tipped, else its free precession; minus the carrier (lab frame).
        /// Relaxation: the moment is only as ordered as the isochromat (|m| / PD).
        /// </summary>
        public Vector3 MomentDir(int i, double carrier)
        {
            if (app.EnsembleMoments || app.Demonstrating)
            {
                Vector3 m = NetM(i); double c = System.Math.Cos(-carrier), sn = System.Math.Sin(-carrier);
                var d = new Vector3((float)(m.x*c-m.y*sn), (float)(m.x*sn+m.y*c), m.z);
                return d.sqrMagnitude > 1e-12f ? d.normalized : Vector3.forward;
            }
            var s = app.Sim.State; var ev = s.CubeEval; var cube = s.Cube; var free = ev.Free;
            double X = ev.X[i], Y = ev.Y[i], Z = ev.Z[i];
            double mp = System.Math.Sqrt(X * X + Y * Y), mag = System.Math.Sqrt(mp * mp + Z * Z);
            double theta = System.Math.Atan2(mp, Z), wTip = SmoothStep(0.03, 0.12, mp / System.Math.Max(mag, 1e-9));
            double azFree = -(free != null && free.Length == cube.N ? free[i] : 0), azTip = System.Math.Atan2(Y, X) - System.Math.PI / 2;
            double az = azFree + wTip * System.Math.IEEERemainder(azTip - azFree, 2 * System.Math.PI) - carrier;
            // R = Rz(az) Rx(-theta): the tip carries +z toward +y' (B1 along +x'), then the azimuth turns it.
            Vector3 b0 = biasDir[i]; float ct = (float)System.Math.Cos(theta), st = (float)System.Math.Sin(theta);
            float y1 = ct * b0.y + st * b0.z, z1 = -st * b0.y + ct * b0.z, x1 = b0.x;
            float ca = (float)System.Math.Cos(az), sa = (float)System.Math.Sin(az);
            var turned = new Vector3(ca * x1 - sa * y1, sa * x1 + ca * y1, z1);
            float order = Mathf.Clamp01((float)(mag / System.Math.Max(1e-9, cube.Pd[i])));
            Vector3 up = Vector3.Lerp(isoDir[i], turned, order); float ul = up.magnitude;
            return ul > 1e-3f ? up / ul : turned;
        }

        /// <summary>
        /// Precession phase of proton i (radians, rotating frame) and the tip of its isochromat (radians from the field).
        /// Tipped protons take their isochromat's phase (the band shares one colour, a gradient spreads it); untipped ones keep
        /// their own thermal azimuth (random), blended by the tipped share and by the isochromat's order.
        /// </summary>
        public double PhaseOf(int i, out double theta)
        {
            if(app.Demonstrating) { var m=NetM(i); theta=System.Math.Atan2(new Vector2(m.x,m.y).magnitude,m.z); return System.Math.Atan2(m.y,m.x); }
            var s = app.Sim.State; var ev = s.CubeEval; var cube = s.Cube;
            double X = ev.X[i], Y = ev.Y[i], Z = ev.Z[i];
            double mp = System.Math.Sqrt(X * X + Y * Y), mag = System.Math.Sqrt(mp * mp + Z * Z);
            theta = System.Math.Atan2(mp, Z);
            var own = MomentDir(i, 0); double ownPhase = System.Math.Atan2(own.y, own.x);
            double iso = System.Math.Atan2(Y, X);
            double w = SmoothStep(0.03, 0.12, mp / System.Math.Max(mag, 1e-9)) * System.Math.Min(1, mag / System.Math.Max(1e-9, cube.Pd[i]));
            return ownPhase + w * System.Math.IEEERemainder(iso - ownPhase, 2 * System.Math.PI);
        }

        /// <summary>Proton i's colour: phase as hue, tip as brightness (as drawn in the block and the close-up).</summary>
        public Color ProtonColour(int i) => i >= 0 && i < phaseNow.Length ? (app.PhaseColour ? Look.Phase(phaseNow[i], Look.TipBrightness(tipNow[i])) : Color.Lerp(Look.B0, Look.RF, (float)System.Math.Sin(tipNow[i]))) : Look.MAG;

        /// <summary>Net magnetization of isochromat i relative to its equilibrium (|m| / PD), rotating frame, P frame.</summary>
        public Vector3 NetM(int i)
        {
            var s = app.Sim.State;
            if(app.Demonstrating) { var q=s.Cube.Pos[i]-s.CubeCentre; var m=TeachingSample.Moment(app.Demo,app.DemoProgress,q.X/Layouts.BlockX,q.Y/Layouts.BlockY,q.Z/Layouts.BlockZ); return new Vector3((float)m.X,(float)m.Y,(float)m.Z); }
            var ev = s.CubeEval; double pd = System.Math.Max(1e-9, s.Cube.Pd[i]);
            return new Vector3((float)(ev.X[i] / pd), (float)(ev.Y[i] / pd), (float)(ev.Z[i] / pd));
        }

        public float SpinAngle(int i) => spinPhase.Length > i && i >= 0 ? spinPhase[i] + spinClock : 0;
        float spinClock;

        /// <summary>The nearest proton the ray passes within 40 % of the spacing of, or -1.</summary>
        public int PickProton(Ray ray)
        {
            int best = -1; float bestT = float.MaxValue; float r = 0.4f * Spacing * Root.lossyScale.x;
            for (int i = 0; i < worldPos.Length; i++)
            {
                if (!present[i]) continue;
                Vector3 w = worldPos[i] - ray.origin; float t = Vector3.Dot(w, ray.direction); if (t < 0) continue;
                if ((w - ray.direction * t).sqrMagnitude <= r * r && t < bestT) { bestT = t; best = i; }
            }
            return best;
        }

        public void Select(int i) { if (i >= 0 && i < present.Length && present[i]) Selected = i; }

        /// <summary>
        /// Per frame. carrier: the displayed carrier phase (App.DisplayCarrier). The worker's last result is read when fresh;
        /// the precession itself advances every frame from the carrier.
        /// </summary>
        public void Tick(float dt, double carrier, bool instant, bool playing, bool fresh = true)
        {
            var sim = app.Sim; var s = sim.State; if (s == null) return;
            if (s.Revision != revision) OnState(s);
            if (s.CubeEval.X == null || sim.EvalRevision != s.Revision && !sim.Synchronous) { Draw(0); return; }
            var cube = s.Cube; int n = cube.N; float D = Spacing;
            if (playing) spinClock += dt * 2 * Mathf.PI / 0.9f; // the spin: one turn every 0.9 s (symbolic)
            float cr = (float)System.Math.Cos(-carrier), sr = (float)System.Math.Sin(-carrier);
            // Emphasis targets from the cue's spin-set referent (membership cached per referent and revision).
            var refs = app.Attention.Refs; string spinRef = null; bool all = refs == null;
            if (refs != null) foreach (var r in refs) { if (r.StartsWith("spins.")) spinRef = r; if (r == "cube") all = true; }
            if (spinRef != memberRef || memberRevision != s.Revision)
            {
                memberRef = spinRef; memberRevision = s.Revision;
                for (int i = 0; i < n; i++) member[i] = spinRef != null && InSet(s, i, spinRef);
            }
            // With no spin set named, every proton stays opaque (nothing in the block is being singled out).
            if (spinRef == null) all = true;
            float ke = instant ? 1 : 1 - Mathf.Exp(-dt / Look.EmphasisTime);
            Matrix4x4 P = Root.localToWorldMatrix;
            Vector3 px = new Vector3(P.m00, P.m10, P.m20), py = new Vector3(P.m01, P.m11, P.m21), pz = new Vector3(P.m02, P.m12, P.m22);
            float scale=app.Demonstrating && app.Demo.StartsWith("single")?2.5f:app.Demonstrating && (app.Demo.StartsWith("pair")||app.Demo=="recover")?2:1;
            float L = 0.72f * D, thick = 0.60f * D, ringR = 0.17f * D; int drawn = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 lp = localPos[i];
                Vector3 w = new Vector3(P.m03 + px.x * lp.x + py.x * lp.y + pz.x * lp.z, P.m13 + px.y * lp.x + py.y * lp.y + pz.y * lp.z, P.m23 + px.z * lp.x + py.z * lp.y + pz.z * lp.z);
                worldPos[i] = w;
                if (!present[i] && !app.Demonstrating) { kind[i] = 0; continue; }
                float target = all || member[i] ? 1 : 0;
                float e = emph[i] += (target - emph[i]) * ke;
                bool solid = e >= 0.985f;
                Vector3 dp = MomentDir(i, carrier);
                phaseNow[i] = PhaseOf(i, out double theta); tipNow[i] = theta;
                Vector3 d = new Vector3(-dp.x, dp.y, dp.z); // Frames.ToS inline
                // Orthonormal basis with the needle axis (mesh +Y) along d; det[u, d, v] = +1 (instances cannot flip culling).
                Vector3 a = Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right;
                Vector3 u = Vector3.Cross(a, d).normalized, v = Vector3.Cross(u, d);
                Vector3 cu = (px * u.x + py * u.y + pz * u.z), cd = (px * d.x + py * d.y + pz * d.z), cv = (px * v.x + py * v.y + pz * v.z);
                var M = new Matrix4x4();
                float emphasisScale=app.Demonstrating && app.Demo.StartsWith("single") && i==Selected ? 1.5f : 1;
                M.SetColumn(0, cu * thick * (i==Selected?1.1f:1)); M.SetColumn(1, cd * L * emphasisScale * ((app.EnsembleMoments || app.Demonstrating) ? Mathf.Clamp01(NetM(i).magnitude) : 1)); M.SetColumn(2, cv * thick); M.SetColumn(3, new Vector4(w.x, w.y, w.z, 1));
                matrices[i] = M;
                // The ring turns about the moment: rotate the basis about d by the spin angle.
                float sp = spinPhase[i] + spinClock, cs = Mathf.Cos(sp), sn = Mathf.Sin(sp);
                Vector3 ru = cu * cs + cv * sn, rv = -cu * sn + cv * cs;
                var R = new Matrix4x4();
                R.SetColumn(0, ru * ringR); R.SetColumn(1, cd * ringR); R.SetColumn(2, rv * ringR); R.SetColumn(3, new Vector4(w.x, w.y, w.z, 1) + (Vector4)(cd * (0.1f * L)));
                ringMats[i] = R; kind[i] = (byte)((app.EnsembleMoments || app.Demonstrating) && NetM(i).sqrMagnitude<1e-10f ? 0 : 1);
                // Colour: phase as hue, tip as brightness. Named protons opaque; the others fade by opacity only.
                var c = app.PhaseColour ? Look.Phase(phaseNow[i], Look.TipBrightness(theta)) : Color.Lerp(Look.B0, Look.RF, (float)System.Math.Sin(theta));
                float opacity=app.Demonstrating && app.Demo.StartsWith("single") ? (i==Selected?0.95f:0.16f) : i==Selected?0.92f:app.PhaseColour?0.50f:0.70f;
                tints[i] = new Vector4(c.r, c.g, c.b, opacity*(0.4f+0.6f*e));
                drawn++;
            }
            DrawnNeedles = drawn;
            Draw(n);
            // The selected proton's halo, facing the eye.
            if (Selected >= 0 && Selected < n && present[Selected])
            {
                haloRenderer.enabled = true; halo.position = worldPos[Selected];
                halo.rotation = Quaternion.LookRotation(app.Eye - halo.position) * Quaternion.Euler(90, 0, 0);
                halo.localScale = Vector3.one * (0.9f * D);
            }
            else haloRenderer.enabled = false;
            // B1 vector through the centre: A(t) b1 at the block's centre, turned by the carrier (lab frame).
            if (sim.RfOn && app.ShowFields)
            {
                s.Tables.Sample(s.CubeCentre, out FieldSample fs);
                double bre = sim.RfRe * fs.B1Re - sim.RfIm * fs.B1Im, bim = sim.RfRe * fs.B1Im + sim.RfIm * fs.B1Re;
                double lx = bre * cr - bim * sr, ly = bre * sr + bim * cr;
                Vector3 dd = Frames.ToS(lx, ly, 0); float amp = dd.magnitude / (float)(0.75 * Scanner.B1Iso);
                if (amp > 1e-4f)
                {
                    b1Vector.gameObject.SetActive(true); dd.Normalize();
                    float len = 0.2f * Mathf.Min(1.2f, amp);
                    b1Vector.localRotation = Quaternion.FromToRotation(Vector3.up, dd); b1Vector.localPosition = dd * (len * 0.5f);
                    b1Vector.localScale = new Vector3(0.2f, len, 0.2f);
                }
                else b1Vector.gameObject.SetActive(false);
            }
            else b1Vector.gameObject.SetActive(false);
            b1Renderer.sharedMaterial.SetColor("_Glow", Look.RF * (0.25f + 0.35f * eB1.E));
            // The edges stay white (a dimmed white would be grey); the frame's emphasis is the pointer and the lit protons.
            for (int k = 0; k < 3; k++) axis[k].sharedMaterial.SetColor("_Glow", Look.Axis(k) * (0.15f + 0.45f * eAxis[k].E));
            TickGradient(s, sim);
            tissue.Tick(s, sim);
            if(!app.Demonstrating) { heading.transform.localPosition=new Vector3(0,DisplaySize.y/2+0.18f,0); example.transform.localPosition=new Vector3(0,DisplaySize.y/2+0.13f,0); }
            Labels.Set(heading,app.Demonstrating ? (app.Demo.StartsWith("single")?"Selected hydrogen · expectation vector in its sample":"Ideal sample · 6 × 6 × 8 moments") : "Tissue · 6 × 6 × 8 sample locations");
            Labels.Set(example,app.Demo switch {"single"=>"f₀ = γ̄ B₀ · precession slowed", "singleRF"=>"B₁ on resonance · α = γ ∫B₁ dt", "uniform"=>"Same field + same material → same response", "t1"=>"T₁ = 0.8 s · Mz / M₀ = 1 − exp(−t/T₁)", "t2"=>"T₂ = 0.10 s · Mxy / M₀ = exp(−t/T₂)", "mixture"=>"Two materials · different relaxation and Δf", "slice"=>"Gz + RF bandwidth → excited slab", "gx"=>"Gx on · Δf(x) = γ̄ Gx x", "gy"=>"Gy on · Δφ(y) = γ Gy y Δt", "gyHold"=>"Gy off · phase differences remain", "pair0"=>"Same x · two y positions · S₀ = A + B = 1.1", "pair1"=>"Second encoding · S₁ → A − B = 0.5", "recover"=>"A = (S₀ + S₁)/2 = 0.8 · B = (S₀ − S₁)/2 = 0.3", _=>app.PhaseColour?"Broad: moment · thin: transverse phase (display scales)":"Moments anchored at sample positions"});
            bool paired=app.Demo=="pair0"||app.Demo=="pair1"||app.Demo=="recover";
            sumCaption.gameObject.SetActive(paired); sourceA.gameObject.SetActive(paired); sourceB.gameObject.SetActive(paired); sumVector.gameObject.SetActive(paired);
            if(paired) { double ph=app.Demo=="recover"?System.Math.PI:app.Demo=="pair1"?System.Math.PI*app.DemoProgress:0; var sum=Frames.ToS(0.8+0.3*System.Math.Cos(ph),-0.3*System.Math.Sin(ph),0); sumVector.position=Root.position-app.HeadCamera.transform.right*(0.43f*Root.lossyScale.x); sumCaption.transform.position=sumVector.position-app.HeadCamera.transform.up*0.08f; sumVector.rotation=Quaternion.FromToRotation(Vector3.up,(app.HeadCamera.transform.right*(float)(0.8+0.3*System.Math.Cos(ph))-app.HeadCamera.transform.up*(float)(0.3*System.Math.Sin(ph))).normalized); sumVector.localScale=new Vector3(0.08f,0.12f*sum.magnitude,0.08f); }
            if(app.Demonstrating) {
                // Captions sit outside the projected volume, independent of its oblique orientation.
                var up=app.HeadCamera.transform.up; heading.transform.position=Root.position+up*0.61f; example.transform.position=Root.position+up*0.54f;
                gradArrow.gameObject.SetActive(false); b1Vector.gameObject.SetActive(app.Demo=="singleRF"||app.Demo=="uniform"||app.Demo=="slice"); b1Vector.localPosition=Vector3.zero; b1Vector.localRotation=Quaternion.FromToRotation(Vector3.up,Frames.ToS(System.Math.Sin(-carrier),-System.Math.Cos(-carrier),0)); b1Vector.localScale=new Vector3(0.1f,0.15f,0.1f); }

        }

        /// <summary>
        /// The gradient arrow: along the block edge nearest the eye that runs parallel to the active gradient, from the weaker
        /// to the stronger field, tapering up toward the stronger end, with the Larmor frequency offset at both ends.
        /// </summary>
        void TickGradient(SimState s, Simulation sim)
        {
            double gx = sim.Ix * Scanner.EtaX, gy = sim.Iy * Scanner.EtaY, gz = sim.Iz * Scanner.EtaZ; // T/m
            double ax = System.Math.Abs(gx), ay = System.Math.Abs(gy), az = System.Math.Abs(gz);
            int a = ax >= ay && ax >= az ? 0 : ay >= az ? 1 : 2; double g = a == 0 ? gx : a == 1 ? gy : gz;
            bool on = app.ShowFields && System.Math.Abs(g) > 0.2e-3;
            if (gradArrow.gameObject.activeSelf != on) gradArrow.gameObject.SetActive(on);
            if (!on) { gradAxis = -1; wallMat.SetVector("_Ramp", Vector4.zero); return; }
            Vector3 half = DisplaySize / 2;
            // Direction of the stronger field along axis a, in S (P x is S -x).
            Vector3 axisS = a == 0 ? Vector3.left : a == 1 ? Vector3.up : Vector3.forward; if (g < 0) axisS = -axisS;
            Color c = Look.Axis(a);
            // The far walls glow toward the stronger field.
            wallMat.SetVector("_Ramp", axisS / (2 * half[a])); wallMat.SetColor("_RampColor", c);
            // Inside the block along the edge parallel to axis a nearest the eye, over the dark walls: it never pokes out into
            // the neighbouring views, and the protons' colour twist runs beside it.
            Vector3 eyeLocal = Root.InverseTransformPoint(app.Eye); int b = (a + 1) % 3, d = (a + 2) % 3;
            Vector3 edge = Vector3.zero; edge[b] = Mathf.Sign(eyeLocal[b]) * (half[b] - 0.028f); edge[d] = Mathf.Sign(eyeLocal[d]) * (half[d] - 0.028f);
            float len = 2 * half[a] - 0.07f;
            Vector3 start = edge - axisS * (len / 2);
            gradRod.localPosition = start; gradRod.localRotation = Quaternion.LookRotation(axisS, Mathf.Abs(axisS.y) < 0.9f ? Vector3.up : Vector3.forward); gradRod.localScale = new Vector3(1, 1, len);
            gradRodR.sharedMaterial.SetColor("_Color", c);
            gradName.color = c;
            Vector3 inward = -edge.normalized; // edge has no component along the arrow
            gradMid = edge;
            gradName.transform.localPosition = edge + inward * 0.05f;
            gradLow.transform.localPosition = start + inward * 0.04f;
            gradHigh.transform.localPosition = start + axisS * len + inward * 0.045f;
            if (a != gradAxis || Time.unscaledTime >= nextGradLabels || app.FixedStep)
            {
                gradAxis = a; nextGradLabels = Time.unscaledTime + 0.2f;
                double L = a == 0 ? Layouts.BlockX : a == 1 ? Layouts.BlockY : Layouts.BlockZ, df = Constants.GammaBar * System.Math.Abs(g) * L / 2; // Hz
                // Only the frequencies at the ends: the colour names the gradient and the sequence plot gives its strength.
                Labels.Set(gradName, ""); Labels.Set(gradLow, "−" + Hz(df)); Labels.Set(gradHigh, "+" + Hz(df));
            }
        }

        static string Hz(double f) => f >= 1000 ? $"{f / 1e3:0.00} kHz" : $"{f:0} Hz";

        /// <summary>Re-submits this frame's protons for one camera (used before a manual Camera.Render, e.g. captures).</summary>
        public void Submit(Camera cam) { } // MeshRenderer draws the grid for every camera and stereo eye.
        void Draw(int n, Camera cam = null)
        {
            if(n==0) return;
            int nv=needlePoints.Length,ni=needleIndices.Length;
            if(meshPoints==null || meshPoints.Length!=n*nv) {
                meshPoints=new Vector3[n*nv]; meshNormals=new Vector3[n*nv]; meshColours=new Color[n*nv]; meshIndices=new int[n*ni];
                for(int i=0;i<n;i++) for(int j=0;j<ni;j++)meshIndices[i*ni+j]=i*nv+needleIndices[j];
                visibleMesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
                visibleMesh.vertices=meshPoints; visibleMesh.triangles=meshIndices;
            }
            var inv=Root.worldToLocalMatrix;
            for(int i=0;i<n;i++) {
                var m=inv*matrices[i]; var normal=m.inverse.transpose; Color c=tints[i];
                for(int j=0;j<nv;j++) { int k=i*nv+j; meshPoints[k]=kind[i]==0?localPos[i]:m.MultiplyPoint3x4(needlePoints[j]); meshNormals[k]=normal.MultiplyVector(needleNormals[j]).normalized; meshColours[k]=c; }
            }
            visibleMesh.vertices=meshPoints; visibleMesh.normals=meshNormals; visibleMesh.colors=meshColours;
            // Thin, shorter transverse projections make phase readable beside the larger full moment.
            if(!app.PhaseColour)phaseMesh.Clear();
            else {
                if(phasePoints==null || phasePoints.Length!=n*nv) { phasePoints=new Vector3[n*nv];phaseNormals=new Vector3[n*nv];phaseColours=new Color[n*nv]; }
                for(int i=0;i<n;i++) {
                    var d=MomentDir(i,app.DisplayCarrier); d.z=0;
                    float amp=d.magnitude;
                    var m=Matrix4x4.TRS(localPos[i],Quaternion.FromToRotation(Vector3.up,amp>1e-4f?Frames.ToS(d.x,d.y,0).normalized:Vector3.up),
                        new Vector3(Spacing*0.26f,Spacing*0.48f*amp,Spacing*0.26f));
                    var normal=m.inverse.transpose; var colour=Look.Phase(phaseNow[i],1); colour.a=app.Demonstrating && app.Demo.StartsWith("single") && i!=Selected ? 0.25f : 0.72f;
                    for(int j=0;j<nv;j++) { int k=i*nv+j; phasePoints[k]=kind[i]==0||amp<1e-4f?localPos[i]:m.MultiplyPoint3x4(needlePoints[j]); phaseNormals[k]=normal.MultiplyVector(needleNormals[j]).normalized;phaseColours[k]=colour; }
                }
                phaseMesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
                phaseMesh.vertices=phasePoints;phaseMesh.triangles=meshIndices;phaseMesh.normals=phaseNormals;phaseMesh.colors=phaseColours;
                phaseMesh.bounds=new Bounds(Vector3.zero,DisplaySize+Vector3.one*0.15f);
            }
            visibleMesh.bounds=new Bounds(Vector3.zero,DisplaySize+Vector3.one*0.15f);
        }
    }
}
