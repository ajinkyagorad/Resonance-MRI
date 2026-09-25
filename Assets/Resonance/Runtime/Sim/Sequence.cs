using System;
using System.Collections.Generic;

namespace Nebulytic.Resonance.Sim
{
    public enum SegKind { Free, Rf }

    /// <summary>An RF pulse: rung-current phasor A per 10 us raster step, in the frame rotating at f_ref.</summary>
    public sealed class RfPulse
    {
        public string Key; public int Steps; public double Dt; public double[] Re, Im; public double CentreFromStart; public double Peak;
        public double Duration => Steps * Dt;

        /// <summary>Rectangular pulse of flip angle 'flip' (radians) and duration 'duration'.</summary>
        public static RfPulse Hard(double flip, double duration)
        {
            int n = (int)Math.Round(duration / Protocol.Raster);
            double amp = flip / Constants.TwoPi / (Constants.GammaBar * Scanner.B1Iso * n * Protocol.Raster);
            var p = new RfPulse { Key = $"hard:{flip:R}:{n}", Steps = n, Dt = Protocol.Raster, Re = new double[n], Im = new double[n], CentreFromStart = 0.5 * n * Protocol.Raster, Peak = amp };
            for (int k = 0; k < n; k++) p.Re[k] = amp;
            return p;
        }

        /// <summary>Hamming-windowed sinc of time-bandwidth tbw, modulated to excite the plane whose offset is offsetHz.</summary>
        public static RfPulse Sinc(double flip, double duration, double tbw, double offsetHz)
        {
            int n = (int)Math.Round(duration / Protocol.Raster);
            double dt = Protocol.Raster, area = 0; var env = new double[n];
            for (int k = 0; k < n; k++)
            {
                double s = (k + 0.5) / n - 0.5;
                env[k] = Numerics.Sinc(tbw * s) * (0.54 + 0.46 * Math.Cos(Constants.TwoPi * s));
                area += env[k] * dt;
            }
            double amp = flip / Constants.TwoPi / (Constants.GammaBar * Scanner.B1Iso * area);
            var p = new RfPulse { Key = $"sinc:{flip:R}:{n}:{tbw:R}:{offsetHz:R}", Steps = n, Dt = dt, Re = new double[n], Im = new double[n], CentreFromStart = 0.5 * n * dt, Peak = amp };
            for (int k = 0; k < n; k++)
            {
                double tk = (k + 0.5) * dt - p.CentreFromStart, ph = -Constants.TwoPi * offsetHz * tk;
                p.Re[k] = amp * env[k] * Math.Cos(ph); p.Im[k] = amp * env[k] * Math.Sin(ph);
            }
            return p;
        }
    }

    /// <summary>A stretch of a block with gradient currents varying linearly from start to end (and an RF pulse if Kind is Rf).</summary>
    public struct Segment
    {
        public SegKind Kind; public string Name; public double T0, T1;
        public double X0, Y0, Z0, X1, Y1, Z1; public RfPulse Rf;
        public double Duration => T1 - T0;
        public void CurrentsAt(double t, out double x, out double y, out double z)
        {
            double f = T1 > T0 ? Math.Max(0, Math.Min(1, (t - T0) / (T1 - T0))) : 0;
            x = X0 + (X1 - X0) * f; y = Y0 + (Y1 - Y0) * f; z = Z0 + (Z1 - Z0) * f;
        }
    }

    /// <summary>
    /// One excitation and what follows it. Every block starts with no transverse magnetization and ends with ideal spoiling;
    /// between blocks the longitudinal magnetization recovers.
    /// </summary>
    public sealed class Block
    {
        public int Index; public string Name; public double Start, End;
        public Segment[] Segs; public int RfSeg; public double[] Adc = new double[0];
        public int Slice = -1, Row = -1; public bool Selective; public double Echo = double.NaN;
        public Segment Rf => Segs[RfSeg];
        public double RfStart => Segs[RfSeg].T0;
        public double RfEnd => Segs[RfSeg].T1;
        public double RfCentre => Segs[RfSeg].T0 + Segs[RfSeg].Rf.CentreFromStart;
        public int SegmentAt(double t)
        {
            for (int i = 0; i < Segs.Length; i++) if (t < Segs[i].T1) return i;
            return Segs.Length - 1;
        }
    }

