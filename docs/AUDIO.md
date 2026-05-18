# Audio

mdSharp emulates Genesis audio through:

- PSG tone/noise generator
- YM2612 FM synthesizer and DAC
- Z80-driven sound programs
- final mixer, filtering, bass shelf, and soft limiting

Audio is functional across many games, but it remains an active development area. The YM2612 implementation is practical and improving; it is not yet a bit-perfect chip model.

## Current Audio Regression Set

`--audio-regression` renders:

- Sonic 1 title
- Sonic 1 attract/demo
- Sonic 1 Green Hill
- Sonic 1 gameplay
- Sonic 2 title
- Sonic 2 idle/demo
- Streets of Rage title
- Castlevania: Bloodlines title
- Toy Story intro

It also compares against local references when present:

- Sonic title reference in the repo root
- Sonic Green Hill reference passed as an argument
- Streets reference in the repo root

Example:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --audio-regression roms render-output\audio-regression "Sonic the Hedgehog-Green Hill Zone Theme.mp3" 300000
```

## Reference Audio

Reference audio is local-only. Do not commit it.

See `docs/AUDIO_REFERENCES.md` for web reference candidates and reference-emulator capture workflows.

Recognized Sonic title names:

- `01 - Title Theme - Masato Nakamura.flac`
- `sonic-title.flac`
- `sonic-title.wav`
- `sonic-title.mp3`

Recognized Streets names:

- `streets-title.flac`
- `streets-title.wav`
- `streets-title.mp3`
- `streets-of-rage-title.flac`
- `streets-intro.flac`

Any root audio file with `streets` in the filename can also be detected.

## Generic Compare

Use the generic comparison path for any game:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --audio-compare "roms\Streets of Rage (USA) (Rev-A).md" streets-title.flac render-output\streets-audio streets-title 900 300000 0
```

Arguments:

- ROM path
- reference audio
- output folder
- comparison ID
- render frames
- instructions per frame
- compare start frame

The compare start frame is important when the desired song begins after boot silence, logos, or sound effects. Sonic title, for example, uses frame `559` in the Sonic-specific path.

Use file comparison when both sides are already rendered audio files, such as comparing a VGM render against a MAME capture:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --audio-file-compare render-output\audio-reference-suite\mame\streets-intro-60s-mame.wav render-output\audio-reference-suite\streets-vgm-mdsharp.wav render-output\streets-vgm-file-compare streets-mame-vs-vgm 10
```

## Reference Suites

Use a manifest when iterating on audio quality so the same reference segments run every time:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --audio-reference-suite docs\audio-reference-manifest.sample.json roms render-output\audio-reference-suite 300000
```

Each case can provide `romContains`, `reference`, `frames`, and either a Sonic-specific `preset` or a generic `compareStartFrame`. Short clips such as the Sonic 1 Sega voice can also use `alignmentWindowSeconds` so the comparison focuses on the voice instead of the surrounding boot silence. If a reference contains repeated sections or the automatic aligner lands on the wrong phrase, set `referenceStartSeconds` and/or `emulatedStartSeconds` to force the analysis window. The sample manifest is intentionally local-reference friendly; missing reference files are reported in the suite summary instead of failing the run.

For MAME captures generated under `render-output\audio-reference-suite\mame`, use:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --audio-reference-suite docs\audio-reference-manifest.mame-local.sample.json roms render-output\audio-reference-suite-mame-local 300000
```

## YM2612 Chip Reference Probes

`tools\ymref` contains a small YM2612 script renderer that compares mdSharp's YM core against Nuked-OPN2, which is kept under `tools\Nuked-OPN2` as a tool-only LGPL reference.

Run the full probe suite with:

```powershell
powershell -ExecutionPolicy Bypass -File tools\ymref\run-ymref.ps1 -OutputRoot render-output\ymref-suite
```

The runner compiles `tools\ymref\ymref.c`, builds mdSharp, renders every script under `tools\ymref\scripts`, compares the Nuked and mdSharp WAVs, and writes `ymref-summary.md`.

By default the Nuked side renders YM2612 pin output. To compare against Nuked's internal channel accumulator instead, use:

```powershell
powershell -ExecutionPolicy Bypass -File tools\ymref\run-ymref.ps1 -OutputRoot render-output\ymref-suite-internal -ReferenceOutput internal
```

Probe coverage:

- `single-operator-carrier`: isolates base operator phase, level, and envelope behavior
- `single-carrier`: stresses algorithm 7 parallel carrier summing
- `feedback-algorithm0`: stresses feedback and algorithm 0 modulation routing
- `attack-decay`: checks envelope attack/decay behavior
- `dac-step`: checks YM DAC shape and filtering

Treat the raw chip probe metrics as diagnostics rather than final audible scores. Nuked pin output includes the YM2612's external ladder/pin behavior, while internal output is better for isolating oscillator, envelope, algorithm, and feedback differences. Normal mdSharp game audio also goes through the emulator mixer and output filtering. These probes help locate chip-core mistakes; audible changes should still be validated with the Sonic and Streets reference suites.

## Metrics

Comparison reports include:

- envelope correlation
- RMS delta
- peak delta
- brightness delta
- bass/body/melody/sparkle band deltas

These metrics are not a substitute for listening. They are guardrails for iteration.

## Stems And Traces

Generic tools can render stems for any ROM:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --audio-stems "roms\Streets of Rage (USA) (Rev-A).md" render-output\streets-stems 1320 300000 258
```

Outputs include per-channel YM stems, PSG stems, a mixed stem, and a stem band report with top detected notes for the analysis window.

