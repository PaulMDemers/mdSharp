using MdSharp.Core.Audio;
using MdSharp.Core.Bus;
using MdSharp.Core.Cartridge;
using MdSharp.Core.Cpu.M68k;
using MdSharp.Core.Cpu.Sh2;
using MdSharp.Core.Cpu.Z80;
using MdSharp.Core.SegaCd;
using MdSharp.Core.ThirtyTwoX;
using MdSharp.Core.Timing;
using MdSharp.Core.Video;

namespace MdSharp.Core.State;

public static class SaveStateSerializer
{
    private const uint Magic = 0x5353444D; // MDSS
    private const int Version = 86;

    public static void Save(MegaDrive machine, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        using FileStream stream = File.Create(path);
        using BinaryWriter writer = new(stream);
        MegaDrive.MegaDriveState state = machine.CaptureState();

        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(state.Frames);
        WriteCpu(writer, state.MainCpu);
        WriteZ80(writer, state.Z80);
        WriteVdp(writer, state.Vdp);
        WriteBus(writer, state.Bus);
        WritePsg(writer, state.Psg);
        WriteYm(writer, state.Ym2612);
        WriteScheduler(writer, state.Scheduler);
        writer.Write(state.PendingM68kInterruptLevels);
        writer.Write(state.Z80MasterCycleCursor);
        writer.Write(state.PsgFilter);
        writer.Write(state.AudioBassFilterLeft);
        writer.Write(state.AudioBassFilterRight);
        writer.Write(state.AudioFilterLeft);
        writer.Write(state.AudioFilterRight);
        writer.Write(state.AudioFadeInSamplesRemaining);
        writer.Write(state.ThirtyTwoXInstructionCarry);
        writer.Write(state.SegaCdSubCpuCycleCarry);
    }

    public static void Load(MegaDrive machine, string path)
    {
        using FileStream stream = File.OpenRead(path);
        using BinaryReader reader = new(stream);
        uint magic = reader.ReadUInt32();
        int version = reader.ReadInt32();
        if (magic != Magic || version is < 1 or > Version)
        {
            throw new InvalidDataException("Unsupported mdSharp save-state file.");
        }

        MegaDrive.MegaDriveState state = new(
            reader.ReadInt64(),
            ReadCpu(reader),
            ReadZ80(reader, version),
            ReadVdp(reader, version),
            ReadBus(reader, version),
            ReadPsg(reader, version),
            ReadYm(reader, version),
            ReadScheduler(reader),
            version >= 22 ? reader.ReadByte() : (byte)0,
            version >= 30 ? reader.ReadInt64() : 0,
            version >= 23 ? reader.ReadDouble() : 0.0,
            version >= 24 ? reader.ReadDouble() : 0.0,
            version >= 24 ? reader.ReadDouble() : 0.0,
            version >= 17 ? reader.ReadDouble() : 0.0,
            version >= 17 ? reader.ReadDouble() : 0.0,
            version >= 27 ? reader.ReadInt32() : 0,
            version >= 40 ? reader.ReadDouble() : 0.0,
            version >= 69 ? reader.ReadDouble() : 0.0);
        machine.RestoreState(state);
    }

    private static void WriteCpu(BinaryWriter writer, M68kCpu.M68kState state)
    {
        WriteArray(writer, state.D);
        WriteArray(writer, state.A);
        writer.Write(state.PC);
        writer.Write(state.SR);
        writer.Write(state.Stopped);
        writer.Write(state.Cycles);
        writer.Write(state.USP);
    }

    private static M68kCpu.M68kState ReadCpu(BinaryReader reader)
    {
        return new M68kCpu.M68kState(ReadUIntArray(reader), ReadUIntArray(reader), reader.ReadUInt32(), reader.ReadUInt16(), reader.ReadBoolean(), reader.ReadInt64(), reader.ReadUInt32());
    }

    private static void WriteZ80(BinaryWriter writer, Z80Core.Z80State state)
    {
        writer.Write(state.A);
        writer.Write(state.F);
        writer.Write(state.B);
        writer.Write(state.C);
        writer.Write(state.D);
        writer.Write(state.E);
        writer.Write(state.H);
        writer.Write(state.L);
        writer.Write(state.AlternateA);
        writer.Write(state.AlternateF);
        writer.Write(state.AlternateB);
        writer.Write(state.AlternateC);
        writer.Write(state.AlternateD);
        writer.Write(state.AlternateE);
        writer.Write(state.AlternateH);
        writer.Write(state.AlternateL);
        writer.Write(state.I);
        writer.Write(state.R);
        writer.Write(state.IX);
        writer.Write(state.IY);
        writer.Write(state.PC);
        writer.Write(state.SP);
        writer.Write(state.Cycles);
        writer.Write(state.ResetAsserted);
        writer.Write(state.BusRequested);
        writer.Write(state.Halted);
        writer.Write(state.Iff1);
        writer.Write(state.Iff2);
        writer.Write(state.InterruptEnableDelay);
        writer.Write(state.InterruptMode);
    }

    private static Z80Core.Z80State ReadZ80(BinaryReader reader, int version)
    {
        byte a = reader.ReadByte();
        byte f = reader.ReadByte();
        byte b = reader.ReadByte();
        byte c = reader.ReadByte();
        byte d = reader.ReadByte();
        byte e = reader.ReadByte();
        byte h = reader.ReadByte();
        byte l = reader.ReadByte();
        byte alternateA = version >= 8 ? reader.ReadByte() : (byte)0;
        byte alternateF = version >= 8 ? reader.ReadByte() : (byte)0;
        byte alternateB = version >= 8 ? reader.ReadByte() : (byte)0;
        byte alternateC = version >= 8 ? reader.ReadByte() : (byte)0;
        byte alternateD = version >= 8 ? reader.ReadByte() : (byte)0;
        byte alternateE = version >= 8 ? reader.ReadByte() : (byte)0;
        byte alternateH = version >= 8 ? reader.ReadByte() : (byte)0;
        byte alternateL = version >= 8 ? reader.ReadByte() : (byte)0;
        byte i = version >= 16 ? reader.ReadByte() : (byte)0;
        byte r = version >= 16 ? reader.ReadByte() : (byte)0;
        ushort ix = version >= 7 ? reader.ReadUInt16() : (ushort)0;
        ushort iy = version >= 7 ? reader.ReadUInt16() : (ushort)0;
        ushort pc = reader.ReadUInt16();
        ushort sp = reader.ReadUInt16();
        long cycles = reader.ReadInt64();
        bool resetAsserted = reader.ReadBoolean();
        bool busRequested = reader.ReadBoolean();
        bool halted = reader.ReadBoolean();
        bool iff1 = version >= 16 && reader.ReadBoolean();
        bool iff2 = version >= 16 && reader.ReadBoolean();
        int interruptEnableDelay = version >= 16 ? reader.ReadInt32() : 0;
        byte interruptMode = version >= 16 ? reader.ReadByte() : (byte)1;
        return new Z80Core.Z80State(
            a,
            f,
            b,
            c,
            d,
            e,
            h,
            l,
            alternateA,
            alternateF,
            alternateB,
            alternateC,
            alternateD,
            alternateE,
            alternateH,
            alternateL,
            i,
            r,
            ix,
            iy,
            pc,
            sp,
            cycles,
            resetAsserted,
            busRequested,
            halted,
            iff1,
            iff2,
            interruptEnableDelay,
            interruptMode);
    }

