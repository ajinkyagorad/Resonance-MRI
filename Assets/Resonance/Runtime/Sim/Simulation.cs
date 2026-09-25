using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nebulytic.Resonance.Sim
{
    /// <summary>
    /// Everything derived from one set of inputs (protocol, specimen pose, cube region): the program, the display sets
    /// with their RF maps, the whole-specimen receiver records and the slice acquisitions. Built off the main thread.
    /// </summary>
    /// <summary>
    /// Parallelism of the background rebuild and acquisition (0.8.2). Their parallel loops run on the thread pool at normal
    /// priority, so on the headset they would take the cores the main and render threads need, for minutes after start and
    /// after every setting change. The app caps them there (App.Initialize); tests and the server use every core.
    /// </summary>
    public static class Work
    {
        public static readonly ParallelOptions Options = new ParallelOptions();
        /// <summary>Largest number of threads a parallel loop of the simulation may use (0 or less: no limit).</summary>
        public static int MaxThreads { get => Options.MaxDegreeOfParallelism; set => Options.MaxDegreeOfParallelism = value <= 0 ? -1 : value; }
    }

    public sealed class SimState
    {
        public int Revision; public Protocol P; public SeqProgram Program; public ISpecimen Specimen; public FieldTables Tables;
        public IsoSet Cube, Hand; public Evaluator CubeEval, HandEval; public D3 CubeCentre;
        public readonly Dictionary<string, AffineMap> CubeMaps = new Dictionary<string, AffineMap>(), HandMaps = new Dictionary<string, AffineMap>();
        public readonly Dictionary<int, BlockRecord> HandRecords = new Dictionary<int, BlockRecord>();
        public SliceAcquisition[] Slices;
        public double[] CubeDfSlice; // slice-select frequency offset of each cube isochromat minus the RF offset (Hz)
        public double[] HandDfSlice; public double HandB1Norm;
        public volatile bool AcquisitionFinished; public volatile string Error;

        static string MapKey(Block b) => b.Rf.Rf.Key + "|" + b.Rf.Z0.ToString("R");

        public AffineMap CubeMap(Block b) => Get(CubeMaps, Cube, b, double.PositiveInfinity);
        public AffineMap HandMap(Block b) => Get(HandMaps, Hand, b, b.Selective ? 4 * P.BandwidthEff : double.PositiveInfinity);

        AffineMap Get(Dictionary<string, AffineMap> cache, IsoSet set, Block b, double far)
        {
            string key = MapKey(b);
            lock (cache)
            {
                if (cache.TryGetValue(key, out var m)) return m;
            }
            double offset = b.Selective ? Constants.GammaBar * P.Gss * Program.SliceZ[b.Slice] : 0;
            var c = new Currents(P.MagnetCurrent, 0, 0, b.Rf.Z0);
            var map = Bloch.Map(set, b.Rf.Rf, c, offset, far);
            lock (cache) cache[key] = map;
            return map;
        }
    }

    /// <summary>Choice of where the magnified cube sits and which slices the volume step acquires.</summary>
    public static class Layouts
    {
        // The proton block (0.8.4): 100 isochromats at random positions in the tissue of a 5 x 5 x 10 mm block centred on the
        // slice, one proton drawn per isochromat. It is displayed with one uniform magnification, so the block and its outline
        // in the scanner have exactly the same shape. In-plane 5 mm keeps the readout's phase twist to about a turn across it;
        // through the slice, 10 mm holds the 5 mm slab and 2.5 mm of tissue outside it on each side.
        public const int GridX = 6, GridY = 6, GridZ = 8, ProtonCount = GridX * GridY * GridZ; public const double BlockX = 0.005, BlockY = 0.005, BlockZ = 0.010, HandCell = 0.004;
        /// <summary>Mean spacing between protons (m): the cube root of the block's volume per proton, also each one's display cell.</summary>
        public static double ProtonSpacing => Math.Pow(BlockX * BlockY * BlockZ / ProtonCount, 1.0 / 3);
        /// <summary>Smallest separation between protons, as a share of the mean spacing.</summary>
        public const double ProtonSeparation = 0.55;
        /// <summary>Physical extent of the proton block along x, y, z (m).</summary>
        public static D3 GridSize => new D3(BlockX, BlockY, BlockZ);
        public static readonly double[] VolumeSlices = { -0.040, -0.030, -0.020, -0.010, 0.0, 0.010, 0.020, 0.030, 0.040 };

        public static double[] FullHandSlices(ISpecimen specimen, Protocol p)
        {
            specimen.Bounds(out D3 lo, out D3 hi);
            double dz = p.SliceThickness;
            int first = (int)Math.Floor((lo.Z - p.SliceZ) / dz + 0.5);
            int last = (int)Math.Ceiling((hi.Z - p.SliceZ) / dz - 0.5);
            var slices = new double[last - first + 1];
            for (int k = 0; k < slices.Length; k++) slices[k] = p.SliceZ + (first + k) * dz;
            return slices;
        }

        /// <summary>In-plane cube position on a 4 mm grid inside the RF coil that shows the most distinct tissues.</summary>
        public static D3 ChooseCube(ISpecimen sp, double z) => ChooseCube(sp, z, true) is D3 c && c.X != double.MaxValue ? c : ChooseCube(sp, z, false);

        static D3 ChooseCube(ISpecimen sp, double z, bool needBone)
        {
            double best = -1; D3 bestC = needBone ? new D3(double.MaxValue, 0, z) : new D3(0, 0, z);
            D3 size = GridSize; double hx = size.X / 2, hy = size.Y / 2, hz = size.Z / 2;
            for (double x = -0.064; x <= 0.064 + 1e-9; x += 0.002)
                for (double y = -0.040; y <= 0.040 + 1e-9; y += 0.002)
                {
                    if (Math.Sqrt(x * x + y * y) + Math.Sqrt(hx * hx + hy * hy) > 0.072) continue;
                    var seen = new bool[TissueTable.Count]; int tissue = 0, total = 0, cortex = 0;
                    // Tissue sampled on a 1 mm lattice through the block.
                    int nx = (int)Math.Round(size.X / 0.001), ny = (int)Math.Round(size.Y / 0.001), nz = (int)Math.Round(size.Z / 0.001);
                    for (int iz = 0; iz < nz; iz++)
                        for (int iy = 0; iy < ny; iy++)
                            for (int ix = 0; ix < nx; ix++)
                            {
                                var c = sp.ClassAt(new D3(x + (ix + 0.5) * 0.001 - hx, y + (iy + 0.5) * 0.001 - hy, z + (iz + 0.5) * 0.001 - hz));
                                total++; if (c != TissueClass.Air) { tissue++; seen[(int)c] = true; } if (c == TissueClass.Cortex || c == TissueClass.Tendon) cortex++;
                            }
                    int distinct = 0; for (int k = 1; k < seen.Length; k++) if (seen[k]) distinct++;
                    double frac = (double)tissue / total, bone = (double)cortex / total;
                    // Mostly signal-bearing tissue: cortical bone (T2* 0.4 ms) and tendon keep no transverse magnetization.
                    if (frac < 0.9 || needBone && (bone > 0.12 || bone < 0.02)) continue;
                    double score = distinct + 0.5 * frac - 4 * bone - 0.1 * Math.Sqrt(x * x + y * y) / 0.072;
                    if (score > best) { best = score; bestC = new D3(x, y, z); }
                }
            return bestC;
        }
    }

    /// <summary>
    /// The simulation facade used by the scene. One physical clock t; all outputs are evaluated at the same t.
    /// </summary>
    public sealed class Simulation
    {
        public readonly FieldTables Tables; public readonly HandLabels Labels;
        public SimState State { get; private set; }
        public bool Busy => pending != null;
        public bool AddNoise = true;
        /// <summary>Keep each slice's signal set after its acquisition (tests read it; the app releases it).</summary>
        public bool KeepSignalSets;
        public int WorkerThreads = 2;
        Thread worker; SimState pending; int revision;

        // Per-frame outputs.
        public double T; public double Ix, Iy, Iz; public bool RfOn; public double RfRe, RfIm; public D3 K;
        public bool SignalOn; public double SignalRe, SignalIm; // receiver output normalised by the block's in-phase reference
        public double Emf; // instantaneous coil EMF relative to the fully coherent specimen (0..1)
        public int BlockIndex = -1;

        // Spin evaluation (0.8.1). Synchronous (validation, fixed step): evaluated on the calling thread inside Update.
        // Otherwise a dedicated worker evaluates the cube and hand sets while the main thread renders; the views read the
        // evaluators' arrays only when the worker is idle (SpinsReady), one frame after the request (Kick).
        public bool Synchronous = true;
        public double EvalT { get; private set; } = double.NaN;
        public int EvalRevision { get; private set; } = -1;
        public D3 EvalK { get; private set; }
        /// <summary>Set when the hand arrays were refreshed; the consumer (glow upload) clears it.</summary>
        public volatile bool HandFresh;
        /// <summary>Runs on the evaluating thread after each hand evaluation (fills the glow texture data).</summary>
        public Action<SimState> AfterHand;
        /// <summary>Duration of the last spin evaluation (ms), for the frame-cost report.</summary>
        public double SpinMs, SpinHandMs;
        Thread spinThread; readonly object spinGate = new object(); bool spinPending, spinRunning, spinQuit; double spinT; bool spinHand; SimState spinState;

        public Simulation(FieldTables tables, HandLabels labels) { Tables = tables; Labels = labels; }

        public static SpecimenPose DefaultPose(HandLabels labels)
        {
            // Mid-metacarpals (z_obj = +70 mm) at the isocentre; in-plane centred on the tissue centroid of that slab.
            double zo = 0.070, sx = 0, sy = 0; int n = 0;
            int iz0 = (int)Math.Floor((zo - 0.005 - labels.Corner.Z) / labels.Pitch), iz1 = (int)Math.Ceiling((zo + 0.005 - labels.Corner.Z) / labels.Pitch);
            for (int iz = Math.Max(0, iz0); iz < Math.Min(labels.Nz, iz1); iz++)
                for (int iy = 0; iy < labels.Ny; iy++)
                    for (int ix = 0; ix < labels.Nx; ix++)
                        if (labels.Labels[(iz * labels.Ny + iy) * labels.Nx + ix] != 0) { var c = labels.Centre(ix, iy, iz); sx += c.X; sy += c.Y; n++; }
            return new SpecimenPose { ObjectAtIsocentre = new D3(n > 0 ? sx / n : 0, n > 0 ? sy / n : 0, zo) };
        }

        /// <summary>Builds a complete state synchronously (tests, headless) and makes it current.</summary>
        public SimState BuildNow(Protocol p, ISpecimen sp, D3? cube = null, bool acquire = true)
        {
            var s = Build(++revision, p.Clone(), sp, cube);
            if (acquire) Acquire(s);
            State = s; return s;
        }

        /// <summary>Starts a background rebuild; the current state stays in use until the new one is ready (see Poll).</summary>
        public void Request(Protocol p, ISpecimen sp, D3? cube)
        {
            int rev = ++revision; var pc = p.Clone();
            var t = new Thread(() =>
            {
                try
                {
                    var s = Build(rev, pc, sp, cube);
                    lock (this) { if (rev == revision) pending = s; }
                    Acquire(s);
                }
                catch (Exception e) { lock (this) { if (rev == revision) { pending = null; } } Console.Error.WriteLine(e); }
            }) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "Resonance simulation " + rev };
            worker = t; t.Start();
        }

        /// <summary>Swaps in a finished rebuild. Returns true when the state changed.</summary>
        public bool Poll()
        {
            lock (this)
            {
                if (pending == null) return false;
                State = pending; pending = null; return true;
            }
        }

        public bool Rebuilding { get { lock (this) return worker != null && worker.IsAlive && pending == null && (State == null || State.Revision != revision); } }

        SimState Build(int rev, Protocol p, ISpecimen sp, D3? cubeCentre)
        {
            var s = new SimState { Revision = rev, P = p, Specimen = sp, Tables = Tables };
            s.Program = SeqProgram.Lesson(p, Layouts.FullHandSlices(sp, p));
            s.CubeCentre = cubeCentre ?? Layouts.ChooseCube(sp, p.SliceZ);
            s.CubeCentre = new D3(s.CubeCentre.X, s.CubeCentre.Y, p.SliceZ);
            s.Cube = IsoSet.Grid(s.CubeCentre, Layouts.GridX, Layouts.GridY, Layouts.GridZ, Layouts.BlockX / Layouts.GridX, Layouts.BlockY / Layouts.GridY, Layouts.BlockZ / Layouts.GridZ, sp, Tables, p);
            s.Hand = IsoSet.Hand(sp, Tables, p, Layouts.HandCell);
            var css = new Currents(p.MagnetCurrent, 0, 0, p.GzCurrent); double off = p.SliceOffsetHz;
            s.CubeDfSlice = new double[s.Cube.N]; for (int i = 0; i < s.Cube.N; i++) s.CubeDfSlice[i] = s.Cube.DeltaF(i, css) - off;
            s.HandDfSlice = new double[s.Hand.N]; for (int i = 0; i < s.Hand.N; i++) s.HandDfSlice[i] = s.Hand.DeltaF(i, css) - off;
            // RF maps for every distinct pulse of the program (display sets).
            var seen = new HashSet<string>();
            foreach (var b in s.Program.Blocks)
            {
                string key = b.Rf.Rf.Key + "|" + b.Rf.Z0.ToString("R");
                if (!seen.Add(key)) continue;
                s.CubeMap(b); s.HandMap(b);
            }
            s.CubeEval = new Evaluator(s.Cube, s.Program, s.CubeMap);
            s.HandEval = new Evaluator(s.Hand, s.Program, s.HandMap);
            // Whole-specimen receiver records for the non-selective blocks.
            var rx = new Receiver(s.Hand, s.Program, s.HandMap, AddNoise, rev);
            for (int b = 0; b < s.Program.Blocks.Count; b++)
                if (!s.Program.Blocks[b].Selective) s.HandRecords[b] = rx.Record(b, s.HandEval.UzAtBlockStart(b), true);
            s.Slices = new SliceAcquisition[s.Program.SliceZ.Count];
            for (int k = 0; k < s.Slices.Length; k++) s.Slices[k] = new SliceAcquisition(k, p.Matrix, s.Program.SliceZ[k]);
            return s;
        }

        /// <summary>Runs every slice acquisition in time order (called on a worker thread or synchronously).</summary>
        public void Acquire(SimState s)
        {
            try
            {
                var prog = s.Program; var p = s.P;
                for (int k = 0; k < s.Slices.Length; k++)
                {
                    if (s.Revision != Volatile.Read(ref revision)) return; // superseded by a newer setting: stop (0.8.2)
                    var acq = s.Slices[k];
                    var set = IsoSet.Signal(s.Specimen, Tables, p, acq.Z);
                    acq.Set = set;
                    var sliceMaps = new Dictionary<string, AffineMap>(); var relaxMaps = new Dictionary<double, AffineMap>();
                    Func<Block, AffineMap> maps = b =>
                    {
                        // Another slice's pulse only lets this slice relax: one shared map per pulse length (0.8.2; it was
                        // rebuilt for every block the chain walked, about 4 GB of garbage per acquisition).
                        if (b.Selective && b.Slice != k)
                            lock (relaxMaps) { if (!relaxMaps.TryGetValue(b.Rf.Rf.Duration, out var r)) relaxMaps[b.Rf.Rf.Duration] = r = RelaxOnly(set, b); return r; }
                        string key = b.Rf.Rf.Key + "|" + b.Rf.Z0.ToString("R");
                        lock (sliceMaps)
                        {
                            if (sliceMaps.TryGetValue(key, out var m)) return m;
                            m = Bloch.Map(set, b.Rf.Rf, new Currents(p.MagnetCurrent, 0, 0, b.Rf.Z0));
                            sliceMaps[key] = m; return m;
                        }
                    };
                    var chain = new Evaluator(set, prog, maps);
                    var rx = new Receiver(set, prog, maps, AddNoise, s.Revision * 64 + k);
                    foreach (int b in prog.SliceBlocks[k])
                    {
                        if (s.Revision != Volatile.Read(ref revision)) return;
                        var blk = prog.Blocks[b];
                        var rec = rx.Record(b, chain.UzAtBlockStart(b), false);
                        int row = blk.Row, n = acq.N;
                        for (int j = 0; j < n && j < rec.Re.Length; j++)
                        {
                            acq.KRe[row * n + j] = rec.Re[j]; acq.KIm[row * n + j] = rec.Im[j]; acq.SampleTime[row * n + j] = rec.Times[j];
                            acq.MaxAbs = Math.Max(acq.MaxAbs, Math.Sqrt(rec.Re[j] * rec.Re[j] + rec.Im[j] * rec.Im[j]));
                        }
                        acq.Reference = Math.Max(acq.Reference, rec.Reference);
                        acq.RowTime[row] = rec.Times[rec.Times.Length - 1];
                        Interlocked.Increment(ref acq.RowsComputed);
                    }
                    acq.Done = true;
                    if (!KeepSignalSets) acq.Set = null; // about 10 MB per slice that nothing reads once its rows exist
                }
                s.AcquisitionFinished = true;
            }
            catch (Exception e) { s.Error = e.ToString(); }
        }

        static AffineMap RelaxOnly(IsoSet set, Block b)
        {
            var m = new AffineMap { Ax = new double[set.N], Ay = new double[set.N], Az = new double[set.N], Bx = new double[set.N], By = new double[set.N], Bz = new double[set.N] };
            double d = b.Rf.Rf.Duration;
            for (int i = 0; i < set.N; i++) { double e1 = Math.Exp(-d * set.R1[i]); m.Az[i] = e1; m.Bz[i] = set.Pd[i] * (1 - e1); }
            return m;
        }

        /// <summary>Evaluates everything at physical time t.</summary>
        public void Update(double t, bool evaluateHand = true)
        {
            var s = State; T = t;
            s.Program.CurrentsAt(t, out Ix, out Iy, out Iz);
            RfOn = s.Program.RfAt(t, out RfRe, out RfIm);
            K = s.Program.MomentFromRfCentre(t);
            if (Synchronous) EvaluateSpins(s, t, evaluateHand);
            BlockIndex = s.Program.BlockAt(t);
            SignalOn = false; SignalRe = SignalIm = 0;
            if (BlockIndex >= 0)
            {
                var blk = s.Program.Blocks[BlockIndex];
                if (blk.Adc.Length > 0 && t >= blk.Adc[0] && t <= blk.Adc[blk.Adc.Length - 1] + (blk.Adc.Length > 1 ? blk.Adc[1] - blk.Adc[0] : 0))
                {
                    int j = LastSample(blk.Adc, t);
                    if (!blk.Selective && s.HandRecords.TryGetValue(BlockIndex, out var rec) && rec.Reference > 0)
                    { SignalOn = true; SignalRe = rec.Re[j] / rec.Reference; SignalIm = rec.Im[j] / rec.Reference; }
                    else if (blk.Selective)
                    {
                        var acq = s.Slices[blk.Slice]; int k = blk.Row * acq.N + j;
                        if (acq.SampleTime[k] <= t && acq.Reference > 0)
                        { SignalOn = true; SignalRe = acq.KRe[k] / acq.Reference; SignalIm = acq.KIm[k] / acq.Reference; }
                    }
                }
            }
        }

        /// <summary>Evaluates the cube (and optionally the hand) set at t on the calling thread.</summary>
        void EvaluateSpins(SimState s, double t, bool hand)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            s.CubeEval.Evaluate(t);
            s.CubeEval.EvaluateFree(t); // free-precession phase of every cube cell (moments that are not tipped still turn)
            var k = s.Program.MomentFromRfCentre(t);
            double cubeMs = sw.Elapsed.TotalMilliseconds;
            if (hand)
            {
                s.HandEval.Evaluate(t);
                // Instantaneous EMF of the RF coil by reciprocity (sum of conj(b1) u_perp over the whole specimen),
                // relative to the fully coherent sum; the coil's receive glow follows it.
                var h = s.Hand; var e = s.HandEval; double re = 0, im = 0;
                if (s.HandB1Norm <= 0) { double norm = 0; for (int i = 0; i < h.N; i++) norm += Math.Sqrt(h.F[i].RxRe * h.F[i].RxRe + h.F[i].RxIm * h.F[i].RxIm) * h.Pd[i]; s.HandB1Norm = norm; }
                for (int i = 0; i < h.N; i++) { double br = h.F[i].RxRe, bi = h.F[i].RxIm; re += br * e.X[i] + bi * e.Y[i]; im += br * e.Y[i] - bi * e.X[i]; }
                double c = h.CellX, cell = Numerics.Sinc(k.X * c) * Numerics.Sinc(k.Y * c) * Numerics.Sinc(k.Z * c);
                Emf = s.HandB1Norm > 0 ? Math.Sqrt(re * re + im * im) / s.HandB1Norm * Math.Abs(cell) : 0;
                AfterHand?.Invoke(s);
                SpinHandMs = sw.Elapsed.TotalMilliseconds - cubeMs;
            }
            EvalK = k; EvalT = t; EvalRevision = s.Revision;
            if (hand) HandFresh = true;
            SpinMs = sw.Elapsed.TotalMilliseconds;
        }

        /// <summary>True when the evaluators hold a finished result for the current state and nothing is evaluating.</summary>
        public bool SpinsReady
        {
            get
            {
                if (State == null) return false;
                if (Synchronous) return EvalRevision == State.Revision;
                lock (spinGate) return !spinPending && !spinRunning && EvalRevision == State.Revision;
            }
        }

        /// <summary>True when no evaluation is queued or running (a new one may be requested).</summary>
        public bool SpinsIdle { get { if (Synchronous) return true; lock (spinGate) return !spinPending && !spinRunning; } }

        /// <summary>Asynchronous mode: evaluate the spins at t on the worker. Call after this frame's views read the previous result.</summary>
        public void Kick(double t, bool hand)
        {
            if (Synchronous || State == null) return;
            lock (spinGate)
            {
                if (spinPending || spinRunning) return;
                spinT = t; spinHand = hand; spinState = State; spinPending = true;
                if (spinThread == null) { spinThread = new Thread(SpinLoop) { IsBackground = true, Name = "Resonance spins" }; spinThread.Start(); }
                Monitor.Pulse(spinGate);
            }
        }

        void SpinLoop()
        {
            while (true)
            {
                SimState s; double t; bool hand;
                lock (spinGate)
                {
                    while (!spinPending && !spinQuit) Monitor.Wait(spinGate);
                    if (spinQuit) return;
                    spinPending = false; spinRunning = true; s = spinState; t = spinT; hand = spinHand;
                }
                try { EvaluateSpins(s, t, hand); }
                catch (Exception e) { Console.Error.WriteLine(e); }
                finally { lock (spinGate) spinRunning = false; }
            }
        }

        /// <summary>Stops the spin worker (application quit).</summary>
        public void StopWorker() { lock (spinGate) { spinQuit = true; Monitor.Pulse(spinGate); } }

        static int LastSample(double[] adc, double t)
        {
            int lo = 0, hi = adc.Length - 1, ans = 0;
            while (lo <= hi) { int mid = (lo + hi) >> 1; if (adc[mid] <= t) { ans = mid; lo = mid + 1; } else hi = mid - 1; }
            return ans;
        }

        /// <summary>Display-cell average factor for the grid's isochromats at the current time (net moment of a uniform cell).</summary>
        public double CubeCellFactor
        {
            get { double h = Layouts.ProtonSpacing; return Numerics.Sinc(EvalK.X * h) * Numerics.Sinc(EvalK.Y * h) * Numerics.Sinc(EvalK.Z * h); }
        }
    }
}
