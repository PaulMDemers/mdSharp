#pragma once

#include <stdint.h>

#ifndef MDSHARP_PICO_MOVIE_COMMAND_DEFINED
#define MDSHARP_PICO_MOVIE_COMMAND_DEFINED
typedef struct {
    uint32_t frames;
    uint16_t buttons;
} MdMovieCommand;
#endif

#define MOVIE_FRAME_RATE_HZ 59.940059940f
#define MOVIE_INITIAL_FRAME 0u
#define MOVIE_COMMAND_COUNT 0u

static const MdMovieCommand movie_commands[] = {
    { 0u, 0x0000u },
};
