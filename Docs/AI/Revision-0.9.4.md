# 0.9.4 — playback and visible spin context

The 0.9.3 source and binaries remain available. This update adds playback controls and preserves volume context in the single-spin example. It does not rewrite the physics narration or replace the receiver architecture.

## Interaction

- Drag the single narration timeline with a controller trigger, hand pinch or desktop mouse. Dragging pauses; releasing resumes only if playback was running before the drag.
- The two small section triangles move to adjacent narrated sections, including chapter boundaries, while preserving pause. Chapter transport remains available.
- The rate chip cycles 1× → 2× → 4×. Desktop S changes rate; comma/period change section. Speed changes preserve the current position.
- Accelerated narration is generated with FFmpeg atempo so speech pitch is retained. Both existing male voices have 2× and 4× resource sets. The original lesson timeline remains authoritative; audio positions map back to it by the selected rate.
- The lesson/acquisition playhead and slowed spin/molecule displays advance with playback. Playback rate does not alter field strengths, RF frequencies, numerical time steps or acquired data.
- Input focus/tracking loss cancels an active scrub and leaves playback paused. XR chapter buttons are gated while scrubbing.
- The information card now contains a clickable GitHub source link.

## Visibility

The single-spin lesson retains all 288 sample glyphs as translucent context. The chosen moment stays at its actual lattice sample and has an open ring marker. The Phase toggle adds shorter, thinner transverse projections, with alpha distinct from the broader moment glyphs. These sizes are display scales.

The narration chevron now draws with casing through occluding geometry. This is an incremental visibility fix. The roadmap still calls for exact entity contours, better cue targets, fewer label collisions, and physical-headset contrast review.

The control strip was repositioned after screen-bounds and plot-overlap checks. Its timeline uses a visible thumb and section ticks.

## Reproducibility

- Fast interaction suite: ResonancePlaybackValidation.Run.
- Fast placement gate: ResonanceValidation.RunLayout.
- Full scene/lesson checks: ResonanceValidation.Run.
- Store capture: ResonanceStoreCapture.Run, actual runtime imagery in the native VR studio.
- Accelerated audio: python3 Tools/prepare-playback-audio.py.
- Public headless builds use graphics under xvfb. Avoid -nographics for Android builds; inspect the resulting Vulkan XR boot entries.

Actual validation and build reports are linked in docs/validation. No physical Quest or Store approval is claimed. A new numeric Meta App ID and release-channel test remain required for the Horizon build.
