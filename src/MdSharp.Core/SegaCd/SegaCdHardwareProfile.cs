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
    public const int SubCpuClockHz = 12_500_000;
    public const uint MainBiosStart = 0x000000;
    public const uint MainBiosEndInclusive = MainBiosStart + BiosSize - 1;
    public const uint MainProgramRamWindowStart = 0x020000;
    public const uint MainProgramRamWindowEndInclusive = 0x03FFFF;
    public const uint MainProgramRamMirrorLowStart = 0x020000;
    public const uint MainProgramRamMirrorLowEndInclusive = 0x1FFFFF;
    public const uint MainProgramRamMirrorHighStart = 0x400000;
    public const uint MainProgramRamVisibleBankBytes = 128 * 1024;
    public const uint MainProgramRamMirrorHighEndInclusive = 0x5FFFFF;
    public const uint MainWordRamStart = 0x200000;
    public const uint MainWordRamEndInclusive = 0x3FFFFF;
    public const uint MainWordRamHighAliasStart = 0x400000;
    public const uint MainWordRamHighAliasEndInclusive = MainWordRamHighAliasStart + (WordRamBytes / 2u) - 1;
    public const uint MainWordRamAliasStart = 0x600000;
    public const uint MainWordRamAliasEndInclusive = 0x7FFFFF;
    public const uint MainRegisterStart = 0xA12000;
    public const uint MainRegisterEndInclusive = MainRegisterStart + RegisterBytes - 1;
    public const uint SubProgramRamStart = 0x000000;
    public const uint SubProgramRamEndInclusive = SubProgramRamStart + ProgramRamBytes - 1;
    public const uint SubWordRamStart = 0x080000;
    public const uint SubWordRamEndInclusive = SubWordRamStart + WordRamBytes - 1;
    public const uint SubWordRam1MStart = 0x0C0000;
    public const uint SubWordRam1MEndInclusive = SubWordRam1MStart + (WordRamBytes / 2) - 1;
    public const uint SubBackupRamStart = 0xFE0000;
    public const uint SubBackupRamEndInclusive = SubBackupRamStart + BackupRamBytes - 1;
    public const uint SubPcmRamStart = 0xFF0000;
    public const uint SubPcmRamEndInclusive = SubPcmRamStart + PcmRamBytes - 1;
    public const uint SubRegisterStart = 0xFF8000;
    public const uint SubRegisterEndInclusive = SubRegisterStart + RegisterBytes - 1;
}
