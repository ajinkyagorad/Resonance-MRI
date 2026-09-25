using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nebulytic.Resonance;
using Nebulytic.Resonance.Sim;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Design 0.9 concept frames (USER-REVIEW-0.8.4, phase 2): a static mock-up of the proposed design language
/// (.pipeline/DESIGN.md) built from the 0.8.x coil geometry, hand anatomy and simulation, rendered at the review camera
/// (76 degrees vertical, 1920 x 1200, 2x supersampled) and composited over a bright room photograph as the headset shows
/// it (premultiplied eye buffer: out = rgb + room x (1 - a), in linear light). Not the app: nothing here ships, and it
/// lives only on the scratch branch design/0.9-concepts. Run: Tools/render-design-concepts.sh
/// </summary>
public static class DesignConcepts
{
    const int W = 1920, H = 1200, SS = 2;
    const float Fov = 76, Yaw = 50;
    const string Out = "validation/design-0.9", Room = "validation/backdrops/room-photo-warm.png";
    static readonly Vector3 Eye = new Vector3(0, 1.55f, 0);

    // ------------------------------------------------------------------------------------------------ palette (DESIGN §4)

    static Color Hex(int rgb, float a = 1) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);
    static readonly Color GlassTint = Hex(0x241B16), RimLight = Hex(0xFFF4E6), Porcelain = Hex(0xF3ECE3), Pearl = Hex(0xFBF7F2);
    static readonly Color Text1 = Hex(0xFFF8F0), Text2 = Hex(0xE6D8C8), Text3 = Hex(0xC4B1A0), Espresso = Hex(0x2A211C);
    static readonly Color XV = Hex(0xFF6A55), XS = Hex(0xF8BBAD), YV = Hex(0x3FD98A), YS = Hex(0xAFE5C1), ZV = Hex(0x7F86FF), ZS = Hex(0xC7C5EE);
    static readonly Color RfV = Hex(0xE05ED6), RfS = Hex(0xECB6DE), B0V = Hex(0xFFF0D8), B0S = Hex(0xF8EEDF);
    static readonly Color Skin = Hex(0xF4C4AE), Muscle = Hex(0xE58C86), Bone = Hex(0xFBF3E8), Smoke = Hex(0x1E1714), Milk = Hex(0xF4EEE8);
    /// <summary>Proton phase colours around the circle (rose at +y', right after the pulse). Blue is skipped: mint passes
    /// into lavender through a pale mist. Interpolated in OKLab.</summary>
    static readonly Color[] PhaseRing = { Hex(0xFF5C8A), Hex(0xFF7F66), Hex(0xFFB48C), Hex(0xC8E07E), Hex(0x62D695), Hex(0x52CDB8), Hex(0xB28CF5), Hex(0xE36BD3) };

    static Color PhaseColour(double phase)
    {
        double u = (phase - Math.PI / 2) / (2 * Math.PI); u -= Math.Floor(u); u *= PhaseRing.Length;
        int i = (int)Math.Floor(u) % PhaseRing.Length; float f = (float)(u - Math.Floor(u));
        return OkMix(PhaseRing[i], PhaseRing[(i + 1) % PhaseRing.Length], f);
    }
    static Color SoftOf(Color vivid, float k = 0.56f) => OkMix(vivid, Pearl, k);

    static Vector3 ToOk(Color c)
    {
        Color q = c.linear;
        float l = Mathf.Pow(0.4122214708f * q.r + 0.5363325363f * q.g + 0.0514459929f * q.b, 1 / 3f);
        float m = Mathf.Pow(0.2119034982f * q.r + 0.6806995451f * q.g + 0.1073969566f * q.b, 1 / 3f);
        float s = Mathf.Pow(0.0883024619f * q.r + 0.2817188376f * q.g + 0.6299787005f * q.b, 1 / 3f);
        return new Vector3(0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s, 1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s, 0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s);
    }
    static Color FromOk(Vector3 o, float a = 1)
    {
        float l = o.x + 0.3963377774f * o.y + 0.2158037573f * o.z, m = o.x - 0.1055613458f * o.y - 0.0638541728f * o.z, s = o.x - 0.0894841775f * o.y - 1.2914855480f * o.z;
        l = l * l * l; m = m * m * m; s = s * s * s;
        var lin = new Color(Mathf.Clamp01(4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s), Mathf.Clamp01(-1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s), Mathf.Clamp01(-0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s), a);
        return lin.gamma;
    }
    static Color OkMix(Color a, Color b, float t) => FromOk(Vector3.Lerp(ToOk(a), ToOk(b), t), Mathf.Lerp(a.a, b.a, t));
    /// <summary>Vertex colours are used as linear values by the concept shaders.</summary>
    static Color Lin(Color c, float a = 1) { var l = c.linear; l.a = a; return l; }

    // ------------------------------------------------------------------------------------------------ state

    static Camera cam; static Simulation sim; static SimState S; static readonly List<string> anatomy = new List<string>();
    static TMP_FontAsset fReg, fSemi, fDisp;
        static Mesh needleMesh, ringMesh;

    public static void Run()
    {
        int exit = 1;
        try
        {
            Directory.CreateDirectory(Out);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            LoadFonts(); LoadSim();
            var go = new GameObject("Review camera"); cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0); cam.fieldOfView = Fov;
            cam.nearClipPlane = 0.02f; cam.farClipPlane = 60; cam.allowHDR = false; cam.allowMSAA = true;
            needleMesh = SoftNeedle(); ringMesh = MeshKit.Torus(0.19f, 0.013f, 40, 8);
            var only = System.Environment.GetEnvironmentVariable("CONCEPT_ONLY") ?? "abcd";
            if (only.Contains("a")) FrameA();
            if (only.Contains("b")) FrameB();
            if (only.Contains("c")) FrameC();
            if (only.Contains("d")) FrameD();
            exit = 0;
        }
        catch (Exception e) { Debug.LogException(e); }
        Debug.Log("DESIGN_CONCEPTS_DONE " + exit);
        EditorApplication.Exit(exit);
    }

    static void LoadFonts()
    {
        fReg = MakeFont("Assets/DesignConcept/Fonts/Inter-Regular.ttf");
        fSemi = MakeFont("Assets/DesignConcept/Fonts/Inter-SemiBold.ttf");
        fDisp = MakeFont("Assets/DesignConcept/Fonts/InterDisplay-SemiBold.ttf");
    }

    static TMP_FontAsset MakeFont(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var font = AssetDatabase.LoadAssetAtPath<Font>(path) ?? throw new Exception("Font missing: " + path);
        var fa = TMP_FontAsset.CreateFontAsset(font, 96, 12, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
        fa.material.renderQueue = 3040;
        return fa;
    }

    static void LoadSim()
    {
        var tables = FieldTables.Load(Resources.Load<TextAsset>("Resonance/FieldTables").bytes);
        var labels = HandLabels.Load(Resources.Load<TextAsset>("Anatomy/HandLabels").bytes);
        foreach (var part in Resources.Load<TextAsset>("Anatomy/AnatomyManifest").text.Split('"'))
            if (part.EndsWith(".obj") && (part.StartsWith("Bone_") || part.StartsWith("Muscle_") || part == "Skin.obj")) anatomy.Add(part.Substring(0, part.Length - 4));
        sim = new Simulation(tables, labels) { AddNoise = true };
        var sw = System.Diagnostics.Stopwatch.StartNew();
        S = sim.BuildNow(new Protocol(), new HandSpecimen(labels, Simulation.DefaultPose(labels)));
        Debug.Log($"DESIGN_CONCEPTS simulation ready in {sw.Elapsed.TotalSeconds:F1} s ({S.Slices.Length} slices)");
    }

    // ------------------------------------------------------------------------------------------------ layout (DESIGN §7)

    /// <summary>A point at azimuth / elevation (degrees) and distance (m) from the eye.</summary>
    static Vector3 Slot(float az, float el, float d)
    {
        float a = az * Mathf.Deg2Rad, e = el * Mathf.Deg2Rad;
        return Eye + d * new Vector3(Mathf.Cos(e) * Mathf.Sin(a), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Cos(a));
    }
    static readonly Vector3 ScannerCentre = new Vector3(1.20f * Mathf.Sin(-3 * Mathf.Deg2Rad), 1.30f, 1.20f * Mathf.Cos(3 * Mathf.Deg2Rad));
    static Vector3 SequencePos => Slot(-36.5f, 12.3f, 1.08f);
    static Vector3 SignalPos => Slot(-40.5f, -13.8f, 1.02f);
    static Vector3 DataPos => Slot(-3, 21, 1.20f);
    static Vector3 CloseUpPos => Slot(32.5f, 12.5f, 1.08f);
    static Vector3 VolumePos => Slot(34, -12.5f, 0.98f);
    static Vector3 StripPos => Slot(-3, -38.5f, 0.76f);
    const float Magnify = 40;

    // ------------------------------------------------------------------------------------------------ frames

    /// <summary>A point seen from cam at the anchor's direction turned by (dYaw, dPitch) degrees, pulled toward cam.</summary>
    static Vector3 View(Vector3 cam, Vector3 anchor, float dYaw, float dPitch, float pull = 0.12f)
    {
        var d = anchor - cam; float dist = d.magnitude; d /= dist;
        float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + dYaw, pitch = Mathf.Asin(d.y) * Mathf.Rad2Deg + dPitch;
        float a = yaw * Mathf.Deg2Rad, e = pitch * Mathf.Deg2Rad;
        return cam + (dist - pull) * new Vector3(Mathf.Cos(e) * Mathf.Sin(a), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Cos(a));
    }

    /// <summary>(a) The opening: the life-size scanner in standby, its parts named plainly, the welcome card with Play.</summary>
    static void FrameA()
    {
        var root = new GameObject("Frame A").transform;
        var sc = BuildScanner(root, "none", false, Color.white, 0.5f, false, Eye);
        // Part tags (plain words; no symbols before the introduction), placed in the view around the scanner.
        const float cap = 0.80f; // degrees
        Tag(root, "Main magnet", sc.Magnet, View(Eye, sc.Magnet, -9, 9), B0V, cap);
        Tag(root, "Magnetic field", sc.Field, View(Eye, sc.Field, -9, -5), B0V, cap);
        Tag(root, "Gradient coils", sc.Gradient, View(Eye, sc.Gradient, 4, 14), ZS, cap);
        Tag(root, "Radio coil", sc.Rf, View(Eye, sc.Rf, 13, 10), RfS, cap);
        Tag(root, "Hand", sc.Hand, View(Eye, sc.Hand, -13, -10), Skin, cap);
        Tag(root, "Receiver", sc.Receiver, View(Eye, sc.Receiver, 10, 2), Pearl, cap);
        // Welcome card.
        var size = new Vector2(0.50f, 0.34f);
        var card = Panel(root, "Welcome card", Slot(37, -4, 0.95f), Eye, size, 0.034f, 0.86f, 0.70f);
        float x0 = -size.x / 2 + 0.036f;
        Text(card, "RESONANCE", new Vector3(x0, 0.137f, -0.003f), 0.0098f, Text3, fSemi, TextAlignmentOptions.TopLeft, 0.43f, 0, 14);
        Text(card, "How an MRI scanner\nmakes a picture of a hand", new Vector3(x0, 0.110f, -0.003f), 0.0215f, Text1, fDisp, TextAlignmentOptions.TopLeft, 0.43f, -6);
        Text(card, "This scanner is life-size. George will show you each part, then what happens inside, one step at a time.", new Vector3(x0, 0.018f, -0.003f), 0.0126f, Text2, fReg, TextAlignmentOptions.TopLeft, 0.43f, 10);
        // Play (primary) and the chips.
        var play = new GameObject("Play").transform; play.SetParent(card, false); play.localPosition = new Vector3(x0 + 0.074f, -0.093f, -0.004f);
        Mats.Object("Play pill", play, GlassQuad(new Vector2(0.074f, 0.031f), 0.03f), GlassMat(new Vector2(0.074f, 0.031f), 0.031f, 0.97f, 0.97f, 0, null, 3010, Pearl, 0.10f));
        Mats.Object("Play icon", play, Triangle(0.0135f), UnlitMat(Espresso, 3050), new Vector3(-0.032f, 0, -0.002f));
        Text(play, "Play", new Vector3(-0.013f, 0, -0.002f), 0.0150f, Espresso, fSemi, TextAlignmentOptions.Left, 0.1f);
        Chip(card, "Narrator · George", new Vector3(x0 + 0.238f, -0.093f, -0.004f), 0.0108f);
        Chip(card, "Subtitles on", new Vector3(x0 + 0.388f, -0.093f, -0.004f), 0.0108f);
        Text(card, "About 12 minutes · pause or go back at any time", new Vector3(x0, -0.141f, -0.003f), 0.0110f, Text3, fReg, TextAlignmentOptions.Left, 0.44f);
        Shot("design-0.9-a-opening", Eye, 8, 8);
        UnityEngine.Object.DestroyImmediate(root.gameObject);
    }

    /// <summary>(b) The full dashboard mid-lesson: a readout. The x gradient, its row and the signal are vivid; the rest soft.</summary>
    static void FrameB()
    {
        var root = new GameObject("Frame B").transform;
        var prog = S.Program; var list = prog.SliceBlocks[0]; var blk = prog.Blocks[list[12]];
        double now = blk.Adc[44];
        var ps = Protons(now);
        var sc = BuildScanner(root, "gx", true, PhaseColour(Math.PI / 2), 0.22f, true, Eye);
        var vol = BuildVolume(root, VolumePos, ps, "band", false, Eye);
        ZoomLines(root, sc, vol);
        SequencePanel(root, SequencePos, Eye, new Vector2(0.48f, 0.34f), blk, now, "gx");
        SignalPanel(root, SignalPos, Eye, new Vector2(0.45f, 0.31f), blk, now, true);
        DataPanel(root, DataPos, Eye, new Vector2(0.62f, 0.32f), blk, now);
        CloseUpPanel(root, CloseUpPos, Eye, new Vector2(0.42f, 0.29f), ps);
        Strip(root, StripPos, Eye, "6 of 10", "Reading the signal", 0.58f, "The x gradient twists the colours along x, and the coil picks up the protons’ signal.");
        // The signal path: the receiver to the received signal.
        var lb = new LineBuilder(); Curve(lb, sc.Receiver, SignalPos + (SignalPos - Eye).normalized * 0.01f + new Vector3(0.21f, -0.07f, 0.06f), 0.06f, 0.0022f, Lin(RfS, 0.55f));
        Mats.Object("Signal path", root, lb.Commit(new Mesh()), LineMat(3, 0.45f, 0, 1, 2998));
        Shot("design-0.9-b-dashboard", Eye, -3, 8);
        UnityEngine.Object.DestroyImmediate(root.gameObject);
    }

    /// <summary>(c) The proton volume, close, during the slice-selective radio pulse: the band tips together, vivid; the
    /// rest stays soft; the radio field B1 is the orchid arrow.</summary>
    static void FrameC()
    {
        var root = new GameObject("Frame C").transform;
        var prog = S.Program; var blk = prog.Blocks[prog.SliceBlocks[0][0]];
        // The moment the band's mean tip passes 55 degrees.
        double t = blk.RfCentre; ProtonState[] ps = null;
        for (int k = 0; k <= 60; k++)
        {
            double tt = blk.RfStart + (blk.RfEnd - blk.RfStart) * k / 60.0; var q = Protons(tt);
            double sum = 0; int n = 0; foreach (var p in q) if (p.Present && p.Band) { sum += p.Tip; n++; }
            if (n > 0 && sum / n >= 55 * Math.PI / 180) { t = tt; ps = q; break; }
        }
        ps = ps ?? Protons(t);
        Debug.Log($"DESIGN_CONCEPTS frame c at t = {t:F6} s (RF {blk.RfStart:F6}..{blk.RfEnd:F6})");
        var vc = VolumePos; var dir = (vc - Eye).normalized;
        var right = Vector3.Cross(Vector3.up, dir).normalized;
        Vector3 camPos = vc - dir * 0.47f + right * 0.10f + new Vector3(0, 0.06f, 0);
        BuildScanner(root, "rf", true, PhaseColour(Math.PI / 2), 0.22f, true, camPos);
        BuildVolume(root, vc, ps, "tip", true, camPos);
        var lookQ = Quaternion.LookRotation(vc + new Vector3(0, -0.035f, 0) - camPos, Vector3.up); var look = lookQ.eulerAngles;
        // The strip follows the view (lazily): low in the view, facing the eye.
        var stripPos = camPos + 0.76f * (lookQ * Quaternion.Euler(33, 0, 0) * Vector3.forward);
        Strip(root, stripPos, camPos, "4 of 10", "Tipping the protons", 0.36f, "The radio pulse tips the protons in this slice together: they lean away from the field.");
        Shot("design-0.9-c-protons-tipping", camPos, look.y, look.x);
        UnityEngine.Object.DestroyImmediate(root.gameObject);
    }

    /// <summary>(d) One plot in the scene: the pulse sequence, grabbed and brought closer to read (any item can be moved;
    /// recenter restores the layout), with the z gradient in focus while the radio pulse plays; the Gz coils in the scanner
    /// are vivid in the same colour. The other views stay in place.</summary>
    static void FrameD()
    {
        var root = new GameObject("Frame D").transform;
        var prog = S.Program; var list = prog.SliceBlocks[0]; var blk = prog.Blocks[list[12]];
        double now = blk.RfCentre;
        BuildScanner(root, "gz", true, PhaseColour(Math.PI / 2), 0.22f, false, Eye);
        var ps = Protons(now);
        BuildVolume(root, VolumePos, ps, "band", false, Eye);
        SequencePanel(root, Slot(-41, 8.5f, 0.60f), Eye, new Vector2(0.48f, 0.34f), blk, now, "gz", 0.3f);
        SignalPanel(root, SignalPos, Eye, new Vector2(0.45f, 0.31f), blk, now, false);
        DataPanel(root, DataPos, Eye, new Vector2(0.62f, 0.32f), blk, now);
        CloseUpPanel(root, CloseUpPos, Eye, new Vector2(0.42f, 0.29f), ps);
        float yaw = -29, pitch = -1; var view = Quaternion.Euler(pitch, yaw, 0);
        Strip(root, Eye + 0.76f * (view * Quaternion.Euler(31, 0, 0) * Vector3.forward), Eye, "5 of 10", "Choosing a slice", 0.47f, "While the radio pulse plays, the z gradient is on, so only one slice of the hand answers.");
        Shot("design-0.9-d-sequence-plot", Eye, yaw, pitch);
        UnityEngine.Object.DestroyImmediate(root.gameObject);
    }

    // ------------------------------------------------------------------------------------------------ scanner

    sealed class ScannerParts { public Transform Root; public Vector3 Magnet, Gradient, Rf, Hand, Receiver, Field; public Vector3[] Block = new Vector3[8]; }
    const float CutFrom = 100, CutTo = 228; // physical azimuths of the cutaway (degrees), centred on the view from the seat

    /// <summary>
    /// The hero scanner, life-size: a frosted milky shell cut away toward the seat, a deep smoked bore liner behind the hand
    /// (the hand reads against it over any room), pearl magnet windings, and the gradient and RF coils as satin tubes. All
    /// coils are always visible: their far halves opaque, their near halves clear glass (the hand shows through); the coil
    /// being explained is vivid and softly glowing. Field lines only outside the bore. cam: the viewpoint (near / far).
    /// </summary>
    static ScannerParts BuildScanner(Transform parent, string focus, bool slab, Color slabColour, float fieldAlpha, bool block, Vector3 cam)
    {
        var sp = new ScannerParts();
        var root = new GameObject("Scanner").transform; root.SetParent(parent, false); root.position = ScannerCentre; root.rotation = Quaternion.Euler(0, Yaw, 0); sp.Root = root;
        Vector3 Wp(Vector3 s) => root.TransformPoint(s);
        const float Rb = 0.122f, Ro = 0.288f, L = 0.405f, Rr = 0.045f;
        var shell = HousingShell(Rb, Ro, L, Rr, CutFrom, CutTo);
        Mats.Object("Housing (inside)", root, shell, SoftMat(Milk, 0, null, 0.2f, 60, 0.25f, 0.10f, 0.32f, 2986, CullMode.Front));
        Mats.Object("Bore liner", root, BoreLiner(Rb, L - Rr, CutFrom, CutTo), SoftMat(Smoke, 0, null, 0.25f, 40, 0.18f, 0.88f, 0.95f, 2987, CullMode.Back));
        Mats.Object("Housing", root, shell, SoftMat(Milk, 0, null, 0.5f, 90, 0.42f, 0.16f, 0.62f, 2988, CullMode.Back));
        // Bore light: a soft ring of light at each opening of the bore.
        var lb = new LineBuilder();
        foreach (float z in new[] { -L - 0.002f, L + 0.002f }) Arc(lb, Rb + 0.012f, z, CutTo, CutFrom + 360, 0.0040f, Lin(B0S, 0.9f));
        Mats.Object("Bore light", root, lb.Commit(new Mesh()), LineMat(3.2f, 0.7f, 0, 1, 2997));
        // Main magnet windings: pearl satin (warm light when named).
        var packMat = SoftMat(Hex(0xE9DFD3), focus == "b0" ? 0.35f : 0, B0V, 0.25f, 40, 0.2f);
        foreach (var p in Scanner.Packs)
            Mats.Object("Magnet winding", root, MeshKit.PackCut((float)p.RIn, (float)p.ROut, (float)(p.ZCentre - p.Length / 2), (float)(p.ZCentre + p.Length / 2), CutFrom, CutTo, 96), packMat);
        var first = Scanner.Packs.OrderBy(p => p.ZCentre).First();
        sp.Magnet = Wp(Frames.ToS(0.0, first.ROut, first.ZCentre));
        // Near / far halves relative to the viewpoint.
        Vector3 axis = root.forward, toCam = cam - root.position; toCam -= Vector3.Dot(toCam, axis) * axis; toCam.Normalize();
        bool Near(Vector3 s) => Vector3.Dot(root.rotation * new Vector3(s.x, s.y, 0), toCam) > 0;
        // Gradient coils.
        foreach (var cd in Scanner.GradientPaths)
        {
            var pts = cd.Points.Select(q => Frames.ToS(q)).ToList();
            string key = cd.Coil == CoilId.Gx ? "gx" : cd.Coil == CoilId.Gy ? "gy" : "gz"; bool f = focus == key;
            Color v = cd.Coil == CoilId.Gx ? XV : cd.Coil == CoilId.Gy ? YV : ZV, s = cd.Coil == CoilId.Gx ? XS : cd.Coil == CoilId.Gy ? YS : ZS;
            var far = f ? SoftMat(v, 0.55f, v, 0.4f, 50, 0.3f) : SoftMat(s, 0.03f, v, 0.4f, 50, 0.3f);
            var near = f ? far : SoftMat(s, 0, null, 0.55f, 80, 0.55f, 0.24f, 0.78f, 2996);
            SplitTube(root, cd.Coil + " winding", pts, f ? 0.0058f : 0.0044f, Near, far, near);
        }
        double gzZ = Math.Sqrt(3) / 2 * Scanner.GzRadius, ga = 112 * Math.PI / 180;
        sp.Gradient = Wp(Frames.ToS(Scanner.GzRadius * Math.Cos(ga), Scanner.GzRadius * Math.Sin(ga), -gzZ));
        // The RF (birdcage) coil.
        bool rf = focus == "rf";
        var rfFar = SoftMat(rf ? RfV : RfS, rf ? 0.42f : 0.03f, RfV, 0.4f, 50, 0.3f);
        var rfNear = rf ? rfFar : SoftMat(RfS, 0, null, 0.55f, 80, 0.55f, 0.24f, 0.78f, 2996);
        foreach (var cd in Scanner.BirdcageParts)
            SplitTube(root, "Birdcage part", cd.Points.Select(q => Frames.ToS(q)).ToList(), rf ? 0.0046f : 0.0034f, Near, rfFar, rfNear);
        double ra = 138 * Math.PI / 180;
        sp.Rf = Wp(Frames.ToS(Scanner.BirdcageRadius * Math.Cos(ra), Scanner.BirdcageRadius * Math.Sin(ra), Scanner.BirdcageHalfLength * 0.8));
        // The hand: warm glassy skin with a lit edge, a faint rose veil of muscle, pearl bones; the excited slab glows.
        var hs = (HandSpecimen)S.Specimen; D3 c = hs.Pose.ObjectAtIsocentre; Vector3 off = Frames.ToS(hs.Pose.Offset);
        var hand = new GameObject("Hand").transform; hand.SetParent(root, false);
        hand.localPosition = off - new Vector3((float)c.X, (float)c.Y, (float)-c.Z); hand.localScale = new Vector3(1, 1, -1);
        Vector3 n = root.forward, sliceP = Wp(Frames.ToS(0, 0, S.P.SliceZ));
        var band = new Vector4(n.x, n.y, n.z, Vector3.Dot(n, sliceP)); float half = slab ? (float)S.P.SliceThickness / 2 : 0;
        Material Banded(Material m) { m.SetVector("_Band", band); m.SetFloat("_BandHalf", half); m.SetFloat("_BandSoft", 0.0035f); m.SetColor("_BandColor", slabColour); return m; }
        var skinMat = Banded(SoftMat(Skin, 0.14f, Skin, 0.3f, 30, 0.95f, 0.32f, 0.94f, 2993, CullMode.Back));
        var muscleMat = Banded(SoftMat(Muscle, 0, null, 0.2f, 20, 0.35f, 0.24f, 0.48f, 2992, CullMode.Back));
        var boneMat = Banded(SoftMat(Bone, 0.04f, Bone, 0.4f, 40, 0.35f));
        foreach (var name in anatomy)
        {
            var src = Resources.Load<GameObject>("Anatomy/" + name); if (src == null) continue;
            var mf = src.GetComponentInChildren<MeshFilter>(); if (mf == null) continue;
            Mats.Object(name, hand, mf.sharedMesh, name == "Skin" ? skinMat : name.StartsWith("Muscle_") ? muscleMat : boneMat);
        }
        sp.Hand = Wp(Frames.ToS(0.0, 0.0, -0.06));
        // The proton block's outline in the hand (5 x 5 x 10 mm, true size).
        var bc = Frames.ToS(S.CubeCentre); var bh = Frames.ToSAbs(Frames.ToS(Layouts.GridSize)) / 2;
        for (int k = 0; k < 8; k++) sp.Block[k] = Wp(bc + Vector3.Scale(bh, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
        if (block)
        {
            var ob = new LineBuilder(); BoxEdges(ob, bc, bh, 0.0014f, Lin(Pearl, 1));
            Mats.Object("Block outline", root, ob.Commit(new Mesh()), LineMat(3, 0.6f, 0, 1, 2999));
        }
        // B0 field lines, traced from the magnet's computed field: drawn only outside the housing (never over the hand),
        // flowing out of both bore openings and fading.
        var fl = new LineBuilder();
        foreach (double rho in new[] { 0.0, 0.04, 0.08 })
            for (int k = 0; k < (rho == 0 ? 1 : 6); k++)
            {
                double phi = (k + (rho > 0.05 ? 0.5 : 0)) * Math.PI / 3;
                var pts = FieldLine(rho, phi);
                for (int i = 1; i < pts.Count; i++)
                {
                    float z = Mathf.Abs(pts[i].z), z0 = Mathf.Abs(pts[i - 1].z); if (Mathf.Min(z, z0) < L + 0.004f) continue;
                    float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(L + 0.01f, 0.62f, z));
                    fl.Segment(pts[i - 1], pts[i], 0.0022f, Lin(B0V, fieldAlpha * fade));
                }
            }
        if (fieldAlpha > 0) Mats.Object("B0 field lines", root, fl.Commit(new Mesh()), LineMat(3, 0.55f, 0, 1, 2998));
        sp.Field = Wp(Frames.ToS(0.0, 0.05, -0.47));
        // Pedestal: a porcelain cradle, a slim column and a round base on the floor ringed by a soft line of light.
        float floor = -ScannerCentre.y; var porcelain = SoftMat(Porcelain, 0, null, 0.3f, 30, 0.22f);
        Mats.Object("Cradle", root, RoundedBox(new Vector3(0.34f, 0.05f, 0.56f), 0.022f, 8), porcelain, new Vector3(0, -0.318f, 0));
        var col = new List<Vector2> { new Vector2(0, floor), new Vector2(0.235f, floor), new Vector2(0.248f, floor + 0.008f), new Vector2(0.25f, floor + 0.02f), new Vector2(0.238f, floor + 0.032f), new Vector2(0.10f, floor + 0.040f),
            new Vector2(0.068f, floor + 0.07f), new Vector2(0.056f, floor + 0.14f), new Vector2(0.052f, -0.52f), new Vector2(0.058f, -0.40f), new Vector2(0.085f, -0.352f), new Vector2(0.10f, -0.344f), new Vector2(0, -0.342f) };
        Mats.Object("Column", root, Lathe(col, 48, "Column"), porcelain);
        var ring = new LineBuilder(); Arc(ring, 0.272f, 0, 0, 360, 0.0035f, Lin(B0S, 0.85f));
        var ringT = new GameObject("Floor light").transform; ringT.SetParent(root, false); ringT.localPosition = new Vector3(0, floor + 0.002f, 0); ringT.localRotation = Quaternion.Euler(90, 0, 0);
        Mats.Object("Floor light", ringT, ring.Commit(new Mesh()), LineMat(3, 0.6f, 0, 1, 2997));
        var cl = new LineBuilder(); foreach (float x in new[] { -0.15f, 0.15f }) cl.Segment(new Vector3(x, -0.291f, -0.25f), new Vector3(x, -0.291f, 0.25f), 0.003f, Lin(B0S, 0.9f));
        Mats.Object("Cradle light", root, cl.Commit(new Mesh()), LineMat(3, 0.6f, 0, 1, 2997));
        // Receiver: a small porcelain unit on the column, its signal line lit.
        var rxPos = new Vector3(0.105f, -0.70f, -0.03f);
        Mats.Object("Receiver", root, RoundedBox(new Vector3(0.12f, 0.085f, 0.15f), 0.022f, 8), porcelain, rxPos);
        var win = new GameObject("Receiver face").transform; win.SetParent(root, false); win.localPosition = rxPos + new Vector3(0.0605f, 0, 0); win.localRotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
        Mats.Object("Glass", win, GlassQuad(new Vector2(0.058f, 0.028f), 0.012f), GlassMat(new Vector2(0.058f, 0.028f), 0.014f, 0.9f, 0.8f, 0, null, 2995, null, 0.03f));
        var rl = new LineBuilder(); rl.Segment(new Vector3(-0.042f, 0, -0.002f), new Vector3(0.042f, 0, -0.002f), 0.0022f, Lin(RfS, 1));
        Mats.Object("Receiver line", win, rl.Commit(new Mesh()), LineMat(3, 0.7f, 0, 1, 2996));
        for (int k = -1; k <= 1; k++) Mats.Object("Stage", win, Disc(0.0062f), UnlitMat(Porcelain, 2996, null, 0, true), new Vector3(k * 0.028f, 0, -0.003f));
        sp.Receiver = win.position;
        return sp;
    }

    /// <summary>Tubes along a path, split into the parts on the near and far side of the bore axis (materials differ).</summary>
    static void SplitTube(Transform root, string name, List<Vector3> pts, float radius, Func<Vector3, bool> near, Material far, Material nearMat)
    {
        var run = new List<Vector3> { pts[0] }; bool cur = near(pts[0]);
        for (int i = 1; i < pts.Count; i++)
        {
            bool nb = near(pts[i]); run.Add(pts[i]);
            if (nb != cur || i == pts.Count - 1)
            {
                if (run.Count >= 2) Mats.Object(name, root, MeshKit.Tube(run, radius, 12), cur ? nearMat : far);
                run = new List<Vector3> { pts[i] }; cur = nb;
            }
        }
    }

    /// <summary>A B0 field line through (rho, 0) in the meridian plane at physical azimuth phi (S frame points).</summary>
    static List<Vector3> FieldLine(double rho0, double phi)
    {
        var back = Trace(rho0, phi, -1); var fwd = Trace(rho0, phi, 1);
        back.Reverse(); back.AddRange(fwd.Skip(1)); return back;
    }
    static List<Vector3> Trace(double rho0, double phi, int dir)
    {
        var pts = new List<Vector3>(); double r = rho0, z = 0, ds = 0.005 * dir;
        for (int k = 0; k < 300; k++)
        {
            pts.Add(new Vector3((float)(-r * Math.Cos(phi)), (float)(r * Math.Sin(phi)), (float)z));
            Scanner.MagnetField(r, z, out double br, out double bz); double m = Math.Sqrt(br * br + bz * bz); if (m < 1e-15) break;
            double r2 = Math.Max(0, r + 0.5 * ds * br / m), z2 = z + 0.5 * ds * bz / m;
            Scanner.MagnetField(r2, z2, out br, out bz); m = Math.Sqrt(br * br + bz * bz); if (m < 1e-15) break;
            r = Math.Max(0, r + ds * br / m); z += ds * bz / m;
            if (Math.Abs(z) > 0.64 || r > 0.5) break;
        }
        return pts;
    }

    /// <summary>The housing's cross-section (rho, z) with outward normals, from the bore wall's lower end round the outside
    /// to its upper end (the bore wall itself is the liner).</summary>
    static List<(Vector2 p, Vector2 n)> HousingProfile(float rb, float ro, float l, float rr)
    {
        var prof = new List<(Vector2 p, Vector2 n)>();
        void Corner(float cr, float cz, float a0, float a1)
        {
            for (int k = 0; k <= 8; k++) { float a = Mathf.Lerp(a0, a1, k / 8f) * Mathf.Deg2Rad; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); prof.Add((new Vector2(cr, cz) + d * rr, d)); }
        }
        Corner(rb + rr, -l + rr, 180, 270); Corner(ro - rr, -l + rr, 270, 360); Corner(ro - rr, l - rr, 0, 90); Corner(rb + rr, l - rr, 90, 180);
        return prof;
    }

    /// <summary>Revolves a profile about z over the physical azimuths outside the cutaway; caps: close each cut with the
    /// profile's filled section (a fan from its centre).</summary>
    static Mesh Revolve(List<(Vector2 p, Vector2 n)> prof, float cutFrom, float cutTo, bool caps, Vector2 capCentre, string name)
    {
        var v = new List<Vector3>(); var nn = new List<Vector3>(); var t = new List<int>();
        float a0 = cutTo * Mathf.Deg2Rad, span = (360 - (cutTo - cutFrom)) * Mathf.Deg2Rad; int segs = 120, m = prof.Count;
        for (int s = 0; s <= segs; s++)
        {
            float phi = a0 + span * s / segs; var rad = new Vector3(-Mathf.Cos(phi), Mathf.Sin(phi), 0);
            foreach (var (p, q) in prof) { v.Add(rad * p.x + new Vector3(0, 0, p.y)); nn.Add((rad * q.x + new Vector3(0, 0, q.y)).normalized); }
        }
        for (int s = 0; s < segs; s++)
            for (int j = 0; j < m - 1; j++) { int a = s * m + j, b = a + m; t.Add(a); t.Add(b); t.Add(a + 1); t.Add(a + 1); t.Add(b); t.Add(b + 1); }
        if (caps)
            foreach (var (phi, sign) in new[] { (a0, -1f), (a0 + span, 1f) })
            {
                var rad = new Vector3(-Mathf.Cos(phi), Mathf.Sin(phi), 0); var tang = new Vector3(Mathf.Sin(phi), Mathf.Cos(phi), 0) * sign;
                int c0 = v.Count; v.Add(rad * capCentre.x + new Vector3(0, 0, capCentre.y)); nn.Add(tang);
                for (int j = 0; j <= m; j++) { var p = prof[j % m].p; v.Add(rad * p.x + new Vector3(0, 0, p.y)); nn.Add(tang); }
                for (int j = 0; j < m; j++) { t.Add(c0); t.Add(c0 + 1 + j); t.Add(c0 + 2 + j); }
            }
        return Build(name, v, nn, t);
    }

    static Mesh HousingShell(float rb, float ro, float l, float rr, float cutFrom, float cutTo) =>
        Revolve(HousingProfile(rb, ro, l, rr), cutFrom, cutTo, true, new Vector2(0.5f * (rb + ro), 0), "Housing");

    static Mesh BoreLiner(float rb, float hl, float cutFrom, float cutTo)
    {
        var prof = new List<(Vector2 p, Vector2 n)>();
        for (int k = 0; k <= 8; k++) prof.Add((new Vector2(rb, Mathf.Lerp(hl, -hl, k / 8f)), new Vector2(-1, 0)));
        return Revolve(prof, cutFrom, cutTo, false, Vector2.zero, "Bore liner");
    }

    static void Arc(LineBuilder lb, float r, float z, float fromDeg, float toDeg, float width, Color c)
    {
        int n = 96; Vector3 prev = default;
        for (int k = 0; k <= n; k++)
        {
            float phi = Mathf.Lerp(fromDeg, toDeg, k / (float)n) * Mathf.Deg2Rad; var p = new Vector3(-r * Mathf.Cos(phi), r * Mathf.Sin(phi), z);
            if (k > 0) lb.Segment(prev, p, width, c); prev = p;
        }
    }

    static void BoxEdges(LineBuilder lb, Vector3 c, Vector3 h, float w, Color col)
    {
        for (int a = 0; a < 3; a++)
            for (int s1 = -1; s1 <= 1; s1 += 2)
                for (int s2 = -1; s2 <= 1; s2 += 2)
                {
                    int b = (a + 1) % 3, d = (a + 2) % 3; Vector3 p = Vector3.zero; p[b] = s1 * h[b]; p[d] = s2 * h[d]; var q = p; p[a] = -h[a]; q[a] = h[a];
                    lb.Segment(c + p, c + q, w, col);
                }
    }

    // ------------------------------------------------------------------------------------------------ protons

    sealed class ProtonState { public Vector3 Pos, Dir; public double Phase, Tip; public bool Band, Present; }

    static uint Hash(uint x) { x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16; return x; }
    static double Unit(ref uint h) { h = Hash(h + 0x9e3779b9u); return (h + 0.5) / 4294967296.0; }
    static double SmoothStep(double a, double b, double x) { double t = Math.Max(0, Math.Min(1, (x - a) / (b - a))); return t * t * (3 - 2 * t); }

    /// <summary>Each proton's moment (thermal, as in 0.8.4's CubeView: a Boltzmann-shaped bias along its isochromat) and
    /// its precession phase and tip, rotating frame, at time t.</summary>
    static ProtonState[] Protons(double t)
    {
        sim.Update(t, false);
        var s = S; var cube = s.Cube; var ev = s.CubeEval; int n = cube.N; var res = new ProtonState[n]; double half = s.P.BandwidthEff / 2;
        for (int i = 0; i < n; i++)
        {
            uint h = Hash((uint)i * 2654435761u + 12345u);
            double u1 = Unit(ref h), u2 = Unit(ref h), u3 = Unit(ref h), u4 = Unit(ref h); Unit(ref h);
            const double kappa = 4.0;
            double c = 1 + Math.Log(u1 + (1 - u1) * Math.Exp(-2 * kappa)) / kappa, sn = Math.Sqrt(Math.Max(0, 1 - c * c)), ph = 2 * Math.PI * u2;
            var bias = new Vector3((float)(sn * Math.Cos(ph)), (float)(sn * Math.Sin(ph)), (float)c);
            double ci = 2 * u3 - 1, si = Math.Sqrt(Math.Max(0, 1 - ci * ci)), pi = 2 * Math.PI * u4;
            var iso = new Vector3((float)(si * Math.Cos(pi)), (float)(si * Math.Sin(pi)), (float)ci);
            double X = ev.X[i], Y = ev.Y[i], Z = ev.Z[i];
            double mp = Math.Sqrt(X * X + Y * Y), mag = Math.Sqrt(mp * mp + Z * Z);
            double theta = Math.Atan2(mp, Z), wTip = SmoothStep(0.03, 0.12, mp / Math.Max(mag, 1e-9));
            double azFree = -(ev.Free != null && ev.Free.Length == n ? ev.Free[i] : 0), azTip = Math.Atan2(Y, X) - Math.PI / 2;
            double az = azFree + wTip * Math.IEEERemainder(azTip - azFree, 2 * Math.PI);
            float ct = (float)Math.Cos(theta), st = (float)Math.Sin(theta);
            float y1 = ct * bias.y + st * bias.z, z1 = -st * bias.y + ct * bias.z, x1 = bias.x;
            float ca = (float)Math.Cos(az), sa = (float)Math.Sin(az);
            var turned = new Vector3(ca * x1 - sa * y1, sa * x1 + ca * y1, z1);
            float order = Mathf.Clamp01((float)(mag / Math.Max(1e-9, cube.Pd[i])));
            var up = Vector3.Lerp(iso, turned, order); float ul = up.magnitude; var dir = ul > 1e-3f ? up / ul : turned;
            double own = Math.Atan2(dir.y, dir.x), isoPhase = Math.Atan2(Y, X);
            double w = SmoothStep(0.03, 0.12, mp / Math.Max(mag, 1e-9)) * Math.Min(1, mag / Math.Max(1e-9, cube.Pd[i]));
            res[i] = new ProtonState
            {
                Pos = Frames.ToS(cube.Pos[i] - s.CubeCentre), Dir = Frames.ToS(new D3(dir.x, dir.y, dir.z)),
                Phase = own + w * Math.IEEERemainder(isoPhase - own, 2 * Math.PI), Tip = theta,
                Band = Math.Abs(s.CubeDfSlice[i]) <= half, Present = cube.Pd[i] > 0
            };
        }
        return res;
    }

    sealed class VolumeParts { public Transform Root; public Vector3[] Corners = new Vector3[8]; }

    /// <summary>The proton volume: a glass case (smoked far walls with a millimetre grid, lit edges) holding the soft
    /// needles; axes and B0 as small tagged arrows; B1 when the pulse plays. focus "tip"/"band": the band vivid, the rest soft.</summary>
    static VolumeParts BuildVolume(Transform parent, Vector3 centre, ProtonState[] ps, string focus, bool b1, Vector3 eye)
    {
        var vp = new VolumeParts();
        var root = new GameObject("Proton volume").transform; root.SetParent(parent, false); root.position = centre; root.rotation = Quaternion.Euler(0, Yaw, 0); vp.Root = root;
        var size = new Vector3(0.005f, 0.005f, 0.010f) * Magnify; var h = size / 2;
        for (int k = 0; k < 8; k++) vp.Corners[k] = root.TransformPoint(Vector3.Scale(h, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
        // Far walls: smoked glass with a millimetre grid.
        for (int a = 0; a < 3; a++)
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                var nl = Vector3.zero; nl[a] = sgn; var fc = nl * h[a]; var nw = root.rotation * nl;
                if (Vector3.Dot(nw, root.TransformPoint(fc) - eye) <= 0) continue;
                Vector3 upL = a == 1 ? Vector3.forward : Vector3.up;
                var face = new GameObject("Wall").transform; face.SetParent(root, false); face.localPosition = fc; face.localRotation = Quaternion.LookRotation(nl, upL);
                Vector2 half = a == 0 ? new Vector2(h.z, h.y) : a == 1 ? new Vector2(h.x, h.z) : new Vector2(h.x, h.y);
                Mats.Object("Glass", face, GlassQuad(half, 0.02f), GlassMat(half, 0.010f, 0.84f, 0.76f, 0, null, 2990, null, 0.03f));
                var g = new LineBuilder(); float step = 0.001f * Magnify;
                for (float x = -half.x + step; x < half.x - 1e-4f; x += step) g.Segment(new Vector3(x, -half.y, -0.001f), new Vector3(x, half.y, -0.001f), 0.0009f, Lin(Text3, 0.36f));
                for (float y = -half.y + step; y < half.y - 1e-4f; y += step) g.Segment(new Vector3(-half.x, y, -0.001f), new Vector3(half.x, y, -0.001f), 0.0009f, Lin(Text3, 0.36f));
                Mats.Object("Grid", face, g.Commit(new Mesh()), LineMat(2.5f, 0, 0, 1, 2991));
            }
        var e = new LineBuilder(); BoxEdges(e, Vector3.zero, h, 0.0024f, Lin(Pearl, 0.95f));
        Mats.Object("Edges", root, e.Commit(new Mesh()), LineMat(3, 0.45f, 0, 1, 2995));
        // Needles: the named set vivid in its phase colour (softly glowing as it tips), the rest soft pastel.
        float spacing = (float)Layouts.ProtonSpacing * Magnify, len = 0.86f * spacing;
        foreach (var p in ps)
        {
            if (!p.Present) continue;
            var vivid = PhaseColour(p.Phase); float tip = (float)Math.Sin(Math.Min(p.Tip, Math.PI / 2));
            bool named = focus == "tip" || focus == "band" ? p.Band : true;
            Color col = named ? OkMix(SoftOf(vivid, 0.45f), vivid, Mathf.Clamp01(0.3f + 0.9f * tip)) : SoftOf(vivid, 0.74f);
            float glow = named ? 0.25f * tip : 0;
            var rot = root.rotation * Quaternion.FromToRotation(Vector3.up, p.Dir);
            var go = Mats.Object("Proton", parent, needleMesh, SoftMat(col, glow, vivid, 0.35f, 40, 0.35f), root.TransformPoint(p.Pos * Magnify), rot, Vector3.one * len);
            Mats.Object("Spin ring", go.transform, ringMesh, SoftMat(OkMix(col, Pearl, 0.3f), glow * 0.5f, vivid, 0.3f, 30, 0.3f), new Vector3(0, -0.14f, 0));
        }
        // Axes (soft) at the near bottom corner, each with a small tag; B0 above the case (warm light).
        // The gizmo sits at the bottom corner furthest to the left as seen from the eye (clear of the strip below).
        int nearest = 0; float best = float.MaxValue; var viewRight = Vector3.Cross(Vector3.up, (centre - eye).normalized).normalized;
        for (int k = 0; k < 8; k++) { if ((k & 2) != 0) continue; float d = Vector3.Dot(vp.Corners[k] - eye, viewRight); if (d < best) { best = d; nearest = k; } }
        var corner = Vector3.Scale(h, new Vector3((nearest & 1) == 0 ? -1 : 1, -1, (nearest & 4) == 0 ? -1 : 1));
        var origin = corner + Vector3.Scale(new Vector3(Mathf.Sign(corner.x), 0, Mathf.Sign(corner.z)), new Vector3(0.035f, 0, 0.035f));
        string[] names = { "x", "y", "z" }; Color[] soft = { XS, YS, ZS }, viv = { XV, YV, ZV };
        for (int k = 0; k < 3; k++)
        {
            var d = Vector3.zero; d[k] = 1; var dS = Frames.ToS(new D3(d.x, d.y, d.z)); // physical +x is S -x
            Mats.Object("Axis " + names[k], root, Arrow(0.07f, 0.0034f, 0.0088f, 0.021f), SoftMat(soft[k], 0.05f, viv[k], 0.35f, 40, 0.3f), origin, Quaternion.FromToRotation(Vector3.up, dS));
            MiniTag(parent, names[k], root.TransformPoint(origin + dS * 0.094f), soft[k], 0.70f, eye);
        }
        var b0From = new Vector3(0, h.y + 0.045f, -h.z * 0.05f);
        Mats.Object("B0", root, Arrow(0.20f, 0.0045f, 0.011f, 0.026f), SoftMat(B0V, 0.35f, B0V, 0.3f, 40, 0.3f), b0From, Quaternion.FromToRotation(Vector3.up, Vector3.forward));
        MiniTag(parent, "B₀", root.TransformPoint(b0From + new Vector3(0, 0.03f, 0.215f)), B0V, 0.72f, eye);
        if (b1)
        {
            var b1From = new Vector3(0.06f, h.y + 0.045f, -h.z * 0.85f);
            Mats.Object("B1", root, Arrow(0.13f, 0.0048f, 0.012f, 0.028f), SoftMat(RfV, 0.55f, RfV, 0.35f, 40, 0.35f), b1From, Quaternion.FromToRotation(Vector3.up, new Vector3(-1, 0, 0)));
            Tag(parent, "Radio pulse", null, root.TransformPoint(b1From + new Vector3(-0.07f, 0.05f, 0)), RfV, 0.75f, eye);
        }
        Tag(parent, "Protons in a 5 × 5 × 10 mm block of the hand", null, root.TransformPoint(new Vector3(0, h.y + 0.15f, 0)), Pearl, 0.78f, eye);
        return vp;
    }

    static void ZoomLines(Transform parent, ScannerParts sc, VolumeParts vp)
    {
        var dir = (vp.Root.position - sc.Block[0]).normalized; var lb = new LineBuilder();
        // Join the four block corners on the side facing the volume to the matching corners of the case.
        var mid = sc.Block.Aggregate(Vector3.zero, (a, b) => a + b) / 8;
        foreach (int k in Enumerable.Range(0, 8).OrderByDescending(k => Vector3.Dot(sc.Block[k] - mid, dir)).Take(4)) lb.Segment(sc.Block[k], vp.Corners[k], 0.0012f, Lin(Pearl, 0.4f));
        Mats.Object("Zoom lines", parent, lb.Commit(new Mesh()), LineMat(3, 0.3f, 0, 1, 2999));
    }

    // ------------------------------------------------------------------------------------------------ plots

    static void SequencePanel(Transform parent, Vector3 pos, Vector3 eye, Vector2 size, Block blk, double now, string focus, float held = 0)
    {
        var p = Panel(parent, "Pulse sequence", pos, eye, size, 0.03f, 0.82f, 0.64f, held, Pearl);
        float l = -size.x / 2 + 0.028f, top = size.y / 2 - 0.024f;
        Text(p, "Pulse sequence", new Vector3(l, top, -0.003f), 0.0210f, Text1, fSemi, TextAlignmentOptions.TopLeft, 0.3f);
        double t0 = blk.RfStart - 0.4e-3, t1 = blk.Adc[blk.Adc.Length - 1] + 0.6e-3;
        string[] names = { "Radio pulse", "z gradient", "y gradient", "x gradient", "Receiver" }; string[] keys = { "rf", "gz", "gy", "gx", "adc" };
        Color[] viv = { RfV, ZV, YV, XV, B0V }, soft = { RfS, ZS, YS, XS, B0S };
        int n = 700; var prog = S.Program;
        var val = new float[5, n];
        for (int k = 0; k < n; k++)
        {
            double t = t0 + (t1 - t0) * k / (n - 1);
            prog.RfAt(t, out double re, out double im); prog.CurrentsAt(t, out double ix, out double iy, out double iz);
            val[0, k] = (float)Math.Sqrt(re * re + im * im); val[1, k] = (float)iz; val[2, k] = (float)iy; val[3, k] = (float)ix;
            val[4, k] = t >= blk.Adc[0] && t <= blk.Adc[blk.Adc.Length - 1] ? 1 : 0;
        }
        float x0 = l + 0.142f, x1 = size.x / 2 - 0.026f, rowH = 0.047f, amp = 0.0165f, y0 = top - 0.074f;
        float xNow = Mathf.Lerp(x0, x1, (float)((now - t0) / (t1 - t0)));
        for (int r = 0; r < 5; r++)
        {
            float y = y0 - r * rowH; bool f = focus == keys[r] || (focus == "gx" && keys[r] == "adc");
            float max = 1e-12f; for (int k = 0; k < n; k++) max = Mathf.Max(max, Mathf.Abs(val[r, k]));
            Color c = f ? viv[r] : soft[r];
            Text(p, names[r], new Vector3(l, y, -0.003f), f ? 0.0150f : 0.0142f, f ? viv[r] : Text2, f ? fSemi : fReg, TextAlignmentOptions.Left, 0.15f);
            var bl = new LineBuilder(); bl.Segment(new Vector3(x0, y, -0.002f), new Vector3(x1, y, -0.002f), 0.0009f, Lin(Text3, 0.35f));
            Mats.Object("Baseline", p, bl.Commit(new Mesh()), LineMat(2.5f, 0, 0, 1, 3005));
            var lb = new LineBuilder(); var area = new List<Vector3>(); Vector3 prev = default;
            for (int k = 0; k < n; k++)
            {
                float x = Mathf.Lerp(x0, x1, k / (float)(n - 1)), v = val[r, k] / max * (r == 4 ? 0.6f : 1);
                var q = new Vector3(x, y + v * amp, -0.003f);
                if (k > 0) lb.Segment(prev, q, f ? 0.0036f : 0.0024f, Lin(c, 1)); prev = q; area.Add(q);
            }
            Mats.Object("Trace", p, lb.Commit(new Mesh()), LineMat(3, f ? 0.75f : 0.3f, 0, 1, 3010));
            Mats.Object("Area", p, AreaMesh(area, y, Lin(c, f ? 0.30f : 0.12f)), UnlitMat(Color.white, 3006));
        }
        if (held > 0) liftAfter.Add(p);
        var nl = new LineBuilder(); nl.Segment(new Vector3(xNow, y0 + 0.028f, -0.004f), new Vector3(xNow, y0 - 4 * rowH - 0.018f, -0.004f), 0.0016f, Lin(Pearl, 0.85f));
        Mats.Object("Now", p, nl.Commit(new Mesh()), LineMat(3, 0.5f, 0, 1, 3012));
        Mats.Object("Now knob", p, Disc(0.0055f), UnlitMat(Pearl, 3013, null, 0, true), new Vector3(xNow, y0 + 0.030f, -0.005f));
        Text(p, $"time →  one repetition, {(t1 - t0) * 1e3:F0} ms", new Vector3(x1, y0 - 4 * rowH - 0.033f, -0.003f), 0.0118f, Text3, fReg, TextAlignmentOptions.Right, 0.3f);
    }

    /// <summary>The received signal as a 3-D line standing off its glass (R0.8.2): time runs along the axis, I up and Q
    /// toward the viewer, so a turning phase is a helix; its colour is that phase (as the protons). I and Q shadows lie on
    /// the back and floor planes. Heard samples are bright; the rest of the readout is a faint guide.</summary>
    static void SignalPanel(Transform parent, Vector3 pos, Vector3 eye, Vector2 size, Block blk, double now, bool focus)
    {
        var p = Panel(parent, "Received signal", pos, eye, size, 0.03f, 0.82f, 0.64f, focus ? 0.35f : 0, Pearl);
        float l = -size.x / 2 + 0.028f, top = size.y / 2 - 0.024f;
        Text(p, "Received signal", new Vector3(l, top, -0.003f), 0.0210f, Text1, fSemi, TextAlignmentOptions.TopLeft, 0.3f);
        Text(p, "the coil’s voltage, heard by the receiver", new Vector3(l, top - 0.036f, -0.003f), 0.0118f, Text3, fReg, TextAlignmentOptions.TopLeft, 0.42f);
        var acq = S.Slices[blk.Slice]; int n = acq.N; double max = 1e-30; int peak = 0;
        for (int j = 0; j < n; j++) { int k = blk.Row * n + j; double m = Math.Sqrt(acq.KRe[k] * acq.KRe[k] + acq.KIm[k] * acq.KIm[k]); if (m > max) { max = m; peak = j; } }
        var hx = new GameObject("Signal (3-D)").transform; hx.SetParent(p, false); hx.localPosition = new Vector3(0.0f, -0.048f, -0.05f); hx.localRotation = Quaternion.Euler(16, -28, 0);
        float x0 = -0.165f, x1 = 0.165f, amp = 0.052f, back = amp * 1.25f, floorY = -amp * 1.25f;
        int sub = 6, last = -1; for (int j = 0; j < n; j++) if (blk.Adc[j] <= now) last = j;
        var H = new LineBuilder(); var SI = new LineBuilder(); var SQ = new LineBuilder(); var guide = new LineBuilder();
        guide.Segment(new Vector3(x0, 0, 0), new Vector3(x1 + 0.012f, 0, 0), 0.0012f, Lin(Text3, 0.55f));
        guide.Segment(new Vector3(x0, 0, back), new Vector3(x1, 0, back), 0.0008f, Lin(Text3, 0.35f));
        guide.Segment(new Vector3(x0, floorY, 0), new Vector3(x1, floorY, 0), 0.0008f, Lin(Text3, 0.35f));
        Vector3 ph = default, pi = default, pq = default;
        for (int j = 0; j < n - 1; j++)
            for (int s = 0; s < sub; s++)
            {
                float f = s / (float)sub; int k0 = blk.Row * n + j, k1 = k0 + 1;
                float re = Mathf.Lerp((float)acq.KRe[k0], (float)acq.KRe[k1], f) / (float)max, im = Mathf.Lerp((float)acq.KIm[k0], (float)acq.KIm[k1], f) / (float)max;
                float x = Mathf.Lerp(x0, x1, (j + f) / (n - 1)); bool heard = j + f <= last;
                var qh = new Vector3(x, re * amp, -im * amp); var qi = new Vector3(x, re * amp, back); var qq = new Vector3(x, floorY, -im * amp);
                float strength = Mathf.Clamp01(Mathf.Sqrt(re * re + im * im) / 0.35f);
                var phase = PhaseColour(Math.Atan2(im, re)); var col = OkMix(SoftOf(phase, 0.7f), phase, strength);
                if (j + s > 0)
                {
                    H.Segment(ph, qh, focus ? 0.0036f : 0.0028f, Lin(heard ? col : Text3, heard ? 1 : 0.14f));
                    SI.Segment(pi, qi, 0.0014f, Lin(Pearl, heard ? 0.55f : 0.10f));
                    SQ.Segment(pq, qq, 0.0014f, Lin(Pearl, heard ? 0.40f : 0.08f));
                }
                ph = qh; pi = qi; pq = qq;
            }
        Mats.Object("Guides", hx, guide.Commit(new Mesh()), LineMat(2.5f, 0, 0, 1, 3005));
        Mats.Object("I shadow", hx, SI.Commit(new Mesh()), LineMat(3, 0.1f, 0, 1, 3007));
        Mats.Object("Q shadow", hx, SQ.Commit(new Mesh()), LineMat(3, 0.1f, 0, 1, 3007));
        Mats.Object("Signal", hx, H.Commit(new Mesh()), LineMat(3, focus ? 0.7f : 0.35f, 0, 1, 3010));
        int kn = blk.Row * n + Math.Max(0, last); float xn = Mathf.Lerp(x0, x1, Mathf.Max(0, last) / (float)(n - 1));
        var knob = new GameObject("Now").transform; knob.SetParent(hx, false); knob.localPosition = new Vector3(xn, (float)(acq.KRe[kn] / max) * amp, -(float)(acq.KIm[kn] / max) * amp);
        knob.rotation = Quaternion.LookRotation(knob.position - eye, Vector3.up);
        Mats.Object("Knob", knob, Disc(0.0058f), UnlitMat(Pearl, 3013, null, 0, true));
        float xp = Mathf.Lerp(x0, x1, peak / (float)(n - 1));
        LabelAt(hx, "echo", new Vector3(xp, amp * 1.3f, 0), 0.0118f, Text3, eye);
        LabelAt(hx, "I", new Vector3(x1 + 0.016f, 0, back), 0.0118f, Text3, eye);
        LabelAt(hx, "Q", new Vector3(x1 + 0.016f, floorY, 0), 0.0118f, Text3, eye);
        LabelAt(hx, "time →", new Vector3(x1 + 0.034f, 0, 0), 0.0118f, Text3, eye);
    }

    static void LabelAt(Transform parent, string text, Vector3 local, float cap, Color c, Vector3 eye)
    {
        var t = new GameObject("Label " + text).transform; t.SetParent(parent, false); t.localPosition = local;
        t.rotation = Quaternion.LookRotation(t.position - eye, Vector3.up);
        Text(t, text, Vector3.zero, cap, c, fReg, TextAlignmentOptions.Center, 0.12f);
    }

    static void DataPanel(Transform parent, Vector3 pos, Vector3 eye, Vector2 size, Block blk, double now)
    {
        var p = Panel(parent, "Measured data", pos, eye, size);
        float l = -size.x / 2 + 0.028f, top = size.y / 2 - 0.024f;
        var acq = S.Slices[blk.Slice]; int n = acq.N, rows = 0; for (int r = 0; r < n; r++) if (acq.RowTime[r] <= now) rows++;
        Text(p, "Measured data → image", new Vector3(l, top, -0.003f), 0.0210f, Text1, fSemi, TextAlignmentOptions.TopLeft, 0.4f);
        Text(p, $"k-space · {rows} of {n} rows", new Vector3(-0.145f, top - 0.044f, -0.003f), 0.0128f, Text2, fReg, TextAlignmentOptions.Top, 0.3f);
        Text(p, $"image · slice {blk.Slice + 1} of {S.Slices.Length}", new Vector3(0.150f, top - 0.044f, -0.003f), 0.0128f, Text2, fReg, TextAlignmentOptions.Top, 0.3f);
        Text(p, "→", new Vector3(0.012f, -0.040f, -0.003f), 0.02f, Text3, fReg, TextAlignmentOptions.Center, 0.05f);
        // k-space relief (log |S| above the noise floor, warm monochrome), the rows measured so far; the current row lit.
        double max = Math.Max(1e-30, acq.MaxAbs);
        var mags = new List<double>(); for (int r = 0; r < n; r++) if (acq.RowTime[r] <= now) for (int j = 0; j < n; j++) { int k = r * n + j; mags.Add(Math.Sqrt(acq.KRe[k] * acq.KRe[k] + acq.KIm[k] * acq.KIm[k]) / max); }
        mags.Sort(); double floorM = mags.Count > 0 ? mags[mags.Count / 2] : 0; float floorU = (float)(Math.Log10(1 + 1000 * floorM) / 3);
        float kw = 0.20f, kh = 0.06f;
        var relief = new GameObject("k-space").transform; relief.SetParent(p, false); relief.localPosition = new Vector3(-0.145f, -0.085f, -0.07f); relief.localRotation = Quaternion.Euler(-36, 0, 0);
        var hgt = new float[n, n];
        for (int r = 0; r < n; r++) for (int j = 0; j < n; j++)
            {
                bool have = acq.RowTime[r] <= now || r == blk.Row; int k = r * n + j;
                double m = have ? Math.Sqrt(acq.KRe[k] * acq.KRe[k] + acq.KIm[k] * acq.KIm[k]) / max : 0;
                float u = (float)(Math.Log10(1 + 1000 * m) / 3); hgt[r, j] = have ? Mathf.Clamp01((u - floorU) / Mathf.Max(0.05f, 1 - floorU)) : 0;
            }
        var v = new List<Vector3>(); var nn = new List<Vector3>(); var cc = new List<Color>(); var t = new List<int>();
        for (int r = 0; r < n; r++) for (int j = 0; j < n; j++)
            {
                float x = -kw / 2 + kw * j / (n - 1), z = -kw / 2 + kw * r / (n - 1);
                float hl = hgt[r, Math.Max(0, j - 1)], hr = hgt[r, Math.Min(n - 1, j + 1)], hd = hgt[Math.Max(0, r - 1), j], hu = hgt[Math.Min(n - 1, r + 1), j];
                v.Add(new Vector3(x, hgt[r, j] * kh, z)); nn.Add(new Vector3(-(hr - hl) * kh / (2 * kw / n), 1, -(hu - hd) * kh / (2 * kw / n)).normalized);
                bool have = acq.RowTime[r] <= now; bool cur = r == blk.Row;
                Color col = cur ? OkMix(XS, XV, 0.3f + 0.7f * hgt[r, j]) : have ? OkMix(Hex(0x6A5446), Hex(0xFFF6EA), Mathf.Pow(hgt[r, j], 0.7f)) : Hex(0x4A3C33);
                cc.Add(Lin(col, 1));
            }
        for (int r = 0; r < n - 1; r++) for (int j = 0; j < n - 1; j++) { int a = r * n + j; t.Add(a); t.Add(a + n); t.Add(a + 1); t.Add(a + 1); t.Add(a + n); t.Add(a + n + 1); }
        var kmat = SoftMat(Color.white, 0, null, 0.25f, 30, 0.2f, 1, 1, 3008); kmat.SetFloat("_Vertex", 1);
        Mats.Object("Relief", relief, Build("k-space relief", v, nn, t, null, cc), kmat);
        // Image.
        var img = acq.Reconstruct(now, out _); double im = 1e-30; foreach (var q in img) im = Math.Max(im, q);
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) px[y * n + (n - 1 - x)] = OkMix(Hex(0x15100D), Hex(0xFFF6EA), (float)Math.Pow(img[y * n + x] / im, 0.8));
        tex.SetPixels(px); tex.Apply();
        Mats.Object("Image", p, Quad(0.18f, 0.18f), UnlitMat(Color.white, 3008, tex, 0.05f), new Vector3(0.150f, -0.066f, -0.003f));
    }

    static void CloseUpPanel(Transform parent, Vector3 pos, Vector3 eye, Vector2 size, ProtonState[] ps)
    {
        var p = Panel(parent, "One proton", pos, eye, size);
        float l = -size.x / 2 + 0.028f, top = size.y / 2 - 0.024f;
        Text(p, "One proton, up close", new Vector3(l, top, -0.003f), 0.0210f, Text1, fSemi, TextAlignmentOptions.TopLeft, 0.35f);
        var sel = ps.Where(q => q.Present && q.Band).OrderBy(q => q.Pos.sqrMagnitude).FirstOrDefault() ?? ps[0];
        var col = PhaseColour(sel.Phase);
        for (int side = 0; side < 2; side++)
        {
            var c = new Vector3(side == 0 ? -0.10f : 0.10f, -0.020f, -0.06f);
            var g = new GameObject(side == 0 ? "In the room" : "Turning with it").transform; g.SetParent(p, false); g.localPosition = c; g.localRotation = Quaternion.Euler(-24, 0, 0);
            Mats.Object("B0", g, Arrow(0.11f, 0.0028f, 0.0075f, 0.018f), SoftMat(B0V, 0.35f, B0V, 0.3f, 40, 0.3f), new Vector3(0, -0.045f, 0));
            var ring = new LineBuilder(); float tip = (float)Math.Min(sel.Tip, Math.PI / 2), rr = 0.058f * Mathf.Sin(tip), hh = -0.045f + 0.058f * Mathf.Cos(tip);
            int segs = 72; Vector3 prev = default;
            for (int k = 0; k <= segs; k++)
            {
                float a = k * 2 * Mathf.PI / segs; var q = new Vector3(rr * Mathf.Cos(a), hh, rr * Mathf.Sin(a));
                var cc = side == 1 ? PhaseColour(a + Math.PI / 2) : Pearl;
                if (k > 0) ring.Segment(prev, q, side == 1 ? 0.0034f : 0.0016f, Lin(cc, side == 1 ? 1 : 0.5f)); prev = q;
            }
            Mats.Object("Circle", g, ring.Commit(new Mesh()), LineMat(3, 0.45f, 0, 1, 3010));
            float az = side == 0 ? 2.1f : 0f;
            var d = new Vector3(Mathf.Sin(tip) * Mathf.Cos(az), Mathf.Cos(tip), Mathf.Sin(tip) * Mathf.Sin(az));
            Mats.Object("Proton", g, needleMesh, SoftMat(col, 0.22f, col, 0.35f, 40, 0.35f), new Vector3(0, -0.045f, 0) + d * 0.03f, Quaternion.FromToRotation(Vector3.up, d), Vector3.one * 0.062f);
            Text(p, side == 0 ? "In the room" : "Turning with it", new Vector3(c.x, -size.y / 2 + 0.030f, -0.003f), 0.0128f, Text2, fReg, TextAlignmentOptions.Center, 0.2f);
        }
    }

    static void Strip(Transform parent, Vector3 pos, Vector3 eye, string step, string title, float progress, string subtitle)
    {
        var size = new Vector2(0.92f, 0.126f);
        var p = Panel(parent, "Strip", pos, eye, size, size.y / 2, 0.86f, 0.72f);
        float l = -size.x / 2 + 0.040f;
        Text(p, "STEP " + step, new Vector3(l, 0.028f, -0.003f), 0.0088f, Text3, fSemi, TextAlignmentOptions.Left, 0.2f, 0, 12);
        Text(p, title, new Vector3(l, 0.002f, -0.003f), 0.0115f, Text1, fSemi, TextAlignmentOptions.Left, 0.2f);
        var pl = new LineBuilder(); pl.Segment(new Vector3(l, -0.026f, -0.003f), new Vector3(l + 0.15f, -0.026f, -0.003f), 0.0026f, Lin(Text3, 0.35f));
        pl.Segment(new Vector3(l, -0.026f, -0.0035f), new Vector3(l + 0.15f * progress, -0.026f, -0.0035f), 0.0026f, Lin(Pearl, 1));
        Mats.Object("Progress", p, pl.Commit(new Mesh()), LineMat(2, 0.3f, 0, 1, 3010));
        Text(p, subtitle, new Vector3(-0.205f, 0.0f, -0.003f), 0.0146f, Text1, fReg, TextAlignmentOptions.Left, 0.50f, 4);
        var btn = new GameObject("Pause").transform; btn.SetParent(p, false); btn.localPosition = new Vector3(size.x / 2 - 0.112f, 0, -0.004f);
        Mats.Object("Button", btn, GlassQuad(new Vector2(0.027f, 0.027f), 0.02f), GlassMat(new Vector2(0.027f, 0.027f), 0.027f, 0.95f, 0.95f, 0, null, 3010, Pearl, 0.1f));
        Mats.Object("Bar", btn, Quad(0.0050f, 0.018f), UnlitMat(Espresso, 3050, null, 0.5f), new Vector3(-0.0048f, 0, -0.002f));
        Mats.Object("Bar", btn, Quad(0.0050f, 0.018f), UnlitMat(Espresso, 3050, null, 0.5f), new Vector3(0.0048f, 0, -0.002f));
        Chip(p, "CC", new Vector3(size.x / 2 - 0.058f, 0.021f, -0.004f), 0.0088f);
        Chip(p, "George", new Vector3(size.x / 2 - 0.058f, -0.021f, -0.004f), 0.0088f);
    }

    // ------------------------------------------------------------------------------------------------ UI pieces

    static Transform Panel(Transform parent, string name, Vector3 worldPos, Vector3 eye, Vector2 size, float radius = 0.03f, float alphaC = 0.82f, float alphaR = 0.64f, float focus = 0, Color? accent = null)
    {
        var t = new GameObject(name).transform; t.SetParent(parent, false); t.position = worldPos; t.rotation = Quaternion.LookRotation(worldPos - eye, Vector3.up);
        Mats.Object("Glass", t, GlassQuad(size / 2, 0.04f), GlassMat(size / 2, radius, alphaC, alphaR, focus, accent));
        return t;
    }

    static void Chip(Transform parent, string text, Vector3 local, float cap)
    {
        var t = new GameObject("Chip").transform; t.SetParent(parent, false); t.localPosition = local;
        var tx = Text(t, text, new Vector3(0, 0, -0.002f), cap, Text2, fSemi, TextAlignmentOptions.Center, 0.3f);
        var half = new Vector2(tx.preferredWidth / 2 + cap * 1.6f, cap * 1.75f);
        Mats.Object("Pill", t, GlassQuad(half, 0.02f), GlassMat(half, half.y, 0.55f, 0.45f, 0, null, 3010, Hex(0x3A2F29), 0.035f));
    }

    /// <summary>A glass tag facing the eye with a colour dot, and a light leader to the part it names (when given).</summary>
    static void Tag(Transform parent, string text, Vector3? anchor, Vector3 at, Color accent, float deg, Vector3? eye = null)
    {
        var e = eye ?? Eye; float cap = (at - e).magnitude * Mathf.Tan(deg * Mathf.Deg2Rad);
        var t = new GameObject("Tag " + text).transform; t.SetParent(parent, false); t.position = at; t.rotation = Quaternion.LookRotation(at - e, Vector3.up);
        var tx = Text(t, text, new Vector3(cap * 0.9f, 0, -0.002f), cap, Text1, fSemi, TextAlignmentOptions.Center, 0.6f);
        var half = new Vector2(tx.preferredWidth / 2 + cap * 2.4f, cap * 1.6f);
        Mats.Object("Pill", t, GlassQuad(half, 0.02f), GlassMat(half, half.y, 0.86f, 0.76f, 0, null, 3020, null, 0.04f));
        Mats.Object("Dot", t, Disc(cap * 0.62f), UnlitMat(accent, 3045, null, 0, true), new Vector3(-half.x + cap * 1.5f, 0, -0.002f));
        if (anchor is Vector3 a)
        {
            var lb = new LineBuilder(); lb.Segment(a, at, 0.0014f, Lin(Pearl, 0.8f));
            Mats.Object("Leader", parent, lb.Commit(new Mesh()), LineMat(3, 0.4f, 0, 1, 3015));
            var d = new GameObject("Anchor").transform; d.SetParent(parent, false); d.position = a; d.rotation = Quaternion.LookRotation(a - e, Vector3.up);
            Mats.Object("Anchor halo", d, Disc(0.0075f), UnlitMat(new Color(Pearl.r, Pearl.g, Pearl.b, 0.30f), 3044, null, 0, true));
            Mats.Object("Anchor dot", d, Disc(0.0036f), UnlitMat(Pearl, 3046, null, 0, true), new Vector3(0, 0, -0.0005f));
        }
    }

    /// <summary>A small glass pill holding a short coloured label (axis letters, B0).</summary>
    static void MiniTag(Transform parent, string text, Vector3 at, Color c, float deg, Vector3 eye)
    {
        float cap = (at - eye).magnitude * Mathf.Tan(deg * Mathf.Deg2Rad);
        var t = new GameObject("Mini tag " + text).transform; t.SetParent(parent, false); t.position = at; t.rotation = Quaternion.LookRotation(at - eye, Vector3.up);
        var tx = Text(t, text, new Vector3(0, 0, -0.002f), cap, c, fSemi, TextAlignmentOptions.Center, 0.1f);
        var half = new Vector2(Mathf.Max(tx.preferredWidth / 2 + cap * 0.9f, cap * 1.45f), cap * 1.45f);
        Mats.Object("Pill", t, GlassQuad(half, 0.02f), GlassMat(half, half.y, 0.86f, 0.76f, 0, null, 3020, null, 0.04f));
    }

    static void Label(Transform parent, string text, Vector3 at, float cap, Color c, TMP_FontAsset f, Vector3 eye)
    {
        var t = new GameObject("Label " + text).transform; t.SetParent(parent, false); t.position = at; t.rotation = Quaternion.LookRotation(at - eye, Vector3.up);
        Text(t, text, Vector3.zero, cap, c, f, TextAlignmentOptions.Center, 0.1f);
    }

    /// <summary>Panels drawn after all others (a held panel is nearest the eye): lifted just before the shot.</summary>
    static readonly List<Transform> liftAfter = new List<Transform>();
    static void Lift()
    {
        foreach (var t in liftAfter)
        {
            if (t == null) continue;
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                var tmp = r.GetComponent<TextMeshPro>();
                if (tmp != null) { var m = tmp.fontMaterial; m.renderQueue = 3140; }
                else r.sharedMaterial.renderQueue += 100;
            }
        }
        liftAfter.Clear();
    }

    static TextMeshPro Text(Transform parent, string s, Vector3 local, float cap, Color c, TMP_FontAsset f, TextAlignmentOptions align, float width, float lineSpacing = 0, float tracking = 0)
    {
        var go = new GameObject("Text"); go.transform.SetParent(parent, false); go.transform.localPosition = local;
        var t = go.AddComponent<TextMeshPro>(); t.font = f; t.fontSharedMaterial = f.material; t.fontSize = cap / 0.0727f; t.color = c; t.alignment = align;
        t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Overflow; t.margin = Vector4.zero; t.lineSpacing = lineSpacing; t.characterSpacing = tracking;
        t.rectTransform.sizeDelta = new Vector2(width, cap * 2);
        bool left = align == TextAlignmentOptions.Left || align == TextAlignmentOptions.TopLeft, right = align == TextAlignmentOptions.Right || align == TextAlignmentOptions.TopRight;
        bool topA = align == TextAlignmentOptions.TopLeft || align == TextAlignmentOptions.TopRight || align == TextAlignmentOptions.Top;
        t.rectTransform.pivot = new Vector2(left ? 0 : right ? 1 : 0.5f, topA ? 1 : 0.5f);
        t.text = s; t.ForceMeshUpdate(true, true);
        var r = t.GetComponent<MeshRenderer>(); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        return t;
    }

    // ------------------------------------------------------------------------------------------------ materials

    static Material SoftMat(Color c, float emission = 0, Color? emissionColour = null, float spec = 0.22f, float gloss = 28, float rim = 0.3f, float alpha = 1, float alphaEdge = 1, int queue = 2000, CullMode cull = CullMode.Back)
    {
        var m = new Material(Shader.Find("Concept/Soft"));
        m.SetColor("_Color", c); m.SetColor("_EmissionColor", emissionColour ?? c); m.SetFloat("_Emission", emission);
        m.SetFloat("_Spec", spec); m.SetFloat("_Gloss", gloss); m.SetFloat("_Rim", rim); m.SetFloat("_Alpha", alpha); m.SetFloat("_AlphaEdge", alphaEdge);
        bool opaque = alpha >= 1 && alphaEdge >= 1;
        m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)(opaque ? BlendMode.Zero : BlendMode.OneMinusSrcAlpha));
        m.SetFloat("_SrcBlendA", (float)BlendMode.One); m.SetFloat("_DstBlendA", (float)(opaque ? BlendMode.Zero : BlendMode.OneMinusSrcAlpha));
        m.SetFloat("_ZWrite", opaque ? 1 : 0); m.SetFloat("_Cull", (float)cull); m.renderQueue = queue;
        return m;
    }

    static Material GlassMat(Vector2 half, float radius, float alphaC = 0.80f, float alphaR = 0.62f, float focus = 0, Color? accent = null, int queue = 3000, Color? tint = null, float glow = 0.05f)
    {
        var m = new Material(Shader.Find("Concept/Glass"));
        m.SetVector("_Half", half); m.SetFloat("_Radius", radius); m.SetColor("_Tint", tint ?? GlassTint); m.SetFloat("_AlphaCentre", alphaC); m.SetFloat("_AlphaRim", alphaR);
        m.SetFloat("_EdgeFade", Mathf.Min(0.035f, 0.4f * Mathf.Min(half.x, half.y))); m.SetFloat("_Glow", glow);
        m.SetFloat("_Focus", focus); m.SetColor("_Accent", accent ?? RimLight); m.renderQueue = queue;
        return m;
    }

    static Material LineMat(float widen, float glow, float halo, float coreAlpha, int queue)
    {
        var m = new Material(Shader.Find("Concept/Line"));
        m.SetFloat("_Widen", widen); m.SetFloat("_Glow", glow); m.SetFloat("_Halo", halo); m.SetFloat("_CoreAlpha", coreAlpha); m.renderQueue = queue;
        return m;
    }

    static Material UnlitMat(Color c, int queue, Texture tex = null, float radius = 0, bool disc = false)
    {
        var m = new Material(Shader.Find("Concept/Unlit")); m.SetColor("_Color", c); if (tex) m.SetTexture("_MainTex", tex); m.SetFloat("_Radius", radius); m.SetFloat("_Disc", disc ? 1 : 0); m.renderQueue = queue;
        return m;
    }

    // ------------------------------------------------------------------------------------------------ meshes

    static Mesh Build(string name, List<Vector3> v, List<Vector3> n, List<int> t, List<Vector2> uv = null, List<Color> col = null)
    {
        for (int i = 0; i + 2 < t.Count; i += 3)
        {
            var face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
            if (Vector3.Dot(face, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) < 0) { int k = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = k; }
        }
        var m = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        m.SetVertices(v); m.SetNormals(n); if (uv != null) m.SetUVs(0, uv);
        m.SetColors(col ?? Enumerable.Repeat(Color.white, v.Count).ToList()); m.SetTriangles(t, 0); m.RecalculateBounds();
        return m;
    }

    /// <summary>A surface of revolution about +Y from a (radius, y) profile, with smooth normals.</summary>
    static Mesh Lathe(IList<Vector2> prof, int sides, string name)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>(); int m = prof.Count;
        for (int j = 0; j < m; j++)
        {
            Vector2 tan = (prof[Mathf.Min(m - 1, j + 1)] - prof[Mathf.Max(0, j - 1)]).normalized, nn = new Vector2(tan.y, -tan.x);
            for (int s = 0; s <= sides; s++)
            {
                float a = s * 2 * Mathf.PI / sides, c = Mathf.Cos(a), sn = Mathf.Sin(a);
                v.Add(new Vector3(prof[j].x * c, prof[j].y, prof[j].x * sn)); n.Add(new Vector3(nn.x * c, nn.y, nn.x * sn).normalized);
            }
        }
        for (int j = 0; j < m - 1; j++) for (int s = 0; s < sides; s++) { int a = j * (sides + 1) + s, b = a + sides + 1; t.Add(a); t.Add(b); t.Add(a + 1); t.Add(a + 1); t.Add(b); t.Add(b + 1); }
        return Build(name, v, n, t);
    }

    /// <summary>A soft needle along +Y (unit length, pivot at the centre): rounded tail, slim shaft, a rounded head.</summary>
    static Mesh SoftNeedle()
    {
        var p = new List<Vector2>(); float r0 = 0.058f, r1 = 0.15f, y0 = -0.5f, yh = 0.08f, tr = 0.028f;
        for (int k = 0; k <= 6; k++) { float a = -Mathf.PI / 2 + k * (Mathf.PI / 2) / 6; p.Add(new Vector2(r0 * Mathf.Cos(a), y0 + r0 + r0 * Mathf.Sin(a))); }
        p.Add(new Vector2(r0, yh - 0.03f)); p.Add(new Vector2(r0 + 0.012f, yh - 0.006f)); p.Add(new Vector2(r1 - 0.016f, yh)); p.Add(new Vector2(r1, yh + 0.016f));
        for (int k = 1; k <= 8; k++) { float f = k / 8f; p.Add(new Vector2(Mathf.Lerp(r1, tr, f), Mathf.Lerp(yh + 0.016f, 0.5f - tr, f))); }
        for (int k = 1; k <= 4; k++) { float a = k * (Mathf.PI / 2) / 4; p.Add(new Vector2(tr * Mathf.Cos(a), 0.5f - tr + tr * Mathf.Sin(a))); }
        return Lathe(p, 18, "Soft needle");
    }

    /// <summary>A soft arrow along +Y from 0 to len: rounded tail, shaft of radius r, a head of radius hr and length hl.</summary>
    static Mesh Arrow(float len, float r, float hr, float hl)
    {
        var p = new List<Vector2>();
        for (int k = 0; k <= 5; k++) { float a = -Mathf.PI / 2 + k * (Mathf.PI / 2) / 5; p.Add(new Vector2(r * Mathf.Cos(a), r + r * Mathf.Sin(a))); }
        p.Add(new Vector2(r, len - hl - r)); p.Add(new Vector2(r * 1.4f, len - hl - r * 0.3f)); p.Add(new Vector2(hr * 0.92f, len - hl)); p.Add(new Vector2(hr, len - hl + hr * 0.12f));
        float tr = hr * 0.18f;
        for (int k = 1; k <= 6; k++) { float f = k / 6f; p.Add(new Vector2(Mathf.Lerp(hr, tr, f), Mathf.Lerp(len - hl + hr * 0.12f, len - tr, f))); }
        for (int k = 1; k <= 3; k++) { float a = k * (Mathf.PI / 2) / 3; p.Add(new Vector2(tr * Mathf.Cos(a), len - tr + tr * Mathf.Sin(a))); }
        return Lathe(p, 16, "Arrow");
    }

    /// <summary>A box with rounded edges (size, edge radius), by pushing a subdivided cube out to its rounded shape.</summary>
    static Mesh RoundedBox(Vector3 size, float r, int div)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>(); var h = size / 2; var inner = h - Vector3.one * r;
        for (int f = 0; f < 6; f++)
        {
            int a = f % 3; float sgn = f < 3 ? 1 : -1; int b = (a + 1) % 3, c = (a + 2) % 3; int start = v.Count;
            for (int i = 0; i <= div; i++)
                for (int j = 0; j <= div; j++)
                {
                    var q = Vector3.zero; q[a] = sgn * h[a]; q[b] = Mathf.Lerp(-h[b], h[b], i / (float)div); q[c] = Mathf.Lerp(-h[c], h[c], j / (float)div);
                    // Concentrate samples near the edges so the rounding is smooth.
                    float ub = Mathf.Lerp(-1, 1, i / (float)div), uc = Mathf.Lerp(-1, 1, j / (float)div);
                    q[b] = Mathf.Sign(ub) * Mathf.Lerp(0, h[b], Mathf.Pow(Mathf.Abs(ub), 0.35f)); q[c] = Mathf.Sign(uc) * Mathf.Lerp(0, h[c], Mathf.Pow(Mathf.Abs(uc), 0.35f));
                    var cl = new Vector3(Mathf.Clamp(q.x, -inner.x, inner.x), Mathf.Clamp(q.y, -inner.y, inner.y), Mathf.Clamp(q.z, -inner.z, inner.z));
                    var d = (q - cl); var dn = d.sqrMagnitude > 1e-12f ? d.normalized : Vector3.zero; if (dn == Vector3.zero) { dn[a] = sgn; }
                    v.Add(cl + dn * r); n.Add(dn);
                }
            for (int i = 0; i < div; i++) for (int j = 0; j < div; j++) { int k = start + i * (div + 1) + j; t.Add(k); t.Add(k + div + 1); t.Add(k + 1); t.Add(k + 1); t.Add(k + div + 1); t.Add(k + div + 2); }
        }
        return Build("Rounded box", v, n, t);
    }

    static Mesh GlassQuad(Vector2 half, float margin)
    {
        float w = half.x + margin, h = half.y + margin;
        var m = new Mesh { name = "Glass" };
        m.vertices = new[] { new Vector3(-w, -h, 0), new Vector3(w, -h, 0), new Vector3(w, h, 0), new Vector3(-w, h, 0) };
        m.uv = new[] { new Vector2(-w, -h), new Vector2(w, -h), new Vector2(w, h), new Vector2(-w, h) };
        m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back }; m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2 }; m.RecalculateBounds(); return m;
    }

    static Mesh Quad(float w, float h)
    {
        var m = new Mesh { name = "Quad" };
        m.vertices = new[] { new Vector3(-w / 2, -h / 2, 0), new Vector3(w / 2, -h / 2, 0), new Vector3(w / 2, h / 2, 0), new Vector3(-w / 2, h / 2, 0) };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) }; m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back }; m.triangles = new[] { 0, 2, 1, 0, 3, 2 }; m.RecalculateBounds(); return m;
    }

    static Mesh Disc(float r)
    {
        var m = Quad(2 * r, 2 * r); m.name = "Disc"; return m;
    }

    static Mesh Triangle(float s)
    {
        var m = new Mesh { name = "Play" };
        m.vertices = new[] { new Vector3(-0.45f * s, -0.55f * s, 0), new Vector3(-0.45f * s, 0.55f * s, 0), new Vector3(0.6f * s, 0, 0) };
        m.uv = new[] { new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f) }; m.colors = new[] { Color.white, Color.white, Color.white };
        m.normals = new[] { Vector3.back, Vector3.back, Vector3.back }; m.triangles = new[] { 0, 1, 2 }; m.RecalculateBounds(); return m;
    }

    /// <summary>The soft area between a trace and its baseline (vertex colour carries the tint and alpha).</summary>
    static Mesh AreaMesh(List<Vector3> pts, float baseY, Color c)
    {
        var v = new List<Vector3>(); var t = new List<int>(); var col = new List<Color>(); var uv = new List<Vector2>();
        for (int k = 0; k < pts.Count; k++)
        {
            v.Add(new Vector3(pts[k].x, baseY, pts[k].z + 0.0005f)); v.Add(pts[k] + new Vector3(0, 0, 0.0005f));
            col.Add(new Color(c.r, c.g, c.b, c.a * 0.4f)); col.Add(c); uv.Add(new Vector2(0.5f, 0.5f)); uv.Add(new Vector2(0.5f, 0.5f));
            if (k > 0) { int a = 2 * (k - 1); t.Add(a); t.Add(a + 1); t.Add(a + 2); t.Add(a + 2); t.Add(a + 1); t.Add(a + 3); }
        }
        var m = new Mesh { name = "Area" }; m.SetVertices(v); m.SetColors(col); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateBounds(); return m;
    }

    /// <summary>A gentle curve (quadratic, bulging up by lift) from a to b.</summary>
    static void Curve(LineBuilder lb, Vector3 a, Vector3 b, float lift, float w, Color c)
    {
        var mid = (a + b) / 2 + Vector3.up * lift; Vector3 prev = a;
        for (int k = 1; k <= 40; k++) { float t = k / 40f; var p = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * mid + t * t * b; lb.Segment(prev, p, w, c); prev = p; }
    }

    // ------------------------------------------------------------------------------------------------ render and composite

    static Color32[] photo;

    /// <summary>Renders the eye buffer (cleared to transparent) at 2x, composites it over the room photograph in linear light
    /// (out = rgb + room x (1 - a)), downsamples to 1920 x 1200 and writes the PNG.</summary>
    static void Shot(string name, Vector3 pos, float yaw, float pitch)
    {
        Lift();
        cam.transform.SetPositionAndRotation(pos, Quaternion.Euler(pitch, yaw, 0));
        int w = W * SS, h = H * SS;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.aspect = (float)W / H; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = prev; cam.targetTexture = null; rt.Release();
        var px = tex.GetPixels32(); UnityEngine.Object.DestroyImmediate(tex);
        if (photo == null)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!t.LoadImage(File.ReadAllBytes(Room)) || t.width != W || t.height != H) throw new Exception("Room photo missing or not 1920 x 1200: " + Room);
            photo = t.GetPixels32(); UnityEngine.Object.DestroyImmediate(t);
        }
        var lut = new float[256]; for (int i = 0; i < 256; i++) { float c = i / 255f; lut[i] = c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f); }
        var outp = new Color32[W * H]; var eye = new Color32[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                var q = photo[y * W + x]; float br = lut[q.r], bg = lut[q.g], bb = lut[q.b];
                float r = 0, g = 0, b = 0, ea = 0, er = 0, eg = 0, eb = 0;
                for (int dy = 0; dy < SS; dy++)
                    for (int dx = 0; dx < SS; dx++)
                    {
                        var c = px[(y * SS + dy) * w + x * SS + dx]; float a = c.a / 255f;
                        r += lut[c.r] + br * (1 - a); g += lut[c.g] + bg * (1 - a); b += lut[c.b] + bb * (1 - a);
                        er += lut[c.r]; eg += lut[c.g]; eb += lut[c.b]; ea += a;
                    }
                float k = 1f / (SS * SS);
                outp[y * W + x] = new Color32(Enc(r * k), Enc(g * k), Enc(b * k), 255);
                eye[y * W + x] = new Color32(Enc(er * k), Enc(eg * k), Enc(eb * k), (byte)Mathf.RoundToInt(255 * Mathf.Clamp01(ea * k)));
            }
        var o = new Texture2D(W, H, TextureFormat.RGBA32, false); o.SetPixels32(outp); o.Apply();
        File.WriteAllBytes(Path.Combine(Out, name + ".png"), o.EncodeToPNG());
        o.SetPixels32(eye); o.Apply(); File.WriteAllBytes(Path.Combine(Out, name + "-eye-rgba.png"), o.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(o);
        Debug.Log("DESIGN_CONCEPTS wrote " + name);
    }

    static byte Enc(float lin)
    {
        lin = Mathf.Clamp01(lin); float s = lin <= 0.0031308f ? lin * 12.92f : 1.055f * Mathf.Pow(lin, 1 / 2.4f) - 0.055f;
        return (byte)Mathf.RoundToInt(255 * s);
    }
}
