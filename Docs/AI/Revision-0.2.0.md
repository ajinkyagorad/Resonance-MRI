# MRI teaching revision 0.2.0
Preserve the native hand, scanner, reconstructed 3D volume, acquisition, desktop and MR/VR interactions.
Replace obstructing solid specimen/result shelves with open cradles. Increase control lettering and round housings.
The default guided lesson runs one short step then holds; NEXT proceeds. STEP/AUTO enables continuous progression; FULL SCAN preserves the original sequence.
Twelve steps connect bound hydrogen-1 and intrinsic spin, static B0, one circular RF cycle, cumulative resonant tip, finite-band slice, slice rephasing, Gy phase, Gx readout, physical I/Q detection, X ambiguity, ky sampling and the 3D result.
The dual-frame microscope shows a magnetic moment/ensemble expectation, not a classical proton surface. The RF carrier is slowed independently of the envelope; one-cycle tip uses actual B1/B0. Two quadrature drives produce circular B1; a single linear RF drive oscillates and can be decomposed into opposite rotating components.
Coils are schematic: static main magnet, three gradient winding systems and one switched RF transmit/receive resonator. Permanent and resistive/superconducting B0 sources are distinguished in the coils page. Separate receive arrays also exist.
I and Q are real demodulated voltages; the complex notation stores both. Product detection mixes a real voltage with cosine/sine references, then low-pass filters. No imaginary physical axis is implied.
Gy changes precession frequency only while on; accumulated phase persists. Gx changes precession frequency during readout. Both gradients perturb longitudinal Bz, not the propagation direction of a radio wave.
Acceptance: readable unclipped enlarged native controls; no broad opaque specimen/result shelf; step holds/next/auto/replay; synchronized coil/field/packet/waveforms; independently correct RF cycle and quadrature math; retained inverse Fourier and native controller/boundary behavior; server captures and both builds.
Hardware validation remains outstanding: stereo comfort/readability, focus handover, passthrough/boundary and sustained Quest frame rate.
Primary teaching references:
https://web.stanford.edu/class/rad229/Notes.html (sections 1B, 1C, 2A-D)
https://www.cis.rit.edu/htbooks/mri/chap-9/chap-9.htm (magnet, RF coils and RF detector)

Build investigation: the first Windows build returned Succeeded with one OpenXR post-build IOException because RuntimeActionBindings.json already existed in the reused output directory. The original log/report is preserved under validation/build-first-020. Windows now uses a version-specific generated output directory; the builder removes only the generated OpenXR binding destination before rebuilding. The final build must have zero reported errors. The desktop starting camera now includes all panel corners with a viewport margin, checked in the fixture.
