using MdSharp.Core.Audio;
using MdSharp.Core.Cartridge;
using MdSharp.Core.Cpu.Z80;
using MdSharp.Core.Input;
using MdSharp.Core.ThirtyTwoX;
using MdSharp.Core.Video;

namespace MdSharp.Core.Bus;

public sealed class GenesisBus : IMemoryBus, IInstructionTraceSink, IZ80Bus
{
    private const long Z80BusGrantDelayMasterCycles = 64;
    private const int M68kMasterDivider = 7;
    private const uint ThirtyTwoXVectorRomBytes = 0x100;
    private const uint ThirtyTwoXVectorRomHelperStart = 0xC0;
    private const uint ThirtyTwoXTrap15ReturnHelper = ThirtyTwoXVectorRomHelperStart + 0x3C;
    private const uint ThirtyTwoXVectorJumpTableStart = 0x88_0200;
    private const uint ThirtyTwoXVectorJumpTableStride = 6;
    private const int ThirtyTwoXCommunicationReadSyncCycles = 32;
    private const int ThirtyTwoXCommunicationWriteSyncCycles = 4096;
    private static readonly byte[] ThirtyTwoXVectorRomHelpers =
    [
        0x08, 0xF9, 0x00, 0x00, 0x00, 0xA1, 0x51, 0x07,
        0x12, 0x80,
        0x08, 0xB9, 0x00, 0x00, 0x00, 0xA1, 0x51, 0x07,
        0x4E, 0x75,
        0x48, 0xE7, 0x01, 0x40,
        0x08, 0xF9, 0x00, 0x00, 0x00, 0xA1, 0x51, 0x07,
        0x43, 0xF9, 0x00, 0xA1, 0x30, 0xF1,
        0x7E, 0x07,
        0x12, 0x98,
        0xD0, 0xFC, 0x00, 0x02,
        0x51, 0xCF, 0xFF, 0xF8,
        0x08, 0xB9, 0x00, 0x00, 0x00, 0xA1, 0x51, 0x07,
        0x4C, 0xDF, 0x02, 0x80,
        0x4E, 0x75
    ];
    private const int Z80AreaM68kWaitCycles = 2;
    private const int Ym2612M68kWaitCycles = 2;
    private const int IoM68kWaitCycles = 2;
    private const int VdpPortM68kWaitCycles = 2;
    private const int CartridgeHardwareM68kWaitCycles = 2;

    private readonly byte[] _workRam = new byte[64 * 1024];
    private readonly byte[] _z80Ram = new byte[8 * 1024];
    private readonly byte[] _tmss = new byte[4];
    private readonly ThreeButtonController[] _controllers;
    private readonly SegaTeamPlayerAdapter _teamPlayer;
    private readonly SvpDevice? _svp;
    private readonly ThirtyTwoXDevice? _thirtyTwoX;
    private readonly byte[]? _thirtyTwoXM68kBios;
    private readonly byte[] _ioData = { 0x40, 0x40, 0x40 };
    private readonly byte[] _ioControl = new byte[3];
    private readonly byte _versionRegister;

    private bool _z80BusRequested;
    private bool _z80ResetAsserted = true;
    private long _z80BusGrantReadyCycle;
    private int _z80BankRegister;
    private byte _ea4WayPlayLatch;
    private byte _justifierState = 0x40;
    private ushort? _lightGunHvLatch;
    private bool _lightGunHvLatchForced;
    private long _svpLastMasterCycle;
    private int _svpClockRemainder;
    private int _pendingM68kWaitCycles;
    private bool _dmaActive;
    private int _lightGunX = Vdp.ScreenWidth / 2;
    private int _lightGunY = Vdp.ScreenHeight / 2;
    private bool _lightGunVisible;

    public GenesisBus(CartridgeImage cartridge, Vdp vdp, Psg psg, Ym2612 ym2612, bool pal = false, ThreeButtonController? controller1 = null, ThreeButtonController? controller2 = null, ThreeButtonController? controller3 = null, ThreeButtonController? controller4 = null, ReadOnlyMemory<byte>? thirtyTwoXM68kBios = null)
    {
        Cartridge = cartridge;
        Vdp = vdp;
        Psg = psg;
        Ym2612 = ym2612;
        _versionRegister = BuildVersionRegister(cartridge, pal);
        _controllers = new[]
        {
            controller1 ?? new ThreeButtonController(),
            controller2 ?? new ThreeButtonController(),
            controller3 ?? new ThreeButtonController(),
            controller4 ?? new ThreeButtonController(),
        };
        _teamPlayer = new SegaTeamPlayerAdapter(_controllers);
        _svp = cartridge.Diagnostics.HasSvp ? new SvpDevice(cartridge.Rom) : null;
        _thirtyTwoX = cartridge.Diagnostics.Requires32X ? new ThirtyTwoXDevice(cartridge.Rom, pal) : null;
        if (_thirtyTwoX is not null && thirtyTwoXM68kBios.HasValue && !thirtyTwoXM68kBios.Value.IsEmpty)
        {
            _thirtyTwoXM68kBios = thirtyTwoXM68kBios.Value.ToArray();
        }
    }

    public CartridgeImage Cartridge { get; }
    public Vdp Vdp { get; }
    public Psg Psg { get; }
    public Ym2612 Ym2612 { get; }
    public SvpDevice? Svp => _svp;
    public ThirtyTwoXDevice? ThirtyTwoX => _thirtyTwoX;
    public bool StepSvpDuringDma { get; set; } = true;
    public ReadOnlySpan<byte> WorkRam => _workRam;
    public ReadOnlySpan<byte> Z80Ram => _z80Ram;
    public ReadOnlySpan<byte> TmssRegister => _tmss;
    public ThreeButtonController Controller1 => _controllers[0];
    public ThreeButtonController Controller2 => _controllers[1];
    public ThreeButtonController Controller3 => _controllers[2];
    public ThreeButtonController Controller4 => _controllers[3];
    public ControllerPortDevice Port1Device { get; set; } = ControllerPortDevice.Gamepad;
    public ControllerPortDevice Port2Device { get; set; } = ControllerPortDevice.Gamepad;
    public int LightGunX => _lightGunX;
    public int LightGunY => _lightGunY;
    public bool LightGunVisible => _lightGunVisible;
    public bool LightGunLatchedThisFrame => _lightGunHvLatch.HasValue;
    public bool Z80BusRequested => _z80BusRequested;
    public bool Z80BusGranted => _z80BusRequested && !_z80ResetAsserted && CurrentMasterCycle >= _z80BusGrantReadyCycle;
    public bool Z80ResetAsserted => _z80ResetAsserted;
    public uint CurrentM68kPc { get; set; }
    public ushort CurrentZ80Pc { get; set; }
    public long CurrentMasterCycle { get; set; }
    public int CurrentScanlineMasterCycleOffset { get; set; }
    public int MasterCyclesPerScanline { get; set; } = 1;
    public Action<MemoryRead>? MemoryReadObserver { get; set; }
    public Action<MemoryWrite>? MemoryWriteObserver { get; set; }
    public Action<IoAccess>? IoObserver { get; set; }
    public Action<AudioAccess>? AudioObserver { get; set; }
    public Action<Z80ControlAccess>? Z80ControlObserver { get; set; }
    public Action<DmaWordTransfer>? DmaWordObserver { get; set; }
    public Action<SvpExternalAccess>? SvpExternalObserver { get; set; }

