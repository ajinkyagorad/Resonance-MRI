using System;

namespace Nebulytic.Resonance.Sim
{
    /// <summary>Double-precision 3-vector in the right-handed scanner frame P (x, y up, z = bore = B0).</summary>
    public struct D3
    {
        public double X, Y, Z;
        public D3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static readonly D3 Zero = new D3(0, 0, 0);
        public static D3 operator +(D3 a, D3 b) => new D3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static D3 operator -(D3 a, D3 b) => new D3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static D3 operator -(D3 a) => new D3(-a.X, -a.Y, -a.Z);
        public static D3 operator *(D3 a, double s) => new D3(a.X * s, a.Y * s, a.Z * s);
        public static D3 operator *(double s, D3 a) => new D3(a.X * s, a.Y * s, a.Z * s);
        public static D3 operator /(D3 a, double s) => new D3(a.X / s, a.Y / s, a.Z / s);
        public static double Dot(D3 a, D3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static D3 Cross(D3 a, D3 b) => new D3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public double Norm => Math.Sqrt(X * X + Y * Y + Z * Z);
        public override string ToString() => $"({X:G6}, {Y:G6}, {Z:G6})";
    }

    /// <summary>Physical constants (CODATA 2022) and derived magnetization values.</summary>
    public static class Constants
    {
        /// <summary>Shielded proton in water, gamma'/2pi in Hz/T. Used for all tissue water.</summary>
        public const double GammaBar = 42.57638543e6;
        public const double GammaProton = 2.6752218708e8; // rad s^-1 T^-1
        public const double Mu0 = 1.25663706127e-6;
        public const double Kb = 1.380649e-23;
        public const double Hbar = 1.054571817e-34;
        public const double BodyTemperature = 310.15;
        /// <summary>Hydrogen nuclei per cubic metre of water at 37 C.</summary>
        public const double ProtonDensity = 6.641e28;
        public const double FatShiftPpm = -3.4;
        public const double TwoPi = 2 * Math.PI;

        /// <summary>Equilibrium magnetization of water, A/m, at field b (T).</summary>
        public static double M0Water(double b) =>
            ProtonDensity * GammaProton * GammaProton * Hbar * Hbar * b / (4 * Kb * BodyTemperature);

        /// <summary>Fractional excess of aligned over anti-aligned protons.</summary>
        public static double Polarization(double b) => Hbar * GammaProton * b / (2 * Kb * BodyTemperature);
    }

    public static class Numerics
    {
        public static double Sinc(double x)
        {
            if (Math.Abs(x) < 1e-8) return 1;
            double p = Math.PI * x;
            return Math.Sin(p) / p;
        }

        /// <summary>Gauss-Legendre nodes and weights on [-1, 1].</summary>
        public static void GaussLegendre(int n, out double[] x, out double[] w)
        {
            x = new double[n]; w = new double[n];
            for (int i = 0; i < n; i++)
            {
                double z = Math.Cos(Math.PI * (i + 0.75) / (n + 0.5)), pp = 0;
                for (int it = 0; it < 100; it++)
                {
                    double p1 = 1, p2 = 0;
                    for (int j = 1; j <= n; j++) { double p3 = p2; p2 = p1; p1 = ((2 * j - 1) * z * p2 - (j - 1) * p3) / j; }
                    pp = n * (z * p1 - p2) / (z * z - 1);
                    double z1 = z; z = z1 - p1 / pp;
                    if (Math.Abs(z - z1) < 1e-15) break;
                }
                x[i] = -z; w[i] = 2 / ((1 - z * z) * pp * pp);
            }
        }

        /// <summary>In-place radix-2 complex FFT. Inverse divides by n.</summary>
        public static void Fft(double[] re, double[] im, bool inverse)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { (re[i], re[j]) = (re[j], re[i]); (im[i], im[j]) = (im[j], im[i]); }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = (inverse ? 2 : -2) * Math.PI / len, wr = Math.Cos(ang), wi = Math.Sin(ang);
                for (int i = 0; i < n; i += len)
                {
                    double cr = 1, ci = 0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int a = i + k, b = a + len / 2;
                        double tr = re[b] * cr - im[b] * ci, ti = re[b] * ci + im[b] * cr;
                        re[b] = re[a] - tr; im[b] = im[a] - ti; re[a] += tr; im[a] += ti;
                        double nr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = nr;
                    }
                }
            }
            if (inverse) for (int i = 0; i < n; i++) { re[i] /= n; im[i] /= n; }
        }

        /// <summary>2-D FFT of an n x n row-major array (row = y index, column = x index).</summary>
        public static void Fft2(double[] re, double[] im, int n, bool inverse)
        {
            var r = new double[n]; var q = new double[n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++) { r[x] = re[y * n + x]; q[x] = im[y * n + x]; }
                Fft(r, q, inverse);
                for (int x = 0; x < n; x++) { re[y * n + x] = r[x]; im[y * n + x] = q[x]; }
            }
            for (int x = 0; x < n; x++)
            {
                for (int y = 0; y < n; y++) { r[y] = re[y * n + x]; q[y] = im[y * n + x]; }
                Fft(r, q, inverse);
                for (int y = 0; y < n; y++) { re[y * n + x] = r[y]; im[y * n + x] = q[y]; }
            }
        }

        /// <summary>Deterministic 64-bit hash for seeds.</summary>
        public static ulong Hash(ulong a, ulong b)
        {
            ulong h = 1469598103934665603UL ^ a;
            h *= 1099511628211UL; h ^= b; h *= 1099511628211UL;
            h ^= h >> 33; h *= 0xff51afd7ed558ccdUL; h ^= h >> 33;
            return h;
        }
    }

    /// <summary>Small deterministic Gaussian generator (xorshift + Box-Muller).</summary>
    public sealed class Gaussian
    {
        ulong s; bool has; double spare;
        public Gaussian(ulong seed) { s = seed == 0 ? 0x9E3779B97F4A7C15UL : seed; }
        double Uniform()
        {
            s ^= s << 13; s ^= s >> 7; s ^= s << 17;
            return ((s >> 11) + 0.5) / 9007199254740992.0;
        }
        public double Next()
        {
            if (has) { has = false; return spare; }
            double u = Uniform(), v = Uniform(), r = Math.Sqrt(-2 * Math.Log(u));
            spare = r * Math.Sin(Constants.TwoPi * v); has = true;
            return r * Math.Cos(Constants.TwoPi * v);
        }
    }
}
