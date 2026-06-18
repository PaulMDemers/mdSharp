#pragma once

#include <stdbool.h>
#include <stdint.h>

#define GENESIS_BUTTON_UP    0x0001u
#define GENESIS_BUTTON_DOWN  0x0002u
#define GENESIS_BUTTON_LEFT  0x0004u
#define GENESIS_BUTTON_RIGHT 0x0008u
#define GENESIS_BUTTON_A     0x0010u
#define GENESIS_BUTTON_B     0x0020u
#define GENESIS_BUTTON_C     0x0040u
#define GENESIS_BUTTON_START 0x0080u
#define GENESIS_BUTTON_X     0x0100u
#define GENESIS_BUTTON_Y     0x0200u
#define GENESIS_BUTTON_Z     0x0400u
#define GENESIS_BUTTON_MODE  0x0800u

typedef enum {
    PLAYBACK_LINE_D0 = 1u << 0,
    PLAYBACK_LINE_D1 = 1u << 1,
    PLAYBACK_LINE_D2 = 1u << 2,
    PLAYBACK_LINE_D3 = 1u << 3,
    PLAYBACK_LINE_TL = 1u << 4,
    PLAYBACK_LINE_TR = 1u << 5,
} PlaybackLine;

uint32_t playback_three_button_lines(uint16_t buttons, bool th_high);
