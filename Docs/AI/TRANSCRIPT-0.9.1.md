# Narration transcript — spatial preview 0.9.1

## 1 · The scanner

1.1 This is an MRI scanner: it images inside the body with magnetism and radio waves.
1.2 These silver windings form the main magnet, carrying a steady current of 133 amperes.
1.3 Their field, B₀, runs along the bore at one tesla; these lines trace it.
1.4 These arrows give the directions: z, in violet, runs along the bore and the field.
1.5 y, in orange, points up; x, in cyan, points across the bore.
1.6 Inside the magnet sit three gradient coils; each makes the field change along one direction.
1.7 Each has its direction's colour: the x coils cyan, y orange, z violet.
1.8 The coral RF coil transmits; the smaller gold loop receives the tissue’s signal.
1.9 A cable carries the receive coil’s voltage into the sampling and filtering electronics.
1.10 A hand lies in the bore; the scanner will image it, slice by slice.

## 2 · What you will see

2.1 This small white box marks a block of the hand, five by five by ten millimetres.
2.2 Here the same block is magnified fifty times, turned exactly like the scanner.
2.3 Grab and turn either one; the other turns with it, so directions always match.
2.4 These arrows illustrate hydrogen magnetic moments in sampled regions of water-rich tissue and fat.
2.5 Heat leaves the spin population disordered, with only a small preference for the field.
2.6 About 3 in a million more point along the field; here that excess is exaggerated.
2.7 Colour shows each proton's phase: how far round its turn it is.
2.8 Brightness shows how far it has tipped: dim along the field, bright across it.
2.9 This magnifies one sampled moment; select another in the tissue to follow its response.
2.10 This circle carries the colours: each colour is one direction across the field.
2.11 This plot will show each coil's current against time: the pulse sequence.
2.12 This plot will show the signal measured by the receive loop.
2.13 And here the measurements will collect and become an image.

## 3 · Resonance

3.1 In this field, protons precess at 42.6 megahertz: the Larmor frequency.
3.2 Their turning is shown slowed here, to one turn every two seconds.
3.3 Now the RF coil's current makes a weak field, B₁, turning at that same frequency.
3.4 These coral arrows show B₁ filling the whole coil.
3.5 The second view turns with that field, and time runs ten thousand times slower.
3.6 Near resonance, the rotating drive steadily tips the tissue’s net magnetization.
3.7 The tipping adds up, though this field is 85 thousand times weaker than the magnet's.
3.8 Transverse magnetization is now strong, with matching phases across the excited tissue.

## 4 · Signal

4.1 In the scanner, the tipped protons sweep round 42.6 million times a second.
4.2 Changing magnetic flux induces a voltage in the receive loop.
4.3 The receiver samples that voltage directly, 122.88 million times a second.
4.4 It multiplies each sample by a cosine and a sine at the RF frequency.
4.5 Filters keep the slow part: 32 thousand number pairs a second.
4.6 Each pair is this arrow, I and Q: the tipped magnetization as the coil sees it.
4.7 Its colour is its phase, the same colours as the protons.
4.8 Now time runs a hundred times faster.
4.9 Each pair joins this line in the signal plot, drawn against time.
4.10 The signal shrinks: across the hand the field differs by millionths, so protons drift apart.
4.11 Each proton's tip fades, and its alignment along the field regrows.
4.12 Muscle's tipped part fades within tens of milliseconds.
4.13 Fat's fades more slowly, and fat's protons turn slightly slower, so their colour drifts.

## 5 · Gradient

5.1 After a pause, the protons are back along the field and tipped again.
5.2 These are the z gradient coils, in violet.
5.3 Their current appears in the pulse sequence, on the Gz row.
5.4 It makes the field grow along the bore: the lines brighten where it is stronger.
5.5 In the block, this violet arrow shows the field rising along z.
5.6 Each plane now precesses at its own frequency, so the colours twist along z.
5.7 When the current stops, every plane shares one frequency again, and the twist stays.
5.8 With phases fanned out, the protons add up to almost nothing: the signal vanishes.
5.9 Reversing the current unwinds the twist.
5.10 The colours line up again, and the signal returns: an echo.

## 6 · Slice

6.1 After another pause, the z coils carry current again.
6.2 Each plane's frequency now rises steadily along the bore.
6.3 With the z gradient active, this pulse’s centre frequency targets the middle plane.
6.4 Its field reaches the whole volume at once.
6.5 Only protons in a 1500-hertz band stay in step, so only their tipping adds up.
6.6 Elsewhere the field slips past the protons, and its pushes cancel: they stay dim.
6.7 The tipped protons form a slab 5 millimetres thick.
6.8 Its thickness is the pulse's bandwidth divided by how fast frequency changes along z.
6.9 Through the slab's thickness, their colours have drifted apart.
6.10 A reversed z pulse, half as long, brings them back in line.

## 7 · Readout

7.1 These are the x gradient coils, in cyan.
7.2 A short reversed pulse first winds the slab's colours along x.
7.3 Then the current flips, and the receiver starts sampling.
7.4 The field now rises along x, as the cyan arrow shows.
7.5 So protons at each x precess at their own rate, unwinding the twist.
7.6 Halfway, the twist is undone: one colour again, and the signal peaks.
7.7 Every sample is the sum over the whole slab at that instant.
7.8 Protons sharing an x but not a y turn alike, so this readout cannot separate them.
7.9 These samples fill one row of a table of measurements, called k-space.
7.10 Along the row, kx counts the turns of x twist at each sample.

## 8 · Phase

8.1 After a pause, the same slab is excited again.
8.2 This time the y gradient coils, in orange, carry a pulse first.
8.3 The field now rises along y.
8.4 Protons higher up precess faster, and the colours twist along y.
8.5 When the current stops, the twist stays.
8.6 Its number of turns, ky, is set by the pulse's strength times its duration.
8.7 The readout then runs as before.
8.8 Each sample now adds up the slab's protons with the y twist included.
8.9 These samples fill a different row of k-space.

## 9 · Image

9.1 The scanner repeats this, each time with a different y pulse.
9.2 Here each repetition is shown only at its echo.
9.3 More turns fill rows farther from the middle of k-space.
9.4 The image forms where the slab is, sharpening as rows arrive.
9.5 For each point, the samples are added again with that point's twists undone.
9.6 Only that point's own signal adds up; the rest cancels.
9.7 Rows with few turns carry the most signal; outer rows add the fine detail.

## 10 · Volume

10.1 Shifting the RF frequency moves the matching plane along the bore.
10.2 Each slab is excited and measured the same way.
10.3 Stacked, the slabs form a three-dimensional image of the hand.
10.4 Its brightness comes from each tissue's protons and how fast they relax.
10.5 Cortical bone and tendon lose their signal within a millisecond, so they stay dark.
10.6 Every sample, row and image here came from the protons you watched.

