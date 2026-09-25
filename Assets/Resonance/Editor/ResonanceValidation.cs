using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nebulytic.Resonance;
using Nebulytic.Resonance.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Headless runtime validation and review renders (SPEC 11). Run with graphics under xvfb:
///   Unity -batchmode -projectPath . -executeMethod ResonanceValidation.Run
/// Writes validation/runtime-report.json, validation/renders/cues/*.png and validation/render-manifest.json.
/// </summary>
public static class ResonanceValidation
{
    [Serializable] class Report { public string unity, renderer, version; public bool passed, physicalQuestTested = false; public List<string> checks = new List<string>(), failures = new List<string>(), errors = new List<string>(), environmentErrors = new List<string>(), shots = new List<string>(); }
    [Serializable] class Shot { public string file, step, title, cue, text; public string[] refs; public double t, tau; public bool lab; }
    [Serializable] class Manifest { public List<Shot> shots = new List<Shot>(); }

    static Report report; static App app; static Camera camera; static readonly Manifest manifest = new Manifest();
    const int ShotW = 1920, ShotH = 1200;
    // Review camera: a Quest-like wide view (76 degrees vertical, about 102 horizontal), level gaze slightly lowered.
    const float ReviewFov = 76, ReviewPitch = 8;

    static void Check(bool ok, string name) { if (ok) report.checks.Add(name); else { report.failures.Add(name); Debug.LogError("CHECK FAILED: " + name); } }

    static async Task Until(Func<bool> pred, double minutes = 5)
    {
        var end = DateTime.UtcNow.AddMinutes(minutes);
        while (!pred()) { if (DateTime.UtcNow > end) throw new TimeoutException("Timed out waiting"); await Task.Delay(40); }
    }

    static async Task WaitFrames(int n)
    {
        for (int i = 0; i < n; i++) { int f = Time.frameCount; await Until(() => Time.frameCount > f, 1); }
    }

    // Fast layout gate for UI geometry changes; the full validation remains authoritative.
    public static async void RunLayout()
    {
        Directory.CreateDirectory("validation/playback-094");
        report = new Report { version = ResonanceBuild.Version };
        int code=1;
        try {
            EditorSceneManager.OpenScene(ResonanceBuild.ScenePath);
            EditorSettings.enterPlayModeOptionsEnabled=true;
            EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.isPlaying=true;
            await Until(()=>UnityEngine.Object.FindAnyObjectByType<App>()?.Ready==true);
            app=UnityEngine.Object.FindAnyObjectByType<App>();camera=app.HeadCamera;
            await Until(()=>app.Sim.State!=null && app.World.gameObject.activeSelf && app.Lesson.Steps.Count>0);
            app.FixedStep=true;app.Controls.enabled=false;app.GoToStep(0,false);
            camera.fieldOfView=ReviewFov;camera.transform.rotation=Quaternion.Euler(ReviewPitch,0,0);
            await WaitFrames(3);LayoutChecks();
            report.passed=report.failures.Count==0;code=report.passed?0:1;
        } catch(Exception e) {report.failures.Add(e.ToString());}
        finally {
            File.WriteAllText("validation/playback-094/layout-report.json",JsonUtility.ToJson(report,true));
            EditorApplication.isPlaying=false;EditorApplication.Exit(code);
        }
    }