    /// <summary>The physical timeline: the lesson demonstrations and the acquisitions, as one monotonic sequence of blocks.</summary>
    public sealed class SeqProgram
    {
        public readonly Protocol P;
        public readonly List<Block> Blocks = new List<Block>();
        public readonly Dictionary<string, double> Events = new Dictionary<string, double>();
        public readonly List<double> SliceZ = new List<double>();
        public readonly List<List<int>> SliceBlocks = new List<List<int>>();
        public double TE; public double Duration;

        public SeqProgram(Protocol p) { P = p; }

        public double Time(string evt)
        {
            if (!Events.TryGetValue(evt, out double t)) throw new KeyNotFoundException("Unknown sequence event " + evt);
            return t;
        }

        public bool HasEvent(string evt) => Events.ContainsKey(evt);

        /// <summary>Index of the last block starting at or before t, or -1.</summary>
        public int BlockAt(double t)
        {
            int lo = 0, hi = Blocks.Count - 1, ans = -1;
            while (lo <= hi) { int mid = (lo + hi) >> 1; if (Blocks[mid].Start <= t) { ans = mid; lo = mid + 1; } else hi = mid - 1; }
            return ans;
        }

        public void CurrentsAt(double t, out double x, out double y, out double z)
        {
            x = y = z = 0;
            int b = BlockAt(t);
            if (b < 0 || t >= Blocks[b].End) return;
            var blk = Blocks[b];
            blk.Segs[blk.SegmentAt(t)].CurrentsAt(t, out x, out y, out z);
        }

        /// <summary>RF rung-current phasor A(t) (rotating frame); false when the RF is off.</summary>
        public bool RfAt(double t, out double re, out double im)
        {
            re = im = 0;
            int b = BlockAt(t);
            if (b < 0) return false;
            var blk = Blocks[b]; var s = blk.Rf;
            if (t < s.T0 || t >= s.T1) return false;
            int k = Math.Min(s.Rf.Steps - 1, (int)((t - s.T0) / s.Rf.Dt));
            re = s.Rf.Re[k]; im = s.Rf.Im[k];
            return true;
        }

        /// <summary>Gradient moment k = gamma-bar * integral of G dt from the RF centre of the current block (m^-1), for display cell averaging.</summary>
        public D3 MomentFromRfCentre(double t)
        {
            int b = BlockAt(t);
            if (b < 0 || t >= Blocks[b].End) return D3.Zero;
            var blk = Blocks[b]; double tc = blk.RfCentre;
            if (t <= tc) return D3.Zero;
            double mx = 0, my = 0, mz = 0;
            foreach (var s in blk.Segs)
            {
                double a = Math.Max(s.T0, tc), e = Math.Min(s.T1, t);
                if (e <= a) continue;
                s.CurrentsAt(a, out double xa, out double ya, out double za);
                s.CurrentsAt(e, out double xe, out double ye, out double ze);
                double d = e - a;
                mx += 0.5 * (xa + xe) * d; my += 0.5 * (ya + ye) * d; mz += 0.5 * (za + ze) * d;
            }
            return new D3(Constants.GammaBar * Scanner.EtaX * mx, Constants.GammaBar * Scanner.EtaY * my, Constants.GammaBar * Scanner.EtaZ * mz);
        }

        // ------------------------------------------------------------------------------------------------ building

        sealed class Builder
        {
            readonly SeqProgram prog; readonly Block blk; readonly List<Segment> segs = new List<Segment>(); public double T;
            public Builder(SeqProgram p, string name, double start) { prog = p; blk = new Block { Name = name, Start = start, Index = p.Blocks.Count }; T = start; p.Events[name + ".start"] = start; }
            void Mark(string name, double t0, double t1) { prog.Events[blk.Name + "." + name + ".start"] = t0; prog.Events[blk.Name + "." + name + ".end"] = t1; }

            public void Free(string name, double duration, double x0 = 0, double y0 = 0, double z0 = 0, double x1 = 0, double y1 = 0, double z1 = 0)
            {
                if (duration <= 0) return;
                segs.Add(new Segment { Kind = SegKind.Free, Name = name, T0 = T, T1 = T + duration, X0 = x0, Y0 = y0, Z0 = z0, X1 = x1, Y1 = y1, Z1 = z1 });
                Mark(name, T, T + duration); T += duration;
            }

            public void Rf(string name, RfPulse pulse, double gz)
            {
                blk.RfSeg = segs.Count;
                segs.Add(new Segment { Kind = SegKind.Rf, Name = name, T0 = T, T1 = T + pulse.Duration, Z0 = gz, Z1 = gz, Rf = pulse });
                Mark(name, T, T + pulse.Duration);
                prog.Events[blk.Name + "." + name + ".centre"] = T + pulse.CentreFromStart;
                T += pulse.Duration;
            }