Use stem comparison when a reference clip is available. This aligns the mixed output to the reference, then reports the reference error and each channel's contribution in the same aligned window:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --audio-stem-compare "roms\Streets of Rage (USA) (Rev-A).md" render-output\audio-reference-suite\mame\streets-intro-60s-mame.wav render-output\streets-stem-compare streets-intro 3600 300000 0
```

Like `--audio-compare`, stem comparison accepts optional forced windows after `alignment-window-seconds`: `reference-start-seconds` and `emulated-start-seconds`.

VGM/VGZ files can be rendered into stems, which is useful when comparing against a reference emulator capture without ROM boot timing:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --vgm-stems .scratch\audio-ref\streets-01-the-street-of-rage.vgz render-output\streets-vgm-stems 60
```

Use VGM stem comparison to align a VGM render with a reference clip and score each chip channel in the aligned window:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --vgm-stem-compare .scratch\audio-ref\streets-01-the-street-of-rage.vgz render-output\audio-reference-suite\mame\streets-intro-60s-mame.wav render-output\streets-vgm-stem-compare streets-vgm 60 10
```

Sonic-specific tools can additionally render Sonic-tailored stems and traces:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --sonic-audio-stems "roms\Sonic the Hedgehog (USA).md" render-output\sonic-stems greenhill 2600 300000
```

Outputs include:

- per-channel YM stems
- PSG stems
- mixed stem
- PSG trace CSV
- YM channel trace/energy reports

## Current Tuning Notes

Recent audio work improved:

- Sega DAC voice pitch/scale
- Sega DAC level compensation after lowering master output headroom
- Sonic title missing melody presence
- Sonic Green Hill melody level
- Streets of Rage PSG prominence by lowering the PSG mix contribution while opening the PSG low-pass filter to preserve square-wave edge
- Streets of Rage YM texture by softening FM phase-modulation depth after comparing the in-game MAME capture with a VGZ register-log render
- upper harmonic presence by opening the final output low-pass to 12 kHz after checking Sonic title, Sonic Green Hill, Sega voice, and Streets MAME guardrails
- YM title/lead transient shape by increasing the default attack envelope scale to 1.25 after checking Sonic title forced-window, Sonic Green Hill MAME, and Streets VGM/MAME guardrails
- YM2612 operator routing for algorithms 0, 1, 2, 3, and 5
- YM2612 envelope/output attenuation scale, including 0.094 dB envelope steps, 0.75 dB total-level steps, and high-attenuation muting
- global clipping behavior through output soft limiting
- objective audio regression summaries
- Sonic title MAME balance by lowering default YM channel 2 and YM channel 6/DAC mix contribution, which keeps the low `G#2`/`D#2` support from overpowering the `D5`/`D6` lead
- Sonic title MAME comparison stability by forcing the title windows near 8.00s and letting the comparer fine-align locally; the current MAME suite refines Sonic title to about reference 7.93s / emulator 7.89s and raises the title correlation to roughly 0.895
- Sonic Sega voice smoothness by lowering the YM DAC low-pass default to 6 kHz and disabling the DAC high-pass stage by default; this removes the small DC-recovery tremor while keeping the setting overrideable with `MDSHARP_YM_DAC_LOW_PASS_HZ` and `MDSHARP_YM_DAC_HIGH_PASS_HZ`
- 0.3.0 guardrail pass lowered the default PSG mix from 1.05 to 0.90 after the local Sonic/Streets MAME reference suite improved average correlation, RMS delta, and band-error score while keeping the case ratings stable
- Sonic Sega voice PCM continuity by running the Z80 up to the current master-cycle slice with a persistent cursor instead of recreating its audio timing at each video frame; this keeps DAC writes from folding backward or bunching at frame ends

Current known rough spots:

- YM2612 envelope generator accuracy
- operator output table accuracy
- feedback-heavy FM instrument texture
- exact PSG/FM balance across games
- title/intro tracks that rely on bright upper harmonics

Future YM work should be measured against both Sonic and Streets, not just one game.

## Local Audio Tuning Knobs

Several audio constants can be overridden with environment variables for local sweeps without editing source:

- `MDSHARP_OUTPUT_LOW_PASS_HZ`
- `MDSHARP_PSG_LOW_PASS_HZ`
- `MDSHARP_BASS_SHELF_HZ`
- `MDSHARP_BASS_SHELF_GAIN`
- `MDSHARP_YM_MIX_LEVEL`
- `MDSHARP_PSG_MIX_LEVEL`
- `MDSHARP_MASTER_MIX_LEVEL`
- `MDSHARP_OUTPUT_SOFT_LIMIT`
- `MDSHARP_YM_PHASE_MOD_SCALE`
- `MDSHARP_YM_PHASE_MOD_SOFT_LIMIT`
- `MDSHARP_YM_FEEDBACK_SHIFT`
- `MDSHARP_YM_ATTACK_SCALE`
- `MDSHARP_YM_DECAY_SCALE`
- `MDSHARP_YM_SUSTAIN_SCALE`
- `MDSHARP_YM_RELEASE_SCALE`
- `MDSHARP_YM_ATTACK_CURVE_DIVISOR`
- `MDSHARP_YM_TABLE_OUTPUT`

`MDSHARP_YM_TABLE_OUTPUT=1` enables the experimental log-sine/attenuation-table operator path. Current measurements show it is very close to the linear path: slightly better for Streets VGM/MAME and Sonic Green Hill melody, but not yet a clear default because Green Hill sparkle/correlation regressed slightly.