    private static void WriteVdp(BinaryWriter writer, Vdp.VdpState state)
    {
        WriteArray(writer, state.Vram);
        WriteArray(writer, state.Cram);
        WriteArray(writer, state.Vsram);
        WriteArray(writer, state.Registers);
        writer.Write(state.Status);
        writer.Write(state.Scanline);
        writer.Write(state.HintCounter);
        writer.Write(state.FifoWords);
        writer.Write(state.OddFrame);
        writer.Write(state.VBlank);
        writer.Write(state.VInterruptPending);
        writer.Write(state.HInterruptPending);
        writer.Write(state.HBlank);
        writer.Write(state.SpriteOverflow);
        writer.Write(state.SpriteCollision);
        writer.Write(state.Address);
        writer.Write(state.Code);
        WriteArray(writer, state.DirectColorSamples);
        writer.Write(state.DmaCycleDebt);
    }

    private static Vdp.VdpState ReadVdp(BinaryReader reader, int version)
    {
        byte[] vram = ReadByteArray(reader);
        ushort[] cram = ReadUShortArray(reader);
        ushort[] vsram = ReadUShortArray(reader);
        byte[] registers = ReadByteArray(reader);
        ushort status = reader.ReadUInt16();
        int scanline = reader.ReadInt32();
        int hintCounter = reader.ReadInt32();
        int fifoWords = reader.ReadInt32();
        bool oddFrame = reader.ReadBoolean();
        bool vBlank = version >= 18 ? reader.ReadBoolean() : (status & 0x0008) != 0;
        bool vInterruptPending = reader.ReadBoolean();
        bool hInterruptPending = reader.ReadBoolean();
        bool hBlank = reader.ReadBoolean();
        bool spriteOverflow = reader.ReadBoolean();
        bool spriteCollision = reader.ReadBoolean();
        uint address = reader.ReadUInt32();
        byte code = reader.ReadByte();
        ushort[] directColorSamples = ReadUShortArray(reader);
        int dmaCycleDebt = reader.ReadInt32();
        return new Vdp.VdpState(
            vram,
            cram,
            vsram,
            registers,
            status,
            scanline,
            hintCounter,
            fifoWords,
            oddFrame,
            vBlank,
            vInterruptPending,
            hInterruptPending,
            hBlank,
            spriteOverflow,
            spriteCollision,
            address,
            code,
            directColorSamples,
            dmaCycleDebt);
    }

    private static void WriteBus(BinaryWriter writer, GenesisBus.BusState state)
    {
        WriteArray(writer, state.WorkRam);
        WriteArray(writer, state.Z80Ram);
        WriteArray(writer, state.Tmss);
        WriteArray(writer, state.IoData);
        WriteArray(writer, state.IoControl);
        writer.Write(state.Z80BusRequested);
        writer.Write(state.Z80ResetAsserted);
        writer.Write(state.Z80BankRegister);
        WriteArray(writer, state.SaveRam);
        WriteArray(writer, state.BankRegisters);
        writer.Write(state.BankSwitchingEnabled);
        writer.Write(state.FallbackSaveRamActive);
        writer.Write(state.SaveRamEnabled);
        writer.Write(state.Z80BusGrantReadyCycle);
        writer.Write(state.PendingM68kWaitCycles);
        writer.Write(state.Svp is not null);
        if (state.Svp is not null)
        {
            WriteSvp(writer, state.Svp);
        }

        writer.Write(state.ThirtyTwoX is not null);
        if (state.ThirtyTwoX is not null)
        {
            WriteThirtyTwoX(writer, state.ThirtyTwoX);
        }

        writer.Write(state.SegaCd is not null);
        if (state.SegaCd is not null)
        {
            WriteSegaCd(writer, state.SegaCd);
        }
    }

    private static GenesisBus.BusState ReadBus(BinaryReader reader, int version)
    {
        byte[] workRam = ReadByteArray(reader);
        byte[] z80Ram = ReadByteArray(reader);
        byte[] tmss = ReadByteArray(reader);
        byte[] ioData = version >= 2 ? ReadByteArray(reader) : [0x40, 0x40, 0x40];
        byte[] ioControl = ReadByteArray(reader);
        bool z80BusRequested = reader.ReadBoolean();
        bool z80ResetAsserted = reader.ReadBoolean();
        int z80BankRegister = version >= 6 ? reader.ReadInt32() : 0;
        byte[] saveRam = ReadByteArray(reader);
        byte[] bankRegisters = ReadByteArray(reader);
        bool bankSwitchingEnabled = reader.ReadBoolean();
        bool fallbackSaveRamActive = version >= 10 && reader.ReadBoolean();
        bool saveRamEnabled = version >= 11 ? reader.ReadBoolean() : true;
        long z80BusGrantReadyCycle = version >= 26 ? reader.ReadInt64() : 0;
        int pendingM68kWaitCycles = version >= 26 ? reader.ReadInt32() : 0;
        SvpDevice.SvpState? svp = version >= 25 && reader.ReadBoolean() ? ReadSvp(reader) : null;
        ThirtyTwoXDevice.ThirtyTwoXState? thirtyTwoX = version >= 31 && reader.ReadBoolean() ? ReadThirtyTwoX(reader, version) : null;
        SegaCdDevice.SegaCdState? segaCd = version >= 70 && reader.ReadBoolean() ? ReadSegaCd(reader, version) : null;
        return new GenesisBus.BusState(workRam, z80Ram, tmss, ioData, ioControl, z80BusRequested, z80ResetAsserted, z80BankRegister, saveRam, bankRegisters, bankSwitchingEnabled, fallbackSaveRamActive, saveRamEnabled, z80BusGrantReadyCycle, pendingM68kWaitCycles, svp, thirtyTwoX, segaCd);
    }

    private static void WriteSegaCd(BinaryWriter writer, SegaCdDevice.SegaCdState state)
    {
        WriteArray(writer, state.ProgramRam);
        WriteArray(writer, state.WordRam);
        WriteArray(writer, state.BackupRam);
        WriteArray(writer, state.PcmRam);
        WriteArray(writer, state.MainRegisters);
        WriteArray(writer, state.MainToSubCommand);
        WriteArray(writer, state.SubToMainStatus);
        writer.Write(state.SubBiosMapped);
        writer.Write(state.SubCpuResetReleased);
        writer.Write(state.SubCpuBusRequested);
        writer.Write(state.CddInterruptCycleCarry);
        writer.Write(state.CdcInterruptCycleCarry);
        writer.Write(state.CddStatusCode);
        writer.Write(state.CddStatusReady);
        writer.Write(state.CddResponseLatched);
        writer.Write(state.CddSeekTicksRemaining);
        WriteArray(writer, state.CdcRegisters);
        WriteArray(writer, state.CdcPacket);
        writer.Write(state.CdcAddress);
        writer.Write(state.CdcPacketOffset);
        writer.Write(state.CdcPacketLength);
        writer.Write(state.CurrentCdcLba);
        writer.Write(state.BootReadStartLba);
        writer.Write(state.BootReadSectorCount);
        writer.Write(state.BootReadStreamActive);
        writer.Write(state.CdcRunning);
        writer.Write(state.CddaLba);
        writer.Write(state.CddaSectorLba);
        writer.Write(state.CddaSectorSampleIndex);
        writer.Write(state.CddaPlaying);
        WriteArray(writer, state.CddaSector);
        WriteSegaCdPcmChannels(writer, state.PcmChannels);
        writer.Write(state.PcmControlChannel);
        writer.Write(state.PcmWriteBank);
        writer.Write(state.PcmEnabled);
        writer.Write(state.PcmRenderCycleCarry);
        writer.Write(state.StickySubFlag6);
        writer.Write(state.SubFlag7BootClearCycles);
        writer.Write(state.BootReadyFlagClearReadsUntilReady);
        writer.Write(state.GenericBootReadyFollowUpFlagPending);
        writer.Write(state.GenericBootReadyEdgeReadPending);
        writer.Write(state.GenericBootMainFlag7PulseYieldPending);
        writer.Write(state.GenericBootMainFlag7SubReadEdgePending);
        writer.Write(state.PendingSubInterruptLevels);
        writer.Write(state.WordRamModeBits);
        writer.Write(state.WordRamOwnedByMain);
        writer.Write(state.SuppressBootStatusUntilMainCommand);
        writer.Write(state.MainCommunicationFlags);
        writer.Write(state.SubCommunicationFlags);
        writer.Write(state.DiscTypeCommPacketPending);
        writer.Write(state.DiscTypeCommPacketReadyAfterClearObserved);
        writer.Write(state.DiscTypeCommPacketClearReadsUntilReady);
        writer.Write(state.DiscTypeCommPacketSyntheticEdgeUsed);
        writer.Write(state.MainBootIpOverrideAllowed);
        writer.Write(state.SyntheticCommand23AckCount);
        WriteCpu(writer, state.SubCpu);
    }

