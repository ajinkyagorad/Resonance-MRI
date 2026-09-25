> Historical 0.8 receiver note. For the current 0.9.3 coil architecture and publication corrections, read [the physics audit](../../docs/PHYSICS.md). Numeric implementation choices here are not universal hardware specifications.

# Receiver and spatial encoding reference (0.8.0)

The app simulates a Cartesian, slice-selective 2-D gradient-echo acquisition. The birdcage both transmits and receives. The winding designs, protocol and tissue values are listed in Scanner-model.

## The received voltage

- The tipped magnetization of every isochromat turns at its own Larmor frequency and induces a voltage in the RF coil.
- By reciprocity, each isochromat contributes in proportion to its volume, its transverse magnetization and the coil's field at its position, conj(b₁). The signal therefore weights tissue by where the coil is most sensitive.
- The model adds thermal noise from the coil and the hand. The noise power is 4kTRB, with the coil and sample resistances scaled with frequency and a 0.5 dB preamplifier noise figure.

## Receiver chain (MaRCoS architecture)

This is the published MaRCoS open-source console (Negnevitsky et al., J. Magn. Reson. 2023; https://arxiv.org/abs/2208.01616):

1. A 16-bit ADC samples the coil voltage directly at 122.88 million samples per second.
2. A numerically controlled oscillator at the RF frequency multiplies each sample by a cosine and a sine.
3. A six-stage CIC filter decimates by 640, to 192 000 samples per second.
4. A FIR filter decimates by 6, to 32 000 samples per second, the receiver bandwidth.

- The app computes the filtered complex samples directly.
- The server tests also run a full model of the chain above on one readout (ADC quantisation, NCO, the CIC as its exact 3835-tap response, a 193-tap Hamming FIR, droop correction). It reproduces the direct samples within 0.02 % RMS of the echo peak.
- MaRCoS does not publish its internal word widths, so the chain model uses ideal arithmetic after the ADC.

## What one k-space sample is

- Each sample is one number pair: the sum over the whole excited slab of every isochromat's tipped magnetization, as the coil sees it at that instant.
- Gradients give each position its own phase. After a phase-encode pulse of area A_y and a readout time t:
  - ky = γ̄·A_y;
  - kx = γ̄·∫G_x dt.
- The sample equals S(kx, ky) = Σ ρ(x, y)·exp[−i2π(kx·x + ky·y)], with ρ including proton density, relaxation, coil sensitivity and the slice profile.
- Spins with the same x but different y turn alike during the readout. One readout therefore fills one row of k-space and cannot separate them. Repeating with different phase-encode areas fills the other rows.
- The inverse 2-D Fourier transform of the acquired rows gives the image. Rows near the centre carry most of the signal, and outer rows add fine detail.

## Checks run on the server

- kx at every sample equals (j − N/2)/FOV to 4 × 10⁻¹⁰ m⁻¹. The second repetition's ky is 125.000 m⁻¹ (row +16).
- The echo of the gradient-echo readout lies at the readout centre.
- Every row is acquired once, and the k-space energy peaks at the centre row.
- Full receiver-chain model versus direct samples: 0.019 % RMS.

References: [Stanford RAD229 notes](https://web.stanford.edu/class/rad229/Notes.html) · [RIT, The Basics of MRI, chapter 7](https://www.cis.rit.edu/htbooks/mri/chap-7/chap-7.htm). Diagrams, code and narration are authored for this app. Source figures are linked, not redistributed.
