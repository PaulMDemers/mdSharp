using MdSharp.Core.Audio;
using MdSharp.Core.Bus;
using MdSharp.Core.Cartridge;
using MdSharp.Core.Cpu.M68k;
using MdSharp.Core.Cpu.Z80;
using MdSharp.Core.ThirtyTwoX;
using MdSharp.Core.Timing;
using MdSharp.Core.Video;

namespace MdSharp.Core;

public sealed class MegaDrive
{
    private const int ThirtyTwoXMinimumCycleBatch = 64;
    private const int ResetAudioFadeInSamples = 512;
    private const int MaxStoppedM68kIdleBatchCycles = 64;
    private static readonly double PsgFilterAlpha = LowPassAlpha(AudioConstants.PsgLowPassCutoffHz, AudioConstants.DefaultSampleRate);
    private static readonly double BassShelfAlpha = LowPassAlpha(AudioConstants.BassShelfCutoffHz, AudioConstants.DefaultSampleRate);
    private static readonly double OutputFilterAlpha = LowPassAlpha(AudioConstants.OutputLowPassCutoffHz, AudioConstants.DefaultSampleRate);

    public MegaDrive(CartridgeImage cartridge, bool pal = false, ReadOnlyMemory<byte>? thirtyTwoXM68kBios = null, ReadOnlyMemory<byte>? thirtyTwoXMasterSh2Bios = null, ReadOnlyMemory<byte>? thirtyTwoXSlaveSh2Bios = null, bool thirtyTwoXUseRealSh2BiosBoot = false)
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
            thirtyTwoXUseRealSh2BiosBoot: thirtyTwoXUseRealSh2BiosBoot);
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
    private FramePerformanceAccumulator _framePerformance;
    private long _z80MasterCycleCursor;
    private double _thirtyTwoXInstructionCarry;
    private long _thirtyTwoXScheduledInstructionRequests;
    private long _thirtyTwoXExecutedInstructionSteps;

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
        _thirtyTwoXScheduledInstructionRequests = 0;
        _thirtyTwoXExecutedInstructionSteps = 0;
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

        for (int line = 0; line < scanlines; line++)
        {
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
                if (VBlankInterruptEnabled())
                {
                    QueueM68kInterrupt(GetM68kVBlankInterruptLevel());
                }
            }

            int activeLineCycles = Scheduler.ActiveDisplayM68kCycles;
            int hblankLineCycles = Math.Max(1, Scheduler.M68kCyclesPerScanline - activeLineCycles);
            int activeDmaDebt = Vdp.ConsumeDmaCycleDebt(activeLineCycles);
            RunThirtyTwoXForMasterCycles((long)activeDmaDebt * GenesisScheduler.M68kDivider);
            int activeBudget = Math.Max(0, activeLineCycles - activeDmaDebt);
            Bus.ThirtyTwoX?.SetHBlank(false);
            int consumed = RunCpuSlice(activeBudget, 0, ref remainingInstructions, ref _z80MasterCycleCursor, ref z80InterruptPending);

            Vdp.SetHBlank(true);
            Bus.ThirtyTwoX?.SetHBlank(true);
            int hblankDmaDebt = Vdp.ConsumeDmaCycleDebt(hblankLineCycles);
            RunThirtyTwoXForMasterCycles((long)hblankDmaDebt * GenesisScheduler.M68kDivider);
            int hblankBudget = Math.Max(0, hblankLineCycles - hblankDmaDebt);
            consumed += RunCpuSlice(hblankBudget, activeLineCycles, ref remainingInstructions, ref _z80MasterCycleCursor, ref z80InterruptPending);
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
    }

    private int RunCpuSlice(int cycleBudget, int lineCycleOffset, ref int remainingInstructions, ref long z80MasterCycleCursor, ref bool z80InterruptPending)
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
            RunThirtyTwoXForMasterCycles((long)cycleBudget * GenesisScheduler.M68kDivider);
            long exhaustedSliceEndMasterCycle = lineStartMasterCycle + ((lineCycleOffset + cycleBudget) * GenesisScheduler.M68kDivider);
            RunZ80Until(lineStartMasterCycle, exhaustedSliceEndMasterCycle, ref z80MasterCycleCursor, ref z80InterruptPending, z80Observer);
            Bus.CurrentMasterCycle = exhaustedSliceEndMasterCycle;
            Bus.CurrentScanlineMasterCycleOffset = (lineCycleOffset + cycleBudget) * GenesisScheduler.M68kDivider;
            return cycleBudget;
        }

        while (consumed < cycleBudget && remainingInstructions > 0)
        {
            Bus.CurrentMasterCycle = lineStartMasterCycle + ((lineCycleOffset + consumed) * GenesisScheduler.M68kDivider);
            Bus.CurrentScanlineMasterCycleOffset = (lineCycleOffset + consumed) * GenesisScheduler.M68kDivider;
            ServicePendingM68kInterrupts();

            if (Bus.ThirtyTwoX is null &&
                MainCpu.TryFastForwardMoveBytePostIncrementDbfLoop(cycleBudget - consumed, out int fastCycles, out int fastInstructions))
            {
                RunThirtyTwoXForMasterCycles((long)fastCycles * GenesisScheduler.M68kDivider);
                consumed += fastCycles;
                remainingInstructions = Math.Max(0, remainingInstructions - fastInstructions);
                continue;
            }

            if (MainCpu.Stopped && !Bus.HasPendingM68kWaitCycles)
            {
                int idleCycles = Math.Min(cycleBudget - consumed, MaxStoppedM68kIdleBatchCycles);
                MainCpu.AddWaitCycles(idleCycles);
                RunThirtyTwoXForMasterCycles((long)idleCycles * GenesisScheduler.M68kDivider);
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
            RunThirtyTwoXForMasterCycles((long)elapsedM68kCycles * GenesisScheduler.M68kDivider);
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

    private void RunThirtyTwoXForMasterCycles(long masterCycles)
    {
        if (Bus.ThirtyTwoX is null || masterCycles <= 0)
        {
            return;
        }

        double sh2Clock = IsPal ? ThirtyTwoXHardwareProfile.PalSh2ClockHz : ThirtyTwoXHardwareProfile.NtscSh2ClockHz;
        _thirtyTwoXInstructionCarry += masterCycles * (sh2Clock / Scheduler.MasterClock);
        int cyclesPerCpu = (int)_thirtyTwoXInstructionCarry;
        if (cyclesPerCpu < ThirtyTwoXMinimumCycleBatch)
        {
            return;
        }

        _thirtyTwoXInstructionCarry -= cyclesPerCpu;
        _thirtyTwoXScheduledInstructionRequests += cyclesPerCpu;
        Bus.ThirtyTwoX.SetCurrentMasterCycle(Bus.CurrentMasterCycle);
        _thirtyTwoXExecutedInstructionSteps += Bus.ThirtyTwoX.RunSh2Cycles(cyclesPerCpu);
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
        const byte vBlankInterruptBits = (1 << 5) | (1 << 6);
        if ((_pendingM68kInterruptLevels & vBlankInterruptBits) != 0
            && (!VBlankInterruptEnabled() || !Vdp.VInterruptLineActive || VBlankVectorTargetsThirtyTwoXBootErrorStub()))
        {
            _pendingM68kInterruptLevels &= unchecked((byte)~vBlankInterruptBits);
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
        Psg.RenderMonoSamplesInto(psg, samples);
        Ym2612.RenderStereoSamplesInto(ym, samples, channelEnergy: ymChannelEnergy);
        pwm.Clear();
        Bus.ThirtyTwoX?.RenderPwmStereoSamplesInto(pwm, samples);
        for (int i = 0; i < samples; i++)
        {
            _psgFilter += (psg[i] - _psgFilter) * PsgFilterAlpha;
            double psgSample = _psgFilter * AudioConstants.PsgMixLevel;
            double left = psgSample + (ym[i * 2] * AudioConstants.YmMixLevel) + pwm[i * 2];
            double right = psgSample + (ym[(i * 2) + 1] * AudioConstants.YmMixLevel) + pwm[(i * 2) + 1];
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
            _thirtyTwoXInstructionCarry);
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
        double ThirtyTwoXInstructionCarry);

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