    public static async void Run()
    {
        Directory.CreateDirectory("validation/renders/cues");
        foreach (var f in Directory.GetFiles("validation/renders/cues", "*.png")) File.Delete(f);
        if (Directory.Exists("validation/renders/grey")) foreach (var f in Directory.GetFiles("validation/renders/grey", "*.png")) File.Delete(f);
        report = new Report { unity = Application.unityVersion, renderer = SystemInfo.graphicsDeviceName, version = ResonanceBuild.Version };
        int exit = 1;
        try
        {
            ResonanceBuild.Configure();
            EditorSceneManager.OpenScene(ResonanceBuild.ScenePath);
            EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            var ready = new TaskCompletionSource<bool>();
            void Changed(PlayModeStateChange s) { if (s == PlayModeStateChange.EnteredPlayMode) { EditorApplication.playModeStateChanged -= Changed; ready.SetResult(true); } }
            EditorApplication.playModeStateChanged += Changed; EditorApplication.isPlaying = true; await ready.Task;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                {
                    if (stack.Contains("UnityEditor.Search") || message.StartsWith("CHECK FAILED")) { if (!message.StartsWith("CHECK FAILED")) report.environmentErrors.Add(message + "\n" + stack); }
                    else report.errors.Add(message + "\n" + stack);
                }
            };
            await Until(() => UnityEngine.Object.FindAnyObjectByType<App>()?.Ready == true);
            app = UnityEngine.Object.FindAnyObjectByType<App>(); camera = app.HeadCamera;
            await Until(() => app.Sim.State != null && app.Lesson.Steps.Count > 0 && app.World.gameObject.activeSelf);
            app.FixedStep = true; app.instantEmphasis = true;
            camera.fieldOfView = ReviewFov; camera.transform.rotation = Quaternion.Euler(ReviewPitch, 0, 0);
            app.GoToStep(0, false); await WaitFrames(3);
            await Capture("00-initial-review", null, null);
            await At("2.18",0.9); Check(app.Cube.ConventionalGrid && !app.Scanner.Root.gameObject.activeSelf,"Ideal example uses regular grid mesh and separates anatomical scanner"); await Capture("F01-uniform-grid",null,null);
            await At("2.22",0.7); await Capture("F02-T2",null,null);
            await At("2.30",0.9); await Capture("F03-Gy",null,null);
            await At("2.34",1); await Capture("F04-two-positions",null,null);
            app.GoToStep(0,false);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await Until(() => app.Sim.State.AcquisitionFinished || app.Sim.State.Error != null, 25);
            Check(app.Sim.State.Error == null, "Acquisition of every slice completed without error (" + sw.Elapsed.TotalSeconds.ToString("F1") + " s)");
            BasicChecks();
            app.GoToStep(0, false); // the app starts playing by itself; the overview is the moment before
            await WaitFrames(3);
            await Capture("00-overview", null, null);
            LayoutChecks();
            await RenderLesson();
            await LessonPhysicsChecks();
            await InteractionChecks();
            await FrameCostReport();
            PaletteChecks();
            report.passed = report.failures.Count == 0 && report.errors.Count == 0;
            exit = report.passed ? 0 : 1;
        }
        catch (Exception e) { report.failures.Add("Harness exception: " + e); Debug.LogException(e); }
        finally
        {
            File.WriteAllText("validation/runtime-report.json", JsonUtility.ToJson(report, true));
            File.WriteAllText("validation/render-manifest.json", JsonUtility.ToJson(manifest, true));
            Debug.Log($"RESONANCE_VALIDATION {(report.passed ? "PASSED" : "FAILED")} checks={report.checks.Count} failures={report.failures.Count} errors={report.errors.Count}");
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(exit);
        }
    }

    static void BasicChecks()
    {
        var s = app.Sim.State;
        Check(s.Cube.N == 288 && s.Cube.Nx == 6 && s.Cube.Ny == 6 && s.Cube.Nz == 8, "Ordered 6 x 6 x 8 tissue lattice");
        s.Specimen.Bounds(out var handLo, out var handHi);
        Check(s.Slices.Min(q=>q.Z)-s.P.SliceThickness/2<=handLo.Z && s.Slices.Max(q=>q.Z)+s.P.SliceThickness/2>=handHi.Z, "Slice stack covers complete hand bounds");
        Check(app.Lesson.Steps.Count == 10, "Ten lesson steps");
        Check(app.Lesson.Steps.Sum(x => x.Cues.Count) == 119, "119 synchronized one-sentence cues");
        // The introduction (0.8.4): the scanner's parts and every view are named, with the pointer on them, before the physics.
        var intro = app.Lesson.Steps.Take(2).SelectMany(x => x.Cues).SelectMany(c => c.Data.refs).ToList();
        string[] mustIntro = { "coil.b0", "field.b", "axis.x", "axis.y", "axis.z", "gradients", "coil.rf", "rx", "hand", "region", "cube", "spins.all", "closeup", "closeup.rot", "sequence", "signal.trace", "kspace" };
        var missing = mustIntro.Where(r => !intro.Contains(r)).ToArray();
        Check(missing.Length == 0 && app.Lesson.Steps[0].Cues.All(c => c.T1 == 0) && app.Lesson.Steps[1].Cues.All(c => c.T1 == 0),
            "INTRO the first two steps introduce the scanner's parts and every view at equilibrium" + (missing.Length > 0 ? " (missing " + string.Join(", ", missing) + ")" : ""));
        // Uniform magnification: the block's display and the scanner's outline have the same proportions as the physical block.
        var ds = CubeView.DisplaySize; var gs = Layouts.GridSize;
        Check(Math.Abs(ds.x / gs.X - ds.z / gs.Z) < 1e-3 * ds.x / gs.X && Math.Abs(ds.y / gs.Y - ds.z / gs.Z) < 1e-3 * ds.x / gs.X, $"BLOCK one uniform magnification ({ds.x / gs.X:F1} x on every axis)");
        // Q1: the level Android actually uses (and every other): 4x MSAA, no real-time shadows, no soft particles (whose depth
        // texture would redraw the scene).
        var qso = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        var levels = qso.FindProperty("m_QualitySettings"); int badLevels = 0;
        for (int q = 0; q < levels.arraySize; q++)
        {
            var l = levels.GetArrayElementAtIndex(q);
            if (l.FindPropertyRelative("antiAliasing").intValue != 4 || l.FindPropertyRelative("shadows").intValue != 0 || l.FindPropertyRelative("softParticles").boolValue) badLevels++;
        }
        Check(badLevels == 0 && QualitySettings.names.Length == levels.arraySize, $"Q1 every quality level, Android's included: 4x MSAA, no shadows, no soft particles ({badLevels} of {levels.arraySize} differ)");
        Check(UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include).Length == 0 && UnityEngine.Object.FindObjectsByType<TrailRenderer>(FindObjectsInactive.Include).Length == 0, "No line or trail renderers in the scene");
    }

    // ------------------------------------------------------------------------------------------------ layout (G1, G2, G4)

    /// <summary>Screen rectangle of everything drawn under root, from its projected mesh vertices (bounding boxes of
    /// rotated coils overstate the silhouette).</summary>
    static Rect ScreenRect(Transform root)
    {
        float x0 = 1, y0 = 1, x1 = 0, y1 = 0;
        void Add(Vector3 w) { var v = camera.WorldToViewportPoint(w); x0 = Mathf.Min(x0, v.x); y0 = Mathf.Min(y0, v.y); x1 = Mathf.Max(x1, v.x); y1 = Mathf.Max(y1, v.y); }
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            var mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh != null && mesh.isReadable)
            {
                var m = r.transform.localToWorldMatrix; var verts = mesh.vertices; int step = Math.Max(1, verts.Length / 6000);
                for (int i = 0; i < verts.Length; i += step) Add(m.MultiplyPoint3x4(verts[i]));
                continue;
            }
            var b = r.bounds;
            for (int k = 0; k < 8; k++) Add(b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
        }
        return Rect.MinMaxRect(x0, y0, x1, y1);
    }

    static void LayoutChecks()
    {
        camera.aspect = (float)ShotW / ShotH;
        var items = new[] { ("scanner", app.Scanner.Root), ("data", app.Plots.Data), ("proton grid", app.Cube.Root), ("signal", app.Plots.Signal), ("sequence", app.Plots.Sequence), ("close-up", app.CloseUp.Root), ("strip", app.Strip.Root) };
        var rects = new List<(string, Rect)>();
        foreach (var (name, t) in items)
        {
            var r = name == "proton grid" ? CubeRect() : ScreenRect(t);
            rects.Add((name, r));
            Check(r.xMin > 0.01f && r.yMin > 0.01f && r.xMax < 0.99f && r.yMax < 0.99f, $"G1 {name} entirely in view from the default head pose ({r.xMin:F2}-{r.xMax:F2}, {r.yMin:F2}-{r.yMax:F2})");
        }
        for (int i = 0; i < rects.Count; i++)
            for (int j = i + 1; j < rects.Count; j++)
            {
                var a = rects[i].Item2; var b = rects[j].Item2;
                float ox = Mathf.Max(0, Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin)), oy = Mathf.Max(0, Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin));
                float frac = ox * oy / Mathf.Min(a.width * a.height, b.width * b.height);
                Check(frac <= 0.01f, $"G2 {rects[i].Item1} and {rects[j].Item1} do not overlap on screen ({frac * 100:F1} %)");
            }
        var toStrip = (app.Strip.Root.position - app.Eye).normalized;
        Check(Vector3.Angle(app.Strip.Root.forward, toStrip) < 3, $"G4 strip faces the eye ({Vector3.Angle(app.Strip.Root.forward, toStrip):F1} deg)");
        var fingers = Vector3.ProjectOnPlane(app.Scanner.Physics.TransformDirection(Frames.ToS(0, 0, -1)), Vector3.up).normalized;
        var left = Vector3.ProjectOnPlane(-camera.transform.right, Vector3.up).normalized;
        Check(Vector3.Angle(fingers, left) <= 42, $"G4 fingertips (-z) point to the viewer's left ({Vector3.Angle(fingers, left):F0} deg from left)");
        var b0 = app.Scanner.Physics.TransformDirection(Frames.ToS(0, 0, 1)); var cubeZ = app.Cube.Root.TransformDirection(Frames.ToS(0, 0, 1));
        Check(Vector3.Angle(b0, cubeZ) < 0.1f, "G8 cube axes parallel to the scanner's (linked orientation)");
        Check(Vector3.Dot(Vector3.Cross(Frames.ToS(0, 0, 1), Frames.ToS(1, 0, 0)), Vector3.up) < 0, "G4 handedness: physical z x x = +y maps to Unity left-handed cross = -up");
    }

    static Rect CubeRect()
    {
        float x0 = 1, y0 = 1, x1 = 0, y1 = 0; Vector3 h = CubeView.DisplaySize / 2;
        for (int k = 0; k < 8; k++)
        {
            var c = app.Cube.Root.TransformPoint(new Vector3((k & 1) == 0 ? -h.x : h.x, (k & 2) == 0 ? -h.y : h.y, (k & 4) == 0 ? -h.z : h.z));
            var v = camera.WorldToViewportPoint(c); x0 = Mathf.Min(x0, v.x); y0 = Mathf.Min(y0, v.y); x1 = Mathf.Max(x1, v.x); y1 = Mathf.Max(y1, v.y);
        }
        return Rect.MinMaxRect(x0, y0, x1, y1);
    }

    // ------------------------------------------------------------------------------------------------ lesson physics

    static async Task At(string cueId, double fraction = 0.6)
    {
        for (int si = 0; si < app.Lesson.Steps.Count; si++)
            foreach (var c in app.Lesson.Steps[si].Cues)
                if (c.Data.id == cueId)
                {
                    double body = Math.Max(0.1, c.Len - c.Lead - Lesson.CueGap);
                    app.Seek(si, c.Start + c.Lead + fraction * body, false); await WaitFrames(2); return;
                }
        throw new Exception("No cue " + cueId);
    }

    static double Tip(Func<int, bool> sel)
    {
        var s = app.Sim.State; var e = s.CubeEval; double a = 0, b = 0;
        for (int i = 0; i < s.Cube.N; i++) if (sel(i) && s.Cube.Pd[i] > 0) { a += Math.Sqrt(e.X[i] * e.X[i] + e.Y[i] * e.Y[i]); b += s.Cube.Pd[i]; }
        return b > 0 ? a / b : 0;
    }

    static double Signal() => app.Sim.SignalOn ? Math.Sqrt(app.Sim.SignalRe * app.Sim.SignalRe + app.Sim.SignalIm * app.Sim.SignalIm) : 0;

    /// <summary>Renders the current view into a readable texture immediately (no frame wait, so per-frame code cannot
    /// undo a renderer toggle made just before).</summary>
    static Texture2D Grab()
    {
        var rt = new RenderTexture(ShotW, ShotH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var old = camera.targetTexture; float aspect = camera.aspect;
        camera.targetTexture = rt; camera.aspect = (float)ShotW / ShotH;
        SubmitInstanced(); camera.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(ShotW, ShotH, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, ShotW, ShotH), 0, 0); tex.Apply();
        RenderTexture.active = prev; camera.targetTexture = old; camera.aspect = aspect;
        rt.Release(); UnityEngine.Object.Destroy(rt);
        return tex;
    }

    /// <summary>Mean luminance in linear light (so added light compares fairly over any backdrop) in a 9 x 9 window.</summary>
    static float Lum(Texture2D tex, Vector3 world)
    {
        float aspect = camera.aspect; camera.aspect = (float)ShotW / ShotH;
        var v = camera.WorldToViewportPoint(world); camera.aspect = aspect;
        int cx = Mathf.RoundToInt(v.x * ShotW), cy = Mathf.RoundToInt(v.y * ShotH); float sum = 0; int n = 0;
        for (int dy = -4; dy <= 4; dy++) for (int dx = -4; dx <= 4; dx++)
            {
                int x = cx + dx, y = cy + dy; if (x < 0 || y < 0 || x >= ShotW || y >= ShotH) continue;
                var c = tex.GetPixel(x, y); sum += 0.2126f * ToLinear(c.r) + 0.7152f * ToLinear(c.g) + 0.0722f * ToLinear(c.b); n++;
            }
        return n > 0 ? sum / n : 0;
    }

    /// <summary>Instanced draws (protons, B1 arrows, the close-up's tip path) for a manual Camera.Render.</summary>
    static void SubmitInstanced() { app.Cube.Submit(camera); app.Scanner.Submit(camera); app.CloseUp.Submit(camera); app.Spatial.Submit(camera); }

    /// <summary>A renderer's own light at two points: a render with it minus one without it (F1-F3). Flow markers paused.</summary>
    static (float a, float b) HazeAt(Renderer volume, Vector3 wa, Vector3 wb)
    {
        var mat = volume.sharedMaterial; float dash = mat.HasProperty("_Dash") ? mat.GetFloat("_Dash") : 0; if (dash > 0) mat.SetFloat("_Dash", 0);
        var on = Grab(); volume.enabled = false; var off = Grab(); volume.enabled = true;
        if (dash > 0) mat.SetFloat("_Dash", dash);
        float a = Lum(on, wa) - Lum(off, wa), b = Lum(on, wb) - Lum(off, wb);
        UnityEngine.Object.Destroy(on); UnityEngine.Object.Destroy(off);
        return (a, b);
    }

    static async Task LessonPhysicsChecks()
    {
        var s = app.Sim.State; var cube = s.Cube; double bw = s.P.BandwidthEff;
        Func<int, bool> water = i => cube.Pd[i] > 0 && cube.R2[i] < 1 / 0.005 && cube.Shift[i] == 0;
        await At("1.2"); Check(Math.Abs(app.Sim.T) < 1e-12 && !app.Sim.RfOn && app.Sim.Iz == 0, "1.2 magnet only: no gradient or RF current at equilibrium");
        await At("3.1"); Check(!app.Sim.RfOn, "3.1 RF still off while the Larmor frequency is introduced");
        await At("3.4"); Check(app.Lab && app.Sim.RfOn, "3.4 lab frame while the RF field turns");
        await At("3.8"); Check(Tip(water) > 0.9, $"3.8 spins lie across the field after the hard pulse (tip {Tip(water):F3})");
        await At("4.6", 0.9); double s46 = Signal(); Check(s46 > 0.5, $"4.6 the receiver arrow exists and is long ({s46:F3})");
        await At("4.10", 1.0); Check(Signal() < 0.5 * s46, $"4.10 the whole-hand signal shrinks first ({Signal():F3} vs {s46:F3})");
        await At("5.8"); double s56 = Signal(); await At("5.10"); double s58 = Signal();
        Check(s56 < 0.15 * s58 && s58 > 0.3, $"5.8/5.10 twisted spins cancel, the echo returns ({s56:F3} then {s58:F3})");
        // F1-F3: "if there is a gradient, show me". The field lines' own light is measured on the lines through (0, +/-70 mm)
        // at both ends of each ramp.
        var haze = app.Scanner.Root.GetComponentsInChildren<Renderer>(true).First(r => r.name == "B0 field lines");
        Vector3 P(double x, double y, double z) => app.Scanner.Physics.TransformPoint(Frames.ToS(x, y, z));
        await At("5.4", 0.9);
        var (zp, zm) = HazeAt(haze, P(0, 0.07, 0.08), P(0, 0.07, -0.08));
        Check(zp > 3 * zm && zp > 0.005f, $"F1 Gz: the field lines are brighter toward +z ({zp:F3} vs {zm:F3} at z = +/-80 mm)");
        await At("5.5", 0.6); GradientArrowCheck("5.5", 2);
        await At("5.6", 0.9);
        // Phase colours twist along the gradient (item 6): after the Gz lobe the protons' hues turn about a turn along z.
        {
            // The hue each proton is drawn with (its phase), untwisted by k turns per metre along z: the best k is the twist.
            var e0 = s.CubeEval; double bestK = 0, bestC = 0, raw = 0;
            for (double k = -200; k <= 200; k += 1)
            {
                double re = 0, im = 0, w = 0;
                for (int i = 0; i < cube.N; i++)
                {
                    if (!water(i)) continue; double mm = Math.Sqrt(e0.X[i] * e0.X[i] + e0.Y[i] * e0.Y[i]); if (mm < 1e-6) continue;
                    double a = app.Cube.PhaseOf(i, out _) + 2 * Math.PI * k * cube.Pos[i].Z; re += mm * Math.Cos(a); im += mm * Math.Sin(a); w += mm;
                }
                double coh = w > 0 ? Math.Sqrt(re * re + im * im) / w : 0; if (k == 0) raw = coh;
                if (coh > bestC) { bestC = coh; bestK = k; }
            }
            double turns = Math.Abs(bestK) * Layouts.BlockZ;
            Check(bestC > 0.8 && raw < 0.6 && turns > 0.5 && turns < 1.6, $"COLOUR 5.6 the protons' colours twist along z: {turns:F2} turns across the block (coherence {bestC:F2} untwisted, {raw:F2} as drawn)");
        }
        await At("6.6", 1.0);
        // The grid's layers: two inside the slab (|z| = 1.5 mm, 0.3 of the bandwidth from its centre) and two outside (4.5 mm).
        Func<int, bool> core = i => water(i) && Math.Abs(s.CubeDfSlice[i]) < 0.35 * bw, far = i => water(i) && Math.Abs(s.CubeDfSlice[i]) > 0.85 * bw;
        int nCore = Enumerable.Range(0, cube.N).Count(core), nFar = Enumerable.Range(0, cube.N).Count(far);
        Check(nCore > 0 && nFar > 0 && Tip(core) > 0.7 && Tip(far) < 0.05, $"6.6 only the band tips (core {Tip(core):F3} over {nCore} protons, far {Tip(far):F4} over {nFar})");
        await At("6.10", 1.0);
        double sx = 0, sy = 0, m = 0; var e = s.CubeEval;
        for (int i = 0; i < cube.N; i++) if (water(i) && Math.Abs(s.CubeDfSlice[i]) <= bw / 2) { sx += e.X[i]; sy += e.Y[i]; m += Math.Sqrt(e.X[i] * e.X[i] + e.Y[i] * e.Y[i]); }
        Check(m > 0 && Math.Sqrt(sx * sx + sy * sy) / m > 0.95, $"6.10 the rephaser brings the band back in line (coherence {Math.Sqrt(sx * sx + sy * sy) / Math.Max(1e-9, m):F3})");
        await At("7.4", 0.9); GradientArrowCheck("7.4", 0);
        await At("7.6", 1.0); Check(Signal() > 0.5, $"7.6 the gradient echo peaks at the readout centre ({Signal():F3})");
        await At("7.9"); var acq = s.Slices[0]; Check(acq.RowTime[s.P.Matrix / 2] <= app.Sim.T, "7.9 the centre k-space row is measured");
        await At("8.3", 0.9); GradientArrowCheck("8.3", 1);
        var (yp, ym) = HazeAt(haze, P(0, 0.07, 0.03), P(0, -0.07, 0.03));
        Check(app.Sim.Iy > 0 && yp > 3 * ym && yp > 0.005f, $"F3 Gy: the field lines are brighter toward +y ({yp:F3} vs {ym:F3} at y = +/-70 mm)");
        await At("9.7", 1.0); int rows = 0; for (int p = 0; p < s.P.Matrix; p++) if (acq.RowTime[p] <= app.Sim.T) rows++;
        Check(rows == s.P.Matrix, $"9.7 every k-space row measured by the end of the image step ({rows})");
        await At("10.6", 1.0); int slices = 0; foreach (var a in s.Slices) if (a.RowTime[s.P.Matrix / 2] <= app.Sim.T) slices++;
        Check(slices == s.Slices.Length, $"10.x every slab acquired ({slices} of {s.Slices.Length})");
        await Capture("S10-complete-hand", null, null);
        // Every cue's referents are lit and everything else sits at context level (instant emphasis in the harness).
        // F4: every conductor of every coil stays drawn (enabled, active, above its brightness floor) at every cue.
        var conductors = app.Scanner.Root.GetComponentsInChildren<Renderer>(true).Where(r => r.sharedMaterial != null && r.sharedMaterial.shader.name == "Resonance/Conductor").ToArray();
        var mpb = new MaterialPropertyBlock(); int hidden = 0; string firstHidden = null;
        int bad = 0; string first = null;
        for (int si = 0; si < app.Lesson.Steps.Count; si++)
            foreach (var c in app.Lesson.Steps[si].Cues)
            {
                app.Seek(si, c.Start + c.Lead + 0.3, false); await WaitFrames(1);
                foreach (var el in app.Attention.Elements)
                {
                    bool hit = c.Data.refs.Any(r => el.Matches(r));
                    if (hit ? el.E < 0.95f : el.E > 0.05f) { bad++; first ??= $"{c.Data.id}:{el.Key}={el.E:F2}"; }
                }
                if (!app.Demonstrating) foreach (var r in conductors)
                {
                    r.GetPropertyBlock(mpb);
                    if (!r.enabled || !r.gameObject.activeInHierarchy || mpb.GetFloat("_Body") < 0.04f) { hidden++; firstHidden ??= c.Data.id + ":" + r.name; }
                }
            }
        Check(bad == 0, "C2 every cue lights exactly its referents" + (first != null ? " (first mismatch " + first + ")" : ""));
        Check(conductors.Length >= 5 && hidden == 0, $"F4 every coil stays visible in scanner lesson cues ({conductors.Length} conductor parts)" + (firstHidden != null ? " (first hidden " + firstHidden + ")" : ""));
    }

    /// <summary>
    /// F2 (0.8.4, item 6): while a gradient runs, the block shows an arrow along that gradient's axis, in the axis colour,
    /// pointing toward the stronger field, with the frequency offset at both ends.
    /// </summary>
    static void GradientArrowCheck(string cue, int axis)
    {
        var sim = app.Sim; double g = axis == 0 ? sim.Ix * Scanner.EtaX : axis == 1 ? sim.Iy * Scanner.EtaY : sim.Iz * Scanner.EtaZ;
        var rod = app.Cube.GradientRod; bool shown = rod != null && rod.gameObject.activeInHierarchy && app.Cube.GradientAxis == axis;
        Vector3 physical = app.Cube.Root.TransformDirection(Frames.ToS(axis == 0 ? 1 : 0, axis == 1 ? 1 : 0, axis == 2 ? 1 : 0)) * Math.Sign(g);
        float angle = shown ? Vector3.Angle(rod.transform.forward, physical) : 180;
        var col = shown ? rod.sharedMaterial.GetColor("_Color") : Color.black;
        Check(shown && angle < 2 && col == Look.Axis(axis), $"F2 {cue}: the block's gradient arrow runs along {"xyz"[axis]} toward the stronger field in the axis colour ({(shown ? angle.ToString("F1") + " deg" : "not shown")})");
    }

    // ------------------------------------------------------------------------------------------------ interaction

    static async Task InteractionChecks()
    {
        // Stillness: no item moves while the lesson plays without input.
        app.GoToStep(5, true); await WaitFrames(2);
        var before = new[] { app.Scanner.Root, app.Cube.Root, app.Strip.Root }.Select(t => (t.position, t.rotation)).ToArray();
        await WaitFrames(120);
        var after = new[] { app.Scanner.Root, app.Cube.Root, app.Strip.Root }.Select(t => (t.position, t.rotation)).ToArray();
        bool still = true; for (int i = 0; i < 3; i++) still &= Vector3.Distance(before[i].position, after[i].position) < 1e-4f && Quaternion.Angle(before[i].rotation, after[i].rotation) < 0.01f;
        Check(still, "G6 no item moves or rotates during 120 frames of playback without input");
        // Pause freezes lesson time and physical time; resume continues.
        app.TogglePlay(); await WaitFrames(2); double tau = app.Tau, t = app.T; await WaitFrames(60);
        Check(!app.Playing && app.Tau == tau && app.T == t, "K pause freezes lesson and physical time");
        app.TogglePlay(); await WaitFrames(10); Check(app.Playing && app.Tau > tau, "K resume continues from the same place");
        // Navigation.
        bool muteBefore = app.Muted; double muteTau = app.Tau, muteT = app.T; bool mutePlaying = app.Playing;
        app.Action("mute");
        Check(app.Muted != muteBefore && app.Tau == muteTau && app.T == muteT && app.Playing == mutePlaying, "K mute toggles audio without seeking or pausing the simulation");
        app.Action("mute");
        app.GoToStep(9, false); app.Next(); Check(app.StepIndex == 9, "K next at the last step stays there");
        app.GoToStep(0, false); app.Previous(); Check(app.StepIndex == 0 && app.Playing && app.Tau == 0, "K previous at the first step restarts it");
        app.GoToStep(3, false); app.Next(); Check(app.StepIndex == 4 && app.Playing, "K next goes to the following step and plays");
        app.Previous(); Check(app.StepIndex == 3, "K previous goes back one step");
        app.Pause();
        // Linked yaw: turning one item turns both physics items by the same angle about their own anchors.
        var freeCube = app.Cube.Root.rotation; app.YawShared(20); await WaitFrames(1);
        Check(Quaternion.Angle(freeCube,app.Cube.Root.rotation)<0.05f,"Independent tissue rotation stays fixed when scanner turns");
        app.YawShared(-20); app.Action("sync"); await WaitFrames(1);
        Check(app.SyncTissue && Quaternion.Angle(app.Cube.Root.rotation,app.Scanner.Root.rotation)<0.05f,"Sync button aligns tissue to scanner");
        var q0 = app.Scanner.Root.rotation; var c0 = app.Cube.Root.rotation; var p0 = app.Cube.Root.position;
        app.YawShared(40); await WaitFrames(1);
        Check(Mathf.Abs(Quaternion.Angle(q0, app.Scanner.Root.rotation) - 40) < 0.1f && Mathf.Abs(Quaternion.Angle(c0, app.Cube.Root.rotation) - 40) < 0.1f && Vector3.Distance(p0, app.Cube.Root.position) < 1e-5f, "G8 linked yaw turns scanner and cube together");
        app.YawShared(-40);
        // Free rotation (0.8.4, item 9): any 3-D turn of one physics item turns both, and the block's outline in the scanner
        // keeps the block's orientation.
        var shared0 = app.SharedRotation; app.RotateShared(Quaternion.Euler(35, -20, 25)); await WaitFrames(1);
        var outline = app.Scanner.Root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Block outline");
        float dScan = Quaternion.Angle(app.Scanner.Root.rotation, app.Cube.Root.rotation), dOutline = Quaternion.Angle(outline.rotation, app.Cube.Root.rotation);
        Check(dScan < 0.05f && dOutline < 0.05f && Quaternion.Angle(shared0, app.SharedRotation) > 30, $"ROT free 3-axis turn: scanner, block and the block's outline stay parallel ({dScan:F2}, {dOutline:F2} deg)");
        app.SetShared(shared0); await WaitFrames(1);
        // Recenter from a different head pose reproduces the slots relative to that pose.
        var home = (camera.transform.position, camera.transform.rotation);
        camera.transform.SetPositionAndRotation(new Vector3(2.3f, 1.2f, -1.7f), Quaternion.Euler(0, 137, 0));
        app.Recenter(); await WaitFrames(1);
        var expect = camera.transform.position + Quaternion.Euler(0, 137, 0) * (Look.CubeDist * new Vector3(Mathf.Cos(Look.CubeEl * Mathf.Deg2Rad) * Mathf.Sin(Look.CubeAz * Mathf.Deg2Rad), Mathf.Sin(Look.CubeEl * Mathf.Deg2Rad), Mathf.Cos(Look.CubeEl * Mathf.Deg2Rad) * Mathf.Cos(Look.CubeAz * Mathf.Deg2Rad)));
        Check(Vector3.Distance(expect, app.Cube.Root.position) < 1e-3f, "G7 recenter lays the dashboard out from the current head pose");
        camera.transform.SetPositionAndRotation(home.position, home.rotation); app.Recenter(); await WaitFrames(1);
        // Voice change keeps the place: the current cue restarts from its start.
        await At("7.6"); int step = app.StepIndex; var cueId = app.Cue.Data.id;
        app.Action("voice"); await WaitFrames(2);
        Check(app.VoiceName == "bm_george" && app.StepIndex == step && app.Cue.Data.id == cueId && Math.Abs(app.Tau - app.Cue.Start) < 1e-9, "K voice change restarts the same cue with the other narrator");
        app.Action("voice"); await WaitFrames(2);
        // Tissue drives the spins: moving the region to another place changes the needles.
        var s = app.Sim.State; var cls0 = s.Cube.Cls.ToArray(); int rev = s.Revision; var regionHome = s.CubeCentre;
        // Move 20 mm toward the hand's axis, so the region stays in tissue.
        double rr = Math.Sqrt(regionHome.X * regionHome.X + regionHome.Y * regionHome.Y), ux = rr > 1e-6 ? -regionHome.X / rr : -1, uy = rr > 1e-6 ? -regionHome.Y / rr : 0;
        app.DragRegion(app.Scanner.Physics.TransformPoint(Frames.ToS(regionHome.X + 0.020 * ux, regionHome.Y + 0.020 * uy, regionHome.Z))); app.CommitRegion();
        await Until(() => app.Sim.State.Revision != rev, 3);
        var s2 = app.Sim.State; int changed = 0, tissue = 0;
        for (int i = 0; i < s2.Cube.N; i++) { if (s2.Cube.Cls[i] != cls0[i]) changed++; if (s2.Cube.Pd[i] > 0) tissue++; }
        Check(changed > 0.3 * s2.Cube.N && tissue > 0.5 * s2.Cube.N, $"S2 moving the region within the hand resamples the tissue under the needles ({changed} of {s2.Cube.N} cells changed class; {tissue} hold tissue)");
        await Until(() => app.Sim.State.AcquisitionFinished, 25);
        rev = app.Sim.State.Revision;
        app.DragRegion(app.Scanner.Physics.TransformPoint(Frames.ToS(regionHome.X, regionHome.Y, regionHome.Z))); app.CommitRegion();
        await Until(() => app.Sim.State.Revision != rev, 3);
        s2 = app.Sim.State; changed = 0; for (int i = 0; i < s2.Cube.N; i++) if (s2.Cube.Cls[i] != cls0[i]) changed++;
        Check(changed == 0, $"S2 moving the region back restores the same tissue ({changed} cells differ)");
        // Selecting a proton: a ray from the eye through a proton of the grid selects it, and the close-up follows it.
        await Until(() => app.Sim.State.AcquisitionFinished, 25); await WaitFrames(3);
        // The proton nearest the eye (no other lies in front of it), other than the one shown.
        var eye = camera.transform.position; int pick = -1; float nearest = float.MaxValue;
        for (int i = 0; i < app.Sim.State.Cube.N; i++) if (app.Cube.Present(i) && i != app.Cube.Selected) { float d = (app.Cube.CellWorld(i) - eye).sqrMagnitude; if (d < nearest) { nearest = d; pick = i; } }
        var target = app.Cube.CellWorld(pick);
        bool hit = app.SelectProton(new Ray(eye, (target - eye).normalized)); await WaitFrames(2);
        Check(hit && app.Cube.Selected == pick && app.CloseUp.Root.gameObject.activeInHierarchy, $"K pointing at a proton selects it for the close-up (proton {pick}, selected {app.Cube.Selected})");
        bool miss = app.SelectProton(new Ray(eye, (app.Cube.Root.position + camera.transform.right * 0.8f - eye).normalized));
        Check(!miss && app.Cube.Selected == pick, "K pointing past the protons keeps the selection");
    }

    // ------------------------------------------------------------------------------------------------ palette

    static double DeltaE2000(Color a, Color b)
    {
        Lab(a, out double l1, out double a1, out double b1); Lab(b, out double l2, out double a2, out double b2);
        double c1 = Math.Sqrt(a1 * a1 + b1 * b1), c2 = Math.Sqrt(a2 * a2 + b2 * b2), cb = (c1 + c2) / 2;
        double g = 0.5 * (1 - Math.Sqrt(Math.Pow(cb, 7) / (Math.Pow(cb, 7) + Math.Pow(25, 7))));
        double ap1 = (1 + g) * a1, ap2 = (1 + g) * a2, cp1 = Math.Sqrt(ap1 * ap1 + b1 * b1), cp2 = Math.Sqrt(ap2 * ap2 + b2 * b2);
        double h1 = (Math.Atan2(b1, ap1) * 180 / Math.PI + 360) % 360, h2 = (Math.Atan2(b2, ap2) * 180 / Math.PI + 360) % 360;
        double dl = l2 - l1, dc = cp2 - cp1, dh = h2 - h1; if (cp1 * cp2 == 0) dh = 0; else if (dh > 180) dh -= 360; else if (dh < -180) dh += 360;
        double dH = 2 * Math.Sqrt(cp1 * cp2) * Math.Sin(dh * Math.PI / 360);
        double lb = (l1 + l2) / 2, cpb = (cp1 + cp2) / 2, hb = h1 + h2;
        if (cp1 * cp2 != 0) { if (Math.Abs(h1 - h2) > 180) hb += h1 + h2 < 360 ? 360 : -360; hb /= 2; }
        double tt = 1 - 0.17 * Math.Cos((hb - 30) * Math.PI / 180) + 0.24 * Math.Cos(2 * hb * Math.PI / 180) + 0.32 * Math.Cos((3 * hb + 6) * Math.PI / 180) - 0.20 * Math.Cos((4 * hb - 63) * Math.PI / 180);
        double sl = 1 + 0.015 * (lb - 50) * (lb - 50) / Math.Sqrt(20 + (lb - 50) * (lb - 50)), sc = 1 + 0.045 * cpb, sh = 1 + 0.015 * cpb * tt;
        double rt = -2 * Math.Sqrt(Math.Pow(cpb, 7) / (Math.Pow(cpb, 7) + Math.Pow(25, 7))) * Math.Sin(60 * Math.Exp(-Math.Pow((hb - 275) / 25, 2)) * Math.PI / 180);
        return Math.Sqrt(Math.Pow(dl / sl, 2) + Math.Pow(dc / sc, 2) + Math.Pow(dH / sh, 2) + rt * (dc / sc) * (dH / sh));
    }

    static void Lab(Color c, out double l, out double a, out double b)
    {
        double Lin(double v) => v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        double r = Lin(c.r), g = Lin(c.g), bl = Lin(c.b);
        double x = (0.4124 * r + 0.3576 * g + 0.1805 * bl) / 0.95047, y = 0.2126 * r + 0.7152 * g + 0.0722 * bl, z = (0.0193 * r + 0.1192 * g + 0.9505 * bl) / 1.08883;
        double F(double v) => v > 0.008856 ? Math.Pow(v, 1.0 / 3) : 7.787 * v + 16.0 / 116;
        l = 116 * F(y) - 16; a = 500 * (F(x) - F(y)); b = 200 * (F(y) - F(z));
    }

    static bool Gold(Color c)
    {
        Color.RGBToHSV(new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b)), out float h, out float s, out float v);
        return h * 360 >= 20 && h * 360 <= 70 && s > 0.30f && v > 0.35f;
    }

    static void PaletteChecks()
    {
        var ents = Look.Entities; double minDe = 999; string pair = "";
        for (int i = 0; i < ents.Length; i++) for (int j = i + 1; j < ents.Length; j++) { double d = DeltaE2000(ents[i], ents[j]); if (d < minDe) { minDe = d; pair = i + "/" + j; } }
        Check(minDe >= 20, $"P1 entity colours pairwise dE2000 >= 20 (min {minDe:F1}, pair {pair})");
        Check(Look.GX == Look.AX && Look.GY == Look.AY && Look.GZ == Look.AZ, "P2 coil and spatial axis colours match");
        var roots = new[] { app.Cube.Root, app.CloseUp.Root, app.Plots.Signal, app.Plots.Sequence, app.Plots.Data };
        Check(roots.All(t => t.Find("Backing") == null && t.Find("Block walls") == null), "P4 tissue, microscope and plots have no opaque backing panels");
        Check(app.Cube.Root.Find("Sampled tissue volume") != null && app.Cube.Root.Find("Tissue fibre bundles (illustrative)") != null, "P5 tissue crop is volumetric and contains visible fibre geometry");
        Check(app.Scanner.Physics.Find("Receive surface coil") != null && Scanner.BirdcageRadius == 0.130 && Scanner.BirdcageHalfLength == 0.270,
            "P7 wide transmit birdcage and separate transverse surface receive loop");
        var lab = app.CloseUp.Root.Find("Lab frame");
        Check(lab != null && lab.Find("Selected hydrogen nucleus") != null && Quaternion.Angle(lab.rotation, app.Cube.Root.rotation) < 0.1f, "P6 selected nucleus and moment share the scanner physical orientation");
        var beforeTau=app.Tau; var beforeTime=app.T; var beforePlay=app.Playing;
        foreach(var action in new[]{"tissue","phase","moments","fields"}) { app.Action(action); app.Action(action); }
        Check(app.Tau==beforeTau && app.T==beforeTime && app.Playing==beforePlay, "View toggles preserve narration and physical clocks");
        Check(app.CloseUp.Root.GetComponentsInChildren<Transform>().Count(q=>q.name=="Water molecule (schematic)")==18,"Volumetric cluster contains 18 water molecules");
        var shell=app.Scanner.Physics.Find("ADC");
        Check(shell && shell.Find("ADC sampling die") && shell.GetComponent<Renderer>().sharedMaterial.shader.name=="Resonance/Glass","Receiver shell exposes sampling die");
        // TX text size (item 10): every scene label's capital height is at least 0.6 degrees from the eye at the default pose,
        // and the subtitles at least 1 degree (TextMeshPro cap height about 0.073 x size metres, times the parent scale).
        float minLabel = float.MaxValue; string smallest = null;
        foreach (var t in Labels.All)
        {
            if (t == null || !t.gameObject.activeInHierarchy || string.IsNullOrEmpty(t.text)) continue;
            float cap = 0.073f * t.fontSize * t.transform.lossyScale.y, deg = Mathf.Atan2(cap, Vector3.Distance(t.transform.position, app.Eye)) * Mathf.Rad2Deg;
            if (deg < minLabel) { minLabel = deg; smallest = t.text; }
        }
        var caption = app.Strip.Root.GetComponentsInChildren<TMPro.TextMeshPro>(true).First(t => t.name == "Subtitles");
        float capDeg = Mathf.Atan2(0.073f * caption.fontSize * caption.transform.lossyScale.y, Vector3.Distance(caption.transform.position, app.Eye)) * Mathf.Rad2Deg;
        Check(minLabel >= 0.6f && capDeg >= 1.0f, $"TX text sizes: smallest scene label {minLabel:F2} deg ('{smallest}'), subtitles {capDeg:F2} deg");
        // Text whitelist: every active text is the strip's, a controller help label, or a short scene label (axis letters and
        // live values beside what they measure); no legends.
        var texts = UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Exclude);
        var labels = new HashSet<TMPro.TMP_Text>(Labels.All);
        int stray = texts.Count(tx => !tx.transform.IsChildOf(app.Strip.Root) && tx.transform.parent != app.Controls.transform && !labels.Contains(tx));
        int longest = Labels.All.Where(l => l != null).Select(l => l.text.Length).DefaultIfEmpty(0).Max();
        Check(stray == 0 && longest <= 110, $"T1 text only on the strip, controller help, or short labels beside what they name ({stray} stray, longest label {longest} characters)");
        int tissueCells = app.Sim.State.Cube.Pd.Count(p => p > 0);
        Check(tissueCells >= 0.9 * app.Sim.State.Cube.N && app.Cube.DrawnNeedles == app.Cube.PresentCount && app.Cube.PresentCount == tissueCells && tissueCells >= 80,
            $"S1 one proton drawn per tissue isochromat of the grid ({app.Cube.DrawnNeedles} drawn, {tissueCells} tissue cells of {app.Sim.State.Cube.N})");
    }

    /// <summary>
    /// The review views (about ten per iteration, not one per sentence; 0.8.3): the protons and the close-up, the RF turning
    /// in the lab frame, the tip in the rotating frame, the signal line, the z twist, the slice (only the band tips), the x
    /// readout, the y twist, and the image forming.
    /// </summary>
    public static readonly string[] KeyViews = { "2.2", "2.10", "3.8", "4.9", "5.6", "6.6", "7.4", "9.4", "10.3" };

    static async Task RenderLesson()
    {
        for (int si = 0; si < app.Lesson.Steps.Count; si++)
        {
            var step = app.Lesson.Steps[si];
            foreach (var cue in step.Cues)
            {
                if (Array.IndexOf(KeyViews, cue.Data.id) < 0) continue;
                double body = Math.Max(0.1, cue.Len - cue.Lead - Lesson.CueGap);
                double tau = cue.Start + cue.Lead + 0.6 * body;
                if (step.StrobeT != null) tau = Math.Min(step.Len - 0.01, cue.Start + 0.6 * cue.Len);
                app.Seek(si, tau, false);
                await WaitFrames(3);
                string id = $"S{step.Data.step:00}C{cue.Index + 1:00}";
                await Capture(id, step, cue);
            }
        }
    }

    // ------------------------------------------------------------------------------------------------ MR preview (0.8.1)

    /// <summary>
    /// Mixed-reality preview renders and a CPU cost probe, without the gates:
    ///   Unity -batchmode -projectPath . -executeMethod ResonanceValidation.RunMR   (output dir from RESONANCE_MR_OUT)
    /// The eye buffer is rendered with alpha exactly as the headset composites it over passthrough (premultiplied,
    /// linear light: out = rgb + room * (1 - a)) over a mid-grey and a bright room backdrop.
    /// </summary>
    public static async void RunMR()
    {
        string outDir = System.Environment.GetEnvironmentVariable("RESONANCE_MR_OUT") ?? "validation/renders/mr";
        Directory.CreateDirectory(outDir);
        report = new Report { unity = Application.unityVersion, renderer = SystemInfo.graphicsDeviceName, version = ResonanceBuild.Version };
        int exit = 1;
        try
        {
            ResonanceBuild.Configure();
            EditorSceneManager.OpenScene(ResonanceBuild.ScenePath);
            EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            var ready = new TaskCompletionSource<bool>();
            void Changed(PlayModeStateChange st) { if (st == PlayModeStateChange.EnteredPlayMode) { EditorApplication.playModeStateChanged -= Changed; ready.SetResult(true); } }
            EditorApplication.playModeStateChanged += Changed; EditorApplication.isPlaying = true; await ready.Task;
            await Until(() => UnityEngine.Object.FindAnyObjectByType<App>()?.Ready == true);
            app = UnityEngine.Object.FindAnyObjectByType<App>(); camera = app.HeadCamera;
            await Until(() => app.Sim.State != null && app.Lesson.Steps.Count > 0 && app.World.gameObject.activeSelf);
            app.FixedStep = true; app.instantEmphasis = true;
            if (System.Environment.GetEnvironmentVariable("RESONANCE_MR_NARROW") == null) { camera.fieldOfView = ReviewFov; camera.transform.rotation = Quaternion.Euler(ReviewPitch, 0, 0); }
            await Until(() => app.Sim.State.AcquisitionFinished || app.Sim.State.Error != null, 25);
            await WaitFrames(3);
            var cues = new[] { "" }.Concat(KeyViews).ToArray();
            var costs = new StringBuilder("{\n  \"probes\": [\n");
            foreach (var id in cues)
            {
                if (id != "") await At(id); else { app.GoToStep(0, false); await WaitFrames(3); }
                string name = id == "" ? "00-overview" : "C" + id.Replace('.', '-');
                await CaptureMR(outDir, name);
                if (System.Environment.GetEnvironmentVariable("RESONANCE_FINAL_REVIEW") == "1")
                {
                    Directory.CreateDirectory("validation/renders/cues");
                    string finalName = id == "" ? "00-overview" : "S" + int.Parse(id.Split('.')[0]).ToString("00") + "C" + int.Parse(id.Split('.')[1]).ToString("00");
                    await Capture(finalName, null, null);
                }
                if (id != "") costs.Append(CostProbe(id)).Append(id == cues[cues.Length - 1] ? "\n" : ",\n");
            }
            costs.Append("  ]\n}\n");
            File.WriteAllText(Path.Combine(outDir, "cpu-cost-probe.json"), costs.ToString());
            exit = 0;
        }
        catch (Exception e) { Debug.LogException(e); }
        finally
        {
            Debug.Log("RESONANCE_MR " + (exit == 0 ? "DONE" : "FAILED"));
            EditorApplication.isPlaying = false; EditorApplication.Exit(exit);
        }
    }

    static float ToLinear(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
    static float ToSrgb(float c) => c <= 0.0031308f ? c * 12.92f : 1.055f * Mathf.Pow(c, 1 / 2.4f) - 0.055f;

    /// <summary>Renders the MR eye buffer (VR room hidden, cleared to transparent) and composites it over two backdrops.</summary>
    static async Task CaptureMR(string dir, string name)
    {
        await WaitFrames(1);
        var room = app.Environment != null ? app.Environment.Room : null; bool roomWas = room && room.activeSelf; var bgWas = camera.backgroundColor;
        if (room) room.SetActive(false); camera.backgroundColor = new Color(0, 0, 0, 0);
        var rt = new RenderTexture(ShotW, ShotH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var old = camera.targetTexture; float aspect = camera.aspect;
        camera.targetTexture = rt; camera.aspect = (float)ShotW / ShotH;
        SubmitInstanced(); camera.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(ShotW, ShotH, TextureFormat.RGBA32, false); tex.ReadPixels(new Rect(0, 0, ShotW, ShotH), 0, 0); tex.Apply();
        RenderTexture.active = prev; camera.targetTexture = old; camera.aspect = aspect;
        if (room) room.SetActive(roomWas); camera.backgroundColor = bgWas;
        File.WriteAllBytes(Path.Combine(dir, name + "-eye-rgba.png"), tex.EncodeToPNG());
        // The same view in the VR studio (for side-by-side comparison with the 0.7.0 renders, which show its studio).
        if (room)
        {
            room.SetActive(true); camera.backgroundColor = Look.Hex(0x14181E);
            var rv = new RenderTexture(ShotW, ShotH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = rv; camera.aspect = (float)ShotW / ShotH;
            SubmitInstanced(); camera.Render();
            RenderTexture.active = rv; var vt = new Texture2D(ShotW, ShotH, TextureFormat.RGB24, false); vt.ReadPixels(new Rect(0, 0, ShotW, ShotH), 0, 0); vt.Apply();
            RenderTexture.active = prev; camera.targetTexture = old; camera.aspect = aspect;
            File.WriteAllBytes(Path.Combine(dir, name + "-vr.png"), vt.EncodeToPNG()); UnityEngine.Object.Destroy(vt); rv.Release(); UnityEngine.Object.Destroy(rv);
            room.SetActive(roomWas); camera.backgroundColor = bgWas;
        }
        var px = tex.GetPixels32();
        foreach (var (tag, grey) in new[] { ("grey", 0.5f), ("room", 0.72f) })
        {
            var outp = new Color32[px.Length]; float bgl = ToLinear(grey);
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i]; float a = c.a / 255f; int y = i / ShotW;
                // "room": a softly lit wall above a darker floor, so bright and dark content can both be judged.
                float b = tag == "room" ? ToLinear(Mathf.Lerp(0.46f, grey, Mathf.SmoothStep(0.30f, 0.45f, y / (float)ShotH))) : bgl;
                float r = ToLinear(c.r / 255f) + b * (1 - a), g = ToLinear(c.g / 255f) + b * (1 - a), bl = ToLinear(c.b / 255f) + b * (1 - a);
                outp[i] = new Color32((byte)Mathf.RoundToInt(255 * ToSrgb(Mathf.Clamp01(r))), (byte)Mathf.RoundToInt(255 * ToSrgb(Mathf.Clamp01(g))), (byte)Mathf.RoundToInt(255 * ToSrgb(Mathf.Clamp01(bl))), 255);
            }
            var o = new Texture2D(ShotW, ShotH, TextureFormat.RGBA32, false); o.SetPixels32(outp); o.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + "-mr-" + tag + ".png"), o.EncodeToPNG()); UnityEngine.Object.Destroy(o);
        }
        UnityEngine.Object.Destroy(tex); rt.Release(); UnityEngine.Object.Destroy(rt);
        Debug.Log("RESONANCE_MR_CAPTURE " + name);
    }

    /// <summary>Main-thread CPU cost of the per-frame view updates at the current lesson point (editor, Mono JIT, x86).</summary>
    static string CostProbe(string id)
    {
        const int n = 40; var sw = new System.Diagnostics.Stopwatch();
        double t = app.T; float dt = 1 / 72f;
        (double ms, long bytes) Measure(Action a)
        {
            a(); long b0 = GC.GetAllocatedBytesForCurrentThread(); sw.Restart();
            for (int i = 0; i < n; i++) a();
            sw.Stop(); return (sw.Elapsed.TotalMilliseconds / n, (GC.GetAllocatedBytesForCurrentThread() - b0) / n);
        }
        var simHand = Measure(() => app.Sim.Update(t, evaluateHand: true));
        var simCube = Measure(() => app.Sim.Update(t, evaluateHand: false));
        var scanner = Measure(() => app.Scanner.Tick(dt, false, app.DisplayCarrier));
        var cube = Measure(() => app.Cube.Tick(dt, app.DisplayCarrier, true, false));
        return $"    {{\"cue\": \"{id}\", \"sim_cube_and_hand_ms\": {simHand.ms:F3}, \"sim_cube_only_ms\": {simCube.ms:F3}, \"scanner_tick_ms\": {scanner.ms:F3}, \"cube_tick_ms\": {cube.ms:F3}, \"alloc_bytes_per_frame\": {simCube.bytes + scanner.bytes + cube.bytes}}}";
    }

    /// <summary>
    /// Review render of one cue: the MR eye buffer composited as the headset shows it, over a bright room photograph (0.8.4:
    /// the review asks for judgement over a bright room) and, for comparison, over mid grey (validation/renders/grey).
    /// </summary>
    static async Task Capture(string name, LessonStep step, LessonCue cue)
    {
        await WaitFrames(1);
        var px = RenderEye();
        var o = new Texture2D(ShotW, ShotH, TextureFormat.RGBA32, false); o.SetPixels32(Composite(px, "photo")); o.Apply();
        string file = "validation/renders/cues/" + name + ".png";
        File.WriteAllBytes(file, o.EncodeToPNG());
        Directory.CreateDirectory("validation/renders/grey"); o.SetPixels32(Composite(px, "grey")); o.Apply();
        File.WriteAllBytes("validation/renders/grey/" + name + ".png", o.EncodeToPNG()); UnityEngine.Object.Destroy(o);
        report.shots.Add(name);
        manifest.shots.Add(new Shot { file = file, step = step != null ? step.Data.step.ToString() : "", title = step?.Title ?? "Overview", cue = cue?.Data.id ?? "", text = cue?.Text ?? app.Strip.CaptionText, refs = cue?.Data.refs ?? new string[0], t = app.T, tau = app.Tau, lab = app.Lab });
        Debug.Log("RESONANCE_CAPTURE " + name);
    }

    /// <summary>The eye buffer with alpha, rendered in MR (VR room hidden, cleared to transparent).</summary>
    static Color32[] RenderEye()
    {
        var room = app.Environment != null ? app.Environment.Room : null; bool roomWas = room && room.activeSelf; var bgWas = camera.backgroundColor;
        if (room) room.SetActive(false); camera.backgroundColor = new Color(0, 0, 0, 0);
        var rt = new RenderTexture(ShotW, ShotH, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var old = camera.targetTexture; float aspect = camera.aspect;
        camera.targetTexture = rt; camera.aspect = (float)ShotW / ShotH;
        SubmitInstanced(); camera.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(ShotW, ShotH, TextureFormat.RGBA32, false); tex.ReadPixels(new Rect(0, 0, ShotW, ShotH), 0, 0); tex.Apply();
        RenderTexture.active = prev; camera.targetTexture = old; camera.aspect = aspect;
        if (room) room.SetActive(roomWas); camera.backgroundColor = bgWas;
        var px = tex.GetPixels32(); UnityEngine.Object.Destroy(tex); rt.Release(); UnityEngine.Object.Destroy(rt);
        return px;
    }

    static Color32[] photo;
    /// <summary>The bright room photograph (validation/backdrops/room-photo.png, 1920 x 1200, CC0; see SOURCE.txt).</summary>
    static Color32[] Photo()
    {
        if (photo != null) return photo;
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!File.Exists("validation/backdrops/room-photo.png") || !t.LoadImage(File.ReadAllBytes("validation/backdrops/room-photo.png")) || t.width != ShotW || t.height != ShotH) throw new Exception("Backdrop photo missing or not 1920 x 1200");
        photo = t.GetPixels32(); UnityEngine.Object.Destroy(t); return photo;
    }

    /// <summary>Premultiplied composite over a backdrop in linear light: out = rgb + room * (1 - a).</summary>
    static Color32[] Composite(Color32[] px, string backdrop)
    {
        var outp = new Color32[px.Length]; float grey = backdrop == "room" ? 0.72f : 0.24f, bgl = ToLinear(grey);
        Color32[] room = null; // Neutral studio review background; no photographic kitchen.
        for (int i = 0; i < px.Length; i++)
        {
            var c = px[i]; float a = c.a / 255f; int y = i / ShotW;
            float b = backdrop == "room" ? ToLinear(Mathf.Lerp(0.46f, grey, Mathf.SmoothStep(0.30f, 0.45f, y / (float)ShotH))) : bgl;
            float br = b, bg = b, bb = b;
            if (room != null) { var q = room[i]; br = ToLinear(q.r / 255f); bg = ToLinear(q.g / 255f); bb = ToLinear(q.b / 255f); }
            float r = ToLinear(c.r / 255f) + br * (1 - a), g = ToLinear(c.g / 255f) + bg * (1 - a), bl = ToLinear(c.b / 255f) + bb * (1 - a);
            outp[i] = new Color32((byte)Mathf.RoundToInt(255 * ToSrgb(Mathf.Clamp01(r))), (byte)Mathf.RoundToInt(255 * ToSrgb(Mathf.Clamp01(g))), (byte)Mathf.RoundToInt(255 * ToSrgb(Mathf.Clamp01(bl))), 255);
        }
        return outp;
    }

    // ------------------------------------------------------------------------------------------------ frame cost (0.8.1)

    /// <summary>
    /// Headless per-frame cost: real-time playback as on the headset (spins on the worker one frame behind, emphasis
    /// animated) at five lesson points. Main-thread milliseconds per part (editor, Mono JIT, x86 server core), the worker's
    /// milliseconds, managed allocation per frame, and GPU proxies: shaded fragments per pixel (overdraw) and raymarch samples
    /// per frame at the Quest 3 default eye resolution. Writes validation/frame-cost.json.
    /// </summary>
    static async Task FrameCostReport()
    {
        var sb = new StringBuilder("{\n  \"note\": \"Editor on the build server (Mono JIT, x86). Quest 3 CPU/GPU times need the headset: use the strip's ms readout.\",\n  \"points\": [\n");
        (int step, string cue)[] points = { (2, "3.6"), (3, "4.8"), (5, "6.7"), (6, "7.5"), (8, "9.4") };
        app.FixedStep = false; app.instantEmphasis = false;
        for (int k = 0; k < points.Length; k++)
        {
            await At(points[k].cue); app.TogglePlay(); if (!app.Playing) app.TogglePlay();
            await WaitFrames(150);
            var c = app.Cost;
            var (over, share4) = Overdraw();
            double samples = RaymarchSamples(); long tris = Triangles(out int draws);
            sb.Append($"    {{\"cue\": \"{points[k].cue}\", \"main_thread_ms\": {c.Total:F3}, \"sim_state_ms\": {c.Sim:F3}, \"scanner_ms\": {c.Scanner:F3}, \"cube_ms\": {c.Cube:F3}, \"plots_ms\": {c.Console:F3}, \"strip_ms\": {c.Strip:F3}, \"attention_ms\": {c.Attention:F3}, \"worker_ms\": {c.Worker:F3}, \"worker_hand_ms\": {c.WorkerHand:F3}, \"alloc_bytes_per_frame\": {c.AllocBytes:F0}, \"fragments_per_pixel\": {over:F3}, \"pixels_with_4plus_layers\": {share4:F4}, \"raymarch_samples_per_frame_quest3\": " + samples.ToString("F0") + $", \"triangles_per_eye\": {tris}, \"draws_per_eye\": {draws}" + ", \"load_average\": \"" + LoadAverage() + "\"}");
            sb.Append(k < points.Length - 1 ? ",\n" : "\n");
            app.Pause();
        }
        app.FixedStep = true; app.instantEmphasis = true;
        sb.Append("  ]\n}\n");
        File.WriteAllText("validation/frame-cost.json", sb.ToString());
        Check(app.Cost.AllocBytes < 2048, $"PERF managed allocation per frame during playback under 2 KB ({app.Cost.AllocBytes:F0} bytes)");
        long maxTris = 0; foreach (var line in sb.ToString().Split('\n')) { int k = line.IndexOf("\"triangles_per_eye\": "); if (k >= 0) maxTris = Math.Max(maxTris, long.Parse(new string(line.Substring(k + 21).TakeWhile(char.IsDigit).ToArray()))); }
        Check(maxTris > 0 && maxTris < 150000, $"PERF submitted geometry under 150 000 triangles per eye at every probe (largest {maxTris})");
    }

    /// <summary>Triangles and draws submitted per eye: every enabled renderer of the dashboard plus the instanced needles.</summary>
    static long Triangles(out int draws)
    {
        long tris = 0; draws = 0;
        foreach (var r in app.World.GetComponentsInChildren<MeshRenderer>(false))
        {
            if (!r.enabled) continue;
            var m = r.GetComponent<MeshFilter>()?.sharedMesh; if (m == null) continue;
            for (int k = 0; k < m.subMeshCount; k++) tris += m.GetIndexCount(k) / 3;
            draws++;
        }
        // Moment lattice is included in the ordinary MeshRenderer totals.
        return tris;
    }

    static string LoadAverage() { try { return File.ReadAllText("/proc/loadavg").Trim(); } catch (Exception) { return ""; } }

    /// <summary>Mean shaded fragments per pixel of the default view (every draw counted once per covered pixel).</summary>
    static (double mean, double share4) Overdraw()
    {
        var shader = Shader.Find("Hidden/Resonance/Overdraw"); if (shader == null) return (-1, -1);
        const int w = 480, h = 300;
        // Measured in MR (the headset's default): the VR studio's dome is hidden, as on the device.
        var room = app.Environment != null ? app.Environment.Room : null; bool roomWas = room && room.activeSelf; if (room) room.SetActive(false);
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGBHalf); var old = camera.targetTexture; float aspect = camera.aspect; var bg = camera.backgroundColor; var flags = camera.clearFlags;
        camera.targetTexture = rt; camera.aspect = (float)w / h; camera.backgroundColor = new Color(0, 0, 0, 0); camera.clearFlags = CameraClearFlags.SolidColor;
        SubmitInstanced();
        camera.RenderWithShader(shader, "");
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBAFloat, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = prev; camera.targetTexture = old; camera.aspect = aspect; camera.backgroundColor = bg; camera.clearFlags = flags;
        if (room) room.SetActive(roomWas);
        var px = tex.GetPixels(); double sum = 0; int many = 0;
        foreach (var p in px) { double layers = p.r * 32; sum += layers; if (layers >= 3.5) many++; }
        UnityEngine.Object.Destroy(tex); rt.Release(); UnityEngine.Object.Destroy(rt);
        return (sum / px.Length, many / (double)px.Length);
    }

    /// <summary>Raymarch samples per frame at the Quest 3 default eye resolution (both eyes): screen share x steps per volume.</summary>
    static double RaymarchSamples()
    {
        const double eyePixels = 1680.0 * 1760.0 * 2; double total = 0;
        float aspect = camera.aspect; camera.aspect = (float)ShotW / ShotH;
        foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
        {
            if (!r.enabled || r.sharedMaterial == null || r.sharedMaterial.shader.name != "Resonance/Volume") continue;
            var pts = new List<Vector2>();
            for (int k = 0; k < 8; k++)
            {
                var c = r.transform.TransformPoint(new Vector3((k & 1) == 0 ? -0.5f : 0.5f, (k & 2) == 0 ? -0.5f : 0.5f, (k & 4) == 0 ? -0.5f : 0.5f));
                var v = camera.WorldToViewportPoint(c); if (v.z <= 0) continue; pts.Add(new Vector2(Mathf.Clamp01(v.x), Mathf.Clamp01(v.y)));
            }
            total += HullArea(pts) * (r.sharedMaterial.GetFloat("_Quad") > 0.5f ? 2 : r.sharedMaterial.GetFloat("_Steps")) * eyePixels;
        }
        camera.aspect = aspect;
        return total;
    }

    static double HullArea(List<Vector2> p)
    {
        if (p.Count < 3) return 0;
        p.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        var hull = new List<Vector2>();
        for (int pass = 0; pass < 2; pass++)
        {
            int start = hull.Count;
            for (int i = 0; i < p.Count; i++)
            {
                var q = pass == 0 ? p[i] : p[p.Count - 1 - i];
                while (hull.Count >= start + 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], q) <= 0) hull.RemoveAt(hull.Count - 1);
                hull.Add(q);
            }
            hull.RemoveAt(hull.Count - 1);
        }
        double area = 0; for (int i = 0; i < hull.Count; i++) { var a = hull[i]; var b = hull[(i + 1) % hull.Count]; area += a.x * b.y - b.x * a.y; }
        return System.Math.Abs(area) / 2;
    }

    static double Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
}
