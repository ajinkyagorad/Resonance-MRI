# Approved reference implementation

The user approved the attached passthrough image on 2026-09-24. Implement the visual hierarchy in Unity: exposed silver main-magnet windings, colour-matched encoding coils and axes, coral RF transmitter, light green static field, sampled tissue crop, freestanding molecular/moment view, time/I/Q signal and measured k-space relief. Remove the opaque plot and close-up backings.

Scientific corrections relative to the image: slice surfaces are perpendicular to physical z; axes originate at the scanner isocentre and tissue ROI centre; I and Q occupy independent axes from time; all physics views use the same right-handed physical frame converted through Frames.ToS. The main magnet is static. Field-line animation denotes direction. RF excitation uses the wider birdcage field; receive weighting uses the separate surface loop through reciprocity. Retain acquired-data timing, so k-space does not appear before sampling.

Preview limitations: winding bundles are simplified; water geometry and microscopic fibre detail are illustrative. Existing full simulation is a 1 T educational extremity model, not a hardware design for a whole-body scanner or real-time diagnostic hand scanner. No claim of medical functionality or physical headset validation is made.

New winding presentation, molecular meshes and fibre meshes are generated in project code; no additional third-party asset was imported. Existing BodyParts3D, Meta and Kokoro notices remain distributed.
