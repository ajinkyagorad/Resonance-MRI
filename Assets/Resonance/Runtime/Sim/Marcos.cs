using System;

namespace Nebulytic.Resonance.Sim
{
    /// <summary>
    /// Model of the MaRCoS receive chain (Negnevitsky et al., J. Magn. Reson. 2023) for one readout, used to validate the
    /// runtime's direct reciprocity samples: the coil's real RF voltage is sampled directly by a 16-bit ADC at
    /// 122.88 MS/s (+-1 V), mixed to baseband by an NCO at the RF frequency, decimated 640x by a six-stage CIC to 192 kS/s,
    /// then 6x by a 193-tap Hamming-windowed sinc FIR to 32 kS/s, with CIC droop and filter delays removed. NCO and CIC
    /// word widths are not published; arithmetic here is ideal apart from the ADC quantisation.
    /// </summary>
    public static class Marcos
    {
        public const double Fs = 122.88e6, AdcFullScale = 1.0; public const int AdcBits = 16, CicStages = 6, CicDecimation = 640, FirTaps = 193, FirDecimation = 6;

        /// <summary>
        /// envRe/envIm: complex baseband envelope S (volts) sampled at 192 kS/s starting at t0 (the coil voltage is
        /// Re{S exp(i 2 pi fRef t)}). Returns the chain's output at the requested output times (on the 32 kS/s grid).
        /// </summary>
        public static void Process(double[] envRe, double[] envIm, double t0, double fRef, double[] outTimes, out double[] outRe, out double[] outIm, out double gain)
        {
            double fMid = Fs / CicDecimation; // 192 kS/s
            int nEnv = envRe.Length; double tEnd = t0 + (nEnv - 1) / fMid;
            // Receiver gain: peak at -6 dBFS.
            double peak = 0; for (int i = 0; i < nEnv; i++) peak = Math.Max(peak, Math.Sqrt(envRe[i] * envRe[i] + envIm[i] * envIm[i]));
            gain = peak > 0 ? 0.5 * AdcFullScale / peak : 1;
            // Stage 1: ADC samples of the real RF voltage (cubic interpolation of the envelope), quantised to 16 bits.
            long nAdc = (long)Math.Floor((tEnd - t0) * Fs);
            double lsb = 2 * AdcFullScale / (1 << AdcBits);
            // Stage 2+3: NCO mix and CIC, evaluated as the CIC's exact FIR at the decimated instants only.
            var cic = CicKernel(); int L = cic.Length; double cicSum = 0; foreach (var h in cic) cicSum += h;
            int nMid = (int)((nAdc - L) / CicDecimation);
            var midRe = new double[nMid]; var midIm = new double[nMid]; var midT = new double[nMid];
            for (int m = 0; m < nMid; m++)
            {
                long end = (long)m * CicDecimation + L - 1; double re = 0, im = 0;
                for (int k = 0; k < L; k++)
                {
                    long n = end - k; double t = t0 + n / Fs;
                    double v = RfVoltage(envRe, envIm, t0, fMid, t, fRef) * gain;
                    v = Math.Max(-AdcFullScale, Math.Min(AdcFullScale - lsb, Math.Round(v / lsb) * lsb));
                    double ph = Constants.TwoPi * FracCycles(fRef, t);
                    // Multiply by 2 exp(-i phi): 2 Re{S exp(i phi)} exp(-i phi) = S + conj(S) exp(-2i phi); the filters remove the image.
                    re += cic[k] * 2 * v * Math.Cos(ph); im -= cic[k] * 2 * v * Math.Sin(ph);
                }
                midRe[m] = re / cicSum; midIm[m] = im / cicSum;
                midT[m] = t0 + (end - (L - 1) / 2.0) / Fs; // group delay removed: output belongs to the kernel centre
            }
            // Stage 4: FIR decimation by 6 (cutoff at half the receiver bandwidth), delay removed.
            var fir = FirKernel(fMid, 0.5 * fMid / FirDecimation); int F = fir.Length, half = F / 2;
            outRe = new double[outTimes.Length]; outIm = new double[outTimes.Length];
            for (int j = 0; j < outTimes.Length; j++)
            {
                double tc = outTimes[j]; double pos = (tc - midT[0]) * fMid; int c = (int)Math.Round(pos);
                double re = 0, im = 0, w = 0;
                for (int k = -half; k <= half; k++)
                {
                    int idx = c + k; if (idx < 0 || idx >= nMid) continue;
                    double h = fir[k + half]; re += h * midRe[idx]; im += h * midIm[idx]; w += h;
                }
                outRe[j] = re / w; outIm[j] = im / w;
            }
            // Stage 5: undo the CIC droop at each output frequency (exact inverse on the readout's N-point spectrum).
            int N = outTimes.Length;
            if ((N & (N - 1)) == 0 && N > 1)
            {
                var fr = (double[])outRe.Clone(); var fi = (double[])outIm.Clone();
                Numerics.Fft(fr, fi, false);
                double df = 1 / ((outTimes[N - 1] - outTimes[0]) / (N - 1) * N);
                for (int k = 0; k < N; k++)
                {
                    double f = (k < N / 2 ? k : k - N) * df; double h = CicResponse(f, fMid);
                    fr[k] /= h; fi[k] /= h;
                }
                Numerics.Fft(fr, fi, true); outRe = fr; outIm = fi;
            }
            for (int j = 0; j < N; j++) { outRe[j] /= gain; outIm[j] /= gain; }
        }

