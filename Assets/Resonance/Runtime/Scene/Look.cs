using UnityEngine;

namespace Nebulytic.Resonance
{
    /// <summary>Approved spatial palette: silver apparatus, mint B0, coral RF and matching cyan/orange/violet
    /// spatial axes and gradient coils. Phase colours are independent measured quantities.
    /// Labels use dark outlines over passthrough; scientific views have no backing panels.</summary>
    public static class Look
    {
        public static readonly Color AX = Hex(0x23D5EF), AY = Hex(0xFFA34F), AZ = Hex(0xAF81FF); // x, y, z
        public static readonly Color GX = AX, GY = AY, GZ = AZ;   // gradient coils: the colour of their axis
        public static readonly Color B0 = Hex(0x9EFFB0);          // main magnet, B0 field lines and arrows
        public static readonly Color RF = Hex(0xFF5275);          // birdcage and its currents, B1, the RF voltage
        public static readonly Color MAG = Hex(0xFFFFFF);         // net magnetization M, the image's brightest tone
        public static readonly Color SCAF = Hex(0xFFFFFF);        // frames, outlines, zoom lines, the pointer
        public static readonly Color INK = Hex(0x060A18);         // dark casing behind lines, text and icons
        public static readonly Color PANEL = new Color(0x0A / 255f, 0x10 / 255f, 0x24 / 255f, 1f); // solid dark backing
        public static readonly Color PANEL_EDGE = Hex(0x3A5BB8);  // the backing's rim
        public static readonly Color CHIP = Hex(0x1B2A57), CHIP_ON = Hex(0x3A5BB8); // strip toggles off / on
        public static readonly Color TRACK = Hex(0x223A7A);       // progress track, plot baselines and grids
        // Equipment (base, cradles, receiver blocks): a deep saturated blue, lit, so it reads as an object over the room.
        public static readonly Color STRUCT = Hex(0xBAC8D1);
        public static readonly Color TEXT = Hex(0xFFFFFF);        // primary text: subtitles, titles, values
        public static readonly Color TEXT2 = Hex(0xD2E2FF);       // secondary: names, notes (pale blue, not grey)
        public static readonly Color TEXT3 = Hex(0xA9C3FF);       // tertiary: equations, the clock
        public static readonly Color BONE = Hex(0xFFEFD6);
        public static readonly Color SKIN = new Color(0xFF / 255f, 0xB4 / 255f, 0x96 / 255f, 0.22f);
        public static readonly Color MUSCLE = Hex(0xE0584E);      // matter, translucent context in the hand

        public static readonly Color[] Entities = { AX, AY, AZ, B0, RF, MAG };

        public static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1);

        public static Color Coil(Sim.CoilId c) => c == Sim.CoilId.B0 ? B0 : c == Sim.CoilId.Rf ? RF : c == Sim.CoilId.Gx ? GX : c == Sim.CoilId.Gy ? GY : GZ;

        /// <summary>Axis colour by index (0 x, 1 y, 2 z).</summary>
        public static Color Axis(int k) => k == 0 ? AX : k == 1 ? AY : AZ;

        /// <summary>
        /// A precession phase (radians, rotating frame) as a colour at brightness v: the hue turns once per turn of phase.
        /// Offset so that the phase right after the RF pulse (M along +y') is violet, far from the room's usual colours.
        /// </summary>
        public static Color Phase(double phase, float v, float s = 0.88f)
        {
            double h = (phase + PhaseHueOffset) / (2 * System.Math.PI); h -= System.Math.Floor(h);
            return Color.HSVToRGB((float)h, s, Mathf.Clamp01(v));
        }
        public const double PhaseHueOffset = (280.0 - 90.0) * System.Math.PI / 180.0;
        /// <summary>Brightness of a proton tipped by theta (radians) from the field: 0.55 at rest (still clear on the dark walls), 1 when fully tipped.</summary>
        public static float TipBrightness(double theta) => 0.55f + 0.45f * (float)System.Math.Sin(System.Math.Min(theta, System.Math.PI / 2));

        // Layout: azimuth, elevation (degrees) and distance (m) from the head at layout time (0.8.4).
        // Top row: the scanner, the magnified proton block, the close-up of one proton. Lower row: the received signal (by
        // the receiver), the pulse sequence, the measured data and image. Bottom: the strip.
        public const float ScannerAz = -29, ScannerEl = 6.5f, ScannerDist = 1.4f, ScannerScale = 0.86f;
        public const float CubeAz = 2, CubeEl = 6.5f, CubeDist = 1.15f;
        public const float CloseAz = 31, CloseEl = 5.0f, CloseDist = 1.22f;
        public const float SignalAz = -28, SignalEl = -18, SignalDist = 1.02f;
        public const float SequenceAz = 0, SequenceEl = -17, SequenceDist = 1.0f;
        public const float DataAz = 28, DataEl = -18, DataDist = 1.02f;
        public const float StripAz = 0, StripEl = -39.5f, StripDist = 0.80f, StripScale = 1.0f;
        /// <summary>Uniform magnification of the proton block (5 x 5 x 10 mm shown as 0.25 x 0.25 x 0.5 m).</summary>
        public const float BlockMagnification = 50;
        // Field lines: brightness scale of the |B| deviation (a gradient's ramp).
        public const float LineFieldWidth = 0.3e-3f;
        // B0 (+z) points right and 40 degrees away; fingertips (-z) point left (40 degrees toward the viewer). 0.8.4: 50 instead
        // of 60 degrees, so the x axis is no longer nearly along the line of sight (its arrows and twists read at 64 % length).
        public const float PhysicsYaw = 76;

        // Text sizes (TextMeshPro world size; cap height about 0.073 x size metres). 0.8.4: about twice 0.8.3's.
        public const float TitleSize = 0.28f, LabelSize = 0.22f, ValueSize = 0.21f, NoteSize = 0.17f;
        // Line widths (m at unit scale): plot traces, axes, frames.
        public const float TraceWidth = 0.0042f, AxisWidth = 0.0026f, FrameWidth = 0.0034f;

        // Emphasis.
        public const float EmphasisTime = 0.10f, CoilFloor = 0.55f;
        // Protons that are not referenced fade by opacity (never darker than the room): opacity at e = 0.
        public const float FadedAlpha = 0.28f;
    }
}
