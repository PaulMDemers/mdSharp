# Contributing

mdSharp is early-stage emulator work. Strong contributions are small, testable, and tied to a specific hardware behavior or repeatable game issue.

## Development Loop

1. Reproduce the issue.
2. Capture a movie, screenshot sequence, trace, or small isolated test.
3. Make the smallest core change that explains the behavior.
4. Add a test when possible.
5. Run the relevant regression tools.
6. Document any new workflow or command that becomes useful.

## Code Style

- Keep `MdSharp.Core` independent of UI frameworks.
- Prefer explicit hardware concepts over generic abstractions.
- Keep game-specific diagnostics in the CLI, not the core.
- Avoid committing generated output.
- Avoid committing ROMs or reference audio.

## Tests

Run:

```powershell
dotnet build mdSharp.sln -c Release
dotnet test mdSharp.sln -c Release --no-build
```

For video or compatibility changes, run a focused ROM/movie regression.

For audio changes, run `--audio-regression`.

## Reporting Issues

Effective issue reports include:

- game title and region/revision
- ROM hash if possible
- expected behavior
- actual behavior
- frame number or input movie
- screenshot or audio clip when relevant
- command used to reproduce

Do not attach commercial ROMs.
