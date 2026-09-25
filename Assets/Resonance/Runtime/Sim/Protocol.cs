using System;

namespace Nebulytic.Resonance.Sim
{
    /// <summary>The adjustable acquisition parameters and everything derived from them (SPEC 3.7).</summary>
    public sealed class Protocol
    {
        // Adjustable.
        public double B0 = 1.0;             // T
        public double RfBandwidth = 1500;   // Hz (nominal, time-bandwidth 4 sinc)
        public double GzCurrent = DefaultGzCurrent; // A, slice-select
        public double SliceZ = 0;           // m
        public int Matrix = 64;

        // Fixed.
        public const double Fov = 0.128, RxBandwidth = 32000, Tr = 3.0, Raster = 10e-6;
        public const double Tbw = 4, HardPulse = 0.5e-3, HardFlip = Math.PI / 2;
        public const double DemoTurnsLength = 0.016, DemoTurns = 2; // Gz demonstration: two turns across the 16 mm cube
        public const double MinB0 = 0.05, MaxB0 = 1.0, MinBandwidth = 500, MaxBandwidth = 5000, MinGz = 6.8, MaxGz = 100, MaxSlice = 0.040;
        public static readonly int[] Matrices = { 32, 64, 128 };
        public static double DefaultGzCurrent => 1500.0 / (Constants.GammaBar * 0.005) / Scanner.EtaZ;

        public Protocol Clone() => (Protocol)MemberwiseClone();

        public bool IsDefault => Math.Abs(B0 - 1.0) < 1e-9 && Math.Abs(RfBandwidth - 1500) < 1e-6 && Math.Abs(GzCurrent - DefaultGzCurrent) < 1e-6 && Math.Abs(SliceZ) < 1e-9 && Matrix == 64;

        public double MagnetCurrent => B0 / Scanner.BIso;
        public double Fref => Constants.GammaBar * B0;
        public double Trf => Math.Max(20, Math.Round(Tbw / RfBandwidth / Raster)) * Raster;
        public double BandwidthEff => Tbw / Trf;
        public double Gss => GzCurrent * Scanner.EtaZ;
        public double SliceThickness => BandwidthEff / (Constants.GammaBar * Gss);
        public double SliceOffsetHz => Constants.GammaBar * Gss * SliceZ;
        public double Dwell => 1 / RxBandwidth;
        public double Gread => 1 / (Constants.GammaBar * Fov * Dwell);
        public double ReadCurrent => Gread / Scanner.EtaX;
        public double PixelSize => Fov / Matrix;

        /// <summary>Returns null when valid, otherwise the reason the combination is rejected.</summary>
        public string Validate()
        {
            if (B0 < MinB0 - 1e-9 || B0 > MaxB0 + 1e-9) return "field out of range";
            if (RfBandwidth < MinBandwidth || RfBandwidth > MaxBandwidth) return "bandwidth out of range";
            if (GzCurrent < MinGz - 1e-9 || GzCurrent > MaxGz + 1e-9) return "gradient current out of range";
            if (Math.Abs(SliceZ) > MaxSlice + 1e-9) return "slice out of range";
            if (Array.IndexOf(Matrices, Matrix) < 0) return "matrix not supported";
            double dz = SliceThickness;
            if (dz < 0.001 - 1e-12 || dz > 0.040 + 1e-12) return "slice thickness outside 1-40 mm";
            return null;
        }

        /// <summary>Slew-limited ramp time for a gradient of g T/m, rounded up to the raster.</summary>
        public static double Ramp(double g) => Math.Max(1, Math.Ceiling(Math.Abs(g) / Scanner.Slew / Raster - 1e-9)) * Raster;
    }
}
