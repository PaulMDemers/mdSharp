# Tooling

Strong emulator tooling turns subjective reports into data. Build tools that answer narrow questions quickly.

## Core CLI Tools

A useful emulator CLI should support:

- run a single file for N frames or instructions
- render a specific frame to an image
- render a frame sequence
- scan media headers without executing
- run a compatibility folder sweep
- filter sweeps by filename
- resume sweeps without rerunning completed cases
- write CSV and HTML dashboards
- trace CPU instructions
- trace bus reads and writes
- trace video register and memory writes
- dump audio to WAV
- compare generated audio against a reference
- run input movies and visual checkpoints

These commands do not need to be beautiful at first. They need to be repeatable, scriptable, and documented.

## Trace Design

Trace logs should be compact, structured, and easy to diff. CSV is often enough.

Useful fields include:

- frame number
- CPU PC, SR/flags, last opcode
- exception count and last exception
- interrupt pending and mask state
- bus address, size, value, source device
- video mode, display enable, scroll registers, palette writes
- framebuffer or tile memory write counts
- audio register writes and sample energy
- save hardware state
- media hash

For long traces, add filters. A 10 MB focused trace is better than a 2 GB full trace nobody reads.

## Compatibility Dashboards

Folder sweeps should produce machine-readable and human-readable output:

- CSV for sorting and filtering
- HTML for quick visual review
- screenshots for visible results
- summary counts by status bucket
- details for exceptions or unsupported hardware

Good buckets include:

- visible
- boots but dark
- display disabled
- framebuffer written but no palette
- audio activity only
- CPU exception
- unsupported hardware
- timeout or safety-budget stop

Buckets are triage hints, not final ratings.

## Reference Capture

Use references to compare behavior:

- known-good emulators
- hardware recordings when available
- public audio/video samples
- test ROM expected outputs
- disassembly of the target program around the failing code

For reference emulators, prefer command-line or deterministic modes when possible. If a GUI emulator is needed, script it only enough to produce a repeatable screenshot, audio file, or state.

Keep reference files local unless they are redistributable.

## Scratch Tools

Small temporary tools are often worth writing:

- dump RAM around a PC
- disassemble a memory range
- print stack frames
- compare two framebuffers
- count changed pixels
- extract register writes from a trace
- search for a constant in ROM or RAM

Scratch tools should stay outside release packages. If a scratch tool becomes part of the normal workflow, promote it into the CLI and document it.

## What To Avoid

- free-form logging with no frame number
- screenshots without ROM hash and frame number
- traces that omit the device that performed the access
- hidden local dependencies
- generated artifacts committed by accident
- tools that require manual clicks for every run

