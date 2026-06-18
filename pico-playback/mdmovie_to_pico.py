#!/usr/bin/env python3
"""Convert mdSharp .mdmovie files into Pico playback command data."""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any


BUTTONS = [
    ("UP", 1 << 0),
    ("DOWN", 1 << 1),
    ("LEFT", 1 << 2),
    ("RIGHT", 1 << 3),
    ("A", 1 << 4),
    ("B", 1 << 5),
    ("C", 1 << 6),
    ("START", 1 << 7),
    ("X", 1 << 8),
    ("Y", 1 << 9),
    ("Z", 1 << 10),
    ("MODE", 1 << 11),
]

THREE_BUTTON_MASK = 0x00FF
SIX_BUTTON_MASK = 0x0F00


@dataclass(frozen=True)
class Run:
    start_frame: int
    frames: int
    buttons: int


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Convert an mdSharp .mdmovie JSON file into Pico playback commands."
    )
    parser.add_argument("movie", type=Path, help="Input .mdmovie file")
    parser.add_argument(
        "-o",
        "--output",
        type=Path,
        help="Output path. Defaults to stdout.",
    )
    parser.add_argument(
        "--format",
        choices=["c-header", "json", "csv"],
        default="c-header",
        help="Output format. Default: c-header.",
    )
    parser.add_argument(
        "--player",
        choices=["1", "2"],
        default="1",
        help="Which movie player port to export. Default: 1.",
    )
    parser.add_argument(
        "--symbol",
        default=None,
        help="C symbol prefix for c-header output. Default derives from the movie filename.",
    )
    parser.add_argument(
        "--fps",
        default="ntsc",
        help="Playback frame rate tag or value. Use ntsc, pal, or a numeric Hz value. Default: ntsc.",
    )
    parser.add_argument(
        "--strict-contiguous",
        action="store_true",
        help="Fail if recorded frame numbers are not initialFrame..initialFrame+N-1.",
    )
    return parser.parse_args()


