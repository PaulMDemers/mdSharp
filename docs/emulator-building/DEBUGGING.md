# Debugging

Emulator debugging is mostly about reducing ambiguity. The question is rarely "why is this game broken?" It is usually "which hardware fact did this program rely on that the emulator does not yet model?"

## Convert Reports Into Reproducers

A good bug report becomes:

- ROM/media identity and hash
- region and configuration
- frame number or timestamp
- screenshot or audio sample
- input movie when interaction matters
- save state near the issue
- trace window around the first bad frame

If the bug is visual, capture the earliest incorrect frame. If the bug is audio, capture both emulator and reference audio with alignment points. If the bug is a crash, capture the first exception or unsupported opcode, not the final parked loop.

## Work Backward From The Symptom

Examples:

- Missing sprites can be CPU object logic, DMA timing, sprite table parsing, tile memory snapshots, priority, clipping, or display enable state.
- Wrong colors can be palette byte order, CRAM timing, shadow/highlight, direct color mode, or region-specific defaults.
- Frozen gameplay can be a CPU flag bug, interrupt vector bug, timer status bit, bus wait condition, or unmodeled peripheral latch.
- Distorted audio can be pitch math, envelope timing, operator routing, DAC level, sample rate conversion, or write timestamping.

Do not assume the subsystem suggested by the symptom is the subsystem at fault.

## Find The First Divergence

The first visible failure may happen long after the first incorrect hardware state. Useful ways to move earlier:

- compare register write sequences against a reference
- inspect the first frame where a line, sprite, or note differs
- trace only a suspected address range
- bisect frame counts
- save state before the failure and rerun with extra tracing
- compare CPU branch decisions around a loop
- log interrupt acceptance and clearing

The first divergence is usually easier to understand than the final symptom.

## Prefer Hardware-Shaped Fixes

Good fixes usually sound like:

- "This status bit clears on read."
- "This interrupt uses vector N but priority level M."
- "This DMA trigger write is also a normal data write."
- "This byte lane is mirrored on odd addresses."
- "This register is visible to CPU A but not CPU B."
- "This frame uses per-line state, not end-of-frame state."

Risky fixes sound like:

- "If this game is running, skip the wait."
- "Clamp this one sprite."
- "Force this title to use mode X."
- "Ignore this opcode result because it looks better."

Temporary probes are fine. They should not become compatibility policy.

## Use Disassembly As Context

When a CPU parks in a loop, inspect the surrounding instructions:

- What address is being polled?
- Is the branch waiting for zero, non-zero, carry, overflow, or a specific bit?
- Which device should change that value?
- Is an interrupt supposed to break the loop?
- Is the loop a real wait, an error handler, or a countdown?

This is often faster than adding broad logs.

## Debugging Hardware Add-Ons

Add-ons and coprocessors need extra care:

- establish reset and boot handoff behavior first
- separate host CPU view from coprocessor view
- trace communication registers by source device
- track interrupt level and vector separately
- expose local RAM, shared RAM, FIFOs, and DMA counters
- verify save states capture the new device state

Most add-on failures look like CPU bugs until the handoff and communication protocol is correct.

