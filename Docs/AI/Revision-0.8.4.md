# Resonance MRI 0.8.4: fixes from the 0.8.3 headset review

This revision answers USER-REVIEW-0.8.3, point by point. The 0.8.x simulation core (real windings, fields by Biot–Savart, Bloch isochromats, the receiver, k-space and the images) is unchanged except for the shape of the proton block. All checks and images come from the build server. The review renders are composited over a bright room photograph, exactly as the headset composites the eye buffer over passthrough.

## The review, point by point

**1. An introduction.** The lesson now opens with two steps before any physics:
- *The scanner:* what an MRI scanner is (it images the inside of the body with magnetism and radio waves), then its parts with the pointer on each:
  - the main magnet and its current;
  - the field B₀ and its lines;
  - the x, y and z directions;
  - the three gradient coils and their colours;
  - the RF coil;
  - the receiver;
  - the hand.
- *What you will see:* the block's outline in the scanner, the magnified block (and that both turn together), what each arrow is and what its colour and brightness mean, the close-up and its colour circle, the pulse sequence, the received signal, and the data and image panel.
- Each plot is named again when it is first used: the signal plot in 4.9, the sequence's Gz row in 5.3, and k-space when its first row appears in 7.9.

**2. Plots.**
- Every plot stands on a solid dark rounded panel with a rim; the rim lights when the narration names the plot.
- Traces are 4–5 mm wide in the coils' colours, and labels are about twice 0.8.3's size.
- k-space is a solid lit relief (height and colour from log |S|), as in 0.5.0. It is built one strip per measured row, so a single row is visible as soon as it is measured.
- The received signal is a 3-D line in time, I and Q, coloured by its phase like the protons, with its I and Q shadows and the RF voltage above.
- The image and the 3-D stack use a violet-to-white map instead of grey.

**3. Colour, no grey.**
- The main magnet is solid cyan, with a cutaway facing the viewer so the coils inside show; its cut faces read as sections.
- The base, cradles and receiver blocks are a saturated blue.
- UI backings are dark navy; text, frames and outlines are white with a dark outline.
- No material, label or token in the dashboard is a mid grey, and a check (P5) enforces this.
- The VR studio is navy instead of graphite.

**4. Proton colours.**
- Each proton's hue is its precession phase: the hue turns once per turn, measured in the frame that turns with the RF.
- Its brightness is how far its isochromat is tipped: dim along the field, bright across it.
- At rest the phases are random, so the protons show every colour and are dim.
- After the pulse, the tipped band shares one bright colour.
- A gradient spreads the colours along its direction, and an echo brings them back to one colour.
- The close-up's rotating-frame circle carries the same colours: a proton's colour is the colour of the circle where its magnetization points.
- The signal arrow and the signal trace use the same colours, because the signal's phase is the protons' shared phase.

**5. Axes and B₀.**
- The axes use the standard colours: x red, y green, z blue.
- **Choice:** B₀ changed colour from green to cyan (the magnet, its field lines and every B₀ arrow and label), so green now means only y.
- Each gradient coil takes the colour of the axis along which it changes the field: Gx red, Gy green, Gz blue.
- The entity colours stay at least ΔE2000 27 apart, and none is gold, orange or yellow.

**6. Gradients in the block.** While a gradient runs, the block shows three things:
- a flat tapered arrow in the gradient's colour, inside the block along the edge nearest you, pointing toward the stronger field, with the Larmor frequency offset at both ends (for example −625 Hz to +625 Hz for the readout);
- the block's far walls glowing in that colour toward the stronger field;
- the protons' colours twisting along that direction: 1.25 turns along z for the Gz demonstration, as measured by check COLOUR.

The scanner still shows the same gradient as a brightness ramp along its field lines.

**7. Protons in the block, and where the block is.**
- The block is a 5 × 5 × 10 mm piece of the hand, shown with one uniform magnification (50×).
- Its twelve edges are white, and its far walls are dark with a millimetre grid, so the 100 protons stand out over any room.
- In the scanner, the block's outline sits in the hand at the true size and orientation, drawn over the skin and coils.
- Four zoom lines join that outline to the magnified block.
- The scanner and the block share one rotation, so the outline always stays aligned (check ROT).
- The default orientation turned from 60° to 50° about the vertical. The fingertips still point to your left (40° toward you), but the x axis no longer runs nearly along your line of sight, so x arrows and x twists read at 64 % of their length instead of 50 %.

**8. Narration.**
- 96 sentences over ten steps; the text was rewritten.
- Every sentence points at what it names.
- Every number it speaks is on screen at that moment:
  - the magnet's current and B₀;
  - f₀ in the close-up;
  - B₁ in microtesla;
  - the receiver's rates, shown only while each block is named;
  - the RF band;
  - the slab thickness;
  - kx and ky, only once k-space has a row.
- The subtitles show B₀ and B₁ while the voice says "B zero" and "B one".

**9. Rotation.**
- A grabbed item follows the controller's full orientation. For the scanner or the block, both turn together, each about its own centre, and the stick still adds a turn about the vertical.
- On the desktop, right-dragging an item turns it about the view's up and right axes, and Q and E roll it. Right-dragging empty space still orbits the view.

**10. Text size.**
- Scene labels are about twice their 0.8.3 size and carry a dark outline.
- The subtitles are 1.9 times larger and wrap onto two lines on a wider strip.
- Check TX measures every label's angular size from the default pose: the smallest is at least 0.6° (capital height), and the subtitles at least 1°.

## Build hygiene (found while preparing this release)

- The Meta XR SDK's `DevAgentBuildProcessor` writes this build server's network address and its AgentBridge access token into `Resources/DevAgentSettings.asset` before every build, even though the DevAgent is disabled. The APKs of 0.7.0, 0.8.0, 0.8.1 and 0.8.3 therefore carry the server's address, and 0.8.3 also carries that token.
- Current exposure is limited: nothing listens on the AgentBridge ports (48735/48736) and the feature is off.
- For 0.8.4, `ResonanceBuildSanitizer` runs after the SDK's processor and clears the address and tokens. `verify_apk.py` now fails if the server's addresses or the token stored in the Unity prefs appear in the packed data; the Windows build was checked the same way.
- The token lives in the user-wide Unity prefs (`Meta.XR.SDK.AI Agent Bridge.RemoteServer_AccessToken`), which other projects on this server share, so it was not rotated here.

## Rules kept

- Narration runs on by itself (A forward, B back, X pause), and subtitles are on by default.
- The UI is dark, with no legends.
- Passthrough is never dimmed: the eye layer is premultiplied, opaque objects cover only themselves, and the premultiplied glow and images write their own alpha.
- 72 Hz budget; about ten review renders per iteration.

## Limits

- No Quest was used; frame rate, text comfort and the new layout must be checked on the headset.
- The server measured about 1.7 shaded fragments per pixel with the opaque panels, against 0.4–0.5 in 0.8.3's mostly light-only scene. The panels are cheap unlit surfaces, but the GPU time needs the strip's **ms** readout on the device.
- Some protons are orange or yellow for moments. Their colours come from the whole hue circle, because the user asked for hue by phase; no fixed object uses those hues.
- The block's in-plane size (5 mm) keeps the readout's phase twist to about a turn and a quarter across it. With 100 protons that is 3–4 protons per turn, so the twist at the start and end of the readout looks mixed rather than a smooth rainbow.
- The image slabs in the bore are small and seen obliquely at the default orientation; the image panel shows the same image large.
