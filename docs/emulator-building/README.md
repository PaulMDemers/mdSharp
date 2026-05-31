# Emulator Building Guide

This guide captures the transferable engineering lessons from building mdSharp. It is written for future emulator projects, including projects targeting different consoles or computer systems.

The details of every machine differ, but the work tends to rhyme: build a deterministic core, make defects reproducible, compare against trusted references, and let compatibility work reveal missing hardware behavior.

## Guide Map

- [Process](PROCESS.md): how to sequence the project from research to playable compatibility.
- [Tooling](TOOLING.md): CLI tools, trace output, dashboards, screenshots, audio dumps, and reference capture.
- [Debugging](DEBUGGING.md): practical methods for turning vague reports into hardware-shaped fixes.
- [Testing And Regression](TESTING_AND_REGRESSION.md): unit tests, ROM sweeps, movies, save states, and baseline checks.
- [Compatibility Triage](COMPATIBILITY_TRIAGE.md): how to pick the next fix and avoid game-specific patches.
- [Audio And Video Accuracy](AUDIO_AND_VIDEO_ACCURACY.md): how to converge on visual and sound quality.
- [Project Hygiene](PROJECT_HYGIENE.md): documentation, release prep, legal boundaries, and repository structure.

## Core Principles

1. Model the hardware, not the game.
2. Preserve determinism from the first useful frame.
3. Build tooling before guessing.
4. Make every important bug replayable.
5. Prefer small, verifiable hardware facts over broad rewrites.
6. Keep generated artifacts and copyrighted inputs out of source control.
7. Treat reference emulators, manuals, traces, and recordings as evidence, not as code to copy.

## A Healthy Emulator Loop

```text
research -> implement a small slice -> add diagnostics -> run tests
         -> try real software -> capture failure -> trace hardware behavior
         -> fix shared behavior -> add regression -> repeat
```

This loop is intentionally repetitive. Most emulator progress comes from applying it carefully across CPUs, buses, video, audio, input, storage, and unusual cartridge hardware.

## What To Build First

For a new emulator, the highest-leverage early pieces are:

- A machine object that owns all hardware state.
- A pure core with no UI dependency.
- A cartridge/media loader with header diagnostics.
- A CPU interpreter with precise enough exceptions and flags to run boot code.
- A bus mapper with read/write observers.
- A frame runner that is deterministic even if timing is approximate.
- A CLI that can run, render, trace, and sweep software.
- A small dependency-free test harness.
- Save states early enough to speed debugging.
- Input recording early enough to make visual bugs repeatable.

The desktop UI can wait until the core and CLI can explain what is happening.

