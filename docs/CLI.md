# CLI Reference

The CLI project is `src/MdSharp.App`.

Use Release builds for compatibility sweeps, audio regression, and performance-sensitive runs:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- <command>
```

## Basic Smoke Run

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- "roms\Sonic the Hedgehog (USA).md" 100000
```

Runs a ROM with a fixed instruction budget and prints emulator state.

## Render A Frame

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --render "roms\Sonic the Hedgehog (USA).md" render-output\sonic.ppm 600 300000
```

Arguments:

- ROM path
- output `.ppm`
- frame count
- instructions per frame

Common flags:

- `--trace-cpu`
- `--trace-vdp`

SVP-focused scripted renders also accept compatibility probe flags:

- `--svp-mld-z`: use the older MAME-style `mld` zero-flag behavior.
- `--svp-al-broad`: clear pending PMAC state on any `AL` read instead of dummy `AL` reads only.
- `--svp-al-mame`: route `AL` reads through the PM external bus behavior.
- `--svp-pmac-loose`: allow non-blind PMAC assignments.
- `--svp-write-rpl`: apply RPL modulo behavior to pointer writes.
- `--svp-mame-timing`: charge MAME-style extra SSP1601 cycles for immediate, indirect, branch, and program-memory operations.

## Render A Frame Sequence

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --render-sequence "roms\Sonic the Hedgehog 2 (USA) (Rev-B).md" render-output\sonic2-seq 1900 2050 5 300000
```

This writes periodic frames for inspecting transient graphics bugs.

## Compatibility Dashboard

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --compat roms render-output\compat-300 300 300000 --screenshots
```

Outputs:

- `compatibility.csv`
- `index.html`
- optional screenshots in `screenshots/`

Common options:

- `--screenshots`: write final-frame screenshots
- `--resume`: skip already completed cases
- `--filter <text>`: run a subset by ROM filename

Compatibility rows include cartridge diagnostics in the detail field when a ROM declares save hardware, expects bank switching, has suspicious header ranges, or needs known unsupported hardware.

## Post-Menu Compatibility

Use `--post-menu-compat` to run a manifest of scripted, later-frame compatibility probes after title/menu input:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --post-menu-compat docs\post-menu-compat.sample.json roms render-output\post-menu 300000
```

Outputs:

- `post-menu-compatibility.csv`
- `post-menu-compatibility.md`
- `index.html`
- screenshots under `screenshots/`

The manifest can select ROMs by filename substrings, choose a built-in script such as `none`, `start`, `repeat-start`, `sonic1-start`, `sonic3-start`, `streets`, or `virtua-racing-drive`, and set a target frame for each case.

## Cartridge Info

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --cart-info "roms\game.md"
```

Prints header metadata, save hardware, expected bank-switch register usage, warnings, and known unsupported hardware such as 32X requirements. SVP cartridges are reported as supported coprocessor cartridges.

Scan a full folder without running emulation:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --cart-scan roms render-output\cartridges.csv
```

The CSV is helpful before a broad compatibility run because it highlights save hardware, bank-switched games, known unsupported hardware, SVP cartridges, and cartridge input devices such as J-Cart extra controller ports.

## Compatibility Summary

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --compat-summary render-output\compat-300\compatibility.csv render-output\compat-300\summary.md
```

Summarizes an existing compatibility CSV.

## Compatibility Matrix Export

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --compat-export render-output\compat-300\compatibility.csv render-output\compat-300\published
```

Exports an existing compatibility CSV to:

- `compatibility-matrix.json`
- `compatibility-matrix.md`

The matrix assigns coarse ratings from the automated sweep data:

- `A`: visible frame, sprite activity, audio activity, no fallback rendering or CPU fault activity
- `B`: boots visibly but has suspicious sampled audio or sprite activity
- `C`: boots visibly with CPU fault-vector activity or fallback rendering
- `Boots`: run completed but sampled frames were near-blank
- `Broken`: emulator error or 68000 exception stopped the run
- `Unsupported Hardware`: cartridge declares unsupported hardware

For a public-safe artifact that redacts local ROM filenames and screenshot links:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --compat-export render-output\compat-300\compatibility.csv render-output\compat-300\public --public
```

## Input Movies

See [INPUT_MOVIES.md](INPUT_MOVIES.md) for the full recording, replay, checkpoint, and publishing workflow.

Print movie metadata:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --movie-info docs\assets\input-movies\sonic-green-hill-sample.mdmovie
```

