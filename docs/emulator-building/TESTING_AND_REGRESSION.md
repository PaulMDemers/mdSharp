# Testing And Regression

Emulator tests should cover small hardware facts and whole-program behavior. Neither is enough alone.

## Unit Tests

Unit tests are best for:

- CPU instruction flags and edge cases
- addressing modes
- bus mirroring
- register read/write side effects
- interrupt acceptance
- DMA trigger behavior
- palette and pixel conversion
- audio register interpretation
- cartridge save hardware
- save-state round trips

Keep CPU tests synthetic and focused. When an unsupported opcode is fixed because a game hit it, add the smallest instruction-level test that proves the behavior.

## Integration Tests

Integration tests should run the machine:

- reset from a synthetic ROM
- execute a small program
- write to video/audio/input hardware
- verify memory, registers, pixels, samples, or exceptions

These tests catch wiring mistakes between CPU, bus, and devices.

## ROM Sweeps

Compatibility sweeps are not formal correctness proofs, but they are excellent smoke tests.

Track:

- status bucket
- frame count
- performance
- CPU exception activity
- unhandled opcodes
- non-background pixel counts
- audio activity
- screenshot path
- media hash

Run short sweeps often and longer sweeps before releases.

## Input Movies

Input movies are one of the most valuable emulator debugging tools.

They should store:

- media hash
- initial save data when relevant
- per-frame input for all supported players
- frame count
- optional checkpoints

Use movies for gameplay bugs, menu paths, idle demos, split-screen modes, boss fights, and save-system checks. A report like "around frame 3400" should be enough to rerun the same sequence.

## Visual Checkpoints

Visual checkpoint tests compare rendered frames against baselines. They are useful for catching regressions in:

- raster effects
- sprite clipping
- palette changes
- scrolling
- DMA timing
- split-screen rendering
- special video modes

Use tolerance carefully. A high tolerance hides real bugs; a zero tolerance can create churn when harmless implementation details change. Keep checkpoint updates deliberate.

## Audio Regression

Audio tests need both structural and perceptual checks:

- register-write traces
- per-channel stems
- sample energy by frequency band
- aligned reference comparisons
- silence/noise checks at startup
- known sample playback checks

Audio rarely becomes accurate in one pass. The repeatable process matters more than any single metric.

## Save-State Tests

Save states should capture everything needed to resume deterministically:

- CPUs and coprocessors
- bus timing debt
- video state and snapshots
- audio chip envelopes and timers
- input adapter state
- cartridge save hardware
- pending interrupts
- DMA/FIFO state

Every new hardware subsystem should extend save-state tests before it is considered integrated.

