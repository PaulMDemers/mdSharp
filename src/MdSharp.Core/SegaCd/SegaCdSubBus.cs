using MdSharp.Core.Bus;

namespace MdSharp.Core.SegaCd;

internal sealed class SegaCdSubBus(SegaCdDevice device) : IMemoryBus
{
    public byte ReadByte(uint address)
    {
        address &= 0x00FF_FFFF;
        if (device.SubBiosMapped && address <= SegaCdHardwareProfile.MainBiosEndInclusive)
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

        if (address is >= SegaCdHardwareProfile.SubBackupRamStart and <= SegaCdHardwareProfile.SubBackupRamEndInclusive)
        {
            return device.ReadBackupRamByte(address - SegaCdHardwareProfile.SubBackupRamStart);
        }

        if (address is >= SegaCdHardwareProfile.SubPcmRamStart and <= SegaCdHardwareProfile.SubPcmRamEndInclusive)
        {
            return device.ReadPcmRamByte(address - SegaCdHardwareProfile.SubPcmRamStart);
        }

        if (address is >= SegaCdHardwareProfile.SubRegisterStart and <= SegaCdHardwareProfile.SubRegisterEndInclusive)
        {
            return device.ReadMainRegisterByte(address - SegaCdHardwareProfile.SubRegisterStart);
        }

        return 0xFF;
    }

    public void WriteByte(uint address, byte value)
    {
        address &= 0x00FF_FFFF;
        if (address is >= SegaCdHardwareProfile.SubProgramRamStart and <= SegaCdHardwareProfile.SubProgramRamEndInclusive)
        {
            device.WriteProgramRamByte(address - SegaCdHardwareProfile.SubProgramRamStart, value);
            return;
        }

        if (address is >= SegaCdHardwareProfile.SubWordRamStart and <= SegaCdHardwareProfile.SubWordRamEndInclusive)
        {
            device.WriteWordRamByte(address - SegaCdHardwareProfile.SubWordRamStart, value);
            return;
        }

        if (address is >= SegaCdHardwareProfile.SubBackupRamStart and <= SegaCdHardwareProfile.SubBackupRamEndInclusive)
        {
            device.WriteBackupRamByte(address - SegaCdHardwareProfile.SubBackupRamStart, value);
            return;
        }

        if (address is >= SegaCdHardwareProfile.SubPcmRamStart and <= SegaCdHardwareProfile.SubPcmRamEndInclusive)
        {
            device.WritePcmRamByte(address - SegaCdHardwareProfile.SubPcmRamStart, value);
            return;
        }

        if (address is >= SegaCdHardwareProfile.SubRegisterStart and <= SegaCdHardwareProfile.SubRegisterEndInclusive)
        {
            device.WriteMainRegisterByte(address - SegaCdHardwareProfile.SubRegisterStart, value);
        }
    }
}