Render a movie to a frame:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --movie-render "TestRoms\Sonic.md" docs\assets\input-movies\sonic-green-hill-sample.mdmovie render-output\sonic-green-hill-sample.ppm 3724 300000
```

Run a movie regression set:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --movie-regress docs\assets\input-movies TestRoms render-output\movie-regress 300000
```

Movie regression matches movies to ROMs by SHA-256, restores movie SRAM when present, runs each movie, and writes CSV/HTML output plus screenshots.

## Visual Checkpoints

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --visual-checkpoints roms render-output\visual-checkpoints 300000
```

Update visual baselines intentionally:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --visual-checkpoints roms render-output\visual-checkpoints 300000 --update-baseline
```

Movie checkpoints work similarly:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --movie-checkpoints movies roms render-output\movie-checkpoints 300000
```

## Audio

Dump raw game audio:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --audio "roms\Sonic the Hedgehog (USA).md" render-output\sonic.wav 600
```

Run the audio regression suite:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --audio-regression roms render-output\audio-regression "Sonic the Hedgehog-Green Hill Zone Theme.mp3" 300000
```

The suite auto-detects a Sonic title reference in the repo root when named like:

- `01 - Title Theme - Masato Nakamura.flac`
- `sonic-title.flac`
- `sonic-title.wav`
- `sonic-title.mp3`

It also auto-detects a Streets of Rage reference when an audio file in the repo root has `streets` in its filename, or is named like:

- `streets-title.flac`
- `streets-title.wav`
- `streets-title.mp3`
- `streets-of-rage-title.flac`
- `streets-intro.flac`

Compare any ROM render against any reference:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --audio-compare "roms\Streets of Rage (USA) (Rev-A).md" streets-title.flac render-output\streets-audio streets-title 900 300000 0
```

Arguments:

- ROM path
- reference audio
- output folder
- comparison ID
- frame count
- instructions per frame
- compare start frame

Use `compare-start-frame` to skip boot silence or sound effects before the target song begins.

## VGM

Render a VGM/VGZ:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --vgm-render music.vgm render-output\music.wav 60
```

Render stems:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --vgm-stems music.vgm render-output\vgm-stems 60
```

## Traces

