# Resonance MRI 0.9.3 — visible lattice and foundations

The volume now has persistent sample-location markers and depth rails. Moment arrows use one conventional dynamic MeshRenderer, avoiding exclusive dependence on indirect instanced submission. Sample locations remain visible when transverse or total net magnetization becomes small. Headset reproduction of the reported missing-grid issue is unavailable; this is a robust alternate path, not a claim of confirmed device diagnosis.

The second lesson begins with ideal, labeled Bloch examples on the same 6x6x8 grid: single-spin expectation vector, resonant RF rotation, a homogeneous ensemble, T1 recovery, T2 decay, two material responses, ideal slice-band excitation, Gx frequency encoding, Gy phase accumulation and retained phase, then two same-x/different-y samples separated by two independent phase encodings. These examples use the existing narration clock and pause/seek/voice controls. The scanner/acquired-data views are hidden during the ideal examples to prevent them being mistaken for their outputs. The anatomically sampled hand lesson follows afterward.

T1/T2 values in foundations are illustrative. The single-spin arrow describes a quantum-state expectation value; the volume arrows describe small-ensemble magnetization. The rectangular RF-band illustration is an ideal selection limit; the full acquisition uses the finite pulse/field model. Precession is deliberately slowed. T1 recovers longitudinal magnetization; T2 describes intrinsic transverse coherence decay. Static frequency offsets add dephasing (T2-star in an ensemble), independently of intrinsic T2. Permittivity/conductivity affect RF-field distribution and loading; chemical shielding and magnetic susceptibility affect local resonance, and molecular dynamics/fluctuating magnetic interactions influence relaxation.

For the two-position example A=0.8, B=0.3: S0=A+B=1.1; a second encoding yields S1=A-B=0.5. Recover A=(S0+S1)/2 and B=(S0-S1)/2. This is a normalized two-point discrete Fourier example. A conventional full image needs all required k-space encodings; each measurement is a sum over the excited slice, not an isolated physical y-line.

The photographic kitchen was only a server review composite; it has been removed from new renders. Desktop/VR uses a neutral blue-grey studio. Native MR continues to show the actual room through Quest passthrough. No new external assets were imported. The existing contextual boundary behavior and controller conventions remain.

Sources checked 2026-09-24:
- Robert W Cox, NIH AFNI, MRI physics (magnetization, excitation, relaxation and Fourier encoding): https://afni.nimh.nih.gov/pub/dist/edu/2000_08_mri_background/Chap2.pdf
- NIH FMRIF MRI basics: https://fmrif.nimh.nih.gov/images/1/17/2017_05_Vinai_20170612.pdf
- Ibrahim et al., coil/sample electrical properties and B1 inhomogeneity: https://pubmed.ncbi.nlm.nih.gov/11358660/
- Conductivity/permittivity imaging from B1 maps: https://pubmed.ncbi.nlm.nih.gov/23599691/

Physical Quest frame rate and visual validation remain outstanding. The full-hand reconstruction retains 0.9.2's weak outer-volume detail with the current coil/field model.

Visual review changes: the ideal sample occupies a larger central oblique view; its prior position, scale and orientation are restored afterward. Unrelated connector geometry and non-applicable controls are hidden during the examples. Zero-magnitude moments leave only their persistent location marker, avoiding flattened arrow end-caps. The summed normalized signal uses viewer-facing I/Q coordinates, independently of the physical sample axes.

Validation: 66 numerical checks and 95 server runtime checks passed, with zero application errors. Target-device confirmation of grid visibility, sustained frame rate and MR/VR behavior remains outstanding.

The two-position example uses A as its phase reference, factoring out the common phase shared by both contributions. Its signal amplitudes are normalized. Both native builds succeeded and all 53 APK checks passed.
