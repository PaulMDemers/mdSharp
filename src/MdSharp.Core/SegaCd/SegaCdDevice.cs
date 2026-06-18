using MdSharp.Core.Cpu.M68k;

namespace MdSharp.Core.SegaCd;

public sealed class SegaCdDevice
{
    private const byte MainControlSubResetRelease = 0x01;
    private const byte MainControlSubBusRequest = 0x02;
    private const byte CddHockBit = 0x04;
    private const uint WordRamModeLowOffset = 0x03;
    private const uint CddControlOffset = 0x37;
    private const uint CddStatusStart = 0x38;
    private const uint CddCommandStart = 0x42;
    private const uint SubToMainFlagOffset = 0x0F;
    private const int CddPacketBytes = 10;
    private const int CddInterruptLevel = 4;
    private const double CddInterruptHz = 75.0;

    private readonly byte[] _bios;
    private readonly byte[] _programRam = new byte[SegaCdHardwareProfile.ProgramRamBytes];
    private readonly byte[] _wordRam = new byte[SegaCdHardwareProfile.WordRamBytes];
    private readonly byte[] _backupRam = new byte[SegaCdHardwareProfile.BackupRamBytes];
    private readonly byte[] _pcmRam = new byte[SegaCdHardwareProfile.PcmRamBytes];
    private readonly byte[] _mainRegisters = new byte[SegaCdHardwareProfile.RegisterBytes];
    private readonly SegaCdSubBus _subBus;
    private double _cddInterruptCycleCarry;
    private byte _cddStatusCode;
    private byte _pendingSubInterruptLevels;

    public SegaCdDevice(ReadOnlyMemory<byte> bios, SegaCdRegion region, DiscImage? disc = null)
    {
        if (bios.Length is not (SegaCdHardwareProfile.BiosSize or SegaCdHardwareProfile.ExtendedBiosSize))
        {
            throw new ArgumentException("Sega CD BIOS must be 128 KiB or 256 KiB.", nameof(bios));
        }

        _bios = bios.ToArray();
        Region = region;
        Disc = disc;
        _subBus = new SegaCdSubBus(this);
        SubCpu = new M68kCpu(_subBus);
        Array.Fill<byte>(_backupRam, 0xFF);
    }

    public SegaCdRegion Region { get; }
    public DiscImage? Disc { get; }
    public M68kCpu SubCpu { get; }
    public bool SubBiosMapped { get; private set; } = true;
    public bool SubCpuResetReleased { get; private set; }
    public bool SubCpuBusRequested { get; private set; }
    public bool SubCpuRunnable => SubCpuResetReleased && !SubCpuBusRequested;
    public ReadOnlySpan<byte> Bios => _bios;
    public ReadOnlySpan<byte> ProgramRam => _programRam;
    public ReadOnlySpan<byte> WordRam => _wordRam;
    public ReadOnlySpan<byte> BackupRam => _backupRam;
    public ReadOnlySpan<byte> PcmRam => _pcmRam;
    public ReadOnlySpan<byte> MainRegisters => _mainRegisters;

    public void Reset()
    {
        Array.Clear(_programRam);
        Array.Clear(_wordRam);
        Array.Clear(_pcmRam);
        Array.Clear(_mainRegisters);
        SubBiosMapped = true;
        SubCpuResetReleased = false;
        SubCpuBusRequested = false;
        _cddInterruptCycleCarry = 0.0;
        _cddStatusCode = Disc is null ? (byte)0x05 : (byte)0x04;
        _pendingSubInterruptLevels = 0;
        SubCpu.Reset();
    }

    public void UnmapSubBios()
    {
        SubBiosMapped = false;
    }

    public byte ReadBiosByte(uint address)
    {
        return _bios[address % (uint)_bios.Length];
    }

    public ushort ReadBiosWord(uint address)
    {
        return (ushort)((ReadBiosByte(address) << 8) | ReadBiosByte(address + 1));
    }

    public byte ReadProgramRamByte(uint address)
    {
        return _programRam[address & (SegaCdHardwareProfile.ProgramRamBytes - 1)];
    }

    public void WriteProgramRamByte(uint address, byte value)
    {
        _programRam[address & (SegaCdHardwareProfile.ProgramRamBytes - 1)] = value;
    }

    public byte ReadWordRamByte(uint address)
    {
        return _wordRam[address & (SegaCdHardwareProfile.WordRamBytes - 1)];
    }

    public void WriteWordRamByte(uint address, byte value)
    {
        _wordRam[address & (SegaCdHardwareProfile.WordRamBytes - 1)] = value;
    }

    public byte ReadBackupRamByte(uint address)
    {
        return _backupRam[address & (SegaCdHardwareProfile.BackupRamBytes - 1)];
    }

    public void WriteBackupRamByte(uint address, byte value)
    {
        _backupRam[address & (SegaCdHardwareProfile.BackupRamBytes - 1)] = value;
    }

