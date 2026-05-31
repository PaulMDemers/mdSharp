# Project Hygiene

Emulator projects accumulate ROMs, screenshots, traces, WAVs, save states, and scratch tools quickly. Good hygiene keeps the repository usable.

## Repository Structure

Recommended layout:

```text
src/
  Core/
  Cli/
  Desktop-or-Frontend/
tests/
docs/
artifacts/          ignored generated output
scratch/            ignored local helpers
roms/               ignored retail/private media
test-roms/          ignored unless explicitly redistributable
```

Keep public diagnostic ROM links in docs instead of committing copyrighted ROMs.

## Ignore Generated And Private Files

Ignore:

- retail ROM collections
- BIOS files
- save RAM
- save states
- local reference recordings
- generated screenshots
- WAV dumps
- compatibility sweep outputs
- trace logs
- package outputs
- temporary disassemblies

Commit only redistributable assets and intentionally curated screenshots.

## Documentation To Maintain

At minimum:

- README
- architecture overview
- build and test instructions
- CLI reference
- testing workflow
- compatibility workflow
- audio/video workflow
- input/movie workflow if supported
- test ROM/source policy
- packaging and release checklist
- roadmap and known limitations
- license

Docs should describe current behavior, not aspirational behavior. Plans belong in roadmap or subsystem notes.

## Release Discipline

Before a release:

- run full tests
- run a representative compatibility sweep
- run benchmark input movies
- verify save-state compatibility notes
- verify package contents exclude private files
- update screenshots only if they are real current output
- update compatibility status honestly
- update release notes
- tag after artifacts are ready

Do not publish generated local paths, ROM names from private collections, or reference files that cannot be redistributed.

## Legal Boundaries

Emulator source code is not the same as copyrighted software for the emulated system.

Keep these separate:

- source code and original docs
- public-domain or permissively licensed test ROMs
- copyrighted retail ROMs
- BIOS images
- SDK samples with unclear redistribution status
- recordings or screenshots that may need attribution or caution

Document where users can find public diagnostic material. Do not ship retail software.

## Development Culture

Healthy emulator development is evidence-driven:

- cite references in notes when they determine behavior
- add a regression for each meaningful hardware fix
- keep subjective impressions tied to screenshots, audio, or traces
- write down open questions
- review compatibility status after broad changes

This matters more as the emulator grows, because old assumptions become hard to rediscover.

