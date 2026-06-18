# mdSharp Pico Playback Firmware

This is the first Raspberry Pi Pico SDK firmware prototype for hardware
controller playback.

## Generate Movie Data

The firmware includes `src/movie_data.h`. Replace it with generated movie data:

```powershell
python ..\mdmovie_to_pico.py ..\..\docs\assets\input-movies\sonic-green-hill-sample.mdmovie --symbol movie -o src\movie_data.h
```

The symbol must be `movie`; the firmware expects:

- `MOVIE_FRAME_RATE_HZ`
- `MOVIE_INITIAL_FRAME`
- `MOVIE_COMMAND_COUNT`
- `movie_commands`

## Build

Install the Raspberry Pi Pico SDK and point `PICO_SDK_PATH` at it:

```powershell
$env:PICO_SDK_PATH = "C:\path\to\pico-sdk"
cmake -S . -B build
cmake --build build
```

Copy `build\mdsharp_pico_playback.uf2` to the Pico in BOOTSEL mode.

## GPIO Map

| Pico GPIO | Genesis DB-9 pin | Signal |
| ---: | ---: | --- |
| GP2 | 1 | D0 |
| GP3 | 2 | D1 |
| GP4 | 3 | D2 |
| GP5 | 4 | D3 |
| GP6 | 6 | TL |
| GP7 | 9 | TR |
| GP8 | 7 | TH input through divider |

The preferred hardware path is a 3.3 V / 5 V logic level converter. In that
mode, the firmware drives GPIO low when the Genesis line should be low and GPIO
high when it should be released/high.

If using the older discrete pull-down-gate circuit instead, build with:

```powershell
cmake -S . -B build -DMDSHARP_USE_DISCRETE_PULLDOWN_GATES=1
```

## Serial Commands

Open the Pico USB serial port after flashing.

| Command | Meaning |
| --- | --- |
| `start` / `s` | Begin playback |
| `pause` / `p` | Pause playback |
| `reset` / `r` | Rewind and pause |
| `status` / `?` | Print current state |
| `help` / `h` | Print command list |

## Current Limitations

- Three-button protocol only.
- Timing is Pico-clock based; there is no VBlank signal on the controller port.
- Start alignment is manual through USB serial for now.