    public byte ReadPcmRamByte(uint address)
    {
        return _pcmRam[address & (SegaCdHardwareProfile.PcmRamBytes - 1)];
    }

    public void WritePcmRamByte(uint address, byte value)
    {
        _pcmRam[address & (SegaCdHardwareProfile.PcmRamBytes - 1)] = value;
    }

    public byte ReadMainRegisterByte(uint offset)
    {
        uint maskedOffset = offset & (SegaCdHardwareProfile.RegisterBytes - 1);
        byte value = _mainRegisters[maskedOffset];
        if (maskedOffset == WordRamModeLowOffset)
        {
            value |= 0x01;
        }

        return value;
    }

    public ushort ReadMainRegisterWord(uint offset)
    {
        return (ushort)((ReadMainRegisterByte(offset) << 8) | ReadMainRegisterByte(offset + 1));
    }

    public byte ReadSubRegisterByte(uint offset)
    {
        uint maskedOffset = offset & (SegaCdHardwareProfile.RegisterBytes - 1);
        byte value = _mainRegisters[maskedOffset];
        if (maskedOffset == 1 && SubCpuResetReleased)
        {
            value |= MainControlSubResetRelease;
        }

        return value;
    }

    public void WriteMainRegisterByte(uint offset, byte value)
    {
        uint maskedOffset = offset & (SegaCdHardwareProfile.RegisterBytes - 1);
        _mainRegisters[maskedOffset] = value;
        if (maskedOffset == 1)
        {
            ApplyMainControlLowByte(value);
        }
    }

    public void WriteMainRegisterWord(uint offset, ushort value)
    {
        WriteMainRegisterByte(offset, (byte)(value >> 8));
        WriteMainRegisterByte(offset + 1, (byte)value);
    }

    public void WriteSubRegisterByte(uint offset, byte value)
    {
        uint maskedOffset = offset & (SegaCdHardwareProfile.RegisterBytes - 1);
        _mainRegisters[maskedOffset] = value;
        if (maskedOffset == CddControlOffset && (value & CddHockBit) != 0)
        {
            RefreshCddStatusRegisters();
            RaiseSubToMainFlag(0x01);
            QueueSubInterrupt(CddInterruptLevel);
            return;
        }

        if (maskedOffset == CddCommandStart + CddPacketBytes - 1)
        {
            ProcessCddCommand();
        }
    }

    public void WriteSubRegisterWord(uint offset, ushort value)
    {
        WriteSubRegisterByte(offset, (byte)(value >> 8));
        WriteSubRegisterByte(offset + 1, (byte)value);
    }

    public SegaCdState CaptureState()
    {
        return new SegaCdState(
            (byte[])_programRam.Clone(),
            (byte[])_wordRam.Clone(),
            (byte[])_backupRam.Clone(),
            (byte[])_pcmRam.Clone(),
            (byte[])_mainRegisters.Clone(),
            SubBiosMapped,
            SubCpuResetReleased,
            SubCpuBusRequested,
            _cddInterruptCycleCarry,
            _cddStatusCode,
            _pendingSubInterruptLevels,
            SubCpu.CaptureState());
    }

    public void RestoreState(SegaCdState state)
    {
        CopyInto(state.ProgramRam, _programRam);
        CopyInto(state.WordRam, _wordRam);
        CopyInto(state.BackupRam, _backupRam);
        CopyInto(state.PcmRam, _pcmRam);
        CopyInto(state.MainRegisters, _mainRegisters);
        SubBiosMapped = state.SubBiosMapped;
        SubCpuResetReleased = state.SubCpuResetReleased;
        SubCpuBusRequested = state.SubCpuBusRequested;
        _cddInterruptCycleCarry = state.CddInterruptCycleCarry;
        _cddStatusCode = state.CddStatusCode;
        _pendingSubInterruptLevels = state.PendingSubInterruptLevels;
        SubCpu.RestoreState(state.SubCpu);
    }

    public int RunSubCpuCycles(int cycleBudget, Func<bool>? shouldAbort = null)
    {
        if (cycleBudget <= 0 || !SubCpuRunnable)
        {
            return 0;
        }

        int executed = 0;
        while (executed < cycleBudget)
        {
            if (shouldAbort?.Invoke() == true)
            {
                return -1;
            }

            ServicePendingSubInterrupts();
            int cycles = SubCpu.Step();
            if (cycles <= 0)
            {
                break;
            }

            executed += cycles;
        }

        AdvanceCddInterrupts(executed);
        return executed;
    }

    private static void CopyInto(byte[] source, byte[] destination)
    {
        Array.Clear(destination);
        Array.Copy(source, destination, Math.Min(source.Length, destination.Length));
    }

    private void ApplyMainControlLowByte(byte value)
    {
        bool nextResetReleased = (value & MainControlSubResetRelease) != 0;
        bool resetRisingEdge = !SubCpuResetReleased && nextResetReleased;
        SubCpuResetReleased = nextResetReleased;
        SubCpuBusRequested = (value & MainControlSubBusRequest) != 0;
        if (resetRisingEdge)
        {
            SubBiosMapped = false;
            SubCpu.Reset();
        }
        else if (!SubCpuResetReleased)
        {
            SubBiosMapped = true;
        }
    }

