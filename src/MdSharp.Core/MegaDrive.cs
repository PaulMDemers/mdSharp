using MdSharp.Core.Audio;
using MdSharp.Core.Bus;
using MdSharp.Core.Cartridge;
using MdSharp.Core.Cpu.M68k;
using MdSharp.Core.Cpu.Z80;
using MdSharp.Core.SegaCd;
using MdSharp.Core.ThirtyTwoX;
using MdSharp.Core.Timing;
using MdSharp.Core.Video;

namespace MdSharp.Core;

public sealed class MegaDrive
{
    private const int ThirtyTwoXMinimumCycleBatch = 2048;
    private const int SegaCdMinimumCycleBatch = 256;
    private const int ThirtyTwoXSystemWordPollLoopCycles = 26;
    private const int ResetAudioFadeInSamples = 512;
    private const int MaxStoppedM68kIdleBatchCycles = 64;
    private const byte M68kVBlankInterruptBits = (1 << 5) | (1 << 6);
    private const uint SegaCdMainBootVectorRestoreStart = 0x00FF_0584;
    private const uint SegaCdMainBootVectorRestoreEndExclusive = 0x00FF_059C;
    private const uint SegaCdMainBootGenericFlag7Clear = 0x00FF_05BE;
    private const uint SegaCdMainBootGenericReadyPollStart = 0x00FF_05C6;
    private const uint SegaCdMainBootGenericReadyPollBranch = 0x00FF_05CE;
    private const uint SegaCdMainBootGenericLowWaitLoop = 0x0000_0210;
    private const uint SegaCdMainBootAlternateHelperStart = 0x00FF_0698;
    private const uint SegaCdMainBootAlternateHelperEndExclusive = 0x00FF_06A0;
    private const uint SegaCdMainBootHelperStart = 0x00FF_1024;
    private const uint SegaCdMainBootHelperEndExclusive = 0x00FF_104E;
    private static readonly double PsgFilterAlpha = LowPassAlpha(AudioConstants.PsgLowPassCutoffHz, AudioConstants.DefaultSampleRate);
    private static readonly double BassShelfAlpha = LowPassAlpha(AudioConstants.BassShelfCutoffHz, AudioConstants.DefaultSampleRate);
    private static readonly double OutputFilterAlpha = LowPassAlpha(AudioConstants.OutputLowPassCutoffHz, AudioConstants.DefaultSampleRate);

    public MegaDrive(CartridgeImage cartridge, bool pal = false, ReadOnlyMemory<byte>? thirtyTwoXM68kBios = null, ReadOnlyMemory<byte>? thirtyTwoXMasterSh2Bios = null, ReadOnlyMemory<byte>? thirtyTwoXSlaveSh2Bios = null, bool thirtyTwoXUseRealSh2BiosBoot = false, SegaCdDevice? segaCd = null)
    {
        IsPal = pal;
        Vdp = new Vdp();
        Psg = new Psg();
        Ym2612 = new Ym2612();
        Bus = new GenesisBus(
            cartridge,
            Vdp,
            Psg,
            Ym2612,
            pal,
            thirtyTwoXM68kBios: thirtyTwoXM68kBios,
            thirtyTwoXMasterSh2Bios: thirtyTwoXMasterSh2Bios,
            thirtyTwoXSlaveSh2Bios: thirtyTwoXSlaveSh2Bios,
            thirtyTwoXUseRealSh2BiosBoot: thirtyTwoXUseRealSh2BiosBoot,
            segaCd: segaCd);
        MainCpu = new M68kCpu(Bus);
        MainCpu.LineFInstructionOverride = TryHandleThirtyTwoXSdkLineF;
        MainCpu.TrapInstructionOverride = TryHandleThirtyTwoXSdkTrap;
        Z80 = new Z80Core();
        Scheduler = new GenesisScheduler(pal);
    }

    public bool IsPal { get; }
    public GenesisBus Bus { get; }
    public M68kCpu MainCpu { get; }
    public Z80Core Z80 { get; }
    public GenesisScheduler Scheduler { get; }
    public Vdp Vdp { get; }
    public Psg Psg { get; }
    public Ym2612 Ym2612 { get; }
    public long Frames { get; private set; }
    public Action<Z80InstructionTrace>? Z80InstructionObserver { get; set; }
    public bool CollectFramePerformance { get; set; }
    public FramePerformanceCounters LastFramePerformance { get; private set; }
    public long ThirtyTwoXScheduledInstructionRequests => _thirtyTwoXScheduledInstructionRequests;
    public long ThirtyTwoXExecutedInstructionSteps => _thirtyTwoXExecutedInstructionSteps;
    public long SegaCdSubCpuScheduledCycles => _segaCdSubCpuScheduledCycles;
    public long SegaCdSubCpuExecutedCycles => _segaCdSubCpuExecutedCycles;
    public long M68kFastPathHits => _m68kFastPathHits;
    public long M68kFastPathCycles => _m68kFastPathCycles;
    public long M68kThirtyTwoXSystemWordPollFastPathHits => _m68kThirtyTwoXSystemWordPollFastPathHits;
    public long M68kThirtyTwoXSystemWordPollFastPathCycles => _m68kThirtyTwoXSystemWordPollFastPathCycles;
    public long M68kLongTstBneWaitFastPathHits => _m68kLongTstBneWaitFastPathHits;
    public long M68kLongTstBneWaitFastPathCycles => _m68kLongTstBneWaitFastPathCycles;
    public long M68kMoveByteFillDbfFastPathHits => _m68kMoveByteFillDbfFastPathHits;
    public long M68kMoveByteFillDbfFastPathCycles => _m68kMoveByteFillDbfFastPathCycles;
    public long M68kLongCmpBeqWaitFastPathHits => _m68kLongCmpBeqWaitFastPathHits;
    public long M68kLongCmpBeqWaitFastPathCycles => _m68kLongCmpBeqWaitFastPathCycles;
    public long M68kMoveByteCopyDbfFastPathHits => _m68kMoveByteCopyDbfFastPathHits;
    public long M68kMoveByteCopyDbfFastPathCycles => _m68kMoveByteCopyDbfFastPathCycles;
    public long M68kMoveLongCopyDbfFastPathHits => _m68kMoveLongCopyDbfFastPathHits;
    public long M68kMoveLongCopyDbfFastPathCycles => _m68kMoveLongCopyDbfFastPathCycles;
    public long M68kMoveWordVdpFillDbfFastPathHits => _m68kMoveWordVdpFillDbfFastPathHits;
    public long M68kMoveWordVdpFillDbfFastPathCycles => _m68kMoveWordVdpFillDbfFastPathCycles;
    public long M68kBitReaderFastPathHits => _m68kBitReaderFastPathHits;
    public long M68kBitReaderFastPathCycles => _m68kBitReaderFastPathCycles;
    public long M68kWordPairCompareFastPathHits => _m68kWordPairCompareFastPathHits;
    public long M68kWordPairCompareFastPathCycles => _m68kWordPairCompareFastPathCycles;
    private byte _pendingM68kInterruptLevels;
    private double _audioSampleCarry;
    private double _psgFilter;
    private double _audioBassFilterLeft;
    private double _audioBassFilterRight;
    private double _audioFilterLeft;
    private double _audioFilterRight;
    private int _audioFadeInSamplesRemaining;
    private short[] _psgMixBuffer = new short[1024];
    private short[] _ymMixBuffer = new short[2048];
    private short[] _thirtyTwoXPwmMixBuffer = new short[2048];
    private short[] _segaCdCddaMixBuffer = new short[2048];
    private short[] _segaCdPcmMixBuffer = new short[2048];
    private FramePerformanceAccumulator _framePerformance;
    private long _z80MasterCycleCursor;
    private double _thirtyTwoXInstructionCarry;
    private double _segaCdSubCpuCycleCarry;
    private long _thirtyTwoXScheduledInstructionRequests;
    private long _thirtyTwoXExecutedInstructionSteps;
    private long _segaCdSubCpuScheduledCycles;
    private long _segaCdSubCpuExecutedCycles;
    private long _m68kFastPathHits;
    private long _m68kFastPathCycles;
    private long _m68kThirtyTwoXSystemWordPollFastPathHits;
    private long _m68kThirtyTwoXSystemWordPollFastPathCycles;
    private long _m68kLongTstBneWaitFastPathHits;
    private long _m68kLongTstBneWaitFastPathCycles;
    private long _m68kMoveByteFillDbfFastPathHits;
    private long _m68kMoveByteFillDbfFastPathCycles;
    private long _m68kLongCmpBeqWaitFastPathHits;
    private long _m68kLongCmpBeqWaitFastPathCycles;
    private long _m68kMoveByteCopyDbfFastPathHits;
    private long _m68kMoveByteCopyDbfFastPathCycles;
    private long _m68kMoveLongCopyDbfFastPathHits;
    private long _m68kMoveLongCopyDbfFastPathCycles;
    private long _m68kMoveWordVdpFillDbfFastPathHits;
    private long _m68kMoveWordVdpFillDbfFastPathCycles;
    private long _m68kBitReaderFastPathHits;
    private long _m68kBitReaderFastPathCycles;
    private long _m68kWordPairCompareFastPathHits;
    private long _m68kWordPairCompareFastPathCycles;

