# MRI rendered-element reference

This reference uses the Unity 0.9.3 project's actual runtime meshes, shaders, molecular models, moment lattice, plots, receiver components and control geometry. It contains no image-generated objects.

The exports run on Hetzner. They use a white camera background, an orthographic framing camera and darker external labels / transport icons for contrast. Colors on control chips remain light. The PNG contact sheet only crops outer whitespace, scales the individual captures, and adds headings.

Each component is captured at a useful lesson time. This is a component inventory; the panels in the sheet do not claim to depict one simultaneous scanner state. The app has no added backing panels. Exact cue IDs and simulation times are in manifest.txt.

- MRI-rendered-elements-white-0.9.3.png: 4800 x 3200 combined reference.
- MRI-rendered-elements-source-0.9.3.zip: eleven individual 1800 x 1300 source renders and composition metadata.
- Assets/Resonance/Editor/ResonanceElementReference.cs: repeatable editor-only capture utility.
- Tools/compose-rendered-elements.py: Pillow contact-sheet assembly on the server.

The reconstruction capture faithfully includes the current app's limited visible signal coverage; it is not replaced with an idealized hand image. Existing mesh, shader and layout limitations remain visible for design review. This export does not rebuild or replace a Quest or Windows app binary.

Run the editor capture under xvfb with graphics:
`Unity -batchmode -job-worker-count 2 -projectPath /home/triton/projects/ResonanceQuest-spatial -executeMethod ResonanceElementReference.Run -logFile validation/element-reference.log`
Then run `python3 Tools/compose-rendered-elements.py` from the project root.

