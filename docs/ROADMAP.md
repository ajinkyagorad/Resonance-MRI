# Public improvement roadmap

The published 0.9.3 baseline preserves the current experience. These tasks record review feedback; unchecked work is planned.

## Attention and spatial explanation

- [ ] Replace the easily buried narration pointer with an object-bound contour / halo, depth-aware outline and short calm cue motion. Maintain contrast on light / dark backgrounds and through partial occlusion. No rapid flashing.
- [ ] Define cue targets at field segments, coil conductors, selected tissue groups and plot regions. Highlight the quantity named by each phrase, then release it; avoid repeatedly lighting unrelated coils.
- [ ] Keep scanner, tissue, molecular / spin close-up and plots synchronized to one physical time and one selection. Show x/y/z at the actual volume and preserve colors and direction across views.
- [ ] Place the relevant plot beside the active object before narration begins. Make newly introduced objects discoverable, movable and recoverable.
- [ ] Preserve the volumetric 6x6x8 grid at useful scale, including outside-band moments; show the hand ROI linked through tissue to microscopic representation.
- [ ] Improve side-view scanner inspection, coil cutaway visibility, winding current flow and cached field-line visibility. Inactive coils remain translucent context.
- [ ] Review four styles per rendered component. Maintain passthrough, depth cues, clear functions, consistent symbols and restrained lighting without dashboard backings.

## Physical causal chain

- [ ] Begin with intrinsic spin and magnetic moment, the magnetic interaction energy and Larmor precession. Separate a quantum spin expectation from classical net magnetization.
- [ ] Show RF coil current creating transverse B1 throughout the selected region and explain resonant tipping in laboratory / rotating frames.
- [ ] Connect coherent transverse magnetization to linked magnetic flux and coil voltage, including receive sensitivity and the sum over the excited sample.
- [ ] Explain T1 energy exchange and T2 coherence loss before tissue contrast; separate susceptibility / chemical shift and T2* from RF dielectric loading.
- [ ] Distinguish real carrier voltage, analog gain / filtering / protection, sampled voltage, digital downconversion, I/Q, decimation and acquisition gating.
- [ ] Audit exact receiver numeric parameters against the chosen hardware / firmware revision. The MaRCoS-inspired teaching chain is an example architecture.

## Encoding, reconstruction and experimentation

- [ ] Use two points at the same x with different y to show the unresolved sum from one readout, changed relative phase under repeated Gy areas, then reconstruction.
- [ ] Explain Gx as frequency encoding across the whole excited slice; show Gy as phase encoding and k-space coordinates as accumulated gradient areas.
- [ ] Display only acquired k-space entries; distinguish a k-space row from an anatomical row. Show contributions from the whole slice.
- [ ] Keep RF bandwidth, Gz strength, slice position, slice thickness, slice spacing and x/y resolution distinct and inspectable.
- [ ] Improve full-hand reconstruction coverage / contrast and voxel correspondence before presenting it as a completed anatomy view.
- [ ] Add a scoped free-experiment mode for sample, one slice or volume, field / gradients and sequence; parameter controls must not unexpectedly restart narration.

## Interaction, release and documentation

- [ ] Make narration progress and pause reliable; use one slim progress indicator, standard speaker/mute symbols, voice selection and persistent discoverable controls.
- [ ] Verify theme contrast and room-light stability, compact button ergonomics, grip / ray manipulation and arrangement recovery.
- [ ] Add a source-repository link in the existing in-app information area after the initial source publication.
- [ ] Run physical Quest checks for readability, selection, boundary behavior, controller alignment and sustained frame rate.
- [ ] Complete Horizon App ID, release signing, privacy / asset declarations and device validation.
- [ ] Keep transcript, primary-source references, generated concepts and implemented screenshots clearly versioned.

Each task needs an actual render or device recording at the affected cue and a focused numerical / interaction check when behavior changes. No new teaching content is implied to be implemented by this roadmap.

