using MdSharp.Core.Bus;

namespace MdSharp.Core.SegaCd;

internal sealed class SegaCdSubBus(SegaCdDevice device) : IMemoryBus, IInstructionTraceSink
{
    public uint CurrentM68kPc { get; set; }

    public byte ReadByte(uint address)
    {
        address &= 0x00FF_FFFF;
        if (device.SubBiosMapped && address <= SegaCdHardwareProfile.MainBiosEndInclusive && IsSubBiosFetch(address))
        {
            return device.ReadBiosByte(address);
        }

        if (address is >= SegaCdHardwareProfile.SubProgramRamStart and <= SegaCdHardwareProfile.SubProgramRamEndInclusive)
        {
            return device.ReadProgramRamByte(address - SegaCdHardwareProfile.SubProgramRamStart);
        }

        if (address is >= SegaCdHardwareProfile.SubWordRamStart and <= SegaCdHardwareProfile.SubWordRamEndInclusive)
        {
            return device.ReadWordRamByte(address - SegaCdHardwareProfile.SubWordRamStart);
        }

        if (address is >= SegaCdHardwareProfile.SubWordRam1MStart and <= SegaCdHardwareProfile.SubWordRam1MEndInclusive)
        {
            return device.ReadSubOneMegWordRamByte(address - SegaCdHardwareProfile.SubWordRam1MStart);
        }

        if (address is >= SegaCdHardwareProfile.SubBackupRamStart and <= SegaCdHardwareProfile.SubBackupRamEndInclusive)
        {
            return device.ReadBackupRamByte(address - SegaCdHardwareProfile.SubBackupRamStart);
        }

        if (address is >= SegaCdHardwareProfile.SubRegisterStart and <= SegaCdHardwareProfile.SubRegisterEndInclusive)
        {
            return device.ReadSubRegisterByte(address - SegaCdHardwareProfile.SubRegisterStart);
        }

        if (address is >= SegaCdHardwareProfile.SubPcmRamStart and <= SegaCdHardwareProfile.SubPcmRamEndInclusive)
        {
            return device.ReadPcmMappedByte(address - SegaCdHardwareProfile.SubPcmRamStart);
        }

        return 0xFF;
    }

    public ushort ReadWord(uint address)
    {
        address &= 0x00FF_FFFF;
        if (device.SubBiosMapped && address <= SegaCdHardwareProfile.MainBiosEndInclusive && IsSubBiosFetch(address))
        {
            return device.ReadBiosWord(address);
        }

        if (address is >= SegaCdHardwareProfile.SubRegisterStart and <= SegaCdHardwareProfile.SubRegisterEndInclusive)
        {
            return device.ReadSubRegisterWord(address - SegaCdHardwareProfile.SubRegisterStart);
        }

        return (ushort)((ReadByte(address) << 8) | ReadByte(address + 1));
    }

    public void WriteByte(uint address, byte value)
    {
        address &= 0x00FF_FFFF;
        if (address is >= SegaCdHardwareProfile.SubProgramRamStart and <= SegaCdHardwareProfile.SubProgramRamEndInclusive)
        {
            device.WriteProgramRamByte(address - SegaCdHardwareProfile.SubProgramRamStart, value);
            TraceWrite(address, value);
            return;
        }

        if (address is >= SegaCdHardwareProfile.SubWordRamStart and <= SegaCdHardwareProfile.SubWordRamEndInclusive)
        {
            device.WriteWordRamByte(address - SegaCdHardwareProfile.SubWordRamStart, value);
            TraceWrite(address, value);
            return;
        }

        if (address is >= SegaCdHardwareProfile.SubWordRam1MStart and <= SegaCdHardwareProfile.SubWordRam1MEndInclusive)
        {
            device.WriteSubOneMegWordRamByte(address - SegaCdHardwareProfile.SubWordRam1MStart, value);
            TraceWrite(address, value);
            return;
        }

        if (address is >= SegaCdHardwareProfile.SubBackupRamStart and <= SegaCdHardwareProfile.SubBackupRamEndInclusive)
        {
            device.WriteBackupRamByte(address - SegaCdHardwareProfile.SubBackupRamStart, value);
            TraceWrite(address, value);
            return;
        }

        if (address is >= SegaCdHardwareProfile.SubRegisterStart and <= SegaCdHardwareProfile.SubRegisterEndInclusive)
        {
            device.WriteSubRegisterByte(address - SegaCdHardwareProfile.SubRegisterStart, value);
            TraceWrite(address, value);
            return;
        }

        if (address is >= SegaCdHardwareProfile.SubPcmRamStart and <= SegaCdHardwareProfile.SubPcmRamEndInclusive)
        {
            device.WritePcmMappedByte(address - SegaCdHardwareProfile.SubPcmRamStart, value);
            TraceWrite(address, value);
        }
    }

    public void WriteWord(uint address, ushort value)
    {
        address &= 0x00FF_FFFF;
        if (address is >= SegaCdHardwareProfile.SubRegisterStart and <= SegaCdHardwareProfile.SubRegisterEndInclusive)
        {
            device.WriteSubRegisterWord(address - SegaCdHardwareProfile.SubRegisterStart, value);
            return;
        }

        WriteByte(address, (byte)(value >> 8));
        WriteByte(address + 1, (byte)value);
    }

    private bool IsSubBiosFetch(uint address)
    {
        uint pc = CurrentM68kPc & 0x00FF_FFFF;
        if (pc > SegaCdHardwareProfile.MainBiosEndInclusive)
        {
            return false;
        }

        uint distance = (address - pc) & 0x00FF_FFFF;
        return distance < 16;
    }

    private void TraceWrite(uint address, byte value)
    {
        device.SubMemoryWriteObserver?.Invoke(new SegaCdDevice.SegaCdSubMemoryWriteTrace(CurrentM68kPc, address, value));
    }
}
