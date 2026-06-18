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
    public const uint MainRegisterStart = 0xA12000;
    public const uint MainRegisterEndInclusive = MainRegisterStart + RegisterBytes - 1;
}
