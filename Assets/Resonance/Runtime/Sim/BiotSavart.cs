using System;

namespace Nebulytic.Resonance.Sim
{
    /// <summary>Exact magnetostatic fields of the two conductor primitives used by every coil.</summary>
    public static class BiotSavart
    {
        /// <summary>Complete elliptic integrals K(m) and E(m) (parameter m = k^2) by the arithmetic-geometric mean.</summary>
        public static void Elliptic(double m, out double k, out double e)
        {
            double a = 1, b = Math.Sqrt(1 - m), sum = 0.5 * m, pow = 0.5;
            for (int n = 0; n < 40; n++)
            {
                double c = 0.5 * (a - b);
                double an = 0.5 * (a + b); b = Math.Sqrt(a * b); a = an;
                pow *= 2; sum += pow * c * c;
                if (Math.Abs(c) < 1e-16) break;
            }
            k = Math.PI / (2 * a);
            e = k * (1 - sum);
        }

        /// <summary>
        /// Field of a circular filament of radius a in the plane z = z0 carrying current i (positive = counter-clockwise
        /// seen from +z), at cylindrical point (rho, z). Returns B_rho and B_z in tesla.
        /// </summary>
        public static void Loop(double a, double z0, double i, double rho, double z, out double bRho, out double bZ)
        {
            double dz = z - z0;
            double q = (a + rho) * (a + rho) + dz * dz;
            double m = 4 * a * rho / q;
            double d = (a - rho) * (a - rho) + dz * dz;
            double sq = Math.Sqrt(q);
            Elliptic(m, out double kk, out double ee);
            double c = Constants.Mu0 * i / (2 * Math.PI * sq);
            bZ = c * (kk + (a * a - rho * rho - dz * dz) / d * ee);
            if (rho <= 0) { bRho = 0; return; }
            if (m < 1e-8)
            {
                double s = a * a + dz * dz;
                bRho = 3 * Constants.Mu0 * i * a * a * rho * dz / (4 * s * s * Math.Sqrt(s));
                return;
            }
            bRho = c * dz / rho * (-kk + (a * a + rho * rho + dz * dz) / d * ee);
        }

        /// <summary>Field at p of a straight segment from a to b carrying current i (a to b).</summary>
        public static D3 Segment(D3 a, D3 b, D3 p, double i)
        {
            D3 r1 = p - a, r2 = p - b;
            double n1 = r1.Norm, n2 = r2.Norm;
            double den = n1 * n2 * (n1 * n2 + D3.Dot(r1, r2));
            if (den < 1e-30) return D3.Zero;
            return D3.Cross(r1, r2) * (Constants.Mu0 * i / (4 * Math.PI) * (n1 + n2) / den);
        }

        /// <summary>Field of a closed or open polyline carrying current i along its point order.</summary>
        public static D3 Path(D3[] pts, D3 p, double i)
        {
            D3 b = D3.Zero;
            for (int k = 0; k + 1 < pts.Length; k++) b += Segment(pts[k], pts[k + 1], p, i);
            return b;
        }
    }
}