    private static SegaCdDevice.SegaCdState ReadSegaCd(BinaryReader reader, int version)
    {
        byte[] programRam = ReadByteArray(reader);
        byte[] wordRam = ReadByteArray(reader);
        byte[] backupRam = ReadByteArray(reader);
        byte[] pcmRam = ReadByteArray(reader);
        byte[] mainRegisters = ReadByteArray(reader);
        byte[] mainToSubCommand = version >= 76 ? ReadByteArray(reader) : ReadRegisterWindow(mainRegisters, 0x10);
        byte[] subToMainStatus = version >= 76 ? ReadByteArray(reader) : ReadRegisterWindow(mainRegisters, 0x20);
        bool subBiosMapped = reader.ReadBoolean();
        bool subCpuResetReleased = reader.ReadBoolean();
        bool subCpuBusRequested = reader.ReadBoolean();
        double cddInterruptCycleCarry = reader.ReadDouble();
        double cdcInterruptCycleCarry = reader.ReadDouble();
        byte cddStatusCode = reader.ReadByte();
        bool cddStatusReady = reader.ReadBoolean();
        bool cddResponseLatched = reader.ReadBoolean();
        int cddSeekTicksRemaining = reader.ReadInt32();
        byte[] cdcRegisters = ReadByteArray(reader);
        byte[] cdcPacket = ReadByteArray(reader);
        byte cdcAddress = reader.ReadByte();
        int cdcPacketOffset = reader.ReadInt32();
        int cdcPacketLength = reader.ReadInt32();
        int currentCdcLba = reader.ReadInt32();
        int bootReadStartLba = version >= 71 ? reader.ReadInt32() : -1;
        int bootReadSectorCount = version >= 71 ? reader.ReadInt32() : 0;
        bool bootReadStreamActive = version >= 72 && reader.ReadBoolean();

        bool cddaRunning = reader.ReadBoolean();
        int cddaLba = reader.ReadInt32();
        int cddaSectorLba = reader.ReadInt32();
        int cddaSectorSampleIndex = reader.ReadInt32();
        bool cddaPlaying = reader.ReadBoolean();
        byte[] cddaSector = ReadByteArray(reader);
        SegaCdDevice.PcmChannelState[] pcmChannels = ReadSegaCdPcmChannels(reader);
        byte pcmControlChannel = reader.ReadByte();
        ushort pcmWriteBank = reader.ReadUInt16();
        bool pcmEnabled = reader.ReadBoolean();
        double pcmRenderCycleCarry = reader.ReadDouble();
        bool stickySubFlag6 = reader.ReadBoolean();
        int subFlag7BootClearCycles = reader.ReadInt32();
        byte bootReadyFlagClearReadsUntilReady = version >= 80 ? reader.ReadByte() : (byte)0;
        bool genericBootReadyFollowUpFlagPending = version >= 83 && reader.ReadBoolean();
        bool genericBootReadyEdgeReadPending = version >= 84 && reader.ReadBoolean();
        bool genericBootMainFlag7PulseYieldPending = version >= 85 && reader.ReadBoolean();
        bool genericBootMainFlag7SubReadEdgePending = version >= 86 && reader.ReadBoolean();
        byte pendingSubInterruptLevels = reader.ReadByte();
        byte wordRamModeBits = reader.ReadByte();
        bool wordRamOwnedByMain = reader.ReadBoolean();
        bool suppressBootStatusUntilMainCommand = version >= 73 && reader.ReadBoolean();
        byte mainCommunicationFlags;
        byte subCommunicationFlags;
        if (version >= 74)
        {
            mainCommunicationFlags = reader.ReadByte();
            subCommunicationFlags = reader.ReadByte();
        }
        else
        {
            mainCommunicationFlags = mainRegisters.Length > 0x0E ? mainRegisters[0x0E] : (byte)0;
            subCommunicationFlags = mainRegisters.Length > 0x0F ? mainRegisters[0x0F] : (byte)0;
        }

        bool discTypeCommPacketPending = version >= 75 && reader.ReadBoolean();
        bool discTypeCommPacketReadyAfterClearObserved = version >= 77 && reader.ReadBoolean();
        byte discTypeCommPacketClearReadsUntilReady = version >= 79
            ? reader.ReadByte()
            : discTypeCommPacketReadyAfterClearObserved ? (byte)1 : (byte)0;
        bool discTypeCommPacketSyntheticEdgeUsed = version >= 78 && reader.ReadBoolean();
        bool mainBootIpOverrideAllowed = version < 81 || reader.ReadBoolean();
        byte syntheticCommand23AckCount = version >= 82 ? reader.ReadByte() : (byte)0;

        return new SegaCdDevice.SegaCdState(
            programRam,
            wordRam,
            backupRam,
            pcmRam,
            mainRegisters,
            mainToSubCommand,
            subToMainStatus,
            subBiosMapped,
            subCpuResetReleased,
            subCpuBusRequested,
            cddInterruptCycleCarry,
            cdcInterruptCycleCarry,
            cddStatusCode,
            cddStatusReady,
            cddResponseLatched,
            cddSeekTicksRemaining,
            cdcRegisters,
            cdcPacket,
            cdcAddress,
            cdcPacketOffset,
            cdcPacketLength,
            currentCdcLba,
            bootReadStartLba,
            bootReadSectorCount,
            bootReadStreamActive,
            cddaRunning,
            cddaLba,
            cddaSectorLba,
            cddaSectorSampleIndex,
            cddaPlaying,
            cddaSector,
            pcmChannels,
            pcmControlChannel,
            pcmWriteBank,
            pcmEnabled,
            pcmRenderCycleCarry,
            stickySubFlag6,
            subFlag7BootClearCycles,
            bootReadyFlagClearReadsUntilReady,
            genericBootReadyFollowUpFlagPending,
            genericBootReadyEdgeReadPending,
            genericBootMainFlag7PulseYieldPending,
            genericBootMainFlag7SubReadEdgePending,
            pendingSubInterruptLevels,
            wordRamModeBits,
            wordRamOwnedByMain,
            suppressBootStatusUntilMainCommand,
            mainCommunicationFlags,
            subCommunicationFlags,
            discTypeCommPacketPending,
            discTypeCommPacketReadyAfterClearObserved,
            discTypeCommPacketClearReadsUntilReady,
            discTypeCommPacketSyntheticEdgeUsed,
            mainBootIpOverrideAllowed,
            syntheticCommand23AckCount,
            ReadCpu(reader));
    }

