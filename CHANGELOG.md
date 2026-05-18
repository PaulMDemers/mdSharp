# Changelog

All notable project changes are tracked here. mdSharp uses semantic version tags for public releases.

## Unreleased

- Started the `0.3.0` development cycle.
- Retuned the default PSG mix level against the local Sonic/Streets MAME audio guardrail suite.
- Kept the Z80 audio execution cursor continuous across frame boundaries and bounded to the current master-cycle slice, removing small PCM gaps in the Sonic 1 Sega voice.
- Extended save states to preserve the Z80 audio timing cursor.
- Re-ran the post-Z80 timing audio, visual, compatibility, and near-blank guardrails and recorded the current local snapshot.
- Corrected VDP DMA fill to perform the trigger data-port word write followed by byte fills using the VDP auto-increment.
- Classified the current no-input blank follow-up set further: `Zany Golf` reaches visible output with repeated Start, while `Ex-Mutants`, `Pac-Man 2`, `Shadow of the Beast`, and `Tyrants` remain focused suspects.
- Corrected VDP V interrupt status latching across 68k interrupt acknowledge, allowing `Pac-Man 2` to leave its TRAP-based VBlank wait and render.
- Returned high unused bits from the Z80 bus request status byte so byte-sized whole-register polls work; `Shadow of the Beast` now reaches its intro splash.
- Refreshed the 571-ROM local compatibility dashboard and near-blank follow-up; all runs completed `ok`, 41 of 42 near-blank samples became visible later, and the remaining `Zany Golf` case is input-gated.

## 0.2.0 - 2026-05-18

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
- Added desktop portable storage detection and release package manifests/content verification.
- Hid desktop developer-only frame safety budget controls behind a persisted View menu toggle.
- Added a desktop `Help -> Diagnostics...` dialog with copyable app, package, storage, input, display, and cartridge details.
- Added a repository hygiene checker to the release gate to catch tracked ROMs, generated output, package artifacts, saves, and local reference audio.
- Hardened the release gate so native command failures stop the script and Virtua Racing summary links point to the generated reports.

## 0.1.0 - 2026-05-17

- Published the first GitHub release.
- Added reusable C# Genesis/Mega Drive emulation core, WinForms desktop frontend, and CLI diagnostics.
- Added desktop ROM loading, recent files, keyboard/gamepad input, input configuration, fullscreen, save states, SRAM/EEPROM persistence, and input movie recording/playback.
- Added VDP rendering support for planes, sprites, scrolling, windowing, DMA, CRAM/VSRAM snapshots, shadow/highlight, interlace, and raster-sensitive behavior.
- Added PSG/YM2612 audio, audio diagnostics, stem rendering, and reference comparison tooling.
- Added deterministic `.mdmovie` sample and checkpoint workflow.
- Added local screenshot showcase, documentation set, MIT license, desktop icon, release packaging script, and GitHub issue/PR templates.
