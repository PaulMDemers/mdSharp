# Release Checklist

Run this checklist before publishing the repository or cutting a binary release.

## Repository Hygiene

- Ensure no commercial ROMs are tracked.
- Ensure no copyrighted reference music is tracked.
- Ensure `render-output/`, `roms/`, `TestRoms/`, `movies/`, local audio references, `svp.bin`, `cfg/`, `nvram/`, `snap/`, `bin/`, and `obj/` are ignored.
- Remove temporary screenshots, traces, WAVs, and generated compatibility dashboards from the working tree.
- Confirm the MIT `LICENSE` file is present.
- Keep public test ROM binaries out of the public repo by default and link to their sources from [TEST_ROMS.md](TEST_ROMS.md).

## Build

```powershell
dotnet clean mdSharp.sln -c Release
dotnet build mdSharp.sln -c Release
dotnet test mdSharp.sln -c Release --no-build
```

## Release Gate Helper

The scripted release gate runs the standard high-signal checks and writes output under `render-output\release-gate` by default:

```powershell
powershell -ExecutionPolicy Bypass -File tools\run-release-gate.ps1
```

The gate runs:

- Release build
- core test harness
- Virtua Racing SVP layout gate with `--fail-on-mismatch`
- visual checkpoints
- performance suite with frame profiling
- compatibility sweep with screenshots and resume support
- near-blank compatibility follow-up at frames `3000`, `6001`, and `9000`
- audio regression when the Sonic Green Hill reference MP3 exists
- movie regression when a local `movies/` folder exists
- `release-gate-summary.md` with links to generated reports

Common variants:

```powershell
powershell -ExecutionPolicy Bypass -File tools\run-release-gate.ps1 -SkipAudio
powershell -ExecutionPolicy Bypass -File tools\run-release-gate.ps1 -SkipPerf
powershell -ExecutionPolicy Bypass -File tools\run-release-gate.ps1 -SkipSvp
powershell -ExecutionPolicy Bypass -File tools\run-release-gate.ps1 -CompatibilityFrames 300
powershell -ExecutionPolicy Bypass -File tools\run-release-gate.ps1 -PerfFrames 300
```

## Smoke Test

Run the desktop frontend:

```powershell
dotnet run --project src\MdSharp.Desktop\MdSharp.Desktop.csproj -c Release
```

Check:

- open ROM dialog
- recent ROM list
- keyboard input
- gamepad input if available
- audio output
- mute
- pause
- reset
- fullscreen
- quick save/load state
- SRAM persistence
- input recording and playback

## Regression

Recommended before a release:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --compat roms render-output\release-compat 300 300000 --screenshots
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --audio-regression roms render-output\release-audio "Sonic the Hedgehog-Green Hill Zone Theme.mp3" 300000
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --movie-regress movies roms render-output\release-movies 300000
```

The movie command requires a local `movies/` folder. Skip it if there are no checked-in or local movie assets.

## Screenshot Showcase

After the visual checkpoints have run, refresh the committed screenshot gallery:

```powershell
powershell -ExecutionPolicy Bypass -File tools\build-showcase-gallery.ps1
```

Review [SHOWCASE.md](SHOWCASE.md), the generated images under `docs\assets\showcase\`, and the README screenshot grid. Keep ad hoc regression output under `render-output\` out of git.

## Publish Desktop Build

Use the release packaging script for normal release candidates:

```powershell
powershell -ExecutionPolicy Bypass -File tools\package-release.ps1 -Version <version>
```

The script writes framework-dependent and self-contained Windows packages under `artifacts\packages\`. See [PACKAGING.md](PACKAGING.md) for options and package contents.

Manual framework-dependent publish:

```powershell
dotnet publish src\MdSharp.Desktop\MdSharp.Desktop.csproj -c Release -o artifacts\mdSharp-desktop
```

Manual self-contained Windows x64 publish:

```powershell
dotnet publish src\MdSharp.Desktop\MdSharp.Desktop.csproj -c Release -r win-x64 --self-contained true -o artifacts\mdSharp-desktop-win-x64
```

Do not include ROMs, save files, save states, reference audio, or generated regression output in release archives.

## GitHub Release Draft

Use [RELEASE_NOTES_DRAFT.md](RELEASE_NOTES_DRAFT.md) as the starting point for the first GitHub release description. Replace `<version>` placeholders, add current verification results, and summarize compatibility changes since the previous tag.

## Clean Clone Check

Before publishing, clone the repository into a fresh directory and verify:

```powershell
dotnet build mdSharp.sln -c Release
dotnet test mdSharp.sln -c Release --no-build
powershell -ExecutionPolicy Bypass -File tools\package-release.ps1 -Version clean-clone -SkipSelfContained
```
