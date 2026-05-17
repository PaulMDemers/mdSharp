namespace MdSharp.Core.Timing;

public sealed class GenesisScheduler(bool pal)
{
    public const int NtscMasterClock = 53_693_175;
    public const int PalMasterClock = 53_203_424;
    public const int M68kDivider = 7;
    public const int Z80Divider = 15;
    public const int ScanlineCountNtsc = 262;
    public const int ScanlineCountPal = 313;
    public const int ActiveDisplayMasterCycles = 2560;

    public bool IsPal { get; } = pal;
    public long MasterCycles { get; private set; }
    public int FrameNumber { get; private set; }
    public int Scanline { get; private set; }

    public int ScanlinesPerFrame => IsPal ? ScanlineCountPal : ScanlineCountNtsc;
    public int MasterClock => IsPal ? PalMasterClock : NtscMasterClock;
    public double FrameRate => IsPal ? 50.0 : 59.922743;
    public int MasterCyclesPerFrame => (int)Math.Round(MasterClock / FrameRate);
    public int MasterCyclesPerScanline => MasterCyclesPerFrame / ScanlinesPerFrame;
    public int M68kCyclesPerScanline => Math.Max(1, MasterCyclesPerScanline / M68kDivider);
    public int Z80CyclesPerScanline => Math.Max(1, MasterCyclesPerScanline / Z80Divider);
    public int ActiveDisplayM68kCycles => Math.Clamp(ActiveDisplayMasterCycles / M68kDivider, 1, M68kCyclesPerScanline);
    public int HBlankM68kCycles => Math.Max(1, M68kCyclesPerScanline - ActiveDisplayM68kCycles);

    public void BeginFrame()
    {
        Scanline = 0;
    }

    public void AdvanceScanline()
    {
        MasterCycles += MasterCyclesPerScanline;
        Scanline++;
        if (Scanline >= ScanlinesPerFrame)
        {
            FrameNumber++;
            Scanline = 0;
        }
    }

    public SchedulerState CaptureState()
    {
        return new SchedulerState(MasterCycles, FrameNumber, Scanline);
    }

    public void RestoreState(SchedulerState state)
    {
        MasterCycles = state.MasterCycles;
        FrameNumber = state.FrameNumber;
        Scanline = state.Scanline;
    }

    public sealed record SchedulerState(long MasterCycles, int FrameNumber, int Scanline);
}
