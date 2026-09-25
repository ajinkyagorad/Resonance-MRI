# Resonance MRI — complete 3D film

The complete narrated lesson is rendered from the actual Unity 0.9.5 simulation, with a neutral background, camera shots focused on the named exhibit, English captions, and Nebulytic opening and closing graphics.

Duration: approximately 12 minutes 44 seconds. 1920 × 1080, 24 frames/second. The film covers all ten chapters and all 119 lesson cues. Its sequence includes single-spin and sample-grid examples, relaxation, RF excitation, receiver processing, gradient echoes, slice selection, readout, phase encoding, k-space, reconstruction and the simulated hand volume.

![Film cover from actual Unity scanner geometry](cover.jpg)

- [Transcript](transcript.md)
- [Timed English captions](captions.en.srt)
- [Chapter timestamps](chapters.txt)
- [App, downloads and Meta Horizon release updates](https://nebulytic.com/apps/resonance-mri/)
- [Physics and receiver references](../PHYSICS.md)

## What was filmed

Actual Linux Unity Editor scene rendering under xvfb; physical events are slowed or held for explanation. The coil geometry, moment lattice, received samples and reconstructed data come from the project. The display grid represents sampled tissue moments; its cells are not individual image voxels or a literal lattice of water molecules.

The model has an RF transmit birdcage and a separate receive loop. Its receiver uses one direct-sampling/digital-down-conversion architecture. The MaRCoS reference supports that architecture and 122.88 MS/s digitization; this project's final 32 ksample-pairs/s and filter ratios are model settings.

The current reconstruction's visible signal coverage and contrast remain limited. This is an educational preview of a computational acquisition. The film does not establish Quest performance, a real scan or Store approval.

## Film-only narration changes

[narration-overrides.json](narration-overrides.json) records the changed sentences; [audio/](audio/) contains their corresponding stock male narration and opening/closing narration.

The corrections qualify the scanner current and receiver rates as example parameters; distinguish ensemble coherence from a single proton's moment; explain finite slice-profile transition regions; specify slice rephasing by gradient area; define ky through gradient area and spatial-frequency units; and qualify finite-resolution voxel reconstruction and tissue contrast. The shipped app narration remains versioned separately.

See the primary sources linked in [PHYSICS.md](../PHYSICS.md): Cox's MRI notes, Stanford RAD229, and the MaRCoS paper (sections III-A, IV-C and V-C). The film follows the app's teaching sequence; outstanding visualization work remains in the public roadmap.

## Reproduction

The capture script is Editor-only. Copy ResonanceFilmCapture.cs into the project's Assets/Resonance/Editor/ directory and set RESONANCE_FILM_DIR to a writable scratch directory. Copy the narration override file there as overrides.json, changing each audio path to its absolute path. Capture-workspace commit: b511bbf (0.9.5 packaging update); public repository baseline: 3bc779a.

Use Unity 6000.5.5f1 with graphics under xvfb. Invoke ResonanceFilmCapture.Run first for still proofs, then with -filmFull for chapter capture. It creates a cue manifest and one MP4 per chapter, with completion markers for resuming. Only one Editor may use a project at a time.

The assembly and graphics scripts document production paths and can be adapted to another machine. They require FFmpeg, Python, Pillow/qrcode and Hyperframes 0.8.36. The intro/outro use the existing Nebulytic logo and the actual scanner-cover-source.png from the project's 0.9.4 render export. Preserve voice durations and source frame counts when editing; regenerate captions and chapter offsets from the same manifest.

## Credits

Original project code: MIT. Project-authored film documentation and renders: CC BY 4.0, subject to [third-party notices](../../THIRD_PARTY.md). Attribution: Resonance MRI contributors / Nebulytic.

Anatomy: modified BodyParts3D, © The Database Center for Life Science, CC BY 4.0. Narration: Kokoro-82M, stock voice am_michael (Apache 2.0). Hyperframes supplies the brand/end-card composition renderer. No third-party background music is used.