def load_movie(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8-sig") as handle:
        movie = json.load(handle)

    version = movie.get("version")
    if version not in (1, 2):
        raise ValueError(f"Unsupported mdmovie version: {version!r}")

    frames = movie.get("frames")
    if not isinstance(frames, list):
        raise ValueError("Movie does not contain a frames array.")

    frames.sort(key=lambda item: int(item.get("frame", 0)))
    normalize_legacy_buttons(frames)
    return movie


def normalize_legacy_buttons(frames: list[dict[str, Any]]) -> None:
    for frame in frames:
        legacy = int(frame.get("buttons", 0) or 0)
        if legacy == 0:
            continue
        if int(frame.get("player1Buttons", 0) or 0) == 0:
            frame["player1Buttons"] = legacy
        if int(frame.get("player2Buttons", 0) or 0) == 0:
            frame["player2Buttons"] = legacy


def select_masks(
    movie: dict[str, Any], player_key: str, strict_contiguous: bool
) -> tuple[int, list[int], list[str]]:
    initial_frame = int(movie.get("initialFrame", 0) or 0)
    warnings: list[str] = []
    masks: list[int] = []

    for index, frame in enumerate(movie["frames"]):
        expected_frame = initial_frame + index
        recorded_frame = int(frame.get("frame", expected_frame))
        if recorded_frame != expected_frame:
            message = (
                f"frame entry {index} has frame={recorded_frame}, "
                f"expected {expected_frame}; mdSharp playback is index-based."
            )
            if strict_contiguous:
                raise ValueError(message)
            warnings.append(message)

        masks.append(int(frame.get(player_key, 0) or 0))

    return initial_frame, masks, warnings


def build_runs(initial_frame: int, masks: list[int]) -> list[Run]:
    if not masks:
        return []

    runs: list[Run] = []
    start = initial_frame
    current = masks[0]
    length = 1

    for index, mask in enumerate(masks[1:], start=1):
        if mask == current:
            length += 1
            continue
        runs.append(Run(start, length, current))
        start = initial_frame + index
        current = mask
        length = 1

    runs.append(Run(start, length, current))
    return runs


def button_names(mask: int) -> str:
    names = [name for name, bit in BUTTONS if (mask & bit) != 0]
    return "+".join(names) if names else "NONE"


def c_identifier(value: str) -> str:
    identifier = re.sub(r"[^0-9A-Za-z_]", "_", value)
    identifier = re.sub(r"_+", "_", identifier).strip("_").lower()
    if not identifier or identifier[0].isdigit():
        identifier = f"movie_{identifier}"
    return identifier


def fps_value(tag: str) -> float:
    lowered = tag.lower()
    if lowered == "ntsc":
        return 60.0 / 1.001
    if lowered == "pal":
        return 50.0
    return float(tag)


def render_c_header(movie: dict[str, Any], runs: list[Run], symbol: str, fps: str) -> str:
    rate = fps_value(fps)
    lines = [
        "#pragma once",
        "",
        "#include <stdint.h>",
        "",
        "#ifndef MDSHARP_PICO_MOVIE_COMMAND_DEFINED",
        "#define MDSHARP_PICO_MOVIE_COMMAND_DEFINED",
        "typedef struct {",
        "    uint32_t frames;",
        "    uint16_t buttons;",
        "} MdMovieCommand;",
        "#endif",
        "",
        f"#define {symbol.upper()}_FRAME_RATE_HZ {rate:.9f}f",
        f"#define {symbol.upper()}_INITIAL_FRAME {int(movie.get('initialFrame', 0) or 0)}u",
        f"#define {symbol.upper()}_COMMAND_COUNT {len(runs)}u",
        "",
        f"static const MdMovieCommand {symbol}_commands[] = {{",
    ]

    for run in runs:
        lines.append(
            f"    {{ {run.frames}u, 0x{run.buttons:04X}u }},"
            f" /* frame {run.start_frame}: {button_names(run.buttons)} */"
        )

    lines.extend(
        [
            "};",
            "",
        ]
    )
    return "\n".join(lines)


def render_json(movie: dict[str, Any], runs: list[Run], player: str, fps: str) -> str:
    output = {
        "source": {
            "emulator": movie.get("emulator"),
            "version": movie.get("version"),
            "romName": movie.get("romName"),
            "romProductCode": movie.get("romProductCode"),
            "romSha256": movie.get("romSha256"),
            "initialFrame": movie.get("initialFrame", 0),
            "player": int(player),
        },
        "frameRateHz": fps_value(fps),
        "commands": [
            {
                "startFrame": run.start_frame,
                "frames": run.frames,
                "buttons": run.buttons,
                "buttonsHex": f"0x{run.buttons:04X}",
                "names": button_names(run.buttons),
            }
            for run in runs
        ],
    }
    return json.dumps(output, indent=2)


def render_csv(runs: list[Run]) -> str:
    lines = ["run,start_frame,frames,buttons,buttons_hex,names"]
    for index, run in enumerate(runs):
        lines.append(
            f'{index},{run.start_frame},{run.frames},{run.buttons},0x{run.buttons:04X},"{button_names(run.buttons)}"'
        )
    return "\n".join(lines) + "\n"


def write_output(path: Path | None, text: str) -> None:
    if path is None:
        sys.stdout.write(text)
        if not text.endswith("\n"):
            sys.stdout.write("\n")
        return

    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def main() -> int:
    args = parse_args()
    movie = load_movie(args.movie)
    player_key = "player1Buttons" if args.player == "1" else "player2Buttons"
    initial_frame, masks, warnings = select_masks(
        movie, player_key, args.strict_contiguous
    )
    runs = build_runs(initial_frame, masks)

    used_mask = 0
    for mask in masks:
        used_mask |= mask
    if (used_mask & SIX_BUTTON_MASK) != 0:
        warnings.append(
            "movie uses X/Y/Z/Mode bits; Pico firmware must enable six-button handshake emulation."
        )
    if (used_mask & ~0x0FFF) != 0:
        warnings.append(f"movie uses unknown button bits: 0x{used_mask & ~0x0FFF:04X}")

    for warning in warnings:
        print(f"warning: {warning}", file=sys.stderr)

    symbol = c_identifier(args.symbol or args.movie.stem)
    if args.format == "c-header":
        rendered = render_c_header(movie, runs, symbol, args.fps)
    elif args.format == "json":
        rendered = render_json(movie, runs, args.player, args.fps)
    else:
        rendered = render_csv(runs)

    write_output(args.output, rendered)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"error: {exc}", file=sys.stderr)
        raise SystemExit(1)
