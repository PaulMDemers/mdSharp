namespace MdSharp.Core.Cartridge;

public sealed class CartridgeImage
{
    private readonly byte[] _rom;
    private readonly byte[] _saveRam = new byte[64 * 1024];
    private readonly byte[] _bankRegisters = new byte[8];
    private readonly SerialEeprom? _eeprom;
    private readonly bool _hasSaveRam;
    private readonly uint _saveRamStart;
    private readonly uint _saveRamEnd;
    private readonly SaveRamLanes _saveRamLanes;
    private readonly string? _sourceName;
    private bool _fallbackSaveRamActive;

    private CartridgeImage(byte[] rom, string? sourceName = null)
    {
        _rom = rom;
        _sourceName = sourceName;
        Header = CartridgeHeader.Parse(_rom);
        _eeprom = TryCreateEeprom(_rom, _saveRam, Header);
        if (_eeprom is not null)
        {
            Array.Fill<byte>(_saveRam, 0xFF, 0, Math.Min(_eeprom.MemorySize, _saveRam.Length));
        }

        _hasSaveRam = _eeprom is null && TryGetSaveRamRange(_rom, out _saveRamStart, out _saveRamEnd, out _saveRamLanes);
        SaveRamEnabled = !_hasSaveRam || (uint)_rom.Length <= _saveRamStart || _saveRamStart == _saveRamEnd;
        Diagnostics = BuildDiagnostics();
    }

    public CartridgeHeader Header { get; }
    public CartridgeDiagnostics Diagnostics { get; }
    public int Length => _rom.Length;
    public ReadOnlyMemory<byte> Rom => _rom;
    public ReadOnlySpan<byte> SaveRam => _saveRam;
    public ReadOnlySpan<byte> BankRegisters => _bankRegisters;
    public bool SaveRamEnabled { get; set; } = true;
    public bool BankSwitchingEnabled { get; set; }
    public bool FallbackSaveRamActive => _fallbackSaveRamActive;

    public static CartridgeImage FromFile(string path)
    {
        return new CartridgeImage(Normalize(File.ReadAllBytes(path)), Path.GetFileNameWithoutExtension(path));
    }

    public static CartridgeImage FromBytes(byte[] bytes)
    {
        return new CartridgeImage(Normalize(bytes));
    }

    public byte ReadByte(uint address)
    {
        if (_eeprom is not null && _eeprom.HandlesAddress(address))
        {
            return _eeprom.ReadByte(address);
        }

        if (SaveRamEnabled && TryGetSaveRamIndex(address, activateFallback: false, out int saveRamIndex))
        {
            return _saveRam[saveRamIndex];
        }

        if (SaveRamEnabled && IsDeclaredSaveRamWindow(address))
        {
            return 0xFF;
        }

        if (_rom.Length == 0)
        {
            return 0xFF;
        }

        uint mapped = MapRomAddress(address);
        return _rom[mapped];
    }

    public ushort ReadWord(uint address)
    {
        if (_eeprom is not null && (_eeprom.HandlesAddress(address) || _eeprom.HandlesAddress(address + 1)))
        {
            return (ushort)((ReadByte(address) << 8) | ReadByte(address + 1));
        }

        if ((SaveRamEnabled && (TryGetSaveRamIndex(address, activateFallback: false, out _) || TryGetSaveRamIndex(address + 1, activateFallback: false, out _))) ||
            (SaveRamEnabled && (IsDeclaredSaveRamWindow(address) || IsDeclaredSaveRamWindow(address + 1))))
        {
            return (ushort)((ReadByte(address) << 8) | ReadByte(address + 1));
        }

        if (_rom.Length == 0)
        {
            return 0xFFFF;
        }

        uint high = MapRomAddress(address);
        uint low = MapRomAddress(address + 1);
        return (ushort)((_rom[high] << 8) | _rom[low]);
    }

    public uint ReadLong(uint address)
    {
        return (uint)((ReadWord(address) << 16) | ReadWord(address + 2));
    }

