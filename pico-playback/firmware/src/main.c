#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>

#include "hardware/gpio.h"
#include "pico/stdlib.h"

#include "movie_data.h"
#include "playback.h"

#define GPIO_D0 2u
#define GPIO_D1 3u
#define GPIO_D2 4u
#define GPIO_D3 5u
#define GPIO_TL 6u
#define GPIO_TR 7u
#define GPIO_TH 8u

#define LINE_COUNT 6u
#define SERIAL_BUFFER_LENGTH 32u

#ifndef MDSHARP_USE_DISCRETE_PULLDOWN_GATES
#define MDSHARP_USE_DISCRETE_PULLDOWN_GATES 0
#endif

static const uint line_gpios[LINE_COUNT] = {
    GPIO_D0,
    GPIO_D1,
    GPIO_D2,
    GPIO_D3,
    GPIO_TL,
    GPIO_TR,
};

typedef struct {
    bool running;
    uint32_t command_index;
    uint32_t frames_remaining;
    uint32_t frame_number;
    uint16_t buttons;
    double next_frame_us;
    double frame_period_us;
} PlaybackState;

static void put_genesis_line(uint gpio, bool line_low)
{
#if MDSHARP_USE_DISCRETE_PULLDOWN_GATES
    gpio_put(gpio, line_low ? 1 : 0);
#else
    gpio_put(gpio, line_low ? 0 : 1);
#endif
}

static void release_all_lines(void)
{
    for (uint i = 0; i < LINE_COUNT; i++) {
        put_genesis_line(line_gpios[i], false);
    }
}

static void apply_low_lines(uint32_t low_lines)
{
    for (uint i = 0; i < LINE_COUNT; i++) {
        put_genesis_line(line_gpios[i], (low_lines & (1u << i)) != 0);
    }
}

static void configure_gpio(void)
{
    for (uint i = 0; i < LINE_COUNT; i++) {
        gpio_init(line_gpios[i]);
        put_genesis_line(line_gpios[i], false);
        gpio_set_dir(line_gpios[i], GPIO_OUT);
    }

    gpio_init(GPIO_TH);
    gpio_set_dir(GPIO_TH, GPIO_IN);
    release_all_lines();
}

static void load_current_command(PlaybackState *state)
{
    if (state->command_index >= MOVIE_COMMAND_COUNT) {
        state->buttons = 0;
        state->frames_remaining = 0;
        state->running = false;
        return;
    }

    state->buttons = movie_commands[state->command_index].buttons;
    state->frames_remaining = movie_commands[state->command_index].frames;
    if (state->frames_remaining == 0) {
        state->command_index++;
        load_current_command(state);
    }
}

static void reset_playback(PlaybackState *state)
{
    state->running = false;
    state->command_index = 0;
    state->frames_remaining = 0;
    state->frame_number = MOVIE_INITIAL_FRAME;
    state->buttons = 0;
    state->next_frame_us = (double)time_us_64();
    state->frame_period_us = 1000000.0 / (double)MOVIE_FRAME_RATE_HZ;
    load_current_command(state);
}

static void start_playback(PlaybackState *state)
{
    if (MOVIE_COMMAND_COUNT == 0) {
        printf("No movie commands loaded. Generate src/movie_data.h first.\n");
        return;
    }

    state->running = true;
    state->next_frame_us = (double)time_us_64() + state->frame_period_us;
}

static void advance_one_frame(PlaybackState *state)
{
    if (!state->running || state->command_index >= MOVIE_COMMAND_COUNT) {
        return;
    }

    if (state->frames_remaining > 0) {
        state->frames_remaining--;
        state->frame_number++;
    }

    if (state->frames_remaining == 0) {
        state->command_index++;
        load_current_command(state);
    }
}

static void service_frame_timer(PlaybackState *state)
{
    if (!state->running) {
        return;
    }

    double now = (double)time_us_64();
    while (now >= state->next_frame_us) {
        advance_one_frame(state);
        state->next_frame_us += state->frame_period_us;
    }
}

static void print_status(const PlaybackState *state)
{
    printf(
        "status running=%u frame=%lu command=%lu/%u remaining=%lu buttons=0x%04x rate=%.6f\n",
        state->running ? 1u : 0u,
        (unsigned long)state->frame_number,
        (unsigned long)state->command_index,
        (unsigned)MOVIE_COMMAND_COUNT,
        (unsigned long)state->frames_remaining,
        state->buttons,
        (double)MOVIE_FRAME_RATE_HZ);
}

static void print_help(void)
{
    printf("commands: start | pause | reset | status | help\n");
}

static void handle_command(PlaybackState *state, const char *command)
{
    if (strcmp(command, "start") == 0 || strcmp(command, "s") == 0) {
        start_playback(state);
        print_status(state);
    } else if (strcmp(command, "pause") == 0 || strcmp(command, "p") == 0) {
        state->running = false;
        print_status(state);
    } else if (strcmp(command, "reset") == 0 || strcmp(command, "r") == 0) {
        reset_playback(state);
        print_status(state);
    } else if (strcmp(command, "status") == 0 || strcmp(command, "?") == 0) {
        print_status(state);
    } else if (strcmp(command, "help") == 0 || strcmp(command, "h") == 0) {
        print_help();
    } else if (command[0] != '\0') {
        printf("unknown command: %s\n", command);
        print_help();
    }
}

static void service_serial(PlaybackState *state)
{
    static char buffer[SERIAL_BUFFER_LENGTH];
    static size_t length = 0;

    int ch = getchar_timeout_us(0);
    while (ch != PICO_ERROR_TIMEOUT) {
        if (ch == '\r' || ch == '\n') {
            buffer[length] = '\0';
            handle_command(state, buffer);
            length = 0;
        } else if (length + 1 < sizeof(buffer)) {
            buffer[length++] = (char)ch;
        }

        ch = getchar_timeout_us(0);
    }
}

int main(void)
{
    stdio_init_all();
    configure_gpio();

    PlaybackState state;
    reset_playback(&state);

    sleep_ms(1500);
    printf("mdSharp Pico Playback ready\n");
    printf("commands loaded: %u, frame rate: %.6f Hz\n",
        (unsigned)MOVIE_COMMAND_COUNT,
        (double)MOVIE_FRAME_RATE_HZ);
    print_help();

    while (true) {
        bool th_high = gpio_get(GPIO_TH) != 0;
        uint32_t low_lines = playback_three_button_lines(state.buttons, th_high);
        apply_low_lines(low_lines);

        service_frame_timer(&state);
        service_serial(&state);
        tight_loop_contents();
    }
}
