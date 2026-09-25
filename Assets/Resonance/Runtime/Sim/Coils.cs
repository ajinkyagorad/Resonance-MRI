using System;
using System.Collections.Generic;

namespace Nebulytic.Resonance.Sim
{
    public enum CoilId { B0 = 0, Gx = 1, Gy = 2, Gz = 3, Rf = 4 }

    /// <summary>A wound pack of the main magnet: rectangular cross-section, turns in series, sense +1 = counter-clockwise seen from +z.</summary>
    public sealed class Pack
    {
        public double RIn, ROut, ZCentre, Length; public double Turns; public int Sense;
    }

    /// <summary>A conductor path as drawn and as computed. Current flows along the point order; Sign multiplies the coil current.</summary>
    public sealed class Conductor
    {
        public CoilId Coil; public int Part; public D3[] Points; public int Turns; public double Sign;
    }

    /// <summary>
    /// The scanner's windings. Every field in the application is computed from these conductors and their currents.
    /// Geometry follows the resolved specification (SPEC.md section 3.2).
    /// </summary>
    public static class Scanner
    {
        // Main magnet: three mirrored NbTi packs, r 220-250 mm, 1.44 x 1.44 mm conductor (14.47 turns per mm of length).
        public const double PackRIn = 0.220, PackROut = 0.250;
        public const double TurnsPerMetre = 30.0 / (1.44 * 1.44) * 1000.0;
        // z-centre and axial length of the +z pack of each pair (metres). Optimised on the 12 and 14 cm spheres with the same
        // Gauss-Legendre pack model used here: 0.58 ppm over 12 cm, 1.9 ppm over 14 cm (Tools/simcore test FLD2).
        // Homogeneity is extremely sensitive to placement (rounding to 10 um costs ~5 ppm); real magnets are shimmed,
        // this model uses the exact design positions.
        public static readonly double[] PackZ = { 0.0363073195, 0.1265089072, 0.3011373731 };
        public static readonly double[] PackL = { 0.0332636648, 0.0492551990, 0.1321808615 };
        public const int PackGaussR = 4, PackGaussZ = 8;

        public const double GxRadius = 0.155, GyRadius = 0.160, GzRadius = 0.165;
        public const int GxTurns = 4, GyTurns = 4, GzTurns = 5;
        public const double BirdcageRadius = 0.130, BirdcageHalfLength = 0.270;
        public const double ReceiveRadius = 0.055, ReceiveY = 0.050; // dorsal surface loop, normal +y
        public const int Rungs = 12, RingArcSegments = 6;
        public const double MaxGradientCurrent = 100, Slew = 60; // A, T/m/s

        public static readonly Pack[] Packs;
        public static readonly Conductor[] GradientPaths; // Gx, Gy saddles and Gz loops
        public static readonly Conductor[] BirdcageParts; // 12 rungs, 12 +z ring arcs, 12 -z ring arcs
        static readonly double[] rungPhi = new double[Rungs];
        static readonly double[] gaussRx, gaussRw, gaussZx, gaussZw;

        /// <summary>Field magnitude at the isocentre per ampere of magnet current (T/A).</summary>
        public static readonly double BIso;
        /// <summary>Gradient efficiencies at the isocentre, T/m per ampere.</summary>
        public static readonly double EtaX, EtaY, EtaZ;
        /// <summary>Co-rotating B1 per ampere of peak rung current at the isocentre (T/A), real by construction.</summary>
        public static readonly double B1Iso;

