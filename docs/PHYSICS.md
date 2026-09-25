# Physics and receiver publication audit

This page distinguishes the current implementation from improvements still needed in the lesson. It is the current entry point when older design notes disagree.

## Spin to received voltage

A hydrogen nucleus has intrinsic spin and a magnetic moment. Its magnetic interaction changes the quantum state; vector diagrams represent spin expectation or ensemble magnetization. In a static field, transverse magnetization precesses; resonant transverse RF excitation changes its orientation. T1 describes longitudinal recovery and T2 transverse coherence loss. These processes should be introduced before comparing tissues. See [Cox's MRI physics notes](https://afni.nimh.nih.gov/pub/dist/edu/2000_08_mri_background/Chap2.pdf).

The receive coil senses voltage from changing linked magnetic flux associated with the ensemble's transverse magnetization. Receive sensitivity weights contributions over the sample. This causal link needs a dedicated scene. The lesson should separate intrinsic angular momentum from a literal rotating charged sphere, and separate T1 energy exchange with the environment from induced RF detection.

## Spatial encoding

Gz during an RF pulse makes the excited slice depend on frequency bandwidth and local field. Gx during readout gives positions different frequencies, while a Gy pulse accumulates position-dependent phase. Repetitions with different Gy areas provide additional encodings. A sample is a weighted sum over the excited region. kx and ky describe spatial frequency, set by accumulated gradient areas; inverse reconstruction separates position contributions. See [Stanford RAD229 notes](https://web.stanford.edu/class/rad229/Notes.html), particularly MRI signal equations / k-space.

## What the hardware reference actually supports

The [MaRCoS paper, sections III-A, IV-C and V-C](https://arxiv.org/html/2208.01616v1) documents 16-bit receive digitization at 122.88 MS/s, FPGA quadrature NCO / mixing, a six-stage CIC and configurable decimation. Subsequent filtering / decimation can happen on the host; its GUI example uses sixfold oversampling. This validates the general digital receiver architecture.

The app's chain chooses CIC decimation 640 followed by FIR decimation 6: 122.88 MS/s → 192 kS/s → 32 kS/s. Those ratios and FIR implementation are project parameters, not a universal scanner specification. Final sample rate is distinct from useful filter passband. The current visual receiver blocks are schematic.

A fuller receiver view should show coil tuning / matching, transmit protection / decoupling appropriate to the selected coil architecture, analog amplification and filtering, ADC input voltage, digital processing and the boundary between FPGA and host. Component placement and bit widths need revision-specific hardware evidence.

## Historical discrepancies preserved with the baseline

- The current 0.9.3 scanner has a transmit birdcage and an independent surface receive loop. The older 0.8 receiver note says the birdcage performs both roles.
- The older note labels 32 kS/s as bandwidth; the public explanation should distinguish sample rate from usable passband.
- The older claim that MaRCoS internal word widths are unpublished needs replacement with a pinned firmware audit; open HDL exists.
- Current receiver tests compare this project's numerical chain with its direct-sample model. They do not validate a built instrument.
- Grid glyphs and water molecules are teaching representations at vastly enlarged scales. Field-line direction, moment phase, frame axes and labels must remain consistent when views rotate.
- The shown reconstruction has limited signal coverage. A complete slice-count check does not establish faithful whole-hand image quality.

The compiled 0.9.3 narration and physics behavior have not been rewritten for publication. These gaps are tracked in the [roadmap](ROADMAP.md). Figures are project renders; source papers are linked rather than copied.