    public bool TryWriteByte(uint address, byte value)
    {
        if (_eeprom is not null && _eeprom.HandlesAddress(address))
        {
            _eeprom.WriteByte(address, value);
            return true;
        }

        if (_hasSaveRam && address == 0xA1_30F1)
        {
            SaveRamEnabled = (value & 0x01) != 0;
            return true;
        }

        if (address is >= 0xA1_3000 and <= 0xA1_300F)
        {
            int bank = (int)((address - 0xA1_3000) & 0x07);
            _bankRegisters[bank] = value;
            BankSwitchingEnabled = true;
            return true;
        }

        if (address is >= 0xA1_30F1 and <= 0xA1_30FF && (address & 1) != 0)
        {
            int slot = (int)((address - 0xA1_30F1) >> 1);
            if (slot > 0 && slot <= _bankRegisters.Length)
            {
                _bankRegisters[slot - 1] = value;
                BankSwitchingEnabled = true;
            }

            return true;
        }

        if (!SaveRamEnabled)
        {
            return false;
        }

        if (TryGetSaveRamIndex(address, activateFallback: true, out int saveRamIndex))
        {
            _saveRam[saveRamIndex] = value;
            return true;
        }

        if (IsDeclaredSaveRamWindow(address))
        {
            return true;
        }

        return false;
    }

    public bool TryWriteWord(uint address, ushort value)
    {
        if (_eeprom is not null && (_eeprom.HandlesAddress(address) || _eeprom.HandlesAddress(address + 1)))
        {
            _eeprom.WriteWord(address, value);
            return true;
        }

        return false;
    }

    public byte[] CaptureSaveRam()
    {
        return (byte[])_saveRam.Clone();
    }

    public void RestoreSaveRam(byte[] data, bool fallbackActive = false)
    {
        Array.Clear(_saveRam);
        Array.Copy(data, _saveRam, Math.Min(data.Length, _saveRam.Length));
        _fallbackSaveRamActive = !_hasSaveRam && fallbackActive;
    }

    public byte[] CaptureBankRegisters()
    {
        return (byte[])_bankRegisters.Clone();
    }

    public void RestoreBankRegisters(byte[] data, bool enabled)
    {
        Array.Clear(_bankRegisters);
        Array.Copy(data, _bankRegisters, Math.Min(data.Length, _bankRegisters.Length));
        BankSwitchingEnabled = enabled;
    }

    private uint MapRomAddress(uint address)
    {
        if (!BankSwitchingEnabled || address < 0x08_0000 || _rom.Length <= 0x08_0000)
        {
            return address % (uint)_rom.Length;
        }

        int slot = (int)((address - 0x08_0000) / 0x08_0000);
        if ((uint)slot >= _bankRegisters.Length)
        {
            return address % (uint)_rom.Length;
        }

        uint bankBase = (uint)_bankRegisters[slot] * 0x08_0000u;
        return (bankBase + (address & 0x07_FFFFu)) % (uint)_rom.Length;
    }

    private CartridgeDiagnostics BuildDiagnostics()
    {
        List<string> warnings = [];
        List<string> unsupported = [];

        if (Header.RomEnd != 0 && Header.RomEnd + 1 != (uint)_rom.Length)
        {
            warnings.Add($"Header ROM end ${Header.RomEnd:X6} does not match normalized ROM size ${_rom.Length:X6}.");
        }

        string console = Header.ConsoleName.ToUpperInvariant();
        string product = Header.ProductCode.ToUpperInvariant();
        string domestic = Header.DomesticName.ToUpperInvariant();
        string overseas = Header.OverseasName.ToUpperInvariant();
        string sourceName = (_sourceName ?? string.Empty).ToUpperInvariant();
        if (console.Contains("32X", StringComparison.Ordinal) ||
            console.Contains("MARS", StringComparison.Ordinal) ||
            product.Contains("32X", StringComparison.Ordinal) ||
            domestic.Contains("32X", StringComparison.Ordinal) ||
            overseas.Contains("32X", StringComparison.Ordinal))
        {
            unsupported.Add("Sega 32X hardware");
        }

        bool hasSvp = LooksLikeSvp(product, domestic, overseas);
        if (hasSvp)
        {
            warnings.Add("SVP coprocessor enabled for Virtua Racing.");
        }

        bool hasJCart = LooksLikeJCart(product, domestic, overseas, sourceName);
        if (hasJCart)
        {
            warnings.Add("J-Cart extra controller ports are mapped as players 3 and 4.");
        }

        if (LooksLikeLightGunTitle(domestic, overseas))
        {
            warnings.Add("Light gun support is available through Menacer/Justifier on port 2; HV hit timing is approximate.");
        }

        if (_rom.Length > 0x40_0000 && _rom.Length <= 0x50_0000)
        {
            warnings.Add("ROM is larger than the 4 MiB linear cartridge window; bank-switch registers are expected.");
        }
        else if (_rom.Length > 0x50_0000)
        {
            warnings.Add("ROM is larger than known mdSharp mapper coverage.");
        }

        string saveHardware = _eeprom is not null ? "serial EEPROM" : _hasSaveRam ? "SRAM" : "none";
        return new CartridgeDiagnostics(
            _rom.Length,
            Header.RomStart,
            Header.RomEnd,
            saveHardware,
            _hasSaveRam ? _saveRamStart : null,
            _hasSaveRam ? _saveRamEnd : null,
            _hasSaveRam ? FormatSaveRamLanes(_saveRamLanes) : "none",
            _eeprom?.MemorySize,
            _rom.Length > 0x40_0000,
            hasJCart,
            hasSvp,
            unsupported.ToArray(),
            warnings.ToArray());
    }