    public byte ReadByte(uint address)
    {
        address &= 0x00FF_FFFF;

        if (TryReadThirtyTwoXVectorRomByte(address, out byte vectorValue))
        {
            return vectorValue;
        }

        if (IsThirtyTwoXLowCartridgeRomBlocked(address))
        {
            return 0xFF;
        }

        if (address <= 0x3F_FFFF)
        {
            if (IsJCartControllerWindow(address))
            {
                return ReadJCartByte(address);
            }

            if (_svp is not null && IsSvpMappedAddress(address))
            {
                SyncSvpToCurrentMasterCycle();
                byte value = _svp.ReadByte(address);
                SvpExternalObserver?.Invoke(new SvpExternalAccess(CurrentMasterCycle, CurrentM68kPc, _dmaActive, false, address, 1, value));
                return value;
            }

            byte cartridgeValue = Cartridge.ReadByte(address);
            MemoryReadObserver?.Invoke(new MemoryRead(CurrentM68kPc, address, 1, cartridgeValue));
            return cartridgeValue;
        }

        if (_thirtyTwoX is not null && TryReadThirtyTwoXByte(address, out byte thirtyTwoXValue))
        {
            AddM68kWaitCycles(CartridgeHardwareM68kWaitCycles);
            MemoryReadObserver?.Invoke(new MemoryRead(CurrentM68kPc, address, 1, thirtyTwoXValue));
            return thirtyTwoXValue;
        }

        if (address is >= 0xA0_0000 and <= 0xA0_1FFF)
        {
            AddM68kWaitCycles(Z80AreaM68kWaitCycles);
            return _z80Ram[address & 0x1FFF];
        }

        if (address is >= 0xA0_4000 and <= 0xA0_4003)
        {
            AddM68kWaitCycles(Ym2612M68kWaitCycles);
            return Ym2612.ReadStatus(CurrentMasterCycle);
        }

        if (address is >= 0xA1_0000 and <= 0xA1_00FF)
        {
            AddM68kWaitCycles(IoM68kWaitCycles);
            return ReadIo(address);
        }

        if (address is >= 0xA1_1100 and <= 0xA1_1101)
        {
            AddM68kWaitCycles(IoM68kWaitCycles);
            return ReadZ80BusRequestStatus();
        }

        if (address is >= 0xA1_1200 and <= 0xA1_1201)
        {
            AddM68kWaitCycles(IoM68kWaitCycles);
            return (byte)(_z80ResetAsserted ? 0x00 : 0x01);
        }

        if (address is >= 0xA1_4000 and <= 0xA1_4003)
        {
            AddM68kWaitCycles(IoM68kWaitCycles);
            return _tmss[address & 0x03];
        }

        if (_thirtyTwoX is not null && TryReadThirtyTwoXByte(address, out thirtyTwoXValue))
        {
            AddM68kWaitCycles(CartridgeHardwareM68kWaitCycles);
            MemoryReadObserver?.Invoke(new MemoryRead(CurrentM68kPc, address, 1, thirtyTwoXValue));
            return thirtyTwoXValue;
        }

        if (_svp is not null && address is >= 0xA1_5000 and <= 0xA1_5009)
        {
            SyncSvpToCurrentMasterCycle();
            byte value = _svp.ReadByte(address);
            SvpExternalObserver?.Invoke(new SvpExternalAccess(CurrentMasterCycle, CurrentM68kPc, _dmaActive, false, address, 1, value));
            return value;
        }

        if (address is >= 0xC0_0000 and <= 0xC0_001F)
        {
            AddM68kWaitCycles(VdpPortM68kWaitCycles);
            return ReadVdpByte(address);
        }

        if (address >= 0xE0_0000)
        {
            uint ramAddress = address & 0xFFFF;
            byte value = _workRam[ramAddress];
            MemoryReadObserver?.Invoke(new MemoryRead(CurrentM68kPc, 0x00FF_0000 | ramAddress, 1, value));
            return value;
        }

        return 0xFF;
    }

    public void WriteByte(uint address, byte value)
    {
        address &= 0x00FF_FFFF;

        if (address is >= 0xA0_0000 and <= 0xA0_1FFF)
        {
            AddM68kWaitCycles(Z80AreaM68kWaitCycles);
            WriteZ80RamByte((int)(address & 0x1FFF), value, CurrentM68kPc);
            return;
        }

        if (IsJCartControllerWindow(address))
        {
            WriteJCart(value);
            return;
        }

        if (_svp is not null && (IsSvpMappedAddress(address) || address is >= 0xA1_5000 and <= 0xA1_5009))
        {
            SyncSvpToCurrentMasterCycle();
            _svp.WriteByte(address, value);
            SvpExternalObserver?.Invoke(new SvpExternalAccess(CurrentMasterCycle, CurrentM68kPc, _dmaActive, true, address, 1, value));
            return;
        }

        if (Cartridge.TryWriteByte(address, value))
        {
            AddM68kWaitCycles(CartridgeHardwareM68kWaitCycles);
            return;
        }

        if (_thirtyTwoX is not null && TryWriteThirtyTwoXByte(address, value))
        {
            AddM68kWaitCycles(CartridgeHardwareM68kWaitCycles);
            MaybeSeedThirtyTwoXSdkCountryBlock();
            return;
        }

        if (address is >= 0xA0_4000 and <= 0xA0_4003)
        {
            AddM68kWaitCycles(Ym2612M68kWaitCycles);
            int port = (int)(address & 0x03);
            if ((port & 1) == 0)
            {
                Ym2612.WriteAddress(port >> 1, value);
                AudioObserver?.Invoke(new AudioAccess(CurrentMasterCycle, CurrentM68kPc, AudioAccessSource.M68k, AudioChip.Ym2612, AudioAccessKind.Address, port >> 1, value, value));
            }
            else
            {
                byte selected = Ym2612.SelectedAddress(port >> 1);
                Ym2612.WriteData(port >> 1, value, CurrentMasterCycle);
                AudioObserver?.Invoke(new AudioAccess(CurrentMasterCycle, CurrentM68kPc, AudioAccessSource.M68k, AudioChip.Ym2612, AudioAccessKind.Data, port >> 1, selected, value));
            }

            return;
        }

        if (address is >= 0xA1_0000 and <= 0xA1_00FF)
        {
            AddM68kWaitCycles(IoM68kWaitCycles);
            WriteIo(address, value);
            return;
        }

        if (address is >= 0xA1_1100 and <= 0xA1_1101)
        {
            AddM68kWaitCycles(IoM68kWaitCycles);
            if ((address & 1) == 0)
            {
                bool request = (value & 0x01) != 0;
                if (request && !_z80BusRequested)
                {
                    _z80BusGrantReadyCycle = CurrentMasterCycle + Z80BusGrantDelayMasterCycles;
                }
                else if (!request)
                {
                    _z80BusGrantReadyCycle = 0;
                }

                _z80BusRequested = request;
                Z80ControlObserver?.Invoke(new Z80ControlAccess(CurrentMasterCycle, CurrentM68kPc, Z80ControlKind.BusRequest, value, _z80BusRequested, _z80ResetAsserted));
            }

            return;
        }

        if (address is >= 0xA1_1200 and <= 0xA1_1201)
        {
            AddM68kWaitCycles(IoM68kWaitCycles);
            if ((address & 1) == 0)
            {
                _z80ResetAsserted = (value & 0x01) == 0;
                Z80ControlObserver?.Invoke(new Z80ControlAccess(CurrentMasterCycle, CurrentM68kPc, Z80ControlKind.Reset, value, _z80BusRequested, _z80ResetAsserted));
            }

            return;
        }

        if (address is >= 0xA1_4000 and <= 0xA1_4003)
        {
            AddM68kWaitCycles(IoM68kWaitCycles);
            _tmss[address & 0x03] = value;
            return;
        }

        if (_thirtyTwoX is not null && TryWriteThirtyTwoXByte(address, value))
        {
            AddM68kWaitCycles(CartridgeHardwareM68kWaitCycles);
            MaybeSeedThirtyTwoXSdkCountryBlock();
            return;
        }

        if (address is >= 0xC0_0000 and <= 0xC0_001F)
        {
            AddM68kWaitCycles(VdpPortM68kWaitCycles);
            WriteVdpByte(address, value);
            return;
        }

        if (address >= 0xE0_0000)
        {
            uint ramAddress = address & 0xFFFF;
            _workRam[ramAddress] = value;
            MemoryWriteObserver?.Invoke(new MemoryWrite(CurrentM68kPc, 0x00FF_0000 | ramAddress, value));
        }
    }