    public void Reset()
    {
        MainCpu.Reset();
        Z80.Reset();
        Psg.Reset();
        Ym2612.Reset();
        Bus.ResetAddOnHardware();
        Vdp.BeginFrame(IsPal);
        Scheduler.RestoreState(new GenesisScheduler.SchedulerState(0, 0, 0));
        Frames = 0;
        _pendingM68kInterruptLevels = 0;
        _audioSampleCarry = 0.0;
        _psgFilter = 0.0;
        _audioBassFilterLeft = 0.0;
        _audioBassFilterRight = 0.0;
        _audioFilterLeft = 0.0;
        _audioFilterRight = 0.0;
        _audioFadeInSamplesRemaining = ResetAudioFadeInSamples;
        _z80MasterCycleCursor = 0;
        _thirtyTwoXInstructionCarry = 0.0;
        _segaCdSubCpuCycleCarry = 0.0;
        _thirtyTwoXScheduledInstructionRequests = 0;
        _thirtyTwoXExecutedInstructionSteps = 0;
        _segaCdSubCpuScheduledCycles = 0;
        _segaCdSubCpuExecutedCycles = 0;
        _m68kFastPathHits = 0;
        _m68kFastPathCycles = 0;
        _m68kThirtyTwoXSystemWordPollFastPathHits = 0;
        _m68kThirtyTwoXSystemWordPollFastPathCycles = 0;
        _m68kLongTstBneWaitFastPathHits = 0;
        _m68kLongTstBneWaitFastPathCycles = 0;
        _m68kMoveByteFillDbfFastPathHits = 0;
        _m68kMoveByteFillDbfFastPathCycles = 0;
        _m68kLongCmpBeqWaitFastPathHits = 0;
        _m68kLongCmpBeqWaitFastPathCycles = 0;
        _m68kMoveByteCopyDbfFastPathHits = 0;
        _m68kMoveByteCopyDbfFastPathCycles = 0;
        _m68kMoveLongCopyDbfFastPathHits = 0;
        _m68kMoveLongCopyDbfFastPathCycles = 0;
        _m68kMoveWordVdpFillDbfFastPathHits = 0;
        _m68kMoveWordVdpFillDbfFastPathCycles = 0;
        _m68kBitReaderFastPathHits = 0;
        _m68kBitReaderFastPathCycles = 0;
        _m68kWordPairCompareFastPathHits = 0;
        _m68kWordPairCompareFastPathCycles = 0;
    }

    public void StepInstruction()
    {
        MainCpu.Step();
    }

    public void RunFrame(int maxInstructions = 200_000)
    {
        RunFrameCycles(maxInstructions);
    }

    public void RunFrameCycles(int maxInstructions = 200_000)
    {
        TryRunFrameCycles(maxInstructions);
    }

    public bool TryRunFrameCycles(int maxInstructions = 200_000, Func<bool>? shouldAbort = null)
    {
        bool collectPerformance = CollectFramePerformance;
        if (collectPerformance)
        {
            _framePerformance = default;
        }

        int scanlines = IsPal ? MdSharp.Core.Video.Vdp.PalScanlines : MdSharp.Core.Video.Vdp.NtscScanlines;
        int remainingInstructions = Math.Max(1, maxInstructions);
        Vdp.BeginFrame(IsPal);
        Psg.BeginAudioFrame(Scheduler.MasterCycles, Scheduler.MasterCycles + Scheduler.MasterCyclesPerFrame);
        Ym2612.BeginAudioFrame(Scheduler.MasterCycles, Scheduler.MasterCycles + Scheduler.MasterCyclesPerFrame);
        Scheduler.BeginFrame();
        Bus.ThirtyTwoX?.BeginFrame(IsPal);
        if (_z80MasterCycleCursor < Scheduler.MasterCycles)
        {
            _z80MasterCycleCursor = Scheduler.MasterCycles;
        }

        bool z80InterruptPending = false;
        Bus.MasterCyclesPerScanline = Scheduler.MasterCyclesPerScanline;
        Bus.BeginLightGunFrame();
        ThirtyTwoXDevice? diagnosticAbortDevice = shouldAbort is null ? null : Bus.ThirtyTwoX;
        Func<bool>? previousDiagnosticAbort = diagnosticAbortDevice?.DiagnosticAbort;
        if (diagnosticAbortDevice is not null)
        {
            diagnosticAbortDevice.DiagnosticAbort = shouldAbort;
        }

        try
        {
            for (int line = 0; line < scanlines; line++)
            {
                if (shouldAbort?.Invoke() == true)
                {
                    return false;
                }

                Vdp.Interrupts interrupts;
                if (collectPerformance)
                {
                    long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                    long start = System.Diagnostics.Stopwatch.GetTimestamp();
                    interrupts = Vdp.StepScanline(line, IsPal);
                    _framePerformance.VdpTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
                    _framePerformance.VdpAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                }
                else
                {
                    interrupts = Vdp.StepScanline(line, IsPal);
                }
                Bus.ThirtyTwoX?.StepScanline(line, IsPal);
                Bus.UpdateLightGunForScanline(line);

                if ((interrupts & Vdp.Interrupts.Horizontal) != 0 && HBlankInterruptEnabled())
                {
                    if (MainCpu.RequestInterrupt(4))
                    {
                        Vdp.AcknowledgeM68kInterrupt(4);
                    }
                }

                if ((interrupts & Vdp.Interrupts.Vertical) != 0)
                {
                    z80InterruptPending = true;
                    Bus.SegaCd?.PulseMainVBlankInterrupt();
                    if (VBlankInterruptEnabled())
                    {
                        QueueM68kInterrupt(GetM68kVBlankInterruptLevel());
                    }
                }

                int activeLineCycles = Scheduler.ActiveDisplayM68kCycles;
                int hblankLineCycles = Math.Max(1, Scheduler.M68kCyclesPerScanline - activeLineCycles);
                int activeDmaDebt = Vdp.ConsumeDmaCycleDebt(activeLineCycles);
                if (!RunAddOnHardwareForMasterCycles((long)activeDmaDebt * GenesisScheduler.M68kDivider, shouldAbort))
                {
                    return false;
                }

                int activeBudget = Math.Max(0, activeLineCycles - activeDmaDebt);
                Bus.ThirtyTwoX?.SetHBlank(false);
                int consumed = RunCpuSlice(activeBudget, 0, ref remainingInstructions, ref _z80MasterCycleCursor, ref z80InterruptPending, shouldAbort);
                if (consumed < 0)
                {
                    return false;
                }

                Vdp.SetHBlank(true);
                Bus.ThirtyTwoX?.SetHBlank(true);
                int hblankDmaDebt = Vdp.ConsumeDmaCycleDebt(hblankLineCycles);
                if (!RunAddOnHardwareForMasterCycles((long)hblankDmaDebt * GenesisScheduler.M68kDivider, shouldAbort))
                {
                    return false;
                }

                int hblankBudget = Math.Max(0, hblankLineCycles - hblankDmaDebt);
                int hblankConsumed = RunCpuSlice(hblankBudget, activeLineCycles, ref remainingInstructions, ref _z80MasterCycleCursor, ref z80InterruptPending, shouldAbort);
                if (hblankConsumed < 0)
                {
                    return false;
                }

                consumed += hblankConsumed;
                Vdp.SetHBlank(false);
                Bus.ThirtyTwoX?.SetHBlank(false);

                int elapsedLineCycles = Scheduler.M68kCyclesPerScanline;
                if (collectPerformance)
                {
                    long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                    long start = System.Diagnostics.Stopwatch.GetTimestamp();
                    Ym2612.Step(elapsedLineCycles);
                    _framePerformance.YmTimerTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
                    _framePerformance.YmTimerAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                }
                else
                {
                    Ym2612.Step(elapsedLineCycles);
                }

                Bus.CurrentMasterCycle = Scheduler.MasterCycles + Scheduler.MasterCyclesPerScanline;
                Bus.SyncSvpToCurrentMasterCycle();

                Scheduler.AdvanceScanline();
            }

            if (collectPerformance)
            {
                LastFramePerformance = _framePerformance.ToCounters();
            }

            Frames++;
            return true;
        }
        finally
        {
            if (diagnosticAbortDevice is not null)
            {
                diagnosticAbortDevice.DiagnosticAbort = previousDiagnosticAbort;
            }
        }
    }