Focused trace examples:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --z80-trace "roms\Sonic the Hedgehog (USA).md" render-output\z80.csv 120
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --audio-trace "roms\Sonic the Hedgehog (USA).md" render-output\audio.csv 300 300000
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --io-trace "roms\Castlevania - Bloodlines (USA).md" 0 600 300000
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --svp-trace "roms\Virtua Racing (USA).md" render-output\svp.csv virtua-racing-drive 7200 300000 4096 '$56,$104,$124,$12A' 7000
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --svp-pm-trace "roms\Virtua Racing (USA).md" render-output\svp-pm.csv virtua-racing-drive 7200 300000 20000 7000
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --svp-pointer-trace "roms\Virtua Racing (USA).md" render-output\svp-pointers.csv virtua-racing-drive 7200 300000 20000 '$E6,$EA,$104,$124' 7190
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --svp-write-history "roms\Virtua Racing (USA).md" render-output\svp-write-history.csv virtua-racing-drive '$46C3,$46E3' 7200 300000 48 16 7190
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --svp-bus-trace "roms\Virtua Racing (USA).md" render-output\svp-bus.csv virtua-racing-drive 7200 300000 20000 '$30FE02,$30FE04,$30FE06,$308D86' 7180
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --dma-word-trace "roms\Virtua Racing (USA).md" render-output\dma.csv virtua-racing-drive 7200 300000 65536 7000 '$300000'
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --vdp-plane-trace "roms\Virtua Racing (USA).md" render-output\planeA-y96.csv virtua-racing-drive 7200 300000 planeA 96 0 319 1
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --svp-vdp-correlate "roms\Virtua Racing (USA).md" render-output\svp-vdp-y96.csv virtua-racing-drive 7200 300000 planeA 96 0 319 1 6960
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --virtua-racing-layout-check "roms\Virtua Racing (USA).md" render-output\virtua-layout virtua-racing-drive 7200 300000 render-output\svp-research\svp_bsd\svp\imageformat.txt
```

`--svp-trace` captures selected SVP instruction PCs with before/after register snapshots. `--svp-pm-trace` captures PMAC/PM I/O reads and writes, including DRAM cell writes, modes, addresses, values, and pointer movement. `--svp-pointer-trace` captures SSP1601 internal RAM pointer operands, including pointer index, modifier, RAM address/value, post-operation pointer value, and indirect IRAM targets for `((r))` reads. `--svp-write-history` watches specific SVP DRAM word addresses and dumps the recent instruction window before each matching write. `--svp-bus-trace` captures 68k and DMA reads/writes against SVP-mapped external addresses, which helps correlate `$30FE02/$30FE04` handshakes with tile-buffer DMA. `--dma-word-trace` captures the words copied by 68k-to-VDP DMA with requested/effective source and destination addresses; pass a source prefix such as `$300000` to focus on SVP DRAM transfers. SVP-sourced VDP DMA reports both addresses because Virtua Racing's SVP buffers are observed one word behind the 68k command source on the DMA path. `--vdp-plane-trace` maps screen pixels to plane/window source coordinates, name-table entries, tile data addresses, and color nibbles. `--svp-vdp-correlate` combines the VDP pixel source, latest SVP-sourced DMA word, and latest SVP DRAM writer in one CSV. `--virtua-racing-layout-check` encodes notaz's documented Virtua Racing SVP DMA chunks and optionally compares the `$C000` name table against `imageformat.txt`; add `--fail-on-mismatch` to make the command exit nonzero when the captured DMA, final SVP DMA VRAM, or documented name-table invariants fail. Local MAME VRAM comparisons are written as supplemental diagnostics when available. In PowerShell, wrap arguments containing `$` in single quotes.

For a known-good Virtua Racing video reference, use MAME after placing the SVP internal ROM where MAME expects it:

```powershell
powershell -ExecutionPolicy Bypass -File tools\capture-mame-reference.ps1 -RomPath "roms\Virtua Racing (USA).md" -SnapshotFrame 7200
```

The helper checks for `svp.bin` and reports the required CRC/SHA1 plus the exact destination path when it is missing. If the file is elsewhere, pass `-SvpBinPath C:\path\to\svp.bin` and the helper will verify the SHA1 before staging it for MAME. It drives MAME with Lua using the same `virtua-racing-drive` timing and saves a PNG at `-SnapshotFrame`; pass `-RecordAvi` to record a video as well. Pass `-DumpMemory` to also write `virtua-racing-mame-svp-dram.bin`, `virtua-racing-mame-vdp-vram.bin`, `virtua-racing-mame-vdp-vsram.bin`, `virtua-racing-mame-vdp-regs.bin`, and a MAME device/memory inventory for reference comparisons. BlastEm is useful for many Genesis references, but the current Windows nightly does not enable SVP for this loose Virtua Racing ROM without additional mapper support.

Compare a reference PNG/BMP against mdSharp output:

```powershell
powershell -ExecutionPolicy Bypass -File tools\compare-reference-images.ps1 -ReferenceImage render-output\virtua-layout\mame-reference\virtua-racing-mame-frame.png -CurrentImage render-output\virtua-layout\virtua-racing-layout-frame.bmp -OutputFolder render-output\virtua-layout\reference-compare -ScaleReferenceToCurrent
```

The comparison helper uses Windows image codecs, so it can read MAME PNG snapshots and mdSharp BMP frames without extra tools. It writes a diff image, side-by-side image, and markdown report with pixel-difference metrics.

Game-specific trace and bench commands also exist for known-sensitive cases. They are less stable than the general commands above.

## Performance

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --perf-suite roms render-output\perf 600 300000 --frame-profile
```

Use Release builds for performance work. With `--frame-profile`, the CSV and Markdown reports include CPU substages (`M68k`, `Z80`, VDP scanline step, YM timer) and render substages:

- plane B
- plane A/window
- sprite rendering
- compositing
- render setup, which includes snapshots, palette, scroll, sprite gathering, borders, display fill, and direct-color paths

The profiling instrumentation is intentionally diagnostic and adds overhead; use non-profiled perf-suite runs for final speed comparisons.

Compare two perf-suite CSV files:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --perf-compare render-output\perf-before\perf-suite.csv render-output\perf-after\perf-suite.csv render-output\perf-compare.md
```

The compare report highlights FPS, render time, CPU time, and audio time deltas per ROM.

Filter a perf run to a smaller ROM subset:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --perf-suite roms render-output\perf-sonic 1200 300000 --filter "Sonic the Hedgehog"
```