    public ushort ReadWord(uint address)
    {
        address &= 0x00FF_FFFF;
        if (TryReadThirtyTwoXVectorRomWord(address, out ushort vectorValue))
        {
            return vectorValue;
        }

        if (IsThirtyTwoXLowCartridgeRomBlocked(address))
        {
            return 0xFFFF;
        }

        if (address <= 0x3F_FFFF)
        {
            if (IsJCartControllerWindow(address) || IsJCartControllerWindow(address + 1))
            {
                return (ushort)((ReadByte(address) << 8) | ReadByte(address + 1));
            }

            if (_svp is not null && IsSvpMappedAddress(address))
            {
                SyncSvpToCurrentMasterCycle();
                ushort value = _svp.ReadWord(address);
                SvpExternalObserver?.Invoke(new SvpExternalAccess(CurrentMasterCycle, CurrentM68kPc, _dmaActive, false, address, 2, value));
                return value;
            }

            ushort cartridgeValue = Cartridge.ReadWord(address);
            MemoryReadObserver?.Invoke(new MemoryRead(CurrentM68kPc, address, 2, cartridgeValue));
            return cartridgeValue;
        }

        if (address is >= 0xA0_0000 and <= 0xA0_1FFE)
        {
            AddM68kWaitCycles(Z80AreaM68kWaitCycles);
            int offset = (int)(address & 0x1FFF);
            return (ushort)((_z80Ram[offset] << 8) | _z80Ram[(offset + 1) & 0x1FFF]);
        }

        if (_thirtyTwoX is not null && TryReadThirtyTwoXWord(address, out ushort thirtyTwoXValue))
        {
            AddM68kWaitCycles(CartridgeHardwareM68kWaitCycles);
            MemoryReadObserver?.Invoke(new MemoryRead(CurrentM68kPc, address, 2, thirtyTwoXValue));
            return thirtyTwoXValue;
        }

        if (address is >= 0xA1_4000 and <= 0xA1_4002)
        {
            AddM68kWaitCycles(IoM68kWaitCycles);
            int offset = (int)(address & 0x03);
            return (ushort)((_tmss[offset] << 8) | _tmss[(offset + 1) & 0x03]);
        }

        if (_thirtyTwoX is not null && TryReadThirtyTwoXWord(address, out thirtyTwoXValue))
        {
            AddM68kWaitCycles(CartridgeHardwareM68kWaitCycles);
            MemoryReadObserver?.Invoke(new MemoryRead(CurrentM68kPc, address, 2, thirtyTwoXValue));
            return thirtyTwoXValue;
        }

        if (_svp is not null && address is >= 0xA1_5000 and <= 0xA1_5009)
        {
            SyncSvpToCurrentMasterCycle();
            ushort value = _svp.ReadWord(address);
            SvpExternalObserver?.Invoke(new SvpExternalAccess(CurrentMasterCycle, CurrentM68kPc, _dmaActive, false, address, 2, value));
            return value;
        }

        if (address is >= 0xC0_0000 and <= 0xC0_001F)
        {
            AddM68kWaitCycles(VdpPortM68kWaitCycles);
            return ReadVdpWord(address);
        }

        if (address >= 0xE0_0000)
        {
            int offset = (int)(address & 0xFFFF);
            ushort value = (ushort)((_workRam[offset] << 8) | _workRam[(offset + 1) & 0xFFFF]);
            MemoryReadObserver?.Invoke(new MemoryRead(CurrentM68kPc, 0x00FF_0000 | (uint)offset, 2, value));
            return value;
        }

        return (ushort)((ReadByte(address) << 8) | ReadByte(address + 1));
    }

    public uint ReadLong(uint address)
    {
        address &= 0x00FF_FFFF;
        if (TryReadThirtyTwoXVectorRomLong(address, out uint vectorValue))
        {
            return vectorValue;
        }

        if (IsThirtyTwoXLowCartridgeRomBlocked(address))
        {
            return 0xFFFF_FFFF;
        }

        if (address <= 0x3F_FFFC)
        {
            if (IsJCartControllerWindow(address) || IsJCartControllerWindow(address + 3))
            {
                return (uint)((ReadWord(address) << 16) | ReadWord(address + 2));
            }

            if (_svp is not null && IsSvpMappedAddress(address))
            {
                SyncSvpToCurrentMasterCycle();
                uint value = (uint)((_svp.ReadWord(address) << 16) | _svp.ReadWord(address + 2));
                SvpExternalObserver?.Invoke(new SvpExternalAccess(CurrentMasterCycle, CurrentM68kPc, _dmaActive, false, address, 4, value));
                return value;
            }

            uint cartridgeValue = Cartridge.ReadLong(address);
            MemoryReadObserver?.Invoke(new MemoryRead(CurrentM68kPc, address, 4, cartridgeValue));
            return cartridgeValue;
        }

        if (address is >= 0xA0_0000 and <= 0xA0_1FFC)
        {
            AddM68kWaitCycles(Z80AreaM68kWaitCycles * 2);
            int offset = (int)(address & 0x1FFF);
            return (uint)((_z80Ram[offset] << 24) | (_z80Ram[(offset + 1) & 0x1FFF] << 16) | (_z80Ram[(offset + 2) & 0x1FFF] << 8) | _z80Ram[(offset + 3) & 0x1FFF]);
        }

        if (_thirtyTwoX is not null && TryReadThirtyTwoXWord(address, out ushort highThirtyTwoXValue) && TryReadThirtyTwoXWord(address + 2, out ushort lowThirtyTwoXValue))
        {
            AddM68kWaitCycles(CartridgeHardwareM68kWaitCycles * 2);
            uint value = (uint)((highThirtyTwoXValue << 16) | lowThirtyTwoXValue);
            MemoryReadObserver?.Invoke(new MemoryRead(CurrentM68kPc, address, 4, value));
            return value;
        }

        if (address >= 0xE0_0000)
        {
            int offset = (int)(address & 0xFFFF);
            uint value = (uint)((_workRam[offset] << 24) | (_workRam[(offset + 1) & 0xFFFF] << 16) | (_workRam[(offset + 2) & 0xFFFF] << 8) | _workRam[(offset + 3) & 0xFFFF]);
            MemoryReadObserver?.Invoke(new MemoryRead(CurrentM68kPc, 0x00FF_0000 | (uint)offset, 4, value));
            return value;
        }

        return (uint)((ReadWord(address) << 16) | ReadWord(address + 2));
    }

    public void WriteWord(uint address, ushort value)
    {
        address &= 0x00FF_FFFF;
        if (address is >= 0xC0_0000 and <= 0xC0_001F)
        {
            AddM68kWaitCycles(VdpPortM68kWaitCycles);
            WriteVdpWord(address, value);
            return;
        }

        if (address is >= 0xA0_0000 and <= 0xA0_1FFE)
        {
            AddM68kWaitCycles(Z80AreaM68kWaitCycles);
            int offset = (int)(address & 0x1FFF);
            WriteZ80RamByte(offset, (byte)(value >> 8), CurrentM68kPc);
            WriteZ80RamByte((offset + 1) & 0x1FFF, (byte)value, CurrentM68kPc);
            return;
        }

        if (IsJCartControllerWindow(address))
        {
            WriteJCart((byte)value);
            return;
        }

        if (_svp is not null && (IsSvpMappedAddress(address) || address is >= 0xA1_5000 and <= 0xA1_5009))
        {
            SyncSvpToCurrentMasterCycle();
            _svp.WriteWord(address, value);
            SvpExternalObserver?.Invoke(new SvpExternalAccess(CurrentMasterCycle, CurrentM68kPc, _dmaActive, true, address, 2, value));
            return;
        }

        if (_thirtyTwoX is not null && TryWriteThirtyTwoXWord(address, value))
        {
            AddM68kWaitCycles(CartridgeHardwareM68kWaitCycles);
            MaybeSeedThirtyTwoXSdkCountryBlock();
            return;
        }

        if (Cartridge.TryWriteWord(address, value))
        {
            AddM68kWaitCycles(CartridgeHardwareM68kWaitCycles);
            return;
        }

        if (address >= 0xE0_0000 && MemoryWriteObserver is null)
        {
            int offset = (int)(address & 0xFFFF);
            _workRam[offset] = (byte)(value >> 8);
            _workRam[(offset + 1) & 0xFFFF] = (byte)value;
            return;
        }

        WriteByte(address, (byte)(value >> 8));
        WriteByte(address + 1, (byte)value);
    }

