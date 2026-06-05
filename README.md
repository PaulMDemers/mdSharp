# mdSharp

mdSharp is an experimental Sega Genesis/Mega Drive emulator written in C#.

It contains a reusable emulation core, a WinForms desktop frontend for interactive play, deterministic input-movie replay, and a command-line tool for compatibility sweeps, rendering, audio diagnostics, trace capture, and regression work.

The project is in active development. It can boot and play a growing set of retail games, including several high-value compatibility targets, but it is not yet cycle-perfect and should be treated as a work-in-progress emulator.

## Screenshots

<table>
  <tr>
    <td><img src="docs/assets/showcase/showcase-composite-1.png" alt="mdSharp showcase composite with Sonic, Sonic 2, Streets of Rage, and Virtua Racing" width="420"></td>
    <td><img src="docs/assets/showcase/showcase-composite-2.png" alt="mdSharp showcase composite with Castlevania Bloodlines, Sonic 3, Aladdin, and Toy Story" width="420"></td>
  </tr>
</table>

<table>
  <tr>
    <td><img src="docs/assets/showcase/sonic1-green-hill.png" alt="Sonic the Hedgehog Green Hill" width="320"><br><sub>Sonic the Hedgehog</sub></td>
    <td><img src="docs/assets/showcase/sonic2-split-screen.png" alt="Sonic 2 split-screen demo" width="320"><br><sub>Sonic the Hedgehog 2 split-screen</sub></td>
  </tr>
  <tr>
    <td><img src="docs/assets/showcase/streets-of-rage-intro.png" alt="Streets of Rage intro" width="320"><br><sub>Streets of Rage</sub></td>
    <td><img src="docs/assets/showcase/virtua-racing-gameplay.png" alt="Virtua Racing gameplay" width="320"><br><sub>Virtua Racing</sub></td>
  </tr>
  <tr>
    <td><img src="docs/assets/showcase/castlevania-bloodlines-gameplay.png" alt="Castlevania Bloodlines gameplay" width="320"><br><sub>Castlevania: Bloodlines</sub></td>
    <td><img src="docs/assets/showcase/sonic3-special-stage.png" alt="Sonic 3 special stage" width="320"><br><sub>Sonic the Hedgehog 3</sub></td>
  </tr>
  <tr>
    <td><img src="docs/assets/showcase/aladdin-gameplay.png" alt="Disney's Aladdin gameplay" width="320"><br><sub>Disney's Aladdin</sub></td>
    <td><img src="docs/assets/showcase/toy-story-bedroom.png" alt="Disney's Toy Story gameplay" width="320"><br><sub>Disney's Toy Story</sub></td>
  </tr>
</table>

## Features

- Motorola 68000 interpreter
- Z80 audio CPU core
- Genesis memory bus and cartridge loader
- SRAM and EEPROM persistence for supported cartridge types
- SVP coprocessor path for Virtua Racing
- Save states
- VDP rendering with planes, sprites, scrolling, windowing, CRAM/VSRAM snapshots, shadow/highlight, interlace, DMA, and raster-sensitive behavior
- PSG and YM2612 audio
- WinForms desktop UI with keyboard and XInput gamepad support
- Four-player input configuration with named profiles
- Preferences, display scaling options, default ROM folder, reopen last ROM, recent ROMs, fullscreen, mute, pause, reset, save/load state, and state slots
- Input movie recording/playback with ROM-hash matching, optional save-RAM snapshots, sidecar checkpoints, and CLI regression
- CLI tooling for screenshots, compatibility dashboards, movie regression, audio regression, CPU/VDP/Z80/audio traces, and performance checks
- 32X cartridge detection and implementation planning; 32X execution is not supported yet

## Project Layout

```text
src/
  MdSharp.Core/      Emulator core with no UI dependency
  MdSharp.App/       Command-line diagnostics and regression tools
  MdSharp.Desktop/   WinForms desktop frontend
tests/
  MdSharp.Tests/     Dependency-free console test harness
docs/
  ARCHITECTURE.md    Core subsystem overview
  CLI.md             Command-line workflow reference
  TESTING.md         Test and regression strategy
  TEST_ROMS.md       Public diagnostic ROM source links
  COMPATIBILITY.md   Compatibility workflow and current focus areas
  INPUT_MOVIES.md    Deterministic input recording and replay workflow
  emulator-building/ Transferable emulator engineering process and tooling guide
  AUDIO.md           Audio implementation and tuning workflow
  32X.md             32X research notes and implementation plan
  SHOWCASE.md        Local screenshot gallery workflow
  PACKAGING.md       Desktop package creation
  RELEASE.md         Release checklist
  RELEASE_NOTES_DRAFT.md  Draft first-release notes
  CONTRIBUTING.md    Contribution workflow
  ROADMAP.md         Current status and next priorities
```

