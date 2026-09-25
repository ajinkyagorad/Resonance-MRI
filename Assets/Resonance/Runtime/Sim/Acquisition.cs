using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nebulytic.Resonance.Sim
{
    /// <summary>Receiver noise model (SPEC 3.8): Johnson noise of coil and sample through an LNA of noise figure 0.5 dB.</summary>
    public static class ReceiverNoise
    {
        public const double NoiseFigureDb = 0.5;
        public static double PortResistance(double f) { double r = f / 42.576e6; return 0.15 * Math.Sqrt(r) + 0.35 * r * r; }
        /// <summary>Expected |n|^2 of one complex sample (V^2).</summary>
        public static double Variance(double f, double bandwidth) =>
            4 * Constants.Kb * Constants.BodyTemperature * PortResistance(f) * Math.Pow(10, NoiseFigureDb / 10) * bandwidth;
    }

    /// <summary>
    /// Receiver output of one block: one complex sample per ADC time, in volts, with the receiver phase calibrated so that
    /// transverse magnetization along +x' of a uniform coil gives a real positive value.
    /// </summary>
    public sealed class BlockRecord
    {
        public int Block; public double[] Times, Re, Im; public double Reference; // Reference: the in-phase sum just after the RF
    }

    /// <summary>
    /// Computes receiver samples of a set of isochromats for the blocks of a program, by reciprocity:
    /// s(t) = omega * M0 * sum dV conj(b1) u_perp(t) * H (H = receiver passband during that segment).
    /// </summary>
    public sealed class Receiver
    {
        readonly IsoSet set; readonly SeqProgram prog; readonly Func<Block, AffineMap> maps; readonly double i0, scale; readonly bool noise; readonly int revision;
        readonly double[] wRe, wIm; readonly double[] weights = new double[IsoSet.PolyN];
        double[] adc; bool passbandOn = true; // sample times and receiver passband of the current Record call
        // Scratch reused by every Record call (0.8.2): one set of arrays per receiver instead of several per row, so the
        // acquisition does not feed the garbage collector while the headset renders.
        double[] bufUx = new double[0], bufUy = new double[0], bufPh = new double[0], bufPhase = new double[0], bufFreq = new double[0];
        double[,] partRe = new double[0, 0], partIm = new double[0, 0];
        static double[] Scratch(ref double[] a, int n) { if (a.Length != n) a = new double[n]; return a; }
        public Receiver(IsoSet set, SeqProgram program, Func<Block, AffineMap> mapProvider, bool addNoise, int revision)
        {
            this.set = set; prog = program; maps = mapProvider; noise = addNoise; this.revision = revision;
            i0 = program.P.MagnetCurrent;
            double omega = Constants.TwoPi * program.P.Fref;
            scale = omega * Constants.M0Water(program.P.B0) * set.CellVolume;
            wRe = new double[set.N]; wIm = new double[set.N];
            for (int i = 0; i < set.N; i++) { wRe[i] = set.F[i].RxRe; wIm[i] = -set.F[i].RxIm; } // independent receive sensitivity, conj(Rx)
        }

        /// <summary>
        /// Records every ADC sample of the given blocks (which must be in time order). uzStart supplies u_z at each block
        /// start (from the caller's chain). cellK: when true, applies the display-cell average sinc(k.h) (lattice sets).
        /// </summary>
        public BlockRecord Record(int b, double[] uzStart, bool cellAverage, double[] times = null, bool passband = true)
        {
            var blk = prog.Blocks[b]; int n = set.N; var m = maps(blk);
            var adc = times ?? blk.Adc;
            var rec = new BlockRecord { Block = b, Times = adc, Re = new double[adc.Length], Im = new double[adc.Length] };
            if (adc.Length == 0) return rec;
            this.adc = adc; this.passbandOn = passband;
            // State just after the RF.
            var ux = Scratch(ref bufUx, n); var uy = Scratch(ref bufUy, n);
            double refSum = 0;
            for (int i = 0; i < n; i++)
            {
                double uz = set.Pd[i] + (uzStart[i] - set.Pd[i]) * Math.Exp(-(blk.RfStart - blk.Start) * set.R1[i]);
                if (m != null) { ux[i] = uz * m.Ax[i] + m.Bx[i]; uy[i] = uz * m.Ay[i] + m.By[i]; } else { ux[i] = 0; uy[i] = 0; }
                refSum += Math.Sqrt(ux[i] * ux[i] + uy[i] * uy[i]) * Math.Sqrt(wRe[i] * wRe[i] + wIm[i] * wIm[i]);
            }
            rec.Reference = refSum * scale;
            // Walk the free segments after the RF, sampling where the ADC is open.
            var ph = Scratch(ref bufPh, n); Array.Clear(ph, 0, n);
            int j = 0; double rxHalf = 0.5 * Protocol.RxBandwidth;
            var gauss = new Gaussian(Numerics.Hash((ulong)revision, (ulong)b + 1));
            double sigma = noise ? Math.Sqrt(ReceiverNoise.Variance(prog.P.Fref, Protocol.RxBandwidth) / 2) : 0;
            for (int k = blk.RfSeg + 1; k < blk.Segs.Length && j < adc.Length; k++)
            {
                var s = blk.Segs[k];
                bool flat = s.X0 == s.X1 && s.Y0 == s.Y1 && s.Z0 == s.Z1;
                // Receiver passband per isochromat for this segment (frequency at the segment midpoint).
                var cm = new Currents(i0, 0.5 * (s.X0 + s.X1), 0.5 * (s.Y0 + s.Y1), 0.5 * (s.Z0 + s.Z1));
                int jStart = j; while (j < adc.Length && adc[j] < s.T1) j++;
                int count = j - jStart;
                if (count > 0)
                {
                    if (flat && count > 1)
                        SampleFlat(blk, s, k, ph, ux, uy, jStart, count, cm, rxHalf, cellAverage, rec);
                    else
                        for (int q = jStart; q < j; q++) SampleAt(blk, s, ph, ux, uy, q, cm, rxHalf, cellAverage, rec);
                }
                Bloch.AddSegmentPhase(set, s, i0, 1, ph, weights);
            }
            for (int q = 0; q < rec.Re.Length; q++)
            {
                rec.Re[q] = rec.Re[q] * scale + sigma * gauss.Next();
                rec.Im[q] = rec.Im[q] * scale + sigma * gauss.Next();
            }
            return rec;
        }

        double CellFactor(double t, bool cellAverage)
        {
            if (!cellAverage) return 1;
            var k = prog.MomentFromRfCentre(t);
            return Numerics.Sinc(k.X * set.CellX) * Numerics.Sinc(k.Y * set.CellY) * Numerics.Sinc(k.Z * set.CellZ);
        }

        void SampleAt(Block blk, in Segment s, double[] ph, double[] ux, double[] uy, int q, in Currents cm, double rxHalf, bool cellAverage, BlockRecord rec)
        {
            double t = adc[q], dt = t - blk.RfEnd, re = 0, im = 0;
            var phase = Scratch(ref bufPhase, set.N); Array.Copy(ph, phase, set.N);
            Bloch.AddSegmentPhase(set, s, i0, s.Duration > 0 ? (t - s.T0) / s.Duration : 0, phase, weights);
            var freq = Scratch(ref bufFreq, set.N); Array.Clear(freq, 0, set.N);
            Bloch.AddSegmentPhase(set, new Segment { Kind = SegKind.Free, T0 = 0, T1 = 1, X0 = cm.X, Y0 = cm.Y, Z0 = cm.Z, X1 = cm.X, Y1 = cm.Y, Z1 = cm.Z }, i0, 1, freq, weights);
            for (int i = 0; i < set.N; i++)
            {
                if (passbandOn && Math.Abs(freq[i] / Constants.TwoPi) > rxHalf) continue;
                double p = phase[i];
                double c = Math.Cos(p), sn = Math.Sin(p), e2 = Math.Exp(-dt * set.R2[i]);
                double mx = (ux[i] * c + uy[i] * sn) * e2, my = (uy[i] * c - ux[i] * sn) * e2;
                re += wRe[i] * mx - wIm[i] * my; im += wRe[i] * my + wIm[i] * mx;
            }
            double f = CellFactor(t, cellAverage);
            rec.Re[q] = re * f; rec.Im[q] = im * f;
        }

        void SampleFlat(Block blk, in Segment s, int k, double[] ph, double[] ux, double[] uy, int j0, int count, in Currents cm, double rxHalf, bool cellAverage, BlockRecord rec)
        {
            int n = set.N; double dwell = adc[j0 + 1] - adc[j0];
            const int chunk = 1024; int chunks = (n + chunk - 1) / chunk;
            if (partRe.GetLength(0) != chunks || partRe.GetLength(1) < count) { partRe = new double[chunks, count]; partIm = new double[chunks, count]; }
            else { Array.Clear(partRe, 0, partRe.Length); Array.Clear(partIm, 0, partIm.Length); }
            var partRe_ = partRe; var partIm_ = partIm;
            // Phase at the first sample and the (constant) frequency of every isochromat on this flat segment.
            double t0 = adc[j0], dt = t0 - blk.RfEnd;
            var phase0 = Scratch(ref bufPhase, n); Array.Copy(ph, phase0, n);
            Bloch.AddSegmentPhase(set, s, i0, s.Duration > 0 ? (t0 - s.T0) / s.Duration : 0, phase0, weights);
            var freq = Scratch(ref bufFreq, n); Array.Clear(freq, 0, n);
            Bloch.AddSegmentPhase(set, new Segment { Kind = SegKind.Free, T0 = 0, T1 = 1, X0 = s.X0, Y0 = s.Y0, Z0 = s.Z0, X1 = s.X0, Y1 = s.Y0, Z1 = s.Z0 }, i0, 1, freq, weights);
            Parallel.For(0, chunks, Work.Options, c =>
            {
                int e = Math.Min(n, c * chunk + chunk);
                for (int i = c * chunk; i < e; i++)
                {
                    double df = freq[i] / Constants.TwoPi;
                    if (passbandOn && Math.Abs(df) > rxHalf) continue;
                    double p = phase0[i];
                    double co = Math.Cos(p), sn = Math.Sin(p), e2 = Math.Exp(-dt * set.R2[i]);
                    double mx = (ux[i] * co + uy[i] * sn) * e2, my = (uy[i] * co - ux[i] * sn) * e2;
                    double step = -Constants.TwoPi * df * dwell, sr = Math.Cos(step), si = Math.Sin(step), d2 = Math.Exp(-dwell * set.R2[i]);
                    sr *= d2; si *= d2;
                    double wr = wRe[i], wi = wIm[i];
                    for (int q = 0; q < count; q++)
                    {
                        partRe_[c, q] += wr * mx - wi * my; partIm_[c, q] += wr * my + wi * mx;
                        double nx = mx * sr - my * si; my = mx * si + my * sr; mx = nx;
                    }
                }
            });
            for (int q = 0; q < count; q++)
            {
                double re = 0, im = 0;
                for (int c = 0; c < chunks; c++) { re += partRe_[c, q]; im += partIm_[c, q]; }
                double f = CellFactor(adc[j0 + q], cellAverage);
                rec.Re[j0 + q] = re * f; rec.Im[j0 + q] = im * f;
            }
        }
    }

    /// <summary>One slice: its signal set, measured k-space (only what has been acquired) and the reconstruction.</summary>
    public sealed class SliceAcquisition
    {
        public int Slice, N; public double Z; public IsoSet Set;
        public double[] KRe, KIm; public double[] RowTime; // physical time of each row's last sample; +inf until measured
        public double[] SampleTime;                         // [p*N + j]
        public double Reference;                            // largest in-phase reference of the slice (for display scaling)
        public int RowsComputed; public double MaxAbs;
        public volatile bool Done;

        public SliceAcquisition(int slice, int n, double z)
        {
            Slice = slice; N = n; Z = z; KRe = new double[n * n]; KIm = new double[n * n]; RowTime = new double[n]; SampleTime = new double[n * n];
            for (int i = 0; i < n; i++) RowTime[i] = double.PositiveInfinity;
            for (int i = 0; i < n * n; i++) SampleTime[i] = double.PositiveInfinity;
        }

        /// <summary>Inverse 2-D DFT of the samples measured by physical time t (zero-filled); returns |image| row-major [y*N + x].</summary>
        public double[] Reconstruct(double t, out int rows)
        {
            int n = N; var re = new double[n * n]; var im = new double[n * n]; rows = 0;
            for (int p = 0; p < n; p++)
            {
                bool any = false;
                for (int j = 0; j < n; j++)
                {
                    int k = p * n + j;
                    if (SampleTime[k] > t) continue;
                    any = true;
                    double sgn = ((p + j) & 1) == 0 ? 1 : -1;
                    re[k] = KRe[k] * sgn; im[k] = KIm[k] * sgn;
                }
                if (any && RowTime[p] <= t) rows++;
            }
            Numerics.Fft2(re, im, n, true);
            var mag = new double[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++) { int k = y * n + x; mag[k] = Math.Sqrt(re[k] * re[k] + im[k] * im[k]); }
            return mag;
        }
    }
}
