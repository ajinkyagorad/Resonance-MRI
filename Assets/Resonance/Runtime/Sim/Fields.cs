using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Nebulytic.Resonance.Sim
{
    /// <summary>Fields per ampere at one point: magnet deviation from the isocentre field, three gradient coils, co-rotating B1.</summary>
    public struct FieldSample
    {
        public D3 D0;          // b0(r) - b_iso z-hat, T per ampere of magnet current
        public D3 Gx, Gy, Gz;  // T per ampere
        public double RxRe, RxIm; // peak-envelope receive sensitivity, phase calibrated on the loop axis
        public double B1Re, B1Im; // co-rotating B1 per ampere of peak rung current
    }

    public struct Currents
    {
        public double B0, X, Y, Z;
        public Currents(double b0, double x, double y, double z) { B0 = b0; X = x; Y = y; Z = z; }
    }

    /// <summary>
    /// Field tables baked from the conductor geometry (same code, run on the server), with direct Biot-Savart
    /// evaluation outside the tables. The header carries a hash of the winding definition.
    /// </summary>
    public sealed class FieldTables
    {
        const int Magic = 0x444C4652; // "RFLD"
        const int Version = 4;

        // Axisymmetric magnet and Maxwell tables: rho in [0, AxR], z in [-AxZ, AxZ], 1 mm pitch.
        public const double AxR = 0.150, AxZ = 0.300, AxPitch = 0.001;
        // 3-D table for Gx, Gy, birdcage modes.
        public const double T3Xy = 0.084, T3Z = 0.104, T3Pitch = 0.004;
        // Display grids.
        // The |B| display volume covers the imaging region, where the magnet alone is uniform to < 0.25 mT, so a gradient's
        // +-0.7 mT reads clearly; beyond it the magnet's own end fields (tens of mT) would saturate the display.
        public const double HazeR = 0.120, HazeZ = 0.270; public const int HazeNx = 32, HazeNz = 40;
        public const double B1HazeXy = 0.090, B1HazeZ = 0.130; public const int B1HazeN = 24;

        public int AxNr, AxNz; public float[] Ax; // per node: b0 rho, b0 z - b_iso, gz rho, gz z
        public int Tn, Tnz; public float[] T3;    // per node: gx(3), gy(3), bS(3), bC(3)
        public float[] Haze;                       // HazeNx*HazeNx*HazeNz * 4: (|b0|-b_iso), b0hat.gx, b0hat.gy, b0hat.gz
        public float[] B1Haze;                     // B1HazeN^3: |b1+| per ampere
        public byte[] Hash;

        public static string WindingDefinition()
        {
            var sb = new StringBuilder();
            sb.Append("v").Append(Version).Append(';');
            for (int i = 0; i < Scanner.PackZ.Length; i++) sb.Append(Scanner.PackZ[i].ToString("R")).Append(',').Append(Scanner.PackL[i].ToString("R")).Append(';');
            sb.Append(Scanner.PackRIn.ToString("R")).Append(',').Append(Scanner.PackROut.ToString("R")).Append(',').Append(Scanner.TurnsPerMetre.ToString("R")).Append(';');
            sb.Append(Scanner.PackGaussR).Append('x').Append(Scanner.PackGaussZ).Append(';');
            sb.Append(Scanner.GxRadius.ToString("R")).Append(',').Append(Scanner.GyRadius.ToString("R")).Append(',').Append(Scanner.GzRadius.ToString("R")).Append(';');
            sb.Append(Scanner.GxTurns).Append(',').Append(Scanner.GyTurns).Append(',').Append(Scanner.GzTurns).Append(';');
            sb.Append(Scanner.BirdcageRadius.ToString("R")).Append(',').Append(Scanner.BirdcageHalfLength.ToString("R")).Append(',').Append(Scanner.Rungs).Append(',').Append(Scanner.RingArcSegments);
            sb.Append(";rx:").Append(Scanner.ReceiveRadius.ToString("R")).Append(",").Append(Scanner.ReceiveY.ToString("R"));
            return sb.ToString();
        }

        public static byte[] DefinitionHash()
        {
            using (var sha = SHA256.Create()) return sha.ComputeHash(Encoding.UTF8.GetBytes(WindingDefinition()));
        }

        // ---------------------------------------------------------------- bake

        public static FieldTables Bake(Action<string> log = null)
        {
            var t = new FieldTables { Hash = DefinitionHash() };
            t.AxNr = (int)Math.Round(AxR / AxPitch) + 1; t.AxNz = (int)Math.Round(2 * AxZ / AxPitch) + 1;
            t.Ax = new float[t.AxNr * t.AxNz * 4];
            double bIso = Scanner.BIso;
            System.Threading.Tasks.Parallel.For(0, t.AxNz, iz =>
            {
                double z = -AxZ + iz * AxPitch;
                for (int ir = 0; ir < t.AxNr; ir++)
                {
                    double rho = ir * AxPitch;
                    Scanner.MagnetField(rho, z, out double br, out double bz);
                    Scanner.MaxwellField(rho, z, out double gr, out double gz);
                    int k = (iz * t.AxNr + ir) * 4;
                    t.Ax[k] = (float)br; t.Ax[k + 1] = (float)(bz - bIso); t.Ax[k + 2] = (float)gr; t.Ax[k + 3] = (float)gz;
                }
            });
            log?.Invoke("axisymmetric table done");
            t.Tn = (int)Math.Round(2 * T3Xy / T3Pitch) + 1; t.Tnz = (int)Math.Round(2 * T3Z / T3Pitch) + 1;
            t.T3 = new float[t.Tn * t.Tn * t.Tnz * 12];
            System.Threading.Tasks.Parallel.For(0, t.Tnz, iz =>
            {
                for (int iy = 0; iy < t.Tn; iy++)
                    for (int ix = 0; ix < t.Tn; ix++)
                    {
                        var p = new D3(-T3Xy + ix * T3Pitch, -T3Xy + iy * T3Pitch, -T3Z + iz * T3Pitch);
                        D3 gx = Scanner.GradientField(CoilId.Gx, p), gy = Scanner.GradientField(CoilId.Gy, p);
                        Scanner.BirdcageModes(p, out D3 bs, out D3 bc);
                        int k = ((iz * t.Tn + iy) * t.Tn + ix) * 12;
                        Put(t.T3, k, gx); Put(t.T3, k + 3, gy); Put(t.T3, k + 6, bs); Put(t.T3, k + 9, bc);
                    }
            });
            log?.Invoke("3-D table done");
            t.Haze = new float[HazeNx * HazeNx * HazeNz * 4];
            System.Threading.Tasks.Parallel.For(0, HazeNz, iz =>
            {
                for (int iy = 0; iy < HazeNx; iy++)
                    for (int ix = 0; ix < HazeNx; ix++)
                    {
                        var p = HazePoint(ix, iy, iz);
                        double rho = Math.Sqrt(p.X * p.X + p.Y * p.Y);
                        Scanner.MagnetField(rho, p.Z, out double br, out double bz);
                        D3 b0 = Scanner.Cylindrical(br, bz, p);
                        double n = b0.Norm; D3 u = n > 0 ? b0 / n : new D3(0, 0, 1);
                        int k = ((iz * HazeNx + iy) * HazeNx + ix) * 4;
                        t.Haze[k] = (float)(n - bIso);
                        t.Haze[k + 1] = (float)D3.Dot(u, Scanner.GradientField(CoilId.Gx, p));
                        t.Haze[k + 2] = (float)D3.Dot(u, Scanner.GradientField(CoilId.Gy, p));
                        t.Haze[k + 3] = (float)D3.Dot(u, Scanner.GradientField(CoilId.Gz, p));
                    }
            });
            t.B1Haze = new float[B1HazeN * B1HazeN * B1HazeN];
            System.Threading.Tasks.Parallel.For(0, B1HazeN, iz =>
            {
                for (int iy = 0; iy < B1HazeN; iy++)
                    for (int ix = 0; ix < B1HazeN; ix++)
                    {
                        var p = B1HazePoint(ix, iy, iz);
                        Scanner.BirdcageModes(p, out D3 bs, out D3 bc);
                        Scanner.B1Plus(bs, bc, out double re, out double im);
                        t.B1Haze[(iz * B1HazeN + iy) * B1HazeN + ix] = (float)Math.Sqrt(re * re + im * im);
                    }
            });
            log?.Invoke("display grids done");
            return t;
        }

        static void Put(float[] a, int k, D3 v) { a[k] = (float)v.X; a[k + 1] = (float)v.Y; a[k + 2] = (float)v.Z; }

        public static D3 HazePoint(int ix, int iy, int iz) =>
            new D3(-HazeR + 2 * HazeR * ix / (HazeNx - 1.0), -HazeR + 2 * HazeR * iy / (HazeNx - 1.0), -HazeZ + 2 * HazeZ * iz / (HazeNz - 1.0));

        public static D3 B1HazePoint(int ix, int iy, int iz) =>
            new D3(-B1HazeXy + 2 * B1HazeXy * ix / (B1HazeN - 1.0), -B1HazeXy + 2 * B1HazeXy * iy / (B1HazeN - 1.0), -B1HazeZ + 2 * B1HazeZ * iz / (B1HazeN - 1.0));

        // ---------------------------------------------------------------- serialization

        public byte[] Serialize()
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Magic); w.Write(Version); w.Write(Hash);
                w.Write(AxNr); w.Write(AxNz); WriteFloats(w, Ax);
                w.Write(Tn); w.Write(Tnz); WriteFloats(w, T3);
                WriteFloats(w, Haze); WriteFloats(w, B1Haze);
                w.Flush(); return ms.ToArray();
            }
        }

        static void WriteFloats(BinaryWriter w, float[] a)
        {
            w.Write(a.Length);
            var bytes = new byte[a.Length * 4]; Buffer.BlockCopy(a, 0, bytes, 0, bytes.Length); w.Write(bytes);
        }

        static float[] ReadFloats(BinaryReader r)
        {
            int n = r.ReadInt32(); var bytes = r.ReadBytes(n * 4); var a = new float[n];
            Buffer.BlockCopy(bytes, 0, a, 0, bytes.Length); return a;
        }

        public static FieldTables Load(byte[] data)
        {
            using (var r = new BinaryReader(new MemoryStream(data)))
            {
                if (r.ReadInt32() != Magic || r.ReadInt32() != Version) throw new InvalidDataException("Field tables: wrong format");
                var t = new FieldTables { Hash = r.ReadBytes(32) };
                var expected = DefinitionHash();
                for (int i = 0; i < 32; i++) if (t.Hash[i] != expected[i]) throw new InvalidDataException("Field tables were baked for different windings; rebake.");
                t.AxNr = r.ReadInt32(); t.AxNz = r.ReadInt32(); t.Ax = ReadFloats(r);
                t.Tn = r.ReadInt32(); t.Tnz = r.ReadInt32(); t.T3 = ReadFloats(r);
                t.Haze = ReadFloats(r); t.B1Haze = ReadFloats(r);
                return t;
            }
        }

        // ---------------------------------------------------------------- sampling

        /// <summary>All per-ampere fields at p, from the tables where p lies inside them and by direct Biot-Savart otherwise.</summary>
        public void Sample(D3 p, out FieldSample s)
        {
            s = default;
            // For lab Mx=A cos(wt), My=-A sin(wt), induced voltage has peak envelope
            // w*A*(By+i Bx) in the exp(-iwt) convention. Receiver applies conj(Rx).
            var rx = Scanner.ReceiveField(p); s.RxRe = rx.Y; s.RxIm = -rx.X;
            double rho = Math.Sqrt(p.X * p.X + p.Y * p.Y);
            double b0r, b0z, gzr, gzz;
            if (rho <= AxR - AxPitch && Math.Abs(p.Z) <= AxZ - AxPitch)
            {
                double fr = rho / AxPitch, fz = (p.Z + AxZ) / AxPitch;
                int ir = (int)fr, iz = (int)fz; double ar = fr - ir, az = fz - iz;
                b0r = Bilinear(0, ir, iz, ar, az); b0z = Bilinear(1, ir, iz, ar, az);
                gzr = Bilinear(2, ir, iz, ar, az); gzz = Bilinear(3, ir, iz, ar, az);
            }
            else
            {
                Scanner.MagnetField(rho, p.Z, out b0r, out double bz); b0z = bz - Scanner.BIso;
                Scanner.MaxwellField(rho, p.Z, out gzr, out gzz);
            }
            s.D0 = Scanner.Cylindrical(b0r, b0z, p);
            s.Gz = Scanner.Cylindrical(gzr, gzz, p);
            // Near the birdcage conductors (rho > 60 mm) the fields vary too fast for the 4 mm table: compute directly.
            if (rho <= 0.060 && Math.Abs(p.Z) <= T3Z)
            {
                double fx = Math.Min((p.X + T3Xy) / T3Pitch, Tn - 1.000001), fy = Math.Min((p.Y + T3Xy) / T3Pitch, Tn - 1.000001), fz = Math.Min((p.Z + T3Z) / T3Pitch, Tnz - 1.000001);
                int ix = (int)fx, iy = (int)fy, iz = (int)fz; double ax = fx - ix, ay = fy - iy, az = fz - iz;
                s.Gx = Trilinear(0, ix, iy, iz, ax, ay, az); s.Gy = Trilinear(3, ix, iy, iz, ax, ay, az);
                D3 bs = Trilinear(6, ix, iy, iz, ax, ay, az), bc = Trilinear(9, ix, iy, iz, ax, ay, az);
                Scanner.B1Plus(bs, bc, out s.B1Re, out s.B1Im);
            }
            else
            {
                s.Gx = Scanner.GradientField(CoilId.Gx, p); s.Gy = Scanner.GradientField(CoilId.Gy, p);
                Scanner.BirdcageModes(p, out D3 bs, out D3 bc);
                Scanner.B1Plus(bs, bc, out s.B1Re, out s.B1Im);
            }
        }

        double Bilinear(int c, int ir, int iz, double ar, double az)
        {
            int n = AxNr;
            double v00 = Ax[(iz * n + ir) * 4 + c], v10 = Ax[(iz * n + ir + 1) * 4 + c];
            double v01 = Ax[((iz + 1) * n + ir) * 4 + c], v11 = Ax[((iz + 1) * n + ir + 1) * 4 + c];
            return (v00 * (1 - ar) + v10 * ar) * (1 - az) + (v01 * (1 - ar) + v11 * ar) * az;
        }

        D3 Trilinear(int c, int ix, int iy, int iz, double ax, double ay, double az)
        {
            double x = 0, y = 0, z = 0;
            for (int dz = 0; dz < 2; dz++)
                for (int dy = 0; dy < 2; dy++)
                    for (int dx = 0; dx < 2; dx++)
                    {
                        double w = (dx == 0 ? 1 - ax : ax) * (dy == 0 ? 1 - ay : ay) * (dz == 0 ? 1 - az : az);
                        int k = (((iz + dz) * Tn + iy + dy) * Tn + ix + dx) * 12 + c;
                        x += w * T3[k]; y += w * T3[k + 1]; z += w * T3[k + 2];
                    }
            return new D3(x, y, z);
        }

        /// <summary>
        /// Local Larmor frequency minus f_ref (Hz) in a numerically stable form (SPEC 3.3), including concomitant terms and
        /// the chemical shift of the tissue.
        /// </summary>
        public static double DeltaF(in FieldSample s, in Currents c, double shiftPpm)
        {
            double bRef = c.B0 * Scanner.BIso;
            double dx = c.B0 * s.D0.X + c.X * s.Gx.X + c.Y * s.Gy.X + c.Z * s.Gz.X;
            double dy = c.B0 * s.D0.Y + c.X * s.Gx.Y + c.Y * s.Gy.Y + c.Z * s.Gz.Y;
            double dz = c.B0 * s.D0.Z + c.X * s.Gx.Z + c.Y * s.Gy.Z + c.Z * s.Gz.Z;
            double num = 2 * bRef * dz + dx * dx + dy * dy + dz * dz;
            double den = Math.Sqrt((bRef + dz) * (bRef + dz) + dx * dx + dy * dy) + bRef;
            return Constants.GammaBar * (num / den + shiftPpm * 1e-6 * bRef);
        }
    }
}
