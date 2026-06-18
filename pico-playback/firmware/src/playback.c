#include "playback.h"

uint32_t playback_three_button_lines(uint16_t buttons, bool th_high)
{
    uint32_t low_lines = 0;

    if ((buttons & GENESIS_BUTTON_UP) != 0) {
        low_lines |= PLAYBACK_LINE_D0;
    }

    if ((buttons & GENESIS_BUTTON_DOWN) != 0) {
        low_lines |= PLAYBACK_LINE_D1;
    }

    if (th_high) {
        if ((buttons & GENESIS_BUTTON_LEFT) != 0) {
            low_lines |= PLAYBACK_LINE_D2;
        }

        if ((buttons & GENESIS_BUTTON_RIGHT) != 0) {
            low_lines |= PLAYBACK_LINE_D3;
        }

        if ((buttons & GENESIS_BUTTON_B) != 0) {
            low_lines |= PLAYBACK_LINE_TL;
        }

        if ((buttons & GENESIS_BUTTON_C) != 0) {
            low_lines |= PLAYBACK_LINE_TR;
        }
    } else {
        low_lines |= PLAYBACK_LINE_D2 | PLAYBACK_LINE_D3;

        if ((buttons & GENESIS_BUTTON_A) != 0) {
            low_lines |= PLAYBACK_LINE_TL;
        }

        if ((buttons & GENESIS_BUTTON_START) != 0) {
            low_lines |= PLAYBACK_LINE_TR;
        }
    }

    return low_lines;
}
