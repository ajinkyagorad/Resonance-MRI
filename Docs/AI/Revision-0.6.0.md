# Resonance MRI 0.6.0 feedback implementation

Feedback addressed: "it doesn't point out and highlight the specific set it is operating on"; what Gx and Gy control; whether gradients activate different places; whether k-space position changes and the response is re-acquired; the 3D volume not showing how moments behave; and placement that prevented watching scanner, tissue and plot together.

## One shared acquisition state

- `ResonanceState` is computed once per frame from physical sequence time. It holds the segment (excite, rephase, Gy, X prephase, readout, recovery), gradient waveforms, k(t), slice centre and thickness, the expanded RF carrier clock, and the **operand**: the set of spins the current interval acts on.
- The scanner (laboratory frame), hand grid, tissue cube, microscope packets, timeline and operand plot all take their moments from one function, `ResonanceState.Moment(r)`. The laboratory frame is the rotating frame turned by the shared carrier phase, and nothing else.
- Lesson visuals and narration now use one clock. Before this, the visual cue was located by `progress × narration length` while the padded lesson duration was longer, so visuals lagged the spoken phrase. Seeking a phrase or changing voice also rescaled the time; both now set the exact phrase time.

## What each interval acts on, shown everywhere at once

- **Gz + RF band:** the operand plot draws the local Larmor frequency f(z) = γ̄Gz·z against the shaded RF band. They meet only in the gold slab, which also glows in the scanner and the tissue cube. Other layers of the cube stay along B₀.
- **−Gz rephase:** the cube's depth needles fan out at the end of RF, so each voxel arrow is short (|M| ≈ 0.02 in the model). The reversed lobe brings them back (≈ 0.92). The plot shows φ(z) flattening.
- **Gy:** the sequence now runs Gy alone (2 ms), then the Gx prephaser (2 ms). Areas, k-space and data are unchanged. During Gy the whole slice stays excited and the phase depends on y only, as φ = −2π·k_y·y. Purple ribbons in the cube twist along Y, and every X column twists identically. A gold A→B ribbon, the φ(y) plot with A/B markers, and φ/Δf row labels in the microscope all show the same twist. It stays after Gy stops.
- **Gx readout:** Δf = γ̄Gx·x, with each X column turning at its own rate while the stored Y twist rides along. A and B share one rate. The plot shows Δf(x); the microscope labels the column frequencies.
- **Reception:** each I/Q pair is the sum over the whole slice. The plot shows the running phasor, and a Σ M⊥ dial on the cube shows the crop's share.
- **Repeats:** each Gy area is one new k_y row, with the whole slice excited and measured again. The plot marks the acquired rows, and step 10 now plays one slow repetition, then three more, before filling.

## Every phrase lights its own elements (acceptance requirement)

Each narrated phrase in all twelve steps declares five highlight channels in `narration-cues-source.json`: object (target), spin set (all · slab · A/B · x = 0 column · nucleus), coil (B₀ · RF · Gx · Gy · Gz), field region (B₀ contours · B₁ samples · active gradient) and plot element (RF/Gz/Gy/Gx/ADC trace, operand plot, current receiver sample). While the phrase plays:

- the named voxels pulse in the cube, and the same packets pulse in the microscope and the scanner (A/B probes, slab);
- the named coil glows while the others dim;
- the field region is emphasised: wider B₀ contours, wider B₁ samples, or thick pulsing gradient arrows on the cube;
- the named trace thickens and pulses, the operand plot gains a gold frame, or a gold marker rides the ADC gate at the current sample.

The runtime fixture seeks every phrase, waits 0.6 s into it, and checks that each declared channel is actually lit and that every phrase lights its object plus at least one physical element.

## Volume representation

The 100 random-seeded symbols are replaced by a 5 × 5 × 5 voxel lattice through the 60 mm crop. Each thick arrow is one voxel's net magnetization in the rotating frame. Thin needles are isochromats at five depths through the slab. A and B keep their orange/blue identity at (0, ∓12 mm). Gradient arrows appear only while a coil carries current and follow its sign. B₀ lines in the cube brighten with the local Bz offset.

## Placement

A new movable **acquisition timeline** sits directly under the microscope and above the scanner (left) and tissue cube (right). The recentred Quest head pose sees all four at once. Controls, receiver and k-space flank the cluster and face the viewer. The desktop camera frames the same arrangement.

## Limits

These are server renders and fixture checks only. No physical Quest has been used, so headset comfort, frame rate, controller alignment and MR/VR boundary behaviour remain unverified. The model is the same ideal single-coil Cartesian simulation, and the timeline draws intervals with equal emphasis, not to time scale.
