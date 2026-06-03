using MdSharp.Core;
using MdSharp.Core.Audio;
using MdSharp.Core.Bus;
using MdSharp.Core.Cartridge;
using MdSharp.Core.Cpu.M68k;
using MdSharp.Core.Cpu.Sh2;
using MdSharp.Core.Input;
using MdSharp.Core.State;
using MdSharp.Core.ThirtyTwoX;
using MdSharp.Core.Video;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

const int DefaultInstructionBudget = 100_000;
const int SonicTitleMusicStartFrame = 559;
const double ThirtyTwoXSweepAdaptiveTimeLimitSeconds = 180.0;
string[] romExtensions = [".bin", ".md", ".gen", ".smd", ".rom", ".32x"];

if (args.Length == 1 && args[0] is "--version" or "-v")
{
    Console.WriteLine($"{AppInfo.Name} {AppInfo.DisplayVersion}");
    return;
}

if (args.Length == 0)
{
    Console.WriteLine($"{AppInfo.Name} {AppInfo.DisplayVersion} - Sega Genesis/Mega Drive emulator prototype");
    Console.WriteLine("Usage:");
    Console.WriteLine("  mdsharp --version");
    Console.WriteLine("  mdsharp <rom-file> [instructions]");
    Console.WriteLine("  mdsharp --sweep <rom-folder> [instructions]");
    Console.WriteLine("  mdsharp --cart-info <rom-file>");
    Console.WriteLine("  mdsharp --cart-scan <rom-folder> <output.csv>");
    Console.WriteLine("  mdsharp --32x-sh2-trace <rom-file> [instructions] [master|slave] [start-pc]");
    Console.WriteLine("  mdsharp --32x-live-sh2-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [master|slave|both] [pc-start] [pc-end] [max-lines] [start-frame]");
    Console.WriteLine("  mdsharp --32x-live-sh2-trace-state <rom-file> <state.mdss> <output.csv> [frames] [instructions-per-frame] [master|slave|both] [pc-start] [pc-end] [max-lines]");
    Console.WriteLine("  mdsharp --32x-irq-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [master|slave|both] [start-frame] [max-lines]");
    Console.WriteLine("  mdsharp --32x-fill-loop-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [start-frame]");
    Console.WriteLine("  mdsharp --32x-runlength-list-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [start-frame] [max-lines]");
    Console.WriteLine("  mdsharp --32x-runlength-rechain-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [start-frame] [max-lines]");
    Console.WriteLine("  mdsharp --32x-sh2-fault-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [history]");
    Console.WriteLine("  mdsharp --32x-bus-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [address-start] [address-end] [max-lines] [all|writes|exact|all-exact|writes-exact|changes-exact|nonzero-exact] [start-frame]");
    Console.WriteLine("  mdsharp --32x-bus-trace-state <rom-file> <state.mdss> <output.csv> [frames] [instructions-per-frame] [address-start] [address-end] [max-lines] [all|writes|exact|all-exact|writes-exact|changes-exact|nonzero-exact]");
    Console.WriteLine("  mdsharp --32x-comm-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [start-frame] [max-lines] [offset-start] [offset-end] [all|writes]");
    Console.WriteLine("  mdsharp --32x-comm-trace-state <rom-file> <state.mdss> <output.csv> [frames] [instructions-per-frame] [max-lines] [offset-start] [offset-end] [all|writes]");
    Console.WriteLine("  mdsharp --32x-diagnostic-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [start-frame] [max-events]");
    Console.WriteLine("  mdsharp --32x-inspect <rom-file> [frames] [instructions-per-frame] [address] [words]");
    Console.WriteLine("  mdsharp --32x-inspect-state <rom-file> <state.mdss> [frames] [instructions-per-frame] [address] [words]");
    Console.WriteLine("  mdsharp --32x-dump-sdram <rom-file> <output.bin> [frames] [instructions-per-frame]");
    Console.WriteLine("  mdsharp --32x-fb-summary <rom-file> [frames] [instructions-per-frame]");
    Console.WriteLine("  mdsharp --32x-rle-dump <rom-file> [frames] [instructions-per-frame] [line] [max-spans]");
    Console.WriteLine("  mdsharp --32x-node-dump <rom-file> [frames] [instructions-per-frame] [address] [count] [linear|next|prev]");
    Console.WriteLine("  mdsharp --32x-cache-inspect <rom-file> [frames] [instructions-per-frame] [address]");
    Console.WriteLine("  mdsharp --32x-trace <rom-file> <output.csv> [frames] [instructions-per-frame]");
    Console.WriteLine("  mdsharp --32x-sweep <rom-folder> <output-folder> [frames] [instructions-per-frame] [--screenshots] [--resume] [--filter <text>] [--limit <count>] [--case-seconds <seconds>]");
    Console.WriteLine("  mdsharp --render <rom-file> <output.ppm> [frames] [instructions-per-frame] [--trace-cpu] [--trace-vdp]");
    Console.WriteLine("  mdsharp --render-state <rom-file> <state.mdss> <output.ppm> [frames] [instructions-per-frame]");
    Console.WriteLine("  mdsharp --scripted-render-state <rom-file> <state.mdss> <output.ppm> <script> [frames] [instructions-per-frame]");
    Console.WriteLine("  mdsharp --render-sequence <rom-file> <output-folder> <start-frame> <end-frame> [step] [instructions-per-frame]");
    Console.WriteLine("  mdsharp --sprite-trace <rom-file> [frames] [instructions-per-frame]");
    Console.WriteLine("  mdsharp --scripted-sprite-trace <rom-file> <script> [frames] [instructions-per-frame]");
    Console.WriteLine("  mdsharp --scripted-render <rom-file> <output.ppm> <script> [frames] [instructions-per-frame] [--no-svp-diagnostics] [--no-line-vram-snapshots] [--no-svp-dma-step] [--svp-mld-z] [--svp-al-broad] [--svp-al-mame] [--svp-pmac-loose] [--svp-write-rpl] [--svp-mame-timing]");
    Console.WriteLine("  mdsharp --movie-render <rom-file> <movie.mdmovie> <output.ppm> [frames] [instructions-per-frame]");
    Console.WriteLine("  mdsharp --movie-info <movie.mdmovie>");
    Console.WriteLine("  mdsharp --savestate <rom-file> <state.mdss> [frames] [instructions-per-frame]");
    Console.WriteLine("  mdsharp --savestate-state <rom-file> <input-state.mdss> <output-state.mdss> [frames] [instructions-per-frame]");
    Console.WriteLine("  mdsharp --loadstate <rom-file> <state.mdss> [frames] [instructions-per-frame]");
    Console.WriteLine("  mdsharp --regress <rom-folder> <output.csv> [frames] [instructions-per-frame]");
    Console.WriteLine("  mdsharp --compat <rom-folder> <output-folder> [frames] [instructions-per-frame] [--screenshots] [--resume] [--filter <text>]");
    Console.WriteLine("  mdsharp --post-menu-compat <manifest.json> <rom-folder> <output-folder> [instructions-per-frame]");
    Console.WriteLine("  mdsharp --perf-suite <rom-folder> <output-folder> [frames] [instructions-per-frame] [--frame-profile] [--filter <text>]");
    Console.WriteLine("  mdsharp --m68k-alloc-profile <rom-file> [frames] [instructions-per-frame] [top]");
    Console.WriteLine("  mdsharp --visual-checkpoints <rom-folder> <output-folder> [instructions-per-frame] [--update-baseline]");
    Console.WriteLine("  mdsharp --movie-checkpoints <movie-folder> <rom-folder> <output-folder> [instructions-per-frame] [--update-baseline]");
    Console.WriteLine("  mdsharp --compat-summary <compatibility.csv> [output.md]");
    Console.WriteLine("  mdsharp --compat-export <compatibility.csv> <output-folder> [--public]");
    Console.WriteLine("  mdsharp --movie-regress <movie-folder> <rom-folder> <output-folder> [instructions-per-frame]");
    Console.WriteLine("  mdsharp --compare <baseline.csv> <current.csv>");
    Console.WriteLine("  mdsharp --perf-compare <baseline-perf.csv> <current-perf.csv> [output.md]");
    Console.WriteLine("  mdsharp --runtime <rom-file> [seconds]");
    Console.WriteLine("  mdsharp --audio <rom-file> <output.wav> [frames]");
    Console.WriteLine("  mdsharp --audio-compare <rom-file> <reference-audio> <output-folder> [id] [frames] [instructions-per-frame] [compare-start-frame] [alignment-window-seconds] [reference-start-seconds] [emulated-start-seconds]");
    Console.WriteLine("  mdsharp --audio-file-compare <reference-audio> <emulated-audio> <output-folder> [id] [alignment-window-seconds] [reference-start-seconds] [emulated-start-seconds]");
    Console.WriteLine("  mdsharp --audio-stems <rom-file> <output-folder> [frames] [instructions-per-frame] [compare-start-frame]");
    Console.WriteLine("  mdsharp --audio-stem-compare <rom-file> <reference-audio> <output-folder> [id] [frames] [instructions-per-frame] [compare-start-frame] [alignment-window-seconds] [reference-start-seconds] [emulated-start-seconds]");
    Console.WriteLine("  mdsharp --ym-script-render <script.txt> <output.wav>");
    Console.WriteLine("  mdsharp --vgm-render <vgm-or-vgz-file> <output.wav> [max-seconds]");
    Console.WriteLine("  mdsharp --vgm-stems <vgm-or-vgz-file> <output-folder> [max-seconds]");
    Console.WriteLine("  mdsharp --vgm-stem-compare <vgm-or-vgz-file> <reference-audio> <output-folder> [id] [max-seconds] [alignment-window-seconds]");
    Console.WriteLine("  mdsharp --sonic-audio-dump <rom-file> <output.wav> <energy.csv> [frames] [instructions-per-frame] [title|greenhill|gameplay]");
    Console.WriteLine("  mdsharp --sonic-audio-compare <rom-file> <reference-audio> <output-folder> [title|greenhill|gameplay] [frames] [instructions-per-frame] [alignment-window-seconds] [reference-start-seconds] [emulated-start-seconds]");
    Console.WriteLine("  mdsharp --sonic-audio-checkpoints <rom-file> <output-folder> [reference-audio] [instructions-per-frame]");
    Console.WriteLine("  mdsharp --sonic-audio-stems <rom-file> <output-folder> [title|greenhill|gameplay] [frames] [instructions-per-frame]");
    Console.WriteLine("  mdsharp --sonic-audio-windows <rom-file> <title-reference> <greenhill-reference> <output-folder> [instructions-per-frame]");
    Console.WriteLine("  mdsharp --audio-regression <rom-folder> <output-folder> [reference-audio] [instructions-per-frame]");
    Console.WriteLine("  mdsharp --audio-reference-suite <manifest.json> <rom-folder> <output-folder> [instructions-per-frame]");
    Console.WriteLine("  mdsharp --audio-trace <rom-file> <output.csv> [frames] [instructions-per-frame]");
    Console.WriteLine("  mdsharp --z80-trace <rom-file> <output.csv> [frames] [max-lines] [instructions-per-frame] [start-frame]");
    Console.WriteLine("  mdsharp --m68k-live-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [pc-start] [pc-end] [max-lines] [start-frame]");
    Console.WriteLine("  mdsharp --32x-m68k-exception-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [max-lines] [start-frame]");
    Console.WriteLine("  mdsharp --32x-sdk-monitor-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [max-lines] [start-frame]");
    Console.WriteLine("  mdsharp --m68k-memory-read-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [address-start] [address-end] [max-lines] [start-frame]");
    Console.WriteLine("  mdsharp --m68k-memory-write-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [address-start] [address-end] [max-lines] [start-frame]");
    Console.WriteLine("  mdsharp --m68k-interrupt-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [max-lines] [start-frame]");
    Console.WriteLine("  mdsharp --inspect <rom-file> [frames] [instructions-per-frame] [address] [bytes]");
    Console.WriteLine("  mdsharp --watch <rom-file> <start-frame> <frames> <address> <bytes> [instructions-per-frame]");
    Console.WriteLine("  mdsharp --io-trace <rom-file> <start-frame> <frames> [instructions-per-frame]");
    Console.WriteLine("  mdsharp --bloodlines-io-trace <rom-file> <start-frame> <frames> [instructions-per-frame]");
    Console.WriteLine("  mdsharp --pc-trace <rom-file> <frames> [instructions] [instructions-per-frame]");
    Console.WriteLine("  mdsharp --svp-trace <rom-file> <output.csv> <script> [frames] [instructions-per-frame] [max-lines] [pc-list] [start-frame]");
    Console.WriteLine("  mdsharp --svp-pm-trace <rom-file> <output.csv> <script> [frames] [instructions-per-frame] [max-lines] [start-frame]");
    Console.WriteLine("  mdsharp --svp-pointer-trace <rom-file> <output.csv> <script> [frames] [instructions-per-frame] [max-lines] [pc-list] [start-frame]");
    Console.WriteLine("  mdsharp --svp-write-history <rom-file> <output.csv> <script> <dram-words> [frames] [instructions-per-frame] [history] [max-events] [start-frame]");
    Console.WriteLine("  mdsharp --svp-bus-trace <rom-file> <output.csv> <script> [frames] [instructions-per-frame] [max-lines] [address-list] [start-frame]");
    Console.WriteLine("  mdsharp --dma-word-trace <rom-file> <output.csv> <script> [frames] [instructions-per-frame] [max-lines] [start-frame] [source-prefix]");
    Console.WriteLine("  mdsharp --vdp-plane-trace <rom-file> <output.csv> <script> <frames> [instructions-per-frame] [planeA|planeB] [y] [start-x] [end-x] [step]");
    Console.WriteLine("  mdsharp --svp-vdp-correlate <rom-file> <output.csv> <script> <frames> [instructions-per-frame] [planeA|planeB] [y] [start-x] [end-x] [step] [trace-start-frame]");
    Console.WriteLine("  mdsharp --virtua-racing-layout-check <rom-file> <output-folder> [script] [frames] [instructions-per-frame] [imageformat.txt] [--fail-on-mismatch]");
    Console.WriteLine("  mdsharp --bloodlines-pc-trace <rom-file> <frames> [instructions] [instructions-per-frame]");
    Console.WriteLine("  mdsharp --bloodlines-render <rom-file> <output.ppm> <frames> [instructions-per-frame]");
    Console.WriteLine("  mdsharp --streets-pc-trace <rom-file> <frames> [instructions] [instructions-per-frame]");
    Console.WriteLine("  mdsharp --streets-render <rom-file> <output.ppm> <frames> [instructions-per-frame]");
    Console.WriteLine("  mdsharp --sonic-bench <rom-file> <output-folder> [instructions-per-frame]");
    Console.WriteLine("  mdsharp --bloodlines-bench <rom-file> <output-folder> [instructions-per-frame]");
    return;
}

if (args[0].Equals("--sweep", StringComparison.OrdinalIgnoreCase))
{
    string folder = args.Length > 1 ? args[1] : "TestRoms";
    int budget = args.Length > 2 && int.TryParse(args[2], out int parsedBudget) ? parsedBudget : DefaultInstructionBudget;
    SweepFolder(folder, budget);
    return;
}

if (args[0].Equals("--cart-info", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: mdsharp --cart-info <rom-file>");
        return;
    }

    PrintCartridgeInfo(args[1]);
    return;
}

if (args[0].Equals("--cart-scan", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --cart-scan <rom-folder> <output.csv>");
        return;
    }

    ScanCartridges(args[1], args[2]);
    return;
}

if (args[0].Equals("--32x-sh2-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-sh2-trace <rom-file> [instructions] [master|slave] [start-pc]");
        return;
    }

    int instructions = args.Length > 2 && int.TryParse(args[2], out int parsedInstructions) ? parsedInstructions : 10_000;
    string cpu = args.Length > 3 ? args[3] : "master";
    uint? startPc = args.Length > 4 ? ParseNumber(args[4]) : null;
    TraceThirtyTwoXSh2(args[1], instructions, cpu, startPc);
    return;
}

if (args[0].Equals("--32x-live-sh2-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-live-sh2-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [master|slave|both] [pc-start] [pc-end] [max-lines] [start-frame]");
        return;
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 60;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    string cpu = args.Length > 5 ? args[5] : "both";
    uint? pcStart = args.Length > 6 ? ParseNumber(args[6]) : null;
    uint? pcEnd = args.Length > 7 ? ParseNumber(args[7]) : pcStart;
    int maxLines = args.Length > 8 && int.TryParse(args[8], out int parsedMaxLines) ? parsedMaxLines : 250_000;
    int startFrame = args.Length > 9 && int.TryParse(args[9], out int parsedStartFrame) ? Math.Max(0, parsedStartFrame) : 0;
    TraceThirtyTwoXLiveSh2(args[1], args[2], frames, instructionsPerFrame, cpu, pcStart, pcEnd, maxLines, startFrame);
    return;
}

if (args[0].Equals("--32x-live-sh2-trace-state", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-live-sh2-trace-state <rom-file> <state.mdss> <output.csv> [frames] [instructions-per-frame] [master|slave|both] [pc-start] [pc-end] [max-lines]");
        Environment.Exit(1);
    }

    int frames = args.Length > 4 && int.TryParse(args[4], out int parsedFrames) ? parsedFrames : 60;
    int instructionsPerFrame = args.Length > 5 && int.TryParse(args[5], out int parsedInstructions) ? parsedInstructions : 300_000;
    string cpu = args.Length > 6 ? args[6] : "both";
    uint? pcStart = args.Length > 7 ? ParseNumber(args[7]) : null;
    uint? pcEnd = args.Length > 8 ? ParseNumber(args[8]) : pcStart;
    int maxLines = args.Length > 9 && int.TryParse(args[9], out int parsedMaxLines) ? parsedMaxLines : 250_000;
    TraceThirtyTwoXLiveSh2(args[1], args[3], frames, instructionsPerFrame, cpu, pcStart, pcEnd, maxLines, startFrame: 0, statePath: args[2]);
    return;
}

if (args[0].Equals("--32x-irq-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-irq-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [master|slave|both] [start-frame] [max-lines]");
        return;
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 120;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 600_000;
    string cpu = args.Length > 5 ? args[5] : "both";
    int startFrame = args.Length > 6 && int.TryParse(args[6], out int parsedStartFrame) ? Math.Max(0, parsedStartFrame) : 0;
    int maxLines = args.Length > 7 && int.TryParse(args[7], out int parsedMaxLines) ? Math.Max(1, parsedMaxLines) : 10_000;
    TraceThirtyTwoXInterrupts(args[1], args[2], frames, instructionsPerFrame, cpu, startFrame, maxLines);
    return;
}

if (args[0].Equals("--32x-fill-loop-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-fill-loop-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [start-frame]");
        return;
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 60;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    int startFrame = args.Length > 5 && int.TryParse(args[5], out int parsedStartFrame) ? Math.Max(0, parsedStartFrame) : 0;
    TraceThirtyTwoXFillLoops(args[1], args[2], frames, instructionsPerFrame, startFrame);
    return;
}

if (args[0].Equals("--32x-runlength-list-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-runlength-list-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [start-frame] [max-lines]");
        return;
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 60;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    int startFrame = args.Length > 5 && int.TryParse(args[5], out int parsedStartFrame) ? Math.Max(0, parsedStartFrame) : 0;
    int maxLines = args.Length > 6 && int.TryParse(args[6], out int parsedMaxLines) ? Math.Max(1, parsedMaxLines) : 50_000;
    TraceThirtyTwoXRunlengthList(args[1], args[2], frames, instructionsPerFrame, startFrame, maxLines);
    return;
}

if (args[0].Equals("--32x-runlength-rechain-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-runlength-rechain-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [start-frame] [max-lines]");
        return;
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 60;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    int startFrame = args.Length > 5 && int.TryParse(args[5], out int parsedStartFrame) ? Math.Max(0, parsedStartFrame) : 0;
    int maxLines = args.Length > 6 && int.TryParse(args[6], out int parsedMaxLines) ? Math.Max(1, parsedMaxLines) : 50_000;
    TraceThirtyTwoXRunlengthRechain(args[1], args[2], frames, instructionsPerFrame, startFrame, maxLines);
    return;
}

if (args[0].Equals("--32x-sh2-fault-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-sh2-fault-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [history]");
        return;
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 60;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    int history = args.Length > 5 && int.TryParse(args[5], out int parsedHistory) ? parsedHistory : 2_000;
    TraceThirtyTwoXSh2Fault(args[1], args[2], frames, instructionsPerFrame, history);
    return;
}

if (args[0].Equals("--32x-bus-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-bus-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [address-start] [address-end] [max-lines] [all|writes|exact|all-exact|writes-exact|changes-exact|nonzero-exact] [start-frame]");
        return;
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 60;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    uint? addressStart = args.Length > 5 ? ParseNumber(args[5]) : null;
    uint? addressEnd = args.Length > 6 ? ParseNumber(args[6]) : addressStart;
    int maxLines = args.Length > 7 && int.TryParse(args[7], out int parsedMaxLines) ? parsedMaxLines : 250_000;
    string traceMode = args.Length > 8 ? args[8] : "all";
    bool writesOnly = traceMode.Equals("writes", StringComparison.OrdinalIgnoreCase) ||
        traceMode.Equals("writes-exact", StringComparison.OrdinalIgnoreCase);
    bool exactAddressMatch = traceMode.Equals("exact", StringComparison.OrdinalIgnoreCase) ||
        traceMode.Equals("all-exact", StringComparison.OrdinalIgnoreCase) ||
        traceMode.Equals("writes-exact", StringComparison.OrdinalIgnoreCase) ||
        traceMode.Equals("changes-exact", StringComparison.OrdinalIgnoreCase) ||
        traceMode.Equals("nonzero-exact", StringComparison.OrdinalIgnoreCase);
    bool changesOnly = traceMode.Equals("changes-exact", StringComparison.OrdinalIgnoreCase);
    bool nonzeroOnly = traceMode.Equals("nonzero-exact", StringComparison.OrdinalIgnoreCase);
    int startFrame = args.Length > 9 && int.TryParse(args[9], out int parsedStartFrame) ? Math.Max(0, parsedStartFrame) : 0;
    TraceThirtyTwoXBus(args[1], args[2], frames, instructionsPerFrame, addressStart, addressEnd, maxLines, writesOnly, exactAddressMatch, startFrame, changesOnly, nonzeroOnly);
    return;
}

if (args[0].Equals("--32x-bus-trace-state", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-bus-trace-state <rom-file> <state.mdss> <output.csv> [frames] [instructions-per-frame] [address-start] [address-end] [max-lines] [all|writes|exact|all-exact|writes-exact|changes-exact|nonzero-exact]");
        Environment.Exit(1);
    }

    int frames = args.Length > 4 && int.TryParse(args[4], out int parsedFrames) ? parsedFrames : 60;
    int instructionsPerFrame = args.Length > 5 && int.TryParse(args[5], out int parsedInstructions) ? parsedInstructions : 300_000;
    uint? addressStart = args.Length > 6 ? ParseNumber(args[6]) : null;
    uint? addressEnd = args.Length > 7 ? ParseNumber(args[7]) : addressStart;
    int maxLines = args.Length > 8 && int.TryParse(args[8], out int parsedMaxLines) ? parsedMaxLines : 250_000;
    string traceMode = args.Length > 9 ? args[9] : "all";
    bool writesOnly = traceMode.Equals("writes", StringComparison.OrdinalIgnoreCase) ||
        traceMode.Equals("writes-exact", StringComparison.OrdinalIgnoreCase) ||
        traceMode.Equals("nonzero-exact", StringComparison.OrdinalIgnoreCase) ||
        traceMode.Equals("changes-exact", StringComparison.OrdinalIgnoreCase);
    bool exactAddressMatch = traceMode.Equals("exact", StringComparison.OrdinalIgnoreCase) ||
        traceMode.Equals("all-exact", StringComparison.OrdinalIgnoreCase) ||
        traceMode.Equals("writes-exact", StringComparison.OrdinalIgnoreCase) ||
        traceMode.Equals("nonzero-exact", StringComparison.OrdinalIgnoreCase) ||
        traceMode.Equals("changes-exact", StringComparison.OrdinalIgnoreCase);
    bool changesOnly = traceMode.Equals("changes-exact", StringComparison.OrdinalIgnoreCase);
    bool nonzeroOnly = traceMode.Equals("nonzero-exact", StringComparison.OrdinalIgnoreCase);
    TraceThirtyTwoXBus(args[1], args[3], frames, instructionsPerFrame, addressStart, addressEnd, maxLines, writesOnly, exactAddressMatch, startFrame: 0, changesOnly, nonzeroOnly, statePath: args[2]);
    return;
}

if (args[0].Equals("--32x-comm-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-comm-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [start-frame] [max-lines] [offset-start] [offset-end] [all|writes]");
        return;
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 60;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    int startFrame = args.Length > 5 && int.TryParse(args[5], out int parsedStartFrame) ? Math.Max(0, parsedStartFrame) : 0;
    int maxLines = args.Length > 6 && int.TryParse(args[6], out int parsedMaxLines) ? Math.Max(1, parsedMaxLines) : 50_000;
    ushort offsetStart = args.Length > 7 ? (ushort)ParseNumber(args[7]) : ThirtyTwoXHardwareProfile.CommunicationPortOffset;
    ushort offsetEnd = args.Length > 8 ? (ushort)ParseNumber(args[8]) : (ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 0x0F);
    bool writesOnly = args.Length > 9 && args[9].Equals("writes", StringComparison.OrdinalIgnoreCase);
    TraceThirtyTwoXCommunication(args[1], args[2], frames, instructionsPerFrame, startFrame, maxLines, offsetStart, offsetEnd, writesOnly);
    return;
}

if (args[0].Equals("--32x-comm-trace-state", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-comm-trace-state <rom-file> <state.mdss> <output.csv> [frames] [instructions-per-frame] [max-lines] [offset-start] [offset-end] [all|writes]");
        Environment.Exit(1);
    }

    int frames = args.Length > 4 && int.TryParse(args[4], out int parsedFrames) ? parsedFrames : 60;
    int instructionsPerFrame = args.Length > 5 && int.TryParse(args[5], out int parsedInstructions) ? parsedInstructions : 300_000;
    int maxLines = args.Length > 6 && int.TryParse(args[6], out int parsedMaxLines) ? Math.Max(1, parsedMaxLines) : 50_000;
    ushort offsetStart = args.Length > 7 ? (ushort)ParseNumber(args[7]) : ThirtyTwoXHardwareProfile.CommunicationPortOffset;
    ushort offsetEnd = args.Length > 8 ? (ushort)ParseNumber(args[8]) : (ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 0x0F);
    bool writesOnly = args.Length > 9 && args[9].Equals("writes", StringComparison.OrdinalIgnoreCase);
    TraceThirtyTwoXCommunication(args[1], args[3], frames, instructionsPerFrame, startFrame: 0, maxLines, offsetStart, offsetEnd, writesOnly, statePath: args[2]);
    return;
}

if (args[0].Equals("--32x-diagnostic-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-diagnostic-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [start-frame] [max-events]");
        return;
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 300;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    int startFrame = args.Length > 5 && int.TryParse(args[5], out int parsedStartFrame) ? parsedStartFrame : 0;
    int maxEvents = args.Length > 6 && int.TryParse(args[6], out int parsedMaxEvents) ? parsedMaxEvents : 10_000;
    TraceThirtyTwoXDiagnostic(args[1], args[2], frames, instructionsPerFrame, startFrame, maxEvents);
    return;
}

if (args[0].Equals("--32x-inspect", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-inspect <rom-file> [frames] [instructions-per-frame] [address] [words]");
        return;
    }

    int frames = args.Length > 2 && int.TryParse(args[2], out int parsedFrames) ? parsedFrames : 0;
    int instructionsPerFrame = args.Length > 3 && int.TryParse(args[3], out int parsedInstructions) ? parsedInstructions : 300_000;
    uint address = args.Length > 4 ? ParseNumber(args[4]) : ThirtyTwoXHardwareProfile.Sh2SdramStart;
    int words = args.Length > 5 && int.TryParse(args[5], out int parsedWords) ? parsedWords : 32;
    InspectThirtyTwoX(args[1], frames, instructionsPerFrame, address, words);
    return;
}

if (args[0].Equals("--32x-inspect-state", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-inspect-state <rom-file> <state.mdss> [frames] [instructions-per-frame] [address] [words]");
        return;
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 0;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    uint address = args.Length > 5 ? ParseNumber(args[5]) : ThirtyTwoXHardwareProfile.Sh2SdramStart;
    int words = args.Length > 6 && int.TryParse(args[6], out int parsedWords) ? parsedWords : 32;
    InspectThirtyTwoXState(args[1], args[2], frames, instructionsPerFrame, address, words);
    return;
}

if (args[0].Equals("--32x-cache-inspect", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-cache-inspect <rom-file> [frames] [instructions-per-frame] [address]");
        return;
    }

    int frames = args.Length > 2 && int.TryParse(args[2], out int parsedFrames) ? parsedFrames : 0;
    int instructionsPerFrame = args.Length > 3 && int.TryParse(args[3], out int parsedInstructions) ? parsedInstructions : 300_000;
    uint address = args.Length > 4 ? ParseNumber(args[4]) : 0;
    InspectThirtyTwoXCache(args[1], frames, instructionsPerFrame, address);
    return;
}

if (args[0].Equals("--32x-dump-sdram", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-dump-sdram <rom-file> <output.bin> [frames] [instructions-per-frame]");
        return;
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 60;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 600_000;
    DumpThirtyTwoXSdram(args[1], args[2], frames, instructionsPerFrame);
    return;
}

if (args[0].Equals("--32x-fb-summary", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-fb-summary <rom-file> [frames] [instructions-per-frame]");
        return;
    }

    int frames = args.Length > 2 && int.TryParse(args[2], out int parsedFrames) ? parsedFrames : 60;
    int instructionsPerFrame = args.Length > 3 && int.TryParse(args[3], out int parsedInstructions) ? parsedInstructions : 300_000;
    SummarizeThirtyTwoXFrameBuffers(args[1], frames, instructionsPerFrame);
    return;
}

if (args[0].Equals("--32x-rle-dump", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-rle-dump <rom-file> [frames] [instructions-per-frame] [line] [max-spans]");
        return;
    }

    int frames = args.Length > 2 && int.TryParse(args[2], out int parsedFrames) ? parsedFrames : 60;
    int instructionsPerFrame = args.Length > 3 && int.TryParse(args[3], out int parsedInstructions) ? parsedInstructions : 300_000;
    int line = args.Length > 4 && int.TryParse(args[4], out int parsedLine) ? parsedLine : 112;
    int maxSpans = args.Length > 5 && int.TryParse(args[5], out int parsedMaxSpans) ? parsedMaxSpans : 64;
    DumpThirtyTwoXRleLine(args[1], frames, instructionsPerFrame, line, maxSpans);
    return;
}

if (args[0].Equals("--32x-node-dump", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-node-dump <rom-file> [frames] [instructions-per-frame] [address] [count] [linear|next|prev]");
        return;
    }

    int frames = args.Length > 2 && int.TryParse(args[2], out int parsedFrames) ? parsedFrames : 60;
    int instructionsPerFrame = args.Length > 3 && int.TryParse(args[3], out int parsedInstructions) ? parsedInstructions : 300_000;
    uint address = args.Length > 4 ? ParseNumber(args[4]) : 0x0602_9874;
    int count = args.Length > 5 && int.TryParse(args[5], out int parsedCount) ? parsedCount : 32;
    string mode = args.Length > 6 ? args[6] : "linear";
    DumpThirtyTwoXNodeRecords(args[1], frames, instructionsPerFrame, address, count, mode);
    return;
}

if (args[0].Equals("--32x-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-trace <rom-file> <output.csv> [frames] [instructions-per-frame]");
        return;
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 300;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    TraceThirtyTwoX(args[1], args[2], frames, instructionsPerFrame);
    return;
}

if (args[0].Equals("--32x-sweep", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-sweep <rom-folder> <output-folder> [frames] [instructions-per-frame] [--screenshots] [--resume] [--filter <text>] [--limit <count>] [--adaptive-seconds <seconds>] [--case-seconds <seconds>]");
        return;
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 600;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    bool screenshots = args.Any(arg => arg.Equals("--screenshots", StringComparison.OrdinalIgnoreCase));
    bool resume = args.Any(arg => arg.Equals("--resume", StringComparison.OrdinalIgnoreCase));
    string? filter = GetOptionValue(args, "--filter");
    int? limit = TryGetPositiveOption(args, "--limit");
    double adaptiveTimeLimitSeconds = TryGetPositiveDoubleOption(args, "--adaptive-seconds") ?? ThirtyTwoXSweepAdaptiveTimeLimitSeconds;
    double caseTimeLimitSeconds = TryGetPositiveDoubleOption(args, "--case-seconds") ?? 0.0;
    SweepThirtyTwoX(args[1], args[2], frames, instructionsPerFrame, screenshots, resume, filter, limit, adaptiveTimeLimitSeconds, caseTimeLimitSeconds);
    return;
}

if (args[0].Equals("--render", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --render <rom-file> <output.ppm> [frames] [instructions-per-frame]");
        Environment.Exit(1);
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 60;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 200_000;
    bool traceVdp = args.Any(arg => arg.Equals("--trace-vdp", StringComparison.OrdinalIgnoreCase));
    bool traceCpu = traceVdp || args.Any(arg => arg.Equals("--trace-cpu", StringComparison.OrdinalIgnoreCase));
    RenderRom(args[1], args[2], frames, instructionsPerFrame, traceCpu, traceVdp);
    return;
}

if (args[0].Equals("--render-state", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --render-state <rom-file> <state.mdss> <output.ppm> [frames] [instructions-per-frame]");
        Environment.Exit(1);
    }

    int frames = args.Length > 4 && int.TryParse(args[4], out int parsedFrames) ? parsedFrames : 0;
    int instructionsPerFrame = args.Length > 5 && int.TryParse(args[5], out int parsedInstructions) ? parsedInstructions : 300_000;
    RenderState(args[1], args[2], args[3], frames, instructionsPerFrame);
    return;
}

if (args[0].Equals("--scripted-render-state", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 5)
    {
        Console.Error.WriteLine("Usage: mdsharp --scripted-render-state <rom-file> <state.mdss> <output.ppm> <script> [frames] [instructions-per-frame]");
        Console.Error.WriteLine("Scripts: none, start, repeat-start, p1-repeat-start, virtua-racing-drive, sonic1-start, sonic3-start, chaotix-title-start, chaotix-play, streets, bloodlines");
        Environment.Exit(1);
    }

    Func<int, ControllerInput> input = ResolveControllerInputScript(args[4]);
    int frames = args.Length > 5 && int.TryParse(args[5], out int parsedFrames) ? parsedFrames : 60;
    int instructionsPerFrame = args.Length > 6 && int.TryParse(args[6], out int parsedInstructions) ? parsedInstructions : 300_000;
    RenderState(args[1], args[2], args[3], frames, instructionsPerFrame, input, args[4]);
    return;
}

if (args[0].Equals("--render-sequence", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 5)
    {
        Console.Error.WriteLine("Usage: mdsharp --render-sequence <rom-file> <output-folder> <start-frame> <end-frame> [step] [instructions-per-frame]");
        Environment.Exit(1);
    }

    int startFrame = int.Parse(args[3], CultureInfo.InvariantCulture);
    int endFrame = int.Parse(args[4], CultureInfo.InvariantCulture);
    int step = args.Length > 5 && int.TryParse(args[5], out int parsedStep) ? Math.Max(1, parsedStep) : 1;
    int instructionsPerFrame = args.Length > 6 && int.TryParse(args[6], out int parsedInstructions) ? parsedInstructions : 300_000;
    RenderSequence(args[1], args[2], startFrame, endFrame, step, instructionsPerFrame);
    return;
}

if (args[0].Equals("--sprite-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: mdsharp --sprite-trace <rom-file> [frames] [instructions-per-frame]");
        Environment.Exit(1);
    }

    int frames = args.Length > 2 && int.TryParse(args[2], out int parsedFrames) ? parsedFrames : 60;
    int instructionsPerFrame = args.Length > 3 && int.TryParse(args[3], out int parsedInstructions) ? parsedInstructions : 300_000;
    TraceSprites(args[1], frames, instructionsPerFrame, NoInput);
    return;
}

if (args[0].Equals("--scripted-sprite-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --scripted-sprite-trace <rom-file> <script> [frames] [instructions-per-frame]");
        Environment.Exit(1);
    }

    Func<int, GenesisButton> input = ResolveInputScript(args[2]);
    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 60;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    TraceSprites(args[1], frames, instructionsPerFrame, input);
    return;
}

if (args[0].Equals("--scripted-render", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --scripted-render <rom-file> <output.ppm> <script> [frames] [instructions-per-frame] [--no-svp-diagnostics] [--no-line-vram-snapshots] [--no-svp-dma-step] [--svp-mld-z] [--svp-al-broad] [--svp-al-mame] [--svp-pmac-loose] [--svp-write-rpl] [--svp-mame-timing]");
        Console.Error.WriteLine("Scripts: none, start, repeat-start, p1-repeat-start, virtua-racing-drive, sonic1-start, sonic3-start, chaotix-title-start, chaotix-play, streets, bloodlines");
        Environment.Exit(1);
    }

    Func<int, ControllerInput> input = ResolveControllerInputScript(args[3]);
    int frames = args.Length > 4 && int.TryParse(args[4], out int parsedFrames) ? parsedFrames : 60;
    int instructionsPerFrame = args.Length > 5 && int.TryParse(args[5], out int parsedInstructions) ? parsedInstructions : 300_000;
    bool enableSvpDiagnostics = !args.Any(arg => arg.Equals("--no-svp-diagnostics", StringComparison.OrdinalIgnoreCase));
    bool useLineVramSnapshots = !args.Any(arg => arg.Equals("--no-line-vram-snapshots", StringComparison.OrdinalIgnoreCase));
    bool stepSvpDuringDma = !args.Any(arg => arg.Equals("--no-svp-dma-step", StringComparison.OrdinalIgnoreCase));
    bool setZeroFlagOnMld = args.Any(arg => arg.Equals("--svp-mld-z", StringComparison.OrdinalIgnoreCase));
    bool clearPmcOnAnyAlRead = args.Any(arg => arg.Equals("--svp-al-broad", StringComparison.OrdinalIgnoreCase));
    bool returnZeroOnAlRead = args.Any(arg => arg.Equals("--svp-al-mame", StringComparison.OrdinalIgnoreCase));
    bool requireBlindPmacSet = !args.Any(arg => arg.Equals("--svp-pmac-loose", StringComparison.OrdinalIgnoreCase));
    bool useModuloOnPointerWrites = args.Any(arg => arg.Equals("--svp-write-rpl", StringComparison.OrdinalIgnoreCase));
    bool useMameCycleTiming = args.Any(arg => arg.Equals("--svp-mame-timing", StringComparison.OrdinalIgnoreCase));
    RenderScriptedRomWithControllers(args[1], args[2], frames, instructionsPerFrame, input, enableSvpDiagnostics, useLineVramSnapshots, stepSvpDuringDma, setZeroFlagOnMld, clearPmcOnAnyAlRead, returnZeroOnAlRead, requireBlindPmacSet, useModuloOnPointerWrites, useMameCycleTiming);
    return;
}

if (args[0].Equals("--movie-render", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --movie-render <rom-file> <movie.mdmovie> <output.ppm> [frames] [instructions-per-frame]");
        Environment.Exit(1);
    }

    InputMovie movie = InputMovie.Load(args[2]);
    int frames = args.Length > 4 && int.TryParse(args[4], out int parsedFrames) ? parsedFrames : movie.FrameCount;
    int instructionsPerFrame = args.Length > 5 && int.TryParse(args[5], out int parsedInstructions) ? parsedInstructions : 300_000;
    RenderInputMovie(args[1], args[2], args[3], frames, instructionsPerFrame);
    return;
}

if (args[0].Equals("--movie-info", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: mdsharp --movie-info <movie.mdmovie>");
        Environment.Exit(1);
    }

    PrintMovieInfo(args[1]);
    return;
}

if (args[0].Equals("--savestate", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --savestate <rom-file> <state.mdss> [frames] [instructions-per-frame]");
        Environment.Exit(1);
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 1;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 200_000;
    SaveState(args[1], args[2], frames, instructionsPerFrame);
    return;
}

if (args[0].Equals("--savestate-state", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --savestate-state <rom-file> <input-state.mdss> <output-state.mdss> [frames] [instructions-per-frame]");
        Environment.Exit(1);
    }

    int frames = args.Length > 4 && int.TryParse(args[4], out int parsedFrames) ? parsedFrames : 1;
    int instructionsPerFrame = args.Length > 5 && int.TryParse(args[5], out int parsedInstructions) ? parsedInstructions : 200_000;
    SaveStateFromState(args[1], args[2], args[3], frames, instructionsPerFrame);
    return;
}

if (args[0].Equals("--loadstate", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --loadstate <rom-file> <state.mdss> [frames] [instructions-per-frame]");
        Environment.Exit(1);
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 1;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 200_000;
    LoadStateAndRun(args[1], args[2], frames, instructionsPerFrame);
    return;
}

if (args[0].Equals("--regress", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --regress <rom-folder> <output.csv> [frames] [instructions-per-frame]");
        Environment.Exit(1);
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 3;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 100_000;
    RegressFolder(args[1], args[2], frames, instructionsPerFrame);
    return;
}

if (args[0].Equals("--compat", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --compat <rom-folder> <output-folder> [frames] [instructions-per-frame] [--screenshots] [--resume] [--filter <text>]");
        Environment.Exit(1);
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 300;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    bool screenshots = args.Any(arg => arg.Equals("--screenshots", StringComparison.OrdinalIgnoreCase));
    bool resume = args.Any(arg => arg.Equals("--resume", StringComparison.OrdinalIgnoreCase));
    string? filter = GetOptionValue(args, "--filter");
    RunCompatibilityDashboard(args[1], args[2], frames, instructionsPerFrame, screenshots, resume, filter);
    return;
}

if (args[0].Equals("--post-menu-compat", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --post-menu-compat <manifest.json> <rom-folder> <output-folder> [instructions-per-frame]");
        Environment.Exit(1);
    }

    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    RunPostMenuCompatibility(args[1], args[2], args[3], instructionsPerFrame);
    return;
}

if (args[0].Equals("--perf-suite", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --perf-suite <rom-folder> <output-folder> [frames] [instructions-per-frame] [--frame-profile] [--filter <text>]");
        Environment.Exit(1);
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 600;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    bool frameProfile = args.Any(arg => arg.Equals("--frame-profile", StringComparison.OrdinalIgnoreCase));
    string? filter = GetOptionValue(args, "--filter");
    RunPerfSuite(args[1], args[2], frames, instructionsPerFrame, frameProfile, filter);
    return;
}

if (args[0].Equals("--m68k-alloc-profile", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: mdsharp --m68k-alloc-profile <rom-file> [frames] [instructions-per-frame] [top]");
        Environment.Exit(1);
    }

    int frames = args.Length > 2 && int.TryParse(args[2], out int parsedFrames) ? parsedFrames : 120;
    int instructionsPerFrame = args.Length > 3 && int.TryParse(args[3], out int parsedInstructions) ? parsedInstructions : 300_000;
    int top = args.Length > 4 && int.TryParse(args[4], out int parsedTop) ? parsedTop : 20;
    ProfileM68kAllocations(args[1], frames, instructionsPerFrame, top);
    return;
}

if (args[0].Equals("--visual-checkpoints", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --visual-checkpoints <rom-folder> <output-folder> [instructions-per-frame] [--update-baseline]");
        Environment.Exit(1);
    }

    int instructionsPerFrame = args.Length > 3 && int.TryParse(args[3], out int parsedInstructions) ? parsedInstructions : 300_000;
    bool updateBaseline = args.Any(arg => arg.Equals("--update-baseline", StringComparison.OrdinalIgnoreCase));
    RunVisualCheckpoints(args[1], args[2], instructionsPerFrame, updateBaseline);
    return;
}

if (args[0].Equals("--compat-summary", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: mdsharp --compat-summary <compatibility.csv> [output.md]");
        Environment.Exit(1);
    }

    SummarizeCompatibilityReport(args[1], args.Length > 2 ? args[2] : null);
    return;
}

if (args[0].Equals("--compat-export", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --compat-export <compatibility.csv> <output-folder> [--public]");
        return;
    }

    bool publicMode = args.Any(arg => arg.Equals("--public", StringComparison.OrdinalIgnoreCase));
    ExportCompatibilityMatrix(args[1], args[2], publicMode);
    return;
}

if (args[0].Equals("--movie-checkpoints", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --movie-checkpoints <movie-folder> <rom-folder> <output-folder> [instructions-per-frame] [--update-baseline]");
        Environment.Exit(1);
    }

    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    bool updateBaseline = args.Any(arg => arg.Equals("--update-baseline", StringComparison.OrdinalIgnoreCase));
    RunMovieVisualCheckpoints(args[1], args[2], args[3], instructionsPerFrame, updateBaseline);
    return;
}

if (args[0].Equals("--movie-regress", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --movie-regress <movie-folder> <rom-folder> <output-folder> [instructions-per-frame]");
        Environment.Exit(1);
    }

    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    RunMovieRegression(args[1], args[2], args[3], instructionsPerFrame);
    return;
}

if (args[0].Equals("--runtime", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: mdsharp --runtime <rom-file> [seconds]");
        Environment.Exit(1);
    }

    int seconds = args.Length > 2 && int.TryParse(args[2], out int parsedSeconds) ? parsedSeconds : 5;
    RuntimeLoop(args[1], seconds);
    return;
}

if (args[0].Equals("--compare", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --compare <baseline.csv> <current.csv>");
        Environment.Exit(1);
    }

    CompareRegression(args[1], args[2]);
    return;
}

if (args[0].Equals("--perf-compare", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --perf-compare <baseline-perf.csv> <current-perf.csv> [output.md]");
        Environment.Exit(1);
    }

    ComparePerfSuite(args[1], args[2], args.Length > 3 ? args[3] : null);
    return;
}

if (args[0].Equals("--audio", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --audio <rom-file> <output.wav> [frames]");
        Environment.Exit(1);
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 60;
    DumpAudio(args[1], args[2], frames);
    return;
}

if (args[0].Equals("--audio-compare", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --audio-compare <rom-file> <reference-audio> <output-folder> [id] [frames] [instructions-per-frame] [compare-start-frame] [alignment-window-seconds] [reference-start-seconds] [emulated-start-seconds]");
        Environment.Exit(1);
    }

    string id = args.Length > 4 ? args[4] : Path.GetFileNameWithoutExtension(args[1]).Replace(' ', '-').ToLowerInvariant();
    int frames = args.Length > 5 && int.TryParse(args[5], out int parsedFrames) ? parsedFrames : 900;
    int instructionsPerFrame = args.Length > 6 && int.TryParse(args[6], out int parsedInstructions) ? parsedInstructions : 300_000;
    int compareStartFrame = args.Length > 7 && int.TryParse(args[7], out int parsedCompareStart) ? parsedCompareStart : 0;
    double? alignmentWindowSeconds = args.Length > 8 && double.TryParse(args[8], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedWindow)
        ? parsedWindow
        : null;
    double? referenceStartSeconds = args.Length > 9 && double.TryParse(args[9], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedReferenceStart)
        ? parsedReferenceStart
        : null;
    double? emulatedStartSeconds = args.Length > 10 && double.TryParse(args[10], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedEmulatedStart)
        ? parsedEmulatedStart
        : null;
    RunGenericAudioCompare(id, args[1], args[2], args[3], frames, instructionsPerFrame, compareStartFrame, alignmentWindowSeconds, referenceStartSeconds, emulatedStartSeconds);
    return;
}

if (args[0].Equals("--audio-file-compare", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --audio-file-compare <reference-audio> <emulated-audio> <output-folder> [id] [alignment-window-seconds] [reference-start-seconds] [emulated-start-seconds]");
        Environment.Exit(1);
    }

    string id = args.Length > 4 ? args[4] : $"{Path.GetFileNameWithoutExtension(args[1])}-vs-{Path.GetFileNameWithoutExtension(args[2])}";
    double? alignmentWindowSeconds = args.Length > 5 && double.TryParse(args[5], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedWindow)
        ? parsedWindow
        : null;
    double? referenceStartSeconds = args.Length > 6 && double.TryParse(args[6], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedReferenceStart)
        ? parsedReferenceStart
        : null;
    double? emulatedStartSeconds = args.Length > 7 && double.TryParse(args[7], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedEmulatedStart)
        ? parsedEmulatedStart
        : null;
    RunAudioFileCompare(id, args[1], args[2], args[3], alignmentWindowSeconds, referenceStartSeconds, emulatedStartSeconds);
    return;
}

if (args[0].Equals("--audio-stems", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --audio-stems <rom-file> <output-folder> [frames] [instructions-per-frame] [compare-start-frame]");
        Environment.Exit(1);
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 900;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    int compareStartFrame = args.Length > 5 && int.TryParse(args[5], out int parsedCompareStart) ? parsedCompareStart : 0;
    RunAudioStems(args[1], args[2], frames, instructionsPerFrame, compareStartFrame);
    return;
}

if (args[0].Equals("--audio-stem-compare", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --audio-stem-compare <rom-file> <reference-audio> <output-folder> [id] [frames] [instructions-per-frame] [compare-start-frame] [alignment-window-seconds] [reference-start-seconds] [emulated-start-seconds]");
        Environment.Exit(1);
    }

    string id = args.Length > 4 ? args[4] : Path.GetFileNameWithoutExtension(args[1]).Replace(' ', '-').ToLowerInvariant();
    int frames = args.Length > 5 && int.TryParse(args[5], out int parsedFrames) ? parsedFrames : 900;
    int instructionsPerFrame = args.Length > 6 && int.TryParse(args[6], out int parsedInstructions) ? parsedInstructions : 300_000;
    int compareStartFrame = args.Length > 7 && int.TryParse(args[7], out int parsedCompareStart) ? parsedCompareStart : 0;
    double? alignmentWindowSeconds = args.Length > 8 && double.TryParse(args[8], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedWindow)
        ? parsedWindow
        : null;
    double? referenceStartSeconds = args.Length > 9 && double.TryParse(args[9], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedReferenceStart)
        ? parsedReferenceStart
        : null;
    double? emulatedStartSeconds = args.Length > 10 && double.TryParse(args[10], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedEmulatedStart)
        ? parsedEmulatedStart
        : null;
    RunAudioStemCompare(id, args[1], args[2], args[3], frames, instructionsPerFrame, compareStartFrame, alignmentWindowSeconds, referenceStartSeconds, emulatedStartSeconds);
    return;
}

if (args[0].Equals("--vgm-render", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --vgm-render <vgm-or-vgz-file> <output.wav> [max-seconds]");
        Environment.Exit(1);
    }

    double? maxSeconds = args.Length > 3 && double.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedSeconds)
        ? parsedSeconds
        : null;
    RenderVgm(args[1], args[2], maxSeconds);
    return;
}

if (args[0].Equals("--vgm-stems", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --vgm-stems <vgm-or-vgz-file> <output-folder> [max-seconds]");
        Environment.Exit(1);
    }

    double? maxSeconds = args.Length > 3 && double.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedSeconds)
        ? parsedSeconds
        : null;
    RenderVgmStems(args[1], args[2], maxSeconds);
    return;
}

if (args[0].Equals("--vgm-stem-compare", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --vgm-stem-compare <vgm-or-vgz-file> <reference-audio> <output-folder> [id] [max-seconds] [alignment-window-seconds]");
        Environment.Exit(1);
    }

    string id = args.Length > 4 ? args[4] : Path.GetFileNameWithoutExtension(args[1]).Replace(' ', '-').ToLowerInvariant();
    double? maxSeconds = args.Length > 5 && double.TryParse(args[5], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedSeconds)
        ? parsedSeconds
        : null;
    double? alignmentWindowSeconds = args.Length > 6 && double.TryParse(args[6], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedWindow)
        ? parsedWindow
        : null;
    RunVgmStemCompare(id, args[1], args[2], args[3], maxSeconds, alignmentWindowSeconds);
    return;
}

if (args[0].Equals("--sonic-audio-dump", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --sonic-audio-dump <rom-file> <output.wav> <energy.csv> [frames] [instructions-per-frame] [title|greenhill|gameplay]");
        Environment.Exit(1);
    }

    int frames = args.Length > 4 && int.TryParse(args[4], out int parsedFrames) ? parsedFrames : 1800;
    int instructionsPerFrame = args.Length > 5 && int.TryParse(args[5], out int parsedInstructions) ? parsedInstructions : 300_000;
    string preset = args.Length > 6 ? args[6] : "greenhill";
    DumpSonicAudio(args[1], args[2], args[3], frames, instructionsPerFrame, preset);
    return;
}

if (args[0].Equals("--sonic-audio-compare", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --sonic-audio-compare <rom-file> <reference-audio> <output-folder> [title|greenhill|gameplay] [frames] [instructions-per-frame] [alignment-window-seconds] [reference-start-seconds] [emulated-start-seconds]");
        Environment.Exit(1);
    }

    string preset = args.Length > 4 ? args[4] : "greenhill";
    int frames = args.Length > 5 && int.TryParse(args[5], out int parsedFrames) ? parsedFrames : 2600;
    int instructionsPerFrame = args.Length > 6 && int.TryParse(args[6], out int parsedInstructions) ? parsedInstructions : 300_000;
    double? alignmentWindowSeconds = args.Length > 7 && double.TryParse(args[7], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedWindow)
        ? parsedWindow
        : null;
    double? referenceStartSeconds = args.Length > 8 && double.TryParse(args[8], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedReferenceStart)
        ? parsedReferenceStart
        : null;
    double? emulatedStartSeconds = args.Length > 9 && double.TryParse(args[9], NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedEmulatedStart)
        ? parsedEmulatedStart
        : null;
    RunSonicAudioCompare(args[1], args[2], args[3], preset, frames, instructionsPerFrame, alignmentWindowSeconds, referenceStartSeconds, emulatedStartSeconds);
    return;
}

if (args[0].Equals("--sonic-audio-checkpoints", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --sonic-audio-checkpoints <rom-file> <output-folder> [reference-audio] [instructions-per-frame]");
        Environment.Exit(1);
    }

    string? referenceAudio = args.Length > 3 && File.Exists(args[3]) ? args[3] : null;
    int instructionsArgument = referenceAudio is null ? 3 : 4;
    int instructionsPerFrame = args.Length > instructionsArgument && int.TryParse(args[instructionsArgument], out int parsedInstructions) ? parsedInstructions : 300_000;
    RunSonicAudioCheckpoints(args[1], args[2], referenceAudio, instructionsPerFrame);
    return;
}

if (args[0].Equals("--sonic-audio-stems", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --sonic-audio-stems <rom-file> <output-folder> [title|greenhill|gameplay] [frames] [instructions-per-frame]");
        Environment.Exit(1);
    }

    string preset = args.Length > 3 ? args[3] : "greenhill";
    int frames = args.Length > 4 && int.TryParse(args[4], out int parsedFrames) ? parsedFrames : 2600;
    int instructionsPerFrame = args.Length > 5 && int.TryParse(args[5], out int parsedInstructions) ? parsedInstructions : 300_000;
    RunSonicAudioStems(args[1], args[2], preset, frames, instructionsPerFrame);
    return;
}

if (args[0].Equals("--sonic-audio-windows", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 5)
    {
        Console.Error.WriteLine("Usage: mdsharp --sonic-audio-windows <rom-file> <title-reference> <greenhill-reference> <output-folder> [instructions-per-frame]");
        Environment.Exit(1);
    }

    int instructionsPerFrame = args.Length > 5 && int.TryParse(args[5], out int parsedInstructions) ? parsedInstructions : 300_000;
    RunSonicAudioWindows(args[1], args[2], args[3], args[4], instructionsPerFrame);
    return;
}

if (args[0].Equals("--audio-regression", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --audio-regression <rom-folder> <output-folder> [reference-audio] [instructions-per-frame]");
        Environment.Exit(1);
    }

    string? referenceAudio = args.Length > 3 && File.Exists(args[3]) ? args[3] : null;
    int instructionsArgument = referenceAudio is null ? 3 : 4;
    int instructionsPerFrame = args.Length > instructionsArgument && int.TryParse(args[instructionsArgument], out int parsedInstructions) ? parsedInstructions : 300_000;
    RunAudioRegressionSuite(args[1], args[2], referenceAudio, instructionsPerFrame);
    return;
}

if (args[0].Equals("--audio-reference-suite", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --audio-reference-suite <manifest.json> <rom-folder> <output-folder> [instructions-per-frame]");
        Environment.Exit(1);
    }

    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    RunAudioReferenceSuite(args[1], args[2], args[3], instructionsPerFrame);
    return;
}

if (args[0].Equals("--ym-script-render", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --ym-script-render <script.txt> <output.wav>");
        Environment.Exit(1);
    }

    RenderYmScript(args[1], args[2]);
    return;
}

if (args[0].Equals("--audio-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --audio-trace <rom-file> <output.csv> [frames] [instructions-per-frame]");
        Environment.Exit(1);
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 300;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    TraceAudio(args[1], args[2], frames, instructionsPerFrame);
    return;
}

if (args[0].Equals("--z80-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --z80-trace <rom-file> <output.csv> [frames] [max-lines] [instructions-per-frame]");
        Environment.Exit(1);
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 120;
    int maxLines = args.Length > 4 && int.TryParse(args[4], out int parsedMaxLines) ? parsedMaxLines : 4096;
    int instructionsPerFrame = args.Length > 5 && int.TryParse(args[5], out int parsedInstructions) ? parsedInstructions : 300_000;
    int startFrame = args.Length > 6 && int.TryParse(args[6], out int parsedStartFrame) ? parsedStartFrame : 0;
    TraceZ80(args[1], args[2], frames, maxLines, instructionsPerFrame, startFrame);
    return;
}

if (args[0].Equals("--m68k-live-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --m68k-live-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [pc-start] [pc-end] [max-lines] [start-frame]");
        Environment.Exit(1);
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 120;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    uint pcStart = args.Length > 5 ? ParseNumber(args[5]) : 0;
    uint pcEnd = args.Length > 6 ? ParseNumber(args[6]) : uint.MaxValue;
    int maxLines = args.Length > 7 && int.TryParse(args[7], out int parsedMaxLines) ? parsedMaxLines : 4096;
    int startFrame = args.Length > 8 && int.TryParse(args[8], out int parsedStartFrame) ? parsedStartFrame : 0;
    TraceM68kLive(args[1], args[2], frames, instructionsPerFrame, pcStart, pcEnd, maxLines, startFrame);
    return;
}

if (args[0].Equals("--32x-m68k-exception-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-m68k-exception-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [max-lines] [start-frame]");
        Environment.Exit(1);
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 120;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    int maxLines = args.Length > 5 && int.TryParse(args[5], out int parsedMaxLines) ? parsedMaxLines : 4096;
    int startFrame = args.Length > 6 && int.TryParse(args[6], out int parsedStartFrame) ? parsedStartFrame : 0;
    TraceThirtyTwoXM68kExceptions(args[1], args[2], frames, instructionsPerFrame, maxLines, startFrame);
    return;
}

if (args[0].Equals("--32x-sdk-monitor-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --32x-sdk-monitor-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [max-lines] [start-frame]");
        Environment.Exit(1);
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 120;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    int maxLines = args.Length > 5 && int.TryParse(args[5], out int parsedMaxLines) ? parsedMaxLines : 4096;
    int startFrame = args.Length > 6 && int.TryParse(args[6], out int parsedStartFrame) ? parsedStartFrame : 0;
    TraceThirtyTwoXSdkMonitor(args[1], args[2], frames, instructionsPerFrame, maxLines, startFrame);
    return;
}

if (args[0].Equals("--m68k-memory-write-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --m68k-memory-write-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [address-start] [address-end] [max-lines] [start-frame]");
        Environment.Exit(1);
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 120;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    uint addressStart = args.Length > 5 ? ParseNumber(args[5]) : 0;
    uint addressEnd = args.Length > 6 ? ParseNumber(args[6]) : uint.MaxValue;
    int maxLines = args.Length > 7 && int.TryParse(args[7], out int parsedMaxLines) ? parsedMaxLines : 4096;
    int startFrame = args.Length > 8 && int.TryParse(args[8], out int parsedStartFrame) ? parsedStartFrame : 0;
    TraceM68kMemoryWrites(args[1], args[2], frames, instructionsPerFrame, addressStart, addressEnd, maxLines, startFrame);
    return;
}

if (args[0].Equals("--m68k-memory-read-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --m68k-memory-read-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [address-start] [address-end] [max-lines] [start-frame]");
        Environment.Exit(1);
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 120;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    uint addressStart = args.Length > 5 ? ParseNumber(args[5]) : 0;
    uint addressEnd = args.Length > 6 ? ParseNumber(args[6]) : uint.MaxValue;
    int maxLines = args.Length > 7 && int.TryParse(args[7], out int parsedMaxLines) ? parsedMaxLines : 4096;
    int startFrame = args.Length > 8 && int.TryParse(args[8], out int parsedStartFrame) ? parsedStartFrame : 0;
    TraceM68kMemoryReads(args[1], args[2], frames, instructionsPerFrame, addressStart, addressEnd, maxLines, startFrame);
    return;
}

if (args[0].Equals("--m68k-interrupt-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --m68k-interrupt-trace <rom-file> <output.csv> [frames] [instructions-per-frame] [max-lines] [start-frame]");
        Environment.Exit(1);
    }

    int frames = args.Length > 3 && int.TryParse(args[3], out int parsedFrames) ? parsedFrames : 120;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 300_000;
    int maxLines = args.Length > 5 && int.TryParse(args[5], out int parsedMaxLines) ? parsedMaxLines : 4096;
    int startFrame = args.Length > 6 && int.TryParse(args[6], out int parsedStartFrame) ? parsedStartFrame : 0;
    TraceM68kInterrupts(args[1], args[2], frames, instructionsPerFrame, maxLines, startFrame);
    return;
}

if (args[0].Equals("--inspect", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: mdsharp --inspect <rom-file> [frames] [instructions-per-frame] [address] [bytes]");
        Environment.Exit(1);
    }

    int frames = args.Length > 2 && int.TryParse(args[2], out int parsedFrames) ? parsedFrames : 60;
    int instructionsPerFrame = args.Length > 3 && int.TryParse(args[3], out int parsedInstructions) ? parsedInstructions : 200_000;
    uint address = args.Length > 4 ? ParseNumber(args[4]) : 0x00FF_FB00;
    int bytes = args.Length > 5 && int.TryParse(args[5], out int parsedBytes) ? parsedBytes : 0x80;
    InspectMachine(args[1], frames, instructionsPerFrame, address, bytes);
    return;
}

if (args[0].Equals("--watch", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 6)
    {
        Console.Error.WriteLine("Usage: mdsharp --watch <rom-file> <start-frame> <frames> <address> <bytes> [instructions-per-frame]");
        Environment.Exit(1);
    }

    int startFrame = int.Parse(args[2]);
    int frames = int.Parse(args[3]);
    uint address = ParseNumber(args[4]);
    int bytes = int.Parse(args[5]);
    int instructionsPerFrame = args.Length > 6 && int.TryParse(args[6], out int parsedInstructions) ? parsedInstructions : 1_000_000;
    WatchMemory(args[1], startFrame, frames, instructionsPerFrame, address, bytes);
    return;
}

if (args[0].Equals("--io-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --io-trace <rom-file> <start-frame> <frames> [instructions-per-frame]");
        Environment.Exit(1);
    }

    int startFrame = int.Parse(args[2]);
    int frames = int.Parse(args[3]);
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 1_000_000;
    TraceIo(args[1], startFrame, frames, instructionsPerFrame);
    return;
}

if (args[0].Equals("--bloodlines-io-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --bloodlines-io-trace <rom-file> <start-frame> <frames> [instructions-per-frame]");
        Environment.Exit(1);
    }

    int startFrame = int.Parse(args[2]);
    int frames = int.Parse(args[3]);
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedInstructions) ? parsedInstructions : 1_000_000;
    TraceIo(args[1], startFrame, frames, instructionsPerFrame, BloodlinesStartThenPlay, "bloodlines-script");
    return;
}

if (args[0].Equals("--pc-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --pc-trace <rom-file> <frames> [instructions] [instructions-per-frame]");
        Environment.Exit(1);
    }

    int frames = int.Parse(args[2]);
    int instructions = args.Length > 3 && int.TryParse(args[3], out int parsedInstructions) ? parsedInstructions : 64;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedFrameInstructions) ? parsedFrameInstructions : 1_000_000;
    TracePc(args[1], frames, instructions, instructionsPerFrame);
    return;
}

if (args[0].Equals("--svp-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --svp-trace <rom-file> <output.csv> <script> [frames] [instructions-per-frame] [max-lines] [pc-list] [start-frame]");
        Environment.Exit(1);
    }

    Func<int, ControllerInput> input = ResolveControllerInputScript(args[3]);
    int frames = args.Length > 4 && int.TryParse(args[4], out int parsedFrames) ? parsedFrames : 7200;
    int instructionsPerFrame = args.Length > 5 && int.TryParse(args[5], out int parsedInstructions) ? parsedInstructions : 300_000;
    int maxLines = args.Length > 6 && int.TryParse(args[6], out int parsedLines) ? parsedLines : 4096;
    int[] pcs = ParseSvpTracePcList(args.Length > 7 ? args[7] : "$56,$104,$124,$12A,$E8,$EA,$EC,$EE,$F0,$F2,$F4,$F6,$F8,$126,$128");
    int startFrame = args.Length > 8 && int.TryParse(args[8], out int parsedStartFrame) ? parsedStartFrame : 0;
    TraceSvp(args[1], args[2], input, frames, instructionsPerFrame, maxLines, pcs, startFrame);
    return;
}

if (args[0].Equals("--svp-pm-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --svp-pm-trace <rom-file> <output.csv> <script> [frames] [instructions-per-frame] [max-lines] [start-frame]");
        Environment.Exit(1);
    }

    Func<int, ControllerInput> input = ResolveControllerInputScript(args[3]);
    int frames = args.Length > 4 && int.TryParse(args[4], out int parsedFrames) ? parsedFrames : 7200;
    int instructionsPerFrame = args.Length > 5 && int.TryParse(args[5], out int parsedInstructions) ? parsedInstructions : 300_000;
    int maxLines = args.Length > 6 && int.TryParse(args[6], out int parsedLines) ? parsedLines : 16_384;
    int startFrame = args.Length > 7 && int.TryParse(args[7], out int parsedStartFrame) ? parsedStartFrame : 0;
    TraceSvpPmIo(args[1], args[2], input, frames, instructionsPerFrame, maxLines, startFrame);
    return;
}

if (args[0].Equals("--svp-pointer-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --svp-pointer-trace <rom-file> <output.csv> <script> [frames] [instructions-per-frame] [max-lines] [pc-list] [start-frame]");
        Environment.Exit(1);
    }

    Func<int, ControllerInput> input = ResolveControllerInputScript(args[3]);
    int frames = args.Length > 4 && int.TryParse(args[4], out int parsedFrames) ? parsedFrames : 7200;
    int instructionsPerFrame = args.Length > 5 && int.TryParse(args[5], out int parsedInstructions) ? parsedInstructions : 300_000;
    int maxLines = args.Length > 6 && int.TryParse(args[6], out int parsedLines) ? parsedLines : 16_384;
    int[] pcs = ParseSvpTracePcList(args.Length > 7 ? args[7] : "*");
    int startFrame = args.Length > 8 && int.TryParse(args[8], out int parsedStartFrame) ? parsedStartFrame : 0;
    TraceSvpPointers(args[1], args[2], input, frames, instructionsPerFrame, maxLines, pcs, startFrame);
    return;
}

if (args[0].Equals("--svp-write-history", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 5)
    {
        Console.Error.WriteLine("Usage: mdsharp --svp-write-history <rom-file> <output.csv> <script> <dram-words> [frames] [instructions-per-frame] [history] [max-events] [start-frame]");
        Environment.Exit(1);
    }

    Func<int, ControllerInput> input = ResolveControllerInputScript(args[3]);
    int[] dramWords = ParseSvpTracePcList(args[4]);
    int frames = args.Length > 5 && int.TryParse(args[5], out int parsedFrames) ? parsedFrames : 7200;
    int instructionsPerFrame = args.Length > 6 && int.TryParse(args[6], out int parsedInstructions) ? parsedInstructions : 300_000;
    int history = args.Length > 7 && int.TryParse(args[7], out int parsedHistory) ? Math.Max(1, parsedHistory) : 48;
    int maxEvents = args.Length > 8 && int.TryParse(args[8], out int parsedEvents) ? parsedEvents : 64;
    int startFrame = args.Length > 9 && int.TryParse(args[9], out int parsedStartFrame) ? parsedStartFrame : 0;
    TraceSvpWriteHistory(args[1], args[2], input, dramWords, frames, instructionsPerFrame, history, maxEvents, startFrame);
    return;
}

if (args[0].Equals("--svp-bus-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --svp-bus-trace <rom-file> <output.csv> <script> [frames] [instructions-per-frame] [max-lines] [address-list] [start-frame]");
        Environment.Exit(1);
    }

    Func<int, ControllerInput> input = ResolveControllerInputScript(args[3]);
    int frames = args.Length > 4 && int.TryParse(args[4], out int parsedFrames) ? parsedFrames : 7200;
    int instructionsPerFrame = args.Length > 5 && int.TryParse(args[5], out int parsedInstructions) ? parsedInstructions : 300_000;
    int maxLines = args.Length > 6 && int.TryParse(args[6], out int parsedLines) ? parsedLines : 65_536;
    int[] addresses = ParseSvpTracePcList(args.Length > 7 ? args[7] : "*");
    int startFrame = args.Length > 8 && int.TryParse(args[8], out int parsedStartFrame) ? parsedStartFrame : 0;
    TraceSvpBus(args[1], args[2], input, frames, instructionsPerFrame, maxLines, addresses, startFrame);
    return;
}

if (args[0].Equals("--dma-word-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --dma-word-trace <rom-file> <output.csv> <script> [frames] [instructions-per-frame] [max-lines] [start-frame] [source-prefix]");
        Environment.Exit(1);
    }

    Func<int, ControllerInput> input = ResolveControllerInputScript(args[3]);
    int frames = args.Length > 4 && int.TryParse(args[4], out int parsedFrames) ? parsedFrames : 7200;
    int instructionsPerFrame = args.Length > 5 && int.TryParse(args[5], out int parsedInstructions) ? parsedInstructions : 300_000;
    int maxLines = args.Length > 6 && int.TryParse(args[6], out int parsedLines) ? parsedLines : 65_536;
    int startFrame = args.Length > 7 && int.TryParse(args[7], out int parsedStartFrame) ? parsedStartFrame : 0;
    uint? sourcePrefix = args.Length > 8 ? ParseOptionalHexPrefix(args[8]) : null;
    TraceDmaWords(args[1], args[2], input, frames, instructionsPerFrame, maxLines, startFrame, sourcePrefix);
    return;
}

if (args[0].Equals("--vdp-plane-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 5)
    {
        Console.Error.WriteLine("Usage: mdsharp --vdp-plane-trace <rom-file> <output.csv> <script> <frames> [instructions-per-frame] [planeA|planeB] [y] [start-x] [end-x] [step]");
        Environment.Exit(1);
    }

    Func<int, ControllerInput> input = ResolveControllerInputScript(args[3]);
    int frames = int.Parse(args[4], CultureInfo.InvariantCulture);
    int instructionsPerFrame = args.Length > 5 && int.TryParse(args[5], out int parsedInstructions) ? parsedInstructions : 300_000;
    VdpDebugLayer layer = args.Length > 6 && args[6].Equals("planeB", StringComparison.OrdinalIgnoreCase) ? VdpDebugLayer.PlaneB : VdpDebugLayer.PlaneA;
    int y = args.Length > 7 && int.TryParse(args[7], out int parsedY) ? parsedY : Vdp.ScreenHeight / 2;
    int startX = args.Length > 8 && int.TryParse(args[8], out int parsedStartX) ? parsedStartX : 0;
    int endX = args.Length > 9 && int.TryParse(args[9], out int parsedEndX) ? parsedEndX : Vdp.ScreenWidth - 1;
    int step = args.Length > 10 && int.TryParse(args[10], out int parsedStep) ? Math.Max(1, parsedStep) : 1;
    TraceVdpPlane(args[1], args[2], input, frames, instructionsPerFrame, layer, y, startX, endX, step);
    return;
}

if (args[0].Equals("--svp-vdp-correlate", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 5)
    {
        Console.Error.WriteLine("Usage: mdsharp --svp-vdp-correlate <rom-file> <output.csv> <script> <frames> [instructions-per-frame] [planeA|planeB] [y] [start-x] [end-x] [step] [trace-start-frame]");
        Environment.Exit(1);
    }

    Func<int, ControllerInput> input = ResolveControllerInputScript(args[3]);
    int frames = int.Parse(args[4], CultureInfo.InvariantCulture);
    int instructionsPerFrame = args.Length > 5 && int.TryParse(args[5], out int parsedInstructions) ? parsedInstructions : 300_000;
    VdpDebugLayer layer = args.Length > 6 && args[6].Equals("planeB", StringComparison.OrdinalIgnoreCase) ? VdpDebugLayer.PlaneB : VdpDebugLayer.PlaneA;
    int y = args.Length > 7 && int.TryParse(args[7], out int parsedY) ? parsedY : Vdp.ScreenHeight / 2;
    int startX = args.Length > 8 && int.TryParse(args[8], out int parsedStartX) ? parsedStartX : 0;
    int endX = args.Length > 9 && int.TryParse(args[9], out int parsedEndX) ? parsedEndX : Vdp.ScreenWidth - 1;
    int step = args.Length > 10 && int.TryParse(args[10], out int parsedStep) ? Math.Max(1, parsedStep) : 1;
    int traceStartFrame = args.Length > 11 && int.TryParse(args[11], out int parsedTraceStart) ? parsedTraceStart : Math.Max(0, frames - 240);
    CorrelateSvpVdp(args[1], args[2], input, frames, instructionsPerFrame, layer, y, startX, endX, step, traceStartFrame);
    return;
}

if (args[0].Equals("--virtua-racing-layout-check", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --virtua-racing-layout-check <rom-file> <output-folder> [script] [frames] [instructions-per-frame] [imageformat.txt] [--fail-on-mismatch]");
        Environment.Exit(1);
    }

    bool failOnMismatch = args.Any(arg => arg.Equals("--fail-on-mismatch", StringComparison.OrdinalIgnoreCase));
    string script = args.Length > 3 ? args[3] : "virtua-racing-drive";
    Func<int, ControllerInput> input = ResolveControllerInputScript(script);
    int frames = args.Length > 4 && int.TryParse(args[4], out int parsedFrames) ? parsedFrames : 7200;
    int instructionsPerFrame = args.Length > 5 && int.TryParse(args[5], out int parsedInstructions) ? parsedInstructions : 300_000;
    string? imageFormatPath = args.Skip(6).FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal));
    imageFormatPath ??= FindDefaultVirtuaRacingImageFormatPath();
    bool passed = CheckVirtuaRacingLayout(args[1], args[2], input, script, frames, instructionsPerFrame, imageFormatPath);
    if (failOnMismatch && !passed)
    {
        Environment.Exit(2);
    }

    return;
}

if (args[0].Equals("--bloodlines-pc-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --bloodlines-pc-trace <rom-file> <frames> [instructions] [instructions-per-frame]");
        Environment.Exit(1);
    }

    int frames = int.Parse(args[2]);
    int instructions = args.Length > 3 && int.TryParse(args[3], out int parsedInstructions) ? parsedInstructions : 64;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedFrameInstructions) ? parsedFrameInstructions : 1_000_000;
    TracePc(args[1], frames, instructions, instructionsPerFrame, BloodlinesStartThenPlay);
    return;
}

if (args[0].Equals("--bloodlines-render", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --bloodlines-render <rom-file> <output.ppm> <frames> [instructions-per-frame]");
        Environment.Exit(1);
    }

    int frames = int.Parse(args[3]);
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedFrameInstructions) ? parsedFrameInstructions : 1_000_000;
    RenderScriptedRom(args[1], args[2], frames, instructionsPerFrame, BloodlinesStartThenPlay);
    return;
}

if (args[0].Equals("--streets-pc-trace", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --streets-pc-trace <rom-file> <frames> [instructions] [instructions-per-frame]");
        Environment.Exit(1);
    }

    int frames = int.Parse(args[2]);
    int instructions = args.Length > 3 && int.TryParse(args[3], out int parsedInstructions) ? parsedInstructions : 64;
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedFrameInstructions) ? parsedFrameInstructions : 1_000_000;
    TracePc(args[1], frames, instructions, instructionsPerFrame, StreetsStartAndSelect);
    return;
}

if (args[0].Equals("--streets-render", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Usage: mdsharp --streets-render <rom-file> <output.ppm> <frames> [instructions-per-frame]");
        Environment.Exit(1);
    }

    int frames = int.Parse(args[3]);
    int instructionsPerFrame = args.Length > 4 && int.TryParse(args[4], out int parsedFrameInstructions) ? parsedFrameInstructions : 1_000_000;
    RenderScriptedRom(args[1], args[2], frames, instructionsPerFrame, StreetsStartAndSelect);
    return;
}

if (args[0].Equals("--sonic-bench", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --sonic-bench <rom-file> <output-folder> [instructions-per-frame]");
        Environment.Exit(1);
    }

    int instructionsPerFrame = args.Length > 3 && int.TryParse(args[3], out int parsedInstructions) ? parsedInstructions : 1_000_000;
    RunSonicBench(args[1], args[2], instructionsPerFrame);
    return;
}

if (args[0].Equals("--bloodlines-bench", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: mdsharp --bloodlines-bench <rom-file> <output-folder> [instructions-per-frame]");
        Environment.Exit(1);
    }

    int instructionsPerFrame = args.Length > 3 && int.TryParse(args[3], out int parsedInstructions) ? parsedInstructions : 1_000_000;
    RunBloodlinesBench(args[1], args[2], instructionsPerFrame);
    return;
}

RunSingleRom(args[0], args.Length > 1 && int.TryParse(args[1], out int parsed) ? parsed : DefaultInstructionBudget);

void RunSingleRom(string path, int instructionBudget)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(path);
    MegaDrive machine = new(cartridge, IsPalRegion(cartridge));
    machine.Reset();

    Console.WriteLine($"Loaded: {Path.GetFileName(path)}");
    Console.WriteLine($"Domestic name: {cartridge.Header.DomesticName}");
    Console.WriteLine($"Overseas name: {cartridge.Header.OverseasName}");
    Console.WriteLine($"Product code: {cartridge.Header.ProductCode}");
    Console.WriteLine($"Region: {cartridge.Header.Region}");
    Console.WriteLine($"ROM size: {cartridge.Length:N0} bytes");
    Console.WriteLine($"Initial SSP=${machine.MainCpu.A[7]:X8} PC=${machine.MainCpu.PC:X8}");

    SmokeResult result = RunSmoke(path, instructionBudget);
    Console.WriteLine($"{result.Status}: {result.Detail}");
    Console.WriteLine(result.State);
}

void SweepFolder(string folder, int instructionBudget)
{
    string fullFolder = Path.GetFullPath(folder);
    if (!Directory.Exists(fullFolder))
    {
        Console.Error.WriteLine($"ROM folder not found: {fullFolder}");
        Environment.Exit(1);
    }

    string[] files = Directory
        .EnumerateFiles(fullFolder, "*.*", SearchOption.AllDirectories)
        .Where(path => romExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    Console.WriteLine($"ROM sweep: {files.Length} file(s), {instructionBudget:N0} instruction budget");
    Console.WriteLine("status,instructions,pc,rom,detail");

    foreach (string file in files)
    {
        SmokeResult result = RunSmoke(file, instructionBudget);
        string relative = Path.GetRelativePath(fullFolder, file);
        Console.WriteLine($"{result.Status},{result.Instructions},${result.PC:X8},\"{relative}\",\"{EscapeCsv(result.Detail)}\"");
    }
}

void RenderRom(string romPath, string outputPath, int frames, int instructionsPerFrame, bool traceCpu, bool traceVdp)
{
    MegaDrive machine = CreateMachine(romPath);
    machine.Vdp.TraceEnabled = traceVdp;
    machine.MainCpu.TraceEnabled = traceCpu;
    List<ThirtyTwoXDevice.SystemRegisterWriteTrace> thirtyTwoXCommWrites = [];
    if (traceCpu && machine.Bus.ThirtyTwoX is ThirtyTwoXDevice traceThirtyTwoX)
    {
        traceThirtyTwoX.SystemRegisterWriteObserver = write =>
        {
            if (write.Offset is >= ThirtyTwoXHardwareProfile.CommunicationPortOffset and < ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8 &&
                thirtyTwoXCommWrites.Count < 256)
            {
                thirtyTwoXCommWrites.Add(write);
            }
        };
    }

    machine.Reset();

    int completedFrames = 0;
    try
    {
        for (; completedFrames < frames; completedFrames++)
        {
            machine.RunFrame(instructionsPerFrame);
        }
    }
    catch (M68kException ex)
    {
        Console.WriteLine($"Execution stopped while rendering: {ex.Message}");
    }

    byte[] framebuffer = machine.RenderFrameRgb();
    int nonBackgroundPixels = CountNonBackgroundPixels(machine.Vdp, framebuffer);
    WritePpm(outputPath, Vdp.ScreenWidth, Vdp.ScreenHeight, framebuffer);
    WriteBmp(Path.ChangeExtension(outputPath, ".bmp"), Vdp.ScreenWidth, Vdp.ScreenHeight, framebuffer);
    string atlasPath = Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".",
        $"{Path.GetFileNameWithoutExtension(outputPath)}.tiles.ppm");
    WritePpm(atlasPath, 32 * 8, 16 * 8, machine.Vdp.RenderTileAtlasRgb());
    WriteBmp(Path.ChangeExtension(atlasPath, ".bmp"), 32 * 8, 16 * 8, machine.Vdp.RenderTileAtlasRgb());
    int firstTile = FindFirstNonzeroTile(machine.Vdp);
    string nonzeroAtlasPath = Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".",
        $"{Path.GetFileNameWithoutExtension(outputPath)}.tiles-nonzero.ppm");
    WritePpm(nonzeroAtlasPath, 32 * 8, 16 * 8, machine.Vdp.RenderTileAtlasRgb(startTile: firstTile));
    WriteBmp(Path.ChangeExtension(nonzeroAtlasPath, ".bmp"), 32 * 8, 16 * 8, machine.Vdp.RenderTileAtlasRgb(startTile: firstTile));
    Console.WriteLine($"Rendered {completedFrames} frame(s) from {Path.GetFileName(romPath)} to {Path.GetFullPath(outputPath)}");
    Console.WriteLine($"Rendered BMP to {Path.GetFullPath(Path.ChangeExtension(outputPath, ".bmp"))}");
    Console.WriteLine($"Rendered tile atlas to {atlasPath}");
    Console.WriteLine($"Rendered tile atlas from tile ${firstTile:X3} to {nonzeroAtlasPath}");
    Console.WriteLine($"Rendered frame mode={machine.Vdp.LastRenderMode} nonBackgroundPixels={nonBackgroundPixels:N0}");
    if (machine.Bus.ThirtyTwoX is ThirtyTwoXDevice thirtyTwoX)
    {
        if (traceCpu && thirtyTwoXCommWrites.Count > 0)
        {
            Console.WriteLine("32X comm writes:");
            foreach (ThirtyTwoXDevice.SystemRegisterWriteTrace write in thirtyTwoXCommWrites)
            {
                Console.WriteLine($"{write.Source} ${write.Offset:X2}=${write.Value:X2}");
            }
        }

        Console.WriteLine(
            $"32X mode={thirtyTwoX.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset) & 0x03} " +
            $"fbctl=${thirtyTwoX.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset):X4} " +
            $"draw={thirtyTwoX.DrawFrameBufferIndex} display={thirtyTwoX.DisplayFrameBufferIndex} " +
            $"swapPending={thirtyTwoX.FrameBufferSwapPending} vblank={thirtyTwoX.VBlank} hblank={thirtyTwoX.HBlank} " +
            $"compositeMode={thirtyTwoX.LastCompositeMode} fallback={thirtyTwoX.LastCompositeUsedFallback} " +
            $"vdpWrites={thirtyTwoX.VdpRegisterWriteCount} modeWrites={thirtyTwoX.BitmapModeWriteCount}/${thirtyTwoX.LastBitmapModeWrite:X4} fbWrites={thirtyTwoX.FrameBufferControlWriteCount}/${thirtyTwoX.LastFrameBufferControlWrite:X4} fbBytes={thirtyTwoX.FrameBufferByteWriteCount:N0} fbDenied={thirtyTwoX.DeniedFrameBufferAccessCount:N0} palBytes={thirtyTwoX.PaletteByteWriteCount:N0} dreqWords={thirtyTwoX.DreqDmaWordTransferCount:N0} compositePixels={thirtyTwoX.LastCompositeWrittenPixels:N0} " +
            $"irqMask=${thirtyTwoX.MasterInterruptMask:X4}/${thirtyTwoX.SlaveInterruptMask:X4} pendingIrq={thirtyTwoX.MasterSh2.PendingInterruptLevel}/{thirtyTwoX.SlaveSh2.PendingInterruptLevel} " +
            $"bootPending={thirtyTwoX.BootRomHandshakePending} bootRead={thirtyTwoX.BootRomSignatureRead} bootLaunch={thirtyTwoX.BootRomLaunchPending} postBoot={thirtyTwoX.BootRomPostStartSignaturePending}/{thirtyTwoX.BootRomPostStartSignatureHiddenFromSh2}/${thirtyTwoX.BootRomPostStartSignatureReadMask:X2} " +
            $"fbNonzero={CountNonzeroBytes(thirtyTwoX.DisplayFrameBuffer):N0}/{CountNonzeroBytes(thirtyTwoX.DrawFrameBuffer):N0} " +
            $"palNonzero={CountNonzeroBytes(thirtyTwoX.Palette):N0} " +
            $"sh2Sched={machine.ThirtyTwoXScheduledInstructionRequests:N0} sh2Exec={machine.ThirtyTwoXExecutedInstructionSteps:N0} " +
            $"master=${thirtyTwoX.MasterSh2.PC:X8} slave=${thirtyTwoX.SlaveSh2.PC:X8}");
        Console.WriteLine($"32X sys={FormatThirtyTwoXWords(thirtyTwoX, system: true, 0x00, 0x40)}");
        Console.WriteLine($"32X vdp={FormatThirtyTwoXWords(thirtyTwoX, system: false, 0x00, 0x10)}");
        Console.WriteLine($"32X masterRegs={FormatSh2Registers(thirtyTwoX.MasterSh2)}");
        Console.WriteLine($"32X slaveRegs={FormatSh2Registers(thirtyTwoX.SlaveSh2)}");
        Console.WriteLine($"32X masterCode={FormatThirtyTwoXCodeWindow(thirtyTwoX, machine.Bus.Cartridge, thirtyTwoX.MasterSh2.PC)}");
        Console.WriteLine($"32X slaveCode={FormatThirtyTwoXCodeWindow(thirtyTwoX, machine.Bus.Cartridge, thirtyTwoX.SlaveSh2.PC)}");
    }

    Console.WriteLine(FormatState(machine));
    Console.WriteLine(FormatVdpState(machine.Vdp));
    if (traceCpu)
    {
        Console.WriteLine("CPU exception trace:");
        foreach (string entry in machine.MainCpu.ExceptionTrace)
        {
            Console.WriteLine(entry);
        }

        Console.WriteLine("CPU recent instruction trace:");
        foreach (string entry in machine.MainCpu.RecentInstructionTrace)
        {
            Console.WriteLine(entry);
        }
    }

    if (traceVdp)
    {
        Console.WriteLine("VDP trace:");
        foreach (string entry in machine.Vdp.TraceEvents)
        {
            Console.WriteLine(entry);
        }

        Console.WriteLine("VDP register writes:");
        foreach (Vdp.RegisterWrite write in machine.Vdp.RegisterWrites)
        {
            Console.WriteLine($"R{write.Register:D2}: ${write.PreviousValue:X2}->${write.Value:X2}");
        }

        Console.WriteLine("VDP control commands:");
        foreach (Vdp.ControlCommand command in machine.Vdp.ControlCommands)
        {
            Console.WriteLine($"code=${command.Code:X2} addr=${command.Address:X4} words=${command.FirstWord:X4},${command.SecondWord:X4}");
        }

        Console.WriteLine("VDP DMA events:");
        foreach (Vdp.DmaEvent dma in machine.Vdp.DmaEvents)
        {
            Console.WriteLine($"mode={dma.Mode} op={dma.Operation} code=${dma.Code:X2} source=${dma.SourceAddress:X6} dest=${dma.DestinationAddress:X4} words={dma.LengthWords}");
        }
    }
}

void RenderState(string romPath, string statePath, string outputPath, int frames, int instructionsPerFrame, Func<int, ControllerInput>? input = null, string inputName = "none")
{
    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();
    SaveStateSerializer.Load(machine, statePath);

    int completedFrames = 0;
    try
    {
        for (; completedFrames < frames; completedFrames++)
        {
            if (input is not null)
            {
                ControllerInput pressed = input(completedFrames);
                machine.Bus.Controller1.Pressed = pressed.Player1;
                machine.Bus.Controller2.Pressed = pressed.Player2;
            }

            machine.RunFrameCycles(instructionsPerFrame);
        }
    }
    catch (M68kException ex)
    {
        Console.WriteLine($"Execution stopped while rendering state: {ex.Message}");
    }

    machine.Bus.Controller1.Pressed = GenesisButton.None;
    machine.Bus.Controller2.Pressed = GenesisButton.None;
    byte[] framebuffer = machine.RenderFrameRgb();
    int nonBackgroundPixels = CountNonBackgroundPixels(machine.Vdp, framebuffer);
    WritePpm(outputPath, Vdp.ScreenWidth, Vdp.ScreenHeight, framebuffer);
    WriteBmp(Path.ChangeExtension(outputPath, ".bmp"), Vdp.ScreenWidth, Vdp.ScreenHeight, framebuffer);
    string layerBase = Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".",
        Path.GetFileNameWithoutExtension(outputPath));
    WriteBmp($"{layerBase}.planeA.bmp", Vdp.ScreenWidth, Vdp.ScreenHeight, machine.Vdp.RenderDebugLayerRgb(VdpDebugLayer.PlaneA));
    WriteBmp($"{layerBase}.planeB.bmp", Vdp.ScreenWidth, Vdp.ScreenHeight, machine.Vdp.RenderDebugLayerRgb(VdpDebugLayer.PlaneB));
    WriteBmp($"{layerBase}.sprites.bmp", Vdp.ScreenWidth, Vdp.ScreenHeight, machine.Vdp.RenderDebugLayerRgb(VdpDebugLayer.Sprites));
    WritePrioritySummaryCsv($"{layerBase}.priority.csv", machine.Vdp.LastFramePriorityPixels);
    string inputSuffix = input is null ? string.Empty : $" with script {inputName}";
    Console.WriteLine($"Rendered {completedFrames} frame(s) from state {Path.GetFileName(statePath)}{inputSuffix} to {Path.GetFullPath(outputPath)}");
    Console.WriteLine($"Rendered BMP to {Path.GetFullPath(Path.ChangeExtension(outputPath, ".bmp"))}");
    Console.WriteLine($"Rendered debug layers to {Path.GetFullPath(layerBase)}.planeA.bmp/.planeB.bmp/.sprites.bmp");
    Console.WriteLine($"Rendered priority summary to {Path.GetFullPath(layerBase)}.priority.csv");
    Console.WriteLine($"Rendered frame mode={machine.Vdp.LastRenderMode} nonBackgroundPixels={nonBackgroundPixels:N0}");
    if (machine.Bus.ThirtyTwoX is ThirtyTwoXDevice thirtyTwoX)
    {
        Console.WriteLine(
            $"32X mode={thirtyTwoX.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset) & 0x03} " +
            $"fbctl=${thirtyTwoX.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset):X4} " +
            $"draw={thirtyTwoX.DrawFrameBufferIndex} display={thirtyTwoX.DisplayFrameBufferIndex} " +
            $"compositeMode={thirtyTwoX.LastCompositeMode} compositePixels={thirtyTwoX.LastCompositeWrittenPixels:N0} " +
            $"fbNonzero={CountNonzeroBytes(thirtyTwoX.DisplayFrameBuffer):N0}/{CountNonzeroBytes(thirtyTwoX.DrawFrameBuffer):N0} " +
            $"palNonzero={CountNonzeroBytes(thirtyTwoX.Palette):N0} " +
            $"master=${thirtyTwoX.MasterSh2.PC:X8} slave=${thirtyTwoX.SlaveSh2.PC:X8}");
    }

    Console.WriteLine(FormatState(machine));
    Console.WriteLine(FormatVdpState(machine.Vdp));
}

void RenderSequence(string romPath, string outputFolder, int startFrame, int endFrame, int step, int instructionsPerFrame)
{
    if (startFrame < 1 || endFrame < startFrame)
    {
        throw new ArgumentOutOfRangeException(nameof(startFrame), "Frame range must start at 1 and end at or after the start frame.");
    }

    string fullOutputFolder = Path.GetFullPath(outputFolder);
    Directory.CreateDirectory(fullOutputFolder);

    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();

    int written = 0;
    for (int frame = 1; frame <= endFrame; frame++)
    {
        machine.RunFrameCycles(instructionsPerFrame);
        if (frame < startFrame || ((frame - startFrame) % step) != 0)
        {
            continue;
        }

        byte[] framebuffer = machine.RenderFrameRgb();
        string basePath = Path.Combine(fullOutputFolder, $"frame-{frame:000000}");
        WriteBmp(basePath + ".bmp", Vdp.ScreenWidth, Vdp.ScreenHeight, framebuffer);
        written++;
    }

    Console.WriteLine($"Rendered {written:N0} sequence frame(s) from {Path.GetFileName(romPath)} to {fullOutputFolder}");
    Console.WriteLine(FormatState(machine));
    Console.WriteLine(FormatVdpState(machine.Vdp));
}

void TraceSprites(string romPath, int frames, int instructionsPerFrame, Func<int, GenesisButton> input)
{
    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();

    int completedFrames = 0;
    try
    {
        for (; completedFrames < frames; completedFrames++)
        {
            GenesisButton pressed = input(completedFrames);
            machine.Bus.Controller1.Pressed = pressed;
            machine.Bus.Controller2.Pressed = pressed;
            machine.RunFrameCycles(instructionsPerFrame);
        }
    }
    catch (M68kException ex)
    {
        Console.WriteLine($"Execution stopped while tracing sprites: {ex.Message}");
    }

    machine.Bus.Controller1.Pressed = GenesisButton.None;
    machine.Bus.Controller2.Pressed = GenesisButton.None;

    _ = machine.RenderFrameRgb();
    Vdp vdp = machine.Vdp;
    ReadOnlySpan<byte> registers = vdp.Registers;
    int activeWidth = (registers[12] & 0x01) != 0 ? 320 : 256;
    int maxSprites = activeWidth == 320 ? 80 : 64;
    int maxSpritesPerLine = activeWidth == 320 ? 20 : 16;
    int maxSpritePixelsPerLine = activeWidth == 320 ? 320 : 256;
    int sat = (((registers[12] & 0x01) != 0 ? registers[5] & 0x7E : registers[5] & 0x7F) << 9) & 0xFFFF;
    bool interlaceDouble = (registers[12] & 0x06) == 0x06;
    int yOrigin = interlaceDouble ? 256 : 128;
    int yMask = interlaceDouble ? 0x03FF : 0x01FF;
    int tileHeight = interlaceDouble ? 16 : 8;
    bool[] visited = new bool[maxSprites];
    List<(int Order, int Index, ushort RawY, ushort SizeLink, ushort Attributes, ushort RawX, int X, int Y, int WidthTiles, int HeightTiles, int Link, bool Mask, bool Priority)> sprites = new();
    int spriteIndex = 0;

    for (int order = 0; order < maxSprites; order++)
    {
        if ((uint)spriteIndex >= (uint)maxSprites || visited[spriteIndex])
        {
            break;
        }

        visited[spriteIndex] = true;
        int entry = sat + (spriteIndex * 8);
        ushort rawY = ReadVramWordForTrace(vdp, entry);
        ushort sizeLink = ReadVramWordForTrace(vdp, entry + 2);
        ushort attributes = ReadVramWordForTrace(vdp, entry + 4);
        ushort rawX = ReadVramWordForTrace(vdp, entry + 6);
        int widthTiles = ((sizeLink >> 10) & 0x03) + 1;
        int heightTiles = ((sizeLink >> 8) & 0x03) + 1;
        int x = (rawX & 0x01FF) - 128;
        int y = (rawY & yMask) - yOrigin;
        int link = sizeLink & 0x7F;
        sprites.Add((order, spriteIndex, rawY, sizeLink, attributes, rawX, x, y, widthTiles, heightTiles, link, (rawX & 0x01FF) == 0, (attributes & 0x8000) != 0));
        if (link == 0)
        {
            break;
        }

        spriteIndex = link;
    }

    Console.WriteLine($"Sprite trace for {Path.GetFileName(romPath)} after {completedFrames:N0} frame(s)");
    Console.WriteLine(FormatState(machine));
    Console.WriteLine(FormatVdpState(vdp));
    Console.WriteLine("order,index,x,y,w,h,tile,pal,prio,mask,link,rawY,sizeLink,attr,rawX");
    foreach (var sprite in sprites)
    {
        int tile = sprite.Attributes & 0x07FF;
        int palette = (sprite.Attributes >> 13) & 0x03;
        Console.WriteLine($"{sprite.Order},{sprite.Index},{sprite.X},{sprite.Y},{sprite.WidthTiles},{sprite.HeightTiles},${tile:X3},{palette},{(sprite.Priority ? 1 : 0)},{(sprite.Mask ? 1 : 0)},{sprite.Link},${sprite.RawY:X4},${sprite.SizeLink:X4},${sprite.Attributes:X4},${sprite.RawX:X4}");
    }

    Console.WriteLine("line,lineSprites,onscreen,low,high,maskedLow,linePixels");
    int emittedLines = 0;
    for (int y = 0; y < Vdp.ScreenHeight; y++)
    {
        int lineSprites = 0;
        int onscreen = 0;
        int low = 0;
        int high = 0;
        int maskedLow = 0;
        int spritePixels = 0;
        bool lowPrioritySpritesMasked = false;

        foreach (var sprite in sprites)
        {
            int heightPixels = sprite.HeightTiles * tileHeight;
            if (y < sprite.Y || y >= sprite.Y + heightPixels)
            {
                continue;
            }

            if (sprite.Mask)
            {
                if (lineSprites > 0)
                {
                    lowPrioritySpritesMasked = true;
                }

                continue;
            }

            if (lowPrioritySpritesMasked && !sprite.Priority)
            {
                maskedLow++;
                continue;
            }

            int widthPixels = sprite.WidthTiles * 8;
            int remainingPixels = maxSpritePixelsPerLine - spritePixels;
            if (lineSprites >= maxSpritesPerLine || remainingPixels <= 0)
            {
                break;
            }

            int visiblePixels = Math.Min(widthPixels, remainingPixels);
            int visibleStart = Math.Max(0, sprite.X);
            int visibleEnd = Math.Min(activeWidth, sprite.X + visiblePixels);
            if (visibleEnd > visibleStart)
            {
                onscreen++;
            }

            lineSprites++;
            if (sprite.Priority)
            {
                high++;
            }
            else
            {
                low++;
            }

            spritePixels += widthPixels;
            if (visiblePixels < widthPixels)
            {
                break;
            }
        }

        if (lineSprites > 0 || maskedLow > 0)
        {
            Console.WriteLine($"{y},{lineSprites},{onscreen},{low},{high},{maskedLow},{spritePixels}");
            emittedLines++;
            if (emittedLines >= 80)
            {
                Console.WriteLine("...");
                break;
            }
        }
    }
}

ushort ReadVramWordForTrace(Vdp vdp, int address)
{
    ReadOnlySpan<byte> vram = vdp.Vram;
    int offset = address & 0xFFFF;
    return (ushort)((vram[offset] << 8) | vram[(offset + 1) & 0xFFFF]);
}

void RenderScriptedRom(string romPath, string outputPath, int frames, int instructionsPerFrame, Func<int, GenesisButton> input)
{
    RenderScriptedRomWithControllers(romPath, outputPath, frames, instructionsPerFrame, frame =>
    {
        GenesisButton buttons = input(frame);
        return new ControllerInput(buttons, buttons);
    }, enableSvpDiagnostics: true);
}

void RenderScriptedRomWithControllers(string romPath, string outputPath, int frames, int instructionsPerFrame, Func<int, ControllerInput> input, bool enableSvpDiagnostics, bool useLineVramSnapshots = true, bool stepSvpDuringDma = true, bool setZeroFlagOnMld = false, bool clearPmcOnAnyAlRead = false, bool returnZeroOnAlRead = false, bool requireBlindPmacSet = true, bool useModuloOnPointerWrites = false, bool useMameCycleTiming = false)
{
    MegaDrive machine = CreateMachine(romPath);
    machine.Vdp.UseLineVramSnapshots = useLineVramSnapshots;
    machine.Bus.StepSvpDuringDma = stepSvpDuringDma;
    if (machine.Bus.Svp is not null)
    {
        machine.Bus.Svp.SetZeroFlagOnMld = setZeroFlagOnMld;
        machine.Bus.Svp.ClearPmcOnAnyAlRead = clearPmcOnAnyAlRead;
        machine.Bus.Svp.ReturnZeroOnAlRead = returnZeroOnAlRead;
        machine.Bus.Svp.RequireBlindPmacSet = requireBlindPmacSet;
        machine.Bus.Svp.UseModuloOnPointerWrites = useModuloOnPointerWrites;
        machine.Bus.Svp.UseMameCycleTiming = useMameCycleTiming;
    }
    if (machine.Bus.Svp is not null && enableSvpDiagnostics)
    {
        machine.Bus.Svp.EnableDramWriteDiagnostics = true;
    }

    machine.Reset();

    int completedFrames = 0;
    try
    {
        for (; completedFrames < frames; completedFrames++)
        {
            ControllerInput pressed = input(completedFrames);
            machine.Bus.Controller1.Pressed = pressed.Player1;
            machine.Bus.Controller2.Pressed = pressed.Player2;
            machine.RunFrameCycles(instructionsPerFrame);
        }
    }
    catch (M68kException ex)
    {
        Console.WriteLine($"Execution stopped while rendering: {ex.Message}");
    }

    machine.Bus.Controller1.Pressed = GenesisButton.None;
    machine.Bus.Controller2.Pressed = GenesisButton.None;
    byte[] framebuffer = machine.RenderFrameRgb();
    int nonBackgroundPixels = CountNonBackgroundPixels(machine.Vdp, framebuffer);
    WritePpm(outputPath, Vdp.ScreenWidth, Vdp.ScreenHeight, framebuffer);
    WriteBmp(Path.ChangeExtension(outputPath, ".bmp"), Vdp.ScreenWidth, Vdp.ScreenHeight, framebuffer);
    string atlasPath = Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".",
        $"{Path.GetFileNameWithoutExtension(outputPath)}.tiles.bmp");
    WriteBmp(atlasPath, 32 * 8, 16 * 8, machine.Vdp.RenderTileAtlasRgb(startTile: FindFirstNonzeroTile(machine.Vdp)));
    string layerBase = Path.Combine(
        Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".",
        Path.GetFileNameWithoutExtension(outputPath));
    WriteBmp($"{layerBase}.planeA.bmp", Vdp.ScreenWidth, Vdp.ScreenHeight, machine.Vdp.RenderDebugLayerRgb(VdpDebugLayer.PlaneA));
    WriteBmp($"{layerBase}.planeB.bmp", Vdp.ScreenWidth, Vdp.ScreenHeight, machine.Vdp.RenderDebugLayerRgb(VdpDebugLayer.PlaneB));
    WriteBmp($"{layerBase}.sprites.bmp", Vdp.ScreenWidth, Vdp.ScreenHeight, machine.Vdp.RenderDebugLayerRgb(VdpDebugLayer.Sprites));
    List<string> dmaSourceAtlases = WriteSvpDmaSourceAtlases(machine, layerBase);
    Console.WriteLine($"Rendered {completedFrames} scripted frame(s) from {Path.GetFileName(romPath)} to {Path.GetFullPath(outputPath)}");
    Console.WriteLine($"Rendered BMP to {Path.GetFullPath(Path.ChangeExtension(outputPath, ".bmp"))}");
    Console.WriteLine($"Rendered tile atlas to {Path.GetFullPath(atlasPath)}");
    Console.WriteLine($"Rendered debug layers to {Path.GetFullPath(layerBase)}.planeA.bmp/.planeB.bmp/.sprites.bmp");
    if (dmaSourceAtlases.Count > 0)
    {
        Console.WriteLine($"Rendered SVP DMA source atlases: {string.Join(", ", dmaSourceAtlases.Select(Path.GetFileName))}");
    }

    Console.WriteLine($"Rendered frame mode={machine.Vdp.LastRenderMode} nonBackgroundPixels={nonBackgroundPixels:N0}");
    Console.WriteLine(FormatState(machine));
    foreach (string entry in machine.MainCpu.ExceptionTrace)
    {
        Console.WriteLine($"EXTRACE {entry}");
    }

    Console.WriteLine(FormatZ80Window(machine));
    Console.WriteLine(FormatVdpState(machine.Vdp));
    foreach (Vdp.DmaEvent dma in machine.Vdp.DmaEvents.TakeLast(12))
    {
        Console.WriteLine($"DMA mode={dma.Mode} op={dma.Operation} code=${dma.Code:X2} source=${dma.SourceAddress:X6} dest=${dma.DestinationAddress:X4} words={dma.LengthWords}");
    }

    if (machine.Bus.Svp is not null)
    {
        Console.WriteLine(FormatSvpState(machine.Bus.Svp));
        PrintSvpDmaWriteDiagnostics(machine);
        PrintSvpWriterOpcodeWindows(machine);
    }
}

string FormatSvpState(SvpDevice svp)
{
    SvpDevice.PmacDiagnostics pmac = svp.PmacStats;
    return $"SVP wait={(svp.IsWaiting ? "yes" : "no")} status=${svp.HostStatus:X4} result=${svp.HostResult:X4} unhandled={svp.UnhandledOpcodeCount} lastOp=${svp.LastUnhandledOpcode:X4}@${svp.LastUnhandledPc:X5} PMAC w(linear={pmac.DramLinearWrites}, cell={pmac.DramCellWrites}, iram={pmac.IramWrites}, other={pmac.UnhandledWrites}) r(rom={pmac.RomReads}, dram={pmac.DramReads}, other={pmac.UnhandledReads})";
}

void PrintSvpDmaWriteDiagnostics(MegaDrive machine)
{
    if (machine.Bus.Svp is null)
    {
        return;
    }

    foreach (Vdp.DmaEvent dma in machine.Vdp.DmaEvents
        .Where(dma => dma.Mode is 0 or 1 && dma.Operation == "68k-to-vdp" && IsSvpDmaSource(dma.SourceAddress))
        .TakeLast(4))
    {
        Console.WriteLine($"SVP DMA source writers source=${dma.SourceAddress:X6} dest=${dma.DestinationAddress:X4} words={dma.LengthWords}");
        IReadOnlyList<SvpDevice.DramWriteDiagnostic> diagnostics = machine.Bus.Svp.GetDramWriteDiagnostics(dma.SourceAddress, dma.LengthWords, maxEntries: 8);
        foreach (SvpDevice.DramWriteDiagnostic entry in diagnostics)
        {
            Console.WriteLine($"  bucket=${entry.BucketStartWord:X4} pc=${entry.Pc:X5} op=${entry.Opcode:X4} mode=${entry.Mode:X4} kind={entry.Kind} ovr={(entry.Overwrite ? 1 : 0)} count={entry.Count} last=${entry.LastWordAddress:X4}:${entry.LastValue:X4}");
        }

        IReadOnlyList<SvpDevice.DramWriteSample> samples = machine.Bus.Svp.GetRecentDramWriteSamples(dma.SourceAddress, dma.LengthWords, maxEntries: 12);
        foreach (SvpDevice.DramWriteSample sample in samples)
        {
            Console.WriteLine(
                $"  sample#{sample.Sequence} addr=${sample.WordAddress:X4} pc=${sample.Pc:X5} op=${sample.Opcode:X4} mode=${sample.Mode:X4} ovr={(sample.Overwrite ? 1 : 0)} prev=${sample.PreviousValue:X4} write=${sample.WrittenValue:X4} stored=${sample.StoredValue:X4} A=${sample.AccumulatorHigh:X4}:{sample.AccumulatorLow:X4} X=${sample.X:X4} Y=${sample.Y:X4} ST=${sample.Status:X4}");
        }
    }
}

void PrintSvpWriterOpcodeWindows(MegaDrive machine)
{
    if (machine.Bus.Svp is null)
    {
        return;
    }

    int[] pcs = machine.Vdp.DmaEvents
        .Where(dma => dma.Mode is 0 or 1 && dma.Operation == "68k-to-vdp" && IsSvpDmaSource(dma.SourceAddress))
        .TakeLast(4)
        .SelectMany(dma => machine.Bus.Svp.GetDramWriteDiagnostics(dma.SourceAddress, dma.LengthWords, maxEntries: 8))
        .Select(item => item.Pc)
        .Distinct()
        .Order()
        .ToArray();
    if (pcs.Length == 0)
    {
        return;
    }

    ushort[] iram = machine.Bus.Svp.CaptureState().Iram;
    Console.WriteLine("SVP writer opcode windows:");
    foreach (int pcByteOffset in pcs)
    {
        int wordPc = pcByteOffset >> 1;
        int start = Math.Max(0, wordPc - 4);
        int end = Math.Min(iram.Length - 1, wordPc + 8);
        Console.Write($"  pc=${pcByteOffset:X5}:");
        for (int word = start; word <= end; word++)
        {
            string marker = word == wordPc ? "*" : string.Empty;
            Console.Write($" {marker}{iram[word]:X4}");
        }

        Console.WriteLine();
        for (int word = start; word <= end; word++)
        {
            ushort? next = word + 1 < iram.Length ? iram[word + 1] : null;
            string marker = word == wordPc ? "*" : " ";
            Console.WriteLine($"    {marker}${word << 1:X5}: {iram[word]:X4}  {DisassembleSsp(iram[word], next)}");
        }
    }
}

string DisassembleSsp(ushort op, ushort? next)
{
    string[] registers =
    [
        "gr0", "x", "y", "a", "st", "stack", "pc", "p",
        "pm0", "pm1", "pm2", "xst", "pm4", "r13", "pmc", "al"
    ];

    string NextWord() => next.HasValue ? $"#${next.Value:X4}" : "#????";
    string RamAddress() => $"($00{op & 0x01FF:X3})";
    string PointerName()
    {
        int pointer = (op & 3) | ((op >> 6) & 4);
        string mode = ((op << 1) & 0x18) switch
        {
            0x08 => "+",
            0x10 => "-",
            0x18 => "++",
            _ => string.Empty,
        };
        return $"r{pointer}{mode}";
    }

    string Condition()
    {
        return (op & 0xF0) switch
        {
            0x00 => string.Empty,
            0x50 => (op & 0x100) == 0 ? "z " : "nz ",
            0x70 => (op & 0x100) == 0 ? "n " : "pl ",
            _ => $"cc${(op & 0xF0) >> 4:X} ",
        };
    }

    string AluOp(string mnemonic, string source) => $"{mnemonic} a,{source}";
    string AluImmediate(string mnemonic) => $"{mnemonic} a,{NextWord()}";
    string AluSmallImmediate(string mnemonic) => $"{mnemonic} a,#${op & 0xFF:X2}";
    string ModOp() => (op & 7) switch
    {
        2 => $"{Condition()}shr a",
        3 => $"{Condition()}shl a",
        6 => $"{Condition()}neg a",
        7 => $"{Condition()}abs a",
        _ => $"{Condition()}mod${op & 7:X}",
    };

    switch (op >> 9)
    {
        case 0x00:
            return op == 0 ? "nop" : $"ld {registers[(op >> 4) & 0x0F]},{registers[op & 0x0F]}";
        case 0x01:
            return $"ld {registers[(op >> 4) & 0x0F]},({PointerName()})";
        case 0x02:
            return $"ld ({PointerName()}),{registers[(op >> 4) & 0x0F]}";
        case 0x03:
            return $"ld a,{RamAddress()}";
        case 0x04:
            return $"ldi {registers[(op >> 4) & 0x0F]},{NextWord()}";
        case 0x05:
            return $"ld {registers[(op >> 4) & 0x0F]},(({PointerName()}))";
        case 0x06:
            return $"ldi ({PointerName()}),{NextWord()}";
        case 0x07:
            return $"ld {RamAddress()},a";
        case 0x09:
            return $"ld {registers[(op >> 4) & 0x0F]},r{(op & 3) | ((op >> 6) & 4)}";
        case 0x0A:
            return $"ld r{(op & 3) | ((op >> 6) & 4)},{registers[(op >> 4) & 0x0F]}";
        case >= 0x0C and <= 0x0F:
            return $"ldi r{(op >> 8) & 7},#${op & 0xFF:X2}";
        case 0x10: return AluOp("sub", registers[op & 0x0F]);
        case 0x11: return AluOp("sub", $"({PointerName()})");
        case 0x13: return AluOp("sub", RamAddress());
        case 0x14: return AluImmediate("sub");
        case 0x15: return AluOp("sub", $"(({PointerName()}))");
        case 0x19: return AluOp("sub", $"r{(op & 3) | ((op >> 6) & 4)}");
        case 0x1B: return $"mpys ({PointerName()}),({(op >> 4) & 3 | ((op >> 5) & 4)})";
        case 0x1C: return AluSmallImmediate("sub");
        case 0x24: return $"{Condition()}call {NextWord()}";
        case 0x25: return $"ld {registers[(op >> 4) & 0x0F]},(a)";
        case 0x26: return $"{Condition()}bra {NextWord()}";
        case 0x30: return AluOp("cmp", registers[op & 0x0F]);
        case 0x31: return AluOp("cmp", $"({PointerName()})");
        case 0x33: return AluOp("cmp", RamAddress());
        case 0x34: return AluImmediate("cmp");
        case 0x35: return AluOp("cmp", $"(({PointerName()}))");
        case 0x39: return AluOp("cmp", $"r{(op & 3) | ((op >> 6) & 4)}");
        case 0x3C: return AluSmallImmediate("cmp");
        case 0x40: return AluOp("add", registers[op & 0x0F]);
        case 0x41: return AluOp("add", $"({PointerName()})");
        case 0x43: return AluOp("add", RamAddress());
        case 0x44: return AluImmediate("add");
        case 0x45: return AluOp("add", $"(({PointerName()}))");
        case 0x48: return ModOp();
        case 0x49: return AluOp("add", $"r{(op & 3) | ((op >> 6) & 4)}");
        case 0x4B: return $"mpya ({PointerName()}),({(op >> 4) & 3 | ((op >> 5) & 4)})";
        case 0x4C: return AluSmallImmediate("add");
        case 0x50: return AluOp("and", registers[op & 0x0F]);
        case 0x51: return AluOp("and", $"({PointerName()})");
        case 0x53: return AluOp("and", RamAddress());
        case 0x54: return AluImmediate("and");
        case 0x55: return AluOp("and", $"(({PointerName()}))");
        case 0x59: return AluOp("and", $"r{(op & 3) | ((op >> 6) & 4)}");
        case 0x5B: return $"mld ({PointerName()}),({(op >> 4) & 3 | ((op >> 5) & 4)})";
        case 0x5C: return AluSmallImmediate("and");
        case 0x60: return AluOp("or", registers[op & 0x0F]);
        case 0x61: return AluOp("or", $"({PointerName()})");
        case 0x63: return AluOp("or", RamAddress());
        case 0x64: return AluImmediate("or");
        case 0x65: return AluOp("or", $"(({PointerName()}))");
        case 0x69: return AluOp("or", $"r{(op & 3) | ((op >> 6) & 4)}");
        case 0x6C: return AluSmallImmediate("or");
        case 0x70: return AluOp("eor", registers[op & 0x0F]);
        case 0x71: return AluOp("eor", $"({PointerName()})");
        case 0x73: return AluOp("eor", RamAddress());
        case 0x74: return AluImmediate("eor");
        case 0x75: return AluOp("eor", $"(({PointerName()}))");
        case 0x79: return AluOp("eor", $"r{(op & 3) | ((op >> 6) & 4)}");
        case 0x7C: return AluSmallImmediate("eor");
        default:
            return $"? group ${op >> 9:X2}";
    }
}

List<string> WriteSvpDmaSourceAtlases(MegaDrive machine, string pathBase)
{
    List<string> paths = new();
    if (machine.Bus.Svp is null)
    {
        return paths;
    }

    foreach (Vdp.DmaEvent dma in machine.Vdp.DmaEvents
        .Where(dma => dma.Mode is 0 or 1 && dma.Operation == "68k-to-vdp" && IsSvpDmaSource(dma.SourceAddress))
        .TakeLast(4))
    {
        string path = $"{pathBase}.svp-${dma.SourceAddress:X6}-to-${dma.DestinationAddress:X4}.bmp";
        WriteBmp(path, 32 * 8, 16 * 8, RenderSvpDmaSourceAtlas(machine.Bus.Svp, dma.SourceAddress, dma.LengthWords));
        paths.Add(Path.GetFullPath(path));
    }

    return paths;
}

bool IsSvpDmaSource(uint sourceAddress)
{
    uint address = sourceAddress & 0x00FF_FFFE;
    return address is >= 0x30_0000 and <= 0x31_FFFE or >= 0x39_0000 and <= 0x3A_FFFE;
}

byte[] RenderSvpDmaSourceAtlas(SvpDevice svp, uint sourceAddress, int lengthWords, int columns = 32, int rows = 16)
{
    int width = columns * 8;
    int height = rows * 8;
    byte[] framebuffer = new byte[width * height * 3];
    int sourceBytes = Math.Max(0, lengthWords * 2);
    int tileCount = Math.Min(columns * rows, sourceBytes / 32);

    for (int tile = 0; tile < tileCount; tile++)
    {
        int tileX = (tile % columns) * 8;
        int tileY = (tile / columns) * 8;
        int tileByteOffset = tile * 32;
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                int byteOffset = tileByteOffset + (y * 4) + (x / 2);
                byte packed = ReadSvpDmaByte(svp, sourceAddress + (uint)byteOffset);
                int color = (x & 1) == 0 ? packed >> 4 : packed & 0x0F;
                byte shade = (byte)(color * 17);
                int offset = (((tileY + y) * width) + tileX + x) * 3;
                framebuffer[offset] = shade;
                framebuffer[offset + 1] = shade;
                framebuffer[offset + 2] = shade;
            }
        }
    }

    return framebuffer;
}

byte ReadSvpDmaByte(SvpDevice svp, uint address)
{
    ushort word = svp.ReadWord(address & 0x00FF_FFFE);
    return (address & 1) == 0 ? (byte)(word >> 8) : (byte)word;
}

void RenderInputMovie(string romPath, string moviePath, string outputPath, int frames, int instructionsPerFrame)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    InputMovie movie = InputMovie.Load(moviePath);
    if (!movie.Matches(cartridge))
    {
        Console.WriteLine("Warning: movie ROM hash does not match the supplied ROM.");
    }

    movie.RestoreInitialSaveRam(cartridge);
    MegaDrive machine = new(cartridge, IsPalRegion(cartridge));
    machine.Reset();

    int completedFrames = 0;
    try
    {
        for (; completedFrames < frames; completedFrames++)
        {
            machine.Bus.Controller1.Pressed = movie.GetButtons(completedFrames, playerIndex: 0);
            machine.Bus.Controller2.Pressed = movie.GetButtons(completedFrames, playerIndex: 1);
            machine.RunFrameCycles(instructionsPerFrame);
        }
    }
    catch (M68kException ex)
    {
        Console.WriteLine($"Execution stopped while rendering movie: {ex.Message}");
    }

    machine.Bus.Controller1.Pressed = GenesisButton.None;
    machine.Bus.Controller2.Pressed = GenesisButton.None;
    byte[] framebuffer = machine.RenderFrameRgb();
    int nonBackgroundPixels = CountNonBackgroundPixels(machine.Vdp, framebuffer);
    WritePpm(outputPath, Vdp.ScreenWidth, Vdp.ScreenHeight, framebuffer);
    WriteBmp(Path.ChangeExtension(outputPath, ".bmp"), Vdp.ScreenWidth, Vdp.ScreenHeight, framebuffer);
    Console.WriteLine($"Rendered {completedFrames} movie frame(s) from {Path.GetFileName(moviePath)} to {Path.GetFullPath(outputPath)}");
    Console.WriteLine($"Rendered BMP to {Path.GetFullPath(Path.ChangeExtension(outputPath, ".bmp"))}");
    Console.WriteLine($"Rendered frame mode={machine.Vdp.LastRenderMode} nonBackgroundPixels={nonBackgroundPixels:N0}");
    Console.WriteLine(FormatState(machine));
    Console.WriteLine(FormatVdpState(machine.Vdp));
}

void PrintMovieInfo(string moviePath)
{
    InputMovie movie = InputMovie.Load(moviePath);
    Console.WriteLine($"Movie: {Path.GetFullPath(moviePath)}");
    Console.WriteLine($"Version: {movie.Version}");
    Console.WriteLine($"ROM: {movie.RomName}");
    Console.WriteLine($"Product: {movie.RomProductCode}");
    Console.WriteLine($"SHA-256: {movie.RomSha256}");
    Console.WriteLine($"SRAM snapshot: {(string.IsNullOrWhiteSpace(movie.SaveRamBase64) ? "no" : "yes")}");
    Console.WriteLine($"Frames: {movie.FrameCount:N0}");
    if (movie.Frames.Count > 0)
    {
        Console.WriteLine($"First frame: {movie.Frames[0].Frame}");
        Console.WriteLine($"Last frame: {movie.Frames[^1].Frame}");
    }
}

void SaveState(string romPath, string statePath, int frames, int instructionsPerFrame)
{
    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();
    for (int i = 0; i < frames; i++)
    {
        machine.RunFrameCycles(instructionsPerFrame);
    }

    SaveStateSerializer.Save(machine, statePath);
    Console.WriteLine($"Saved state after {frames} frame(s) to {Path.GetFullPath(statePath)}");
    Console.WriteLine(FormatState(machine));
}

void SaveStateFromState(string romPath, string inputStatePath, string outputStatePath, int frames, int instructionsPerFrame)
{
    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();
    SaveStateSerializer.Load(machine, inputStatePath);
    for (int i = 0; i < frames; i++)
    {
        machine.RunFrameCycles(instructionsPerFrame);
    }

    SaveStateSerializer.Save(machine, outputStatePath);
    Console.WriteLine($"Loaded {Path.GetFullPath(inputStatePath)} and saved state after {frames} additional frame(s) to {Path.GetFullPath(outputStatePath)}");
    Console.WriteLine(FormatState(machine));
    Console.WriteLine(FormatVdpState(machine.Vdp));
}

void LoadStateAndRun(string romPath, string statePath, int frames, int instructionsPerFrame)
{
    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();
    SaveStateSerializer.Load(machine, statePath);
    for (int i = 0; i < frames; i++)
    {
        machine.RunFrameCycles(instructionsPerFrame);
    }

    Console.WriteLine($"Loaded {Path.GetFullPath(statePath)} and ran {frames} frame(s)");
    Console.WriteLine(FormatState(machine));
    Console.WriteLine(FormatVdpState(machine.Vdp));
}

void PrintCartridgeInfo(string romPath)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    CartridgeDiagnostics diagnostics = cartridge.Diagnostics;
    Console.WriteLine(Path.GetFullPath(romPath));
    Console.WriteLine($"Domestic: {cartridge.Header.DomesticName}");
    Console.WriteLine($"Overseas: {cartridge.Header.OverseasName}");
    Console.WriteLine($"Product: {cartridge.Header.ProductCode}");
    Console.WriteLine($"Region: {cartridge.Header.Region}");
    Console.WriteLine($"ROM size: {diagnostics.RomSize:N0} bytes");
    Console.WriteLine($"Header ROM range: ${diagnostics.HeaderRomStart:X6}-${diagnostics.HeaderRomEnd:X6}");
    Console.WriteLine($"Save hardware: {FormatSaveHardware(diagnostics)}");
    Console.WriteLine($"Bank-switch registers: {(diagnostics.UsesBankSwitchRegisters ? "expected" : "not expected")}");
    Console.WriteLine($"SVP coprocessor: {(diagnostics.HasSvp ? "yes" : "no")}");
    Console.WriteLine($"32X hardware: {(diagnostics.Requires32X ? "required" : "not required")}");
    if (diagnostics.Requires32X)
    {
        Console.WriteLine($"32X runtime pieces: {string.Join("; ", ThirtyTwoXHardwareProfile.RequiredSubsystems)}");
    }

    if (diagnostics.HasUnsupportedHardware)
    {
        Console.WriteLine($"Unsupported hardware: {string.Join(", ", diagnostics.UnsupportedHardware)}");
    }

    foreach (string warning in diagnostics.Warnings.Concat(FilenameCartridgeWarnings(romPath)).Distinct(StringComparer.Ordinal))
    {
        Console.WriteLine($"Warning: {warning}");
    }
}

void ScanCartridges(string romFolder, string outputCsv)
{
    string fullRomFolder = Path.GetFullPath(romFolder);
    string[] files = EnumerateRomFiles(fullRomFolder);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputCsv)) ?? ".");

    int unsupported = 0;
    int saveHardware = 0;
    int bankSwitching = 0;
    int warnings = 0;

    using StreamWriter writer = new(outputCsv, false, Encoding.UTF8);
    writer.WriteLine("rom,domestic,overseas,product,region,romSize,headerRomEnd,saveHardware,saveRange,saveLanes,eepromSize,bankSwitchRegisters,svp,requires32x,unsupportedHardware,warnings");
    foreach (string file in files)
    {
        string relative = Path.GetRelativePath(fullRomFolder, file);
        try
        {
            CartridgeImage cartridge = CartridgeImage.FromFile(file);
            CartridgeDiagnostics diagnostics = cartridge.Diagnostics;
            string[] filenameWarnings = FilenameCartridgeWarnings(file);
            unsupported += diagnostics.HasUnsupportedHardware ? 1 : 0;
            saveHardware += diagnostics.HasSaveHardware ? 1 : 0;
            bankSwitching += diagnostics.UsesBankSwitchRegisters ? 1 : 0;
            warnings += diagnostics.Warnings.Length > 0 || filenameWarnings.Length > 0 ? 1 : 0;

            writer.WriteLine(string.Join(
                ',',
                Csv(relative),
                Csv(cartridge.Header.DomesticName),
                Csv(cartridge.Header.OverseasName),
                Csv(cartridge.Header.ProductCode),
                Csv(cartridge.Header.Region),
                diagnostics.RomSize.ToString(CultureInfo.InvariantCulture),
                Csv($"${diagnostics.HeaderRomEnd:X6}"),
                Csv(diagnostics.SaveHardware),
                Csv(FormatSaveRange(diagnostics)),
                Csv(diagnostics.SaveRamLanes),
                diagnostics.EepromSize?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                diagnostics.UsesBankSwitchRegisters ? "true" : "false",
                diagnostics.HasSvp ? "true" : "false",
                diagnostics.Requires32X ? "true" : "false",
                Csv(string.Join("; ", diagnostics.UnsupportedHardware)),
                Csv(string.Join("; ", diagnostics.Warnings.Concat(filenameWarnings).Distinct(StringComparer.Ordinal)))));
        }
        catch (Exception ex)
        {
            writer.WriteLine(string.Join(
                ',',
                Csv(relative),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "0",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "false",
                "false",
                "false",
                string.Empty,
                Csv(ex.Message)));
            warnings++;
        }
    }

    Console.WriteLine($"Scanned {files.Length:N0} ROM(s) to {Path.GetFullPath(outputCsv)}");
    Console.WriteLine($"Save hardware: {saveHardware:N0}; bank switching expected: {bankSwitching:N0}; unsupported hardware: {unsupported:N0}; warnings: {warnings:N0}");
}

void TraceThirtyTwoXSh2(string romPath, int instructionLimit, string cpuName, uint? startPc)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    if (!cartridge.Diagnostics.Requires32X)
    {
        Console.Error.WriteLine("The supplied ROM is not detected as a 32X cartridge.");
        return;
    }

    MegaDrive machine = new(cartridge, IsPalRegion(cartridge));
    machine.Reset();
    ThirtyTwoXDevice device = machine.Bus.ThirtyTwoX ?? throw new InvalidOperationException("32X device was not attached.");
    device.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset, 0x0083);
    Sh2Cpu cpu = cpuName.Equals("slave", StringComparison.OrdinalIgnoreCase) ? device.SlaveSh2 : device.MasterSh2;
    if (startPc is uint pc)
    {
        cpu.Reset(pc);
    }

    Console.WriteLine($"32X SH-2 trace: {Path.GetFullPath(romPath)}");
    Console.WriteLine($"CPU: {(ReferenceEquals(cpu, device.SlaveSh2) ? "slave" : "master")}; limit: {instructionLimit:N0}; start PC=${cpu.PC:X8}");
    int executed = 0;
    try
    {
        while (!cpu.Halted && executed < instructionLimit)
        {
            cpu.Step();
            executed++;
        }

        Console.WriteLine($"Completed {executed:N0} instruction(s). halted={(cpu.Halted ? "yes" : "no")} PC=${cpu.PC:X8} last=${cpu.LastOpcode:X4}@${cpu.LastOpcodePc:X8} cycles={cpu.Cycles:N0}");
    }
    catch (Sh2Exception ex)
    {
        Console.WriteLine($"Stopped after {executed:N0} instruction(s): {ex.Message}");
        Console.WriteLine($"PC=${cpu.PC:X8} last=${cpu.LastOpcode:X4}@${cpu.LastOpcodePc:X8} R0=${cpu.R[0]:X8} R1=${cpu.R[1]:X8} R2=${cpu.R[2]:X8} R3=${cpu.R[3]:X8} R4=${cpu.R[4]:X8} R15=${cpu.R[15]:X8} SR=${cpu.SR:X8} PR=${cpu.PR:X8}");
    }
}

void TraceThirtyTwoXLiveSh2(string romPath, string outputCsv, int frames, int instructionsPerFrame, string cpuFilter, uint? pcStart, uint? pcEnd, int maxLines, int startFrame, string? statePath = null)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    if (!cartridge.Diagnostics.Requires32X)
    {
        Console.Error.WriteLine("The supplied ROM is not detected as a 32X cartridge.");
        return;
    }

    MegaDrive machine = new(cartridge, IsPalRegion(cartridge));
    machine.Reset();
    if (!string.IsNullOrWhiteSpace(statePath))
    {
        SaveStateSerializer.Load(machine, statePath);
    }

    ThirtyTwoXDevice device = machine.Bus.ThirtyTwoX ?? throw new InvalidOperationException("32X device was not attached.");
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputCsv)) ?? ".");

    uint start = pcStart ?? 0;
    uint end = pcEnd ?? uint.MaxValue;
    if (end < start)
    {
        (start, end) = (end, start);
    }

    bool traceMaster = cpuFilter.Equals("both", StringComparison.OrdinalIgnoreCase) || cpuFilter.Equals("master", StringComparison.OrdinalIgnoreCase);
    bool traceSlave = cpuFilter.Equals("both", StringComparison.OrdinalIgnoreCase) || cpuFilter.Equals("slave", StringComparison.OrdinalIgnoreCase);
    int currentFrame = 0;
    int sequence = 0;
    bool limitReached = false;

    using StreamWriter writer = new(outputCsv, false, Encoding.UTF8) { AutoFlush = true };
    writer.WriteLine("frame,sequence,type,cpu,pc,opcode,nextPc,delaySlot,beforeCycles,cycles,stepCycles,masterCycle,scanline,lineCycle,beforeSr,beforeR0,beforeR1,beforeR2,beforeR3,beforeR4,beforeR5,beforeR6,beforeR7,beforeR8,beforeR9,beforeR10,beforeR11,beforeR12,beforeR13,beforeR14,beforeR15,sr,r0,r1,r2,r3,r4,r5,r6,r7,r8,r9,r10,r11,r12,r13,r14,r15,beforePr,beforeGbr,beforeVbr,pr,gbr,vbr,interruptLevel,interruptVector,handlerPc,asm");

    void WriteInstruction(Sh2Cpu.Sh2InstructionTrace trace)
    {
        if (limitReached || trace.Pc < start || trace.Pc > end)
        {
            return;
        }

        if (!ShouldTraceCpu(trace.Cpu))
        {
            return;
        }

        writer.WriteLine(string.Join(
            ',',
            currentFrame.ToString(CultureInfo.InvariantCulture),
            (++sequence).ToString(CultureInfo.InvariantCulture),
            "instruction",
            Csv(trace.Cpu),
            $"${trace.Pc:X8}",
            $"${trace.Opcode:X4}",
            $"${trace.NextPc:X8}",
            trace.DelaySlot ? "true" : "false",
            trace.BeforeCycles.ToString(CultureInfo.InvariantCulture),
            trace.Cycles.ToString(CultureInfo.InvariantCulture),
            trace.StepCycles.ToString(CultureInfo.InvariantCulture),
            machine.Bus.CurrentMasterCycle.ToString(CultureInfo.InvariantCulture),
            machine.Vdp.CurrentScanline.ToString(CultureInfo.InvariantCulture),
            machine.Bus.CurrentScanlineMasterCycleOffset.ToString(CultureInfo.InvariantCulture),
            $"${trace.BeforeSr:X8}",
            $"${trace.BeforeR0:X8}",
            $"${trace.BeforeR1:X8}",
            $"${trace.BeforeR2:X8}",
            $"${trace.BeforeR3:X8}",
            $"${trace.BeforeR4:X8}",
            $"${trace.BeforeR5:X8}",
            $"${trace.BeforeR6:X8}",
            $"${trace.BeforeR7:X8}",
            $"${trace.BeforeR8:X8}",
            $"${trace.BeforeR9:X8}",
            $"${trace.BeforeR10:X8}",
            $"${trace.BeforeR11:X8}",
            $"${trace.BeforeR12:X8}",
            $"${trace.BeforeR13:X8}",
            $"${trace.BeforeR14:X8}",
            $"${trace.BeforeR15:X8}",
            $"${trace.Sr:X8}",
            $"${trace.R0:X8}",
            $"${trace.R1:X8}",
            $"${trace.R2:X8}",
            $"${trace.R3:X8}",
            $"${trace.R4:X8}",
            $"${trace.R5:X8}",
            $"${trace.R6:X8}",
            $"${trace.R7:X8}",
            $"${trace.R8:X8}",
            $"${trace.R9:X8}",
            $"${trace.R10:X8}",
            $"${trace.R11:X8}",
            $"${trace.R12:X8}",
            $"${trace.R13:X8}",
            $"${trace.R14:X8}",
            $"${trace.R15:X8}",
            $"${trace.BeforePr:X8}",
            $"${trace.BeforeGbr:X8}",
            $"${trace.BeforeVbr:X8}",
            $"${trace.Pr:X8}",
            $"${trace.Gbr:X8}",
            $"${trace.Vbr:X8}",
            string.Empty,
            string.Empty,
            string.Empty,
            Csv(DisassembleSh2(trace.Opcode, trace.Pc))));
        if (sequence >= maxLines)
        {
            limitReached = true;
            DisableObservers();
        }
    }

    void WriteInterrupt(Sh2Cpu.Sh2InterruptTrace trace)
    {
        if (limitReached || !ShouldTraceCpu(trace.Cpu) || trace.HandlerPc < start || trace.HandlerPc > end)
        {
            return;
        }

        string[] columns = new string[58];
        columns[0] = currentFrame.ToString(CultureInfo.InvariantCulture);
        columns[1] = (++sequence).ToString(CultureInfo.InvariantCulture);
        columns[2] = "interrupt";
        columns[3] = Csv(trace.Cpu);
        columns[31] = $"${trace.Sr:X8}";
        columns[47] = $"${trace.R15:X8}";
        columns[54] = trace.Level.ToString(CultureInfo.InvariantCulture);
        columns[55] = trace.VectorNumber.ToString(CultureInfo.InvariantCulture);
        columns[56] = $"${trace.HandlerPc:X8}";
        writer.WriteLine(string.Join(',', columns));
        if (sequence >= maxLines)
        {
            limitReached = true;
            DisableObservers();
        }
    }

    bool ShouldTraceCpu(string cpuName)
    {
        return traceMaster && cpuName.Contains("master", StringComparison.OrdinalIgnoreCase) ||
            traceSlave && cpuName.Contains("slave", StringComparison.OrdinalIgnoreCase);
    }

    void DisableObservers()
    {
        device.MasterSh2.InstructionObserver = null;
        device.SlaveSh2.InstructionObserver = null;
        device.MasterSh2.InterruptObserver = null;
        device.SlaveSh2.InterruptObserver = null;
    }

    device.MasterSh2.InstructionObserver = WriteInstruction;
    device.SlaveSh2.InstructionObserver = WriteInstruction;
    device.MasterSh2.InterruptObserver = WriteInterrupt;
    device.SlaveSh2.InterruptObserver = WriteInterrupt;

    int endFrame = startFrame + frames;
    string origin = string.IsNullOrWhiteSpace(statePath)
        ? $"startFrame={startFrame:N0}"
        : $"state={Path.GetFileName(statePath)}";
    Console.WriteLine($"32X live SH-2 trace: {Path.GetFileName(romPath)}, frames={frames:N0}, {origin}, budget={instructionsPerFrame:N0}, cpu={cpuFilter}, pc=${start:X8}-${end:X8}");
    for (currentFrame = 0; currentFrame < endFrame && !limitReached; currentFrame++)
    {
        try
        {
            if (currentFrame == 0 && startFrame > 0)
            {
                DisableObservers();
            }

            if (currentFrame == startFrame)
            {
                device.MasterSh2.InstructionObserver = WriteInstruction;
                device.SlaveSh2.InstructionObserver = WriteInstruction;
                device.MasterSh2.InterruptObserver = WriteInterrupt;
                device.SlaveSh2.InterruptObserver = WriteInterrupt;
            }

            machine.RunFrameCycles(instructionsPerFrame);
        }
        catch (Exception ex) when (ex is M68kException or Sh2Exception or InvalidOperationException)
        {
            writer.WriteLine(string.Join(
                ',',
                currentFrame.ToString(CultureInfo.InvariantCulture),
                (++sequence).ToString(CultureInfo.InvariantCulture),
                "error",
                Csv(ex.GetType().Name),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                Csv(ex.Message)));
            break;
        }
    }

    DisableObservers();
    Console.WriteLine($"Wrote {sequence:N0} SH-2 trace row(s) to {Path.GetFullPath(outputCsv)}");
    Console.WriteLine($"Master PC=${device.MasterSh2.PC:X8} SR=${device.MasterSh2.SR:X8}; slave PC=${device.SlaveSh2.PC:X8} SR=${device.SlaveSh2.SR:X8}");
}

void TraceThirtyTwoXInterrupts(string romPath, string outputCsv, int frames, int instructionsPerFrame, string cpuFilter, int startFrame, int maxLines)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    if (!cartridge.Diagnostics.Requires32X)
    {
        Console.Error.WriteLine("The supplied ROM is not detected as a 32X cartridge.");
        return;
    }

    MegaDrive machine = new(cartridge, IsPalRegion(cartridge));
    machine.Reset();
    ThirtyTwoXDevice device = machine.Bus.ThirtyTwoX ?? throw new InvalidOperationException("32X device was not attached.");
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputCsv)) ?? ".");

    bool traceMaster = cpuFilter.Equals("both", StringComparison.OrdinalIgnoreCase) || cpuFilter.Equals("master", StringComparison.OrdinalIgnoreCase);
    bool traceSlave = cpuFilter.Equals("both", StringComparison.OrdinalIgnoreCase) || cpuFilter.Equals("slave", StringComparison.OrdinalIgnoreCase);
    int currentFrame = 0;
    int sequence = 0;
    bool limitReached = false;

    using StreamWriter writer = new(outputCsv, false, Encoding.UTF8) { AutoFlush = true };
    writer.WriteLine("frame,sequence,cpu,level,vector,handlerPc,sr,r15,masterPc,masterSr,masterPendingLevel,masterPendingVector,slavePc,slaveSr,slavePendingLevel,slavePendingVector,m68kPc,masterMask,slaveMask,sys20,sys22,sys24,sys26,sys28,sys2a,sys2c,sys2e,vblank,hblank,scanline,lineCycle,masterCycles,slaveCycles,adapterEnabled,sh2ResetReleased");

    void WriteInterrupt(Sh2Cpu.Sh2InterruptTrace trace)
    {
        if (limitReached || !ShouldTraceCpu(trace.Cpu))
        {
            return;
        }

        writer.WriteLine(string.Join(
            ',',
            currentFrame.ToString(CultureInfo.InvariantCulture),
            (++sequence).ToString(CultureInfo.InvariantCulture),
            Csv(trace.Cpu),
            trace.Level.ToString(CultureInfo.InvariantCulture),
            trace.VectorNumber.ToString(CultureInfo.InvariantCulture),
            $"${trace.HandlerPc:X8}",
            $"${trace.Sr:X8}",
            $"${trace.R15:X8}",
            $"${device.MasterSh2.PC:X8}",
            $"${device.MasterSh2.SR:X8}",
            device.MasterSh2.PendingInterruptLevel.ToString(CultureInfo.InvariantCulture),
            device.MasterSh2.PendingInterruptVectorNumber.ToString(CultureInfo.InvariantCulture),
            $"${device.SlaveSh2.PC:X8}",
            $"${device.SlaveSh2.SR:X8}",
            device.SlaveSh2.PendingInterruptLevel.ToString(CultureInfo.InvariantCulture),
            device.SlaveSh2.PendingInterruptVectorNumber.ToString(CultureInfo.InvariantCulture),
            $"${machine.MainCpu.PC:X8}",
            $"${device.MasterInterruptMask:X4}",
            $"${device.SlaveInterruptMask:X4}",
            $"${device.DebugPeekSystemRegisterWord(0x20):X4}",
            $"${device.DebugPeekSystemRegisterWord(0x22):X4}",
            $"${device.DebugPeekSystemRegisterWord(0x24):X4}",
            $"${device.DebugPeekSystemRegisterWord(0x26):X4}",
            $"${device.DebugPeekSystemRegisterWord(0x28):X4}",
            $"${device.DebugPeekSystemRegisterWord(0x2A):X4}",
            $"${device.DebugPeekSystemRegisterWord(0x2C):X4}",
            $"${device.DebugPeekSystemRegisterWord(0x2E):X4}",
            device.VBlank ? "true" : "false",
            device.HBlank ? "true" : "false",
            machine.Vdp.CurrentScanline.ToString(CultureInfo.InvariantCulture),
            machine.Bus.CurrentScanlineMasterCycleOffset.ToString(CultureInfo.InvariantCulture),
            device.MasterSh2.Cycles.ToString(CultureInfo.InvariantCulture),
            device.SlaveSh2.Cycles.ToString(CultureInfo.InvariantCulture),
            device.AdapterEnabled ? "true" : "false",
            device.Sh2ResetReleased ? "true" : "false"));

        if (sequence >= maxLines)
        {
            limitReached = true;
            DisableObservers();
        }
    }

    bool ShouldTraceCpu(string cpuName)
    {
        return traceMaster && cpuName.Contains("master", StringComparison.OrdinalIgnoreCase) ||
            traceSlave && cpuName.Contains("slave", StringComparison.OrdinalIgnoreCase);
    }

    void DisableObservers()
    {
        device.MasterSh2.InterruptObserver = null;
        device.SlaveSh2.InterruptObserver = null;
    }

    device.MasterSh2.InterruptObserver = WriteInterrupt;
    device.SlaveSh2.InterruptObserver = WriteInterrupt;

    int endFrame = startFrame + frames;
    Console.WriteLine($"32X SH-2 interrupt trace: {Path.GetFileName(romPath)}, frames={frames:N0}, startFrame={startFrame:N0}, budget={instructionsPerFrame:N0}, cpu={cpuFilter}");
    for (currentFrame = 0; currentFrame < endFrame && !limitReached; currentFrame++)
    {
        try
        {
            if (currentFrame == 0 && startFrame > 0)
            {
                DisableObservers();
            }

            if (currentFrame == startFrame)
            {
                device.MasterSh2.InterruptObserver = WriteInterrupt;
                device.SlaveSh2.InterruptObserver = WriteInterrupt;
            }

            machine.RunFrameCycles(instructionsPerFrame);
        }
        catch (Exception ex) when (ex is M68kException or Sh2Exception or InvalidOperationException)
        {
            writer.WriteLine(string.Join(
                ',',
                currentFrame.ToString(CultureInfo.InvariantCulture),
                (++sequence).ToString(CultureInfo.InvariantCulture),
                Csv(ex.GetType().Name),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                $"${device.MasterSh2.PC:X8}",
                $"${device.MasterSh2.SR:X8}",
                device.MasterSh2.PendingInterruptLevel.ToString(CultureInfo.InvariantCulture),
                device.MasterSh2.PendingInterruptVectorNumber.ToString(CultureInfo.InvariantCulture),
                $"${device.SlaveSh2.PC:X8}",
                $"${device.SlaveSh2.SR:X8}",
                device.SlaveSh2.PendingInterruptLevel.ToString(CultureInfo.InvariantCulture),
                device.SlaveSh2.PendingInterruptVectorNumber.ToString(CultureInfo.InvariantCulture),
                $"${machine.MainCpu.PC:X8}",
                $"${device.MasterInterruptMask:X4}",
                $"${device.SlaveInterruptMask:X4}",
                Csv(ex.Message)));
            Console.WriteLine($"Stopped at frame {currentFrame:N0}: {ex.Message}");
            break;
        }
    }

    DisableObservers();
    Console.WriteLine($"Wrote {sequence:N0} SH-2 interrupt row(s) to {Path.GetFullPath(outputCsv)}");
    Console.WriteLine($"Master PC=${device.MasterSh2.PC:X8} SR=${device.MasterSh2.SR:X8}; slave PC=${device.SlaveSh2.PC:X8} SR=${device.SlaveSh2.SR:X8}");
}

void TraceThirtyTwoXFillLoops(string romPath, string outputCsv, int frames, int instructionsPerFrame, int startFrame)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    if (!cartridge.Diagnostics.Requires32X)
    {
        Console.Error.WriteLine("The supplied ROM is not detected as a 32X cartridge.");
        return;
    }

    MegaDrive machine = new(cartridge, IsPalRegion(cartridge));
    machine.Reset();
    ThirtyTwoXDevice device = machine.Bus.ThirtyTwoX ?? throw new InvalidOperationException("32X device was not attached.");
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputCsv)) ?? ".");

    Dictionary<string, Queue<Sh2Cpu.Sh2InstructionTrace>> history = new(StringComparer.OrdinalIgnoreCase)
    {
        ["master"] = new Queue<Sh2Cpu.Sh2InstructionTrace>(4),
        ["slave"] = new Queue<Sh2Cpu.Sh2InstructionTrace>(4),
    };
    Dictionary<FillLoopKey, FillLoopSummary> summaries = [];
    int currentFrame = 0;

    void Observe(Sh2Cpu.Sh2InstructionTrace trace)
    {
        string cpu = trace.Cpu.Contains("slave", StringComparison.OrdinalIgnoreCase) ? "slave" : "master";
        Queue<Sh2Cpu.Sh2InstructionTrace> queue = history[cpu];
        queue.Enqueue(trace);
        while (queue.Count > 4)
        {
            queue.Dequeue();
        }

        if (currentFrame < startFrame || queue.Count < 4)
        {
            return;
        }

        Sh2Cpu.Sh2InstructionTrace[] recent = queue.ToArray();
        Sh2Cpu.Sh2InstructionTrace store = recent[0];
        Sh2Cpu.Sh2InstructionTrace add = recent[1];
        Sh2Cpu.Sh2InstructionTrace dt = recent[2];
        Sh2Cpu.Sh2InstructionTrace branch = recent[3];
        if (!TryDecodeSh2WordStoreAddDtBfLoop(store.Opcode, add.Opcode, dt.Opcode, branch.Opcode, store.Pc, out int addressRegister, out int sourceRegister, out int countRegister, out int increment))
        {
            return;
        }

        uint address = GetBeforeRegister(store, addressRegister);
        uint value = GetBeforeRegister(store, sourceRegister) & 0xFFFF;
        uint countBefore = GetBeforeRegister(dt, countRegister);
        uint countAfter = GetAfterRegister(dt, countRegister);
        string target = ClassifySh2WriteAddress(address);
        FillLoopKey key = new(currentFrame, cpu, store.Pc, target);
        if (!summaries.TryGetValue(key, out FillLoopSummary? summary))
        {
            summary = new FillLoopSummary(key)
            {
                FirstMasterCycle = machine.Bus.CurrentMasterCycle,
                FirstScanline = machine.Vdp.CurrentScanline,
                FirstAddress = address,
                FirstCount = countBefore,
                Value = value,
                Increment = increment,
                AddressRegister = addressRegister,
                SourceRegister = sourceRegister,
                CountRegister = countRegister,
            };
            summaries.Add(key, summary);
        }

        summary.Hits++;
        summary.LastMasterCycle = machine.Bus.CurrentMasterCycle;
        summary.LastScanline = machine.Vdp.CurrentScanline;
        summary.LastAddress = address;
        summary.LastCount = countAfter;
        summary.FrameBufferControl = device.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset);
        summary.BitmapMode = device.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset);
        summary.DrawFrameBufferIndex = device.DrawFrameBufferIndex;
        summary.DisplayFrameBufferIndex = device.DisplayFrameBufferIndex;
        summary.SwapPending = device.FrameBufferSwapPending;
        summary.DeniedFrameBufferAccessCount = device.DeniedFrameBufferAccessCount;
        summary.FrameBufferByteWriteCount = device.FrameBufferByteWriteCount;
    }

    device.MasterSh2.InstructionObserver = Observe;
    device.SlaveSh2.InstructionObserver = Observe;
    Console.WriteLine($"32X fill-loop trace: {Path.GetFileName(romPath)}, frames={frames:N0}, startFrame={startFrame:N0}, budget={instructionsPerFrame:N0}");
    for (currentFrame = 0; currentFrame < frames; currentFrame++)
    {
        try
        {
            machine.RunFrameCycles(instructionsPerFrame);
        }
        catch (Exception ex) when (ex is M68kException or Sh2Exception or InvalidOperationException)
        {
            Console.WriteLine($"Stopped at frame {currentFrame:N0}: {ex.GetType().Name}: {ex.Message}");
            break;
        }
    }

    device.MasterSh2.InstructionObserver = null;
    device.SlaveSh2.InstructionObserver = null;

    using StreamWriter writer = new(outputCsv, false, Encoding.UTF8) { AutoFlush = true };
    writer.WriteLine("frame,cpu,loopPc,target,hits,firstCycle,lastCycle,firstScanline,lastScanline,firstAddress,lastAddress,value,countFirst,countLast,increment,addressRegister,sourceRegister,countRegister,fbctl,bitmapMode,drawFb,displayFb,swapPending,deniedFbAccess,fbBytes");
    foreach (FillLoopSummary summary in summaries.Values.OrderBy(item => item.Key.Frame).ThenBy(item => item.Key.Cpu).ThenBy(item => item.Key.LoopPc))
    {
        writer.WriteLine(summary.ToCsv());
    }

    Console.WriteLine($"Wrote {summaries.Count:N0} fill-loop summary row(s) to {Path.GetFullPath(outputCsv)}");
}

void TraceThirtyTwoXRunlengthList(string romPath, string outputCsv, int frames, int instructionsPerFrame, int startFrame, int maxLines)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    if (!cartridge.Diagnostics.Requires32X)
    {
        Console.Error.WriteLine("The supplied ROM is not detected as a 32X cartridge.");
        return;
    }

    MegaDrive machine = new(cartridge, IsPalRegion(cartridge));
    machine.Reset();
    ThirtyTwoXDevice device = machine.Bus.ThirtyTwoX ?? throw new InvalidOperationException("32X device was not attached.");
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputCsv)) ?? ".");

    Dictionary<string, HashSet<uint>> seenNodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["master"] = [],
        ["slave"] = [],
    };
    Dictionary<string, uint> lastNode = new(StringComparer.OrdinalIgnoreCase);
    int currentFrame = 0;
    int sequence = 0;
    bool limitReached = false;

    using StreamWriter writer = new(outputCsv, false, Encoding.UTF8) { AutoFlush = true };
    writer.WriteLine("frame,sequence,cpu,pc,half,masterCycle,scanline,lineCycle,threshold,node,next,value,newNode,r1,r2,r3,r4,current,oldPrevious,writeNewNext,writeNewPrev,writeOldPrevNext,writeCurrentPrev,match,completed,noOp,repeated,sameAsPrevious,validNode,validNext,validNewNode,sdramOffset,nextOffset,newNodeOffset,sr,asm");

    void Observe(Sh2Cpu.Sh2LinkedListTrace trace)
    {
        if (limitReached || currentFrame < startFrame)
        {
            return;
        }

        string cpu = trace.Cpu.Contains("slave", StringComparison.OrdinalIgnoreCase) ? "slave" : "master";
        uint threshold = trace.Threshold;
        uint node = trace.Node;
        uint next = trace.Next;
        uint value = trace.Value;
        uint newNode = trace.NewNode;
        bool match = trace.Match;
        bool repeated = !seenNodes[cpu].Add(node);
        bool sameAsPrevious = lastNode.TryGetValue(cpu, out uint previousNode) && previousNode == node;
        lastNode[cpu] = node;
        bool validNode = TryMapThirtyTwoXSdramMirror(node, out int sdramOffset);
        bool validNext = TryMapThirtyTwoXSdramMirror(next, out int nextOffset);
        bool validNewNode = TryMapThirtyTwoXSdramMirror(newNode, out int newNodeOffset);

        writer.WriteLine(string.Join(
            ',',
            currentFrame.ToString(CultureInfo.InvariantCulture),
            (++sequence).ToString(CultureInfo.InvariantCulture),
            Csv(cpu),
            $"${trace.Pc:X8}",
            trace.Half,
            machine.Bus.CurrentMasterCycle.ToString(CultureInfo.InvariantCulture),
            machine.Vdp.CurrentScanline.ToString(CultureInfo.InvariantCulture),
            machine.Bus.CurrentScanlineMasterCycleOffset.ToString(CultureInfo.InvariantCulture),
            $"${threshold:X8}",
            $"${node:X8}",
            $"${next:X8}",
            $"${value:X8}",
            $"${newNode:X8}",
            $"${trace.RegisterR1:X8}",
            $"${trace.RegisterR2:X8}",
            $"${trace.RegisterR3:X8}",
            $"${trace.RegisterR4:X8}",
            $"${trace.Current:X8}",
            $"${trace.OldPrevious:X8}",
            $"${trace.WriteNewNext:X8}",
            $"${trace.WriteNewPrev:X8}",
            $"${trace.WriteOldPrevNext:X8}",
            $"${trace.WriteCurrentPrev:X8}",
            match ? "true" : "false",
            trace.Completed ? "true" : "false",
            trace.NoOp ? "true" : "false",
            repeated ? "true" : "false",
            sameAsPrevious ? "true" : "false",
            validNode ? "true" : "false",
            validNext ? "true" : "false",
            validNewNode ? "true" : "false",
            validNode ? $"${sdramOffset:X5}" : string.Empty,
            validNext ? $"${nextOffset:X5}" : string.Empty,
            validNewNode ? $"${newNodeOffset:X5}" : string.Empty,
            string.Empty,
            Csv(trace.Half == "first" ? "CMP/GE R0,R3; BT" : "CMP/GE R0,R3; BF")));

        if (sequence >= maxLines)
        {
            limitReached = true;
            DisableObservers();
        }
    }

    void DisableObservers()
    {
        device.Sh2LinkedListObserver = null;
    }

    device.Sh2LinkedListObserver = Observe;

    Console.WriteLine($"32X Runlength list trace: {Path.GetFileName(romPath)}, frames={frames:N0}, startFrame={startFrame:N0}, budget={instructionsPerFrame:N0}, maxLines={maxLines:N0}");
    for (currentFrame = 0; currentFrame < frames && !limitReached; currentFrame++)
    {
        try
        {
            if (currentFrame == 0 && startFrame > 0)
            {
                DisableObservers();
            }

            if (currentFrame == startFrame)
            {
                device.Sh2LinkedListObserver = Observe;
            }

            machine.RunFrameCycles(instructionsPerFrame);
        }
        catch (Exception ex) when (ex is M68kException or Sh2Exception or InvalidOperationException)
        {
            writer.WriteLine(string.Join(
                ',',
                currentFrame.ToString(CultureInfo.InvariantCulture),
                (++sequence).ToString(CultureInfo.InvariantCulture),
                Csv(ex.GetType().Name),
                string.Empty,
                "error",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                Csv(ex.Message)));
            break;
        }
    }

    DisableObservers();
    Console.WriteLine($"Wrote {sequence:N0} Runlength list row(s) to {Path.GetFullPath(outputCsv)}");
    Console.WriteLine($"Master PC=${device.MasterSh2.PC:X8} SR=${device.MasterSh2.SR:X8}; slave PC=${device.SlaveSh2.PC:X8} SR=${device.SlaveSh2.SR:X8}");
}

void TraceThirtyTwoXRunlengthRechain(string romPath, string outputCsv, int frames, int instructionsPerFrame, int startFrame, int maxLines)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    if (!cartridge.Diagnostics.Requires32X)
    {
        Console.Error.WriteLine("The supplied ROM is not detected as a 32X cartridge.");
        return;
    }

    MegaDrive machine = new(cartridge, IsPalRegion(cartridge));
    machine.Reset();
    ThirtyTwoXDevice device = machine.Bus.ThirtyTwoX ?? throw new InvalidOperationException("32X device was not attached.");
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputCsv)) ?? ".");

    int currentFrame = 0;
    int sequence = 0;
    bool limitReached = false;

    using StreamWriter writer = new(outputCsv, false, Encoding.UTF8) { AutoFlush = true };
    writer.WriteLine("frame,sequence,cpu,pc,phase,masterCycle,scanline,lineCycle,current,previous,next,currentValue,tail,insertPrevious,insertNext,insertValue,writePreviousNext,writeNextPrevious,writeCurrentPrevious,writeCurrentNext,match,currentOffset,previousOffset,nextOffset,tailOffset,insertPreviousOffset,insertNextOffset");

    void Observe(Sh2Cpu.Sh2RechainTrace trace)
    {
        if (limitReached || currentFrame < startFrame)
        {
            return;
        }

        string cpu = trace.Cpu.Contains("slave", StringComparison.OrdinalIgnoreCase) ? "slave" : "master";
        bool validCurrent = TryMapThirtyTwoXSdramMirror(trace.Current, out int currentOffset);
        bool validPrevious = TryMapThirtyTwoXSdramMirror(trace.Previous, out int previousOffset);
        bool validNext = TryMapThirtyTwoXSdramMirror(trace.Next, out int nextOffset);
        bool validTail = TryMapThirtyTwoXSdramMirror(trace.Tail, out int tailOffset);
        bool validInsertPrevious = TryMapThirtyTwoXSdramMirror(trace.InsertPrevious, out int insertPreviousOffset);
        bool validInsertNext = TryMapThirtyTwoXSdramMirror(trace.InsertNext, out int insertNextOffset);

        writer.WriteLine(string.Join(
            ',',
            currentFrame.ToString(CultureInfo.InvariantCulture),
            (++sequence).ToString(CultureInfo.InvariantCulture),
            Csv(cpu),
            $"${trace.Pc:X8}",
            Csv(trace.Phase),
            machine.Bus.CurrentMasterCycle.ToString(CultureInfo.InvariantCulture),
            machine.Vdp.CurrentScanline.ToString(CultureInfo.InvariantCulture),
            machine.Bus.CurrentScanlineMasterCycleOffset.ToString(CultureInfo.InvariantCulture),
            $"${trace.Current:X8}",
            $"${trace.Previous:X8}",
            $"${trace.Next:X8}",
            $"${trace.CurrentValue:X8}",
            $"${trace.Tail:X8}",
            $"${trace.InsertPrevious:X8}",
            $"${trace.InsertNext:X8}",
            $"${trace.InsertValue:X8}",
            $"${trace.WritePreviousNext:X8}",
            $"${trace.WriteNextPrevious:X8}",
            $"${trace.WriteCurrentPrevious:X8}",
            $"${trace.WriteCurrentNext:X8}",
            trace.Match ? "true" : "false",
            validCurrent ? $"${currentOffset:X5}" : string.Empty,
            validPrevious ? $"${previousOffset:X5}" : string.Empty,
            validNext ? $"${nextOffset:X5}" : string.Empty,
            validTail ? $"${tailOffset:X5}" : string.Empty,
            validInsertPrevious ? $"${insertPreviousOffset:X5}" : string.Empty,
            validInsertNext ? $"${insertNextOffset:X5}" : string.Empty));

        if (sequence >= maxLines)
        {
            limitReached = true;
            DisableObservers();
        }
    }

    void DisableObservers()
    {
        device.Sh2RechainObserver = null;
    }

    device.Sh2RechainObserver = Observe;

    Console.WriteLine($"32X Runlength rechain trace: {Path.GetFileName(romPath)}, frames={frames:N0}, startFrame={startFrame:N0}, budget={instructionsPerFrame:N0}, maxLines={maxLines:N0}");
    for (currentFrame = 0; currentFrame < frames && !limitReached; currentFrame++)
    {
        try
        {
            if (currentFrame == 0 && startFrame > 0)
            {
                DisableObservers();
            }

            if (currentFrame == startFrame)
            {
                device.Sh2RechainObserver = Observe;
            }

            machine.RunFrameCycles(instructionsPerFrame);
        }
        catch (Exception ex) when (ex is M68kException or Sh2Exception or InvalidOperationException)
        {
            writer.WriteLine(string.Join(
                ',',
                currentFrame.ToString(CultureInfo.InvariantCulture),
                (++sequence).ToString(CultureInfo.InvariantCulture),
                Csv(ex.GetType().Name),
                string.Empty,
                "error",
                string.Empty,
                string.Empty,
                string.Empty,
                Csv(ex.Message)));
            break;
        }
    }

    DisableObservers();
    Console.WriteLine($"Wrote {sequence:N0} Runlength rechain row(s) to {Path.GetFullPath(outputCsv)}");
    Console.WriteLine($"Master PC=${device.MasterSh2.PC:X8} SR=${device.MasterSh2.SR:X8}; slave PC=${device.SlaveSh2.PC:X8} SR=${device.SlaveSh2.SR:X8}");
}

static bool TryMapThirtyTwoXSdramMirror(uint address, out int offset)
{
    if (address is > 0 and < ThirtyTwoXHardwareProfile.SdramBytes)
    {
        offset = (int)address;
        return true;
    }

    if ((address & 0xFE00_0000u) is 0x0600_0000u or 0x2600_0000u or 0x0C00_0000u)
    {
        offset = (int)(address & (ThirtyTwoXHardwareProfile.SdramBytes - 1));
        return true;
    }

    offset = 0;
    return false;
}

static bool TryDecodeSh2WordStoreAddDtBfLoop(ushort storeOpcode, ushort addOpcode, ushort dtOpcode, ushort branchOpcode, uint storePc, out int addressRegister, out int sourceRegister, out int countRegister, out int increment)
{
    addressRegister = 0;
    sourceRegister = 0;
    countRegister = 0;
    increment = 0;
    if ((storeOpcode & 0xF00F) != 0x2001 ||
        (addOpcode & 0xF000) != 0x7000 ||
        (dtOpcode & 0xF0FF) != 0x4010 ||
        (branchOpcode & 0xFF00) != 0x8B00)
    {
        return false;
    }

    addressRegister = (storeOpcode >> 8) & 0x0F;
    sourceRegister = (storeOpcode >> 4) & 0x0F;
    countRegister = (dtOpcode >> 8) & 0x0F;
    if (((addOpcode >> 8) & 0x0F) != addressRegister || countRegister == addressRegister)
    {
        return false;
    }

    int displacement = (sbyte)branchOpcode;
    uint target = unchecked((uint)((int)(storePc + 10) + (displacement * 2)));
    if (target != storePc)
    {
        return false;
    }

    increment = (sbyte)addOpcode;
    return true;
}

static string ClassifySh2WriteAddress(uint address)
{
    uint window = address & 0xFE00_0000u;
    if (window is 0x0400_0000u or 0x2400_0000u)
    {
        return (address & 0x0002_0000u) != 0 ? "overwrite" : "framebuffer";
    }

    if (address is >= ThirtyTwoXHardwareProfile.Sh2SdramStart and < ThirtyTwoXHardwareProfile.Sh2SdramStart + ThirtyTwoXHardwareProfile.SdramBytes ||
        address is >= ThirtyTwoXHardwareProfile.Sh2SdramCacheThroughStart and < ThirtyTwoXHardwareProfile.Sh2SdramCacheThroughStart + ThirtyTwoXHardwareProfile.SdramBytes)
    {
        return "sdram";
    }

    return "other";
}

static uint GetBeforeRegister(Sh2Cpu.Sh2InstructionTrace trace, int register)
{
    return register switch
    {
        0 => trace.BeforeR0,
        1 => trace.BeforeR1,
        2 => trace.BeforeR2,
        3 => trace.BeforeR3,
        4 => trace.BeforeR4,
        5 => trace.BeforeR5,
        6 => trace.BeforeR6,
        7 => trace.BeforeR7,
        8 => trace.BeforeR8,
        9 => trace.BeforeR9,
        10 => trace.BeforeR10,
        11 => trace.BeforeR11,
        12 => trace.BeforeR12,
        13 => trace.BeforeR13,
        14 => trace.BeforeR14,
        15 => trace.BeforeR15,
        _ => 0,
    };
}

static uint GetAfterRegister(Sh2Cpu.Sh2InstructionTrace trace, int register)
{
    return register switch
    {
        0 => trace.R0,
        1 => trace.R1,
        2 => trace.R2,
        3 => trace.R3,
        4 => trace.R4,
        5 => trace.R5,
        6 => trace.R6,
        7 => trace.R7,
        8 => trace.R8,
        9 => trace.R9,
        10 => trace.R10,
        11 => trace.R11,
        12 => trace.R12,
        13 => trace.R13,
        14 => trace.R14,
        15 => trace.R15,
        _ => 0,
    };
}

void TraceThirtyTwoXBus(string romPath, string outputCsv, int frames, int instructionsPerFrame, uint? addressStart, uint? addressEnd, int maxLines, bool writesOnly, bool exactAddressMatch, int startFrame, bool changesOnly = false, bool nonzeroOnly = false, string? statePath = null)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    if (!cartridge.Diagnostics.Requires32X)
    {
        Console.Error.WriteLine("The supplied ROM is not detected as a 32X cartridge.");
        return;
    }

    MegaDrive machine = new(cartridge, IsPalRegion(cartridge));
    machine.Reset();
    if (!string.IsNullOrWhiteSpace(statePath))
    {
        SaveStateSerializer.Load(machine, statePath);
    }

    ThirtyTwoXDevice device = machine.Bus.ThirtyTwoX ?? throw new InvalidOperationException("32X device was not attached.");
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputCsv)) ?? ".");
    uint start = addressStart ?? 0;
    uint end = addressEnd ?? uint.MaxValue;
    if (end < start)
    {
        (start, end) = (end, start);
    }

    bool AddressMatches(uint address)
    {
        if (exactAddressMatch)
        {
            return address >= start && address <= end;
        }

        uint normalized = address & 0x0FFF_FFFFu;
        uint normalizedStart = start & 0x0FFF_FFFFu;
        uint normalizedEnd = end & 0x0FFF_FFFFu;
        return address >= start && address <= end || normalized >= normalizedStart && normalized <= normalizedEnd;
    }

    int currentFrame = 0;
    int lines = 0;
    Dictionary<string, ushort> lastValues = new(StringComparer.Ordinal);
    using StreamWriter writer = new(outputCsv, false, Encoding.UTF8) { AutoFlush = true };
    writer.WriteLine("frame,sequence,source,operation,address,value,masterPc,slavePc,m68kPc,masterCycle,scanline,lineCycle");
    void WriteRow(string source, string operation, uint address, ushort value)
    {
        if (lines >= maxLines)
        {
            return;
        }

        if (writesOnly && !IsTraceWriteOperation(operation))
        {
            return;
        }

        if (nonzeroOnly && value == 0)
        {
            return;
        }

        if (changesOnly)
        {
            string key = string.Concat(source, "|", operation, "|", address.ToString("X8", CultureInfo.InvariantCulture));
            if (lastValues.TryGetValue(key, out ushort previous) && previous == value)
            {
                return;
            }

            lastValues[key] = value;
        }

        writer.WriteLine(string.Join(
            ',',
            currentFrame.ToString(CultureInfo.InvariantCulture),
            (++lines).ToString(CultureInfo.InvariantCulture),
            source,
            operation,
            $"${address:X8}",
            $"${value:X4}",
            $"${device.MasterSh2.PC:X8}",
            $"${device.SlaveSh2.PC:X8}",
            $"${machine.MainCpu.PC:X8}",
            machine.Bus.CurrentMasterCycle.ToString(CultureInfo.InvariantCulture),
            machine.Vdp.CurrentScanline.ToString(CultureInfo.InvariantCulture),
            machine.Bus.CurrentScanlineMasterCycleOffset.ToString(CultureInfo.InvariantCulture)));
    }

    void ArmBusObservers()
    {
        device.SystemRegisterAccessObserver = access =>
        {
            uint address = access.Source == "M68K"
                ? ThirtyTwoXHardwareProfile.M68kSystemRegisterStart + access.Offset
                : ThirtyTwoXHardwareProfile.Sh2SystemRegisterStart + access.Offset;
            if (AddressMatches(address))
            {
                WriteRow(access.Source, access.Operation, address, access.Value);
            }
        };
        device.VdpRegisterAccessObserver = access =>
        {
            uint address = ThirtyTwoXHardwareProfile.Sh2VdpRegisterStart + access.Offset;
            if (AddressMatches(address))
            {
                WriteRow(access.Source, access.Operation, address, access.Value);
            }
        };
        device.PaletteAccessObserver = access =>
        {
            uint address = access.Source == "M68K"
                ? ThirtyTwoXHardwareProfile.M68kColorPaletteStart + access.Offset
                : ThirtyTwoXHardwareProfile.Sh2ColorPaletteStart + access.Offset;
            if (AddressMatches(address))
            {
                WriteRow(access.Source, access.Operation, address, access.Value);
            }
        };
        device.Sh2MemoryAccessTraceFilter = AddressMatches;
        device.Sh2MemoryAccessObserver = access => WriteRow(access.Source, access.Operation, access.Address, access.Value);
        device.FrameBufferAccessObserver = access =>
        {
            uint baseAddress = access.Operation.Contains("OW", StringComparison.Ordinal)
                ? ThirtyTwoXHardwareProfile.Sh2OverwriteImageStart
                : ThirtyTwoXHardwareProfile.Sh2FrameBufferStart;
            uint address = baseAddress + access.Offset;
            if (AddressMatches(address))
            {
                WriteRow(access.Source, access.Operation, address, access.Value);
            }
        };
    }

    void DisableBusObservers()
    {
        device.SystemRegisterAccessObserver = null;
        device.SystemRegisterWriteObserver = null;
        device.VdpRegisterAccessObserver = null;
        device.PaletteAccessObserver = null;
        device.Sh2MemoryAccessObserver = null;
        device.Sh2MemoryAccessTraceFilter = null;
        device.FrameBufferAccessObserver = null;
    }

    string modeName = changesOnly ? "changes-exact" : nonzeroOnly ? "nonzero-exact" : exactAddressMatch ? (writesOnly ? "writes-exact" : "exact") : (writesOnly ? "writes" : "all");
    int endFrame = startFrame + frames;
    string origin = string.IsNullOrWhiteSpace(statePath)
        ? $"startFrame={startFrame:N0}"
        : $"state={Path.GetFileName(statePath)}";
    Console.WriteLine($"32X bus trace: {Path.GetFileName(romPath)}, frames={frames:N0}, {origin}, budget={instructionsPerFrame:N0}, address=${start:X8}-${end:X8}, mode={modeName}");
    for (currentFrame = 0; currentFrame < endFrame && lines < maxLines; currentFrame++)
    {
        if (currentFrame == startFrame)
        {
            ArmBusObservers();
        }

        machine.RunFrameCycles(instructionsPerFrame);
    }

    DisableBusObservers();
    Console.WriteLine($"Wrote {lines:N0} 32X bus trace row(s) to {Path.GetFullPath(outputCsv)}");
    Console.WriteLine($"Master PC=${device.MasterSh2.PC:X8}; slave PC=${device.SlaveSh2.PC:X8}; M68K PC=${machine.MainCpu.PC:X8}");
}

void TraceThirtyTwoXCommunication(string romPath, string outputCsv, int frames, int instructionsPerFrame, int startFrame, int maxLines, ushort offsetStart, ushort offsetEnd, bool writesOnly, string? statePath = null)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    if (!cartridge.Diagnostics.Requires32X)
    {
        Console.Error.WriteLine("The supplied ROM is not detected as a 32X cartridge.");
        return;
    }

    if (offsetEnd < offsetStart)
    {
        (offsetStart, offsetEnd) = (offsetEnd, offsetStart);
    }

    MegaDrive machine = new(cartridge, IsPalRegion(cartridge));
    machine.Reset();
    if (!string.IsNullOrWhiteSpace(statePath))
    {
        SaveStateSerializer.Load(machine, statePath);
    }

    ThirtyTwoXDevice device = machine.Bus.ThirtyTwoX ?? throw new InvalidOperationException("32X device was not attached.");
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputCsv)) ?? ".");

    int currentFrame = 0;
    int lines = 0;
    using StreamWriter writer = new(outputCsv, false, Encoding.UTF8) { AutoFlush = true };
    writer.WriteLine("frame,sequence,source,operation,offset,value,m68kAddress,sh2Address,masterPc,slavePc,m68kPc,masterCycles,slaveCycles,masterCycle,scanline,lineCycle");

    void WriteRow(ThirtyTwoXDevice.SystemRegisterAccessTrace access)
    {
        if (lines >= maxLines ||
            writesOnly && !IsTraceWriteOperation(access.Operation) ||
            access.Offset < offsetStart ||
            access.Offset > offsetEnd)
        {
            return;
        }

        writer.WriteLine(string.Join(
            ',',
            currentFrame.ToString(CultureInfo.InvariantCulture),
            (++lines).ToString(CultureInfo.InvariantCulture),
            access.Source,
            access.Operation,
            $"${access.Offset:X2}",
            $"${access.Value:X4}",
            $"${ThirtyTwoXHardwareProfile.M68kSystemRegister(access.Offset):X8}",
            $"${ThirtyTwoXHardwareProfile.Sh2SystemRegister(access.Offset):X8}",
            $"${device.MasterSh2.PC:X8}",
            $"${device.SlaveSh2.PC:X8}",
            $"${machine.MainCpu.PC:X8}",
            device.MasterSh2.Cycles.ToString(CultureInfo.InvariantCulture),
            device.SlaveSh2.Cycles.ToString(CultureInfo.InvariantCulture),
            machine.Bus.CurrentMasterCycle.ToString(CultureInfo.InvariantCulture),
            machine.Vdp.CurrentScanline.ToString(CultureInfo.InvariantCulture),
            machine.Bus.CurrentScanlineMasterCycleOffset.ToString(CultureInfo.InvariantCulture)));
    }

    int endFrame = startFrame + frames;
    string modeName = writesOnly ? "writes" : "all";
    string origin = string.IsNullOrWhiteSpace(statePath)
        ? $"startFrame={startFrame:N0}"
        : $"state={Path.GetFileName(statePath)}";
    Console.WriteLine($"32X communication trace: {Path.GetFileName(romPath)}, frames={frames:N0}, {origin}, budget={instructionsPerFrame:N0}, offsets=${offsetStart:X2}-${offsetEnd:X2}, mode={modeName}");
    for (currentFrame = 0; currentFrame < endFrame && lines < maxLines; currentFrame++)
    {
        if (currentFrame == startFrame)
        {
            device.SystemRegisterAccessObserver = WriteRow;
        }

        machine.RunFrameCycles(instructionsPerFrame);
    }

    device.SystemRegisterAccessObserver = null;
    Console.WriteLine($"Wrote {lines:N0} 32X communication trace row(s) to {Path.GetFullPath(outputCsv)}");
    Console.WriteLine($"Master PC=${device.MasterSh2.PC:X8}; slave PC=${device.SlaveSh2.PC:X8}; M68K PC=${machine.MainCpu.PC:X8}");
}

static bool IsTraceWriteOperation(string operation)
{
    return operation.StartsWith('W') ||
        operation.StartsWith("OW", StringComparison.Ordinal) ||
        operation.StartsWith("AF", StringComparison.Ordinal) ||
        operation.StartsWith("DENY-W", StringComparison.Ordinal) ||
        operation.StartsWith("DENY-OW", StringComparison.Ordinal);
}

void TraceThirtyTwoXDiagnostic(string romPath, string outputCsv, int frames, int instructionsPerFrame, int startFrame, int maxEvents)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    if (!cartridge.Diagnostics.Requires32X)
    {
        Console.Error.WriteLine("The supplied ROM is not detected as a 32X cartridge.");
        return;
    }

    MegaDrive machine = new(cartridge, IsPalRegion(cartridge));
    machine.Reset();
    ThirtyTwoXDevice device = machine.Bus.ThirtyTwoX ?? throw new InvalidOperationException("32X device was not attached.");
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputCsv)) ?? ".");

    using StreamWriter writer = new(outputCsv, false, Encoding.UTF8) { AutoFlush = true };
    writer.WriteLine("frame,sequence,event,source,operation,address,offset,value,detail,m68kPc,m68kSr,m68kExceptions,masterPc,masterSr,masterLastPc,masterLastOpcode,masterPendingIrq,masterUnhandled,slavePc,slaveSr,slaveLastPc,slaveLastOpcode,slavePendingIrq,slaveUnhandled,adapter,intCtrl,bankSet,dreq,dreqSourceHi,dreqSourceLo,dreqDestHi,dreqDestLo,dreqLen,dreqFifo,comm20,comm22,comm24,comm26,comm28,comm2A,comm2C,comm2E,bitmap,fbctl,draw,display,swapPending,vblank,hblank,modeWrites,fbctlWrites,vdpWrites,fbBytes,paletteBytes,dreqWrites,dreqDmaWords,displayPayload,drawPayload,paletteNonzero,bank");

    int currentFrame = 0;
    int sequence = 0;
    int emittedEvents = 0;
    int frameBufferEventsThisFrame = 0;

    void WriteSnapshot(string eventKind, string source, string operation, uint address, ushort offset, ushort value, string detail)
    {
        if (currentFrame < startFrame || emittedEvents >= maxEvents)
        {
            return;
        }

        Action<ThirtyTwoXDevice.SystemRegisterAccessTrace>? systemObserver = device.SystemRegisterAccessObserver;
        Action<ThirtyTwoXDevice.SystemRegisterAccessTrace>? vdpObserver = device.VdpRegisterAccessObserver;
        device.SystemRegisterAccessObserver = null;
        device.VdpRegisterAccessObserver = null;
        try
        {
            ushort adapter = device.DebugPeekSystemRegisterWord(ThirtyTwoXHardwareProfile.AdapterControlOffset);
            ushort intCtrl = device.DebugPeekSystemRegisterWord(ThirtyTwoXHardwareProfile.InterruptControlOffset);
            ushort bankSet = device.DebugPeekSystemRegisterWord(ThirtyTwoXHardwareProfile.BankSetOffset);
            ushort dreq = device.DebugPeekSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset);
            ushort dreqSourceHi = device.DebugPeekSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqSourceAddressOffset);
            ushort dreqSourceLo = device.DebugPeekSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqSourceAddressOffset + 2);
            ushort dreqDestHi = device.DebugPeekSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqDestinationAddressOffset);
            ushort dreqDestLo = device.DebugPeekSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqDestinationAddressOffset + 2);
            ushort dreqLen = device.DebugPeekSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqLengthOffset);
            ushort comm20 = device.DebugPeekSystemRegisterWord(0x20);
            ushort comm22 = device.DebugPeekSystemRegisterWord(0x22);
            ushort comm24 = device.DebugPeekSystemRegisterWord(0x24);
            ushort comm26 = device.DebugPeekSystemRegisterWord(0x26);
            ushort comm28 = device.DebugPeekSystemRegisterWord(0x28);
            ushort comm2A = device.DebugPeekSystemRegisterWord(0x2A);
            ushort comm2C = device.DebugPeekSystemRegisterWord(0x2C);
            ushort comm2E = device.DebugPeekSystemRegisterWord(0x2E);
            ushort bitmap = device.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset);
            ushort fbctl = device.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset);

            writer.WriteLine(string.Join(
                ',',
                currentFrame.ToString(CultureInfo.InvariantCulture),
                (++sequence).ToString(CultureInfo.InvariantCulture),
                eventKind,
                source,
                operation,
                address == 0 ? string.Empty : $"${address:X8}",
                $"${offset:X4}",
                $"${value:X4}",
                $"\"{EscapeCsv(detail)}\"",
                $"${machine.MainCpu.PC:X8}",
                $"${machine.MainCpu.SR:X4}",
                $"\"{EscapeCsv(FormatExceptions(machine.MainCpu))}\"",
                $"${device.MasterSh2.PC:X8}",
                $"${device.MasterSh2.SR:X8}",
                $"${device.MasterSh2.LastOpcodePc:X8}",
                $"${device.MasterSh2.LastOpcode:X4}",
                device.MasterSh2.PendingInterruptLevel.ToString(CultureInfo.InvariantCulture),
                device.MasterSh2.UnhandledOpcodeCount.ToString(CultureInfo.InvariantCulture),
                $"${device.SlaveSh2.PC:X8}",
                $"${device.SlaveSh2.SR:X8}",
                $"${device.SlaveSh2.LastOpcodePc:X8}",
                $"${device.SlaveSh2.LastOpcode:X4}",
                device.SlaveSh2.PendingInterruptLevel.ToString(CultureInfo.InvariantCulture),
                device.SlaveSh2.UnhandledOpcodeCount.ToString(CultureInfo.InvariantCulture),
                $"${adapter:X4}",
                $"${intCtrl:X4}",
                $"${bankSet:X4}",
                $"${dreq:X4}",
                $"${dreqSourceHi:X4}",
                $"${dreqSourceLo:X4}",
                $"${dreqDestHi:X4}",
                $"${dreqDestLo:X4}",
                $"${dreqLen:X4}",
                device.DreqFifoCount.ToString(CultureInfo.InvariantCulture),
                $"${comm20:X4}",
                $"${comm22:X4}",
                $"${comm24:X4}",
                $"${comm26:X4}",
                $"${comm28:X4}",
                $"${comm2A:X4}",
                $"${comm2C:X4}",
                $"${comm2E:X4}",
                $"${bitmap:X4}",
                $"${fbctl:X4}",
                device.DrawFrameBufferIndex.ToString(CultureInfo.InvariantCulture),
                device.DisplayFrameBufferIndex.ToString(CultureInfo.InvariantCulture),
                device.FrameBufferSwapPending ? "true" : "false",
                device.VBlank ? "true" : "false",
                device.HBlank ? "true" : "false",
                device.BitmapModeWriteCount.ToString(CultureInfo.InvariantCulture),
                device.FrameBufferControlWriteCount.ToString(CultureInfo.InvariantCulture),
                device.VdpRegisterWriteCount.ToString(CultureInfo.InvariantCulture),
                device.FrameBufferByteWriteCount.ToString(CultureInfo.InvariantCulture),
                device.PaletteByteWriteCount.ToString(CultureInfo.InvariantCulture),
                device.DreqFifoWriteCount.ToString(CultureInfo.InvariantCulture),
                device.DreqDmaWordTransferCount.ToString(CultureInfo.InvariantCulture),
                CountFramebufferPayloadNonzero(device.DisplayFrameBuffer).ToString(CultureInfo.InvariantCulture),
                CountFramebufferPayloadNonzero(device.DrawFrameBuffer).ToString(CultureInfo.InvariantCulture),
                CountNonzeroBytes(device.Palette).ToString(CultureInfo.InvariantCulture),
                device.M68kCartridgeBank.ToString(CultureInfo.InvariantCulture)));
            emittedEvents++;
        }
        finally
        {
            device.SystemRegisterAccessObserver = systemObserver;
            device.VdpRegisterAccessObserver = vdpObserver;
        }
    }

    device.SystemRegisterAccessObserver = access =>
    {
        uint address = access.Source == "M68K"
            ? ThirtyTwoXHardwareProfile.M68kSystemRegister(access.Offset)
            : ThirtyTwoXHardwareProfile.Sh2SystemRegister(access.Offset);
        WriteSnapshot("sys", access.Source, access.Operation, address, access.Offset, access.Value, string.Empty);
    };
    device.VdpRegisterAccessObserver = access =>
    {
        if (IsTraceWriteOperation(access.Operation))
        {
            WriteSnapshot("vdp", access.Source, access.Operation, ThirtyTwoXHardwareProfile.Sh2VdpRegister(access.Offset), access.Offset, access.Value, string.Empty);
        }
    };
    device.PaletteAccessObserver = access =>
    {
        if (!IsTraceWriteOperation(access.Operation))
        {
            return;
        }

        uint address = access.Source == "M68K"
            ? ThirtyTwoXHardwareProfile.M68kColorPaletteStart + access.Offset
            : ThirtyTwoXHardwareProfile.Sh2ColorPaletteStart + access.Offset;
        WriteSnapshot("palette", access.Source, access.Operation, address, access.Offset, access.Value, string.Empty);
    };
    device.FrameBufferAccessObserver = access =>
    {
        if (frameBufferEventsThisFrame >= 16)
        {
            return;
        }

        bool isLineTable = access.Offset < 0x200;
        if (!isLineTable && access.Value == 0)
        {
            return;
        }

        frameBufferEventsThisFrame++;
        uint baseAddress = access.Operation.StartsWith("OW", StringComparison.Ordinal)
            ? ThirtyTwoXHardwareProfile.Sh2OverwriteImageStart
            : ThirtyTwoXHardwareProfile.Sh2FrameBufferStart;
        WriteSnapshot("fb", access.Source, access.Operation, baseAddress + access.Offset, (ushort)(access.Offset & 0xFFFF), access.Value, $"fb{access.BufferIndex} pc=${access.Pc:X8} op=${access.Opcode:X4}");
    };

    Console.WriteLine($"32X diagnostic trace: {Path.GetFileName(romPath)}, {frames:N0} frame(s), {instructionsPerFrame:N0} instructions/frame, startFrame={startFrame:N0}, max {maxEvents:N0} event(s)");
    for (currentFrame = 0; currentFrame < frames && emittedEvents < maxEvents; currentFrame++)
    {
        frameBufferEventsThisFrame = 0;
        string status = "ok";
        string detail = string.Empty;
        try
        {
            machine.RunFrameCycles(instructionsPerFrame);
        }
        catch (Exception ex) when (ex is M68kException or Sh2Exception or InvalidOperationException)
        {
            status = ex.GetType().Name;
            detail = ex.Message;
        }

        _ = machine.RenderFrameRgb();
        WriteSnapshot("frame", string.Empty, string.Empty, 0, 0, 0, status == "ok" ? string.Empty : $"{status}: {detail}");
        if (status != "ok")
        {
            break;
        }
    }

    device.SystemRegisterAccessObserver = null;
    device.VdpRegisterAccessObserver = null;
    device.PaletteAccessObserver = null;
    device.FrameBufferAccessObserver = null;
    Console.WriteLine($"Wrote {emittedEvents:N0} 32X diagnostic trace row(s) to {Path.GetFullPath(outputCsv)}");
    Console.WriteLine($"Master PC=${device.MasterSh2.PC:X8}; slave PC=${device.SlaveSh2.PC:X8}; M68K PC=${machine.MainCpu.PC:X8}");
}

void TraceThirtyTwoXSh2Fault(string romPath, string outputCsv, int frames, int instructionsPerFrame, int history)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    if (!cartridge.Diagnostics.Requires32X)
    {
        Console.Error.WriteLine("The supplied ROM is not detected as a 32X cartridge.");
        return;
    }

    MegaDrive machine = new(cartridge, IsPalRegion(cartridge));
    machine.Reset();
    ThirtyTwoXDevice device = machine.Bus.ThirtyTwoX ?? throw new InvalidOperationException("32X device was not attached.");
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputCsv)) ?? ".");

    history = Math.Clamp(history, 16, 250_000);
    Sh2FaultTraceRow[] rows = new Sh2FaultTraceRow[history];
    int sequence = 0;
    int rowCount = 0;
    int frame = 0;
    int lastMasterUnhandled = 0;
    int lastSlaveUnhandled = 0;
    bool faultSeen = false;
    string faultDetail = string.Empty;

    void Record(Sh2Cpu.Sh2InstructionTrace trace)
    {
        if (device.MasterSh2.UnhandledOpcodeCount != lastMasterUnhandled ||
            device.SlaveSh2.UnhandledOpcodeCount != lastSlaveUnhandled)
        {
            faultSeen = true;
            faultDetail = device.MasterSh2.UnhandledOpcodeCount != lastMasterUnhandled
                ? $"master ${device.MasterSh2.LastUnhandledOpcode:X4}@${device.MasterSh2.LastUnhandledOpcodePc:X8}"
                : $"slave ${device.SlaveSh2.LastUnhandledOpcode:X4}@${device.SlaveSh2.LastUnhandledOpcodePc:X8}";
            throw new InvalidOperationException(faultDetail);
        }

        rows[sequence % rows.Length] = new Sh2FaultTraceRow(
            frame,
            sequence + 1,
            trace.Cpu,
            trace.Pc,
            trace.Opcode,
            trace.NextPc,
            trace.Sr,
            trace.R0,
            trace.R1,
            trace.R2,
            trace.R3,
            trace.R4,
            trace.R5,
            trace.R6,
            trace.R7,
            trace.R15);
        sequence++;
        rowCount = Math.Min(rowCount + 1, rows.Length);
    }

    device.MasterSh2.InstructionObserver = Record;
    device.SlaveSh2.InstructionObserver = Record;
    Console.WriteLine($"32X SH-2 fault trace: {Path.GetFileName(romPath)}, frames={frames:N0}, budget={instructionsPerFrame:N0}, history={history:N0}");
    for (frame = 0; frame < frames && !faultSeen; frame++)
    {
        lastMasterUnhandled = device.MasterSh2.UnhandledOpcodeCount;
        lastSlaveUnhandled = device.SlaveSh2.UnhandledOpcodeCount;
        try
        {
            machine.RunFrameCycles(instructionsPerFrame);
        }
        catch (Exception ex) when (ex is M68kException or Sh2Exception or InvalidOperationException)
        {
            faultSeen = true;
            faultDetail = ex.Message;
        }
    }

    device.MasterSh2.InstructionObserver = null;
    device.SlaveSh2.InstructionObserver = null;

    using StreamWriter writer = new(outputCsv, false, Encoding.UTF8) { AutoFlush = true };
    writer.WriteLine("frame,sequence,cpu,pc,opcode,nextPc,sr,r0,r1,r2,r3,r4,r5,r6,r7,r15");
    int first = sequence - rowCount;
    for (int i = 0; i < rowCount; i++)
    {
        Sh2FaultTraceRow row = rows[(first + i) % rows.Length];
        writer.WriteLine(string.Join(
            ',',
            row.Frame.ToString(CultureInfo.InvariantCulture),
            row.Sequence.ToString(CultureInfo.InvariantCulture),
            Csv(row.Cpu),
            $"${row.Pc:X8}",
            $"${row.Opcode:X4}",
            $"${row.NextPc:X8}",
            $"${row.Sr:X8}",
            $"${row.R0:X8}",
            $"${row.R1:X8}",
            $"${row.R2:X8}",
            $"${row.R3:X8}",
            $"${row.R4:X8}",
            $"${row.R5:X8}",
            $"${row.R6:X8}",
            $"${row.R7:X8}",
            $"${row.R15:X8}"));
    }

    Console.WriteLine($"Fault: {(faultSeen ? faultDetail : "not observed")}");
    Console.WriteLine($"Wrote {rowCount:N0} SH-2 trace row(s) to {Path.GetFullPath(outputCsv)}");
    Console.WriteLine($"Master PC=${device.MasterSh2.PC:X8} unhandled={device.MasterSh2.UnhandledOpcodeCount}; slave PC=${device.SlaveSh2.PC:X8} unhandled={device.SlaveSh2.UnhandledOpcodeCount}");
}

void InspectThirtyTwoX(string romPath, int frames, int instructionsPerFrame, uint address, int words)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    if (!cartridge.Diagnostics.Requires32X)
    {
        Console.Error.WriteLine("The supplied ROM is not detected as a 32X cartridge.");
        return;
    }

    MegaDrive machine = new(cartridge, IsPalRegion(cartridge));
    machine.Reset();
    ThirtyTwoXDevice device = machine.Bus.ThirtyTwoX ?? throw new InvalidOperationException("32X device was not attached.");
    for (int frame = 0; frame < frames; frame++)
    {
        machine.RunFrameCycles(instructionsPerFrame);
    }

    ThirtyTwoXDevice.ThirtyTwoXState state = device.CaptureState();
    Console.WriteLine($"Inspected {Path.GetFileName(romPath)} after {frames} frame(s)");
    Console.WriteLine($"M68K PC=${machine.MainCpu.PC:X8}; master PC=${device.MasterSh2.PC:X8} SR=${device.MasterSh2.SR:X8} GBR=${device.MasterSh2.GBR:X8} VBR=${device.MasterSh2.VBR:X8} PR=${device.MasterSh2.PR:X8}; slave PC=${device.SlaveSh2.PC:X8} SR=${device.SlaveSh2.SR:X8} GBR=${device.SlaveSh2.GBR:X8} VBR=${device.SlaveSh2.VBR:X8} PR=${device.SlaveSh2.PR:X8}");
    Console.WriteLine($"32X irq: mask=${device.MasterInterruptMask:X4}/${device.SlaveInterruptMask:X4} raw=${state.MasterInterruptMask:X4}/${state.SlaveInterruptMask:X4} pendingLevel={device.MasterSh2.PendingInterruptLevel}/{device.SlaveSh2.PendingInterruptLevel} pendingVector={device.MasterSh2.PendingInterruptVectorNumber}/{device.SlaveSh2.PendingInterruptVectorNumber} vPending={state.MasterVerticalInterruptPending}/{state.SlaveVerticalInterruptPending} hPending={state.MasterHorizontalInterruptPending}/{state.SlaveHorizontalInterruptPending} vblank={state.VBlank} hblank={state.HBlank}");
    Console.WriteLine($"32X boot: pending={device.BootRomHandshakePending} read={device.BootRomSignatureRead} launch={device.BootRomLaunchPending} post={device.BootRomPostStartSignaturePending} hidden={device.BootRomPostStartSignatureHiddenFromSh2} mask=${device.BootRomPostStartSignatureReadMask:X2}");
    Console.WriteLine($"32X sys: {FormatThirtyTwoXWords(device, system: true, 0x00, 0x40)}");
    for (int i = 0; i < words; i += 8)
    {
        uint lineAddress = address + (uint)(i * 2);
        StringBuilder line = new();
        line.Append('$');
        line.Append(lineAddress.ToString("X8", CultureInfo.InvariantCulture));
        line.Append(':');
        for (int j = 0; j < 8 && i + j < words; j++)
        {
            uint wordAddress = lineAddress + (uint)(j * 2);
            line.Append(' ');
            line.Append(ReadThirtyTwoXDebugWord(device, cartridge, wordAddress).ToString("X4", CultureInfo.InvariantCulture));
        }

        Console.WriteLine(line.ToString());
    }
}

void InspectThirtyTwoXState(string romPath, string statePath, int frames, int instructionsPerFrame, uint address, int words)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    if (!cartridge.Diagnostics.Requires32X)
    {
        Console.Error.WriteLine("The supplied ROM is not detected as a 32X cartridge.");
        return;
    }

    MegaDrive machine = new(cartridge, IsPalRegion(cartridge));
    machine.Reset();
    SaveStateSerializer.Load(machine, statePath);
    ThirtyTwoXDevice device = machine.Bus.ThirtyTwoX ?? throw new InvalidOperationException("32X device was not attached.");
    for (int frame = 0; frame < frames; frame++)
    {
        machine.RunFrameCycles(instructionsPerFrame);
    }

    ThirtyTwoXDevice.ThirtyTwoXState state = device.CaptureState();
    Console.WriteLine($"Inspected {Path.GetFileName(romPath)} from {Path.GetFileName(statePath)} plus {frames} frame(s)");
    Console.WriteLine($"M68K PC=${machine.MainCpu.PC:X8}; master PC=${device.MasterSh2.PC:X8} SR=${device.MasterSh2.SR:X8} GBR=${device.MasterSh2.GBR:X8} VBR=${device.MasterSh2.VBR:X8} PR=${device.MasterSh2.PR:X8}; slave PC=${device.SlaveSh2.PC:X8} SR=${device.SlaveSh2.SR:X8} GBR=${device.SlaveSh2.GBR:X8} VBR=${device.SlaveSh2.VBR:X8} PR=${device.SlaveSh2.PR:X8}");
    Console.WriteLine($"32X irq: mask=${device.MasterInterruptMask:X4}/${device.SlaveInterruptMask:X4} raw=${state.MasterInterruptMask:X4}/${state.SlaveInterruptMask:X4} pendingLevel={device.MasterSh2.PendingInterruptLevel}/{device.SlaveSh2.PendingInterruptLevel} pendingVector={device.MasterSh2.PendingInterruptVectorNumber}/{device.SlaveSh2.PendingInterruptVectorNumber} vPending={state.MasterVerticalInterruptPending}/{state.SlaveVerticalInterruptPending} hPending={state.MasterHorizontalInterruptPending}/{state.SlaveHorizontalInterruptPending} vblank={state.VBlank} hblank={state.HBlank}");
    Console.WriteLine($"32X boot: pending={device.BootRomHandshakePending} read={device.BootRomSignatureRead} launch={device.BootRomLaunchPending} post={device.BootRomPostStartSignaturePending} hidden={device.BootRomPostStartSignatureHiddenFromSh2} mask=${device.BootRomPostStartSignatureReadMask:X2}");
    Console.WriteLine($"32X sys: {FormatThirtyTwoXWords(device, system: true, 0x00, 0x40)}");
    for (int i = 0; i < words; i += 8)
    {
        uint lineAddress = address + (uint)(i * 2);
        StringBuilder line = new();
        line.Append('$');
        line.Append(lineAddress.ToString("X8", CultureInfo.InvariantCulture));
        line.Append(':');
        for (int j = 0; j < 8 && i + j < words; j++)
        {
            uint wordAddress = lineAddress + (uint)(j * 2);
            line.Append(' ');
            line.Append(ReadThirtyTwoXDebugWord(device, cartridge, wordAddress).ToString("X4", CultureInfo.InvariantCulture));
        }

        Console.WriteLine(line.ToString());
    }
}

void DumpThirtyTwoXSdram(string romPath, string outputPath, int frames, int instructionsPerFrame)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    if (!cartridge.Diagnostics.Requires32X)
    {
        Console.Error.WriteLine("The supplied ROM is not detected as a 32X cartridge.");
        return;
    }

    MegaDrive machine = new(cartridge, IsPalRegion(cartridge));
    machine.Reset();
    ThirtyTwoXDevice device = machine.Bus.ThirtyTwoX ?? throw new InvalidOperationException("32X device was not attached.");
    for (int frame = 0; frame < frames; frame++)
    {
        machine.RunFrameCycles(instructionsPerFrame);
    }

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
    File.WriteAllBytes(outputPath, device.Sdram.ToArray());
    Console.WriteLine($"Dumped {device.Sdram.Length:N0} SDRAM byte(s) from {Path.GetFileName(romPath)} after {frames:N0} frame(s) to {Path.GetFullPath(outputPath)}");
    Console.WriteLine($"M68K PC=${machine.MainCpu.PC:X8}; master PC=${device.MasterSh2.PC:X8}; slave PC=${device.SlaveSh2.PC:X8}");
}

void InspectThirtyTwoXCache(string romPath, int frames, int instructionsPerFrame, uint address)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    if (!cartridge.Diagnostics.Requires32X)
    {
        Console.Error.WriteLine("The supplied ROM is not detected as a 32X cartridge.");
        return;
    }

    MegaDrive machine = new(cartridge, IsPalRegion(cartridge));
    machine.Reset();
    ThirtyTwoXDevice device = machine.Bus.ThirtyTwoX ?? throw new InvalidOperationException("32X device was not attached.");
    for (int frame = 0; frame < frames; frame++)
    {
        machine.RunFrameCycles(instructionsPerFrame);
    }

    Console.WriteLine($"Inspected SH-2 cache for {Path.GetFileName(romPath)} after {frames} frame(s)");
    Console.WriteLine($"M68K PC=${machine.MainCpu.PC:X8}; master PC=${device.MasterSh2.PC:X8} SR=${device.MasterSh2.SR:X8}; slave PC=${device.SlaveSh2.PC:X8} SR=${device.SlaveSh2.SR:X8}");
    Console.WriteLine(device.FormatSh2CacheLineDebug(address, cpuIndex: 0));
    Console.WriteLine(device.FormatSh2CacheLineDebug(address, cpuIndex: 1));
}

void SummarizeThirtyTwoXFrameBuffers(string romPath, int frames, int instructionsPerFrame)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    if (!cartridge.Diagnostics.Requires32X)
    {
        Console.Error.WriteLine("The supplied ROM is not detected as a 32X cartridge.");
        return;
    }

    MegaDrive machine = new(cartridge, IsPalRegion(cartridge));
    machine.Reset();
    ThirtyTwoXDevice device = machine.Bus.ThirtyTwoX ?? throw new InvalidOperationException("32X device was not attached.");
    for (int frame = 0; frame < frames; frame++)
    {
        machine.RunFrameCycles(instructionsPerFrame);
    }

    ushort mode = device.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset);
    ushort fbctl = device.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset);
    Console.WriteLine($"32X framebuffer summary: {Path.GetFileName(romPath)} frame={frames}");
    Console.WriteLine($"mode={mode & 0x03} rawMode=${mode:X4} fbctl=${fbctl:X4} display={device.DisplayFrameBufferIndex} draw={device.DrawFrameBufferIndex} compositeMode={device.LastCompositeMode} fallback={device.LastCompositeUsedFallback} pixels={device.LastCompositeWrittenPixels}");
    int paletteNonzero = 0;
    Console.Write("palette nonzero:");
    for (int i = 0; i < ThirtyTwoXHardwareProfile.PaletteEntries; i++)
    {
        ushort color = device.ReadPaletteWord((ushort)(i * 2));
        if (color == 0)
        {
            continue;
        }

        paletteNonzero++;
        if (paletteNonzero <= 24)
        {
            Console.Write($" ${i:X2}:${color:X4}");
        }
    }

    Console.WriteLine($" count={paletteNonzero}");
    int bitmapMode = mode & 0x03;
    SummarizeFrameBuffer("display", device.DisplayFrameBuffer, device, bitmapMode);
    SummarizeFrameBuffer("draw", device.DrawFrameBuffer, device, bitmapMode);

    static void SummarizeFrameBuffer(string label, ReadOnlySpan<byte> buffer, ThirtyTwoXDevice device, int bitmapMode)
    {
        int nonzero = 0;
        int first = -1;
        int last = -1;
        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] == 0)
            {
                continue;
            }

            nonzero++;
            first = first < 0 ? i : first;
            last = i;
        }

        Console.WriteLine($"[{label}] nonzero={nonzero} first=${Math.Max(first, 0):X5} last=${Math.Max(last, 0):X5}");
        int[] histogram = new int[256];
        for (int i = 0x200; i < buffer.Length; i++)
        {
            histogram[buffer[i]]++;
        }

        Console.Write($"[{label}] top indices:");
        for (int rank = 0; rank < 8; rank++)
        {
            int bestIndex = 0;
            int bestCount = -1;
            for (int i = 1; i < histogram.Length; i++)
            {
                if (histogram[i] > bestCount)
                {
                    bestCount = histogram[i];
                    bestIndex = i;
                }
            }

            if (bestCount <= 0)
            {
                break;
            }

            ushort color = device.ReadPaletteWord((ushort)(bestIndex * 2));
            Console.Write($" ${bestIndex:X2}:{bestCount}/${color:X4}");
            histogram[bestIndex] = -1;
        }

        Console.WriteLine();
        for (int bucket = 0; bucket < buffer.Length; bucket += 0x2000)
        {
            int bucketNonzero = 0;
            int end = Math.Min(buffer.Length, bucket + 0x2000);
            for (int i = bucket; i < end; i++)
            {
                if (buffer[i] != 0)
                {
                    bucketNonzero++;
                }
            }

            if (bucketNonzero > 0)
            {
                Console.WriteLine($"[{label}] bucket ${bucket:X5}-${end - 1:X5} nonzero={bucketNonzero}");
            }
        }

        for (int y = 0; y < Vdp.ScreenHeight; y += 32)
        {
            int lineWordOffset = y * 2;
            int lineAddress = ReadBigEndianWordSpan(buffer, lineWordOffset) * 2;
            int colored = 0;
            int firstPixel = -1;
            int lastPixel = -1;
            if ((uint)lineAddress < (uint)buffer.Length)
            {
                if (bitmapMode == 2)
                {
                    for (int x = 0; x < ThirtyTwoXHardwareProfile.NominalWidth; x++)
                    {
                        int sourceIndex = lineAddress + (x * 2);
                        if (sourceIndex + 1 >= buffer.Length)
                        {
                            break;
                        }

                        ushort color = ReadBigEndianWordSpan(buffer, sourceIndex);
                        if (color != 0)
                        {
                            colored++;
                            firstPixel = firstPixel < 0 ? x : firstPixel;
                            lastPixel = x;
                        }
                    }
                }
                else if (bitmapMode == 3)
                {
                    int x = 0;
                    int sourceIndex = lineAddress;
                    while (x < ThirtyTwoXHardwareProfile.NominalWidth && sourceIndex + 1 < buffer.Length)
                    {
                        ushort span = ReadBigEndianWordSpan(buffer, sourceIndex);
                        sourceIndex += 2;
                        int runLength = (span >> 8) + 1;
                        int paletteIndex = span & 0xFF;
                        ushort color = device.ReadPaletteWord((ushort)(paletteIndex * 2));
                        int end = Math.Min(ThirtyTwoXHardwareProfile.NominalWidth, x + runLength);
                        if (paletteIndex != 0 && color != 0)
                        {
                            colored += end - x;
                            firstPixel = firstPixel < 0 ? x : firstPixel;
                            lastPixel = end - 1;
                        }

                        x = end;
                    }
                }
                else
                {
                    for (int x = 0; x < ThirtyTwoXHardwareProfile.NominalWidth; x++)
                    {
                        int sourceIndex = lineAddress + x;
                        if ((uint)sourceIndex >= (uint)buffer.Length)
                        {
                            break;
                        }

                        byte paletteIndex = (byte)(buffer[sourceIndex] & 0x3F);
                        ushort color = device.ReadPaletteWord((ushort)(paletteIndex * 2));
                        if (paletteIndex != 0 && color != 0)
                        {
                            colored++;
                            firstPixel = firstPixel < 0 ? x : firstPixel;
                            lastPixel = x;
                        }
                    }
                }
            }

            Console.WriteLine($"[{label}] y={y,3} line=${lineAddress:X5} colored={colored,3} firstX={firstPixel,4} lastX={lastPixel,4}");
        }
    }
}

void DumpThirtyTwoXRleLine(string romPath, int frames, int instructionsPerFrame, int line, int maxSpans)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    if (!cartridge.Diagnostics.Requires32X)
    {
        Console.Error.WriteLine("The supplied ROM is not detected as a 32X cartridge.");
        return;
    }

    MegaDrive machine = new(cartridge, IsPalRegion(cartridge));
    machine.Reset();
    ThirtyTwoXDevice device = machine.Bus.ThirtyTwoX ?? throw new InvalidOperationException("32X device was not attached.");
    for (int frame = 0; frame < frames; frame++)
    {
        machine.RunFrameCycles(instructionsPerFrame);
    }

    maxSpans = Math.Max(1, maxSpans);
    ReadOnlySpan<byte> source = device.DisplayFrameBuffer;
    if (line < 0)
    {
        Console.WriteLine($"RLE summary: {Path.GetFileName(romPath)} frame={frames}");
        Console.WriteLine($"mode={device.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset) & 0x03} fbctl=${device.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset):X4} draw={device.DrawFrameBufferIndex} display={device.DisplayFrameBufferIndex}");

        SummarizeRleBuffer("display", device.DisplayFrameBuffer);
        SummarizeRleBuffer("draw", device.DrawFrameBuffer);
        return;

        void SummarizeRleBuffer(string label, ReadOnlySpan<byte> buffer)
        {
            int populatedLines = 0;
            int totalSpans = 0;
            int totalColored = 0;
            Console.WriteLine($"[{label}]");
            for (int y = 0; y < Vdp.ScreenHeight; y++)
            {
                int pointer = ReadBigEndianWordSpan(buffer, y * 2) * 2;
                if (pointer < 0 || pointer >= buffer.Length)
                {
                    continue;
                }

                int summaryX = 0;
                int spans = 0;
                int colored = 0;
                int summarySourceIndex = pointer;
                int firstColoredX = -1;
                int lastColoredX = -1;
                while (summaryX < Vdp.ScreenWidth && summarySourceIndex + 1 < buffer.Length && spans < 256)
                {
                    ushort span = ReadBigEndianWordSpan(buffer, summarySourceIndex);
                    summarySourceIndex += 2;
                    int runLength = (span >> 8) + 1;
                    int paletteIndex = span & 0x00FF;
                    ushort color = ReadBigEndianWordSpan(device.Palette, paletteIndex * 2);
                    if (color != 0)
                    {
                        int runEnd = Math.Min(Vdp.ScreenWidth - 1, summaryX + runLength - 1);
                        firstColoredX = firstColoredX < 0 ? summaryX : firstColoredX;
                        lastColoredX = Math.Max(lastColoredX, runEnd);
                        colored += Math.Max(0, runEnd - summaryX + 1);
                    }

                    summaryX += runLength;
                    spans++;
                }

                if (colored > 0)
                {
                    populatedLines++;
                    totalSpans += spans;
                    totalColored += colored;
                    Console.WriteLine($"line={y,3} ptr=${pointer:X5} spans={spans,3} colored={colored,3} x={firstColoredX,3}-{lastColoredX,3}");
                }
            }

            Console.WriteLine($"[{label}] populatedLines={populatedLines} totalSpans={totalSpans} totalColored={totalColored}");
        }
    }

    line = Math.Clamp(line, 0, Vdp.ScreenHeight - 1);
    int lineTableOffset = line * 2;
    int lineAddress = ReadBigEndianWordSpan(source, lineTableOffset) * 2;

    Console.WriteLine($"RLE dump: {Path.GetFileName(romPath)} frame={frames} line={line}");
    Console.WriteLine($"mode={device.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset) & 0x03} fbctl=${device.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset):X4} draw={device.DrawFrameBufferIndex} display={device.DisplayFrameBufferIndex} linePtr=${lineAddress:X5}");
    if (lineAddress < 0 || lineAddress >= source.Length)
    {
        Console.WriteLine("line pointer is outside the display frame buffer");
        return;
    }

    int x = 0;
    int sourceIndex = lineAddress;
    for (int i = 0; i < maxSpans && x < Vdp.ScreenWidth && sourceIndex + 1 < source.Length; i++)
    {
        ushort span = ReadBigEndianWordSpan(source, sourceIndex);
        int runLength = (span >> 8) + 1;
        int paletteIndex = span & 0x00FF;
        Console.WriteLine($"{i,3}: addr=${sourceIndex:X5} word=${span:X4} x={x,3}-{Math.Min(Vdp.ScreenWidth - 1, x + runLength - 1),3} len={runLength,3} pal=${paletteIndex:X2}");
        sourceIndex += 2;
        x += runLength;
    }

    Console.WriteLine($"decodedX={x} next=${sourceIndex:X5}");
}

void DumpThirtyTwoXNodeRecords(string romPath, int frames, int instructionsPerFrame, uint address, int count, string mode)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    if (!cartridge.Diagnostics.Requires32X)
    {
        Console.Error.WriteLine("The supplied ROM is not detected as a 32X cartridge.");
        return;
    }

    MegaDrive machine = new(cartridge, IsPalRegion(cartridge));
    machine.Reset();
    ThirtyTwoXDevice device = machine.Bus.ThirtyTwoX ?? throw new InvalidOperationException("32X device was not attached.");
    for (int frame = 0; frame < frames; frame++)
    {
        machine.RunFrameCycles(instructionsPerFrame);
    }

    count = Math.Clamp(count, 1, 4096);
    bool followNext = mode.Equals("next", StringComparison.OrdinalIgnoreCase);
    bool followPrev = mode.Equals("prev", StringComparison.OrdinalIgnoreCase);
    uint current = address;
    HashSet<uint> visited = [];

    Console.WriteLine($"32X node dump: {Path.GetFileName(romPath)} frame={frames} mode={mode} start=${address:X8} count={count}");
    Console.WriteLine($"M68K PC=${machine.MainCpu.PC:X8}; master PC=${device.MasterSh2.PC:X8} SR=${device.MasterSh2.SR:X8}; slave PC=${device.SlaveSh2.PC:X8} SR=${device.SlaveSh2.SR:X8}");
    Console.WriteLine("idx,address,offset,prev,next,value,w0C,w10,w14,w18,w1C,w20,w24,note");

    for (int i = 0; i < count; i++)
    {
        if (!TryMapThirtyTwoXSdramMirror(current, out int offset) || offset + 0x27 >= device.Sdram.Length)
        {
            Console.WriteLine($"{i},${current:X8},,,,,,,,,,,,outside-sdram");
            break;
        }

        uint prev = ReadBigEndianLongSpan(device.Sdram, offset);
        uint next = ReadBigEndianLongSpan(device.Sdram, offset + 4);
        uint value = ReadBigEndianLongSpan(device.Sdram, offset + 8);
        uint w0C = ReadBigEndianLongSpan(device.Sdram, offset + 0x0C);
        uint w10 = ReadBigEndianLongSpan(device.Sdram, offset + 0x10);
        uint w14 = ReadBigEndianLongSpan(device.Sdram, offset + 0x14);
        uint w18 = ReadBigEndianLongSpan(device.Sdram, offset + 0x18);
        uint w1C = ReadBigEndianLongSpan(device.Sdram, offset + 0x1C);
        uint w20 = ReadBigEndianLongSpan(device.Sdram, offset + 0x20);
        uint w24 = ReadBigEndianLongSpan(device.Sdram, offset + 0x24);
        string note = visited.Add(current) ? string.Empty : "cycle";

        Console.WriteLine(string.Join(
            ',',
            i.ToString(CultureInfo.InvariantCulture),
            $"${current:X8}",
            $"${offset:X5}",
            $"${prev:X8}",
            $"${next:X8}",
            $"${value:X8}",
            $"${w0C:X8}",
            $"${w10:X8}",
            $"${w14:X8}",
            $"${w18:X8}",
            $"${w1C:X8}",
            $"${w20:X8}",
            $"${w24:X8}",
            note));

        if (!string.IsNullOrEmpty(note))
        {
            break;
        }

        if (followNext)
        {
            current = next;
        }
        else if (followPrev)
        {
            current = prev;
        }
        else
        {
            current += 0x28;
        }
    }
}

void TraceThirtyTwoX(string romPath, string outputCsv, int frames, int instructionsPerFrame)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    if (!cartridge.Diagnostics.Requires32X)
    {
        Console.Error.WriteLine("The supplied ROM is not detected as a 32X cartridge.");
        return;
    }

    MegaDrive machine = new(cartridge, IsPalRegion(cartridge));
    machine.Reset();
    ThirtyTwoXDevice device = machine.Bus.ThirtyTwoX ?? throw new InvalidOperationException("32X device was not attached.");
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputCsv)) ?? ".");
    int frameCommWrites = 0;
    int frameCommAccesses = 0;
    int frameMailboxAccesses = 0;
    int frameMailboxWrites = 0;
    int frameFrameBufferAccesses = 0;
    int frameFrameBufferLineTableWrites = 0;
    int frameFrameBufferPixelWrites = 0;
    int frameFrameBufferOverwriteWrites = 0;
    int frameFrameBufferNonzeroWrites = 0;
    int frameFrameBufferNonzeroPixelWrites = 0;
    int frameFrameBufferFirstNonzeroPixelOffset = -1;
    int frameFrameBufferLastNonzeroPixelOffset = -1;
    string lastCommWrite = string.Empty;
    string lastCommAccess = string.Empty;
    string lastMailboxAccess = string.Empty;
    string lastMailboxWrite = string.Empty;
    string lastFrameBufferAccess = string.Empty;
    device.SystemRegisterWriteObserver = write =>
    {
        if (write.Offset is >= ThirtyTwoXHardwareProfile.CommunicationPortOffset and < ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8)
        {
            frameCommWrites++;
            lastCommWrite = $"{write.Source} ${write.Offset:X2}=${write.Value:X2}";
        }
    };
    device.SystemRegisterAccessObserver = access =>
    {
        if (access.Offset is >= ThirtyTwoXHardwareProfile.CommunicationPortOffset and < ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8)
        {
            frameCommAccesses++;
            lastCommAccess = $"{access.Source} {access.Operation} ${access.Offset:X2}=${access.Value:X4}";
        }
    };
    device.Sh2MemoryAccessTraceFilter = address =>
    {
        uint normalized = address & 0x0FFF_FFFFu;
        return normalized is >= 0x0600_1780u and <= 0x0600_17AFu;
    };
    device.Sh2MemoryAccessObserver = access =>
    {
        frameMailboxAccesses++;
        lastMailboxAccess = $"{access.Source} {access.Operation} ${access.Address:X8}=${access.Value:X4}";
        if (access.Operation.StartsWith('W'))
        {
            frameMailboxWrites++;
            lastMailboxWrite = lastMailboxAccess;
        }
    };
    device.FrameBufferAccessObserver = access =>
    {
        frameFrameBufferAccesses++;
        if (access.Offset < 0x200)
        {
            frameFrameBufferLineTableWrites++;
        }
        else
        {
            frameFrameBufferPixelWrites++;
        }

        if (access.Value != 0)
        {
            frameFrameBufferNonzeroWrites++;
            if (access.Offset >= 0x200)
            {
                frameFrameBufferNonzeroPixelWrites++;
                int offset = (int)Math.Min(access.Offset, int.MaxValue);
                if (frameFrameBufferFirstNonzeroPixelOffset < 0)
                {
                    frameFrameBufferFirstNonzeroPixelOffset = offset;
                }

                frameFrameBufferLastNonzeroPixelOffset = offset;
            }
        }

        if (access.Operation.StartsWith("OW", StringComparison.Ordinal))
        {
            frameFrameBufferOverwriteWrites++;
        }

        lastFrameBufferAccess = $"{access.Source} {access.Operation} fb{access.BufferIndex}:${access.Offset:X5}=${access.Value:X4} pc=${access.Pc:X8} op=${access.Opcode:X4}";
    };

    using StreamWriter writer = new(outputCsv, false, Encoding.UTF8) { AutoFlush = true };
    writer.WriteLine("frame,status,detail,m68kPc,m68kSr,m68kExceptions,renderMode,nonBackgroundPixels,compositeMode,compositeFallback,compositePixels,bitmapMode,fbctl,draw,display,swapPending,vblank,hblank,masterPc,masterSr,masterLastPc,masterLastOpcode,masterPendingIrq,masterUnhandled,masterLastUnhandledPc,masterLastUnhandledOpcode,slavePc,slaveSr,slaveLastPc,slaveLastOpcode,slavePendingIrq,slaveUnhandled,slaveLastUnhandledPc,slaveLastUnhandledOpcode,masterMask,slaveMask,adapterEnabled,sh2ResetReleased,sh2HeldInReset,vdpAccessSh2,bootPending,bootRead,bootLaunch,sys00,sys02,sys20,sys22,sys24,sys26,dreq,dreqFifo,modeWrites,fbctlWrites,vdpWrites,fbBytes,paletteBytes,dreqWrites,dreqDmaWords,displayFbNonzero,drawFbNonzero,displayFbPayloadNonzero,drawFbPayloadNonzero,paletteNonzero,fbAccesses,fbLineTableWrites,fbPixelWrites,fbOverwriteWrites,fbNonzeroWrites,fbNonzeroPixelWrites,fbFirstNonzeroPixelOffset,fbLastNonzeroPixelOffset,lastFbAccess,commWrites,lastCommWrite,commAccesses,lastCommAccess,mailboxAccesses,lastMailboxAccess,mailboxWrites,lastMailboxWrite");
    Console.WriteLine($"32X frame trace: {Path.GetFileName(romPath)}, {frames:N0} frame(s), {instructionsPerFrame:N0} instructions/frame");
    for (int frame = 0; frame < frames; frame++)
    {
        frameCommWrites = 0;
        frameCommAccesses = 0;
        frameMailboxAccesses = 0;
        frameMailboxWrites = 0;
        frameFrameBufferAccesses = 0;
        frameFrameBufferLineTableWrites = 0;
        frameFrameBufferPixelWrites = 0;
        frameFrameBufferOverwriteWrites = 0;
        frameFrameBufferNonzeroWrites = 0;
        frameFrameBufferNonzeroPixelWrites = 0;
        frameFrameBufferFirstNonzeroPixelOffset = -1;
        frameFrameBufferLastNonzeroPixelOffset = -1;
        lastCommWrite = string.Empty;
        lastCommAccess = string.Empty;
        lastMailboxAccess = string.Empty;
        lastMailboxWrite = string.Empty;
        lastFrameBufferAccess = string.Empty;
        string status = "ok";
        string detail = string.Empty;
        try
        {
            machine.RunFrameCycles(instructionsPerFrame);
        }
        catch (Exception ex) when (ex is M68kException or Sh2Exception or InvalidOperationException)
        {
            status = ex.GetType().Name;
            detail = ex.Message;
        }

        byte[] rgb = machine.RenderFrameRgb();
        int nonBackground = CountNonBackgroundPixels(machine.Vdp, rgb);
        Action<ThirtyTwoXDevice.SystemRegisterAccessTrace>? registerAccessObserver = device.SystemRegisterAccessObserver;
        device.SystemRegisterAccessObserver = null;
        ushort sys00 = device.ReadSystemRegisterWord(0x00);
        ushort sys02 = device.ReadSystemRegisterWord(0x02);
        ushort sys20 = device.DebugPeekSystemRegisterWord(0x20);
        ushort sys22 = device.DebugPeekSystemRegisterWord(0x22);
        ushort sys24 = device.DebugPeekSystemRegisterWord(0x24);
        ushort sys26 = device.DebugPeekSystemRegisterWord(0x26);
        ushort dreq = device.ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.DreqControlOffset);
        device.SystemRegisterAccessObserver = registerAccessObserver;
        writer.WriteLine(string.Join(
            ',',
            frame.ToString(CultureInfo.InvariantCulture),
            status,
            $"\"{EscapeCsv(detail)}\"",
            $"${machine.MainCpu.PC:X8}",
            $"${machine.MainCpu.SR:X4}",
            $"\"{EscapeCsv(FormatExceptions(machine.MainCpu))}\"",
            machine.Vdp.LastRenderMode,
            nonBackground.ToString(CultureInfo.InvariantCulture),
            device.LastCompositeMode.ToString(CultureInfo.InvariantCulture),
            device.LastCompositeUsedFallback ? "true" : "false",
            device.LastCompositeWrittenPixels.ToString(CultureInfo.InvariantCulture),
            (device.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset) & 0x03).ToString(CultureInfo.InvariantCulture),
            $"${device.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset):X4}",
            device.DrawFrameBufferIndex.ToString(CultureInfo.InvariantCulture),
            device.DisplayFrameBufferIndex.ToString(CultureInfo.InvariantCulture),
            device.FrameBufferSwapPending ? "true" : "false",
            device.VBlank ? "true" : "false",
            device.HBlank ? "true" : "false",
            $"${device.MasterSh2.PC:X8}",
            $"${device.MasterSh2.SR:X8}",
            $"${device.MasterSh2.LastOpcodePc:X8}",
            $"${device.MasterSh2.LastOpcode:X4}",
            device.MasterSh2.PendingInterruptLevel.ToString(CultureInfo.InvariantCulture),
            device.MasterSh2.UnhandledOpcodeCount.ToString(CultureInfo.InvariantCulture),
            $"${device.MasterSh2.LastUnhandledOpcodePc:X8}",
            $"${device.MasterSh2.LastUnhandledOpcode:X4}",
            $"${device.SlaveSh2.PC:X8}",
            $"${device.SlaveSh2.SR:X8}",
            $"${device.SlaveSh2.LastOpcodePc:X8}",
            $"${device.SlaveSh2.LastOpcode:X4}",
            device.SlaveSh2.PendingInterruptLevel.ToString(CultureInfo.InvariantCulture),
            device.SlaveSh2.UnhandledOpcodeCount.ToString(CultureInfo.InvariantCulture),
            $"${device.SlaveSh2.LastUnhandledOpcodePc:X8}",
            $"${device.SlaveSh2.LastUnhandledOpcode:X4}",
            $"${device.MasterInterruptMask:X4}",
            $"${device.SlaveInterruptMask:X4}",
            device.AdapterEnabled ? "true" : "false",
            device.Sh2ResetReleased ? "true" : "false",
            device.Sh2HeldInReset ? "true" : "false",
            device.VdpAccessGrantedToSh2 ? "true" : "false",
            device.BootRomHandshakePending ? "true" : "false",
            device.BootRomSignatureRead ? "true" : "false",
            device.BootRomLaunchPending ? "true" : "false",
            $"${sys00:X4}",
            $"${sys02:X4}",
            $"${sys20:X4}",
            $"${sys22:X4}",
            $"${sys24:X4}",
            $"${sys26:X4}",
            $"${dreq:X4}",
            device.DreqFifoCount.ToString(CultureInfo.InvariantCulture),
            device.BitmapModeWriteCount.ToString(CultureInfo.InvariantCulture),
            device.FrameBufferControlWriteCount.ToString(CultureInfo.InvariantCulture),
            device.VdpRegisterWriteCount.ToString(CultureInfo.InvariantCulture),
            device.FrameBufferByteWriteCount.ToString(CultureInfo.InvariantCulture),
            device.PaletteByteWriteCount.ToString(CultureInfo.InvariantCulture),
            device.DreqFifoWriteCount.ToString(CultureInfo.InvariantCulture),
            device.DreqDmaWordTransferCount.ToString(CultureInfo.InvariantCulture),
            CountNonzeroBytes(device.DisplayFrameBuffer).ToString(CultureInfo.InvariantCulture),
            CountNonzeroBytes(device.DrawFrameBuffer).ToString(CultureInfo.InvariantCulture),
            CountFramebufferPayloadNonzero(device.DisplayFrameBuffer).ToString(CultureInfo.InvariantCulture),
            CountFramebufferPayloadNonzero(device.DrawFrameBuffer).ToString(CultureInfo.InvariantCulture),
            CountNonzeroBytes(device.Palette).ToString(CultureInfo.InvariantCulture),
            frameFrameBufferAccesses.ToString(CultureInfo.InvariantCulture),
            frameFrameBufferLineTableWrites.ToString(CultureInfo.InvariantCulture),
            frameFrameBufferPixelWrites.ToString(CultureInfo.InvariantCulture),
            frameFrameBufferOverwriteWrites.ToString(CultureInfo.InvariantCulture),
            frameFrameBufferNonzeroWrites.ToString(CultureInfo.InvariantCulture),
            frameFrameBufferNonzeroPixelWrites.ToString(CultureInfo.InvariantCulture),
            frameFrameBufferFirstNonzeroPixelOffset < 0 ? string.Empty : $"${frameFrameBufferFirstNonzeroPixelOffset:X5}",
            frameFrameBufferLastNonzeroPixelOffset < 0 ? string.Empty : $"${frameFrameBufferLastNonzeroPixelOffset:X5}",
            $"\"{EscapeCsv(lastFrameBufferAccess)}\"",
            frameCommWrites.ToString(CultureInfo.InvariantCulture),
            $"\"{EscapeCsv(lastCommWrite)}\"",
            frameCommAccesses.ToString(CultureInfo.InvariantCulture),
            $"\"{EscapeCsv(lastCommAccess)}\"",
            frameMailboxAccesses.ToString(CultureInfo.InvariantCulture),
            $"\"{EscapeCsv(lastMailboxAccess)}\"",
            frameMailboxWrites.ToString(CultureInfo.InvariantCulture),
            $"\"{EscapeCsv(lastMailboxWrite)}\""));

        if (status != "ok")
        {
            break;
        }
    }

    Console.WriteLine($"Wrote 32X trace to {Path.GetFullPath(outputCsv)}");
}

void SweepThirtyTwoX(string romFolder, string outputFolder, int frames, int instructionsPerFrame, bool writeScreenshots, bool resume, string? filter, int? limit, double adaptiveTimeLimitSeconds, double caseTimeLimitSeconds)
{
    string fullRomFolder = Path.GetFullPath(romFolder);
    string fullOutputFolder = Path.GetFullPath(outputFolder);
    string screenshotFolder = Path.Combine(fullOutputFolder, "screenshots");
    string[] files = EnumerateRomFiles(fullRomFolder)
        .Where(IsLikelyThirtyTwoXRom)
        .ToArray();
    if (!string.IsNullOrWhiteSpace(filter))
    {
        files = files
            .Where(path => Path.GetRelativePath(fullRomFolder, path).Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    Directory.CreateDirectory(fullOutputFolder);
    if (writeScreenshots)
    {
        Directory.CreateDirectory(screenshotFolder);
    }

    string csvPath = Path.Combine(fullOutputFolder, "32x-sweep.csv");
    HashSet<string> completed = resume ? ReadCompletedSweepRoms(csvPath) : [];
    bool append = resume && completed.Count > 0;
    using StreamWriter writer = new(csvPath, append, Encoding.UTF8) { AutoFlush = true };
    if (!append)
    {
        writer.WriteLine(ThirtyTwoXSweepResult.CsvHeader);
    }

    Console.WriteLine($"32X sweep: {files.Length:N0} ROM(s), {frames:N0} frame(s), {instructionsPerFrame:N0} instructions/frame, adaptive cap={adaptiveTimeLimitSeconds:0.###}s{(caseTimeLimitSeconds > 0.0 ? $", case cap={caseTimeLimitSeconds:0.###}s" : string.Empty)}{(resume ? $", resume skipped={completed.Count:N0}" : string.Empty)}{(limit is > 0 ? $", limit={limit.Value:N0}" : string.Empty)}");
    int processed = 0;
    foreach (string file in files)
    {
        string relative = Path.GetRelativePath(fullRomFolder, file);
        if (completed.Contains(relative))
        {
            Console.WriteLine($"{"skip",-22} {relative}");
            continue;
        }

        ThirtyTwoXSweepResult result = RunThirtyTwoXSweepCase(file, fullRomFolder, screenshotFolder, frames, instructionsPerFrame, writeScreenshots, adaptiveTimeLimitSeconds, caseTimeLimitSeconds);
        writer.WriteLine(result.ToCsv());
        Console.WriteLine($"{result.Status,-22} {result.RelativeRom}");
        processed++;
        if (limit is > 0 && processed >= limit.Value)
        {
            break;
        }
    }

    Console.WriteLine($"Wrote 32X sweep report to {Path.GetFullPath(csvPath)}");
}

HashSet<string> ReadCompletedSweepRoms(string csvPath)
{
    HashSet<string> completed = new(StringComparer.OrdinalIgnoreCase);
    if (!File.Exists(csvPath))
    {
        return completed;
    }

    foreach (string line in File.ReadLines(csvPath).Skip(1))
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        string? rom = ReadFirstCsvField(line);
        if (!string.IsNullOrWhiteSpace(rom))
        {
            completed.Add(rom);
        }
    }

    return completed;
}

string? ReadFirstCsvField(string line)
{
    if (line.Length == 0)
    {
        return null;
    }

    if (line[0] != '"')
    {
        int comma = line.IndexOf(',');
        return comma < 0 ? line : line[..comma];
    }

    StringBuilder value = new();
    for (int i = 1; i < line.Length; i++)
    {
        char c = line[i];
        if (c == '"')
        {
            if (i + 1 < line.Length && line[i + 1] == '"')
            {
                value.Append('"');
                i++;
                continue;
            }

            return value.ToString();
        }

        value.Append(c);
    }

    return value.ToString();
}

ThirtyTwoXSweepResult RunThirtyTwoXSweepCase(string romPath, string romRoot, string screenshotFolder, int frames, int instructionsPerFrame, bool writeScreenshot, double adaptiveTimeLimitSeconds, double caseTimeLimitSeconds)
{
    string relative = Path.GetRelativePath(romRoot, romPath);
    System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
    MegaDrive? machine = null;
    ThirtyTwoXDevice? device = null;
    byte[] rgb = new byte[Vdp.ScreenWidth * Vdp.ScreenHeight * 3];
    int completedFrames = 0;
    int nonBackground = 0;
    int maxNonBackground = 0;
    string status = "ok";
    string detail = string.Empty;
    string hash = string.Empty;
    string bmpPath = string.Empty;

    try
    {
        CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
        if (!cartridge.Diagnostics.Requires32X)
        {
            status = "not-32x";
            detail = "Cartridge header/diagnostics did not request 32X hardware.";
        }
        else
        {
            machine = new MegaDrive(cartridge, IsPalRegion(cartridge));
            machine.Reset();
            device = machine.Bus.ThirtyTwoX ?? throw new InvalidOperationException("32X device was not attached.");
            for (; completedFrames < frames; completedFrames++)
            {
                if (caseTimeLimitSeconds > 0.0 && stopwatch.Elapsed.TotalSeconds >= caseTimeLimitSeconds)
                {
                    status = "case-timeout";
                    detail = $"case time cap after {completedFrames:N0} frame(s)";
                    break;
                }

                machine.RunFrameCycles(instructionsPerFrame);
                machine.RenderFrameRgbInto(rgb);
                nonBackground = CountNonBackgroundPixels(machine.Vdp, rgb);
                maxNonBackground = Math.Max(maxNonBackground, nonBackground);
                if (device is not null && completedFrames >= 44)
                {
                    string liveStatus = ClassifyThirtyTwoXSweep(machine, device, nonBackground, maxNonBackground);
                    if (IsDirectVisibleThirtyTwoXStatus(liveStatus))
                    {
                        completedFrames++;
                        status = liveStatus;
                        detail = $"early visible 32X classification after {completedFrames:N0} frame(s)";
                        break;
                    }
                }
            }
        }
    }
    catch (Exception ex) when (ex is M68kException or Sh2Exception or InvalidOperationException or IOException)
    {
        status = ex.GetType().Name;
        detail = ex.Message;
        if (machine is not null)
        {
            machine.RenderFrameRgbInto(rgb);
            nonBackground = CountNonBackgroundPixels(machine.Vdp, rgb);
            maxNonBackground = Math.Max(maxNonBackground, nonBackground);
        }
    }

    stopwatch.Stop();
    if (machine is not null)
    {
        if (device is not null && status == "ok")
        {
            status = ClassifyThirtyTwoXSweep(machine, device, nonBackground, maxNonBackground);
            if (adaptiveTimeLimitSeconds > 0.0 && ShouldAdaptiveResampleThirtyTwoX(status, completedFrames))
            {
                string initialStatus = status;
                int initialFrames = completedFrames;
                int adaptiveTargetFrames = Math.Max(completedFrames, 900);
                int adaptiveInstructionBudget = Math.Max(instructionsPerFrame, 50_000);
                for (; completedFrames < adaptiveTargetFrames; completedFrames++)
                {
                    if (caseTimeLimitSeconds > 0.0 && stopwatch.Elapsed.TotalSeconds >= caseTimeLimitSeconds)
                    {
                        detail = $"case time cap during adaptive resample after {completedFrames:N0} frame(s)";
                        break;
                    }

                    if (stopwatch.Elapsed.TotalSeconds >= adaptiveTimeLimitSeconds)
                    {
                        detail = $"adaptive resample time cap after {completedFrames:N0} frame(s)";
                        break;
                    }

                    machine.RunFrameCycles(adaptiveInstructionBudget);
                    machine.RenderFrameRgbInto(rgb);
                    nonBackground = CountNonBackgroundPixels(machine.Vdp, rgb);
                    maxNonBackground = Math.Max(maxNonBackground, nonBackground);
                    status = ClassifyThirtyTwoXSweep(machine, device, nonBackground, maxNonBackground);
                    if (IsVisibleThirtyTwoXStatus(status))
                    {
                        break;
                    }
                }

                if (status != initialStatus)
                {
                    detail = $"adaptive resample: {initialStatus} at {initialFrames:N0} frame(s), {status} at {completedFrames:N0} frame(s)";
                }
            }
        }

        hash = Convert.ToHexString(SHA256.HashData(rgb));
        if (writeScreenshot)
        {
            bmpPath = Path.Combine(screenshotFolder, $"{SanitizeFileName(relative)}.bmp");
            WriteBmp(bmpPath, Vdp.ScreenWidth, Vdp.ScreenHeight, rgb);
        }
    }

    double fps = completedFrames <= 0 || stopwatch.Elapsed.TotalSeconds <= 0.0
        ? 0.0
        : completedFrames / stopwatch.Elapsed.TotalSeconds;
    ThirtyTwoXDevice.ThirtyTwoXState? thirtyTwoXState = device?.CaptureState();
    string exceptions = machine is null ? string.Empty : FormatExceptions(machine.MainCpu);

    return new ThirtyTwoXSweepResult(
        relative,
        status,
        completedFrames,
        stopwatch.ElapsedMilliseconds,
        fps,
        machine?.MainCpu.PC ?? 0,
        exceptions,
        CountFaultExceptionEvents(exceptions),
        CountTrapOrInterruptEvents(exceptions),
        machine?.Vdp.LastRenderMode ?? string.Empty,
        nonBackground,
        maxNonBackground,
        device?.LastCompositeMode ?? 0,
        device?.LastCompositeUsedFallback ?? false,
        device?.LastCompositeWrittenPixels ?? 0,
        device is null ? 0 : device.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset) & 0x03,
        device is null ? (ushort)0 : device.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.FrameBufferControlOffset),
        device?.BitmapModeWriteCount ?? 0,
        device?.FrameBufferControlWriteCount ?? 0,
        device?.VdpRegisterWriteCount ?? 0,
        device?.FrameBufferByteWriteCount ?? 0,
        device?.PaletteByteWriteCount ?? 0,
        device?.DreqFifoWriteCount ?? 0,
        device?.DreqDmaWordTransferCount ?? 0,
        device is null ? 0 : CountNonzeroBytes(device.DisplayFrameBuffer),
        device is null ? 0 : CountNonzeroBytes(device.DrawFrameBuffer),
        device is null ? 0 : CountFramebufferPayloadNonzero(device.DisplayFrameBuffer),
        device is null ? 0 : CountFramebufferPayloadNonzero(device.DrawFrameBuffer),
        device is null ? 0 : CountNonzeroBytes(device.Palette),
        thirtyTwoXState is null ? (ushort)0 : ReadBigEndianWord(thirtyTwoXState.SystemRegisters, ThirtyTwoXHardwareProfile.CommunicationPortOffset),
        thirtyTwoXState is null ? (ushort)0 : ReadBigEndianWord(thirtyTwoXState.SystemRegisters, ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2),
        thirtyTwoXState is null ? (ushort)0 : ReadBigEndianWord(thirtyTwoXState.SystemRegisters, ThirtyTwoXHardwareProfile.CommunicationPortOffset + 4),
        thirtyTwoXState is null ? (ushort)0 : ReadBigEndianWord(thirtyTwoXState.SystemRegisters, ThirtyTwoXHardwareProfile.CommunicationPortOffset + 6),
        thirtyTwoXState?.MasterSh2.PC ?? 0,
        thirtyTwoXState?.SlaveSh2.PC ?? 0,
        thirtyTwoXState?.MasterSh2.LastOpcode ?? 0,
        thirtyTwoXState?.SlaveSh2.LastOpcode ?? 0,
        device?.MasterSh2.UnhandledOpcodeCount ?? 0,
        device?.SlaveSh2.UnhandledOpcodeCount ?? 0,
        thirtyTwoXState?.MasterInterruptMask ?? 0,
        thirtyTwoXState?.SlaveInterruptMask ?? 0,
        thirtyTwoXState?.PwmLeft.Length ?? 0,
        thirtyTwoXState?.PwmRight.Length ?? 0,
        thirtyTwoXState?.PwmMono.Length ?? 0,
        thirtyTwoXState?.PwmLeftHardwareFifo.Length ?? 0,
        thirtyTwoXState?.PwmRightHardwareFifo.Length ?? 0,
        thirtyTwoXState?.PwmMonoHardwareFifo.Length ?? 0,
        thirtyTwoXState?.PwmCycleCounter ?? 0,
        thirtyTwoXState?.PwmTimerCounter ?? 0,
        thirtyTwoXState?.MasterPwmInterruptPending ?? false,
        thirtyTwoXState?.SlavePwmInterruptPending ?? false,
        thirtyTwoXState?.BootRomHandshakePending ?? false,
        thirtyTwoXState?.BootRomSignatureRead ?? false,
        thirtyTwoXState?.BootRomLaunchPending ?? false,
        hash,
        bmpPath,
        detail);
}

static ushort ReadBigEndianWord(byte[] data, int offset)
{
    if ((uint)(offset + 1) >= (uint)data.Length)
    {
        return 0;
    }

    return (ushort)((data[offset] << 8) | data[offset + 1]);
}

static ushort ReadBigEndianWordSpan(ReadOnlySpan<byte> data, int offset)
{
    if ((uint)(offset + 1) >= (uint)data.Length)
    {
        return 0;
    }

    return (ushort)((data[offset] << 8) | data[offset + 1]);
}

static uint ReadBigEndianLongSpan(ReadOnlySpan<byte> data, int offset)
{
    if ((uint)(offset + 3) >= (uint)data.Length)
    {
        return 0;
    }

    return (uint)((data[offset] << 24) |
        (data[offset + 1] << 16) |
        (data[offset + 2] << 8) |
        data[offset + 3]);
}

string ClassifyThirtyTwoXSweep(MegaDrive machine, ThirtyTwoXDevice device, int nonBackground, int maxNonBackground)
{
    string exceptions = FormatExceptions(machine.MainCpu);
    string exceptionSuffix = ThirtyTwoXExceptionStatusSuffix(exceptions);
    int displayFbNonzero = CountNonzeroBytes(device.DisplayFrameBuffer);
    int drawFbNonzero = CountNonzeroBytes(device.DrawFrameBuffer);
    int displayFbPayloadNonzero = CountFramebufferPayloadNonzero(device.DisplayFrameBuffer);
    int drawFbPayloadNonzero = CountFramebufferPayloadNonzero(device.DrawFrameBuffer);
    int paletteNonzero = CountNonzeroBytes(device.Palette);
    int bitmapMode = device.ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset) & 0x03;
    if (device.BootRomHandshakePending || device.BootRomLaunchPending)
    {
        return "boot-wait";
    }

    if (device.Sh2HeldInReset && device.BitmapModeWriteCount == 0)
    {
        return "sh2-reset-wait";
    }

    if (device.LastCompositeMode != 0 && device.LastCompositeWrittenPixels > 0 && nonBackground > 0)
    {
        string visible = device.LastCompositeUsedFallback ? "visible-32x-fallback" : "visible-32x";
        return WithThirtyTwoXExceptionSuffix(visible, exceptionSuffix);
    }

    if (nonBackground > 0)
    {
        if (displayFbNonzero + drawFbNonzero > 0)
        {
            if (displayFbPayloadNonzero + drawFbPayloadNonzero == 0)
            {
                string lineTableOnlyVisible = paletteNonzero == 0 ? "md-visible-framebuffer-line-table-only-no-palette" : "md-visible-framebuffer-line-table-only";
                return WithThirtyTwoXExceptionSuffix(lineTableOnlyVisible, exceptionSuffix);
            }

            string visible = paletteNonzero == 0 ? "md-visible-framebuffer-no-palette" : "md-visible-framebuffer-dark";
            return WithThirtyTwoXExceptionSuffix(visible, exceptionSuffix);
        }

        if (device.FrameBufferByteWriteCount > 0 || device.PaletteByteWriteCount > 0 || bitmapMode != 0)
        {
            string visible = "md-visible-32x-vdp-idle";
            return WithThirtyTwoXExceptionSuffix(visible, exceptionSuffix);
        }

        return WithThirtyTwoXExceptionSuffix("md-only", exceptionSuffix);
    }

    if (displayFbNonzero + drawFbNonzero > 0)
    {
        if (displayFbPayloadNonzero + drawFbPayloadNonzero == 0)
        {
            return paletteNonzero == 0 ? "framebuffer-line-table-only-no-palette" : "framebuffer-line-table-only";
        }

        return paletteNonzero == 0 ? "framebuffer-no-palette" : "framebuffer-dark";
    }

    if (exceptionSuffix.Length != 0)
    {
        return exceptionSuffix[1..];
    }

    if (device.FrameBufferByteWriteCount > 0 || device.PaletteByteWriteCount > 0 || bitmapMode != 0)
    {
        return "vdp-dark";
    }

    if (device.MasterSh2.UnhandledOpcodeCount > 0 || device.SlaveSh2.UnhandledOpcodeCount > 0)
    {
        return "sh2-unhandled-opcode";
    }

    return "stalled";
}

string ThirtyTwoXExceptionStatusSuffix(string exceptions)
{
    if (!HasCpuExceptions(exceptions))
    {
        return string.Empty;
    }

    return HasFaultExceptions(exceptions) ? "-m68k-fault" : "-m68k-trap";
}

static string WithThirtyTwoXExceptionSuffix(string status, string exceptionSuffix)
{
    return exceptionSuffix.Length == 0 ? status : status + exceptionSuffix;
}

bool ShouldAdaptiveResampleThirtyTwoX(string status, int completedFrames)
{
    if (completedFrames >= 900)
    {
        return false;
    }

    string baseStatus = status
        .Replace("-m68k-fault", string.Empty, StringComparison.Ordinal)
        .Replace("-m68k-trap", string.Empty, StringComparison.Ordinal)
        .Replace("-m68k-exception", string.Empty, StringComparison.Ordinal);

    return baseStatus is
        "vdp-dark" or
        "framebuffer-dark" or
        "framebuffer-no-palette" or
        "framebuffer-line-table-only" or
        "framebuffer-line-table-only-no-palette" or
        "md-visible-32x-vdp-idle" or
        "md-visible-framebuffer-line-table-only" or
        "md-visible-framebuffer-line-table-only-no-palette" or
        "stalled";
}

static int CountFramebufferPayloadNonzero(ReadOnlySpan<byte> frameBuffer)
{
    int lineTableBytes = Math.Min(frameBuffer.Length, ThirtyTwoXHardwareProfile.NtscVisibleLines * 2);
    int count = 0;
    for (int i = lineTableBytes; i < frameBuffer.Length; i++)
    {
        if (frameBuffer[i] != 0)
        {
            count++;
        }
    }

    return count;
}

bool IsVisibleThirtyTwoXStatus(string status)
{
    return status.StartsWith("visible-32x", StringComparison.Ordinal) ||
        status.StartsWith("md-visible", StringComparison.Ordinal);
}

bool IsDirectVisibleThirtyTwoXStatus(string status)
{
    return status.StartsWith("visible-32x", StringComparison.Ordinal);
}

bool IsLikelyThirtyTwoXRom(string path)
{
    if (Path.GetExtension(path).Equals(".32x", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    try
    {
        return CartridgeImage.FromFile(path).Diagnostics.Requires32X;
    }
    catch
    {
        return false;
    }
}

void RegressFolder(string folder, string outputCsv, int frames, int instructionsPerFrame)
{
    string fullFolder = Path.GetFullPath(folder);
    string[] files = Directory
        .EnumerateFiles(fullFolder, "*.*", SearchOption.AllDirectories)
        .Where(path => romExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputCsv)) ?? ".");
    using StreamWriter writer = new(outputCsv, false, Encoding.UTF8);
    writer.WriteLine("rom,status,pc,exceptions,renderMode,nonBackgroundPixels,sha256");
    foreach (string file in files)
    {
        string status = "ok";
        string hash = string.Empty;
        int nonBackground = 0;
        MegaDrive? machine = null;
        try
        {
            CartridgeImage cartridge = CartridgeImage.FromFile(file);
            machine = new MegaDrive(cartridge, IsPalRegion(cartridge));
            machine.Reset();
            for (int frame = 0; frame < frames; frame++)
            {
                machine.RunFrameCycles(instructionsPerFrame);
            }

            byte[] rgb = machine.RenderFrameRgb();
            nonBackground = CountNonBackgroundPixels(machine.Vdp, rgb);
            hash = Convert.ToHexString(SHA256.HashData(rgb));
        }
        catch (Exception ex)
        {
            status = ex.GetType().Name;
        }

        string relative = Path.GetRelativePath(fullFolder, file);
        string exceptions = machine is null ? string.Empty : FormatExceptions(machine.MainCpu);
        string mode = machine?.Vdp.LastRenderMode ?? string.Empty;
        uint pc = machine?.MainCpu.PC ?? 0;
        writer.WriteLine($"\"{EscapeCsv(relative)}\",{status},${pc:X8},\"{EscapeCsv(exceptions)}\",{mode},{nonBackground},{hash}");
    }

    Console.WriteLine($"Wrote regression report for {files.Length} ROM(s) to {Path.GetFullPath(outputCsv)}");
}

void RunCompatibilityDashboard(string romFolder, string outputFolder, int frames, int instructionsPerFrame, bool writeScreenshots, bool resume, string? filter)
{
    string fullRomFolder = Path.GetFullPath(romFolder);
    string fullOutputFolder = Path.GetFullPath(outputFolder);
    string screenshotFolder = Path.Combine(fullOutputFolder, "screenshots");
    string[] files = EnumerateRomFiles(fullRomFolder);
    if (!string.IsNullOrWhiteSpace(filter))
    {
        files = files
            .Where(path => Path.GetRelativePath(fullRomFolder, path).Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    Directory.CreateDirectory(fullOutputFolder);
    if (writeScreenshots)
    {
        Directory.CreateDirectory(screenshotFolder);
    }

    List<CompatibilityResult> results = [];
    string csvPath = Path.Combine(fullOutputFolder, "compatibility.csv");
    HashSet<string> completed = new(StringComparer.OrdinalIgnoreCase);
    if (resume && File.Exists(csvPath))
    {
        HashSet<string> selected = files
            .Select(path => Path.GetRelativePath(fullRomFolder, path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (CompatibilityCsvRow row in LoadCompatibilityRows(csvPath))
        {
            if (row.Frames >= frames && selected.Contains(row.Rom))
            {
                completed.Add(row.Rom);
                results.Add(CompatibilityResult.FromCsvRow(row));
            }
        }
    }

    using StreamWriter writer = new(csvPath, false, Encoding.UTF8) { AutoFlush = true };
    writer.WriteLine("rom,status,frames,elapsedMs,fps,pc,exceptions,renderMode,nonBackgroundPixels,maxNonBackgroundPixels,cramNonzero,sprites,audioPeak,sha256,bmp,detail");
    foreach (CompatibilityResult existing in results.OrderBy(result => result.RelativeRom, StringComparer.OrdinalIgnoreCase))
    {
        writer.WriteLine(existing.ToCsv());
    }

    Console.WriteLine($"Compatibility run: {files.Length} ROM(s), {frames:N0} frame(s), {instructionsPerFrame:N0} instructions/frame");
    if (!string.IsNullOrWhiteSpace(filter))
    {
        Console.WriteLine($"Filter: {filter}");
    }

    if (resume)
    {
        Console.WriteLine($"Resume: {completed.Count:N0} existing row(s), {files.Length - completed.Count:N0} remaining");
    }

    for (int i = 0; i < files.Length; i++)
    {
        string file = files[i];
        string relative = Path.GetRelativePath(fullRomFolder, file);
        if (completed.Contains(relative))
        {
            Console.WriteLine($"{i + 1,4}/{files.Length}: skip         {relative}");
            continue;
        }

        CompatibilityResult result = RunCompatibilityCase(file, fullRomFolder, screenshotFolder, frames, instructionsPerFrame, writeScreenshots);
        results.Add(result);
        writer.WriteLine(result.ToCsv());
        Console.WriteLine($"{i + 1,4}/{files.Length}: {result.Status,-12} {result.RelativeRom} frames={result.Frames} fps={result.Fps:0.0} pc=${result.Pc:X8} pixels={result.NonBackgroundPixels:N0} maxPixels={result.MaxNonBackgroundPixels:N0}");
    }

    writer.Dispose();
    string htmlPath = Path.Combine(fullOutputFolder, "index.html");
    string summaryPath = Path.Combine(fullOutputFolder, "summary.md");
    WriteCompatibilityHtml(htmlPath, "mdSharp compatibility dashboard", results, frames, instructionsPerFrame);
    SummarizeCompatibilityReport(csvPath, summaryPath, printToConsole: false);
    Console.WriteLine($"Wrote compatibility dashboard to {htmlPath}");
}

CompatibilityResult RunCompatibilityCase(string romPath, string romRoot, string screenshotFolder, int frames, int instructionsPerFrame, bool writeScreenshot)
{
    string relative = Path.GetRelativePath(romRoot, romPath);
    string status = "ok";
    string detail = string.Empty;
    MegaDrive? machine = null;
    int completedFrames = 0;
    long audioPeak = 0;
    int maxNonBackground = 0;
    System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

    try
    {
        CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
        detail = FormatCartridgeDetail(cartridge.Diagnostics, FilenameCartridgeWarnings(romPath));
        if (cartridge.Diagnostics.HasUnsupportedHardware && !cartridge.Diagnostics.Requires32X)
        {
            status = "unsupported";
        }
        else
        {
            machine = new MegaDrive(cartridge, IsPalRegion(cartridge));
            machine.Reset();
            for (; completedFrames < frames; completedFrames++)
            {
                machine.RunFrameCycles(instructionsPerFrame);
                audioPeak = Math.Max(audioPeak, PeakAbs(machine.RenderFrameStereoAudioSamples()));
                int frameNumber = completedFrames + 1;
                if (frameNumber % 60 == 0 || frameNumber == frames)
                {
                    byte[] sample = machine.RenderFrameRgb();
                    maxNonBackground = Math.Max(maxNonBackground, CountNonBackgroundPixels(machine.Vdp, sample));
                }
            }
        }
    }
    catch (M68kException ex)
    {
        status = "m68k";
        detail = AppendDetail(detail, ex.Message);
    }
    catch (Exception ex)
    {
        status = "error";
        detail = AppendDetail(detail, ex.Message);
    }

    stopwatch.Stop();
    byte[] rgb = machine?.RenderFrameRgb() ?? new byte[Vdp.ScreenWidth * Vdp.ScreenHeight * 3];
    string hash = Convert.ToHexString(SHA256.HashData(rgb));
    int nonBackground = machine is null ? 0 : CountNonBackgroundPixels(machine.Vdp, rgb);
    maxNonBackground = Math.Max(maxNonBackground, nonBackground);
    int cramNonzero = machine is null ? 0 : CountNonzeroCram(machine.Vdp);
    int sprites = machine?.Vdp.GetDiagnostics().LikelySpriteCount ?? 0;
    string mode = machine?.Vdp.LastRenderMode ?? string.Empty;
    uint pc = machine?.MainCpu.PC ?? 0;
    string exceptions = machine is null ? string.Empty : FormatExceptions(machine.MainCpu);
    string bmpPath = string.Empty;
    if (writeScreenshot)
    {
        bmpPath = Path.Combine(screenshotFolder, $"{SanitizeFileName(relative)}.bmp");
        WriteBmp(bmpPath, Vdp.ScreenWidth, Vdp.ScreenHeight, rgb);
    }

    double fps = completedFrames <= 0 || stopwatch.Elapsed.TotalSeconds <= 0.0
        ? 0.0
        : completedFrames / stopwatch.Elapsed.TotalSeconds;

    return new CompatibilityResult(
        relative,
        status,
        completedFrames,
        stopwatch.ElapsedMilliseconds,
        fps,
        pc,
        exceptions,
        mode,
        nonBackground,
        maxNonBackground,
        cramNonzero,
        sprites,
        audioPeak,
        hash,
        bmpPath,
        detail);
}

void RunPostMenuCompatibility(string manifestPath, string romFolder, string outputFolder, int instructionsPerFrame)
{
    string fullManifestPath = Path.GetFullPath(manifestPath);
    string fullRomFolder = Path.GetFullPath(romFolder);
    string fullOutputFolder = Path.GetFullPath(outputFolder);
    string screenshotFolder = Path.Combine(fullOutputFolder, "screenshots");
    Directory.CreateDirectory(fullOutputFolder);
    Directory.CreateDirectory(screenshotFolder);

    PostMenuCompatibilityManifest manifest = JsonSerializer.Deserialize<PostMenuCompatibilityManifest>(
            File.ReadAllText(fullManifestPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? new PostMenuCompatibilityManifest();

    string[] roms = EnumerateRomFiles(fullRomFolder);
    List<PostMenuCompatibilityResult> results = [];
    string csvPath = Path.Combine(fullOutputFolder, "post-menu-compatibility.csv");
    using StreamWriter writer = new(csvPath, false, Encoding.UTF8) { AutoFlush = true };
    writer.WriteLine("id,name,rom,status,script,player2Script,frames,elapsedMs,fps,pc,exceptions,renderMode,nonBackgroundPixels,maxNonBackgroundPixels,cramNonzero,sprites,audioPeak,sha256,bmp,detail");

    Console.WriteLine($"Post-menu compatibility run: {manifest.Cases.Count:N0} case(s), {instructionsPerFrame:N0} instructions/frame");
    foreach (PostMenuCompatibilityCase testCase in manifest.Cases)
    {
        string? rom = ResolvePostMenuRom(testCase.RomMatches(), roms);
        PostMenuCompatibilityResult result = rom is null
            ? PostMenuCompatibilityResult.Missing(testCase)
            : RunPostMenuCompatibilityCase(testCase, rom, fullRomFolder, screenshotFolder, instructionsPerFrame);
        results.Add(result);
        writer.WriteLine(result.ToCsv());
        Console.WriteLine($"{result.Status,-12} {result.Id} {result.Name} frames={result.Frames:N0} pixels={result.NonBackgroundPixels:N0} maxPixels={result.MaxNonBackgroundPixels:N0} hash={ShortHash(result.Sha256)}");
    }

    writer.Dispose();
    string markdownPath = Path.Combine(fullOutputFolder, "post-menu-compatibility.md");
    string htmlPath = Path.Combine(fullOutputFolder, "index.html");
    WritePostMenuCompatibilityMarkdown(markdownPath, fullManifestPath, results, instructionsPerFrame);
    WritePostMenuCompatibilityHtml(htmlPath, results, instructionsPerFrame);
    Console.WriteLine($"Wrote post-menu compatibility report to {markdownPath}");
    Console.WriteLine($"Wrote post-menu compatibility dashboard to {htmlPath}");
}

PostMenuCompatibilityResult RunPostMenuCompatibilityCase(PostMenuCompatibilityCase testCase, string romPath, string romRoot, string screenshotFolder, int instructionsPerFrame)
{
    string status = "ok";
    string detail = string.Empty;
    MegaDrive? machine = null;
    int completedFrames = 0;
    long audioPeak = 0;
    int maxNonBackground = 0;
    System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

    try
    {
        CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
        detail = FormatCartridgeDetail(cartridge.Diagnostics, FilenameCartridgeWarnings(romPath));
        if (cartridge.Diagnostics.HasUnsupportedHardware)
        {
            status = "unsupported";
        }
        else
        {
            Func<int, ControllerInput> input = ResolveControllerInputScript(testCase.ScriptOrDefault());
            Func<int, GenesisButton>? player2Input = string.IsNullOrWhiteSpace(testCase.Player2Script)
                ? null
                : ResolveInputScript(testCase.Player2Script!);
            machine = new MegaDrive(cartridge, IsPalRegion(cartridge));
            machine.Reset();
            for (; completedFrames < Math.Max(1, testCase.Frames); completedFrames++)
            {
                ControllerInput pressed = input(completedFrames);
                machine.Bus.Controller1.Pressed = pressed.Player1;
                machine.Bus.Controller2.Pressed = player2Input?.Invoke(completedFrames) ?? pressed.Player2;
                machine.RunFrameCycles(instructionsPerFrame);
                audioPeak = Math.Max(audioPeak, PeakAbs(machine.RenderFrameStereoAudioSamples()));
                int frameNumber = completedFrames + 1;
                if (frameNumber % 60 == 0 || frameNumber == testCase.Frames)
                {
                    byte[] sample = machine.RenderFrameRgb();
                    maxNonBackground = Math.Max(maxNonBackground, CountNonBackgroundPixels(machine.Vdp, sample));
                }
            }
        }
    }
    catch (M68kException ex)
    {
        status = "m68k";
        detail = AppendDetail(detail, ex.Message);
    }
    catch (Exception ex)
    {
        status = "error";
        detail = AppendDetail(detail, ex.Message);
    }

    stopwatch.Stop();
    byte[] rgb = machine?.RenderFrameRgb() ?? new byte[Vdp.ScreenWidth * Vdp.ScreenHeight * 3];
    string hash = Convert.ToHexString(SHA256.HashData(rgb));
    int nonBackground = machine is null ? 0 : CountNonBackgroundPixels(machine.Vdp, rgb);
    maxNonBackground = Math.Max(maxNonBackground, nonBackground);
    if (status == "ok" && nonBackground <= Math.Max(64, testCase.MinimumPixels))
    {
        status = "blank";
        detail = AppendDetail(detail, "Final checkpoint frame is near blank.");
    }

    int cramNonzero = machine is null ? 0 : CountNonzeroCram(machine.Vdp);
    int sprites = machine?.Vdp.GetDiagnostics().LikelySpriteCount ?? 0;
    string mode = machine?.Vdp.LastRenderMode ?? string.Empty;
    uint pc = machine?.MainCpu.PC ?? 0;
    string exceptions = machine is null ? string.Empty : FormatExceptions(machine.MainCpu);
    string bmpPath = Path.Combine(screenshotFolder, $"{SanitizeFileName(testCase.IdOrName())}.bmp");
    WriteBmp(bmpPath, Vdp.ScreenWidth, Vdp.ScreenHeight, rgb);
    double fps = completedFrames <= 0 || stopwatch.Elapsed.TotalSeconds <= 0.0
        ? 0.0
        : completedFrames / stopwatch.Elapsed.TotalSeconds;

    return new PostMenuCompatibilityResult(
        testCase.IdOrName(),
        testCase.NameOrId(),
        Path.GetRelativePath(romRoot, romPath),
        status,
        testCase.ScriptOrDefault(),
        testCase.Player2Script ?? string.Empty,
        completedFrames,
        stopwatch.ElapsedMilliseconds,
        fps,
        pc,
        exceptions,
        mode,
        nonBackground,
        maxNonBackground,
        cramNonzero,
        sprites,
        audioPeak,
        hash,
        bmpPath,
        detail);
}

string? ResolvePostMenuRom(string[] matches, string[] roms)
{
    foreach (string match in matches)
    {
        string? rom = roms.FirstOrDefault(path => string.Equals(Path.GetFileNameWithoutExtension(path), match, StringComparison.OrdinalIgnoreCase));
        if (rom is not null)
        {
            return rom;
        }
    }

    foreach (string match in matches)
    {
        string? rom = roms.FirstOrDefault(path => Path.GetFileNameWithoutExtension(path).Contains(match, StringComparison.OrdinalIgnoreCase));
        if (rom is not null)
        {
            return rom;
        }
    }

    return null;
}

void WritePostMenuCompatibilityMarkdown(string path, string manifestPath, IReadOnlyList<PostMenuCompatibilityResult> results, int instructionsPerFrame)
{
    int ok = results.Count(result => result.Status == "ok");
    int blank = results.Count(result => result.Status == "blank");
    int failed = results.Count(result => result.Status is not "ok" and not "blank");
    StringBuilder builder = new();
    builder.AppendLine("# mdSharp Post-Menu Compatibility");
    builder.AppendLine();
    builder.AppendLine($"Manifest: `{manifestPath}`");
    builder.AppendLine();
    builder.AppendLine($"- Cases: {results.Count:N0}");
    builder.AppendLine($"- OK: {ok:N0}");
    builder.AppendLine($"- Near blank: {blank:N0}");
    builder.AppendLine($"- Failed/missing/unsupported: {failed:N0}");
    builder.AppendLine($"- Instructions per frame: {instructionsPerFrame:N0}");
    builder.AppendLine();
    builder.AppendLine("| ID | Name | ROM | Status | Script | Frames | PC | Pixels | Max pixels | Sprites | Audio | Hash | Screenshot | Detail |");
    builder.AppendLine("| --- | --- | --- | --- | --- | ---: | --- | ---: | ---: | ---: | ---: | --- | --- | --- |");
    foreach (PostMenuCompatibilityResult result in results)
    {
        string bmp = string.IsNullOrWhiteSpace(result.BmpPath) ? string.Empty : Path.GetRelativePath(Path.GetDirectoryName(path)!, result.BmpPath).Replace('\\', '/');
        string image = string.IsNullOrWhiteSpace(bmp) ? string.Empty : $"[bmp]({EscapeMarkdown(bmp)})";
        builder.AppendLine($"| `{EscapeMarkdown(result.Id)}` | {EscapeMarkdown(result.Name)} | {EscapeMarkdown(result.RelativeRom)} | `{EscapeMarkdown(result.Status)}` | `{EscapeMarkdown(result.Script)}` | {result.Frames:N0} | `${result.Pc:X8}` | {result.NonBackgroundPixels:N0} | {result.MaxNonBackgroundPixels:N0} | {result.Sprites:N0} | {result.AudioPeak:N0} | `{ShortHash(result.Sha256)}` | {image} | {EscapeMarkdown(result.Detail)} |");
    }

    File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
}

void WritePostMenuCompatibilityHtml(string path, IReadOnlyList<PostMenuCompatibilityResult> results, int instructionsPerFrame)
{
    int ok = results.Count(result => result.Status == "ok");
    int failed = results.Count - ok;
    StringBuilder html = new();
    html.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>mdSharp post-menu compatibility</title>");
    html.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:24px;background:#f6f7f9;color:#15171a}table{border-collapse:collapse;width:100%;background:white}th,td{padding:6px 8px;border-bottom:1px solid #ddd;font-size:12px;text-align:left}th{position:sticky;top:0;background:#20242b;color:white}.ok{color:#127a34;font-weight:600}.bad{color:#b42318;font-weight:600}.blank{color:#9a6700;font-weight:600}img{image-rendering:pixelated;width:160px}</style></head><body>");
    html.AppendLine($"<h1>mdSharp post-menu compatibility</h1><p>{results.Count:N0} cases, {ok:N0} ok, {failed:N0} flagged, {instructionsPerFrame:N0} instructions/frame.</p>");
    html.AppendLine("<table><thead><tr><th>ID</th><th>Name</th><th>ROM</th><th>Status</th><th>Script</th><th>Frames</th><th>FPS</th><th>PC</th><th>Mode</th><th>Pixels</th><th>Max Pixels</th><th>Sprites</th><th>Audio</th><th>Hash</th><th>Screenshot</th><th>Detail</th></tr></thead><tbody>");
    foreach (PostMenuCompatibilityResult result in results)
    {
        string css = result.Status == "ok" ? "ok" : result.Status == "blank" ? "blank" : "bad";
        string screenshot = string.IsNullOrWhiteSpace(result.BmpPath)
            ? string.Empty
            : $"<a href=\"{EscapeHtml(Path.GetRelativePath(Path.GetDirectoryName(path)!, result.BmpPath).Replace('\\', '/'))}\"><img src=\"{EscapeHtml(Path.GetRelativePath(Path.GetDirectoryName(path)!, result.BmpPath).Replace('\\', '/'))}\"></a>";
        html.AppendLine($"<tr><td>{EscapeHtml(result.Id)}</td><td>{EscapeHtml(result.Name)}</td><td>{EscapeHtml(result.RelativeRom)}</td><td class=\"{css}\">{EscapeHtml(result.Status)}</td><td>{EscapeHtml(result.Script)}</td><td>{result.Frames:N0}</td><td>{result.Fps:0.0}</td><td>${result.Pc:X8}</td><td>{EscapeHtml(result.RenderMode)}</td><td>{result.NonBackgroundPixels:N0}</td><td>{result.MaxNonBackgroundPixels:N0}</td><td>{result.Sprites:N0}</td><td>{result.AudioPeak:N0}</td><td><code>{EscapeHtml(ShortHash(result.Sha256))}</code></td><td>{screenshot}</td><td>{EscapeHtml(result.Detail)}</td></tr>");
    }

    html.AppendLine("</tbody></table></body></html>");
    File.WriteAllText(path, html.ToString(), Encoding.UTF8);
}

void RunPerfSuite(string romFolder, string outputFolder, int frames, int instructionsPerFrame, bool frameProfile, string? filter = null)
{
    string fullRomFolder = Path.GetFullPath(romFolder);
    string fullOutputFolder = Path.GetFullPath(outputFolder);
    Directory.CreateDirectory(fullOutputFolder);

    string[] allFiles = EnumerateRomFiles(fullRomFolder);
    string[] files = SelectPerfSuiteRoms(allFiles, filter);
    string csvPath = Path.Combine(fullOutputFolder, "perf-suite.csv");
    List<PerfSuiteResult> results = [];

    using StreamWriter writer = new(csvPath, false, Encoding.UTF8) { AutoFlush = true };
    writer.WriteLine("rom,status,frames,instructionsPerFrame,totalMs,cpuMs,renderMs,audioMs,m68kMs,z80Ms,vdpStepMs,ymTimerMs,renderSnapshotMs,renderPaletteMs,renderScrollMs,renderSpriteGatherMs,renderPlaneBMs,renderPlaneAWindowMs,renderSpriteRenderMs,renderCompositingMs,renderBorderMs,renderDisplayFillMs,renderDirectColorMs,fps,cpuMsPerFrame,renderMsPerFrame,audioMsPerFrame,allocatedBytes,allocatedBytesPerFrame,cpuAllocatedBytes,renderAllocatedBytes,audioAllocatedBytes,m68kAllocatedBytes,z80AllocatedBytes,vdpStepAllocatedBytes,ymTimerAllocatedBytes,pc,renderMode,sprites,nonBackgroundPixels,audioPeak,ym0,ym1,ym2,ym3,ym4,ym5,sha256,detail");

    Console.WriteLine($"Perf suite: {files.Length} ROM(s), {frames:N0} measured frame(s), {instructionsPerFrame:N0} instructions/frame{(string.IsNullOrWhiteSpace(filter) ? string.Empty : $", filter='{filter}'")}");
    for (int i = 0; i < files.Length; i++)
    {
        PerfSuiteResult result = RunPerfSuiteCase(files[i], fullRomFolder, frames, instructionsPerFrame, frameProfile);
        results.Add(result);
        writer.WriteLine(result.ToCsv());
        Console.WriteLine($"{i + 1,3}/{files.Length}: {result.Status,-8} {result.RelativeRom} fps={result.Fps:0.0} cpu={result.CpuMsPerFrame:0.###}ms render={result.RenderMsPerFrame:0.###}ms audio={result.AudioMsPerFrame:0.###}ms alloc(cpu/render/audio)={result.CpuAllocatedBytesPerFrame:0}/{result.RenderAllocatedBytesPerFrame:0}/{result.AudioAllocatedBytesPerFrame:0}B");
    }

    string summaryPath = Path.Combine(fullOutputFolder, "perf-suite.md");
    WritePerfSuiteSummary(summaryPath, results, frames, instructionsPerFrame);
    Console.WriteLine($"Wrote perf suite report to {csvPath}");
}

string[] SelectPerfSuiteRoms(string[] files, string? filter = null)
{
    if (!string.IsNullOrWhiteSpace(filter))
    {
        string[] filtered = files
            .Where(file => Path.GetFileNameWithoutExtension(file).Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return filtered.Length > 0 ? filtered : files.Take(0).ToArray();
    }

    string[] preferredNames =
    [
        "sonic the hedgehog (usa)",
        "sonic the hedgehog 2 (usa)",
        "sonic the hedgehog 3 (usa)",
        "streets of rage (usa)",
        "castlevania - bloodlines (usa)",
        "sonic spinball (usa)",
        "gunstar heroes (usa)",
        "shinobi iii",
        "zool - ninja",
        "zero tolerance",
        "zombies ate my neighbors",
        "street racer",
        "zero wing",
        "ecco - the tides of time",
        "b.o.b.",
        "ys iii",
        "disney's aladdin - final cut",
        "world trophy soccer"
    ];

    List<string> selected = [];
    foreach (string preferred in preferredNames)
    {
        string? match = files.FirstOrDefault(file => Path.GetFileNameWithoutExtension(file).Contains(preferred, StringComparison.OrdinalIgnoreCase));
        if (match is not null && !selected.Contains(match, StringComparer.OrdinalIgnoreCase))
        {
            selected.Add(match);
        }
    }

    if (selected.Count == 0)
    {
        selected.AddRange(files.Take(8));
    }

    return selected.ToArray();
}

PerfSuiteResult RunPerfSuiteCase(string romPath, string romRoot, int frames, int instructionsPerFrame, bool frameProfile)
{
    string relative = Path.GetRelativePath(romRoot, romPath);
    string status = "ok";
    string detail = string.Empty;
    MegaDrive? machine = null;
    int completedFrames = 0;
    long cpuTicks = 0;
    long renderTicks = 0;
    long audioTicks = 0;
    long audioPeak = 0;
    int audioSamples = 0;
    byte[] framebuffer = new byte[Vdp.ScreenWidth * Vdp.ScreenHeight * 3];
    short[] audioBuffer = new short[4096];
    long[] ymChannelEnergy = new long[6];
    long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    long cpuAllocatedBytes = 0;
    long renderAllocatedBytes = 0;
    long audioAllocatedBytes = 0;
    long m68kTicks = 0;
    long z80Ticks = 0;
    long vdpStepTicks = 0;
    long ymTimerTicks = 0;
    long renderSnapshotTicks = 0;
    long renderPaletteTicks = 0;
    long renderScrollTicks = 0;
    long renderSpriteGatherTicks = 0;
    long renderPlaneBTicks = 0;
    long renderPlaneAWindowTicks = 0;
    long renderSpriteRenderTicks = 0;
    long renderCompositingTicks = 0;
    long renderBorderTicks = 0;
    long renderDisplayFillTicks = 0;
    long renderDirectColorTicks = 0;
    long m68kAllocatedBytes = 0;
    long z80AllocatedBytes = 0;
    long vdpStepAllocatedBytes = 0;
    long ymTimerAllocatedBytes = 0;
    long totalStart = System.Diagnostics.Stopwatch.GetTimestamp();

    try
    {
        CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
        machine = new MegaDrive(cartridge, IsPalRegion(cartridge));
        machine.CollectFramePerformance = frameProfile;
        machine.Vdp.CollectRenderPerformance = frameProfile;
        machine.Reset();

        for (; completedFrames < frames; completedFrames++)
        {
            long stageAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            machine.RunFrameCycles(instructionsPerFrame);
            cpuTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
            cpuAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - stageAllocatedBefore;
            if (frameProfile)
            {
                MegaDrive.FramePerformanceCounters counters = machine.LastFramePerformance;
                m68kTicks += counters.M68kTicks;
                z80Ticks += counters.Z80Ticks;
                vdpStepTicks += counters.VdpTicks;
                ymTimerTicks += counters.YmTimerTicks;
                m68kAllocatedBytes += counters.M68kAllocatedBytes;
                z80AllocatedBytes += counters.Z80AllocatedBytes;
                vdpStepAllocatedBytes += counters.VdpAllocatedBytes;
                ymTimerAllocatedBytes += counters.YmTimerAllocatedBytes;
            }

            stageAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            start = System.Diagnostics.Stopwatch.GetTimestamp();
            machine.RenderFrameRgbInto(framebuffer);
            renderTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
            renderAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - stageAllocatedBefore;
            if (frameProfile)
            {
                Vdp.RenderPerformanceCounters counters = machine.Vdp.LastRenderPerformance;
                renderSnapshotTicks += counters.SnapshotTicks;
                renderPaletteTicks += counters.PaletteTicks;
                renderScrollTicks += counters.ScrollTicks;
                renderSpriteGatherTicks += counters.SpriteGatherTicks;
                renderPlaneBTicks += counters.PlaneBTicks;
                renderPlaneAWindowTicks += counters.PlaneAWindowTicks;
                renderSpriteRenderTicks += counters.SpriteRenderTicks;
                renderCompositingTicks += counters.CompositingTicks;
                renderBorderTicks += counters.BorderTicks;
                renderDisplayFillTicks += counters.DisplayFillTicks;
                renderDirectColorTicks += counters.DirectColorTicks;
            }

            stageAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            start = System.Diagnostics.Stopwatch.GetTimestamp();
            int written = machine.RenderFrameStereoAudioSamplesInto(audioBuffer, ymChannelEnergy: ymChannelEnergy);
            audioTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
            audioAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - stageAllocatedBefore;
            audioSamples = Math.Max(audioSamples, written);
            audioPeak = Math.Max(audioPeak, PeakAbsSpan(audioBuffer.AsSpan(0, written)));
        }
    }
    catch (M68kException ex)
    {
        status = "m68k";
        detail = ex.Message;
    }
    catch (Exception ex)
    {
        status = "error";
        detail = ex.Message;
    }

    long totalTicks = System.Diagnostics.Stopwatch.GetTimestamp() - totalStart;
    long allocatedBytes = Math.Max(0, GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
    string hash = Convert.ToHexString(SHA256.HashData(framebuffer));
    int nonBackground = machine is null ? 0 : CountNonBackgroundPixels(machine.Vdp, framebuffer);
    int sprites = machine?.Vdp.GetDiagnostics().LikelySpriteCount ?? 0;
    uint pc = machine?.MainCpu.PC ?? 0;
    string mode = machine?.Vdp.LastRenderMode ?? string.Empty;

    return new PerfSuiteResult(
        relative,
        status,
        completedFrames,
        instructionsPerFrame,
        TicksToMs(totalTicks),
        TicksToMs(cpuTicks),
        TicksToMs(renderTicks),
        TicksToMs(audioTicks),
        TicksToMs(m68kTicks),
        TicksToMs(z80Ticks),
        TicksToMs(vdpStepTicks),
        TicksToMs(ymTimerTicks),
        TicksToMs(renderSnapshotTicks),
        TicksToMs(renderPaletteTicks),
        TicksToMs(renderScrollTicks),
        TicksToMs(renderSpriteGatherTicks),
        TicksToMs(renderPlaneBTicks),
        TicksToMs(renderPlaneAWindowTicks),
        TicksToMs(renderSpriteRenderTicks),
        TicksToMs(renderCompositingTicks),
        TicksToMs(renderBorderTicks),
        TicksToMs(renderDisplayFillTicks),
        TicksToMs(renderDirectColorTicks),
        completedFrames > 0 && totalTicks > 0 ? completedFrames / (TicksToMs(totalTicks) / 1000.0) : 0.0,
        allocatedBytes,
        cpuAllocatedBytes,
        renderAllocatedBytes,
        audioAllocatedBytes,
        m68kAllocatedBytes,
        z80AllocatedBytes,
        vdpStepAllocatedBytes,
        ymTimerAllocatedBytes,
        pc,
        mode,
        sprites,
        nonBackground,
        audioPeak,
        ymChannelEnergy.ToArray(),
        hash,
        detail);
}

void WritePerfSuiteSummary(string path, IReadOnlyList<PerfSuiteResult> results, int frames, int instructionsPerFrame)
{
    StringBuilder builder = new();
    builder.AppendLine("# mdSharp Perf Suite");
    builder.AppendLine();
    builder.AppendLine($"Frames: {frames:N0}");
    builder.AppendLine($"Instructions/frame: {instructionsPerFrame:N0}");
    builder.AppendLine();
    builder.AppendLine("| ROM | Status | FPS | CPU ms/frame | M68k ms/frame | Z80 ms/frame | VDP step ms/frame | Render ms/frame | Plane B | Plane A/window | Sprites | Compose | Render setup | Audio ms/frame | CPU alloc/frame | Render alloc/frame | Audio alloc/frame |");
    builder.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
    foreach (PerfSuiteResult result in results)
    {
        builder.AppendLine($"| {EscapeMarkdown(result.RelativeRom)} | {result.Status} | {result.Fps:0.0} | {result.CpuMsPerFrame:0.###} | {result.M68kMsPerFrame:0.###} | {result.Z80MsPerFrame:0.###} | {result.VdpStepMsPerFrame:0.###} | {result.RenderMsPerFrame:0.###} | {result.RenderPlaneBMsPerFrame:0.###} | {result.RenderPlaneAWindowMsPerFrame:0.###} | {result.RenderSpriteRenderMsPerFrame:0.###} | {result.RenderCompositingMsPerFrame:0.###} | {result.RenderSetupMsPerFrame:0.###} | {result.AudioMsPerFrame:0.###} | {result.CpuAllocatedBytesPerFrame:0} | {result.RenderAllocatedBytesPerFrame:0} | {result.AudioAllocatedBytesPerFrame:0} |");
    }

    File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
}

static double TicksToMs(long ticks)
{
    return ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
}

void ProfileM68kAllocations(string romPath, int frames, int instructionsPerFrame, int top)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    MegaDrive machine = new(cartridge, IsPalRegion(cartridge));
    machine.Reset();
    machine.MainCpu.ClearAllocationProfile();
    machine.MainCpu.AllocationProfilingEnabled = true;

    for (int frame = 0; frame < frames; frame++)
    {
        machine.RunFrameCycles(instructionsPerFrame);
    }

    machine.MainCpu.AllocationProfilingEnabled = false;
    Console.WriteLine($"Top 68k allocation opcodes after {frames:N0} frame(s):");
    Console.WriteLine("opcode,samples,allocatedBytes,bytesPerSample");
    foreach (M68kCpu.M68kOpcodeAllocation row in machine.MainCpu.GetAllocationProfile()
        .OrderByDescending(row => row.AllocatedBytes)
        .ThenBy(row => row.Opcode)
        .Take(top))
    {
        double perSample = row.Samples <= 0 ? 0.0 : row.AllocatedBytes / (double)row.Samples;
        Console.WriteLine($"${row.Opcode:X4},{row.Samples},{row.AllocatedBytes},{perSample:0.###}");
    }
}

void RunVisualCheckpoints(string romFolder, string outputFolder, int instructionsPerFrame, bool updateBaseline)
{
    string fullRomFolder = Path.GetFullPath(romFolder);
    string fullOutputFolder = Path.GetFullPath(outputFolder);
    string screenshotFolder = Path.Combine(fullOutputFolder, "screenshots");
    string diffFolder = Path.Combine(fullOutputFolder, "diffs");
    string currentPath = Path.Combine(fullOutputFolder, "visual-checkpoints-current.csv");
    string baselinePath = Path.Combine(fullOutputFolder, "visual-checkpoints-baseline.csv");
    string summaryPath = Path.Combine(fullOutputFolder, "visual-checkpoints.md");
    Directory.CreateDirectory(fullOutputFolder);
    Directory.CreateDirectory(screenshotFolder);
    Directory.CreateDirectory(diffFolder);
    foreach (string diff in Directory.EnumerateFiles(diffFolder, "*.bmp"))
    {
        File.Delete(diff);
    }

    string[] roms = EnumerateRomFiles(fullRomFolder);
    VisualCheckpointSpec[] specs = BuildVisualCheckpointSpecs();
    Dictionary<string, VisualCheckpointBaseline> baseline = File.Exists(baselinePath)
        ? LoadVisualCheckpointBaseline(baselinePath)
        : new Dictionary<string, VisualCheckpointBaseline>(StringComparer.OrdinalIgnoreCase);

    List<VisualCheckpointResult> results = [];
    using StreamWriter writer = new(currentPath, false, Encoding.UTF8) { AutoFlush = true };
    writer.WriteLine("id,name,rom,status,frames,instructionsPerFrame,pc,renderMode,nonBackgroundPixels,sprites,sha256,bmp,detail");

    foreach (VisualCheckpointSpec spec in specs)
    {
        string? rom = ResolveCheckpointRom(spec, roms);
        VisualCheckpointResult result = rom is null
            ? VisualCheckpointResult.Missing(spec)
            : RunVisualCheckpointCase(spec, rom, fullRomFolder, screenshotFolder, diffFolder, instructionsPerFrame, baseline);
        results.Add(result);
        writer.WriteLine(result.ToCsv());
        string compare = result.ExpectedSha256 is null ? "baseline:new" : result.MatchesBaseline ? "match" : "changed";
        Console.WriteLine($"{result.Status,-8} {compare,-12} {result.Id} {result.Name} hash={ShortHash(result.Sha256)}");
    }

    writer.Dispose();
    WriteVisualCheckpointSummary(summaryPath, results, currentPath, baselinePath, instructionsPerFrame);
    if (updateBaseline || !File.Exists(baselinePath))
    {
        File.Copy(currentPath, baselinePath, overwrite: true);
        Console.WriteLine($"Updated visual checkpoint baseline at {baselinePath}");
    }

    int changed = results.Count(result => result.Status == "ok" && result.ExpectedSha256 is not null && !result.MatchesBaseline);
    int missing = results.Count(result => result.Status == "missing");
    Console.WriteLine($"Visual checkpoints complete: {results.Count:N0} checkpoint(s), {changed:N0} changed, {missing:N0} missing.");
    Console.WriteLine($"Wrote report to {summaryPath}");
}

VisualCheckpointSpec[] BuildVisualCheckpointSpecs()
{
    return
    [
        new VisualCheckpointSpec("sonic1-title", "Sonic 1 title", ["sonic the hedgehog (usa)", "sonic"], 900, NoInput),
        new VisualCheckpointSpec("sonic1-start", "Sonic 1 post-start", ["sonic the hedgehog (usa)", "sonic"], 1_800, Sonic1TitleStartPulse),
        new VisualCheckpointSpec("sonic2-title", "Sonic 2 title", ["sonic the hedgehog 2 (usa) (rev-b)", "sonic the hedgehog 2"], 900, NoInput),
        new VisualCheckpointSpec("sonic2-idle-split-early", "Sonic 2 split-view early raster", ["sonic the hedgehog 2 (usa) (rev-b)", "sonic the hedgehog 2"], 1_600, NoInput),
        new VisualCheckpointSpec("sonic2-idle-split", "Sonic 2 idle split-view demo", ["sonic the hedgehog 2 (usa) (rev-b)", "sonic the hedgehog 2"], 2_000, NoInput),
        new VisualCheckpointSpec("sonic2-idle-split-loop", "Sonic 2 split-view loop raster", ["sonic the hedgehog 2 (usa) (rev-b)", "sonic the hedgehog 2"], 2_800, NoInput),
        new VisualCheckpointSpec("sonic2-idle-split-load", "Sonic 2 split-view load transition", ["sonic the hedgehog 2 (usa) (rev-b)", "sonic the hedgehog 2"], 3_200, NoInput),
        new VisualCheckpointSpec("sonic2-idle-split-late", "Sonic 2 split-view late demo", ["sonic the hedgehog 2 (usa) (rev-b)", "sonic the hedgehog 2"], 3_800, NoInput),
        new VisualCheckpointSpec("sonic3-title", "Sonic 3 title", ["sonic the hedgehog 3"], 1_800, NoInput),
        new VisualCheckpointSpec("sonic3-post-start", "Sonic 3 post-start", ["sonic the hedgehog 3"], 2_700, Sonic3TitleStartPulse),
        new VisualCheckpointSpec("sonic3-special-preview", "Sonic 3 later raster preview", ["sonic the hedgehog 3"], 3_600, Sonic3TitleStartPulse),
        new VisualCheckpointSpec("sonic3-and-knuckles-title", "Sonic 3 & Knuckles title", ["sonic & knuckles (sonic the hedgehog 3 & knuckles)"], 1_800, NoInput),
        new VisualCheckpointSpec("streets-title", "Streets of Rage title or intro", ["streets of rage"], 900, NoInput),
        new VisualCheckpointSpec("streets-post-start", "Streets of Rage post-start", ["streets of rage"], 1_500, StartPulse),
        new VisualCheckpointSpec("streets-hud", "Streets of Rage HUD gameplay", ["streets of rage"], 2_700, StreetsOfRageStartAndSelect),
        new VisualCheckpointSpec("streets2-title", "Streets of Rage 2 title", ["streets of rage 2"], 1_200, NoInput),
        new VisualCheckpointSpec("streets3-title", "Streets of Rage 3 title", ["streets of rage 3"], 1_200, NoInput),
        new VisualCheckpointSpec("bloodlines-title", "Bloodlines title", ["castlevania - bloodlines"], 360, NoInput),
        new VisualCheckpointSpec("bloodlines-menu", "Bloodlines later title/menu", ["castlevania - bloodlines"], 1_500, NoInput),
        new VisualCheckpointSpec("bloodlines-cutscene", "Bloodlines cutscene color check", ["castlevania - bloodlines"], 2_400, NoInput),
        new VisualCheckpointSpec("aladdin-genie-logo", "Aladdin Genie logo animation", ["disney's aladdin", "aladdin"], 600, NoInput),
        new VisualCheckpointSpec("aladdin-title", "Aladdin title", ["disney's aladdin", "aladdin"], 900, NoInput),
        new VisualCheckpointSpec("aladdin-gameplay", "Aladdin early gameplay", ["disney's aladdin", "aladdin"], 1_800, RepeatedStartPulse),
        new VisualCheckpointSpec("toy-story-travelers-tales", "Toy Story Traveler's Tales logo", ["disney's toy story", "toy story"], 850, NoInput),
        new VisualCheckpointSpec("toy-story-psygnosis", "Toy Story Psygnosis logo", ["disney's toy story", "toy story"], 1_200, NoInput),
        new VisualCheckpointSpec("toy-story-title", "Toy Story title sequence", ["disney's toy story", "toy story"], 2_100, NoInput),
        new VisualCheckpointSpec("toy-story-bedroom", "Toy Story bedroom sprites", ["disney's toy story", "toy story"], 3_600, RepeatedStartPulse),
        new VisualCheckpointSpec("zero-wing-title", "Zero Wing title", ["zero wing"], 900, NoInput),
        new VisualCheckpointSpec("zero-wing-idle-dialog", "Zero Wing idle dialogue", ["zero wing"], 1_200, NoInput),
        new VisualCheckpointSpec("zero-wing-idle-gameplay", "Zero Wing idle gameplay sprites", ["zero wing"], 7_200, NoInput),
        new VisualCheckpointSpec("virtua-racing-gameplay", "Virtua Racing SVP gameplay", ["virtua racing"], 4_800, VirtuaRacingStartAndDrive, NoInput),
        new VisualCheckpointSpec("chaotix-title", "Knuckles Chaotix 32X title", ["chaotix"], 900, NoInput),
        new VisualCheckpointSpec("sonic-spinball-title", "Sonic Spinball title", ["sonic spinball"], 600, NoInput),
        new VisualCheckpointSpec("gunstar-title", "Gunstar Heroes post-start title", ["gunstar heroes"], 2_400, RepeatedStartPulse),
        new VisualCheckpointSpec("vectorman-title", "Vectorman title", ["vectorman (usa)"], 1_200, NoInput),
        new VisualCheckpointSpec("ecco-title", "Ecco title", ["ecco the dolphin"], 1_200, NoInput),
        new VisualCheckpointSpec("thunder-force-iii-title", "Thunder Force III gameplay intro", ["thunder force iii"], 2_400, NoInput),
        new VisualCheckpointSpec("bubsy-late-blank", "Bubsy late blank suspect", ["bubsy in claws encounters"], 1_200, NoInput),
        new VisualCheckpointSpec("chase-hq-gameplay", "Chase HQ II gameplay", ["chase h.q. ii"], 6_000, RepeatedStartPulse),
        new VisualCheckpointSpec("evander-late-blank", "Evander Holyfield late blank suspect", ["evander holyfield"], 1_200, NoInput),
        new VisualCheckpointSpec("fifa98-late-blank", "FIFA 98 late blank suspect", ["fifa 98"], 1_200, NoInput),
        new VisualCheckpointSpec("hurricanes-late-blank", "Hurricanes late blank suspect", ["hurricanes"], 1_200, NoInput),
        new VisualCheckpointSpec("jewel-master-late-blank", "Jewel Master late blank suspect", ["jewel master"], 1_200, NoInput),
        new VisualCheckpointSpec("lemmings2-title", "Lemmings 2 title", ["lemmings 2"], 6_200, NoInput),
        new VisualCheckpointSpec("nba-showdown-gameplay", "NBA Showdown '94 slow boot gameplay", ["nba showdown"], 3_001, NoInput),
        new VisualCheckpointSpec("mercs-late-blank", "Mercs late blank suspect", ["mercs"], 1_200, NoInput),
        new VisualCheckpointSpec("raiden-trad-late-blank", "Raiden Trad late blank suspect", ["raiden trad"], 1_200, NoInput),
        new VisualCheckpointSpec("saint-sword-title", "Saint Sword title", ["saint sword"], 6_000, NoInput),
        new VisualCheckpointSpec("wings-of-wor-late-blank", "Wings of Wor late blank suspect", ["wings of wor"], 1_200, NoInput),
        new VisualCheckpointSpec("super-hydlide-gameplay", "Super Hydlide input-gated gameplay", ["super hydlide"], 9_000, StartPulse),
        new VisualCheckpointSpec("zany-golf-instructions", "Zany Golf input-gated instructions", ["zany golf"], 9_000, RepeatedStartPulse),
    ];
}

GenesisButton NoInput(int frame)
{
    return GenesisButton.None;
}

GenesisButton StartPulse(int frame)
{
    return frame is >= 120 and < 150 ? GenesisButton.Start : GenesisButton.None;
}

GenesisButton RepeatedStartPulse(int frame)
{
    return frame >= 300 && frame % 180 < 18 ? GenesisButton.Start : GenesisButton.None;
}

GenesisButton VirtuaRacingStartAndDrive(int frame)
{
    GenesisButton buttons = GenesisButton.None;
    if (frame is >= 300 and < 330 or >= 900 and < 930 or >= 1_500 and < 1_530)
    {
        buttons |= GenesisButton.Start;
    }

    if (frame is >= 2_400 and < 4_200)
    {
        buttons |= GenesisButton.C;
    }

    if (frame >= 4_200)
    {
        buttons |= GenesisButton.B;
    }

    if (frame >= 3_200 && frame % 240 < 120)
    {
        buttons |= GenesisButton.Right;
    }

    return buttons;
}

GenesisButton Sonic1TitleStartPulse(int frame)
{
    return frame is >= 900 and < 930 ? GenesisButton.Start : GenesisButton.None;
}

GenesisButton Sonic3TitleStartPulse(int frame)
{
    return frame is >= 1_800 and < 1_830 ? GenesisButton.Start : GenesisButton.None;
}

GenesisButton ChaotixTitleStartPulse(int frame)
{
    return frame is >= 1_800 and < 1_830 ? GenesisButton.Start : GenesisButton.None;
}

GenesisButton ChaotixStartAndPlay(int frame)
{
    GenesisButton buttons = GenesisButton.None;
    if (frame is >= 300 and < 330)
    {
        buttons |= GenesisButton.Start;
    }

    if (frame >= 1_800)
    {
        buttons |= GenesisButton.Right;
    }

    if (frame is >= 2_100 and < 2_130 or >= 2_700 and < 2_730)
    {
        buttons |= GenesisButton.C;
    }

    return buttons;
}

GenesisButton StreetsOfRageStartAndSelect(int frame)
{
    GenesisButton buttons = GenesisButton.None;
    if (frame is >= 900 and < 930)
    {
        buttons |= GenesisButton.Start;
    }

    if (frame is >= 1_500 and < 1_530)
    {
        buttons |= GenesisButton.Start;
    }

    return buttons;
}

string? ResolveCheckpointRom(VisualCheckpointSpec spec, string[] roms)
{
    foreach (string match in spec.RomNameContains)
    {
        string? rom = roms.FirstOrDefault(path => string.Equals(Path.GetFileNameWithoutExtension(path), match, StringComparison.OrdinalIgnoreCase));
        if (rom is not null)
        {
            return rom;
        }
    }

    foreach (string match in spec.RomNameContains)
    {
        string? rom = roms.FirstOrDefault(path => Path.GetFileNameWithoutExtension(path).Contains(match, StringComparison.OrdinalIgnoreCase));
        if (rom is not null)
        {
            return rom;
        }
    }

    return null;
}

VisualCheckpointResult RunVisualCheckpointCase(
    VisualCheckpointSpec spec,
    string romPath,
    string romRoot,
    string screenshotFolder,
    string diffFolder,
    int instructionsPerFrame,
    IReadOnlyDictionary<string, VisualCheckpointBaseline> baseline)
{
    string status = "ok";
    string detail = string.Empty;
    int completedFrames = 0;
    MegaDrive? machine = null;

    try
    {
        CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
        machine = new MegaDrive(cartridge, IsPalRegion(cartridge));
        machine.Reset();
        for (; completedFrames < spec.Frames; completedFrames++)
        {
            GenesisButton buttons = spec.Input(completedFrames);
            machine.Bus.Controller1.Pressed = buttons;
            machine.Bus.Controller2.Pressed = spec.Player2Input?.Invoke(completedFrames) ?? buttons;
            machine.RunFrameCycles(instructionsPerFrame);
        }
    }
    catch (M68kException ex)
    {
        status = "m68k";
        detail = ex.Message;
    }
    catch (Exception ex)
    {
        status = "error";
        detail = ex.Message;
    }

    byte[] rgb = machine?.RenderFrameRgb() ?? new byte[Vdp.ScreenWidth * Vdp.ScreenHeight * 3];
    string sha = Convert.ToHexString(SHA256.HashData(rgb));
    string relativeRom = Path.GetRelativePath(romRoot, romPath);
    string bmpPath = Path.Combine(screenshotFolder, $"{SanitizeFileName(spec.Id)}.bmp");
    int baselineWidth = 0;
    int baselineHeight = 0;
    byte[] baselineRgb = [];
    baseline.TryGetValue(spec.Id, out VisualCheckpointBaseline? expected);
    bool hasBaselineImage = !string.IsNullOrWhiteSpace(expected?.BmpPath)
        && TryReadBmpRgb(expected.BmpPath, out baselineWidth, out baselineHeight, out baselineRgb);
    WriteBmp(bmpPath, Vdp.ScreenWidth, Vdp.ScreenHeight, rgb);
    int nonBackground = machine is null ? 0 : CountNonBackgroundPixels(machine.Vdp, rgb);
    int sprites = machine?.Vdp.GetDiagnostics().LikelySpriteCount ?? 0;
    uint pc = machine?.MainCpu.PC ?? 0;
    string renderMode = machine?.Vdp.LastRenderMode ?? string.Empty;
    if (status == "ok" && nonBackground <= 64)
    {
        status = "blank";
        detail = "Checkpoint frame is near blank.";
    }

    bool matchesBaseline = string.IsNullOrWhiteSpace(expected?.Sha256) || string.Equals(expected.Sha256, sha, StringComparison.OrdinalIgnoreCase);
    string diffPath = string.Empty;
    if (status == "ok" && !matchesBaseline && hasBaselineImage && baselineWidth == Vdp.ScreenWidth && baselineHeight == Vdp.ScreenHeight)
    {
        diffPath = Path.Combine(diffFolder, $"{SanitizeFileName(spec.Id)}-diff.bmp");
        WriteVisualDiffBmpFromRgb(baselineRgb, rgb, Vdp.ScreenWidth, Vdp.ScreenHeight, diffPath);
    }

    return new VisualCheckpointResult(
        spec.Id,
        spec.Name,
        relativeRom,
        status,
        completedFrames,
        instructionsPerFrame,
        pc,
        renderMode,
        nonBackground,
        sprites,
        sha,
        expected?.Sha256,
        matchesBaseline,
        bmpPath,
        diffPath,
        detail);
}

Dictionary<string, VisualCheckpointBaseline> LoadVisualCheckpointBaseline(string path)
{
    Dictionary<string, VisualCheckpointBaseline> baseline = new(StringComparer.OrdinalIgnoreCase);
    foreach (string line in File.ReadLines(path).Skip(1))
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        string[] parts = SplitCsvLine(line);
        if (parts.Length >= 12)
        {
            baseline[parts[0]] = new VisualCheckpointBaseline(parts[0], parts[10], parts[11]);
        }
    }

    return baseline;
}

void WriteVisualCheckpointSummary(string path, IReadOnlyList<VisualCheckpointResult> results, string currentPath, string baselinePath, int instructionsPerFrame)
{
    int ok = results.Count(result => result.Status == "ok");
    int changed = results.Count(result => result.Status == "ok" && result.ExpectedSha256 is not null && !result.MatchesBaseline);
    int missing = results.Count(result => result.Status == "missing");
    StringBuilder builder = new();
    builder.AppendLine("# mdSharp Visual Checkpoints");
    builder.AppendLine();
    builder.AppendLine($"Current: `{Path.GetFullPath(currentPath)}`");
    builder.AppendLine($"Baseline: `{Path.GetFullPath(baselinePath)}`");
    builder.AppendLine($"Instructions/frame: {instructionsPerFrame:N0}");
    builder.AppendLine();
    builder.AppendLine($"- Checkpoints: {results.Count:N0}");
    builder.AppendLine($"- OK: {ok:N0}");
    builder.AppendLine($"- Changed: {changed:N0}");
    builder.AppendLine($"- Missing ROMs: {missing:N0}");
    builder.AppendLine();
    builder.AppendLine("| ID | Name | Status | Compare | Frames | PC | Pixels | Sprites | Hash | Screenshot | Diff | Detail |");
    builder.AppendLine("| --- | --- | --- | --- | ---: | --- | ---: | ---: | --- | --- | --- | --- |");
    foreach (VisualCheckpointResult result in results)
    {
        string compare = result.ExpectedSha256 is null ? "new" : result.MatchesBaseline ? "match" : "changed";
        string bmp = string.IsNullOrWhiteSpace(result.BmpPath) ? string.Empty : Path.GetRelativePath(Path.GetDirectoryName(path)!, result.BmpPath).Replace('\\', '/');
        string image = string.IsNullOrWhiteSpace(bmp) ? string.Empty : $"[bmp]({EscapeMarkdown(bmp)})";
        string diff = string.IsNullOrWhiteSpace(result.DiffPath) ? string.Empty : Path.GetRelativePath(Path.GetDirectoryName(path)!, result.DiffPath).Replace('\\', '/');
        string diffImage = string.IsNullOrWhiteSpace(diff) ? string.Empty : $"[diff]({EscapeMarkdown(diff)})";
        builder.AppendLine($"| `{EscapeMarkdown(result.Id)}` | {EscapeMarkdown(result.Name)} | `{EscapeMarkdown(result.Status)}` | `{compare}` | {result.Frames:N0} | `${result.Pc:X8}` | {result.NonBackgroundPixels:N0} | {result.Sprites:N0} | `{ShortHash(result.Sha256)}` | {image} | {diffImage} | {EscapeMarkdown(result.Detail)} |");
    }

    File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
}

string ShortHash(string? hash)
{
    return string.IsNullOrWhiteSpace(hash) ? string.Empty : hash[..Math.Min(12, hash.Length)];
}

void RunMovieVisualCheckpoints(string movieFolder, string romFolder, string outputFolder, int instructionsPerFrame, bool updateBaseline)
{
    string fullMovieFolder = Path.GetFullPath(movieFolder);
    string fullRomFolder = Path.GetFullPath(romFolder);
    string fullOutputFolder = Path.GetFullPath(outputFolder);
    string screenshotFolder = Path.Combine(fullOutputFolder, "screenshots");
    Directory.CreateDirectory(fullOutputFolder);
    Directory.CreateDirectory(screenshotFolder);

    string currentPath = Path.Combine(fullOutputFolder, "movie-visual-checkpoints-current.csv");
    string baselinePath = Path.Combine(fullOutputFolder, "movie-visual-checkpoints-baseline.csv");
    string summaryPath = Path.Combine(fullOutputFolder, "movie-visual-checkpoints.md");
    Dictionary<string, MovieVisualCheckpointBaseline> baseline = File.Exists(baselinePath)
        ? LoadMovieVisualCheckpointBaseline(baselinePath)
        : new Dictionary<string, MovieVisualCheckpointBaseline>(StringComparer.OrdinalIgnoreCase);

    string[] movies = Directory
        .EnumerateFiles(fullMovieFolder, "*.mdmovie", SearchOption.AllDirectories)
        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    Dictionary<string, string> romsBySha = BuildRomShaIndex(fullRomFolder);
    List<MovieVisualCheckpointResult> results = [];

    using StreamWriter writer = new(currentPath, false, Encoding.UTF8) { AutoFlush = true };
    writer.WriteLine("id,name,movie,rom,status,targetFrame,movieFrames,instructionsPerFrame,pc,renderMode,nonBackgroundPixels,sprites,audioPeak,sha256,bmp,detail");

    Console.WriteLine($"Movie visual checkpoints: {movies.Length:N0} movie(s), {romsBySha.Count:N0} indexed ROM(s), {instructionsPerFrame:N0} instructions/frame");
    foreach (string moviePath in movies)
    {
        MovieCheckpointSpec[] specs = BuildMovieCheckpointSpecs(moviePath, fullMovieFolder);
        foreach (MovieCheckpointSpec spec in specs)
        {
            MovieVisualCheckpointResult result = RunMovieVisualCheckpointCase(spec, fullMovieFolder, romsBySha, screenshotFolder, instructionsPerFrame, baseline);
            results.Add(result);
            writer.WriteLine(result.ToCsv());
            string compare = result.ExpectedSha256 is null ? "baseline:new" : result.MatchesBaseline ? "match" : "changed";
            Console.WriteLine($"{result.Status,-8} {compare,-12} {result.Id} frame={result.TargetFrame:N0} hash={ShortHash(result.Sha256)}");
        }
    }

    writer.Dispose();
    WriteMovieVisualCheckpointSummary(summaryPath, results, currentPath, baselinePath, instructionsPerFrame);
    if (updateBaseline || !File.Exists(baselinePath))
    {
        File.Copy(currentPath, baselinePath, overwrite: true);
        Console.WriteLine($"Updated movie visual checkpoint baseline at {baselinePath}");
    }

    int changed = results.Count(result => result.Status == "ok" && result.ExpectedSha256 is not null && !result.MatchesBaseline);
    int missing = results.Count(result => result.Status == "missing-rom");
    int failed = results.Count(result => result.Status is not "ok" and not "blank");
    Console.WriteLine($"Movie visual checkpoints complete: {results.Count:N0} checkpoint(s), {changed:N0} changed, {missing:N0} missing ROM(s), {failed:N0} failed.");
    Console.WriteLine($"Wrote report to {summaryPath}");
}

MovieCheckpointSpec[] BuildMovieCheckpointSpecs(string moviePath, string movieRoot)
{
    string relativeMovie = Path.GetRelativePath(movieRoot, moviePath);
    string baseId = Path.ChangeExtension(relativeMovie, null).Replace('\\', '/');
    string sidecarPath = Path.ChangeExtension(moviePath, ".mdcheckpoints.json");
    if (!File.Exists(sidecarPath))
    {
        string alternateSidecar = moviePath + ".checkpoints.json";
        if (File.Exists(alternateSidecar))
        {
            sidecarPath = alternateSidecar;
        }
    }

    if (File.Exists(sidecarPath))
    {
        return LoadMovieCheckpointSidecar(sidecarPath, moviePath, relativeMovie, baseId);
    }

    InputMovie movie = InputMovie.Load(moviePath);
    int frame = Math.Max(0, movie.FrameCount);
    return [new MovieCheckpointSpec($"{baseId}:final", $"{relativeMovie} final frame", moviePath, relativeMovie, frame)];
}

MovieCheckpointSpec[] LoadMovieCheckpointSidecar(string sidecarPath, string moviePath, string relativeMovie, string baseId)
{
    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(sidecarPath));
    JsonElement root = document.RootElement;
    JsonElement checkpoints = root.ValueKind == JsonValueKind.Array
        ? root
        : root.TryGetProperty("checkpoints", out JsonElement nested) ? nested : default;
    if (checkpoints.ValueKind != JsonValueKind.Array)
    {
        throw new InvalidDataException($"Movie checkpoint sidecar must contain a checkpoints array: {sidecarPath}");
    }

    List<MovieCheckpointSpec> specs = [];
    foreach (JsonElement item in checkpoints.EnumerateArray())
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("frame", out JsonElement frameElement) || !frameElement.TryGetInt32(out int frame))
        {
            throw new InvalidDataException($"Movie checkpoint sidecar entry is missing an integer frame: {sidecarPath}");
        }

        string idPart = item.TryGetProperty("id", out JsonElement idElement) && idElement.ValueKind == JsonValueKind.String
            ? idElement.GetString() ?? $"frame-{frame}"
            : $"frame-{frame}";
        string name = item.TryGetProperty("name", out JsonElement nameElement) && nameElement.ValueKind == JsonValueKind.String
            ? nameElement.GetString() ?? $"{relativeMovie} frame {frame:N0}"
            : $"{relativeMovie} frame {frame:N0}";
        specs.Add(new MovieCheckpointSpec($"{baseId}:{idPart}", name, moviePath, relativeMovie, Math.Max(0, frame)));
    }

    return specs.ToArray();
}

MovieVisualCheckpointResult RunMovieVisualCheckpointCase(
    MovieCheckpointSpec spec,
    string movieRoot,
    Dictionary<string, string> romsBySha,
    string screenshotFolder,
    int instructionsPerFrame,
    IReadOnlyDictionary<string, MovieVisualCheckpointBaseline> baseline)
{
    string relativeRom = string.Empty;
    string status = "ok";
    string detail = string.Empty;
    int completedFrames = 0;
    int movieFrames = 0;
    long audioPeak = 0;
    MegaDrive? machine = null;

    try
    {
        InputMovie movie = InputMovie.Load(spec.MoviePath);
        movieFrames = movie.FrameCount;
        string? romPath = ResolveMovieRom(movie, romsBySha);
        if (romPath is null)
        {
            status = "missing-rom";
            detail = "No matching ROM found for movie SHA/path.";
        }
        else
        {
            relativeRom = Path.GetFileName(romPath);
            CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
            if (!movie.Matches(cartridge))
            {
                status = "hash-mismatch";
                detail = "Movie ROM hash does not match resolved ROM.";
            }

            if (spec.TargetFrame > movie.FrameCount)
            {
                detail = AppendDetail(detail, $"Checkpoint is {spec.TargetFrame - movie.FrameCount:N0} frame(s) beyond the recorded input; padding with no input.");
            }

            movie.RestoreInitialSaveRam(cartridge);
            machine = new MegaDrive(cartridge, IsPalRegion(cartridge));
            machine.Reset();
            for (; completedFrames < spec.TargetFrame; completedFrames++)
            {
                machine.Bus.Controller1.Pressed = movie.GetButtons(completedFrames, playerIndex: 0);
                machine.Bus.Controller2.Pressed = movie.GetButtons(completedFrames, playerIndex: 1);
                machine.RunFrameCycles(instructionsPerFrame);
                audioPeak = Math.Max(audioPeak, PeakAbs(machine.RenderFrameStereoAudioSamples()));
            }

            machine.Bus.Controller1.Pressed = GenesisButton.None;
            machine.Bus.Controller2.Pressed = GenesisButton.None;
        }
    }
    catch (M68kException ex)
    {
        status = "m68k";
        detail = AppendDetail(detail, ex.Message);
    }
    catch (Exception ex)
    {
        status = "error";
        detail = AppendDetail(detail, ex.Message);
    }

    byte[] rgb = machine?.RenderFrameRgb() ?? new byte[Vdp.ScreenWidth * Vdp.ScreenHeight * 3];
    string sha = Convert.ToHexString(SHA256.HashData(rgb));
    string bmpPath = Path.Combine(screenshotFolder, $"{SanitizeFileName(spec.Id)}.bmp");
    WriteBmp(bmpPath, Vdp.ScreenWidth, Vdp.ScreenHeight, rgb);
    int nonBackground = machine is null ? 0 : CountNonBackgroundPixels(machine.Vdp, rgb);
    int sprites = machine?.Vdp.GetDiagnostics().LikelySpriteCount ?? 0;
    uint pc = machine?.MainCpu.PC ?? 0;
    string renderMode = machine?.Vdp.LastRenderMode ?? string.Empty;
    if (status == "ok" && nonBackground <= 64)
    {
        status = "blank";
        detail = AppendDetail(detail, "Checkpoint frame is near blank.");
    }

    baseline.TryGetValue(spec.Id, out MovieVisualCheckpointBaseline? expected);
    return new MovieVisualCheckpointResult(
        spec.Id,
        spec.Name,
        Path.GetRelativePath(movieRoot, spec.MoviePath),
        relativeRom,
        status,
        spec.TargetFrame,
        movieFrames,
        completedFrames,
        instructionsPerFrame,
        pc,
        renderMode,
        nonBackground,
        sprites,
        audioPeak,
        sha,
        expected?.Sha256,
        string.IsNullOrWhiteSpace(expected?.Sha256) || string.Equals(expected.Sha256, sha, StringComparison.OrdinalIgnoreCase),
        bmpPath,
        detail);
}

Dictionary<string, MovieVisualCheckpointBaseline> LoadMovieVisualCheckpointBaseline(string path)
{
    Dictionary<string, MovieVisualCheckpointBaseline> baseline = new(StringComparer.OrdinalIgnoreCase);
    foreach (string line in File.ReadLines(path).Skip(1))
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        string[] parts = SplitCsvLine(line);
        if (parts.Length >= 14)
        {
            baseline[parts[0]] = new MovieVisualCheckpointBaseline(parts[0], parts[13]);
        }
    }

    return baseline;
}

void WriteMovieVisualCheckpointSummary(string path, IReadOnlyList<MovieVisualCheckpointResult> results, string currentPath, string baselinePath, int instructionsPerFrame)
{
    int ok = results.Count(result => result.Status == "ok");
    int changed = results.Count(result => result.Status == "ok" && result.ExpectedSha256 is not null && !result.MatchesBaseline);
    int blank = results.Count(result => result.Status == "blank");
    int failed = results.Count(result => result.Status is not "ok" and not "blank");
    StringBuilder builder = new();
    builder.AppendLine("# mdSharp Movie Visual Checkpoints");
    builder.AppendLine();
    builder.AppendLine($"Current: `{Path.GetFullPath(currentPath)}`");
    builder.AppendLine($"Baseline: `{Path.GetFullPath(baselinePath)}`");
    builder.AppendLine($"Instructions/frame: {instructionsPerFrame:N0}");
    builder.AppendLine();
    builder.AppendLine($"- Checkpoints: {results.Count:N0}");
    builder.AppendLine($"- OK: {ok:N0}");
    builder.AppendLine($"- Changed: {changed:N0}");
    builder.AppendLine($"- Blank: {blank:N0}");
    builder.AppendLine($"- Failed: {failed:N0}");
    builder.AppendLine();
    builder.AppendLine("| ID | Name | Movie | ROM | Status | Compare | Target | PC | Pixels | Sprites | Audio | Hash | Screenshot | Detail |");
    builder.AppendLine("| --- | --- | --- | --- | --- | --- | ---: | --- | ---: | ---: | ---: | --- | --- | --- |");
    foreach (MovieVisualCheckpointResult result in results)
    {
        string compare = result.ExpectedSha256 is null ? "new" : result.MatchesBaseline ? "match" : "changed";
        string bmp = string.IsNullOrWhiteSpace(result.BmpPath) ? string.Empty : Path.GetRelativePath(Path.GetDirectoryName(path)!, result.BmpPath).Replace('\\', '/');
        string image = string.IsNullOrWhiteSpace(bmp) ? string.Empty : $"[bmp]({EscapeMarkdown(bmp)})";
        builder.AppendLine($"| `{EscapeMarkdown(result.Id)}` | {EscapeMarkdown(result.Name)} | {EscapeMarkdown(result.RelativeMovie)} | {EscapeMarkdown(result.RelativeRom)} | `{EscapeMarkdown(result.Status)}` | `{compare}` | {result.TargetFrame:N0} | `${result.Pc:X8}` | {result.NonBackgroundPixels:N0} | {result.Sprites:N0} | {result.AudioPeak:N0} | `{ShortHash(result.Sha256)}` | {image} | {EscapeMarkdown(result.Detail)} |");
    }

    File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
}

string AppendDetail(string existing, string detail)
{
    return string.IsNullOrWhiteSpace(existing) ? detail : $"{existing} {detail}";
}

void RunMovieRegression(string movieFolder, string romFolder, string outputFolder, int instructionsPerFrame)
{
    string fullMovieFolder = Path.GetFullPath(movieFolder);
    string fullRomFolder = Path.GetFullPath(romFolder);
    string fullOutputFolder = Path.GetFullPath(outputFolder);
    string screenshotFolder = Path.Combine(fullOutputFolder, "screenshots");
    Directory.CreateDirectory(fullOutputFolder);
    Directory.CreateDirectory(screenshotFolder);

    string[] movies = Directory
        .EnumerateFiles(fullMovieFolder, "*.mdmovie", SearchOption.AllDirectories)
        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    Dictionary<string, string> romsBySha = BuildRomShaIndex(fullRomFolder);
    List<MovieRegressionResult> results = [];
    string csvPath = Path.Combine(fullOutputFolder, "movie-regression.csv");
    using StreamWriter writer = new(csvPath, false, Encoding.UTF8) { AutoFlush = true };
    writer.WriteLine("movie,rom,status,frames,elapsedMs,fps,pc,exceptions,renderMode,nonBackgroundPixels,cramNonzero,sprites,audioPeak,sha256,bmp,detail");

    Console.WriteLine($"Movie regression: {movies.Length} movie(s), {romsBySha.Count} indexed ROM(s), {instructionsPerFrame:N0} instructions/frame");
    for (int i = 0; i < movies.Length; i++)
    {
        MovieRegressionResult result = RunMovieRegressionCase(movies[i], fullMovieFolder, romsBySha, screenshotFolder, instructionsPerFrame);
        results.Add(result);
        writer.WriteLine(result.ToCsv());
        Console.WriteLine($"{i + 1,4}/{movies.Length}: {result.Status,-12} {result.RelativeMovie} frames={result.Frames} fps={result.Fps:0.0} pc=${result.Pc:X8}");
    }

    WriteMovieRegressionHtml(Path.Combine(fullOutputFolder, "index.html"), results, instructionsPerFrame);
    Console.WriteLine($"Wrote movie regression dashboard to {Path.Combine(fullOutputFolder, "index.html")}");
}

void SummarizeCompatibilityReport(string csvPath, string? outputMarkdownPath, bool printToConsole = true)
{
    List<CompatibilityCsvRow> rows = LoadCompatibilityRows(csvPath);
    if (rows.Count == 0)
    {
        Console.WriteLine("No compatibility rows found.");
        return;
    }

    int ok = rows.Count(row => row.Status == "ok");
    int failed = rows.Count - ok;
    double averageFps = rows.Average(row => row.Fps);
    int nearBlank = rows.Count(row => row.Status == "ok" && row.NonBackgroundPixels <= 64);
    int neverVisible = rows.Count(row => row.Status == "ok" && row.MaxNonBackgroundPixels <= 64);
    int silent = rows.Count(row => row.Status == "ok" && row.AudioPeak == 0);
    int lowSprite = rows.Count(row => row.Status == "ok" && row.Sprites == 0);
    int fallback = rows.Count(row => row.RenderMode.Contains("fallback", StringComparison.OrdinalIgnoreCase));
    int cpuFaults = rows.Count(row => HasFaultExceptions(row.Exceptions));
    int cpuTrapsOrInterrupts = rows.Count(row => HasTrapOrInterruptActivity(row.Exceptions));
    string summary = BuildCompatibilitySummaryMarkdown(csvPath, rows, ok, failed, averageFps, nearBlank, neverVisible, silent, lowSprite, fallback, cpuFaults, cpuTrapsOrInterrupts);

    if (printToConsole)
    {
        Console.WriteLine(summary);
    }
    if (!string.IsNullOrWhiteSpace(outputMarkdownPath))
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputMarkdownPath)) ?? ".");
        File.WriteAllText(outputMarkdownPath, summary, Encoding.UTF8);
        Console.WriteLine($"Wrote summary to {Path.GetFullPath(outputMarkdownPath)}");
    }
}

void ExportCompatibilityMatrix(string csvPath, string outputFolder, bool publicMode)
{
    List<CompatibilityCsvRow> rows = LoadCompatibilityRows(csvPath);
    string fullOutputFolder = Path.GetFullPath(outputFolder);
    Directory.CreateDirectory(fullOutputFolder);

    string generatedAtUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    string? commit = ResolveGitCommit();
    CompatibilityExportEntry[] entries = rows
        .OrderBy(row => row.Rom, StringComparer.OrdinalIgnoreCase)
        .Select((row, index) => BuildCompatibilityExportEntry(row, index + 1, csvPath, fullOutputFolder, publicMode))
        .ToArray();

    CompatibilityExportReport report = new(
        generatedAtUtc,
        publicMode ? Path.GetFileName(csvPath) : Path.GetFullPath(csvPath),
        commit,
        publicMode,
        entries.Length,
        entries.GroupBy(entry => entry.Rating).OrderBy(group => group.Key).ToDictionary(group => group.Key, group => group.Count()),
        entries);

    JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
    };
    string jsonPath = Path.Combine(fullOutputFolder, "compatibility-matrix.json");
    string markdownPath = Path.Combine(fullOutputFolder, "compatibility-matrix.md");
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(report, jsonOptions), Encoding.UTF8);
    File.WriteAllText(markdownPath, BuildCompatibilityMatrixMarkdown(report, markdownPath), Encoding.UTF8);
    Console.WriteLine($"Wrote compatibility matrix JSON to {jsonPath}");
    Console.WriteLine($"Wrote compatibility matrix Markdown to {markdownPath}");
}

CompatibilityExportEntry BuildCompatibilityExportEntry(CompatibilityCsvRow row, int index, string csvPath, string outputFolder, bool publicMode)
{
    string rating = RateCompatibility(row);
    string displayName = publicMode ? $"ROM {index:000}" : row.Rom;
    string screenshot = publicMode || string.IsNullOrWhiteSpace(row.BmpPath)
        ? string.Empty
        : NormalizeReportPath(row.BmpPath, outputFolder);
    string notes = BuildCompatibilityNotes(row);

    return new CompatibilityExportEntry(
        displayName,
        publicMode ? string.Empty : row.Rom,
        rating,
        row.Status,
        row.Frames,
        row.Fps,
        $"${row.Pc:X8}",
        row.RenderMode,
        row.NonBackgroundPixels,
        row.MaxNonBackgroundPixels,
        row.Sprites,
        row.AudioPeak,
        row.Sha256,
        screenshot,
        notes);
}

string RateCompatibility(CompatibilityCsvRow row)
{
    if (row.Status.Equals("unsupported", StringComparison.OrdinalIgnoreCase))
    {
        return "Unsupported Hardware";
    }

    if (!row.Status.Equals("ok", StringComparison.OrdinalIgnoreCase))
    {
        return "Broken";
    }

    if (row.MaxNonBackgroundPixels <= 64 || row.NonBackgroundPixels <= 64)
    {
        return "Boots";
    }

    if (HasFaultExceptions(row.Exceptions) || row.RenderMode.Contains("fallback", StringComparison.OrdinalIgnoreCase))
    {
        return "C";
    }

    if (row.AudioPeak == 0 || row.Sprites == 0)
    {
        return "B";
    }

    return "A";
}

string BuildCompatibilityNotes(CompatibilityCsvRow row)
{
    List<string> notes = [];
    if (!string.IsNullOrWhiteSpace(row.Detail))
    {
        notes.Add(row.Detail);
    }

    if (!row.Status.Equals("ok", StringComparison.OrdinalIgnoreCase))
    {
        return string.Join(" ", notes.Distinct(StringComparer.Ordinal));
    }

    if (row.MaxNonBackgroundPixels <= 64)
    {
        notes.Add("No visible sampled frame.");
    }
    else if (row.NonBackgroundPixels <= 64)
    {
        notes.Add("Final frame is near-blank.");
    }

    if (row.AudioPeak == 0)
    {
        notes.Add("No audio activity sampled.");
    }

    if (row.Sprites == 0)
    {
        notes.Add("No sprites detected.");
    }

    if (HasFaultExceptions(row.Exceptions))
    {
        notes.Add($"CPU fault activity: {row.Exceptions}");
    }

    if (row.RenderMode.Contains("fallback", StringComparison.OrdinalIgnoreCase))
    {
        notes.Add($"Fallback render mode: {row.RenderMode}");
    }

    return string.Join(" ", notes.Distinct(StringComparer.Ordinal));
}

string BuildCompatibilityMatrixMarkdown(CompatibilityExportReport report, string markdownPath)
{
    StringBuilder builder = new();
    builder.AppendLine("# mdSharp Compatibility Matrix");
    builder.AppendLine();
    builder.AppendLine($"Generated: `{report.GeneratedAtUtc}`");
    builder.AppendLine($"Source CSV: `{report.SourceCsv}`");
    if (!string.IsNullOrWhiteSpace(report.LastTestedCommit))
    {
        builder.AppendLine($"Commit: `{report.LastTestedCommit}`");
    }

    if (report.PublicMode)
    {
        builder.AppendLine();
        builder.AppendLine("Public mode redacts ROM filenames and screenshot links. Use the JSON artifact from a local export for private debugging.");
    }

    builder.AppendLine();
    builder.AppendLine($"- Rows: `{report.TotalRows:N0}`");
    foreach ((string rating, int count) in report.RatingCounts.OrderBy(pair => CompatibilityRatingSort(pair.Key)).ThenBy(pair => pair.Key))
    {
        builder.AppendLine($"- `{rating}`: `{count:N0}`");
    }

    builder.AppendLine();
    builder.AppendLine("| ROM | Rating | Status | Frames | FPS | PC | Pixels | Max Pixels | Sprites | Audio | Mode | Screenshot | Notes |");
    builder.AppendLine("| --- | --- | --- | ---: | ---: | --- | ---: | ---: | ---: | ---: | --- | --- | --- |");
    foreach (CompatibilityExportEntry entry in report.Entries)
    {
        string screenshot = string.IsNullOrWhiteSpace(entry.Screenshot)
            ? string.Empty
            : $"[bmp]({EscapeMarkdown(RelativeMarkdownPath(markdownPath, entry.Screenshot))})";
        builder.AppendLine($"| {EscapeMarkdown(entry.DisplayName)} | `{EscapeMarkdown(entry.Rating)}` | `{EscapeMarkdown(entry.Status)}` | {entry.Frames:N0} | {entry.Fps:0.0} | `{EscapeMarkdown(entry.Pc)}` | {entry.NonBackgroundPixels:N0} | {entry.MaxNonBackgroundPixels:N0} | {entry.Sprites:N0} | {entry.AudioPeak:N0} | `{EscapeMarkdown(entry.RenderMode)}` | {screenshot} | {EscapeMarkdown(entry.Notes)} |");
    }

    return builder.ToString();
}

int CompatibilityRatingSort(string rating)
{
    return rating switch
    {
        "A" => 0,
        "B" => 1,
        "C" => 2,
        "Boots" => 3,
        "Broken" => 4,
        "Unsupported Hardware" => 5,
        _ => 6,
    };
}

string NormalizeReportPath(string path, string outputFolder)
{
    string fullPath = Path.GetFullPath(path);
    string fullOutput = Path.GetFullPath(outputFolder);
    return Path.GetRelativePath(fullOutput, fullPath).Replace('\\', '/');
}

string RelativeMarkdownPath(string markdownPath, string relativeToOutput)
{
    string baseFolder = Path.GetDirectoryName(Path.GetFullPath(markdownPath)) ?? ".";
    string fullPath = Path.GetFullPath(Path.Combine(baseFolder, relativeToOutput));
    return Path.GetRelativePath(baseFolder, fullPath).Replace('\\', '/');
}

string? ResolveGitCommit()
{
    try
    {
        using System.Diagnostics.Process process = new();
        process.StartInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = "rev-parse --short HEAD",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        process.Start();
        string output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit(2000);
        return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) ? output : null;
    }
    catch
    {
        return null;
    }
}

List<CompatibilityCsvRow> LoadCompatibilityRows(string csvPath)
{
    List<CompatibilityCsvRow> rows = [];
    foreach (string line in File.ReadLines(csvPath).Skip(1))
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        string[] parts = SplitCsvLine(line);
        if (parts.Length < 15)
        {
            continue;
        }

        bool hasMaxPixels = parts.Length >= 16;
        rows.Add(new CompatibilityCsvRow(
            parts[0],
            parts[1],
            ParseInt(parts[2]),
            ParseLong(parts[3]),
            ParseDouble(parts[4]),
            ParseHexPc(parts[5]),
            parts[6],
            parts[7],
            ParseInt(parts[8]),
            hasMaxPixels ? ParseInt(parts[9]) : ParseInt(parts[8]),
            ParseInt(parts[hasMaxPixels ? 10 : 9]),
            ParseInt(parts[hasMaxPixels ? 11 : 10]),
            ParseLong(parts[hasMaxPixels ? 12 : 11]),
            parts[hasMaxPixels ? 13 : 12],
            parts[hasMaxPixels ? 14 : 13],
            parts[hasMaxPixels ? 15 : 14]));
    }

    return rows;
}

string BuildCompatibilitySummaryMarkdown(
    string csvPath,
    IReadOnlyList<CompatibilityCsvRow> rows,
    int ok,
    int failed,
    double averageFps,
    int nearBlank,
    int neverVisible,
    int silent,
    int lowSprite,
    int fallback,
    int cpuFaults,
    int cpuTrapsOrInterrupts)
{
    StringBuilder builder = new();
    builder.AppendLine("# mdSharp Compatibility Summary");
    builder.AppendLine();
    builder.AppendLine($"Source: `{Path.GetFullPath(csvPath)}`");
    builder.AppendLine();
    builder.AppendLine($"- ROMs: {rows.Count:N0}");
    builder.AppendLine($"- OK: {ok:N0}");
    builder.AppendLine($"- Failed: {failed:N0}");
    builder.AppendLine($"- Average emulation FPS: {averageFps:0.0}");
    builder.AppendLine($"- Near-blank final frames: {nearBlank:N0}");
    builder.AppendLine($"- Never-visible sampled successful runs: {neverVisible:N0}");
    builder.AppendLine($"- Silent successful runs: {silent:N0}");
    builder.AppendLine($"- Successful runs with zero detected sprites: {lowSprite:N0}");
    builder.AppendLine($"- Runs using fallback render modes: {fallback:N0}");
    builder.AppendLine($"- Runs with CPU fault vectors recorded: {cpuFaults:N0}");
    builder.AppendLine($"- Runs with TRAP/autovector activity: {cpuTrapsOrInterrupts:N0}");
    builder.AppendLine();

    AppendStatusBreakdown(builder, rows);
    AppendRows(builder, "Failures", rows.Where(row => row.Status != "ok").Take(40));
    AppendRows(builder, "CPU Fault Vector Activity", rows.Where(row => HasFaultExceptions(row.Exceptions)).OrderByDescending(row => CountFaultExceptionEvents(row.Exceptions)).ThenBy(row => row.Rom).Take(40));
    AppendRows(builder, "TRAP/Autovector Activity", rows.Where(row => HasTrapOrInterruptActivity(row.Exceptions)).OrderByDescending(row => CountTrapOrInterruptEvents(row.Exceptions)).ThenBy(row => row.Rom).Take(40));
    AppendRows(builder, "Near-Blank Final Frames", rows.Where(row => row.Status == "ok" && row.NonBackgroundPixels <= 64).OrderBy(row => row.NonBackgroundPixels).ThenBy(row => row.Rom).Take(40));
    AppendRows(builder, "Never-Visible Sampled Runs", rows.Where(row => row.Status == "ok" && row.MaxNonBackgroundPixels <= 64).OrderBy(row => row.MaxNonBackgroundPixels).ThenBy(row => row.Rom).Take(40));
    AppendRows(builder, "Lowest Audio Activity", rows.Where(row => row.Status == "ok").OrderBy(row => row.AudioPeak).ThenBy(row => row.Rom).Take(40));
    AppendRows(builder, "Slowest Runs", rows.Where(row => row.Status == "ok").OrderBy(row => row.Fps).ThenBy(row => row.Rom).Take(40));
    return builder.ToString();
}

void AppendStatusBreakdown(StringBuilder builder, IReadOnlyList<CompatibilityCsvRow> rows)
{
    builder.AppendLine("## Status Breakdown");
    builder.AppendLine();
    foreach (IGrouping<string, CompatibilityCsvRow> group in rows.GroupBy(row => row.Status).OrderByDescending(group => group.Count()).ThenBy(group => group.Key))
    {
        builder.AppendLine($"- `{group.Key}`: {group.Count():N0}");
    }

    builder.AppendLine();
}

void AppendRows(StringBuilder builder, string title, IEnumerable<CompatibilityCsvRow> rows)
{
    builder.AppendLine($"## {title}");
    builder.AppendLine();
    builder.AppendLine("| ROM | Status | Frames | FPS | PC | Pixels | Max Pixels | Sprites | Audio | Detail |");
    builder.AppendLine("| --- | --- | ---: | ---: | --- | ---: | ---: | ---: | ---: | --- |");
    int count = 0;
    foreach (CompatibilityCsvRow row in rows)
    {
        string detail = string.IsNullOrWhiteSpace(row.Detail) ? row.Exceptions : row.Detail;
        builder.AppendLine($"| {EscapeMarkdown(row.Rom)} | `{EscapeMarkdown(row.Status)}` | {row.Frames:N0} | {row.Fps:0.0} | `${row.Pc:X8}` | {row.NonBackgroundPixels:N0} | {row.MaxNonBackgroundPixels:N0} | {row.Sprites:N0} | {row.AudioPeak:N0} | {EscapeMarkdown(detail)} |");
        count++;
    }

    if (count == 0)
    {
        builder.AppendLine("| None | | | | | | | | | |");
    }

    builder.AppendLine();
}

bool HasCpuExceptions(string exceptions)
{
    return !string.IsNullOrWhiteSpace(exceptions) && !exceptions.Equals("none", StringComparison.OrdinalIgnoreCase);
}

bool HasFaultExceptions(string exceptions)
{
    return ParseExceptionEvents(exceptions).Any(exception => IsFaultVector(exception.Vector));
}

bool HasTrapOrInterruptActivity(string exceptions)
{
    return ParseExceptionEvents(exceptions).Any(exception => !IsFaultVector(exception.Vector));
}

bool IsFaultVector(int vector)
{
    return vector is 2 or 3 or >= 5 and <= 9 or > 47;
}

int CountFaultExceptionEvents(string exceptions)
{
    return ParseExceptionEvents(exceptions).Where(exception => IsFaultVector(exception.Vector)).Sum(exception => exception.Count);
}

int CountTrapOrInterruptEvents(string exceptions)
{
    return ParseExceptionEvents(exceptions).Where(exception => !IsFaultVector(exception.Vector)).Sum(exception => exception.Count);
}

IEnumerable<(int Vector, int Count)> ParseExceptionEvents(string exceptions)
{
    if (!HasCpuExceptions(exceptions))
    {
        yield break;
    }

    foreach (string part in exceptions.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        int colon = part.IndexOf(':', StringComparison.Ordinal);
        if (colon > 0 && int.TryParse(part[..colon], out int vector) && int.TryParse(part[(colon + 1)..], out int count))
        {
            yield return (vector, count);
        }
    }
}

int ParseInt(string value)
{
    return int.TryParse(value, out int parsed) ? parsed : 0;
}

long ParseLong(string value)
{
    return long.TryParse(value, out long parsed) ? parsed : 0;
}

double ParseDouble(string value)
{
    return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed)
        ? parsed
        : 0.0;
}

uint ParseHexPc(string value)
{
    value = value.Trim();
    if (value.StartsWith("$", StringComparison.Ordinal))
    {
        value = value[1..];
    }

    return uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out uint parsed) ? parsed : 0;
}

string EscapeMarkdown(string value)
{
    return value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}

MovieRegressionResult RunMovieRegressionCase(string moviePath, string movieRoot, Dictionary<string, string> romsBySha, string screenshotFolder, int instructionsPerFrame)
{
    string relativeMovie = Path.GetRelativePath(movieRoot, moviePath);
    string relativeRom = string.Empty;
    string status = "ok";
    string detail = string.Empty;
    MegaDrive? machine = null;
    int completedFrames = 0;
    long audioPeak = 0;
    System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

    try
    {
        InputMovie movie = InputMovie.Load(moviePath);
        string? romPath = ResolveMovieRom(movie, romsBySha);
        if (romPath is null)
        {
            throw new FileNotFoundException("No matching ROM found for movie SHA/path.");
        }

        relativeRom = Path.GetFileName(romPath);
        CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
        if (!movie.Matches(cartridge))
        {
            status = "hash-mismatch";
            detail = "Movie ROM hash does not match resolved ROM.";
        }

        movie.RestoreInitialSaveRam(cartridge);
        machine = new MegaDrive(cartridge, IsPalRegion(cartridge));
        machine.Reset();
        for (; completedFrames < movie.FrameCount; completedFrames++)
        {
            machine.Bus.Controller1.Pressed = movie.GetButtons(completedFrames, playerIndex: 0);
            machine.Bus.Controller2.Pressed = movie.GetButtons(completedFrames, playerIndex: 1);
            machine.RunFrameCycles(instructionsPerFrame);
            audioPeak = Math.Max(audioPeak, PeakAbs(machine.RenderFrameStereoAudioSamples()));
        }

        machine.Bus.Controller1.Pressed = GenesisButton.None;
        machine.Bus.Controller2.Pressed = GenesisButton.None;
    }
    catch (M68kException ex)
    {
        status = "m68k";
        detail = ex.Message;
    }
    catch (Exception ex)
    {
        status = "error";
        detail = ex.Message;
    }

    stopwatch.Stop();
    byte[] rgb = machine?.RenderFrameRgb() ?? new byte[Vdp.ScreenWidth * Vdp.ScreenHeight * 3];
    string hash = Convert.ToHexString(SHA256.HashData(rgb));
    int nonBackground = machine is null ? 0 : CountNonBackgroundPixels(machine.Vdp, rgb);
    int cramNonzero = machine is null ? 0 : CountNonzeroCram(machine.Vdp);
    int sprites = machine?.Vdp.GetDiagnostics().LikelySpriteCount ?? 0;
    string mode = machine?.Vdp.LastRenderMode ?? string.Empty;
    uint pc = machine?.MainCpu.PC ?? 0;
    string exceptions = machine is null ? string.Empty : FormatExceptions(machine.MainCpu);
    string bmpPath = Path.Combine(screenshotFolder, $"{SanitizeFileName(relativeMovie)}.bmp");
    WriteBmp(bmpPath, Vdp.ScreenWidth, Vdp.ScreenHeight, rgb);
    double fps = completedFrames <= 0 || stopwatch.Elapsed.TotalSeconds <= 0.0
        ? 0.0
        : completedFrames / stopwatch.Elapsed.TotalSeconds;

    return new MovieRegressionResult(
        relativeMovie,
        relativeRom,
        status,
        completedFrames,
        stopwatch.ElapsedMilliseconds,
        fps,
        pc,
        exceptions,
        mode,
        nonBackground,
        cramNonzero,
        sprites,
        audioPeak,
        hash,
        bmpPath,
        detail);
}

string[] EnumerateRomFiles(string folder)
{
    if (!Directory.Exists(folder))
    {
        Console.Error.WriteLine($"ROM folder not found: {folder}");
        Environment.Exit(1);
    }

    return Directory
        .EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
        .Where(path => romExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

string? GetOptionValue(string[] values, string option)
{
    for (int i = 0; i < values.Length; i++)
    {
        string value = values[i];
        if (value.Equals(option, StringComparison.OrdinalIgnoreCase))
        {
            return i + 1 < values.Length ? values[i + 1] : null;
        }

        string prefix = option + "=";
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return value[prefix.Length..];
        }
    }

    return null;
}

int? TryGetPositiveOption(string[] values, string option)
{
    string? value = GetOptionValue(values, option);
    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed > 0
        ? parsed
        : null;
}

double? TryGetPositiveDoubleOption(string[] values, string option)
{
    string? value = GetOptionValue(values, option);
    return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) && parsed > 0.0
        ? parsed
        : null;
}

Dictionary<string, string> BuildRomShaIndex(string romFolder)
{
    Dictionary<string, string> index = new(StringComparer.OrdinalIgnoreCase);
    foreach (string rom in EnumerateRomFiles(romFolder))
    {
        CartridgeImage cartridge = CartridgeImage.FromFile(rom);
        index[InputMovie.ComputeRomSha256(cartridge)] = rom;
    }

    return index;
}

string? ResolveMovieRom(InputMovie movie, Dictionary<string, string> romsBySha)
{
    if (!string.IsNullOrWhiteSpace(movie.RomSha256) && romsBySha.TryGetValue(movie.RomSha256, out string? romByHash))
    {
        return romByHash;
    }

    if (!string.IsNullOrWhiteSpace(movie.RomPath) && File.Exists(movie.RomPath))
    {
        return movie.RomPath;
    }

    return null;
}

bool IsPalRegion(CartridgeImage cartridge)
{
    return cartridge.Header.PrefersPal;
}

MegaDrive CreateMachine(string romPath)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    return new MegaDrive(cartridge, IsPalRegion(cartridge), TryLoadThirtyTwoXM68kBios());
}

ReadOnlyMemory<byte>? TryLoadThirtyTwoXM68kBios()
{
    string[] candidates =
    [
        Environment.GetEnvironmentVariable("MDSHARP_32X_M68K_BIOS") ?? string.Empty,
        Path.Combine(AppContext.BaseDirectory, "32X_G_BIOS.BIN"),
        Path.Combine(AppContext.BaseDirectory, "32X_M68K_BIOS.BIN"),
        Path.Combine(Environment.CurrentDirectory, "32X_G_BIOS.BIN"),
        Path.Combine(Environment.CurrentDirectory, "32X_M68K_BIOS.BIN"),
    ];

    foreach (string candidate in candidates)
    {
        if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
        {
            return File.ReadAllBytes(candidate);
        }
    }

    return null;
}

long PeakAbs(short[] samples)
{
    long peak = 0;
    foreach (short sample in samples)
    {
        peak = Math.Max(peak, Math.Abs((int)sample));
    }

    return peak;
}

long PeakAbsSpan(ReadOnlySpan<short> samples)
{
    long peak = 0;
    foreach (short sample in samples)
    {
        peak = Math.Max(peak, Math.Abs((int)sample));
    }

    return peak;
}

void WriteCompatibilityHtml(string path, string title, IReadOnlyList<CompatibilityResult> results, int frames, int instructionsPerFrame)
{
    int ok = results.Count(result => result.Status == "ok");
    int failed = results.Count - ok;
    StringBuilder html = new();
    html.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>" + EscapeHtml(title) + "</title>");
    html.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:24px;background:#f6f7f9;color:#15171a}table{border-collapse:collapse;width:100%;background:white}th,td{padding:6px 8px;border-bottom:1px solid #ddd;font-size:12px;text-align:left}th{position:sticky;top:0;background:#20242b;color:white}.ok{color:#127a34;font-weight:600}.bad{color:#b42318;font-weight:600}img{image-rendering:pixelated;width:160px}</style></head><body>");
    html.AppendLine($"<h1>{EscapeHtml(title)}</h1><p>{results.Count:N0} ROMs, {ok:N0} ok, {failed:N0} failed, {frames:N0} frames, {instructionsPerFrame:N0} instructions/frame.</p>");
    html.AppendLine("<table><thead><tr><th>ROM</th><th>Status</th><th>Frames</th><th>FPS</th><th>PC</th><th>Mode</th><th>Pixels</th><th>Max Pixels</th><th>Sprites</th><th>Audio</th><th>Hash</th><th>Screenshot</th><th>Detail</th></tr></thead><tbody>");
    foreach (CompatibilityResult result in results)
    {
        string css = result.Status == "ok" ? "ok" : "bad";
        string screenshot = string.IsNullOrWhiteSpace(result.BmpPath)
            ? string.Empty
            : $"<a href=\"{EscapeHtml(Path.GetRelativePath(Path.GetDirectoryName(path)!, result.BmpPath).Replace('\\', '/'))}\"><img src=\"{EscapeHtml(Path.GetRelativePath(Path.GetDirectoryName(path)!, result.BmpPath).Replace('\\', '/'))}\"></a>";
        html.AppendLine($"<tr><td>{EscapeHtml(result.RelativeRom)}</td><td class=\"{css}\">{EscapeHtml(result.Status)}</td><td>{result.Frames:N0}</td><td>{result.Fps:0.0}</td><td>${result.Pc:X8}</td><td>{EscapeHtml(result.RenderMode)}</td><td>{result.NonBackgroundPixels:N0}</td><td>{result.MaxNonBackgroundPixels:N0}</td><td>{result.Sprites:N0}</td><td>{result.AudioPeak:N0}</td><td><code>{EscapeHtml(result.Sha256[..Math.Min(12, result.Sha256.Length)])}</code></td><td>{screenshot}</td><td>{EscapeHtml(result.Detail)}</td></tr>");
    }

    html.AppendLine("</tbody></table></body></html>");
    File.WriteAllText(path, html.ToString(), Encoding.UTF8);
}

void WriteMovieRegressionHtml(string path, IReadOnlyList<MovieRegressionResult> results, int instructionsPerFrame)
{
    int ok = results.Count(result => result.Status == "ok");
    int failed = results.Count - ok;
    StringBuilder html = new();
    html.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>mdSharp movie regression</title>");
    html.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:24px;background:#f6f7f9;color:#15171a}table{border-collapse:collapse;width:100%;background:white}th,td{padding:6px 8px;border-bottom:1px solid #ddd;font-size:12px;text-align:left}th{position:sticky;top:0;background:#20242b;color:white}.ok{color:#127a34;font-weight:600}.bad{color:#b42318;font-weight:600}img{image-rendering:pixelated;width:160px}</style></head><body>");
    html.AppendLine($"<h1>mdSharp movie regression</h1><p>{results.Count:N0} movies, {ok:N0} ok, {failed:N0} failed, {instructionsPerFrame:N0} instructions/frame.</p>");
    html.AppendLine("<table><thead><tr><th>Movie</th><th>ROM</th><th>Status</th><th>Frames</th><th>FPS</th><th>PC</th><th>Mode</th><th>Pixels</th><th>Sprites</th><th>Audio</th><th>Hash</th><th>Screenshot</th><th>Detail</th></tr></thead><tbody>");
    foreach (MovieRegressionResult result in results)
    {
        string css = result.Status == "ok" ? "ok" : "bad";
        string relBmp = Path.GetRelativePath(Path.GetDirectoryName(path)!, result.BmpPath).Replace('\\', '/');
        html.AppendLine($"<tr><td>{EscapeHtml(result.RelativeMovie)}</td><td>{EscapeHtml(result.RelativeRom)}</td><td class=\"{css}\">{EscapeHtml(result.Status)}</td><td>{result.Frames:N0}</td><td>{result.Fps:0.0}</td><td>${result.Pc:X8}</td><td>{EscapeHtml(result.RenderMode)}</td><td>{result.NonBackgroundPixels:N0}</td><td>{result.Sprites:N0}</td><td>{result.AudioPeak:N0}</td><td><code>{EscapeHtml(result.Sha256[..Math.Min(12, result.Sha256.Length)])}</code></td><td><a href=\"{EscapeHtml(relBmp)}\"><img src=\"{EscapeHtml(relBmp)}\"></a></td><td>{EscapeHtml(result.Detail)}</td></tr>");
    }

    html.AppendLine("</tbody></table></body></html>");
    File.WriteAllText(path, html.ToString(), Encoding.UTF8);
}

string SanitizeFileName(string value)
{
    StringBuilder builder = new(value.Length);
    char[] invalid = Path.GetInvalidFileNameChars();
    foreach (char ch in value)
    {
        builder.Append(invalid.Contains(ch) || ch is '\\' or '/' or ':' ? '_' : ch);
    }

    return builder.ToString();
}

string EscapeHtml(string value)
{
    return value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);
}

void CompareRegression(string baselineCsv, string currentCsv)
{
    Dictionary<string, string> baseline = LoadRegressionHashes(baselineCsv);
    Dictionary<string, string> current = LoadRegressionHashes(currentCsv);
    int changed = 0;
    foreach ((string rom, string hash) in current.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
    {
        if (!baseline.TryGetValue(rom, out string? oldHash))
        {
            Console.WriteLine($"added,{rom},{hash}");
            changed++;
            continue;
        }

        if (!string.Equals(hash, oldHash, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"changed,{rom},{oldHash},{hash}");
            changed++;
        }
    }

    foreach (string removed in baseline.Keys.Except(current.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine($"removed,{removed},{baseline[removed]}");
        changed++;
    }

    Console.WriteLine($"Regression compare complete: {changed} difference(s).");
}

void ComparePerfSuite(string baselineCsv, string currentCsv, string? outputPath)
{
    Dictionary<string, PerfCsvRow> baseline = LoadPerfCsvRows(baselineCsv);
    Dictionary<string, PerfCsvRow> current = LoadPerfCsvRows(currentCsv);
    List<PerfCompareRow> rows = [];

    foreach ((string rom, PerfCsvRow currentRow) in current.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
    {
        if (!baseline.TryGetValue(rom, out PerfCsvRow? baselineRow))
        {
            rows.Add(new PerfCompareRow(rom, "added", 0, currentRow.Fps, 0, 0, currentRow.RenderMsPerFrame, 0, currentRow.CpuMsPerFrame, 0, currentRow.AudioMsPerFrame, 0));
            continue;
        }

        rows.Add(new PerfCompareRow(
            rom,
            string.Equals(currentRow.Status, baselineRow.Status, StringComparison.OrdinalIgnoreCase) ? currentRow.Status : $"{baselineRow.Status}->{currentRow.Status}",
            baselineRow.Fps,
            currentRow.Fps,
            PercentChange(currentRow.Fps, baselineRow.Fps),
            baselineRow.RenderMsPerFrame,
            currentRow.RenderMsPerFrame,
            PercentChange(currentRow.RenderMsPerFrame, baselineRow.RenderMsPerFrame),
            currentRow.CpuMsPerFrame,
            PercentChange(currentRow.CpuMsPerFrame, baselineRow.CpuMsPerFrame),
            currentRow.AudioMsPerFrame,
            PercentChange(currentRow.AudioMsPerFrame, baselineRow.AudioMsPerFrame)));
    }

    foreach (string removed in baseline.Keys.Except(current.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
    {
        PerfCsvRow baselineRow = baseline[removed];
        rows.Add(new PerfCompareRow(removed, "removed", baselineRow.Fps, 0, 0, baselineRow.RenderMsPerFrame, 0, 0, 0, 0, 0, 0));
    }

    Console.WriteLine("rom,status,baselineFps,currentFps,fpsDeltaPct,baselineRenderMs,currentRenderMs,renderDeltaPct,cpuDeltaPct,audioDeltaPct");
    foreach (PerfCompareRow row in rows.OrderBy(row => row.FpsDeltaPct))
    {
        Console.WriteLine($"{Csv(row.Rom)},{row.Status},{row.BaselineFps:0.###},{row.CurrentFps:0.###},{row.FpsDeltaPct:0.##},{row.BaselineRenderMsPerFrame:0.###},{row.CurrentRenderMsPerFrame:0.###},{row.RenderDeltaPct:0.##},{row.CpuDeltaPct:0.##},{row.AudioDeltaPct:0.##}");
    }

    if (outputPath is not null)
    {
        WritePerfCompareMarkdown(outputPath, baselineCsv, currentCsv, rows);
        Console.WriteLine($"Wrote perf comparison report to {Path.GetFullPath(outputPath)}");
    }
}

void WritePerfCompareMarkdown(string path, string baselineCsv, string currentCsv, IReadOnlyList<PerfCompareRow> rows)
{
    StringBuilder builder = new();
    builder.AppendLine("# mdSharp Perf Compare");
    builder.AppendLine();
    builder.AppendLine($"Baseline: `{Path.GetFullPath(baselineCsv)}`");
    builder.AppendLine($"Current: `{Path.GetFullPath(currentCsv)}`");
    builder.AppendLine();

    PerfCompareRow[] matched = rows.Where(row => row.Status is not "added" and not "removed").ToArray();
    if (matched.Length > 0)
    {
        double averageFpsDelta = matched.Average(row => row.FpsDeltaPct);
        double averageRenderDelta = matched.Average(row => row.RenderDeltaPct);
        builder.AppendLine($"- Average FPS delta: `{averageFpsDelta:0.##}%`");
        builder.AppendLine($"- Average render ms/frame delta: `{averageRenderDelta:0.##}%`");
        builder.AppendLine();
    }

    builder.AppendLine("| ROM | Status | Baseline FPS | Current FPS | FPS delta | Baseline render | Current render | Render delta | CPU delta | Audio delta |");
    builder.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
    foreach (PerfCompareRow row in rows.OrderBy(row => row.FpsDeltaPct))
    {
        builder.AppendLine($"| {EscapeMarkdown(row.Rom)} | {EscapeMarkdown(row.Status)} | {row.BaselineFps:0.0} | {row.CurrentFps:0.0} | {row.FpsDeltaPct:0.##}% | {row.BaselineRenderMsPerFrame:0.###} | {row.CurrentRenderMsPerFrame:0.###} | {row.RenderDeltaPct:0.##}% | {row.CpuDeltaPct:0.##}% | {row.AudioDeltaPct:0.##}% |");
    }

    File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
}

Dictionary<string, PerfCsvRow> LoadPerfCsvRows(string path)
{
    using StreamReader reader = new(path, Encoding.UTF8);
    string? headerLine = reader.ReadLine();
    if (headerLine is null)
    {
        return new Dictionary<string, PerfCsvRow>(StringComparer.OrdinalIgnoreCase);
    }

    string[] headers = SplitCsvLine(headerLine);
    Dictionary<string, int> headerIndex = new(StringComparer.OrdinalIgnoreCase);
    for (int i = 0; i < headers.Length; i++)
    {
        headerIndex[headers[i]] = i;
    }

    Dictionary<string, PerfCsvRow> rows = new(StringComparer.OrdinalIgnoreCase);
    while (!reader.EndOfStream)
    {
        string? line = reader.ReadLine();
        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        string[] parts = SplitCsvLine(line);
        string rom = GetCsv(parts, headerIndex, "rom");
        if (rom.Length == 0)
        {
            continue;
        }

        rows[rom] = new PerfCsvRow(
            rom,
            GetCsv(parts, headerIndex, "status"),
            GetCsvDouble(parts, headerIndex, "fps"),
            GetCsvDouble(parts, headerIndex, "cpuMsPerFrame"),
            GetCsvDouble(parts, headerIndex, "renderMsPerFrame"),
            GetCsvDouble(parts, headerIndex, "audioMsPerFrame"));
    }

    return rows;
}

string GetCsv(string[] parts, Dictionary<string, int> headerIndex, string name)
{
    return headerIndex.TryGetValue(name, out int index) && (uint)index < (uint)parts.Length ? parts[index] : string.Empty;
}

double GetCsvDouble(string[] parts, Dictionary<string, int> headerIndex, string name)
{
    return double.TryParse(GetCsv(parts, headerIndex, name), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0.0;
}

static double PercentChange(double current, double baseline)
{
    return Math.Abs(baseline) <= double.Epsilon ? 0.0 : ((current - baseline) / baseline) * 100.0;
}

Dictionary<string, string> LoadRegressionHashes(string path)
{
    Dictionary<string, string> hashes = new(StringComparer.OrdinalIgnoreCase);
    foreach (string line in File.ReadLines(path).Skip(1))
    {
        string[] parts = SplitCsvLine(line);
        if (parts.Length >= 7)
        {
            hashes[parts[0]] = parts[6];
        }
    }

    return hashes;
}

string[] SplitCsvLine(string line)
{
    List<string> parts = new();
    StringBuilder current = new();
    bool quoted = false;
    for (int i = 0; i < line.Length; i++)
    {
        char ch = line[i];
        if (ch == '"')
        {
            if (quoted && i + 1 < line.Length && line[i + 1] == '"')
            {
                current.Append('"');
                i++;
            }
            else
            {
                quoted = !quoted;
            }
        }
        else if (ch == ',' && !quoted)
        {
            parts.Add(current.ToString());
            current.Clear();
        }
        else
        {
            current.Append(ch);
        }
    }

    parts.Add(current.ToString());
    return parts.ToArray();
}

void RuntimeLoop(string romPath, int seconds)
{
    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();
    DateTime end = DateTime.UtcNow.AddSeconds(seconds);
    int frames = 0;
    while (DateTime.UtcNow < end)
    {
        machine.RunFrameCycles();
        frames++;
        double targetMs = 1000.0 / machine.Scheduler.FrameRate;
        Thread.Sleep(Math.Max(0, (int)(targetMs - 1)));
    }

    Console.WriteLine($"Runtime loop completed {frames} frame(s) in {seconds} second(s).");
    Console.WriteLine(FormatState(machine));
}

short[] DumpAudio(string romPath, string outputPath, int frames, int? instructionsPerFrame = null)
{
    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();
    List<short> samples = new();
    for (int frame = 0; frame < frames; frame++)
    {
        if (instructionsPerFrame is int frameBudget)
        {
            machine.RunFrameCycles(frameBudget);
        }
        else
        {
            machine.RunFrameCycles();
        }

        samples.AddRange(machine.RenderFrameStereoAudioSamples());
    }

    short[] renderedSamples = samples.ToArray();
    WriteWav(outputPath, renderedSamples, 44_100, 2);
    Console.WriteLine($"Wrote {samples.Count / 2:N0} stereo audio frame(s) to {Path.GetFullPath(outputPath)}");
    return renderedSamples;
}

void RenderVgm(string vgmPath, string outputPath, double? maxSeconds)
{
    byte[] data = LoadVgmBytes(vgmPath);
    if (data.Length < 0x40 || data[0] != 'V' || data[1] != 'g' || data[2] != 'm' || data[3] != ' ')
    {
        throw new InvalidDataException("Input is not a VGM/VGZ file.");
    }

    int dataOffset = 0x40;
    uint version = ReadU32(data, 0x08);
    if (version >= 0x00000150 && data.Length >= 0x38)
    {
        dataOffset = 0x34 + (int)ReadU32(data, 0x34);
    }

    int maxSamples = maxSeconds is > 0.0
        ? Math.Max(1, (int)Math.Round(maxSeconds.Value * AudioConstants.DefaultSampleRate))
        : int.MaxValue;
    Ym2612 ym = new();
    Psg psg = new();
    List<short> samples = new(Math.Min(maxSamples, AudioConstants.DefaultSampleRate * 120) * AudioConstants.StereoChannels);
    byte[] pcmData = [];
    int pcmOffset = 0;
    double filterLeft = 0.0;
    double filterRight = 0.0;
    double psgFilter = 0.0;
    double bassFilterLeft = 0.0;
    double bassFilterRight = 0.0;
    double filterAlpha = AudioOutputLowPassAlpha(AudioConstants.DefaultSampleRate);
    double psgFilterAlpha = AudioPsgLowPassAlpha(AudioConstants.DefaultSampleRate);
    double bassShelfAlpha = AudioBassShelfAlpha(AudioConstants.DefaultSampleRate);
    int position = dataOffset;
    bool ended = false;

    while (position < data.Length && !ended && samples.Count / AudioConstants.StereoChannels < maxSamples)
    {
        byte command = data[position++];
        switch (command)
        {
            case 0x4F:
                position++;
                break;
            case 0x50:
                psg.Write(data[position++]);
                break;
            case 0x52:
            case 0x53:
            {
                int port = command == 0x53 ? 1 : 0;
                byte address = data[position++];
                byte value = data[position++];
                ym.WriteAddress(port, address);
                ym.WriteData(port, value);
                break;
            }
            case 0x61:
            {
                int wait = ReadU16(data, position);
                position += 2;
                AppendVgmMixedSamples(samples, ym, psg, Math.Min(wait, maxSamples - (samples.Count / AudioConstants.StereoChannels)), ref psgFilter, ref bassFilterLeft, ref bassFilterRight, ref filterLeft, ref filterRight, psgFilterAlpha, bassShelfAlpha, filterAlpha);
                break;
            }
            case 0x62:
                AppendVgmMixedSamples(samples, ym, psg, Math.Min(735, maxSamples - (samples.Count / AudioConstants.StereoChannels)), ref psgFilter, ref bassFilterLeft, ref bassFilterRight, ref filterLeft, ref filterRight, psgFilterAlpha, bassShelfAlpha, filterAlpha);
                break;
            case 0x63:
                AppendVgmMixedSamples(samples, ym, psg, Math.Min(882, maxSamples - (samples.Count / AudioConstants.StereoChannels)), ref psgFilter, ref bassFilterLeft, ref bassFilterRight, ref filterLeft, ref filterRight, psgFilterAlpha, bassShelfAlpha, filterAlpha);
                break;
            case 0x66:
                ended = true;
                break;
            case 0x67:
            {
                if (data[position++] != 0x66)
                {
                    throw new InvalidDataException("Malformed VGM data block.");
                }

                byte type = data[position++];
                int size = (int)ReadU32(data, position);
                position += 4;
                if (type == 0x00)
                {
                    pcmData = data.AsSpan(position, size).ToArray();
                    pcmOffset = 0;
                }

                position += size;
                break;
            }
            case 0xE0:
                pcmOffset = (int)ReadU32(data, position);
                position += 4;
                break;
            default:
                if (command >= 0x70 && command <= 0x7F)
                {
                    int wait = (command & 0x0F) + 1;
                    AppendVgmMixedSamples(samples, ym, psg, Math.Min(wait, maxSamples - (samples.Count / AudioConstants.StereoChannels)), ref psgFilter, ref bassFilterLeft, ref bassFilterRight, ref filterLeft, ref filterRight, psgFilterAlpha, bassShelfAlpha, filterAlpha);
                }
                else if (command >= 0x80 && command <= 0x8F)
                {
                    if ((uint)pcmOffset < pcmData.Length)
                    {
                        ym.WriteAddress(0, 0x2A);
                        ym.WriteData(0, pcmData[pcmOffset++]);
                    }

                    int wait = command & 0x0F;
                    AppendVgmMixedSamples(samples, ym, psg, Math.Min(wait, maxSamples - (samples.Count / AudioConstants.StereoChannels)), ref psgFilter, ref bassFilterLeft, ref bassFilterRight, ref filterLeft, ref filterRight, psgFilterAlpha, bassShelfAlpha, filterAlpha);
                }
                else
                {
                    throw new NotSupportedException($"Unsupported VGM command ${command:X2} at offset ${position - 1:X}");
                }

                break;
        }
    }

    WriteWav(outputPath, samples.ToArray(), AudioConstants.DefaultSampleRate, AudioConstants.StereoChannels);
    Console.WriteLine($"Rendered {samples.Count / AudioConstants.StereoChannels:N0} VGM sample(s) to {Path.GetFullPath(outputPath)}");
}

void RenderVgmStems(string vgmPath, string outputFolder, double? maxSeconds)
{
    byte[] data = LoadVgmBytes(vgmPath);
    if (data.Length < 0x40 || data[0] != 'V' || data[1] != 'g' || data[2] != 'm' || data[3] != ' ')
    {
        throw new InvalidDataException("Input is not a VGM/VGZ file.");
    }

    string outputRoot = Path.GetFullPath(outputFolder);
    Directory.CreateDirectory(outputRoot);
    int dataOffset = 0x40;
    uint version = ReadU32(data, 0x08);
    if (version >= 0x00000150 && data.Length >= 0x38)
    {
        dataOffset = 0x34 + (int)ReadU32(data, 0x34);
    }

    int maxSamples = maxSeconds is > 0.0
        ? Math.Max(1, (int)Math.Round(maxSeconds.Value * AudioConstants.DefaultSampleRate))
        : int.MaxValue;
    string[] names = ["ym1", "ym2", "ym3", "ym4", "ym5", "ym6-dac", "psg1", "psg2", "psg3", "psg-noise", "mixed"];
    List<short>[] stems = names.Select(_ => new List<short>(Math.Min(maxSamples, AudioConstants.DefaultSampleRate * 120) * AudioConstants.StereoChannels)).ToArray();
    Ym2612 ym = new();
    Psg psg = new();
    byte[] pcmData = [];
    int pcmOffset = 0;
    double[] filterLeft = new double[names.Length];
    double[] filterRight = new double[names.Length];
    double[] psgPreFilter = new double[4];
    double[] bassFilterLeft = new double[names.Length];
    double[] bassFilterRight = new double[names.Length];
    double filterAlpha = AudioOutputLowPassAlpha(AudioConstants.DefaultSampleRate);
    double psgFilterAlpha = AudioPsgLowPassAlpha(AudioConstants.DefaultSampleRate);
    double bassShelfAlpha = AudioBassShelfAlpha(AudioConstants.DefaultSampleRate);
    int position = dataOffset;
    bool ended = false;

    while (position < data.Length && !ended && stems[^1].Count / AudioConstants.StereoChannels < maxSamples)
    {
        byte command = data[position++];
        switch (command)
        {
            case 0x4F:
                position++;
                break;
            case 0x50:
                psg.Write(data[position++]);
                break;
            case 0x52:
            case 0x53:
            {
                int port = command == 0x53 ? 1 : 0;
                byte address = data[position++];
                byte value = data[position++];
                ym.WriteAddress(port, address);
                ym.WriteData(port, value);
                break;
            }
            case 0x61:
            {
                int wait = ReadU16(data, position);
                position += 2;
                AppendVgmStemSamples(stems, ym, psg, Math.Min(wait, maxSamples - (stems[^1].Count / AudioConstants.StereoChannels)), psgPreFilter, bassFilterLeft, bassFilterRight, filterLeft, filterRight, psgFilterAlpha, bassShelfAlpha, filterAlpha);
                break;
            }
            case 0x62:
                AppendVgmStemSamples(stems, ym, psg, Math.Min(735, maxSamples - (stems[^1].Count / AudioConstants.StereoChannels)), psgPreFilter, bassFilterLeft, bassFilterRight, filterLeft, filterRight, psgFilterAlpha, bassShelfAlpha, filterAlpha);
                break;
            case 0x63:
                AppendVgmStemSamples(stems, ym, psg, Math.Min(882, maxSamples - (stems[^1].Count / AudioConstants.StereoChannels)), psgPreFilter, bassFilterLeft, bassFilterRight, filterLeft, filterRight, psgFilterAlpha, bassShelfAlpha, filterAlpha);
                break;
            case 0x66:
                ended = true;
                break;
            case 0x67:
            {
                if (data[position++] != 0x66)
                {
                    throw new InvalidDataException("Malformed VGM data block.");
                }

                byte type = data[position++];
                int size = (int)ReadU32(data, position);
                position += 4;
                if (type == 0x00)
                {
                    pcmData = data.AsSpan(position, size).ToArray();
                    pcmOffset = 0;
                }

                position += size;
                break;
            }
            case 0xE0:
                pcmOffset = (int)ReadU32(data, position);
                position += 4;
                break;
            default:
                if (command >= 0x70 && command <= 0x7F)
                {
                    int wait = (command & 0x0F) + 1;
                    AppendVgmStemSamples(stems, ym, psg, Math.Min(wait, maxSamples - (stems[^1].Count / AudioConstants.StereoChannels)), psgPreFilter, bassFilterLeft, bassFilterRight, filterLeft, filterRight, psgFilterAlpha, bassShelfAlpha, filterAlpha);
                }
                else if (command >= 0x80 && command <= 0x8F)
                {
                    if ((uint)pcmOffset < pcmData.Length)
                    {
                        ym.WriteAddress(0, 0x2A);
                        ym.WriteData(0, pcmData[pcmOffset++]);
                    }

                    int wait = command & 0x0F;
                    AppendVgmStemSamples(stems, ym, psg, Math.Min(wait, maxSamples - (stems[^1].Count / AudioConstants.StereoChannels)), psgPreFilter, bassFilterLeft, bassFilterRight, filterLeft, filterRight, psgFilterAlpha, bassShelfAlpha, filterAlpha);
                }
                else
                {
                    throw new NotSupportedException($"Unsupported VGM command ${command:X2} at offset ${position - 1:X}");
                }

                break;
        }
    }

    string manifestPath = Path.Combine(outputRoot, "vgm-stems.csv");
    string reportPath = Path.Combine(outputRoot, "vgm-stem-bands.md");
    using StreamWriter manifest = new(manifestPath, false, Encoding.UTF8);
    manifest.WriteLine("stem,wav,rmsDb,peakDb,brightnessDb,bassDb,bodyDb,melodyDb,sparkleDb,topNotes");

    using StreamWriter report = new(reportPath, false, Encoding.UTF8);
    report.WriteLine("# VGM Audio Stems");
    report.WriteLine();
    report.WriteLine($"Source: `{Path.GetFullPath(vgmPath)}`");
    report.WriteLine($"Samples: {(stems[^1].Count / AudioConstants.StereoChannels).ToString(CultureInfo.InvariantCulture)}");
    report.WriteLine();
    report.WriteLine("| Stem | RMS | Peak | Brightness | Bass | Body | Melody | Sparkle | Top notes |");
    report.WriteLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |");
    for (int stem = 0; stem < names.Length; stem++)
    {
        string wavPath = Path.Combine(outputRoot, $"vgm-{names[stem]}.wav");
        short[] stemSamples = stems[stem].ToArray();
        WriteWav(wavPath, stemSamples, AudioConstants.DefaultSampleRate, AudioConstants.StereoChannels);
        double[] mono = ToMono(stemSamples, 0, stemSamples.Length / AudioConstants.StereoChannels);
        AudioSignalStats stats = AnalyzeAudioSignal(mono, 0, mono.Length);
        AudioBandStats bands = AnalyzeAudioBands(mono, 0, mono.Length);
        string topNotes = FormatTopMusicalFrequencies(mono, 0, mono.Length, 5);
        manifest.WriteLine(string.Join(',',
            Csv(names[stem]),
            Csv(Path.GetFullPath(wavPath)),
            F(stats.RmsDb),
            F(stats.PeakDb),
            F(stats.BrightnessDb),
            F(bands.BassDb),
            F(bands.BodyDb),
            F(bands.MelodyDb),
            F(bands.SparkleDb),
            Csv(topNotes)));
        report.WriteLine($"| {names[stem]} | {F(stats.RmsDb)} | {F(stats.PeakDb)} | {F(stats.BrightnessDb)} | {F(bands.BassDb)} | {F(bands.BodyDb)} | {F(bands.MelodyDb)} | {F(bands.SparkleDb)} | {EscapeMarkdown(topNotes)} |");
    }

    Console.WriteLine($"Wrote VGM stems to {outputRoot}");
    Console.WriteLine($"Wrote VGM stem report to {reportPath}");
}

byte[] LoadVgmBytes(string path)
{
    byte[] data = File.ReadAllBytes(path);
    if (Path.GetExtension(path).Equals(".vgz", StringComparison.OrdinalIgnoreCase))
    {
        using MemoryStream input = new(data);
        using GZipStream gzip = new(input, CompressionMode.Decompress);
        using MemoryStream output = new();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    return data;
}

void AppendVgmMixedSamples(List<short> destination, Ym2612 ym, Psg psg, int count, ref double psgFilter, ref double bassFilterLeft, ref double bassFilterRight, ref double filterLeft, ref double filterRight, double psgFilterAlpha, double bassShelfAlpha, double filterAlpha)
{
    if (count <= 0)
    {
        return;
    }

    short[] ymSamples = ym.RenderStereoSamples(count);
    short[] psgSamples = psg.RenderMonoSamples(count);
    for (int i = 0; i < count; i++)
    {
        psgFilter += (psgSamples[i] - psgFilter) * psgFilterAlpha;
        double psgSample = psgFilter * AudioConstants.PsgMixLevel;
        double left = psgSample + (ymSamples[i * 2] * AudioConstants.YmMixLevel);
        double right = psgSample + (ymSamples[(i * 2) + 1] * AudioConstants.YmMixLevel);
        bassFilterLeft += (left - bassFilterLeft) * bassShelfAlpha;
        bassFilterRight += (right - bassFilterRight) * bassShelfAlpha;
        left += bassFilterLeft * AudioConstants.BassShelfGain;
        right += bassFilterRight * AudioConstants.BassShelfGain;
        filterLeft += (left - filterLeft) * filterAlpha;
        filterRight += (right - filterRight) * filterAlpha;
        destination.Add(AudioConstants.LimitOutputSample(filterLeft * AudioConstants.MasterMixLevel));
        destination.Add(AudioConstants.LimitOutputSample(filterRight * AudioConstants.MasterMixLevel));
    }
}

void AppendVgmStemSamples(List<short>[] stems, Ym2612 ym, Psg psg, int count, double[] psgPreFilter, double[] bassFilterLeft, double[] bassFilterRight, double[] filterLeft, double[] filterRight, double psgFilterAlpha, double bassShelfAlpha, double filterAlpha)
{
    if (count <= 0)
    {
        return;
    }

    short[] ymSamples = new short[count * 6 * AudioConstants.StereoChannels];
    short[] psgSamples = new short[count * 4];
    ym.RenderStereoChannelStemsInto(ymSamples, count);
    psg.RenderMonoChannelStemsInto(psgSamples, count);
    for (int sample = 0; sample < count; sample++)
    {
        double mixedLeft = 0.0;
        double mixedRight = 0.0;
        for (int channel = 0; channel < 6; channel++)
        {
            int offset = ((channel * count) + sample) * AudioConstants.StereoChannels;
            double ymMix = AudioConstants.YmMixLevel * AudioConstants.YmChannelMixLevel(channel);
            double rawLeft = ymSamples[offset] * ymMix;
            double rawRight = ymSamples[offset + 1] * ymMix;
            bassFilterLeft[channel] += (rawLeft - bassFilterLeft[channel]) * bassShelfAlpha;
            bassFilterRight[channel] += (rawRight - bassFilterRight[channel]) * bassShelfAlpha;
            rawLeft += bassFilterLeft[channel] * AudioConstants.BassShelfGain;
            rawRight += bassFilterRight[channel] * AudioConstants.BassShelfGain;
            filterLeft[channel] += (rawLeft - filterLeft[channel]) * filterAlpha;
            filterRight[channel] += (rawRight - filterRight[channel]) * filterAlpha;
            short left = AudioConstants.ClampSample(filterLeft[channel] * AudioConstants.MasterMixLevel);
            short right = AudioConstants.ClampSample(filterRight[channel] * AudioConstants.MasterMixLevel);
            stems[channel].Add(left);
            stems[channel].Add(right);
            mixedLeft += left;
            mixedRight += right;
        }

        for (int channel = 0; channel < 4; channel++)
        {
            int stem = 6 + channel;
            int offset = (channel * count) + sample;
            psgPreFilter[channel] += (psgSamples[offset] - psgPreFilter[channel]) * psgFilterAlpha;
            double raw = psgPreFilter[channel] * AudioConstants.PsgMixLevel * AudioConstants.PsgChannelMixLevel(channel);
            bassFilterLeft[stem] += (raw - bassFilterLeft[stem]) * bassShelfAlpha;
            bassFilterRight[stem] += (raw - bassFilterRight[stem]) * bassShelfAlpha;
            double rawLeft = raw + (bassFilterLeft[stem] * AudioConstants.BassShelfGain);
            double rawRight = raw + (bassFilterRight[stem] * AudioConstants.BassShelfGain);
            filterLeft[stem] += (rawLeft - filterLeft[stem]) * filterAlpha;
            filterRight[stem] += (rawRight - filterRight[stem]) * filterAlpha;
            short left = AudioConstants.ClampSample(filterLeft[stem] * AudioConstants.MasterMixLevel);
            short right = AudioConstants.ClampSample(filterRight[stem] * AudioConstants.MasterMixLevel);
            stems[stem].Add(left);
            stems[stem].Add(right);
            mixedLeft += left;
            mixedRight += right;
        }

        stems[^1].Add(AudioConstants.LimitOutputSample(mixedLeft));
        stems[^1].Add(AudioConstants.LimitOutputSample(mixedRight));
    }
}

static ushort ReadU16(byte[] data, int offset)
{
    return (ushort)(data[offset] | (data[offset + 1] << 8));
}

static uint ReadU32(byte[] data, int offset)
{
    return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
}

static double AudioOutputLowPassAlpha(int sampleRate)
{
    double normalized = -2.0 * Math.PI * AudioConstants.OutputLowPassCutoffHz / Math.Max(1, sampleRate);
    return Math.Clamp(1.0 - Math.Exp(normalized), 0.0, 1.0);
}

static double AudioPsgLowPassAlpha(int sampleRate)
{
    double normalized = -2.0 * Math.PI * AudioConstants.PsgLowPassCutoffHz / Math.Max(1, sampleRate);
    return Math.Clamp(1.0 - Math.Exp(normalized), 0.0, 1.0);
}

static double AudioBassShelfAlpha(int sampleRate)
{
    double normalized = -2.0 * Math.PI * AudioConstants.BassShelfCutoffHz / Math.Max(1, sampleRate);
    return Math.Clamp(1.0 - Math.Exp(normalized), 0.0, 1.0);
}

void RenderYmScript(string scriptPath, string outputPath)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
    Ym2612 ym = new();
    List<short> samples = new();
    int lineNumber = 0;
    foreach (string rawLine in File.ReadLines(scriptPath))
    {
        lineNumber++;
        string line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
        {
            continue;
        }

        string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        string command = parts[0].ToLowerInvariant();
        if (command is "write" or "w")
        {
            if (parts.Length < 3)
            {
                throw new InvalidOperationException($"Invalid YM write command at line {lineNumber}.");
            }

            int port = (int)ParseNumber(parts[1]) & 0x03;
            byte value = (byte)ParseNumber(parts[2]);
            if ((port & 1) == 0)
            {
                ym.WriteAddress(port >> 1, value);
            }
            else
            {
                ym.WriteData(port >> 1, value);
            }
        }
        else if (command is "render" or "r")
        {
            if (parts.Length < 2)
            {
                throw new InvalidOperationException($"Invalid YM render command at line {lineNumber}.");
            }

            int count = Math.Max(1, (int)ParseNumber(parts[1]));
            samples.AddRange(ym.RenderStereoSamples(count));
        }
        else if (command == "reset")
        {
            ym.Reset();
        }
        else
        {
            throw new InvalidOperationException($"Unknown YM script command '{parts[0]}' at line {lineNumber}.");
        }
    }

    WriteWav(outputPath, samples.ToArray(), AudioConstants.DefaultSampleRate, AudioConstants.StereoChannels);
    Console.WriteLine($"Wrote {samples.Count / AudioConstants.StereoChannels:N0} YM script sample(s) to {Path.GetFullPath(outputPath)}");
}

short[] DumpSonicAudio(string romPath, string outputPath, string energyPath, int frames, int instructionsPerFrame, string preset)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(energyPath)) ?? ".");

    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();
    List<short> samples = new();
    using StreamWriter writer = new(energyPath, false, Encoding.UTF8);
    writer.WriteLine("frame,pc,mixedEnergy,dacEnabled,dacSample,ym1,ym2,ym3,ym4,ym5,ym6,key1,key2,key3,key4,key5,key6,alg1,alg2,alg3,alg4,alg5,alg6,feedback1,feedback2,feedback3,feedback4,feedback5,feedback6,pms1,pms2,pms3,pms4,pms5,pms6,ams1,ams2,ams3,ams4,ams5,ams6,fnum1,fnum2,fnum3,fnum4,fnum5,fnum6,block1,block2,block3,block4,block5,block6,carrierTl1,carrierTl2,carrierTl3,carrierTl4,carrierTl5,carrierTl6,carrierEnv1,carrierEnv2,carrierEnv3,carrierEnv4,carrierEnv5,carrierEnv6,carrierStage1,carrierStage2,carrierStage3,carrierStage4,carrierStage5,carrierStage6");

    for (int frame = 0; frame < frames; frame++)
    {
        GenesisButton pressed = SonicAudioInput(preset, frame);
        machine.Bus.Controller1.Pressed = pressed;
        machine.Bus.Controller2.Pressed = pressed;
        machine.RunFrameCycles(instructionsPerFrame);

        long[] ymEnergy = new long[6];
        short[] frameSamples = machine.RenderFrameStereoAudioSamples(ymChannelEnergy: ymEnergy);
        long mixedEnergy = 0;
        for (int i = 0; i < frameSamples.Length; i += 32)
        {
            mixedEnergy += Math.Abs((int)frameSamples[i]);
        }

        samples.AddRange(frameSamples);
        Ym2612.Ym2612ChannelSnapshot[] snapshots = machine.Ym2612.GetChannelSnapshots();
        string keyColumns = string.Join(',', snapshots.Select(snapshot => $"${snapshot.KeyOn:X1}"));
        string algorithmColumns = string.Join(',', snapshots.Select(snapshot => snapshot.Algorithm.ToString()));
        string feedbackColumns = string.Join(',', snapshots.Select(snapshot => snapshot.Feedback.ToString()));
        string pmsColumns = string.Join(',', snapshots.Select(snapshot => snapshot.PhaseModulationSensitivity.ToString()));
        string amsColumns = string.Join(',', snapshots.Select(snapshot => snapshot.AmplitudeModulationSensitivity.ToString()));
        string fnumColumns = string.Join(',', snapshots.Select(snapshot => snapshot.FNumber.ToString()));
        string blockColumns = string.Join(',', snapshots.Select(snapshot => snapshot.Block.ToString()));
        string carrierTlColumns = string.Join(',', snapshots.Select(CarrierTotalLevel));
        string carrierEnvColumns = string.Join(',', snapshots.Select(CarrierEnvelope));
        string carrierStageColumns = string.Join(',', snapshots.Select(CarrierStage));
        writer.WriteLine($"{frame},${machine.MainCpu.PC:X8},{mixedEnergy},{(machine.Ym2612.DacEnabled ? 1 : 0)},${machine.Ym2612.DacSample:X2},{ymEnergy[0]},{ymEnergy[1]},{ymEnergy[2]},{ymEnergy[3]},{ymEnergy[4]},{ymEnergy[5]},{keyColumns},{algorithmColumns},{feedbackColumns},{pmsColumns},{amsColumns},{fnumColumns},{blockColumns},{carrierTlColumns},{carrierEnvColumns},{carrierStageColumns}");
    }

    machine.Bus.Controller1.Pressed = GenesisButton.None;
    machine.Bus.Controller2.Pressed = GenesisButton.None;
    short[] renderedSamples = samples.ToArray();
    WriteWav(outputPath, renderedSamples, 44_100, 2);
    Console.WriteLine($"Wrote Sonic {preset} audio to {Path.GetFullPath(outputPath)}");
    Console.WriteLine($"Wrote YM channel energy to {Path.GetFullPath(energyPath)}");
    return renderedSamples;

    static GenesisButton SonicAudioInput(string preset, int frame)
    {
        if (preset.Equals("title", StringComparison.OrdinalIgnoreCase))
        {
            return GenesisButton.None;
        }

        GenesisButton buttons = frame is >= 620 and < 660 ? GenesisButton.Start : GenesisButton.None;
        if (preset.Equals("gameplay", StringComparison.OrdinalIgnoreCase) && frame >= 950)
        {
            buttons |= GenesisButton.Right;
            if (frame >= 1100 && frame % 120 < 18)
            {
                buttons |= GenesisButton.C;
            }
        }

        return buttons;
    }

    static int CarrierTotalLevel(Ym2612.Ym2612ChannelSnapshot snapshot)
    {
        int total = 0;
        foreach (int op in CarrierOperators(snapshot.Algorithm))
        {
            total += snapshot.TotalLevels[op];
        }

        return total;
    }

    static int CarrierEnvelope(Ym2612.Ym2612ChannelSnapshot snapshot)
    {
        int total = 0;
        foreach (int op in CarrierOperators(snapshot.Algorithm))
        {
            total += 1024 - snapshot.Envelopes[op];
        }

        return total;
    }

    static int CarrierStage(Ym2612.Ym2612ChannelSnapshot snapshot)
    {
        int stage = 0;
        foreach (int op in CarrierOperators(snapshot.Algorithm))
        {
            stage = Math.Max(stage, snapshot.Stages[op]);
        }

        return stage;
    }

    static int[] CarrierOperators(int algorithm)
    {
        return algorithm switch
        {
            4 => [2, 3],
            5 or 6 => [1, 2, 3],
            7 => [0, 1, 2, 3],
            _ => [3],
        };
    }
}

SonicAudioCompareResult RunSonicAudioCompare(string romPath, string referencePath, string outputFolder, string preset, int frames, int instructionsPerFrame, double? alignmentWindowSeconds = null, double? referenceStartSeconds = null, double? emulatedStartSeconds = null)
{
    Directory.CreateDirectory(outputFolder);
    string outputRoot = Path.GetFullPath(outputFolder);
    string emulatedWav = Path.Combine(outputRoot, $"sonic-{preset}-emulated.wav");
    string energyCsv = Path.Combine(outputRoot, $"sonic-{preset}-ym-energy.csv");
    string reportCsv = Path.Combine(outputRoot, $"sonic-{preset}-audio-compare.csv");
    string reportMarkdown = Path.Combine(outputRoot, $"sonic-{preset}-audio-compare.md");
    string envelopeCsv = Path.Combine(outputRoot, $"sonic-{preset}-aligned-envelope.csv");

    SonicAudioRender render = RenderSonicAudioForCompare(romPath, emulatedWav, energyCsv, preset, frames, instructionsPerFrame);
    string rawReferencePath = Path.Combine(outputRoot, $"sonic-{preset}-reference.raw");
    short[] referenceSamples = DecodeReferenceAudio(referencePath, rawReferencePath);

    double[] referenceMono = ToMono(referenceSamples, 0, referenceSamples.Length / AudioConstants.StereoChannels);
    double[] emulatedMono = ToMono(render.Samples, render.CompareStartSample, (render.Samples.Length / AudioConstants.StereoChannels) - render.CompareStartSample);

    const int envelopeWindow = 1024;
    const int envelopeHop = 512;
    double[][] referenceFeatures = BuildAudioAlignmentFeatures(referenceMono, envelopeWindow, envelopeHop);
    double[][] emulatedFeatures = BuildAudioAlignmentFeatures(emulatedMono, envelopeWindow, envelopeHop);
    double[] referenceEnvelope = referenceFeatures[0];
    double[] emulatedEnvelope = emulatedFeatures[0];
    AudioAlignment alignment = FindBestAudioAlignment(referenceFeatures, emulatedFeatures, AudioConstants.DefaultSampleRate, envelopeHop, alignmentWindowSeconds, referenceStartSeconds, emulatedStartSeconds);

    int referenceStart = Math.Clamp((int)Math.Round(alignment.ReferenceOffsetSeconds * AudioConstants.DefaultSampleRate), 0, Math.Max(0, referenceMono.Length - 1));
    int emulatedStart = Math.Clamp((int)Math.Round(alignment.EmulatedOffsetSeconds * AudioConstants.DefaultSampleRate), 0, Math.Max(0, emulatedMono.Length - 1));
    int analysisSamples = Math.Min(
        (int)Math.Round(alignment.WindowSeconds * AudioConstants.DefaultSampleRate),
        Math.Min(referenceMono.Length - referenceStart, emulatedMono.Length - emulatedStart));

    AudioSignalStats referenceStats = AnalyzeAudioSignal(referenceMono, referenceStart, analysisSamples);
    AudioSignalStats emulatedStats = AnalyzeAudioSignal(emulatedMono, emulatedStart, analysisSamples);
    AudioBandStats referenceBands = AnalyzeAudioBands(referenceMono, referenceStart, analysisSamples);
    AudioBandStats emulatedBands = AnalyzeAudioBands(emulatedMono, emulatedStart, analysisSamples);
    string referenceTopNotes = FormatTopMusicalFrequencies(referenceMono, referenceStart, analysisSamples, 6);
    string emulatedTopNotes = FormatTopMusicalFrequencies(emulatedMono, emulatedStart, analysisSamples, 6);
    WriteAudioCompareCsv(reportCsv, preset, frames, instructionsPerFrame, render.CompareStartFrame, alignment, referenceStats, emulatedStats, referenceBands, emulatedBands, emulatedWav, energyCsv, referencePath);
    WriteAudioCompareMarkdown(reportMarkdown, preset, frames, instructionsPerFrame, render.CompareStartFrame, alignment, referenceStats, emulatedStats, referenceBands, emulatedBands, emulatedWav, energyCsv, envelopeCsv, referencePath, referenceTopNotes, emulatedTopNotes);
    WriteAlignedEnvelopeCsv(envelopeCsv, referenceEnvelope, emulatedEnvelope, alignment, AudioConstants.DefaultSampleRate, envelopeHop);

    Console.WriteLine($"Wrote emulated Sonic {preset} audio to {emulatedWav}");
    Console.WriteLine($"Wrote YM energy to {energyCsv}");
    Console.WriteLine($"Wrote audio comparison report to {reportMarkdown}");
    return new SonicAudioCompareResult(
        preset,
        frames,
        instructionsPerFrame,
        render.CompareStartFrame,
        alignment,
        referenceStats,
        emulatedStats,
        referenceBands,
        emulatedBands,
        emulatedWav,
        energyCsv,
        reportCsv,
        reportMarkdown,
        envelopeCsv,
        referencePath);
}

SonicAudioCompareResult RunGenericAudioCompare(string id, string romPath, string referencePath, string outputFolder, int frames, int instructionsPerFrame, int compareStartFrame = 0, double? alignmentWindowSeconds = null, double? referenceStartSeconds = null, double? emulatedStartSeconds = null)
{
    Directory.CreateDirectory(outputFolder);
    string outputRoot = Path.GetFullPath(outputFolder);
    string emulatedWav = Path.Combine(outputRoot, $"{id}-emulated.wav");
    string reportCsv = Path.Combine(outputRoot, $"{id}-audio-compare.csv");
    string reportMarkdown = Path.Combine(outputRoot, $"{id}-audio-compare.md");
    string envelopeCsv = Path.Combine(outputRoot, $"{id}-aligned-envelope.csv");

    (short[] emulatedSamples, int compareStartSample) = RenderGenericAudioForCompare(romPath, emulatedWav, frames, instructionsPerFrame, compareStartFrame);
    string rawReferencePath = Path.Combine(outputRoot, $"{id}-reference.raw");
    short[] referenceSamples = DecodeReferenceAudio(referencePath, rawReferencePath);

    double[] referenceMono = ToMono(referenceSamples, 0, referenceSamples.Length / AudioConstants.StereoChannels);
    double[] emulatedMono = ToMono(emulatedSamples, compareStartSample, (emulatedSamples.Length / AudioConstants.StereoChannels) - compareStartSample);

    const int envelopeWindow = 1024;
    const int envelopeHop = 512;
    double[][] referenceFeatures = BuildAudioAlignmentFeatures(referenceMono, envelopeWindow, envelopeHop);
    double[][] emulatedFeatures = BuildAudioAlignmentFeatures(emulatedMono, envelopeWindow, envelopeHop);
    double[] referenceEnvelope = referenceFeatures[0];
    double[] emulatedEnvelope = emulatedFeatures[0];
    AudioAlignment alignment = FindBestAudioAlignment(referenceFeatures, emulatedFeatures, AudioConstants.DefaultSampleRate, envelopeHop, alignmentWindowSeconds, referenceStartSeconds, emulatedStartSeconds);

    int referenceStart = Math.Clamp((int)Math.Round(alignment.ReferenceOffsetSeconds * AudioConstants.DefaultSampleRate), 0, Math.Max(0, referenceMono.Length - 1));
    int emulatedStart = Math.Clamp((int)Math.Round(alignment.EmulatedOffsetSeconds * AudioConstants.DefaultSampleRate), 0, Math.Max(0, emulatedMono.Length - 1));
    int analysisSamples = Math.Min(
        (int)Math.Round(alignment.WindowSeconds * AudioConstants.DefaultSampleRate),
        Math.Min(referenceMono.Length - referenceStart, emulatedMono.Length - emulatedStart));

    AudioSignalStats referenceStats = AnalyzeAudioSignal(referenceMono, referenceStart, analysisSamples);
    AudioSignalStats emulatedStats = AnalyzeAudioSignal(emulatedMono, emulatedStart, analysisSamples);
    AudioBandStats referenceBands = AnalyzeAudioBands(referenceMono, referenceStart, analysisSamples);
    AudioBandStats emulatedBands = AnalyzeAudioBands(emulatedMono, emulatedStart, analysisSamples);
    string referenceTopNotes = FormatTopMusicalFrequencies(referenceMono, referenceStart, analysisSamples, 6);
    string emulatedTopNotes = FormatTopMusicalFrequencies(emulatedMono, emulatedStart, analysisSamples, 6);
    WriteAudioCompareCsv(reportCsv, id, frames, instructionsPerFrame, compareStartFrame, alignment, referenceStats, emulatedStats, referenceBands, emulatedBands, emulatedWav, string.Empty, referencePath);
    WriteAudioCompareMarkdown(reportMarkdown, id, frames, instructionsPerFrame, compareStartFrame, alignment, referenceStats, emulatedStats, referenceBands, emulatedBands, emulatedWav, string.Empty, envelopeCsv, referencePath, referenceTopNotes, emulatedTopNotes);
    WriteAlignedEnvelopeCsv(envelopeCsv, referenceEnvelope, emulatedEnvelope, alignment, AudioConstants.DefaultSampleRate, envelopeHop);

    Console.WriteLine($"Wrote emulated {id} audio to {emulatedWav}");
    Console.WriteLine($"Wrote audio comparison report to {reportMarkdown}");
    return new SonicAudioCompareResult(
        id,
        frames,
        instructionsPerFrame,
        compareStartFrame,
        alignment,
        referenceStats,
        emulatedStats,
        referenceBands,
        emulatedBands,
        emulatedWav,
        string.Empty,
        reportCsv,
        reportMarkdown,
        envelopeCsv,
        referencePath);
}

void RunAudioFileCompare(string id, string referencePath, string emulatedPath, string outputFolder, double? alignmentWindowSeconds, double? referenceStartSeconds = null, double? emulatedStartSeconds = null)
{
    Directory.CreateDirectory(outputFolder);
    string outputRoot = Path.GetFullPath(outputFolder);
    string reportCsv = Path.Combine(outputRoot, $"{id}-audio-file-compare.csv");
    string reportMarkdown = Path.Combine(outputRoot, $"{id}-audio-file-compare.md");
    string envelopeCsv = Path.Combine(outputRoot, $"{id}-aligned-envelope.csv");
    string rawReferencePath = Path.Combine(outputRoot, $"{id}-reference.raw");
    string rawEmulatedPath = Path.Combine(outputRoot, $"{id}-emulated.raw");

    short[] referenceSamples = DecodeReferenceAudio(referencePath, rawReferencePath);
    short[] emulatedSamples = DecodeReferenceAudio(emulatedPath, rawEmulatedPath);
    double[] referenceMono = ToMono(referenceSamples, 0, referenceSamples.Length / AudioConstants.StereoChannels);
    double[] emulatedMono = ToMono(emulatedSamples, 0, emulatedSamples.Length / AudioConstants.StereoChannels);

    const int envelopeWindow = 1024;
    const int envelopeHop = 512;
    double[][] referenceFeatures = BuildAudioAlignmentFeatures(referenceMono, envelopeWindow, envelopeHop);
    double[][] emulatedFeatures = BuildAudioAlignmentFeatures(emulatedMono, envelopeWindow, envelopeHop);
    double[] referenceEnvelope = referenceFeatures[0];
    double[] emulatedEnvelope = emulatedFeatures[0];
    AudioAlignment alignment = FindBestAudioAlignment(referenceFeatures, emulatedFeatures, AudioConstants.DefaultSampleRate, envelopeHop, alignmentWindowSeconds, referenceStartSeconds, emulatedStartSeconds);

    int referenceStart = Math.Clamp((int)Math.Round(alignment.ReferenceOffsetSeconds * AudioConstants.DefaultSampleRate), 0, Math.Max(0, referenceMono.Length - 1));
    int emulatedStart = Math.Clamp((int)Math.Round(alignment.EmulatedOffsetSeconds * AudioConstants.DefaultSampleRate), 0, Math.Max(0, emulatedMono.Length - 1));
    int analysisSamples = Math.Min(
        (int)Math.Round(alignment.WindowSeconds * AudioConstants.DefaultSampleRate),
        Math.Min(referenceMono.Length - referenceStart, emulatedMono.Length - emulatedStart));

    AudioSignalStats referenceStats = AnalyzeAudioSignal(referenceMono, referenceStart, analysisSamples);
    AudioSignalStats emulatedStats = AnalyzeAudioSignal(emulatedMono, emulatedStart, analysisSamples);
    AudioBandStats referenceBands = AnalyzeAudioBands(referenceMono, referenceStart, analysisSamples);
    AudioBandStats emulatedBands = AnalyzeAudioBands(emulatedMono, emulatedStart, analysisSamples);
    string referenceTopNotes = FormatTopMusicalFrequencies(referenceMono, referenceStart, analysisSamples, 6);
    string emulatedTopNotes = FormatTopMusicalFrequencies(emulatedMono, emulatedStart, analysisSamples, 6);
    WriteAudioCompareCsv(reportCsv, id, 0, 0, 0, alignment, referenceStats, emulatedStats, referenceBands, emulatedBands, emulatedPath, string.Empty, referencePath);
    WriteAudioCompareMarkdown(reportMarkdown, id, 0, 0, 0, alignment, referenceStats, emulatedStats, referenceBands, emulatedBands, emulatedPath, string.Empty, envelopeCsv, referencePath, referenceTopNotes, emulatedTopNotes);
    WriteAlignedEnvelopeCsv(envelopeCsv, referenceEnvelope, emulatedEnvelope, alignment, AudioConstants.DefaultSampleRate, envelopeHop);

    Console.WriteLine($"Wrote audio file comparison report to {reportMarkdown}");
}

(short[] Samples, int CompareStartSample) RenderGenericAudioForCompare(string romPath, string outputPath, int frames, int instructionsPerFrame, int compareStartFrame)
{
    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();
    List<short> samples = new();
    int compareStartSample = 0;
    for (int frame = 0; frame < frames; frame++)
    {
        if (frame == compareStartFrame)
        {
            compareStartSample = samples.Count / AudioConstants.StereoChannels;
        }

        machine.RunFrameCycles(instructionsPerFrame);
        samples.AddRange(machine.RenderFrameStereoAudioSamples());
    }

    short[] renderedSamples = samples.ToArray();
    WriteWav(outputPath, renderedSamples, AudioConstants.DefaultSampleRate, AudioConstants.StereoChannels);
    Console.WriteLine($"Wrote {samples.Count / AudioConstants.StereoChannels:N0} stereo audio frame(s) to {Path.GetFullPath(outputPath)}");
    return (renderedSamples, compareStartSample);
}

void RunAudioRegressionSuite(string romFolder, string outputFolder, string? referenceAudio, int instructionsPerFrame)
{
    string fullRomFolder = Path.GetFullPath(romFolder);
    string outputRoot = Path.GetFullPath(outputFolder);
    Directory.CreateDirectory(outputRoot);
    string[] roms = EnumerateRomFiles(fullRomFolder);
    List<AudioRegressionRow> rows = [];
    string? sonicTitleReference = ResolveReferenceAudioByKeywords(
        ["01 - Title Theme - Masato Nakamura.flac", "sonic-title.flac", "sonic-title.wav", "sonic-title.mp3"],
        "sonic",
        "title");
    string? streetsReference = ResolveReferenceAudioByKeywords(
        ["streets-title.flac", "streets-title.wav", "streets-title.mp3", "streets-of-rage-title.flac", "streets-of-rage-title.wav", "streets-of-rage-title.mp3", "streets-intro.flac", "streets-intro.wav", "streets-intro.mp3"],
        "streets");

    string? sonic1 = ResolveAudioRom(roms, "sonic the hedgehog (usa)", "sonic the hedgehog");
    if (sonic1 is not null)
    {
        SonicAudioCheckpointSpec[] sonicCheckpoints =
        [
            new("title", 760),
            new("attract", 2200),
            new("greenhill", 2600),
            new("gameplay", 1800),
        ];

        foreach (SonicAudioCheckpointSpec checkpoint in sonicCheckpoints)
        {
            string caseFolder = Path.Combine(outputRoot, $"sonic1-{checkpoint.Preset}");
            Directory.CreateDirectory(caseFolder);
            string wav = Path.Combine(caseFolder, $"sonic1-{checkpoint.Preset}.wav");
            string energy = Path.Combine(caseFolder, $"sonic1-{checkpoint.Preset}-ym-energy.csv");
            short[] samples = DumpSonicAudio(sonic1, wav, energy, checkpoint.Frames, instructionsPerFrame, checkpoint.Preset);
            rows.Add(new AudioRegressionRow($"sonic1-{checkpoint.Preset}", Path.GetRelativePath(fullRomFolder, sonic1), checkpoint.Frames, wav, energy, string.Empty, SmokeAudioSummary(samples)));
        }

        if (sonicTitleReference is not null)
        {
            string compareFolder = Path.Combine(outputRoot, "sonic1-title-reference");
            SonicAudioCompareResult compare = RunSonicAudioCompare(sonic1, sonicTitleReference, compareFolder, "title", 760, instructionsPerFrame);
            rows.Add(new AudioRegressionRow(
                "sonic1-title-reference",
                Path.GetRelativePath(fullRomFolder, sonic1),
                760,
                compare.EmulatedWav,
                compare.EnergyCsv,
                compare.ReportMarkdown,
                FormatAudioRegressionMetrics(compare)));
        }

        if (referenceAudio is not null)
        {
            string compareFolder = Path.Combine(outputRoot, "sonic1-greenhill-reference");
            SonicAudioCompareResult compare = RunSonicAudioCompare(sonic1, referenceAudio, compareFolder, "greenhill", 2600, instructionsPerFrame);
            rows.Add(new AudioRegressionRow(
                "sonic1-greenhill-reference",
                Path.GetRelativePath(fullRomFolder, sonic1),
                2600,
                compare.EmulatedWav,
                compare.EnergyCsv,
                compare.ReportMarkdown,
                FormatAudioRegressionMetrics(compare)));
        }

        string stemsFolder = Path.Combine(outputRoot, "sonic1-greenhill-stems");
        RunSonicAudioStems(sonic1, stemsFolder, "greenhill", 2600, instructionsPerFrame);
        rows.Add(new AudioRegressionRow("sonic1-greenhill-stems", Path.GetRelativePath(fullRomFolder, sonic1), 2600, string.Empty, string.Empty, Path.Combine(stemsFolder, "sonic-greenhill-stem-bands.md"), "per-channel stems and PSG trace"));
    }

    AudioSmokeSpec[] smokeSpecs =
    [
        new("sonic2-title", ["sonic the hedgehog 2 (usa) (rev-b)", "sonic the hedgehog 2"], 900),
        new("sonic2-idle-demo", ["sonic the hedgehog 2 (usa) (rev-b)", "sonic the hedgehog 2"], 4200),
        new("streets-title", ["streets of rage (usa)", "streets of rage"], 900),
        new("bloodlines-title", ["castlevania - bloodlines"], 900),
        new("toy-story-intro", ["disney's toy story", "toy story"], 1200),
    ];

    foreach (AudioSmokeSpec spec in smokeSpecs)
    {
        string? rom = ResolveAudioRom(roms, spec.RomNameContains);
        if (rom is null)
        {
            rows.Add(new AudioRegressionRow(spec.Id, string.Empty, spec.Frames, string.Empty, string.Empty, string.Empty, "missing ROM"));
            continue;
        }

        string caseFolder = Path.Combine(outputRoot, spec.Id);
        Directory.CreateDirectory(caseFolder);
        string wav = Path.Combine(caseFolder, $"{spec.Id}.wav");
        short[] samples = DumpAudio(rom, wav, spec.Frames);
        rows.Add(new AudioRegressionRow(spec.Id, Path.GetRelativePath(fullRomFolder, rom), spec.Frames, wav, string.Empty, string.Empty, SmokeAudioSummary(samples)));

        if (spec.Id.Equals("streets-title", StringComparison.OrdinalIgnoreCase) && streetsReference is not null)
        {
            string compareFolder = Path.Combine(outputRoot, "streets-title-reference");
            SonicAudioCompareResult compare = RunGenericAudioCompare("streets-title", rom, streetsReference, compareFolder, spec.Frames, instructionsPerFrame);
            rows.Add(new AudioRegressionRow(
                "streets-title-reference",
                Path.GetRelativePath(fullRomFolder, rom),
                spec.Frames,
                compare.EmulatedWav,
                compare.EnergyCsv,
                compare.ReportMarkdown,
                FormatAudioRegressionMetrics(compare)));
        }
    }

    string manifestPath = Path.Combine(outputRoot, "audio-regression.csv");
    using StreamWriter manifest = new(manifestPath, false, Encoding.UTF8);
    manifest.WriteLine("id,rom,frames,wav,energyCsv,report,summary");
    foreach (AudioRegressionRow row in rows)
    {
        manifest.WriteLine(string.Join(',',
            Csv(row.Id),
            Csv(row.Rom),
            row.Frames.ToString(CultureInfo.InvariantCulture),
            Csv(string.IsNullOrWhiteSpace(row.Wav) ? string.Empty : Path.GetFullPath(row.Wav)),
            Csv(string.IsNullOrWhiteSpace(row.EnergyCsv) ? string.Empty : Path.GetFullPath(row.EnergyCsv)),
            Csv(string.IsNullOrWhiteSpace(row.Report) ? string.Empty : Path.GetFullPath(row.Report)),
            Csv(row.Summary)));
    }

    string summaryPath = Path.Combine(outputRoot, "audio-regression.md");
    using StreamWriter summary = new(summaryPath, false, Encoding.UTF8);
    summary.WriteLine("# mdSharp Audio Regression");
    summary.WriteLine();
    summary.WriteLine($"ROM folder: `{fullRomFolder}`");
    summary.WriteLine($"Instructions per frame: {instructionsPerFrame.ToString(CultureInfo.InvariantCulture)}");
    if (referenceAudio is not null)
    {
        summary.WriteLine($"Green Hill reference audio: `{Path.GetFullPath(referenceAudio)}`");
    }

    if (sonicTitleReference is not null)
    {
        summary.WriteLine($"Sonic title reference audio: `{Path.GetFullPath(sonicTitleReference)}`");
    }

    if (streetsReference is not null)
    {
        summary.WriteLine($"Streets reference audio: `{Path.GetFullPath(streetsReference)}`");
    }

    summary.WriteLine();
    summary.WriteLine("| Case | ROM | Frames | WAV | Report | Summary |");
    summary.WriteLine("| --- | --- | ---: | --- | --- | --- |");
    foreach (AudioRegressionRow row in rows)
    {
        string wav = string.IsNullOrWhiteSpace(row.Wav) ? string.Empty : Path.GetFileName(row.Wav);
        string report = string.IsNullOrWhiteSpace(row.Report) ? string.Empty : Path.GetFileName(row.Report);
        summary.WriteLine($"| {EscapeMarkdown(row.Id)} | {EscapeMarkdown(row.Rom)} | {row.Frames.ToString(CultureInfo.InvariantCulture)} | {EscapeMarkdown(wav)} | {EscapeMarkdown(report)} | {EscapeMarkdown(row.Summary)} |");
    }

    Console.WriteLine($"Wrote audio regression manifest to {manifestPath}");
    Console.WriteLine($"Wrote audio regression summary to {summaryPath}");
}

void RunAudioReferenceSuite(string manifestPath, string romFolder, string outputFolder, int instructionsPerFrame)
{
    string manifestFullPath = Path.GetFullPath(manifestPath);
    string manifestRoot = Path.GetDirectoryName(manifestFullPath) ?? Directory.GetCurrentDirectory();
    string fullRomFolder = Path.GetFullPath(romFolder);
    string outputRoot = Path.GetFullPath(outputFolder);
    Directory.CreateDirectory(outputRoot);

    AudioReferenceManifest manifest = JsonSerializer.Deserialize<AudioReferenceManifest>(
        File.ReadAllText(manifestFullPath),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException($"Could not read audio reference manifest: {manifestFullPath}");

    string[] roms = EnumerateRomFiles(fullRomFolder);
    List<AudioRegressionRow> rows = [];
    foreach (AudioReferenceCase referenceCase in manifest.Cases ?? [])
    {
        if (string.IsNullOrWhiteSpace(referenceCase.Id))
        {
            rows.Add(new AudioRegressionRow("(missing id)", string.Empty, referenceCase.Frames, string.Empty, string.Empty, string.Empty, "missing id"));
            continue;
        }

        string? rom = ResolveManifestRom(referenceCase, roms, fullRomFolder);
        string? reference = ResolveManifestPath(referenceCase.Reference, manifestRoot);
        if (rom is null || reference is null)
        {
            rows.Add(new AudioRegressionRow(
                referenceCase.Id,
                rom is null ? string.Empty : Path.GetRelativePath(fullRomFolder, rom),
                referenceCase.Frames,
                string.Empty,
                string.Empty,
                string.Empty,
                rom is null ? "missing ROM" : "missing reference"));
            continue;
        }

        int frames = referenceCase.Frames > 0 ? referenceCase.Frames : 900;
        string caseFolder = Path.Combine(outputRoot, SafeFileName(referenceCase.Id));
        Directory.CreateDirectory(caseFolder);
        SonicAudioCompareResult compare;
        if (!string.IsNullOrWhiteSpace(referenceCase.Preset))
        {
            compare = RunSonicAudioCompare(rom, reference, caseFolder, referenceCase.Preset, frames, instructionsPerFrame, referenceCase.AlignmentWindowSeconds, referenceCase.ReferenceStartSeconds, referenceCase.EmulatedStartSeconds);
        }
        else
        {
            int compareStartFrame = referenceCase.CompareStartFrame ?? 0;
            compare = RunGenericAudioCompare(referenceCase.Id, rom, reference, caseFolder, frames, instructionsPerFrame, compareStartFrame, referenceCase.AlignmentWindowSeconds, referenceCase.ReferenceStartSeconds, referenceCase.EmulatedStartSeconds);
        }

        rows.Add(new AudioRegressionRow(
            referenceCase.Id,
            Path.GetRelativePath(fullRomFolder, rom),
            frames,
            compare.EmulatedWav,
            compare.EnergyCsv,
            compare.ReportMarkdown,
            FormatAudioRegressionMetrics(compare)));
    }

    string csvPath = Path.Combine(outputRoot, "audio-reference-suite.csv");
    using StreamWriter csv = new(csvPath, false, Encoding.UTF8);
    csv.WriteLine("id,rom,frames,wav,energyCsv,report,summary");
    foreach (AudioRegressionRow row in rows)
    {
        csv.WriteLine(string.Join(',',
            Csv(row.Id),
            Csv(row.Rom),
            row.Frames.ToString(CultureInfo.InvariantCulture),
            Csv(string.IsNullOrWhiteSpace(row.Wav) ? string.Empty : Path.GetFullPath(row.Wav)),
            Csv(string.IsNullOrWhiteSpace(row.EnergyCsv) ? string.Empty : Path.GetFullPath(row.EnergyCsv)),
            Csv(string.IsNullOrWhiteSpace(row.Report) ? string.Empty : Path.GetFullPath(row.Report)),
            Csv(row.Summary)));
    }

    string reportPath = Path.Combine(outputRoot, "audio-reference-suite.md");
    using StreamWriter report = new(reportPath, false, Encoding.UTF8);
    report.WriteLine("# mdSharp Audio Reference Suite");
    report.WriteLine();
    report.WriteLine($"Manifest: `{manifestFullPath}`");
    report.WriteLine($"ROM folder: `{fullRomFolder}`");
    report.WriteLine($"Instructions per frame: {instructionsPerFrame.ToString(CultureInfo.InvariantCulture)}");
    report.WriteLine();
    report.WriteLine("| Case | ROM | Frames | Report | Summary |");
    report.WriteLine("| --- | --- | ---: | --- | --- |");
    foreach (AudioRegressionRow row in rows)
    {
        string reportName = string.IsNullOrWhiteSpace(row.Report) ? string.Empty : Path.GetFileName(row.Report);
        report.WriteLine($"| {EscapeMarkdown(row.Id)} | {EscapeMarkdown(row.Rom)} | {row.Frames.ToString(CultureInfo.InvariantCulture)} | {EscapeMarkdown(reportName)} | {EscapeMarkdown(row.Summary)} |");
    }

    Console.WriteLine($"Wrote audio reference suite CSV to {csvPath}");
    Console.WriteLine($"Wrote audio reference suite report to {reportPath}");
}

string? ResolveManifestRom(AudioReferenceCase referenceCase, string[] roms, string fullRomFolder)
{
    if (!string.IsNullOrWhiteSpace(referenceCase.Rom))
    {
        string candidate = Path.IsPathRooted(referenceCase.Rom)
            ? referenceCase.Rom
            : Path.Combine(fullRomFolder, referenceCase.Rom);
        if (File.Exists(candidate))
        {
            return Path.GetFullPath(candidate);
        }
    }

    return referenceCase.RomContains is { Length: > 0 }
        ? ResolveManifestRomByContains(roms, referenceCase.RomContains)
        : null;
}

string? ResolveManifestRomByContains(string[] roms, string[] requiredNameParts)
{
    string[] parts = requiredNameParts
        .Where(part => !string.IsNullOrWhiteSpace(part))
        .Select(part => part.Trim())
        .ToArray();
    if (parts.Length == 0)
    {
        return null;
    }

    foreach (string part in parts)
    {
        string? exact = roms.FirstOrDefault(path => string.Equals(Path.GetFileNameWithoutExtension(path), part, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }
    }

    return roms
        .Where(path =>
        {
            string name = Path.GetFileNameWithoutExtension(path);
            return parts.All(part => name.Contains(part, StringComparison.OrdinalIgnoreCase));
        })
        .OrderBy(path => Path.GetFileNameWithoutExtension(path).Length)
        .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
        .FirstOrDefault();
}

string? ResolveManifestPath(string? path, string manifestRoot)
{
    if (string.IsNullOrWhiteSpace(path))
    {
        return null;
    }

    string candidate = Path.IsPathRooted(path) ? path : Path.Combine(manifestRoot, path);
    if (File.Exists(candidate))
    {
        return Path.GetFullPath(candidate);
    }

    candidate = Path.GetFullPath(path);
    return File.Exists(candidate) ? candidate : null;
}

string SafeFileName(string value)
{
    char[] invalid = Path.GetInvalidFileNameChars();
    StringBuilder builder = new(value.Length);
    foreach (char ch in value)
    {
        builder.Append(invalid.Contains(ch) ? '_' : ch);
    }

    return builder.Length == 0 ? "audio-case" : builder.ToString();
}

string FormatAudioRegressionMetrics(SonicAudioCompareResult compare)
{
    double rmsDelta = compare.EmulatedStats.RmsDb - compare.ReferenceStats.RmsDb;
    return string.Join(' ',
    [
        $"rating={AudioQualityRating(compare)}",
        $"corr={F(compare.Alignment.EnvelopeCorrelation)}",
        $"rmsDelta={F(rmsDelta)}dB",
        $"brightDelta={F(compare.EmulatedStats.BrightnessDb - compare.ReferenceStats.BrightnessDb)}dB",
        $"bassRel={F(RelativeBandDelta(compare.ReferenceBands.BassDb, compare.EmulatedBands.BassDb, rmsDelta))}dB",
        $"bodyRel={F(RelativeBandDelta(compare.ReferenceBands.BodyDb, compare.EmulatedBands.BodyDb, rmsDelta))}dB",
        $"melodyRel={F(RelativeBandDelta(compare.ReferenceBands.MelodyDb, compare.EmulatedBands.MelodyDb, rmsDelta))}dB",
        $"sparkleRel={F(RelativeBandDelta(compare.ReferenceBands.SparkleDb, compare.EmulatedBands.SparkleDb, rmsDelta))}dB",
    ]);
}

string AudioQualityRating(SonicAudioCompareResult compare)
{
    double rmsDelta = Math.Abs(compare.EmulatedStats.RmsDb - compare.ReferenceStats.RmsDb);
    double brightnessDelta = Math.Abs(compare.EmulatedStats.BrightnessDb - compare.ReferenceStats.BrightnessDb);
    double gainDelta = compare.EmulatedStats.RmsDb - compare.ReferenceStats.RmsDb;
    double bodyDelta = Math.Abs(RelativeBandDelta(compare.ReferenceBands.BodyDb, compare.EmulatedBands.BodyDb, gainDelta));
    double melodyDelta = Math.Abs(RelativeBandDelta(compare.ReferenceBands.MelodyDb, compare.EmulatedBands.MelodyDb, gainDelta));
    double sparkleDelta = Math.Abs(RelativeBandDelta(compare.ReferenceBands.SparkleDb, compare.EmulatedBands.SparkleDb, gainDelta));
    double maxBandDelta = Math.Max(Math.Max(bodyDelta, melodyDelta), sparkleDelta);

    if (compare.Alignment.EnvelopeCorrelation >= 0.86 && rmsDelta <= 18.0 && brightnessDelta <= 2.0 && maxBandDelta <= 4.0)
    {
        return "A";
    }

    if (compare.Alignment.EnvelopeCorrelation >= 0.75 && rmsDelta <= 22.0 && brightnessDelta <= 4.0 && maxBandDelta <= 8.0)
    {
        return "B";
    }

    if (compare.Alignment.EnvelopeCorrelation >= 0.65 && rmsDelta <= 28.0 && brightnessDelta <= 7.0 && maxBandDelta <= 14.0)
    {
        return "C";
    }

    return "needs-work";
}

double RelativeBandDelta(double referenceBandDb, double emulatedBandDb, double rmsDeltaDb)
{
    return emulatedBandDb - referenceBandDb - rmsDeltaDb;
}

string SmokeAudioSummary(short[] samples)
{
    int sampleFrames = samples.Length / AudioConstants.StereoChannels;
    double[] mono = ToMono(samples, 0, sampleFrames);
    AudioSignalStats stats = AnalyzeAudioSignal(mono, 0, mono.Length);
    int bandCount = Math.Min(mono.Length, AudioConstants.DefaultSampleRate * 20);
    int bandStart = Math.Max(0, mono.Length - bandCount);
    AudioBandStats bands = AnalyzeAudioBands(mono, bandStart, bandCount);
    int nearClipped = CountNearClippedSamples(samples);
    return string.Join(' ',
    [
        $"rms={F(stats.RmsDb)}dB",
        $"peak={F(stats.PeakDb)}dB",
        $"bright={F(stats.BrightnessDb)}dB",
        $"bass={F(bands.BassDb)}dB",
        $"body={F(bands.BodyDb)}dB",
        $"melody={F(bands.MelodyDb)}dB",
        $"sparkle={F(bands.SparkleDb)}dB",
        $"nearClip={nearClipped.ToString(CultureInfo.InvariantCulture)}",
    ]);
}

int CountNearClippedSamples(short[] samples)
{
    int count = 0;
    foreach (short sample in samples)
    {
        if (Math.Abs((int)sample) >= 32760)
        {
            count++;
        }
    }

    return count;
}

string? ResolveAudioRom(string[] roms, params string[] names)
{
    foreach (string name in names)
    {
        string? exact = roms.FirstOrDefault(path => string.Equals(Path.GetFileNameWithoutExtension(path), name, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }
    }

    foreach (string name in names)
    {
        string? contains = roms.FirstOrDefault(path => Path.GetFileNameWithoutExtension(path).Contains(name, StringComparison.OrdinalIgnoreCase));
        if (contains is not null)
        {
            return contains;
        }
    }

    return null;
}

string? ResolveReferenceAudioByKeywords(string[] candidates, params string[] requiredNameParts)
{
    string currentDirectory = Directory.GetCurrentDirectory();
    foreach (string candidate in candidates)
    {
        string exactPath = Path.GetFullPath(candidate, currentDirectory);
        if (File.Exists(exactPath))
        {
            return exactPath;
        }
    }

    string[] extensions = [".wav", ".flac", ".mp3", ".ogg"];
    foreach (string file in Directory.EnumerateFiles(currentDirectory))
    {
        string extension = Path.GetExtension(file);
        if (!extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            continue;
        }

        string name = Path.GetFileNameWithoutExtension(file);
        if (requiredNameParts.All(part => name.Contains(part, StringComparison.OrdinalIgnoreCase)))
        {
            return Path.GetFullPath(file);
        }
    }

    return null;
}

void RunSonicAudioCheckpoints(string romPath, string outputFolder, string? referenceAudio, int instructionsPerFrame)
{
    Directory.CreateDirectory(outputFolder);
    string outputRoot = Path.GetFullPath(outputFolder);
    SonicAudioCheckpointSpec[] checkpoints =
    [
        new("title", 760),
        new("attract", 2200),
        new("greenhill", 2600),
        new("gameplay", 1800),
    ];

    using StreamWriter manifest = new(Path.Combine(outputRoot, "sonic-audio-checkpoints.csv"), false, Encoding.UTF8);
    manifest.WriteLine("preset,frames,wav,energyCsv,compareReport");
    foreach (SonicAudioCheckpointSpec checkpoint in checkpoints)
    {
        string wav = Path.Combine(outputRoot, $"sonic-{checkpoint.Preset}.wav");
        string energy = Path.Combine(outputRoot, $"sonic-{checkpoint.Preset}-ym-energy.csv");
        DumpSonicAudio(romPath, wav, energy, checkpoint.Frames, instructionsPerFrame, checkpoint.Preset);
        string compareReport = string.Empty;
        if (referenceAudio is not null && checkpoint.Preset.Equals("greenhill", StringComparison.OrdinalIgnoreCase))
        {
            string compareFolder = Path.Combine(outputRoot, "greenhill-compare");
            RunSonicAudioCompare(romPath, referenceAudio, compareFolder, checkpoint.Preset, checkpoint.Frames, instructionsPerFrame);
            compareReport = Path.Combine(compareFolder, "sonic-greenhill-audio-compare.md");
        }

        manifest.WriteLine($"{Csv(checkpoint.Preset)},{checkpoint.Frames.ToString(CultureInfo.InvariantCulture)},{Csv(Path.GetFullPath(wav))},{Csv(Path.GetFullPath(energy))},{Csv(compareReport)}");
    }

    Console.WriteLine($"Wrote Sonic audio checkpoint manifest to {Path.Combine(outputRoot, "sonic-audio-checkpoints.csv")}");
}

void RunAudioStems(string romPath, string outputFolder, int frames, int instructionsPerFrame, int compareStartFrame)
{
    Directory.CreateDirectory(outputFolder);
    string outputRoot = Path.GetFullPath(outputFolder);
    SonicAudioStemRender render = RenderAudioStems(romPath, frames, instructionsPerFrame, compareStartFrame);
    string manifestPath = Path.Combine(outputRoot, "audio-stems.csv");
    string reportPath = Path.Combine(outputRoot, "audio-stem-bands.md");
    using StreamWriter manifest = new(manifestPath, false, Encoding.UTF8);
    manifest.WriteLine("stem,wav,rmsDb,peakDb,brightnessDb,bassDb,bodyDb,melodyDb,sparkleDb");

    int analysisStart = Math.Min(render.Stems[0].Length / AudioConstants.StereoChannels, render.CompareStartSample);
    int analysisSamples = Math.Min(
        10 * AudioConstants.DefaultSampleRate,
        Math.Max(0, (render.Stems[0].Length / AudioConstants.StereoChannels) - analysisStart));

    List<(string Name, string Wav, AudioSignalStats Stats, AudioBandStats Bands, string TopNotes)> rows = new();
    for (int stem = 0; stem < render.Names.Length; stem++)
    {
        string wavPath = Path.Combine(outputRoot, $"{render.Names[stem]}.wav");
        WriteWav(wavPath, render.Stems[stem], AudioConstants.DefaultSampleRate, AudioConstants.StereoChannels);
        double[] mono = ToMono(render.Stems[stem], 0, render.Stems[stem].Length / AudioConstants.StereoChannels);
        AudioSignalStats stats = AnalyzeAudioSignal(mono, analysisStart, analysisSamples);
        AudioBandStats bands = AnalyzeAudioBands(mono, analysisStart, analysisSamples);
        string topNotes = FormatTopMusicalFrequencies(mono, analysisStart, analysisSamples, 5);
        rows.Add((render.Names[stem], wavPath, stats, bands, topNotes));
        manifest.WriteLine(string.Join(',',
            Csv(render.Names[stem]),
            Csv(Path.GetFullPath(wavPath)),
            F(stats.RmsDb),
            F(stats.PeakDb),
            F(stats.BrightnessDb),
            F(bands.BassDb),
            F(bands.BodyDb),
            F(bands.MelodyDb),
            F(bands.SparkleDb),
            Csv(topNotes)));
    }

    using StreamWriter report = new(reportPath, false, Encoding.UTF8);
    report.WriteLine("# Audio Stems");
    report.WriteLine();
    report.WriteLine($"- ROM: `{Path.GetFullPath(romPath)}`");
    report.WriteLine($"- Frames: {frames.ToString(CultureInfo.InvariantCulture)}");
    report.WriteLine($"- Instructions per frame: {instructionsPerFrame.ToString(CultureInfo.InvariantCulture)}");
    report.WriteLine($"- Compare start frame: {compareStartFrame.ToString(CultureInfo.InvariantCulture)}");
    report.WriteLine($"- Analysis starts at sample: {analysisStart.ToString(CultureInfo.InvariantCulture)}");
    report.WriteLine($"- Analysis window: {(analysisSamples / (double)AudioConstants.DefaultSampleRate).ToString("0.###", CultureInfo.InvariantCulture)}s");
    report.WriteLine("- Stems are scaled by the emulator YM/PSG/master mix levels and output low-pass filter before writing.");
    report.WriteLine();
    report.WriteLine("| Stem | RMS | Peak | Brightness | Bass | Body | Melody | Sparkle | Top notes |");
    report.WriteLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |");
    foreach ((string name, _, AudioSignalStats stats, AudioBandStats bands, string topNotes) in rows)
    {
        report.WriteLine($"| {name} | {F(stats.RmsDb)} | {F(stats.PeakDb)} | {F(stats.BrightnessDb)} | {F(bands.BassDb)} | {F(bands.BodyDb)} | {F(bands.MelodyDb)} | {F(bands.SparkleDb)} | {EscapeMarkdown(topNotes)} |");
    }

    Console.WriteLine($"Wrote audio stems to {outputRoot}");
    Console.WriteLine($"Wrote stem attribution report to {reportPath}");
}

void RunAudioStemCompare(string id, string romPath, string referencePath, string outputFolder, int frames, int instructionsPerFrame, int compareStartFrame, double? alignmentWindowSeconds, double? referenceStartSeconds = null, double? emulatedStartSeconds = null)
{
    Directory.CreateDirectory(outputFolder);
    string outputRoot = Path.GetFullPath(outputFolder);
    string stemsFolder = Path.Combine(outputRoot, "stems");
    Directory.CreateDirectory(stemsFolder);
    string reportCsv = Path.Combine(outputRoot, $"{id}-audio-stem-compare.csv");
    string reportMarkdown = Path.Combine(outputRoot, $"{id}-audio-stem-compare.md");
    string envelopeCsv = Path.Combine(outputRoot, $"{id}-aligned-envelope.csv");
    string psgTraceCsv = Path.Combine(outputRoot, $"{id}-psg-trace.csv");
    string rawReferencePath = Path.Combine(outputRoot, $"{id}-reference.raw");

    SonicAudioStemRender render = RenderAudioStems(romPath, frames, instructionsPerFrame, compareStartFrame);
    string[] wavPaths = new string[render.Names.Length];
    for (int stem = 0; stem < render.Names.Length; stem++)
    {
        wavPaths[stem] = Path.Combine(stemsFolder, $"{render.Names[stem]}.wav");
        WriteWav(wavPaths[stem], render.Stems[stem], AudioConstants.DefaultSampleRate, AudioConstants.StereoChannels);
    }

    short[] referenceSamples = DecodeReferenceAudio(referencePath, rawReferencePath);
    double[] referenceMono = ToMono(referenceSamples, 0, referenceSamples.Length / AudioConstants.StereoChannels);
    double[] emulatedMonoAfterCompare = ToMono(render.Stems[^1], render.CompareStartSample, (render.Stems[^1].Length / AudioConstants.StereoChannels) - render.CompareStartSample);

    const int envelopeWindow = 1024;
    const int envelopeHop = 512;
    double[][] referenceFeatures = BuildAudioAlignmentFeatures(referenceMono, envelopeWindow, envelopeHop);
    double[][] emulatedFeatures = BuildAudioAlignmentFeatures(emulatedMonoAfterCompare, envelopeWindow, envelopeHop);
    double[] referenceEnvelope = referenceFeatures[0];
    double[] emulatedEnvelope = emulatedFeatures[0];
    AudioAlignment alignment = FindBestAudioAlignment(referenceFeatures, emulatedFeatures, AudioConstants.DefaultSampleRate, envelopeHop, alignmentWindowSeconds, referenceStartSeconds, emulatedStartSeconds);

    int referenceStart = Math.Clamp((int)Math.Round(alignment.ReferenceOffsetSeconds * AudioConstants.DefaultSampleRate), 0, Math.Max(0, referenceMono.Length - 1));
    int emulatedStartAfterCompare = Math.Clamp((int)Math.Round(alignment.EmulatedOffsetSeconds * AudioConstants.DefaultSampleRate), 0, Math.Max(0, emulatedMonoAfterCompare.Length - 1));
    int emulatedStart = render.CompareStartSample + emulatedStartAfterCompare;
    int analysisSamples = Math.Min(
        (int)Math.Round(alignment.WindowSeconds * AudioConstants.DefaultSampleRate),
        Math.Min(referenceMono.Length - referenceStart, (render.Stems[^1].Length / AudioConstants.StereoChannels) - emulatedStart));

    AudioSignalStats referenceStats = AnalyzeAudioSignal(referenceMono, referenceStart, analysisSamples);
    double[] mixedMono = ToMono(render.Stems[^1], 0, render.Stems[^1].Length / AudioConstants.StereoChannels);
    AudioSignalStats emulatedStats = AnalyzeAudioSignal(mixedMono, emulatedStart, analysisSamples);
    AudioBandStats referenceBands = AnalyzeAudioBands(referenceMono, referenceStart, analysisSamples);
    AudioBandStats emulatedBands = AnalyzeAudioBands(mixedMono, emulatedStart, analysisSamples);
    string referenceTopNotes = FormatTopMusicalFrequencies(referenceMono, referenceStart, analysisSamples, 6);
    string emulatedTopNotes = FormatTopMusicalFrequencies(mixedMono, emulatedStart, analysisSamples, 6);

    WriteAudioCompareCsv(reportCsv, id, frames, instructionsPerFrame, compareStartFrame, alignment, referenceStats, emulatedStats, referenceBands, emulatedBands, wavPaths[^1], string.Empty, referencePath);
    WriteAlignedEnvelopeCsv(envelopeCsv, referenceEnvelope, emulatedEnvelope, alignment, AudioConstants.DefaultSampleRate, envelopeHop);

    List<(string Name, string Wav, AudioSignalStats Stats, AudioBandStats Bands, string TopNotes)> stemRows = new();
    for (int stem = 0; stem < render.Names.Length; stem++)
    {
        double[] stemMono = ToMono(render.Stems[stem], 0, render.Stems[stem].Length / AudioConstants.StereoChannels);
        stemRows.Add((
            render.Names[stem],
            wavPaths[stem],
            AnalyzeAudioSignal(stemMono, emulatedStart, analysisSamples),
            AnalyzeAudioBands(stemMono, emulatedStart, analysisSamples),
            FormatTopMusicalFrequencies(stemMono, emulatedStart, analysisSamples, 5)));
    }

    WriteSonicPsgTrace(psgTraceCsv, render.PsgTrace);
    SonicPsgTraceSummary psgSummary = SummarizeSonicPsgTrace(render.PsgTrace, emulatedStart, analysisSamples);
    SonicYmTraceSummary ymSummary = SummarizeSonicYmTrace(render.YmTrace, emulatedStart, analysisSamples);

    WriteAudioStemCompareMarkdown(
        reportMarkdown,
        id,
        romPath,
        referencePath,
        frames,
        instructionsPerFrame,
        compareStartFrame,
        alignment,
        referenceStats,
        emulatedStats,
        referenceBands,
        emulatedBands,
        referenceTopNotes,
        emulatedTopNotes,
        stemRows,
        psgSummary,
        ymSummary,
        wavPaths[^1],
        psgTraceCsv,
        envelopeCsv);

    Console.WriteLine($"Wrote audio stem comparison to {reportMarkdown}");
    Console.WriteLine($"Wrote stems to {stemsFolder}");
}

void RunVgmStemCompare(string id, string vgmPath, string referencePath, string outputFolder, double? maxSeconds, double? alignmentWindowSeconds)
{
    Directory.CreateDirectory(outputFolder);
    string outputRoot = Path.GetFullPath(outputFolder);
    string stemsFolder = Path.Combine(outputRoot, "stems");
    Directory.CreateDirectory(stemsFolder);
    string reportCsv = Path.Combine(outputRoot, $"{id}-vgm-stem-compare.csv");
    string reportMarkdown = Path.Combine(outputRoot, $"{id}-vgm-stem-compare.md");
    string envelopeCsv = Path.Combine(outputRoot, $"{id}-aligned-envelope.csv");
    string rawReferencePath = Path.Combine(outputRoot, $"{id}-reference.raw");
    string rawMixedPath = Path.Combine(outputRoot, $"{id}-mixed.raw");

    RenderVgmStems(vgmPath, stemsFolder, maxSeconds);

    string[] names = ["ym1", "ym2", "ym3", "ym4", "ym5", "ym6-dac", "psg1", "psg2", "psg3", "psg-noise", "mixed"];
    string[] wavPaths = names.Select(name => Path.Combine(stemsFolder, $"vgm-{name}.wav")).ToArray();
    string mixedWav = wavPaths[^1];
    if (!File.Exists(mixedWav))
    {
        throw new FileNotFoundException("VGM mixed stem was not written.", mixedWav);
    }

    short[] referenceSamples = DecodeReferenceAudio(referencePath, rawReferencePath);
    short[] mixedSamples = DecodeReferenceAudio(mixedWav, rawMixedPath);
    double[] referenceMono = ToMono(referenceSamples, 0, referenceSamples.Length / AudioConstants.StereoChannels);
    double[] mixedMono = ToMono(mixedSamples, 0, mixedSamples.Length / AudioConstants.StereoChannels);

    const int envelopeWindow = 1024;
    const int envelopeHop = 512;
    double[][] referenceFeatures = BuildAudioAlignmentFeatures(referenceMono, envelopeWindow, envelopeHop);
    double[][] emulatedFeatures = BuildAudioAlignmentFeatures(mixedMono, envelopeWindow, envelopeHop);
    double[] referenceEnvelope = referenceFeatures[0];
    double[] emulatedEnvelope = emulatedFeatures[0];
    AudioAlignment alignment = FindBestAudioAlignment(referenceFeatures, emulatedFeatures, AudioConstants.DefaultSampleRate, envelopeHop, alignmentWindowSeconds);

    int referenceStart = Math.Clamp((int)Math.Round(alignment.ReferenceOffsetSeconds * AudioConstants.DefaultSampleRate), 0, Math.Max(0, referenceMono.Length - 1));
    int emulatedStart = Math.Clamp((int)Math.Round(alignment.EmulatedOffsetSeconds * AudioConstants.DefaultSampleRate), 0, Math.Max(0, mixedMono.Length - 1));
    int analysisSamples = Math.Min(
        (int)Math.Round(alignment.WindowSeconds * AudioConstants.DefaultSampleRate),
        Math.Min(referenceMono.Length - referenceStart, mixedMono.Length - emulatedStart));

    AudioSignalStats referenceStats = AnalyzeAudioSignal(referenceMono, referenceStart, analysisSamples);
    AudioSignalStats emulatedStats = AnalyzeAudioSignal(mixedMono, emulatedStart, analysisSamples);
    AudioBandStats referenceBands = AnalyzeAudioBands(referenceMono, referenceStart, analysisSamples);
    AudioBandStats emulatedBands = AnalyzeAudioBands(mixedMono, emulatedStart, analysisSamples);
    string referenceTopNotes = FormatTopMusicalFrequencies(referenceMono, referenceStart, analysisSamples, 6);
    string emulatedTopNotes = FormatTopMusicalFrequencies(mixedMono, emulatedStart, analysisSamples, 6);

    WriteAudioCompareCsv(reportCsv, id, 0, 0, 0, alignment, referenceStats, emulatedStats, referenceBands, emulatedBands, mixedWav, string.Empty, referencePath);
    WriteAlignedEnvelopeCsv(envelopeCsv, referenceEnvelope, emulatedEnvelope, alignment, AudioConstants.DefaultSampleRate, envelopeHop);

    List<(string Name, string Wav, AudioSignalStats Stats, AudioBandStats Bands, string TopNotes)> stemRows = new();
    for (int stem = 0; stem < names.Length; stem++)
    {
        string wavPath = wavPaths[stem];
        if (!File.Exists(wavPath))
        {
            continue;
        }

        short[] stemSamples = DecodeReferenceAudio(wavPath, Path.Combine(outputRoot, $"{id}-{names[stem]}.raw"));
        double[] stemMono = ToMono(stemSamples, 0, stemSamples.Length / AudioConstants.StereoChannels);
        int stemStart = Math.Clamp(emulatedStart, 0, Math.Max(0, stemMono.Length - 1));
        int stemSamplesToAnalyze = Math.Min(analysisSamples, Math.Max(0, stemMono.Length - stemStart));
        stemRows.Add((
            names[stem],
            wavPath,
            AnalyzeAudioSignal(stemMono, stemStart, stemSamplesToAnalyze),
            AnalyzeAudioBands(stemMono, stemStart, stemSamplesToAnalyze),
            FormatTopMusicalFrequencies(stemMono, stemStart, stemSamplesToAnalyze, 5)));
    }

    WriteVgmStemCompareMarkdown(
        reportMarkdown,
        id,
        vgmPath,
        referencePath,
        maxSeconds,
        alignment,
        referenceStats,
        emulatedStats,
        referenceBands,
        emulatedBands,
        referenceTopNotes,
        emulatedTopNotes,
        stemRows,
        mixedWav,
        envelopeCsv);

    Console.WriteLine($"Wrote VGM stem comparison to {reportMarkdown}");
    Console.WriteLine($"Wrote VGM stems to {stemsFolder}");
}

void RunSonicAudioStems(string romPath, string outputFolder, string preset, int frames, int instructionsPerFrame)
{
    Directory.CreateDirectory(outputFolder);
    string outputRoot = Path.GetFullPath(outputFolder);
    SonicAudioStemRender render = RenderSonicAudioStems(romPath, preset, frames, instructionsPerFrame);
    string manifestPath = Path.Combine(outputRoot, $"sonic-{preset}-stems.csv");
    string reportPath = Path.Combine(outputRoot, $"sonic-{preset}-stem-bands.md");
    string psgTracePath = Path.Combine(outputRoot, $"sonic-{preset}-psg-trace.csv");
    using StreamWriter manifest = new(manifestPath, false, Encoding.UTF8);
    manifest.WriteLine("stem,wav,rmsDb,peakDb,brightnessDb,bassDb,bodyDb,melodyDb,sparkleDb");

    int analysisStart = Math.Min(
        render.Stems[0].Length / AudioConstants.StereoChannels,
        render.CompareStartSample + (int)Math.Round(SonicStemAnalysisOffsetSeconds(preset) * AudioConstants.DefaultSampleRate));
    int analysisSamples = Math.Min(
        10 * AudioConstants.DefaultSampleRate,
        Math.Max(0, (render.Stems[0].Length / AudioConstants.StereoChannels) - analysisStart));

    List<(string Name, string Wav, AudioSignalStats Stats, AudioBandStats Bands)> rows = new();
    for (int stem = 0; stem < render.Names.Length; stem++)
    {
        string wavPath = Path.Combine(outputRoot, $"sonic-{preset}-{render.Names[stem]}.wav");
        WriteWav(wavPath, render.Stems[stem], AudioConstants.DefaultSampleRate, AudioConstants.StereoChannels);
        double[] mono = ToMono(render.Stems[stem], 0, render.Stems[stem].Length / AudioConstants.StereoChannels);
        AudioSignalStats stats = AnalyzeAudioSignal(mono, analysisStart, analysisSamples);
        AudioBandStats bands = AnalyzeAudioBands(mono, analysisStart, analysisSamples);
        rows.Add((render.Names[stem], wavPath, stats, bands));
        manifest.WriteLine(string.Join(',',
            Csv(render.Names[stem]),
            Csv(Path.GetFullPath(wavPath)),
            F(stats.RmsDb),
            F(stats.PeakDb),
            F(stats.BrightnessDb),
            F(bands.BassDb),
            F(bands.BodyDb),
            F(bands.MelodyDb),
            F(bands.SparkleDb)));
    }

    WriteSonicPsgTrace(psgTracePath, render.PsgTrace);
    SonicPsgTraceSummary psgSummary = SummarizeSonicPsgTrace(render.PsgTrace, analysisStart, analysisSamples);

    using StreamWriter report = new(reportPath, false, Encoding.UTF8);
    report.WriteLine($"# Sonic {preset} Audio Stems");
    report.WriteLine();
    report.WriteLine($"- Frames: {frames.ToString(CultureInfo.InvariantCulture)}");
    report.WriteLine($"- Instructions per frame: {instructionsPerFrame.ToString(CultureInfo.InvariantCulture)}");
    report.WriteLine($"- Analysis starts at sample: {analysisStart.ToString(CultureInfo.InvariantCulture)}");
    report.WriteLine($"- Analysis window: {(analysisSamples / (double)AudioConstants.DefaultSampleRate).ToString("0.###", CultureInfo.InvariantCulture)}s");
    report.WriteLine("- Stems are scaled by the emulator YM/PSG/master mix levels and output low-pass filter before writing.");
    report.WriteLine($"- PSG trace: `{Path.GetFileName(psgTracePath)}`");
    report.WriteLine();
    report.WriteLine("| Stem | RMS | Peak | Brightness | Bass | Body | Melody | Sparkle |");
    report.WriteLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
    foreach ((string name, _, AudioSignalStats stats, AudioBandStats bands) in rows)
    {
        report.WriteLine($"| {name} | {F(stats.RmsDb)} | {F(stats.PeakDb)} | {F(stats.BrightnessDb)} | {F(bands.BassDb)} | {F(bands.BodyDb)} | {F(bands.MelodyDb)} | {F(bands.SparkleDb)} |");
    }

    report.WriteLine();
    report.WriteLine("## PSG Activity");
    report.WriteLine();
    report.WriteLine("| Channel | Active frames | Avg volume | Min Hz | Max Hz | Avg Hz |");
    report.WriteLine("| --- | ---: | ---: | ---: | ---: | ---: |");
    foreach (SonicPsgChannelTraceSummary channel in psgSummary.Channels)
    {
        report.WriteLine($"| {channel.Name} | {channel.ActiveFrames} | {F(channel.AverageVolume)} | {F(channel.MinFrequencyHz)} | {F(channel.MaxFrequencyHz)} | {F(channel.AverageFrequencyHz)} |");
    }

    Console.WriteLine($"Wrote Sonic {preset} audio stems to {outputRoot}");
    Console.WriteLine($"Wrote stem attribution report to {reportPath}");
}

void RunSonicAudioWindows(string romPath, string titleReferencePath, string greenhillReferencePath, string outputFolder, int instructionsPerFrame)
{
    string outputRoot = Path.GetFullPath(outputFolder);
    Directory.CreateDirectory(outputRoot);

    AnalyzeSonicPresetWindows(
        romPath,
        titleReferencePath,
        Path.Combine(outputRoot, "title"),
        "title",
        1800,
        instructionsPerFrame,
        [
            new("title-hit-1", 0.10, 0.45),
            new("title-phrase-1", 0.55, 0.55),
            new("title-phrase-2", 1.20, 0.60),
            new("title-phrase-3", 2.05, 0.60),
            new("title-loop-peak", 3.05, 0.60),
        ]);

    AnalyzeSonicPresetWindows(
        romPath,
        greenhillReferencePath,
        Path.Combine(outputRoot, "greenhill"),
        "greenhill",
        2600,
        instructionsPerFrame,
        [
            new("ghz-main-1", 0.20, 0.60),
            new("ghz-main-2", 1.00, 0.60),
            new("ghz-high-peak", 2.00, 0.60),
            new("ghz-response", 3.10, 0.60),
            new("ghz-loop-peak", 5.20, 0.75),
        ]);

    Console.WriteLine($"Wrote Sonic window diagnostics to {outputRoot}");
}

void AnalyzeSonicPresetWindows(string romPath, string referencePath, string outputFolder, string preset, int frames, int instructionsPerFrame, SonicAudioWindowSpec[] windows)
{
    Directory.CreateDirectory(outputFolder);
    string outputRoot = Path.GetFullPath(outputFolder);
    SonicAudioStemRender render = RenderSonicAudioStems(romPath, preset, frames, instructionsPerFrame);
    string rawReferencePath = Path.Combine(outputRoot, $"sonic-{preset}-reference.raw");
    short[] referenceSamples = DecodeReferenceAudio(referencePath, rawReferencePath);
    double[] referenceMono = ToMono(referenceSamples, 0, referenceSamples.Length / AudioConstants.StereoChannels);
    double[] emulatedMonoAfterCompare = ToMono(render.Stems[^1], render.CompareStartSample, (render.Stems[^1].Length / AudioConstants.StereoChannels) - render.CompareStartSample);

    const int envelopeWindow = 1024;
    const int envelopeHop = 512;
    double[][] referenceFeatures = BuildAudioAlignmentFeatures(referenceMono, envelopeWindow, envelopeHop);
    double[][] emulatedFeatures = BuildAudioAlignmentFeatures(emulatedMonoAfterCompare, envelopeWindow, envelopeHop);
    double[] referenceEnvelope = referenceFeatures[0];
    double[] emulatedEnvelope = emulatedFeatures[0];
    AudioAlignment alignment = FindBestAudioAlignment(referenceFeatures, emulatedFeatures, AudioConstants.DefaultSampleRate, envelopeHop);
    int referenceAlignedSample = Math.Clamp((int)Math.Round(alignment.ReferenceOffsetSeconds * AudioConstants.DefaultSampleRate), 0, Math.Max(0, referenceMono.Length - 1));
    int emulatedAlignedSample = render.CompareStartSample + Math.Clamp((int)Math.Round(alignment.EmulatedOffsetSeconds * AudioConstants.DefaultSampleRate), 0, Math.Max(0, (render.Stems[^1].Length / AudioConstants.StereoChannels) - 1));

    string stemsFolder = Path.Combine(outputRoot, "stems");
    Directory.CreateDirectory(stemsFolder);
    for (int stem = 0; stem < render.Names.Length; stem++)
    {
        WriteWav(Path.Combine(stemsFolder, $"sonic-{preset}-{render.Names[stem]}.wav"), render.Stems[stem], AudioConstants.DefaultSampleRate, AudioConstants.StereoChannels);
    }

    List<SonicAudioWindowAnalysis> analyses = [];
    foreach (SonicAudioWindowSpec window in windows)
    {
        int referenceStart = referenceAlignedSample + (int)Math.Round(window.OffsetSeconds * AudioConstants.DefaultSampleRate);
        int emulatedStart = emulatedAlignedSample + (int)Math.Round(window.OffsetSeconds * AudioConstants.DefaultSampleRate);
        int requestedSamples = Math.Max(1, (int)Math.Round(window.DurationSeconds * AudioConstants.DefaultSampleRate));
        int referenceCount = Math.Min(requestedSamples, Math.Max(0, referenceMono.Length - referenceStart));
        double[] emulatedMono = ToMono(render.Stems[^1], 0, render.Stems[^1].Length / AudioConstants.StereoChannels);
        int emulatedCount = Math.Min(requestedSamples, Math.Max(0, emulatedMono.Length - emulatedStart));
        int count = Math.Min(referenceCount, emulatedCount);
        if (count <= 0)
        {
            continue;
        }

        AudioSignalStats referenceStats = AnalyzeAudioSignal(referenceMono, referenceStart, count);
        AudioSignalStats emulatedStats = AnalyzeAudioSignal(emulatedMono, emulatedStart, count);
        AudioBandStats referenceBands = AnalyzeAudioBands(referenceMono, referenceStart, count);
        AudioBandStats emulatedBands = AnalyzeAudioBands(emulatedMono, emulatedStart, count);

        SonicAudioStemWindow[] stemWindows = new SonicAudioStemWindow[render.Names.Length];
        for (int stem = 0; stem < render.Names.Length; stem++)
        {
            double[] stemMono = ToMono(render.Stems[stem], 0, render.Stems[stem].Length / AudioConstants.StereoChannels);
            stemWindows[stem] = new SonicAudioStemWindow(
                render.Names[stem],
                AnalyzeAudioSignal(stemMono, emulatedStart, count),
                AnalyzeAudioBands(stemMono, emulatedStart, count),
                FormatTopMusicalFrequencies(stemMono, emulatedStart, count, 4));
        }

        analyses.Add(new SonicAudioWindowAnalysis(
            window.Name,
            window.OffsetSeconds,
            count / (double)AudioConstants.DefaultSampleRate,
            referenceStats,
            emulatedStats,
            referenceBands,
            emulatedBands,
            FormatTopMusicalFrequencies(referenceMono, referenceStart, count, 6),
            FormatTopMusicalFrequencies(emulatedMono, emulatedStart, count, 6),
            stemWindows,
            SummarizeSonicPsgTrace(render.PsgTrace, emulatedStart, count),
            SummarizeSonicYmTrace(render.YmTrace, emulatedStart, count)));
    }

    WriteSonicAudioWindowReports(outputRoot, preset, referencePath, frames, instructionsPerFrame, alignment, analyses);
}

void WriteSonicAudioWindowReports(string outputRoot, string preset, string referencePath, int frames, int instructionsPerFrame, AudioAlignment alignment, IReadOnlyList<SonicAudioWindowAnalysis> analyses)
{
    string csvPath = Path.Combine(outputRoot, $"sonic-{preset}-windows.csv");
    using StreamWriter csv = new(csvPath, false, Encoding.UTF8);
    csv.WriteLine("window,offsetSeconds,durationSeconds,referenceRmsDb,emulatedRmsDb,rmsDeltaDb,referencePeakDb,emulatedPeakDb,referenceBrightnessDb,emulatedBrightnessDb,referenceBassDb,emulatedBassDb,referenceBodyDb,emulatedBodyDb,referenceMelodyDb,emulatedMelodyDb,referenceSparkleDb,emulatedSparkleDb,referenceTopNotes,emulatedTopNotes");
    foreach (SonicAudioWindowAnalysis row in analyses)
    {
        csv.WriteLine(string.Join(',',
            Csv(row.Name),
            F(row.OffsetSeconds),
            F(row.DurationSeconds),
            F(row.ReferenceStats.RmsDb),
            F(row.EmulatedStats.RmsDb),
            F(row.EmulatedStats.RmsDb - row.ReferenceStats.RmsDb),
            F(row.ReferenceStats.PeakDb),
            F(row.EmulatedStats.PeakDb),
            F(row.ReferenceStats.BrightnessDb),
            F(row.EmulatedStats.BrightnessDb),
            F(row.ReferenceBands.BassDb),
            F(row.EmulatedBands.BassDb),
            F(row.ReferenceBands.BodyDb),
            F(row.EmulatedBands.BodyDb),
            F(row.ReferenceBands.MelodyDb),
            F(row.EmulatedBands.MelodyDb),
            F(row.ReferenceBands.SparkleDb),
            F(row.EmulatedBands.SparkleDb),
            Csv(row.ReferenceTopNotes),
            Csv(row.EmulatedTopNotes)));
    }

    string reportPath = Path.Combine(outputRoot, $"sonic-{preset}-windows.md");
    using StreamWriter report = new(reportPath, false, Encoding.UTF8);
    report.WriteLine($"# Sonic {preset} Audio Windows");
    report.WriteLine();
    report.WriteLine($"- Frames: {frames.ToString(CultureInfo.InvariantCulture)}");
    report.WriteLine($"- Instructions per frame: {instructionsPerFrame.ToString(CultureInfo.InvariantCulture)}");
    report.WriteLine($"- Reference: `{Path.GetFullPath(referencePath)}`");
    report.WriteLine($"- Reference alignment offset: {F(alignment.ReferenceOffsetSeconds)}s");
    report.WriteLine($"- Emulator alignment offset after compare start: {F(alignment.EmulatedOffsetSeconds)}s");
    report.WriteLine($"- Envelope correlation: {F(alignment.EnvelopeCorrelation)}");
    report.WriteLine();
    report.WriteLine("| Window | RMS Delta | Body Delta | Melody Delta | Sparkle Delta | Reference Notes | Emulator Notes |");
    report.WriteLine("| --- | ---: | ---: | ---: | ---: | --- | --- |");
    foreach (SonicAudioWindowAnalysis row in analyses)
    {
        report.WriteLine($"| {EscapeMarkdown(row.Name)} | {F(row.EmulatedStats.RmsDb - row.ReferenceStats.RmsDb)} | {F(row.EmulatedBands.BodyDb - row.ReferenceBands.BodyDb)} | {F(row.EmulatedBands.MelodyDb - row.ReferenceBands.MelodyDb)} | {F(row.EmulatedBands.SparkleDb - row.ReferenceBands.SparkleDb)} | {EscapeMarkdown(row.ReferenceTopNotes)} | {EscapeMarkdown(row.EmulatedTopNotes)} |");
    }

    foreach (SonicAudioWindowAnalysis row in analyses)
    {
        report.WriteLine();
        report.WriteLine($"## {row.Name}");
        report.WriteLine();
        report.WriteLine("| Stem | RMS | Melody | Sparkle | Top Notes |");
        report.WriteLine("| --- | ---: | ---: | ---: | --- |");
        foreach (SonicAudioStemWindow stem in row.Stems.OrderByDescending(stem => stem.Stats.RmsDb).Take(8))
        {
            report.WriteLine($"| {stem.Name} | {F(stem.Stats.RmsDb)} | {F(stem.Bands.MelodyDb)} | {F(stem.Bands.SparkleDb)} | {EscapeMarkdown(stem.TopNotes)} |");
        }

        report.WriteLine();
        report.WriteLine("| PSG | Active Frames | Avg Vol | Min Hz | Max Hz | Avg Hz |");
        report.WriteLine("| --- | ---: | ---: | ---: | ---: | ---: |");
        foreach (SonicPsgChannelTraceSummary channel in row.Psg.Channels)
        {
            report.WriteLine($"| {channel.Name} | {channel.ActiveFrames} | {F(channel.AverageVolume)} | {F(channel.MinFrequencyHz)} | {F(channel.MaxFrequencyHz)} | {F(channel.AverageFrequencyHz)} |");
        }

        report.WriteLine();
        report.WriteLine("| YM | Active Frames | Avg Hz | Min Hz | Max Hz | Avg Alg | Avg FB | Avg PMS | Avg AMS | Avg Carrier TL | Avg Carrier Env |");
        report.WriteLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (SonicYmChannelTraceSummary channel in row.Ym.Channels)
        {
            report.WriteLine($"| {channel.Name} | {channel.ActiveFrames} | {F(channel.AverageFrequencyHz)} | {F(channel.MinFrequencyHz)} | {F(channel.MaxFrequencyHz)} | {F(channel.AverageAlgorithm)} | {F(channel.AverageFeedback)} | {F(channel.AveragePhaseModulationSensitivity)} | {F(channel.AverageAmplitudeModulationSensitivity)} | {F(channel.AverageCarrierTotalLevel)} | {F(channel.AverageCarrierEnvelope)} |");
        }
    }

    Console.WriteLine($"Wrote Sonic {preset} window report to {reportPath}");
    Console.WriteLine($"Wrote Sonic {preset} window CSV to {csvPath}");
}

SonicAudioStemRender RenderSonicAudioStems(string romPath, string preset, int frames, int instructionsPerFrame)
{
    string[] names = ["ym1", "ym2", "ym3", "ym4", "ym5", "ym6-dac", "psg1", "psg2", "psg3", "psg-noise", "mixed"];
    List<short>[] stems = names.Select(_ => new List<short>(frames * 735 * AudioConstants.StereoChannels)).ToArray();
    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();
    int compareStartFrame = SonicAudioCompareStartFrame(preset);
    int compareStartSample = 0;
    short[] ymFrameStems = new short[6 * 1024 * AudioConstants.StereoChannels];
    short[] psgFrameStems = new short[4 * 1024];
    double[] filterLeft = new double[names.Length];
    double[] filterRight = new double[names.Length];
    double[] psgPreFilter = new double[4];
    double[] bassFilterLeft = new double[names.Length];
    double[] bassFilterRight = new double[names.Length];
    double filterAlpha = OutputLowPassAlpha(AudioConstants.DefaultSampleRate);
    double psgFilterAlpha = PsgLowPassAlpha(AudioConstants.DefaultSampleRate);
    double bassShelfAlpha = BassShelfAlpha(AudioConstants.DefaultSampleRate);
    List<SonicPsgTraceFrame> psgTrace = new(frames);
    List<SonicYmTraceFrame> ymTrace = new(frames);

    for (int frame = 0; frame < frames; frame++)
    {
        if (frame == compareStartFrame)
        {
            compareStartSample = stems[0].Count / AudioConstants.StereoChannels;
        }

        GenesisButton pressed = SonicAudioInputForPreset(preset, frame);
        machine.Bus.Controller1.Pressed = pressed;
        machine.Bus.Controller2.Pressed = pressed;
        machine.RunFrameCycles(instructionsPerFrame);
        int samples = machine.RenderFrameAudioStemSamplesInto(ymFrameStems, psgFrameStems);
        int frameStartSample = stems[0].Count / AudioConstants.StereoChannels;
        psgTrace.Add(new SonicPsgTraceFrame(frame, frameStartSample, machine.MainCpu.PC, machine.Psg.GetChannelSnapshots(), machine.Psg.GetNoiseSnapshot()));
        ymTrace.Add(new SonicYmTraceFrame(frame, frameStartSample, machine.MainCpu.PC, machine.Ym2612.GetChannelSnapshots()));

        for (int sample = 0; sample < samples; sample++)
        {
            double mixedLeft = 0.0;
            double mixedRight = 0.0;
            for (int channel = 0; channel < 6; channel++)
            {
                int offset = ((channel * samples) + sample) * AudioConstants.StereoChannels;
                int stem = channel;
                double ymMix = AudioConstants.YmMixLevel * AudioConstants.YmChannelMixLevel(channel);
                double rawLeft = ymFrameStems[offset] * ymMix;
                double rawRight = ymFrameStems[offset + 1] * ymMix;
                bassFilterLeft[stem] += (rawLeft - bassFilterLeft[stem]) * bassShelfAlpha;
                bassFilterRight[stem] += (rawRight - bassFilterRight[stem]) * bassShelfAlpha;
                rawLeft += bassFilterLeft[stem] * AudioConstants.BassShelfGain;
                rawRight += bassFilterRight[stem] * AudioConstants.BassShelfGain;
                filterLeft[stem] += (rawLeft - filterLeft[stem]) * filterAlpha;
                filterRight[stem] += (rawRight - filterRight[stem]) * filterAlpha;
                short left = AudioConstants.ClampSample(filterLeft[stem] * AudioConstants.MasterMixLevel);
                short right = AudioConstants.ClampSample(filterRight[stem] * AudioConstants.MasterMixLevel);
                stems[channel].Add(left);
                stems[channel].Add(right);
                mixedLeft += left;
                mixedRight += right;
            }

            for (int channel = 0; channel < 4; channel++)
            {
                int offset = (channel * samples) + sample;
                int stem = 6 + channel;
                psgPreFilter[channel] += (psgFrameStems[offset] - psgPreFilter[channel]) * psgFilterAlpha;
                double raw = psgPreFilter[channel] * AudioConstants.PsgMixLevel * AudioConstants.PsgChannelMixLevel(channel);
                bassFilterLeft[stem] += (raw - bassFilterLeft[stem]) * bassShelfAlpha;
                bassFilterRight[stem] += (raw - bassFilterRight[stem]) * bassShelfAlpha;
                double rawLeft = raw + (bassFilterLeft[stem] * AudioConstants.BassShelfGain);
                double rawRight = raw + (bassFilterRight[stem] * AudioConstants.BassShelfGain);
                filterLeft[stem] += (rawLeft - filterLeft[stem]) * filterAlpha;
                filterRight[stem] += (rawRight - filterRight[stem]) * filterAlpha;
                short monoLeft = AudioConstants.ClampSample(filterLeft[stem] * AudioConstants.MasterMixLevel);
                short monoRight = AudioConstants.ClampSample(filterRight[stem] * AudioConstants.MasterMixLevel);
                stems[stem].Add(monoLeft);
                stems[stem].Add(monoRight);
                mixedLeft += monoLeft;
                mixedRight += monoRight;
            }

            stems[^1].Add(AudioConstants.LimitOutputSample(mixedLeft));
            stems[^1].Add(AudioConstants.LimitOutputSample(mixedRight));
        }
    }

    machine.Bus.Controller1.Pressed = GenesisButton.None;
    machine.Bus.Controller2.Pressed = GenesisButton.None;
    return new SonicAudioStemRender(names, stems.Select(stem => stem.ToArray()).ToArray(), compareStartFrame, compareStartSample, psgTrace.ToArray(), ymTrace.ToArray());
}

SonicAudioStemRender RenderAudioStems(string romPath, int frames, int instructionsPerFrame, int compareStartFrame)
{
    string[] names = ["ym1", "ym2", "ym3", "ym4", "ym5", "ym6-dac", "psg1", "psg2", "psg3", "psg-noise", "mixed"];
    List<short>[] stems = names.Select(_ => new List<short>(frames * 735 * AudioConstants.StereoChannels)).ToArray();
    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();
    int compareStartSample = 0;
    short[] ymFrameStems = new short[6 * 1024 * AudioConstants.StereoChannels];
    short[] psgFrameStems = new short[4 * 1024];
    double[] filterLeft = new double[names.Length];
    double[] filterRight = new double[names.Length];
    double[] psgPreFilter = new double[4];
    double[] bassFilterLeft = new double[names.Length];
    double[] bassFilterRight = new double[names.Length];
    double filterAlpha = OutputLowPassAlpha(AudioConstants.DefaultSampleRate);
    double psgFilterAlpha = PsgLowPassAlpha(AudioConstants.DefaultSampleRate);
    double bassShelfAlpha = BassShelfAlpha(AudioConstants.DefaultSampleRate);
    List<SonicPsgTraceFrame> psgTrace = new(frames);
    List<SonicYmTraceFrame> ymTrace = new(frames);

    for (int frame = 0; frame < frames; frame++)
    {
        if (frame == compareStartFrame)
        {
            compareStartSample = stems[0].Count / AudioConstants.StereoChannels;
        }

        machine.Bus.Controller1.Pressed = GenesisButton.None;
        machine.Bus.Controller2.Pressed = GenesisButton.None;
        machine.RunFrameCycles(instructionsPerFrame);
        int samples = machine.RenderFrameAudioStemSamplesInto(ymFrameStems, psgFrameStems);
        int frameStartSample = stems[0].Count / AudioConstants.StereoChannels;
        psgTrace.Add(new SonicPsgTraceFrame(frame, frameStartSample, machine.MainCpu.PC, machine.Psg.GetChannelSnapshots(), machine.Psg.GetNoiseSnapshot()));
        ymTrace.Add(new SonicYmTraceFrame(frame, frameStartSample, machine.MainCpu.PC, machine.Ym2612.GetChannelSnapshots()));

        for (int sample = 0; sample < samples; sample++)
        {
            double mixedLeft = 0.0;
            double mixedRight = 0.0;
            for (int channel = 0; channel < 6; channel++)
            {
                int offset = ((channel * samples) + sample) * AudioConstants.StereoChannels;
                int stem = channel;
                double ymMix = AudioConstants.YmMixLevel * AudioConstants.YmChannelMixLevel(channel);
                double rawLeft = ymFrameStems[offset] * ymMix;
                double rawRight = ymFrameStems[offset + 1] * ymMix;
                bassFilterLeft[stem] += (rawLeft - bassFilterLeft[stem]) * bassShelfAlpha;
                bassFilterRight[stem] += (rawRight - bassFilterRight[stem]) * bassShelfAlpha;
                rawLeft += bassFilterLeft[stem] * AudioConstants.BassShelfGain;
                rawRight += bassFilterRight[stem] * AudioConstants.BassShelfGain;
                filterLeft[stem] += (rawLeft - filterLeft[stem]) * filterAlpha;
                filterRight[stem] += (rawRight - filterRight[stem]) * filterAlpha;
                short left = AudioConstants.ClampSample(filterLeft[stem] * AudioConstants.MasterMixLevel);
                short right = AudioConstants.ClampSample(filterRight[stem] * AudioConstants.MasterMixLevel);
                stems[channel].Add(left);
                stems[channel].Add(right);
                mixedLeft += left;
                mixedRight += right;
            }

            for (int channel = 0; channel < 4; channel++)
            {
                int offset = (channel * samples) + sample;
                int stem = 6 + channel;
                psgPreFilter[channel] += (psgFrameStems[offset] - psgPreFilter[channel]) * psgFilterAlpha;
                double raw = psgPreFilter[channel] * AudioConstants.PsgMixLevel * AudioConstants.PsgChannelMixLevel(channel);
                bassFilterLeft[stem] += (raw - bassFilterLeft[stem]) * bassShelfAlpha;
                bassFilterRight[stem] += (raw - bassFilterRight[stem]) * bassShelfAlpha;
                double rawLeft = raw + (bassFilterLeft[stem] * AudioConstants.BassShelfGain);
                double rawRight = raw + (bassFilterRight[stem] * AudioConstants.BassShelfGain);
                filterLeft[stem] += (rawLeft - filterLeft[stem]) * filterAlpha;
                filterRight[stem] += (rawRight - filterRight[stem]) * filterAlpha;
                short monoLeft = AudioConstants.ClampSample(filterLeft[stem] * AudioConstants.MasterMixLevel);
                short monoRight = AudioConstants.ClampSample(filterRight[stem] * AudioConstants.MasterMixLevel);
                stems[stem].Add(monoLeft);
                stems[stem].Add(monoRight);
                mixedLeft += monoLeft;
                mixedRight += monoRight;
            }

            stems[^1].Add(AudioConstants.LimitOutputSample(mixedLeft));
            stems[^1].Add(AudioConstants.LimitOutputSample(mixedRight));
        }
    }

    machine.Bus.Controller1.Pressed = GenesisButton.None;
    machine.Bus.Controller2.Pressed = GenesisButton.None;
    return new SonicAudioStemRender(names, stems.Select(stem => stem.ToArray()).ToArray(), compareStartFrame, compareStartSample, psgTrace.ToArray(), ymTrace.ToArray());
}

void WriteSonicPsgTrace(string path, IReadOnlyList<SonicPsgTraceFrame> frames)
{
    using StreamWriter writer = new(path, false, Encoding.UTF8);
    writer.WriteLine("frame,sample,pc,psg1Period,psg2Period,psg3Period,noisePeriod,psg1Hz,psg2Hz,psg3Hz,psg1Volume,psg2Volume,psg3Volume,noiseVolume,psg1Amplitude,psg2Amplitude,psg3Amplitude,noiseAmplitude,noiseControl,noiseWhite,noisePeriodMode,noiseShift");
    foreach (SonicPsgTraceFrame frame in frames)
    {
        Psg.PsgChannelSnapshot[] channels = frame.Channels;
        Psg.PsgNoiseSnapshot noise = frame.Noise;
        writer.WriteLine(string.Join(',',
            frame.Frame.ToString(CultureInfo.InvariantCulture),
            frame.Sample.ToString(CultureInfo.InvariantCulture),
            $"${frame.Pc:X8}",
            channels[0].Period.ToString(CultureInfo.InvariantCulture),
            channels[1].Period.ToString(CultureInfo.InvariantCulture),
            channels[2].Period.ToString(CultureInfo.InvariantCulture),
            channels[3].Period.ToString(CultureInfo.InvariantCulture),
            F(channels[0].FrequencyHz),
            F(channels[1].FrequencyHz),
            F(channels[2].FrequencyHz),
            F(channels[0].Volume),
            F(channels[1].Volume),
            F(channels[2].Volume),
            F(channels[3].Volume),
            channels[0].Amplitude.ToString(CultureInfo.InvariantCulture),
            channels[1].Amplitude.ToString(CultureInfo.InvariantCulture),
            channels[2].Amplitude.ToString(CultureInfo.InvariantCulture),
            channels[3].Amplitude.ToString(CultureInfo.InvariantCulture),
            $"${noise.Control:X1}",
            noise.WhiteNoise ? "1" : "0",
            noise.PeriodMode.ToString(CultureInfo.InvariantCulture),
            $"${noise.Shift:X4}"));
    }
}

SonicPsgTraceSummary SummarizeSonicPsgTrace(IReadOnlyList<SonicPsgTraceFrame> frames, int analysisStartSample, int analysisSamples)
{
    int analysisEndSample = analysisStartSample + analysisSamples;
    SonicPsgChannelTraceSummary[] channels = new SonicPsgChannelTraceSummary[4];
    for (int channel = 0; channel < channels.Length; channel++)
    {
        int activeFrames = 0;
        double volumeSum = 0.0;
        double frequencySum = 0.0;
        double minFrequency = double.PositiveInfinity;
        double maxFrequency = 0.0;
        foreach (SonicPsgTraceFrame frame in frames)
        {
            if (frame.Sample < analysisStartSample || frame.Sample >= analysisEndSample)
            {
                continue;
            }

            Psg.PsgChannelSnapshot snapshot = frame.Channels[channel];
            if (snapshot.Volume >= 15 || snapshot.Amplitude == 0)
            {
                continue;
            }

            activeFrames++;
            volumeSum += snapshot.Volume;
            if (channel < 3)
            {
                frequencySum += snapshot.FrequencyHz;
                minFrequency = Math.Min(minFrequency, snapshot.FrequencyHz);
                maxFrequency = Math.Max(maxFrequency, snapshot.FrequencyHz);
            }
        }

        channels[channel] = new SonicPsgChannelTraceSummary(
            channel < 3 ? $"psg{channel + 1}" : "psg-noise",
            activeFrames,
            activeFrames == 0 ? 0.0 : volumeSum / activeFrames,
            activeFrames == 0 || channel == 3 ? 0.0 : minFrequency,
            activeFrames == 0 || channel == 3 ? 0.0 : maxFrequency,
            activeFrames == 0 || channel == 3 ? 0.0 : frequencySum / activeFrames);
    }

    return new SonicPsgTraceSummary(channels);
}

SonicYmTraceSummary SummarizeSonicYmTrace(IReadOnlyList<SonicYmTraceFrame> frames, int analysisStartSample, int analysisSamples)
{
    int analysisEndSample = analysisStartSample + analysisSamples;
    SonicYmChannelTraceSummary[] channels = new SonicYmChannelTraceSummary[6];
    for (int channel = 0; channel < channels.Length; channel++)
    {
        int activeFrames = 0;
        double frequencySum = 0.0;
        double minFrequency = double.PositiveInfinity;
        double maxFrequency = 0.0;
        double algorithmSum = 0.0;
        double feedbackSum = 0.0;
        double pmsSum = 0.0;
        double amsSum = 0.0;
        double carrierTlSum = 0.0;
        double carrierEnvelopeSum = 0.0;
        foreach (SonicYmTraceFrame frame in frames)
        {
            if (frame.Sample < analysisStartSample || frame.Sample >= analysisEndSample)
            {
                continue;
            }

            Ym2612.Ym2612ChannelSnapshot snapshot = frame.Channels[channel];
            if (snapshot.KeyOn == 0 || CarrierEnvelopeForSnapshot(snapshot) <= 0)
            {
                continue;
            }

            double frequency = YmChannelFrequencyHz(snapshot.FNumber, snapshot.Block);
            activeFrames++;
            frequencySum += frequency;
            minFrequency = Math.Min(minFrequency, frequency);
            maxFrequency = Math.Max(maxFrequency, frequency);
            algorithmSum += snapshot.Algorithm;
            feedbackSum += snapshot.Feedback;
            pmsSum += snapshot.PhaseModulationSensitivity;
            amsSum += snapshot.AmplitudeModulationSensitivity;
            carrierTlSum += CarrierTotalLevelForSnapshot(snapshot);
            carrierEnvelopeSum += CarrierEnvelopeForSnapshot(snapshot);
        }

        channels[channel] = new SonicYmChannelTraceSummary(
            $"ym{channel + 1}",
            activeFrames,
            activeFrames == 0 ? 0.0 : frequencySum / activeFrames,
            activeFrames == 0 ? 0.0 : minFrequency,
            activeFrames == 0 ? 0.0 : maxFrequency,
            activeFrames == 0 ? 0.0 : algorithmSum / activeFrames,
            activeFrames == 0 ? 0.0 : feedbackSum / activeFrames,
            activeFrames == 0 ? 0.0 : pmsSum / activeFrames,
            activeFrames == 0 ? 0.0 : amsSum / activeFrames,
            activeFrames == 0 ? 0.0 : carrierTlSum / activeFrames,
            activeFrames == 0 ? 0.0 : carrierEnvelopeSum / activeFrames);
    }

    return new SonicYmTraceSummary(channels);
}

double YmChannelFrequencyHz(int fnum, int block)
{
    if (fnum <= 0)
    {
        return 0.0;
    }

    return fnum * Math.Pow(2.0, block - 1) * 7_670_454.0 / (144.0 * 1_048_576.0);
}

string FormatTopMusicalFrequencies(double[] samples, int start, int count, int take)
{
    if (count <= 0 || start < 0 || start >= samples.Length)
    {
        return string.Empty;
    }

    count = Math.Min(count, samples.Length - start);
    List<(int Midi, double Frequency, double Db)> candidates = [];
    for (int midi = 36; midi <= 108; midi++)
    {
        double frequency = 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);
        double power = GoertzelPower(samples, start, count, frequency, AudioConstants.DefaultSampleRate);
        double db = ToDb(Math.Sqrt(power));
        candidates.Add((midi, frequency, db));
    }

    List<(int Midi, double Frequency, double Db)> picked = [];
    foreach ((int midi, double frequency, double db) in candidates.OrderByDescending(candidate => candidate.Db))
    {
        if (db <= -180.0)
        {
            break;
        }

        if (picked.Any(existing => Math.Abs(existing.Midi - midi) <= 1))
        {
            continue;
        }

        picked.Add((midi, frequency, db));
        if (picked.Count >= take)
        {
            break;
        }
    }

    return string.Join("; ", picked.Select(note => $"{MidiNoteName(note.Midi)} {F(note.Frequency)}Hz {F(note.Db)}dB"));
}

string MidiNoteName(int midi)
{
    string[] names = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    int octave = (midi / 12) - 1;
    return $"{names[midi % 12]}{octave}";
}

double OutputLowPassAlpha(int sampleRate)
{
    double normalized = -2.0 * Math.PI * AudioConstants.OutputLowPassCutoffHz / Math.Max(1, sampleRate);
    return Math.Clamp(1.0 - Math.Exp(normalized), 0.0, 1.0);
}

double PsgLowPassAlpha(int sampleRate)
{
    double normalized = -2.0 * Math.PI * AudioConstants.PsgLowPassCutoffHz / Math.Max(1, sampleRate);
    return Math.Clamp(1.0 - Math.Exp(normalized), 0.0, 1.0);
}

double BassShelfAlpha(int sampleRate)
{
    double normalized = -2.0 * Math.PI * AudioConstants.BassShelfCutoffHz / Math.Max(1, sampleRate);
    return Math.Clamp(1.0 - Math.Exp(normalized), 0.0, 1.0);
}

double SonicStemAnalysisOffsetSeconds(string preset)
{
    if (preset.Equals("title", StringComparison.OrdinalIgnoreCase))
    {
        return 0.0;
    }

    return 8.0;
}

SonicAudioRender RenderSonicAudioForCompare(string romPath, string outputPath, string energyPath, string preset, int frames, int instructionsPerFrame)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(energyPath)) ?? ".");

    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();
    List<short> samples = new(capacity: frames * 735 * AudioConstants.StereoChannels);
    int compareStartFrame = SonicAudioCompareStartFrame(preset);
    int compareStartSample = 0;

    using StreamWriter writer = new(energyPath, false, Encoding.UTF8);
    writer.WriteLine("frame,pc,mixedEnergy,dacEnabled,dacSample,ym1,ym2,ym3,ym4,ym5,ym6,key1,key2,key3,key4,key5,key6,alg1,alg2,alg3,alg4,alg5,alg6,feedback1,feedback2,feedback3,feedback4,feedback5,feedback6,pms1,pms2,pms3,pms4,pms5,pms6,ams1,ams2,ams3,ams4,ams5,ams6,fnum1,fnum2,fnum3,fnum4,fnum5,fnum6,block1,block2,block3,block4,block5,block6,carrierTl1,carrierTl2,carrierTl3,carrierTl4,carrierTl5,carrierTl6,carrierEnv1,carrierEnv2,carrierEnv3,carrierEnv4,carrierEnv5,carrierEnv6,carrierStage1,carrierStage2,carrierStage3,carrierStage4,carrierStage5,carrierStage6");

    for (int frame = 0; frame < frames; frame++)
    {
        if (frame == compareStartFrame)
        {
            compareStartSample = samples.Count / AudioConstants.StereoChannels;
        }

        GenesisButton pressed = SonicAudioInputForPreset(preset, frame);
        machine.Bus.Controller1.Pressed = pressed;
        machine.Bus.Controller2.Pressed = pressed;
        machine.RunFrameCycles(instructionsPerFrame);

        long[] ymEnergy = new long[6];
        short[] frameSamples = machine.RenderFrameStereoAudioSamples(ymChannelEnergy: ymEnergy);
        samples.AddRange(frameSamples);
        WriteSonicEnergyRow(writer, machine, frame, frameSamples, ymEnergy);
    }

    if (compareStartFrame >= frames)
    {
        compareStartFrame = Math.Max(0, frames / 2);
        compareStartSample = Math.Min(samples.Count / AudioConstants.StereoChannels, compareStartFrame * 735);
    }

    machine.Bus.Controller1.Pressed = GenesisButton.None;
    machine.Bus.Controller2.Pressed = GenesisButton.None;
    short[] renderedSamples = samples.ToArray();
    WriteWav(outputPath, renderedSamples, AudioConstants.DefaultSampleRate, AudioConstants.StereoChannels);
    return new SonicAudioRender(renderedSamples, compareStartFrame, compareStartSample);
}

void WriteSonicEnergyRow(StreamWriter writer, MegaDrive machine, int frame, short[] frameSamples, long[] ymEnergy)
{
    long mixedEnergy = 0;
    for (int i = 0; i < frameSamples.Length; i += 32)
    {
        mixedEnergy += Math.Abs((int)frameSamples[i]);
    }

    Ym2612.Ym2612ChannelSnapshot[] snapshots = machine.Ym2612.GetChannelSnapshots();
    string keyColumns = string.Join(',', snapshots.Select(snapshot => $"${snapshot.KeyOn:X1}"));
    string algorithmColumns = string.Join(',', snapshots.Select(snapshot => snapshot.Algorithm.ToString(CultureInfo.InvariantCulture)));
    string feedbackColumns = string.Join(',', snapshots.Select(snapshot => snapshot.Feedback.ToString(CultureInfo.InvariantCulture)));
    string pmsColumns = string.Join(',', snapshots.Select(snapshot => snapshot.PhaseModulationSensitivity.ToString(CultureInfo.InvariantCulture)));
    string amsColumns = string.Join(',', snapshots.Select(snapshot => snapshot.AmplitudeModulationSensitivity.ToString(CultureInfo.InvariantCulture)));
    string fnumColumns = string.Join(',', snapshots.Select(snapshot => snapshot.FNumber.ToString(CultureInfo.InvariantCulture)));
    string blockColumns = string.Join(',', snapshots.Select(snapshot => snapshot.Block.ToString(CultureInfo.InvariantCulture)));
    string carrierTlColumns = string.Join(',', snapshots.Select(CarrierTotalLevelForSnapshot));
    string carrierEnvColumns = string.Join(',', snapshots.Select(CarrierEnvelopeForSnapshot));
    string carrierStageColumns = string.Join(',', snapshots.Select(CarrierStageForSnapshot));
    writer.WriteLine($"{frame},${machine.MainCpu.PC:X8},{mixedEnergy},{(machine.Ym2612.DacEnabled ? 1 : 0)},${machine.Ym2612.DacSample:X2},{ymEnergy[0]},{ymEnergy[1]},{ymEnergy[2]},{ymEnergy[3]},{ymEnergy[4]},{ymEnergy[5]},{keyColumns},{algorithmColumns},{feedbackColumns},{pmsColumns},{amsColumns},{fnumColumns},{blockColumns},{carrierTlColumns},{carrierEnvColumns},{carrierStageColumns}");
}

GenesisButton SonicAudioInputForPreset(string preset, int frame)
{
    if (preset.Equals("title", StringComparison.OrdinalIgnoreCase)
        || preset.Equals("attract", StringComparison.OrdinalIgnoreCase)
        || preset.Equals("idle", StringComparison.OrdinalIgnoreCase)
        || preset.Equals("demo", StringComparison.OrdinalIgnoreCase))
    {
        return GenesisButton.None;
    }

    GenesisButton buttons = frame is >= 620 and < 660 ? GenesisButton.Start : GenesisButton.None;
    if (preset.Equals("gameplay", StringComparison.OrdinalIgnoreCase) && frame >= 950)
    {
        buttons |= GenesisButton.Right;
        if (frame >= 1100 && frame % 120 < 18)
        {
            buttons |= GenesisButton.C;
        }
    }

    return buttons;
}

int SonicAudioCompareStartFrame(string preset)
{
    if (preset.Equals("title", StringComparison.OrdinalIgnoreCase))
    {
        return SonicTitleMusicStartFrame;
    }

    if (preset.Equals("attract", StringComparison.OrdinalIgnoreCase)
        || preset.Equals("idle", StringComparison.OrdinalIgnoreCase)
        || preset.Equals("demo", StringComparison.OrdinalIgnoreCase))
    {
        return 1_200;
    }

    return 900;
}

int CarrierTotalLevelForSnapshot(Ym2612.Ym2612ChannelSnapshot snapshot)
{
    int total = 0;
    foreach (int op in CarrierOperatorsForAlgorithm(snapshot.Algorithm))
    {
        total += snapshot.TotalLevels[op];
    }

    return total;
}

int CarrierEnvelopeForSnapshot(Ym2612.Ym2612ChannelSnapshot snapshot)
{
        int total = 0;
        foreach (int op in CarrierOperatorsForAlgorithm(snapshot.Algorithm))
        {
            total += 1024 - snapshot.Envelopes[op];
        }

    return total;
}

int CarrierStageForSnapshot(Ym2612.Ym2612ChannelSnapshot snapshot)
{
    int stage = 0;
    foreach (int op in CarrierOperatorsForAlgorithm(snapshot.Algorithm))
    {
        stage = Math.Max(stage, snapshot.Stages[op]);
    }

    return stage;
}

int[] CarrierOperatorsForAlgorithm(int algorithm)
{
    return algorithm switch
    {
        4 => [1, 3],
        5 or 6 => [1, 2, 3],
        7 => [0, 1, 2, 3],
        _ => [3],
    };
}

short[] DecodeReferenceAudio(string referencePath, string rawOutputPath)
{
    string ffmpeg = FindFfmpeg();
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(rawOutputPath)) ?? ".");
    if (File.Exists(rawOutputPath))
    {
        File.Delete(rawOutputPath);
    }

    System.Diagnostics.ProcessStartInfo startInfo = new(ffmpeg)
    {
        UseShellExecute = false,
        RedirectStandardError = true,
        RedirectStandardOutput = true,
        CreateNoWindow = true,
    };
    startInfo.ArgumentList.Add("-y");
    startInfo.ArgumentList.Add("-i");
    startInfo.ArgumentList.Add(referencePath);
    startInfo.ArgumentList.Add("-ac");
    startInfo.ArgumentList.Add(AudioConstants.StereoChannels.ToString(CultureInfo.InvariantCulture));
    startInfo.ArgumentList.Add("-ar");
    startInfo.ArgumentList.Add(AudioConstants.DefaultSampleRate.ToString(CultureInfo.InvariantCulture));
    startInfo.ArgumentList.Add("-f");
    startInfo.ArgumentList.Add("s16le");
    startInfo.ArgumentList.Add(rawOutputPath);

    using System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start ffmpeg.");
    string error = process.StandardError.ReadToEnd();
    string output = process.StandardOutput.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"ffmpeg failed while decoding reference audio.{Environment.NewLine}{output}{Environment.NewLine}{error}");
    }

    byte[] bytes = File.ReadAllBytes(rawOutputPath);
    short[] samples = new short[bytes.Length / sizeof(short)];
    for (int i = 0; i < samples.Length; i++)
    {
        int offset = i * sizeof(short);
        samples[i] = (short)(bytes[offset] | (bytes[offset + 1] << 8));
    }

    return samples;
}

string FindFfmpeg()
{
    string? pathEnvironment = Environment.GetEnvironmentVariable("PATH");
    if (!string.IsNullOrWhiteSpace(pathEnvironment))
    {
        foreach (string folder in pathEnvironment.Split(Path.PathSeparator))
        {
            string candidate = Path.Combine(folder.Trim(), OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    string bundled = Path.Combine(appData, "Python", "Python314", "site-packages", "imageio_ffmpeg", "binaries", "ffmpeg-win-x86_64-v7.1.exe");
    if (File.Exists(bundled))
    {
        return bundled;
    }

    string local = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
    if (File.Exists(local))
    {
        return local;
    }

    throw new FileNotFoundException("Could not find ffmpeg. Install ffmpeg or place ffmpeg.exe on PATH to decode reference audio.");
}

double[] ToMono(short[] stereoSamples, int startSample, int sampleCount)
{
    int available = Math.Max(0, (stereoSamples.Length / AudioConstants.StereoChannels) - startSample);
    int count = Math.Min(sampleCount, available);
    double[] mono = new double[count];
    for (int i = 0; i < count; i++)
    {
        int source = (startSample + i) * AudioConstants.StereoChannels;
        mono[i] = ((stereoSamples[source] + stereoSamples[source + 1]) * 0.5) / short.MaxValue;
    }

    return mono;
}

double[][] BuildAudioAlignmentFeatures(double[] samples, int window, int hop)
{
    if (samples.Length < window)
    {
        return [[], []];
    }

    int frames = 1 + ((samples.Length - window) / hop);
    double[] rms = new double[frames];
    double[] transient = new double[frames];
    for (int frame = 0; frame < frames; frame++)
    {
        int start = frame * hop;
        double sum = 0.0;
        double diffSum = 0.0;
        double previous = samples[start];
        for (int i = 0; i < window; i++)
        {
            double sample = samples[start + i];
            sum += sample * sample;
            if (i > 0)
            {
                double diff = sample - previous;
                diffSum += diff * diff;
            }

            previous = sample;
        }

        rms[frame] = Math.Sqrt(sum / window);
        double diffRms = Math.Sqrt(diffSum / Math.Max(1, window - 1));
        transient[frame] = diffRms / Math.Max(rms[frame], 1e-9);
    }

    return [rms, transient];
}

AudioAlignment FindBestAudioAlignment(double[][] referenceFeatures, double[][] emulatedFeatures, int sampleRate, int hop, double? requestedWindowSeconds = null, double? forcedReferenceStartSeconds = null, double? forcedEmulatedStartSeconds = null)
{
    const double forcedFineSearchSeconds = 0.10;
    double[] referenceEnvelope = referenceFeatures.Length > 0 ? referenceFeatures[0] : [];
    double[] emulatedEnvelope = emulatedFeatures.Length > 0 ? emulatedFeatures[0] : [];
    if (referenceEnvelope.Length == 0 || emulatedEnvelope.Length == 0)
    {
        return new AudioAlignment(0.0, 0.0, 0.0, 0.0);
    }

    double envelopeRate = sampleRate / (double)hop;
    double windowSeconds = Math.Clamp(requestedWindowSeconds ?? 10.0, 0.25, 10.0);
    int window = Math.Min((int)Math.Round(windowSeconds * envelopeRate), Math.Min(referenceEnvelope.Length, emulatedEnvelope.Length));
    window = Math.Max(8, window);

    if (forcedReferenceStartSeconds.HasValue || forcedEmulatedStartSeconds.HasValue)
    {
        int referenceStart = Math.Clamp((int)Math.Round((forcedReferenceStartSeconds ?? 0.0) * envelopeRate), 0, Math.Max(0, referenceEnvelope.Length - window));
        int emulatedStart = Math.Clamp((int)Math.Round((forcedEmulatedStartSeconds ?? 0.0) * envelopeRate), 0, Math.Max(0, emulatedEnvelope.Length - window));
        int forcedMaxReferenceStart = Math.Max(0, referenceEnvelope.Length - window);
        int forcedMaxEmulatedStart = Math.Max(0, emulatedEnvelope.Length - window);
        int radius = Math.Max(1, (int)Math.Round(forcedFineSearchSeconds * envelopeRate));
        int referenceSearchStart = Math.Max(0, referenceStart - radius);
        int referenceSearchEnd = Math.Min(forcedMaxReferenceStart, referenceStart + radius);
        int emulatedSearchStart = Math.Max(0, emulatedStart - radius);
        int emulatedSearchEnd = Math.Min(forcedMaxEmulatedStart, emulatedStart + radius);
        (int reference, int emulated, double correlation) forcedBest = (referenceStart, emulatedStart, double.NegativeInfinity);
        SearchAlignment(referenceFeatures, emulatedFeatures, window, referenceSearchStart, referenceSearchEnd, emulatedSearchStart, emulatedSearchEnd, 1, ref forcedBest);

        return new AudioAlignment(
            forcedBest.reference / envelopeRate,
            forcedBest.emulated / envelopeRate,
            window / envelopeRate,
            forcedBest.correlation,
            referenceStart / envelopeRate,
            emulatedStart / envelopeRate);
    }

    int maxReferenceStart = Math.Max(0, Math.Min(referenceEnvelope.Length - window, (int)Math.Round(45.0 * envelopeRate)));
    int maxEmulatedStart = Math.Max(0, Math.Min(emulatedEnvelope.Length - window, (int)Math.Round(8.0 * envelopeRate)));
    int stride = Math.Max(1, (int)Math.Round(0.25 * envelopeRate));
    (int reference, int emulated, double correlation) best = (0, 0, double.NegativeInfinity);

    SearchAlignment(referenceFeatures, emulatedFeatures, window, 0, maxReferenceStart, 0, maxEmulatedStart, stride, ref best);

    int fineReferenceStart = Math.Max(0, best.reference - stride);
    int fineReferenceEnd = Math.Min(maxReferenceStart, best.reference + stride);
    int fineEmulatedStart = Math.Max(0, best.emulated - stride);
    int fineEmulatedEnd = Math.Min(maxEmulatedStart, best.emulated + stride);
    SearchAlignment(referenceFeatures, emulatedFeatures, window, fineReferenceStart, fineReferenceEnd, fineEmulatedStart, fineEmulatedEnd, 1, ref best);

    return new AudioAlignment(
        best.reference / envelopeRate,
        best.emulated / envelopeRate,
        window / envelopeRate,
        best.correlation);
}

void SearchAlignment(double[][] referenceFeatures, double[][] emulatedFeatures, int window, int referenceStart, int referenceEnd, int emulatedStart, int emulatedEnd, int stride, ref (int reference, int emulated, double correlation) best)
{
    for (int reference = referenceStart; reference <= referenceEnd; reference += stride)
    {
        for (int emulated = emulatedStart; emulated <= emulatedEnd; emulated += stride)
        {
            double correlation = CorrelateFeatures(referenceFeatures, reference, emulatedFeatures, emulated, window);
            if (correlation > best.correlation)
            {
                best = (reference, emulated, correlation);
            }
        }
    }
}

double CorrelateFeatures(double[][] leftFeatures, int leftStart, double[][] rightFeatures, int rightStart, int count)
{
    int featureCount = Math.Min(leftFeatures.Length, rightFeatures.Length);
    if (featureCount == 0)
    {
        return 0.0;
    }

    double total = 0.0;
    double weightTotal = 0.0;
    for (int feature = 0; feature < featureCount; feature++)
    {
        double weight = feature == 0 ? 1.0 : 0.05;
        total += Correlate(leftFeatures[feature], leftStart, rightFeatures[feature], rightStart, count) * weight;
        weightTotal += weight;
    }

    return weightTotal <= 0.0 ? 0.0 : total / weightTotal;
}

double Correlate(double[] left, int leftStart, double[] right, int rightStart, int count)
{
    double leftMean = 0.0;
    double rightMean = 0.0;
    for (int i = 0; i < count; i++)
    {
        leftMean += left[leftStart + i];
        rightMean += right[rightStart + i];
    }

    leftMean /= count;
    rightMean /= count;

    double numerator = 0.0;
    double leftEnergy = 0.0;
    double rightEnergy = 0.0;
    for (int i = 0; i < count; i++)
    {
        double l = left[leftStart + i] - leftMean;
        double r = right[rightStart + i] - rightMean;
        numerator += l * r;
        leftEnergy += l * l;
        rightEnergy += r * r;
    }

    double denominator = Math.Sqrt(leftEnergy * rightEnergy);
    return denominator <= 0.0 ? 0.0 : numerator / denominator;
}

AudioSignalStats AnalyzeAudioSignal(double[] samples, int start, int count)
{
    if (count <= 0)
    {
        return new AudioSignalStats(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity);
    }

    double sum = 0.0;
    double diffSum = 0.0;
    double peak = 0.0;
    double previous = samples[start];
    for (int i = 0; i < count; i++)
    {
        double sample = samples[start + i];
        sum += sample * sample;
        peak = Math.Max(peak, Math.Abs(sample));
        if (i > 0)
        {
            double diff = sample - previous;
            diffSum += diff * diff;
        }

        previous = sample;
    }

    double rms = Math.Sqrt(sum / count);
    double diffRms = Math.Sqrt(diffSum / Math.Max(1, count - 1));
    return new AudioSignalStats(ToDb(rms), ToDb(peak), ToDb(diffRms / Math.Max(rms, 1e-12)));
}

AudioBandStats AnalyzeAudioBands(double[] samples, int start, int count)
{
    if (count <= 0)
    {
        return new AudioBandStats(-240.0, -240.0, -240.0, -240.0);
    }

    return new AudioBandStats(
        BandDb(samples, start, count, [82.41, 98.0, 110.0, 123.47, 146.83, 164.81, 196.0, 220.0, 246.94]),
        BandDb(samples, start, count, [261.63, 293.66, 329.63, 392.0, 440.0, 493.88, 587.33, 659.25, 783.99]),
        BandDb(samples, start, count, [880.0, 987.77, 1174.66, 1318.51, 1567.98, 1760.0, 1975.53, 2349.32, 2637.02]),
        BandDb(samples, start, count, [3135.96, 3520.0, 3951.07, 4698.64, 5274.04, 6271.93, 7040.0, 7902.13]));
}

double BandDb(double[] samples, int start, int count, double[] frequencies)
{
    double power = 0.0;
    foreach (double frequency in frequencies)
    {
        power += GoertzelPower(samples, start, count, frequency, AudioConstants.DefaultSampleRate);
    }

    double rms = Math.Sqrt(power / Math.Max(1, frequencies.Length));
    return ToDb(rms);
}

double GoertzelPower(double[] samples, int start, int count, double frequency, int sampleRate)
{
    double omega = Math.Tau * frequency / sampleRate;
    double coefficient = 2.0 * Math.Cos(omega);
    double s0;
    double s1 = 0.0;
    double s2 = 0.0;
    for (int i = 0; i < count; i++)
    {
        s0 = samples[start + i] + (coefficient * s1) - s2;
        s2 = s1;
        s1 = s0;
    }

    double power = (s1 * s1) + (s2 * s2) - (coefficient * s1 * s2);
    return Math.Max(0.0, power) / (count * (double)count);
}

double ToDb(double value)
{
    return value <= 1e-12 ? -240.0 : 20.0 * Math.Log10(value);
}

void WriteAudioCompareCsv(string path, string preset, int frames, int instructionsPerFrame, int compareStartFrame, AudioAlignment alignment, AudioSignalStats referenceStats, AudioSignalStats emulatedStats, AudioBandStats referenceBands, AudioBandStats emulatedBands, string emulatedWav, string energyCsv, string referencePath)
{
    double rmsDelta = emulatedStats.RmsDb - referenceStats.RmsDb;
    using StreamWriter writer = new(path, false, Encoding.UTF8);
    writer.WriteLine("preset,frames,instructionsPerFrame,compareStartFrame,referenceOffsetSeconds,emulatedOffsetSeconds,windowSeconds,envelopeCorrelation,referenceRmsDb,emulatedRmsDb,rmsDeltaDb,referencePeakDb,emulatedPeakDb,referenceBrightnessDb,emulatedBrightnessDb,brightnessDeltaDb,referenceBassDb,emulatedBassDb,bassDeltaDb,bassRelativeDeltaDb,referenceBodyDb,emulatedBodyDb,bodyDeltaDb,bodyRelativeDeltaDb,referenceMelodyDb,emulatedMelodyDb,melodyDeltaDb,melodyRelativeDeltaDb,referenceSparkleDb,emulatedSparkleDb,sparkleDeltaDb,sparkleRelativeDeltaDb,referencePath,emulatedWav,energyCsv");
    writer.WriteLine(string.Join(',',
        Csv(preset),
        frames.ToString(CultureInfo.InvariantCulture),
        instructionsPerFrame.ToString(CultureInfo.InvariantCulture),
        compareStartFrame.ToString(CultureInfo.InvariantCulture),
        F(alignment.ReferenceOffsetSeconds),
        F(alignment.EmulatedOffsetSeconds),
        F(alignment.WindowSeconds),
        F(alignment.EnvelopeCorrelation),
        F(referenceStats.RmsDb),
        F(emulatedStats.RmsDb),
        F(rmsDelta),
        F(referenceStats.PeakDb),
        F(emulatedStats.PeakDb),
        F(referenceStats.BrightnessDb),
        F(emulatedStats.BrightnessDb),
        F(emulatedStats.BrightnessDb - referenceStats.BrightnessDb),
        F(referenceBands.BassDb),
        F(emulatedBands.BassDb),
        F(emulatedBands.BassDb - referenceBands.BassDb),
        F(RelativeBandDelta(referenceBands.BassDb, emulatedBands.BassDb, rmsDelta)),
        F(referenceBands.BodyDb),
        F(emulatedBands.BodyDb),
        F(emulatedBands.BodyDb - referenceBands.BodyDb),
        F(RelativeBandDelta(referenceBands.BodyDb, emulatedBands.BodyDb, rmsDelta)),
        F(referenceBands.MelodyDb),
        F(emulatedBands.MelodyDb),
        F(emulatedBands.MelodyDb - referenceBands.MelodyDb),
        F(RelativeBandDelta(referenceBands.MelodyDb, emulatedBands.MelodyDb, rmsDelta)),
        F(referenceBands.SparkleDb),
        F(emulatedBands.SparkleDb),
        F(emulatedBands.SparkleDb - referenceBands.SparkleDb),
        F(RelativeBandDelta(referenceBands.SparkleDb, emulatedBands.SparkleDb, rmsDelta)),
        Csv(Path.GetFullPath(referencePath)),
        Csv(Path.GetFullPath(emulatedWav)),
        Csv(string.IsNullOrWhiteSpace(energyCsv) ? string.Empty : Path.GetFullPath(energyCsv))));
}

void WriteAudioCompareMarkdown(string path, string preset, int frames, int instructionsPerFrame, int compareStartFrame, AudioAlignment alignment, AudioSignalStats referenceStats, AudioSignalStats emulatedStats, AudioBandStats referenceBands, AudioBandStats emulatedBands, string emulatedWav, string energyCsv, string envelopeCsv, string referencePath, string referenceTopNotes = "", string emulatedTopNotes = "")
{
    double rmsDelta = emulatedStats.RmsDb - referenceStats.RmsDb;
    using StreamWriter writer = new(path, false, Encoding.UTF8);
    writer.WriteLine($"# {preset} Audio Compare");
    writer.WriteLine();
    writer.WriteLine($"- ROM render frames: {frames.ToString(CultureInfo.InvariantCulture)}");
    writer.WriteLine($"- Instructions per frame: {instructionsPerFrame.ToString(CultureInfo.InvariantCulture)}");
    writer.WriteLine($"- Emulator compare starts at frame: {compareStartFrame.ToString(CultureInfo.InvariantCulture)}");
    writer.WriteLine($"- Reference alignment offset: {F(alignment.ReferenceOffsetSeconds)}s");
    writer.WriteLine($"- Emulator alignment offset after compare start: {F(alignment.EmulatedOffsetSeconds)}s");
    WriteForcedAlignmentMarkdown(writer, alignment);
    writer.WriteLine($"- Compared window: {F(alignment.WindowSeconds)}s");
    writer.WriteLine($"- Envelope correlation: {F(alignment.EnvelopeCorrelation)}");
    writer.WriteLine($"- Gain normalization for relative band deltas: {F(rmsDelta)}dB");
    WriteAudioMixTuningMarkdown(writer);
    writer.WriteLine();
    writer.WriteLine("| Metric | Reference | Emulator | Delta |");
    writer.WriteLine("| --- | ---: | ---: | ---: |");
    writer.WriteLine($"| RMS dBFS | {F(referenceStats.RmsDb)} | {F(emulatedStats.RmsDb)} | {F(emulatedStats.RmsDb - referenceStats.RmsDb)} |");
    writer.WriteLine($"| Peak dBFS | {F(referenceStats.PeakDb)} | {F(emulatedStats.PeakDb)} | {F(emulatedStats.PeakDb - referenceStats.PeakDb)} |");
    writer.WriteLine($"| Brightness dB | {F(referenceStats.BrightnessDb)} | {F(emulatedStats.BrightnessDb)} | {F(emulatedStats.BrightnessDb - referenceStats.BrightnessDb)} |");
    writer.WriteLine($"| Bass band dB | {F(referenceBands.BassDb)} | {F(emulatedBands.BassDb)} | {F(emulatedBands.BassDb - referenceBands.BassDb)} |");
    writer.WriteLine($"| Body band dB | {F(referenceBands.BodyDb)} | {F(emulatedBands.BodyDb)} | {F(emulatedBands.BodyDb - referenceBands.BodyDb)} |");
    writer.WriteLine($"| Melody band dB | {F(referenceBands.MelodyDb)} | {F(emulatedBands.MelodyDb)} | {F(emulatedBands.MelodyDb - referenceBands.MelodyDb)} |");
    writer.WriteLine($"| Sparkle band dB | {F(referenceBands.SparkleDb)} | {F(emulatedBands.SparkleDb)} | {F(emulatedBands.SparkleDb - referenceBands.SparkleDb)} |");
    writer.WriteLine();
    writer.WriteLine("| Gain-normalized band | Delta |");
    writer.WriteLine("| --- | ---: |");
    writer.WriteLine($"| Bass relative dB | {F(RelativeBandDelta(referenceBands.BassDb, emulatedBands.BassDb, rmsDelta))} |");
    writer.WriteLine($"| Body relative dB | {F(RelativeBandDelta(referenceBands.BodyDb, emulatedBands.BodyDb, rmsDelta))} |");
    writer.WriteLine($"| Melody relative dB | {F(RelativeBandDelta(referenceBands.MelodyDb, emulatedBands.MelodyDb, rmsDelta))} |");
    writer.WriteLine($"| Sparkle relative dB | {F(RelativeBandDelta(referenceBands.SparkleDb, emulatedBands.SparkleDb, rmsDelta))} |");
    writer.WriteLine();
    if (!string.IsNullOrWhiteSpace(referenceTopNotes) || !string.IsNullOrWhiteSpace(emulatedTopNotes))
    {
        writer.WriteLine("| Signal | Top notes |");
        writer.WriteLine("| --- | --- |");
        writer.WriteLine($"| Reference | {EscapeMarkdown(referenceTopNotes)} |");
        writer.WriteLine($"| Emulator | {EscapeMarkdown(emulatedTopNotes)} |");
        writer.WriteLine();
    }

    writer.WriteLine($"Reference: `{Path.GetFullPath(referencePath)}`");
    writer.WriteLine($"Emulated WAV: `{Path.GetFullPath(emulatedWav)}`");
    if (!string.IsNullOrWhiteSpace(energyCsv))
    {
        writer.WriteLine($"YM energy CSV: `{Path.GetFullPath(energyCsv)}`");
    }

    writer.WriteLine($"Aligned envelope CSV: `{Path.GetFullPath(envelopeCsv)}`");
}

void WriteForcedAlignmentMarkdown(StreamWriter writer, AudioAlignment alignment)
{
    if (!alignment.RequestedReferenceOffsetSeconds.HasValue && !alignment.RequestedEmulatedOffsetSeconds.HasValue)
    {
        return;
    }

    double requestedReference = alignment.RequestedReferenceOffsetSeconds ?? alignment.ReferenceOffsetSeconds;
    double requestedEmulated = alignment.RequestedEmulatedOffsetSeconds ?? alignment.EmulatedOffsetSeconds;
    double referenceDelta = alignment.ReferenceOffsetSeconds - requestedReference;
    double emulatedDelta = alignment.EmulatedOffsetSeconds - requestedEmulated;
    writer.WriteLine($"- Requested reference alignment offset: {F(requestedReference)}s");
    writer.WriteLine($"- Requested emulator alignment offset after compare start: {F(requestedEmulated)}s");
    writer.WriteLine($"- Forced-window fine adjustment: reference {F(referenceDelta)}s, emulator {F(emulatedDelta)}s");
}

void WriteAudioStemCompareMarkdown(
    string path,
    string id,
    string romPath,
    string referencePath,
    int frames,
    int instructionsPerFrame,
    int compareStartFrame,
    AudioAlignment alignment,
    AudioSignalStats referenceStats,
    AudioSignalStats emulatedStats,
    AudioBandStats referenceBands,
    AudioBandStats emulatedBands,
    string referenceTopNotes,
    string emulatedTopNotes,
    IReadOnlyList<(string Name, string Wav, AudioSignalStats Stats, AudioBandStats Bands, string TopNotes)> stems,
    SonicPsgTraceSummary psgSummary,
    SonicYmTraceSummary ymSummary,
    string mixedWav,
    string psgTraceCsv,
    string envelopeCsv)
{
    double rmsDelta = emulatedStats.RmsDb - referenceStats.RmsDb;
    using StreamWriter writer = new(path, false, Encoding.UTF8);
    writer.WriteLine($"# {id} Audio Stem Compare");
    writer.WriteLine();
    writer.WriteLine($"- ROM: `{Path.GetFullPath(romPath)}`");
    writer.WriteLine($"- Reference: `{Path.GetFullPath(referencePath)}`");
    writer.WriteLine($"- ROM render frames: {frames.ToString(CultureInfo.InvariantCulture)}");
    writer.WriteLine($"- Instructions per frame: {instructionsPerFrame.ToString(CultureInfo.InvariantCulture)}");
    writer.WriteLine($"- Emulator compare starts at frame: {compareStartFrame.ToString(CultureInfo.InvariantCulture)}");
    writer.WriteLine($"- Reference alignment offset: {F(alignment.ReferenceOffsetSeconds)}s");
    writer.WriteLine($"- Emulator alignment offset after compare start: {F(alignment.EmulatedOffsetSeconds)}s");
    WriteForcedAlignmentMarkdown(writer, alignment);
    writer.WriteLine($"- Compared window: {F(alignment.WindowSeconds)}s");
    writer.WriteLine($"- Envelope correlation: {F(alignment.EnvelopeCorrelation)}");
    writer.WriteLine($"- Gain normalization for relative band deltas: {F(rmsDelta)}dB");
    writer.WriteLine();
    writer.WriteLine("| Metric | Reference | Emulator | Delta |");
    writer.WriteLine("| --- | ---: | ---: | ---: |");
    writer.WriteLine($"| RMS dBFS | {F(referenceStats.RmsDb)} | {F(emulatedStats.RmsDb)} | {F(emulatedStats.RmsDb - referenceStats.RmsDb)} |");
    writer.WriteLine($"| Peak dBFS | {F(referenceStats.PeakDb)} | {F(emulatedStats.PeakDb)} | {F(emulatedStats.PeakDb - referenceStats.PeakDb)} |");
    writer.WriteLine($"| Brightness dB | {F(referenceStats.BrightnessDb)} | {F(emulatedStats.BrightnessDb)} | {F(emulatedStats.BrightnessDb - referenceStats.BrightnessDb)} |");
    writer.WriteLine($"| Bass band dB | {F(referenceBands.BassDb)} | {F(emulatedBands.BassDb)} | {F(emulatedBands.BassDb - referenceBands.BassDb)} |");
    writer.WriteLine($"| Body band dB | {F(referenceBands.BodyDb)} | {F(emulatedBands.BodyDb)} | {F(emulatedBands.BodyDb - referenceBands.BodyDb)} |");
    writer.WriteLine($"| Melody band dB | {F(referenceBands.MelodyDb)} | {F(emulatedBands.MelodyDb)} | {F(emulatedBands.MelodyDb - referenceBands.MelodyDb)} |");
    writer.WriteLine($"| Sparkle band dB | {F(referenceBands.SparkleDb)} | {F(emulatedBands.SparkleDb)} | {F(emulatedBands.SparkleDb - referenceBands.SparkleDb)} |");
    writer.WriteLine();
    writer.WriteLine("| Gain-normalized band | Delta |");
    writer.WriteLine("| --- | ---: |");
    writer.WriteLine($"| Bass relative dB | {F(RelativeBandDelta(referenceBands.BassDb, emulatedBands.BassDb, rmsDelta))} |");
    writer.WriteLine($"| Body relative dB | {F(RelativeBandDelta(referenceBands.BodyDb, emulatedBands.BodyDb, rmsDelta))} |");
    writer.WriteLine($"| Melody relative dB | {F(RelativeBandDelta(referenceBands.MelodyDb, emulatedBands.MelodyDb, rmsDelta))} |");
    writer.WriteLine($"| Sparkle relative dB | {F(RelativeBandDelta(referenceBands.SparkleDb, emulatedBands.SparkleDb, rmsDelta))} |");
    writer.WriteLine();
    writer.WriteLine("| Signal | Top notes |");
    writer.WriteLine("| --- | --- |");
    writer.WriteLine($"| Reference | {EscapeMarkdown(referenceTopNotes)} |");
    writer.WriteLine($"| Emulator mixed | {EscapeMarkdown(emulatedTopNotes)} |");
    writer.WriteLine();
    writer.WriteLine("## Stem Attribution");
    writer.WriteLine();
    writer.WriteLine("| Stem | RMS | Peak | Brightness | Bass | Body | Melody | Sparkle | Top notes | WAV |");
    writer.WriteLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |");
    foreach ((string name, string wav, AudioSignalStats stats, AudioBandStats bands, string topNotes) in stems)
    {
        writer.WriteLine($"| {EscapeMarkdown(name)} | {F(stats.RmsDb)} | {F(stats.PeakDb)} | {F(stats.BrightnessDb)} | {F(bands.BassDb)} | {F(bands.BodyDb)} | {F(bands.MelodyDb)} | {F(bands.SparkleDb)} | {EscapeMarkdown(topNotes)} | `{Path.GetFileName(wav)}` |");
    }

    writer.WriteLine();
    writer.WriteLine("## PSG Activity");
    writer.WriteLine();
    writer.WriteLine("| Channel | Active Frames | Avg Vol | Min Hz | Max Hz | Avg Hz |");
    writer.WriteLine("| --- | ---: | ---: | ---: | ---: | ---: |");
    foreach (SonicPsgChannelTraceSummary channel in psgSummary.Channels)
    {
        writer.WriteLine($"| {channel.Name} | {channel.ActiveFrames} | {F(channel.AverageVolume)} | {F(channel.MinFrequencyHz)} | {F(channel.MaxFrequencyHz)} | {F(channel.AverageFrequencyHz)} |");
    }

    writer.WriteLine();
    writer.WriteLine("## YM Activity");
    writer.WriteLine();
    writer.WriteLine("| Channel | Active Frames | Avg Hz | Min Hz | Max Hz | Avg Alg | Avg FB | Avg PMS | Avg AMS | Avg Carrier TL | Avg Carrier Env |");
    writer.WriteLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
    foreach (SonicYmChannelTraceSummary channel in ymSummary.Channels)
    {
        writer.WriteLine($"| {channel.Name} | {channel.ActiveFrames} | {F(channel.AverageFrequencyHz)} | {F(channel.MinFrequencyHz)} | {F(channel.MaxFrequencyHz)} | {F(channel.AverageAlgorithm)} | {F(channel.AverageFeedback)} | {F(channel.AveragePhaseModulationSensitivity)} | {F(channel.AverageAmplitudeModulationSensitivity)} | {F(channel.AverageCarrierTotalLevel)} | {F(channel.AverageCarrierEnvelope)} |");
    }

    writer.WriteLine();
    writer.WriteLine($"Emulated mixed WAV: `{Path.GetFullPath(mixedWav)}`");
    writer.WriteLine($"PSG trace CSV: `{Path.GetFullPath(psgTraceCsv)}`");
    writer.WriteLine($"Aligned envelope CSV: `{Path.GetFullPath(envelopeCsv)}`");
}

void WriteAudioMixTuningMarkdown(StreamWriter writer)
{
    writer.WriteLine();
    writer.WriteLine("## Mix Tuning");
    writer.WriteLine();
    writer.WriteLine("| Channel | Gain | Env var |");
    writer.WriteLine("| --- | ---: | --- |");
    for (int channel = 0; channel < 6; channel++)
    {
        writer.WriteLine($"| ym{channel + 1} | {F(AudioConstants.YmChannelMixLevel(channel))} | `MDSHARP_YM_CH{channel + 1}_MIX_LEVEL` |");
    }

    for (int channel = 0; channel < 4; channel++)
    {
        string name = channel == 3 ? "psg-noise" : $"psg{channel + 1}";
        writer.WriteLine($"| {name} | {F(AudioConstants.PsgChannelMixLevel(channel))} | `MDSHARP_PSG_CH{channel + 1}_MIX_LEVEL` |");
    }
}

void WriteVgmStemCompareMarkdown(
    string path,
    string id,
    string vgmPath,
    string referencePath,
    double? maxSeconds,
    AudioAlignment alignment,
    AudioSignalStats referenceStats,
    AudioSignalStats emulatedStats,
    AudioBandStats referenceBands,
    AudioBandStats emulatedBands,
    string referenceTopNotes,
    string emulatedTopNotes,
    IReadOnlyList<(string Name, string Wav, AudioSignalStats Stats, AudioBandStats Bands, string TopNotes)> stems,
    string mixedWav,
    string envelopeCsv)
{
    double rmsDelta = emulatedStats.RmsDb - referenceStats.RmsDb;
    using StreamWriter writer = new(path, false, Encoding.UTF8);
    writer.WriteLine($"# {id} VGM Stem Compare");
    writer.WriteLine();
    writer.WriteLine($"- VGM: `{Path.GetFullPath(vgmPath)}`");
    writer.WriteLine($"- Reference: `{Path.GetFullPath(referencePath)}`");
    writer.WriteLine($"- VGM render limit: {(maxSeconds is > 0.0 ? F(maxSeconds.Value) + "s" : "full file")}");
    writer.WriteLine($"- Reference alignment offset: {F(alignment.ReferenceOffsetSeconds)}s");
    writer.WriteLine($"- VGM alignment offset: {F(alignment.EmulatedOffsetSeconds)}s");
    writer.WriteLine($"- Compared window: {F(alignment.WindowSeconds)}s");
    writer.WriteLine($"- Envelope correlation: {F(alignment.EnvelopeCorrelation)}");
    writer.WriteLine($"- Gain normalization for relative band deltas: {F(rmsDelta)}dB");
    WriteAudioMixTuningMarkdown(writer);
    writer.WriteLine();
    writer.WriteLine("| Metric | Reference | VGM | Delta |");
    writer.WriteLine("| --- | ---: | ---: | ---: |");
    writer.WriteLine($"| RMS dBFS | {F(referenceStats.RmsDb)} | {F(emulatedStats.RmsDb)} | {F(emulatedStats.RmsDb - referenceStats.RmsDb)} |");
    writer.WriteLine($"| Peak dBFS | {F(referenceStats.PeakDb)} | {F(emulatedStats.PeakDb)} | {F(emulatedStats.PeakDb - referenceStats.PeakDb)} |");
    writer.WriteLine($"| Brightness dB | {F(referenceStats.BrightnessDb)} | {F(emulatedStats.BrightnessDb)} | {F(emulatedStats.BrightnessDb - referenceStats.BrightnessDb)} |");
    writer.WriteLine($"| Bass band dB | {F(referenceBands.BassDb)} | {F(emulatedBands.BassDb)} | {F(emulatedBands.BassDb - referenceBands.BassDb)} |");
    writer.WriteLine($"| Body band dB | {F(referenceBands.BodyDb)} | {F(emulatedBands.BodyDb)} | {F(emulatedBands.BodyDb - referenceBands.BodyDb)} |");
    writer.WriteLine($"| Melody band dB | {F(referenceBands.MelodyDb)} | {F(emulatedBands.MelodyDb)} | {F(emulatedBands.MelodyDb - referenceBands.MelodyDb)} |");
    writer.WriteLine($"| Sparkle band dB | {F(referenceBands.SparkleDb)} | {F(emulatedBands.SparkleDb)} | {F(emulatedBands.SparkleDb - referenceBands.SparkleDb)} |");
    writer.WriteLine();
    writer.WriteLine("| Gain-normalized band | Delta |");
    writer.WriteLine("| --- | ---: |");
    writer.WriteLine($"| Bass relative dB | {F(RelativeBandDelta(referenceBands.BassDb, emulatedBands.BassDb, rmsDelta))} |");
    writer.WriteLine($"| Body relative dB | {F(RelativeBandDelta(referenceBands.BodyDb, emulatedBands.BodyDb, rmsDelta))} |");
    writer.WriteLine($"| Melody relative dB | {F(RelativeBandDelta(referenceBands.MelodyDb, emulatedBands.MelodyDb, rmsDelta))} |");
    writer.WriteLine($"| Sparkle relative dB | {F(RelativeBandDelta(referenceBands.SparkleDb, emulatedBands.SparkleDb, rmsDelta))} |");
    writer.WriteLine();
    writer.WriteLine("| Signal | Top notes |");
    writer.WriteLine("| --- | --- |");
    writer.WriteLine($"| Reference | {EscapeMarkdown(referenceTopNotes)} |");
    writer.WriteLine($"| VGM mixed | {EscapeMarkdown(emulatedTopNotes)} |");
    writer.WriteLine();
    writer.WriteLine("## Stem Attribution");
    writer.WriteLine();
    writer.WriteLine("| Stem | RMS | Peak | Brightness | Bass | Body | Melody | Sparkle | Top notes | WAV |");
    writer.WriteLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |");
    foreach ((string name, string wav, AudioSignalStats stats, AudioBandStats bands, string topNotes) in stems)
    {
        writer.WriteLine($"| {EscapeMarkdown(name)} | {F(stats.RmsDb)} | {F(stats.PeakDb)} | {F(stats.BrightnessDb)} | {F(bands.BassDb)} | {F(bands.BodyDb)} | {F(bands.MelodyDb)} | {F(bands.SparkleDb)} | {EscapeMarkdown(topNotes)} | `{Path.GetFileName(wav)}` |");
    }

    writer.WriteLine();
    writer.WriteLine($"VGM mixed WAV: `{Path.GetFullPath(mixedWav)}`");
    writer.WriteLine($"Aligned envelope CSV: `{Path.GetFullPath(envelopeCsv)}`");
}

void WriteAlignedEnvelopeCsv(string path, double[] referenceEnvelope, double[] emulatedEnvelope, AudioAlignment alignment, int sampleRate, int hop)
{
    double envelopeRate = sampleRate / (double)hop;
    int referenceStart = Math.Clamp((int)Math.Round(alignment.ReferenceOffsetSeconds * envelopeRate), 0, Math.Max(0, referenceEnvelope.Length - 1));
    int emulatedStart = Math.Clamp((int)Math.Round(alignment.EmulatedOffsetSeconds * envelopeRate), 0, Math.Max(0, emulatedEnvelope.Length - 1));
    int count = Math.Min((int)Math.Round(alignment.WindowSeconds * envelopeRate), Math.Min(referenceEnvelope.Length - referenceStart, emulatedEnvelope.Length - emulatedStart));

    using StreamWriter writer = new(path, false, Encoding.UTF8);
    writer.WriteLine("timeSeconds,referenceEnvelope,emulatedEnvelope");
    for (int i = 0; i < count; i++)
    {
        writer.WriteLine($"{F(i / envelopeRate)},{F(referenceEnvelope[referenceStart + i])},{F(emulatedEnvelope[emulatedStart + i])}");
    }
}

string F(double value)
{
    return value.ToString("0.######", CultureInfo.InvariantCulture);
}

string Csv(string value)
{
    return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}

void TraceAudio(string romPath, string outputPath, int frames, int instructionsPerFrame)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();

    int currentFrame = 0;
    long ymAddressWrites = 0;
    long ymDataWrites = 0;
    long psgWrites = 0;
    long dacSampleWrites = 0;
    long dacEnableWrites = 0;
    long keyOnWrites = 0;
    long busRequestWrites = 0;
    long resetWrites = 0;
    long firstDacCycle = -1;
    long lastDacCycle = -1;

    using StreamWriter writer = new(outputPath, false, Encoding.UTF8);
    writer.WriteLine("frame,cycle,source,chip,kind,port,register,value");
    machine.Bus.AudioObserver = access =>
    {
        if (access.Chip == AudioChip.Ym2612)
        {
            if (access.Kind == AudioAccessKind.Address)
            {
                ymAddressWrites++;
            }
            else
            {
                ymDataWrites++;
                if (access.Port == 0 && access.Register == 0x2A)
                {
                    dacSampleWrites++;
                    firstDacCycle = firstDacCycle < 0 ? access.MasterCycle : firstDacCycle;
                    lastDacCycle = access.MasterCycle;
                }
                else if (access.Port == 0 && access.Register == 0x2B)
                {
                    dacEnableWrites++;
                }
                else if (access.Port == 0 && access.Register == 0x28)
                {
                    keyOnWrites++;
                }
            }
        }
        else if (access.Chip == AudioChip.Psg)
        {
            psgWrites++;
        }

        writer.WriteLine($"{currentFrame},{access.MasterCycle},{access.Source},{access.Chip},{access.Kind},{access.Port},${access.Register:X2},${access.Value:X2}");
    };
    machine.Bus.Z80ControlObserver = access =>
    {
        if (access.Kind == Z80ControlKind.BusRequest)
        {
            busRequestWrites++;
        }
        else
        {
            resetWrites++;
        }

        writer.WriteLine($"{currentFrame},{access.MasterCycle},M68k,Z80Control,{access.Kind},0,{(access.ResetAsserted ? "reset" : "run")},{(access.BusRequested ? "busreq" : "busfree")}");
    };

    for (currentFrame = 0; currentFrame < frames; currentFrame++)
    {
        machine.RunFrameCycles(instructionsPerFrame);
        _ = machine.RenderFrameStereoAudioSamples();
    }

    machine.Bus.AudioObserver = null;
    machine.Bus.Z80ControlObserver = null;
    Console.WriteLine($"Wrote audio trace to {Path.GetFullPath(outputPath)}");
    Console.WriteLine($"YM address={ymAddressWrites:N0} YM data={ymDataWrites:N0} PSG={psgWrites:N0} keyOn={keyOnWrites:N0}");
    Console.WriteLine($"DAC sample writes={dacSampleWrites:N0} DAC enable writes={dacEnableWrites:N0} firstDAC={firstDacCycle} lastDAC={lastDacCycle}");
    Console.WriteLine($"Z80 bus writes={busRequestWrites:N0} reset writes={resetWrites:N0} final Z80 PC=${machine.Z80.PC:X4} B=${machine.Z80.B:X2} reset={(machine.Z80.ResetAsserted ? 1 : 0)} bus={(machine.Z80.BusRequested ? 1 : 0)} cycles={machine.Z80.Cycles:N0}");
}

void TraceZ80(string romPath, string outputPath, int frames, int maxLines, int instructionsPerFrame, int startFrame)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();
    int currentFrame = 0;
    int lines = 0;
    using StreamWriter writer = new(outputPath, false, Encoding.UTF8);
    writer.WriteLine("frame,cycle,pc,opcode,cycles,nextpc,a,b,c,d,e,h,l,ix,iy,bus,reset");
    machine.Z80InstructionObserver = trace =>
    {
        if (currentFrame < startFrame || lines >= maxLines || trace.Cycles <= 0)
        {
            return;
        }

        writer.WriteLine($"{currentFrame},{trace.MasterCycle},${trace.Pc:X4},${trace.Opcode:X2},{trace.Cycles},${trace.NextPc:X4},${trace.A:X2},${trace.B:X2},${trace.C:X2},${trace.D:X2},${trace.E:X2},${trace.H:X2},${trace.L:X2},${trace.IX:X4},${trace.IY:X4},{(trace.BusRequested ? 1 : 0)},{(trace.ResetAsserted ? 1 : 0)}");
        lines++;
    };

    for (currentFrame = 0; currentFrame < frames && lines < maxLines; currentFrame++)
    {
        machine.RunFrameCycles(instructionsPerFrame);
    }

    machine.Z80InstructionObserver = null;
    Console.WriteLine($"Wrote {lines:N0} Z80 trace line(s) to {Path.GetFullPath(outputPath)}");
    Console.WriteLine($"Final Z80 PC=${machine.Z80.PC:X4} A=${machine.Z80.A:X2} B=${machine.Z80.B:X2} reset={(machine.Z80.ResetAsserted ? 1 : 0)} bus={(machine.Z80.BusRequested ? 1 : 0)} cycles={machine.Z80.Cycles:N0}");
}

void TraceM68kLive(string romPath, string outputPath, int frames, int instructionsPerFrame, uint pcStart, uint pcEnd, int maxLines, int startFrame)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();
    if (pcEnd < pcStart)
    {
        (pcStart, pcEnd) = (pcEnd, pcStart);
    }

    int currentFrame = 0;
    int lines = 0;
    using StreamWriter writer = new(outputPath, false, Encoding.UTF8);
    writer.WriteLine("frame,sequence,pc,opcode,ext0,ext1,nextPc,sr,sp,d0,d1,d2,d3,d4,d5,d6,d7,a0,a1,a2,a3,a4,a5,a6,cycles,masterCycle,scanline,lineCycle,c400,c41d,sys20,sys22,masterPc,slavePc");
    Action<M68kCpu.M68kInstructionTrace> writeTrace = trace =>
    {
        if (lines >= maxLines || trace.Pc < pcStart || trace.Pc > pcEnd)
        {
            return;
        }

        ThirtyTwoXDevice? thirtyTwoX = machine.Bus.ThirtyTwoX;
        ushort sys20 = thirtyTwoX is null ? (ushort)0 : thirtyTwoX.ReadSystemRegisterWord(0x20);
        ushort sys22 = thirtyTwoX is null ? (ushort)0 : thirtyTwoX.ReadSystemRegisterWord(0x22);
        uint masterPc = thirtyTwoX?.MasterSh2.PC ?? 0;
        uint slavePc = thirtyTwoX?.SlaveSh2.PC ?? 0;
        ushort ext0 = machine.Bus.ReadWord(trace.Pc + 2);
        ushort ext1 = machine.Bus.ReadWord(trace.Pc + 4);
        writer.WriteLine(string.Join(
            ',',
            currentFrame.ToString(CultureInfo.InvariantCulture),
            (++lines).ToString(CultureInfo.InvariantCulture),
            $"${trace.Pc:X8}",
            $"${trace.Opcode:X4}",
            $"${ext0:X4}",
            $"${ext1:X4}",
            $"${trace.NextPc:X8}",
            $"${trace.Sr:X4}",
            $"${trace.StackPointer:X8}",
            $"${trace.D0:X8}",
            $"${trace.D1:X8}",
            $"${trace.D2:X8}",
            $"${trace.D3:X8}",
            $"${trace.D4:X8}",
            $"${trace.D5:X8}",
            $"${trace.D6:X8}",
            $"${trace.D7:X8}",
            $"${trace.A0:X8}",
            $"${trace.A1:X8}",
            $"${trace.A2:X8}",
            $"${trace.A3:X8}",
            $"${trace.A4:X8}",
            $"${trace.A5:X8}",
            $"${trace.A6:X8}",
            trace.Cycles.ToString(CultureInfo.InvariantCulture),
            machine.Bus.CurrentMasterCycle.ToString(CultureInfo.InvariantCulture),
            machine.Vdp.CurrentScanline.ToString(CultureInfo.InvariantCulture),
            machine.Bus.CurrentScanlineMasterCycleOffset.ToString(CultureInfo.InvariantCulture),
            $"${machine.Bus.ReadWord(0x00FF_C400):X4}",
            $"${machine.Bus.ReadByte(0x00FF_C41D):X2}",
            $"${sys20:X4}",
            $"${sys22:X4}",
            $"${masterPc:X8}",
            $"${slavePc:X8}"));
    };

    try
    {
        int endFrame = startFrame + frames;
        for (currentFrame = 0; currentFrame < endFrame && lines < maxLines; currentFrame++)
        {
            if (currentFrame == startFrame)
            {
                machine.MainCpu.InstructionObserver = writeTrace;
            }

            machine.RunFrameCycles(instructionsPerFrame);
        }
    }
    finally
    {
        machine.MainCpu.InstructionObserver = null;
    }

    Console.WriteLine($"Wrote {lines:N0} 68K live trace line(s) to {Path.GetFullPath(outputPath)}");
    Console.WriteLine(FormatState(machine));
}

void TraceM68kInterrupts(string romPath, string outputPath, int frames, int instructionsPerFrame, int maxLines, int startFrame)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();
    int currentFrame = 0;
    int lines = 0;
    using StreamWriter writer = new(outputPath, false, Encoding.UTF8);
    writer.WriteLine("frame,sequence,level,vector,returnPc,handlerPc,oldSr,newSr,sp,vdpReg0,vdpReg1,vInterruptPending,status,thirtyTwoXSys20,thirtyTwoXSys22");
    machine.MainCpu.InterruptObserver = trace =>
    {
        if (currentFrame < startFrame || lines >= maxLines)
        {
            return;
        }

        ThirtyTwoXDevice? thirtyTwoX = machine.Bus.ThirtyTwoX;
        ushort sys20 = thirtyTwoX is null ? (ushort)0 : thirtyTwoX.ReadSystemRegisterWord(0x20);
        ushort sys22 = thirtyTwoX is null ? (ushort)0 : thirtyTwoX.ReadSystemRegisterWord(0x22);
        writer.WriteLine(string.Join(
            ',',
            currentFrame.ToString(CultureInfo.InvariantCulture),
            (++lines).ToString(CultureInfo.InvariantCulture),
            trace.Level.ToString(CultureInfo.InvariantCulture),
            trace.Vector.ToString(CultureInfo.InvariantCulture),
            $"${trace.ReturnPc:X8}",
            $"${trace.HandlerPc:X8}",
            $"${trace.OldSr:X4}",
            $"${trace.NewSr:X4}",
            $"${trace.StackPointer:X8}",
            $"${machine.Vdp.Registers[0]:X2}",
            $"${machine.Vdp.Registers[1]:X2}",
            machine.Vdp.VInterruptPending ? "1" : "0",
            $"${machine.Vdp.Status:X4}",
            $"${sys20:X4}",
            $"${sys22:X4}"));
    };

    try
    {
        int endFrame = startFrame + frames;
        for (currentFrame = 0; currentFrame < endFrame && lines < maxLines; currentFrame++)
        {
            machine.RunFrameCycles(instructionsPerFrame);
        }
    }
    finally
    {
        machine.MainCpu.InterruptObserver = null;
    }

    Console.WriteLine($"Wrote {lines:N0} 68K interrupt trace line(s) to {Path.GetFullPath(outputPath)}");
    Console.WriteLine(FormatState(machine));
    Console.WriteLine(FormatVdpState(machine.Vdp));
}

void TraceThirtyTwoXM68kExceptions(string romPath, string outputPath, int frames, int instructionsPerFrame, int maxLines, int startFrame)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();

    int currentFrame = 0;
    int lines = 0;
    using StreamWriter writer = new(outputPath, false, Encoding.UTF8);
    writer.WriteLine("frame,sequence,vector,opcodePc,opcode,framePc,handlerPc,oldSr,newSr,sp,d0,d1,d2,d3,a0,a1,a2,a3,a4,a5,a6,rv,dreq,bank,aden,sh2Reset,sys00,sys02,sys04,sys06,sys20,sys22,vdpMode,fbctl,masterPc,slavePc,masterCycle,scanline,lineCycle");
    machine.MainCpu.ExceptionObserver = trace =>
    {
        if (currentFrame < startFrame || lines >= maxLines)
        {
            return;
        }

        ThirtyTwoXDevice? thirtyTwoX = machine.Bus.ThirtyTwoX;
        ushort sys00 = thirtyTwoX is null ? (ushort)0 : thirtyTwoX.ReadSystemRegisterWord(0x00);
        ushort sys02 = thirtyTwoX is null ? (ushort)0 : thirtyTwoX.ReadSystemRegisterWord(0x02);
        ushort sys04 = thirtyTwoX is null ? (ushort)0 : thirtyTwoX.ReadSystemRegisterWord(0x04);
        ushort sys06 = thirtyTwoX is null ? (ushort)0 : thirtyTwoX.ReadSystemRegisterWord(0x06);
        ushort sys20 = thirtyTwoX is null ? (ushort)0 : thirtyTwoX.ReadSystemRegisterWord(0x20);
        ushort sys22 = thirtyTwoX is null ? (ushort)0 : thirtyTwoX.ReadSystemRegisterWord(0x22);
        ushort vdpMode = thirtyTwoX is null ? (ushort)0 : thirtyTwoX.ReadVdpRegisterWord(0x00);
        ushort fbctl = thirtyTwoX is null ? (ushort)0 : thirtyTwoX.ReadVdpRegisterWord(0x0A);
        uint masterPc = thirtyTwoX?.MasterSh2.PC ?? 0;
        uint slavePc = thirtyTwoX?.SlaveSh2.PC ?? 0;
        writer.WriteLine(string.Join(
            ',',
            currentFrame.ToString(CultureInfo.InvariantCulture),
            (++lines).ToString(CultureInfo.InvariantCulture),
            trace.Vector.ToString(CultureInfo.InvariantCulture),
            $"${trace.OpcodePc:X8}",
            $"${trace.Opcode:X4}",
            $"${trace.FramePc:X8}",
            $"${trace.HandlerPc:X8}",
            $"${trace.OldSr:X4}",
            $"${trace.NewSr:X4}",
            $"${trace.StackPointer:X8}",
            $"${trace.D0:X8}",
            $"${trace.D1:X8}",
            $"${trace.D2:X8}",
            $"${trace.D3:X8}",
            $"${trace.A0:X8}",
            $"${trace.A1:X8}",
            $"${trace.A2:X8}",
            $"${trace.A3:X8}",
            $"${trace.A4:X8}",
            $"${trace.A5:X8}",
            $"${trace.A6:X8}",
            thirtyTwoX?.RomToVramDmaActive == true ? "1" : "0",
            $"${sys06:X4}",
            (thirtyTwoX?.M68kCartridgeBank ?? 0).ToString(CultureInfo.InvariantCulture),
            thirtyTwoX?.AdapterEnabled == true ? "1" : "0",
            thirtyTwoX?.Sh2HeldInReset == true ? "1" : "0",
            $"${sys00:X4}",
            $"${sys02:X4}",
            $"${sys04:X4}",
            $"${sys06:X4}",
            $"${sys20:X4}",
            $"${sys22:X4}",
            $"${vdpMode:X4}",
            $"${fbctl:X4}",
            $"${masterPc:X8}",
            $"${slavePc:X8}",
            machine.Bus.CurrentMasterCycle.ToString(CultureInfo.InvariantCulture),
            machine.Vdp.CurrentScanline.ToString(CultureInfo.InvariantCulture),
            machine.Bus.CurrentScanlineMasterCycleOffset.ToString(CultureInfo.InvariantCulture)));
    };

    try
    {
        int endFrame = startFrame + frames;
        for (currentFrame = 0; currentFrame < endFrame && lines < maxLines; currentFrame++)
        {
            machine.RunFrameCycles(instructionsPerFrame);
        }
    }
    finally
    {
        machine.MainCpu.ExceptionObserver = null;
    }

    Console.WriteLine($"Wrote {lines:N0} 32X 68K exception trace line(s) to {Path.GetFullPath(outputPath)}");
    Console.WriteLine(FormatState(machine));
}

void TraceThirtyTwoXSdkMonitor(string romPath, string outputPath, int frames, int instructionsPerFrame, int maxLines, int startFrame)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();

    int currentFrame = 0;
    int lines = 0;
    using StreamWriter writer = new(outputPath, false, Encoding.UTF8);
    writer.WriteLine("frame,sequence,event,pc,opcode,nextPc,vector,handlerPc,sr,sp,d0,d1,d2,d3,a0,a1,a6,service,ffd0a0,ffd0a2,ffd0a4,ffd0a6,ffd0a8,ffd0aa,ffd0ac,ffd0ae,ffd0b0,ffd0b2,sys00,sys02,masterPc,slavePc,masterSr,slaveSr,masterCycle,scanline,lineCycle");

    void WriteRow(
        string eventName,
        uint pc,
        ushort opcode,
        uint nextPc,
        int vector,
        uint handlerPc,
        ushort sr,
        uint sp,
        uint d0,
        uint d1,
        uint d2,
        uint d3,
        uint a0,
        uint a1,
        uint a6)
    {
        if (currentFrame < startFrame || lines >= maxLines)
        {
            return;
        }

        ThirtyTwoXDevice? thirtyTwoX = machine.Bus.ThirtyTwoX;
        ushort sys00 = thirtyTwoX is null ? (ushort)0 : thirtyTwoX.ReadSystemRegisterWord(0x00);
        ushort sys02 = thirtyTwoX is null ? (ushort)0 : thirtyTwoX.ReadSystemRegisterWord(0x02);
        uint masterPc = thirtyTwoX?.MasterSh2.PC ?? 0;
        uint slavePc = thirtyTwoX?.SlaveSh2.PC ?? 0;
        uint masterSr = thirtyTwoX?.MasterSh2.SR ?? 0;
        uint slaveSr = thirtyTwoX?.SlaveSh2.SR ?? 0;

        writer.WriteLine(string.Join(
            ',',
            currentFrame.ToString(CultureInfo.InvariantCulture),
            (++lines).ToString(CultureInfo.InvariantCulture),
            Csv(eventName),
            $"${pc:X8}",
            $"${opcode:X4}",
            nextPc == uint.MaxValue ? string.Empty : $"${nextPc:X8}",
            vector < 0 ? string.Empty : vector.ToString(CultureInfo.InvariantCulture),
            handlerPc == uint.MaxValue ? string.Empty : $"${handlerPc:X8}",
            $"${sr:X4}",
            $"${sp:X8}",
            $"${d0:X8}",
            $"${d1:X8}",
            $"${d2:X8}",
            $"${d3:X8}",
            $"${a0:X8}",
            $"${a1:X8}",
            $"${a6:X8}",
            $"${d0 & 0xFF:X2}",
            $"${machine.Bus.ReadWord(0x00FF_D0A0):X4}",
            $"${machine.Bus.ReadWord(0x00FF_D0A2):X4}",
            $"${machine.Bus.ReadWord(0x00FF_D0A4):X4}",
            $"${machine.Bus.ReadWord(0x00FF_D0A6):X4}",
            $"${machine.Bus.ReadWord(0x00FF_D0A8):X4}",
            $"${machine.Bus.ReadWord(0x00FF_D0AA):X4}",
            $"${machine.Bus.ReadWord(0x00FF_D0AC):X4}",
            $"${machine.Bus.ReadWord(0x00FF_D0AE):X4}",
            $"${machine.Bus.ReadWord(0x00FF_D0B0):X4}",
            $"${machine.Bus.ReadWord(0x00FF_D0B2):X4}",
            $"${sys00:X4}",
            $"${sys02:X4}",
            $"${masterPc:X8}",
            $"${slavePc:X8}",
            $"${masterSr:X8}",
            $"${slaveSr:X8}",
            machine.Bus.CurrentMasterCycle.ToString(CultureInfo.InvariantCulture),
            machine.Vdp.CurrentScanline.ToString(CultureInfo.InvariantCulture),
            machine.Bus.CurrentScanlineMasterCycleOffset.ToString(CultureInfo.InvariantCulture)));
    }

    machine.MainCpu.InstructionObserver = trace =>
    {
        if (!IsInterestingThirtyTwoXSdkMonitorInstruction(trace.Pc, trace.Opcode))
        {
            return;
        }

        WriteRow(
            "instruction",
            trace.Pc,
            trace.Opcode,
            trace.NextPc,
            -1,
            uint.MaxValue,
            trace.Sr,
            trace.StackPointer,
            trace.D0,
            trace.D1,
            trace.D2,
            trace.D3,
            trace.A0,
            trace.A1,
            trace.A6);
    };

    machine.MainCpu.ExceptionObserver = trace =>
    {
        if (!IsInterestingThirtyTwoXSdkMonitorInstruction(trace.OpcodePc, trace.Opcode) &&
            trace.OpcodePc is < 0x00FF_0000 or >= 0x00FF_C020)
        {
            return;
        }

        WriteRow(
            $"exception-{trace.Vector}",
            trace.OpcodePc,
            trace.Opcode,
            trace.FramePc,
            trace.Vector,
            trace.HandlerPc,
            trace.OldSr,
            trace.StackPointer,
            trace.D0,
            trace.D1,
            trace.D2,
            trace.D3,
            trace.A0,
            trace.A1,
            trace.A6);
    };

    try
    {
        int endFrame = startFrame + frames;
        for (currentFrame = 0; currentFrame < endFrame && lines < maxLines; currentFrame++)
        {
            machine.RunFrameCycles(instructionsPerFrame);
        }
    }
    finally
    {
        machine.MainCpu.InstructionObserver = null;
        machine.MainCpu.ExceptionObserver = null;
    }

    Console.WriteLine($"Wrote {lines:N0} 32X SDK monitor trace line(s) to {Path.GetFullPath(outputPath)}");
    Console.WriteLine(FormatState(machine));
}

static bool IsInterestingThirtyTwoXSdkMonitorInstruction(uint pc, ushort opcode)
{
    if (opcode == 0x4E40 || opcode == 0x4E4F || opcode >= 0xFF00)
    {
        return pc is >= 0x00FF_0000 and < 0x00FF_C020 ||
            pc is >= ThirtyTwoXHardwareProfile.M68kCartridgeFixedStart and < 0x00A0_0000;
    }

    return pc is >= 0x00FF_0000 and < 0x00FF_0028 ||
        pc is >= 0x00FF_00D0 and <= 0x00FF_00F4;
}

void TraceM68kMemoryWrites(string romPath, string outputPath, int frames, int instructionsPerFrame, uint addressStart, uint addressEnd, int maxLines, int startFrame)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();
    if (addressStart > addressEnd)
    {
        (addressStart, addressEnd) = (addressEnd, addressStart);
    }

    int currentFrame = 0;
    int lines = 0;
    using StreamWriter writer = new(outputPath, false, Encoding.UTF8);
    writer.WriteLine("frame,sequence,pc,address,value,sys20,sys22,masterPc,slavePc");
    machine.Bus.MemoryWriteObserver = write =>
    {
        if (currentFrame < startFrame || lines >= maxLines || write.Address < addressStart || write.Address > addressEnd)
        {
            return;
        }

        ThirtyTwoXDevice? thirtyTwoX = machine.Bus.ThirtyTwoX;
        ushort sys20 = thirtyTwoX is null ? (ushort)0 : thirtyTwoX.ReadSystemRegisterWord(0x20);
        ushort sys22 = thirtyTwoX is null ? (ushort)0 : thirtyTwoX.ReadSystemRegisterWord(0x22);
        uint masterPc = thirtyTwoX?.MasterSh2.PC ?? 0;
        uint slavePc = thirtyTwoX?.SlaveSh2.PC ?? 0;
        writer.WriteLine(string.Join(
            ',',
            currentFrame.ToString(CultureInfo.InvariantCulture),
            (++lines).ToString(CultureInfo.InvariantCulture),
            $"${write.Pc:X8}",
            $"${write.Address:X8}",
            $"${write.Value:X2}",
            $"${sys20:X4}",
            $"${sys22:X4}",
            $"${masterPc:X8}",
            $"${slavePc:X8}"));
    };

    try
    {
        int endFrame = startFrame + frames;
        for (currentFrame = 0; currentFrame < endFrame && lines < maxLines; currentFrame++)
        {
            machine.RunFrameCycles(instructionsPerFrame);
        }
    }
    finally
    {
        machine.Bus.MemoryWriteObserver = null;
    }

    Console.WriteLine($"Wrote {lines:N0} 68K memory write trace line(s) to {Path.GetFullPath(outputPath)}");
    Console.WriteLine(FormatState(machine));
}

void TraceM68kMemoryReads(string romPath, string outputPath, int frames, int instructionsPerFrame, uint addressStart, uint addressEnd, int maxLines, int startFrame)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");
    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();
    if (addressStart > addressEnd)
    {
        (addressStart, addressEnd) = (addressEnd, addressStart);
    }

    int currentFrame = 0;
    int lines = 0;
    using StreamWriter writer = new(outputPath, false, Encoding.UTF8);
    writer.WriteLine("frame,sequence,pc,address,size,value,sys20,sys22,masterPc,slavePc");
    machine.Bus.MemoryReadObserver = read =>
    {
        uint endAddress = read.Address + (uint)Math.Max(0, read.Size - 1);
        if (currentFrame < startFrame || lines >= maxLines || endAddress < addressStart || read.Address > addressEnd)
        {
            return;
        }

        ThirtyTwoXDevice? thirtyTwoX = machine.Bus.ThirtyTwoX;
        ushort sys20 = thirtyTwoX is null ? (ushort)0 : thirtyTwoX.ReadSystemRegisterWord(0x20);
        ushort sys22 = thirtyTwoX is null ? (ushort)0 : thirtyTwoX.ReadSystemRegisterWord(0x22);
        uint masterPc = thirtyTwoX?.MasterSh2.PC ?? 0;
        uint slavePc = thirtyTwoX?.SlaveSh2.PC ?? 0;
        writer.WriteLine(string.Join(
            ',',
            currentFrame.ToString(CultureInfo.InvariantCulture),
            (++lines).ToString(CultureInfo.InvariantCulture),
            $"${read.Pc:X8}",
            $"${read.Address:X8}",
            read.Size.ToString(CultureInfo.InvariantCulture),
            $"${read.Value:X8}",
            $"${sys20:X4}",
            $"${sys22:X4}",
            $"${masterPc:X8}",
            $"${slavePc:X8}"));
    };

    try
    {
        int endFrame = startFrame + frames;
        for (currentFrame = 0; currentFrame < endFrame && lines < maxLines; currentFrame++)
        {
            machine.RunFrameCycles(instructionsPerFrame);
        }
    }
    finally
    {
        machine.Bus.MemoryReadObserver = null;
    }

    Console.WriteLine($"Wrote {lines:N0} 68K memory read trace line(s) to {Path.GetFullPath(outputPath)}");
    Console.WriteLine(FormatState(machine));
}

void InspectMachine(string romPath, int frames, int instructionsPerFrame, uint address, int bytes)
{
    CartridgeImage cartridge = CartridgeImage.FromFile(romPath);
    MegaDrive machine = new(cartridge, IsPalRegion(cartridge));
    machine.Reset();
    for (int frame = 0; frame < frames; frame++)
    {
        machine.RunFrameCycles(instructionsPerFrame);
    }

    Console.WriteLine($"Inspected {Path.GetFileName(romPath)} after {frames} frame(s)");
    Console.WriteLine(FormatState(machine));
    Console.WriteLine(FormatVdpState(machine.Vdp));
    for (uint line = address; line < address + bytes; line += 0x20)
    {
        Console.Write($"${line:X6}: ");
        for (uint offset = 0; offset < 0x20 && line + offset < address + bytes; offset += 2)
        {
            Console.Write($"{machine.Bus.ReadWord(line + offset):X4} ");
        }

        Console.WriteLine();
    }
}

void WatchMemory(string romPath, int startFrame, int frames, int instructionsPerFrame, uint address, int bytes)
{
    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();
    for (int frame = 0; frame < startFrame; frame++)
    {
        machine.RunFrameCycles(instructionsPerFrame);
    }

    uint start = address & 0x00FF_FFFF;
    uint end = start + (uint)Math.Max(1, bytes);
    int currentFrame = startFrame;
    machine.Bus.MemoryWriteObserver = write =>
    {
        if (write.Address >= start && write.Address < end)
        {
            Console.WriteLine($"frame={currentFrame} pc=${write.Pc:X6} ${write.Address:X6}=${write.Value:X2} objectX=${machine.Bus.ReadWord(0x00FF_D008):X4} xvel=${machine.Bus.ReadWord(0x00FF_D010):X4} input=${machine.Bus.ReadWord(0x00FF_F602):X4} cam=${machine.Bus.ReadWord(0x00FF_F700):X4}");
        }
    };

    for (int frame = 0; frame < frames; frame++)
    {
        currentFrame = startFrame + frame;
        machine.RunFrameCycles(instructionsPerFrame);
    }

    machine.Bus.MemoryWriteObserver = null;
    Console.WriteLine(FormatState(machine));
}

void TraceIo(string romPath, int startFrame, int frames, int instructionsPerFrame, Func<int, GenesisButton>? input = null, string inputName = "none")
{
    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();
    for (int frame = 0; frame < startFrame; frame++)
    {
        if (input is not null)
        {
            GenesisButton pressed = input(frame);
            machine.Bus.Controller1.Pressed = pressed;
            machine.Bus.Controller2.Pressed = pressed;
        }

        machine.RunFrameCycles(instructionsPerFrame);
    }

    int currentFrame = startFrame;
    int lines = 0;
    machine.Bus.IoObserver = access =>
    {
        if (lines >= 512)
        {
            return;
        }

        uint offset = access.Address & 0x1F;
        if (offset is not (0x03 or 0x05 or 0x09 or 0x0B))
        {
            return;
        }

        string kind = access.IsWrite ? "W" : "R";
        Console.WriteLine($"frame={currentFrame} pc=${access.Pc:X6} {kind} ${access.Address:X6}=${access.Value:X2} data1=${access.Data0:X2} ctrl1=${access.Control0:X2} pressed={inputName}");
        lines++;
    };

    for (int frame = 0; frame < frames; frame++)
    {
        currentFrame = startFrame + frame;
        GenesisButton pressed = input?.Invoke(currentFrame) ?? GenesisButton.None;
        machine.Bus.Controller1.Pressed = pressed;
        machine.Bus.Controller2.Pressed = pressed;
        machine.RunFrameCycles(instructionsPerFrame);
    }

    machine.Bus.IoObserver = null;
    machine.Bus.Controller1.Pressed = GenesisButton.None;
    machine.Bus.Controller2.Pressed = GenesisButton.None;
    Console.WriteLine(FormatState(machine));
}

void TracePc(string romPath, int frames, int instructions, int instructionsPerFrame, Func<int, GenesisButton>? input = null)
{
    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();
    for (int frame = 0; frame < frames; frame++)
    {
        if (input is not null)
        {
            GenesisButton pressed = input(frame);
            machine.Bus.Controller1.Pressed = pressed;
            machine.Bus.Controller2.Pressed = pressed;
        }

        machine.RunFrameCycles(instructionsPerFrame);
    }

    for (int i = 0; i < instructions; i++)
    {
        uint pc = machine.MainCpu.PC;
        ushort opcode = machine.Bus.ReadWord(pc);
        uint sp = machine.MainCpu.A[7];
        Console.WriteLine($"{i:D4} PC=${pc:X8} OP=${opcode:X4} SR=${machine.MainCpu.SR:X4} "
            + $"D0=${machine.MainCpu.D[0]:X8} D1=${machine.MainCpu.D[1]:X8} D2=${machine.MainCpu.D[2]:X8} D3=${machine.MainCpu.D[3]:X8} "
            + $"A0=${machine.MainCpu.A[0]:X8} A1=${machine.MainCpu.A[1]:X8} A2=${machine.MainCpu.A[2]:X8} A3=${machine.MainCpu.A[3]:X8} A4=${machine.MainCpu.A[4]:X8} "
            + $"A7=${sp:X8} SP0=${machine.Bus.ReadLong(sp):X8}");
        machine.StepInstruction();
    }

    Console.WriteLine(FormatState(machine));
}

void TraceSvp(string romPath, string outputPath, Func<int, ControllerInput> input, int frames, int instructionsPerFrame, int maxLines, int[] pcs, int startFrame = 0)
{
    MegaDrive machine = CreateMachine(romPath);
    if (machine.Bus.Svp is null)
    {
        throw new InvalidOperationException("The supplied ROM does not expose an SVP device.");
    }

    HashSet<int> pcSet = new(pcs);
    string fullOutputPath = Path.GetFullPath(outputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath) ?? ".");
    using StreamWriter writer = new(fullOutputPath, false, Encoding.UTF8);
    writer.WriteLine("frame,sequence,pc,opcode,next,asm,beforePc,afterPc,beforeStatus,afterStatus,beforeA,afterA,beforeX,afterX,beforeY,afterY,beforeSt,afterSt,beforeP,afterP,beforePmc,afterPmc,beforePm0,afterPm0,beforePm4,afterPm4,beforePointers,afterPointers");

    int currentFrame = 0;
    int lines = 0;
    ulong sequence = 0;
    machine.Bus.Svp.InstructionTraceFilter = pc => pcSet.Contains(pc);
    machine.Bus.Svp.InstructionObserver = trace =>
    {
        if (currentFrame < startFrame)
        {
            return;
        }

        if (lines >= maxLines)
        {
            return;
        }

        string asm = DisassembleSsp(trace.Opcode, trace.NextWord);
        SvpDevice.SvpInstructionSnapshot before = trace.Before;
        SvpDevice.SvpInstructionSnapshot after = trace.After;
        writer.WriteLine(
            $"{currentFrame},{++sequence},${trace.Pc:X5},${trace.Opcode:X4},${trace.NextWord:X4},\"{EscapeCsv(asm)}\","
            + $"${before.Pc:X4},${after.Pc:X4},${before.EmuStatus:X4},${after.EmuStatus:X4},"
            + $"${before.A:X8},${after.A:X8},${before.X:X8},${after.X:X8},${before.Y:X8},${after.Y:X8},"
            + $"${before.St >> 16:X4},${after.St >> 16:X4},${before.P:X8},${after.P:X8},${before.Pmc:X8},${after.Pmc:X8},"
            + $"${before.Pm0:X8},${after.Pm0:X8},${before.Pm4:X8},${after.Pm4:X8},${before.Pointers:X16},${after.Pointers:X16}");
        lines++;
    };

    machine.Reset();
    for (int frame = 0; frame < frames && lines < maxLines; frame++)
    {
        currentFrame = frame;
        ControllerInput pressed = input(frame);
        machine.Bus.Controller1.Pressed = pressed.Player1;
        machine.Bus.Controller2.Pressed = pressed.Player2;
        machine.RunFrameCycles(instructionsPerFrame);
    }

    machine.Bus.Svp.InstructionTraceFilter = null;
    machine.Bus.Svp.InstructionObserver = null;
    Console.WriteLine($"Wrote {lines:N0} SVP trace row(s) to {fullOutputPath}");
    Console.WriteLine(FormatState(machine));
}

void TraceSvpPmIo(string romPath, string outputPath, Func<int, ControllerInput> input, int frames, int instructionsPerFrame, int maxLines, int startFrame = 0)
{
    MegaDrive machine = CreateMachine(romPath);
    if (machine.Bus.Svp is null)
    {
        throw new InvalidOperationException("The supplied ROM does not expose an SVP device.");
    }

    string fullOutputPath = Path.GetFullPath(outputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath) ?? ".");
    using StreamWriter writer = new(fullOutputPath, false, Encoding.UTF8);
    writer.WriteLine("frame,sequence,pc,opcode,asm,register,direction,kind,mode,addressBefore,addressAfter,data,previous,stored,pmacBefore,pmacAfter,status,a,x,y,st,pointers");

    int currentFrame = 0;
    int lines = 0;
    ulong sequence = 0;
    machine.Bus.Svp.PmIoObserver = trace =>
    {
        if (currentFrame < startFrame)
        {
            return;
        }

        if (lines >= maxLines)
        {
            return;
        }

        string asm = DisassembleSsp(trace.Opcode, 0);
        writer.WriteLine(
            $"{currentFrame},{++sequence},${trace.Pc:X5},${trace.Opcode:X4},\"{EscapeCsv(asm)}\","
            + $"{trace.Register},{(trace.Write ? "W" : "R")},\"{EscapeCsv(trace.Kind)}\",${trace.Mode:X4},${trace.AddressBefore:X4},${trace.AddressAfter:X4},"
            + $"${trace.Data:X4},${trace.PreviousValue:X4},${trace.StoredValue:X4},${trace.PmacBefore:X8},${trace.PmacAfter:X8},"
            + $"${trace.EmuStatus:X4},${trace.A:X8},${trace.X:X4},${trace.Y:X4},${trace.St:X4},${trace.Pointers:X16}");
        lines++;
    };

    machine.Reset();
    for (int frame = 0; frame < frames && lines < maxLines; frame++)
    {
        currentFrame = frame;
        ControllerInput pressed = input(frame);
        machine.Bus.Controller1.Pressed = pressed.Player1;
        machine.Bus.Controller2.Pressed = pressed.Player2;
        machine.RunFrameCycles(instructionsPerFrame);
    }

    machine.Bus.Svp.PmIoObserver = null;
    Console.WriteLine($"Wrote {lines:N0} SVP PM trace row(s) to {fullOutputPath}");
    Console.WriteLine(FormatState(machine));
}

void TraceSvpPointers(string romPath, string outputPath, Func<int, ControllerInput> input, int frames, int instructionsPerFrame, int maxLines, int[] pcs, int startFrame = 0)
{
    MegaDrive machine = CreateMachine(romPath);
    if (machine.Bus.Svp is null)
    {
        throw new InvalidOperationException("The supplied ROM does not expose an SVP device.");
    }

    HashSet<int> pcSet = new(pcs);
    bool traceAllPcs = pcSet.Count == 0;
    string fullOutputPath = Path.GetFullPath(outputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath) ?? ".");
    using StreamWriter writer = new(fullOutputPath, false, Encoding.UTF8);
    writer.WriteLine("frame,sequence,pc,opcode,asm,operation,modifier,bankBase,pointer,pointerBefore,pointerAfter,ramAddress,value,indirectAddress,indirectValue,rpl,status,a,x,y,st,p,pointers");

    int currentFrame = 0;
    int lines = 0;
    ulong sequence = 0;
    machine.Bus.Svp.PointerObserver = trace =>
    {
        if (currentFrame < startFrame)
        {
            return;
        }

        if (lines >= maxLines)
        {
            return;
        }

        if (!traceAllPcs && !pcSet.Contains(trace.Pc))
        {
            return;
        }

        string asm = DisassembleSsp(trace.Opcode, 0);
        writer.WriteLine(
            $"{currentFrame},{++sequence},${trace.Pc:X5},${trace.Opcode:X4},\"{EscapeCsv(asm)}\",\"{EscapeCsv(trace.Operation)}\","
            + $"${trace.Modifier:X2},${trace.BankBase:X3},{trace.Pointer},${trace.PointerBefore:X2},${trace.PointerAfter:X2},"
            + $"${trace.RamAddress:X3},${trace.Value:X4},{(trace.IndirectAddress >= 0 ? $"${trace.IndirectAddress:X4}" : string.Empty)},"
            + $"{(trace.IndirectAddress >= 0 ? $"${trace.IndirectValue:X4}" : string.Empty)},{trace.Rpl},${trace.EmuStatus:X4},"
            + $"${trace.A:X8},${trace.X:X4},${trace.Y:X4},${trace.St:X4},${trace.P:X8},${trace.Pointers:X16}");
        lines++;
    };

    machine.Reset();
    for (int frame = 0; frame < frames && lines < maxLines; frame++)
    {
        currentFrame = frame;
        ControllerInput pressed = input(frame);
        machine.Bus.Controller1.Pressed = pressed.Player1;
        machine.Bus.Controller2.Pressed = pressed.Player2;
        machine.RunFrameCycles(instructionsPerFrame);
    }

    machine.Bus.Svp.PointerObserver = null;
    Console.WriteLine($"Wrote {lines:N0} SVP pointer trace row(s) to {fullOutputPath}");
    Console.WriteLine(FormatState(machine));
}

void TraceSvpWriteHistory(string romPath, string outputPath, Func<int, ControllerInput> input, int[] dramWords, int frames, int instructionsPerFrame, int historyLength, int maxEvents, int startFrame = 0)
{
    MegaDrive machine = CreateMachine(romPath);
    if (machine.Bus.Svp is null)
    {
        throw new InvalidOperationException("The supplied ROM does not expose an SVP device.");
    }

    HashSet<int> targets = new(dramWords.Select(word => word & 0xFFFF));
    if (targets.Count == 0)
    {
        throw new ArgumentException("At least one DRAM word address is required.", nameof(dramWords));
    }

    string fullOutputPath = Path.GetFullPath(outputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath) ?? ".");
    using StreamWriter writer = new(fullOutputPath, false, Encoding.UTF8);
    writer.WriteLine("event,historyIndex,frame,sequence,pc,opcode,next,asm,beforeA,afterA,beforeX,afterX,beforeY,afterY,beforeSt,afterSt,beforeP,afterP,beforePmc,afterPmc,beforePointers,afterPointers,writePc,writeOpcode,writeAsm,writeKind,writeMode,writeAddress,writeData,writePrevious,writeStored,writePmacBefore,writePmacAfter,writeA,writeX,writeY,writeSt");

    Queue<SvpHistoryEntry> history = new(historyLength);
    int currentFrame = 0;
    ulong sequence = 0;
    int events = 0;

    machine.Bus.Svp.InstructionObserver = trace =>
    {
        if (currentFrame < startFrame)
        {
            return;
        }

        if (history.Count >= historyLength)
        {
            history.Dequeue();
        }

        history.Enqueue(new SvpHistoryEntry(currentFrame, ++sequence, trace));
    };

    machine.Bus.Svp.PmIoObserver = trace =>
    {
        if (currentFrame < startFrame || events >= maxEvents || !trace.Write || !trace.Kind.StartsWith("Dram", StringComparison.Ordinal))
        {
            return;
        }

        if (!targets.Contains(trace.AddressBefore & 0xFFFF))
        {
            return;
        }

        events++;
        string writeAsm = DisassembleSsp(trace.Opcode, 0);
        SvpHistoryEntry[] entries = history.ToArray();
        for (int i = 0; i < entries.Length; i++)
        {
            SvpHistoryEntry entry = entries[i];
            SvpDevice.SvpInstructionTrace instruction = entry.Trace;
            SvpDevice.SvpInstructionSnapshot before = instruction.Before;
            SvpDevice.SvpInstructionSnapshot after = instruction.After;
            string asm = DisassembleSsp(instruction.Opcode, instruction.NextWord);
            writer.WriteLine(
                $"{events},{i - entries.Length},{entry.Frame},{entry.Sequence},${instruction.Pc:X5},${instruction.Opcode:X4},${instruction.NextWord:X4},\"{EscapeCsv(asm)}\","
                + $"${before.A:X8},${after.A:X8},${before.X:X8},${after.X:X8},${before.Y:X8},${after.Y:X8},${before.St >> 16:X4},${after.St >> 16:X4},"
                + $"${before.P:X8},${after.P:X8},${before.Pmc:X8},${after.Pmc:X8},${before.Pointers:X16},${after.Pointers:X16},"
                + $"${trace.Pc:X5},${trace.Opcode:X4},\"{EscapeCsv(writeAsm)}\",\"{EscapeCsv(trace.Kind)}\",${trace.Mode:X4},${trace.AddressBefore:X4},"
                + $"${trace.Data:X4},${trace.PreviousValue:X4},${trace.StoredValue:X4},${trace.PmacBefore:X8},${trace.PmacAfter:X8},"
                + $"${trace.A:X8},${trace.X:X4},${trace.Y:X4},${trace.St:X4}");
        }
    };

    machine.Reset();
    for (int frame = 0; frame < frames && events < maxEvents; frame++)
    {
        currentFrame = frame;
        ControllerInput pressed = input(frame);
        machine.Bus.Controller1.Pressed = pressed.Player1;
        machine.Bus.Controller2.Pressed = pressed.Player2;
        machine.RunFrameCycles(instructionsPerFrame);
    }

    machine.Bus.Svp.InstructionObserver = null;
    machine.Bus.Svp.PmIoObserver = null;
    Console.WriteLine($"Wrote {events:N0} SVP write-history event(s) to {fullOutputPath}");
    Console.WriteLine(FormatState(machine));
}

void TraceSvpBus(string romPath, string outputPath, Func<int, ControllerInput> input, int frames, int instructionsPerFrame, int maxLines, int[] addresses, int startFrame = 0)
{
    MegaDrive machine = CreateMachine(romPath);
    if (machine.Bus.Svp is null)
    {
        throw new InvalidOperationException("The supplied ROM does not expose an SVP device.");
    }

    HashSet<uint> addressSet = new(addresses.Select(address => (uint)address & 0x00FF_FFFE));
    bool traceAllAddresses = addressSet.Count == 0;
    string fullOutputPath = Path.GetFullPath(outputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath) ?? ".");
    using StreamWriter writer = new(fullOutputPath, false, Encoding.UTF8);
    writer.WriteLine("frame,sequence,masterCycle,pc,source,direction,address,size,value");

    int currentFrame = 0;
    int lines = 0;
    ulong sequence = 0;
    machine.Bus.SvpExternalObserver = access =>
    {
        if (currentFrame < startFrame || lines >= maxLines)
        {
            return;
        }

        uint address = access.Address & 0x00FF_FFFE;
        if (!traceAllAddresses && !addressSet.Contains(address))
        {
            return;
        }

        writer.WriteLine(
            $"{currentFrame},{++sequence},{access.MasterCycle},${access.Pc:X8},{(access.DuringDma ? "DMA" : "68K")},{(access.IsWrite ? "W" : "R")},"
            + $"${access.Address & 0x00FF_FFFF:X6},{access.SizeBytes},${access.Value:X8}");
        lines++;
    };

    machine.Reset();
    for (int frame = 0; frame < frames && lines < maxLines; frame++)
    {
        currentFrame = frame;
        ControllerInput pressed = input(frame);
        machine.Bus.Controller1.Pressed = pressed.Player1;
        machine.Bus.Controller2.Pressed = pressed.Player2;
        machine.RunFrameCycles(instructionsPerFrame);
    }

    machine.Bus.SvpExternalObserver = null;
    Console.WriteLine($"Wrote {lines:N0} SVP bus trace row(s) to {fullOutputPath}");
    Console.WriteLine(FormatState(machine));
}

void TraceDmaWords(string romPath, string outputPath, Func<int, ControllerInput> input, int frames, int instructionsPerFrame, int maxLines, int startFrame = 0, uint? sourcePrefix = null)
{
    MegaDrive machine = CreateMachine(romPath);
    string fullOutputPath = Path.GetFullPath(outputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath) ?? ".");
    using StreamWriter writer = new(fullOutputPath, false, Encoding.UTF8);
    writer.WriteLine("frame,sequence,mode,code,requestedSource,effectiveSource,destination,index,value,hasSourceSamples,sourceBeforeStep,sourceAfterTransfer,masterCycleStart,masterCycleEnd,scanline");

    int currentFrame = 0;
    int lines = 0;
    ulong sequence = 0;
    machine.Bus.DmaWordObserver = transfer =>
    {
        if (currentFrame < startFrame)
        {
            return;
        }

        if (lines >= maxLines)
        {
            return;
        }

        if (sourcePrefix.HasValue && (transfer.SourceAddress & 0x00FF_0000) != sourcePrefix.Value)
        {
            return;
        }

        writer.WriteLine($"{currentFrame},{++sequence},{transfer.Mode},${transfer.Code:X2},${transfer.RequestedSourceAddress:X6},${transfer.SourceAddress:X6},${transfer.DestinationAddress:X4},{transfer.WordIndex},${transfer.Value:X4},{transfer.HasSourceSamples},${transfer.SourceBeforeStep:X4},${transfer.SourceAfterTransfer:X4},{transfer.MasterCycleStart},{transfer.MasterCycleEnd},{transfer.Scanline}");
        lines++;
    };

    machine.Reset();
    for (int frame = 0; frame < frames && lines < maxLines; frame++)
    {
        currentFrame = frame;
        ControllerInput pressed = input(frame);
        machine.Bus.Controller1.Pressed = pressed.Player1;
        machine.Bus.Controller2.Pressed = pressed.Player2;
        machine.RunFrameCycles(instructionsPerFrame);
    }

    machine.Bus.DmaWordObserver = null;
    Console.WriteLine($"Wrote {lines:N0} DMA word trace row(s) to {fullOutputPath}");
    Console.WriteLine(FormatState(machine));
}

void TraceVdpPlane(string romPath, string outputPath, Func<int, ControllerInput> input, int frames, int instructionsPerFrame, VdpDebugLayer layer, int y, int startX, int endX, int step)
{
    MegaDrive machine = CreateMachine(romPath);
    machine.Reset();
    for (int frame = 0; frame < frames; frame++)
    {
        ControllerInput pressed = input(frame);
        machine.Bus.Controller1.Pressed = pressed.Player1;
        machine.Bus.Controller2.Pressed = pressed.Player2;
        machine.RunFrameCycles(instructionsPerFrame);
    }

    string fullOutputPath = Path.GetFullPath(outputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath) ?? ".");
    using StreamWriter writer = new(fullOutputPath, false, Encoding.UTF8);
    writer.WriteLine("x,y,layer,kind,planeX,lineY,activeWidth,xOffset,planeWidthTiles,planeHeightTiles,scrollX,scrollY,sourceX,sourceY,nameTableBase,nameTableAddress,name,tileIndex,tileAddress,pixelX,pixelY,effectivePixelX,effectivePixelY,palette,priority,colorIndex,packedByte");
    int left = Math.Clamp(Math.Min(startX, endX), 0, Vdp.ScreenWidth - 1);
    int right = Math.Clamp(Math.Max(startX, endX), 0, Vdp.ScreenWidth - 1);
    int row = Math.Clamp(y, 0, Vdp.ScreenHeight - 1);
    int lines = 0;
    for (int x = left; x <= right; x += step)
    {
        Vdp.VdpPlanePixelTrace trace = machine.Vdp.TracePlanePixel(layer, x, row);
        writer.WriteLine(
            $"{trace.ScreenX},{trace.ScreenY},{trace.Layer},{trace.SourceKind},{trace.PlaneX},{trace.LineY},{trace.ActiveWidth},{trace.XOffset},"
            + $"{trace.PlaneWidthTiles},{trace.PlaneHeightTiles},{trace.ScrollX},{trace.ScrollY},{trace.SourceX},{trace.SourceY},"
            + $"${trace.NameTableBase:X4},${trace.NameTableAddress:X4},${trace.Name:X4},{trace.TileIndex},${trace.TileAddress:X4},"
            + $"{trace.PixelX},{trace.PixelY},{trace.EffectivePixelX},{trace.EffectivePixelY},{trace.Palette},{(trace.Priority ? 1 : 0)},{trace.ColorIndex},${trace.PackedByte:X2}");
        lines++;
    }

    Console.WriteLine($"Wrote {lines:N0} VDP plane trace row(s) to {fullOutputPath}");
    Console.WriteLine(FormatState(machine));
}

void CorrelateSvpVdp(string romPath, string outputPath, Func<int, ControllerInput> input, int frames, int instructionsPerFrame, VdpDebugLayer layer, int y, int startX, int endX, int step, int traceStartFrame)
{
    MegaDrive machine = CreateMachine(romPath);
    if (machine.Bus.Svp is null)
    {
        throw new InvalidOperationException("The supplied ROM does not expose an SVP device.");
    }

    Dictionary<uint, DmaWordTransfer> latestDmaByDestination = new();
    Dictionary<int, (int Frame, SvpDevice.SvpPmIoTrace Trace)> latestPmWriteByDramWord = new();
    int currentFrame = 0;
    machine.Bus.DmaWordObserver = transfer =>
    {
        if (currentFrame < traceStartFrame || transfer.Mode != 0 || (transfer.Code & 0x0F) != 0x01)
        {
            return;
        }

        if (!TryMapSvpDramWord(transfer.RequestedSourceAddress, out _))
        {
            return;
        }

        latestDmaByDestination[transfer.DestinationAddress & 0xFFFE] = transfer;
    };

    machine.Bus.Svp.PmIoObserver = trace =>
    {
        if (currentFrame < traceStartFrame || !trace.Write || !trace.Kind.StartsWith("Dram", StringComparison.Ordinal))
        {
            return;
        }

        latestPmWriteByDramWord[trace.AddressBefore & 0xFFFF] = (currentFrame, trace);
    };

    machine.Reset();
    for (int frame = 0; frame < frames; frame++)
    {
        currentFrame = frame;
        ControllerInput pressed = input(frame);
        machine.Bus.Controller1.Pressed = pressed.Player1;
        machine.Bus.Controller2.Pressed = pressed.Player2;
        machine.RunFrameCycles(instructionsPerFrame);
    }

    machine.Bus.DmaWordObserver = null;
    machine.Bus.Svp.PmIoObserver = null;

    string fullOutputPath = Path.GetFullPath(outputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath) ?? ".");
    using StreamWriter writer = new(fullOutputPath, false, Encoding.UTF8);
    writer.WriteLine("x,y,layer,kind,colorIndex,packedByte,tileAddress,nameTableAddress,name,tileIndex,sourceX,sourceY,dmaSource,dmaDestination,dmaValue,svpDramWord,pmFramePc,pmPc,pmOpcode,pmAsm,pmKind,pmMode,pmData,pmPrevious,pmStored,pmPmacBefore,pmPmacAfter,pmA,pmX,pmY,pmSt");

    int left = Math.Clamp(Math.Min(startX, endX), 0, Vdp.ScreenWidth - 1);
    int right = Math.Clamp(Math.Max(startX, endX), 0, Vdp.ScreenWidth - 1);
    int row = Math.Clamp(y, 0, Vdp.ScreenHeight - 1);
    int lines = 0;
    for (int x = left; x <= right; x += step)
    {
        Vdp.VdpPlanePixelTrace pixel = machine.Vdp.TracePlanePixel(layer, x, row);
        latestDmaByDestination.TryGetValue((uint)(pixel.TileAddress & 0xFFFE), out DmaWordTransfer dma);
        bool hasDma = dma.SourceAddress != 0 || dma.Value != 0 || latestDmaByDestination.ContainsKey((uint)(pixel.TileAddress & 0xFFFE));
        int dramWord = 0;
        bool hasDram = hasDma && TryMapSvpDramWord(dma.SourceAddress, out dramWord);
        SvpDevice.SvpPmIoTrace pm = default;
        (int Frame, SvpDevice.SvpPmIoTrace Trace) pmRecord = default;
        bool hasPm = hasDram && latestPmWriteByDramWord.TryGetValue(dramWord, out pmRecord);
        if (hasPm)
        {
            pm = pmRecord.Trace;
        }

        string asm = hasPm ? DisassembleSsp(pm.Opcode, 0) : string.Empty;
        writer.WriteLine(
            $"{pixel.ScreenX},{pixel.ScreenY},{pixel.Layer},{pixel.SourceKind},{pixel.ColorIndex},${pixel.PackedByte:X2},"
            + $"${pixel.TileAddress:X4},${pixel.NameTableAddress:X4},${pixel.Name:X4},{pixel.TileIndex},{pixel.SourceX},{pixel.SourceY},"
            + $"{(hasDma ? $"${dma.SourceAddress:X6}" : string.Empty)},{(hasDma ? $"${dma.DestinationAddress:X4}" : string.Empty)},{(hasDma ? $"${dma.Value:X4}" : string.Empty)},"
            + $"{(hasDram ? $"${dramWord:X4}" : string.Empty)},"
            + $"{(hasPm ? pmRecord.Frame.ToString(CultureInfo.InvariantCulture) : string.Empty)},{(hasPm ? $"${pm.Pc:X5}" : string.Empty)},{(hasPm ? $"${pm.Opcode:X4}" : string.Empty)},\"{EscapeCsv(asm)}\","
            + $"{(hasPm ? pm.Kind : string.Empty)},{(hasPm ? $"${pm.Mode:X4}" : string.Empty)},{(hasPm ? $"${pm.Data:X4}" : string.Empty)},"
            + $"{(hasPm ? $"${pm.PreviousValue:X4}" : string.Empty)},{(hasPm ? $"${pm.StoredValue:X4}" : string.Empty)},"
            + $"{(hasPm ? $"${pm.PmacBefore:X8}" : string.Empty)},{(hasPm ? $"${pm.PmacAfter:X8}" : string.Empty)},"
            + $"{(hasPm ? $"${pm.A:X8}" : string.Empty)},{(hasPm ? $"${pm.X:X4}" : string.Empty)},{(hasPm ? $"${pm.Y:X4}" : string.Empty)},{(hasPm ? $"${pm.St:X4}" : string.Empty)}");
        lines++;
    }

    Console.WriteLine($"Wrote {lines:N0} SVP/VDP correlation row(s) to {fullOutputPath}");
    Console.WriteLine($"Captured {latestDmaByDestination.Count:N0} latest DMA destination word(s) and {latestPmWriteByDramWord.Count:N0} latest SVP DRAM writer(s) from frame {traceStartFrame:N0} onward.");
    Console.WriteLine(FormatState(machine));
}

bool CheckVirtuaRacingLayout(string romPath, string outputFolder, Func<int, ControllerInput> input, string script, int frames, int instructionsPerFrame, string? imageFormatPath)
{
    MegaDrive machine = CreateMachine(romPath);
    if (machine.Bus.Svp is null)
    {
        throw new InvalidOperationException("The supplied ROM does not expose an SVP device.");
    }

    Directory.CreateDirectory(outputFolder);
    List<CapturedDmaChunk> chunks = new();
    VirtuaRacingExpectedDma[] expectedDmas = GetVirtuaRacingExpectedDmas();
    Dictionary<(uint Source, uint Destination), List<DmaTransferSample>> latestExpectedSamples = new();
    Dictionary<uint, ushort> latestVramWordFromSvpDma = new();
    CapturedDmaChunk? currentChunk = null;
    List<DmaTransferSample>? currentSampleList = null;
    int currentFrame = 0;
    machine.Bus.DmaWordObserver = transfer =>
    {
        if (transfer.Mode != 0 || (transfer.Code & 0x0F) != 0x01 || !IsSvpDmaSource(transfer.RequestedSourceAddress))
        {
            currentChunk = null;
            return;
        }

        if (transfer.WordIndex == 0
            || currentChunk is null
            || transfer.RequestedSourceAddress != currentChunk.SourceAddress + ((uint)transfer.WordIndex * 2u)
            || transfer.DestinationAddress != ((currentChunk.DestinationAddress + ((uint)transfer.WordIndex * 2u)) & 0xFFFF))
        {
            currentChunk = new CapturedDmaChunk(currentFrame, transfer.RequestedSourceAddress, transfer.DestinationAddress & 0xFFFE, transfer.Code);
            chunks.Add(currentChunk);
            currentSampleList = expectedDmas.Any(expected => expected.SourceAddress == currentChunk.SourceAddress && expected.DestinationAddress == currentChunk.DestinationAddress)
                ? new List<DmaTransferSample>()
                : null;
            if (currentSampleList is not null)
            {
                latestExpectedSamples[(currentChunk.SourceAddress, currentChunk.DestinationAddress)] = currentSampleList;
            }
        }

        currentChunk.LengthWords = Math.Max(currentChunk.LengthWords, transfer.WordIndex + 1);
        latestVramWordFromSvpDma[transfer.DestinationAddress & 0xFFFE] = transfer.Value;
        currentSampleList?.Add(new DmaTransferSample(
            currentFrame,
            transfer.WordIndex,
            transfer.RequestedSourceAddress,
            transfer.SourceAddress,
            transfer.DestinationAddress & 0xFFFE,
            transfer.Value,
            transfer.HasSourceSamples,
            transfer.SourceBeforeStep,
            transfer.SourceAfterTransfer,
            transfer.MasterCycleStart,
            transfer.MasterCycleEnd,
            transfer.Scanline));
    };

    machine.Reset();
    int completedFrames = 0;
    try
    {
        for (; completedFrames < frames; completedFrames++)
        {
            currentFrame = completedFrames;
            ControllerInput pressed = input(completedFrames);
            machine.Bus.Controller1.Pressed = pressed.Player1;
            machine.Bus.Controller2.Pressed = pressed.Player2;
            machine.RunFrameCycles(instructionsPerFrame);
        }
    }
    catch (M68kException ex)
    {
        Console.WriteLine($"Execution stopped during Virtua Racing layout check: {ex.Message}");
    }
    finally
    {
        machine.Bus.DmaWordObserver = null;
    }

    byte[] rgb = machine.RenderFrameRgb();
    string framePath = Path.Combine(outputFolder, "virtua-racing-layout-frame.bmp");
    WriteBmp(framePath, Vdp.ScreenWidth, Vdp.ScreenHeight, rgb);
    string dmaCsvPath = Path.Combine(outputFolder, "virtua-racing-svp-dma.csv");
    WriteVirtuaRacingDmaCsv(dmaCsvPath, chunks);
    string dmaSampleCsvPath = Path.Combine(outputFolder, "virtua-racing-svp-dma-samples.csv");
    WriteVirtuaRacingDmaSampleCsv(dmaSampleCsvPath, expectedDmas, latestExpectedSamples);
    string dramDumpPath = Path.Combine(outputFolder, "virtua-racing-mdsharp-svp-dram.bin");
    WriteSvpDramDump(dramDumpPath, machine.Bus.Svp.CaptureState().Dram);

    Vdp.VdpState vdpState = machine.Vdp.CaptureState();
    string vramDumpPath = Path.Combine(outputFolder, "virtua-racing-mdsharp-vdp-vram.bin");
    File.WriteAllBytes(vramDumpPath, vdpState.Vram);
    string visibleVramDumpPath = Path.Combine(outputFolder, "virtua-racing-mdsharp-visible-vdp-vram.bin");
    File.WriteAllBytes(visibleVramDumpPath, machine.Vdp.CaptureVisibleFrameVramSnapshot());
    VirtuaRacingDmaMatch[] dmaMatches = expectedDmas
        .Select(expected => MatchExpectedVirtuaRacingDma(expected, chunks))
        .ToArray();
    string? mameVramPath = FindDefaultVirtuaRacingMameVramPath();
    string? dmaCompareCsvPath = null;
    IReadOnlyList<VirtuaRacingDmaReferenceComparison> dmaReferenceComparisons = [];
    if (mameVramPath is not null)
    {
        dmaCompareCsvPath = Path.Combine(outputFolder, "virtua-racing-mame-vdp-compare.csv");
        byte[] mameVram = File.ReadAllBytes(mameVramPath);
        dmaReferenceComparisons = CompareVirtuaRacingDmaAgainstReference(expectedDmas, latestExpectedSamples, vdpState.Vram, mameVram);
        WriteVirtuaRacingDmaReferenceComparisonCsv(dmaCompareCsvPath, dmaReferenceComparisons);
    }
    int vramCompared = 0;
    int vramMismatches = 0;
    foreach ((uint destination, ushort value) in latestVramWordFromSvpDma)
    {
        vramCompared++;
        if (ReadWordFromBytes(vdpState.Vram, (int)destination) != value)
        {
            vramMismatches++;
        }
    }

    int?[,]? expectedNameTable = LoadVirtuaRacingNameTable(imageFormatPath);
    IReadOnlyList<VirtuaRacingNameTableCheck> nameTableChecks = expectedNameTable is null
        ? []
        : CheckVirtuaRacingNameTables(vdpState.Vram, expectedNameTable);
    int latestDestinationOwnerTransferredMismatches = dmaReferenceComparisons
        .Where(comparison => comparison.IsLatestDestinationOwner)
        .Sum(comparison => comparison.TransferredMismatches);
    bool passed = dmaMatches.All(match => match.Matched)
        && vramMismatches == 0
        && (nameTableChecks.Count == 0 || nameTableChecks[0].ExactMismatchedCells == 0);

    string reportPath = Path.Combine(outputFolder, "virtua-racing-layout-report.md");
    WriteVirtuaRacingLayoutReport(
        reportPath,
        romPath,
        script,
        completedFrames,
        instructionsPerFrame,
        imageFormatPath,
        framePath,
        dmaCsvPath,
        chunks,
        dmaMatches,
        vramCompared,
        vramMismatches,
        nameTableChecks,
        dmaSampleCsvPath,
        dmaCompareCsvPath,
        dmaReferenceComparisons,
        passed,
        machine);

    Console.WriteLine($"Wrote Virtua Racing layout report to {Path.GetFullPath(reportPath)}");
    Console.WriteLine($"Wrote frame BMP to {Path.GetFullPath(framePath)}");
    Console.WriteLine($"Wrote DMA CSV to {Path.GetFullPath(dmaCsvPath)}");
    Console.WriteLine($"Wrote DMA sample CSV to {Path.GetFullPath(dmaSampleCsvPath)}");
    if (dmaCompareCsvPath is not null)
    {
        Console.WriteLine($"Wrote MAME VDP comparison CSV to {Path.GetFullPath(dmaCompareCsvPath)}");
    }

    Console.WriteLine($"Wrote SVP DRAM dump to {Path.GetFullPath(dramDumpPath)}");
    Console.WriteLine($"Wrote VDP VRAM dump to {Path.GetFullPath(vramDumpPath)}");
    Console.WriteLine($"Wrote visible-frame VDP VRAM dump to {Path.GetFullPath(visibleVramDumpPath)}");
    Console.WriteLine($"Expected DMA chunks matched: {dmaMatches.Count(match => match.Matched)}/{dmaMatches.Length}");
    Console.WriteLine($"SVP DMA VRAM words compared: {vramCompared:N0}, mismatches: {vramMismatches:N0}");
    Console.WriteLine($"Supplemental MAME transfer mismatches: {latestDestinationOwnerTransferredMismatches:N0}");
    if (nameTableChecks.Count > 0)
    {
        VirtuaRacingNameTableCheck consoleCheck = nameTableChecks[0];
        Console.WriteLine($"Best name table candidate: base=${consoleCheck.BaseAddress:X4} width={consoleCheck.TableWidth} exact mismatches={consoleCheck.ExactMismatchedCells:N0}, delta-adjusted mismatches={consoleCheck.BestDeltaMismatchedCells:N0} (delta=${consoleCheck.BestDelta:X3})");
    }

    Console.WriteLine($"Virtua Racing layout regression: {(passed ? "PASS" : "FAIL")}");
    Console.WriteLine(FormatState(machine));
    return passed;
}

void WriteSvpDramDump(string path, IReadOnlyList<ushort> dram)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
    using FileStream stream = File.Create(path);
    Span<byte> bytes = stackalloc byte[2];
    foreach (ushort word in dram)
    {
        bytes[0] = (byte)(word >> 8);
        bytes[1] = (byte)word;
        stream.Write(bytes);
    }
}

void WriteVirtuaRacingDmaSampleCsv(string path, IReadOnlyList<VirtuaRacingExpectedDma> expectedDmas, IReadOnlyDictionary<(uint Source, uint Destination), List<DmaTransferSample>> latestExpectedSamples)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
    using StreamWriter writer = new(path, false, Encoding.UTF8);
    writer.WriteLine("expectedSource,expectedDestination,frame,index,requestedSource,effectiveSource,destination,value,hasSourceSamples,sourceBeforeStep,sourceAfterTransfer,masterCycleStart,masterCycleEnd,scanline");
    foreach (VirtuaRacingExpectedDma expected in expectedDmas)
    {
        if (!latestExpectedSamples.TryGetValue((expected.SourceAddress, expected.DestinationAddress), out List<DmaTransferSample>? samples))
        {
            continue;
        }

        foreach (DmaTransferSample sample in samples)
        {
            writer.WriteLine(
                $"${expected.SourceAddress:X6},${expected.DestinationAddress:X4},{sample.Frame},{sample.Index},${sample.RequestedSourceAddress:X6},${sample.SourceAddress:X6},${sample.DestinationAddress:X4},${sample.Value:X4},"
                + $"{sample.HasSourceSamples},${sample.SourceBeforeStep:X4},${sample.SourceAfterTransfer:X4},{sample.MasterCycleStart},{sample.MasterCycleEnd},{sample.Scanline}");
        }
    }
}

string? FindDefaultVirtuaRacingMameVramPath()
{
    string[] candidates =
    [
        Path.Combine("render-output", "virtua-layout", "mame-reference", "virtua-racing-mame-vdp-vram.bin"),
        Path.Combine("render-output", "virtua-layout-current", "mame-reference", "virtua-racing-mame-vdp-vram.bin"),
    ];

    return candidates.FirstOrDefault(path => File.Exists(path) && new FileInfo(path).Length >= 64 * 1024);
}

IReadOnlyList<VirtuaRacingDmaReferenceComparison> CompareVirtuaRacingDmaAgainstReference(
    IReadOnlyList<VirtuaRacingExpectedDma> expectedDmas,
    IReadOnlyDictionary<(uint Source, uint Destination), List<DmaTransferSample>> latestExpectedSamples,
    byte[] mdVram,
    byte[] mameVram)
{
    List<VirtuaRacingDmaReferenceComparison> comparisons = new();
    for (int expectedIndex = 0; expectedIndex < expectedDmas.Count; expectedIndex++)
    {
        VirtuaRacingExpectedDma expected = expectedDmas[expectedIndex];
        bool isLatestDestinationOwner = !expectedDmas
            .Skip(expectedIndex + 1)
            .Any(later => VirtuaRacingDmaRangesOverlap(expected, later));
        latestExpectedSamples.TryGetValue((expected.SourceAddress, expected.DestinationAddress), out List<DmaTransferSample>? samples);
        int sampledWords = samples?.Count ?? 0;
        int comparedWords = Math.Min(expected.LengthWords, Math.Min((mdVram.Length - (int)expected.DestinationAddress) / 2, (mameVram.Length - (int)expected.DestinationAddress) / 2));
        int transferredMismatches = 0;
        int finalMismatches = 0;
        int sourceChangedDuringDma = 0;
        int firstTransferredMismatch = -1;
        int firstFinalMismatch = -1;
        for (int i = 0; i < comparedWords; i++)
        {
            int address = (int)expected.DestinationAddress + (i * 2);
            ushort mame = ReadWordFromBytes(mameVram, address);
            ushort mdFinal = ReadWordFromBytes(mdVram, address);
            if (mdFinal != mame)
            {
                if (firstFinalMismatch < 0)
                {
                    firstFinalMismatch = i;
                }

                finalMismatches++;
            }

            if (samples is not null && i < samples.Count)
            {
                DmaTransferSample sample = samples[i];
                if (sample.Value != mame)
                {
                    if (firstTransferredMismatch < 0)
                    {
                        firstTransferredMismatch = i;
                    }

                    transferredMismatches++;
                }

                if (sample.HasSourceSamples && sample.SourceBeforeStep != sample.SourceAfterTransfer)
                {
                    sourceChangedDuringDma++;
                }
            }
        }

        comparisons.Add(new VirtuaRacingDmaReferenceComparison(
            expected.SourceAddress,
            expected.DestinationAddress,
            expected.LengthWords,
            sampledWords,
            comparedWords,
            transferredMismatches,
            finalMismatches,
            sourceChangedDuringDma,
            firstTransferredMismatch,
            firstFinalMismatch,
            isLatestDestinationOwner));
    }

    return comparisons;
}

bool VirtuaRacingDmaRangesOverlap(VirtuaRacingExpectedDma first, VirtuaRacingExpectedDma second)
{
    uint firstStart = first.DestinationAddress;
    uint firstEnd = firstStart + ((uint)first.LengthWords * 2u);
    uint secondStart = second.DestinationAddress;
    uint secondEnd = secondStart + ((uint)second.LengthWords * 2u);
    return firstStart < secondEnd && secondStart < firstEnd;
}

void WriteVirtuaRacingDmaReferenceComparisonCsv(string path, IReadOnlyList<VirtuaRacingDmaReferenceComparison> comparisons)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
    using StreamWriter writer = new(path, false, Encoding.UTF8);
    writer.WriteLine("source,destination,lengthWords,sampledWords,comparedWords,transferredMismatches,finalVramMismatches,sourceChangedDuringDma,firstTransferredMismatchIndex,firstFinalMismatchIndex,isLatestDestinationOwner");
    foreach (VirtuaRacingDmaReferenceComparison comparison in comparisons)
    {
        writer.WriteLine(
            $"${comparison.SourceAddress:X6},${comparison.DestinationAddress:X4},{comparison.LengthWords},{comparison.SampledWords},{comparison.ComparedWords},"
            + $"{comparison.TransferredMismatches},{comparison.FinalVramMismatches},{comparison.SourceChangedDuringDma},{comparison.FirstTransferredMismatchIndex},{comparison.FirstFinalMismatchIndex},{comparison.IsLatestDestinationOwner}");
    }
}

string? FindDefaultVirtuaRacingImageFormatPath()
{
    string[] candidates =
    [
        Path.Combine("render-output", "svp-research", "svp_bsd", "svp", "imageformat.txt"),
        Path.Combine("docs", "research", "virtua-racing-imageformat.txt"),
    ];

    return candidates.FirstOrDefault(File.Exists);
}

void WriteVirtuaRacingDmaCsv(string path, IReadOnlyList<CapturedDmaChunk> chunks)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
    using StreamWriter writer = new(path, false, Encoding.UTF8);
    writer.WriteLine("frame,source,destination,lengthWords,code");
    foreach (CapturedDmaChunk chunk in chunks)
    {
        writer.WriteLine($"{chunk.Frame},${chunk.SourceAddress:X6},${chunk.DestinationAddress:X4},{chunk.LengthWords},${chunk.Code:X2}");
    }
}

void WriteVirtuaRacingLayoutReport(
    string path,
    string romPath,
    string script,
    int frames,
    int instructionsPerFrame,
    string? imageFormatPath,
    string framePath,
    string dmaCsvPath,
    IReadOnlyList<CapturedDmaChunk> chunks,
    IReadOnlyList<VirtuaRacingDmaMatch> dmaMatches,
    int vramCompared,
    int vramMismatches,
    IReadOnlyList<VirtuaRacingNameTableCheck> nameTableChecks,
    string dmaSampleCsvPath,
    string? dmaCompareCsvPath,
    IReadOnlyList<VirtuaRacingDmaReferenceComparison> dmaReferenceComparisons,
    bool passed,
    MegaDrive machine)
{
    using StreamWriter writer = new(path, false, Encoding.UTF8);
    writer.WriteLine("# Virtua Racing SVP Layout Check");
    writer.WriteLine();
    writer.WriteLine($"ROM: `{Path.GetFileName(romPath)}`");
    writer.WriteLine($"Script: `{script}`");
    writer.WriteLine($"Frames: `{frames.ToString(CultureInfo.InvariantCulture)}`");
    writer.WriteLine($"Instructions/frame: `{instructionsPerFrame.ToString(CultureInfo.InvariantCulture)}`");
    writer.WriteLine($"Image format reference: `{(imageFormatPath is not null ? Path.GetFullPath(imageFormatPath) : "not found")}`");
    writer.WriteLine($"Frame BMP: `{Path.GetFullPath(framePath)}`");
    writer.WriteLine($"DMA CSV: `{Path.GetFullPath(dmaCsvPath)}`");
    writer.WriteLine($"DMA sample CSV: `{Path.GetFullPath(dmaSampleCsvPath)}`");
    writer.WriteLine($"Regression status: `{(passed ? "PASS" : "FAIL")}`");
    if (dmaCompareCsvPath is not null)
    {
        writer.WriteLine($"MAME VDP comparison CSV: `{Path.GetFullPath(dmaCompareCsvPath)}`");
    }

    writer.WriteLine();
    writer.WriteLine("## Expected SVP DMA Chunks");
    writer.WriteLine();
    writer.WriteLine("| Expected | Matched | Observed frame | Observed |");
    writer.WriteLine("| --- | --- | ---: | --- |");
    foreach (VirtuaRacingDmaMatch match in dmaMatches)
    {
        string expected = $"${match.Expected.SourceAddress:X6}->${match.Expected.DestinationAddress:X4} words={match.Expected.LengthWords}";
        string observed = match.Observed is null
            ? ""
            : $"${match.Observed.SourceAddress:X6}->${match.Observed.DestinationAddress:X4} words={match.Observed.LengthWords}";
        writer.WriteLine($"| `{expected}` | {(match.Matched ? "yes" : "no")} | {(match.Observed is null ? "" : match.Observed.Frame.ToString(CultureInfo.InvariantCulture))} | `{observed}` |");
    }

    if (dmaReferenceComparisons.Count > 0)
    {
        int latestTransferredMismatches = dmaReferenceComparisons
            .Where(comparison => comparison.IsLatestDestinationOwner)
            .Sum(comparison => comparison.TransferredMismatches);
        int latestFinalSnapshotMismatches = dmaReferenceComparisons
            .Where(comparison => comparison.IsLatestDestinationOwner)
            .Sum(comparison => comparison.FinalVramMismatches);
        int overwrittenFinalSnapshotMismatches = dmaReferenceComparisons
            .Where(comparison => !comparison.IsLatestDestinationOwner)
            .Sum(comparison => comparison.FinalVramMismatches);

        writer.WriteLine();
        writer.WriteLine("## MAME VDP DMA Comparison");
        writer.WriteLine();
        writer.WriteLine($"Latest destination-owner transferred mismatches: `{latestTransferredMismatches.ToString(CultureInfo.InvariantCulture)}`");
        writer.WriteLine($"Latest destination-owner final snapshot mismatches after later writes: `{latestFinalSnapshotMismatches.ToString(CultureInfo.InvariantCulture)}`");
        writer.WriteLine($"Overwritten earlier-chunk final snapshot mismatches: `{overwrittenFinalSnapshotMismatches.ToString(CultureInfo.InvariantCulture)}`");
        writer.WriteLine();
        writer.WriteLine("The MAME VDP dump is a local supplemental reference. It is useful when investigating SVP layout changes, but the release gate is based on mdSharp's captured DMA chunks, final SVP DMA VRAM, and documented name-table invariants. Final snapshot mismatches can include later VRAM writes after the expected DMA chunk.");
        writer.WriteLine();
        writer.WriteLine("| Source | Destination | Words | Sampled | Latest owner | Transferred mismatches | Final snapshot mismatches | Source changed during DMA | First transfer mismatch | First final mismatch |");
        writer.WriteLine("| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (VirtuaRacingDmaReferenceComparison comparison in dmaReferenceComparisons)
        {
            writer.WriteLine(
                $"| `${comparison.SourceAddress:X6}` | `${comparison.DestinationAddress:X4}` | {comparison.LengthWords} | {comparison.SampledWords} | "
                + $"{(comparison.IsLatestDestinationOwner ? "yes" : "no")} | "
                + $"{comparison.TransferredMismatches} | {comparison.FinalVramMismatches} | {comparison.SourceChangedDuringDma} | "
                + $"{(comparison.FirstTransferredMismatchIndex < 0 ? "" : comparison.FirstTransferredMismatchIndex.ToString(CultureInfo.InvariantCulture))} | "
                + $"{(comparison.FirstFinalMismatchIndex < 0 ? "" : comparison.FirstFinalMismatchIndex.ToString(CultureInfo.InvariantCulture))} |");
        }
    }

    writer.WriteLine();
    writer.WriteLine("## Captured Layout Invariants");
    writer.WriteLine();
    writer.WriteLine($"- SVP-sourced DMA chunks captured: `{chunks.Count.ToString(CultureInfo.InvariantCulture)}`");
    writer.WriteLine($"- Latest SVP DMA VRAM words compared against final VRAM: `{vramCompared.ToString(CultureInfo.InvariantCulture)}`");
    writer.WriteLine($"- Final VRAM mismatches after later writes: `{vramMismatches.ToString(CultureInfo.InvariantCulture)}`");
    if (nameTableChecks.Count == 0)
    {
        writer.WriteLine("- Name-table reference rows: `not checked`");
    }
    else
    {
        VirtuaRacingNameTableCheck checkedNameTable = nameTableChecks[0];
        writer.WriteLine($"- Best name-table base: `${checkedNameTable.BaseAddress:X4}`");
        writer.WriteLine($"- Best name-table width: `{checkedNameTable.TableWidth.ToString(CultureInfo.InvariantCulture)}`");
        writer.WriteLine($"- Name-table cells checked: `{checkedNameTable.ComparedCells.ToString(CultureInfo.InvariantCulture)}`");
        writer.WriteLine($"- Exact name-table mismatches: `{checkedNameTable.ExactMismatchedCells.ToString(CultureInfo.InvariantCulture)}`");
        writer.WriteLine($"- Best uniform nonzero tile delta: `${checkedNameTable.BestDelta:X3}`");
        writer.WriteLine($"- Delta-adjusted name-table mismatches: `{checkedNameTable.BestDeltaMismatchedCells.ToString(CultureInfo.InvariantCulture)}`");
        writer.WriteLine($"- Zero-row cells checked: `{checkedNameTable.ZeroRowCells.ToString(CultureInfo.InvariantCulture)}`");
        writer.WriteLine($"- Zero-row mismatches: `{checkedNameTable.ZeroRowMismatches.ToString(CultureInfo.InvariantCulture)}`");
    }

    if (nameTableChecks.Count > 0)
    {
        writer.WriteLine();
        writer.WriteLine("## Name Table Candidate Search");
        writer.WriteLine();
        writer.WriteLine("| Base | Width | Exact mismatches | Best delta | Delta-adjusted mismatches | Zero-row mismatches |");
        writer.WriteLine("| ---: | ---: | ---: | ---: | ---: | ---: |");
        foreach (VirtuaRacingNameTableCheck check in nameTableChecks.Take(8))
        {
            writer.WriteLine($"| `${check.BaseAddress:X4}` | {check.TableWidth} | {check.ExactMismatchedCells} | `${check.BestDelta:X3}` | {check.BestDeltaMismatchedCells} | {check.ZeroRowMismatches} |");
        }
    }

    writer.WriteLine();
    writer.WriteLine("## Interpretation");
    writer.WriteLine();
    writer.WriteLine("notaz's Virtua Racing notes describe a non-linear SVP-generated tile layout, double-buffered DMA ranges, explicit zero rows, and a historical dithering-related SVP emulation fix. If the expected DMA chunks and name-table arrangement match, vertical column patterns are more likely to be intentional Virtua Racing dithering/layout than a generic VDP column bug. If they do not match, this report narrows the fault to SVP DMA generation, VRAM writes after DMA, or name-table construction.");
    writer.WriteLine();
    writer.WriteLine("Sources: https://notaz.gp2x.de/docs/svpdoc.txt, https://notaz.gp2x.de/releases/PicoDrive/svp_bsd.zip, https://github.com/ekeeke/Genesis-Plus-GX/tree/master/core/cart_hw/svp, https://github.com/mamedev/mame/blob/master/src/devices/cpu/ssp1601/ssp1601.cpp");
    writer.WriteLine();
    writer.WriteLine("## Emulator State");
    writer.WriteLine();
    writer.WriteLine("```text");
    writer.WriteLine(FormatState(machine));
    writer.WriteLine(FormatVdpState(machine.Vdp));
    writer.WriteLine(FormatSvpState(machine.Bus.Svp!));
    writer.WriteLine("```");
}

VirtuaRacingDmaMatch MatchExpectedVirtuaRacingDma(VirtuaRacingExpectedDma expected, IReadOnlyList<CapturedDmaChunk> chunks)
{
    CapturedDmaChunk? exact = chunks.LastOrDefault(chunk =>
        chunk.SourceAddress == expected.SourceAddress
        && chunk.DestinationAddress == expected.DestinationAddress
        && chunk.LengthWords == expected.LengthWords);
    if (exact is not null)
    {
        return new VirtuaRacingDmaMatch(expected, exact, true);
    }

    CapturedDmaChunk? close = chunks.LastOrDefault(chunk =>
        chunk.SourceAddress == expected.SourceAddress
        && chunk.DestinationAddress == expected.DestinationAddress);
    return new VirtuaRacingDmaMatch(expected, close, false);
}

VirtuaRacingExpectedDma[] GetVirtuaRacingExpectedDmas()
{
    return
    [
        new(0x30_0002, 0x0020, 4961),
        new(0x30_26C2, 0x26E0, 2369),
        new(0x30_3942, 0x72E0, 4961),
        new(0x30_6002, 0x3980, 4961),
        new(0x30_86C2, 0x6040, 2369),
        new(0x30_9942, 0x72E0, 4961),
    ];
}

int?[,]? LoadVirtuaRacingNameTable(string? imageFormatPath)
{
    if (string.IsNullOrWhiteSpace(imageFormatPath) || !File.Exists(imageFormatPath))
    {
        return null;
    }

    int?[,] cells = new int?[64, 32];
    foreach (string rawLine in File.ReadLines(imageFormatPath))
    {
        string line = rawLine.Trim();
        if (line.Length < 5 || line[3] != ':' || !int.TryParse(line[..3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int row))
        {
            continue;
        }

        if ((uint)row >= 64)
        {
            continue;
        }

        string[] parts = line[4..].Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int column = 0; column < Math.Min(32, parts.Length); column++)
        {
            if (int.TryParse(parts[column], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value))
            {
                cells[row, column] = value;
            }
        }
    }

    return cells;
}

IReadOnlyList<VirtuaRacingNameTableCheck> CheckVirtuaRacingNameTables(byte[] vram, int?[,] expected)
{
    int[] bases =
    [
        0xC000,
        0xE000,
        0x3000,
        0x0000,
        0x2000,
        0x4000,
        0x6000,
        0x8000,
        0xA000,
    ];

    List<VirtuaRacingNameTableCheck> checks = new();
    foreach (int nameTableBase in bases.Distinct())
    {
        checks.Add(CheckVirtuaRacingNameTable(vram, expected, nameTableBase, tableWidth: 64));
        checks.Add(CheckVirtuaRacingNameTable(vram, expected, nameTableBase, tableWidth: 32));
    }

    return checks
        .OrderBy(check => check.BestDeltaMismatchedCells)
        .ThenBy(check => check.ExactMismatchedCells)
        .ThenBy(check => check.BaseAddress)
        .ThenBy(check => check.TableWidth)
        .ToArray();
}

VirtuaRacingNameTableCheck CheckVirtuaRacingNameTable(byte[] vram, int?[,] expected, int nameTableBase, int tableWidth)
{
    int compared = 0;
    int exactMismatched = 0;
    int zeroRows = 0;
    int zeroRowMismatches = 0;
    Dictionary<int, int> deltaCounts = new();
    int nonzeroCompared = 0;

    for (int row = 0; row < expected.GetLength(0); row++)
    {
        for (int column = 0; column < expected.GetLength(1); column++)
        {
            int? expectedTile = expected[row, column];
            if (!expectedTile.HasValue)
            {
                continue;
            }

            compared++;
            ushort name = ReadWordFromBytes(vram, nameTableBase + (((row * tableWidth) + column) * 2));
            int actualTile = name & 0x07FF;
            if (expectedTile.Value == 0)
            {
                zeroRows++;
                if (actualTile != 0)
                {
                    zeroRowMismatches++;
                }
            }
            else
            {
                nonzeroCompared++;
                int delta = (actualTile - expectedTile.Value) & 0x07FF;
                deltaCounts[delta] = deltaCounts.GetValueOrDefault(delta) + 1;
            }

            if (actualTile != expectedTile.Value)
            {
                exactMismatched++;
            }
        }
    }

    int bestDelta = 0;
    int bestDeltaMatches = 0;
    foreach ((int delta, int count) in deltaCounts)
    {
        if (count > bestDeltaMatches)
        {
            bestDelta = delta;
            bestDeltaMatches = count;
        }
    }

    int bestDeltaMismatched = zeroRowMismatches + Math.Max(0, nonzeroCompared - bestDeltaMatches);
    return new VirtuaRacingNameTableCheck(nameTableBase, tableWidth, compared, exactMismatched, bestDelta, bestDeltaMismatched, zeroRows, zeroRowMismatches);
}

ushort ReadWordFromBytes(byte[] bytes, int address)
{
    int offset = address & 0xFFFF;
    return (ushort)((bytes[offset] << 8) | bytes[(offset + 1) & 0xFFFF]);
}

GenesisButton BloodlinesStartThenPlay(int frame)
{
    GenesisButton buttons = BloodlinesStartFromTitle(frame);
    if (frame >= 13500)
    {
        buttons |= GenesisButton.Right;
    }

    if (frame >= 14200 && frame % 150 < 22)
    {
        buttons |= GenesisButton.C;
    }

    return buttons;
}

GenesisButton BloodlinesStartFromTitle(int frame)
{
    if (frame >= 10080 && frame < 10104)
    {
        return GenesisButton.Start;
    }

    return (frame >= 10300 && frame < 10324)
        || (frame >= 10600 && frame < 10624)
        || (frame >= 11100 && frame < 11124)
        || (frame >= 11800 && frame < 11824)
        || (frame >= 12600 && frame < 12624)
            ? GenesisButton.Start | GenesisButton.A | GenesisButton.C
            : GenesisButton.None;
}

GenesisButton StreetsStartAndSelect(int frame)
{
    if (frame < 300)
    {
        return GenesisButton.None;
    }

    return frame % 180 < 18
        ? GenesisButton.Start | GenesisButton.C
        : GenesisButton.None;
}

Func<int, GenesisButton> ResolveInputScript(string script)
{
    return script.ToLowerInvariant() switch
    {
        "none" or "idle" => NoInput,
        "start" => StartPulse,
        "repeat-start" or "menu" => RepeatedStartPulse,
        "sonic1-start" => Sonic1TitleStartPulse,
        "sonic3-start" => Sonic3TitleStartPulse,
        "chaotix-title-start" or "chaotix-start" => ChaotixTitleStartPulse,
        "chaotix-play" or "chaotix-start-play" => ChaotixStartAndPlay,
        "streets" or "streets-start" => StreetsStartAndSelect,
        "bloodlines" or "bloodlines-start" => BloodlinesStartThenPlay,
        _ => throw new ArgumentException($"Unknown input script '{script}'."),
    };
}

Func<int, ControllerInput> ResolveControllerInputScript(string script)
{
    return script.ToLowerInvariant() switch
    {
        "p1-repeat-start" or "virtua-racing" or "virtua-racing-start" => frame => new ControllerInput(RepeatedStartPulse(frame), GenesisButton.None),
        "virtua-racing-drive" => frame => new ControllerInput(VirtuaRacingStartAndDrive(frame), GenesisButton.None),
        "p1-start" => frame => new ControllerInput(StartPulse(frame), GenesisButton.None),
        _ => frame =>
        {
            GenesisButton buttons = ResolveInputScript(script)(frame);
            return new ControllerInput(buttons, buttons);
        },
    };
}

void RunSonicBench(string romPath, string outputFolder, int instructionsPerFrame)
{
    Directory.CreateDirectory(outputFolder);
    SonicPreset[] presets =
    [
        new("title", 600, NoInput),
        new("attract", 1800, NoInput),
        new("greenhill", 1600, StartThenIdle),
        new("gameplay", 2200, StartThenRun),
    ];

    string csvPath = Path.Combine(outputFolder, "sonic-bench.csv");
    using StreamWriter writer = new(csvPath, false, Encoding.UTF8);
    writer.WriteLine("preset,frames,pc,exceptions,renderMode,nonBackgroundPixels,cramNonzero,sprites,sha256,bmp");

    foreach (SonicPreset preset in presets)
    {
        MegaDrive machine = CreateMachine(romPath);
        machine.Reset();
        for (int frame = 0; frame < preset.Frames; frame++)
        {
            GenesisButton pressed = preset.Input(frame);
            machine.Bus.Controller1.Pressed = pressed;
            machine.Bus.Controller2.Pressed = pressed;
            machine.RunFrameCycles(instructionsPerFrame);
        }

        machine.Bus.Controller1.Pressed = GenesisButton.None;
        machine.Bus.Controller2.Pressed = GenesisButton.None;
        byte[] rgb = machine.RenderFrameRgb();
        int nonBackground = CountNonBackgroundPixels(machine.Vdp, rgb);
        string hash = Convert.ToHexString(SHA256.HashData(rgb));
        string ppmPath = Path.Combine(outputFolder, $"{preset.Name}.ppm");
        string bmpPath = Path.Combine(outputFolder, $"{preset.Name}.bmp");
        WritePpm(ppmPath, Vdp.ScreenWidth, Vdp.ScreenHeight, rgb);
        WriteBmp(bmpPath, Vdp.ScreenWidth, Vdp.ScreenHeight, rgb);
        Vdp.VdpDiagnostics diagnostics = machine.Vdp.GetDiagnostics();
        int cramNonzero = CountNonzeroCram(machine.Vdp);

        writer.WriteLine($"{preset.Name},{preset.Frames},${machine.MainCpu.PC:X8},\"{EscapeCsv(FormatExceptions(machine.MainCpu))}\",{machine.Vdp.LastRenderMode},{nonBackground},{cramNonzero},{diagnostics.LikelySpriteCount},{hash},\"{EscapeCsv(Path.GetFullPath(bmpPath))}\"");
        Console.WriteLine($"{preset.Name}: mode={machine.Vdp.LastRenderMode} pixels={nonBackground:N0} CRAM={cramNonzero} sprites={diagnostics.LikelySpriteCount} PC=${machine.MainCpu.PC:X8} EX={FormatExceptions(machine.MainCpu)}");
        Console.WriteLine($"  {Path.GetFullPath(bmpPath)}");
    }

    Console.WriteLine($"Wrote Sonic benchmark report to {Path.GetFullPath(csvPath)}");

    static GenesisButton NoInput(int frame) => GenesisButton.None;

    static GenesisButton StartThenIdle(int frame)
    {
        return frame is >= 620 and < 660 ? GenesisButton.Start : GenesisButton.None;
    }

    static GenesisButton StartThenRun(int frame)
    {
        GenesisButton buttons = StartThenIdle(frame);
        if (frame >= 950)
        {
            buttons |= GenesisButton.Right;
        }

        if (frame >= 1100 && frame % 120 < 18)
        {
            buttons |= GenesisButton.C;
        }

        return buttons;
    }
}

void RunBloodlinesBench(string romPath, string outputFolder, int instructionsPerFrame)
{
    Directory.CreateDirectory(outputFolder);
    SonicPreset[] presets =
    [
        new("konami", 900, NoInput),
        new("intro", 3000, NoInput),
        new("attract-gameplay", 5000, NoInput),
        new("title", 10000, NoInput),
        new("fast-start", 4200, StartThroughMenus),
        new("title-press", 10200, StartFromTitle),
        new("title-transition", 10600, StartFromTitle),
        new("title-start", 13000, StartFromTitle),
        new("play-right", 16000, StartThenPlay),
        new("gameplay-run", 22000, StartThenPlay),
    ];

    string csvPath = Path.Combine(outputFolder, "bloodlines-bench.csv");
    using StreamWriter writer = new(csvPath, false, Encoding.UTF8);
    writer.WriteLine("preset,frames,pc,exceptions,renderMode,nonBackgroundPixels,cramNonzero,sprites,sha256,bmp");

    foreach (SonicPreset preset in presets)
    {
        MegaDrive machine = CreateMachine(romPath);
        machine.Reset();
        for (int frame = 0; frame < preset.Frames; frame++)
        {
            GenesisButton pressed = preset.Input(frame);
            machine.Bus.Controller1.Pressed = pressed;
            machine.Bus.Controller2.Pressed = pressed;
            machine.RunFrameCycles(instructionsPerFrame);
        }

        machine.Bus.Controller1.Pressed = GenesisButton.None;
        machine.Bus.Controller2.Pressed = GenesisButton.None;
        byte[] rgb = machine.RenderFrameRgb();
        int nonBackground = CountNonBackgroundPixels(machine.Vdp, rgb);
        string hash = Convert.ToHexString(SHA256.HashData(rgb));
        string ppmPath = Path.Combine(outputFolder, $"{preset.Name}.ppm");
        string bmpPath = Path.Combine(outputFolder, $"{preset.Name}.bmp");
        WritePpm(ppmPath, Vdp.ScreenWidth, Vdp.ScreenHeight, rgb);
        WriteBmp(bmpPath, Vdp.ScreenWidth, Vdp.ScreenHeight, rgb);
        Vdp.VdpDiagnostics diagnostics = machine.Vdp.GetDiagnostics();
        int cramNonzero = CountNonzeroCram(machine.Vdp);

        writer.WriteLine($"{preset.Name},{preset.Frames},${machine.MainCpu.PC:X8},\"{EscapeCsv(FormatExceptions(machine.MainCpu))}\",{machine.Vdp.LastRenderMode},{nonBackground},{cramNonzero},{diagnostics.LikelySpriteCount},{hash},\"{EscapeCsv(Path.GetFullPath(bmpPath))}\"");
        Console.WriteLine($"{preset.Name}: mode={machine.Vdp.LastRenderMode} pixels={nonBackground:N0} CRAM={cramNonzero} sprites={diagnostics.LikelySpriteCount} PC=${machine.MainCpu.PC:X8} EX={FormatExceptions(machine.MainCpu)}");
        Console.WriteLine($"  {Path.GetFullPath(bmpPath)}");
    }

    Console.WriteLine($"Wrote Bloodlines benchmark report to {Path.GetFullPath(csvPath)}");

    static GenesisButton NoInput(int frame) => GenesisButton.None;

    static GenesisButton StartThroughMenus(int frame)
    {
        return IsPulse(frame, 60, 18)
            || IsPulse(frame, 420, 18)
            || IsPulse(frame, 900, 18)
            || IsPulse(frame, 1380, 18)
            || IsPulse(frame, 1860, 18)
            || IsPulse(frame, 2340, 18)
                ? GenesisButton.Start
                : GenesisButton.None;
    }

    static GenesisButton StartThenPlay(int frame)
    {
        GenesisButton buttons = StartFromTitle(frame);
        if (frame >= 13500)
        {
            buttons |= GenesisButton.Right;
        }

        if (frame >= 14200 && frame % 150 < 22)
        {
            buttons |= GenesisButton.C;
        }

        return buttons;
    }

    static GenesisButton StartFromTitle(int frame)
    {
        if (IsPulse(frame, 10080, 24))
        {
            return GenesisButton.Start;
        }

        return IsPulse(frame, 10300, 24)
            || IsPulse(frame, 10600, 24)
            || IsPulse(frame, 11100, 24)
            || IsPulse(frame, 11800, 24)
            || IsPulse(frame, 12600, 24)
                ? GenesisButton.Start | GenesisButton.A | GenesisButton.C
                : GenesisButton.None;
    }

    static bool IsPulse(int frame, int start, int length)
    {
        return frame >= start && frame < start + length;
    }
}

SmokeResult RunSmoke(string path, int instructionBudget)
{
    MegaDrive? machine = null;
    int executed = 0;

    try
    {
        CartridgeImage cartridge = CartridgeImage.FromFile(path);
        string cartridgeDetail = FormatCartridgeDetail(cartridge.Diagnostics, FilenameCartridgeWarnings(path));
        if (cartridge.Diagnostics.HasUnsupportedHardware)
        {
            return new SmokeResult("unsupported", executed, 0, cartridgeDetail, string.Empty);
        }

        machine = new MegaDrive(cartridge, IsPalRegion(cartridge));
        machine.Reset();

        for (; executed < instructionBudget && !machine.MainCpu.Stopped; executed++)
        {
            machine.StepInstruction();
        }

        string status = machine.MainCpu.Stopped ? "stopped" : "budget";
        string detail = AppendDetail(cartridgeDetail, machine.MainCpu.Stopped ? "CPU STOP reached" : "instruction budget reached");
        return new SmokeResult(status, executed, machine.MainCpu.PC, detail, FormatState(machine));
    }
    catch (M68kException ex)
    {
        uint pc = ExtractPcFromMessage(ex.Message);
        if (pc == 0 && machine is not null)
        {
            pc = machine.MainCpu.PC;
        }

        return new SmokeResult("unsupported", executed, pc, ex.Message, machine is null ? string.Empty : FormatState(machine));
    }
    catch (Exception ex)
    {
        return new SmokeResult("error", executed, machine?.MainCpu.PC ?? 0, ex.Message, machine is null ? string.Empty : FormatState(machine));
    }
}

string FormatState(MegaDrive machine)
{
    M68kCpu cpu = machine.MainCpu;
    return $"PC=${cpu.PC:X8} SR=${cpu.SR:X4} cycles={cpu.Cycles:N0} "
        + $"D0=${cpu.D[0]:X8} D1=${cpu.D[1]:X8} D2=${cpu.D[2]:X8} D3=${cpu.D[3]:X8} "
        + $"A0=${cpu.A[0]:X8} A1=${cpu.A[1]:X8} A6=${cpu.A[6]:X8} A7=${cpu.A[7]:X8} "
        + $"Z80PC=${machine.Z80.PC:X4} Z80A=${machine.Z80.A:X2} Z80B=${machine.Z80.B:X2} Z80IX=${machine.Z80.IX:X4} Z80IY=${machine.Z80.IY:X4} Z80Cyc={machine.Z80.Cycles:N0} Z80H={(machine.Z80.Halted ? 1 : 0)} Z80RST={(machine.Z80.ResetAsserted ? 1 : 0)} Z80BUS={(machine.Z80.BusRequested ? 1 : 0)} "
        + $"YMST=${machine.Ym2612.ReadStatus(machine.Bus.CurrentMasterCycle):X2} YM24=${machine.Ym2612.ReadRegister(0, 0x24):X2} YM25=${machine.Ym2612.ReadRegister(0, 0x25):X2} YM26=${machine.Ym2612.ReadRegister(0, 0x26):X2} YM27=${machine.Ym2612.ReadRegister(0, 0x27):X2} YMTA={machine.Ym2612.TimerACounter} YMTB={machine.Ym2612.TimerBCounter} "
        + $"EX={FormatExceptions(cpu)}";
}

string FormatCartridgeDetail(CartridgeDiagnostics diagnostics, IEnumerable<string>? extraWarnings = null)
{
    List<string> parts = [];
    if (diagnostics.HasUnsupportedHardware)
    {
        parts.Add($"unsupported hardware: {string.Join(", ", diagnostics.UnsupportedHardware)}");
    }

    if (diagnostics.Requires32X)
    {
        parts.Add("requires: Sega 32X");
    }

    if (diagnostics.HasSaveHardware)
    {
        parts.Add($"save: {FormatSaveHardware(diagnostics)}");
    }

    if (diagnostics.UsesBankSwitchRegisters)
    {
        parts.Add("mapper: bank-switch registers");
    }

    parts.AddRange(diagnostics.Warnings);
    if (extraWarnings is not null)
    {
        parts.AddRange(extraWarnings);
    }

    return string.Join("; ", parts.Distinct(StringComparer.Ordinal));
}

string[] FilenameCartridgeWarnings(string path)
{
    string name = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
    List<string> warnings = [];
    if (name.Contains("J-CART", StringComparison.Ordinal) || name.Contains("J CART", StringComparison.Ordinal))
    {
        warnings.Add("J-Cart extra controller ports are mapped as players 3 and 4.");
    }

    if (name.Contains("LETHAL ENFORCERS", StringComparison.Ordinal) ||
        name.Contains("BODY COUNT", StringComparison.Ordinal) ||
        name.Contains("MENACER", StringComparison.Ordinal) ||
        name.Contains("T2 - THE ARCADE GAME", StringComparison.Ordinal) ||
        name.Contains("TERMINATOR 2 - THE ARCADE GAME", StringComparison.Ordinal) ||
        name.Contains("TERMINATOR 2 THE ARCADE GAME", StringComparison.Ordinal))
    {
        warnings.Add("Light gun support is available through Menacer/Justifier on port 2; HV hit timing is approximate.");
    }

    return warnings.ToArray();
}

string FormatSaveHardware(CartridgeDiagnostics diagnostics)
{
    if (diagnostics.SaveHardware == "SRAM" && diagnostics.SaveRamStart is uint start && diagnostics.SaveRamEnd is uint end)
    {
        return $"SRAM ${start:X6}-${end:X6} lanes={diagnostics.SaveRamLanes}";
    }

    if (diagnostics.SaveHardware == "serial EEPROM" && diagnostics.EepromSize is int size)
    {
        return $"serial EEPROM {size:N0} bytes";
    }

    return diagnostics.SaveHardware;
}

string FormatSaveRange(CartridgeDiagnostics diagnostics)
{
    return diagnostics.SaveRamStart is uint start && diagnostics.SaveRamEnd is uint end
        ? $"${start:X6}-${end:X6}"
        : string.Empty;
}

string FormatZ80Window(MegaDrive machine)
{
    ushort pc = machine.Z80.PC;
    int start = Math.Max(0, pc - 8);
    StringBuilder builder = new();
    builder.Append($"Z80 bytes @{start:X4}:");
    for (int i = 0; i < 16; i++)
    {
        ushort address = (ushort)(start + i);
        builder.Append($" {machine.Bus.ReadZ80Byte(address):X2}");
    }

    return builder.ToString();
}

string FormatExceptions(M68kCpu cpu)
{
    if (cpu.ExceptionCounts.Count == 0)
    {
        return "none";
    }

    return string.Join(";", cpu.ExceptionCounts.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"));
}

string FormatVdpState(Vdp vdp)
{
    int nonzeroVram = 0;
    int firstVram = -1;
    int lastVram = -1;
    int index = 0;
    foreach (byte value in vdp.Vram)
    {
        if (value != 0)
        {
            nonzeroVram++;
            firstVram = firstVram < 0 ? index : firstVram;
            lastVram = index;
        }

        index++;
    }

    int nonzeroCram = 0;
    foreach (ushort value in vdp.Cram)
    {
        if (value != 0)
        {
            nonzeroCram++;
        }
    }

    ReadOnlySpan<byte> r = vdp.Registers;
    Vdp.VdpDiagnostics d = vdp.GetDiagnostics();
    string cramSample = string.Join(" ", vdp.Cram.ToArray().Take(16).Select(value => $"${value:X3}"));
    string fallback = vdp.LastRenderFallbackNameTableBase.HasValue ? $" fallbackName=${vdp.LastRenderFallbackNameTableBase.Value:X4}" : string.Empty;
    string tileFallback = vdp.LastRenderFallbackTileStart.HasValue ? $" fallbackTile=${vdp.LastRenderFallbackTileStart.Value:X3}" : string.Empty;
    return $"VDP nonzero: VRAM={nonzeroVram} range=${Math.Max(firstVram, 0):X4}-${Math.Max(lastVram, 0):X4} CRAM={nonzeroCram} "
        + $"R1=${r[1]:X2} R2=${r[2]:X2} R4=${r[4]:X2} R5=${r[5]:X2} R7=${r[7]:X2} R12=${r[12]:X2} R15=${r[15]:X2} R16=${r[16]:X2} "
        + $"R11=${r[11]:X2} R13=${r[13]:X2} R17=${r[17]:X2} R18=${r[18]:X2} "
        + $"mode={vdp.LastRenderMode} "
        + $"A=${d.PlaneANameTableBase:X4}/{d.PlaneAScore} B=${d.PlaneBNameTableBase:X4}/{d.PlaneBScore} "
        + $"SAT=${d.SpriteAttributeTableBase:X4} sprites={d.LikelySpriteCount} plane={d.PlaneWidthTiles}x{d.PlaneHeightTiles} "
        + $"regWrites(R2/R4/R5)={d.PlaneARegisterWriteCount}/{d.PlaneBRegisterWriteCount}/{d.SpriteAttributeRegisterWriteCount} "
        + $"lastCmd=${d.LastCommandCode:X2}@${d.LastCommandAddress:X4} dmaEvents={d.DmaEventCount} directColorSamples={d.DirectColorSampleCount} fifo={d.FifoWords} "
        + $"spriteOF={(d.SpriteOverflow ? 1 : 0)} spriteCOL={(d.SpriteCollision ? 1 : 0)} "
        + $"best=${d.BestFallbackNameTableBase:X4}/{d.BestFallbackNameTableScore} tiles={d.NonzeroTileCount} firstTile=${d.FirstNonzeroTile:X3} "
        + $"CRAM0-15={cramSample}{fallback}{tileFallback}";
}

uint ExtractPcFromMessage(string message)
{
    int marker = message.LastIndexOf(" at $", StringComparison.OrdinalIgnoreCase);
    if (marker < 0)
    {
        return 0;
    }

    string hex = message[(marker + 5)..].Trim();
    return uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out uint pc) ? pc : 0;
}

string EscapeCsv(string value)
{
    return value.Replace("\"", "\"\"");
}

int[] ParseSvpTracePcList(string value)
{
    if (string.IsNullOrWhiteSpace(value) || value.Trim().Equals("*", StringComparison.Ordinal))
    {
        return [];
    }

    return value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(item => (int)ParseNumber(item))
        .Distinct()
        .Order()
        .ToArray();
}

uint? ParseOptionalHexPrefix(string value)
{
    if (string.IsNullOrWhiteSpace(value) || value.Equals("*", StringComparison.Ordinal))
    {
        return null;
    }

    return ParseNumber(value) & 0x00FF_0000;
}

bool TryMapSvpDramWord(uint address, out int wordAddress)
{
    address &= 0x00FF_FFFE;
    if (address is >= 0x30_0000 and <= 0x31_FFFE)
    {
        wordAddress = (int)((address & 0x1_FFFE) >> 1);
        return true;
    }

    if (address is >= 0x39_0000 and <= 0x39_FFFE)
    {
        uint offset = (address - 0x39_0000) >> 1;
        wordAddress = (int)((offset & 0x7001) | ((offset & 0x003E) << 6) | ((offset & 0x0FC0) >> 5));
        return true;
    }

    if (address is >= 0x3A_0000 and <= 0x3A_FFFE)
    {
        uint offset = (address - 0x3A_0000) >> 1;
        wordAddress = (int)((offset & 0x7801) | ((offset & 0x001E) << 6) | ((offset & 0x07E0) >> 4));
        return true;
    }

    wordAddress = 0;
    return false;
}

uint ParseNumber(string value)
{
    value = value.Trim();
    if (value.StartsWith("$", StringComparison.Ordinal))
    {
        return uint.Parse(value[1..], System.Globalization.NumberStyles.HexNumber);
    }

    if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
    {
        return uint.Parse(value[2..], System.Globalization.NumberStyles.HexNumber);
    }

    return uint.Parse(value);
}

int FindFirstNonzeroTile(Vdp vdp)
{
    ReadOnlySpan<byte> vram = vdp.Vram;
    for (int tile = 0; tile < 2048; tile++)
    {
        int offset = tile * 32;
        for (int i = 0; i < 32; i++)
        {
            if (vram[(offset + i) & 0xFFFF] != 0)
            {
                return tile;
            }
        }
    }

    return 0;
}

int CountNonBackgroundPixels(Vdp vdp, byte[] rgb)
{
    ushort background = vdp.Cram[vdp.Registers[7] & 0x3F];
    byte r = ExpandGenesisColor((background >> 1) & 0x7);
    byte g = ExpandGenesisColor((background >> 5) & 0x7);
    byte b = ExpandGenesisColor((background >> 9) & 0x7);
    int count = 0;
    for (int i = 0; i < rgb.Length; i += 3)
    {
        if (rgb[i] != r || rgb[i + 1] != g || rgb[i + 2] != b)
        {
            count++;
        }
    }

    return count;
}

int CountNonzeroCram(Vdp vdp)
{
    int count = 0;
    foreach (ushort color in vdp.Cram)
    {
        if (color != 0)
        {
            count++;
        }
    }

    return count;
}

int CountNonzeroBytes(ReadOnlySpan<byte> data)
{
    int count = 0;
    foreach (byte value in data)
    {
        if (value != 0)
        {
            count++;
        }
    }

    return count;
}

string FormatThirtyTwoXWords(ThirtyTwoXDevice device, bool system, int start, int length)
{
    StringBuilder builder = new();
    for (int offset = start; offset < start + length; offset += 2)
    {
        ushort value = system
            ? device.ReadSystemRegisterWord((ushort)offset)
            : device.ReadVdpRegisterWord((ushort)offset);
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append('$');
        builder.Append(offset.ToString("X2", CultureInfo.InvariantCulture));
        builder.Append('=');
        builder.Append(value.ToString("X4", CultureInfo.InvariantCulture));
    }

    return builder.ToString();
}

string FormatThirtyTwoXCodeWindow(ThirtyTwoXDevice device, CartridgeImage cartridge, uint pc)
{
    uint start = pc >= 8 ? pc - 8 : pc;
    StringBuilder builder = new();
    for (int i = 0; i < 8; i++)
    {
        uint address = start + (uint)(i * 2);
        ushort word = ReadThirtyTwoXDebugWord(device, cartridge, address);
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        if (address == pc)
        {
            builder.Append('>');
        }

        builder.Append('$');
        builder.Append(address.ToString("X8", CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(word.ToString("X4", CultureInfo.InvariantCulture));
    }

    return builder.ToString();
}

string DisassembleSh2(ushort opcode, uint pc)
{
    int n = (opcode >> 8) & 0x0F;
    int m = (opcode >> 4) & 0x0F;
    int d8 = opcode & 0xFF;
    int d4 = opcode & 0x0F;
    string R(int index) => $"R{index}";
    string Disp(uint address) => $"${address:X8}";
    string Imm(int value) => $"#{value}";
    uint PcDisp8(int scale) => pc + 4 + (uint)(SignExtend8ForDisassembly(d8) * scale);
    uint PcDisp12() => pc + 4 + (uint)(SignExtend12ForDisassembly(opcode & 0x0FFF) * 2);

    return opcode switch
    {
        0x0008 => "CLRT",
        0x0009 => "NOP",
        0x000B => "RTS",
        0x0018 => "SETT",
        0x0019 => "DIV0U",
        0x001B => "SLEEP",
        0x0028 => "CLRMAC",
        0x002B => "RTE",
        _ when (opcode & 0xF0FF) == 0x400B => $"JSR @{R(n)}",
        _ when (opcode & 0xF0FF) == 0x402B => $"JMP @{R(n)}",
        _ when (opcode & 0xF0FF) == 0x0003 => $"BSRF {R(n)}",
        _ when (opcode & 0xF0FF) == 0x0023 => $"BRAF {R(n)}",
        _ when (opcode & 0xF0FF) == 0x0002 => $"STC SR,{R(n)}",
        _ when (opcode & 0xF0FF) == 0x0012 => $"STC GBR,{R(n)}",
        _ when (opcode & 0xF0FF) == 0x0022 => $"STC VBR,{R(n)}",
        _ when (opcode & 0xF0FF) == 0x000A => $"STS MACH,{R(n)}",
        _ when (opcode & 0xF0FF) == 0x001A => $"STS MACL,{R(n)}",
        _ when (opcode & 0xF0FF) == 0x002A => $"STS PR,{R(n)}",
        _ when (opcode & 0xF0FF) == 0x0029 => $"MOVT {R(n)}",
        _ when (opcode & 0xF0FF) == 0x400E => $"LDC {R(n)},SR",
        _ when (opcode & 0xF0FF) == 0x401E => $"LDC {R(n)},GBR",
        _ when (opcode & 0xF0FF) == 0x402E => $"LDC {R(n)},VBR",
        _ when (opcode & 0xF0FF) == 0x400A => $"LDS {R(n)},MACH",
        _ when (opcode & 0xF0FF) == 0x401A => $"LDS {R(n)},MACL",
        _ when (opcode & 0xF0FF) == 0x402A => $"LDS {R(n)},PR",
        _ when (opcode & 0xF0FF) == 0x4002 => $"STS.L MACH,@-{R(n)}",
        _ when (opcode & 0xF0FF) == 0x4012 => $"STS.L MACL,@-{R(n)}",
        _ when (opcode & 0xF0FF) == 0x4022 => $"STS.L PR,@-{R(n)}",
        _ when (opcode & 0xF0FF) == 0x4003 => $"STC.L SR,@-{R(n)}",
        _ when (opcode & 0xF0FF) == 0x4013 => $"STC.L GBR,@-{R(n)}",
        _ when (opcode & 0xF0FF) == 0x4023 => $"STC.L VBR,@-{R(n)}",
        _ when (opcode & 0xF0FF) == 0x4006 => $"LDS.L @{R(n)}+,MACH",
        _ when (opcode & 0xF0FF) == 0x4016 => $"LDS.L @{R(n)}+,MACL",
        _ when (opcode & 0xF0FF) == 0x4026 => $"LDS.L @{R(n)}+,PR",
        _ when (opcode & 0xF0FF) == 0x4007 => $"LDC.L @{R(n)}+,SR",
        _ when (opcode & 0xF0FF) == 0x4017 => $"LDC.L @{R(n)}+,GBR",
        _ when (opcode & 0xF0FF) == 0x4027 => $"LDC.L @{R(n)}+,VBR",
        _ when (opcode & 0xF0FF) == 0x4015 => $"CMP/PL {R(n)}",
        _ when (opcode & 0xF0FF) == 0x4011 => $"CMP/PZ {R(n)}",
        _ when (opcode & 0xF0FF) == 0x4010 => $"DT {R(n)}",
        _ when (opcode & 0xF0FF) == 0x4000 => $"SHLL {R(n)}",
        _ when (opcode & 0xF0FF) == 0x4001 => $"SHLR {R(n)}",
        _ when (opcode & 0xF0FF) == 0x4020 => $"SHAL {R(n)}",
        _ when (opcode & 0xF0FF) == 0x4021 => $"SHAR {R(n)}",
        _ when (opcode & 0xF0FF) == 0x4004 => $"ROTL {R(n)}",
        _ when (opcode & 0xF0FF) == 0x4024 => $"ROTCL {R(n)}",
        _ when (opcode & 0xF0FF) == 0x4005 => $"ROTR {R(n)}",
        _ when (opcode & 0xF0FF) == 0x4025 => $"ROTCR {R(n)}",
        _ when (opcode & 0xF0FF) == 0x4008 => $"SHLL2 {R(n)}",
        _ when (opcode & 0xF0FF) == 0x4009 => $"SHLR2 {R(n)}",
        _ when (opcode & 0xF0FF) == 0x4018 => $"SHLL8 {R(n)}",
        _ when (opcode & 0xF0FF) == 0x4019 => $"SHLR8 {R(n)}",
        _ when (opcode & 0xF0FF) == 0x4028 => $"SHLL16 {R(n)}",
        _ when (opcode & 0xF0FF) == 0x4029 => $"SHLR16 {R(n)}",
        _ when (opcode & 0xF0FF) == 0x401B => $"TAS.B @{R(n)}",
        _ when (opcode & 0xF00F) == 0x400C => $"SHAD {R(m)},{R(n)}",
        _ when (opcode & 0xF00F) == 0x400D => $"SHLD {R(m)},{R(n)}",
        _ when (opcode & 0xF00F) == 0x400F => $"MAC.W @{R(m)}+,@{R(n)}+",
        _ when (opcode & 0xF00F) == 0x6008 => $"SWAP.B {R(m)},{R(n)}",
        _ when (opcode & 0xF00F) == 0x6009 => $"SWAP.W {R(m)},{R(n)}",
        _ when (opcode & 0xF00F) == 0x600A => $"NEGC {R(m)},{R(n)}",
        _ when (opcode & 0xF00F) == 0x600B => $"NEG {R(m)},{R(n)}",
        _ when (opcode & 0xF00F) == 0x600C => $"EXTU.B {R(m)},{R(n)}",
        _ when (opcode & 0xF00F) == 0x600D => $"EXTU.W {R(m)},{R(n)}",
        _ when (opcode & 0xF00F) == 0x600E => $"EXTS.B {R(m)},{R(n)}",
        _ when (opcode & 0xF00F) == 0x600F => $"EXTS.W {R(m)},{R(n)}",
        _ when (opcode & 0xF00F) == 0x0004 => $"MOV.B {R(m)},@(R0,{R(n)})",
        _ when (opcode & 0xF00F) == 0x0005 => $"MOV.W {R(m)},@(R0,{R(n)})",
        _ when (opcode & 0xF00F) == 0x0006 => $"MOV.L {R(m)},@(R0,{R(n)})",
        _ when (opcode & 0xF00F) == 0x0007 => $"MUL.L {R(m)},{R(n)}",
        _ when (opcode & 0xF00F) == 0x000F => $"MAC.L @{R(m)}+,@{R(n)}+",
        _ when (opcode & 0xF00F) == 0x000C => $"MOV.B @(R0,{R(m)}),{R(n)}",
        _ when (opcode & 0xF00F) == 0x000D => $"MOV.W @(R0,{R(m)}),{R(n)}",
        _ when (opcode & 0xF00F) == 0x000E => $"MOV.L @(R0,{R(m)}),{R(n)}",
        _ when (opcode >> 12) == 0x1 => $"MOV.L {R(m)},@({d4 * 4},{R(n)})",
        _ when (opcode >> 12) == 0x5 => $"MOV.L @({d4 * 4},{R(m)}),{R(n)}",
        _ when (opcode >> 12) == 0x7 => $"ADD {Imm(SignExtend8ForDisassembly(d8))},{R(n)}",
        _ when (opcode >> 12) == 0x9 => $"MOV.W @({Disp((pc + 4 + (uint)(d8 * 2)) & 0xFFFF_FFFE)},PC),{R(n)}",
        _ when (opcode >> 12) == 0xA => $"BRA {Disp(PcDisp12())}",
        _ when (opcode >> 12) == 0xB => $"BSR {Disp(PcDisp12())}",
        _ when (opcode >> 12) == 0xD => $"MOV.L @({Disp(((pc + 4) & 0xFFFF_FFFCu) + (uint)(d8 * 4))},PC),{R(n)}",
        _ when (opcode >> 12) == 0xE => $"MOV {Imm(SignExtend8ForDisassembly(d8))},{R(n)}",
        _ => DisassembleSh2ByGroup(opcode, pc, n, m, d4, d8, R, Disp, PcDisp8),
    };
}

string DisassembleSh2ByGroup(ushort opcode, uint pc, int n, int m, int d4, int d8, Func<int, string> R, Func<uint, string> Disp, Func<int, uint> PcDisp8)
{
    return (opcode >> 12) switch
    {
        0x2 => (opcode & 0x0F) switch
        {
            0x0 => $"MOV.B {R(m)},@{R(n)}",
            0x1 => $"MOV.W {R(m)},@{R(n)}",
            0x2 => $"MOV.L {R(m)},@{R(n)}",
            0x4 => $"MOV.B {R(m)},@-{R(n)}",
            0x5 => $"MOV.W {R(m)},@-{R(n)}",
            0x6 => $"MOV.L {R(m)},@-{R(n)}",
            0x7 => $"DIV0S {R(m)},{R(n)}",
            0x8 => $"TST {R(m)},{R(n)}",
            0x9 => $"AND {R(m)},{R(n)}",
            0xA => $"XOR {R(m)},{R(n)}",
            0xB => $"OR {R(m)},{R(n)}",
            0xC => $"CMP/STR {R(m)},{R(n)}",
            0xD => $"XTRCT {R(m)},{R(n)}",
            0xE => $"MULU.W {R(m)},{R(n)}",
            0xF => $"MULS.W {R(m)},{R(n)}",
            _ => "?"
        },
        0x3 => (opcode & 0x0F) switch
        {
            0x0 => $"CMP/EQ {R(m)},{R(n)}",
            0x2 => $"CMP/HS {R(m)},{R(n)}",
            0x3 => $"CMP/GE {R(m)},{R(n)}",
            0x4 => $"DIV1 {R(m)},{R(n)}",
            0x5 => $"DMULU.L {R(m)},{R(n)}",
            0x6 => $"CMP/HI {R(m)},{R(n)}",
            0x7 => $"CMP/GT {R(m)},{R(n)}",
            0x8 => $"SUB {R(m)},{R(n)}",
            0xA => $"SUBC {R(m)},{R(n)}",
            0xB => $"SUBV {R(m)},{R(n)}",
            0xC => $"ADD {R(m)},{R(n)}",
            0xD => $"DMULS.L {R(m)},{R(n)}",
            0xE => $"ADDC {R(m)},{R(n)}",
            0xF => $"ADDV {R(m)},{R(n)}",
            _ => "?"
        },
        0x6 => (opcode & 0x0F) switch
        {
            0x0 => $"MOV.B @{R(m)},{R(n)}",
            0x1 => $"MOV.W @{R(m)},{R(n)}",
            0x2 => $"MOV.L @{R(m)},{R(n)}",
            0x3 => $"MOV {R(m)},{R(n)}",
            0x4 => $"MOV.B @{R(m)}+,{R(n)}",
            0x5 => $"MOV.W @{R(m)}+,{R(n)}",
            0x6 => $"MOV.L @{R(m)}+,{R(n)}",
            0x7 => $"NOT {R(m)},{R(n)}",
            0xA => $"NEGC {R(m)},{R(n)}",
            0xC => $"MOV.B @(R0,{R(m)}),{R(n)}",
            0xD => $"MOV.W @(R0,{R(m)}),{R(n)}",
            0xE => $"MOV.L @(R0,{R(m)}),{R(n)}",
            _ => "?"
        },
        0x8 => ((opcode >> 8) & 0x0F) switch
        {
            0x0 => $"MOV.B R0,@({d4},{R(m)})",
            0x1 => $"MOV.W R0,@({d4 * 2},{R(m)})",
            0x4 => $"MOV.B @({d4},{R(m)}),R0",
            0x5 => $"MOV.W @({d4 * 2},{R(m)}),R0",
            0x8 => $"CMP/EQ #{SignExtend8ForDisassembly(d8)},R0",
            0x9 => $"BT {Disp(PcDisp8(2))}",
            0xB => $"BF {Disp(PcDisp8(2))}",
            0xD => $"BT/S {Disp(PcDisp8(2))}",
            0xF => $"BF/S {Disp(PcDisp8(2))}",
            _ => "?"
        },
        0xC => ((opcode >> 8) & 0x0F) switch
        {
            0x0 => $"MOV.B R0,@({d8},GBR)",
            0x1 => $"MOV.W R0,@({d8 * 2},GBR)",
            0x2 => $"MOV.L R0,@({d8 * 4},GBR)",
            0x3 => $"TRAPA #{d8}",
            0x4 => $"MOV.B @({d8},GBR),R0",
            0x5 => $"MOV.W @({d8 * 2},GBR),R0",
            0x6 => $"MOV.L @({d8 * 4},GBR),R0",
            0x7 => $"MOVA @({Disp(((pc + 4) & 0xFFFF_FFFCu) + (uint)(d8 * 4))},PC),R0",
            0x8 => $"TST #{d8},R0",
            0x9 => $"AND #{d8},R0",
            0xA => $"XOR #{d8},R0",
            0xB => $"OR #{d8},R0",
            0xC => $"TST.B #{d8},@(R0,GBR)",
            0xD => $"AND.B #{d8},@(R0,GBR)",
            0xE => $"XOR.B #{d8},@(R0,GBR)",
            0xF => $"OR.B #{d8},@(R0,GBR)",
            _ => "?"
        },
        _ => "?"
    };
}

static int SignExtend8ForDisassembly(int value)
{
    value &= 0xFF;
    return (value & 0x80) != 0 ? value | unchecked((int)0xFFFF_FF00) : value;
}

static int SignExtend12ForDisassembly(int value)
{
    value &= 0x0FFF;
    return (value & 0x0800) != 0 ? value | unchecked((int)0xFFFF_F000) : value;
}

string FormatSh2Registers(Sh2Cpu cpu)
{
    StringBuilder builder = new();
    for (int i = 0; i < cpu.R.Length; i++)
    {
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append('R');
        builder.Append(i.ToString(CultureInfo.InvariantCulture));
        builder.Append("=$");
        builder.Append(cpu.R[i].ToString("X8", CultureInfo.InvariantCulture));
    }

    builder.Append(" SR=$");
    builder.Append(cpu.SR.ToString("X8", CultureInfo.InvariantCulture));
    builder.Append(" PR=$");
    builder.Append(cpu.PR.ToString("X8", CultureInfo.InvariantCulture));
    builder.Append(" GBR=$");
    builder.Append(cpu.GBR.ToString("X8", CultureInfo.InvariantCulture));
    builder.Append(" VBR=$");
    builder.Append(cpu.VBR.ToString("X8", CultureInfo.InvariantCulture));
    return builder.ToString();
}

ushort ReadThirtyTwoXDebugWord(ThirtyTwoXDevice device, CartridgeImage cartridge, uint address)
{
    if (address is >= 0xF000_0000 and < 0xF000_0000 + ThirtyTwoXHardwareProfile.FrameBufferBytes - 1)
    {
        int offset = (int)(address - 0xF000_0000);
        ReadOnlySpan<byte> frameBuffer = device.DisplayFrameBuffer;
        return (ushort)((frameBuffer[offset] << 8) | frameBuffer[offset + 1]);
    }

    if (address is >= 0xF100_0000 and < 0xF100_0000 + ThirtyTwoXHardwareProfile.FrameBufferBytes - 1)
    {
        int offset = (int)(address - 0xF100_0000);
        ReadOnlySpan<byte> frameBuffer = device.DrawFrameBuffer;
        return (ushort)((frameBuffer[offset] << 8) | frameBuffer[offset + 1]);
    }

    if (address is >= ThirtyTwoXHardwareProfile.Sh2SdramStart and < ThirtyTwoXHardwareProfile.Sh2SdramStart + ThirtyTwoXHardwareProfile.SdramBytes - 1)
    {
        int offset = (int)(address - ThirtyTwoXHardwareProfile.Sh2SdramStart);
        ReadOnlySpan<byte> sdram = device.Sdram;
        return (ushort)((sdram[offset] << 8) | sdram[offset + 1]);
    }

    if (address is >= ThirtyTwoXHardwareProfile.Sh2SdramCacheThroughStart and < ThirtyTwoXHardwareProfile.Sh2SdramCacheThroughStart + ThirtyTwoXHardwareProfile.SdramBytes - 1)
    {
        int offset = (int)(address - ThirtyTwoXHardwareProfile.Sh2SdramCacheThroughStart);
        ReadOnlySpan<byte> sdram = device.Sdram;
        return (ushort)((sdram[offset] << 8) | sdram[offset + 1]);
    }

    if (address is >= ThirtyTwoXHardwareProfile.Sh2VdpRegisterStart and < ThirtyTwoXHardwareProfile.Sh2VdpRegisterStart + 0x80 - 1)
    {
        return device.ReadVdpRegisterWord((ushort)(address - ThirtyTwoXHardwareProfile.Sh2VdpRegisterStart));
    }

    if (address is >= ThirtyTwoXHardwareProfile.Sh2VdpRegisterCachedStart and < ThirtyTwoXHardwareProfile.Sh2VdpRegisterCachedStart + 0x80 - 1)
    {
        return device.ReadVdpRegisterWord((ushort)(address - ThirtyTwoXHardwareProfile.Sh2VdpRegisterCachedStart));
    }

    if (address is >= ThirtyTwoXHardwareProfile.Sh2ColorPaletteStart and < ThirtyTwoXHardwareProfile.Sh2ColorPaletteStart + (ThirtyTwoXHardwareProfile.PaletteEntries * 2) - 1)
    {
        return device.ReadPaletteWord((ushort)(address - ThirtyTwoXHardwareProfile.Sh2ColorPaletteStart));
    }

    if (address is >= ThirtyTwoXHardwareProfile.Sh2ColorPaletteCachedStart and < ThirtyTwoXHardwareProfile.Sh2ColorPaletteCachedStart + (ThirtyTwoXHardwareProfile.PaletteEntries * 2) - 1)
    {
        return device.ReadPaletteWord((ushort)(address - ThirtyTwoXHardwareProfile.Sh2ColorPaletteCachedStart));
    }

    if (address is >= ThirtyTwoXHardwareProfile.M68kColorPaletteStart and < ThirtyTwoXHardwareProfile.M68kColorPaletteStart + (ThirtyTwoXHardwareProfile.PaletteEntries * 2) - 1)
    {
        return device.ReadPaletteWord((ushort)(address - ThirtyTwoXHardwareProfile.M68kColorPaletteStart));
    }

    if (address is >= ThirtyTwoXHardwareProfile.Sh2FrameBufferStart and < ThirtyTwoXHardwareProfile.Sh2FrameBufferStart + ThirtyTwoXHardwareProfile.FrameBufferBytes - 1)
    {
        return device.ReadFrameBufferWord(address - ThirtyTwoXHardwareProfile.Sh2FrameBufferStart);
    }

    if (address is >= ThirtyTwoXHardwareProfile.Sh2FrameBufferCachedStart and < ThirtyTwoXHardwareProfile.Sh2FrameBufferCachedStart + ThirtyTwoXHardwareProfile.FrameBufferBytes - 1)
    {
        return device.ReadFrameBufferWord(address - ThirtyTwoXHardwareProfile.Sh2FrameBufferCachedStart);
    }

    if (address is >= ThirtyTwoXHardwareProfile.M68kFrameBufferStart and < ThirtyTwoXHardwareProfile.M68kFrameBufferStart + ThirtyTwoXHardwareProfile.FrameBufferBytes - 1)
    {
        return device.ReadFrameBufferWord(address - ThirtyTwoXHardwareProfile.M68kFrameBufferStart);
    }

    if (address is >= ThirtyTwoXHardwareProfile.Sh2CartridgeFixedCachedStart and < ThirtyTwoXHardwareProfile.Sh2CartridgeFixedCachedStart + 0x40_0000)
    {
        uint offset = address - ThirtyTwoXHardwareProfile.Sh2CartridgeFixedCachedStart;
        return cartridge.ReadWord(offset);
    }

    if (address >= ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart)
    {
        uint offset = address - ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart;
        return cartridge.ReadWord(offset);
    }

    return 0xFFFF;
}

byte ExpandGenesisColor(int value)
{
    return (byte)((value << 5) | (value << 2) | (value >> 1));
}

void WritePpm(string path, int width, int height, byte[] rgb)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
    using FileStream stream = File.Create(path);
    byte[] header = Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n");
    stream.Write(header);
    stream.Write(rgb);
}

void WriteBmp(string path, int width, int height, byte[] rgb)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
    int stride = ((width * 3) + 3) & ~3;
    int pixelSize = stride * height;
    int fileSize = 54 + pixelSize;

    using FileStream stream = File.Create(path);
    using BinaryWriter writer = new(stream, Encoding.ASCII, leaveOpen: false);
    writer.Write((byte)'B');
    writer.Write((byte)'M');
    writer.Write(fileSize);
    writer.Write(0);
    writer.Write(54);
    writer.Write(40);
    writer.Write(width);
    writer.Write(height);
    writer.Write((ushort)1);
    writer.Write((ushort)24);
    writer.Write(0);
    writer.Write(pixelSize);
    writer.Write(2835);
    writer.Write(2835);
    writer.Write(0);
    writer.Write(0);

    Span<byte> padding = stackalloc byte[3];
    int paddingLength = stride - (width * 3);
    for (int y = height - 1; y >= 0; y--)
    {
        int row = y * width * 3;
        for (int x = 0; x < width; x++)
        {
            int offset = row + (x * 3);
            writer.Write(rgb[offset + 2]);
            writer.Write(rgb[offset + 1]);
            writer.Write(rgb[offset]);
        }

        writer.Write(padding[..paddingLength]);
    }
}

void WritePrioritySummaryCsv(string path, ReadOnlySpan<bool> priorityPixels)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
    using StreamWriter writer = new(path, false, Encoding.UTF8);
    writer.WriteLine("line,priorityPixels,firstX,lastX");
    for (int y = 0; y < Vdp.ScreenHeight; y++)
    {
        int count = 0;
        int firstX = -1;
        int lastX = -1;
        int lineBase = y * Vdp.ScreenWidth;
        for (int x = 0; x < Vdp.ScreenWidth; x++)
        {
            int index = lineBase + x;
            if ((uint)index >= (uint)priorityPixels.Length || !priorityPixels[index])
            {
                continue;
            }

            count++;
            firstX = firstX < 0 ? x : firstX;
            lastX = x;
        }

        writer.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{y},{count},{firstX},{lastX}"));
    }
}

void WriteVisualDiffBmpFromRgb(byte[] baseline, byte[] current, int width, int height, string outputPath)
{
    if (baseline.Length != current.Length || baseline.Length != width * height * 3)
    {
        return;
    }

    int panelGap = 8;
    int outputWidth = (width * 3) + (panelGap * 2);
    byte[] output = new byte[outputWidth * height * 3];
    FillRgb(output, outputWidth, height, 0x20, 0x20, 0x20);

    BlitRgb(output, outputWidth, baseline, width, height, 0);
    BlitRgb(output, outputWidth, current, width, height, width + panelGap);

    byte[] diff = new byte[baseline.Length];
    for (int i = 0; i < baseline.Length; i += 3)
    {
        int dr = Math.Abs(current[i] - baseline[i]);
        int dg = Math.Abs(current[i + 1] - baseline[i + 1]);
        int db = Math.Abs(current[i + 2] - baseline[i + 2]);
        int delta = Math.Max(dr, Math.Max(dg, db));
        if (delta == 0)
        {
            byte dim = (byte)(((baseline[i] + baseline[i + 1] + baseline[i + 2]) / 3) / 4);
            diff[i] = dim;
            diff[i + 1] = dim;
            diff[i + 2] = dim;
        }
        else
        {
            diff[i] = 255;
            diff[i + 1] = (byte)Math.Min(255, delta * 3);
            diff[i + 2] = 0;
        }
    }

    BlitRgb(output, outputWidth, diff, width, height, (width + panelGap) * 2);
    WriteBmp(outputPath, outputWidth, height, output);
}

bool TryReadBmpRgb(string path, out int width, out int height, out byte[] rgb)
{
    width = 0;
    height = 0;
    rgb = [];
    if (!File.Exists(path))
    {
        return false;
    }

    byte[] bytes = File.ReadAllBytes(path);
    if (bytes.Length < 54 || bytes[0] != (byte)'B' || bytes[1] != (byte)'M')
    {
        return false;
    }

    int dataOffset = BitConverter.ToInt32(bytes, 10);
    int dibSize = BitConverter.ToInt32(bytes, 14);
    if (dibSize < 40)
    {
        return false;
    }

    width = BitConverter.ToInt32(bytes, 18);
    int rawHeight = BitConverter.ToInt32(bytes, 22);
    ushort planes = BitConverter.ToUInt16(bytes, 26);
    ushort bitsPerPixel = BitConverter.ToUInt16(bytes, 28);
    int compression = BitConverter.ToInt32(bytes, 30);
    if (width <= 0 || rawHeight == 0 || planes != 1 || bitsPerPixel != 24 || compression != 0)
    {
        return false;
    }

    bool topDown = rawHeight < 0;
    height = Math.Abs(rawHeight);
    int stride = ((width * 3) + 3) & ~3;
    if (dataOffset < 0 || dataOffset + (stride * height) > bytes.Length)
    {
        return false;
    }

    rgb = new byte[width * height * 3];
    for (int y = 0; y < height; y++)
    {
        int sourceY = topDown ? y : height - 1 - y;
        int sourceRow = dataOffset + (sourceY * stride);
        int targetRow = y * width * 3;
        for (int x = 0; x < width; x++)
        {
            int source = sourceRow + (x * 3);
            int target = targetRow + (x * 3);
            rgb[target] = bytes[source + 2];
            rgb[target + 1] = bytes[source + 1];
            rgb[target + 2] = bytes[source];
        }
    }

    return true;
}

void FillRgb(byte[] rgb, int width, int height, byte r, byte g, byte b)
{
    for (int y = 0; y < height; y++)
    {
        int row = y * width * 3;
        for (int x = 0; x < width; x++)
        {
            int offset = row + (x * 3);
            rgb[offset] = r;
            rgb[offset + 1] = g;
            rgb[offset + 2] = b;
        }
    }
}

void BlitRgb(byte[] target, int targetWidth, byte[] source, int sourceWidth, int sourceHeight, int targetX)
{
    for (int y = 0; y < sourceHeight; y++)
    {
        int sourceRow = y * sourceWidth * 3;
        int targetRow = ((y * targetWidth) + targetX) * 3;
        Array.Copy(source, sourceRow, target, targetRow, sourceWidth * 3);
    }
}

void WriteWav(string path, short[] samples, int sampleRate, ushort channels = 1)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
    using FileStream stream = File.Create(path);
    using BinaryWriter writer = new(stream, Encoding.ASCII, leaveOpen: false);
    int dataSize = samples.Length * sizeof(short);
    ushort blockAlign = (ushort)(channels * sizeof(short));
    writer.Write(Encoding.ASCII.GetBytes("RIFF"));
    writer.Write(36 + dataSize);
    writer.Write(Encoding.ASCII.GetBytes("WAVE"));
    writer.Write(Encoding.ASCII.GetBytes("fmt "));
    writer.Write(16);
    writer.Write((ushort)1);
    writer.Write(channels);
    writer.Write(sampleRate);
    writer.Write(sampleRate * blockAlign);
    writer.Write(blockAlign);
    writer.Write((ushort)16);
    writer.Write(Encoding.ASCII.GetBytes("data"));
    writer.Write(dataSize);
    foreach (short sample in samples)
    {
        writer.Write(sample);
    }
}

file sealed record SmokeResult(string Status, int Instructions, uint PC, string Detail, string State);
file sealed record SonicPreset(string Name, int Frames, Func<int, GenesisButton> Input);
file sealed record SonicAudioCheckpointSpec(string Preset, int Frames);
file sealed record SonicAudioRender(short[] Samples, int CompareStartFrame, int CompareStartSample);
file sealed record SonicAudioCompareResult(string Preset, int Frames, int InstructionsPerFrame, int CompareStartFrame, AudioAlignment Alignment, AudioSignalStats ReferenceStats, AudioSignalStats EmulatedStats, AudioBandStats ReferenceBands, AudioBandStats EmulatedBands, string EmulatedWav, string EnergyCsv, string ReportCsv, string ReportMarkdown, string EnvelopeCsv, string ReferencePath);
file sealed record SonicAudioStemRender(string[] Names, short[][] Stems, int CompareStartFrame, int CompareStartSample, SonicPsgTraceFrame[] PsgTrace, SonicYmTraceFrame[] YmTrace);
file sealed record SonicPsgTraceFrame(int Frame, int Sample, uint Pc, Psg.PsgChannelSnapshot[] Channels, Psg.PsgNoiseSnapshot Noise);
file sealed record SonicYmTraceFrame(int Frame, int Sample, uint Pc, Ym2612.Ym2612ChannelSnapshot[] Channels);
file sealed record SonicPsgTraceSummary(SonicPsgChannelTraceSummary[] Channels);
file sealed record SonicPsgChannelTraceSummary(string Name, int ActiveFrames, double AverageVolume, double MinFrequencyHz, double MaxFrequencyHz, double AverageFrequencyHz);
file sealed record SonicAudioWindowSpec(string Name, double OffsetSeconds, double DurationSeconds);
file sealed record SonicAudioWindowAnalysis(string Name, double OffsetSeconds, double DurationSeconds, AudioSignalStats ReferenceStats, AudioSignalStats EmulatedStats, AudioBandStats ReferenceBands, AudioBandStats EmulatedBands, string ReferenceTopNotes, string EmulatedTopNotes, SonicAudioStemWindow[] Stems, SonicPsgTraceSummary Psg, SonicYmTraceSummary Ym);
file sealed record SonicAudioStemWindow(string Name, AudioSignalStats Stats, AudioBandStats Bands, string TopNotes);
file sealed record SonicYmTraceSummary(SonicYmChannelTraceSummary[] Channels);
file sealed record SonicYmChannelTraceSummary(string Name, int ActiveFrames, double AverageFrequencyHz, double MinFrequencyHz, double MaxFrequencyHz, double AverageAlgorithm, double AverageFeedback, double AveragePhaseModulationSensitivity, double AverageAmplitudeModulationSensitivity, double AverageCarrierTotalLevel, double AverageCarrierEnvelope);
file sealed record AudioSmokeSpec(string Id, string[] RomNameContains, int Frames);
file sealed record AudioRegressionRow(string Id, string Rom, int Frames, string Wav, string EnergyCsv, string Report, string Summary);
file sealed record AudioReferenceManifest(AudioReferenceCase[]? Cases);
file sealed record AudioReferenceCase(string Id, string? Rom, string[]? RomContains, string? Reference, string? Preset, int Frames, int? CompareStartFrame, double? AlignmentWindowSeconds, double? ReferenceStartSeconds, double? EmulatedStartSeconds);
file sealed record AudioAlignment(double ReferenceOffsetSeconds, double EmulatedOffsetSeconds, double WindowSeconds, double EnvelopeCorrelation, double? RequestedReferenceOffsetSeconds = null, double? RequestedEmulatedOffsetSeconds = null);
file sealed record AudioSignalStats(double RmsDb, double PeakDb, double BrightnessDb);
file sealed record AudioBandStats(double BassDb, double BodyDb, double MelodyDb, double SparkleDb);
file readonly record struct ControllerInput(GenesisButton Player1, GenesisButton Player2);
file sealed record VisualCheckpointSpec(string Id, string Name, string[] RomNameContains, int Frames, Func<int, GenesisButton> Input, Func<int, GenesisButton>? Player2Input = null);
file sealed record VisualCheckpointBaseline(string Id, string Sha256, string BmpPath);
file sealed record MovieCheckpointSpec(string Id, string Name, string MoviePath, string RelativeMovie, int TargetFrame);
file sealed record MovieVisualCheckpointBaseline(string Id, string Sha256);

file sealed record VisualCheckpointResult(
    string Id,
    string Name,
    string RelativeRom,
    string Status,
    int Frames,
    int InstructionsPerFrame,
    uint Pc,
    string RenderMode,
    int NonBackgroundPixels,
    int Sprites,
    string Sha256,
    string? ExpectedSha256,
    bool MatchesBaseline,
    string BmpPath,
    string DiffPath,
    string Detail)
{
    public static VisualCheckpointResult Missing(VisualCheckpointSpec spec)
    {
        return new VisualCheckpointResult(spec.Id, spec.Name, string.Empty, "missing", 0, 0, 0, string.Empty, 0, 0, string.Empty, null, false, string.Empty, string.Empty, "ROM not found");
    }

    public string ToCsv()
    {
        return $"\"{Escape(Id)}\",\"{Escape(Name)}\",\"{Escape(RelativeRom)}\",{Status},{Frames},{InstructionsPerFrame},${Pc:X8},{RenderMode},{NonBackgroundPixels},{Sprites},{Sha256},\"{Escape(BmpPath)}\",\"{Escape(Detail)}\"";
    }

    private static string Escape(string value)
    {
        return value.Replace("\"", "\"\"", StringComparison.Ordinal);
    }
}

file sealed record MovieVisualCheckpointResult(
    string Id,
    string Name,
    string RelativeMovie,
    string RelativeRom,
    string Status,
    int TargetFrame,
    int MovieFrames,
    int CompletedFrames,
    int InstructionsPerFrame,
    uint Pc,
    string RenderMode,
    int NonBackgroundPixels,
    int Sprites,
    long AudioPeak,
    string Sha256,
    string? ExpectedSha256,
    bool MatchesBaseline,
    string BmpPath,
    string Detail)
{
    public string ToCsv()
    {
        return $"\"{Escape(Id)}\",\"{Escape(Name)}\",\"{Escape(RelativeMovie)}\",\"{Escape(RelativeRom)}\",{Status},{TargetFrame},{MovieFrames},{InstructionsPerFrame},${Pc:X8},{RenderMode},{NonBackgroundPixels},{Sprites},{AudioPeak},{Sha256},\"{Escape(BmpPath)}\",\"{Escape(Detail)}\"";
    }

    private static string Escape(string value)
    {
        return value.Replace("\"", "\"\"", StringComparison.Ordinal);
    }
}

file sealed record CompatibilityResult(
    string RelativeRom,
    string Status,
    int Frames,
    long ElapsedMs,
    double Fps,
    uint Pc,
    string Exceptions,
    string RenderMode,
    int NonBackgroundPixels,
    int MaxNonBackgroundPixels,
    int CramNonzero,
    int Sprites,
    long AudioPeak,
    string Sha256,
    string BmpPath,
    string Detail)
{
    public static CompatibilityResult FromCsvRow(CompatibilityCsvRow row)
    {
        return new CompatibilityResult(
            row.Rom,
            row.Status,
            row.Frames,
            row.ElapsedMs,
            row.Fps,
            row.Pc,
            row.Exceptions,
            row.RenderMode,
            row.NonBackgroundPixels,
            row.MaxNonBackgroundPixels,
            row.CramNonzero,
            row.Sprites,
            row.AudioPeak,
            row.Sha256,
            row.BmpPath,
            row.Detail);
    }

    public string ToCsv()
    {
        return $"\"{Escape(RelativeRom)}\",{Status},{Frames},{ElapsedMs},{Fps:0.###},${Pc:X8},\"{Escape(Exceptions)}\",{RenderMode},{NonBackgroundPixels},{MaxNonBackgroundPixels},{CramNonzero},{Sprites},{AudioPeak},{Sha256},\"{Escape(BmpPath)}\",\"{Escape(Detail)}\"";
    }

    private static string Escape(string value)
    {
        return value.Replace("\"", "\"\"", StringComparison.Ordinal);
    }
}

file sealed record ThirtyTwoXSweepResult(
    string RelativeRom,
    string Status,
    int Frames,
    long ElapsedMs,
    double Fps,
    uint Pc,
    string Exceptions,
    int M68kFaultEvents,
    int M68kTrapEvents,
    string RenderMode,
    int NonBackgroundPixels,
    int MaxNonBackgroundPixels,
    int CompositeMode,
    bool CompositeFallback,
    int CompositePixels,
    int BitmapMode,
    ushort FrameBufferControl,
    int ModeWrites,
    int FrameBufferControlWrites,
    int VdpWrites,
    int FrameBufferByteWrites,
    int PaletteByteWrites,
    int DreqWrites,
    int DreqDmaWords,
    int DisplayFrameBufferNonzero,
    int DrawFrameBufferNonzero,
    int DisplayFrameBufferPayloadNonzero,
    int DrawFrameBufferPayloadNonzero,
    int PaletteNonzero,
    ushort Comm0,
    ushort Comm2,
    ushort Comm4,
    ushort Comm6,
    uint MasterPc,
    uint SlavePc,
    ushort MasterLastOpcode,
    ushort SlaveLastOpcode,
    int MasterUnhandledOpcodes,
    int SlaveUnhandledOpcodes,
    ushort MasterInterruptMask,
    ushort SlaveInterruptMask,
    int PwmAudioLeft,
    int PwmAudioRight,
    int PwmAudioMono,
    int PwmHardwareLeft,
    int PwmHardwareRight,
    int PwmHardwareMono,
    int PwmCycleCounter,
    int PwmTimerCounter,
    bool MasterPwmPending,
    bool SlavePwmPending,
    bool BootPending,
    bool BootRead,
    bool BootLaunch,
    string Sha256,
    string BmpPath,
    string Detail)
{
    public const string CsvHeader = "rom,status,frames,elapsedMs,fps,pc,exceptions,m68kFaults,m68kTraps,renderMode,nonBackgroundPixels,maxNonBackgroundPixels,compositeMode,compositeFallback,compositePixels,bitmapMode,fbctl,modeWrites,fbctlWrites,vdpWrites,fbBytes,paletteBytes,dreqWrites,dreqDmaWords,displayFbNonzero,drawFbNonzero,displayFbPayloadNonzero,drawFbPayloadNonzero,paletteNonzero,comm0,comm2,comm4,comm6,masterPc,slavePc,masterLast,slaveLast,masterUnhandled,slaveUnhandled,masterMask,slaveMask,pwmAudioL,pwmAudioR,pwmAudioM,pwmHwL,pwmHwR,pwmHwM,pwmCycle,pwmTimer,masterPwmPending,slavePwmPending,bootPending,bootRead,bootLaunch,sha256,bmp,detail";

    public string ToCsv()
    {
        return string.Join(
            ',',
            $"\"{Escape(RelativeRom)}\"",
            Status,
            Frames.ToString(CultureInfo.InvariantCulture),
            ElapsedMs.ToString(CultureInfo.InvariantCulture),
            Fps.ToString("0.###", CultureInfo.InvariantCulture),
            $"${Pc:X8}",
            $"\"{Escape(Exceptions)}\"",
            M68kFaultEvents.ToString(CultureInfo.InvariantCulture),
            M68kTrapEvents.ToString(CultureInfo.InvariantCulture),
            RenderMode,
            NonBackgroundPixels.ToString(CultureInfo.InvariantCulture),
            MaxNonBackgroundPixels.ToString(CultureInfo.InvariantCulture),
            CompositeMode.ToString(CultureInfo.InvariantCulture),
            CompositeFallback ? "true" : "false",
            CompositePixels.ToString(CultureInfo.InvariantCulture),
            BitmapMode.ToString(CultureInfo.InvariantCulture),
            $"${FrameBufferControl:X4}",
            ModeWrites.ToString(CultureInfo.InvariantCulture),
            FrameBufferControlWrites.ToString(CultureInfo.InvariantCulture),
            VdpWrites.ToString(CultureInfo.InvariantCulture),
            FrameBufferByteWrites.ToString(CultureInfo.InvariantCulture),
            PaletteByteWrites.ToString(CultureInfo.InvariantCulture),
            DreqWrites.ToString(CultureInfo.InvariantCulture),
            DreqDmaWords.ToString(CultureInfo.InvariantCulture),
            DisplayFrameBufferNonzero.ToString(CultureInfo.InvariantCulture),
            DrawFrameBufferNonzero.ToString(CultureInfo.InvariantCulture),
            DisplayFrameBufferPayloadNonzero.ToString(CultureInfo.InvariantCulture),
            DrawFrameBufferPayloadNonzero.ToString(CultureInfo.InvariantCulture),
            PaletteNonzero.ToString(CultureInfo.InvariantCulture),
            $"${Comm0:X4}",
            $"${Comm2:X4}",
            $"${Comm4:X4}",
            $"${Comm6:X4}",
            $"${MasterPc:X8}",
            $"${SlavePc:X8}",
            $"${MasterLastOpcode:X4}",
            $"${SlaveLastOpcode:X4}",
            MasterUnhandledOpcodes.ToString(CultureInfo.InvariantCulture),
            SlaveUnhandledOpcodes.ToString(CultureInfo.InvariantCulture),
            $"${MasterInterruptMask:X4}",
            $"${SlaveInterruptMask:X4}",
            PwmAudioLeft.ToString(CultureInfo.InvariantCulture),
            PwmAudioRight.ToString(CultureInfo.InvariantCulture),
            PwmAudioMono.ToString(CultureInfo.InvariantCulture),
            PwmHardwareLeft.ToString(CultureInfo.InvariantCulture),
            PwmHardwareRight.ToString(CultureInfo.InvariantCulture),
            PwmHardwareMono.ToString(CultureInfo.InvariantCulture),
            PwmCycleCounter.ToString(CultureInfo.InvariantCulture),
            PwmTimerCounter.ToString(CultureInfo.InvariantCulture),
            MasterPwmPending ? "true" : "false",
            SlavePwmPending ? "true" : "false",
            BootPending ? "true" : "false",
            BootRead ? "true" : "false",
            BootLaunch ? "true" : "false",
            Sha256,
            $"\"{Escape(BmpPath)}\"",
            $"\"{Escape(Detail)}\"");
    }

    private static string Escape(string value)
    {
        return value.Replace("\"", "\"\"", StringComparison.Ordinal);
    }
}

file sealed class PostMenuCompatibilityManifest
{
    public List<PostMenuCompatibilityCase> Cases { get; set; } = [];
}

file sealed class PostMenuCompatibilityCase
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string[]? Rom { get; set; }
    public string[]? RomNameContains { get; set; }
    public string? Script { get; set; }
    public string? Player2Script { get; set; }
    public int Frames { get; set; } = 1_800;
    public int MinimumPixels { get; set; } = 64;

    public string IdOrName()
    {
        if (!string.IsNullOrWhiteSpace(Id))
        {
            return Id!;
        }

        if (!string.IsNullOrWhiteSpace(Name))
        {
            return Name!;
        }

        return "post-menu-case";
    }

    public string NameOrId()
    {
        if (!string.IsNullOrWhiteSpace(Name))
        {
            return Name!;
        }

        return IdOrName();
    }

    public string ScriptOrDefault()
    {
        return string.IsNullOrWhiteSpace(Script) ? "none" : Script!;
    }

    public string[] RomMatches()
    {
        if (Rom is { Length: > 0 })
        {
            return Rom;
        }

        if (RomNameContains is { Length: > 0 })
        {
            return RomNameContains;
        }

        return [];
    }
}

file sealed record PostMenuCompatibilityResult(
    string Id,
    string Name,
    string RelativeRom,
    string Status,
    string Script,
    string Player2Script,
    int Frames,
    long ElapsedMs,
    double Fps,
    uint Pc,
    string Exceptions,
    string RenderMode,
    int NonBackgroundPixels,
    int MaxNonBackgroundPixels,
    int CramNonzero,
    int Sprites,
    long AudioPeak,
    string Sha256,
    string BmpPath,
    string Detail)
{
    public static PostMenuCompatibilityResult Missing(PostMenuCompatibilityCase testCase)
    {
        return new PostMenuCompatibilityResult(
            testCase.IdOrName(),
            testCase.NameOrId(),
            string.Empty,
            "missing",
            testCase.ScriptOrDefault(),
            testCase.Player2Script ?? string.Empty,
            testCase.Frames,
            0,
            0,
            0,
            string.Empty,
            string.Empty,
            0,
            0,
            0,
            0,
            0,
            string.Empty,
            string.Empty,
            "ROM not found");
    }

    public string ToCsv()
    {
        return $"\"{Escape(Id)}\",\"{Escape(Name)}\",\"{Escape(RelativeRom)}\",{Status},\"{Escape(Script)}\",\"{Escape(Player2Script)}\",{Frames},{ElapsedMs},{Fps:0.###},${Pc:X8},\"{Escape(Exceptions)}\",{RenderMode},{NonBackgroundPixels},{MaxNonBackgroundPixels},{CramNonzero},{Sprites},{AudioPeak},{Sha256},\"{Escape(BmpPath)}\",\"{Escape(Detail)}\"";
    }

    private static string Escape(string value)
    {
        return value.Replace("\"", "\"\"", StringComparison.Ordinal);
    }
}

file sealed record PerfSuiteResult(
    string RelativeRom,
    string Status,
    int Frames,
    int InstructionsPerFrame,
    double TotalMs,
    double CpuMs,
    double RenderMs,
    double AudioMs,
    double M68kMs,
    double Z80Ms,
    double VdpStepMs,
    double YmTimerMs,
    double RenderSnapshotMs,
    double RenderPaletteMs,
    double RenderScrollMs,
    double RenderSpriteGatherMs,
    double RenderPlaneBMs,
    double RenderPlaneAWindowMs,
    double RenderSpriteRenderMs,
    double RenderCompositingMs,
    double RenderBorderMs,
    double RenderDisplayFillMs,
    double RenderDirectColorMs,
    double Fps,
    long AllocatedBytes,
    long CpuAllocatedBytes,
    long RenderAllocatedBytes,
    long AudioAllocatedBytes,
    long M68kAllocatedBytes,
    long Z80AllocatedBytes,
    long VdpStepAllocatedBytes,
    long YmTimerAllocatedBytes,
    uint Pc,
    string RenderMode,
    int Sprites,
    int NonBackgroundPixels,
    long AudioPeak,
    long[] YmChannelEnergy,
    string Sha256,
    string Detail)
{
    public double CpuMsPerFrame => Frames <= 0 ? 0.0 : CpuMs / Frames;
    public double RenderMsPerFrame => Frames <= 0 ? 0.0 : RenderMs / Frames;
    public double AudioMsPerFrame => Frames <= 0 ? 0.0 : AudioMs / Frames;
    public double M68kMsPerFrame => Frames <= 0 ? 0.0 : M68kMs / Frames;
    public double Z80MsPerFrame => Frames <= 0 ? 0.0 : Z80Ms / Frames;
    public double VdpStepMsPerFrame => Frames <= 0 ? 0.0 : VdpStepMs / Frames;
    public double YmTimerMsPerFrame => Frames <= 0 ? 0.0 : YmTimerMs / Frames;
    public double RenderSnapshotMsPerFrame => Frames <= 0 ? 0.0 : RenderSnapshotMs / Frames;
    public double RenderPaletteMsPerFrame => Frames <= 0 ? 0.0 : RenderPaletteMs / Frames;
    public double RenderScrollMsPerFrame => Frames <= 0 ? 0.0 : RenderScrollMs / Frames;
    public double RenderSpriteGatherMsPerFrame => Frames <= 0 ? 0.0 : RenderSpriteGatherMs / Frames;
    public double RenderPlaneBMsPerFrame => Frames <= 0 ? 0.0 : RenderPlaneBMs / Frames;
    public double RenderPlaneAWindowMsPerFrame => Frames <= 0 ? 0.0 : RenderPlaneAWindowMs / Frames;
    public double RenderSpriteRenderMsPerFrame => Frames <= 0 ? 0.0 : RenderSpriteRenderMs / Frames;
    public double RenderCompositingMsPerFrame => Frames <= 0 ? 0.0 : RenderCompositingMs / Frames;
    public double RenderBorderMsPerFrame => Frames <= 0 ? 0.0 : RenderBorderMs / Frames;
    public double RenderDisplayFillMsPerFrame => Frames <= 0 ? 0.0 : RenderDisplayFillMs / Frames;
    public double RenderDirectColorMsPerFrame => Frames <= 0 ? 0.0 : RenderDirectColorMs / Frames;
    public double RenderSetupMsPerFrame => RenderSnapshotMsPerFrame + RenderPaletteMsPerFrame + RenderScrollMsPerFrame + RenderSpriteGatherMsPerFrame + RenderBorderMsPerFrame + RenderDisplayFillMsPerFrame + RenderDirectColorMsPerFrame;
    public double AllocatedBytesPerFrame => Frames <= 0 ? 0.0 : AllocatedBytes / (double)Frames;
    public double CpuAllocatedBytesPerFrame => Frames <= 0 ? 0.0 : CpuAllocatedBytes / (double)Frames;
    public double RenderAllocatedBytesPerFrame => Frames <= 0 ? 0.0 : RenderAllocatedBytes / (double)Frames;
    public double AudioAllocatedBytesPerFrame => Frames <= 0 ? 0.0 : AudioAllocatedBytes / (double)Frames;
    public double M68kAllocatedBytesPerFrame => Frames <= 0 ? 0.0 : M68kAllocatedBytes / (double)Frames;
    public double Z80AllocatedBytesPerFrame => Frames <= 0 ? 0.0 : Z80AllocatedBytes / (double)Frames;
    public double VdpStepAllocatedBytesPerFrame => Frames <= 0 ? 0.0 : VdpStepAllocatedBytes / (double)Frames;
    public double YmTimerAllocatedBytesPerFrame => Frames <= 0 ? 0.0 : YmTimerAllocatedBytes / (double)Frames;

    public string ToCsv()
    {
        return $"\"{Escape(RelativeRom)}\",{Status},{Frames},{InstructionsPerFrame},{TotalMs:0.###},{CpuMs:0.###},{RenderMs:0.###},{AudioMs:0.###},{M68kMs:0.###},{Z80Ms:0.###},{VdpStepMs:0.###},{YmTimerMs:0.###},{RenderSnapshotMs:0.###},{RenderPaletteMs:0.###},{RenderScrollMs:0.###},{RenderSpriteGatherMs:0.###},{RenderPlaneBMs:0.###},{RenderPlaneAWindowMs:0.###},{RenderSpriteRenderMs:0.###},{RenderCompositingMs:0.###},{RenderBorderMs:0.###},{RenderDisplayFillMs:0.###},{RenderDirectColorMs:0.###},{Fps:0.###},{CpuMsPerFrame:0.###},{RenderMsPerFrame:0.###},{AudioMsPerFrame:0.###},{AllocatedBytes},{AllocatedBytesPerFrame:0.###},{CpuAllocatedBytes},{RenderAllocatedBytes},{AudioAllocatedBytes},{M68kAllocatedBytes},{Z80AllocatedBytes},{VdpStepAllocatedBytes},{YmTimerAllocatedBytes},${Pc:X8},{RenderMode},{Sprites},{NonBackgroundPixels},{AudioPeak},{YmChannelEnergy.ElementAtOrDefault(0)},{YmChannelEnergy.ElementAtOrDefault(1)},{YmChannelEnergy.ElementAtOrDefault(2)},{YmChannelEnergy.ElementAtOrDefault(3)},{YmChannelEnergy.ElementAtOrDefault(4)},{YmChannelEnergy.ElementAtOrDefault(5)},{Sha256},\"{Escape(Detail)}\"";
    }

    private static string Escape(string value)
    {
        return value.Replace("\"", "\"\"", StringComparison.Ordinal);
    }
}

file sealed record PerfCsvRow(
    string Rom,
    string Status,
    double Fps,
    double CpuMsPerFrame,
    double RenderMsPerFrame,
    double AudioMsPerFrame);

file sealed record PerfCompareRow(
    string Rom,
    string Status,
    double BaselineFps,
    double CurrentFps,
    double FpsDeltaPct,
    double BaselineRenderMsPerFrame,
    double CurrentRenderMsPerFrame,
    double RenderDeltaPct,
    double CurrentCpuMsPerFrame,
    double CpuDeltaPct,
    double CurrentAudioMsPerFrame,
    double AudioDeltaPct);

file sealed record MovieRegressionResult(
    string RelativeMovie,
    string RelativeRom,
    string Status,
    int Frames,
    long ElapsedMs,
    double Fps,
    uint Pc,
    string Exceptions,
    string RenderMode,
    int NonBackgroundPixels,
    int CramNonzero,
    int Sprites,
    long AudioPeak,
    string Sha256,
    string BmpPath,
    string Detail)
{
    public string ToCsv()
    {
        return $"\"{Escape(RelativeMovie)}\",\"{Escape(RelativeRom)}\",{Status},{Frames},{ElapsedMs},{Fps:0.###},${Pc:X8},\"{Escape(Exceptions)}\",{RenderMode},{NonBackgroundPixels},{CramNonzero},{Sprites},{AudioPeak},{Sha256},\"{Escape(BmpPath)}\",\"{Escape(Detail)}\"";
    }

    private static string Escape(string value)
    {
        return value.Replace("\"", "\"\"", StringComparison.Ordinal);
    }
}

file sealed record CompatibilityCsvRow(
    string Rom,
    string Status,
    int Frames,
    long ElapsedMs,
    double Fps,
    uint Pc,
    string Exceptions,
    string RenderMode,
    int NonBackgroundPixels,
    int MaxNonBackgroundPixels,
    int CramNonzero,
    int Sprites,
    long AudioPeak,
    string Sha256,
    string BmpPath,
    string Detail);

file sealed record CompatibilityExportReport(
    string GeneratedAtUtc,
    string SourceCsv,
    string? LastTestedCommit,
    bool PublicMode,
    int TotalRows,
    IReadOnlyDictionary<string, int> RatingCounts,
    IReadOnlyList<CompatibilityExportEntry> Entries);

file sealed record CompatibilityExportEntry(
    string DisplayName,
    string LocalRom,
    string Rating,
    string Status,
    int Frames,
    double Fps,
    string Pc,
    string RenderMode,
    int NonBackgroundPixels,
    int MaxNonBackgroundPixels,
    int Sprites,
    long AudioPeak,
    string Sha256,
    string Screenshot,
    string Notes);

file sealed class CapturedDmaChunk(int frame, uint sourceAddress, uint destinationAddress, byte code)
{
    public int Frame { get; } = frame;
    public uint SourceAddress { get; } = sourceAddress & 0x00FF_FFFE;
    public uint DestinationAddress { get; } = destinationAddress & 0xFFFE;
    public byte Code { get; } = code;
    public int LengthWords { get; set; }
}

file readonly record struct DmaTransferSample(
    int Frame,
    int Index,
    uint RequestedSourceAddress,
    uint SourceAddress,
    uint DestinationAddress,
    ushort Value,
    bool HasSourceSamples,
    ushort SourceBeforeStep,
    ushort SourceAfterTransfer,
    long MasterCycleStart,
    long MasterCycleEnd,
    int Scanline);

file readonly record struct Sh2FaultTraceRow(
    int Frame,
    int Sequence,
    string Cpu,
    uint Pc,
    ushort Opcode,
    uint NextPc,
    uint Sr,
    uint R0,
    uint R1,
    uint R2,
    uint R3,
    uint R4,
    uint R5,
    uint R6,
    uint R7,
    uint R15);

file sealed record FillLoopKey(int Frame, string Cpu, uint LoopPc, string Target);

file sealed class FillLoopSummary(FillLoopKey key)
{
    public FillLoopKey Key { get; } = key;
    public int Hits { get; set; }
    public long FirstMasterCycle { get; set; }
    public long LastMasterCycle { get; set; }
    public int FirstScanline { get; set; }
    public int LastScanline { get; set; }
    public uint FirstAddress { get; set; }
    public uint LastAddress { get; set; }
    public uint Value { get; set; }
    public uint FirstCount { get; set; }
    public uint LastCount { get; set; }
    public int Increment { get; set; }
    public int AddressRegister { get; set; }
    public int SourceRegister { get; set; }
    public int CountRegister { get; set; }
    public ushort FrameBufferControl { get; set; }
    public ushort BitmapMode { get; set; }
    public int DrawFrameBufferIndex { get; set; }
    public int DisplayFrameBufferIndex { get; set; }
    public bool SwapPending { get; set; }
    public int DeniedFrameBufferAccessCount { get; set; }
    public int FrameBufferByteWriteCount { get; set; }

    public string ToCsv()
    {
        return string.Join(
            ',',
            Key.Frame.ToString(CultureInfo.InvariantCulture),
            Quote(Key.Cpu),
            $"${Key.LoopPc:X8}",
            Quote(Key.Target),
            Hits.ToString(CultureInfo.InvariantCulture),
            FirstMasterCycle.ToString(CultureInfo.InvariantCulture),
            LastMasterCycle.ToString(CultureInfo.InvariantCulture),
            FirstScanline.ToString(CultureInfo.InvariantCulture),
            LastScanline.ToString(CultureInfo.InvariantCulture),
            $"${FirstAddress:X8}",
            $"${LastAddress:X8}",
            $"${Value:X4}",
            FirstCount.ToString(CultureInfo.InvariantCulture),
            LastCount.ToString(CultureInfo.InvariantCulture),
            Increment.ToString(CultureInfo.InvariantCulture),
            AddressRegister.ToString(CultureInfo.InvariantCulture),
            SourceRegister.ToString(CultureInfo.InvariantCulture),
            CountRegister.ToString(CultureInfo.InvariantCulture),
            $"${FrameBufferControl:X4}",
            $"${BitmapMode:X4}",
            DrawFrameBufferIndex.ToString(CultureInfo.InvariantCulture),
            DisplayFrameBufferIndex.ToString(CultureInfo.InvariantCulture),
            SwapPending ? "true" : "false",
            DeniedFrameBufferAccessCount.ToString(CultureInfo.InvariantCulture),
            FrameBufferByteWriteCount.ToString(CultureInfo.InvariantCulture));
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}

file readonly record struct VirtuaRacingExpectedDma(uint SourceAddress, uint DestinationAddress, int LengthWords);

file readonly record struct VirtuaRacingDmaMatch(VirtuaRacingExpectedDma Expected, CapturedDmaChunk? Observed, bool Matched);

file readonly record struct VirtuaRacingDmaReferenceComparison(
    uint SourceAddress,
    uint DestinationAddress,
    int LengthWords,
    int SampledWords,
    int ComparedWords,
    int TransferredMismatches,
    int FinalVramMismatches,
    int SourceChangedDuringDma,
    int FirstTransferredMismatchIndex,
    int FirstFinalMismatchIndex,
    bool IsLatestDestinationOwner);

file readonly record struct VirtuaRacingNameTableCheck(
    int BaseAddress,
    int TableWidth,
    int ComparedCells,
    int ExactMismatchedCells,
    int BestDelta,
    int BestDeltaMismatchedCells,
    int ZeroRowCells,
    int ZeroRowMismatches);

file readonly record struct SvpHistoryEntry(
    int Frame,
    ulong Sequence,
    SvpDevice.SvpInstructionTrace Trace);
