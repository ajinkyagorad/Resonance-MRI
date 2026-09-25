# Resonance Quest context
Verified root: /home/triton/projects/ResonanceQuest. New native MRI science app, isolated from MRI_Viewer, RobotAtlasQuest and the WebXR release. Unity 6000.5.5f1; built-in pipeline; Meta XR Core 205; OpenXR 1.17.1; XR Management 4.7; TMP/UGUI 2.5. Uses existing server Unity CLI, Android ARM64 IL2CPP, Vulkan and single-pass instanced stereo. Windows standalone module is also installed. No Unity Editor currently connected to the available MCP server; batch editor/build/render validation will run on the server.

Entry: generated Resonance scene with official OVRCameraRig. Runtime ownership split into physics/acquisition, anatomy, exhibit, input, panel and environment. Meshes and MRI input volume are prepared on server; acquisition reconstruction works in a background task; animation uses cached arrays and instanced draws. All major exhibits and the single instrument panel are separately grabbable. No external scene/prefab hand edits: an editor builder creates and validates required assets/references.

MR and VR use contextual boundary transitions. Do not declare whole-app BOUNDARYLESS_APP. MR launches transparently, requests boundary suppression; opaque VR waits for system boundary restoration. Official Touch Plus controllers retain native scale and animate physical controls. Help off by default; button-adjacent labels only when enabled. Tracking/focus/modality loss clears captures and requires release. System gestures reserved. No physical Quest is connected; simulated runtime and APK checks are not device certification.

The hand is an educational specimen, not a patient scan. Preserve source identities/licences and explicitly distinguish anatomical source geometry, derived soft-tissue envelope, illustrative MRI tissue parameters and reconstructed results. Native source and all builds remain on Hetzner.

0.2.0 adds ResonanceLesson (12-step teaching clock) and ResonanceMicroscope (dual-frame and spatial packet visualizations). Default is narrated step-and-hold; FULL SCAN preserves the original sequence. Volume rendering samples runtime scene depth, including stereo depth texture arrays. The guided fixtures cover two real I/Q mixers, RF cycle tip, panel fit and opaque-depth regression. No device measurement has been added.

0.5.0 adds permanent transport with source-based narration progress, queued parameter actions, white surfaces, side-on scanner, linked ResonanceFocus ROI/molecular magnification, receiver downconversion close-up, upright causal k-space texture and symbolic math glyphs. Meta Platform SDK 205 adds a Store-only entitlement gate; BuildStore requires a real App ID. Private sideload and release preview intentionally omit the ownership gate. The dedicated release keystore is outside the project. 708 runtime assertions include actual ray-selected controls and all twelve lessons.

0.6.0 adds ResonanceState (one per-frame acquisition state: segment, k(t), carrier, slice, operand; all views take moments from State.Moment), ResonanceTimeline (central sequence + operand plot), a 5x5x5 voxel-lattice tissue cube, per-phrase highlight specs (spins/coil/field/plot in narration-cues-source.json) with a runtime acceptance check, Gy-then-Gx-prephaser sequence order, one lesson/narration clock and a one-field-of-view layout. versionCode 7.

0.7.0 redesign: one colour per concept (ResonanceGeometry palette + ResonanceLegend colour key), dark theme only, ResonanceCoils (named coils, isolation, current arrows), scanner rotated so +z points right, 7-layer 3D tissue cube with image-voxel net M, one sentence per cue with span/rows timing and a callout, A/B/stick stepping controls. versionCode 8.

0.8.0 rebuild (branch rebuild/0.8.0 from 0c2537f; the 0.7.0 runtime was deleted):
- Assets/Resonance/Runtime/Sim is the physics: windings and Biot–Savart (Coils, BiotSavart), baked field tables (Fields, Resources/Resonance/FieldTables.bytes), tissue (Tissue, Resources/Anatomy/HandLabels.bytes), isochromat sets and the Bloch engine (Spins, Evaluator), the one-timeline lesson program (Sequence, Protocol), the reciprocity receiver and k-space (Acquisition), and the MaRCoS chain model (Marcos).
- Assets/Resonance/Runtime/Scene is the views: App, ScannerView, CubeView, Strip, Attention (referents and pointer), Lesson, Controls, Environment, StoreGate, and Look/Frames/MeshKit/Mats.
- Tools/simcore compiles Sim/*.cs under dotnet for the tests and the field bake.
- Lesson text lives in Docs/AI/narration-cues-source.json. Tools/generate-cued-narration.py turns it into Assets/Resources/Lesson and Narration.
- Validation: ResonanceValidation.Run via Tools/run-validation.sh.
- Specification: Docs/AI/Spec-0.8.0.md (§13 records the as-built deviations). versionCode 9.


## Approved spatial reference implementation — 2026-09-24

Branch `codex/spatial-mri-reference`, worktree `/home/triton/projects/ResonanceQuest-spatial`, based on clean d07e835. User supplied an exposed-coil passthrough reference and requested implementation. The older bans on silver/grey/orange and demands for opaque plot backings are superseded by this explicit reference. Existing simulation, sequence, receiver and controller architecture remain authoritative. Presentation changes use shared physical coordinates and the simulation clock.

Main magnet pack bounds remain the same; visible silver loops are representative winding bundles, not a claim of literal turn count. RF uses a 130 mm radius, 540 mm long transmit birdcage and a separate 55 mm radius receive surface loop at y=50 mm, normal to y. The independent receive sensitivity weights the live signal and acquisition. TissueDetail samples actual specimen classes within the selected ROI. Its fine fibre texture and the water cluster are schematic, not resolved anatomical microscopy. Proton arrows illustrate moments/ensemble behaviour; the decorative literal spinning rings are removed.

Validation is performed with headless server Unity 6000.5.5f1. The laptop is used only for small source reads and inspecting downloaded render images. Physical Quest validation and Horizon release credentials remain separate requirements.

## 0.9.2 inspection controls
6x6x8 net-moment lattice, optional microscopic directions, separate tissue orientation with Sync, cached field overlays and full specimen z coverage. SpatialTools owns mini controls and two instanced field draws. Controls resolves linked rotation dynamically. Field intensity is computed by superposition from cached per-ampere winding fields. Tissue material is optional. All plotting and reconstruction remains acquisition-timed.

## 0.9.3 foundations and lattice visibility
CubeView owns a persistent sample-location lattice and a dynamic conventional MeshRenderer for moments. Shader vertex tint supports this non-instanced path. TeachingSample provides explicitly ideal analytical Bloch/encoding examples. CueData.demo selects them on the existing narration clock. App temporarily centers/enlarges/angles the sample and hides unrelated views, restoring position/scale/orientation afterward. Only the working Phase toggle remains during ideal examples. Studio/review backgrounds are neutral; real Quest passthrough remains unchanged.