        static Scanner()
        {
            var packs = new List<Pack>();
            for (int i = 0; i < PackZ.Length; i++)
                foreach (int s in new[] { 1, -1 })
                    packs.Add(new Pack { RIn = PackRIn, ROut = PackROut, ZCentre = s * PackZ[i], Length = PackL[i], Turns = TurnsPerMetre * PackL[i], Sense = 1 });
            Packs = packs.ToArray();
            Numerics.GaussLegendre(PackGaussR, out gaussRx, out gaussRw);
            Numerics.GaussLegendre(PackGaussZ, out gaussZx, out gaussZw);

            var paths = new List<Conductor>();
            int part = 0;
            foreach (var (coil, a, rot) in new[] { (CoilId.Gx, GxRadius, 0.0), (CoilId.Gy, GyRadius, Math.PI / 2) })
                foreach (int zs in new[] { 1, -1 })
                    foreach (int side in new[] { 1, -1 })
                    {
                        double phic = rot + (side > 0 ? 0 : Math.PI);
                        paths.Add(new Conductor { Coil = coil, Part = part++, Points = Saddle(a, phic, zs * 0.389 * a, zs * 2.2 * a), Turns = coil == CoilId.Gx ? GxTurns : GyTurns, Sign = side });
                    }
            double dz = Math.Sqrt(3) / 2 * GzRadius;
            paths.Add(new Conductor { Coil = CoilId.Gz, Part = part++, Points = Circle(GzRadius, dz, 96), Turns = GzTurns, Sign = 1 });
            paths.Add(new Conductor { Coil = CoilId.Gz, Part = part++, Points = Circle(GzRadius, -dz, 96), Turns = GzTurns, Sign = -1 });
            GradientPaths = paths.ToArray();

            var cage = new List<Conductor>();
            for (int n = 0; n < Rungs; n++)
            {
                rungPhi[n] = n * Constants.TwoPi / Rungs;
                double c = Math.Cos(rungPhi[n]) * BirdcageRadius, s = Math.Sin(rungPhi[n]) * BirdcageRadius;
                cage.Add(new Conductor { Coil = CoilId.Rf, Part = n, Points = new[] { new D3(c, s, -BirdcageHalfLength), new D3(c, s, BirdcageHalfLength) }, Turns = 1, Sign = 1 });
            }
            foreach (int end in new[] { 1, -1 })
                for (int k = 0; k < Rungs; k++)
                {
                    var pts = new D3[RingArcSegments + 1];
                    for (int j = 0; j <= RingArcSegments; j++)
                    {
                        double ph = rungPhi[k] + j * (Constants.TwoPi / Rungs) / RingArcSegments;
                        pts[j] = new D3(BirdcageRadius * Math.Cos(ph), BirdcageRadius * Math.Sin(ph), end * BirdcageHalfLength);
                    }
                    cage.Add(new Conductor { Coil = CoilId.Rf, Part = Rungs + (end > 0 ? 0 : Rungs) + k, Points = pts, Turns = 1, Sign = end });
                }
            BirdcageParts = cage.ToArray();

            MagnetField(0, 0, out _, out BIso);
            const double h = 1e-3;
            EtaX = (GradientField(CoilId.Gx, new D3(h, 0, 0)).Z - GradientField(CoilId.Gx, new D3(-h, 0, 0)).Z) / (2 * h);
            EtaY = (GradientField(CoilId.Gy, new D3(0, h, 0)).Z - GradientField(CoilId.Gy, new D3(0, -h, 0)).Z) / (2 * h);
            EtaZ = (GradientField(CoilId.Gz, new D3(0, 0, h)).Z - GradientField(CoilId.Gz, new D3(0, 0, -h)).Z) / (2 * h);
            BirdcageModes(D3.Zero, out D3 bs, out D3 bc);
            B1Iso = 0.5 * (bs.X - bc.Y);
        }

        static D3[] Saddle(double a, double phic, double zin, double zout)
        {
            const int n = 12;
            var pts = new List<D3>();
            for (int j = 0; j <= n; j++) { double ph = phic - Math.PI / 3 + j * (2 * Math.PI / 3) / n; pts.Add(new D3(a * Math.Cos(ph), a * Math.Sin(ph), zin)); }
            for (int j = 0; j <= n; j++) { double ph = phic + Math.PI / 3 - j * (2 * Math.PI / 3) / n; pts.Add(new D3(a * Math.Cos(ph), a * Math.Sin(ph), zout)); }
            pts.Add(pts[0]);
            return pts.ToArray();
        }

        static D3[] Circle(double a, double z, int n)
        {
            var pts = new D3[n + 1];
            for (int j = 0; j <= n; j++) { double ph = j * Constants.TwoPi / n; pts[j] = new D3(a * Math.Cos(ph), a * Math.Sin(ph), z); }
            return pts;
        }

        /// <summary>Main-magnet field per ampere at cylindrical (rho, z): Gauss-Legendre quadrature over every pack cross-section.</summary>
        public static void MagnetField(double rho, double z, out double bRho, out double bZ)
        {
            bRho = 0; bZ = 0;
            foreach (var p in Packs)
            {
                double rc = 0.5 * (p.RIn + p.ROut), rh = 0.5 * (p.ROut - p.RIn), zh = 0.5 * p.Length;
                for (int i = 0; i < gaussRx.Length; i++)
                    for (int j = 0; j < gaussZx.Length; j++)
                    {
                        double w = gaussRw[i] * gaussZw[j] * 0.25 * p.Turns * p.Sense;
                        BiotSavart.Loop(rc + gaussRx[i] * rh, p.ZCentre + gaussZx[j] * zh, w, rho, z, out double br, out double bz);
                        bRho += br; bZ += bz;
                    }
            }
        }

