# Sega CD Support Plan

This document tracks mdSharp's Sega CD / Mega-CD implementation path. The goal is to build Sega CD support as a first-class add-on subsystem while reusing the existing Genesis 68000, VDP, controller, save-state, audio, CLI, and desktop infrastructure.

Sega CD is materially different from a cartridge mapper. It adds a second 68000 running at roughly 12.5 MHz, a BIOS ROM, PRG RAM, shared Word RAM, backup RAM, a CD controller/decoder path, a Ricoh RF5C164 PCM chip, CD-DA playback, CDD/CD drive command/status behavior, and a graphics ASIC for stamp/tile rotation and scaling into Word RAM.

## References

- [Mega-CD development manuals on Internet Archive](https://archive.org/details/mega-cd-dev-manuals)
- [Exodus MegaCD documentation index](https://techdocs.exodusemulator.com/Console/SegaMegaDrive/Documentation.html)
- [Mega-CD Hardware Manual](https://segaretro.org/images/archive/5/51/20190509115046%21Mega-CD_Hardware_Manual.pdf)
- [Mega-CD Software Development Manual](https://segaretro.org/images/archive/6/6f/20190509144929%21Mega-CD_Software_Development_Manual.pdf)
- [Sega Mega-CD technical specifications](https://segaretro.org/Sega_Mega-CD/Technical_specifications)
- [RetroDev Sega CD notes](https://www.retrodev.com/segacd.html)

Use the official hardware, BIOS, software, and disc-format manuals as the primary source when behavior conflicts with informal notes. The Sanyo LC8950/LC8951 documentation listed in the Exodus index is especially important for accurate CDC behavior.

## Local Files

Commercial games, BIOS files, CD images, save data, and reference captures must stay local and ignored.

Recommended local-only paths:

- `segacd_roms/` for `.cue`, `.bin`, `.iso`, `.chd`, or converted CD test images
- `Sega CD BIOS/` for regional BIOS files
- `render-output/segacd-*` for screenshots, traces, WAVs, and compatibility reports

Expected BIOS search names should be implemented in the desktop and CLI once the subsystem exists:

- `bios_CD_U.bin`, `Sega CD BIOS/US.bin`, or `Sega CD BIOS/Sega CD - Model 2 BIOS V2.00 (USA).bin`
- `bios_CD_E.bin`, `Sega CD BIOS/EU.bin`, or `Sega CD BIOS/Mega-CD - BIOS V2.00 (Europe).bin`
- `bios_CD_J.bin`, `Sega CD BIOS/JP.bin`, or `Sega CD BIOS/Mega-CD - BIOS V1.00 (Japan).bin`

Environment variable fallbacks:

- `MDSHARP_SEGACD_BIOS_US`
- `MDSHARP_SEGACD_BIOS_EU`
- `MDSHARP_SEGACD_BIOS_JP`

## High-Level Architecture

Add `MdSharp.Core.SegaCd` as a sibling to `ThirtyTwoX`:

```text
MegaDrive
  GenesisBus
    SegaCdDevice
      Sub M68k CPU
      PRG RAM
      Word RAM
      Backup RAM
      CDC / CDD / disc image
      PCM chip
      Graphics ASIC
```

The Genesis main CPU remains the display owner. Sega CD code can DMA, transform, or stream data into shared memory, but final Genesis display output still comes from the existing VDP.

## Hardware Blocks

### Cartridge/Disc Detection

Sega CD software is disc-based rather than cartridge-header based. Add a new media abstraction instead of bending `CartridgeImage` into a CD loader:

- `DiscImage` parses `.cue` sheets first, with `.iso` single-track support as a convenience path.
- Track metadata includes mode, pregap, sector size, file offset, LBA, track type, and audio/data distinction.
- `MegaDrive` gets an optional Sega CD attachment constructor/factory path that takes a BIOS image and `DiscImage`.
- Desktop open dialog should allow Sega CD images only when developer options enable Sega CD loading.

Prefer `.cue` support first. Many Sega CD games mix a data track with CD-DA tracks, and `.iso` alone loses the audio layout.

### BIOS Boot

First milestone should boot the regional BIOS to the CD player screen before attempting game boot.

Required pieces:

- Map Sega CD BIOS into the Genesis expansion area.
- Expose the Sega CD gate-array registers to the main 68000.
- Model reset, bus request, and sub-CPU control bits.
- Run the sub 68000 from BIOS vectors when released.
- Add enough CDD responses for the BIOS to query drive state, TOC, and disc presence.
- Render normal Genesis VDP output from BIOS code using the existing renderer.

BIOS boot is the first "green bar" milestone because it proves memory mapping, register visibility, main/sub CPU scheduling, and basic CD status are all connected.

### Main/Sub CPU Scheduling

The Sega CD sub CPU is another Motorola 68000. Reuse `M68kCpu` with a new bus implementation:

- `SegaCdSubBus` maps BIOS, PRG RAM, Word RAM, backup RAM, PCM registers/RAM, CDC, CDD, gate-array registers, and graphics ASIC registers.
- Main CPU and sub CPU must run in master-clock-coordinated slices.
- Start with scanline-sized scheduling, matching mdSharp's current frame loop.
- Add shared register interrupt latches between main and sub CPU.
- Track cycle debt for Word RAM arbitration, CDC DMA, PCM access, graphics ASIC activity, and PRG RAM access once functional behavior is stable.

Initial scheduling can be coarse. Compatibility-grade work will need exact enough interrupt, DMA, and Word RAM handoff timing for streaming games.

### Memory Map

Implement stateful RAM and register blocks:

- BIOS ROM: regional, local-only, read-only.
- PRG RAM: 512 KB, primarily sub CPU program/data RAM.
- Word RAM: 256 KB shared memory, with 2M and 1M/1M modes.
- Backup RAM: 8 KB internal persistent save RAM.
- Optional Backup RAM Cartridge: later support.
- Program/image header transfer behavior through BIOS services.

Word RAM is the center of many bugs. The implementation should explicitly model:

- main/sub ownership bits
- 2M mode whole-block handoff
- 1M/1M split mode
- cell-arranged image access
- dot-arranged image access
- DMA and graphics ASIC interactions

### CDD / CDC / Disc IO

Separate high-level drive commands from sector decoding:

- `SegaCdDrive` owns track table, current LBA, play/seek/pause/status state, and CD-DA playback position.
- `SegaCdCdd` exposes command/status nibbles used by BIOS/game code.
- `SegaCdCdc` models sector buffer, header/subheader/status, destination selection, DMA, and interrupt behavior.
- Data sectors should support at least 2048-byte ISO user data and 2352-byte raw-sector sources.
- Audio tracks feed CD-DA samples into the mixer with proper start/stop/loop behavior.

Milestones:

1. BIOS sees a disc and reads TOC.
2. BIOS reads the boot sector/header.
3. Game code loads into PRG RAM.
4. Streaming FMV/data games sustain CDC transfers without desync.

### PCM Audio

Add a Ricoh RF5C164-style PCM device under `MdSharp.Core.Audio` or `MdSharp.Core.SegaCd`:

- 8 channels
- 64 KB PCM RAM
- channel start address, loop address, envelope, pan, step/frequency, enable state
- odd-address sub-CPU register mapping
- frame-timestamped writes like PSG/YM2612
- mix into the existing stereo output pipeline alongside PSG, YM2612, 32X PWM, and CD-DA

Start practical and testable, then tune against BIOS sound tests, Sonic CD, Lunar, and Snatcher.

### CD-DA Audio

Implement CD audio separately from PCM:

- decode WAV/BIN audio tracks from `.cue`
- resample to mdSharp's output sample rate
- honor CDD play, pause, stop, scan, and track boundary behavior
- mix as a stereo source with configurable gain

For `.chd`, either add a managed reader later or shell out/convert through tooling. `.cue` plus track files is the first implementation target.

### Graphics ASIC

The Sega CD graphics block writes transformed stamp-map output into Word RAM; it does not replace the Genesis VDP.

Implement after BIOS boot and simple game startup:

- register block
- stamp map formats
- trace vector/table interpretation
- image buffer address and cell/dot arrangement
- busy/status timing
- interrupt/completion behavior

Targets:

- Sonic CD special-stage/intro effects
- Batman Returns driving stages
- Silpheed/Starblade-style transform usage later

### Backup RAM

Persist internal backup RAM through the desktop save folder:

- `.brm` or `.segacd.sav` sidecar keyed by BIOS region and disc identity
- format detection/export can come later
- include backup RAM in save states and input movie snapshots only when explicitly needed

## Implementation Milestones

### 1. Media And BIOS Loader

Deliverables:

- `DiscImage` parser for `.cue`, simple `.iso`, and `.chd` via optional `chdman` extraction cache
- CUE audio tracks backed by raw binary or 44.1 kHz stereo 16-bit PCM WAV files
- BIOS file discovery helpers
- cartridge/media diagnostics report Sega CD requirements
- CLI `--segacd-info <cue-iso-or-chd>`
- tests for CUE parsing, track offsets, and BIOS discovery

Exit criteria:

- CLI can identify data/audio tracks and selected BIOS region without booting.

### 2. Sega CD Device Shell

Deliverables:

- `SegaCdDevice`
- `SegaCdHardwareProfile`
- PRG RAM, Word RAM, backup RAM, register storage
- main-side expansion-bus register reads/writes
- sub-side bus skeleton
- save-state placeholders

Exit criteria:

- deterministic reset state, register tests, and no impact on normal cartridge games.

### 3. BIOS CD Player Boot

Deliverables:

- sub 68000 execution through `M68kCpu`
- main/sub reset and communication registers
- enough CDD status for no-disc and disc-present BIOS screens
- CLI render support for Sega CD BIOS frames
- desktop developer option for Sega CD image loading

Exit criteria:

- BIOS reaches visible CD player or "press start" style screen.

### 4. Disc Boot And Program Load

Deliverables:

- CDD TOC/read status
- CDC sector buffer and basic data-read path
- BIOS-recognized boot sector/header flow
- PRG RAM load path
- Word RAM transfer path used by boot code

Exit criteria:

- one simple Sega CD game reaches its initial publisher/title screen.

### 5. Word RAM Accuracy Pass

Deliverables:

- 2M mode handoff
- 1M/1M mode
- RET/DMNA/MODE semantics
- cell/dot arranged mapping
- ownership/status timing tests

Exit criteria:

- Sonic CD and one FMV-heavy title can repeatedly load scenes without Word RAM deadlock.

### 6. PCM And CD-DA

Deliverables:

- RF5C164 PCM implementation
- `.cue` audio-track decoding
- CD-DA mixer source
- audio trace/regression hooks

Exit criteria:

- BIOS CD player plays an audio track.
- Sonic CD title/gameplay has PSG/YM/PCM/CD-DA sources mixed without crackle or drift.

### 7. Graphics ASIC

Deliverables:

- stamp renderer
- image buffer writes into Word RAM
- busy/status behavior
- CLI trace and screenshot tests

Exit criteria:

- Sonic CD special-stage-like transform output is recognizable.

### 8. Compatibility Sweep

Deliverables:

- `--segacd-compat <folder> <output-folder> ...`
- screenshots, audio activity, CD activity, and BIOS/game phase classification
- focused manifests for Sonic CD, Lunar, Snatcher, Popful Mail, Batman Returns, Night Trap, and Ecco CD

Exit criteria:

- dashboard separates BIOS boot, disc boot, title screen, gameplay, and CD audio status.

### 9. Desktop Quality Pass

Deliverables:

- developer-gated Sega CD open support
- BIOS path diagnostics
- disc/track metadata in Diagnostics
- CD audio mute/volume setting if needed
- save path display for backup RAM

Exit criteria:

- desktop can open a CUE, pick a BIOS, boot, save backup RAM, and report useful support info.

### 10. Release Hardening

Deliverables:

- docs and release notes
- hygiene checks for CD images and BIOS files
- save-state version bump
- package validation
- public test-ROM links where legal

Exit criteria:

- Sega CD support can be described honestly as experimental, with specific known-good targets.

## First Compatibility Targets

Use lawful local copies only.

Suggested order:

1. BIOS with no disc
2. BIOS with audio CD
3. Sonic CD
4. Lunar: The Silver Star
5. Snatcher
6. Popful Mail
7. Ecco the Dolphin CD
8. Batman Returns
9. Night Trap or another FMV-heavy title
10. Silpheed

## Testing Strategy

Add unit tests before game-specific debugging:

- CUE parsing and track LBA math
- BIOS discovery and region selection
- gate-array register reset/read/write behavior
- Word RAM ownership transitions
- Word RAM 2M and 1M mappings
- backup RAM persistence
- CDC sector read addressing
- CDD command/status packet formatting
- PCM register/RAM writes and simple generated sample output
- save-state round trip for Sega CD state

Then add CLI diagnostics:

- `--segacd-info`
- `--segacd-trace-main`
- `--segacd-trace-sub`
- `--segacd-cdd-trace`
- `--segacd-cdc-trace`
- `--segacd-wordram-trace`
- `--segacd-pcm-trace`
- `--segacd-render`
- `--segacd-compat`

## Main Risks

- CDD/CDC timing: BIOS and streaming games may depend on status cadence and interrupt timing.
- Word RAM ownership: most visible lockups will likely be incorrect RET/DMNA/MODE behavior.
- Mixed media support: `.cue` layouts vary, and `.chd` support may need a separate reader or conversion flow.
- Audio drift: CD-DA, PCM, YM2612, PSG, and video frame timing all need one shared timing model.
- Graphics ASIC edge cases: stamp rendering is isolated but format-heavy.
- Region behavior: BIOS/game region mismatches need clear diagnostics.

## Practical First Pass

The first implementation batch should avoid FMV, PCM accuracy, and graphics ASIC complexity. The fastest useful path is:

1. Add `DiscImage` and BIOS discovery.
2. Add `SegaCdDevice` shell and register tests.
3. Map BIOS and boot to the CD player screen.
4. Add minimal CDD TOC/status responses.
5. Add sector reads sufficient for Sonic CD boot.
6. Add Word RAM 2M handoff.
7. Render Sonic CD title screen.

That gets the system from "planned" to "real," with the smallest number of moving parts.