    private bool TryGetSaveRamIndex(uint address, bool activateFallback, out int index)
    {
        if (_hasSaveRam)
        {
            if (address >= _saveRamStart && address <= _saveRamEnd)
            {
                if (!SaveRamLaneMatches(address))
                {
                    index = 0;
                    return false;
                }

                index = GetDeclaredSaveRamIndex(address);
                return index < _saveRam.Length;
            }

            index = 0;
            return false;
        }

        if (address is >= 0x20_0000 and <= 0x20_FFFF)
        {
            if (activateFallback)
            {
                _fallbackSaveRamActive = true;
            }

            if (_fallbackSaveRamActive)
            {
                index = (int)((address - 0x20_0000) & 0xFFFF);
                return true;
            }
        }

        index = 0;
        return false;
    }

    private bool IsDeclaredSaveRamWindow(uint address)
    {
        return _hasSaveRam && address >= _saveRamStart && address <= _saveRamEnd;
    }

    private bool SaveRamLaneMatches(uint address)
    {
        SaveRamLanes lane = (address & 1) == 0 ? SaveRamLanes.Even : SaveRamLanes.Odd;
        return (_saveRamLanes & lane) != 0;
    }

    private int GetDeclaredSaveRamIndex(uint address)
    {
        uint offset = address - _saveRamStart;
        if (_saveRamLanes != SaveRamLanes.Both)
        {
            offset >>= 1;
        }

        return (int)(offset & 0xFFFF);
    }

    private static byte[] Normalize(byte[] bytes)
    {
        if (LooksLikeSmd(bytes))
        {
            return DeinterleaveSmd(bytes);
        }

        return bytes;
    }

    private static bool TryGetSaveRamRange(ReadOnlySpan<byte> rom, out uint start, out uint end, out SaveRamLanes lanes)
    {
        start = 0;
        end = 0;
        lanes = SaveRamLanes.Both;
        if (rom.Length < 0x1BC || rom[0x1B0] != (byte)'R' || rom[0x1B1] != (byte)'A')
        {
            return false;
        }

        start = ReadUInt32(rom, 0x1B4);
        end = ReadUInt32(rom, 0x1B8);
        lanes = (rom[0x1B3] & 0x60) switch
        {
            0x20 => SaveRamLanes.Odd,
            0x40 => SaveRamLanes.Even,
            _ => SaveRamLanes.Both,
        };
        if (start == end && !AddressMatchesSaveRamLane(start, lanes))
        {
            lanes = (start & 1) == 0 ? SaveRamLanes.Even : SaveRamLanes.Odd;
        }

        return start <= end && start >= 0x20_0000 && end <= 0x3F_FFFF;
    }

