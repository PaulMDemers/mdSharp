# mdSharp Pico Playback

This folder is the planning and tooling area for turning mdSharp `.mdmovie`
recordings into Raspberry Pi Pico controller-playback data for a real Sega
Genesis / Mega Drive controller port.

The first concrete tool is `mdmovie_to_pico.py`. It reads mdSharp movie JSON,
selects player 1 or player 2, and emits run-length encoded playback commands.

```powershell
python pico-playback\mdmovie_to_pico.py docs\assets\input-movies\sonic-green-hill-sample.mdmovie -o pico-playback\sonic-green-hill-sample.h
```

Other useful forms:

```powershell
python pico-playback\mdmovie_to_pico.py movie.mdmovie --format json -o movie.commands.json
python pico-playback\mdmovie_to_pico.py movie.mdmovie --format csv -o movie.commands.csv
python pico-playback\mdmovie_to_pico.py movie.mdmovie --player 2 -o player2.h
```

For the included Pico firmware, generate the header with the stable `movie`
symbol:

```powershell
python pico-playback\mdmovie_to_pico.py docs\assets\input-movies\sonic-green-hill-sample.mdmovie --symbol movie -o pico-playback\firmware\src\movie_data.h
```

## Firmware Prototype

`firmware/` contains a Raspberry Pi Pico SDK C project for the first
three-button playback milestone.

Build outline:

```powershell
$env:PICO_SDK_PATH = "C:\path\to\pico-sdk"
cmake -S pico-playback\firmware -B pico-playback\firmware\build
cmake --build pico-playback\firmware\build
```

Flash the generated `.uf2` to the Pico. On boot, the firmware exposes USB
serial commands:

| Command | Meaning |
| --- | --- |
| `start` or `s` | Start playback from the current loaded command |
| `pause` or `p` | Pause playback and keep outputting the current frame |
| `reset` or `r` | Rewind to `MOVIE_INITIAL_FRAME` and pause |
| `status` or `?` | Print current frame, command index, and button mask |
| `help` or `h` | Print command list |

The firmware currently implements three-button protocol output. Movies using
X/Y/Z/Mode will convert, but they need the planned six-button handshake
milestone before those buttons work on hardware.

## Movie Input Shape

mdSharp movie files are JSON. Version 2 stores ROM metadata, `initialFrame`,
and one frame entry per emulated frame with `player1Buttons` and
`player2Buttons` integer masks.

Button bits match `src/MdSharp.Core/Input/GenesisButton.cs`:

| Bit | Mask | Button |
| --- | ---: | --- |
| 0 | `0x0001` | Up |
| 1 | `0x0002` | Down |
| 2 | `0x0004` | Left |
| 3 | `0x0008` | Right |
| 4 | `0x0010` | A |
| 5 | `0x0020` | B |
| 6 | `0x0040` | C |
| 7 | `0x0080` | Start |
| 8 | `0x0100` | X |
| 9 | `0x0200` | Y |
| 10 | `0x0400` | Z |
| 11 | `0x0800` | Mode |

The converter preserves those masks in the command data. The Pico firmware is
responsible for translating the current mask into active-low controller-port
states based on the TH/select line.

## Hardware Direction

The Pico should emulate a controller through a 3.3 V / 5 V logic level
converter, not drive the Genesis bus directly.

- Genesis pin 7, TH/select, is a console output. Level shift it down before the
  Pico reads it.
- Genesis data pins 1, 2, 3, 4, 6, and 9 are controller outputs. Route them
  through the level converter's high-voltage side.
- Use the console port's +5 V only as the controller-port pull-up rail unless
  the power path is deliberately designed and tested.
- Share ground between the Genesis port and the Pico.

See `SCHEMATIC.md` for the proposed first hardware revision and `PLAN.md` for
the firmware and validation plan.
