# Working on Resonance MRI

Read README.md, docs/PHYSICS.md, docs/ROADMAP.md and the current revision notes first.

- Keep the simulation authoritative. Use one acquisition state and shared clock for physical views, plots and narration.
- Preserve physical axes and identities across scanner, hand ROI, slab, tissue lattice and molecular zoom.
- Treat spin glyphs as expectation / ensemble representations. Explain the representation in the lesson.
- Associate each narrated claim with the exact visible object or plot region. Maintain usable contrast over actual passthrough.
- Preserve current releases. Make small reviewable changes; update the transcript, references and evidence when the scientific explanation changes.
- Cite primary sources for physics, receiver architecture and numeric specifications. Distinguish chosen simulation parameters from instrument specifications.
- Use real Unity renders for implementation evidence. Label generated design concepts clearly.
- No browser automation is needed for this native project. Prefer a headless build server when available; never invent its credentials.
- Never commit credentials, signing keys, DevAgent settings, Library, package caches or private user feedback logs.
- Run only one Unity Editor per project. Numerical tests: dotnet run --project Tools/simcore/simcore.csproj -c Release -- test.
- Runtime validation: ResonanceValidation.Run with graphics under xvfb. Build entry points: ResonanceBuild.BuildAndroid / BuildWindows.
- For MR + VR, retain contextual boundary suppression and restore boundary behavior for opaque VR. Confirm platform behavior on device.
- Controller models and help follow actual current actions. System-reserved inputs remain reserved.
- Preserve asset provenance and bundled notices. Unity / Meta dependencies retain their own licenses.
- Do not claim headset performance, diagnostic accuracy or Store readiness from server renders.

