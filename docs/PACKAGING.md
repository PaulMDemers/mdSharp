# Packaging

Release packages are created with:

```powershell
powershell -ExecutionPolicy Bypass -File tools\package-release.ps1 -Version <version>
```

The script performs a clean Release build, stamps assemblies with the requested version, runs tests by default, publishes the desktop frontend, copies release documentation into each package folder, writes a package `manifest.json`, verifies package contents, and writes zip files under `artifacts/packages/`.

## Outputs

Default output:

- `artifacts/mdSharp-desktop-<version>-framework-dependent/`
- `artifacts/mdSharp-desktop-<version>-win-x64/`
- `artifacts/packages/mdSharp-desktop-<version>-framework-dependent.zip`
- `artifacts/packages/mdSharp-desktop-<version>-win-x64.zip`

The framework-dependent package requires the appropriate .NET Desktop Runtime. The self-contained `win-x64` package includes the runtime and is larger.

Self-contained publishing requires Microsoft runtime packs. The script uses `https://api.nuget.org/v3/index.json` explicitly for that publish step because the repository `NuGet.Config` otherwise clears package sources.

## Options

```powershell
powershell -ExecutionPolicy Bypass -File tools\package-release.ps1 -Version 0.1.0
powershell -ExecutionPolicy Bypass -File tools\package-release.ps1 -Version 0.1.0 -SkipTests
powershell -ExecutionPolicy Bypass -File tools\package-release.ps1 -Version 0.1.0 -SkipSelfContained
powershell -ExecutionPolicy Bypass -File tools\package-release.ps1 -Version 0.1.0 -Runtime win-arm64
powershell -ExecutionPolicy Bypass -File tools\package-release.ps1 -Version 0.1.0 -Portable
```

`-Portable` adds `mdsharp-portable.json` and a `portable/` storage root to each package. When present next to `MdSharp.Desktop.exe`, the desktop app stores settings, save RAM, and quick states under `portable/` instead of `%APPDATA%\mdSharp\`, unless the user configures explicit save folders in Preferences.

## Package Contents

Packages include:

- desktop emulator binaries
- `README.md`
- `LICENSE`
- `NOTICE.txt`
- `manifest.json`

The manifest records package name, version, build date, commit, runtime mode, portable mode, and SHA-256 hashes for package files.

Packages must not include:

- commercial ROMs
- BIOS files or coprocessor blobs
- save RAM or save states
- copyrighted reference audio
- local screenshots, traces, dashboards, or regression output

## GitHub Actions

The `Build` workflow builds, tests, packages the desktop app, writes `SHA256SUMS.txt`, and uploads zip artifacts for each run. Normal branch and pull-request runs use the package version `ci`.

When a tag named `v<version>` is pushed, the workflow packages with `<version>` and attaches the framework-dependent zip, self-contained zip, and checksum file to the matching GitHub release. Local release candidates should still follow [RELEASE.md](RELEASE.md) before tagging.