        static double FracCycles(double f, double t) { double c = f * t; return c - Math.Floor(c); }

        /// <summary>Real coil voltage Re{S(t) exp(i 2 pi fRef t)} with S cubic-interpolated from its 192 kS/s samples.</summary>
        static double RfVoltage(double[] re, double[] im, double t0, double fs, double t, double fRef)
        {
            double x = (t - t0) * fs; int i = (int)Math.Floor(x); double u = x - i;
            double Sr = Cubic(re, i, u), Si = Cubic(im, i, u);
            double ph = Constants.TwoPi * FracCycles(fRef, t);
            return Sr * Math.Cos(ph) - Si * Math.Sin(ph);
        }

        static double Cubic(double[] a, int i, double u)
        {
            double p0 = a[Math.Max(0, Math.Min(a.Length - 1, i - 1))], p1 = a[Math.Max(0, Math.Min(a.Length - 1, i))];
            double p2 = a[Math.Max(0, Math.Min(a.Length - 1, i + 1))], p3 = a[Math.Max(0, Math.Min(a.Length - 1, i + 2))];
            return p1 + 0.5 * u * (p2 - p0 + u * (2 * p0 - 5 * p1 + 4 * p2 - p3 + u * (3 * (p1 - p2) + p3 - p0)));
        }

        /// <summary>Six-stage CIC (R = 640, M = 1) as its exact FIR: a length-R boxcar convolved with itself six times.</summary>
        public static double[] CicKernel()
        {
            var h = new double[] { 1 };
            for (int s = 0; s < CicStages; s++)
            {
                var n = new double[h.Length + CicDecimation - 1];
                for (int i = 0; i < h.Length; i++) for (int k = 0; k < CicDecimation; k++) n[i + k] += h[i];
                h = n;
            }
            return h;
        }

        public static double CicResponse(double f, double fsOut)
        {
            double fsIn = fsOut * CicDecimation, x = Math.PI * f / fsIn;
            if (Math.Abs(x) < 1e-12) return 1;
            double r = Math.Sin(CicDecimation * x) / (CicDecimation * Math.Sin(x));
            return Math.Pow(r, CicStages);
        }

        public static double[] FirKernel(double fs, double cutoff)
        {
            var h = new double[FirTaps]; int m = FirTaps - 1;
            for (int n = 0; n < FirTaps; n++)
            {
                double x = n - m / 2.0;
                h[n] = 2 * cutoff / fs * Numerics.Sinc(2 * cutoff / fs * x) * (0.54 - 0.46 * Math.Cos(Constants.TwoPi * n / m));
            }
            return h;
        }
    }
}
