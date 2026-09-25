# Resonance MRI 0.8.0: resolved rebuild specification

Written 2026-09-23 after a critical review of the three unreviewed drafts in this folder (A simulation core, B scene and interaction, C explanation). This file is the single source of truth for the rebuild. Where it is silent, the draft sections may be consulted **only** if they do not contradict it. Numbers marked *(verified)* were recomputed independently during this review (Python/SciPy, `scratchpad/phys`), not copied.

Reading order: §1 verdict and decisions, §2 complaint trace, §3 physics, §4 time, §5 scene, §6 attention, §7 lesson, §8 controls, §9 budgets, §10 code, §11 validation, §12 release, §13 as built. **Where §13 records a deviation found during implementation or render review, §13 governs.**

---

## 1. Review verdict

### 1.1 What the drafts got right (kept)

- **A's physics is sound and its numbers check out.** Independently recomputed: shielded magnet b_iso 4.00229 mT/A and 2.74 ppm over a 12 cm DSV; Golay Gx 1.5415×10⁻⁴ T/m/A (4 turns, a = 155 mm); birdcage b₁(0) = 16.287 µT/A with b_S ∥ +x, b_C ∥ −y and Kirchhoff residual 10⁻¹⁶; Hamming-sinc TBW 4 slice FWHM/BW = 0.996 (10°) and 1.088 (90°), coherence 0.98 after the half-area rephaser; B₁ along +x′ tips +z toward +y′; free precession with Δf > 0 turns +x toward −y. M₀ = 3.086×10⁻³ A/m per tesla and polarization 3.29×10⁻⁶ at 1 T are correct.
- A's key simplification: every RF pulse starts with M⊥ = 0 (ideal spoiling, or full recovery), so each pulse is an exact per-isochromat **affine map** u⁺ = u_z⁻·a + b. With closed-form free precession this makes the state at any time t an O(1) function per isochromat: seekable, deterministic, no animation.
- A's numerically stable Larmor offset, reciprocity receive weight conj(b₁), noise model and MaRCoS receiver architecture.
- B's invariants: one colour per entity, no gold, all coils always drawn, fields only as volumetric shading, no line renderers, layout computed from the head pose only at start and recenter, linked orientation, dark only, the referent registry, emphasis by brightness only, one pointer ("point in the figure").
- C's lesson: ten steps, one sentence per cue, one referent set per cue, narration-slaved clock, explicit hold / span / cut / strobe, no auto-advance, per-cue physical assertions, forbidden-word lint.

### 1.2 Contradictions and errors found, and how they are resolved