    public void WriteLong(uint address, uint value)
    {
        address &= 0x00FF_FFFF;
        if (address is >= 0xA0_0000 and <= 0xA0_1FFC)
        {
            AddM68kWaitCycles(Z80AreaM68kWaitCycles * 2);
            int offset = (int)(address & 0x1FFF);
            WriteZ80RamByte(offset, (byte)(value >> 24), CurrentM68kPc);
            WriteZ80RamByte((offset + 1) & 0x1FFF, (byte)(value >> 16), CurrentM68kPc);
            WriteZ80RamByte((offset + 2) & 0x1FFF, (byte)(value >> 8), CurrentM68kPc);
            WriteZ80RamByte((offset + 3) & 0x1FFF, (byte)value, CurrentM68kPc);
            return;
        }

        if (address >= 0xE0_0000 && MemoryWriteObserver is null)
        {
            int offset = (int)(address & 0xFFFF);
            _workRam[offset] = (byte)(value >> 24);
            _workRam[(offset + 1) & 0xFFFF] = (byte)(value >> 16);
            _workRam[(offset + 2) & 0xFFFF] = (byte)(value >> 8);
            _workRam[(offset + 3) & 0xFFFF] = (byte)value;
            return;
        }

        WriteWord(address, (ushort)(value >> 16));
        WriteWord(address + 2, (ushort)value);
    }

    public byte ReadZ80Byte(ushort address)
    {
        if (address <= 0x3FFF)
        {
            return _z80Ram[address & 0x1FFF];
        }

        if (address is >= 0x4000 and <= 0x5FFF)
        {
            return (address & 0x03) <= 0x02 ? Ym2612.ReadStatus(CurrentMasterCycle) : (byte)0xFF;
        }

        if (address >= 0x8000)
        {
            return ReadByte(MapZ80BankedAddress(address));
        }

        return 0xFF;
    }

    public void WriteZ80Byte(ushort address, byte value)
    {
        if (address <= 0x3FFF)
        {
            WriteZ80RamByte(address & 0x1FFF, value, 0);
            return;
        }

        if (address is >= 0x4000 and <= 0x5FFF)
        {
            int port = address & 0x03;
            if ((port & 1) == 0)
            {
                Ym2612.WriteAddress(port >> 1, value);
                AudioObserver?.Invoke(new AudioAccess(CurrentMasterCycle, 0, AudioAccessSource.Z80, AudioChip.Ym2612, AudioAccessKind.Address, port >> 1, value, value));
            }
            else
            {
                byte selected = Ym2612.SelectedAddress(port >> 1);
                Ym2612.WriteData(port >> 1, value, CurrentMasterCycle);
                AudioObserver?.Invoke(new AudioAccess(CurrentMasterCycle, 0, AudioAccessSource.Z80, AudioChip.Ym2612, AudioAccessKind.Data, port >> 1, selected, value));
            }

            return;
        }

        if (address is >= 0x6000 and <= 0x60FF)
        {
            _z80BankRegister = ((_z80BankRegister >> 1) | ((value & 0x01) << 8)) & 0x1FF;
            return;
        }

        if (address is 0x7F11 or 0x7F12)
        {
            Psg.Write(value, CurrentMasterCycle);
            AudioObserver?.Invoke(new AudioAccess(CurrentMasterCycle, 0, AudioAccessSource.Z80, AudioChip.Psg, AudioAccessKind.Data, 0, 0, value));
            return;
        }

        if (address >= 0x8000)
        {
            WriteByte(MapZ80BankedAddress(address), value);
        }
    }

    public void SetLightGunPosition(int x, int y, bool visible)
    {
        _lightGunX = Math.Clamp(x, 0, Vdp.ScreenWidth - 1);
        _lightGunY = Math.Clamp(y, 0, Vdp.ScreenHeight - 1);
        _lightGunVisible = visible;
    }

    public void BeginLightGunFrame()
    {
        _lightGunHvLatch = null;
        _lightGunHvLatchForced = false;
    }

    public void UpdateLightGunForScanline(int scanline)
    {
        if (_lightGunHvLatchForced && scanline != _lightGunY)
        {
            _lightGunHvLatch = null;
            _lightGunHvLatchForced = false;
        }

        if (!IsLightGunDeviceActive() || !_lightGunVisible || _lightGunHvLatch.HasValue || scanline != _lightGunY || !IsLightGunHlEnabled())
        {
            return;
        }

        int lineCycles = Math.Max(1, MasterCyclesPerScanline);
        int offset = Math.Clamp((_lightGunX * lineCycles) / Math.Max(1, Vdp.ScreenWidth), 0, lineCycles - 1);
        _lightGunHvLatch = Vdp.ReadHvCounter(offset, lineCycles);
        _lightGunHvLatchForced = (Vdp.Registers[0] & 0x02) == 0;
    }

    public void ResetSvp()
    {
        _svp?.Reset();
        _svpLastMasterCycle = CurrentMasterCycle;
        _svpClockRemainder = 0;
    }

    public void ResetAddOnHardware()
    {
        ResetSvp();
        _thirtyTwoX?.Reset();
    }

    public void StepSvp(int cycles)
    {
        RunSvpCycles(cycles);
    }

    private void RunSvpCycles(int cycles)
    {
        if (_svp is null || cycles <= 0)
        {
            return;
        }

        const int MaxSvpRunChunk = 1024;
        while (cycles > 0 && !_svp.IsWaiting)
        {
            int chunk = Math.Min(cycles, MaxSvpRunChunk);
            _svp.Run(chunk);
            cycles -= chunk;
        }
    }

    private void StepSvpElapsedMasterCycles(long masterCycles)
    {
        if (_svp is null || masterCycles <= 0)
        {
            return;
        }

        long scaled = (masterCycles * 3) + _svpClockRemainder;
        int cycles = (int)(scaled / 7);
        _svpClockRemainder = (int)(scaled % 7);
        _svpLastMasterCycle += masterCycles;
        if (cycles > 0)
        {
            RunSvpCycles(cycles);
        }
    }

    public void SyncSvpToCurrentMasterCycle()
    {
        if (_svp is null)
        {
            return;
        }

        long delta = CurrentMasterCycle - _svpLastMasterCycle;
        if (delta <= 0)
        {
            return;
        }

        long scaled = (delta * 3) + _svpClockRemainder;
        int cycles = (int)(scaled / 7);
        _svpClockRemainder = (int)(scaled % 7);
        _svpLastMasterCycle = CurrentMasterCycle;
        if (cycles > 0)
        {
            RunSvpCycles(cycles);
        }
    }

    public void AnchorSvpTiming(long masterCycle)
    {
        _svpLastMasterCycle = masterCycle;
        _svpClockRemainder = 0;
    }

    public int ConsumeM68kWaitCycles()
    {
        int cycles = _pendingM68kWaitCycles;
        _pendingM68kWaitCycles = 0;
        return cycles;
    }

    public bool HasPendingM68kWaitCycles => _pendingM68kWaitCycles > 0;

    private void AddM68kWaitCycles(int cycles)
    {
        if (cycles > 0)
        {
            _pendingM68kWaitCycles += cycles;
        }
    }

    byte IZ80Bus.ReadByte(ushort address)
    {
        return ReadZ80Byte(address);
    }

    void IZ80Bus.WriteByte(ushort address, byte value)
    {
        WriteZ80Byte(address, value);
    }

    private void WriteZ80RamByte(int offset, byte value, uint pc)
    {
        offset &= 0x1FFF;
        _z80Ram[offset] = value;
        uint tracePc = pc != 0 ? pc : 0x00A0_0000u | CurrentZ80Pc;
        MemoryWriteObserver?.Invoke(new MemoryWrite(tracePc, 0x00A0_0000u | (uint)offset, value));
    }

    private uint MapZ80BankedAddress(ushort address)
    {
        return (uint)(((_z80BankRegister << 15) | (address & 0x7FFF)) & 0x00FF_FFFF);
    }

    private byte ReadIo(uint address)
    {
        byte value = (address & 0x1F) switch
        {
            0x01 => _versionRegister,
            0x03 => ReadControllerPort(0),
            0x05 => ReadControllerPort(1),
            0x07 => (byte)(_ioData[2] | 0x80),
            0x09 => _ioControl[0],
            0x0B => _ioControl[1],
            0x0D => _ioControl[2],
            _ => 0x00,
        };
        IoObserver?.Invoke(new IoAccess(CurrentM68kPc, IsWrite: false, address, value, _ioData[0], _ioControl[0]));
        return value;
    }

    private byte ReadZ80BusRequestStatus()
    {
        const byte unusedBitsHigh = 0xFE;
        if (!_z80BusRequested || _z80ResetAsserted)
        {
            return 0x01 | unusedBitsHigh;
        }

        return CurrentMasterCycle < _z80BusGrantReadyCycle ? (byte)(0x01 | unusedBitsHigh) : unusedBitsHigh;
    }

