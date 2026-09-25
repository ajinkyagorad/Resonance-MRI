using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Nebulytic.Resonance.Sim;

/// <summary>
/// Server-side runner for the simulation core (same source files as the Unity runtime):
///   dotnet run -c Release -- bake    writes Assets/Resources/Resonance/FieldTables.bytes and Docs/AI/scanner-windings.json
///   dotnet run -c Release -- test    runs the acceptance tests and writes validation/sim-report.json
/// </summary>
static class SimCore
{
    static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
    static readonly List<string> lines = new List<string>();
    static int failures;

    static int Main(string[] args)
    {
        string cmd = args.Length > 0 ? args[0] : "test";
        if (!File.Exists(Path.Combine(Root, "Assets/Resonance/Runtime/Sim/Coils.cs"))) { Console.Error.WriteLine("Root not found: " + Root); return 2; }
        if (cmd == "bake") return Bake();
        if (cmd == "test") return Test(args.Skip(1).ToArray());
        if (cmd == "derive") return Derive();
        if (cmd == "bench") return Bench(args.Skip(1).ToArray());
        Console.Error.WriteLine("usage: bake | test | derive | bench [threads]"); return 2;
    }

    /// <summary>
    /// CPU cost of a full rebuild and of the whole acquisition (all slices), with the simulation's parallel loops limited to
    /// the given number of threads (0: every core). Process CPU time is what the headset's cores would have to supply.
    /// </summary>
    static int Bench(string[] a)
    {
        int threads = a.Length > 0 ? int.Parse(a[0]) : 0; Work.MaxThreads = threads;
        var sim = new Simulation(Tables, Labels) { AddNoise = true }; var p = new Protocol();
        var sp = new HandSpecimen(Labels, Simulation.DefaultPose(Labels));
        var proc = Process.GetCurrentProcess(); proc.Refresh(); var cpu0 = proc.TotalProcessorTime; var sw = Stopwatch.StartNew();
        long a0 = GC.GetTotalAllocatedBytes(true);
        var st = sim.BuildNow(p, sp, null, acquire: false);
        proc.Refresh(); double buildCpu = (proc.TotalProcessorTime - cpu0).TotalSeconds, buildWall = sw.Elapsed.TotalSeconds;
        long a1 = GC.GetTotalAllocatedBytes(true); cpu0 = proc.TotalProcessorTime; sw.Restart();
        sim.Acquire(st);
        proc.Refresh(); double acqCpu = (proc.TotalProcessorTime - cpu0).TotalSeconds, acqWall = sw.Elapsed.TotalSeconds;
        long a2 = GC.GetTotalAllocatedBytes(true);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); long live = GC.GetTotalMemory(true);
        Console.WriteLine($"threads {(threads > 0 ? threads.ToString() : "all (" + Environment.ProcessorCount + ")")}: build {buildWall:F1} s wall, {buildCpu:F1} s CPU, {(a1 - a0) / 1e6:F0} MB allocated; acquisition of {st.Slices.Length} slices {acqWall:F1} s wall, {acqCpu:F1} s CPU, {(a2 - a1) / 1e6:F0} MB allocated; live heap {live / 1e6:F0} MB; error {st.Error ?? "none"}");
        // The spin worker's per-frame evaluation inside one repetition, and one block change per frame (strobed rows).
        var prog = st.Program; double ta = prog.Time("s0.r0.ss.start"), tb = prog.Time("s0.r0.ro.end"); sim.Update(ta);
        long f0 = GC.GetTotalAllocatedBytes(true); const int frames = 400;
        for (int f = 0; f < frames; f++) sim.Update(ta + (tb - ta) * f / frames, f % 3 == 0);
        long f1 = GC.GetTotalAllocatedBytes(true); var rows = prog.SliceBlocks[0];
        for (int f = 2; f < rows.Count; f++) sim.Update(prog.Blocks[rows[f]].Echo, true);
        long f2 = GC.GetTotalAllocatedBytes(true);
        Console.WriteLine($"spin evaluation: {(f1 - f0) / (double)frames:F0} B allocated per frame within a repetition, {(f2 - f1) / (double)(rows.Count - 2) / 1e3:F1} kB per change of repetition");
        GC.KeepAlive(st);
        return st.Error == null ? 0 : 1;
    }

    /// <summary>Numbers spoken by the narration, computed by the running code (never typed by hand).</summary>
    static int Derive()
    {
        var p = new Protocol();
        var hard = RfPulse.Hard(Protocol.HardFlip, Protocol.HardPulse);
        double b1 = hard.Peak * Scanner.B1Iso, ratio = p.B0 / b1;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string Sig(double v, int d) => Math.Round(v, d).ToString("0.###", inv);
        var d = new Dictionary<string, string>
        {
            ["I_B0"] = Math.Round(p.MagnetCurrent).ToString("0", inv),
            ["f0"] = Sig(p.Fref / 1e6, 1),
            ["pol"] = Sig(Constants.Polarization(p.B0) * 1e6, 0),
            ["ratio"] = Math.Round(ratio / 1000).ToString("0", inv) + " thousand",
            ["rx_rate"] = Sig(Protocol.RxBandwidth / 1000, 0),
            ["BW"] = (Math.Round(p.BandwidthEff / 100) * 100).ToString("0", inv),
            ["dz"] = Sig(p.SliceThickness * 1e3, 0),
        };
        var sb = new StringBuilder("{\n");
        int k = 0; foreach (var kv in d) sb.Append($"  \"{kv.Key}\": \"{kv.Value}\"{(++k < d.Count ? "," : "")}\n");
        sb.Append($"}}\n");
        File.WriteAllText(Path.Combine(Root, "Docs/AI/lesson-derived.json"), sb.ToString());
        Console.Write(sb.ToString());
        Console.WriteLine($"exact: I_B0 {p.MagnetCurrent:F3} A, f0 {p.Fref:F1} Hz, polarization {Constants.Polarization(p.B0):E3}, hard B1 {b1 * 1e6:F3} uT (ratio {ratio:F0}), BW {p.BandwidthEff:F1} Hz, slice {p.SliceThickness * 1e3:F3} mm, TE computed in tests");
        return 0;
    }

    static int Bake()
    {
        var sw = Stopwatch.StartNew();
        var t = FieldTables.Bake(s => Console.WriteLine($"{sw.Elapsed.TotalSeconds:F1}s {s}"));
        var bytes = t.Serialize();
        var path = Path.Combine(Root, "Assets/Resources/Resonance/FieldTables.bytes");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, bytes);
        Console.WriteLine($"wrote {path} ({bytes.Length / 1e6:F2} MB) in {sw.Elapsed.TotalSeconds:F1} s");
        var sb = new StringBuilder();
        sb.Append("{\n  \"magnet\": {\"conductor_mm\": [1.44, 1.44], \"r_mm\": [220, 250], \"packs\": [");
        for (int i = 0; i < Scanner.PackZ.Length; i++)
            sb.Append($"{(i > 0 ? ", " : "")}{{\"z_centre_mm\": {Scanner.PackZ[i] * 1e3:F2}, \"length_mm\": {Scanner.PackL[i] * 1e3:F2}, \"turns\": {Scanner.TurnsPerMetre * Scanner.PackL[i]:F1}}}");
        sb.Append($"], \"mirrored\": true, \"b_iso_mT_per_A\": {Scanner.BIso * 1e3:F6}, \"current_for_1T_A\": {1 / Scanner.BIso:F3}}},\n");
        sb.Append($"  \"gradients\": {{\"eta_x_mT_m_A\": {Scanner.EtaX * 1e3:F6}, \"eta_y_mT_m_A\": {Scanner.EtaY * 1e3:F6}, \"eta_z_mT_m_A\": {Scanner.EtaZ * 1e3:F6}}},\n");
        sb.Append($"  \"birdcage\": {{\"b1_iso_uT_per_A\": {Scanner.B1Iso * 1e6:F4}}},\n  \"definition\": \"{FieldTables.WindingDefinition()}\"\n}}\n");
        File.WriteAllText(Path.Combine(Root, "Docs/AI/scanner-windings.json"), sb.ToString());
        return 0;
    }

    static void Check(string id, bool ok, string measured)
    {
        string line = $"[{(ok ? "PASS" : "FAIL")}] {id}: {measured}";
        Console.WriteLine(line); lines.Add(line); if (!ok) failures++;
    }

    static FieldTables tables; static HandLabels labels;
    static FieldTables Tables => tables ??= FieldTables.Load(File.ReadAllBytes(Path.Combine(Root, "Assets/Resources/Resonance/FieldTables.bytes")));
    static HandLabels Labels => labels ??= HandLabels.Load(File.ReadAllBytes(Path.Combine(Root, "Assets/Resources/Anatomy/HandLabels.bytes")));

    static void TeachingTests()
    {
        var t1=TeachingSample.Moment("t1",0.4,0,0,0); var t2=TeachingSample.Moment("t2",1.0/3,0,0,0);
        Check("TEACH T1",Math.Abs(t1.Z-(1-Math.Exp(-1)))<1e-10,"one T1 recovers 63.2% of missing Mz");
        Check("TEACH T2",Math.Abs(t2.X-Math.Exp(-1))<1e-10,"one T2 retains 36.8% transverse magnetization");
        var x1=TeachingSample.Moment("gx",1,0.1,-0.2,0);var x2=TeachingSample.Moment("gx",1,0.1,0.2,0);
        Check("TEACH equal x",(x1-x2).Norm<1e-10,"same x produces same frequency regardless of y");
        var gy=TeachingSample.Moment("gy",1,0,0.25,0);var hold=TeachingSample.Moment("gyHold",0.2,0,0.25,0);
        Check("TEACH phase memory",(gy-hold).Norm<1e-10,"Gy-off retains accumulated phase");
        var outer=TeachingSample.Moment("slice",1,0,0,0.4);var inner=TeachingSample.Moment("slice",1,0,0,0);
        Check("TEACH slice",outer.Z>0.999 && inner.X>0.999,"ideal band tips inner slab and leaves outer longitudinal");
        var a=TeachingSample.Moment("pair1",1,1.0/12,-0.25,0.0625);var b=TeachingSample.Moment("pair1",1,1.0/12,0.25,0.0625);
        double s0=1.1,s1=a.X+b.X;
        Check("TEACH separate y",Math.Abs((s0+s1)/2-0.8)<1e-10 && Math.Abs((s0-s1)/2-0.3)<1e-10,"two phase encodings recover both same-x samples");
    }

    static int Test(string[] only)
    {
        var sw = Stopwatch.StartNew();
        bool Run(string g) => only.Length == 0 || only.Contains(g);
        if (Run("teach")) TeachingTests();
        if (Run("fld")) Fields();
        if (Run("tis")) TissueTests();
        if (Run("blo")) BlochTests();
        if (Run("seq")) SequenceTests();
        if (Run("lesson")) LessonTests();
        Console.WriteLine($"{failures} failures, {lines.Count} checks, {sw.Elapsed.TotalSeconds:F1} s");
        var report = new StringBuilder("{\n  \"checks\": [\n");
        for (int i = 0; i < lines.Count; i++) report.Append("    ").Append(Json(lines[i])).Append(i + 1 < lines.Count ? ",\n" : "\n");
        report.Append($"  ],\n  \"failures\": {failures}\n}}\n");
        Directory.CreateDirectory(Path.Combine(Root, "validation"));
        File.WriteAllText(Path.Combine(Root, "validation/sim-report.json"), report.ToString());
        return failures == 0 ? 0 : 1;
    }

    static string Json(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    // ------------------------------------------------------------------------------------------------ fields

    static void Fields()
    {
        // FLD1: loop primitive against a fine polygon and the on-axis formula.
        var rnd = new Random(7); double worst = 0;
        for (int k = 0; k < 20; k++)
        {
            double a = 0.1, rho = rnd.NextDouble() * 0.2, z = (rnd.NextDouble() - 0.5) * 0.2;
            if (Math.Abs(Math.Sqrt((rho - a) * (rho - a) + z * z)) < 0.005) continue;
            BiotSavart.Loop(a, 0, 1, rho, z, out double br, out double bz);
            var poly = new D3[3601];
            for (int j = 0; j <= 3600; j++) { double ph = j * 2 * Math.PI / 3600; poly[j] = new D3(a * Math.Cos(ph), a * Math.Sin(ph), 0); }
            var b = BiotSavart.Path(poly, new D3(rho, 0, z), 1);
            worst = Math.Max(worst, Math.Max(Math.Abs(b.X - br), Math.Abs(b.Z - bz)) / Math.Sqrt(br * br + bz * bz));
        }
        Check("FLD1 loop vs 3600-segment polygon", worst < 2e-6, $"max rel err {worst:E2}");
        BiotSavart.Loop(0.1, 0, 1, 0, 0.05, out _, out double bzAxis);
        double exact = Constants.Mu0 * 0.01 / (2 * Math.Pow(0.01 + 0.0025, 1.5));
        Check("FLD1 loop on axis", Math.Abs(bzAxis / exact - 1) < 1e-12, $"rel err {Math.Abs(bzAxis / exact - 1):E2}");

        // FLD2: magnet.
        Check("FLD2 b_iso", Scanner.BIso > 0.0074 && Scanner.BIso < 0.0077, $"{Scanner.BIso * 1e3:F6} mT/A; 1 T at {1 / Scanner.BIso:F3} A");
        foreach (double d in new[] { 0.12, 0.14, 0.16 })
        {
            double min = double.MaxValue, max = double.MinValue;
            for (int j = 0; j <= 180; j++)
            {
                double th = j * Math.PI / 180, r = d / 2 * Math.Sin(th), z = d / 2 * Math.Cos(th);
                Scanner.MagnetField(r, z, out double br, out double bz);
                double m = Math.Sqrt(br * br + bz * bz); min = Math.Min(min, m); max = Math.Max(max, m);
            }
            double ppm = (max - min) / Scanner.BIso * 1e6;
            double lim = d < 0.13 ? 0.7 : d < 0.15 ? 2.1 : 7;
            Check($"FLD2 homogeneity over {d * 100:F0} cm DSV", ppm <= lim, $"{ppm:F3} ppm (limit {lim})");
        }

        // FLD3: gradients.
        Check("FLD3 eta_x", Math.Abs(Scanner.EtaX / 1.54150e-4 - 1) < 0.002, $"{Scanner.EtaX:E5} T/m/A");
        Check("FLD3 eta_y", Math.Abs(Scanner.EtaY / 1.44666e-4 - 1) < 0.002, $"{Scanner.EtaY:E5} T/m/A");
        double maxwell = 0.64131 * Constants.Mu0 / (Scanner.GzRadius * Scanner.GzRadius) * Scanner.GzTurns;
        Check("FLD3 eta_z (Maxwell 0.64131 mu0/a^2 per turn)", Math.Abs(Scanner.EtaZ / maxwell - 1) < 5e-4, $"{Scanner.EtaZ:E5} T/m/A vs {maxwell:E5}");
        double cross = Math.Abs(Scanner.GradientField(CoilId.Gx, new D3(0, 1e-3, 0)).Z) / (Scanner.EtaX * 1e-3);
        Check("FLD3 Gx has no y gradient", cross < 1e-6, $"{cross:E2}");
        foreach (var (coil, eta, dir) in new[] { (CoilId.Gz, Scanner.EtaZ, new D3(0, 0, 0.06)), (CoilId.Gx, Scanner.EtaX, new D3(0.06, 0, 0)) })
        {
            double lin = Scanner.GradientField(coil, dir).Z / (eta * dir.Norm) - 1;
            Check($"FLD3 {coil} linearity at 60 mm", Math.Abs(lin) < 0.02, $"{lin * 100:F2} %");
        }

        // FLD4: birdcage.
        // Independent midpoint integration of dl x r/r^3 validates the enlarged coil's analytic-segment solver.
        D3 IntegrateMode(bool cosine)
        {
            D3 field = D3.Zero;
            foreach (var conductor in Scanner.BirdcageParts)
            {
                double current = Scanner.PartCurrent(conductor.Part, cosine ? 0 : 1, cosine ? 1 : 0);
                for (int j = 1; j < conductor.Points.Length; j++)
                {
                    D3 a = conductor.Points[j - 1], dl = (conductor.Points[j] - a) / 128;
                    for (int k = 0; k < 128; k++) { D3 r = -(a + dl * (k + 0.5)); field += D3.Cross(dl, r) * (Constants.Mu0 * current / (4 * Math.PI * Math.Pow(r.Norm, 3))); }
                }
            }
            return field;
        }
        var qs = IntegrateMode(false); var qc = IntegrateMode(true); double referenceB1 = (qs.X - qc.Y) * 0.5;
        Check("FLD4 b1(0), independent numerical line integral", referenceB1 > 0 && Math.Abs(Scanner.B1Iso / referenceB1 - 1) < 0.0002, $"{Scanner.B1Iso * 1e6:F4} vs {referenceB1 * 1e6:F4} uT/A");
        var rx0 = Scanner.ReceiveField(new D3(0, Scanner.ReceiveY, 0));
        double rxExpected = Constants.Mu0 / (2 * Scanner.ReceiveRadius);
        Check("RX loop centre and transverse orientation", Math.Abs(rx0.Y / rxExpected - 1) < 1e-9 && Math.Abs(rx0.X) + Math.Abs(rx0.Z) < 1e-12, $"{rx0.Y * 1e6:F4} uT/A along y");
        double height = 0.05;
        var rxa = Scanner.ReceiveField(new D3(0, Scanner.ReceiveY - height, 0));
        double decay = Math.Pow(Scanner.ReceiveRadius * Scanner.ReceiveRadius / (Scanner.ReceiveRadius * Scanner.ReceiveRadius + height * height), 1.5);
        Check("RX sensitivity decays with distance from surface coil", Math.Abs(rxa.Y / rxExpected - decay) < 1e-9, $"relative sensitivity {rxa.Y / rxExpected:F4}");
        double lo = 9, hi = 0, minus = 0;
        foreach (var p in new[] { new D3(0.05, 0, 0), new D3(0, 0.05, 0), new D3(0.035, 0.035, 0.06), new D3(0, 0, 0.06), new D3(-0.03, 0.02, -0.05) })
        {
            Scanner.BirdcageModes(p, out D3 bs, out D3 bc); Scanner.B1Plus(bs, bc, out double re, out double im);
            double r = Math.Sqrt(re * re + im * im) / Scanner.B1Iso; lo = Math.Min(lo, r); hi = Math.Max(hi, r);
            minus = Math.Max(minus, Scanner.B1Minus(bs, bc) / Math.Sqrt(re * re + im * im));
        }
        Check("FLD4 b1 uniformity (rho<=50, |z|<=60 mm)", lo >= 0.86 && hi <= 1.03, $"[{lo:F3}, {hi:F3}]");
        Check("FLD4 counter-rotating fraction", minus <= 0.075, $"{minus:F4}");
        double kirch = 0;
        for (int n = 0; n < Scanner.Rungs; n++)
        {
            int prev = (n + Scanner.Rungs - 1) % Scanner.Rungs;
            kirch = Math.Max(kirch, Math.Abs(Scanner.PartCurrent(n, 1, 0.3) + Scanner.PartCurrent(Scanner.Rungs + prev, 1, 0.3) - Scanner.PartCurrent(Scanner.Rungs + n, 1, 0.3)));
        }
        Check("FLD4 Kirchhoff at every +z ring node", kirch < 1e-12, $"{kirch:E2} A");

        // FLD5: tables vs direct.
        var t = Tables; double gerr = 0, b1err = 0, d0err = 0;
        for (int k = 0; k < 400; k++)
        {
            var p = new D3((rnd.NextDouble() - 0.5) * 0.14, (rnd.NextDouble() - 0.5) * 0.10, (rnd.NextDouble() - 0.5) * 0.18);
            if (Math.Sqrt(p.X * p.X + p.Y * p.Y) > 0.070) { k--; continue; }
            t.Sample(p, out var s);
            var gx = Scanner.GradientField(CoilId.Gx, p); var gy = Scanner.GradientField(CoilId.Gy, p); var gz = Scanner.GradientField(CoilId.Gz, p);
            gerr = Math.Max(gerr, Math.Max(Math.Abs(s.Gx.Z - gx.Z), Math.Max(Math.Abs(s.Gy.Z - gy.Z), Math.Abs(s.Gz.Z - gz.Z))) * 100);
            Scanner.BirdcageModes(p, out D3 bs, out D3 bc); Scanner.B1Plus(bs, bc, out double re, out double im);
            b1err = Math.Max(b1err, Math.Sqrt((s.B1Re - re) * (s.B1Re - re) + (s.B1Im - im) * (s.B1Im - im)) / Scanner.B1Iso);
            double rho = Math.Sqrt(p.X * p.X + p.Y * p.Y); Scanner.MagnetField(rho, p.Z, out double br, out double bz);
            d0err = Math.Max(d0err, Math.Abs(s.D0.Z - (bz - Scanner.BIso)) / Scanner.BIso * 1e6);
        }
        Check("FLD5 gradient Bz table error at 100 A", gerr < 1.5e-6, $"{gerr * 1e6:F3} uT ({gerr * Constants.GammaBar:F1} Hz)");
        Check("FLD5 b1 table error", b1err < 0.005, $"{b1err * 100:F3} %");
        Check("FLD5 magnet deviation table error", d0err < 0.05, $"{d0err:F4} ppm ({d0err * 42.58:F2} Hz at 1 T)");

        // FLD6: stable delta-f against a direct high-precision difference.
        double dferr = 0;
        for (int k = 0; k < 200; k++)
        {
            var p = new D3((rnd.NextDouble() - 0.5) * 0.1, (rnd.NextDouble() - 0.5) * 0.06, (rnd.NextDouble() - 0.5) * 0.1);
            t.Sample(p, out var s); var c = new Currents(1 / Scanner.BIso, (rnd.NextDouble() - 0.5) * 100, (rnd.NextDouble() - 0.5) * 100, (rnd.NextDouble() - 0.5) * 100);
            double df = FieldTables.DeltaF(s, c, 0);
            decimal bx = (decimal)(c.B0 * s.D0.X + c.X * s.Gx.X + c.Y * s.Gy.X + c.Z * s.Gz.X);
            decimal by = (decimal)(c.B0 * s.D0.Y + c.X * s.Gx.Y + c.Y * s.Gy.Y + c.Z * s.Gz.Y);
            decimal bz = (decimal)(c.B0 * Scanner.BIso) + (decimal)(c.B0 * s.D0.Z + c.X * s.Gx.Z + c.Y * s.Gy.Z + c.Z * s.Gz.Z);
            decimal b2 = bx * bx + by * by + bz * bz; decimal r = (decimal)Math.Sqrt((double)b2);
            for (int it = 0; it < 6; it++) r = (r + b2 / r) / 2;
            double direct = (double)((r - (decimal)(c.B0 * Scanner.BIso)) * (decimal)Constants.GammaBar);
            dferr = Math.Max(dferr, Math.Abs(df - direct));
        }
        Check("FLD6 stable Larmor offset vs 28-digit direct", dferr < 0.01, $"{dferr:E2} Hz");

        // FLD7: single-coil polynomial model of the Larmor offset against the exact formula, at 1 T and 0.05 T.
        foreach (double b0 in new[] { 1.0, 0.05 })
        {
            var p = new Protocol { B0 = b0 };
            var set = IsoSet.Cube(new D3(0.02, -0.01, 0.03), 6, 0.01, new CylinderPhantom { Radius = 0.2, HalfLength = 0.2 }, t, p);
            double perr = 0;
            for (int k = 0; k < 300; k++)
            {
                int i = rnd.Next(set.N), axis = rnd.Next(3); double I = (rnd.NextDouble() - 0.5) * 200;
                var c = new Currents(p.MagnetCurrent, axis == 0 ? I : 0, axis == 1 ? I : 0, axis == 2 ? I : 0);
                double exactF = set.DeltaF(i, c), polyF = set.PolyIntegral(axis, i, I, I, 1, 1);
                perr = Math.Max(perr, Math.Abs(exactF - polyF));
            }
            Check($"FLD7 polynomial Larmor offset at {b0} T", perr < 0.01, $"max error {perr:E2} Hz");
        }
    }

    // ------------------------------------------------------------------------------------------------ tissue

    static void TissueTests()
    {
        Check("TIS1 muscle at 1 T", Math.Abs(TissueTable.T1(TissueClass.Muscle, 1) * 1e3 - 902) < 1.5 && Math.Abs(TissueTable.T2(TissueClass.Muscle, 1) * 1e3 - 35.7) < 0.1,
            $"T1 {TissueTable.T1(TissueClass.Muscle, 1) * 1e3:F1} ms, T2 {TissueTable.T2(TissueClass.Muscle, 1) * 1e3:F2} ms");
        Check("TIS1 fat at 1 T", Math.Abs(TissueTable.T1(TissueClass.Fat, 1) * 1e3 - 262) < 1.5 && Math.Abs(TissueTable.T2(TissueClass.Fat, 1) * 1e3 - 153.5) < 0.6,
            $"T1 {TissueTable.T1(TissueClass.Fat, 1) * 1e3:F1} ms, T2 {TissueTable.T2(TissueClass.Fat, 1) * 1e3:F1} ms");
        Check("TIS1 tendon T2* at 1 T", Math.Abs(TissueTable.T2(TissueClass.Tendon, 1) * 1e3 - 1.49) < 0.01, $"{TissueTable.T2(TissueClass.Tendon, 1) * 1e3:F3} ms");
        var lab = Labels; var counts = new int[9];
        foreach (var b in lab.Labels) counts[b]++;
        Check("TIS2 all tissue classes present", Enumerable.Range(1, 7).All(c => counts[c] > 0), string.Join(", ", Enumerable.Range(0, 8).Select(c => TissueTable.Names[c] + " " + counts[c])));
        var pose = Simulation.DefaultPose(lab); var sp = new HandSpecimen(lab, pose);
        sp.Bounds(out D3 mn, out D3 mx);
        Check("TIS2 hand pose: fingertips toward -z", mn.Z < -0.12 && mx.Z > 0.25, $"z from {mn.Z * 1e3:F0} to {mx.Z * 1e3:F0} mm");
    }

    // ------------------------------------------------------------------------------------------------ Bloch

    static void BlochTests()
    {
        // BLO1: free precession sense and rate.
        double x = 1, y = 0, z = 0, df = 1234.5, T = 0.010;
        Bloch.Step(ref x, ref y, ref z, 0, 0, df, T, 1, 1, 0);
        double ang = Math.Atan2(y, x), expect = -2 * Math.PI * df * T;
        double d = Math.IEEERemainder(ang - expect, 2 * Math.PI);
        Check("BLO1 free precession angle -2*pi*df*t", Math.Abs(d) < 1e-9, $"error {d:E2} rad");
        // BLO2: hard pulse along +x' tips +z toward +y'.
        x = 0; y = 0; z = 1; Bloch.Step(ref x, ref y, ref z, 250, 0, 0, 1e-3, 1, 1, 0);
        Check("BLO2 B1 along +x' tips +z to +y' (90 deg)", Math.Abs(y - 1) < 1e-12 && Math.Abs(z) < 1e-12, $"({x:F6}, {y:F6}, {z:F6})");
        double f1 = 250, off = 400, Om = Math.Sqrt(f1 * f1 + off * off), th = 2 * Math.PI * Om * 1e-3;
        x = 0; y = 0; z = 1; Bloch.Step(ref x, ref y, ref z, f1, 0, off, 1e-3, 1, 1, 0);
        double uz = Math.Cos(th) + off * off / (Om * Om) * (1 - Math.Cos(th));
        Check("BLO2 off-resonance hard pulse", Math.Abs(z - uz) < 1e-12, $"uz {z:F9} vs {uz:F9}");
        // BLO4: slice profile of the protocol pulse with ideal linear fields.
        var p = new Protocol();
        var pulse = SeqProgram.SlicePulse(p, 0);
        foreach (double flip in new[] { 10.0, 90.0 })
        {
            double scale = flip / 90.0; int n = 2001; double lo = double.MaxValue, hi = double.MinValue, peak = 0;
            var prof = new double[n];
            for (int k = 0; k < n; k++)
            {
                double f = (k - n / 2) * 4.0 * p.BandwidthEff / (n - 1);
                double mx = 0, my = 0, mz = 1;
                for (int s = 0; s < pulse.Steps; s++)
                    Bloch.Step(ref mx, ref my, ref mz, Constants.GammaBar * Scanner.B1Iso * pulse.Re[s] * scale, Constants.GammaBar * Scanner.B1Iso * pulse.Im[s] * scale, f, pulse.Dt, 1, 1, 0);
                prof[k] = Math.Sqrt(mx * mx + my * my); peak = Math.Max(peak, prof[k]);
            }
            for (int k = 0; k < n; k++) if (prof[k] >= peak / 2) { double f = (k - n / 2) * 4.0 * p.BandwidthEff / (n - 1); lo = Math.Min(lo, f); hi = Math.Max(hi, f); }
            double fwhm = (hi - lo) / p.BandwidthEff, target = flip < 45 ? 0.996 : 1.088;
            Check($"BLO4 slice profile FWHM/BW at {flip} deg", Math.Abs(fwhm - target) < 0.015, $"{fwhm:F4} (draft A {target})");
        }
    }

    // ------------------------------------------------------------------------------------------------ sequence

    static void SequenceTests()
    {
        var p = new Protocol();
        var prog = SeqProgram.Lesson(p, Layouts.VolumeSlices);
        Check("SEQ1 slice thickness", Math.Abs(p.SliceThickness - 0.005) < 1e-4, $"{p.SliceThickness * 1e3:F3} mm, Gss {p.Gss * 1e3:F3} mT/m at {p.GzCurrent:F2} A, Trf {p.Trf * 1e3:F2} ms");
        Check("SEQ1 TE", prog.TE > 0.003 && prog.TE < 0.012, $"TE {prog.TE * 1e3:F3} ms; program {prog.Blocks.Count} blocks, {prog.Duration:F1} s");
        // SEQ2: k at the ADC samples of the first imaging block.
        var blk = prog.Blocks[prog.SliceBlocks[0][0]]; double worst = 0; int n = p.Matrix;
        for (int j = 0; j < n; j++) worst = Math.Max(worst, Math.Abs(prog.MomentFromRfCentre(blk.Adc[j]).X - (j - n / 2) / Protocol.Fov));
        Check("SEQ2 kx(t_j) = (j - N/2)/FOV", worst < 1e-6, $"max err {worst:E2} 1/m");
        var b2 = prog.Blocks[prog.SliceBlocks[0][1]];
        double ky = prog.MomentFromRfCentre(b2.Adc[0]).Y, kyExpect = (b2.Row - n / 2) / Protocol.Fov;
        Check("SEQ2 ky of the second repetition", Math.Abs(ky - kyExpect) < 1e-6, $"{ky:F4} vs {kyExpect:F4} 1/m (row {b2.Row})");
        double kz = prog.MomentFromRfCentre(prog.Time("s0.r0.reph.end")).Z;
        Check("SEQ2 kz after the rephaser", Math.Abs(kz) < 1e-6, $"{kz:E2} 1/m");
        double maxI = 0, maxSlew = 0;
        foreach (var b in prog.Blocks)
            foreach (var s in b.Segs)
            {
                maxI = Math.Max(maxI, Math.Max(Math.Abs(s.X0), Math.Max(Math.Abs(s.Y0), Math.Abs(s.Z0))));
                if (s.Duration > 0) maxSlew = Math.Max(maxSlew, Math.Max(Math.Abs(s.X1 - s.X0) * Scanner.EtaX, Math.Max(Math.Abs(s.Y1 - s.Y0) * Scanner.EtaY, Math.Abs(s.Z1 - s.Z0) * Scanner.EtaZ)) / s.Duration);
            }
        Check("SEQ3 currents and slew within limits", maxI <= 100 && maxSlew <= 60.001, $"max {maxI:F1} A, {maxSlew:F1} T/m/s");
        var rows = prog.SliceBlocks[0].Select(i => prog.Blocks[i].Row).ToList();
        Check("SEQ4 every row acquired once", rows.Distinct().Count() == n && rows[0] == n / 2 && rows[1] == n / 2 + 16, $"{rows.Count} rows, first {rows[0]}, second {rows[1]}");
    }

    // ------------------------------------------------------------------------------------------------ lesson physics

    static double Tip(double[] x, double[] y, double[] pd, Func<int, bool> sel)
    {
        double a = 0, b = 0; for (int i = 0; i < x.Length; i++) if (sel(i) && pd[i] > 0) { a += Math.Sqrt(x[i] * x[i] + y[i] * y[i]); b += pd[i]; }
        return b > 0 ? a / b : 0;
    }
    static double Coh(double[] x, double[] y, Func<int, bool> sel)
    {
        double sx = 0, sy = 0, m = 0; for (int i = 0; i < x.Length; i++) if (sel(i)) { sx += x[i]; sy += y[i]; m += Math.Sqrt(x[i] * x[i] + y[i] * y[i]); }
        return m > 0 ? Math.Sqrt(sx * sx + sy * sy) / m : 0;
    }

    static void LessonTests()
    {
        var sw = Stopwatch.StartNew();
        var sim = new Simulation(Tables, Labels) { AddNoise = false, KeepSignalSets = true };
        var p = new Protocol();
        var sp = new HandSpecimen(Labels, Simulation.DefaultPose(Labels));
        var st = sim.BuildNow(p, sp, null, acquire: false);
        Console.WriteLine($"build {sw.Elapsed.TotalSeconds:F2} s: cube {st.Cube.N} at {st.CubeCentre}, hand {st.Hand.N}, blocks {st.Program.Blocks.Count}");
        var cube = st.Cube; var e = st.CubeEval; var prog = st.Program;
        sp.Bounds(out var lo, out var hi);
        Check("GRID 6x6x8", cube.N == 288 && cube.Nx == 6 && cube.Ny == 6 && cube.Nz == 8, "Regular spatial sampling");
        Check("SCAN whole-hand z coverage", prog.SliceZ.Min()-p.SliceThickness/2 <= lo.Z && prog.SliceZ.Max()+p.SliceThickness/2 >= hi.Z, $"{prog.SliceZ.Count} slices over [{lo.Z:F3}, {hi.Z:F3}] m");
        var classes = cube.Cls.GroupBy(c => c).Select(g => $"{g.Key} {g.Count()}");
        Check("LES cube tissue classes", cube.Cls.Distinct().Count(c => c != TissueClass.Air) >= 3, string.Join(", ", classes));
        Func<int, bool> mobile = i => cube.Pd[i] > 0 && cube.R2[i] < 1 / 0.005;
        // Equilibrium.
        sim.Update(0); double mz = 0, pd = 0; for (int i = 0; i < cube.N; i++) { mz += e.Z[i]; pd += cube.Pd[i]; }
        Check("LES equilibrium along +z", Math.Abs(mz / pd - 1) < 1e-9, $"mz/PD {mz / pd:F9}");
        // Hard pulse.
        sim.Update(prog.Time("hard1.rf.end"));
        Check("LES 3.6 hard pulse tips mobile spins to transverse", Tip(e.X, e.Y, cube.Pd, mobile) > 0.9, $"tip {Tip(e.X, e.Y, cube.Pd, mobile):F3}, coherence {Coh(e.X, e.Y, mobile):F3}");
        double my = 0; for (int i = 0; i < cube.N; i++) if (mobile(i)) my += e.Y[i];
        Check("LES 3.6 tipped toward +y'", my > 0, $"sum My {my:F2}");
        // Whole-hand FID and signal arrow.
        foreach (double ms in new[] { 0.05, 0.3, 1, 3, 10, 30, 99 })
        {
            double t = prog.Time("hard1.rf.end") + ms * 1e-3; sim.Update(t);
            Console.WriteLine($"   FID t+{ms,5} ms: signal {Math.Sqrt(sim.SignalRe * sim.SignalRe + sim.SignalIm * sim.SignalIm):F3} cube tip {Tip(e.X, e.Y, cube.Pd, mobile):F3} coh {Coh(e.X, e.Y, mobile):F3}");
        }
        // Gz demo.
        sim.Update(prog.Time("hard2.rf.end")); double s0 = Math.Sqrt(sim.SignalRe * sim.SignalRe + sim.SignalIm * sim.SignalIm);
        sim.Update(prog.Time("hard2.gap.start"));
        double turns = TwistTurns(cube, e, 2), side = Layouts.GridSize.Z, twist = 125 * side; // 125 turns per metre
        double sTw = Math.Sqrt(sim.SignalRe * sim.SignalRe + sim.SignalIm * sim.SignalIm);
        Check($"LES 5.4 Gz twist: {twist:F1} turns along z across the cube", Math.Abs(Math.Abs(turns) - twist) < 0.075 * twist, $"{turns:F3} turns; signal {sTw:F3} of {s0:F3}");
        sim.Update(prog.Time("hard2.echo")); double sEcho = Math.Sqrt(sim.SignalRe * sim.SignalRe + sim.SignalIm * sim.SignalIm);
        Check("LES 5.8 echo returns", sEcho > 3 * sTw, $"echo {sEcho:F3}, twisted {sTw:F3}, cube coherence {Coh(e.X, e.Y, mobile):F3}");
        // Slice selection.
        var df = st.CubeDfSlice; double bw = p.BandwidthEff;
        // The grid's layers (0.8.3): two inside the slab (|z| = 1.5 mm, 0.3 of the bandwidth from its centre), two outside (4.5 mm).
        Func<int, bool> core = i => mobile(i) && cube.Shift[i] == 0 && Math.Abs(df[i]) < 0.35 * bw;
        Func<int, bool> far = i => mobile(i) && cube.Shift[i] == 0 && Math.Abs(df[i]) > 0.85 * bw;
        Func<int, bool> band = i => mobile(i) && Math.Abs(df[i]) <= bw / 2;
        Func<int, bool> waterBand = i => band(i) && cube.Shift[i] == 0;
        sim.Update(prog.Time("s0.r0.ss.end"));
        Check("LES 6.5/6.6 slice pulse: core tipped, far untouched", Tip(e.X, e.Y, cube.Pd, core) > 0.7 && Tip(e.X, e.Y, cube.Pd, far) < 0.05,
            $"core {Tip(e.X, e.Y, cube.Pd, core):F3}, far {Tip(e.X, e.Y, cube.Pd, far):F4}, band coherence {Coh(e.X, e.Y, band):F3}");
        sim.Update(prog.Time("s0.r0.reph.end"));
        Check("LES 6.10 rephaser realigns the band (water)", Coh(e.X, e.Y, waterBand) > 0.95, $"water coherence {Coh(e.X, e.Y, waterBand):F3}, with fat {Coh(e.X, e.Y, band):F3}");
        int bandCount = 0, excited = 0, outside = 0, outsideExcited = 0;
        for (int i = 0; i < cube.N; i++) if (mobile(i))
            {
                double tip = Math.Sqrt(e.X[i] * e.X[i] + e.Y[i] * e.Y[i]) / cube.Pd[i];
                if (Math.Abs(df[i]) <= bw / 2) { bandCount++; if (tip >= 0.5) excited++; } else if (Math.Abs(df[i]) > bw) { outside++; if (tip >= 0.5) outsideExcited++; }
            }
        Check("REF1 band excited, outside not", excited >= 0.9 * bandCount && outsideExcited <= 0.02 * outside, $"{excited}/{bandCount} in band, {outsideExcited}/{outside} outside");
        sim.Update(prog.Time("s0.r0.pre.end"));
        double tx = TwistTurns(cube, e, 0);
        double expectX = -(p.Matrix / 2 + 0.5) * Layouts.GridSize.X / Protocol.Fov;
        Check("LES 7.2 prephaser winds x", Math.Abs(tx / expectX - 1) < 0.08, $"{tx:F3} turns vs {expectX:F3}");
        sim.Update(prog.Time("s0.r0.echo")); double echo = Math.Sqrt(sim.SignalRe * sim.SignalRe + sim.SignalIm * sim.SignalIm);
        // Water realigns at the echo; fat, shifted 3.4 ppm, is a fraction of a turn away (0.8.3: the 100-cell grid is 41 % fat).
        Check("LES 7.6 echo at the readout centre (water)", Coh(e.X, e.Y, waterBand) > 0.9, $"water coherence {Coh(e.X, e.Y, waterBand):F3}, with fat {Coh(e.X, e.Y, band):F3}, signal {echo:F3} (no acquisition in this probe)");
        sim.Update(prog.Time("s0.r1.pe.end"));
        double ty = TwistTurns(cube, e, 1);
        double pe = 16 / Protocol.Fov * Layouts.GridSize.Y;
        Check($"LES 8.4 phase encode row +16: {pe:F2} turns along y across the cube", Math.Abs(ty - pe) < 0.08 * pe, $"{ty:F3} turns");
        Console.WriteLine($"   lesson probe {sw.Elapsed.TotalSeconds:F2} s");
        // Per-frame cost of evaluating both display sets at pulse-speed times inside one repetition (main-thread path).
        {
            double ta = prog.Time("s0.r0.ss.start"), tb = prog.Time("s0.r0.ro.end");
            // Process CPU time, not wall time: the build server is shared, and other jobs inflate wall-clock timings.
            sim.Update(ta); var proc = System.Diagnostics.Process.GetCurrentProcess(); var cpu0 = proc.TotalProcessorTime; var fw = Stopwatch.StartNew(); int frames = 400;
            for (int f = 0; f < frames; f++) sim.Update(ta + (tb - ta) * f / frames);
            proc.Refresh(); double ms = (proc.TotalProcessorTime - cpu0).TotalMilliseconds / frames, wall = fw.Elapsed.TotalMilliseconds / frames;
            Check("PERF per-frame evaluation of cube + hand sets (server core, CPU time)", ms < 2.0, $"{ms:F3} ms CPU per frame ({wall:F3} ms wall) for {cube.N + st.Hand.N} isochromats");
        }

        // Acquisition of the first slice and its image.
        sw.Restart();
        var st2 = sim.BuildNow(p, sp, null, acquire: false);
        sim.Acquire(st2);
        var acq = st2.Slices[0];
        Console.WriteLine($"   acquisition of {st2.Slices.Length} slices: {sw.Elapsed.TotalSeconds:F1} s; slice 0 signal set {acq.Set.N}");
        var img = acq.Reconstruct(double.PositiveInfinity, out int rowsDone);
        Check("SIG image reconstructs every row", rowsDone == p.Matrix, $"{rowsDone} rows");
        WritePgm(Path.Combine(Root, "validation/sim-slice0.pgm"), img, p.Matrix);
        // RX1: the same readout through the modelled MaRCoS chain (direct RF sampling at 122.88 MS/s, 16-bit ADC, NCO,
        // CIC, FIR) against the runtime's direct reciprocity samples (noise off in both).
        {
            var rxSw = Stopwatch.StartNew();
            var set = acq.Set; var prog2 = st2.Program; int b0 = prog2.SliceBlocks[0][0]; var blk = prog2.Blocks[b0];
            var mapCache = new Dictionary<string, AffineMap>();
            Func<Block, AffineMap> maps = bb =>
            {
                if (bb.Selective && bb.Slice != 0) return null;
                string key = bb.Rf.Rf.Key + "|" + bb.Rf.Z0;
                if (!mapCache.TryGetValue(key, out var m)) mapCache[key] = m = Bloch.Map(set, bb.Rf.Rf, new Currents(p.MagnetCurrent, 0, 0, bb.Rf.Z0));
                return m;
            };
            var chainEval = new Evaluator(set, prog2, bb => maps(bb) ?? RelaxOnlyMap(set, bb));
            var rx = new Receiver(set, prog2, bb => maps(bb) ?? RelaxOnlyMap(set, bb), false, 1);
            var uz = chainEval.UzAtBlockStart(b0);
            var fast = rx.Record(b0, uz, false);
            // ADC start chosen so that the decimated 192 kS/s outputs (CIC group delay 1917 input samples) fall exactly on
            // the 32 kS/s sample instants.
            double fmid = Marcos.Fs / Marcos.CicDecimation, t0 = blk.Adc[0] - 115 / fmid - 1917 / Marcos.Fs, t1 = blk.Adc[blk.Adc.Length - 1] + 0.6e-3;
            int nEnv = (int)((t1 - t0) * fmid) + 1; var times = new double[nEnv];
            for (int i = 0; i < nEnv; i++) times[i] = t0 + i / fmid;
            var env = rx.Record(b0, uz, false, times, passband: false);
            Marcos.Process(env.Re, env.Im, t0, p.Fref, blk.Adc, out var oRe, out var oIm, out double gain);
            double num = 0, peak = 0;
            for (int j = 0; j < oRe.Length; j++)
            {
                double dr = oRe[j] - fast.Re[j], di = oIm[j] - fast.Im[j]; num += dr * dr + di * di;
                peak = Math.Max(peak, Math.Sqrt(fast.Re[j] * fast.Re[j] + fast.Im[j] * fast.Im[j]));
            }
            double rms = Math.Sqrt(num / oRe.Length) / peak;
            Check("RX1 MaRCoS chain (122.88 MS/s, 16-bit, NCO, CIC 640x, FIR 6x) equals the direct samples", rms < 0.005, $"RMS difference {rms * 100:F3} % of the echo peak {peak * 1e6:F1} uV; receiver gain {gain:F0}; {rxSw.Elapsed.TotalSeconds:F1} s");
            var cic = Marcos.CicKernel(); double sum = 0; foreach (var h in cic) sum += h;
            Check("RX3 CIC kernel: 3835 taps summing to R^6", cic.Length == 3835 && Math.Abs(sum / Math.Pow(640, 6) - 1) < 1e-12, $"{cic.Length} taps, sum/R^6 = {sum / Math.Pow(640, 6):F12}");
        }
        int peakRow = 0; double best = 0;
        for (int r = 0; r < p.Matrix; r++) { double m = 0; for (int j = 0; j < p.Matrix; j++) m += acq.KRe[r * p.Matrix + j] * acq.KRe[r * p.Matrix + j] + acq.KIm[r * p.Matrix + j] * acq.KIm[r * p.Matrix + j]; if (m > best) { best = m; peakRow = r; } }
        Check("SIG k-space energy peaks at the centre row", Math.Abs(peakRow - p.Matrix / 2) <= 1, $"row {peakRow}");
    }

    static AffineMap RelaxOnlyMap(IsoSet set, Block b)
    {
        var m = new AffineMap { Ax = new double[set.N], Ay = new double[set.N], Az = new double[set.N], Bx = new double[set.N], By = new double[set.N], Bz = new double[set.N] };
        double d = b.Rf.Rf.Duration;
        for (int i = 0; i < set.N; i++) { double e1 = Math.Exp(-d * set.R1[i]); m.Az[i] = e1; m.Bz[i] = set.Pd[i] * (1 - e1); }
        return m;
    }

    static void WritePgm(string path, double[] img, int n)
    {
        double max = img.Max(); var sb = new List<byte>(Encoding.ASCII.GetBytes($"P5 {n} {n} 255\n"));
        for (int y = n - 1; y >= 0; y--) for (int x = 0; x < n; x++) sb.Add((byte)Math.Min(255, 255 * img[y * n + x] / max));
        File.WriteAllBytes(path, sb.ToArray());
    }

    /// <summary>
    /// Turns of transverse phase across the proton volume along one axis (0 x, 1 y, 2 z): the spatial frequency k that best
    /// straightens the water protons' phases (maximum coherence of m_perp e^(i 2 pi k x)), times the block's extent. The
    /// protons sit at random positions, so neighbours are not a lattice; fat (3.4 ppm) is left out.
    /// </summary>
    static double TwistTurns(IsoSet cube, Evaluator e, int axis)
    {
        D3 size = Layouts.GridSize; double L = axis == 0 ? size.X : axis == 1 ? size.Y : size.Z;
        double bestK = 0, bestC = -1;
        // A regular lattice aliases k at 1/spacing; fit only its Nyquist interval.
        double spacing = axis == 0 ? cube.CellX : axis == 1 ? cube.CellY : cube.CellZ;
        double limit = cube.Nx > 0 ? 0.5 / spacing - 0.5 : 800;
        for (double k = -limit; k <= limit; k += 0.5)
        {
            double re = 0, im = 0, w = 0;
            for (int i = 0; i < cube.N; i++)
            {
                if (cube.Pd[i] <= 0 || cube.Shift[i] != 0 || cube.R2[i] > 1 / 0.005) continue;
                double m = Math.Sqrt(e.X[i] * e.X[i] + e.Y[i] * e.Y[i]); if (m < 1e-9) continue;
                double x = axis == 0 ? cube.Pos[i].X : axis == 1 ? cube.Pos[i].Y : cube.Pos[i].Z;
                double a = Math.Atan2(e.Y[i], e.X[i]) + 2 * Math.PI * k * x;
                re += m * Math.Cos(a); im += m * Math.Sin(a); w += m;
            }
            double c = w > 0 ? Math.Sqrt(re * re + im * im) / w : 0;
            if (c > bestC) { bestC = c; bestK = k; }
        }
        return bestK * L;
    }
}
