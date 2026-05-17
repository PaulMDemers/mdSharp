# Audio Reference Sources

This file tracks candidate local-only reference material for audio accuracy work. Do not commit downloaded copyrighted audio files; keep them in the workspace root or another ignored local folder.

## Best Current Targets

| Case | Preferred local filename | Source | Notes |
| --- | --- | --- | --- |
| Streets of Rage title | `streets-title.ogg` or `streets-title.flac` | VGMPF `The Street of Rage` | Direct per-track game-rip OGG, listed as track 01, 1:38. Best quick source for the missing Streets reference. |
| Streets of Rage title/stage tracks | `streets-title.vgm` / rendered WAV | OC ReMix Streets of Rage VGM archive | Exact VGM register-log archive with `The Street of Rage` and 15 other tracks. Good for YM/PSG register-level synthesis checks. |
| Streets of Rage official CD | `streets-title.flac` | Bare Knuckle Original Soundtrack | Official digitally remastered album, track `The Street of Rage`, 1:41. Best if available from a legally owned CD/digital purchase. |
| Sonic 1 title/Green Hill | existing root FLAC/MP3 | Current local files | Already present locally and wired into the sample manifest. |
| Sonic 1 VGM checks | rendered VGM WAVs | OC ReMix Sonic VGM archive | Exact VGM archive including `Title Theme` and `Green Hill Zone`. Useful for isolated chip synthesis regressions. |

## Links

- Streets of Rage VGMPF page: https://www.vgmpf.com/Wiki/index.php/Streets_of_Rage_(GEN)
- Streets of Rage direct `The Street of Rage` OGG: https://www.vgmpf.com/Wiki/images/7/7d/01_-_Streets_of_Rage_-_GEN_-_The_Street_of_Rage.ogg
- Streets of Rage OC ReMix VGM archive page: https://ocremix.org/chip/2306
- Sonic 1 OC ReMix VGM archive page: https://ocremix.org/chip/2142
- Bare Knuckle Original Soundtrack VGMdb entry: https://vgmdb.net/album/33417

## Recommended Next Local Setup

1. Download the VGMPF `The Street of Rage` OGG.
2. Save it as `streets-title.ogg` in the repo root.
3. Update `docs/audio-reference-manifest.sample.json` to use `../streets-title.ogg`, or copy the manifest and adjust the local path.
4. Run:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --audio-reference-suite docs\audio-reference-manifest.sample.json roms render-output\audio-reference-suite 300000
```

For chip-level checks, download the OC ReMix VGM ZIPs and render individual `.vgm` files through `--vgm-render`; those are better for validating YM2612/PSG synthesis than for validating full in-game Z80 timing.

## Reference Emulator Capture

For end-to-end game audio, a known-good emulator capture is usually cleaner than YouTube or album audio because it preserves boot timing, Z80 driver behavior, sound effects, and the exact game mix. MAME is the preferred local capture source because it can write the final mixer output directly to WAV and can stop after a fixed emulated duration.

The repo includes a helper script for loose Genesis/Mega Drive ROMs:

```powershell
powershell -ExecutionPolicy Bypass -File tools\capture-mame-audio-reference.ps1 -RomPath "TestRoms\Sonic.md" -OutputWav render-output\audio-reference-suite\mame\sonic-title-mame.wav -SecondsToRun 32
```

For a post-start reference, use a deterministic start-button pulse:

```powershell
powershell -ExecutionPolicy Bypass -File tools\capture-mame-audio-reference.ps1 -RomPath "TestRoms\Sonic.md" -OutputWav render-output\audio-reference-suite\mame\sonic-start-mame.wav -SecondsToRun 45 -InputPreset PressStart -StartFrame 560 -PressFrames 24
```

For the Sonic 1 Sega voice, keep a separate short boot clip so DAC tuning is not compared against the later title loop:

```powershell
powershell -ExecutionPolicy Bypass -File tools\capture-mame-audio-reference.ps1 -RomPath "TestRoms\Sonic.md" -OutputWav render-output\audio-reference-suite\mame\sonic-sega-intro-mame.wav -SecondsToRun 9
```

The direct MAME command behind the helper is:

```powershell
render-output\reference-emulators\mame\mame0287\mame.exe genesis -cart "TestRoms\Sonic.md" -video none -window -nothrottle -seconds_to_run 32 -samplerate 44100 -wavwrite render-output\audio-reference-suite\mame\sonic-title-mame.wav
```

MAME reference captures are generated files and should stay under `render-output` or another ignored local directory. Keep the Sonic title capture long enough for at least 10 seconds of title-loop music after the intro; otherwise the aligner can accidentally compare against the quiet pre-title segment. Once captured, point a copied manifest at the WAV path and run `--audio-reference-suite` normally.