| # | Problem | Resolution |
|---|---|---|
| R1 | **Field strength.** A models a 1.0 T superconducting extremity scanner. B's expected looks and C's whole lesson assume 64 mT (2.72 MHz, "150 amperes"). | **1.0 T.** The user asked "is it the actual MRI?": a 1 T superconducting extremity magnet is the clinical instrument class; 64 mT systems use permanent magnets, which have no winding current (complaint 1). The rotating-frame display is scale-invariant (turns shown during a slice pulse = 2.5·TBW regardless of B₀), so nothing visual is lost. C's numbers are re-derived for 1 T. |
| R2 | A's magnet includes an active shield pack at r ≈ 0.4 m carrying reverse current. | **Cut the shield.** A reverse-current outer coil would read as a second, contradictory magnet and enlarges the scanner to 0.83 m. Redesigned unshielded 3-pair solenoid in §3.2 (same pack cross-section and current density). Stray field is irrelevant to the lesson. |
| R3 | **Time.** C demands one dilation D = 1000 for everything. A varies the rate per step and switches lab/rotating frame automatically. At 1 T the phenomena span 23 ns (carrier) to 0.5 ms (hard pulse), 2.7 ms (slice pulse) and 100 ms (relaxation): no single D shows all of them. C's own parameters only worked at 64 mT with 16 ms pulses; at 1 T a 16 ms, 15.6 Hz hard pulse would not tip fat (145 Hz chemical shift) or spins in a 2 ppm field error (85 Hz). | **One physical clock, a few named speeds.** Physical time t is the only time. The display maps lesson time to t with hold, span, cut or strobe (C). A span runs at one of four fixed speeds: *carrier* (lab frame, one Larmor turn per 2 s), *pulse* (1 s ↔ 0.1 ms), *relax* (1 s ↔ 10 ms), *strobe* (one repetition per frame). The speed is constant within a span, shown on the strip, and every change is spoken. No automatic frame switching: the lab frame appears only in the two carrier spans. |
| R4 | **Spin grid.** B: 20³ needles over 40 mm. C: 48×48×24 = 55 296 over 100 mm, "every simulated spin drawn". A: 16³ at 1 mm plus a 4 mm whole-hand set plus a ~65 k signal set. | **Display cube 16³ at 1 mm (4096 needles).** Legibility requires the display cell to be at most half an image pixel: k_max·h ≤ 0.25 gives ≥ 4 needles per phase turn at the edge of the readout. With FOV 128 mm and N = 64 (2 mm pixels), h = 1 mm meets it exactly. C's 55 k needles cost ~1.4 M triangles (Quest budget ~0.7 M) and are not see-through; B's 2 mm cells alias at every readout. Plus a **whole-hand set** at 4 mm (~15 k) shown as the scanner-scale magnetization glow, and a **signal set** (1 mm in-plane, Δz/8 through ±1.5 Δz, ~60 k) that produces the measured data. All three are solved by the same engine; every drawn needle is one simulated isochromat. |
| R5 | Matrix: A 128, C 24. | **64 default** (32/64/128 selectable), FOV 128 mm, pixel 2 mm. 24 is too coarse to show bones; 128 breaks display legibility (R4) and costs 4× acquisition. |
| R6 | Hand orientation: A fingers toward +z, B fingers toward −z (user's left), C unspecified. | **Fingers point to the user's left** (complaint 3): fingers along −z, B₀ (+z) to the user's right and 30° away, dorsum up (+y), right thumb toward the user (−x). |
| R7 | B's atomic close-up sphere: ~140 water molecules with proton moments sampled at polarization 3×10⁻⁶ (indistinguishable from isotropic), rotating rigidly. | **Cut.** It is a still, visually isotropic snapshot whose only motion is a rigid rotation: it teaches nothing the needle definition (spoken in step 2) does not, and it adds a fourth item (complaints 5, 19). |
| R8 | B's separate hand-reference model in the top row, plus an in-scanner hand drawn only when referenced. | **One hand, in the scanner**, which sits near eye level (complaint 3 "sits higher"). The hand is shown by its own magnetization glow at all times (complaint 5 "show the spins in the matter"); the anatomical mesh (bones, translucent skin) is faint context and brightens when referenced. |
| R9 | B's signal trace: a 240-sample tube along a cable. | **Cut** (it is a plot). The signal is one arrow (C). k-space and image are shown on the scanner console (data, not plots). |
| R10 | A's full MaRCoS chain (122.88 MS/s, CIC, FIR) proposed for runtime. | **Validation model only.** Runtime fills k-space from the reciprocity fast path; a test proves the full chain gives the same samples (RX1). The narration's receiver facts are backed by that model. |
| R11 | B's three raymarched volumes for matter, slab and field, plus hand mesh; A's volume set undefined in the scene. | **Two raymarched volumes only:** field ‖B‖ (green) and B₁ (RF colour). Magnetization at scanner scale is **one** glow volume filled from the whole-hand set: faint for M_z, bright for ‖M⊥‖. The reconstructed image is a textured slab at the slice. |
| R12 | B's six axis letters, knob readouts and "+x" type labels in the scene. | **No text in the scene.** Axis triads are unlabelled arrows, named by narration while lit. Knob readouts appear on the strip only. |
| R13 | C has no pointer ("emphasis only"); B has one. The user asked to "point in the figure instead". | **Keep B's pointer.** One chevron flies to each cue's primary referent. |
| R14 | C's step 4 claims "So the arrow shrinks" follows T₂. At 1 T the whole-hand free induction decay is dominated by the magnet's ppm field error across the hand (T₂*, a few ms), while each 1 mm cube keeps its magnetization for tens of ms. | **Narrate the truth:** the signal arrow collapses first because distant spins drift out of step; the cube's needles then relax at tissue rates. This also motivates step 5 (a gradient does the same on purpose). |
| R15 | C's hard pulse (16 ms, 0.367 µT) and Gz demo (0.0587 mT/m) are 64 mT values. | Hard pulse **0.5 ms, 90°, B₁ = 11.74 µT** (f₁ = 500 Hz ≫ fat shift and field error). Gz demo uses the slice-select amplitude for 0.42 ms: two turns across the 16 mm cube. |
| R16 | C's strobe of all rows at P = 1 s, and the first-repetition bias: with TR 500 ms, the first repetition (steps 7, 8) starts from full M₀ while later rows are in steady state, which corrupts the k-space centre. | **TR = 3 s** (≥ 96 % recovery for every tissue at 1 T), so every row is in steady state within 4 % and the lesson's demonstration repetitions are genuine rows. Strobe period min(1, 22/(N−2)) s. |
| R17 | Numbers in C's text: "2 in ten million" (64 mT), "one pair per millisecond" (1 kHz receiver), "174-hertz band", "20 millimetres", "170,000 times weaker". | Recomputed at 1 T in §7 and filled from the running code (`lesson-derived.json`), never typed by hand: about 3 in a million; 32 thousand pairs a second; 1500 Hz; 5 mm; ~85 thousand times (hard pulse). |
| R18 | B's haze "expected look" assumes 64 mT and 5 mT/m. | Recomputed at 1 T: W = 1 mT fixed; slice-select Gz (7.05 mT/m) gives ±0.70 mT at z = ±0.1 m, emission ratio ≈ 16 : 1 end to end. |
| R19 | A: volume set limited to the RF-coil interior. | Whole hand in the bore, so fingertips outside the coil are visibly **not** tipped (weak B₁, off-resonant B₀): real behaviour, kept. |
| R20 | A requires a density knob-free design, B adds a density knob, C a slider set with commit. | Five knobs on the scanner plinth (B₀, Gz current, RF bandwidth, slice position, matrix) plus grabbing the region frame. No density knob (the display cube is fixed at 16³ × 1 mm, R4). |
| R21 | Noise: A physical noise on; C noise off. | Physical noise on at runtime (negligible at 1 T, dominant near 0.05 T, which makes B₀ changes meaningful); every assertion in tests runs noise-free as well. |
| R22 | B's procedural-indirect instancing for needles depends on stereo-safe indirect instance IDs that cannot be checked headless. | Use `Graphics.RenderMeshInstanced` batches (≤ 1023) with a standard instancing shader, which Unity supports in single-pass instanced stereo. Still listed as device check D1. |
| R23 | C launches paused; B lays out after tracking settles. | Both kept: lay out once from a settled head pose, show the whole dashboard, wait for A or ▶. |
| R24 | C's multi-slice step at ±40 mm with 20 mm slabs; A limits slice centres to \|z\| ≤ 40 mm (gradient nonlinearity). | Nine 5 mm slices at 10 mm spacing, z = −40…+40 mm. |

### 1.3 Cut entirely (over-complicated or contradicting the user)

Close-up sphere; hand-reference model; signal trace tube; density knob; axis letters; knob readout text in the scene; shield coil; timeline or sequence diagram; any plot, legend, callout or leader line; light theme; automatic lab/rotating switching; 12.5 MB of bore/room field tables (only the tables in §3.3 are baked); B's sabotage "proof harness" (replaced by a smaller set of negative tests in §11); word-level highlighting.

---

## 2. Complaint trace

Every row is verified twice: by an automated check (§11) and by the render review of every step from the default head pose. Checks were never enough before; the render review decides.

| # | User complaint (0.7.0 review) | Fix in 0.8.0 | Check |
|---|---|---|---|
| 1 | No real magnet coil; B₀ from real windings with current, field shown in green; all fields from real coils by Biot–Savart; simulation, not animation. | Six NbTi winding packs (3 pairs) carrying a persistent current (132.7 A for 1 T, §13), drawn as wound packs with current-direction flow; ‖B‖ in green computed only from those conductors (elliptic-integral loops, exact straight segments). Every moving quantity is a function of physical t. | FLD1–FLD4; S-COIL render; no scripted motion in `Scene/` (grep gate). |
| 2 | All coils always translucently visible; active one emphasised; nothing pops in; RF from the RF coil, Gz from the Z coils. | All five systems drawn every frame (floor emission 0.15); brightness rises with the coil's own current and with emphasis; birdcage rung brightness follows its computed rung currents; Gz loops show opposite circulation. | F4 per cue; no renderer ever disabled. |
| 3 | Hand faces the user's left and sits higher; the text panel faced away; clean dashboard, no constant re-adjusting; user can rearrange. | Fingers along −z = user's left; scanner (with hand) at eye level; strip billboarded to the eye; layout computed from the settled head pose at start and on recenter only; every item grabbable. | G0 (real boot path at an offset pose), G4 facing, G6 stillness. |
| 4 | "Is it the actual MRI or concept based?" Make it realistic. | 1 T extremity scanner, real protocol (2-D spoiled gradient echo), literature tissue values, reciprocity signal, MaRCoS receiver architecture, physical noise. | SIG, RX, TIS tests; numbers in narration come from the code. |
| 5 | Show the spins in the matter directly; hand need not always be in the scanner; one thing at a time. | Hand in the scanner is its own magnetization glow; anatomy mesh faint unless referenced; a magnified 16 mm cube of real tissue shows every isochromat; one referent set per sentence. | C1–C3 per cue. |
| 6 | Volumetric grid of simulated spins (10³ to dense-but-see-through), every spin shown; band spins tip, spring back, give signal; not 9 representative spins. | 16³ = 4096 needles, each one isochromat simulated with its tissue's PD, T₁, T₂, shift; only the resonant band tips; relaxation and signal follow. | S1 needle count = N³; REF1 band test; render review. |
| 7 | Changing the hand/tissue changes the voxels and the simulation. | Moving the region or the hand resamples every isochromat's tissue and re-runs the acquisition. | S2, TIS3. |
| 8 | Explanation from physical reality: Gz gives each position its own Larmor frequency; RF rotates at one frequency and fills the volume; only matching spins accumulate. | Lesson steps 3, 5, 6 say exactly this, with the rotating B₁ shown filling the coil and only the band tipping. | Cue assertions 3.x, 6.x. |
| 9 | No plots away from the space; field strength shown in the setup; gradients as shading, not arrows; "if there is a gradient, show me". | ‖B‖ haze fills the bore; a gradient is a brightness ramp along its axis (≥ 3 : 1 end-to-end for every imaging gradient). No plots. | F1–F3 luminance ratios; T2 no-line gate. |
| 10 | Slab and cube had the same colour; no gold; never one colour for two entities. | Closed palette (§5.2), ΔE2000 ≥ 20 between entity colours, no gold hue anywhere including additive overlaps. | P1–P4. |
| 11 | Spin orientation in the cube matches the scanner; linked views rotate together (don't over-invest). | Cube and scanner share one orientation; yawing either yaws both. | G8. |
| 12 | Slide 6 showed nothing: narration while nothing pointed and nothing happened. | Every cue has a referent, the pointer, and a physical change or an explicit hold on a visible state. | C1–C3, per-cue Δ assertions. |
| 13 | Remove the legend; point in the figure; one +x arrow, no ± text; no text in the visualization. | No legend; pointer; unlabelled triads; text only on the strip. | T1 text whitelist. |
| 14 | The field appeared in one plane as three lines; it should fill the volume. | Volumetric haze through the whole bore. | F1–F3; no line renderers. |
| 15 | "Phase across slab thickness" plot is confusing. | Removed. The through-slab dephasing and rephasing is visible in the 3D needles (cues 6.9, 6.10). | No plot objects exist. |
| 16 | Jagged lines, flat lines, flat planes and connected lines in the cube are garbage; every moment is 3D. | Needles are lit 3D meshes; no lines, ribbons or sheets anywhere. | T2 gate (0 LineRenderer/TrailRenderer in scene and source). |
| 17 | Every sentence highlights its referent in-scene as it happens. | Emphasis switches at each cue start, slaved to the audio playhead; pointer arrives within 0.45 s. | C2 at @s and @e of every cue. |
| 18 | Controls: A next, B previous, stick left/right, pause; dark UI only. | Exactly that (§8); X pauses; dark palette only, no theme code. | K1–K6, P4. |
| 19 | Simplify; do not complicate. | Three items (scanner, spin cube, strip), ten steps, one referent set per sentence, cuts listed in §1.3. | Render review. |

---

## 3. Physics (resolved Section A)

### 3.1 Scanner class, frame, constants

- 1.0 T superconducting extremity scanner; envelope follows the ONI OrthOne class (28 cm patient bore, 15 mT/m, 160 mm T/R birdcage). Winding layouts are this app's own designs; say so in Docs and credits, never "OrthOne windings".
- Scanner frame P: origin at the isocentre, **+z = bore axis = B₀**, +y up, +x = y × z (right-handed). SI units, double precision for t, carrier phase and display isochromats.
- γ̄′ = 42.57638543 MHz/T (shielded proton in water, CODATA 2022) for all tissue water; fat at −3.4 ppm. μ₀ = 1.25663706127×10⁻⁶. T_body = 310.15 K.
- M₀,water(B₀) = 3.086×10⁻³ A/m per tesla (verified). State variable u = M/M₀,water, in the frame rotating at f_ref = γ̄′·B_ref.
- An arrow is **the net magnetization of the protons in its cell (an isochromat)**, never a single nucleus.

### 3.2 Windings (conductor geometry drawn = geometry computed)

**B₀ magnet (unshielded, R2).** Three mirrored pack pairs, r = 220–250 mm, 1.44 × 1.44 mm NbTi conductor (engineering current density 120 A/mm² at 250 A, 14.47 turns per mm of pack length), all in series, +φ (counter-clockwise seen from +z, giving B₀ ∥ +z). Pack z-centres and lengths are optimised for the 12 cm DSV; the final table and its measured homogeneity are written by the bake to `Docs/AI/scanner-windings.json` and asserted by FLD2. Field model: each pack is 4 radial × 8 axial filament loops carrying NI/32 (the discretisation is part of the model). I_B0 is set so that ‖B(0)‖ = B₀.

**Gradient coils** (copper, turns lumped on one path, ≤ 100 A, slew ≤ 60 T/m/s, 10 µs raster):

| Coil | Design | Radius | Turns | η (T/m/A) |
|---|---|---|---|---|
| Gx | Golay double saddle, 120° arcs at \|z\| = 0.389a and 2.2a | 155 mm | 4 | 1.5415×10⁻⁴ (verified) |
| Gy | Gx rotated +90° about z | 160 mm | 4 | ≈1.4467×10⁻⁴ (computed at bake) |
| Gz | Maxwell pair at z = ±(√3/2)a | 165 mm | 5 | 1.4800×10⁻⁴ |

Positive current gives a positive gradient. Concomitant fields come for free from the full vector field.

**RF:** quadrature low-pass birdcage, 12 rungs at r = 80 mm (rung 0 on +x), z ∈ [−100, +100] mm, end rings of 6 segments per arc, Kirchhoff-consistent ring currents. b₁(0) = 16.287 µT/A, real (verified). Co-rotating component b₁ = ½[(b_Sx − b_Cy) + i(b_Sy + b_Cx)]. Drive: rung n carries \|A\|·sin(φₙ + θ), θ = 2π f_ref t − arg A; in the rotating frame the pattern is static. Transmit and receive with the same coil (T/R switch).

### 3.3 Fields

- Primitives: circular filament by AGM elliptic integrals (with the small-m limit); straight segment by the exact finite formula. Superposition B = I_B0·b₀ + Σ I_c g_c; RF handled in the rotating frame. Quasi-static (hand ≪ 0.8 m tissue wavelength).
- **Stable Larmor offset (mandatory):** Δ = I_B0·δb₀ + Σ I_c g_c with δb₀ = b₀ − b_iso ẑ, B_ref = I_B0·b_iso; Δf = γ̄′(2 B_ref Δ_z + ‖Δ‖²)/(√((B_ref + Δ_z)² + Δ_x² + Δ_y²) + B_ref) + shift_ppm·10⁻⁶·f_ref.
- **Baked tables** (`Assets/Resources/Resonance/FieldTables.bytes`, produced by the same C# code run under dotnet on the server; header carries a hash of the winding definition and the runtime refuses a mismatch):
  - axisymmetric B₀ and Gz: (B_ρ, B_z) per ampere on ρ 0–150 mm × z ±300 mm at 1 mm (B₀ deviation in double);
  - 3-D Gx, Gy, b_S, b_C per ampere (float3) over x, y ±84 mm, z ±168 mm at 4 mm;
  - display grid for the haze: per-voxel first-order ‖B‖ deviation per unit current for each coil over the bore box (32 × 32 × 64), plus ‖b₁‖ over the coil interior (24³).
  - Isochromat field values are interpolated once per set rebuild, never per frame.

### 3.4 Tissue

Classes and values exactly as draft A.5.1 (log–log interpolation between literature anchors, B₀ clamped to 0.05–1.5 T). At 1.0 T: skin 902/10 ms, fat 262/153.5 ms (−3.4 ppm), muscle 902/35.7 ms, tendon 600/1.49 ms, marrow 262/153.5 ms (−3.4 ppm), cortical bone 140/0.4 ms (PD 0.20), unsegmented soft tissue as muscle, phantom 300/250 ms. Segmentation per A.5.2 (cortex, marrow, tendon distal to the wrist, muscle, skin, fat, soft tissue) written by `Tools/build_anatomy.py` to `Assets/Resources/Anatomy/HandLabels.bytes` (`RNML`, 144×80×336 at 1.25 mm). `HandTissue.bytes` is deleted. Dorsal check: centroid_y(extensor) > centroid_y(flexor).

Default pose: mid-metacarpals (z_obj = +70 mm) at the isocentre, fingers toward −z, in-plane centred on the tissue centroid of that slab.

### 3.5 Isochromat sets (one engine)

| Set | Lattice | Extent | Count (default) | Shown as |
|---|---|---|---|---|
| Cube | 16³ at 1 mm | 16 mm cube centred on the slice, in-plane position chosen on a 4 mm grid to maximise distinct tissue classes; user-movable | 4096 (air kept, PD 0) | 4096 needles in the spin cube |
| Hand | 4 mm cubic, z planes aligned to the slice centre | every tissue voxel of the hand in the bore | ≈ 15 k | scanner magnetization glow; whole-hand signal for non-selective steps |
| Signal | 1 mm in-plane (FOV/128), z step Δz/8 over ±1.5 Δz | tissue with ‖b₁‖ ≥ 0.1 b₁(0) and \|Δf_ss − Δf_slice\| ≤ 1.5 BW + \|shift\| | ≈ 60 k | not drawn; produces k-space |

Per isochromat: position, class, ΔV, δb₀, g_x, g_y, g_z (float3, per ampere), b₁ (complex), Δf_ss, affine RF map (a, b), state.

Display cell average (anti-aliasing and the true net moment of a uniform cell): shown u⊥ = u⊥(centre)·Π sinc(k_a h_a), k measured from the RF centre (1 before it). Scanner glow uses the unaveraged ‖u⊥‖ (local tipped magnetization).

### 3.6 Bloch engine

Rotating frame at f_ref; dM/dt = γM × B_eff − R₂M⊥ − R₁(M_z − M₀)ẑ; each 10 µs RF step is a right-handed rotation by −2π‖w‖Δt about ŵ, w = (γ̄′Re(b₁A), γ̄′Im(b₁A), Δf), followed by exact relaxation. Free precession: u⊥ ← u⊥e^(−iφ)E₂, u_z ← PD + (u_z − PD)E₁, φ = 2π∫Δf dt (Simpson on ramps, exact on flat tops). RF pulses are affine maps (every pulse starts with u⊥ = 0). Inside an RF segment the display state is stepped from the segment start with partial steps, so any t is exact. Spoiling: the crusher is simulated, then u⊥ := 0 at the repetition end.

### 3.7 Protocol and the lesson program

Adjustable (range, default): B₀ [0.05, 1.00] T, 1.00; RF bandwidth [500, 5000] Hz, 1500; Gz slice current [6.8, 100] A, 47.6 A (7.05 mT/m); slice centre [−40, +40] mm, 0; matrix {32, 64, 128}, 64. Derived: T_rf = round(4/BW, 10 µs), Δz = BW_eff/(γ̄′ G_ss) (5.0 mm default), f_ref, I_B0, slice RF offset γ̄′G_ss z_s, TE, scan time. Rejected if Δz ∉ [1, 40] mm.

Fixed: FOV 128 mm, readout x, phase y; BW_rx 32 kHz (dwell 31.25 µs), G_x = 1/(γ̄′·FOV·dwell) = 5.87 mT/m for every N; **TR 3 s** (R16); 90° Hamming-sinc TBW 4; sequential lobes (one gradient at a time); centric row order; Gz crusher of area 4/(γ̄′Δz); ideal spoiling. Hard pulse (steps 3–5): 0.5 ms rectangle, 90°, B₁ 11.74 µT.

**One lesson program = one physical timeline** (t increases monotonically across all ten steps; cuts only skip forward over recovery):

| Block | Content |
|---|---|
| rest | t < 1 ms, B₀ only |
| hard₁ | 0.5 ms hard pulse, then free induction decay for 100 ms (steps 3–4) |
| pause | 5 s recovery (cut) |
| hard₂ + gz demo | hard pulse, Gz +7.05 mT/m for 0.42 ms, 0.1 ms off, Gz −7.05 mT/m for 0.42 ms (echo) (step 5) |
| pause | 5 s (cut) |
| TR 1 | slice pulse, rephaser, (row 0: no Gy), Gx prephaser, readout with ADC, crusher (steps 6–7) |
| TR 2 | row +16: Gy lobe, prephaser, readout (step 8) |
| TR 3 … N | remaining rows in centric order (step 9, strobed at each echo) |
| slices | eight more slices at z = −40…+40 mm, each a full acquisition (step 10, strobed) |

Every lesson point is addressed by an event (e.g., `ss1.start`, `ro2.echo`) plus a fraction, so it survives parameter changes.

### 3.8 Signal, receiver, image

- EMF by reciprocity: s(t) = iω_ref·M₀·Σ ΔV·conj(b₁)·u⊥·H (H: receiver passband ±BW_rx/2). The net-signal arrow shows s(t)/s_ref in the rotating frame; s_ref is the sum over the excited set at full tip (automatic receiver gain, as at prescan).
- Noise: complex Gaussian, E\|n\|² = 4k_BT·R·F·BW_rx with R(f) = 0.15 Ω√(f/42.576 MHz) + 0.35 Ω(f/42.576 MHz)², F = 0.5 dB; seed = hash(revision, repetition).
- MaRCoS receiver model (validation): 16-bit ADC at 122.88 MS/s (±1 V), NCO at f_ref, 6-stage CIC decimating 640× to 192 kS/s, 193-tap Hamming FIR decimating 6× to 32 kS/s, droop and delay correction, quadrature combination s = (y_S − i y_C)/2.
- k-space: K[p, j] complex, ky = (p − N/2)/FOV, kx(t_j) = (j − N/2)/FOV exactly; sample revealed when t ≥ t_j; a row is complete at readout end.
- Image: inverse 2-D DFT with centring phase; zero-filled partial reconstruction after every row; magnitude displayed at z = z_s.

### 3.9 Parameter propagation

Any change (parameter, region, hand pose) increments `Revision`, rebuilds the cube and hand sets first (≤ 0.4 s target on Quest), then the signal set and the acquisition on worker threads. The lesson stays on the current cue and restarts it; physical t is recomputed from the cue's event, not kept as a number.

### 3.10 Not modelled (stated in Docs with sizes)

As draft A.16, minus the shield: eddy currents and amplifier dynamics, no shims (field error as designed), RF shield/coupling/SAR/dielectric effects, susceptibility (T₂′), diffusion, flow, magnetisation transfer, multi-peak fat, mono-exponential relaxation, instantaneous B₀ changes, ideal spoiling, signal-set cut-offs, flat receiver passband in the fast path, unpublished MaRCoS word widths.

---

## 4. Time

- **Physical time t** (double seconds) is the only time. Every consumer (spins, currents, fields, RF, EMF, ADC, k-space, image) reads the same t.
- **Lesson clock τ** (display seconds) runs while playing; while a cue's audio plays, τ is slaved to the audio playhead (≤ 30 ms correction), so words and visuals cannot drift. Headless tests use a fixed 1/72 s step.
- A cue maps τ to t by exactly one of: **hold** (t fixed), **span** (t = t_from + (τ − τ₀)/D, clamped at t_to, at one of the four speeds), **cut** (0.15 s dim, jump, 0.25 s restore; no blending), **strobe** (t = echo time of repetition ⌊(τ − τ₀)/P⌋).
- Cue length = lead-in + max(spoken duration, span length on screen) + 0.2 s.
- Speeds: *carrier* D = 2/f_ref s per s... i.e. one Larmor turn per 2 s, **lab frame**; *pulse* 1 s ↔ 100 µs; *relax* 1 s ↔ 10 ms; *strobe* one repetition per P. Rotating frame for all but *carrier*. Frame changes happen only at carrier boundaries, where both pictures coincide.
- The strip shows t and the speed (for example `t 1.842 ms · 1 s = 0.1 ms`). No other text about time.
- Stall: while k-space or the image is visible, t never passes the completion time of the first row not yet computed.

---

## 5. Scene (resolved Section B)

### 5.1 Items and layout

Three items plus the pointer. Positions are functions of the head pose at layout time (E, forward f_h flattened, yaw only): `Slot(α, ε, d)`, α azimuth (+ right), ε elevation.

| Item | Anchor | α | ε | d | Orientation | Notes |
|---|---|---|---|---|---|---|
| Scanner (with hand, console, knobs) | isocentre | −17° | −4° | 1.15 m | R_phys | displayed at scale 0.6 |
| Spin cube | cube centre | +21° | −4° | 0.95 m | R_phys | 0.30 m cube, 18.75× magnification |
| Strip | panel centre | 0° | −25° | 0.85 m | billboard (yaw + pitch) | 0.56 × 0.10 m |

R_phys = Q_D·yaw(ψ), ψ = 60° by default: B₀ (+z) points right and 30° away, +x points away and 30° left, fingers (−z) point left and 30° toward the user. Values are the starting point; the render review may tune α/ε/d (never the orientation or the finger direction). Layout runs once when the head pose is tracked and settled (≤ 4 s wait, desktop immediately) and on recenter; never on focus changes, mode toggles, steps or parameter changes. Unity is left-handed: P→Unity is `(x, y, z) → (−x, y, z)`, applied only in `Frames.cs`; no Unity cross products on physical vectors.

### 5.2 Palette (closed; every runtime colour comes from `Look.cs`)

| Token | sRGB | Entity |
|---|---|---|
| B0 | #3FD46B | B₀ windings and current, ‖B‖ haze, B₀ knob ring |
| RF | #FF4FB6 | birdcage and currents, B₁ haze, B₁ vector, bandwidth and slice knob rings |
| GX | #A64DFF | Gx windings and current |
| GY | #407FFF | Gy windings and current |
| GZ | #22E0E0 | Gz windings and current, Gz knob ring |
| MAG | #F2F5F7 | magnetization (needles, hand glow), signal arrow, k-space, image, matrix knob ring |
| SCAF | #8F959B | UI text and icons, pointer, axis triads, region frames, controller ray |
| PANEL #0E1216 (α 0.92) · TRACK #1F262E · STRUCT #1A2027 | | strip, progress track, plinth/console/receiver blocks/knob bodies |
| BONE #C8C2B8 · SKIN #A0877A (α ≤ 0.25) | | anatomy context |

Rules: entity colours pairwise ΔE2000 ≥ 20; matter colours ≥ 12 from every entity colour; no colour (or additive mix of tokens) with hue 20–70°, S > 0.30, V > 0.35 (no gold, amber, orange, yellow); brightness within one hue only ever means amount or emphasis.

### 5.3 What each item shows

**Scanner** (P frame, scale 0.6):
- B₀ packs as wound annular shells (turn stripes) in B0 colour, current flow shown by dashes moving in the conventional-current direction, speed ∝ √(I/I_max), always on (persistent current).
- Gradient windings and the birdcage as tubes along their conductor paths in their colours; brightness = 0.15 floor + emphasis + ‖I‖/I_max; dashes show direction for gradients; birdcage rungs and ring segments are lit by their own computed currents (rotating pattern in the lab frame, static in the rotating frame).
- ‖B‖ haze: raymarched bore volume, v = ½ + ½ tanh((‖B‖ − ‖B_iso‖)/1 mT), emission ∝ v², density scaled by B₀/1 T. Uniform mid-level in the imaging region with gradients off; a ramp along the gradient axis when one is on; dark beyond the magnet ends.
- B₁ haze: raymarched coil interior, emission ∝ ‖b₁(r)‖·\|A(t)\| (fills the coil at once, zero when RF is off).
- Hand magnetization glow (hand set): 0.06·u_z + 0.9·‖u⊥‖ in MAG.
- Anatomy mesh: bones and translucent skin, faint (×0.3) unless referenced.
- Region frame (SCAF bars) marking the cube's 16 mm region; grab to move it (x, y).
- Reconstructed image slab(s) at z_s ± Δz/2, point-sampled MAG, after the first row.
- Plinth (STRUCT) with five knobs (coloured rings), the receiver chain (three blocks: ADC package, mixer can, six-plate filter stack) joined to the coil by a cable, the net-signal arrow over a flat ring on the console, and the k-space and image squares (tilted 25°). No labels.
- Unlabelled axis triad at a plinth corner.

**Spin cube** (P frame, 0.30 m, same orientation):
- 4096 needles (lit meshes, pivot at the centre, length 0.8·s·min(‖m‖, 1.25), s = 18.75 mm), MAG, brightness 0.30 + 0.70·e; cells with ‖m‖ < 0.03 are small dots, air cells are not drawn.
- One B₁ vector (RF colour) through the centre while \|A\| > 0.
- Cube edges as SCAF bars (the same region frame as in the scanner), unlabelled triad on one corner.
- Clipped bone surfaces inside the cube as faint BONE context (optional; cut if it clutters in review).

**Strip** (dark panel): counter "3 / 10", title, the spoken sentence (captions on by default), progress bar with cue ticks, ◀ ❚❚/▶ ▶, voice (M/G), MR/VR, ⓘ credits; the physical clock line; knob readout while a knob is turned.

### 5.4 Text whitelist

Strip content; controller help labels (opt-in, Y); credits card while open. Nothing else. Font: the existing DejaVu-derived AtlasSans SDF asset; every glyph used must be in the static atlas.

---

## 6. Attention

- Cue refs (≤ 2, first is primary) are validated at load; unknown refs fail the load.
- Registry: `scanner`, `cube`, `coil.b0|gx|gy|gz|rf`, `field.b`, `field.b1`, `spins.all|band|off|col|kind:<class>`, `hand.glow`, `hand`, `region`, `axis.x|y|z`, `rx.adc|mix|filter`, `signal`, `kspace`, `kspace.row`, `image`, `knobs`. Spin sets are predicates on physics (band = \|Δf_ss − Δf_slice − shift\| ≤ BW/2 etc.), evaluated per isochromat.
- Emphasis e ∈ [0, 1] per element, smoothed with τ = 0.1 s in unscaled time. Referents e* = 1, others 0 (coils never below the floor). Brightness only; geometry never depends on e. Before the first cue and between steps everything is at e = 1 (overview).
- Pointer: one SCAF chevron; flies (0.45 s) to a point 5 cm in front of the primary referent's anchor on the eye side, then tracks it; rests beside the strip when no cue is active.

---

## 7. Lesson (resolved Section C)

Ten steps, one sentence per cue (≤ 16 words), no colour words, no directional words (left/right/above/below), no "plot/graph/legend/imagine/concept". `{…}` values come from `lesson-derived.json` written by the running code; cues that depend on adjustable parameters have a generic variant used when parameters are not default. Speeds: C = carrier (lab frame), P = pulse, R = relax, S = strobe.

**1 Magnet** (hold, equilibrium)
1.1 Every field, spin and signal here is calculated from coil currents and tissue. — `scanner`
1.2 These windings are the main magnet, carrying a steady {I_B0} amperes. (g) — `coil.b0`
1.3 Their field fills the bore: one tesla along it, uniform where the hand lies. (g) — `field.b`
1.4 This arrow points along the bore: that direction is z. — `axis.z`
1.5 This one points up, y; this one across the bore, x. — `axis.y`, `axis.x`
1.6 The gradient coils and the RF coil wait inside, carrying no current. — `coil.gz`, `coil.rf`

**2 Protons** (hold)
2.1 The hand lies in the bore, along the field. — `hand`
2.2 This small block of it is enlarged here, turned the same way as the scanner. — `region`, `cube`
2.3 What responds are its hydrogen nuclei: the protons in water and fat. — `spins.all`
2.4 Each arrow is the net magnetization of the protons in one cubic millimetre. — `spins.all`
2.5 About {pol} in a million more point with the field than against it. (g) — `spins.all`
2.6 Arrow length follows how many mobile protons the tissue holds. — `spins.all`
2.7 Cortical bone holds almost none. — `spins.kind:cortex`

**3 Resonance**
3.1 In this field, protons precess at {f0} megahertz: the Larmor frequency. (g) — `field.b` — hold
3.2 This is the RF coil. — `coil.rf` — hold
3.3 Its current makes a weak field that turns at that same frequency. — `field.b1` — span C, first 3 carrier turns of hard₁
3.4 Now the view turns with that field, and time runs ten thousand times slower. — `field.b1` — hold (frame change at a carrier boundary)
3.5 Every spin shares its frequency, so the field pushes each the same way. — `spins.all` — span P, hard₁ 0 → ½
3.6 The tipping adds up, though this field is {ratio} times weaker than the magnet's. (g) — `field.b1` — span P, hard₁ ½ → 1
3.7 The spins now lie across the field, a quarter turn from where they began. — `spins.all` — hold

**4 Signal**
4.1 In the scanner, this tipped magnetization sweeps round {f0} million times a second. (g) — `spins.all` — span C, 3 turns (lab frame)
4.2 Its turning field induces a voltage in the RF coil. — `coil.rf` — span C, 2 turns
4.3 The receiver samples that voltage directly, 122.88 million times a second. — `rx.adc` — hold (rotating frame)
4.4 It multiplies each sample by a cosine and a sine at the RF frequency. — `rx.mix` — hold
4.5 Filters keep the slow part: {rx_rate} thousand number pairs a second. — `rx.filter` — hold
4.6 Each pair is this arrow: the tipped magnetization as the coil sees it. — `signal` — hold
4.7 Now time runs a hundred times faster. — `signal` — span R starts
4.8 The arrow shrinks first: across the hand the field differs by millionths, so spins drift apart. — `signal`, `hand.glow` — span R
4.9 Each arrow's tipped part fades, and its part along the field regrows. — `spins.all` — span R
4.10 Muscle's tipped part fades within tens of milliseconds. — `spins.kind:muscle` — span R
4.11 Fat's fades more slowly, and fat's protons turn slightly slower. — `spins.kind:fat` — span R

**5 Gradient**
5.1 After a pause, the spins are back along the field and tipped again. — `spins.all` — cut to hard₂ end
5.2 These are the z gradient coils. — `coil.gz` — hold
5.3 A current in them makes the field grow along the bore: brighter is stronger. — `field.b` — span P, Gz ramp and ⅓ flat
5.4 Each plane now precesses at its own frequency, so the spins twist along z. — `spins.all` — span P, rest of Gz⁺
5.5 When the current stops, every plane shares one frequency again, and the twist stays. — `spins.all` — span P, gap
5.6 Fanned out, the spins add up to almost nothing: the voltage vanishes. — `signal` — hold
5.7 Reversing the current unwinds the twist. — `coil.gz` — span P, Gz⁻
5.8 The spins line up again, and the signal returns: an echo. — `signal` — hold at the echo

**6 Slice**
6.1 After another pause, the z coils carry current again. — `coil.gz` — cut to ss₁ ramp end
6.2 Each plane's frequency now rises steadily along the bore. — `field.b` — hold
6.3 The RF coil sends a pulse at one frequency: that of the middle plane. (g) — `coil.rf` — span P, ss₁ 0 → 0.25
6.4 Its field reaches the whole volume at once. — `field.b1` — span P, 0.25 → 0.4
6.5 Only spins in a {BW}-hertz band stay in step, so only their tipping adds up. (g) — `spins.band` — span P, 0.4 → 0.62
6.6 Elsewhere the field keeps slipping past the spins, and its pushes cancel. — `spins.off` — span P, 0.62 → 1
6.7 The tipped spins form a slab {dz} millimetres thick. (g) — `spins.band`, `hand.glow` — hold
6.8 Its thickness is the pulse's bandwidth divided by how fast frequency changes along z. — `spins.band` — hold
6.9 Through the slab's thickness, the spins have drifted apart. — `spins.band` — hold
6.10 A reversed z pulse, half as long, brings them back in line. — `coil.gz` — span P, rephaser

**7 Readout** (row 0)
7.1 These are the x gradient coils. — `coil.gx` — hold
7.2 A short reversed pulse first winds the slab's spins along x. — `spins.band` — span P, prephaser
7.3 Then the current flips, and the receiver starts sampling. — `coil.gx` — span P, readout 0 → 1/12
7.4 The field now rises along x. — `field.b` — span P
7.5 So spins at each x precess at their own rate, unwinding the twist. — `spins.band` — span P
7.6 Halfway, the twist is undone: the spins line up, and the signal peaks. — `signal` — span P to the echo
7.7 Every sample is the sum over the whole slab at that instant. — `spins.band`, `signal` — span P
7.8 Spins sharing an x but not a y turn alike, so this readout cannot separate them. — `spins.col` — span P to readout end
7.9 This readout fills one row of the measurement table: k-space. — `kspace.row` — hold

**8 Phase** (row +16)
8.1 After a pause, the same slab is excited again. — `spins.band` — cut to rephaser₂ end
8.2 This time the y gradient coils carry a pulse first. — `coil.gy` — span P, Gy ramp
8.3 The field now rises along y. — `field.b` — span P
8.4 Spins higher up precess faster, and the slab twists along y. — `spins.band` — span P, Gy flat
8.5 When the current stops, the twist stays. — `spins.band` — hold
8.6 Its number of turns is set by the pulse's strength times its duration. — `coil.gy` — hold
8.7 The readout then runs as before. — `coil.gx` — span P, prephaser and readout start
8.8 Each sample now adds up the slab's spins with the y twist included. — `spins.band`, `signal` — span P
8.9 These samples fill a different row. — `kspace.row` — span P to readout end

**9 Image** (strobe of the remaining rows, centric order)
9.1 The scanner repeats this, each time with a different y pulse. — `spins.band` — strobe
9.2 Here each repetition is shown only at its echo. — `spins.band`
9.3 More turns fill rows farther from the middle of k-space. — `kspace`
9.4 The image forms where the slab is, sharpening as rows arrive. — `image`
9.5 For each point, the samples are added again with that point's twists undone. — `image`
9.6 Only that point's own signal adds up; the rest cancels. — `image`
9.7 Rows with few turns carry the most signal; outer rows add the fine detail. — `kspace`

**10 Volume** (strobe of eight more slices)
10.1 Shifting the RF frequency moves the matching plane along the bore. — `spins.band`, `hand.glow`
10.2 Each slab is excited and measured the same way. — `image`
10.3 Stacked, the slabs form a three-dimensional image of the hand. — `image`
10.4 Its brightness comes from each tissue's protons and how fast they relax. — `image`
10.5 Cortical bone and tendon, with almost no mobile signal, stay dark. — `image`
10.6 Change the field, bandwidth, gradient, matrix or slice, and all of this is recalculated. — `knobs`

Every non-hold cue carries a physical assertion (C.6 style) in the walkthrough test: e.g. 3.5/3.6 tip(mobile) ≥ 0.75 at the end; 5.4 turns along z = 2.0 ± 0.15; 5.6 signal ≤ 0.15 of 5.1; 6.5/6.6 core tip ≥ 0.7 and far tip ≤ 0.02 at the pulse end; 6.7 FWHM = Δz ± 15 %; 6.10 slab coherence ≥ 0.95; 7.2 turns along x = −(N/2 + ½)·16 mm/FOV ± 5 %; 7.6 argmax\|k\| at j = N/2 ± 1; 8.4 turns along y = 8·16/128 = 1.0 ± 5 %; 9.x image equals the inverse DFT of k-space; 10.1 band centroid = z_s ± 1 mm.

Narration pipeline: `Docs/AI/narration-cues-source.json` (schema v8), per-cue WAVs per voice (am_michael, bm_george) from the existing local Kokoro service, cached by sha256(voice + text + speed). Old per-step WAVs and `narration.json` are deleted.

---

## 8. Controls and Quest policies

| Input | Action |
|---|---|
| Right A | next step (from its start), no pointing |
| Right B | previous step |
| Either stick flick left/right (not holding) | previous / next |
| Left X, strip ❚❚ | pause / resume (audio, τ and t freeze) |
| Left stick click | recenter layout |
| Left Y | controller help on/off (off by default, labels beside the physical controls) |
| Grip on an item | move; stick-x yaws the shared frame, stick-y scales that item |
| Point at a knob + stick-y, or trigger-drag | one detent per 0.25 s; readout on the strip |
| Grip on the region frame | move the region in x, y |
| Hands | pinch = trigger, pinch-hold = grip; strip icons replace A/B/X |
| Desktop | → next, ← previous, Space pause, R recenter, V MR/VR, M voice, drag items, wheel on knobs |

Preserved infrastructure and policies: official Touch Plus controller models with animated physical controls; `ReleaseGate` (input only after a neutral release; disarm on focus/tracking loss); hide controllers, hands, rays and help and pause on focus loss, stay paused on return; haptics on step, grab and detent; MR default with contextual boundary suppression tied to actual passthrough, restore boundary before VR and stay in MR on failure; FloorLevel tracking; no whole-app BOUNDARYLESS_APP; Store entitlement gate in Store builds only.

---

## 9. Budgets (Quest 3 at 72 Hz; device-unverified)

Triangles ≤ 700 k (needles ~110 k, anatomy ≤ 250 k, coils ≤ 60 k, rest small); draw calls ≤ 90; raymarch steps: ‖B‖ 24, B₁ 16, hand glow 16. Main thread: display-set state ≤ 1.5 ms (≈ 20 k isochromats, one sincos each; partial RF step while RF is on), instance packing ≤ 0.6 ms, glow texture ≤ 0.3 ms at ≤ 30 Hz. Workers: cube/hand rebuild ≤ 0.4 s, acquisition of one slice ≤ 3 s, nine slices ≤ 20 s (below-normal priority threads, no Unity API).

---

## 10. Code

- Branch `rebuild/0.8.0` from `0c2537f` (history preserved).
- **Delete:** all of `Assets/Resonance/Runtime/*`, all of `Assets/Resonance/Shaders/*`, `Assets/Resonance/Editor/ResonanceValidation.cs`, `Assets/Resources/Audio/**`, `Assets/Resources/GuidedCues*.json`, `Assets/Resources/Anatomy/HandTissue.bytes`, `Docs/AI/narration.json`, `Docs/AI/guided-narration*.json`, Slice-Lab and Slice-Lab notices (the lab is gone).
- **Keep:** `Tools/` pipeline (updated for 0.8.0 / code 9), signing continuity (`sign-release.py`, private keystore outside the repo), `ResonanceBuild.cs` configuration and scene generation (rewired), `ResonanceManifest.cs`, `ResonanceAnatomyImport.cs`, BodyParts3D meshes and notices, AtlasSans font and SDF asset, Kokoro narration pipeline, `StreamingAssets/Notices`, the Store gate, the environment/boundary and controller policies (re-implemented in the new classes).
- **New runtime** (`Assets/Resonance/Runtime/`): `Sim/` (plain C#, no UnityEngine except one loader: `Physics`, `Coils`, `Fields`, `Tissue`, `Protocol`, `Program`, `Spins`, `Acquisition`, `Receiver`, `Simulation`), `Scene/` (`Look`, `Frames`, `App`, `ScannerView`, `CubeView`, `Strip`, `Attention`, `Lesson`, `Controls`, `Environment`, `MeshKit`, `StoreGate`), `Shaders/` (`Conductor`, `Haze`, `Needle`, `Solid`, `Skin`, `Panel`).
- **Tools:** `Tools/simcore/` dotnet project compiling `Runtime/Sim/*.cs` for fast tests (`dotnet run -- test`) and the field bake (`-- bake`).

---

## 11. Validation

1. **Sim tests** (dotnet, seconds; also inside Unity): FLD1–FLD4 (primitives, magnet b_iso and DSV homogeneity, gradient efficiencies and linearity, birdcage b₁ and Kirchhoff), FLD5 table vs direct, FLD6 stable Δf; TIS1–TIS3; SEQ1–SEQ4; BLO1–BLO7; SIG1, SIG3, SIG4, SIG5; RX1 (full MaRCoS chain vs fast path ≤ 0.5 %), RX2 noise variance; PAR1–PAR4; REF1 (≥ 90 % of the band excited, ≤ 2 % outside); determinism across thread counts.
2. **Scene gates** (Unity, xvfb): G0 real boot path at an offset head pose; G1 all items in view; G2 no overlap; G4 strip faces the eye, fingers to the head's left, B₀ direction; G6 stillness without input; G7 recenter; G8 linked yaw; P1–P4 palette; T1 text whitelist at every cue; T2 no line renderers; F1–F3 haze ramps and B₁ fill (luminance ratios); F4 every coil visible at every cue; S1 needle count; S2 moving the region changes the needles; K1–K6 controls; negative tests (strip rotated 180° must fail G4; a disabled Gz renderer must fail F4; W = 1 T must fail F1).
3. **Lesson walkthrough** (both voices, fixed step): every cue's refs lit (e ≥ 0.95) and others dim (≤ 0.05), referent pixels ≥ 300 from the home pose, per-cue physics assertion, pause/resume identity, voice change restarts the cue at the same state, parameter change restarts with generic narration, navigation lands on step starts, focus loss pauses.
4. **Render review (decisive):** after every milestone, one full-scene image per cue and a per-step sheet from the default head pose (`validation/renders/`, published as `v0.8.0-*.png`), judged against §2 row by row. Findings are fixed before moving on.
5. **Device-only, unverified until tested on a Quest:** stereo instancing of needles, 72 Hz GPU time, additive glows over passthrough, text legibility, ray picking of knobs and icons, passthrough dimming, hand-tracking pinch, boundary behaviour.

---

## 12. Release

0.8.0 / versionCode 9, package `com.nebulytic.resonance`, private update signed with the existing test certificate (update continuity with 0.7.0), release-signed Horizon preview with the dedicated key. Pipeline: `Tools/simcore` tests → narration generation (both voices) → `ResonanceValidation.Run` → Android build → Windows build → `verify_apk.py` → `sign-release.py` → `publish-native.py` → `publish-handoff.py`. Preserve 0.7.0 evidence in `validation/baseline-0.7.0` before any fixture run; never delete published releases. Result in `.pipeline/RESULT-0.8.0.md`: downloads, changes, honest validation limits, unresolved items.

---

## 13. As built (deviations recorded during implementation and render review)

Each item names what changed and why; the code and the server checks follow this section.

**Physics**
- Magnet: the drafted pack table did not reach the DSV target with a midpoint 4 × 8 filament model. The packs were re-optimised against the true DSV objective with a 4 × 8 **Gauss–Legendre** quadrature per pack (weights, not NI/32). Result: centres ±36.31, ±126.51, ±301.14 mm; lengths 33.26, 49.26, 132.18 mm; b_iso 7.537 mT/A, so **1 T at 132.68 A** (64 A/mm²); 0.58 / 1.9 / 6.0 ppm over 12 / 14 / 16 cm (FLD2). Pack values are kept at full precision (rounding to 0.01 mm gave 5.2 ppm).
- Field tables: the 3-D tables are used where ρ ≤ 60 mm; beyond that (near the birdcage rungs) the field is evaluated directly (FLD5 b₁ error 0.24 %). Table format version 4 with a winding-definition hash.
- Larmor offset: per isochromat and gradient axis, a quartic polynomial in that coil's current replaces the per-step stable formula (max error 1.3 × 10⁻¹⁰ Hz at 1 T, FLD7). Segment phase weights are shared per segment. This took the acquisition of nine slices from 36 s to about 7 s on one server core.
- Signal set: 1 mm in-plane, **Δz/4 through the slice over ±1.25 Δz** (≈ 43 k isochromats at the default), instead of Δz/8 over ±1.5 Δz (≈ 60 k). REF1, SIG and RX checks pass at this density.
- Hand map for the glow: the RF band cut is measured from the pulse's band centre (4 × BW), not from 0 Hz.
- Receiver chain model: the NCO multiplies by 2·e^(−iφ). An e^(+iφ) mixer returns the conjugate, and the output grid must be aligned to the ADC samples (RX1 0.019 %).

**Lesson program**
- hard₁ starts with a 50 µs RF-free segment ("pre"), so cue 3.1 can hold on the Larmor precession with the RF still off.
- The phase-encode demonstration uses **row +16** (two turns across the cube, 8 needles per turn), not row +8. At row +8 the Gy lobe (±0.3 mT across the haze) was too weak to read.
- Cue changes after render review:
  - 1.3 is "Their field runs along the bore: one tesla, uniform where the hand lies."
  - 3.1 holds at hard₁.pre.start.
  - 3.6 ends at 95 % of the pulse, so the field it names is still on; 3.7 completes the tip.
  - 3.4 and 3.6 point at the cube's B₁ vector (`cube.b1`, with `field.b1` lit).
  - 7.4 points at the cube's field ramp (`cube.field`, with `field.b` lit).
  - 9.4, 10.2 and 10.3 point at the image slabs (`image.slabs`).
- The strobe of step 10 shows each slice at its last echo, so every frame shows a finished slice.

**Scene**
- Layout as reviewed:
  - scanner α −19°, ε +3°, d 1.15 m, scale **0.8** (at 0.6 the coils were too small to read);
  - cube α +21°, ε +1°, d 0.95 m;
  - strip α 0°, ε −27°, d 0.85 m (the plinth overlapped the strip at −25°).
  - ψ stays 60°.
- ‖B‖ haze:
  - W = **0.6 mT** (1 mT washed out the Gy ramp).
  - The haze box is limited to the imaging region, ±0.12 m radially and ±0.16 m along z, with a soft z fade. The full bore let the magnet's end fields saturate the view.
- **Cube field ramp** (new):
  - At ψ = 60° the scanner's +x axis is only about 11° from the line of sight to the scanner. An additive haze integrates the whole x range along every ray, so an x ramp cannot be seen there.
  - The cube (about 51° to x) therefore also shows the active gradient's field change across its 16 mm block, magnified with it: W_cube = W · (scanner scale / cube magnification) ≈ 26 µT. Only the stronger side is shown, so nothing is drawn without a gradient.
  - Element `cube.field` in group `field.b`.
- Signal dial: moved past the magnet's +z end (P (−0.19, −0.20, 0.39)) on a post, with a dark face behind the arrow. In front of the green packs the arrow was unreadable. The pointer aims at the dial's rim, so it does not cover the arrow.
- Receive cable re-routed beside the receiver chain; the filter's six plates share one material, so the whole filter lights.
- Region frame: a locator drawn over the skin and coils (ZTest Always), with 2.2 mm edges and a faint fill. Behind the translucent skin, the 13 mm frame was invisible.
- Cube region choice: tissue fraction ≥ 0.9 and bone (cortex + tendon) fraction 2–12 %. With 20 % cortex, the band appeared only at the top of the cube.
- Needles: 1.3 mm shaft radius, length 0.92 · s · min(‖m‖, 1.1), brightness (0.10 + 0.90 e)(1 − 0.30 depth), tail-to-head shading.
- The camera requests no depth texture (no shader samples it; it cost a depth pre-pass on Quest).
- The pointer approaches from above. Among elements matching a ref, the **element that owns the key** gets the pointer, and group members only light up (the scanner's axes, not the cube's copies). Without an owner, the nearest member gets it.

**Validation**
- G1 measures projected mesh vertices, not bounding boxes.
- F1–F3 compare renders with and without the haze at two points on each ramp (Gz at 5.3, the cube's Gx ramp at 7.4, Gy at 8.3).
- S2 moves the region 20 mm within the hand and back.
- P4 requires surface luma ≤ 0.15 (track ≤ 0.2).
- F4 checks every conductor part of every coil (enabled, active, above its brightness floor) at every cue.
- Not implemented: the negative tests of §11.2, G0 on a device boot path (the harness uses Editor play mode), and haptics/K-tests with real controllers.

---

## 14. 0.8.1: changes after the headset test of 0.8.0 (USER-REVIEW-0.8.0)

**Passthrough and compositing.** MR shows passthrough exactly as captured: `DisableColorMap()`, no edge rendering, opacity 1. 0.8.0 called `SetBrightnessContrastSaturation(-0.20, 0, -0.35)`, and 0.7.0 never did. The eye layer is premultiplied (`OVRManager.eyeFovPremultipliedAlphaModeEnabled = true`: out = rgb + room · (1 − a)). The shaders follow suit:
- light-only effects (haze, glows, magnet packs) use `Blend One One, Zero One` and write no alpha;
- alpha-blended matter and UI use `Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha`;
- opaque objects write alpha 1.
Nothing in the scene is a full-screen or large dark layer. Equipment (plinth, console housing) is lit mid grey (#5A626B). UI panels and screens stay dark. The VR studio is mid grey instead of black.

**Emphasis without darkening.** Referenced spins are opaque and white. The others are light grey and see-through (opacity 0.40, never darker than the room). Gradient coils and the birdcage are solid, lit, emissive tubes with a brightness floor of 0.42; the magnet packs add light (floor 0.22). Bones sit at 55 % brightness when not named. Strip text is near-white (#E8ECF0), 12 % larger, with a 0.128 caption.

**Views restored (all visible from the default head pose, attached to their sources, no text or legend).**
- Spin cube: a translucent volume of its own tissue (muscle, fat and marrow, tendon, cortical bone, skin; trilinear) around the needles. Referent `cube.tissue`.
- Microscope (`micro`, `micro.spins`, `micro.net`): one water-rich cell in the band core, magnified to molecules above the cube and joined to its cell by a faint zoom cone.
  - 40 water molecules form a frozen snapshot of liquid water.
  - Each proton carries a random moment turned by the cell's own rotation. In the lab frame this is Rz(carrier) · FromTo(z, M̂_rot); all protons of a cell share the Larmor frequency.
  - The cell's simulated net magnetization is bold. B₀ and, while RF is on, B₁ are shown.
- Console (`sequence`, `signal.trace`, `kspace`, `kspace.row`, `image`, `recon`), cabled to the receiver on the plinth:
  - sequence: RF envelope, Gz, Gy and Gx currents plus the ADC, in the coils' colours, with a "now" line. The time window follows the lesson's slowing, and a strobed step shows its whole repetition;
  - received samples (I bright, Q dim), revealed as they arrive;
  - k-space with the row being read;
  - the image;
  - a 3-D display of the stacked slices, turned like the scanner.
  - The k-space and image moved here from the plinth; the scanner keeps the in-bore image slabs.
- Narration: 84 sentences. New: 2.3 tissue, 2.5 microscope, 4.9 signal trace and 5.3 sequence. 2.6 and 2.7 point into the microscope, 3.5 at its protons, and 10.3 at the 3-D display.

**Layout** (az, el, d): scanner −20°, +9°, 1.15 m; cube +22°, +3°, 0.95 m; microscope +20°, +21.5°, 1.0 m (radius 0.09 m); console 0°, −19.5°, 0.78 m (faces the eye); strip 0°, −31.5°, 0.80 m (scale 1.12).

**Frame rate.**
- The spins (cube and hand sets, hand EMF and glow data) are evaluated on a dedicated worker thread. The views read a finished result one frame later (14 ms of real time, microseconds of physical time); validation stays synchronous and exact.
- Needle matrices are built directly with one-pass batching; spin-set membership is cached; the glow texture uploads only when new; strip strings change only when their content does.
- No managed allocation per frame (gated). Dynamic fixed-foveated rendering is set to high, and CPU/GPU to sustained high.
- An optional strip readout shows CPU/GPU ms and Hz (off by default; "ms" icon or F).
- The harness writes validation/frame-cost.json with main-thread, worker, allocation and GPU-proxy numbers.

**Validation.**
- Review renders are MR eye buffers composited over mid grey (and a lit room) exactly as on the headset, at a Quest-like 76° vertical view.
- F1–F3 measure added light in linear luminance.
- P4 keeps UI surfaces dark and equipment lit.
- G1/G2 cover all five items.
- Before/after sets come from `ResonanceValidation.RunMR` (Tools/run-mr-preview.sh), and brightness and clarity metrics from Tools/mr_compare.py.

---

## 15. 0.8.3: the 0.8.x simulation with the richness of 0.5.0 (USER-REVIEW-0.8.2)

0.8.2 (the fixes after USER-REVIEW-0.8.1) was not released on its own; it is folded into 0.8.3. The review reverses several 0.8.0 invariants (§1.1). Where this section and earlier sections disagree, this section governs.

**Reversed invariants.**
- *No auto-advance* becomes **continuous narration**. The app starts playing by itself and moves to the next step when a step ends; it stops after the last step. A jumps forward, B back, X pauses. Validation (FixedStep) keeps the old stepwise behaviour so each step can be checked.
- *Fields only as volumetric shading* becomes **field lines and arrows**.
  - B₀ lines are traced along the magnet's computed field from 19 points of the z = 0 plane.
  - Each vertex carries the \|B\| deviation per unit current of the magnet and of each gradient coil. The Line3D shader lights it from the live currents: v = 0.5 + 0.5 tanh(ΔB / 0.3 mT), brightness 0.2 + 1.6 v². Markers flow along +z.
  - B₁ is shown by 45 instanced arrows, A(t)·b₁(r) at 3 × 3 × 5 points in the birdcage, turned by the displayed carrier.
  - The \|B\| and B₁ volumes are removed.
- *No text in the scene* becomes **short labels that name or measure what they sit beside**:
  - axis letters;
  - live values beside the plots and the close-up;
  - receiver rates;
  - z and Δf along the bore while Gz runs;
  - one short equation per plot.
  Legends (colour keys) are still excluded; T1 checks that all text is on the strip, controller help, or such a label.
- *No plots* becomes **3-D line plots in the scene** (Line3D ribbons that turn to the eye in the vertex shader, built once per window, readout or row batch, revealed by a shader key). The console's flat screens are removed.
- *Knobs* are removed with no replacement; the protocol is fixed at its default. Region dragging remains.

**Proton volume** (`CubeView`, referents unchanged: `cube`, `spins.*`, `cube.b1`, `cube.field`, `axis.*`, `region`; addendum: random positions).
- 100 isochromats (`Layouts.ProtonCount`) at random positions in tissue in a 5 × 5 × 12 mm block at the slice (`IsoSet.Scatter`: fixed seed; no two closer than 0.55 of the mean spacing in display-normalised units).
- The block is displayed stretched in-plane (5 : 5 : 4, 68 mm per unit).
- Each drawn proton stands for its isochromat's protons; there is no proton-density sampling. Neighbours in the same local field behave alike because each isochromat sees its own field.
- `spins.col` is the 0.7 mm x-slab of the band holding the most protons.
- The simulation-core twist tests fit the phase's spatial frequency by maximum coherence.
- Thermal bias κ = 2.5 (mean cos 0.61).
- The proton's direction is R(isochromat) applied to its thermal direction:
  - the tip θ of the isochromat's net magnetization and its transverse azimuth;
  - free precession Evaluator.Free;
  - minus the **displayed carrier**: App.DisplayCarrier, one turn per 2 s while playing; during carrier-speed cues the physical carrier plus a fixed offset, so it never jumps.
  - The direction is lerped toward an isotropic direction by \|m\|/PD (relaxation).
- A spin ring with a bead turns about each needle (symbolic spin, one turn per 0.9 s).
- Selection: `CubeView.PickProton(ray)` takes the nearest proton within 0.4 spacing of the ray. The trigger selects in XR and a click on desktop; the default is a water proton in the band core. A halo marks the selected proton.

**Close-up** (`CloseUpView`; referents `closeup`, `closeup.lab`, `closeup.rot`).
- The selected proton is shown in two views with B₀ vertical: the lab frame (displayed carrier) and the rotating frame.
- Each view shows μ with its spin ring, the precession circle (height cos θ, radius sin θ), the tip's path (48 instanced dots, 1/16 s apart), M (the isochromat's net magnetization over PD), B₀, B₁ while RF is on, and labelled axes.
- A readout line gives position, tissue, current Δf, Mz, M⊥ and phase.
- A zoom line joins the proton in the grid to the close-up.

**Subtitles** (addendum). The strip shows the spoken sentence as subtitles, on by default. The CC chip (or C on the desktop) toggles them.

**Theme** (addendum: redesign after a critique of the renders).
- *Palette:* B0 #46D6A0, RF #FF6496, GX #B06EFF, GY #5A9BFF, GZ #2FD3E6, MAG #F4F6FA, SCAF #60697A. Pairwise ΔE2000 ≥ 23.4; no gold in any additive mix.
- *Neutrals:* PANEL #101419 (α 0.86), HAIRLINE white 7 %, chips white 7 % / 18 %, TRACK #2A313B, STRUCT #4B535E.
- *Text scale:* TEXT #F2F4F7, TEXT2 #B4BCC7, TEXT3 #7E8894.
- *Shading:* `ResLighting.cginc`, shared by Solid, Needle, Conductor and Glass:
  - wrap diffuse, saturate((n·L + 0.35)/1.35);
  - sky/ground ambient;
  - Blinn-Phong highlight;
  - cool fresnel rim.
- *Magnet packs:* light-only glass shells, emission e·0.45 plus fresnel, with no winding stripes.
- *Shapes:* the plinth and cradles are rounded slabs; the proton volume has corner brackets.
- *Strip:* a pill of dark glass with a hairline edge, centred subtitles, a thin progress line, and lit chips.
- *Pointer:* a soft-white light-only chevron that faces the eye and bobs (±4 mm).
- *VR studio:* a gradient dome.

**Plots** (`PlotsView`; referents `sequence`, `signal.trace`, `kspace`, `kspace.row`, `image`, `recon`).
- **Sequence:** 5 rows receding in depth, a moving cursor, live values, the window span, and "k = (γ/2π) ∫ G dt".
- **Signal:** the readout's samples as a 3-D line (t, I, Q) with I and Q shadows and v(t) (12 carrier cycles over the readout), revealed by sample time. A link runs from the plinth's receiver port.
- **Data:** one 3-D line per measured k-space row (height and brightness from log magnitude), revealed by sample time and rebuilt at most twice a second while rows arrive; the image; the 3-D volume.

**Layout** (az, el, d):

| item | az | el | d |
|---|---|---|---|
| scanner (scale 0.7) | −31° | +5° | 1.25 m |
| data | −3° | +13° | 1.12 m |
| grid | +26° | +7° | 1.0 m |
| signal | −33° | −19° | 0.85 m |
| sequence | −3° | −17° | 0.85 m |
| close-up | +28° | −18° | 0.85 m |
| strip | 0° | −35° | 0.8 m |

Plots, close-up and strip face the eye.

**Narration**: 83 sentences.
- 2.6 introduces the close-up and selection.
- A new 3.2 says the precession is shown slowed; 3.3–3.8 follow.
- 3.5 and 3.7 point at the close-up's rotating frame.
- 4.9, 5.3 and 10.6 name the new plots.

**Anatomy**: muscles are drawn again (translucent, as in 0.5.0). Bones, muscles and skin are decimated to about 10 % (34 000 triangles).

**Frame rate** (from 0.8.2):
- `Work.MaxThreads = 1` on Android.
- Buffers are reused in the receiver and the evaluator.
- Relax-only maps are cached per slice.
- Signal sets are released after their slice.
- No per-frame mesh rebuilds.
- 4x MSAA on every quality level.

## 16. 0.8.4: fixes from the 0.8.3 headset review (USER-REVIEW-0.8.3)

**Palette** (`Look`):
- **Axes:** x red #FF4238, y green #2EDB4E, z blue #3D7BFF. The gradient coils take their axis colour (Gx, Gy, Gz).
- **Fields:** B₀ cyan #1FD6EE (changed from green); RF and B₁ magenta #FF3DC8. Magnetization M, frames, outlines, zoom lines and the pointer are white.
- **Surfaces:** backings (PANEL) #0A1024, rims #3A5BB8, and the base and equipment a saturated blue #2848A8. Text is white, #D2E2FF or #A9C3FF, never grey.
- **Casing:** lines, text and the pointer have a dark casing (INK #060A18).
- **Protons:** `Look.Phase(phase, v)` gives hue = phase + 190° (so M along +y′ is violet), saturation 0.88, value = 0.42 + 0.58 sin(min(θ, 90°)).
- **Checks:** P1 entity ΔE2000 ≥ 20 (minimum 27); P2 no gold in any token (entity colours are opaque, so there are no additive mixes); P5 no mid grey (S < 0.12, 0.15 < V < 0.92) in any material or label.

**Proton block** (`CubeView`, `Layouts`):
- **Geometry:** 5 × 5 × 10 mm, 100 isochromats at random positions. The minimum separation is 0.55 of the mean spacing (1.36 mm), which is also each proton's display cell (sinc averaging).
- **Display:** one uniform magnification of 50 (0.25 × 0.25 × 0.5 m). PhysicsYaw is 50° (was 60°): the fingertips point 40° from the viewer's left, and x is foreshortened to 64 % instead of 50 %.
- **Frame:** far walls built from an inward box (`Backdrop` shader, 1 mm grid) and twelve white edges.
- **Directions:** an x/y/z triad at the near bottom corner, and a B₀ arrow along the top edge on the viewer's side.
- **Protons:** needle and spin ring, tinted by `Look.Phase`.
  - Their hue blends from the proton's own thermal azimuth (at rest) to the isochromat's phase, weighted by the tipped share times the isochromat's order.
  - The thermal bias κ is 4.
  - Protons that the sentence names are opaque; the others have opacity 0.28, and all are opaque when no spin set is named.
- **Gradient:** while |G| > 0.2 mT/m, the dominant axis shows a flat eye-facing tapered arrow (`LineBuilder.Taper`) inside the block along the edge nearest the eye. It carries the frequency offsets ±γ̄|G|L/2 at its ends. The walls take up to 30 % of the axis colour toward the stronger field (`_Ramp`).

**Scanner** (`ScannerView`):
- **Magnet:** packs are solid `Conductor` shells (cyan, body 0.78 + 0.30 e) with a cutaway from 125° to 235° physical azimuth (`MeshKit.PackCut`); cut faces are shaded at 0.68.
- **Coils:** gradient tubes have radius 6.2 mm and the birdcage 4.8 mm (physical), with a floor of 0.55.
- **Field lines:** opaque with casing. Mode 1 shades them from 0.28 of the colour (weak) through the colour to 65 % toward white (strong).
- **Glow:** the hand's glow is premultiplied (volume mode 4, |M⊥| only, violet to white). The image slabs use the `Image` shader, transparent where dark.
- **Block outline:** the twelve edges at the block's true size, drawn over everything. Four zoom lines join the outline's face that faces the block to the block's face that faces the outline, corner to corner.
- **Receiver:** the chain is spread along the base (z = −0.24, 0, +0.24 m). Its two-line labels show only while that block is named, and the magnet's current label only while the magnet is named.
- **Labels:** "B₀ 1.00 T" sits above the magnet's −z end, and "slab 5.0 mm" under the magnet.

**Plots** (`PlotsView`), each on an opaque `Plate` panel with a rim that lights while named:
- **Sequence** (0.42 × 0.275 m): 4.2 mm traces.
- **Signal** (0.42 × 0.30 m): the I/Q trace coloured by `Look.Phase` of the signal, with v(t) in magenta.
- **Data** (0.48 × 0.30 m):
  - k-space as one lit strip per measured row (`Relief` shader, navy-violet-white by height, clipped until sampled), with a cursor above the row being read;
  - the image (`Image`, opaque);
  - the 3-D image as nine transparent slice quads sharing the scanner's slab textures.
- **k-space labels:** kx/ky and the "k-space · rows" label show only once a row is measured.

**Close-up** (`CloseUpView`):
- On a 0.52 × 0.47 m backing.
- μ is in the proton's colour (1.35× thicker), and M is slim and white.
- The rotating-frame circle carries the phase colours; the lab circle is pale blue.
- B₀ and B₁ labels carry their values, and f₀ appears under the lab frame.
- The tip path resets on a cue change or a jump in physical time.

**Rotation:** `App.SharedRotation` is a full quaternion (`SetShared`, `RotateShared`; `YawShared` kept).
- Controllers: a held item follows `aim.rotation · s.Rotation`; the scanner and block use `aim · inv(grabAim) · grabShared`; the strip only moves.
- Desktop: right-drag on an item turns it about the camera's up and right axes, and Q/E roll it.

**Layout** (az, el, d):

| item | az | el | d |
|---|---|---|---|
| scanner (scale 0.66) | −31° | +6.5° | 1.4 m |
| block | +1° | +7.5° | 1.08 m |
| close-up | +32° | +6.5° | 1.15 m |
| signal | −28° | −18° | 1.02 m |
| sequence | 0° | −17° | 1.0 m |
| data | +28° | −18° | 1.02 m |
| strip | 0° | −39.5° | 0.8 m |

The plots, close-up and strip face the eye in yaw and pitch.

**Text:** scene labels share one outlined TextMeshPro material (dilate 0.18, outline 0.26 INK). Labels printed on a panel lie flat on it; only labels floating in 3-D turn to the eye. Sizes are title 0.28, label 0.22, value 0.19 and note 0.17 (scanner-local 0.40 and 0.34). The strip is 0.72 × 0.152 m with subtitles at 0.2 on two lines. Check TX requires every scene label's capital height ≥ 0.6° and the subtitles ≥ 1° from the default pose.

**Narration** (schema v9, 96 sentences, 10 steps):
- Steps 1–2 are the introduction (check INTRO).
- `[display|spoken]` groups give subtitles and voice their own forms (B₀ / "B zero").
- The lint allows colour words and "plot"; directions, legend words and gold still fail.

**Review renders:** the MR eye buffer is composited over a CC0 bright kitchen photograph (`validation/backdrops/room-photo.png`), with a mid-grey copy in `validation/renders/grey`.

**Build hygiene:** `ResonanceBuildSanitizer` (IPreprocessBuildWithReport, order 100) clears the DevAgent's address and tokens after the Meta XR SDK's DevAgentBuildProcessor (order 1) injects them. `verify_apk.py` checks that no server address and no Unity-prefs AgentBridge token appear in `assets/bin/Data`.

**Panel text:** flat text sits 1 cm in front of its backing (not in front of the 3-D content), so it does not shift against the panel with the viewer's height.
