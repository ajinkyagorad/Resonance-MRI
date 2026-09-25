# Resonance MRI 0.7.0: redesign after the 0.6.0 review

The review said the chain of explanation was broken. Each of its ten points is answered below.

1. **One colour per concept, with a key.**
   - B₀ green · RF / B₁ / RF coil / RF currents orange · x & Gx red · y & Gy blue · z & Gz cyan (bore axis, ∥ B₀) · selected slab magenta · received samples & I yellow · Q lavender · magnetization silver · "what the narration names now" white.
   - Anatomy keeps natural colours, and idle conductors are neutral grey.
   - A movable **colour key** (KEY button) lists every entry.
   - The fixture checks that all key colours are pairwise distinct, and the narration never refers to a colour.
2. **Coils.**
   - New `ResonanceCoils`: five named systems (B₀ main winding, RF birdcage, Gx saddle pair, Gy saddle pair, Gz Maxwell pair).
   - When a sentence names a coil, only that coil is shown, in its concept colour, with a callout (name and current state) and 3D arrows moving along the conductors in the direction of its present current.
   - RF rungs show per-rung current direction. With no coil named, all coils are neutral grey with small name labels.
3. **f(z) plot.**
   - Axes are labelled with units and sign: z in mm (+z to the right, as in the scanner) and f − f_RF in Hz.
   - The origin is labelled (slab centre z₀, f = f_RF), the RF band and slab have their true widths (a 0.6.0 bug drew them at half size), and a cyan line joins the plot origin to the scanner slab.
   - The scanner was turned so its +z also points right.
4. **Axes.** The gizmo moved to the frame corner ("axes measured from the isocentre"). A separate isocentre marker sits at (0, 0, 0), and the narration explains why the isocentre is special: the gradient fields are zero there.
5. **True 3D spins.**
   - The tissue cube shows a 60 × 60 mm patch through seven depths of the slab. The z stretch factor is labelled, and 175 lit 3D arrows fill the volume.
   - Front-plane arrows show each image voxel's net magnetization (the average through the slab).
   - Dephasing appears as a helix through depth, and the Gy/Gx twists as 3D sheets of arrows with blue/red phase ribbons.
6. **A/B.** Coloured circles are gone. A and B are neutral letter pins with coordinates in the cube, scanner, microscope and plots.
7. **One sentence per cue.**
   - 85 sentences, each declaring its object, spin set, coil, field region, plot element, and the exact span of the sequence it shows.
   - A white callout above the focus brackets names the referent.
   - The fixture checks every sentence: callout text, spin set, the only visible coil, the field region and the plot trace or sample.
8. **Dark only.** Light mode and its button are removed. Panels and the VR room are dark.
9. **Controls.** Right A = next step, B = previous step, either stick left/right = previous/next, left X or right-stick click = pause/play, left-stick click = arrange, left Y = help. Help remains opt-in and adjacent to the actual controls.
10. **Narration rewritten for precision**, in both voices (Michael, George). Corrections include:
    - equilibrium polarization of about 2 × 10⁻⁷ at 64 mT;
    - random-phase precession, so the transverse components cancel;
    - flip angle ∝ ∫B₁ dt;
    - Δz = BW / (γ̄G_z);
    - a rephasing lobe of about half the area;
    - the prephaser placing the echo at the readout centre;
    - induction in the RF coil as receiver;
    - the MaRCoS ADC → NCO mixing → CIC chain;
    - S(k) as a spatially weighted whole-slice sum;
    - MR contrast that is not X-ray attenuation.

The receiver instrument no longer repeats a linear-time sequence plot. The central timeline owns the sequence.

## Limits

These are server fixture checks and software renders only. No physical Quest has been used, so comfort, performance, input and passthrough behaviour remain unverified. The physics is the same ideal single-coil Cartesian teaching model.
