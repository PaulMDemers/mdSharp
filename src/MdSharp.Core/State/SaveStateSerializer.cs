using MdSharp.Core.Audio;
using MdSharp.Core.Bus;
using MdSharp.Core.Cartridge;
using MdSharp.Core.Cpu.M68k;
using MdSharp.Core.Cpu.Z80;
using MdSharp.Core.Timing;
using MdSharp.Core.Video;

namespace MdSharp.Core.State;

public static class SaveStateSerializer
{
    private const uint Magic = 0x5353444D; // MDSS
    private const int Version = 30;

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
            version >= 27 ? reader.ReadInt32() : 0);
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
        return new GenesisBus.BusState(workRam, z80Ram, tmss, ioData, ioControl, z80BusRequested, z80ResetAsserted, z80BankRegister, saveRam, bankRegisters, bankSwitchingEnabled, fallbackSaveRamActive, saveRamEnabled, z80BusGrantReadyCycle, pendingM68kWaitCycles, svp);
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
    private static ushort[] ReadUShortArray(BinaryReader reader) => ReadArray(reader, r => r.ReadUInt16());
    private static uint[] ReadUIntArray(BinaryReader reader) => ReadArray(reader, r => r.ReadUInt32());
    private static int[] ReadIntArray(BinaryReader reader) => ReadArray(reader, r => r.ReadInt32());
    private static bool[] ReadBoolArray(BinaryReader reader) => ReadArray(reader, r => r.ReadBoolean());
    private static double[] ReadDoubleArray(BinaryReader reader) => ReadArray(reader, r => r.ReadDouble());

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
