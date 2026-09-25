# Spatial RF topology — 0.9.2

The educational 1 T magnet and gradient windings are unchanged. The transmit birdcage is now 130 mm radius and 540 mm long, inside the gradient assembly. Its quadrature current pattern and B1+ are computed from its conductor paths. Main-magnet display loops are representative winding bundles of the existing pack model.

The independent 55 mm radius receive loop lies in x-z at y=50 mm, above the hand. Its normal is transverse to B0. The loop's unit-current field is calculated with the circular-loop Biot–Savart solution. The complex peak-envelope weighting is phase-calibrated to By+iBx in the exp(-iωt) convention. For Mx=A cos(ωt), My=-A sin(ωt), -d(M·B)/dt = ωA(By cos(ωt)+Bx sin(ωt)), verifying the phase/sign convention. The received signal uses this sensitivity, not the transmitter B1+ map. Receive amplitude variation therefore appears in the reconstructed image; no receive-bias correction is claimed.

This is a quasistatic educational model. It does not solve sample-dependent RF propagation, matching networks, active detuning electronics, dielectric effects, SAR or clinical hardware tolerances. Receiver electronics retain the explicit MaRCoS-derived example documented separately. The displayed transmit current uses the same slowed carrier phase as the magnetic moments and B1 glyphs; flow markers show conventional current direction.

Checks compare the transmit centre field against independent numerical line integration, the receive loop centre against μ0/(2R), receive falloff against the on-axis loop formula, and baked fields against direct evaluation. Existing Bloch, acquisition, demodulation and reconstruction tests also run.

References:
- Oxford FMRIB MRI hardware overview: https://users.fmrib.ox.ac.uk/~stuart/thesis/chapter_2/section2_6.html
- Vaidya et al., transmit/receive field patterns and reciprocity: https://pmc.ncbi.nlm.nih.gov/articles/PMC5082994/
- Existing MaRCoS reference and tissue/provenance documentation remains distributed.
