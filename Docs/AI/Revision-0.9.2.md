# Resonance MRI 0.9.2

Coils fade to translucent context when unnamed. Receiver shells expose schematic internal dies and filter stages below the coil envelope. Fields use precomputed conductor fields, superposed with the shared current/time state; moving markers indicate direction, not field propagation speed.

The tissue is a 6 x 6 x 8 isochromat lattice in a 5 x 5 x 10 mm block. Net moments are the default; M / μ toggles illustrative microscopic directions. Air remains empty. Tissue, phase colouring and field overlays have independent compact controls. Sync is off initially; enabling it aligns tissue with the scanner. All axes and local fields move with their own physical view.

The full scan sequence covers the specimen’s complete z bounds at the protocol slice spacing. Reconstructed images remain acquired data, with sensitivity, off-resonance, relaxation and field nonuniformity retained; this does not promise diagnostic-quality coverage at every location. Lesson, Scan hand and Receiver controls reopen the existing lesson modes. Molecular translation/rotation illustrates slowed thermal motion, independently of the MRI carrier.

Native MR/VR uses contextual boundary suppression. Controller conventions remain unchanged. Physical headset performance, input and passthrough validation remain outstanding.

All reconstructed slices use the centre slice’s shared intensity reference rather than per-slice auto-brightening. The small field dots encode the gradient-induced local frequency offset; RF vectors show the local transverse B1 amplitude/direction on the same clock. The field direction markers are a visual convention, not a propagation simulation.

Controls are 18 mm spheres with 30 mm ray-hit targets at default scale, with viewer-facing captions. Receiver housings sit near x=-200 mm, y=-310 mm, outside the 250 mm outer magnet radius. The separate receive lead exits beyond the magnet before descending to the receiver.

The final lesson visits all slice echoes before its closing cue. The full stack is more expensive to compute than the previous nine-slice preview; it runs on the simulation worker while scene rendering remains separate. Initial acquisition and region/protocol changes can take minutes on the server; headset timing still needs measurement.

Render review: the completed 85-slice stack has weak outer-hand detail with the current finite magnet and local receiver. Full spatial coverage is implemented; a clear whole-hand reconstruction is not yet achieved. The final slice can be effectively empty when its spins fall outside excitation/receive sensitivity.

Validation: 60 numerical checks and 94 server runtime checks passed. Runtime probes reported zero managed allocation per playback frame and at most 120,072 submitted triangles per eye. These are server checks, not measured Quest frame rates. The moved region changed 243 of 288 tissue classes, and restoring its position restored all classes exactly.
