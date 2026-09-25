using System;
using System.Collections.Generic;

namespace Nebulytic.Resonance.Sim
{
    /// <summary>
    /// Evaluates the magnetization of every isochromat of one set at any physical time t of a program, exactly:
    /// longitudinal recovery between blocks, exact RF steps inside a pulse (with partial steps), affine RF maps and
    /// closed-form precession and relaxation after it. Output is in the frame rotating at f_ref.
    /// Relaxation factors depend only on tissue class, so the per-frame loops use one factor per class.
    /// </summary>
    public sealed class Evaluator
    {
        public readonly IsoSet Set; readonly SeqProgram prog; readonly Func<Block, AffineMap> maps; readonly double i0;
        public readonly double[] X, Y, Z;

        // u_z at the start of each block, computed forward from equilibrium with checkpoints.
        readonly Dictionary<int, double[]> uzCheckpoint = new Dictionary<int, double[]>();
        int chainBlock = -1; double[] chainUz, chainTmp;

        // Cache for the current block.
        int cBlock = -2; double[] uzStart, uzRf, upx, upy, upz, uzEnd, segAcc; double[][] segCum = new double[0][];
        double[] dfRf; readonly double[] phaseBuf, weights = new double[IsoSet.PolyN];
        readonly double[] e1 = new double[TissueTable.Count], e2 = new double[TissueTable.Count];
        readonly double[] se1 = new double[TissueTable.Count], se2 = new double[TissueTable.Count];
        // RF stepping cache.
        int rfBlock = -2, rfStep; double[] rx, ry, rz;

        public Evaluator(IsoSet set, SeqProgram program, Func<Block, AffineMap> mapProvider)
        {
            Set = set; prog = program; maps = mapProvider; i0 = program.P.MagnetCurrent;
            X = new double[set.N]; Y = new double[set.N]; Z = new double[set.N]; phaseBuf = new double[set.N];
        }

        static double[] Buf(ref double[] a, int n) { if (a == null || a.Length != n) a = new double[n]; return a; }

        void Decay(double dt, double[] d1, double[] d2)
        {
            for (int c = 0; c < d1.Length; c++) { d1[c] = Math.Exp(-dt * Set.ClassR1[c]); d2[c] = Math.Exp(-dt * Set.ClassR2[c]); }
        }

        void RelaxAll(double[] from, double[] to, double dt)
        {
            Decay(dt, e1, e2); var pd = Set.Pd; var cls = Set.ClassIndex;
            for (int i = 0; i < from.Length; i++) to[i] = pd[i] + (from[i] - pd[i]) * e1[cls[i]];
        }

        /// <summary>
        /// u_z at the start of block b (before any of its gradients or RF). The result is valid until the next call (callers
        /// read it or copy it): walking forward reuses the chain's own buffer instead of allocating one per block (0.8.2).
        /// </summary>
        public double[] UzAtBlockStart(int b)
        {
            int start; double[] uz;
            if (chainBlock >= 0 && chainBlock <= b) { start = chainBlock; uz = chainUz; }
            else
            {
                start = 0; uz = null;
                foreach (var kv in uzCheckpoint) if (kv.Key <= b && kv.Key > start) { start = kv.Key; uz = kv.Value; }
                if (uz == null) { start = 0; uz = Set.Pd; }
                // Never walk a checkpoint or Pd in place.
                if (chainUz == null || chainUz.Length != uz.Length) chainUz = new double[uz.Length];
                Array.Copy(uz, chainUz, uz.Length); uz = chainUz;
            }
            if (chainTmp == null || chainTmp.Length != uz.Length) chainTmp = new double[uz.Length];
            var tmp = chainTmp;
            for (int k = start; k < b; k++)
            {
                var blk = prog.Blocks[k]; var next = prog.Blocks[k + 1]; var m = maps(blk);
                RelaxAll(uz, tmp, blk.RfStart - blk.Start);
                if (m != null) for (int i = 0; i < tmp.Length; i++) tmp[i] = tmp[i] * m.Az[i] + m.Bz[i];
                RelaxAll(tmp, uz, blk.End - blk.RfEnd);
                RelaxAll(uz, uz, next.Start - blk.End);
                if ((k + 1) % 16 == 0 && !uzCheckpoint.ContainsKey(k + 1)) uzCheckpoint[k + 1] = (double[])uz.Clone();
            }
            chainBlock = b; chainUz = uz;
            return uz;
        }

        void EnsureBlock(int b)
        {
            if (b == cBlock) return;
            cBlock = b; rfBlock = -2;
            var blk = prog.Blocks[b]; int n = Set.N;
            // The per-block arrays are reused from block to block (0.8.2): nothing is allocated when the lesson moves between
            // repetitions (strobed rows change block several times a second).
            Array.Copy(UzAtBlockStart(b), Buf(ref uzStart, n), n);
            var m = maps(blk);
            Buf(ref uzRf, n); Buf(ref upx, n); Buf(ref upy, n); Buf(ref upz, n); Buf(ref uzEnd, n); Buf(ref dfRf, n);
            RelaxAll(uzStart, uzRf, blk.RfStart - blk.Start);
            var rfc = new Currents(i0, 0, 0, blk.Rf.Z0);
            for (int i = 0; i < n; i++)
            {
                if (m != null) { upx[i] = uzRf[i] * m.Ax[i] + m.Bx[i]; upy[i] = uzRf[i] * m.Ay[i] + m.By[i]; upz[i] = uzRf[i] * m.Az[i] + m.Bz[i]; }
                else { upx[i] = 0; upy[i] = 0; upz[i] = uzRf[i]; }
                dfRf[i] = Set.DeltaF(i, rfc);
            }
            RelaxAll(upz, uzEnd, blk.End - blk.RfEnd);
            if (segCum.Length < blk.Segs.Length) System.Array.Resize(ref segCum, blk.Segs.Length);
            var acc = Buf(ref segAcc, n); Array.Clear(acc, 0, n);
            for (int k = blk.RfSeg + 1; k < blk.Segs.Length; k++)
            {
                Array.Copy(acc, Buf(ref segCum[k], n), n);
                Bloch.AddSegmentPhase(Set, blk.Segs[k], i0, 1, acc, weights);
            }
        }

