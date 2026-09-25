# Resonance MRI 0.8.1: fixes from the headset test of 0.8.0

0.8.0 was tested on a Quest and found to have four problems: darkened passthrough, a low frame rate, missing views, and content that was too dark and less clear than 0.7.0. Each point of that review is answered below. All checks and images are from the build server, and the frame rate on the headset still has to be confirmed there.

## 1. Passthrough darkened

**Cause.** Two things covered or dimmed the real room in MR:
1. The app applied a colour map to the passthrough feed: brightness −0.20 and saturation −0.35. 0.8.0 added this call; 0.7.0 never made it.
2. The glowing coils, field haze and hand glow wrote alpha into the eye buffer. The headset composites that buffer over passthrough as premultiplied light: room × (1 − alpha) + colour. Every glow therefore also blocked part of the room behind it, and several glow layers stacked up.

**Fix.**
- Passthrough is shown as captured: no colour map, no edge rendering, full opacity. The premultiplied eye layer is set explicitly.
- Glows and fields now add light and never write alpha.
- Faded objects become more transparent instead of darker.
- Equipment is a lit mid grey instead of near-black. No full-screen or large dark layer exists.

**Measured** on the eye buffer's alpha, same views and framing:
- The share of the screen where translucent layers dimmed the room fell from 7–9 % (dimming it by about 70 %) in 0.8.0 to 0.8 % (about 22 %, the hand's skin and the cube's tissue) in 0.8.1.
- The share where content adds pure light over the room rose from 0–2.5 % to about 8 %.

## 2. Low frame rate

**Cause.** Every frame, the main thread:
- re-simulated about 20 000 spins in double precision;
- rebuilt 4096 needle matrices with general-purpose rotations;
- matched the cue's spin set by string for every needle;
- re-uploaded the hand-glow 3-D texture and re-filled k-space with a fresh array (allocations);
- rebuilt the strip's text.
Foveated rendering was off.

**Fix.**
- The spins (cube and hand sets, coil EMF, glow data) are evaluated on a dedicated worker thread while the main thread renders. The views read a finished result one frame later (14 ms of real time, microseconds of physical time).
- Needle geometry is built directly, and not rebuilt when nothing changed; spin-set membership is cached; batching takes one pass.
- The glow uploads only when new, and k-space only when a sample arrives. Strip text changes only when its content does.
- Nothing allocates per frame (gated).
- On the headset, dynamic fixed-foveated rendering is set to high and the CPU and GPU levels to sustained high.

**Measured** (server editor, real-time playback, one x86 core shared with other jobs):
- Main thread: 0.9–1.2 ms per frame in the final run, of which the cube is 0.4–0.5 ms (load average 16–18). The run before the unchanged-geometry skip measured 2.3–2.7 ms at load 21–27. 0.8.0 spent about 4.5–5 ms on the same work, summed from probes of its parts, plus allocations.
- Spin worker: 0.9–1.7 ms, plus 1.3–2.3 ms on frames that include the hand, in parallel.
- GPU proxies: 1.6–1.8 shaded fragments per pixel, and 13–18 million raymarch samples per frame at the Quest 3 default eye resolution. Full numbers are in frame-cost.json.

**Not measured.** Quest 3 CPU and GPU milliseconds need the headset. The strip's **ms** icon (off by default) shows CPU ms, GPU ms and Hz live, and frame-timings.csv in the app's data folder records 30-second means.

## 3. Missing views

All are back, attached to their physical sources and visible together from the default head pose. None carries a legend, text, gold, or a floating unexplained plot; colours are the coils' own.
- **Tissue in the spin cube.** The 16 mm block shows its own tissue (muscle, fat and marrow, tendon, bone, skin) as translucent matter around the 4096 spins.
- **Microscope.** One water-rich cell of the cube is magnified to molecules above the cube, joined to that cell by a faint zoom cone. It shows:
  - 40 water molecules;
  - every proton's magnetic moment in its random direction, all turned together by the cell's own precession and tip;
  - the cell's simulated net magnetization, bold;
  - B₀ and, while RF is on, B₁.
- **Console.** It is cabled to the receiver on the plinth and holds:
  - the pulse sequence: RF, Gz, Gy and Gx currents and the ADC over time, with a "now" line. The time window follows the lesson's slowing, and strobed steps show the whole repetition;
  - the received signal, sample by sample as it arrives;
  - k-space, with the row being read;
  - the reconstructed image;
  - a 3-D display of the stacked slices, turned like the scanner.
- The narration introduces and points at each view: 84 sentences, with new ones at 2.3, 2.5, 4.9 and 5.3.

## 4. Too dark, less clear than 0.7.0

- Gradient coils and the birdcage are now solid, lit and emissive, so they stay crisp over any room. The magnet packs, fields, spins and bones are brighter.
- The narration strip is 12 % larger, with near-white text.
- Review images are now the eye buffer composited over a mid-grey room exactly as the headset composites it over passthrough, at a Quest-like wide view.
- Against 0.8.0 in the same framing:
  - the mean brightness of the content rose by about 40–60 %;
  - its 90th-percentile brightness rose by about 50–60 %;
  - edge sharpness rose in 7 of the 9 views present in both versions.
- Side-by-side sheets with the 0.7.0 renders are published with the release (v0.8.1-mr-*).

## 5. Show it properly

One field of view now holds all of it:
- the physical scanner and its fields;
- the spins in the hand's tissue;
- the molecules behind one cell;
- the sequence that drives the coils;
- the signal that comes back, k-space, and the image.
Each narrated sentence points at the part it describes, and the numbers still come from the simulation.

## Limits

- No Quest was used for this revision either. Headset frame rate, comfort, text size and the look over real passthrough need your check.
- The GPU numbers above are proxies, not milliseconds.
- The microscope uses the classical ensemble picture: random proton directions, turned coherently by the cell's rotation. The three-in-a-million excess along B₀ is far too small to see and is represented by the bold net arrow.
