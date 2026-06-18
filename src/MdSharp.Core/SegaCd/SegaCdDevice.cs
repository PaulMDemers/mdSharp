using MdSharp.Core.Cpu.M68k;

namespace MdSharp.Core.SegaCd;

public sealed class SegaCdDevice
{
    private readonly byte[] _bios;
    private readonly byte[] _programRam = new byte[SegaCdHardwareProfile.ProgramRamBytes];
    private readonly byte[] _wordRam = new byte[SegaCdHardwareProfile.WordRamBytes];
    private readonly byte[] _backupRam = new byte[SegaCdHardwareProfile.BackupRamBytes];
    private readonly byte[] _pcmRam = new byte[SegaCdHardwareProfile.PcmRamBytes];
    private readonly byte[] _mainRegisters = new byte[SegaCdHardwareProfile.RegisterBytes];
    private readonly SegaCdSubBus _subBus;

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
        return _mainRegisters[offset & (SegaCdHardwareProfile.RegisterBytes - 1)];
    }

    public ushort ReadMainRegisterWord(uint offset)
    {
        return (ushort)((ReadMainRegisterByte(offset) << 8) | ReadMainRegisterByte(offset + 1));
    }

    public void WriteMainRegisterByte(uint offset, byte value)
    {
        _mainRegisters[offset & (SegaCdHardwareProfile.RegisterBytes - 1)] = value;
    }

    public void WriteMainRegisterWord(uint offset, ushort value)
    {
        WriteMainRegisterByte(offset, (byte)(value >> 8));
        WriteMainRegisterByte(offset + 1, (byte)value);
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
        SubCpu.RestoreState(state.SubCpu);
    }

    private static void CopyInto(byte[] source, byte[] destination)
    {
        Array.Clear(destination);
        Array.Copy(source, destination, Math.Min(source.Length, destination.Length));
    }

    public sealed record SegaCdState(
        byte[] ProgramRam,
        byte[] WordRam,
        byte[] BackupRam,
        byte[] PcmRam,
        byte[] MainRegisters,
        bool SubBiosMapped,
        M68kCpu.M68kState SubCpu);
}