    private int RunCpuSlice(int cycleBudget, int lineCycleOffset, ref int remainingInstructions, ref long z80MasterCycleCursor, ref bool z80InterruptPending, Func<bool>? shouldAbort)
    {
        if (cycleBudget <= 0)
        {
            return 0;
        }

        int consumed = 0;
        long lineStartMasterCycle = Scheduler.MasterCycles;
        Action<Z80InstructionTrace>? z80Observer = Z80InstructionObserver;
        if (remainingInstructions <= 0)
        {
            Bus.CurrentMasterCycle = lineStartMasterCycle + (lineCycleOffset * GenesisScheduler.M68kDivider);
            Bus.CurrentScanlineMasterCycleOffset = lineCycleOffset * GenesisScheduler.M68kDivider;
            ServicePendingM68kInterrupts();
            if (TryHandleSegaCdMainBootAlternateHelperRts(out int exhaustedAlternateHelperRtsCycles))
            {
                if (!RunAddOnHardwareForMasterCycles((long)exhaustedAlternateHelperRtsCycles * GenesisScheduler.M68kDivider, shouldAbort))
                {
                    return -1;
                }

                return Math.Min(cycleBudget, exhaustedAlternateHelperRtsCycles);
            }

            if (!RunAddOnHardwareForMasterCycles((long)cycleBudget * GenesisScheduler.M68kDivider, shouldAbort))
            {
                return -1;
            }

            long exhaustedSliceEndMasterCycle = lineStartMasterCycle + ((lineCycleOffset + cycleBudget) * GenesisScheduler.M68kDivider);
            RunZ80Until(lineStartMasterCycle, exhaustedSliceEndMasterCycle, ref z80MasterCycleCursor, ref z80InterruptPending, z80Observer);
            Bus.CurrentMasterCycle = exhaustedSliceEndMasterCycle;
            Bus.CurrentScanlineMasterCycleOffset = (lineCycleOffset + cycleBudget) * GenesisScheduler.M68kDivider;
            return cycleBudget;
        }

        while (consumed < cycleBudget && remainingInstructions > 0)
        {
            if (shouldAbort?.Invoke() == true)
            {
                return -1;
            }

            Bus.CurrentMasterCycle = lineStartMasterCycle + ((lineCycleOffset + consumed) * GenesisScheduler.M68kDivider);
            Bus.CurrentScanlineMasterCycleOffset = (lineCycleOffset + consumed) * GenesisScheduler.M68kDivider;
            Bus.CurrentM68kPc = MainCpu.PC & 0x00FF_FFFFu;
            ServicePendingM68kInterrupts();
            bool allowM68kLoopFastPaths = Bus.SegaCd is null;
            ClearSegaCdSonicCdIpxVSyncWaitIfNeeded();
            ClearSegaCdGenericBootMainProgramWaitFlagIfNeeded();

            if (TryHandleSegaCdMainBootAlternateHelperRts(out int earlyAlternateHelperRtsCycles))
            {
                if (!RunAddOnHardwareForMasterCycles((long)earlyAlternateHelperRtsCycles * GenesisScheduler.M68kDivider, shouldAbort))
                {
                    return -1;
                }

                consumed += earlyAlternateHelperRtsCycles;
                remainingInstructions = Math.Max(0, remainingInstructions - 1);
                continue;
            }

            if (TryHandleSegaCdGenericBootFinalMainProgramHandoff(out int genericBootHandoffCycles))
            {
                if (!RunAddOnHardwareForMasterCycles((long)genericBootHandoffCycles * GenesisScheduler.M68kDivider, shouldAbort))
                {
                    return -1;
                }

                consumed += genericBootHandoffCycles;
                remainingInstructions = Math.Max(0, remainingInstructions - 1);
                continue;
            }

            if (IsSegaCdMainBiosGenericFlag7PulseClear())
            {
                int yieldCycles = Math.Min(cycleBudget - consumed, SegaCdMinimumCycleBatch);
                MainCpu.AddWaitCycles(yieldCycles);
                if (!RunAddOnHardwareForMasterCycles((long)yieldCycles * GenesisScheduler.M68kDivider, shouldAbort))
                {
                    return -1;
                }

                consumed += yieldCycles;
                continue;
            }

            if (IsSegaCdMainBiosGenericReadyPollLoop())
            {
                int yieldCycles = Math.Min(cycleBudget - consumed, SegaCdMinimumCycleBatch);
                MainCpu.AddWaitCycles(yieldCycles);
                if (!RunAddOnHardwareForMasterCycles((long)yieldCycles * GenesisScheduler.M68kDivider, shouldAbort))
                {
                    return -1;
                }

                consumed += yieldCycles;
                continue;
            }

            if (IsSegaCdMainGenericLowWaitLoop())
            {
                int yieldCycles = Math.Min(cycleBudget - consumed, SegaCdMinimumCycleBatch);
                MainCpu.AddWaitCycles(yieldCycles);
                if (!RunAddOnHardwareForMasterCycles((long)yieldCycles * GenesisScheduler.M68kDivider, shouldAbort))
                {
                    return -1;
                }

                consumed += yieldCycles;
                continue;
            }

            if (Bus.SegaCd is not null &&
                IsSegaCdSonicCdIpxWordRamWaitLoop() &&
                (Bus.SegaCd.TryHandleSonicCdIpxWordRamWaitHle((ushort)(MainCpu.D[1] & 0xFFFF)) ||
                    Bus.SegaCd.TryReturnSonicCdIpxWordRamToMainHle()))
            {
                const int hleCycles = 8;
                MainCpu.AddWaitCycles(hleCycles);
                if (!RunAddOnHardwareForMasterCycles((long)hleCycles * GenesisScheduler.M68kDivider, shouldAbort))
                {
                    return -1;
                }

                consumed += hleCycles;
                continue;
            }

            if (TryFastForwardThirtyTwoXSystemWordPollLoop(cycleBudget - consumed, out int pollCycles))
            {
                RecordM68kFastPath(pollCycles, ref _m68kThirtyTwoXSystemWordPollFastPathHits, ref _m68kThirtyTwoXSystemWordPollFastPathCycles);
                ThirtyTwoXDevice thirtyTwoX = Bus.ThirtyTwoX!;
                MainCpu.AddWaitCycles(pollCycles);
                if (!RunAddOnHardwareForMasterCycles((long)pollCycles * GenesisScheduler.M68kDivider, shouldAbort))
                {
                    return -1;
                }

                int skippedPolls = pollCycles / ThirtyTwoXSystemWordPollLoopCycles;
                thirtyTwoX.SetCurrentMasterCycle(Bus.CurrentMasterCycle);
                int sh2Executed = thirtyTwoX.RunSh2Cycles(skippedPolls * 32, shouldAbort);
                if (sh2Executed < 0)
                {
                    return -1;
                }

                _thirtyTwoXExecutedInstructionSteps += sh2Executed;
                ushort value = thirtyTwoX.ReadSystemRegisterWord(0x20);
                MainCpu.D[0] = (MainCpu.D[0] & 0xFFFF_0000u) | value;
                consumed += pollCycles;
                continue;
            }

            if (allowM68kLoopFastPaths &&
                !HasServiceableM68kInterrupt() &&
                MainCpu.TryFastForwardLongAbsoluteTstBneWaitLoop(
                    cycleBudget - consumed,
                    IsM68kWorkRamAddress,
                    out int waitLoopCycles,
                    out int waitLoopInstructions))
            {
                RecordM68kFastPath(waitLoopCycles, ref _m68kLongTstBneWaitFastPathHits, ref _m68kLongTstBneWaitFastPathCycles);
                if (!RunAddOnHardwareForMasterCycles((long)waitLoopCycles * GenesisScheduler.M68kDivider, shouldAbort))
                {
                    return -1;
                }

                consumed += waitLoopCycles;
                continue;
            }

            if (allowM68kLoopFastPaths &&
                Bus.ThirtyTwoX is null &&
                MainCpu.TryFastForwardMoveBytePostIncrementDbfLoop(cycleBudget - consumed, out int fastCycles, out int fastInstructions))
            {
                RecordM68kFastPath(fastCycles, ref _m68kMoveByteFillDbfFastPathHits, ref _m68kMoveByteFillDbfFastPathCycles);
                if (!RunAddOnHardwareForMasterCycles((long)fastCycles * GenesisScheduler.M68kDivider, shouldAbort))
                {
                    return -1;
                }

                consumed += fastCycles;
                remainingInstructions = Math.Max(0, remainingInstructions - fastInstructions);
                continue;
            }

            if (Bus.SegaCd is not null &&
                MainCpu.TryFastForwardMoveLongRegisterQuadFillDbfLoop(
                    IsSegaCdSonicCdIpxFileBufferClearLoop() ? 0x40000 : cycleBudget - consumed,
                    IsM68kFastSegaCdProgramCopyAddress,
                    out int quadFillCycles,
                    out int quadFillInstructions))
            {
                int quadFillWaitCycles = Bus.ConsumeM68kWaitCycles();
                MainCpu.AddWaitCycles(quadFillWaitCycles);
                int elapsedCycles = quadFillCycles + quadFillWaitCycles;
                RecordM68kFastPath(elapsedCycles);
                if (!RunAddOnHardwareForMasterCycles((long)elapsedCycles * GenesisScheduler.M68kDivider, shouldAbort))
                {
                    return -1;
                }

                consumed += Math.Min(cycleBudget - consumed, elapsedCycles);
                remainingInstructions = Math.Max(0, remainingInstructions - quadFillInstructions);
                continue;
            }

            if (allowM68kLoopFastPaths &&
                !HasServiceableM68kInterrupt() &&
                MainCpu.TryFastForwardLongAbsoluteCmpBeqWaitLoop(
                    cycleBudget - consumed,
                    IsM68kWorkRamAddress,
                    out int cmpWaitLoopCycles,
                    out int cmpWaitLoopInstructions))
            {
                RecordM68kFastPath(cmpWaitLoopCycles, ref _m68kLongCmpBeqWaitFastPathHits, ref _m68kLongCmpBeqWaitFastPathCycles);
                if (!RunAddOnHardwareForMasterCycles((long)cmpWaitLoopCycles * GenesisScheduler.M68kDivider, shouldAbort))
                {
                    return -1;
                }

                consumed += cmpWaitLoopCycles;
                continue;
            }

            if (allowM68kLoopFastPaths &&
                !HasServiceableM68kInterrupt() &&
                MainCpu.TryFastForwardShiftRegisterBitReaderLoop(cycleBudget - consumed, out int bitReaderCycles, out int bitReaderInstructions))
            {
                RecordM68kFastPath(bitReaderCycles, ref _m68kBitReaderFastPathHits, ref _m68kBitReaderFastPathCycles);
                if (!RunAddOnHardwareForMasterCycles((long)bitReaderCycles * GenesisScheduler.M68kDivider, shouldAbort))
                {
                    return -1;
                }

                consumed += bitReaderCycles;
                remainingInstructions = Math.Max(0, remainingInstructions - bitReaderInstructions);
                continue;
            }

            if (allowM68kLoopFastPaths &&
                !HasServiceableM68kInterrupt() &&
                MainCpu.TryFastForwardWordPairCompareSubroutineDbfLoop(cycleBudget - consumed, out int wordPairCycles, out int wordPairInstructions))
            {
                RecordM68kFastPath(wordPairCycles, ref _m68kWordPairCompareFastPathHits, ref _m68kWordPairCompareFastPathCycles);
                if (!RunAddOnHardwareForMasterCycles((long)wordPairCycles * GenesisScheduler.M68kDivider, shouldAbort))
                {
                    return -1;
                }

                consumed += wordPairCycles;
                remainingInstructions = Math.Max(0, remainingInstructions - wordPairInstructions);
                continue;
            }

            if (allowM68kLoopFastPaths &&
                !HasServiceableM68kInterrupt() &&
                MainCpu.TryFastForwardBtstRegisterDbccLoop(cycleBudget - consumed, out int btstDbccCycles, out int btstDbccInstructions))
            {
                RecordM68kFastPath(btstDbccCycles);
                if (!RunAddOnHardwareForMasterCycles((long)btstDbccCycles * GenesisScheduler.M68kDivider, shouldAbort))
                {
                    return -1;
                }

                consumed += btstDbccCycles;
                remainingInstructions = Math.Max(0, remainingInstructions - btstDbccInstructions);
                continue;
            }

            if (allowM68kLoopFastPaths &&
                !HasServiceableM68kInterrupt() &&
                MainCpu.TryFastForwardMoveBytePostIncrementCopyDbfLoop(
                    cycleBudget - consumed,
                    IsM68kFastByteCopyAddress,
                    out int byteCopyCycles,
                    out int byteCopyInstructions))
            {
                int byteCopyWaitCycles = Bus.ConsumeM68kWaitCycles();
                MainCpu.AddWaitCycles(byteCopyWaitCycles);
                int elapsedCycles = byteCopyCycles + byteCopyWaitCycles;
                RecordM68kFastPath(elapsedCycles, ref _m68kMoveByteCopyDbfFastPathHits, ref _m68kMoveByteCopyDbfFastPathCycles);
                if (!RunAddOnHardwareForMasterCycles((long)elapsedCycles * GenesisScheduler.M68kDivider, shouldAbort))
                {
                    return -1;
                }

                consumed += elapsedCycles;
                remainingInstructions = Math.Max(0, remainingInstructions - byteCopyInstructions);
                continue;
            }

            bool segaCdMainBiosProgramCopyLoop = IsSegaCdMainBiosProgramCopyLoop();
            uint segaCdMainBiosHelperPc = MainCpu.PC & 0x00FF_FFFFu;
            if (TryHandleSegaCdMainBootAlternateHelperRts(out int alternateHelperRtsCycles))
            {
                if (!RunAddOnHardwareForMasterCycles((long)alternateHelperRtsCycles * GenesisScheduler.M68kDivider, shouldAbort))
                {
                    return -1;
                }

                consumed += alternateHelperRtsCycles;
                remainingInstructions = Math.Max(0, remainingInstructions - 1);
                continue;
            }

            bool segaCdMainBiosHelperCopyLoop = segaCdMainBiosHelperPc is 0x00FF_0698u or 0x00FF_1024u;
            bool allowM68kLongCopyFastPath = allowM68kLoopFastPaths || (segaCdMainBiosProgramCopyLoop && segaCdMainBiosHelperCopyLoop);
            if (allowM68kLongCopyFastPath &&
                !HasServiceableM68kInterrupt() &&
                MainCpu.TryFastForwardMoveLongPostIncrementCopyDbfLoop(
                    cycleBudget - consumed,
                    Bus.SegaCd is null ? IsM68kFastLongCopyAddress : IsM68kFastSegaCdProgramCopyAddress,
                    out int longCopyCycles,
                    out int longCopyInstructions))
            {
                int longCopyWaitCycles = Bus.ConsumeM68kWaitCycles();
                MainCpu.AddWaitCycles(longCopyWaitCycles);
                int elapsedCycles = longCopyCycles + longCopyWaitCycles;
                RecordM68kFastPath(elapsedCycles, ref _m68kMoveLongCopyDbfFastPathHits, ref _m68kMoveLongCopyDbfFastPathCycles);
                if (!RunAddOnHardwareForMasterCycles((long)elapsedCycles * GenesisScheduler.M68kDivider, shouldAbort))
                {
                    return -1;
                }

                consumed += elapsedCycles;
                remainingInstructions = Math.Max(0, remainingInstructions - longCopyInstructions);
                continue;
            }

            if (allowM68kLoopFastPaths &&
                !HasServiceableM68kInterrupt() &&
                MainCpu.TryFastForwardMoveWordAbsoluteDbfLoop(
                    cycleBudget - consumed,
                    IsM68kVdpDataPortAddress,
                    out int wordFillCycles,
                    out int wordFillInstructions))
            {
                int wordFillWaitCycles = Bus.ConsumeM68kWaitCycles();
                MainCpu.AddWaitCycles(wordFillWaitCycles);
                int elapsedCycles = wordFillCycles + wordFillWaitCycles;
                RecordM68kFastPath(elapsedCycles, ref _m68kMoveWordVdpFillDbfFastPathHits, ref _m68kMoveWordVdpFillDbfFastPathCycles);
                if (!RunAddOnHardwareForMasterCycles((long)elapsedCycles * GenesisScheduler.M68kDivider, shouldAbort))
                {
                    return -1;
                }

                consumed += elapsedCycles;
                remainingInstructions = Math.Max(0, remainingInstructions - wordFillInstructions);
                continue;
            }

            if (MainCpu.Stopped && !Bus.HasPendingM68kWaitCycles)
            {
                int idleCycles = Math.Min(cycleBudget - consumed, MaxStoppedM68kIdleBatchCycles);
                MainCpu.AddWaitCycles(idleCycles);
                if (!RunAddOnHardwareForMasterCycles((long)idleCycles * GenesisScheduler.M68kDivider, shouldAbort))
                {
                    return -1;
                }

                consumed += idleCycles;

                long idleSliceEndMasterCycle = lineStartMasterCycle + ((lineCycleOffset + consumed) * GenesisScheduler.M68kDivider);
                RunZ80Until(lineStartMasterCycle, idleSliceEndMasterCycle, ref z80MasterCycleCursor, ref z80InterruptPending, z80Observer);
                continue;
            }

            bool wasStopped = MainCpu.Stopped;
            int m68kCycles;
            if (CollectFramePerformance)
            {
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                m68kCycles = MainCpu.Step();
                _framePerformance.M68kTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
                _framePerformance.M68kAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            }
            else
            {
                m68kCycles = MainCpu.Step();
            }

            int waitCycles = Bus.ConsumeM68kWaitCycles();
            MainCpu.AddWaitCycles(waitCycles);
            int elapsedM68kCycles = m68kCycles + waitCycles;
            if (!RunAddOnHardwareForMasterCycles((long)elapsedM68kCycles * GenesisScheduler.M68kDivider, shouldAbort))
            {
                return -1;
            }

            consumed += elapsedM68kCycles;
            if (!wasStopped || !MainCpu.Stopped)
            {
                remainingInstructions--;
            }

            long sliceEndMasterCycle = lineStartMasterCycle + ((lineCycleOffset + consumed) * GenesisScheduler.M68kDivider);
            RunZ80Until(lineStartMasterCycle, sliceEndMasterCycle, ref z80MasterCycleCursor, ref z80InterruptPending, z80Observer);
        }

        Bus.CurrentMasterCycle = lineStartMasterCycle + ((lineCycleOffset + consumed) * GenesisScheduler.M68kDivider);
        Bus.CurrentScanlineMasterCycleOffset = (lineCycleOffset + consumed) * GenesisScheduler.M68kDivider;
        return consumed;
    }

