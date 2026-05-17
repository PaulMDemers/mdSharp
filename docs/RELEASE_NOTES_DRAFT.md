# mdSharp Release Notes Draft

## Highlights

- Experimental Sega Genesis/Mega Drive emulator written in C#.
- Reusable emulation core with separate WinForms desktop and CLI projects.
- Desktop frontend with ROM loading, recent files, pause, mute, fullscreen, save/load state slots, keyboard input, XInput gamepads, and configurable controller mappings.
- Deterministic `.mdmovie` input recording and replay with ROM-hash matching, optional save-RAM snapshots, sidecar checkpoints, and CLI regression support.
- Local screenshot showcase and release-gate tooling for repeatable visual checks.
- MIT licensed source code.

## Compatibility

Recent development has focused on:

- Sonic the Hedgehog, Sonic the Hedgehog 2, Sonic the Hedgehog 3, and Sonic & Knuckles
- Sonic 2 split-screen viewport and sprite behavior
- Streets of Rage intro, menus, HUD, gameplay, and audio-driver timing
- Castlevania: Bloodlines palette and cutscene behavior
- Disney's Aladdin and Toy Story sprite/DMA edge cases
- Virtua Racing SVP/SSP1601 path
- Zero Wing sprite visibility

Compatibility is still game-dependent. The project should be described as experimental rather than cycle-perfect.

## Audio

- PSG, YM2612, DAC, Z80-driven sound programs, mixer filtering, and audio regression tooling are implemented.
- Sonic and Streets of Rage have been used as primary tuning targets.
- YM2612 behavior remains practical rather than bit-perfect; envelope, output-table, feedback-heavy instrument texture, and cross-game PSG/FM balance remain active accuracy areas.

## Tooling

- CLI compatibility dashboards
- visual checkpoints
- input movie regression
- movie checkpoint sidecars
- audio comparison and stem tools
- performance profiling
- release gate helper
- desktop packaging script
- GitHub Actions build/test/package workflow

## Known Issues

- Not cycle-perfect.
- PAL-specific behavior needs more coverage.
- Some uncommon cartridge mappers and special hardware remain partial or unsupported.
- Exact light-gun timing/calibration remains approximate.
- YM2612 accuracy is not yet bit-perfect.
- Commercial ROMs, BIOS files, coprocessor blobs, save data, and copyrighted reference audio are not included.

## Verification Checklist

- `dotnet clean mdSharp.sln -c Release`
- `dotnet build mdSharp.sln -c Release`
- `dotnet test mdSharp.sln -c Release --no-build`
- `powershell -ExecutionPolicy Bypass -File tools\package-release.ps1 -Version <version>`
- Optional local release gate: `powershell -ExecutionPolicy Bypass -File tools\run-release-gate.ps1`
