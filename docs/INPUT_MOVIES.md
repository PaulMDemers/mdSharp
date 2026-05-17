# Input Movies

mdSharp input movies are deterministic frame-by-frame controller recordings. They are stored as JSON `.mdmovie` files and can be replayed by the desktop frontend or by CLI regression tools.

Unlike a video capture, an input movie does not contain rendered frames, audio, ROM data, or game assets. It stores enough metadata to identify the intended ROM and enough per-frame input data to drive the emulator back through the same scenario.

## Recorded Data

Version 2 input movies store:

- emulator name and movie format version
- ROM display name and product code
- ROM SHA-256 for exact matching
- optional initial save-RAM snapshot
- initial frame index
- per-frame player 1 and player 2 button masks

The ROM hash is used by CLI regression and desktop playback to prevent accidentally replaying a movie against the wrong ROM revision. The optional save-RAM snapshot allows movies to start from a known cartridge save state when a game depends on persistent progress.

## Why It Matters

Input movies turn subjective emulator reports into repeatable test cases. A report such as "the HUD flickers around frame 3400" can become a durable asset:

1. Record the path once in the desktop frontend.
2. Save the `.mdmovie`.
3. Add checkpoint frames with a `.mdcheckpoints.json` sidecar file.
4. Re-render the same frames after future CPU, VDP, audio, or input changes.
5. Compare screenshots, hashes, trace output, audio activity, and final machine state.

This workflow is especially useful for raster effects, long intro sequences, title-screen idle demos, save-dependent scenes, and hardware edge cases that are hard to reach with a simple boot smoke test.

## Desktop Workflow

1. Open a ROM in the desktop frontend.
2. Select `Emulation -> Start Input Recording`.
3. Play to the target scene.
4. Select `Emulation -> Stop Input Recording`.
5. Save the `.mdmovie`.
6. Replay it with `Emulation -> Play Input Movie`.

During playback, mdSharp reloads a fresh machine, restores movie save data when present, and applies the recorded controller state at each frame.

## CLI Workflow

Print movie metadata:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --movie-info docs\assets\input-movies\sonic-green-hill-sample.mdmovie
```

Render a movie to a frame:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --movie-render "TestRoms\Sonic.md" docs\assets\input-movies\sonic-green-hill-sample.mdmovie render-output\sonic-green-hill-sample.ppm 3724 300000
```

Run movie checkpoints:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --movie-checkpoints docs\assets\input-movies TestRoms render-output\movie-checkpoints 300000
```

Run a folder of movies as a regression suite:

```powershell
dotnet run --project src\MdSharp.App\MdSharp.App.csproj -c Release -- --movie-regress docs\assets\input-movies TestRoms render-output\movie-regression 300000
```

## Committed Sample

The repository includes one sanitized sample movie:

- `docs/assets/input-movies/sonic-green-hill-sample.mdmovie`
- `docs/assets/input-movies/sonic-green-hill-sample.mdcheckpoints.json`

The sample records 3,724 frames of Sonic the Hedgehog input and includes checkpoint frames for the title/start transition and Green Hill gameplay. It stores the ROM name, product code, and SHA-256, but it does not include the ROM or a save-RAM snapshot.

The sample requires a local, legally obtained Sonic ROM matching the recorded hash. The ROM itself is not distributed by mdSharp.

## Publishing Rules

Input movies may contain ROM hashes, local file paths, and save-RAM snapshots. Before committing a movie:

- remove absolute local paths
- omit save-RAM snapshots unless the snapshot is necessary and safe to publish
- keep commercial ROMs and generated screenshots out of the repository
- include a checkpoint sidecar when specific frames matter
- verify `--movie-info` and at least one replay command