        /// <summary>Free-precession phase (rad) of every isochromat from the start of the current block to the last EvaluateFree.</summary>
        public double[] Free;

        /// <summary>
        /// Phase accumulated by free precession since the start of the block holding t: every segment up to t at each
        /// isochromat's own Larmor offset (the RF segment at its offset during the pulse). Shows that moments the RF does not
        /// tip still precess. Same thread as Evaluate (shares the scratch weights).
        /// </summary>
        public void EvaluateFree(double t)
        {
            int n = Set.N; if (Free == null || Free.Length != n) Free = new double[n];
            Array.Clear(Free, 0, n);
            int b = prog.BlockAt(t); if (b < 0) return;
            EnsureBlock(b); var blk = prog.Blocks[b];
            for (int k = 0; k < blk.Segs.Length; k++)
            {
                var s = blk.Segs[k]; if (t <= s.T0) break;
                if (k == blk.RfSeg) { double dt = Math.Min(t, s.T1) - s.T0; for (int i = 0; i < n; i++) Free[i] += Constants.TwoPi * dfRf[i] * dt; continue; }
                double sig = s.Duration > 0 ? Math.Min(1, (t - s.T0) / s.Duration) : 1;
                Bloch.AddSegmentPhase(Set, s, i0, sig, Free, weights);
            }
        }

        /// <summary>Evaluate every isochromat at physical time t (rotating frame, no cell averaging).</summary>
        public void Evaluate(double t)
        {
            int n = Set.N; int b = prog.BlockAt(t);
            if (b < 0) { for (int i = 0; i < n; i++) { X[i] = 0; Y[i] = 0; Z[i] = Set.Pd[i]; } return; }
            EnsureBlock(b);
            var blk = prog.Blocks[b];
            if (t >= blk.End || t < blk.RfStart)
            {
                bool after = t >= blk.End;
                RelaxAll(after ? uzEnd : uzStart, Z, after ? t - blk.End : t - blk.Start);
                Array.Clear(X, 0, n); Array.Clear(Y, 0, n);
                return;
            }
            if (t < blk.RfEnd) { EvaluateRf(blk, b, t); return; }
            int k = blk.SegmentAt(t);
            if (k <= blk.RfSeg) k = blk.RfSeg + 1;
            if (k >= blk.Segs.Length) k = blk.Segs.Length - 1;
            var s = blk.Segs[k];
            double dt = t - blk.RfEnd, sig = s.Duration > 0 ? Math.Max(0, Math.Min(1, (t - s.T0) / s.Duration)) : 0;
            Array.Copy(segCum[k], phaseBuf, n);
            Bloch.AddSegmentPhase(Set, s, i0, sig, phaseBuf, weights);
            Decay(dt, e1, e2);
            var pd = Set.Pd; var cls = Set.ClassIndex;
            for (int i = 0; i < n; i++)
            {
                double ph = phaseBuf[i], c = Math.Cos(ph), sn = Math.Sin(ph), d2 = e2[cls[i]];
                // u_perp * exp(-i ph) * E2
                X[i] = (upx[i] * c + upy[i] * sn) * d2;
                Y[i] = (upy[i] * c - upx[i] * sn) * d2;
                Z[i] = pd[i] + (upz[i] - pd[i]) * e1[cls[i]];
            }
        }

        void EvaluateRf(Block blk, int b, double t)
        {
            int n = Set.N; var s = blk.Rf; var pulse = s.Rf; double dt = pulse.Dt;
            int kt = Math.Min(pulse.Steps - 1, (int)Math.Floor((t - s.T0) / dt));
            if (rfBlock != b || rfStep > kt || rx == null)
            {
                rfBlock = b; rfStep = 0;
                Array.Clear(Buf(ref rx, n), 0, n); Array.Clear(Buf(ref ry, n), 0, n); Array.Copy(uzRf, Buf(ref rz, n), n);
            }
            double g = Constants.GammaBar; var pd = Set.Pd; var cls = Set.ClassIndex; var f = Set.F;
            if (rfStep < kt) Decay(dt, se1, se2);
            for (; rfStep < kt; rfStep++)
            {
                double ar = pulse.Re[rfStep], ai = pulse.Im[rfStep];
                for (int i = 0; i < n; i++)
                {
                    double b1r = f[i].B1Re, b1i = f[i].B1Im;
                    Bloch.Step(ref rx[i], ref ry[i], ref rz[i], g * (b1r * ar - b1i * ai), g * (b1r * ai + b1i * ar), dfRf[i], dt, se1[cls[i]], se2[cls[i]], pd[i]);
                }
            }
            double part = t - s.T0 - kt * dt;
            double pr = pulse.Re[kt], pi = pulse.Im[kt];
            if (part > 0) Decay(part, e1, e2);
            for (int i = 0; i < n; i++)
            {
                double x = rx[i], y = ry[i], z = rz[i];
                if (part > 0)
                {
                    double b1r = f[i].B1Re, b1i = f[i].B1Im;
                    Bloch.Step(ref x, ref y, ref z, g * (b1r * pr - b1i * pi), g * (b1r * pi + b1i * pr), dfRf[i], part, e1[cls[i]], e2[cls[i]], pd[i]);
                }
                X[i] = x; Y[i] = y; Z[i] = z;
            }
        }
    }
}
