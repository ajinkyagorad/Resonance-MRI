# Resonance MRI 0.9.1 — spatial reference preview

Implements the supplied exposed-coil passthrough reference in the existing native Unity app. Main magnet packs are shown as silver winding bundles. Gradient colours match their spatial axes; RF is coral and B0 is light green. The scanner is presented more nearly side-on. Axes originate at the isocentre and the magnified tissue centre.

The tissue crop samples the same anatomical class volume as the simulation and follows the same ROI. Transparent fibre geometry is illustrative microstructure. The selected z slab clips to that volume. Water molecules and the selected hydrogen moment occupy real 3D space; the lab and rotating views keep the scanner orientation. Decorative spinning rings were removed. The time/I/Q signal and measured k-space remain driven by the acquisition clock.

Opaque backings were removed from plots, tissue, microscope and narration. Labels use SDF outlines/shadows. The lesson starts paused with an explicit play action. A standard speaker/mute icon changes audibility without restarting or pausing the simulation. Desktop camera framing now includes the full layout. Both narration voices were regenerated for changed colour names.

Existing physics remains the 1 T educational extremity model with a wide transmit birdcage and separate surface receiver. The surface loop lies in x-z, normal to y, so it detects transverse magnetization. Geometry is simplified and microscopic scale changes are schematic. The numerical simulator is an educational model, not a hardware specification.

Validation evidence is in validation/sim-report.json, runtime-report.json, apk-report.json and build-*.json. Physical Quest testing remains required. This is a private preview, not a Horizon Store submission; the user's Meta App ID is still outstanding.

Server validation: 58 numerical checks and 88 Unity runtime checks passed, with zero application errors. Headless-editor startup/plugin messages are recorded separately from application errors. Generated images are server renders composited over a room photograph, not Quest captures.

Native outputs: Quest Android and Windows builds succeeded with zero build errors. APK verification passed all 53 checks. Versioned packages, notes and reports are in `/home/triton/builds/resonance-quest`; rendered stage images are in `/home/triton/builds/shots/resonance-quest`. Android build has seven warnings and Windows one (including an unused presentation field); no physical device execution was performed.