Project history is tracked in [CHANGELOG.md](CHANGELOG.md).

## Requirements

- .NET 10 SDK
- Windows for `MdSharp.Desktop`
- Legally usable ROM images

`MdSharp.Core`, `MdSharp.App`, and `MdSharp.Tests` target `net10.0`. The desktop frontend targets `net10.0-windows` and uses Windows Forms plus XInput for gamepads.

## Build And Test

```powershell
dotnet build mdSharp.sln -c Release
dotnet test mdSharp.sln -c Release --no-build
```

The test project is a console-based verification harness. Running it through `dotnet test` still works because the project exits non-zero on failure.

## Run The Desktop Emulator

```powershell
dotnet run --project src\MdSharp.Desktop\MdSharp.Desktop.csproj -c Release -- "path\to\game.md"
```

The desktop app can also be launched without an argument; ROMs can then be opened from the File menu.

Print the CLI/app version:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --version
```

Default player 1 keyboard controls:

- Arrow keys: D-pad
- `Z`: A
- `X`: B
- `C`: C
- `Enter`: Start
- `V`, `B`, `N`: X, Y, Z
- `Shift`: Mode

Default player 2 keyboard controls:

- `W`, `A`, `S`, `D`: D-pad
- `J`: A
- `K`: B
- `L`: C
- `Space`: Start
- `U`, `I`, `O`: X, Y, Z
- `H`: Mode

The desktop input configuration dialog can switch each player between three-button and six-button pad behavior, remap keyboard or XInput controls, and save/load named input profiles.
Players 3 and 4 are available for cartridge hardware such as J-Cart and default to XInput controllers 3 and 4 with keyboard bindings unset.
The same dialog can switch port 1 to Sega Team Player or EA 4-Way Play multitap adapters and can switch port 2 to Menacer or Konami Justifier light gun behavior. For light gun games, aim with the mouse over the video surface; left-click maps to trigger and right-click maps to Start. Screen-position HV timing is implemented approximately and still needs calibration against more games.

Other shortcuts:

- `Ctrl+O`: open ROM
- `Ctrl+R`: reopen last ROM
- `P`: pause
- `M`: mute
- `F5`: quick save current state slot
- `F8`: quick load current state slot
- `Alt+1` through `Alt+0`: select state slots 1 through 10
- `F11`: fullscreen
- `Esc`: exit fullscreen

The desktop app stores settings under `%APPDATA%\mdSharp\desktop-settings.json`. If `mdsharp-portable.json` or a `portable\` folder exists next to the executable, settings, save RAM, and quick states are stored under `portable\` instead. The File menu includes Preferences for display aspect/scaling, default ROM folder, save RAM folder, save-state folder, and audio mute. `Help -> Diagnostics...` opens a copyable support summary with app/package details, storage paths, input/display settings, and current cartridge metadata. `View -> Developer Options` reveals diagnostic frame safety budget controls used during emulator development.

## Command-Line Examples

Run a single ROM smoke test:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- "path\to\game.md" 100000
```

Render a frame:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --render "path\to\game.md" render-output\frame.ppm 600 300000
```

Build a compatibility dashboard:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --compat roms render-output\compat-300 300 300000 --screenshots
```

Run audio regression:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --audio-regression roms render-output\audio-regression "Sonic the Hedgehog-Green Hill Zone Theme.mp3" 300000
```

Compare any rendered game audio against a reference track:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --audio-compare "roms\Streets of Rage (USA) (Rev-A).md" streets-title.flac render-output\streets-audio streets-title 900 300000 0
```

See [docs/CLI.md](docs/CLI.md) for the fuller CLI reference.

## Input Movies

mdSharp can record controller input once and replay it deterministically by frame. A `.mdmovie` file stores ROM identity metadata, the ROM SHA-256, optional save-RAM state, and player input masks for each frame. It does not contain ROM data, rendered video, or audio.

This makes long or timing-sensitive bug reports repeatable. A scene such as a title-screen idle demo, Green Hill gameplay path, Sonic 2 split-screen sequence, or save-dependent scene can be recorded once, then replayed through the CLI after future CPU, VDP, audio, or input changes.