            /// <summary>Trapezoid on one axis ('x', 'y', 'z') with the given peak gradient (T/m, signed) and exact area (T s/m, signed).</summary>
            public void Trapezoid(string name, char axis, double gPeak, double area)
            {
                double t0 = T;
                if (Math.Abs(area) < 1e-15) { return; }
                double g = Math.Abs(gPeak), r = Protocol.Ramp(g);
                double flat = Math.Abs(area) / g - r;
                if (flat < 0) { g = Math.Sqrt(Math.Abs(area) * Scanner.Slew); r = Protocol.Ramp(g); flat = 0; }
                flat = Math.Ceiling(flat / Protocol.Raster - 1e-9) * Protocol.Raster;
                g = Math.Abs(area) / (flat + r) * Math.Sign(area);
                AddTrap(name, axis, g, r, flat);
                Mark(name, t0, T);
            }

            /// <summary>Trapezoid with a fixed shape (ramp, flat) and the amplitude that gives 'area'. Occupies its time even when area is 0.</summary>
            public void FixedTrapezoid(string name, char axis, double ramp, double flat, double area)
            {
                double t0 = T; double g = area / (flat + ramp);
                AddTrap(name, axis, g, ramp, flat);
                Mark(name, t0, T);
            }

            void AddTrap(string name, char axis, double g, double ramp, double flat)
            {
                double eta = axis == 'x' ? Scanner.EtaX : axis == 'y' ? Scanner.EtaY : Scanner.EtaZ, i = g / eta;
                double x = axis == 'x' ? i : 0, y = axis == 'y' ? i : 0, z = axis == 'z' ? i : 0;
                Free(name + ".up", ramp, 0, 0, 0, x, y, z);
                Free(name + ".flat", flat, x, y, z, x, y, z);
                Free(name + ".down", ramp, x, y, z, 0, 0, 0);
            }

            public void Adc(double first, double dwell, int count)
            {
                var a = new double[blk.Adc.Length + count];
                Array.Copy(blk.Adc, a, blk.Adc.Length);
                for (int j = 0; j < count; j++) a[blk.Adc.Length + j] = first + j * dwell;
                blk.Adc = a;
            }

            public Block Done()
            {
                blk.Segs = segs.ToArray(); blk.End = T; prog.Events[blk.Name + ".end"] = T;
                prog.Blocks.Add(blk); return blk;
            }

            public Block Current => blk;
        }

        /// <summary>
        /// Builds the whole lesson timeline (SPEC 3.7): two non-selective hard-pulse experiments, then one 2-D slice
        /// acquisition (rows in lesson order), then the other slices of the multi-slice volume.
        /// </summary>
        public static SeqProgram Lesson(Protocol p, IList<double> otherSlices)
        {
            var prog = new SeqProgram(p);
            double gammaBar = Constants.GammaBar, dwell = p.Dwell;
            var hard = RfPulse.Hard(Protocol.HardFlip, Protocol.HardPulse);

            // Block hard1: 90-degree hard pulse, then 100 ms of free induction decay with the receiver on.
            // A short idle lead-in lets the lesson hold "just before the pulse" with the RF still off.
            var b = new Builder(prog, "hard1", 1e-3);
            b.Free("pre", 50e-6);
            b.Rf("rf", hard, 0);
            double fidStart = b.T; b.Free("fid", 0.100);
            b.Adc(fidStart + 0.5 * dwell, dwell, (int)Math.Floor(0.100 / dwell));
            b.Done();

            // Block hard2: hard pulse, Gz twist, pause, Gz untwist (gradient echo).
            b = new Builder(prog, "hard2", prog.Blocks[0].End + 5.0);
            b.Rf("rf", hard, 0);
            double demoArea = Protocol.DemoTurns / Protocol.DemoTurnsLength / gammaBar;
            double adcStart = b.T;
            b.Trapezoid("gzp", 'z', p.Gss, demoArea);
            b.Free("gap", 0.3e-3);
            b.Trapezoid("gzm", 'z', p.Gss, -demoArea);
            prog.Events["hard2.echo"] = b.T;
            b.Current.Echo = b.T;
            b.Free("after", 1.5e-3);
            b.Adc(adcStart + 0.5 * dwell, dwell, (int)Math.Floor((b.T - adcStart) / dwell));
            b.Done();

            double t0 = prog.Blocks[1].End + 5.0;
            int n = p.Matrix;
            var order = new List<int> { n / 2, n / 2 + 16 };
            foreach (int c in Centric(n)) if (c != n / 2 && c != n / 2 + 16) order.Add(c);
            var slices = new List<double> { p.SliceZ };
            foreach (double z in otherSlices) if (Math.Abs(z - p.SliceZ) > 1e-4) slices.Add(z);
            for (int s = 0; s < slices.Count; s++)
            {
                prog.SliceZ.Add(slices[s]); var list = new List<int>(); prog.SliceBlocks.Add(list);
                var rows = s == 0 ? order : Centric(n);
                var pulse = SlicePulse(p, slices[s]);
                for (int r = 0; r < rows.Count; r++)
                {
                    var blk = ImagingBlock(prog, $"s{s}.r{r}", t0, p, pulse, rows[r]);
                    blk.Slice = s; list.Add(blk.Index);
                    t0 = blk.Start + Protocol.Tr;
                }
            }
            prog.Duration = prog.Blocks[prog.Blocks.Count - 1].End;
            var first = prog.Blocks[prog.SliceBlocks[0][0]];
            prog.TE = first.Echo - first.RfCentre;
            return prog;
        }

