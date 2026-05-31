# Audio And Video Accuracy

Audio and video accuracy require different workflows, but both benefit from references, snapshots, and narrow comparisons.

## Video Accuracy

Video systems often fail because state is sampled at the wrong time.

Important questions:

- Are registers captured per frame or per line?
- Can software change scroll, palette, display enable, or tile data mid-frame?
- Does DMA update memory immediately or over time?
- Are sprites evaluated from current memory or a line snapshot?
- Are priority and transparency rules correct?
- Are special modes region-dependent?
- Are blanking and border areas modeled enough for the target software?

Many mature fixes come from moving from end-of-frame rendering to line-aware rendering.

## Visual Debug Outputs

Useful video diagnostics:

- final composed screenshot
- individual planes/layers
- sprite-only view
- priority map
- palette dump
- tile/pattern memory dump
- per-line scroll table
- register timeline
- pixel difference image against a reference

Even if these tools are rough, they reduce guessing.

## Audio Accuracy

Audio work should separate three questions:

1. Did the program write the right registers at the right time?
2. Did the chip model interpret those registers correctly?
3. Did the mixer/output path preserve the result?

Missing notes can come from CPU timing, audio CPU bus access, timers, channel muting, envelope state, operator routing, or mixer balance. Distortion can come from output tables, clipping, DAC bias, interpolation, or filters.

## Audio References

Good references include:

- hardware recordings
- trusted emulator recordings
- VGM/VGZ files when available
- per-channel stem output from a reference tool
- known sample playback tests

Align audio before comparing it. A small offset can make a correct track look wrong.

## Audio Tools To Build

- WAV dump from emulator
- reference-vs-current comparison
- per-channel or per-voice stems
- register write trace
- frequency-band energy report
- silence and clipping detector
- startup-noise detector
- fixed-seed input movie audio render

The goal is not only to hear the problem. The goal is to identify which channel, register, or timing event caused it.

## Practical Accuracy Target

For most projects, "not noticeable to a player" is a useful milestone before bit perfection. Define levels:

- boots with sound
- recognizable music and effects
- no missing channels
- balanced mix
- no obvious pitch errors
- no startup pops or static
- close enough against reference recordings that casual users do not notice
- hardware-accurate edge cases

Move upward intentionally.

