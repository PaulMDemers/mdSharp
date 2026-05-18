# Roadmap

This roadmap is a planning document, not a promise of exact release order.

## Current Status

mdSharp can boot and play a growing set of Sega Genesis/Mega Drive games. The emulator has working CPU, bus, VDP, input, save, audio, desktop UI, and regression tooling, but still has known accuracy gaps.

The frame scheduler now runs fixed active-display and HBlank CPU phases per scanline, charges DMA debt against those phases, and treats the instruction budget as a frame-level safety guard. Shared bus timing has started moving out of approximations too: Z80 bus requests now have a delayed grant, VDP FIFO-full writes can stall the 68000, 68000 peripheral accesses accrue wait-cycle debt, and accepted 68000 VDP interrupts acknowledge their pending flags. More exact DMA/FIFO edge timing and interrupt edge timing remain future accuracy work.

## Short-Term Priorities

- Improve YM2612 envelope/output accuracy.
- Add more audio references, especially Streets of Rage.
- Continue compatibility sweeps against broad ROM folders.
- Turn user-visible bugs into input movies or visual checkpoints.
- Expand cartridge hardware coverage.
- Harden Z80 and 68000 edge cases with more tests.
- Profile and optimize hot paths in Release builds.

## Medium-Term Priorities

- Continue tightening cycle/event scheduling, especially exact per-device bus waits, DMA/FIFO stalls, and interrupt edge timing.
- Improve PAL behavior and region handling.
- Harden six-button controller edge cases and add more controller regression tests.
- Expand unusual input hardware beyond J-Cart, Sega Team Player, EA 4-Way Play, and approximate light-gun support.
- Improve unsupported-hardware diagnostics.
- Build a curated public regression set using redistributable test ROMs and local-only movie metadata when possible.
- Continue improving automated release packaging and release-gate coverage.

## Known Gaps

- Not cycle-perfect.
- YM2612 is practical and improving, not bit-perfect.
- Some cartridge mappers and special hardware beyond the supported SVP path are incomplete.
- Some specialty input behavior remains incomplete, especially exact light-gun timing and calibration.
- Compatibility data is still developer-local rather than published as a maintained matrix.

## Release Goals

Before each public tagged release:

- Keep the MIT license, README, docs, and changelog current.
- Run the repository hygiene check so local ROMs, reference audio, generated output, saves, and package artifacts remain untracked.
- Run the release gate and review generated compatibility, visual, audio, movie, and performance reports.
- Verify README and docs against a clean clone when release packaging changes.
- Publish a Windows desktop build.
- Include current compatibility expectations and known issues in release notes.