    private void RecordM68kFastPath(int cycles, ref long hits, ref long accumulatedCycles)
    {
        hits++;
        accumulatedCycles += cycles;
        _m68kFastPathHits++;
        _m68kFastPathCycles += cycles;
    }

    private void RecordM68kFastPath(int cycles)
    {
        _m68kFastPathHits++;
        _m68kFastPathCycles += cycles;
    }

    private bool HasServiceableM68kInterrupt()
    {
        if (_pendingM68kInterruptLevels == 0 ||
            IsSegaCdMainBiosBootCriticalSectionActive())
        {
            return false;
        }

        int mask = (MainCpu.SR >> 8) & 0x07;
        for (int level = 7; level > mask; level--)
        {
            if ((_pendingM68kInterruptLevels & (1 << level)) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryFastForwardThirtyTwoXSystemWordPollLoop(int cycleBudget, out int cycles)
    {
        cycles = 0;
        if (Bus.ThirtyTwoX is null ||
            MainCpu.InstructionObserver is not null ||
            cycleBudget < ThirtyTwoXSystemWordPollLoopCycles)
        {
            return false;
        }

        uint comparePc = MainCpu.PC;
        if (comparePc < 6 ||
            Bus.ReadWord(comparePc - 6) != 0x3039 ||
            Bus.ReadWord(comparePc - 4) != 0x00A1 ||
            Bus.ReadWord(comparePc - 2) != 0x5120 ||
            Bus.ReadWord(comparePc) != 0x0C40 ||
            Bus.ReadWord(comparePc + 2) != 0x0001)
        {
            return false;
        }

        ushort branch = Bus.ReadWord(comparePc + 4);
        if ((branch & 0xFF00) != 0x6C00)
        {
            return false;
        }

        uint branchTarget = comparePc + 6 + (uint)((sbyte)(branch & 0x00FF) * 2);
        if (branchTarget != comparePc - 6 ||
            (short)(MainCpu.D[0] & 0xFFFF) < 1)
        {
            return false;
        }

        cycles = cycleBudget - (cycleBudget % ThirtyTwoXSystemWordPollLoopCycles);
        if (cycles <= 0)
        {
            return false;
        }

        return true;
    }

    private static bool IsM68kWorkRamAddress(uint address)
    {
        return (address & 0x00FF_0000u) == 0x00FF_0000u;
    }

    private static bool IsM68kFastByteCopyAddress(uint address)
    {
        address &= 0x00FF_FFFFu;
        return address < 0x00A0_0000u || address >= 0x00E0_0000u;
    }

    private bool IsM68kFastLongCopyAddress(uint address)
    {
        if (!IsM68kFastByteCopyAddress(address))
        {
            return false;
        }

        if (Bus.SegaCd is null)
        {
            return true;
        }

        address &= 0x00FF_FFFFu;
        return !IsInRange(address, SegaCdHardwareProfile.MainProgramRamMirrorLowStart, SegaCdHardwareProfile.MainProgramRamMirrorLowEndInclusive) &&
            !IsInRange(address, SegaCdHardwareProfile.MainProgramRamMirrorHighStart, SegaCdHardwareProfile.MainProgramRamMirrorHighEndInclusive) &&
            !IsInRange(address, SegaCdHardwareProfile.MainWordRamStart, SegaCdHardwareProfile.MainWordRamEndInclusive) &&
            !IsInRange(address, SegaCdHardwareProfile.MainWordRamHighAliasStart, SegaCdHardwareProfile.MainWordRamHighAliasEndInclusive) &&
            !IsInRange(address, SegaCdHardwareProfile.MainWordRamAliasStart, SegaCdHardwareProfile.MainWordRamAliasEndInclusive) &&
            !IsInRange(address, SegaCdHardwareProfile.MainRegisterStart, SegaCdHardwareProfile.MainRegisterEndInclusive);
    }

    private bool IsSegaCdMainBiosProgramCopyLoop()
    {
        if (Bus.SegaCd is null ||
            MainCpu.InstructionObserver is not null)
        {
            return false;
        }

        uint pc = MainCpu.PC & 0x00FF_FFFFu;
        return pc is 0x00FF_0698u or 0x00FF_1024u;
    }

    private bool TryHandleSegaCdMainBootAlternateHelperRts(out int cycles)
    {
        const int RtsCycles = 16;
        cycles = 0;
        if (Bus.SegaCd is null ||
            (MainCpu.PC & 0x00FF_FFFFu) != 0x00FF_069Eu)
        {
            return false;
        }

        M68kCpu.M68kState state = MainCpu.CaptureState();
        uint stack = state.A[7];
        uint returnAddress = Bus.ReadLong(stack);
        uint stackAfterReturn = unchecked(stack + 4);
        if (!IsSegaCdMainBootHelperReturnAddress(returnAddress))
        {
            ushort stackedSr = Bus.ReadWord(stack);
            uint stackedPc = Bus.ReadLong(stack + 2) & 0x00FF_FFFFu;
            uint interruptedReturnAddress = Bus.ReadLong(stack + 6);
            if ((stackedSr & 0x2000) == 0 ||
                stackedPc != 0x00FF_069Eu ||
                !IsSegaCdMainBootHelperReturnAddress(interruptedReturnAddress))
            {
                return false;
            }

            returnAddress = interruptedReturnAddress;
            stackAfterReturn = unchecked(stack + 10);
        }

        uint[] d = (uint[])state.D.Clone();
        uint[] a = (uint[])state.A.Clone();
        a[7] = stackAfterReturn;
        MainCpu.RestoreState(new M68kCpu.M68kState(
            d,
            a,
            returnAddress,
            state.SR,
            state.Stopped,
            state.Cycles,
            state.USP));
        MainCpu.AddWaitCycles(RtsCycles);
        cycles = RtsCycles;
        return true;
    }

    private bool TryHandleSegaCdGenericBootFinalMainProgramHandoff(out int cycles)
    {
        const uint MainProgramEntry = 0x00FF_0000u;
        const int HandoffCycles = 24;
        cycles = 0;
        SegaCdDevice? segaCd = Bus.SegaCd;
        if (segaCd is null ||
            !segaCd.ShouldHandoffGenericBootMainProgram(MainCpu.PC) ||
            Bus.ReadWord(MainProgramEntry) != 0x43FA)
        {
            return false;
        }

        M68kCpu.M68kState state = MainCpu.CaptureState();
        uint[] d = (uint[])state.D.Clone();
        uint[] a = (uint[])state.A.Clone();
        MainCpu.RestoreState(new M68kCpu.M68kState(
            d,
            a,
            MainProgramEntry,
            state.SR,
            state.Stopped,
            state.Cycles,
            state.USP));
        segaCd.StartGenericBootSubProgramIfNeeded();
        MainCpu.AddWaitCycles(HandoffCycles);
        cycles = HandoffCycles;
        return true;
    }

    private static bool IsSegaCdMainBootHelperReturnAddress(uint address)
    {
        return (address & 1) == 0 &&
            (address & 0x00FF_0000u) == 0x00FF_0000u;
    }

    private bool IsSegaCdMainBiosBootCriticalSectionActive()
    {
        if (Bus.SegaCd is null)
        {
            return false;
        }

        uint pc = MainCpu.PC & 0x00FF_FFFFu;
        return (pc >= SegaCdMainBootAlternateHelperStart && pc < SegaCdMainBootAlternateHelperEndExclusive) ||
            (pc >= SegaCdMainBootHelperStart && pc < SegaCdMainBootHelperEndExclusive) ||
            (pc >= SegaCdMainBootVectorRestoreStart && pc < SegaCdMainBootVectorRestoreEndExclusive) ||
            Bus.SegaCd.ShouldDeferMainInterruptsForGenericBoot(pc);
    }

    private bool IsSegaCdMainBiosGenericReadyPollLoop()
    {
        SegaCdDevice? segaCd = Bus.SegaCd;
        if (segaCd is null ||
            !segaCd.ShouldYieldMainForGenericBootReadyPoll ||
            HasServiceableM68kInterrupt())
        {
            return false;
        }

        uint pc = MainCpu.PC & 0x00FF_FFFFu;
        if (pc is not (SegaCdMainBootGenericReadyPollStart or SegaCdMainBootGenericReadyPollBranch))
        {
            return false;
        }

        return Bus.ReadWord(SegaCdMainBootGenericReadyPollStart) == 0x0839 &&
            Bus.ReadWord(SegaCdMainBootGenericReadyPollStart + 2) == 0x0007 &&
            Bus.ReadWord(SegaCdMainBootGenericReadyPollStart + 4) == 0x00A1 &&
            Bus.ReadWord(SegaCdMainBootGenericReadyPollStart + 6) == 0x200F &&
            Bus.ReadWord(SegaCdMainBootGenericReadyPollStart + 8) == 0x6700 &&
            Bus.ReadWord(SegaCdMainBootGenericReadyPollStart + 10) == 0xFFF6;
    }

    private bool IsSegaCdMainBiosGenericFlag7PulseClear()
    {
        SegaCdDevice? segaCd = Bus.SegaCd;
        if (segaCd is null || HasServiceableM68kInterrupt())
        {
            return false;
        }

        uint pc = MainCpu.PC & 0x00FF_FFFFu;
        if (pc != SegaCdMainBootGenericFlag7Clear)
        {
            return false;
        }

        if (Bus.ReadWord(pc) != 0x08B9 ||
            Bus.ReadWord(pc + 2) != 0x0007 ||
            Bus.ReadWord(pc + 4) != 0x00A1 ||
            Bus.ReadWord(pc + 6) != 0x200E)
        {
            return false;
        }

        return segaCd.TryConsumeGenericBootMainFlag7PulseYield();
    }

    private bool IsSegaCdMainGenericLowWaitLoop()
    {
        SegaCdDevice? segaCd = Bus.SegaCd;
        if (segaCd is null ||
            !segaCd.ShouldYieldMainForGenericBootLowWaitLoop ||
            HasServiceableM68kInterrupt())
        {
            return false;
        }

        uint pc = MainCpu.PC & 0x00FF_FFFFu;
        return pc == SegaCdMainBootGenericLowWaitLoop &&
            Bus.ReadWord(pc) == 0x60FE;
    }

    private bool IsSegaCdSonicCdIpxFileBufferClearLoop()
    {
        if (Bus.SegaCd is null)
        {
            return false;
        }

        uint pc = MainCpu.PC & 0x00FF_FFFFu;
        return pc is >= 0x00FF_0B0Cu and < 0x00FF_0B14u;
    }

    private bool IsSegaCdSonicCdIpxWordRamWaitLoop()
    {
        if (Bus.SegaCd is null)
        {
            return false;
        }

        uint pc = MainCpu.PC & 0x00FF_FFFFu;
        return pc is 0x00FF_0D10u or 0x00FF_0D18u;
    }

    private void ClearSegaCdSonicCdIpxVSyncWaitIfNeeded()
    {
        if (Bus.SegaCd is null)
        {
            return;
        }

        uint pc = MainCpu.PC & 0x00FF_FFFFu;
        if (pc is not (0x00FF_0D6Au or 0x00FF_0D72u))
        {
            return;
        }

        const uint SonicCdIpxVSyncFlag = 0x00FF_0F00u;
        if (Bus.ReadByte(SonicCdIpxVSyncFlag) != 0)
        {
            Bus.WriteByte(SonicCdIpxVSyncFlag, 0);
        }
    }

    private void ClearSegaCdGenericBootMainProgramWaitFlagIfNeeded()
    {
        SegaCdDevice? segaCd = Bus.SegaCd;
        if (segaCd is null)
        {
            return;
        }

        bool shouldClearWaitFlag =
            segaCd.IsGenericBootMainProgramHandoffActive ||
            segaCd.ShouldClearGenericBootTransferredMainProgramWaitFlag ||
            segaCd.ShouldClearGenericBootCdcServiceMainProgramWaitFlag;
        if (!shouldClearWaitFlag)
        {
            return;
        }

        uint pc = MainCpu.PC & 0x00FF_FFFFu;
        if (pc is not (0x0000_0A1Au or 0x0000_0A1Eu) ||
            Bus.ReadWord(0x00FF_0000u) != 0x43FA ||
            Bus.ReadWord(0x0000_0A1Au) != 0x4A38 ||
            Bus.ReadWord(0x0000_0A1Cu) != 0xFE26 ||
            Bus.ReadWord(0x0000_0A1Eu) != 0x66FA ||
            Bus.ReadByte(0x00FF_FE26u) == 0)
        {
            return;
        }

        Bus.WriteByte(0x00FF_FE26u, 0);
    }

    private static bool IsM68kFastSegaCdProgramCopyAddress(uint address)
    {
        address &= 0x00FF_FFFFu;
        return address < 0x00A0_0000u ||
            address >= 0x00E0_0000u ||
            IsInRange(address, SegaCdHardwareProfile.MainProgramRamMirrorHighStart, SegaCdHardwareProfile.MainProgramRamMirrorHighEndInclusive) ||
            IsInRange(address, SegaCdHardwareProfile.MainWordRamHighAliasStart, SegaCdHardwareProfile.MainWordRamHighAliasEndInclusive) ||
            IsInRange(address, SegaCdHardwareProfile.MainWordRamAliasStart, SegaCdHardwareProfile.MainWordRamAliasEndInclusive);
    }

    private static bool IsInRange(uint address, uint start, uint endInclusive)
    {
        return address >= start && address <= endInclusive;
    }

    private static bool IsM68kVdpDataPortAddress(uint address)
    {
        return (address & 0x00FF_FFFCu) == 0x00C0_0000u;
    }

    private void RunZ80Until(long lineStartMasterCycle, long sliceEndMasterCycle, ref long z80MasterCycleCursor, ref bool z80InterruptPending, Action<Z80InstructionTrace>? z80Observer)
    {
        if (z80MasterCycleCursor < lineStartMasterCycle)
        {
            z80MasterCycleCursor = lineStartMasterCycle;
        }

        while (z80MasterCycleCursor < sliceEndMasterCycle)
        {
            Bus.CurrentMasterCycle = z80MasterCycleCursor;
            Bus.CurrentScanlineMasterCycleOffset = (int)Math.Clamp(z80MasterCycleCursor - lineStartMasterCycle, 0, Scheduler.MasterCyclesPerScanline - 1);
            Z80.SetLines(Bus.Z80ResetAsserted, Bus.Z80BusGranted);
            ushort z80Pc = z80Observer is null ? (ushort)0 : Z80.PC;
            Bus.CurrentZ80Pc = Z80.PC;
            byte z80Opcode = z80Observer is null ? (byte)0 : Bus.ReadZ80Byte(z80Pc);
            int stepped;
            if (CollectFramePerformance)
            {
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                stepped = Z80.StepInstruction(Bus, z80InterruptPending);
                _framePerformance.Z80Ticks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
                _framePerformance.Z80AllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            }
            else
            {
                stepped = Z80.StepInstruction(Bus, z80InterruptPending);
            }

            if (Z80.LastStepAcceptedInterrupt)
            {
                z80InterruptPending = false;
            }

            z80Observer?.Invoke(new Z80InstructionTrace(Bus.CurrentMasterCycle, z80Pc, z80Opcode, stepped, Z80.PC, Z80.A, Z80.B, Z80.C, Z80.D, Z80.E, Z80.H, Z80.L, Z80.IX, Z80.IY, Z80.BusRequested, Z80.ResetAsserted));
            if (stepped <= 0)
            {
                z80MasterCycleCursor = sliceEndMasterCycle;
                break;
            }

            z80MasterCycleCursor += stepped * GenesisScheduler.Z80Divider;
        }
    }

    private bool RunAddOnHardwareForMasterCycles(long masterCycles, Func<bool>? shouldAbort = null)
    {
        return RunThirtyTwoXForMasterCycles(masterCycles, shouldAbort) &&
            RunSegaCdSubCpuForMasterCycles(masterCycles, shouldAbort);
    }

    private bool RunThirtyTwoXForMasterCycles(long masterCycles, Func<bool>? shouldAbort = null)
    {
        if (Bus.ThirtyTwoX is null || masterCycles <= 0)
        {
            return true;
        }

        double sh2Clock = IsPal ? ThirtyTwoXHardwareProfile.PalSh2ClockHz : ThirtyTwoXHardwareProfile.NtscSh2ClockHz;
        _thirtyTwoXInstructionCarry += masterCycles * (sh2Clock / Scheduler.MasterClock);
        int cyclesPerCpu = (int)_thirtyTwoXInstructionCarry;
        if (cyclesPerCpu < ThirtyTwoXMinimumCycleBatch)
        {
            return true;
        }

        _thirtyTwoXInstructionCarry -= cyclesPerCpu;
        _thirtyTwoXScheduledInstructionRequests += cyclesPerCpu;
        Bus.ThirtyTwoX.SetCurrentMasterCycle(Bus.CurrentMasterCycle);
        int executed = Bus.ThirtyTwoX.RunSh2Cycles(cyclesPerCpu, shouldAbort);
        if (executed < 0)
        {
            return false;
        }

        _thirtyTwoXExecutedInstructionSteps += executed;
        return true;
    }

    private bool TryHandleThirtyTwoXSdkLineF(uint opcodeAddress, ushort opcode)
    {
        bool inCartridgeBootWindow = opcodeAddress is >= ThirtyTwoXHardwareProfile.M68kCartridgeFixedStart and < 0x00A0_0000;
        bool inCopiedBootWorkspace = opcodeAddress is >= 0x00FF_0000 and < 0x00FF_C020;
        bool isSdkOpcode = inCopiedBootWorkspace
            ? opcode >= 0xFF00
            : opcode is >= 0xFF00 and <= 0xFF21;

        if (Bus.ThirtyTwoX is null ||
            (!inCartridgeBootWindow && !inCopiedBootWorkspace) ||
            !isSdkOpcode ||
            Bus.ReadWord(0x00FF_BFFC) != 0x4E4D)
        {
            return false;
        }

        if (opcode == 0xFF14)
        {
            Bus.ThirtyTwoX.RunSh2Cycles(256);

            if (Bus.ThirtyTwoX.MasterSh2.PC is >= 0x0600_1422 and <= 0x0600_142C ||
                Bus.ThirtyTwoX.MasterSh2.PC is >= 0x0600_1740 and <= 0x0600_174A)
            {
                Bus.ThirtyTwoX.WriteSystemRegisterWord(ThirtyTwoXHardwareProfile.InterruptControlOffset, 0x0001);
                Bus.ThirtyTwoX.RunSh2Cycles(256);
            }
        }

        return true;
    }

    private bool RunSegaCdSubCpuForMasterCycles(long masterCycles, Func<bool>? shouldAbort = null)
    {
        SegaCdDevice? segaCd = Bus.SegaCd;
        if (segaCd is null || masterCycles <= 0 || !segaCd.SubCpuRunnable)
        {
            return true;
        }

        _segaCdSubCpuCycleCarry += masterCycles * (SegaCdHardwareProfile.SubCpuClockHz / (double)Scheduler.MasterClock);
        int cycles = (int)_segaCdSubCpuCycleCarry;
        if (cycles < SegaCdMinimumCycleBatch)
        {
            return true;
        }

        _segaCdSubCpuCycleCarry -= cycles;
        _segaCdSubCpuScheduledCycles += cycles;
        int executed = segaCd.RunSubCpuCycles(cycles, shouldAbort);
        if (executed < 0)
        {
            return false;
        }

        _segaCdSubCpuExecutedCycles += executed;
        return true;
    }

    private bool TryHandleThirtyTwoXSdkTrap(uint opcodeAddress, ushort opcode)
    {
        if (Bus.ThirtyTwoX is null ||
            opcodeAddress is < 0x00FF_0000 or >= 0x00FF_1000 ||
            Bus.ReadWord(0x00FF_BFFC) != 0x4E4D)
        {
            return false;
        }

        if (opcode == 0x4E4F)
        {
            Bus.ThirtyTwoX.RunSh2Cycles(256);
            return true;
        }

        if (opcode != 0x4E40 ||
            (MainCpu.D[0] & 0xFF) < 0x81)
        {
            return false;
        }

        Bus.ThirtyTwoX.RunSh2Cycles(256);
        return true;
    }

    private void QueueM68kInterrupt(int level)
    {
        if (level is <= 0 or > 7)
        {
            return;
        }

        _pendingM68kInterruptLevels |= (byte)(1 << level);
    }

    private void ServicePendingM68kInterrupts()
    {
        if ((_pendingM68kInterruptLevels & M68kVBlankInterruptBits) != 0
            && (!VBlankInterruptEnabled() || !Vdp.VInterruptLineActive || VBlankVectorTargetsThirtyTwoXBootErrorStub()))
        {
            _pendingM68kInterruptLevels &= unchecked((byte)~M68kVBlankInterruptBits);
        }

        if (_pendingM68kInterruptLevels != 0 &&
            IsSegaCdMainBiosBootCriticalSectionActive())
        {
            return;
        }

        int mask = (MainCpu.SR >> 8) & 0x07;
        for (int level = 7; level > mask; level--)
        {
            byte bit = (byte)(1 << level);
            if ((_pendingM68kInterruptLevels & bit) == 0)
            {
                continue;
            }

            _pendingM68kInterruptLevels &= (byte)~bit;
            if (MainCpu.RequestInterrupt(level))
            {
                Vdp.AcknowledgeM68kInterrupt(level);
            }
            return;
        }
    }

    public short[] RenderAudioSamples(int samples)
    {
        short[] stereo = RenderStereoAudioSamples(samples);
        short[] mono = new short[samples];
        for (int i = 0; i < samples; i++)
        {
            mono[i] = (short)Math.Clamp((stereo[i * 2] + stereo[(i * 2) + 1]) / 2, short.MinValue, short.MaxValue);
        }

        return mono;
    }

    public short[] RenderStereoAudioSamples(int samples, long[]? ymChannelEnergy = null)
    {
        short[] mixed = new short[samples * AudioConstants.StereoChannels];
        RenderStereoAudioSamplesInto(mixed, samples, ymChannelEnergy);
        return mixed;
    }

    public void RenderStereoAudioSamplesInto(Span<short> destination, int samples, long[]? ymChannelEnergy = null)
    {
        if (destination.Length < samples * AudioConstants.StereoChannels)
        {
            throw new ArgumentException("Destination buffer is too small.", nameof(destination));
        }

        EnsureAudioMixBuffers(samples);
        Span<short> psg = _psgMixBuffer.AsSpan(0, samples);
        Span<short> ym = _ymMixBuffer.AsSpan(0, samples * AudioConstants.StereoChannels);
        Span<short> pwm = _thirtyTwoXPwmMixBuffer.AsSpan(0, samples * AudioConstants.StereoChannels);
        Span<short> cdda = _segaCdCddaMixBuffer.AsSpan(0, samples * AudioConstants.StereoChannels);
        Span<short> segaCdPcm = _segaCdPcmMixBuffer.AsSpan(0, samples * AudioConstants.StereoChannels);
        Psg.RenderMonoSamplesInto(psg, samples);
        Ym2612.RenderStereoSamplesInto(ym, samples, channelEnergy: ymChannelEnergy);
        pwm.Clear();
        Bus.ThirtyTwoX?.RenderPwmStereoSamplesInto(pwm, samples);
        cdda.Clear();
        Bus.SegaCd?.RenderCddaStereoSamplesInto(cdda, samples);
        segaCdPcm.Clear();
        Bus.SegaCd?.RenderPcmStereoSamplesInto(segaCdPcm, samples, sampleRate: AudioConstants.DefaultSampleRate);
        for (int i = 0; i < samples; i++)
        {
            _psgFilter += (psg[i] - _psgFilter) * PsgFilterAlpha;
            double psgSample = _psgFilter * AudioConstants.PsgMixLevel;
            double left = psgSample + (ym[i * 2] * AudioConstants.YmMixLevel) + pwm[i * 2] + (cdda[i * 2] * AudioConstants.SegaCdCddaMixLevel) + (segaCdPcm[i * 2] * AudioConstants.SegaCdPcmMixLevel);
            double right = psgSample + (ym[(i * 2) + 1] * AudioConstants.YmMixLevel) + pwm[(i * 2) + 1] + (cdda[(i * 2) + 1] * AudioConstants.SegaCdCddaMixLevel) + (segaCdPcm[(i * 2) + 1] * AudioConstants.SegaCdPcmMixLevel);
            _audioBassFilterLeft += (left - _audioBassFilterLeft) * BassShelfAlpha;
            _audioBassFilterRight += (right - _audioBassFilterRight) * BassShelfAlpha;
            left += _audioBassFilterLeft * AudioConstants.BassShelfGain;
            right += _audioBassFilterRight * AudioConstants.BassShelfGain;
            _audioFilterLeft += (left - _audioFilterLeft) * OutputFilterAlpha;
            _audioFilterRight += (right - _audioFilterRight) * OutputFilterAlpha;
            double fade = 1.0;
            if (_audioFadeInSamplesRemaining > 0)
            {
                fade = 1.0 - (_audioFadeInSamplesRemaining / (double)ResetAudioFadeInSamples);
                _audioFadeInSamplesRemaining--;
            }

            destination[i * 2] = AudioConstants.LimitOutputSample(_audioFilterLeft * AudioConstants.MasterMixLevel * fade);
            destination[(i * 2) + 1] = AudioConstants.LimitOutputSample(_audioFilterRight * AudioConstants.MasterMixLevel * fade);
        }
    }

    public short[] RenderFrameStereoAudioSamples(int sampleRate = AudioConstants.DefaultSampleRate, long[]? ymChannelEnergy = null)
    {
        int samples = ConsumeFrameAudioSampleCount(sampleRate);
        return RenderStereoAudioSamples(samples, ymChannelEnergy);
    }

    public int RenderFrameStereoAudioSamplesInto(Span<short> destination, int sampleRate = AudioConstants.DefaultSampleRate, long[]? ymChannelEnergy = null)
    {
        int samples = ConsumeFrameAudioSampleCount(sampleRate);
        RenderStereoAudioSamplesInto(destination, samples, ymChannelEnergy);
        return samples * AudioConstants.StereoChannels;
    }

    public int RenderFrameAudioStemSamplesInto(Span<short> ymStems, Span<short> psgStems, int sampleRate = AudioConstants.DefaultSampleRate, long[]? ymChannelEnergy = null)
    {
        int samples = ConsumeFrameAudioSampleCount(sampleRate);
        if (ymStems.Length < samples * 6 * AudioConstants.StereoChannels)
        {
            throw new ArgumentException("YM stem buffer is too small.", nameof(ymStems));
        }

        if (psgStems.Length < samples * 4)
        {
            throw new ArgumentException("PSG stem buffer is too small.", nameof(psgStems));
        }

        Psg.RenderMonoChannelStemsInto(psgStems, samples, sampleRate);
        Ym2612.RenderStereoChannelStemsInto(ymStems, samples, sampleRate, channelEnergy: ymChannelEnergy);
        return samples;
    }

    public byte[] RenderFrameRgb()
    {
        byte[] framebuffer = new byte[Vdp.ScreenWidth * Vdp.ScreenHeight * 3];
        RenderFrameRgbInto(framebuffer);
        return framebuffer;
    }

    public void RenderFrameRgbInto(byte[] framebuffer)
    {
        Vdp.RenderFrameRgbInto(framebuffer);
        Bus.ThirtyTwoX?.CompositeFrameRgbInto(framebuffer, Vdp.LastFrameOpaquePixels);
    }

    public void RenderFrameBgrInto(byte[] framebuffer)
    {
        Vdp.RenderFrameBgrInto(framebuffer);
        Bus.ThirtyTwoX?.CompositeFrameBgrInto(framebuffer, Vdp.LastFrameOpaquePixels);
    }

    private int ConsumeFrameAudioSampleCount(int sampleRate)
    {
        double exactSamples = (sampleRate / Scheduler.FrameRate) + _audioSampleCarry;
        int samples = Math.Max(1, (int)Math.Floor(exactSamples));
        _audioSampleCarry = exactSamples - samples;
        return samples;
    }

    private void EnsureAudioMixBuffers(int samples)
    {
        if (_psgMixBuffer.Length < samples)
        {
            _psgMixBuffer = new short[samples];
        }

        int stereoSamples = samples * AudioConstants.StereoChannels;
        if (_ymMixBuffer.Length < stereoSamples)
        {
            _ymMixBuffer = new short[stereoSamples];
        }

        if (_thirtyTwoXPwmMixBuffer.Length < stereoSamples)
        {
            _thirtyTwoXPwmMixBuffer = new short[stereoSamples];
        }

        if (_segaCdCddaMixBuffer.Length < stereoSamples)
        {
            _segaCdCddaMixBuffer = new short[stereoSamples];
        }

        if (_segaCdPcmMixBuffer.Length < stereoSamples)
        {
            _segaCdPcmMixBuffer = new short[stereoSamples];
        }
    }

    public MegaDriveState CaptureState()
    {
        return new MegaDriveState(
            Frames,
            MainCpu.CaptureState(),
            Z80.CaptureState(),
            Vdp.CaptureState(),
            Bus.CaptureState(),
            Psg.CaptureState(),
            Ym2612.CaptureState(),
            Scheduler.CaptureState(),
            _pendingM68kInterruptLevels,
            _z80MasterCycleCursor,
            _psgFilter,
            _audioBassFilterLeft,
            _audioBassFilterRight,
            _audioFilterLeft,
            _audioFilterRight,
            _audioFadeInSamplesRemaining,
            _thirtyTwoXInstructionCarry,
            _segaCdSubCpuCycleCarry);
    }

    public void RestoreState(MegaDriveState state)
    {
        Frames = state.Frames;
        MainCpu.RestoreState(state.MainCpu);
        Z80.RestoreState(state.Z80);
        Vdp.RestoreState(state.Vdp);
        Bus.RestoreState(state.Bus);
        Psg.RestoreState(state.Psg);
        Ym2612.RestoreState(state.Ym2612);
        Scheduler.RestoreState(state.Scheduler);
        Bus.CurrentMasterCycle = Scheduler.MasterCycles;
        Bus.AnchorSvpTiming(Scheduler.MasterCycles);
        _pendingM68kInterruptLevels = state.PendingM68kInterruptLevels;
        _z80MasterCycleCursor = Math.Max(state.Z80MasterCycleCursor, Scheduler.MasterCycles);
        _psgFilter = state.PsgFilter;
        _audioBassFilterLeft = state.AudioBassFilterLeft;
        _audioBassFilterRight = state.AudioBassFilterRight;
        _audioFilterLeft = state.AudioFilterLeft;
        _audioFilterRight = state.AudioFilterRight;
        _audioFadeInSamplesRemaining = state.AudioFadeInSamplesRemaining;
        _thirtyTwoXInstructionCarry = state.ThirtyTwoXInstructionCarry;
        _segaCdSubCpuCycleCarry = state.SegaCdSubCpuCycleCarry;
    }

    private static double LowPassAlpha(double cutoffHz, int sampleRate)
    {
        double normalized = -2.0 * Math.PI * cutoffHz / Math.Max(1, sampleRate);
        return Math.Clamp(1.0 - Math.Exp(normalized), 0.0, 1.0);
    }

    private bool VBlankInterruptEnabled()
    {
        return (Vdp.Registers[1] & 0x20) != 0;
    }

    private int GetM68kVBlankInterruptLevel()
    {
        return 6;
    }

    private bool HBlankInterruptEnabled()
    {
        return (Vdp.Registers[0] & 0x10) != 0;
    }

    private bool VBlankVectorTargetsThirtyTwoXBootErrorStub()
    {
        if (Bus.ThirtyTwoX is null)
        {
            return false;
        }

        uint handler = Bus.ReadLong(30 * 4);
        return Bus.ReadWord(handler) == 0x4EF9 && Bus.ReadLong(handler + 2) == 0x00FF_BFFC;
    }

    public sealed record MegaDriveState(
        long Frames,
        M68kCpu.M68kState MainCpu,
        Z80Core.Z80State Z80,
        Vdp.VdpState Vdp,
        GenesisBus.BusState Bus,
        Psg.PsgState Psg,
        Ym2612.Ym2612State Ym2612,
        GenesisScheduler.SchedulerState Scheduler,
        byte PendingM68kInterruptLevels,
        long Z80MasterCycleCursor,
        double PsgFilter,
        double AudioBassFilterLeft,
        double AudioBassFilterRight,
        double AudioFilterLeft,
        double AudioFilterRight,
        int AudioFadeInSamplesRemaining,
        double ThirtyTwoXInstructionCarry,
        double SegaCdSubCpuCycleCarry);

    public readonly record struct Z80InstructionTrace(long MasterCycle, ushort Pc, byte Opcode, int Cycles, ushort NextPc, byte A, byte B, byte C, byte D, byte E, byte H, byte L, ushort IX, ushort IY, bool BusRequested, bool ResetAsserted);
    public readonly record struct FramePerformanceCounters(long VdpTicks, long M68kTicks, long Z80Ticks, long YmTimerTicks, long VdpAllocatedBytes, long M68kAllocatedBytes, long Z80AllocatedBytes, long YmTimerAllocatedBytes);

    private struct FramePerformanceAccumulator
    {
        public long VdpTicks;
        public long M68kTicks;
        public long Z80Ticks;
        public long YmTimerTicks;
        public long VdpAllocatedBytes;
        public long M68kAllocatedBytes;
        public long Z80AllocatedBytes;
        public long YmTimerAllocatedBytes;

        public readonly FramePerformanceCounters ToCounters()
        {
            return new FramePerformanceCounters(VdpTicks, M68kTicks, Z80Ticks, YmTimerTicks, VdpAllocatedBytes, M68kAllocatedBytes, Z80AllocatedBytes, YmTimerAllocatedBytes);
        }
    }
}
