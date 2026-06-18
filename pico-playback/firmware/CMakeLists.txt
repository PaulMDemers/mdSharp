cmake_minimum_required(VERSION 3.13)

include(pico_sdk_import.cmake)

project(mdsharp_pico_playback C CXX ASM)

set(CMAKE_C_STANDARD 11)
set(CMAKE_CXX_STANDARD 17)

pico_sdk_init()

add_executable(mdsharp_pico_playback
    src/main.c
    src/playback.c
)

target_include_directories(mdsharp_pico_playback PRIVATE
    ${CMAKE_CURRENT_LIST_DIR}/src
)

target_compile_options(mdsharp_pico_playback PRIVATE
    -Wall
    -Wextra
    -Werror
)

set(MDSHARP_USE_DISCRETE_PULLDOWN_GATES "0" CACHE STRING "Use inverted GPIO polarity for discrete FET pull-down-gate hardware")
target_compile_definitions(mdsharp_pico_playback PRIVATE
    MDSHARP_USE_DISCRETE_PULLDOWN_GATES=${MDSHARP_USE_DISCRETE_PULLDOWN_GATES}
)

pico_enable_stdio_usb(mdsharp_pico_playback 1)
pico_enable_stdio_uart(mdsharp_pico_playback 0)

pico_add_extra_outputs(mdsharp_pico_playback)
