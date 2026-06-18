# mdSharp 0.3.0 Release Notes Draft

## Highlights

- Experimental Sega Genesis/Mega Drive emulator written in C#, with reusable core, WinForms desktop frontend, and CLI diagnostics.
- Experimental 32X bring-up: developer-gated `.32x` loading, MARS user-header boot, dual SH-2 execution, 68000/SH-2 communication registers, SDRAM/framebuffer/palette paths, packed/direct/RLE 32X VDP compositing, PWM scaffolding, and focused 32X diagnostics.
- Continued Genesis compatibility work across VDP DMA, interrupt status, Z80 bus timing, input hardware, and post-menu regression tooling.
- Audio timing improvements for Z80-driven sound programs, including smoother Sonic 1 Sega voice playback and updated PSG/FM balance guardrails.
- Pico playback tooling for converting mdSharp `.mdmovie` recordings into Raspberry Pi Pico controller-playback data for real hardware experiments.
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
- Early 32X bring-up targets including Knuckles' Chaotix, Doom, After Burner Complete, Space Harrier, and Cyber Brawl/Cosmic Carnage

The latest local Genesis release-gate work completed a 571-ROM, 600-frame compatibility sweep with all runs completing `ok`. 32X compatibility is newer and remains preliminary: selected titles reach visible or playable states, while many still depend on exact SH-2 interrupt/status, framebuffer, DMA, PWM, and bus-timing work. These sweeps are sampled local checks, not a guarantee that every game is complete from start to finish.

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
- 32X SH-2, communication, VDP, DMA, SDRAM, and SDK-handshake traces
- release gate helper
- desktop packaging script
- GitHub Actions build/test/package workflow
- repository hygiene checker for local ROMs, generated output, save files, package artifacts, and reference audio

## Known Issues

- Not cycle-perfect.
- PAL-specific behavior needs more coverage.
- Some uncommon cartridge mappers and special hardware remain partial or unsupported.
- 32X support is experimental and not yet broad compatibility-grade.
- Exact light-gun timing/calibration remains approximate.
- YM2612 accuracy is not yet bit-perfect.
- Commercial ROMs, BIOS files, coprocessor blobs, save data, and copyrighted reference audio are not included.

## Verification Checklist

- `dotnet clean mdSharp.sln -c Release`
- `dotnet build mdSharp.sln -c Release`
- `dotnet test mdSharp.sln -c Release --no-build`
- `powershell -ExecutionPolicy Bypass -File tools\check-repo-hygiene.ps1`
- `powershell -ExecutionPolicy Bypass -File tools\run-release-gate.ps1`
- `powershell -ExecutionPolicy Bypass -File tools\package-release.ps1 -Version 0.3.0`

Generated package artifacts:

- `mdSharp-desktop-0.3.0-framework-dependent.zip`
- `mdSharp-desktop-0.3.0-win-x64.zip`