        /// <summary>Maxwell-pair (Gz) field per ampere at cylindrical (rho, z), using the exact loop formula.</summary>
        public static void MaxwellField(double rho, double z, out double bRho, out double bZ)
        {
            double dz = Math.Sqrt(3) / 2 * GzRadius;
            BiotSavart.Loop(GzRadius, dz, GzTurns, rho, z, out double r1, out double z1);
            BiotSavart.Loop(GzRadius, -dz, -GzTurns, rho, z, out double r2, out double z2);
            bRho = r1 + r2; bZ = z1 + z2;
        }

        public static D3 Cylindrical(double bRho, double bZ, D3 p)
        {
            double rho = Math.Sqrt(p.X * p.X + p.Y * p.Y);
            if (rho < 1e-12) return new D3(0, 0, bZ);
            return new D3(bRho * p.X / rho, bRho * p.Y / rho, bZ);
        }

        /// <summary>Field per ampere of coil current at p. Gz uses the loop formula; Gx and Gy use exact straight segments.</summary>
        public static D3 GradientField(CoilId coil, D3 p)
        {
            if (coil == CoilId.Gz)
            {
                double rho = Math.Sqrt(p.X * p.X + p.Y * p.Y);
                MaxwellField(rho, p.Z, out double br, out double bz);
                return Cylindrical(br, bz, p);
            }
            D3 b = D3.Zero;
            foreach (var c in GradientPaths)
                if (c.Coil == coil) b += BiotSavart.Path(c.Points, p, c.Sign * c.Turns);
            return b;
        }

        /// <summary>Unit mode patterns: port S has rung currents sin(phi_n), port C cos(phi_n) (peak 1 A).</summary>
        public static double RungMode(int n, bool cosine) => cosine ? Math.Cos(rungPhi[n]) : Math.Sin(rungPhi[n]);

        /// <summary>Kirchhoff-consistent end-ring segment current J_k (from rung k to k+1 at +z; the -z ring carries -J_k).</summary>
        public static double RingMode(int k, bool cosine)
        {
            double mean = 0, acc = 0; var cum = new double[Rungs];
            for (int j = 0; j < Rungs; j++) { acc += RungMode(j, cosine); cum[j] = acc; mean += acc; }
            mean /= Rungs;
            return cum[k] - mean;
        }

        /// <summary>Current (A) of a birdcage part for rung-current pattern amplitude s on port S and c on port C.</summary>
        public static double PartCurrent(int part, double s, double c)
        {
            if (part < Rungs) return s * RungMode(part, false) + c * RungMode(part, true);
            int k = (part - Rungs) % Rungs; double sign = part < 2 * Rungs ? 1 : -1;
            return sign * (s * RingMode(k, false) + c * RingMode(k, true));
        }

        public static void BirdcageModes(D3 p, out D3 bS, out D3 bC)
        {
            bS = D3.Zero; bC = D3.Zero;
            foreach (var c in BirdcageParts)
            {
                D3 unit = BiotSavart.Path(c.Points, p, 1);
                double js = PartCurrent(c.Part, 1, 0), jc = PartCurrent(c.Part, 0, 1);
                bS += unit * js; bC += unit * jc;
            }
        }

        /// <summary>Co-rotating (B1+) complex field per ampere from the two mode fields: 0.5[(bSx - bCy) + i(bSy + bCx)].</summary>
        public static void B1Plus(D3 bS, D3 bC, out double re, out double im)
        {
            re = 0.5 * (bS.X - bC.Y); im = 0.5 * (bS.Y + bC.X);
        }

        /// <summary>Counter-rotating component magnitude (for the off-axis quality check).</summary>
        public static double B1Minus(D3 bS, D3 bC)
        {
            double re = 0.5 * (bS.X + bC.Y), im = 0.5 * (bS.Y - bC.X);
            return Math.Sqrt(re * re + im * im);
        }

        /// <summary>Unit-current receive loop field. The loop lies in x-z above the hand; its normal is +y,
        /// transverse to B0. Quasistatic reciprocity, evaluated independently of the transmit birdcage.</summary>
        public static D3 ReceiveField(D3 p)
        {
            double rho = Math.Sqrt(p.X * p.X + p.Z * p.Z);
            BiotSavart.Loop(ReceiveRadius, ReceiveY, 1, rho, p.Y, out double br, out double by);
            return rho > 1e-12 ? new D3(br * p.X / rho, by, br * p.Z / rho) : new D3(0, by, 0);
        }

        public static D3[] ReceivePath(int segments = 96)
        {
            var pts = new D3[segments + 1];
            for (int k = 0; k <= segments; k++) { double a = Constants.TwoPi * k / segments; pts[k] = new D3(ReceiveRadius * Math.Cos(a), ReceiveY, -ReceiveRadius * Math.Sin(a)); }
            return pts;
        }

        /// <summary>Rung angle phi_n of rung n (rung 0 on +x).</summary>
        public static double RungAngle(int n) => rungPhi[n];
    }
}
