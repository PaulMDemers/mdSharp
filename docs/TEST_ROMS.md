# Test ROM Sources

mdSharp does not commit ROM images. Keep local ROM copies under `TestRoms/` or `roms/`; both folders are ignored except for their `.gitkeep` placeholders.

This document lists sources for recreating the public diagnostic ROM set used during development. Only download and use legally permitted ROMs. Retail games used for compatibility checks must come from lawful local sources and must stay local.

## Public Diagnostic ROMs

The most useful single index is the Exodus Mega Drive software page:

- [Exodus Mega Drive test ROM collection](https://techdocs.exodusemulator.com/Console/SegaMegaDrive/Software.html)

That page links to many of the public/homebrew diagnostics used while building mdSharp, including:

| Local name seen in development | Purpose | Upstream/source |
| --- | --- | --- |
| `VDPFIFOTesting.zip` | VDP port/FIFO access timing | [Exodus list](https://techdocs.exodusemulator.com/Console/SegaMegaDrive/Software.html), [Exodus article with VDPFIFOTesting link](https://www.exodusemulator.com/) |
| `SpriteMaskingTestRom.zip` | VDP sprite masking and overflow behavior | [Exodus list](https://techdocs.exodusemulator.com/Console/SegaMegaDrive/Software.html), [Sega Retro background page](https://segaretro.org/Sprite_Masking_and_Overflow_Test_ROM) |
| `Direct-Color-DMA.zip` | Direct Color DMA / raster-sensitive CRAM writes | [Exodus list](https://techdocs.exodusemulator.com/Console/SegaMegaDrive/Software.html) |
| `cram flicker.zip` | CRAM dot timing | [Exodus list](https://techdocs.exodusemulator.com/Console/SegaMegaDrive/Software.html) |
| `m68k_opcode_sizes.zip` | 68000 opcode size and illegal-size validation | [realmonster/smd_emu_tests](https://github.com/realmonster/smd_emu_tests), [SpritesMind discussion](https://gendev.spritesmind.net/forum/viewtopic.php?t=2699) |
| `bcd-verifier-u1.zip` | 68000 ABCD/SBCD/NBCD flag behavior | [Exodus list](https://techdocs.exodusemulator.com/Console/SegaMegaDrive/Software.html) |
| `memtest_68k.zip` | undefined 68000 memory-map reads | [Exodus list](https://techdocs.exodusemulator.com/Console/SegaMegaDrive/Software.html) |
| `FM Test by DevSter (PD).zip` | Basic YM2612 audio smoke test | [Exodus list](https://techdocs.exodusemulator.com/Console/SegaMegaDrive/Software.html) |
| `Graphics & Joystick Sampler by Charles Doty (PD).zip` | Early graphics/input smoke test | [Exodus list](https://techdocs.exodusemulator.com/Console/SegaMegaDrive/Software.html) |
| `Multitap - IO Sample Program (U) (Nov 28 1992).zip` | Multitap/controller I/O behavior | [Exodus list](https://techdocs.exodusemulator.com/Console/SegaMegaDrive/Software.html) |
| `Shadow-Highlight Test Program #2 (PD).zip` | VDP shadow/highlight rendering | [Exodus list](https://techdocs.exodusemulator.com/Console/SegaMegaDrive/Software.html), [SpritesMind discussion](https://gendev.spritesmind.net/forum/viewtopic.php?t=2692) |
| `TEST1536.zip` | 1536-color shadow/highlight palette demo | [Exodus list](https://techdocs.exodusemulator.com/Console/SegaMegaDrive/Software.html) |
| `Window Test by Fonzie (PD).zip` | Window plane and hscroll edge cases | [Exodus list](https://techdocs.exodusemulator.com/Console/SegaMegaDrive/Software.html) |
| `Window distortion bug.zip` | Window/hscroll distortion behavior | [Exodus list](https://techdocs.exodusemulator.com/Console/SegaMegaDrive/Software.html) |
| `titan-overdrive2.zip` / `titan-overdrivemegademo-v1.1-final.zip` | Hardware-stress demo content used as demanding compatibility targets | [Exodus list](https://techdocs.exodusemulator.com/Console/SegaMegaDrive/Software.html) |

## Retail Compatibility ROMs

Retail ROMs were used locally as compatibility targets, but are not redistributable through this repository. Keep them under `roms/` or another ignored local folder.

Common local compatibility targets have included:

- Sonic the Hedgehog series
- Streets of Rage
- Castlevania: Bloodlines
- Disney's Aladdin
- Disney's Toy Story
- Virtua Racing, with a local `svp.bin` coprocessor blob when needed

The repo intentionally tracks only source code, docs, placeholders, scripts, and test harness code. It does not track commercial ROMs, BIOS/coproc blobs, save RAM, save states, generated audio, screenshots, or reference emulator captures.
