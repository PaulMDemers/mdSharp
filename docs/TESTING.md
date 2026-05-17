# Testing And Regression

mdSharp uses a layered testing strategy:

- unit-style core tests in `tests/MdSharp.Tests`
- public hardware/test ROMs
- targeted retail game smoke tests
- compatibility dashboards
- input movie regression
- visual checkpoints
- audio regression against reference recordings

Public diagnostic ROMs are not committed. See [TEST_ROMS.md](TEST_ROMS.md) for source links used to recreate the local `TestRoms/` folder.

## Fast Verification

```powershell
dotnet build mdSharp.sln -c Release
dotnet test mdSharp.sln -c Release --no-build
```

Run this before and after focused emulator changes.

## Core Test Harness

`tests/MdSharp.Tests` is a dependency-free console harness. It covers CPU behavior, VDP behavior, cartridge save hardware, input movies, save states, PSG, YM2612, Z80, and targeted regression cases.

When fixing a bug, add a test when the behavior can be isolated without a full retail ROM.

Good test candidates:

- opcode semantics
- flags
- exception stack frames
- bus mirroring
- cartridge save behavior
- VDP register and DMA behavior
- sprite/plane/window rendering rules
- PSG/YM register semantics
- save-state round trips
- input movie serialization

## Compatibility Dashboard

Use `--compat` for broad ROM health:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --compat roms render-output\compat 300 300000 --screenshots
```

Review:

- exceptions
- blank or mostly blank frames
- stuck PCs
- low or missing audio activity
- suspicious sprite/plane counts
- final screenshots

`--resume` is useful for large folders.

When a broad sweep reports near-blank final frames, classify them with later follow-up frames:

```powershell
powershell -ExecutionPolicy Bypass -File tools\classify-near-blank.ps1 -CompatibilityCsv render-output\compat\compatibility.csv -RomFolder roms -OutputFolder render-output\near-blank -Frames "3000,6001,9000"
```

This rerenders only successful rows whose final sampled frame had at most 64 non-background pixels, then writes:

- `near-blank-classification.csv`
- `near-blank-classification.md`
- follow-up screenshots under `screenshots/`

Rows that become visible at a later frame are usually slow boots or transition timing. Rows that remain blank after all follow-up frames are stronger compatibility suspects.

## Input Movie Regression

Input movies are the preferred way to capture a human-observed issue.

Desktop workflow:

1. Load the ROM.
2. Use `Emulation -> Start Input Recording`.
3. Drive to the broken scene.
4. Stop recording and save the `.mdmovie`.
5. Re-run with `--movie-regress` or `--movie-checkpoints`.

CLI:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --movie-regress movies roms render-output\movie-regress 300000
```

Input movies store ROM hashes and optional initial SRAM snapshots, which helps make frame numbers repeatable.

## Visual Checkpoints

Visual checkpoints compare rendered frames against stored hashes.

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --visual-checkpoints roms render-output\visual 300000
```

Only update baselines when the new output has been reviewed:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --visual-checkpoints roms render-output\visual 300000 --update-baseline
```

## Performance Profiling

Use the perf suite to identify bottlenecks across representative ROMs:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --perf-suite roms render-output\perf 600 300000 --frame-profile
```

`--frame-profile` adds CPU and render substage columns. Treat those numbers as attribution data, not final speed data, because the timing probes add overhead.

## Audio Regression

Audio regression renders a set of known-sensitive cases and optionally compares against local reference tracks.

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --audio-regression roms render-output\audio-regression "Sonic the Hedgehog-Green Hill Zone Theme.mp3" 300000
```

Outputs include:

- WAV renders
- YM energy CSVs for Sonic checkpoints
- stem reports
- reference comparison reports
- a Markdown summary with RMS, peak, brightness, band deltas, and near-clipping counts

Reference audio is intentionally local-only and should not be committed.

## Suggested Pre-PR Check

For most emulator changes:

```powershell
dotnet build mdSharp.sln -c Release
dotnet test mdSharp.sln -c Release --no-build
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --compat roms render-output\compat-pr 300 300000 --screenshots --resume
```

For audio changes, also run:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --audio-regression roms render-output\audio-pr "Sonic the Hedgehog-Green Hill Zone Theme.mp3" 300000
```

For video timing or game-specific fixes, add or update movie checkpoints for the affected scene.

## Release Gate

For a broader pre-release pass, use:

```powershell
powershell -ExecutionPolicy Bypass -File tools\run-release-gate.ps1
```

The release gate combines build, tests, Virtua Racing SVP invariants, visual checkpoints, performance profiling, compatibility, near-blank classification, optional audio regression, and optional movie regression. It also writes `release-gate-summary.md` under the output folder with links to each generated report.
