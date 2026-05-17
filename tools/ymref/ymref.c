#include <ctype.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "../Nuked-OPN2/ym3438.h"

#define YM_CLOCK_HZ 7670454.0
#define YM_INTERNAL_CLOCK_HZ (YM_CLOCK_HZ / 6.0)
#define SAMPLE_RATE 44100
#define PIN_SCALE 128
#define INTERNAL_SCALE 96

typedef enum
{
    output_pins,
    output_internal
} output_mode_t;

static void clock_chip(ym3438_t *chip, int clocks, output_mode_t mode, int64_t *left_sum, int64_t *right_sum, int *count)
{
    Bit16s buffer[2];
    for (int i = 0; i < clocks; i++)
    {
        OPN2_Clock(chip, buffer);
        if (left_sum != NULL)
        {
            if (mode == output_internal)
            {
                int left = 0;
                int right = 0;
                for (int channel = 0; channel < 6; channel++)
                {
                    if (chip->pan_l[channel])
                    {
                        left += chip->ch_out[channel];
                    }

                    if (chip->pan_r[channel])
                    {
                        right += chip->ch_out[channel];
                    }
                }

                *left_sum += left;
                *right_sum += right;
            }
            else
            {
                *left_sum += buffer[0];
                *right_sum += buffer[1];
            }

            (*count)++;
        }
    }
}

static void settle_write(ym3438_t *chip)
{
    clock_chip(chip, 48, output_pins, NULL, NULL, NULL);
}

static void write_wav_header(FILE *file, uint32_t sample_count)
{
    uint32_t data_bytes = sample_count * 2u * sizeof(int16_t);
    uint32_t riff_size = 36u + data_bytes;
    uint16_t audio_format = 1;
    uint16_t channels = 2;
    uint32_t sample_rate = SAMPLE_RATE;
    uint16_t bits_per_sample = 16;
    uint16_t block_align = channels * bits_per_sample / 8;
    uint32_t byte_rate = sample_rate * block_align;

    fwrite("RIFF", 1, 4, file);
    fwrite(&riff_size, sizeof(riff_size), 1, file);
    fwrite("WAVEfmt ", 1, 8, file);
    uint32_t fmt_size = 16;
    fwrite(&fmt_size, sizeof(fmt_size), 1, file);
    fwrite(&audio_format, sizeof(audio_format), 1, file);
    fwrite(&channels, sizeof(channels), 1, file);
    fwrite(&sample_rate, sizeof(sample_rate), 1, file);
    fwrite(&byte_rate, sizeof(byte_rate), 1, file);
    fwrite(&block_align, sizeof(block_align), 1, file);
    fwrite(&bits_per_sample, sizeof(bits_per_sample), 1, file);
    fwrite("data", 1, 4, file);
    fwrite(&data_bytes, sizeof(data_bytes), 1, file);
}

static int parse_hex(const char *text, unsigned int *value)
{
    char *end = NULL;
    *value = (unsigned int)strtoul(text, &end, 0);
    return end != text;
}

static int render_samples(ym3438_t *chip, FILE *wav, int samples, double *clock_carry, output_mode_t mode)
{
    for (int sample = 0; sample < samples; sample++)
    {
        *clock_carry += YM_INTERNAL_CLOCK_HZ / SAMPLE_RATE;
        int clocks = (int)(*clock_carry);
        *clock_carry -= clocks;
        if (clocks <= 0)
        {
            clocks = 1;
        }

        int64_t left_sum = 0;
        int64_t right_sum = 0;
        int count = 0;
        clock_chip(chip, clocks, mode, &left_sum, &right_sum, &count);
        int scale = mode == output_internal ? INTERNAL_SCALE : PIN_SCALE;
        int left = count > 0 ? (int)(left_sum / count) * scale : 0;
        int right = count > 0 ? (int)(right_sum / count) * scale : 0;
        if (left > INT16_MAX) left = INT16_MAX;
        if (left < INT16_MIN) left = INT16_MIN;
        if (right > INT16_MAX) right = INT16_MAX;
        if (right < INT16_MIN) right = INT16_MIN;
        int16_t out[2] = { (int16_t)left, (int16_t)right };
        fwrite(out, sizeof(int16_t), 2, wav);
    }

    return samples;
}

int main(int argc, char **argv)
{
    if (argc < 3)
    {
        fprintf(stderr, "Usage: ymref <script.txt> <output.wav> [pins|internal]\n");
        return 1;
    }

    FILE *script = fopen(argv[1], "rb");
    if (script == NULL)
    {
        perror(argv[1]);
        return 1;
    }

    FILE *wav = fopen(argv[2], "wb+");
    if (wav == NULL)
    {
        perror(argv[2]);
        fclose(script);
        return 1;
    }

    write_wav_header(wav, 0);

    ym3438_t chip;
    OPN2_SetChipType(ym3438_mode_ym2612);
    OPN2_Reset(&chip);
    output_mode_t mode = output_pins;
    if (argc >= 4)
    {
        if (strcmp(argv[3], "internal") == 0)
        {
            mode = output_internal;
        }
        else if (strcmp(argv[3], "pins") != 0)
        {
            fprintf(stderr, "Unknown output mode '%s'. Use pins or internal.\n", argv[3]);
            return 1;
        }
    }

    double clock_carry = 0.0;
    uint32_t samples_written = 0;
    char line[256];
    int line_number = 0;

    while (fgets(line, sizeof(line), script) != NULL)
    {
        line_number++;
        char *cursor = line;
        while (isspace((unsigned char)*cursor))
        {
            cursor++;
        }

        if (*cursor == '\0' || *cursor == '#')
        {
            continue;
        }

        char command[32];
        char arg1[64];
        char arg2[64];
        command[0] = arg1[0] = arg2[0] = '\0';
        int parts = sscanf(cursor, "%31s %63s %63s", command, arg1, arg2);
        if (parts <= 0)
        {
            continue;
        }

        if (strcmp(command, "write") == 0 || strcmp(command, "w") == 0)
        {
            unsigned int port;
            unsigned int value;
            if (parts < 3 || !parse_hex(arg1, &port) || !parse_hex(arg2, &value))
            {
                fprintf(stderr, "Invalid write at line %d\n", line_number);
                return 1;
            }

            OPN2_Write(&chip, port & 3u, value & 0xffu);
            settle_write(&chip);
        }
        else if (strcmp(command, "render") == 0 || strcmp(command, "r") == 0)
        {
            unsigned int samples;
            if (parts < 2 || !parse_hex(arg1, &samples))
            {
                fprintf(stderr, "Invalid render at line %d\n", line_number);
                return 1;
            }

            samples_written += (uint32_t)render_samples(&chip, wav, (int)samples, &clock_carry, mode);
        }
        else if (strcmp(command, "reset") == 0)
        {
            OPN2_Reset(&chip);
            clock_carry = 0.0;
        }
        else
        {
            fprintf(stderr, "Unknown command '%s' at line %d\n", command, line_number);
            return 1;
        }
    }

    fseek(wav, 0, SEEK_SET);
    write_wav_header(wav, samples_written);
    fclose(wav);
    fclose(script);
    fprintf(stdout, "Wrote %u sample(s) to %s\n", samples_written, argv[2]);
    return 0;
}
