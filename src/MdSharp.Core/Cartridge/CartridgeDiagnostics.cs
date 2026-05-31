namespace MdSharp.Core.Cartridge;

public sealed record CartridgeDiagnostics(
    int RomSize,
    uint HeaderRomStart,
    uint HeaderRomEnd,
    string SaveHardware,
    uint? SaveRamStart,
    uint? SaveRamEnd,
    string SaveRamLanes,
    int? EepromSize,
    bool UsesBankSwitchRegisters,
    bool HasJCart,
    bool HasSvp,
    bool Requires32X,
    string[] UnsupportedHardware,
    string[] Warnings)
{
    public bool HasSaveHardware => SaveHardware != "none";
    public bool HasUnsupportedHardware => UnsupportedHardware.Length > 0;
}