    private void WriteIo(uint address, byte value)
    {
        switch (address & 0x1F)
        {
            case 0x03:
                WriteControllerPort(0, value);
                break;
            case 0x05:
                WriteControllerPort(1, value);
                break;
            case 0x07:
                _ioData[2] = value;
                break;
            case 0x09:
            case 0x0B:
            case 0x0D:
                int index = (address & 0x1F) switch { 0x09 => 0, 0x0B => 1, _ => 2 };
                if (index < 2)
                {
                    WriteControllerControl(index, value);
                }
                else
                {
                    _ioControl[index] = value;
                }

                break;
        }

        IoObserver?.Invoke(new IoAccess(CurrentM68kPc, IsWrite: true, address, value, _ioData[0], _ioControl[0]));
    }

    private byte ReadControllerPort(int index)
    {
        byte controller = ReadControllerDevice(index);
        if (index == 1 && Port1Device == ControllerPortDevice.Ea4WayPlay)
        {
            return (byte)(controller | 0x80);
        }

        if (index == 1 && IsLightGunDeviceActive())
        {
            return (byte)(controller | 0x80);
        }

        byte direction = _ioControl[index];
        byte output = _ioData[index];
        return (byte)(((controller & ~direction) | (output & direction)) | 0x80);
    }

    private void WriteControllerPort(int index, byte value)
    {
        _ioData[index] = value;
        WriteControllerDevice(index);
    }

    private void WriteControllerControl(int index, byte value)
    {
        _ioControl[index] = value;
        WriteControllerDevice(index);
    }

    private byte GetDrivenControllerData(int index)
    {
        return (byte)(_ioData[index] | ~_ioControl[index]);
    }

    private byte ReadControllerDevice(int index)
    {
        return index switch
        {
            0 when Port1Device == ControllerPortDevice.SegaTeamPlayer => _teamPlayer.ReadData(),
            0 when Port1Device == ControllerPortDevice.Ea4WayPlay => ReadEa4WayPlayPort1(),
            1 when Port1Device == ControllerPortDevice.Ea4WayPlay => 0x7F,
            1 when Port2Device == ControllerPortDevice.Menacer => ReadMenacer(),
            1 when Port2Device == ControllerPortDevice.KonamiJustifier => ReadKonamiJustifier(),
            _ => _controllers[index].ReadData(),
        };
    }

    private void WriteControllerDevice(int index)
    {
        if (index == 0 && Port1Device == ControllerPortDevice.SegaTeamPlayer)
        {
            _teamPlayer.WriteData(_ioData[index], _ioControl[index]);
            return;
        }

        if (Port1Device == ControllerPortDevice.Ea4WayPlay)
        {
            if (index == 0)
            {
                _controllers[_ea4WayPlayLatch & 0x03].WriteData(GetDrivenControllerData(index), CurrentMasterCycle);
            }
            else if (index == 1)
            {
                byte data = _ioData[index];
                if ((data & 0x03) == 0)
                {
                    _ea4WayPlayLatch = (byte)((data >> 4) & 0x07);
                    _controllers[_ea4WayPlayLatch & 0x03].WriteData(GetDrivenControllerData(0), CurrentMasterCycle);
                }
            }

            return;
        }

        if (index == 1 && Port2Device == ControllerPortDevice.KonamiJustifier)
        {
            _justifierState = (byte)(_ioData[index] & _ioControl[index]);
            return;
        }

        if (index == 1 && Port2Device == ControllerPortDevice.Menacer)
        {
            return;
        }

        _controllers[index].WriteData(GetDrivenControllerData(index), CurrentMasterCycle);
    }

    private byte ReadEa4WayPlayPort1()
    {
        if ((_ea4WayPlayLatch & 0x04) != 0)
        {
            return 0x7C;
        }

        return _controllers[_ea4WayPlayLatch & 0x03].ReadData();
    }

    private byte ReadMenacer()
    {
        GenesisButton pressed = Controller2.Pressed;
        byte value = 0x40;
        if (IsLightGunOnCurrentBeam())
        {
            value &= unchecked((byte)~0x40);
        }

        if ((pressed & GenesisButton.B) != 0) value |= 0x01;
        if ((pressed & GenesisButton.A) != 0) value |= 0x02;
        if ((pressed & GenesisButton.C) != 0) value |= 0x04;
        if ((pressed & GenesisButton.Start) != 0) value |= 0x08;
        return value;
    }

    private byte ReadKonamiJustifier()
    {
        if ((_justifierState & 0x40) != 0)
        {
            return 0x30;
        }

        ThreeButtonController controller = (_justifierState & 0x20) != 0 ? Controller3 : Controller2;
        byte value = 0x73;
        if ((controller.Pressed & GenesisButton.A) != 0) value &= unchecked((byte)~0x01);
        if ((controller.Pressed & GenesisButton.Start) != 0) value &= unchecked((byte)~0x02);
        return value;
    }

    private bool IsLightGunDeviceActive()
    {
        return Port2Device is ControllerPortDevice.Menacer or ControllerPortDevice.KonamiJustifier;
    }

    private bool IsLightGunHlEnabled()
    {
        return (_ioControl[1] & 0x80) != 0;
    }

    private bool IsLightGunOnCurrentBeam()
    {
        if (!IsLightGunDeviceActive() || !_lightGunVisible || !IsLightGunHlEnabled() || Vdp.CurrentScanline != _lightGunY)
        {
            return false;
        }

        int lineCycles = Math.Max(1, MasterCyclesPerScanline);
        int beamX = (CurrentScanlineMasterCycleOffset * Vdp.ScreenWidth) / lineCycles;
        return Math.Abs(beamX - _lightGunX) <= 8;
    }

    private bool IsJCartControllerWindow(uint address)
    {
        return Cartridge.Diagnostics.HasJCart && address is >= 0x38_0000 and <= 0x3F_FFFF;
    }

    private bool IsSvpMappedAddress(uint address)
    {
        return address is >= 0x30_0000 and <= 0x31_FFFF or >= 0x39_0000 and <= 0x3A_FFFF;
    }

    private bool TryReadThirtyTwoXByte(uint address, out byte value)
    {
        value = 0xFF;
        if (_thirtyTwoX is null)
        {
            return false;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kSuper32XId and <= ThirtyTwoXHardwareProfile.M68kSuper32XId + 3)
        {
            value = _thirtyTwoX.ReadSuper32XIdByte(address);
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kFrameBufferStart and < ThirtyTwoXHardwareProfile.M68kFrameBufferStart + ThirtyTwoXHardwareProfile.FrameBufferBytes)
        {
            value = _thirtyTwoX.ReadFrameBufferByte(address - ThirtyTwoXHardwareProfile.M68kFrameBufferStart);
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kOverwriteImageStart and < ThirtyTwoXHardwareProfile.M68kOverwriteImageStart + ThirtyTwoXHardwareProfile.FrameBufferBytes)
        {
            value = _thirtyTwoX.ReadFrameBufferByte(address - ThirtyTwoXHardwareProfile.M68kOverwriteImageStart);
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kCartridgeFixedStart and < 0xA0_0000)
        {
            AddM68kWaitCycles(_thirtyTwoX.ClaimM68kCartridgeBus(1, CurrentMasterCycle));
            value = Cartridge.ReadByte(_thirtyTwoX.MapM68kCartridgeAddress(address));
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kVdpRegisterStart and < ThirtyTwoXHardwareProfile.M68kVdpRegisterStart + 0x80)
        {
            value = _thirtyTwoX.ReadVdpRegisterByte((ushort)(address - ThirtyTwoXHardwareProfile.M68kVdpRegisterStart));
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kSystemRegisterStart and < ThirtyTwoXHardwareProfile.M68kSystemRegisterStart + 0x80)
        {
            ushort offset = (ushort)(address - ThirtyTwoXHardwareProfile.M68kSystemRegisterStart);
            if (ShouldSampleThirtyTwoXRegisterBeforeSync(offset))
            {
                value = _thirtyTwoX.ReadSystemRegisterByte(offset);
                SyncThirtyTwoXSystemHandshake(address, isWrite: false);
            }
            else
            {
                SyncThirtyTwoXSystemHandshake(address, isWrite: false);
                value = _thirtyTwoX.ReadSystemRegisterByte(offset);
            }

            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kColorPaletteStart and < ThirtyTwoXHardwareProfile.M68kColorPaletteStart + (ThirtyTwoXHardwareProfile.PaletteEntries * 2))
        {
            value = _thirtyTwoX.ReadPaletteByte((ushort)(address - ThirtyTwoXHardwareProfile.M68kColorPaletteStart));
            return true;
        }

        return false;
    }

