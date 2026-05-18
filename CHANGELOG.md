# Changelog

All notable project changes are tracked here. mdSharp uses semantic version tags for public releases.

## Unreleased

- Started the `0.2.0` development cycle.
- Added shared assembly version metadata.
- Added desktop `Help -> About mdSharp` dialog with version, license, and repository links.
- Added `mdsharp --version` to the CLI.
- Updated release packaging to stamp binaries with the requested package version.
- Added README showcase composite panels.
- Added tag-triggered GitHub release artifact upload with SHA-256 checksums.
- Added desktop shortcuts for opening a ROM and reopening the last ROM.
- Added desktop Preferences for default ROM folder, mute state, and instruction budget.
- Added named desktop input profiles for controller and port-device settings.
- Added desktop Preferences for save RAM and save-state storage folders.
- Added desktop display Preferences for aspect mode, integer scaling, and smoothing.
- Added CLI compatibility matrix export to JSON and Markdown, with public redaction mode.

## 0.1.0 - 2026-05-17

- Published the first GitHub release.
- Added reusable C# Genesis/Mega Drive emulation core, WinForms desktop frontend, and CLI diagnostics.
- Added desktop ROM loading, recent files, keyboard/gamepad input, input configuration, fullscreen, save states, SRAM/EEPROM persistence, and input movie recording/playback.
- Added VDP rendering support for planes, sprites, scrolling, windowing, DMA, CRAM/VSRAM snapshots, shadow/highlight, interlace, and raster-sensitive behavior.
- Added PSG/YM2612 audio, audio diagnostics, stem rendering, and reference comparison tooling.
- Added deterministic `.mdmovie` sample and checkpoint workflow.
- Added local screenshot showcase, documentation set, MIT license, desktop icon, release packaging script, and GitHub issue/PR templates.