    private static void WriteSegaCdPcmChannels(BinaryWriter writer, SegaCdDevice.PcmChannelState[] channels)
    {
        writer.Write(channels.Length);
        foreach (SegaCdDevice.PcmChannelState channel in channels)
        {
            writer.Write(channel.Enabled);
            writer.Write(channel.Envelope);
            writer.Write(channel.Pan);
            writer.Write(channel.Start);
            writer.Write(channel.Address);
            writer.Write(channel.Step);
            writer.Write(channel.LoopStart);
        }
    }

    private static SegaCdDevice.PcmChannelState[] ReadSegaCdPcmChannels(BinaryReader reader)
    {
        int count = Math.Clamp(reader.ReadInt32(), 0, 8);
        SegaCdDevice.PcmChannelState[] channels = new SegaCdDevice.PcmChannelState[count];
        for (int i = 0; i < count; i++)
        {
            channels[i] = new SegaCdDevice.PcmChannelState(
                reader.ReadBoolean(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadByte(),
                reader.ReadUInt32(),
                reader.ReadUInt16(),
                reader.ReadUInt16());
        }

        return channels;
    }

    private static void WriteThirtyTwoX(BinaryWriter writer, ThirtyTwoXDevice.ThirtyTwoXState state)
    {
        WriteArray(writer, state.Sdram);
        WriteArray(writer, state.FrameBuffer0);
        WriteArray(writer, state.FrameBuffer1);
        WriteArray(writer, state.Palette);
        WriteArray(writer, state.SystemRegisters);
        WriteArray(writer, state.M68kCommunicationPendingHostBytes);
        WriteArray(writer, state.M68kCommunicationDeferredSh2ClearBytes);
        WriteArray(writer, state.VdpRegisters);
        WriteArray(writer, state.PwmLeft);
        WriteArray(writer, state.PwmRight);
        WriteArray(writer, state.PwmMono);
        WriteArray(writer, state.PwmLeftHardwareFifo);
        WriteArray(writer, state.PwmRightHardwareFifo);
        WriteArray(writer, state.PwmMonoHardwareFifo);
        writer.Write(state.PwmLeftLevel);
        writer.Write(state.PwmRightLevel);
        writer.Write(state.PwmMonoLevel);
        writer.Write(state.MasterPwmInterruptPending);
        writer.Write(state.SlavePwmInterruptPending);
        writer.Write(state.PwmCycleCounter);
        writer.Write(state.PwmTimerCounter);
        WriteArray(writer, state.DreqFifo);
        WriteArray(writer, state.MasterDmaRegisters);
        WriteArray(writer, state.SlaveDmaRegisters);
        WriteArray(writer, state.MasterPeripheralRegisters);
        WriteArray(writer, state.SlavePeripheralRegisters);
        WriteArray(writer, state.WatchdogCycleCounters);
        WriteArray(writer, state.WatchdogInterruptPending);
        WriteArray(writer, state.WatchdogWriteSelect);
        WriteArray(writer, state.FrtBaseCycles);
        WriteArray(writer, state.FrtBaseCounters);
        WriteArray(writer, state.FrtLastCounters);
        WriteArray(writer, state.FrtOutputCompareB);
        WriteArray(writer, state.MasterCacheDataArray);
        WriteArray(writer, state.SlaveCacheDataArray);
        WriteArray(writer, state.MasterCacheDataValid);
        WriteArray(writer, state.SlaveCacheDataValid);
        WriteArray(writer, state.MasterPrivateWorkRam);
        WriteArray(writer, state.SlavePrivateWorkRam);
        WriteArray(writer, state.MasterCacheTags);
        WriteArray(writer, state.SlaveCacheTags);
        WriteArray(writer, state.MasterCacheLru);
        WriteArray(writer, state.SlaveCacheLru);
        WriteArray(writer, state.MasterDivisionRegisters);
        WriteArray(writer, state.SlaveDivisionRegisters);
        WriteArray(writer, state.DmaRequestSelect);
        writer.Write(state.ActiveDisplayFrameBufferIndex);
        writer.Write(state.AdapterEnabled);
        writer.Write(state.Sh2ResetEnabled);
        writer.Write(state.Sh2ResetReleased);
        writer.Write(state.VdpAccessGrantedToSh2);
        writer.Write(state.VBlank);
        writer.Write(state.HBlank);
        writer.Write(state.CurrentScanline);
        writer.Write(state.FrameBufferSwapPending);
        writer.Write(state.PendingDrawFrameBufferIndex);
        writer.Write(state.RequestedDisplayFrameBufferIndex);
        writer.Write(state.LatchedBitmapMode);
        writer.Write(state.LatchedScreenShiftControl);
        writer.Write(state.LastCompositeUsedFallback);
        writer.Write(state.LastCompositeMode);
        writer.Write(state.MasterInterruptMask);
        writer.Write(state.SlaveInterruptMask);
        writer.Write(state.MasterVerticalInterruptPending);
        writer.Write(state.SlaveVerticalInterruptPending);
        writer.Write(state.MasterVresInterruptPending);
        writer.Write(state.SlaveVresInterruptPending);
        writer.Write(state.MasterHorizontalInterruptPending);
        writer.Write(state.SlaveHorizontalInterruptPending);
        writer.Write(state.HorizontalInterruptPeriod);
        writer.Write(state.HorizontalInterruptCounter);
        writer.Write(state.MasterCommandInterruptPending);
        writer.Write(state.SlaveCommandInterruptPending);
        writer.Write(state.SdkWordStreamTerminatorPendingYieldRelease);
        writer.Write(state.BootRomHandshakePending);
        writer.Write(state.BootRomSignatureRead);
        writer.Write(state.BootRomSignatureReadbackActive);
        writer.Write(state.BootRomLaunchPending);
        writer.Write(state.BootRomPostStartSignaturePending);
        writer.Write(state.BootRomPostStartSignatureHiddenFromSh2);
        writer.Write(state.BootRomPostStartSignatureReadMask);
        writer.Write(state.BootRomPostStartHostClearProtectMask);
        writer.Write(state.BootRomChecksumPublished);
        writer.Write(state.BootRomChecksumHostCleared);
        writer.Write(state.BootRomSixtyEightUpPending);
        writer.Write(state.BootRomSixtyEightUpReadyHiddenFromSh2);
        writer.Write(state.M68kVdpControlMailboxArmed);
        WriteSh2(writer, state.MasterSh2);
        WriteSh2(writer, state.SlaveSh2);
    }

    private static ThirtyTwoXDevice.ThirtyTwoXState ReadThirtyTwoX(BinaryReader reader, int version)
    {
        byte[] sdram = ReadByteArray(reader);
        byte[] frameBuffer0 = ReadByteArray(reader);
        byte[] frameBuffer1 = ReadByteArray(reader);
        byte[] palette = ReadByteArray(reader);
        byte[] systemRegisters = ReadByteArray(reader);
        bool[] m68kCommunicationPendingHostBytes = version >= 58 ? ReadBoolArray(reader) : new bool[16];
        bool[] m68kCommunicationDeferredSh2ClearBytes = version >= 58 ? ReadBoolArray(reader) : new bool[16];
        byte[] vdpRegisters = ReadByteArray(reader);
        ushort[] pwmLeft = ReadUShortArray(reader);
        ushort[] pwmRight = ReadUShortArray(reader);
        ushort[] pwmMono = ReadUShortArray(reader);
        ushort[] pwmLeftHardwareFifo = [];
        ushort[] pwmRightHardwareFifo = [];
        ushort[] pwmMonoHardwareFifo = [];
        double pwmLeftLevel = 0.0;
        double pwmRightLevel = 0.0;
        double pwmMonoLevel = 0.0;
        bool masterPwmInterruptPending = false;
        bool slavePwmInterruptPending = false;
        int pwmCycleCounter = 0;
        int pwmTimerCounter = 0;
        if (version >= 49)
        {
            pwmLeftHardwareFifo = ReadUShortArray(reader);
            pwmRightHardwareFifo = ReadUShortArray(reader);
            pwmMonoHardwareFifo = ReadUShortArray(reader);
            pwmLeftLevel = reader.ReadDouble();
            pwmRightLevel = reader.ReadDouble();
            pwmMonoLevel = reader.ReadDouble();
            masterPwmInterruptPending = reader.ReadBoolean();
            slavePwmInterruptPending = reader.ReadBoolean();
            pwmCycleCounter = reader.ReadInt32();
            pwmTimerCounter = reader.ReadInt32();
        }

        ushort[] dreqFifo = version >= 39 ? ReadUShortArray(reader) : [];
        byte[] masterDmaRegisters = version >= 42 ? ReadByteArray(reader) : [];
        byte[] slaveDmaRegisters = version >= 42 ? ReadByteArray(reader) : [];
        byte[] masterPeripheralRegisters = version >= 43 ? ReadByteArray(reader) : [];
        byte[] slavePeripheralRegisters = version >= 43 ? ReadByteArray(reader) : [];
        int[] watchdogCycleCounters = version >= 53 ? ReadIntArray(reader) : new int[2];
        bool[] watchdogInterruptPending = version >= 53 ? ReadBoolArray(reader) : new bool[2];
        byte[] watchdogWriteSelect = version >= 53 ? ReadByteArray(reader) : new byte[2];
        long[] frtBaseCycles = version >= 63 ? ReadLongArray(reader) : new long[2];
        ushort[] frtBaseCounters = version >= 63 ? ReadUShortArray(reader) : new ushort[2];
        ushort[] frtLastCounters = version >= 63 ? ReadUShortArray(reader) : new ushort[2];
        ushort[] frtOutputCompareB = version >= 66 ? ReadUShortArray(reader) : [0xFFFF, 0xFFFF];
        byte[] masterCacheDataArray = version >= 48 ? ReadByteArray(reader) : [];
        byte[] slaveCacheDataArray = version >= 48 ? ReadByteArray(reader) : [];
        byte[] masterCacheDataValid = version >= 51 ? ReadByteArray(reader) : BuildLegacyCacheValid(masterCacheDataArray);
        byte[] slaveCacheDataValid = version >= 51 ? ReadByteArray(reader) : BuildLegacyCacheValid(slaveCacheDataArray);
        byte[] masterPrivateWorkRam = version >= 67 ? ReadByteArray(reader) : BuildLegacyPrivateWorkRam(masterCacheDataArray);
        byte[] slavePrivateWorkRam = version >= 67 ? ReadByteArray(reader) : BuildLegacyPrivateWorkRam(slaveCacheDataArray);
        uint[] masterCacheTags = version >= 52 ? ReadUIntArray(reader) : BuildLegacyCacheTags();
        uint[] slaveCacheTags = version >= 52 ? ReadUIntArray(reader) : BuildLegacyCacheTags();
        byte[] masterCacheLru = version >= 52 ? ReadByteArray(reader) : new byte[64];
        byte[] slaveCacheLru = version >= 52 ? ReadByteArray(reader) : new byte[64];
        uint[] masterDivisionRegisters = version >= 50 ? ReadUIntArray(reader) : [];
        uint[] slaveDivisionRegisters = version >= 50 ? ReadUIntArray(reader) : [];
        byte[] dmaRequestSelect = version >= 42 ? ReadByteArray(reader) : [];
        int activeDisplayFrameBufferIndex = reader.ReadInt32();
        bool adapterEnabled = reader.ReadBoolean();
        bool sh2ResetEnabled = reader.ReadBoolean();
        bool sh2ResetReleased = reader.ReadBoolean();
        bool vdpAccessGrantedToSh2 = reader.ReadBoolean();
        bool vBlank = false;
        bool hBlank = false;
        int currentScanline = 0;
        bool frameBufferSwapPending = false;
        int pendingDrawFrameBufferIndex = activeDisplayFrameBufferIndex ^ 1;
        int requestedDisplayFrameBufferIndex = activeDisplayFrameBufferIndex;
        ushort latchedBitmapMode = ReadBigEndianWord(vdpRegisters, ThirtyTwoXHardwareProfile.BitmapModeOffset);
        ushort latchedScreenShiftControl = ReadBigEndianWord(vdpRegisters, ThirtyTwoXHardwareProfile.ScreenShiftControlOffset);
        bool lastCompositeUsedFallback = false;
        int lastCompositeMode = 0;
        ushort masterInterruptMask = 0;
        ushort slaveInterruptMask = 0;
        bool masterVerticalInterruptPending = false;
        bool slaveVerticalInterruptPending = false;
        bool masterVresInterruptPending = false;
        bool slaveVresInterruptPending = false;
        bool masterHorizontalInterruptPending = false;
        bool slaveHorizontalInterruptPending = false;
        byte horizontalInterruptPeriod = 0;
        byte horizontalInterruptCounter = 0;
        bool masterCommandInterruptPending = false;
        bool slaveCommandInterruptPending = false;
        bool bootRomHandshakePending = false;
        bool bootRomSignatureRead = false;
        bool bootRomSignatureReadbackActive = false;
        bool bootRomLaunchPending = false;
        bool bootRomPostStartSignaturePending = false;
        bool bootRomChecksumPublished = false;
        if (version >= 32)
        {
            vBlank = reader.ReadBoolean();
            hBlank = reader.ReadBoolean();
            if (version >= 59)
            {
                currentScanline = reader.ReadInt32();
            }

            frameBufferSwapPending = reader.ReadBoolean();
            pendingDrawFrameBufferIndex = reader.ReadInt32();
            requestedDisplayFrameBufferIndex = pendingDrawFrameBufferIndex ^ 1;
            if (version >= 46)
            {
                requestedDisplayFrameBufferIndex = reader.ReadInt32();
                latchedBitmapMode = reader.ReadUInt16();
                latchedScreenShiftControl = reader.ReadUInt16();
            }

            lastCompositeUsedFallback = reader.ReadBoolean();
            lastCompositeMode = reader.ReadInt32();
        }
        if (version >= 34)
        {
            masterInterruptMask = reader.ReadUInt16();
            slaveInterruptMask = reader.ReadUInt16();
            masterVerticalInterruptPending = reader.ReadBoolean();
            slaveVerticalInterruptPending = reader.ReadBoolean();
            if (version >= 60)
            {
                masterVresInterruptPending = reader.ReadBoolean();
                slaveVresInterruptPending = reader.ReadBoolean();
            }

            masterHorizontalInterruptPending = reader.ReadBoolean();
            slaveHorizontalInterruptPending = reader.ReadBoolean();
            if (version >= 47)
            {
                horizontalInterruptPeriod = reader.ReadByte();
                horizontalInterruptCounter = reader.ReadByte();
            }

            masterCommandInterruptPending = reader.ReadBoolean();
            slaveCommandInterruptPending = reader.ReadBoolean();
        }
        bool sdkWordStreamTerminatorPendingYieldRelease = false;
        if (version >= 68)
        {
            sdkWordStreamTerminatorPendingYieldRelease = reader.ReadBoolean();
        }
        if (version >= 36)
        {
            bootRomHandshakePending = reader.ReadBoolean();
        }
        if (version >= 37)
        {
            bootRomSignatureRead = reader.ReadBoolean();
        }
        if (version >= 38)
        {
            bootRomSignatureReadbackActive = reader.ReadBoolean();
        }
        if (version >= 41)
        {
            bootRomLaunchPending = reader.ReadBoolean();
        }
        if (version >= 45)
        {
            bootRomPostStartSignaturePending = reader.ReadBoolean();
        }
        bool bootRomPostStartSignatureHiddenFromSh2 = false;
        if (version >= 55)
        {
            bootRomPostStartSignatureHiddenFromSh2 = reader.ReadBoolean();
        }
        byte bootRomPostStartSignatureReadMask = 0;
        if (version >= 56)
        {
            bootRomPostStartSignatureReadMask = reader.ReadByte();
        }
        byte bootRomPostStartHostClearProtectMask = 0;
        if (version >= 57)
        {
            bootRomPostStartHostClearProtectMask = reader.ReadByte();
        }

        if (version >= 54)
        {
            bootRomChecksumPublished = reader.ReadBoolean();
        }

        bool bootRomChecksumHostCleared = false;
        if (version >= 62)
        {
            bootRomChecksumHostCleared = reader.ReadBoolean();
        }

        bool bootRomSixtyEightUpPending = false;
        if (version >= 61)
        {
            bootRomSixtyEightUpPending = reader.ReadBoolean();
        }

        bool bootRomSixtyEightUpReadyHiddenFromSh2 = false;
        if (version >= 64)
        {
            bootRomSixtyEightUpReadyHiddenFromSh2 = reader.ReadBoolean();
        }

        bool m68kVdpControlMailboxArmed = false;
        if (version >= 65)
        {
            m68kVdpControlMailboxArmed = reader.ReadBoolean();
        }

        return new ThirtyTwoXDevice.ThirtyTwoXState(
            sdram,
            frameBuffer0,
            frameBuffer1,
            palette,
            systemRegisters,
            m68kCommunicationPendingHostBytes,
            m68kCommunicationDeferredSh2ClearBytes,
            vdpRegisters,
            pwmLeft,
            pwmRight,
            pwmMono,
            pwmLeftHardwareFifo,
            pwmRightHardwareFifo,
            pwmMonoHardwareFifo,
            pwmLeftLevel,
            pwmRightLevel,
            pwmMonoLevel,
            masterPwmInterruptPending,
            slavePwmInterruptPending,
            pwmCycleCounter,
            pwmTimerCounter,
            dreqFifo,
            masterDmaRegisters,
            slaveDmaRegisters,
            masterPeripheralRegisters,
            slavePeripheralRegisters,
            watchdogCycleCounters,
            watchdogInterruptPending,
            watchdogWriteSelect,
            frtBaseCycles,
            frtBaseCounters,
            frtLastCounters,
            frtOutputCompareB,
            masterCacheDataArray,
            slaveCacheDataArray,
            masterCacheDataValid,
            slaveCacheDataValid,
            masterPrivateWorkRam,
            slavePrivateWorkRam,
            masterCacheTags,
            slaveCacheTags,
            masterCacheLru,
            slaveCacheLru,
            masterDivisionRegisters,
            slaveDivisionRegisters,
            dmaRequestSelect,
            activeDisplayFrameBufferIndex,
            adapterEnabled,
            sh2ResetEnabled,
            sh2ResetReleased,
            vdpAccessGrantedToSh2,
            vBlank,
            hBlank,
            currentScanline,
            frameBufferSwapPending,
            pendingDrawFrameBufferIndex,
            requestedDisplayFrameBufferIndex,
            latchedBitmapMode,
            latchedScreenShiftControl,
            lastCompositeUsedFallback,
            lastCompositeMode,
            masterInterruptMask,
            slaveInterruptMask,
            masterVerticalInterruptPending,
            slaveVerticalInterruptPending,
            masterVresInterruptPending,
            slaveVresInterruptPending,
            masterHorizontalInterruptPending,
            slaveHorizontalInterruptPending,
            horizontalInterruptPeriod,
            horizontalInterruptCounter,
            masterCommandInterruptPending,
            slaveCommandInterruptPending,
            sdkWordStreamTerminatorPendingYieldRelease,
            bootRomHandshakePending,
            bootRomSignatureRead,
            bootRomSignatureReadbackActive,
            bootRomLaunchPending,
            bootRomPostStartSignaturePending,
            bootRomPostStartSignatureHiddenFromSh2,
            bootRomPostStartSignatureReadMask,
            bootRomPostStartHostClearProtectMask,
            bootRomChecksumPublished,
            bootRomChecksumHostCleared,
            bootRomSixtyEightUpPending,
            bootRomSixtyEightUpReadyHiddenFromSh2,
            m68kVdpControlMailboxArmed,
            ReadSh2(reader, version),
            ReadSh2(reader, version));
    }

    private static void WriteSh2(BinaryWriter writer, Sh2Cpu.Sh2State state)
    {
        WriteArray(writer, state.R);
        WriteArray(writer, state.BankedR);
        writer.Write(state.PC);
        writer.Write(state.PR);
        writer.Write(state.GBR);
        writer.Write(state.VBR);
        writer.Write(state.MACH);
        writer.Write(state.MACL);
        writer.Write(state.SR);
        writer.Write(state.Cycles);
        writer.Write(state.Halted);
        writer.Write(state.LastOpcode);
        writer.Write(state.LastOpcodePc);
        writer.Write(state.UnhandledOpcodeCount);
        writer.Write(state.DelaySlotActive);
        writer.Write(state.PendingInterruptLevel);
        writer.Write(state.PendingInterruptVectorNumber);
    }

    private static Sh2Cpu.Sh2State ReadSh2(BinaryReader reader, int version)
    {
        uint[] r = ReadUIntArray(reader);
        uint[] bankedR = version >= 35 ? ReadUIntArray(reader) : new uint[8];
        uint pc = reader.ReadUInt32();
        uint pr = reader.ReadUInt32();
        uint gbr = reader.ReadUInt32();
        uint vbr = reader.ReadUInt32();
        uint mach = reader.ReadUInt32();
        uint macl = reader.ReadUInt32();
        uint sr = reader.ReadUInt32();
        long cycles = reader.ReadInt64();
        bool halted = reader.ReadBoolean();
        ushort lastOpcode = reader.ReadUInt16();
        uint lastOpcodePc = reader.ReadUInt32();
        int unhandledOpcodeCount = reader.ReadInt32();
        bool delaySlotActive = reader.ReadBoolean();
        int pendingInterruptLevel = version >= 33 ? reader.ReadInt32() : 0;
        int pendingInterruptVectorNumber = version >= 44 ? reader.ReadInt32() : pendingInterruptLevel == 0 ? 0 : 64 + pendingInterruptLevel;

        return new Sh2Cpu.Sh2State(
            r,
            bankedR,
            pc,
            pr,
            gbr,
            vbr,
            mach,
            macl,
            sr,
            cycles,
            halted,
            lastOpcode,
            lastOpcodePc,
            unhandledOpcodeCount,
            delaySlotActive,
            pendingInterruptLevel,
            pendingInterruptVectorNumber);
    }

    private static void WriteSvp(BinaryWriter writer, SvpDevice.SvpState state)
    {
        WriteArray(writer, state.Iram);
        WriteArray(writer, state.Ram);
        WriteArray(writer, state.Dram);
        WriteArray(writer, state.Gr);
        WriteArray(writer, state.Pointers);
        WriteArray(writer, state.Stack);
        WriteArray(writer, state.Pmac);
        writer.Write(state.EmuStatus);
        writer.Write(state.Pc);
        writer.Write(state.Cycles);
        writer.Write(state.LastOp);
        writer.Write(state.LastOpByteOffset);
        writer.Write(state.UnhandledOpcodeCount);
        writer.Write(state.LastUnhandledOpcode);
        writer.Write(state.LastUnhandledPc);
    }

    private static SvpDevice.SvpState ReadSvp(BinaryReader reader)
    {
        return new SvpDevice.SvpState(
            ReadUShortArray(reader),
            ReadUShortArray(reader),
            ReadUShortArray(reader),
            ReadUIntArray(reader),
            ReadByteArray(reader),
            ReadUShortArray(reader),
            ReadUIntArray(reader),
            reader.ReadUInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadUInt16(),
            reader.ReadInt32(),
            reader.ReadUInt32(),
            reader.ReadUInt16(),
            reader.ReadInt32());
    }

    private static void WritePsg(BinaryWriter writer, Psg.PsgState state)
    {
        WriteArray(writer, state.Registers);
        WriteArray(writer, state.TonePeriods);
        WriteArray(writer, state.ToneCounters);
        WriteArray(writer, state.ToneOutputs);
        writer.Write(state.NoiseShift);
        writer.Write(state.LatchedRegister);
        writer.Write(state.TickRemainder);
    }

    private static Psg.PsgState ReadPsg(BinaryReader reader, int version)
    {
        return new Psg.PsgState(ReadByteArray(reader), ReadIntArray(reader), ReadIntArray(reader), ReadBoolArray(reader), reader.ReadUInt16(), reader.ReadInt32(), version >= 12 ? reader.ReadDouble() : 0.0);
    }

    private static void WriteYm(BinaryWriter writer, Ym2612.Ym2612State state)
    {
        writer.Write(state.Registers.GetLength(0));
        writer.Write(state.Registers.GetLength(1));
        foreach (byte value in state.Registers)
        {
            writer.Write(value);
        }

        WriteArray(writer, state.Selected);
        writer.Write(state.TimerACounter);
        writer.Write(state.TimerBCounter);
        writer.Write(state.Status);
        writer.Write(state.DacSample);
        writer.Write(state.DacEnabled);
        WriteArray(writer, state.KeyOn);
        WriteArray(writer, state.ChannelFNumbers);
        WriteArray(writer, state.ChannelBlocks);
        WriteArray(writer, state.Channel3SpecialFNumbers);
        WriteArray(writer, state.Channel3SpecialBlocks);
        WriteArray(writer, state.Phase);
        WriteArray(writer, state.OperatorEnvelope);
        WriteArray(writer, state.OperatorEnvelopeRemainder);
        WriteArray(writer, state.OperatorStage);
        WriteArray(writer, state.Feedback);
        WriteArray(writer, state.FeedbackPrevious);
        WriteArray(writer, state.AlgorithmMemory);
        WriteArray(writer, state.SsgInverted);
        WriteArray(writer, state.SsgHolding);
        writer.Write(state.LfoPhase);
        writer.Write(state.BusyUntilMasterCycle);
        writer.Write(state.DacFilteredSample);
        writer.Write(state.DacHighPassInput);
        writer.Write(state.DacHighPassOutput);
    }

    private static Ym2612.Ym2612State ReadYm(BinaryReader reader, int version)
    {
        int dim0 = reader.ReadInt32();
        int dim1 = reader.ReadInt32();
        byte[,] registers = new byte[dim0, dim1];
        for (int i = 0; i < dim0; i++)
        {
            for (int j = 0; j < dim1; j++)
            {
                registers[i, j] = reader.ReadByte();
            }
        }

        byte[] selected = ReadByteArray(reader);
        int timerA = reader.ReadInt32();
        int timerB = reader.ReadInt32();
        byte status = reader.ReadByte();
        byte dacSample = reader.ReadByte();
        bool dacEnabled = reader.ReadBoolean();
        byte[] keyOn;
        uint[] phase;
        if (version >= 3)
        {
            keyOn = ReadByteArray(reader);
            phase = ReadUIntArray(reader);
        }
        else
        {
            keyOn = new byte[6];
            keyOn[0] = reader.ReadByte();
            phase = new uint[6];
            phase[0] = (uint)reader.ReadInt32();
        }

        int[] channelFNumbers = version >= 19 ? ReadIntArray(reader) : DefaultYmChannelFNumbers(registers);
        int[] channelBlocks = version >= 19 ? ReadIntArray(reader) : DefaultYmChannelBlocks(registers);
        int[] channel3SpecialFNumbers = version >= 19 ? ReadIntArray(reader) : DefaultYmChannel3SpecialFNumbers(registers);
        int[] channel3SpecialBlocks = version >= 19 ? ReadIntArray(reader) : DefaultYmChannel3SpecialBlocks(registers);
        int[] operatorEnvelope = version >= 4 ? ReadIntArray(reader) : DefaultOperatorEnvelope(keyOn);
        if (version < 20)
        {
            ConvertLegacyYmEnvelopeAmplitudeToAttenuation(operatorEnvelope);
        }
        double[] operatorEnvelopeRemainder = version >= 15 ? ReadDoubleArray(reader) : new double[24];
        byte[] operatorStage = version >= 5 ? ReadByteArray(reader) : DefaultOperatorStage(keyOn);
        int[] feedback = version >= 5 ? ReadIntArray(reader) : new int[6];
        int[] feedbackPrevious = version >= 14 ? ReadIntArray(reader) : new int[6];
        int[] algorithmMemory = version >= 21 ? ReadIntArray(reader) : new int[6];
        bool[] ssgInverted = version >= 14 ? ReadBoolArray(reader) : new bool[24];
        bool[] ssgHolding = version >= 14 ? ReadBoolArray(reader) : new bool[24];
        double lfoPhase = version >= 9 ? reader.ReadDouble() : 0.0;
        long busyUntilMasterCycle = version >= 13 ? reader.ReadInt64() : 0;
        double dacFilteredSample = version >= 28 ? reader.ReadDouble() : 0.0;
        double dacHighPassInput = version >= 29 ? reader.ReadDouble() : dacFilteredSample;
        double dacHighPassOutput = version >= 29 ? reader.ReadDouble() : 0.0;

        return new Ym2612.Ym2612State(registers, selected, timerA, timerB, status, dacSample, dacEnabled, keyOn, channelFNumbers, channelBlocks, channel3SpecialFNumbers, channel3SpecialBlocks, phase, operatorEnvelope, operatorEnvelopeRemainder, operatorStage, feedback, feedbackPrevious, algorithmMemory, ssgInverted, ssgHolding, lfoPhase, busyUntilMasterCycle, dacFilteredSample, dacHighPassInput, dacHighPassOutput);
    }

    private static int[] DefaultYmChannelFNumbers(byte[,] registers)
    {
        int[] values = new int[6];
        for (int bank = 0; bank < Math.Min(2, registers.GetLength(0)); bank++)
        {
            for (int slot = 0; slot < 3; slot++)
            {
                values[(bank * 3) + slot] = ((registers[bank, 0xA4 + slot] & 0x07) << 8) | registers[bank, 0xA0 + slot];
            }
        }

        return values;
    }

    private static int[] DefaultYmChannelBlocks(byte[,] registers)
    {
        int[] values = new int[6];
        for (int bank = 0; bank < Math.Min(2, registers.GetLength(0)); bank++)
        {
            for (int slot = 0; slot < 3; slot++)
            {
                values[(bank * 3) + slot] = (registers[bank, 0xA4 + slot] >> 3) & 0x07;
            }
        }

        return values;
    }

    private static int[] DefaultYmChannel3SpecialFNumbers(byte[,] registers)
    {
        int[] values = new int[3];
        if (registers.GetLength(0) == 0)
        {
            return values;
        }

        for (int register = 0; register < values.Length; register++)
        {
            values[register] = ((registers[0, 0xAC + register] & 0x07) << 8) | registers[0, 0xA8 + register];
        }

        return values;
    }

    private static int[] DefaultYmChannel3SpecialBlocks(byte[,] registers)
    {
        int[] values = new int[3];
        if (registers.GetLength(0) == 0)
        {
            return values;
        }

        for (int register = 0; register < values.Length; register++)
        {
            values[register] = (registers[0, 0xAC + register] >> 3) & 0x07;
        }

        return values;
    }

    private static int[] DefaultOperatorEnvelope(byte[] keyOn)
    {
        int[] envelopes = new int[24];
        for (int channel = 0; channel < Math.Min(6, keyOn.Length); channel++)
        {
            for (int op = 0; op < 4; op++)
            {
                if ((keyOn[channel] & YmKeyOnMask(op)) != 0)
                {
                    envelopes[(channel * 4) + op] = 1024;
                }
            }
        }

        return envelopes;
    }

    private static void ConvertLegacyYmEnvelopeAmplitudeToAttenuation(int[] envelopes)
    {
        for (int i = 0; i < envelopes.Length; i++)
        {
            envelopes[i] = Math.Clamp(1024 - envelopes[i], 0, 1024);
        }
    }

    private static byte[] DefaultOperatorStage(byte[] keyOn)
    {
        byte[] stages = new byte[24];
        Array.Fill(stages, (byte)3);
        for (int channel = 0; channel < Math.Min(6, keyOn.Length); channel++)
        {
            for (int op = 0; op < 4; op++)
            {
                if ((keyOn[channel] & YmKeyOnMask(op)) != 0)
                {
                    stages[(channel * 4) + op] = 2;
                }
            }
        }

        return stages;
    }

    private static byte YmKeyOnMask(int op)
    {
        return op switch
        {
            0 => 0x01,
            1 => 0x04,
            2 => 0x02,
            _ => 0x08,
        };
    }

    private static void WriteScheduler(BinaryWriter writer, GenesisScheduler.SchedulerState state)
    {
        writer.Write(state.MasterCycles);
        writer.Write(state.FrameNumber);
        writer.Write(state.Scanline);
    }

    private static GenesisScheduler.SchedulerState ReadScheduler(BinaryReader reader)
    {
        return new GenesisScheduler.SchedulerState(reader.ReadInt64(), reader.ReadInt32(), reader.ReadInt32());
    }

    private static void WriteArray(BinaryWriter writer, byte[] values)
    {
        writer.Write(values.Length);
        writer.Write(values);
    }

    private static void WriteArray(BinaryWriter writer, ushort[] values)
    {
        writer.Write(values.Length);
        foreach (ushort value in values)
        {
            writer.Write(value);
        }
    }

    private static void WriteArray(BinaryWriter writer, uint[] values)
    {
        writer.Write(values.Length);
        foreach (uint value in values)
        {
            writer.Write(value);
        }
    }

    private static void WriteArray(BinaryWriter writer, int[] values)
    {
        writer.Write(values.Length);
        foreach (int value in values)
        {
            writer.Write(value);
        }
    }

    private static void WriteArray(BinaryWriter writer, long[] values)
    {
        writer.Write(values.Length);
        foreach (long value in values)
        {
            writer.Write(value);
        }
    }

    private static void WriteArray(BinaryWriter writer, bool[] values)
    {
        writer.Write(values.Length);
        foreach (bool value in values)
        {
            writer.Write(value);
        }
    }

    private static void WriteArray(BinaryWriter writer, double[] values)
    {
        writer.Write(values.Length);
        foreach (double value in values)
        {
            writer.Write(value);
        }
    }

    private static byte[] ReadByteArray(BinaryReader reader) => reader.ReadBytes(reader.ReadInt32());
    private static byte[] ReadRegisterWindow(byte[] registers, int start)
    {
        byte[] window = new byte[16];
        if (registers.Length > start)
        {
            Array.Copy(registers, start, window, 0, Math.Min(window.Length, registers.Length - start));
        }

        return window;
    }

    private static ushort[] ReadUShortArray(BinaryReader reader) => ReadArray(reader, r => r.ReadUInt16());
    private static uint[] ReadUIntArray(BinaryReader reader) => ReadArray(reader, r => r.ReadUInt32());
    private static int[] ReadIntArray(BinaryReader reader) => ReadArray(reader, r => r.ReadInt32());
    private static long[] ReadLongArray(BinaryReader reader) => ReadArray(reader, r => r.ReadInt64());
    private static bool[] ReadBoolArray(BinaryReader reader) => ReadArray(reader, r => r.ReadBoolean());
    private static double[] ReadDoubleArray(BinaryReader reader) => ReadArray(reader, r => r.ReadDouble());

    private static byte[] BuildLegacyCacheValid(byte[] data)
    {
        byte[] valid = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            valid[i] = data[i] == 0 ? (byte)0 : (byte)1;
        }

        return valid;
    }

    private static byte[] BuildLegacyPrivateWorkRam(byte[] cacheDataArray)
    {
        byte[] ram = new byte[0x800];
        Array.Copy(cacheDataArray, ram, Math.Min(cacheDataArray.Length, ram.Length));
        return ram;
    }

    private static uint[] BuildLegacyCacheTags()
    {
        uint[] tags = new uint[256];
        Array.Fill(tags, 1u << 19);
        return tags;
    }

    private static ushort ReadBigEndianWord(byte[] data, int offset)
    {
        if ((uint)(offset + 1) >= (uint)data.Length)
        {
            return 0;
        }

        return (ushort)((data[offset] << 8) | data[offset + 1]);
    }

    private static T[] ReadArray<T>(BinaryReader reader, Func<BinaryReader, T> read)
    {
        int length = reader.ReadInt32();
        T[] values = new T[length];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = read(reader);
        }

        return values;
    }
}
