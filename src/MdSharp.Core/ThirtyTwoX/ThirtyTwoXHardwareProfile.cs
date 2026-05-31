namespace MdSharp.Core.ThirtyTwoX;

public static class ThirtyTwoXHardwareProfile
{
    public const int Sh2CpuCount = 2;
    public const int Sh2CacheBytes = 4 * 1024;
    public const int SdramBytes = 256 * 1024;
    public const int FrameBufferBytes = 128 * 1024;
    public const int FrameBufferCount = 2;
    public const int PaletteEntries = 256;
    public const int NominalWidth = 320;
    public const int NtscVisibleLines = 224;
    public const int PalVisibleLines = 240;

    public const double NtscSh2ClockHz = 23_011_360.0;
    public const double PalSh2ClockHz = 22_801_467.0;

    public const uint M68kFrameBufferStart = 0x84_0000;
    public const uint M68kOverwriteImageStart = 0x86_0000;
    public const uint M68kCartridgeFixedStart = 0x88_0000;
    public const uint M68kCartridgeBankedStart = 0x90_0000;
    public const uint M68kCartridgeBankedBytes = 0x10_0000;
    public const uint M68kSuper32XId = 0xA1_30EC;
    public const uint M68kSystemRegisterStart = 0xA1_5100;
    public const uint M68kVdpRegisterStart = 0xA1_5180;
    public const uint M68kColorPaletteStart = 0xA1_5200;
    public const uint M68kFrameBufferMirrorStart = 0xA1_5400;

    public const uint Sh2SystemRegisterStart = 0x2000_4000;
    public const uint Sh2VdpRegisterStart = 0x2000_4100;
    public const uint Sh2ColorPaletteStart = 0x2000_4200;
    public const uint MarsUserHeaderStart = 0x0000_03C0;
    public const uint Sh2SystemRegisterCachedStart = 0x0000_4000;
    public const uint Sh2VdpRegisterCachedStart = 0x0000_4100;
    public const uint Sh2ColorPaletteCachedStart = 0x0000_4200;
    public const uint Sh2CartridgeLowCachedStart = 0x0000_0000;
    public const uint Sh2CartridgeLowCachedBytes = 0x0400_0000;
    public const uint Sh2FrameBufferCachedStart = 0x0400_0000;
    public const uint Sh2OverwriteImageCachedStart = 0x0402_0000;
    public const uint Sh2SdramStart = 0x0600_0000;
    public const uint Sh2CartridgeFixedCachedStart = 0x0200_0000;
    public const uint Sh2CartridgeBankedCachedStart = 0x0240_0000;
    public const uint Sh2CartridgeFixedStart = 0x2200_0000;
    public const uint Sh2CartridgeBankedStart = 0x2240_0000;
    public const uint Sh2FrameBufferStart = 0x2400_0000;
    public const uint Sh2OverwriteImageStart = 0x2402_0000;
    public const uint Sh2SdramCacheThroughStart = 0x2600_0000;

    public const ushort AdapterControlOffset = 0x0000;
    public const ushort InterruptControlOffset = 0x0002;
    public const ushort BankSetOffset = 0x0004;
    public const ushort HCountOffset = 0x0004;
    public const ushort DreqControlOffset = 0x0006;
    public const ushort DreqSourceAddressOffset = 0x0008;
    public const ushort DreqDestinationAddressOffset = 0x000C;
    public const ushort DreqLengthOffset = 0x0010;
    public const ushort DreqFifoOffset = 0x0012;
    public const ushort CommunicationPortOffset = 0x0020;
    public const ushort PwmControlOffset = 0x0030;
    public const ushort PwmCycleOffset = 0x0032;
    public const ushort PwmLeftPulseWidthOffset = 0x0034;
    public const ushort PwmRightPulseWidthOffset = 0x0036;
    public const ushort PwmMonoPulseWidthOffset = 0x0038;
    public const ushort VResInterruptClearOffset = 0x0014;
    public const ushort VInterruptClearOffset = 0x0016;
    public const ushort HInterruptClearOffset = 0x0018;
    public const ushort CommandInterruptClearOffset = 0x001A;
    public const ushort PwmInterruptClearOffset = 0x001C;

    public const ushort BitmapModeOffset = 0x0000;
    public const ushort ScreenShiftControlOffset = 0x0002;
    public const ushort AutoFillLengthOffset = 0x0004;
    public const ushort AutoFillStartAddressOffset = 0x0006;
    public const ushort AutoFillDataOffset = 0x0008;
    public const ushort FrameBufferControlOffset = 0x000A;

    public static readonly string[] RequiredSubsystems =
    [
        "32X boot ROM handoff and security checks",
        "dual SH-2 interpreter with exceptions, interrupts, cache-through behavior, and DMA",
        "68000/SH-2 cartridge and register bus arbitration",
        "32X system registers, communication ports, FIFO, DREQ, and interrupt routing",
        "32X VDP framebuffers, palette, line tables, fill, shifts, priority, and MD compositing",
        "32X stereo PWM audio mixed with PSG/YM2612 output",
        "save-state, movie, trace, compatibility, and performance coverage for the added processors"
    ];

    public static uint M68kSystemRegister(ushort offset)
    {
        return M68kSystemRegisterStart + offset;
    }

    public static uint M68kVdpRegister(ushort offset)
    {
        return M68kVdpRegisterStart + offset;
    }

    public static uint Sh2SystemRegister(ushort offset)
    {
        return Sh2SystemRegisterStart + offset;
    }

    public static uint Sh2VdpRegister(ushort offset)
    {
        return Sh2VdpRegisterStart + offset;
    }
}
