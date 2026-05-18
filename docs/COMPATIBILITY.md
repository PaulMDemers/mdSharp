# Compatibility

mdSharp is compatibility-driven. Each visible issue should become a repeatable test, movie, trace, or dashboard entry.

## Current High-Value Targets

These games and scenarios have been used frequently during development:

- Sonic the Hedgehog
- Sonic the Hedgehog 2, including split-screen idle/demo behavior
- Sonic the Hedgehog 3 and special-stage behavior
- Sonic & Knuckles
- Streets of Rage
- Castlevania: Bloodlines
- Disney's Aladdin
- Disney's Toy Story
- Zero Wing

The list is not a compatibility guarantee. It is a practical set of regression targets.

## Known Sensitive Areas

Video:

- raster effects
- split-screen viewports
- window plane behavior
- per-line horizontal and vertical scroll
- sprite masking and sprite limits
- DMA timing and FIFO behavior
- CRAM/VSRAM changes during active display
- interlace and shadow/highlight behavior

Audio:

- YM2612 envelope and operator output accuracy
- YM2612 feedback-heavy instruments
- DAC timing and sample scale
- PSG/FM balance
- per-frame audio write timing

CPU/bus:

- 68000 exception and interrupt details
- Z80 bus request/reset timing
- Z80/YM/PSG access timing
- cartridge save hardware and special mappers; `--cart-info` reports detected save hardware, bank switching, and known unsupported cartridge hardware

## Broad Sweep Workflow

Run:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --compat roms render-output\compat 300 300000 --screenshots
```

Inspect:

- `index.html`
- `compatibility.csv`
- screenshots under `screenshots/`

Prioritize issues that appear across many games or point to shared hardware behavior.

Near-blank final frames require a second pass before being treated as failures. Some games are simply in a blank transition at the sampled frame. Use:

```powershell
powershell -ExecutionPolicy Bypass -File tools\classify-near-blank.ps1 -CompatibilityCsv render-output\compat\compatibility.csv -RomFolder roms -OutputFolder render-output\near-blank -Frames "3000,6001,9000"
```

The follow-up report classifies each near-blank candidate as visible, still blank, missing, or errored and links the later screenshots.

## Publishing A Matrix

After a sweep, export the CSV to a stable JSON artifact and a docs-ready Markdown table:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --compat-export render-output\compat\compatibility.csv render-output\compat\published
```

Outputs:

- `compatibility-matrix.json`
- `compatibility-matrix.md`

The matrix is derived from sampled emulator behavior and should be treated as a triage aid, not a promise that a game is complete. Ratings are intentionally coarse:

| Rating | Meaning |
| --- | --- |
| `A` | Visible frame, sprite activity, audio activity, no fallback rendering or CPU fault activity in the sampled run |
| `B` | Boots visibly but has suspicious sampled audio or sprite activity |
| `C` | Boots visibly with CPU fault-vector activity or fallback rendering |
| `Boots` | Run completed but sampled frames were near-blank |
| `Broken` | Emulator error or 68000 exception stopped the run |
| `Unsupported Hardware` | Cartridge declares unsupported hardware |

Use `--public` when creating artifacts for a public repository page:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --compat-export render-output\compat\compatibility.csv render-output\compat\public --public
```

Public mode redacts local ROM filenames and screenshot links. Keep the full local export under `render-output/`.

## Recent Local Sweep Snapshot

The latest local 600-frame sweep over `roms/` completed after the Z80 audio timing continuity pass with:

- 571 ROMs
- 571 `ok`
- 0 failed
- 0 CPU fault vector cases
- 46 near-blank final frames at the initial 600-frame sample
- 0 fallback render modes
- average emulation speed of 376.6 FPS

Near-blank follow-up at frames `3000`, `6001`, and `9000` classified 41 of those 46 as visible later. The remaining no-input still-blank follow-up candidates are:

- `Ex-Mutants (USA).md`
- `Pac-Man 2 - The New Adventures (USA).md`
- `Shadow of the Beast (USA).md`
- `Tyrants - Fight Through Time (USA).md`
- `Zany Golf (USA).md`

Some no-input still-blank cases are expected to require title/input setup or longer per-game movies. `Zany Golf` has been checked with repeated Start input and reaches a visible instruction/gameplay screen; it is tracked by the `zany-golf-instructions` visual checkpoint.

The focused visual checkpoint set completed 50/50 `ok` over current high-value scenes, including Sonic 2 split-view idle, Aladdin Genie animation, Toy Story Traveler's Tales, Zero Wing idle gameplay, and Virtua Racing SVP gameplay.

This snapshot is local and depends on the ROM set in `roms/`; regenerate it before publishing compatibility claims.

## Focused Game Workflow

1. Reproduce the issue in the desktop frontend.
2. Record an input movie if the scene requires input or a long setup.
3. Note the approximate frame number.
4. Use `--movie-render`, `--render-sequence`, or `--movie-checkpoints` to make the frame repeatable.
5. Trace the suspected subsystem.
6. Fix the shared behavior.
7. Add a core test when possible.
8. Re-run the affected movie and a small broad sweep.

## Movie Sidecar Checkpoints

Movie checkpoint sidecars let one `.mdmovie` provide multiple named frame checks. A sidecar sits next to the movie with a `.mdcheckpoints.json` suffix, such as `sonic-green-hill-sample.mdcheckpoints.json`.

See [INPUT_MOVIES.md](INPUT_MOVIES.md) for the full input movie workflow and the committed Sonic sample.

Example:

```json
{
  "checkpoints": [
    { "id": "title", "name": "Title screen", "frame": 600 },
    { "id": "gameplay", "name": "Gameplay after start", "frame": 3400 }
  ]
}
```

Then run:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --movie-checkpoints movies roms render-output\movie-checkpoints 300000
```

## Unsupported Or Partial Areas

Expect gaps around:

- uncommon cartridge mappers
- SVP accuracy beyond Virtua Racing's supported SSP1601 path
- multitap behavior beyond the selectable Sega Team Player and EA 4-Way Play adapters
- exact light gun HV timing and per-game calibration
- cartridge-hosted extra input hardware beyond J-Cart ports
- six-button edge cases in unusual polling loops
- exact CPU cycle timing
- exact YM2612 chip behavior
- PAL-specific edge cases

When unsupported hardware is detected, prefer a clear diagnostic over silent incorrect behavior.