    private bool TryReadThirtyTwoXVectorRomByte(uint address, out byte value)
    {
        value = 0xFF;
        if (_thirtyTwoX is null ||
            !_thirtyTwoX.AdapterEnabled ||
            address >= ThirtyTwoXVectorRomBytes)
        {
            return false;
        }

        if (address < 4)
        {
            value = Cartridge.ReadByte(address);
            return true;
        }

        if (_thirtyTwoXM68kBios is not null)
        {
            if (address < _thirtyTwoXM68kBios.Length)
            {
                value = _thirtyTwoXM68kBios[address];
            }

            return true;
        }

        if (address == ThirtyTwoXTrap15ReturnHelper)
        {
            value = 0x4E;
            return true;
        }

        if (address == ThirtyTwoXTrap15ReturnHelper + 1)
        {
            value = 0x73;
            return true;
        }

        if (address >= ThirtyTwoXVectorRomHelperStart)
        {
            value = ThirtyTwoXVectorRomHelpers[(int)(address - ThirtyTwoXVectorRomHelperStart)];
            return true;
        }

        uint vector = address >> 2;
        uint byteOffset = address & 0x03;
        uint target = vector == 47
            ? ThirtyTwoXTrap15ReturnHelper
            : ThirtyTwoXVectorJumpTableStart + (vector * ThirtyTwoXVectorJumpTableStride);
        value = byteOffset switch
        {
            0 => (byte)(target >> 24),
            1 => (byte)(target >> 16),
            2 => (byte)(target >> 8),
            _ => (byte)target,
        };
        return true;
    }

    private bool IsThirtyTwoXLowCartridgeRomBlocked(uint address)
    {
        return _thirtyTwoX is not null &&
            _thirtyTwoX.AdapterEnabled &&
            !_thirtyTwoX.RomToVramDmaActive &&
            address is >= ThirtyTwoXVectorRomBytes and <= 0x3F_FFFF;
    }

    private bool TryReadThirtyTwoXVectorRomWord(uint address, out ushort value)
    {
        if (TryReadThirtyTwoXVectorRomByte(address, out byte high) &&
            TryReadThirtyTwoXVectorRomByte((address + 1) & 0x00FF_FFFF, out byte low))
        {
            value = (ushort)((high << 8) | low);
            return true;
        }

        value = 0xFFFF;
        return false;
    }

    private bool TryReadThirtyTwoXVectorRomLong(uint address, out uint value)
    {
        if (TryReadThirtyTwoXVectorRomWord(address, out ushort high) &&
            TryReadThirtyTwoXVectorRomWord((address + 2) & 0x00FF_FFFF, out ushort low))
        {
            value = (uint)((high << 16) | low);
            return true;
        }

        value = 0xFFFF_FFFF;
        return false;
    }

    private bool TryReadThirtyTwoXWord(uint address, out ushort value)
    {
        value = 0xFFFF;
        if (_thirtyTwoX is null)
        {
            return false;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kSuper32XId and <= ThirtyTwoXHardwareProfile.M68kSuper32XId + 2)
        {
            value = (ushort)((_thirtyTwoX.ReadSuper32XIdByte(address) << 8) | _thirtyTwoX.ReadSuper32XIdByte(address + 1));
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kFrameBufferStart and < ThirtyTwoXHardwareProfile.M68kFrameBufferStart + ThirtyTwoXHardwareProfile.FrameBufferBytes - 1)
        {
            value = _thirtyTwoX.ReadFrameBufferWord(address - ThirtyTwoXHardwareProfile.M68kFrameBufferStart);
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kOverwriteImageStart and < ThirtyTwoXHardwareProfile.M68kOverwriteImageStart + ThirtyTwoXHardwareProfile.FrameBufferBytes - 1)
        {
            value = _thirtyTwoX.ReadFrameBufferWord(address - ThirtyTwoXHardwareProfile.M68kOverwriteImageStart);
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kCartridgeFixedStart and < 0xA0_0000)
        {
            AddM68kWaitCycles(_thirtyTwoX.ClaimM68kCartridgeBus(2, CurrentMasterCycle));
            value = Cartridge.ReadWord(_thirtyTwoX.MapM68kCartridgeAddress(address));
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kVdpRegisterStart and < ThirtyTwoXHardwareProfile.M68kVdpRegisterStart + 0x7F)
        {
            value = _thirtyTwoX.ReadVdpRegisterWord((ushort)(address - ThirtyTwoXHardwareProfile.M68kVdpRegisterStart));
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kSystemRegisterStart and < ThirtyTwoXHardwareProfile.M68kSystemRegisterStart + 0x7F)
        {
            ushort offset = (ushort)(address - ThirtyTwoXHardwareProfile.M68kSystemRegisterStart);
            if (ShouldSampleThirtyTwoXRegisterBeforeSync(offset))
            {
                value = _thirtyTwoX.ReadSystemRegisterWord(offset);
                SyncThirtyTwoXSystemHandshake(address, isWrite: false);
            }
            else
            {
                SyncThirtyTwoXSystemHandshake(address, isWrite: false);
                value = _thirtyTwoX.ReadSystemRegisterWord(offset);
            }

            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kColorPaletteStart and < ThirtyTwoXHardwareProfile.M68kColorPaletteStart + (ThirtyTwoXHardwareProfile.PaletteEntries * 2) - 1)
        {
            value = _thirtyTwoX.ReadPaletteWord((ushort)(address - ThirtyTwoXHardwareProfile.M68kColorPaletteStart));
            return true;
        }

        return false;
    }

    private bool TryWriteThirtyTwoXByte(uint address, byte value)
    {
        if (_thirtyTwoX is null)
        {
            return false;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kFrameBufferStart and < ThirtyTwoXHardwareProfile.M68kFrameBufferStart + ThirtyTwoXHardwareProfile.FrameBufferBytes)
        {
            _thirtyTwoX.WriteFrameBufferByte(address - ThirtyTwoXHardwareProfile.M68kFrameBufferStart, value);
            _thirtyTwoX.GrantVdpAccessToSh2();
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kOverwriteImageStart and < ThirtyTwoXHardwareProfile.M68kOverwriteImageStart + ThirtyTwoXHardwareProfile.FrameBufferBytes)
        {
            _thirtyTwoX.WriteOverwriteImageByte(address - ThirtyTwoXHardwareProfile.M68kOverwriteImageStart, value);
            _thirtyTwoX.GrantVdpAccessToSh2();
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kVdpRegisterStart and < ThirtyTwoXHardwareProfile.M68kVdpRegisterStart + 0x80)
        {
            _thirtyTwoX.WriteVdpRegisterByte((ushort)(address - ThirtyTwoXHardwareProfile.M68kVdpRegisterStart), value);
            _thirtyTwoX.GrantVdpAccessToSh2();
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kSystemRegisterStart and < ThirtyTwoXHardwareProfile.M68kSystemRegisterStart + 0x80)
        {
            _thirtyTwoX.WriteSystemRegisterByte((ushort)(address - ThirtyTwoXHardwareProfile.M68kSystemRegisterStart), value);
            SyncThirtyTwoXSystemHandshake(address, isWrite: true);
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kColorPaletteStart and < ThirtyTwoXHardwareProfile.M68kColorPaletteStart + (ThirtyTwoXHardwareProfile.PaletteEntries * 2))
        {
            _thirtyTwoX.WritePaletteByte((ushort)(address - ThirtyTwoXHardwareProfile.M68kColorPaletteStart), value);
            _thirtyTwoX.GrantVdpAccessToSh2();
            return true;
        }

        return false;
    }