    private void ProcessCddCommand()
    {
        int command = ((_mainRegisters[CddCommandStart] & 0x0F) << 4) | (_mainRegisters[CddCommandStart + 1] & 0x0F);
        int parameter = (_mainRegisters[CddCommandStart + 3] & 0x0F);
        switch (command)
        {
            case 0x00:
                break;
            case 0x01:
                _cddStatusCode = Disc is null ? (byte)0x05 : (byte)0x09;
                break;
            case 0x02:
                RefreshCddStatusRegisters(Disc is null ? (byte)0x05 : (byte)0x04, (byte)parameter);
                QueueCddInterruptIfEnabled();
                return;
            case 0x03:
                _cddStatusCode = Disc is null ? (byte)0x05 : (byte)0x01;
                break;
            case 0x04:
                _cddStatusCode = Disc is null ? (byte)0x05 : (byte)0x02;
                break;
            case 0x06:
                _cddStatusCode = Disc is null ? (byte)0x05 : (byte)0x04;
                break;
            case 0x0C:
                _cddStatusCode = Disc is null ? (byte)0x0B : (byte)0x04;
                break;
            case 0x0D:
                _cddStatusCode = 0x05;
                break;
        }

        RefreshCddStatusRegisters();
        RaiseSubToMainFlag(0x01);
        QueueCddInterruptIfEnabled();
    }

    private void RefreshCddStatusRegisters(byte? statusOverride = null, byte parameter = 0)
    {
        byte status = statusOverride ?? _cddStatusCode;
        ClearCddStatusBytes();
        _mainRegisters[CddStatusStart] = (byte)((status >> 4) & 0x0F);
        _mainRegisters[CddStatusStart + 1] = (byte)(status & 0x0F);
        _mainRegisters[CddStatusStart + 2] = 0;
        _mainRegisters[CddStatusStart + 3] = parameter;
        WriteCddChecksum(CddStatusStart);
    }

    private void ClearCddStatusBytes()
    {
        for (int i = 0; i < CddPacketBytes; i++)
        {
            _mainRegisters[CddStatusStart + i] = 0;
        }
    }

    private void WriteCddChecksum(uint packetStart)
    {
        int sum = 0;
        for (int i = 0; i < CddPacketBytes - 1; i++)
        {
            sum += _mainRegisters[packetStart + i] & 0x0F;
        }

        _mainRegisters[packetStart + CddPacketBytes - 2] = 0;
        _mainRegisters[packetStart + CddPacketBytes - 1] = (byte)((~sum) & 0x0F);
    }

    private void AdvanceCddInterrupts(int executedCycles)
    {
        if (executedCycles <= 0 || (_mainRegisters[CddControlOffset] & CddHockBit) == 0)
        {
            return;
        }

        _cddInterruptCycleCarry += executedCycles;
        double cyclesPerInterrupt = SegaCdHardwareProfile.SubCpuClockHz / CddInterruptHz;
        while (_cddInterruptCycleCarry >= cyclesPerInterrupt)
        {
            _cddInterruptCycleCarry -= cyclesPerInterrupt;
            RefreshCddStatusRegisters();
            QueueSubInterrupt(CddInterruptLevel);
        }
    }

    private void QueueCddInterruptIfEnabled()
    {
        if ((_mainRegisters[CddControlOffset] & CddHockBit) != 0)
        {
            QueueSubInterrupt(CddInterruptLevel);
        }
    }

    private void RaiseSubToMainFlag(byte mask)
    {
        _mainRegisters[SubToMainFlagOffset] |= mask;
    }

    private void QueueSubInterrupt(int level)
    {
        if (level is <= 0 or > 7)
        {
            return;
        }

        _pendingSubInterruptLevels |= (byte)(1 << level);
    }

    private void ServicePendingSubInterrupts()
    {
        int mask = (SubCpu.SR >> 8) & 0x07;
        for (int level = 7; level > mask; level--)
        {
            byte bit = (byte)(1 << level);
            if ((_pendingSubInterruptLevels & bit) == 0)
            {
                continue;
            }

            if (SubCpu.RequestInterrupt(level))
            {
                _pendingSubInterruptLevels &= (byte)~bit;
            }

            return;
        }
    }

    public sealed record SegaCdState(
        byte[] ProgramRam,
        byte[] WordRam,
        byte[] BackupRam,
        byte[] PcmRam,
        byte[] MainRegisters,
        bool SubBiosMapped,
        bool SubCpuResetReleased,
        bool SubCpuBusRequested,
        double CddInterruptCycleCarry,
        byte CddStatusCode,
        byte PendingSubInterruptLevels,
        M68kCpu.M68kState SubCpu);
}
