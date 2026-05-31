# Compatibility Triage

Compatibility work should be prioritized by shared impact, confidence, and testability.

## Pick The Next Fix

Good candidates:

- affects many games in a sweep bucket
- blocks a high-value benchmark title
- has a clear hardware explanation
- can be reproduced with a short trace or movie
- can be covered by a unit or integration test
- improves a subsystem known to be incomplete

Lower-priority candidates:

- affects one obscure scene with no repro
- requires guessing without reference material
- depends on a feature that is about to be redesigned
- would be solved by a game-specific hack

## Bucket-Based Workflow

Folder sweeps should guide the next investigation.

Example bucket meanings:

- `cpu-exception`: inspect unsupported opcode, invalid access, or exception vector behavior.
- `vdp-dark`: check display enable, boot wait loops, missing interrupts, or insufficient frame count.
- `framebuffer-no-palette`: check palette writes, display buffer swaps, or color mode.
- `visible-but-corrupt`: check DMA timing, tile/sprite snapshots, priority, or scroll state.
- `audio-silent`: check audio CPU startup, bus ownership, chip enable, or write timestamps.
- `input-stuck`: check controller protocol, latch timing, or UI mapping.

The bucket is a question, not the answer.

## Avoid Compatibility Whack-A-Mole

When a fix improves one game and breaks another, pause and identify the real hardware rule. It may require:

- more precise reset state
- per-device read visibility
- byte/word access distinctions
- timing instead of immediate side effects
- region-dependent behavior
- preserving pending latches until a real acknowledge event

Do not stack special cases until the underlying rule is known.

## Maintain A Benchmark Set

Keep a small, intentional set of benchmark software:

- one simple early title
- one timing-sensitive title
- one audio benchmark
- one raster-effects benchmark
- one save-hardware benchmark
- one input-hardware benchmark
- one unusual mapper or coprocessor title
- one public diagnostic or SDK sample

The benchmark set should be quick enough to run frequently and broad enough to catch mistakes.

## Record Known Limitations

Every partial subsystem should have a documented status:

- implemented behaviors
- known gaps
- diagnostic commands
- useful references
- benchmark titles
- current blockers

This prevents the team from rediscovering the same missing feature.

