using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nebulytic.Resonance.Sim
{
    /// <summary>
    /// A set of isochromats: each is the net magnetization of the protons in one cell of tissue (never a single nucleus).
    /// Positions are fixed in the scanner frame; tissue, fields and relaxation are sampled once per rebuild.
    /// </summary>
    public sealed class IsoSet
    {
        public string Name; public int N;
        public D3[] Pos; public TissueClass[] Cls; public FieldSample[] F;
        public double[] Pd, R1, R2, Shift;
        public double CellX, CellY, CellZ, CellVolume;
        public int Nx, Ny, Nz; public int[] Lattice; // lattice index (ix + Nx*(iy + Ny*iz)) of each isochromat, for display sets
        public D3 LatticeOrigin;                     // centre of lattice cell (0,0,0)

        public static IsoSet FromPoints(string name, List<D3> pts, List<int> lattice, ISpecimen specimen, FieldTables tables, Protocol p, double cx, double cy, double cz, bool keepAir)
        {
            var s = new IsoSet { Name = name, CellX = cx, CellY = cy, CellZ = cz, CellVolume = cx * cy * cz };
            var pos = new List<D3>(); var cls = new List<TissueClass>(); var lat = new List<int>();
            for (int i = 0; i < pts.Count; i++)
            {
                var c = specimen.ClassAt(pts[i]);
                if (c == TissueClass.Air && !keepAir) continue;
                pos.Add(pts[i]); cls.Add(c); if (lattice != null) lat.Add(lattice[i]);
            }
            s.N = pos.Count; s.Pos = pos.ToArray(); s.Cls = cls.ToArray(); s.Lattice = lattice != null ? lat.ToArray() : null;
            s.F = new FieldSample[s.N];
            Parallel.For(0, (s.N + 255) / 256, Work.Options, chunk =>
            {
                int e = Math.Min(s.N, chunk * 256 + 256);
                for (int i = chunk * 256; i < e; i++) tables.Sample(s.Pos[i], out s.F[i]);
            });
            s.SetTissue(p.B0);
            return s;
        }

        /// <summary>Per-class relaxation rates at the set's field (display fast paths use these).</summary>
        public readonly double[] ClassR1 = new double[TissueTable.Count], ClassR2 = new double[TissueTable.Count];
        public byte[] ClassIndex;

        /// <summary>Relaxation and density at field b0 (called again when B0 changes).</summary>
        public double B0Field;
        public void SetTissue(double b0)
        {
            B0Field = b0;
            for (int c = 0; c < TissueTable.Count; c++)
            {
                double t1c = TissueTable.T1((TissueClass)c, b0), t2c = TissueTable.T2((TissueClass)c, b0);
                ClassR1[c] = t1c > 0 ? 1 / t1c : 0; ClassR2[c] = t2c > 0 ? 1 / t2c : 0;
            }
            ClassIndex = new byte[N]; for (int i = 0; i < N; i++) ClassIndex[i] = (byte)Cls[i];
            Pd = new double[N]; R1 = new double[N]; R2 = new double[N]; Shift = new double[N];
            for (int i = 0; i < N; i++)
            {
                var c = Cls[i];
                Pd[i] = TissueTable.ProtonDensity(c);
                double t1 = TissueTable.T1(c, b0), t2 = TissueTable.T2(c, b0);
                R1[i] = t1 > 0 ? 1 / t1 : 0; R2[i] = t2 > 0 ? 1 / t2 : 0; Shift[i] = TissueTable.ShiftPpm(c);
            }
        }

        public double DeltaF(int i, in Currents c) => FieldTables.DeltaF(F[i], c, Shift[i]);

        // Larmor offset with one gradient coil on, as a polynomial in that coil's current: Poly[axis][i*5 + k] (Hz / A^k).
        // The exact offset is the square root of a quadratic in the current whose series converges with ratio ~1e-3 at 1 T
        // (~2e-2 at 0.05 T) over +-100 A, so a quartic through five Chebyshev nodes is exact to well below 0.01 Hz.
        public double[][] Poly; public double PolyB0Current;
        public const int PolyN = 5; static readonly double[] polyNodes = ChebyshevNodes();
        static double[] ChebyshevNodes() { var n = new double[PolyN]; for (int k = 0; k < PolyN; k++) n[k] = 100 * Math.Cos(Math.PI * (k + 0.5) / PolyN); return n; }

        public void BuildPolynomials(double b0Current)
        {
            PolyB0Current = b0Current; Poly = new double[3][];
            for (int axis = 0; axis < 3; axis++)
            {
                var poly = new double[N * PolyN]; Poly[axis] = poly; int ax = axis;
                Parallel.For(0, (N + 511) / 512, Work.Options, chunk =>
                {
                    var v = new double[PolyN]; var coef = new double[PolyN];
                    int e = Math.Min(N, chunk * 512 + 512);
                    for (int i = chunk * 512; i < e; i++)
                    {
                        for (int k = 0; k < PolyN; k++)
                        {
                            double I = polyNodes[k];
                            v[k] = DeltaF(i, new Currents(b0Current, ax == 0 ? I : 0, ax == 1 ? I : 0, ax == 2 ? I : 0));
                        }
                        Interpolate(v, coef);
                        for (int k = 0; k < PolyN; k++) poly[i * PolyN + k] = coef[k];
                    }
                });
            }
        }

        // Newton divided differences converted to monomial coefficients (degree 4).
        static void Interpolate(double[] v, double[] coef)
        {
            var x = polyNodes; var d = (double[])v.Clone();
            for (int j = 1; j < PolyN; j++) for (int k = PolyN - 1; k >= j; k--) d[k] = (d[k] - d[k - 1]) / (x[k] - x[k - j]);
            Array.Clear(coef, 0, PolyN);
            for (int k = PolyN - 1; k >= 0; k--)
            {
                // coef = coef * (X - x[k]) + d[k]
                for (int m = PolyN - 1; m > 0; m--) coef[m] = coef[m - 1] - x[k] * coef[m];
                coef[0] = -x[k] * coef[0] + d[k];
            }
        }

        /// <summary>Which single coil a segment drives (0 x, 1 y, 2 z), -1 for none, -2 for more than one.</summary>
        public static int SegmentAxis(in Segment s)
        {
            bool x = s.X0 != 0 || s.X1 != 0, y = s.Y0 != 0 || s.Y1 != 0, z = s.Z0 != 0 || s.Z1 != 0;
            int n = (x ? 1 : 0) + (y ? 1 : 0) + (z ? 1 : 0);
            if (n == 0) return -1; if (n > 1) return -2;
            return x ? 0 : y ? 1 : 2;
        }

        /// <summary>
        /// Weights w_k such that the integral of the Larmor offset over the first fraction sig of a segment whose single-coil
        /// current goes linearly from i0 to i1 equals sum_k c_k w_k (seconds x A^k). Shared by every isochromat.
        /// </summary>
        public static void PolyWeights(double i0, double i1, double duration, double sig, double[] w)
        {
            double di = i1 - i0;
            if (Math.Abs(di) < 1e-12) { double pk = 1; for (int k = 0; k < PolyN; k++) { w[k] = duration * sig * pk; pk *= i0; } return; }
            double ie = i0 + di * sig, pe = ie, p0 = i0;
            for (int k = 0; k < PolyN; k++) { w[k] = duration / di * (pe - p0) / (k + 1); pe *= ie; p0 *= i0; }
        }

        /// <summary>target[i] += scale * sum_k c_k(i) w_k for every isochromat.</summary>
        public void AddPoly(int axis, double[] w, double[] target, double scale)
        {
            var c = Poly[axis]; double w0 = w[0] * scale, w1 = w[1] * scale, w2 = w[2] * scale, w3 = w[3] * scale, w4 = w[4] * scale;
            for (int i = 0, o = 0; i < N; i++, o += PolyN)
                target[i] += c[o] * w0 + c[o + 1] * w1 + c[o + 2] * w2 + c[o + 3] * w3 + c[o + 4] * w4;
        }

        /// <summary>
        /// Integral of the Larmor offset (Hz s) over the first fraction sig of a segment whose single-coil current goes
        /// linearly from i0 to i1 (exact for the polynomial model).
        /// </summary>
        public double PolyIntegral(int axis, int i, double i0, double i1, double duration, double sig)
        {
            var c = Poly[axis]; int o = i * PolyN; double di = i1 - i0;
            if (Math.Abs(di) < 1e-12)
            {
                double f = c[o] + i0 * (c[o + 1] + i0 * (c[o + 2] + i0 * (c[o + 3] + i0 * c[o + 4])));
                return f * duration * sig;
            }
            double ie = i0 + di * sig, acc = 0, pe = ie, p0 = i0;
            for (int k = 0; k < PolyN; k++) { acc += c[o + k] * (pe - p0) / (k + 1); pe *= ie; p0 *= i0; }
            return acc * duration / di;
        }

        /// <summary>Magnified cube: n^3 cells of size h centred at 'centre' (air cells kept so the grid is complete).</summary>
        public static IsoSet Cube(D3 centre, int n, double h, ISpecimen sp, FieldTables tables, Protocol p) => Grid(centre, n, n, n, h, h, h, sp, tables, p);

        /// <summary>
        /// Magnified block: nx x ny x nz cells of hx x hy x hz centred at 'centre' (air cells kept so the grid is complete).
        /// Lattice index ix + nx (iy + ny iz).
        /// </summary>
        public static IsoSet Grid(D3 centre, int nx, int ny, int nz, double hx, double hy, double hz, ISpecimen sp, FieldTables tables, Protocol p)
        {
            var pts = new List<D3>(); var lat = new List<int>();
            for (int iz = 0; iz < nz; iz++)
                for (int iy = 0; iy < ny; iy++)
                    for (int ix = 0; ix < nx; ix++)
                    {
                        pts.Add(new D3(centre.X + (ix + 0.5 - nx / 2.0) * hx, centre.Y + (iy + 0.5 - ny / 2.0) * hy, centre.Z + (iz + 0.5 - nz / 2.0) * hz));
                        lat.Add(ix + nx * (iy + ny * iz));
                    }
            var s = FromPoints("cube", pts, lat, sp, tables, p, hx, hy, hz, true);
            s.Nx = nx; s.Ny = ny; s.Nz = nz; s.LatticeOrigin = new D3(centre.X + (0.5 - nx / 2.0) * hx, centre.Y + (0.5 - ny / 2.0) * hy, centre.Z + (0.5 - nz / 2.0) * hz);
            s.BuildPolynomials(p.MagnetCurrent);
            return s;
        }

        /// <summary>
        /// The proton volume (0.8.3): count isochromats at random positions in tissue inside a block of the given size centred
        /// at 'centre', no two closer than minSep in coordinates normalised to the display (x, y, z scaled to 5, 5, 4 units
        /// across the block), so each stays distinct when magnified. Deterministic (fixed seed). Lattice holds each point's
        /// index; Nx = Ny = Nz = 0 marks the set as scattered. Cell sizes are the nominal spacing (for display factors only).
        /// </summary>
        public static IsoSet Scatter(D3 centre, D3 size, int count, double minSep, ISpecimen sp, FieldTables tables, Protocol p, int seed = 20260924)
        {
            var rng = new Random(seed); var pts = new List<D3>(); var lat = new List<int>(); var norm = new List<D3>();
            // Separation in units of the mean spacing (the block is shown with one uniform magnification, 0.8.4).
            double spacing = Math.Pow(size.X * size.Y * size.Z / count, 1.0 / 3), sep2 = minSep * minSep;
            for (int attempt = 0; pts.Count < count && attempt < 200000; attempt++)
            {
                double u = rng.NextDouble() - 0.5, v = rng.NextDouble() - 0.5, w = rng.NextDouble() - 0.5;
                var q = new D3(centre.X + u * size.X, centre.Y + v * size.Y, centre.Z + w * size.Z);
                if (sp.ClassAt(q) == TissueClass.Air) continue;
                var nq = new D3(u * size.X / spacing, v * size.Y / spacing, w * size.Z / spacing); bool ok = true;
                foreach (var o in norm) { var d = nq - o; if (D3.Dot(d, d) < sep2) { ok = false; break; } }
                if (!ok) continue;
                pts.Add(q); norm.Add(nq); lat.Add(pts.Count - 1);
            }
            var s = FromPoints("cube", pts, lat, sp, tables, p, spacing, spacing, spacing, false);
            s.Nx = s.Ny = s.Nz = 0; s.LatticeOrigin = centre - size / 2;
            s.BuildPolynomials(p.MagnetCurrent);
            return s;
        }

        /// <summary>Whole specimen on an h lattice whose z planes include the slice centre.</summary>
        public static IsoSet Hand(ISpecimen sp, FieldTables tables, Protocol p, double h)
        {
            sp.Bounds(out D3 min, out D3 max);
            double zs = p.SliceZ;
            int kx0 = (int)Math.Floor(min.X / h), kx1 = (int)Math.Ceiling(max.X / h);
            int ky0 = (int)Math.Floor(min.Y / h), ky1 = (int)Math.Ceiling(max.Y / h);
            int kz0 = (int)Math.Floor((min.Z - zs) / h), kz1 = (int)Math.Ceiling((max.Z - zs) / h);
            int nx = kx1 - kx0 + 1, ny = ky1 - ky0 + 1, nz = kz1 - kz0 + 1;
            var pts = new List<D3>(); var lat = new List<int>();
            for (int iz = 0; iz < nz; iz++)
                for (int iy = 0; iy < ny; iy++)
                    for (int ix = 0; ix < nx; ix++)
                    {
                        pts.Add(new D3((kx0 + ix) * h, (ky0 + iy) * h, zs + (kz0 + iz) * h));
                        lat.Add(ix + nx * (iy + ny * iz));
                    }
            var s = FromPoints("hand", pts, lat, sp, tables, p, h, h, h, false);
            s.Nx = nx; s.Ny = ny; s.Nz = nz; s.LatticeOrigin = new D3(kx0 * h, ky0 * h, zs + kz0 * h);
            s.BuildPolynomials(p.MagnetCurrent);
            return s;
        }

        /// <summary>
        /// The isochromats that produce the measured slice: FOV/128 in-plane, dz/4 through +-1.25 slice thicknesses, tissue
        /// with |b1| >= 10 % of the centre value whose slice-select frequency lies within 1.25 bandwidths of the RF.
        /// (No z gradient acts during the readout, so four samples per slice thickness integrate the profile.)
        /// </summary>
        public static IsoSet Signal(ISpecimen sp, FieldTables tables, Protocol p, double sliceZ)
        {
            double h = Protocol.Fov / 128, dz = p.SliceThickness / 4;
            int nzh = 5;
            var cand = new List<D3>();
            for (int k = -nzh; k <= nzh; k++)
                for (int j = 0; j < 128; j++)
                    for (int i = 0; i < 128; i++)
                        cand.Add(new D3((i - 64) * h, (j - 64) * h, sliceZ + k * dz));
            var s = FromPoints("signal", cand, null, sp, tables, p, h, h, dz, false);
            // Frequency selection with only the slice-select current on.
            var keep = new List<int>();
            var css = new Currents(p.MagnetCurrent, 0, 0, p.GzCurrent);
            double target = Constants.GammaBar * p.Gss * sliceZ, lim = 1.25 * p.BandwidthEff;
            double b1min = 0.1 * Scanner.B1Iso;
            for (int i = 0; i < s.N; i++)
            {
                double b1 = Math.Sqrt(s.F[i].B1Re * s.F[i].B1Re + s.F[i].B1Im * s.F[i].B1Im);
                if (b1 < b1min) continue;
                if (Math.Abs(s.DeltaF(i, css) - target) > lim) continue;
                keep.Add(i);
            }
            var sub = s.Subset(keep);
            sub.BuildPolynomials(p.MagnetCurrent);
            return sub;
        }

        IsoSet Subset(List<int> keep)
        {
            var s = new IsoSet { Name = Name, CellX = CellX, CellY = CellY, CellZ = CellZ, CellVolume = CellVolume, N = keep.Count };
            s.Pos = new D3[s.N]; s.Cls = new TissueClass[s.N]; s.F = new FieldSample[s.N];
            for (int k = 0; k < s.N; k++) { int i = keep[k]; s.Pos[k] = Pos[i]; s.Cls[k] = Cls[i]; s.F[k] = F[i]; }
            s.SetTissue(B0Field);
            return s;
        }
    }

    /// <summary>
    /// Exact per-isochromat effect of one RF pulse (with its gradient and relaxation) on a state with no transverse part:
    /// u+ = uz- * A + B. Valid because every pulse in the program starts from zero transverse magnetization.
    /// </summary>
    public sealed class AffineMap
    {
        public double[] Ax, Ay, Az, Bx, By, Bz;
    }

    public static class Bloch
    {
        /// <summary>One step: right-handed rotation by -2*pi*|w|*dt about w (Hz vector), then exact relaxation.</summary>
        public static void Step(ref double x, ref double y, ref double z, double wx, double wy, double wz, double dt, double e1, double e2, double pd)
        {
            double w = Math.Sqrt(wx * wx + wy * wy + wz * wz);
            if (w > 0)
            {
                double th = -Constants.TwoPi * w * dt, c = Math.Cos(th), s = Math.Sin(th);
                double kx = wx / w, ky = wy / w, kz = wz / w;
                double dot = kx * x + ky * y + kz * z;
                double cx = ky * z - kz * y, cy = kz * x - kx * z, cz = kx * y - ky * x;
                double nx = x * c + cx * s + kx * dot * (1 - c);
                double ny = y * c + cy * s + ky * dot * (1 - c);
                double nz = z * c + cz * s + kz * dot * (1 - c);
                x = nx; y = ny; z = nz;
            }
            x *= e2; y *= e2; z = pd + (z - pd) * e1;
        }

        /// <summary>
        /// Affine RF maps for every isochromat of a set. Isochromats whose frequency is more than farHz from the pulse's band
        /// centre (centreHz) are left untipped and only relax (used for the coarse whole-specimen set; the tip there is below 1 %).
        /// </summary>
        public static AffineMap Map(IsoSet set, RfPulse pulse, in Currents during, double centreHz = 0, double farHz = double.PositiveInfinity)
        {
            var m = new AffineMap { Ax = new double[set.N], Ay = new double[set.N], Az = new double[set.N], Bx = new double[set.N], By = new double[set.N], Bz = new double[set.N] };
            var cur = during;
            int chunk = 512;
            Parallel.For(0, (set.N + chunk - 1) / chunk, Work.Options, c =>
            {
                int e = Math.Min(set.N, c * chunk + chunk);
                for (int i = c * chunk; i < e; i++)
                {
                    double df = set.DeltaF(i, cur);
                    double dt = pulse.Dt, e1 = Math.Exp(-dt * set.R1[i]), e2 = Math.Exp(-dt * set.R2[i]), pd = set.Pd[i];
                    double E1 = Math.Exp(-pulse.Duration * set.R1[i]);
                    if (Math.Abs(df - centreHz) > farHz)
                    {
                        m.Az[i] = E1; m.Bz[i] = pd * (1 - E1); continue;
                    }
                    double b1r = set.F[i].B1Re, b1i = set.F[i].B1Im, g = Constants.GammaBar;
                    double x1 = 0, y1 = 0, z1 = 1, x0 = 0, y0 = 0, z0 = 0;
                    for (int k = 0; k < pulse.Steps; k++)
                    {
                        double ar = pulse.Re[k], ai = pulse.Im[k];
                        double wx = g * (b1r * ar - b1i * ai), wy = g * (b1r * ai + b1i * ar);
                        Step(ref x1, ref y1, ref z1, wx, wy, df, dt, e1, e2, pd);
                        Step(ref x0, ref y0, ref z0, wx, wy, df, dt, e1, e2, pd);
                    }
                    m.Bx[i] = x0; m.By[i] = y0; m.Bz[i] = z0;
                    m.Ax[i] = x1 - x0; m.Ay[i] = y1 - y0; m.Az[i] = z1 - z0;
                }
            });
            return m;
        }

        /// <summary>
        /// Adds scale x (phase in radians over the first fraction sig of segment s) to target for every isochromat.
        /// Uses the shared polynomial weights; falls back to per-isochromat Simpson when the model does not apply.
        /// </summary>
        public static void AddSegmentPhase(IsoSet set, in Segment s, double b0Current, double sig, double[] target, double[] scratch)
        {
            if (Poly(set, s, b0Current, out int ax, out double pi0, out double pi1))
            {
                IsoSet.PolyWeights(pi0, pi1, s.Duration, sig, scratch);
                set.AddPoly(ax, scratch, target, Constants.TwoPi);
                return;
            }
            double t = s.T0 + sig * s.Duration;
            for (int i = 0; i < set.N; i++) target[i] += PartialPhase(set, i, s, b0Current, t);
        }

        static bool Poly(IsoSet set, in Segment s, double b0Current, out int axis, out double i0, out double i1)
        {
            axis = IsoSet.SegmentAxis(s); i0 = i1 = 0;
            if (set.Poly == null || axis == -2 || Math.Abs(set.PolyB0Current - b0Current) > 1e-9) return false;
            if (axis == -1) { axis = 0; return true; }
            i0 = axis == 0 ? s.X0 : axis == 1 ? s.Y0 : s.Z0; i1 = axis == 0 ? s.X1 : axis == 1 ? s.Y1 : s.Z1;
            return true;
        }

        /// <summary>Phase (radians) accumulated by isochromat i over a free segment (polynomial model; Simpson fallback).</summary>
        public static double SegmentPhase(IsoSet set, int i, in Segment s, double b0Current)
        {
            if (Poly(set, s, b0Current, out int ax, out double pi0, out double pi1)) return Constants.TwoPi * set.PolyIntegral(ax, i, pi0, pi1, s.Duration, 1);
            var c0 = new Currents(b0Current, s.X0, s.Y0, s.Z0);
            double f0 = set.DeltaF(i, c0);
            if (s.X0 == s.X1 && s.Y0 == s.Y1 && s.Z0 == s.Z1) return Constants.TwoPi * f0 * s.Duration;
            var cm = new Currents(b0Current, 0.5 * (s.X0 + s.X1), 0.5 * (s.Y0 + s.Y1), 0.5 * (s.Z0 + s.Z1));
            var c1 = new Currents(b0Current, s.X1, s.Y1, s.Z1);
            double fm = set.DeltaF(i, cm), f1 = set.DeltaF(i, c1);
            return Constants.TwoPi * (f0 + 4 * fm + f1) / 6 * s.Duration;
        }

        public static double PartialPhase(IsoSet set, int i, in Segment s, double b0Current, double t)
        {
            double sig = s.Duration > 0 ? Math.Max(0, Math.Min(1, (t - s.T0) / s.Duration)) : 0;
            if (Poly(set, s, b0Current, out int ax, out double pi0, out double pi1)) return Constants.TwoPi * set.PolyIntegral(ax, i, pi0, pi1, s.Duration, sig);
            var c0 = new Currents(b0Current, s.X0, s.Y0, s.Z0);
            double f0 = set.DeltaF(i, c0);
            if (s.X0 == s.X1 && s.Y0 == s.Y1 && s.Z0 == s.Z1) return Constants.TwoPi * f0 * s.Duration * sig;
            var cm = new Currents(b0Current, 0.5 * (s.X0 + s.X1), 0.5 * (s.Y0 + s.Y1), 0.5 * (s.Z0 + s.Z1));
            var c1 = new Currents(b0Current, s.X1, s.Y1, s.Z1);
            double fm = set.DeltaF(i, cm), f1 = set.DeltaF(i, c1);
            double a = f0, b = -3 * f0 + 4 * fm - f1, c = 2 * f0 - 4 * fm + 2 * f1;
            return Constants.TwoPi * s.Duration * (a * sig + b * sig * sig / 2 + c * sig * sig * sig / 3);
        }
    }
}
