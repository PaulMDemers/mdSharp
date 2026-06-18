# Pico Playback Schematic Plan

This is a first hardware plan for one Genesis / Mega Drive controller port.
Duplicate the same data and TH interface for player 2.

Use a real 3.3 V / 5 V logic level converter between the Pico and the Genesis
controller port. Do not connect Pico GPIO directly to the controller port.

## DB-9 Controller-Port Signals

Genesis controller ports use a DE-9/DB-9 connector. Confirm connector
orientation with a continuity tester before wiring; plug and socket views are
mirrored.

| DB-9 pin | Genesis name | Direction here | Three-button meaning |
| ---: | --- | --- | --- |
| 1 | D0 | Pico to console | Up |
| 2 | D1 | Pico to console | Down |
| 3 | D2 | Pico to console | Left when TH high, low/id when TH low |
| 4 | D3 | Pico to console | Right when TH high, low/id when TH low |
| 5 | +5 V | Console to adapter | Pull-up rail only |
| 6 | TL | Pico to console | B when TH high, A when TH low |
| 7 | TH / Select | Console to Pico | Select line |
| 8 | GND | Common | Ground |
| 9 | TR | Pico to console | C when TH high, Start when TH low |

All controller data signals are active low.

## Required Components

- Raspberry Pi Pico or Pico-compatible RP2040 board.
- One 8-channel 3.3 V / 5 V logic level converter per controller port.
- Male DE-9/DB-9 connector or controller extension cable breakout.
- Common-ground wiring between Pico, level converter, and Genesis port.
- Optional inline 100-220 ohm resistors on controller signal lines for bring-up.

Recommended level converter type:

- A bidirectional MOSFET/open-drain style level-shifter board, commonly built
  around BSS138 MOSFETs, is a good fit for this first prototype.
- Avoid connecting through a raw Pico GPIO or a push-pull auto-direction part
  unless its datasheet behavior has been checked with the Genesis pull-ups and
  polling rate.

## Proposed Circuit

Wire the level converter with the Pico on the low-voltage side and the Genesis
controller port on the high-voltage side:

```text
Pico 3V3 ---------------- LV
Genesis DB-9 pin 5 +5V -- HV
Pico GND ---------------- GND ---------------- Genesis DB-9 pin 8 GND

Pico GPIO ---- LV channel N  level converter  HV channel N ---- Genesis signal
```

Use one converter channel for each signal:

| Level-shifter channel | Pico side | Genesis side |
| ---: | --- | --- |
| 1 | GP2 | DB-9 pin 1 / D0 |
| 2 | GP3 | DB-9 pin 2 / D1 |
| 3 | GP4 | DB-9 pin 3 / D2 |
| 4 | GP5 | DB-9 pin 4 / D3 |
| 5 | GP6 | DB-9 pin 6 / TL |
| 6 | GP7 | DB-9 pin 9 / TR |
| 7 | GP8 | DB-9 pin 7 / TH |

The firmware defaults to direct level-shifter polarity:

- Pico GPIO low means the Genesis signal is low.
- Pico GPIO high means the Genesis signal is high/released.

That matches the active-low controller protocol without exposing the Pico pins
to 5 V.

## Optional Discrete Pull-Down Alternative

If a level-converter board is unavailable, the older discrete pull-down design
can still be built, but treat it as an advanced fallback. Use one pull-down
stage per data line and keep the TH input level-shifted:

```text
Genesis +5 V
    |
   10k
    |
Genesis data pin ---- 100R ---- drain 2N7002/BSS138
                              source ---- GND
Pico GPIO ---- 100R ---- gate
                       |
                      100k
                       |
                      GND
```

With this fallback circuit, GPIO high pulls the Genesis line low. Build the
firmware with `MDSHARP_USE_DISCRETE_PULLDOWN_GATES=1`.

Suggested Pico GPIO assignment:

| Pico GPIO | DB-9 pin | Signal |
| ---: | ---: | --- |
| GP2 | 1 | D0 |
| GP3 | 2 | D1 |
| GP4 | 3 | D2 |
| GP5 | 4 | D3 |
| GP6 | 6 | TL |
| GP7 | 9 | TR |
| GP8 | 7 | TH input through divider |

If not using a level-converter channel for TH/select, level shift TH/select
down with a resistor divider:

```text
DB-9 pin 7 TH ---- 20k ---- Pico GP8
                            |
                           33k
                            |
                           GND
```

At 5 V TH high this gives about 3.1 V at the Pico input. Keep the Pico input
configured as high impedance.

Power and ground:

```text
DB-9 pin 8 GND ---------------- Pico GND
DB-9 pin 5 +5 V ---- level-converter HV
Pico powered from USB during development
```

Do not connect DB-9 +5 V to Pico VSYS in the first revision. A later
standalone revision can add a fused and diode-isolated power path after USB
back-power behavior is tested.

## Controller Protocol Mapping

For three-button mode:

| TH state | D0 | D1 | D2 | D3 | TL | TR |
| --- | --- | --- | --- | --- | --- | --- |
| High | Up | Down | Left | Right | B | C |
| Low | Up | Down | Low/id | Low/id | A | Start |

For six-button mode, the same pins are used. The firmware must count rapid TH
transitions and expose the six-button signature and extra buttons:

| Handshake state | TH | D0 | D1 | D2 | D3 | TL | TR |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Normal high | High | Up | Down | Left | Right | B | C |
| Normal low | Low | Up | Down | Low | Low | A | Start |
| Signature | Low | Low | Low | Low | Low | A | Start |
| Extra buttons | High | Z | Y | X | Mode | B | C |

The first firmware milestone should support three-button playback. The second
milestone should add this six-button handshake so movies using X/Y/Z/Mode can
play back on hardware.

## Safety Checklist

- Verify the console is off before plugging or unplugging the adapter.
- Scope TH and one data line before connecting all six data lines.
- Confirm Pico GPIO never sees raw 5 V; measure the Pico-side TH channel before
  plugging into the Pico.
- Confirm a Pico reset leaves every data line released high through pull-ups.
- Use current-limited bench power for the first standalone-power experiment.