    private static SerialEeprom? TryCreateEeprom(ReadOnlySpan<byte> rom, byte[] backing, CartridgeHeader header)
    {
        if (rom.Length >= 0x1BC && rom[0x1B0] == (byte)'R' && rom[0x1B1] == (byte)'A' && rom[0x1B2] == 0xE8 && rom[0x1B3] == 0x40)
        {
            uint start = ReadUInt32(rom, 0x1B4);
            uint end = ReadUInt32(rom, 0x1B8);
            if (start == 0x20_0001 && end == 0x20_0001)
            {
                return CreateSegaMode1Eeprom(backing);
            }
        }

        string product = header.ProductCode.ToUpperInvariant();
        string domestic = header.DomesticName.ToUpperInvariant();
        string overseas = header.OverseasName.ToUpperInvariant();
        if (product.Contains("00001211") || domestic.Contains("SPORTS TALK BASEBALL") || overseas.Contains("SPORTS TALK BASEBALL"))
        {
            return CreateSegaMode1Eeprom(backing);
        }

        if (product.Contains("T-50396") || product.Contains("T-50176"))
        {
            return new SerialEeprom(backing, sizeMask: 0x7F, sdaInAddress: 0x20_0001, sdaInBit: 7, sdaOutAddress: 0x20_0001, sdaOutBit: 7, sclAddress: 0x20_0001, sclBit: 6);
        }

        if (product.Contains("T-081326") || product.Contains("T-81033"))
        {
            return new SerialEeprom(backing, sizeMask: 0xFF, sdaInAddress: 0x20_0001, sdaInBit: 0, sdaOutAddress: 0x20_0001, sdaOutBit: 1, sclAddress: 0x20_0001, sclBit: 1, addressBytes: 1, commandAddressBits: 3);
        }

        if (product.Contains("T-81406") || product.Contains("T-81143"))
        {
            return new SerialEeprom(backing, sizeMask: 0xFF, sdaInAddress: 0x20_0001, sdaInBit: 0, sdaOutAddress: 0x20_0001, sdaOutBit: 0, sclAddress: 0x20_0000, sclBit: 0, addressBytes: 1, commandAddressBits: 3);
        }

        if (product.Contains("T-081276"))
        {
            return new SerialEeprom(backing, sizeMask: 0xFF, sdaInAddress: 0x20_0001, sdaInBit: 0, sdaOutAddress: 0x20_0001, sdaOutBit: 0, sclAddress: 0x20_0000, sclBit: 0, addressBytes: 1, commandAddressBits: 3);
        }

        if (product.Contains("T-081586"))
        {
            return new SerialEeprom(backing, sizeMask: 0x7FF, sdaInAddress: 0x20_0001, sdaInBit: 0, sdaOutAddress: 0x20_0001, sdaOutBit: 0, sclAddress: 0x20_0000, sclBit: 0, addressBytes: 1, commandAddressBits: 3, pageMask: 0x07);
        }

        if (product.Contains("T-81576") || product.Contains("T-81476"))
        {
            return new SerialEeprom(backing, sizeMask: 0x1FFF, sdaInAddress: 0x20_0001, sdaInBit: 0, sdaOutAddress: 0x20_0001, sdaOutBit: 0, sclAddress: 0x20_0000, sclBit: 0, addressBytes: 2, commandAddressBits: 0, pageMask: 0x07);
        }

        if (product.Contains("T-120096") || domestic.Contains("MICRO MACHINES 2") || overseas.Contains("MICRO MACHINES 2") ||
            domestic.Contains("MICRO MACHINES MILITARY") || overseas.Contains("MICRO MACHINES MILITARY"))
        {
            return new SerialEeprom(backing, sizeMask: 0x3FF, sdaInAddress: 0x30_0000, sdaInBit: 0, sdaOutAddress: 0x38_0001, sdaOutBit: 7, sclAddress: 0x30_0000, sclBit: 1, addressBytes: 1, commandAddressBits: 3, pageMask: 0x0F);
        }

        if (domestic.Contains("MICRO MACHINES TURBO") || overseas.Contains("MICRO MACHINES TURBO"))
        {
            return new SerialEeprom(backing, sizeMask: 0x7FF, sdaInAddress: 0x30_0000, sdaInBit: 0, sdaOutAddress: 0x38_0001, sdaOutBit: 7, sclAddress: 0x30_0000, sclBit: 1, addressBytes: 1, commandAddressBits: 3, pageMask: 0x0F);
        }

        if (product.Contains("T-120146") || domestic.Contains("BRIAN LARA CRICKET 96") || overseas.Contains("BRIAN LARA CRICKET 96"))
        {
            return new SerialEeprom(backing, sizeMask: 0x1FFF, sdaInAddress: 0x30_0000, sdaInBit: 0, sdaOutAddress: 0x38_0001, sdaOutBit: 7, sclAddress: 0x30_0000, sclBit: 1, addressBytes: 2, commandAddressBits: 0, pageMask: 0x0F);
        }

        return null;
    }

