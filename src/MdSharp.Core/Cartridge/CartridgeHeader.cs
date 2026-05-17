using System.Text;

namespace MdSharp.Core.Cartridge;

public sealed record CartridgeHeader(
    string ConsoleName,
    string DomesticName,
    string OverseasName,
    string ProductCode,
    uint RomStart,
    uint RomEnd,
    uint RamStart,
    uint RamEnd,
    string Region)
{
    public bool PrefersPal
    {
        get
        {
            string region = Region.ToUpperInvariant();
            return region.Contains('8') || (region.Contains('E') && !region.Contains('U'));
        }
    }

    public static CartridgeHeader Parse(ReadOnlySpan<byte> rom)
    {
        if (rom.Length < 0x200)
        {
            return new CartridgeHeader(string.Empty, string.Empty, string.Empty, string.Empty, 0, (uint)Math.Max(rom.Length - 1, 0), 0, 0, string.Empty);
        }

        return new CartridgeHeader(
            ReadAscii(rom, 0x100, 16),
            ReadAscii(rom, 0x120, 48),
            ReadAscii(rom, 0x150, 48),
            ReadAscii(rom, 0x180, 14),
            ReadUInt32(rom, 0x1A0),
            ReadUInt32(rom, 0x1A4),
            ReadUInt32(rom, 0x1B4),
            ReadUInt32(rom, 0x1B8),
            ReadAscii(rom, 0x1F0, 16));
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset)
    {
        return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
    }

    private static string ReadAscii(ReadOnlySpan<byte> data, int offset, int length)
    {
        return Encoding.ASCII.GetString(data.Slice(offset, length)).TrimEnd('\0', ' ');
    }
}
