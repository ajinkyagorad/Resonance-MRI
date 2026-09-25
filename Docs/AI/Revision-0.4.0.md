# 0.4.0 feedback and implementation record

- Light/dark switch is always available. Panel surfaces use fixed local bevel shading independent of room lights.
- Side-on, complete main windings, RF birdcage and Gx/Gy/Gz coils replace cropped white housings. Physical winding paths carry slowed current highlights. Main field lines have 65 samples and increased thickness. The XYZ origin is at the cylinder centre.
- COILS opens explicit winding isolation controls. Current values use the model's declared calibration. Spatial field/frequency labels sit clear of the bore; the scanner has no menu plaques.
- Tissue cube now owns a selectable collider and movable entity. Controller grip, hand pinch and desktop dragging use the shared movement path. ROI connections follow it. Arrange restores all entities and the default desktop view.
- The 3D time/I/Q curve is restored at inspection height beside the receiver. Only received samples appear. K-space retains its two spatial-frequency axes and has magnitude relief, a causal cursor and whole-slice wording.
- Every guided narration phrase has a target and exact audio-segment start/end times. One current region is emphasized. PREV FOCUS/NEXT FOCUS revisit phrases. RF selection now evolves through the actual Bloch pulse; repeated encodes animate RF/current events as data arrive.
- Two male narrators, Michael and George, support live switching at the same phrase/fraction. Mute is separate. The transport uses familiar symbols and hover help. One smooth narration progress bar replaces the two bars and numeric time readouts.
- Receiver labels identify the documented MaRCoS ADC/NCO/CIC architecture. Rates and hardware/model limits are in Receiver-reference.md. K-space coefficients are explicitly checked against direct phase-weighted whole-slice sums.

Carried forward: all twelve lessons, previous/next/play/pause/restart, reconstruction and cutaway, 29 anatomical bones and muscles, slice BW/current/B0/matrix controls, contextual MR/VR boundary handling, official animated Touch Plus controllers, optional help, focus/tracking release gates, Windows desktop controls, source attribution and the new-app Horizon preparation flow.

Server rendering and input fixtures validate the implementation. Physical Quest comfort, control alignment, frame rate, haptics and boundary response still require headset testing. The final Store build still needs the real Meta App ID and account/device validation.