    private static SerialEeprom CreateSegaMode1Eeprom(byte[] backing)
    {
        // Common 24C01 wiring used by Evander Holyfield, Greatest Heavyweights,
        // Wonder Boy in Monster World, Sports Talk Baseball, and similar titles.
        return new SerialEeprom(backing, sizeMask: 0x7F, sdaInAddress: 0x20_0001, sdaInBit: 0, sdaOutAddress: 0x20_0001, sdaOutBit: 0, sclAddress: 0x20_0001, sclBit: 1);
    }

    private static bool AddressMatchesSaveRamLane(uint address, SaveRamLanes lanes)
    {
        SaveRamLanes lane = (address & 1) == 0 ? SaveRamLanes.Even : SaveRamLanes.Odd;
        return (lanes & lane) != 0;
    }

    private static string FormatSaveRamLanes(SaveRamLanes lanes)
    {
        return lanes switch
        {
            SaveRamLanes.Odd => "odd",
            SaveRamLanes.Even => "even",
            SaveRamLanes.Both => "both",
            _ => "none",
        };
    }

    private static bool LooksLikeJCart(string product, string domestic, string overseas, string sourceName)
    {
        return product.Contains("T-120096", StringComparison.Ordinal) ||
            product.Contains("T-120066", StringComparison.Ordinal) ||
            product.Contains("T-123456", StringComparison.Ordinal) ||
            sourceName.Contains("J-CART", StringComparison.Ordinal) ||
            sourceName.Contains("J CART", StringComparison.Ordinal) ||
            HasTitle(domestic, overseas, "MICRO MACHINES II") ||
            HasTitle(domestic, overseas, "MICRO MACHINES 2") ||
            HasTitle(domestic, overseas, "MICRO MACHINES TURBO") ||
            HasTitle(domestic, overseas, "MICRO MACHINES MILITARY") ||
            HasTitle(domestic, overseas, "PETE SAMPRAS TENNIS") ||
            HasTitle(domestic, overseas, "SAMPRAS TENNIS") ||
            HasTitle(domestic, overseas, "SUPER SKIDMARKS");
    }

    private static bool LooksLikeLightGunTitle(string domestic, string overseas)
    {
        return HasTitle(domestic, overseas, "LETHAL ENFORCERS") ||
            HasTitle(domestic, overseas, "BODY COUNT") ||
            HasTitle(domestic, overseas, "T2 THE ARCADE GAME") ||
            HasTitle(domestic, overseas, "TERMINATOR 2 THE ARCADE GAME") ||
            HasTitle(domestic, overseas, "MENACER");
    }

    private static bool LooksLikeSvp(string product, string domestic, string overseas)
    {
        return product.Contains("G-7001", StringComparison.Ordinal) ||
            product.Contains("MK-1229", StringComparison.Ordinal) ||
            HasTitle(domestic, overseas, "VIRTUA RACING");
    }

    private static bool HasTitle(string domestic, string overseas, string value)
    {
        return domestic.Contains(value, StringComparison.Ordinal) ||
            overseas.Contains(value, StringComparison.Ordinal);
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset)
    {
        return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
    }

    private static bool LooksLikeSmd(byte[] bytes)
    {
        if (bytes.Length < 0x4200 || (bytes.Length - 0x200) % 0x4000 != 0)
        {
            return false;
        }

        int headerOffset = 0x200 + 0x100;
        if (headerOffset + 4 >= bytes.Length)
        {
            return false;
        }

        return bytes[headerOffset] == (byte)'S'
            && bytes[headerOffset + 1] == (byte)'E'
            && bytes[headerOffset + 2] == (byte)'G'
            && bytes[headerOffset + 3] == (byte)'A';
    }

    private static byte[] DeinterleaveSmd(byte[] bytes)
    {
        byte[] output = new byte[bytes.Length - 0x200];
        int outputOffset = 0;

        for (int block = 0x200; block < bytes.Length; block += 0x4000)
        {
            for (int i = 0; i < 0x2000; i++)
            {
                output[outputOffset + (i * 2)] = bytes[block + 0x2000 + i];
                output[outputOffset + (i * 2) + 1] = bytes[block + i];
            }

            outputOffset += 0x4000;
        }

        return output;
    }

    [Flags]
    private enum SaveRamLanes
    {
        None = 0,
        Odd = 1,
        Even = 2,
        Both = Odd | Even,
    }
}
