# Resonance MRI 0.8.3: the 0.8.x simulation with the richness of 0.5.0

This revision answers USER-REVIEW-0.8.2 and its addendum (random proton positions, subtitles, a redesigned theme). 0.8.2 was not released on its own; its fixes from the 0.8.1 headset test (frame rate, the magnified moments, fewer renders) are part of this release. All checks and images are from the build server; the headset still has to confirm the frame rate.

## What the review asked, and what changed

**Combine 0.5.0 and 0.8.x.** The 0.8.x simulation core is kept unchanged: real windings, fields computed by Biot–Savart, Bloch isochromats, the receiver chain, k-space and the images. On top of it, 0.5.0's views and information come back, now driven by that simulation:

| 0.5.0 | 0.8.3 |
|---|---|
| resonance microscope: lab and rotating frames side by side, B0, B1, the tip | close-up of the selected proton in both frames: μ with its spin, the precession circle, the tip's path, M, B0, B1, live values |
| 100 representative moments in a tissue crop | a volume of 100 isochromats of the hand at random positions, one proton each |
| B0 field lines and 45 rotating B1 arrows in the bore | B0 field lines traced from the magnet's computed field, brightness from the local \|B\| so a gradient is a ramp; 45 B1 arrows from the birdcage's computed field |
| pulse sequence traces with a cursor and live values | a 3-D line plot of RF, Gz, Gy, Gx and ADC with a moving "now" line and live currents and gradients |
| receive chain: v(t), I(t), Q(t), 3-D t/I/Q signal | the received samples as a 3-D line in t, I and Q with its I and Q shadows and v(t); receiver blocks labelled with their rates |
| k-space relief (height = log \|S\|) | one 3-D line per measured row, height = log magnitude, revealed sample by sample |
| reconstructed MRI volume | image and 3-D volume beside k-space; image slabs in the bore |
| equations and readouts on the panel | short equations beside the plots, z/B/f along the bore while Gz runs, axis letters |

**Remove the knobs.** The sliders are gone and nothing else replaces them. The block of tissue shown in the proton volume can still be moved by grabbing its frame in the hand.

**No tiny plots, no flat panel for plots.** The console with its four 13 cm screens is gone. The plots are lines standing in the scene (26–28 cm wide), with no backing panel.

**About 100 protons at random positions, each visibly spinning and precessing, driven by the simulation.**
- **The volume.** 100 isochromats of the hand's tissue at random positions (not a lattice) in a 5 × 5 × 12 mm block at the slice.
  - In-plane, 5 mm keeps the readout's phase twist to about a turn across the block, so its pattern is readable.
  - Through the slice, 12 mm holds the 5 mm slab and tissue outside it.
  - No two protons are closer than about half the mean spacing, so each stays distinct when magnified.
  - The block is displayed stretched in-plane (5 : 5 : 4).
- **Orientations.** Each proton's thermal orientation is random, with a bias toward the field that is exaggerated so it can be seen; the narration says so.
- **Neighbours.** Protons sitting in the same local field behave alike, because each is its own isochromat of the simulation, with its own field.
- **Drawing.** Each proton is a lit needle (its moment) with a ring and bead turning about it (its spin). It precesses about B0 in the lab frame at one turn every 2 s, and the narration says it is slowed.
- **Physics.** The tip, relaxation and phase come from its isochromat: only protons inside the RF band tip, and gradients twist the volume.

**A close-up of any proton.** Point at a proton and pull the trigger, or click it on the desktop. The close-up follows it, and a faint line joins it to its place in the volume.

**Narration flows continuously.** It starts by itself and runs on from step to step. A jumps forward, B back, and X pauses.

**Subtitles.** The spoken sentence is shown as subtitles on the strip, on by default. The strip's CC chip, or C on the desktop, turns them off and on.

**Theme: critique and redesign.** Looking at my own 0.8.3 renders before the redesign:
- *Colour:* five saturated primaries (neon green, magenta, violet, blue, cyan) at similar brightness on flat grey, with no hierarchy. This was the dated "vector display" look.
- *Materials:* magnet packs as translucent rings with a stripe texture, and hard grey boxes.
- *Lighting:* almost flat, with no highlights, rim or ambient gradient, so objects read as cardboard.
- *Typography:* many grey label sizes of similar weight, and cryptic tiny toggles.
- *Pointer:* a grey cone with an odd glow.
- *UI chrome:* a dark slab with a hard outline and ticks.

The redesign:
- *Colour:* a calmer palette of balanced lightness (mint B₀, rose RF, violet Gx, azure Gy, cyan Gz, soft white for magnetization), still pairwise ΔE ≥ 23 and still free of gold. Graphite neutrals, and a three-step text scale (primary white, secondary grey, tertiary slate).
- *Lighting:* one shading model shared by every lit surface: a soft wrap-around key light, a sky/ground ambient, a gloss highlight and a cool rim. Coils, protons, bones and equipment now read as finished objects.
- *Materials:* the magnet packs are plain glass shells lit at their edges, with no stripes. The plinth and cradles are rounded slabs. The proton volume has corner brackets instead of a wire cage.
- *UI chrome:* the strip is a dark glass pill with a hairline edge. Subtitles are centred; the transport icons are small; the toggles are lit chips (CC, MR/VR, voice, ms, i).
- *Pointer:* a small soft-white chevron that faces you and bobs gently toward what is being named. It adds light only, so it never darkens the room.
- *VR studio:* a soft gradient dome replaces the flat grey room.

**Clean, minimal UI.** The console and the sliders are gone. Labels only name or measure what they sit beside.

**Rules kept.**
- no gold
- no legends: labels only name or measure what they sit beside
- dark UI
- light-only glows over passthrough
- 72 Hz budget
- 10 review renders per iteration

## Frame rate (the 0.8.2 work, folded in)

- **Background acquisition.**
  - Its parallel loops took every CPU core for tens of seconds after start and after every change. On the headset they now run on one low-priority thread.
  - It produced 5.3 GB of short-lived arrays, now 0.47 GB. Its live heap fell from 120 MB to 31 MB, so incremental garbage collection has far less to do on the main thread.
- **Spin evaluation.** Changing repetition allocated 3.4 MB; it now allocates 10 kB.
- **Hand model.** Bones and skin are decimated from 226 000 to 22 600 triangles. The muscles, drawn again as in 0.5.0, are decimated the same way: 34 000 triangles in all.
- **Meshes.** No mesh is rebuilt per frame: plots are built once per window, readout or row batch and revealed by the shader; lines turn to the eye in the shader.
- **Volumes.** The \|B\| and B1 volumes are replaced by lines and arrows. Only the hand glow (10 steps) and the small 3-D volume (16 steps) are still raymarched.
- **MSAA.** 4x MSAA now applies to the quality level Android actually uses. 0.8.1 set it only on the editor's level.

## Limits

- No Quest was used. Check the frame rate with the strip's **ms** icon and the comfort of the new layout from your seat.
- Not restored from 0.5.0:
  - the Slice Lab (point patterns, drawings, PNG phantoms, follow-pixel);
  - individually movable bones and the tissue-mode toggle;
  - the water-molecule view;
  - the light theme (the dark UI is a standing rule).
- Each proton is drawn in the classical picture: a moment with a thermal direction whose bias toward the field is exaggerated about 10⁵ times. The simulation itself tracks each cell's net magnetization.
