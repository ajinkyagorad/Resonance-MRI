# Resonance MRI 0.8.0: rebuilt from scratch after the 0.7.0 review

The 0.7.0 runtime was deleted. 0.8.0 is a physical simulation of one 1 T extremity scanner (Scanner-model). Every field comes from the drawn windings and their currents. Every arrow is a simulated isochromat of the hand's tissue, and the signal and image come from those spins. The lesson is one physical timeline narrated in 80 sentences. Each point of the review is answered below.

1. **Real magnet coil.**
   - B₀ comes from six NbTi winding packs carrying 132.7 A (1.000 T), drawn as wound packs in green.
   - All fields (magnet, three gradient coils, birdcage) are computed from their conductors by Biot–Savart: loops by elliptic integrals, straight segments exactly.
   - Spins, signal, k-space and image are computed from the same currents at one physical time. Nothing is animated by hand.
2. **All coils always visible.**
   - The magnet, Gx, Gy, Gz and the RF birdcage are drawn in every frame, translucent.
   - A coil brightens with its own current and when the sentence names it. No coil appears or disappears.
   - RF comes from the birdcage (its rungs carry the computed rung currents), and Gz from the Maxwell pair.
3. **Layout.**
   - Fingers point to your left and the hand sits at eye level in the scanner.
   - The narration strip is below, facing you. The spin cube is to the right.
   - The layout is computed once from your head pose at start and again only when you recenter. It never adjusts on its own.
   - Scanner, cube and strip can each be grabbed and moved.
4. **Realistic.** The scanner is a 1 T superconducting extremity design; its numbers are in Scanner-model:
   - a 2-D spoiled gradient-echo protocol (5 mm slice, 64 × 64, 128 mm field of view, TE 6 ms);
   - literature relaxation times for seven tissue classes of the hand;
   - the reciprocity signal with thermal noise;
   - the MaRCoS receiver architecture.
5. **Spins in the matter, one thing at a time.**
   - In the scanner, the hand's tipped magnetization glows where it is, and the anatomy stays faint unless named.
   - The cube magnifies a 16 mm block of real tissue.
   - Each sentence lights only its own referents.
6. **A volumetric grid of every spin.**
   - The cube shows all 16 × 16 × 16 = 4096 isochromats as lit 3-D needles. Each has its own tissue values.
   - Only the band whose Larmor frequency matches the RF tips. It then dephases, relaxes back along the field, and its tipped part gives the signal.
7. **Tissue drives the simulation.** Moving the region or changing a scanner setting resamples every isochromat and recomputes the acquisition. The test moves the region 20 mm within the hand: 2593 of the 4096 cells change tissue class, and moving it back restores the same tissue.
8. **Explanation from physical reality.** Steps 3, 5 and 6 say it in these terms:
   - the Gz current makes each plane's Larmor frequency different;
   - the RF field turns at one frequency and fills the coil;
   - only spins that keep step with it accumulate a tip.
   The narration's numbers are computed by the simulation code.
9. **Field strength and gradients in the setup, no plots.**
   - |B| is a green haze through the bore: uniform for B₀, and a brightness ramp along the axis of each active gradient.
   - The x axis runs almost along your line of sight to the scanner, where a haze cannot show a ramp. The cube therefore also shows the gradient's field change across its block, on the same scale magnified with it.
   - Server gates measure each ramp in the renders: brighter toward +z, +x and +y by more than 3 : 1.
   - There are no plots anywhere.
10. **Colours.**
    - One closed palette: B₀ green, RF pink, Gx violet, Gy blue, Gz cyan, magnetization white, structure grey.
    - Every pair of entity colours differs by ΔE2000 ≥ 20.
    - No gold, amber, orange or yellow in any colour or additive mix (checked).
11. **Linked orientation.** The cube's axes are the scanner's axes, and turning either turns both (checked).
12. **Nothing happens / nothing pointed at.** Every sentence has a referent in the scene and the pointer on it. Its physical time moves or holds on a visible state. The server renders one image per sentence from the default head pose, and each was reviewed.
13. **No legend, no text in the scene.**
    - The key is gone; a single pointer points in the figure.
    - The axes are one unlabelled arrow each (+x, +y, +z) at the scanner and at the cube.
    - The only text is on the narration strip (checked at every sentence).
14. **Field fills the volume.** The haze is a raymarched volume through the whole bore. There are no field lines, and the scene contains no line renderers (checked).
15. **"Phase across slab thickness" plot.** Removed. The through-slab dephasing and its undoing by the rephaser are visible in the 3-D needles (6.9, 6.10).
16. **No lines or planes in the cube.** Every moment is a lit 3-D needle; there are no ribbons, sheets or connected lines.
17. **Every sentence highlights its referent as it is spoken.** Emphasis switches at each sentence start, slaved to the audio. A server check confirms that every one of the 80 sentences lights exactly its referents.
18. **Controls.** A next step, B previous step, either stick left/right steps, X pauses. The UI is dark only.
19. **Simpler.** Three things (scanner, spin cube, strip), ten steps, one sentence per cue. No menus, pages, plots, key or light theme.

## Known limits of this revision

- From the default seat the scanner's transverse plane is seen nearly edge-on (its x axis points almost at you). Two views compensate: the console shows the image face-on, and the cube shows x ramps and twists. Turning the scanner by hand also works.
- The dial arrow is small (8 cm) at 1.2 m. Its legibility in the headset is untested.
- All checks and images come from the server. No Quest was used, so frame rate, comfort, input and passthrough behaviour remain unverified.
