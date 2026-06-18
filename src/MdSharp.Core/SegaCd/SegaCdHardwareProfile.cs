namespace MdSharp.Core.SegaCd;

public static class SegaCdHardwareProfile
{
    public const int BiosSize = 128 * 1024;
    public const int ExtendedBiosSize = 256 * 1024;
    public const int ProgramRamBytes = 512 * 1024;
    public const int WordRamBytes = 256 * 1024;
    public const int BackupRamBytes = 8 * 1024;
    public const int PcmRamBytes = 64 * 1024;
    public const int RegisterBytes = 0x200;
    public const uint MainBiosStart = 0x000000;
    public const uint MainBiosEndInclusive = MainBiosStart + BiosSize - 1;
    public const uint MainProgramRamWindowStart = 0x020000;
    public const uint MainProgramRamWindowEndInclusive = 0x03FFFF;
    public const uint MainWordRamStart = 0x200000;
    public const uint MainWordRamEndInclusive = MainWordRamStart + WordRamBytes - 1;
    public const uint MainRegisterStart = 0xA12000;
    public const uint MainRegisterEndInclusive = MainRegisterStart + RegisterBytes - 1;
}
