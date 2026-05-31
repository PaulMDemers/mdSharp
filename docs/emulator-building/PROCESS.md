# Process

## Research First, But Keep It Actionable

Start with primary sources whenever possible:

- official hardware manuals
- CPU programmer manuals
- memory maps
- service manuals
- technical notes from emulator authors
- open-source emulator behavior, used for comparison rather than direct copying
- public test ROMs and SDK samples

Extract addresses, bit meanings, reset states, timing relationships, and known quirks into project notes. Avoid trying to fully understand the entire machine before implementing anything. The goal is enough structure to build the first verified slice.

## Build In Vertical Slices

An emulator becomes useful faster when each slice can be run end to end:

1. Load media and parse metadata.
2. Reset CPU and bus.
3. Execute enough instructions to reach a known loop.
4. Render a blank or diagnostic frame.
5. Add input, audio, storage, and timing incrementally.

Each slice should include diagnostics. For example, a video slice should expose register writes and non-background pixel counts; an audio slice should expose chip writes and generated sample energy.

## Keep The Core Pure

The core should not know about windows, menus, file dialogs, audio devices, or user preferences. It should expose:

- deterministic run methods
- serializable state
- media and save-data interfaces
- controller state inputs
- video and audio output buffers
- optional trace hooks

This keeps command-line regression, desktop play, automated tests, and future frontends using the same hardware model.

## Use Real Software Early

Synthetic tests are essential, but real games and demos reveal integration bugs. Use a small compatibility set with different stress patterns:

- early simple titles
- titles with raster effects
- titles with complex audio drivers
- titles with unusual input or save hardware
- titles that use known hardware edge cases
- SDK samples or public diagnostics

Do not wait for perfect CPU or video implementation before running real software. The first crash is useful evidence.

## Add Features Only When They Reduce Friction

Tooling features should pay for themselves quickly. Good early examples:

- render a specific frame
- dump CPU trace around a PC
- run a folder sweep
- write screenshot output
- record and replay inputs
- compare audio against a reference
- print cartridge/media diagnostics
- save and restore machine state

Avoid polishing the UI before the debugging loop is strong. A convenient frontend is valuable later, but core progress usually accelerates through CLI tools first.

## Define Ratings Conservatively

Compatibility labels should be based on observable behavior:

- Does it boot?
- Does it render non-background pixels?
- Does input work?
- Does audio activity exist?
- Does it survive menus and gameplay?
- Are there CPU exceptions, unhandled opcodes, or fallback render paths?
- Is the result visually and audibly acceptable?

Do not call something "supported" because it reaches a title screen once. Ratings should move upward only when repeated tests support the claim.