    private bool TryWriteThirtyTwoXWord(uint address, ushort value)
    {
        if (_thirtyTwoX is null)
        {
            return false;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kFrameBufferStart and < ThirtyTwoXHardwareProfile.M68kFrameBufferStart + ThirtyTwoXHardwareProfile.FrameBufferBytes - 1)
        {
            _thirtyTwoX.WriteFrameBufferWord(address - ThirtyTwoXHardwareProfile.M68kFrameBufferStart, value);
            _thirtyTwoX.GrantVdpAccessToSh2();
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kOverwriteImageStart and < ThirtyTwoXHardwareProfile.M68kOverwriteImageStart + ThirtyTwoXHardwareProfile.FrameBufferBytes - 1)
        {
            _thirtyTwoX.WriteOverwriteImageWord(address - ThirtyTwoXHardwareProfile.M68kOverwriteImageStart, value);
            _thirtyTwoX.GrantVdpAccessToSh2();
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kVdpRegisterStart and < ThirtyTwoXHardwareProfile.M68kVdpRegisterStart + 0x7F)
        {
            _thirtyTwoX.WriteVdpRegisterWord((ushort)(address - ThirtyTwoXHardwareProfile.M68kVdpRegisterStart), value);
            _thirtyTwoX.GrantVdpAccessToSh2();
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kSystemRegisterStart and < ThirtyTwoXHardwareProfile.M68kSystemRegisterStart + 0x7F)
        {
            _thirtyTwoX.WriteSystemRegisterWord((ushort)(address - ThirtyTwoXHardwareProfile.M68kSystemRegisterStart), value);
            SyncThirtyTwoXSystemHandshake(address, isWrite: true);
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kColorPaletteStart and < ThirtyTwoXHardwareProfile.M68kColorPaletteStart + (ThirtyTwoXHardwareProfile.PaletteEntries * 2) - 1)
        {
            _thirtyTwoX.WritePaletteWord((ushort)(address - ThirtyTwoXHardwareProfile.M68kColorPaletteStart), value);
            _thirtyTwoX.GrantVdpAccessToSh2();
            return true;
        }

        return false;
    }

    private void SyncThirtyTwoXSystemHandshake(uint address, bool isWrite)
    {
        if (_thirtyTwoX is null || !IsThirtyTwoXSystemHandshakeAddress(address))
        {
            return;
        }

        _thirtyTwoX.SetCurrentMasterCycle(CurrentMasterCycle);
        _thirtyTwoX.RunSh2Cycles(isWrite ? ThirtyTwoXCommunicationWriteSyncCycles : ThirtyTwoXCommunicationReadSyncCycles);
    }

    private static bool IsThirtyTwoXSystemHandshakeAddress(uint address)
    {
        const uint communicationPortStart = ThirtyTwoXHardwareProfile.M68kSystemRegisterStart + ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        const uint interruptControlStart = ThirtyTwoXHardwareProfile.M68kSystemRegisterStart + ThirtyTwoXHardwareProfile.InterruptControlOffset;
        return address is >= communicationPortStart and < communicationPortStart + 0x10 ||
            address is >= interruptControlStart and < interruptControlStart + 2;
    }

    private static bool ShouldSampleThirtyTwoXRegisterBeforeSync(ushort offset)
    {
        const ushort upperMailboxStart = ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8;
        return offset is >= upperMailboxStart and < upperMailboxStart + 8;
    }

    private byte ReadJCartByte(uint address)
    {
        if ((address & 1) == 0)
        {
            return (byte)(Controller4.ReadData() & 0x3F);
        }

        byte value = (byte)(Controller3.ReadData() & 0x7F);
        if (Cartridge.Diagnostics.EepromSize is not null)
        {
            value |= (byte)(Cartridge.ReadByte(address) & 0x80);
        }

        return value;
    }

    private void WriteJCart(byte value)
    {
        byte th = (value & 0x01) != 0 ? (byte)0x40 : (byte)0x00;
        Controller3.WriteData(th, CurrentMasterCycle);
        Controller4.WriteData(th, CurrentMasterCycle);
    }

    private static byte BuildVersionRegister(CartridgeImage cartridge, bool pal)
    {
        string region = cartridge.Header.Region.ToUpperInvariant();
        bool domesticOnly = (region.Contains('J') || region.Contains('1'))
            && !region.Contains('U')
            && !region.Contains('E')
            && !region.Contains('W')
            && !region.Contains('4')
            && !region.Contains('8')
            && !region.Contains('F');
        byte version = 0x21; // no expansion unit, TMSS-era version bit set
        if (!domesticOnly)
        {
            version |= 0x80;
        }

        if (pal)
        {
            version |= 0x40;
        }

        return version;
    }

    private void MaybeSeedThirtyTwoXSdkCountryBlock()
    {
        if (_thirtyTwoX is null ||
            _workRam[0xD008] != (byte)'I' ||
            _workRam[0xD009] != (byte)'N' ||
            _workRam[0xD00A] != (byte)'I' ||
            _workRam[0xD00B] != (byte)'T' ||
            _workRam[0xD09A] != 0 ||
            _workRam[0xD09B] != 0 ||
            _workRam[0xD09C] != 0 ||
            _workRam[0xD09D] != 0 ||
            _workRam[0xD09E] != 0)
        {
            return;
        }

        bool overseas = (_versionRegister & 0x80) != 0;
        _workRam[0xD09A] = overseas ? (byte)0x00 : (byte)0x01;
        _workRam[0xD09B] = overseas ? (byte)0x01 : (byte)0x00;
        _workRam[0xD09C] = 0x05;
        _workRam[0xD09D] = 0x00;
        _workRam[0xD09E] = 0x06;
    }

    private byte ReadVdpByte(uint address)
    {
        ushort word = ReadVdpWord(address & 0xFFFF_FFFE);
        return (address & 1) == 0 ? (byte)(word >> 8) : (byte)word;
    }

    private ushort ReadVdpWord(uint address)
    {
        return (address & 0x1E) switch
        {
            0x00 or 0x02 => Vdp.ReadDataPort(),
            0x04 or 0x06 => Vdp.ReadControlPort(),
            0x08 or 0x0A => _lightGunHvLatch ?? Vdp.ReadHvCounter(CurrentScanlineMasterCycleOffset, MasterCyclesPerScanline),
            _ => 0,
        };
    }

    private void WriteVdpByte(uint address, byte value)
    {
        ushort word = (ushort)((value << 8) | value);
        WriteVdpWord(address & 0xFFFF_FFFE, word);
    }

    private void WriteVdpWord(uint address, ushort value)
    {
        switch (address & 0x1E)
        {
            case 0x00:
            case 0x02:
                AddVdpDataPortWait();
                Vdp.WriteDataPort(value);
                break;
            case 0x04:
            case 0x06:
                Vdp.WriteControlPort(value);
                TryRunDma();
                break;
            case 0x10:
            case 0x12:
            case 0x14:
            case 0x16:
                Psg.Write((byte)value, CurrentMasterCycle);
                AudioObserver?.Invoke(new AudioAccess(CurrentMasterCycle, CurrentM68kPc, AudioAccessSource.M68k, AudioChip.Psg, AudioAccessKind.Data, 0, 0, (byte)value));
                break;
        }
    }

    private void AddVdpDataPortWait()
    {
        if (Vdp.FifoWords >= 4)
        {
            AddM68kWaitCycles(4);
        }
    }