The repo includes a sanitized sample:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --movie-info docs\assets\input-movies\sonic-green-hill-sample.mdmovie
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --movie-checkpoints docs\assets\input-movies TestRoms render-output\movie-checkpoints 300000
```

See [docs/INPUT_MOVIES.md](docs/INPUT_MOVIES.md) for the full workflow.

## Visual Showcase

The committed screenshot set can be refreshed from local visual checkpoint output:

```powershell
powershell -ExecutionPolicy Bypass -File tools\build-showcase-gallery.ps1
```

The full gallery is documented in [docs/SHOWCASE.md](docs/SHOWCASE.md). Intermediate regression images still live under `render-output/` and remain ignored by git.

## Compatibility Status

mdSharp is being developed against a mix of public test ROMs, targeted retail-game scenarios, and repeatable input movies. Recent focus areas include:

- Sonic 1, Sonic 2, Sonic 3, and Sonic & Knuckles behavior
- Sonic 2 split-screen raster/viewport rendering
- Streets of Rage boot, intro, menus, gameplay, and HUD rendering
- Castlevania: Bloodlines cutscene and palette behavior
- Disney's Aladdin and Toy Story sprite/DMA edge cases
- YM2612/PSG audio balance against reference recordings

Compatibility varies by game. The emulator aims for correctness first, with broad compatibility and speed improving over time.

Sega 32X support is in active bring-up. The desktop and CLI recognize `.32x` cartridges, attach the 32X shell, boot MARS user-header SH-2 programs, run both SH-2s with coarse frame scheduling, render packed/direct/RLE 32X VDP output over the Mega Drive frame, and reach visible or playable states in several retail titles. Compatibility is still preliminary; the remaining SH-2, interrupt/status, VDP, PWM, and bus-integration work is tracked in [docs/32X.md](docs/32X.md).

## ROMs, BIOS, And Reference Audio

No commercial ROMs, BIOS files, or copyrighted reference music should be committed to this repository.

Recommended local-only folders/files:

- `roms/` for retail ROM collections
- `TestRoms/` for public or homebrew test ROMs
- `movies/` for local input movie captures
- `render-output/` for generated screenshots, audio, traces, dashboards, and regression reports
- `svp.bin` for a local Virtua Racing SVP coprocessor blob, when available
- `32X_G_BIOS.BIN` or `32X_M68K_BIOS.BIN` for local-only 32X 68000 BIOS testing, or set `MDSHARP_32X_M68K_BIOS`
- `32X_M_BIOS.BIN`, `32X_MASTER_BIOS.BIN`, or `32X_MSH2_BIOS.BIN` for local-only 32X master SH-2 BIOS testing, or set `MDSHARP_32X_MASTER_SH2_BIOS`
- `32X_S_BIOS.BIN`, `32X_SLAVE_BIOS.BIN`, or `32X_SSH2_BIOS.BIN` for local-only 32X slave SH-2 BIOS testing, or set `MDSHARP_32X_SLAVE_SH2_BIOS`
- `Sega 32X BIOS/32X/32X - Genesis BIOS.bin`, `32X - Master BIOS.bin`, and `32X - Slave BIOS.bin` are also detected for local 32X BIOS testing
- `MDSHARP_32X_REAL_SH2_BIOS_BOOT=1` enables the experimental real SH-2 BIOS reset-vector path for diagnostics; the default path still uses mdSharp's synthetic user-header handoff
- local `.wav`, `.flac`, `.mp3`, or `.ogg` reference audio used for diagnostics

These paths are ignored by the release-oriented `.gitignore`, with only `.gitkeep` placeholders tracked for empty local folders.

See [docs/TEST_ROMS.md](docs/TEST_ROMS.md) for public diagnostic/test ROM source links used during development. Commercial games used as compatibility targets must stay local and are not redistributed by this project.

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [CLI reference](docs/CLI.md)
- [Testing and regression](docs/TESTING.md)
- [Test ROM sources](docs/TEST_ROMS.md)
- [Compatibility workflow](docs/COMPATIBILITY.md)
- [Input movies](docs/INPUT_MOVIES.md)
- [Audio workflow](docs/AUDIO.md)
- [Visual showcase](docs/SHOWCASE.md)
- [Packaging](docs/PACKAGING.md)
- [Release checklist](docs/RELEASE.md)
- [Release notes draft](docs/RELEASE_NOTES_DRAFT.md)
- [Contributing](docs/CONTRIBUTING.md)
- [Roadmap](docs/ROADMAP.md)
- [Changelog](CHANGELOG.md)

## Legal

mdSharp does not include Sega code, game ROMs, or copyrighted game assets. Use only legally obtained ROM images and reference material.

mdSharp is licensed under the MIT License. See [LICENSE](LICENSE).
