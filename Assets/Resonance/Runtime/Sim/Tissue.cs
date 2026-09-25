using System;
using System.IO;

namespace Nebulytic.Resonance.Sim
{
    public enum TissueClass : byte { Air = 0, Skin = 1, Fat = 2, Muscle = 3, Tendon = 4, Marrow = 5, Cortex = 6, Soft = 7, Phantom = 8 }

    /// <summary>
    /// Relaxation and proton-density values per class. Each relaxation time is interpolated log-log between two measured
    /// anchors (SPEC 3.4, draft A.5.1 sources); values marked as estimates there stay estimates.
    /// </summary>
    public static class TissueTable
    {
        public const int Count = 9;
        public static readonly string[] Names = { "air", "skin", "fat", "muscle", "tendon", "marrow", "cortex", "soft", "phantom" };
        // PD relative to water; T1 anchors (T at 0.05 T, T at 1.5 T) in seconds; T2 anchors; shift ppm.
        static readonly double[] pd = { 0, 0.70, 0.95, 0.80, 0.70, 0.85, 0.20, 0.80, 1.00 };
        static readonly double[,] t1 = { { 0, 0 }, { 0.171, 1.130 }, { 0.130, 0.288 }, { 0.171, 1.130 }, { 0.600, 0.600 }, { 0.130, 0.288 }, { 0.140, 0.140 }, { 0.171, 1.130 }, { 0.300, 0.300 } };
        static readonly double[,] t2 = { { 0, 0 }, { 0.010, 0.010 }, { 0.090, 0.165 }, { 0.039, 0.0353 }, { 0, 0 }, { 0.090, 0.165 }, { 0.0004, 0.0004 }, { 0.039, 0.0353 }, { 0.250, 0.250 } };
        static readonly double[] shift = { 0, 0, Constants.FatShiftPpm, 0, 0, Constants.FatShiftPpm, 0, 0, 0 };

        public static double ProtonDensity(TissueClass c) => pd[(int)c];
        public static double ShiftPpm(TissueClass c) => shift[(int)c];

        static double LogLog(double a, double b, double ba, double bb, double field)
        {
            if (a <= 0 || b <= 0) return 0;
            if (Math.Abs(a - b) < 1e-15) return a;
            double f = Math.Min(1.5, Math.Max(0.05, field));
            return a * Math.Pow(f / ba, Math.Log(b / a) / Math.Log(bb / ba));
        }

        public static double T1(TissueClass c, double field) => LogLog(t1[(int)c, 0], t1[(int)c, 1], 0.05, 1.5, field);

        public static double T2(TissueClass c, double field)
        {
            // Tendon: short T2* anchors 1.83 ms at 0.35 T and 1.2 ms at 3 T.
            if (c == TissueClass.Tendon) return LogLog(0.00183, 0.0012, 0.35, 3.0, Math.Min(3, Math.Max(0.05, field)));
            return LogLog(t2[(int)c, 0], t2[(int)c, 1], 0.05, 1.5, field);
        }
    }

    /// <summary>The segmented hand (BodyParts3D-derived label volume) and its rigid pose in the scanner.</summary>
    public sealed class HandLabels
    {
        public int Nx, Ny, Nz; public double Pitch; public D3 Corner; // object frame, metres
        public byte[] Labels;

        public static HandLabels Load(byte[] data)
        {
            using (var r = new BinaryReader(new MemoryStream(data)))
            {
                var magic = new string(r.ReadChars(4));
                if (magic != "RNML") throw new InvalidDataException("HandLabels: bad magic");
                var h = new HandLabels { Nx = r.ReadInt32(), Ny = r.ReadInt32(), Nz = r.ReadInt32(), Pitch = r.ReadSingle() };
                h.Corner = new D3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                h.Labels = r.ReadBytes(h.Nx * h.Ny * h.Nz);
                return h;
            }
        }

        public TissueClass ClassAtObject(D3 o)
        {
            int ix = (int)Math.Floor((o.X - Corner.X) / Pitch), iy = (int)Math.Floor((o.Y - Corner.Y) / Pitch), iz = (int)Math.Floor((o.Z - Corner.Z) / Pitch);
            if (ix < 0 || iy < 0 || iz < 0 || ix >= Nx || iy >= Ny || iz >= Nz) return TissueClass.Air;
            return (TissueClass)Labels[(iz * Ny + iy) * Nx + ix];
        }

        public D3 Centre(int ix, int iy, int iz) => new D3(Corner.X + (ix + 0.5) * Pitch, Corner.Y + (iy + 0.5) * Pitch, Corner.Z + (iz + 0.5) * Pitch);
    }

    /// <summary>
    /// Rigid pose of the specimen: object frame (x, y dorsal, z toward the fingertips) to scanner frame P.
    /// The hand lies along the bore with the fingertips toward -z (the user's left) and the dorsum up.
    /// </summary>
    public struct SpecimenPose
    {
        public D3 ObjectAtIsocentre; // object-frame point placed at the scanner isocentre
        public D3 Offset;            // additional scanner-frame translation (user moves the hand)

        public D3 ToScanner(D3 o)
        {
            D3 d = o - ObjectAtIsocentre;
            return new D3(-d.X, d.Y, -d.Z) + Offset; // 180 degrees about y: proper rotation
        }

        public D3 ToObject(D3 p)
        {
            D3 d = p - Offset;
            return new D3(-d.X, d.Y, -d.Z) + ObjectAtIsocentre;
        }
    }

    /// <summary>Anything that can be put in the bore.</summary>
    public interface ISpecimen
    {
        TissueClass ClassAt(D3 scannerPoint);
        /// <summary>Scanner-frame bounds of tissue (min, max).</summary>
        void Bounds(out D3 min, out D3 max);
    }

    public sealed class HandSpecimen : ISpecimen
    {
        public readonly HandLabels Labels; public SpecimenPose Pose;
        public HandSpecimen(HandLabels labels, SpecimenPose pose) { Labels = labels; Pose = pose; }
        public TissueClass ClassAt(D3 p) => Labels.ClassAtObject(Pose.ToObject(p));
        public void Bounds(out D3 min, out D3 max)
        {
            D3 a = Pose.ToScanner(Labels.Corner), b = Pose.ToScanner(Labels.Corner + new D3(Labels.Nx, Labels.Ny, Labels.Nz) * Labels.Pitch);
            min = new D3(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z));
            max = new D3(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
        }
    }

    /// <summary>Doped-water cylinder along z, radius 30 mm, length 100 mm (a test object).</summary>
    public sealed class CylinderPhantom : ISpecimen
    {
        public double Radius = 0.030, HalfLength = 0.050; public D3 Offset;
        public TissueClass ClassAt(D3 p)
        {
            D3 d = p - Offset;
            return d.X * d.X + d.Y * d.Y <= Radius * Radius && Math.Abs(d.Z) <= HalfLength ? TissueClass.Phantom : TissueClass.Air;
        }
        public void Bounds(out D3 min, out D3 max) { min = Offset - new D3(Radius, Radius, HalfLength); max = Offset + new D3(Radius, Radius, HalfLength); }
    }
}
