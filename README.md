# mdSharp

mdSharp is an experimental Sega Genesis/Mega Drive emulator written in C#.

It contains a reusable emulation core, a WinForms desktop frontend for interactive play, and a command-line tool for compatibility sweeps, rendering, audio diagnostics, trace capture, and replay regression.

The project is in active development. It can boot and play a growing set of retail games, including several high-value compatibility targets, but it is not yet cycle-perfect and should be treated as a work-in-progress emulator.

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
- Two-player input configuration
- Recent ROMs, fullscreen, mute, pause, reset, save/load state, and state slots
- Input movie recording and playback for repeatable compatibility testing
- CLI tooling for screenshots, compatibility dashboards, movie regression, audio regression, CPU/VDP/Z80/audio traces, and performance checks

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
  AUDIO.md           Audio implementation and tuning workflow
  RELEASE.md         Release checklist
  CONTRIBUTING.md    Contribution workflow
  ROADMAP.md         Current status and next priorities
```

## Requirements

- .NET 10 SDK
- Windows for `MdSharp.Desktop`
- ROM images you are legally permitted to use

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

You can also launch the desktop app without an argument and open a ROM from the File menu.

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

The desktop input configuration dialog can switch each player between three-button and six-button pad behavior and can remap keyboard or XInput controls.
Players 3 and 4 are available for cartridge hardware such as J-Cart and default to XInput controllers 3 and 4 with keyboard bindings unset.
The same dialog can switch port 1 to Sega Team Player or EA 4-Way Play multitap adapters and can switch port 2 to Menacer or Konami Justifier light gun behavior. For light gun games, aim with the mouse over the video surface; left-click maps to trigger and right-click maps to Start. Screen-position HV timing is implemented approximately and still needs calibration against more games.

Other shortcuts:

- `P`: pause
- `M`: mute
- `F5`: quick save current state slot
- `F8`: quick load current state slot
- `Alt+1` through `Alt+0`: select state slots 1 through 10
- `F11`: fullscreen
- `Esc`: exit fullscreen

The desktop app stores settings under `%APPDATA%\mdSharp\desktop-settings.json`. Save RAM and state files are stored next to the ROM by default.

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

## Compatibility Status

mdSharp is being developed against a mix of public test ROMs, targeted retail-game scenarios, and repeatable input movies. Recent focus areas include:

- Sonic 1, Sonic 2, Sonic 3, and Sonic & Knuckles behavior
- Sonic 2 split-screen raster/viewport rendering
- Streets of Rage boot, intro, menus, gameplay, and HUD rendering
- Castlevania: Bloodlines cutscene and palette behavior
- Disney's Aladdin and Toy Story sprite/DMA edge cases
- YM2612/PSG audio balance against reference recordings

Compatibility varies by game. The emulator aims for correctness first, with broad compatibility and speed improving over time.

## ROMs, BIOS, And Reference Audio

No commercial ROMs, BIOS files, or copyrighted reference music should be committed to this repository.

Recommended local-only folders/files:

- `roms/` for retail ROM collections
- `TestRoms/` for public or homebrew test ROMs
- `movies/` for local input movie captures
- `render-output/` for generated screenshots, audio, traces, dashboards, and regression reports
- `svp.bin` for a local Virtua Racing SVP coprocessor blob, if you have one
- local `.wav`, `.flac`, `.mp3`, or `.ogg` reference audio used for diagnostics

These paths are ignored by the release-oriented `.gitignore`, with only `.gitkeep` placeholders tracked for empty local folders.

See [docs/TEST_ROMS.md](docs/TEST_ROMS.md) for public diagnostic/test ROM source links used during development. Commercial games used as compatibility targets must stay local and are not redistributed by this project.

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [CLI reference](docs/CLI.md)
- [Testing and regression](docs/TESTING.md)
- [Test ROM sources](docs/TEST_ROMS.md)
- [Compatibility workflow](docs/COMPATIBILITY.md)
- [Audio workflow](docs/AUDIO.md)
- [Release checklist](docs/RELEASE.md)
- [Contributing](docs/CONTRIBUTING.md)
- [Roadmap](docs/ROADMAP.md)

## Legal

mdSharp does not include Sega code, game ROMs, or copyrighted game assets. Use only ROM images and reference material that you have the right to use.

No license has been selected in this working tree yet. Choose and add a `LICENSE` file before publishing the repository publicly.
