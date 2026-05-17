using MdSharp.Core.Audio;
using MdSharp.Core.Cartridge;
using MdSharp.Core.Cpu.Z80;
using MdSharp.Core.Input;
using MdSharp.Core.Video;

namespace MdSharp.Core.Bus;

public sealed class GenesisBus : IMemoryBus, IInstructionTraceSink, IZ80Bus
{
    private const long Z80BusGrantDelayMasterCycles = 64;
    private const int M68kMasterDivider = 7;
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

    public GenesisBus(CartridgeImage cartridge, Vdp vdp, Psg psg, Ym2612 ym2612, bool pal = false, ThreeButtonController? controller1 = null, ThreeButtonController? controller2 = null, ThreeButtonController? controller3 = null, ThreeButtonController? controller4 = null)
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
    }

    public CartridgeImage Cartridge { get; }
    public Vdp Vdp { get; }
    public Psg Psg { get; }
    public Ym2612 Ym2612 { get; }
    public SvpDevice? Svp => _svp;
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
    public Action<MemoryWrite>? MemoryWriteObserver { get; set; }
    public Action<IoAccess>? IoObserver { get; set; }
    public Action<AudioAccess>? AudioObserver { get; set; }
    public Action<Z80ControlAccess>? Z80ControlObserver { get; set; }
    public Action<DmaWordTransfer>? DmaWordObserver { get; set; }
    public Action<SvpExternalAccess>? SvpExternalObserver { get; set; }

    public byte ReadByte(uint address)
    {
        address &= 0x00FF_FFFF;

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

            return Cartridge.ReadByte(address);
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
            return _workRam[address & 0xFFFF];
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

            return Cartridge.ReadWord(address);
        }

        if (address is >= 0xA0_0000 and <= 0xA0_1FFE)
        {
            AddM68kWaitCycles(Z80AreaM68kWaitCycles);
            int offset = (int)(address & 0x1FFF);
            return (ushort)((_z80Ram[offset] << 8) | _z80Ram[(offset + 1) & 0x1FFF]);
        }

        if (address is >= 0xA1_4000 and <= 0xA1_4002)
        {
            AddM68kWaitCycles(IoM68kWaitCycles);
            int offset = (int)(address & 0x03);
            return (ushort)((_tmss[offset] << 8) | _tmss[(offset + 1) & 0x03]);
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
            return (ushort)((_workRam[offset] << 8) | _workRam[(offset + 1) & 0xFFFF]);
        }

        return (ushort)((ReadByte(address) << 8) | ReadByte(address + 1));
    }

    public uint ReadLong(uint address)
    {
        address &= 0x00FF_FFFF;
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

            return Cartridge.ReadLong(address);
        }

        if (address is >= 0xA0_0000 and <= 0xA0_1FFC)
        {
            AddM68kWaitCycles(Z80AreaM68kWaitCycles * 2);
            int offset = (int)(address & 0x1FFF);
            return (uint)((_z80Ram[offset] << 24) | (_z80Ram[(offset + 1) & 0x1FFF] << 16) | (_z80Ram[(offset + 2) & 0x1FFF] << 8) | _z80Ram[(offset + 3) & 0x1FFF]);
        }

        if (address >= 0xE0_0000)
        {
            int offset = (int)(address & 0xFFFF);
            return (uint)((_workRam[offset] << 24) | (_workRam[(offset + 1) & 0xFFFF] << 16) | (_workRam[(offset + 2) & 0xFFFF] << 8) | _workRam[(offset + 3) & 0xFFFF]);
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
        if (!_z80BusRequested || _z80ResetAsserted)
        {
            return 0x01;
        }

        return CurrentMasterCycle < _z80BusGrantReadyCycle ? (byte)0x01 : (byte)0x00;
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
                _controllers[_ea4WayPlayLatch & 0x03].WriteData(_ioData[index], CurrentMasterCycle);
            }
            else if (index == 1)
            {
                byte data = _ioData[index];
                if ((data & 0x03) == 0)
                {
                    _ea4WayPlayLatch = (byte)((data >> 4) & 0x07);
                    _controllers[_ea4WayPlayLatch & 0x03].WriteData(_ioData[0], CurrentMasterCycle);
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

        _controllers[index].WriteData(_ioData[index], CurrentMasterCycle);
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
            _svp?.CaptureState());
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
        SvpDevice.SvpState? Svp);
}

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