    private void TryRunDma()
    {
        if (!Vdp.TryDequeueDmaRequest(out Vdp.DmaRequest request))
        {
            return;
        }

        if (request.Mode == 2)
        {
            Vdp.BeginDmaFill(request);
            return;
        }

        if (request.Mode == 3)
        {
            Vdp.RunDmaVramCopy(request);
            return;
        }

        Vdp.BeginDmaMemoryCopy(request);
        uint requestedSource = request.SourceAddress;
        uint source = requestedSource;
        if ((request.Code & 0x0F) == 0x01 && IsSvpDramAddress(source))
        {
            // Virtua Racing's SVP buffers are observed through the VDP DMA path one word behind the 68k command source.
            source = (source - 2) & 0x00FF_FFFE;
        }

        uint destination = request.DestinationAddress;
        uint destinationIncrement = Vdp.AutoIncrement;
        Action<DmaWordTransfer>? dmaWordObserver = DmaWordObserver;
        SyncSvpToCurrentMasterCycle();
        bool sourceIsSvpDram = IsSvpDramAddress(source);
        int dmaCycles = Math.Max(1, request.LengthWords * 2);
        long dmaMasterCycles = (long)dmaCycles * M68kMasterDivider;
        long previousElapsedMasterCycles = 0;
        int lastDmaVramSnapshotLine = Vdp.CurrentScanline;
        bool captureDmaVramTiming = (request.Code & 0x0F) == 0x01;
        _dmaActive = true;
        try
        {
            for (int i = 0; i < request.LengthWords; i++)
            {
                long elapsedBeforeMasterCycles = previousElapsedMasterCycles;
                long elapsedMasterCycles = (((long)i + 1) * dmaMasterCycles) / request.LengthWords;
                long absoluteStartMasterCycle = CurrentMasterCycle + elapsedBeforeMasterCycles;
                long absoluteEndMasterCycle = CurrentMasterCycle + elapsedMasterCycles;
                bool sampleSource = dmaWordObserver is not null && sourceIsSvpDram;
                ushort sourceBeforeStep = sampleSource ? ReadWord(source) : (ushort)0;
                if (StepSvpDuringDma)
                {
                    StepSvpElapsedMasterCycles(elapsedMasterCycles - previousElapsedMasterCycles);
                }
                previousElapsedMasterCycles = elapsedMasterCycles;
                ushort value = ReadWord(source);
                _thirtyTwoX?.SnoopM68kVdpDmaWord(source, value);
                Vdp.WriteDmaWord(value);
                ushort sourceAfterTransfer = sampleSource ? ReadWord(source) : value;
                dmaWordObserver?.Invoke(new DmaWordTransfer(
                    request.Mode,
                    request.Code,
                    requestedSource + ((uint)i * 2u),
                    source,
                    destination & 0xFFFF,
                    i,
                    value,
                    sampleSource,
                    sourceBeforeStep,
                    sourceAfterTransfer,
                    absoluteStartMasterCycle,
                    absoluteEndMasterCycle,
                    Vdp.CurrentScanline));
                if (captureDmaVramTiming)
                {
                    CaptureDmaVramTimingSnapshots(elapsedMasterCycles, ref lastDmaVramSnapshotLine);
                }

                source += 2;
                destination = (destination + destinationIncrement) & 0xFFFF;
            }
        }
        finally
        {
            _dmaActive = false;
        }

        if (!StepSvpDuringDma)
        {
            StepSvpElapsedMasterCycles(dmaMasterCycles);
        }
    }

    private bool IsSvpDramAddress(uint address)
    {
        if (_svp is null)
        {
            return false;
        }

        address &= 0x00FF_FFFE;
        return address is >= 0x30_0000 and <= 0x31_FFFE
            or >= 0x39_0000 and <= 0x39_FFFE
            or >= 0x3A_0000 and <= 0x3A_FFFE;
    }

    private void CaptureDmaVramTimingSnapshots(long elapsedMasterCycles, ref int lastCapturedLine)
    {
        int lineCycles = Math.Max(1, MasterCyclesPerScanline);
        long lineOffset = CurrentScanlineMasterCycleOffset + elapsedMasterCycles;
        int targetLine = Vdp.CurrentScanline + (int)(lineOffset / lineCycles);
        int cappedTarget = Math.Min(targetLine, MdSharp.Core.Video.Vdp.ScreenHeight - 1);
        for (int line = lastCapturedLine + 1; line <= cappedTarget; line++)
        {
            Vdp.CaptureLineVramForDmaTiming(line);
        }

        if (cappedTarget > lastCapturedLine)
        {
            lastCapturedLine = cappedTarget;
        }
    }

    public BusState CaptureState()
    {
        return new BusState(
            (byte[])_workRam.Clone(),
            (byte[])_z80Ram.Clone(),
            (byte[])_tmss.Clone(),
            (byte[])_ioData.Clone(),
            (byte[])_ioControl.Clone(),
            _z80BusRequested,
            _z80ResetAsserted,
            _z80BankRegister,
            Cartridge.CaptureSaveRam(),
            Cartridge.CaptureBankRegisters(),
            Cartridge.BankSwitchingEnabled,
            Cartridge.FallbackSaveRamActive,
            Cartridge.SaveRamEnabled,
            _z80BusGrantReadyCycle,
            _pendingM68kWaitCycles,
            _svp?.CaptureState(),
            _thirtyTwoX?.CaptureState());
    }

    public void RestoreState(BusState state)
    {
        Array.Copy(state.WorkRam, _workRam, Math.Min(_workRam.Length, state.WorkRam.Length));
        Array.Copy(state.Z80Ram, _z80Ram, Math.Min(_z80Ram.Length, state.Z80Ram.Length));
        Array.Copy(state.Tmss, _tmss, Math.Min(_tmss.Length, state.Tmss.Length));
        Array.Copy(state.IoData, _ioData, Math.Min(_ioData.Length, state.IoData.Length));
        Array.Copy(state.IoControl, _ioControl, Math.Min(_ioControl.Length, state.IoControl.Length));
        Controller1.ResetProtocol();
        Controller2.ResetProtocol();
        Controller3.ResetProtocol();
        Controller4.ResetProtocol();
        _teamPlayer.Reset();
        _ea4WayPlayLatch = 0;
        _justifierState = 0x40;
        _lightGunHvLatch = null;
        _lightGunHvLatchForced = false;
        Controller1.WriteControl(_ioData[0]);
        Controller2.WriteControl(_ioData[1]);
        _z80BusRequested = state.Z80BusRequested;
        _z80ResetAsserted = state.Z80ResetAsserted;
        _z80BusGrantReadyCycle = state.Z80BusGrantReadyCycle;
        _pendingM68kWaitCycles = state.PendingM68kWaitCycles;
        _z80BankRegister = state.Z80BankRegister & 0x1FF;
        Cartridge.RestoreSaveRam(state.SaveRam, state.FallbackSaveRamActive);
        Cartridge.RestoreBankRegisters(state.BankRegisters, state.BankSwitchingEnabled);
        Cartridge.SaveRamEnabled = state.SaveRamEnabled;
        if (_svp is not null && state.Svp is not null)
        {
            _svp.RestoreState(state.Svp);
        }

        if (_thirtyTwoX is not null && state.ThirtyTwoX is not null)
        {
            _thirtyTwoX.RestoreState(state.ThirtyTwoX);
        }
    }

    public sealed record BusState(
        byte[] WorkRam,
        byte[] Z80Ram,
        byte[] Tmss,
        byte[] IoData,
        byte[] IoControl,
        bool Z80BusRequested,
        bool Z80ResetAsserted,
        int Z80BankRegister,
        byte[] SaveRam,
        byte[] BankRegisters,
        bool BankSwitchingEnabled,
        bool FallbackSaveRamActive,
        bool SaveRamEnabled,
        long Z80BusGrantReadyCycle,
        int PendingM68kWaitCycles,
        SvpDevice.SvpState? Svp,
        ThirtyTwoXDevice.ThirtyTwoXState? ThirtyTwoX);
}

public readonly record struct MemoryRead(uint Pc, uint Address, int Size, uint Value);
public readonly record struct MemoryWrite(uint Pc, uint Address, byte Value);
public readonly record struct IoAccess(uint Pc, bool IsWrite, uint Address, byte Value, byte Data0, byte Control0);
public readonly record struct AudioAccess(long MasterCycle, uint Pc, AudioAccessSource Source, AudioChip Chip, AudioAccessKind Kind, int Port, byte Register, byte Value);
public readonly record struct Z80ControlAccess(long MasterCycle, uint Pc, Z80ControlKind Kind, byte Value, bool BusRequested, bool ResetAsserted);
public readonly record struct DmaWordTransfer(
    byte Mode,
    byte Code,
    uint RequestedSourceAddress,
    uint SourceAddress,
    uint DestinationAddress,
    int WordIndex,
    ushort Value,
    bool HasSourceSamples,
    ushort SourceBeforeStep,
    ushort SourceAfterTransfer,
    long MasterCycleStart,
    long MasterCycleEnd,
    int Scanline);
public readonly record struct SvpExternalAccess(long MasterCycle, uint Pc, bool DuringDma, bool IsWrite, uint Address, int SizeBytes, uint Value);
public enum AudioAccessSource
{
    M68k,
    Z80,
}

public enum AudioChip
{
    Ym2612,
    Psg,
}

public enum AudioAccessKind
{
    Address,
    Data,
}

public enum Z80ControlKind
{
    BusRequest,
    Reset,
}