        public static RfPulse SlicePulse(Protocol p, double sliceZ) =>
            RfPulse.Sinc(Math.PI / 2, p.Trf, Protocol.Tbw, Constants.GammaBar * p.Gss * sliceZ);

        /// <summary>Row order 0, -1, +1, -2, +2, ... around the centre row N/2 (returns row indices p).</summary>
        public static List<int> Centric(int n)
        {
            var list = new List<int> { n / 2 };
            for (int d = 1; list.Count < n; d++) { if (n / 2 - d >= 0) list.Add(n / 2 - d); if (list.Count < n && n / 2 + d < n) list.Add(n / 2 + d); }
            return list;
        }

        /// <summary>One repetition of the 2-D spoiled gradient echo with sequential lobes (SPEC 3.7).</summary>
        static Block ImagingBlock(SeqProgram prog, string name, double start, Protocol p, RfPulse pulse, int row)
        {
            double gammaBar = Constants.GammaBar, dwell = p.Dwell; int n = p.Matrix;
            var b = new Builder(prog, name, start);
            double gss = p.Gss, iss = p.GzCurrent, rss = Protocol.Ramp(gss);
            b.Free("ssup", rss, 0, 0, 0, 0, 0, iss);
            b.Rf("ss", pulse, iss);
            b.Free("ssdown", rss, 0, 0, iss, 0, 0, 0);
            b.Trapezoid("reph", 'z', -gss, -gss * (pulse.Duration / 2 + rss / 2));
            // Phase encode: fixed shape sized for the largest row, amplitude proportional to (row - N/2).
            double kyMax = (n / 2) / Protocol.Fov, areaMax = kyMax / gammaBar, gMax = 10e-3;
            double rpe = Protocol.Ramp(gMax), flatPe = Math.Max(Protocol.Raster, Math.Ceiling((areaMax / gMax - rpe) / Protocol.Raster - 1e-9) * Protocol.Raster);
            b.FixedTrapezoid("pe", 'y', rpe, flatPe, (row - n / 2) / Protocol.Fov / gammaBar);
            double gx = p.Gread, rx = Protocol.Ramp(gx);
            b.Trapezoid("pre", 'x', -gx, -gx * ((n / 2 + 0.5) * dwell + rx / 2));
            double ix = gx / Scanner.EtaX;
            b.Free("ro.up", rx, 0, 0, 0, ix, 0, 0);
            double flatStart = b.T;
            b.Free("ro.flat", n * dwell, ix, 0, 0, ix, 0, 0);
            b.Free("ro.down", rx, ix, 0, 0, 0, 0, 0);
            prog.Events[name + ".ro.start"] = flatStart - rx; prog.Events[name + ".ro.end"] = b.T;
            b.Adc(flatStart + 0.5 * dwell, dwell, n);
            prog.Events[name + ".adc.start"] = flatStart; prog.Events[name + ".adc.end"] = flatStart + n * dwell;
            b.Current.Echo = flatStart + (n / 2 + 0.5) * dwell; prog.Events[name + ".echo"] = b.Current.Echo;
            b.Trapezoid("crush", 'z', gss, 4 / (gammaBar * p.SliceThickness));
            var blk = b.Done();
            blk.Row = row; blk.Selective = true;
            return blk;
        }
    }
}
