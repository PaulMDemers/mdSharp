using MdSharp.Core.Cpu.Sh2;
using MdSharp.Core.Timing;

namespace MdSharp.Core.ThirtyTwoX;

public sealed class ThirtyTwoXDevice
{
    private static readonly bool EnableSh2FastPaths =
        Environment.GetEnvironmentVariable("MDSHARP_DISABLE_SH2_FASTPATHS") != "1";

    private static readonly bool EnableSh2ListFastPaths =
        EnableSh2FastPaths &&
        Environment.GetEnvironmentVariable("MDSHARP_ENABLE_SH2_LIST_FASTPATHS") == "1";

    private static readonly int Sh2FastPathCycleBudgetCap = ParseSh2FastPathCycleBudgetCap();

    private static readonly string Sh2DisabledFastPathGroups =
        Environment.GetEnvironmentVariable("MDSHARP_DISABLE_SH2_FASTPATH_GROUPS") ?? string.Empty;

    private const int SystemRegisterBytes = 0x100;
    private const int VdpRegisterBytes = 0x100;
    private const int Sh2PeripheralRegisterBytes = 0x100;
    private const int Sh2DmaRegisterBytes = 0x40;
    private const int OutputWidth = ThirtyTwoXHardwareProfile.NominalWidth;
    private const int OutputHeight = ThirtyTwoXHardwareProfile.NtscVisibleLines;
    private const uint Sh2PeripheralRegisterStart = 0xFFFF_FE00;
    private const uint Sh2PeripheralRegisterEnd = Sh2PeripheralRegisterStart + Sh2PeripheralRegisterBytes;
    private const uint Sh2FrtRegisterStart = 0xFFFF_FE10;
    private const uint Sh2FreeRunningCounterStart = 0xFFFF_FE12;
    private const uint Sh2FrtOutputCompareRegisterStart = 0xFFFF_FE14;
    private const uint Sh2FrtControlRegisterAddress = 0xFFFF_FE16;
    private const uint Sh2FrtControlStatusRegisterAddress = 0xFFFF_FE11;
    private const uint Sh2FrtInputCaptureRegisterStart = 0xFFFF_FE18;
    private const uint Sh2SciTransmitDataRegisterAddress = 0xFFFF_FE03;
    private const uint Sh2SciStatusRegisterAddress = 0xFFFF_FE04;
    private const uint Sh2SciReceiveDataRegisterAddress = 0xFFFF_FE05;
    private const uint Sh2WatchdogRegisterStart = 0xFFFF_FE80;
    private const uint Sh2WatchdogCounterAddress = 0xFFFF_FE81;
    private const uint Sh2WatchdogResetControlAddress = 0xFFFF_FE83;
    private const uint Sh2WatchdogVectorAddress = 0xFFFF_FEE4;
    private const uint Sh2InterruptPriorityRegisterAHighAddress = 0xFFFF_FEE3;
    private const uint Sh2InterruptPriorityRegisterBAddress = 0xFFFF_FE60;
    private const uint Sh2FrtVectorRegisterCAddress = 0xFFFF_FE66;
    private const uint Sh2FrtVectorRegisterDAddress = 0xFFFF_FE68;
    private const uint Sh2CacheControlRegisterAddress = 0xFFFF_FE92;
    private const uint Sh2DmaRegisterStart = 0xFFFF_FF80;
    private const uint Sh2DmaRegisterEnd = Sh2DmaRegisterStart + Sh2DmaRegisterBytes;
    private const uint Sh2DivisionUnitRegisterStart = 0xFFFF_FF00;
    private const uint Sh2DivisionUnitRegisterEnd = Sh2DivisionUnitRegisterStart + 0x20;
    private const uint Sh2DivisionDvcrAddress = 0xFFFF_FF08;
    private const uint Sh2DivisionVcrdivAddress = 0xFFFF_FF0C;
    private const uint Sh2DivisionRemainderAliasAddress = 0xFFFF_FF18;
    private const uint Sh2DivisionQuotientAliasAddress = 0xFFFF_FF1C;
    private const uint Sh2DmaRequestSelect0 = 0xFFFF_FE71;
    private const uint Sh2DmaRequestSelect1 = 0xFFFF_FE72;
    private const uint Sh2SlaveInputCaptureSignalStart = 0x2100_0000;
    private const uint Sh2MasterInputCaptureSignalStart = 0x2180_0000;
    private const uint Sh2InputCaptureSignalBytes = 0x0080_0000;
    private const uint Sh2SdramStackAliasStart = 0x0C00_0000;
    private const uint Sh2CacheDataArrayStart = 0xC000_0000;
    private const uint Sh2CacheDataArrayEnd = 0xE000_0000;
    private const uint Sh2CacheDataArrayBytes = ThirtyTwoXHardwareProfile.Sh2CacheBytes;
    private const uint Sh2PrivateWorkRamLowMirrorStart = 0xBFFF_F800;
    private const uint Sh2PrivateWorkRamLowMirrorEnd = 0xBFFF_FC00;
    private const uint Sh2PrivateWorkRamBytes = 0x800;
    private const uint Sh2BootRomMappedBytes = 0x1000;
    private const uint Sh2CachePurgeStart = 0x4000_0000;
    private const uint Sh2CachePurgeEnd = 0x6000_0000;
    private const uint Sh2CacheAddressArrayStart = 0x6000_0000;
    private const uint Sh2CacheAddressArrayEnd = 0x8000_0000;
    private const int Sh2CacheLineBytes = 16;
    private const int Sh2CacheEntriesPerWay = 64;
    private const int Sh2CacheWays = 4;
    private const uint Sh2CacheInvalidTag = 1u << 19;
    private const ushort Sh2DmaChannelEnable = 0x0001;
    private const ushort Sh2DmaTransferEnd = 0x0002;
    private const ushort Sh2DmaInterruptEnable = 0x0004;
    private const ushort Sh2DmaOperationEnable = 0x0001;
    private const uint Sh2DmaAutoRequestMode = 0x0200;
    private const byte Sh2DmaRequestSelectDreq = 0x00;
    private const byte Sh2SciStatusTransmitDataEmpty = 0x80;
    private const byte Sh2SciStatusReceiveDataFull = 0x40;
    private const byte Sh2SciStatusTransmitEnd = 0x04;
    private const int Sh2DmaSource0Offset = 0x00;
    private const int Sh2DmaDestination0Offset = 0x04;
    private const int Sh2DmaTransferCount0Offset = 0x08;
    private const int Sh2DmaChannelControl0Offset = 0x0C;
    private const int Sh2DmaChannelRegisterStride = 0x10;
    private const int Sh2DmaVector0Offset = 0x20;
    private const int Sh2DmaVector1Offset = 0x28;
    private const int Sh2DmaOperationOffset = 0x30;
    private const ushort AdapterControlAdapterEnable = 0x0001;
    private const ushort AdapterControlSh2ResetRelease = 0x0002;
    private const ushort AdapterControlSh2ResetEnable = 0x0080;
    private const ushort AdapterControlVdpAccessSh2 = 0x8000;
    private const ushort DreqControlRomToVramDma = 0x0001;
    private const ushort DreqControlDma = 0x0002;
    private const ushort DreqControlActive = 0x0004;
    private const ushort Sh2InterruptMaskPwm = 0x0001;
    private const ushort Sh2InterruptMaskCommand = 0x0002;
    private const ushort Sh2InterruptMaskHorizontal = 0x0004;
    private const ushort Sh2InterruptMaskVertical = 0x0008;
    private const ushort Sh2InterruptMaskHorizontalInVBlank = 0x0020;
    private const ushort PwmFifoFull = 0x8000;
    private const ushort PwmFifoEmpty = 0x4000;
    private const ushort PwmRoutingEnabledMask = 0x000F;
    private const ushort FrameBufferStatusVBlank = 0x8000;
    private const ushort FrameBufferStatusHBlank = 0x4000;
    private const ushort FrameBufferStatusPaletteAccess = 0x2000;
    private const ushort FrameBufferStatusFrameBufferDenied = 0x0002;
    private const ushort FrameBufferStatusFrameBufferSelect = 0x0001;
    private const int PwmHardwareFifoCapacity = 3;
    private const int DreqFifoCapacity = 8;
    private const uint Sh2DmaSourceIncrement = 0x1000;
    private const uint Sh2DmaSourceDecrement = 0x2000;
    private const uint Sh2DmaDestinationIncrement = 0x4000;
    private const uint Sh2DmaDestinationDecrement = 0x8000;
    private const int Sh2PwmInterruptVector = 67;
    private const int Sh2CommandInterruptVector = 68;
    private const int Sh2HorizontalInterruptVector = 69;
    private const int Sh2VerticalInterruptVector = 70;
    private const int Sh2VresInterruptVector = 71;
    private const int Sh2FrtInputCaptureInterruptVector = 64;
    private const int Sh2FrtOutputCompareInterruptVector = 65;
    private const int Sh2FrtOverflowInterruptVector = 66;
    private const int Sh2FrtDefaultInterruptLevel = 15;
    private const int Sh2SystemRegisterWaitCycles = 1;
    private const int Sh2VdpRegisterWaitCycles = 5;
    private const int Sh2PaletteWaitCycles = 5;
    private const int Sh2PaletteBusyWaitCycles = 40;
    private const int Sh2FrameBufferReadWaitCycles = 5;
    private const int Sh2FrameBufferWriteWaitCycles = 2;
    private const int Sh2FrameBufferBusyWaitCycles = 40;
    private const int Sh2SdramReadWaitCycles = 12;
    private const int Sh2SdramWriteWaitCycles = 2;
    private const int Sh2FrameBufferWordFillLoopCycles = 6;
    private const int Sh2FrameBufferWordFillLoopMaxBurstIterations = 32768;
    private const int Sh2LongStoreFillLoopCycles = 4;
    private const int Sh2LongStoreFillLoopMaxBurstIterations = 2048;
    private const int Sh2LongStoreDelayFillLoopCycles = 20;
    private const int Sh2LongStoreDelayFillLoopMaxBurstIterations = 4096;
    private const int Sh2MovWordStridedCopyMinBurstCycles = 4096;
    private const int Sh2MovWordStridedCopyMaxBurstCycles = 6 * 32768;
    private const int Sh2BraSelfIdleLoopTimerSensitiveBurstCycles = 32;
    private const int DreqBackpressureSh2Cycles = 64;
    private const int Sh2CartridgeByteWaitCycles = 6;
    private const int Sh2CachedCartridgeLineFillWaitCycles = 16;
    private const int Sh2CartridgeRvBlockedWaitCycles = 64;
    private const int CartridgeRomBusMasterCyclesPerByte = 12;
    private const int Sh2CommunicationSyncStepLimit = 32768;
    private const int Sh2CommunicationSyncCycleBudget = 64;
    private const byte Sh2FrtTierInputCaptureEnable = 0x80;
    private const byte Sh2FrtTierOutputCompareAEnable = 0x08;
    private const byte Sh2FrtTierOutputCompareBEnable = 0x04;
    private const byte Sh2FrtTierOverflowEnable = 0x02;
    private const byte Sh2FrtFtcsrInputCaptureFlag = 0x80;
    private const byte Sh2FrtFtcsrOutputCompareAFlag = 0x08;
    private const byte Sh2FrtFtcsrOutputCompareBFlag = 0x04;
    private const byte Sh2FrtFtcsrOverflowFlag = 0x02;
    private const byte Sh2FrtFtcsrWritableMask = 0x8E | Sh2FrtFtcsrOutputCompareBFlag;
    private const byte Sh2WatchdogWriteCounterKey = 0x5A;
    private const byte Sh2WatchdogWriteControlKey = 0xA5;
    private const byte Sh2WatchdogControlInitial = 0x18;
    private const byte Sh2WatchdogResetControlInitial = 0x1F;
    private const byte Sh2WatchdogTimerEnable = 0x20;
    private const byte Sh2WatchdogModeWatchdog = 0x40;
    private const byte Sh2WatchdogOverflow = 0x80;
    private const byte Sh2CacheControlPurge = 0x10;
    private const int Sh2DivisionRegisterCount = 6;
    private const int Sh2DivisionDvsrIndex = 0;
    private const int Sh2DivisionDvdntIndex = 1;
    private const int Sh2DivisionDvcrIndex = 2;
    private const int Sh2DivisionVcrdivIndex = 3;
    private const int Sh2DivisionDvdnthIndex = 4;
    private const int Sh2DivisionDvdntlIndex = 5;
    private const uint Sh2DivisionOverflowFlag = 0x0000_0001;
    private const uint Sh2DivisionOverflowInterruptEnable = 0x0000_0002;
    private static readonly byte[] Super32XId = [(byte)'M', (byte)'A', (byte)'R', (byte)'S'];
    private static readonly byte[] BootRomCommunicationSignature = [(byte)'M', (byte)'_', (byte)'O', (byte)'K', (byte)'S', (byte)'_', (byte)'O', (byte)'K'];
    private static readonly byte[] Sh2CacheLruSelect = BuildSh2CacheLruSelect();
    private static readonly byte[][] Sh2CacheLruUpdate = BuildSh2CacheLruUpdate();
    private static readonly int[] Sh2WatchdogDividers = [2, 64, 128, 256, 512, 1024, 4096, 8192];

    private readonly byte[] _sdram = new byte[ThirtyTwoXHardwareProfile.SdramBytes];
    private readonly byte[][] _frameBuffers =
    [
        new byte[ThirtyTwoXHardwareProfile.FrameBufferBytes],
        new byte[ThirtyTwoXHardwareProfile.FrameBufferBytes]
    ];
    private readonly byte[] _palette = new byte[ThirtyTwoXHardwareProfile.PaletteEntries * 2];
    private readonly byte[] _systemRegisters = new byte[SystemRegisterBytes];
    private readonly byte[] _m68kCommunicationStaleBytes = new byte[16];
    private readonly bool[] _m68kCommunicationStaleValid = new bool[16];
    private readonly ushort[] _m68kCommunicationStaleWords = new ushort[8];
    private readonly bool[] _m68kCommunicationStaleWordValid = new bool[8];
    private readonly bool[] _m68kCommunicationPendingHostBytes = new bool[16];
    private readonly bool[] _m68kCommunicationDeferredSh2ClearBytes = new bool[16];
    private readonly byte[] _vdpRegisters = new byte[VdpRegisterBytes];
    private readonly Queue<ushort> _pwmLeft = new(capacity: 4096);
    private readonly Queue<ushort> _pwmRight = new(capacity: 4096);
    private readonly Queue<ushort> _pwmMono = new(capacity: 4096);
    private readonly Queue<ushort> _pwmLeftHardwareFifo = new(capacity: PwmHardwareFifoCapacity);
    private readonly Queue<ushort> _pwmRightHardwareFifo = new(capacity: PwmHardwareFifoCapacity);
    private readonly Queue<ushort> _pwmMonoHardwareFifo = new(capacity: PwmHardwareFifoCapacity);
    private short[] _pwmLeftRenderBuffer = [];
    private short[] _pwmRightRenderBuffer = [];
    private readonly Queue<ushort> _dreqFifo = new(capacity: DreqFifoCapacity);
    private readonly byte[][] _sh2DmaRegisters =
    [
        new byte[Sh2DmaRegisterBytes],
        new byte[Sh2DmaRegisterBytes]
    ];
    private readonly byte[][] _sh2PeripheralRegisters =
    [
        new byte[Sh2PeripheralRegisterBytes],
        new byte[Sh2PeripheralRegisterBytes]
    ];
    private readonly byte[][] _sh2CacheDataArrays =
    [
        new byte[ThirtyTwoXHardwareProfile.Sh2CacheBytes],
        new byte[ThirtyTwoXHardwareProfile.Sh2CacheBytes]
    ];
    private readonly byte[][] _sh2CacheDataValid =
    [
        new byte[ThirtyTwoXHardwareProfile.Sh2CacheBytes],
        new byte[ThirtyTwoXHardwareProfile.Sh2CacheBytes]
    ];
    private readonly byte[][] _sh2PrivateWorkRam =
    [
        new byte[Sh2PrivateWorkRamBytes],
        new byte[Sh2PrivateWorkRamBytes]
    ];
    private readonly uint[][] _sh2CacheTags =
    [
        new uint[Sh2CacheWays * Sh2CacheEntriesPerWay],
        new uint[Sh2CacheWays * Sh2CacheEntriesPerWay]
    ];
    private readonly byte[][] _sh2CacheLru =
    [
        new byte[Sh2CacheEntriesPerWay],
        new byte[Sh2CacheEntriesPerWay]
    ];
    private readonly uint[][] _sh2DivisionRegisters =
    [
        new uint[Sh2DivisionRegisterCount],
        new uint[Sh2DivisionRegisterCount]
    ];
    private readonly int[] _sh2WatchdogCycleCounters = new int[ThirtyTwoXHardwareProfile.Sh2CpuCount];
    private readonly bool[] _sh2WatchdogInterruptPending = new bool[ThirtyTwoXHardwareProfile.Sh2CpuCount];
    private readonly byte[] _sh2WatchdogWriteSelect = new byte[ThirtyTwoXHardwareProfile.Sh2CpuCount];
    private readonly long[] _sh2FrtBaseCycles = new long[ThirtyTwoXHardwareProfile.Sh2CpuCount];
    private readonly ushort[] _sh2FrtBaseCounters = new ushort[ThirtyTwoXHardwareProfile.Sh2CpuCount];
    private readonly ushort[] _sh2FrtLastCounters = new ushort[ThirtyTwoXHardwareProfile.Sh2CpuCount];
    private readonly ushort[] _sh2FrtOutputCompareB = new ushort[ThirtyTwoXHardwareProfile.Sh2CpuCount];
    private readonly int[] _sh2WaitCycles = new int[ThirtyTwoXHardwareProfile.Sh2CpuCount];
    private readonly Dictionary<uint, byte[]>[] _sh2LowCartridgeCacheLines =
    [
        [],
        []
    ];
    private readonly Func<uint, ushort, bool>[] _sh2FrameBufferWordWriters = new Func<uint, ushort, bool>[ThirtyTwoXHardwareProfile.Sh2CpuCount];
    private readonly Func<uint, ushort?>[] _sh2WordReaders = new Func<uint, ushort?>[ThirtyTwoXHardwareProfile.Sh2CpuCount];
    private readonly Func<uint, ushort, bool>[] _sh2WordWriters = new Func<uint, ushort, bool>[ThirtyTwoXHardwareProfile.Sh2CpuCount];
    private readonly Func<uint, ushort?>[] _sh2FastCopyWordReaders = new Func<uint, ushort?>[ThirtyTwoXHardwareProfile.Sh2CpuCount];
    private readonly Func<uint, ushort, bool>[] _sh2FastCopyWordWriters = new Func<uint, ushort, bool>[ThirtyTwoXHardwareProfile.Sh2CpuCount];
    private readonly Func<uint, byte?>[] _sh2ByteReaders = new Func<uint, byte?>[ThirtyTwoXHardwareProfile.Sh2CpuCount];
    private readonly Func<uint, byte, bool>[] _sh2ByteWriters = new Func<uint, byte, bool>[ThirtyTwoXHardwareProfile.Sh2CpuCount];
    private readonly Func<uint, uint, bool>[] _sh2LongWriters = new Func<uint, uint, bool>[ThirtyTwoXHardwareProfile.Sh2CpuCount];
    private readonly Func<uint, uint?>[] _sh2LongReaders = new Func<uint, uint?>[ThirtyTwoXHardwareProfile.Sh2CpuCount];
    private readonly byte[] _sh2DmaRequestSelect = new byte[2];
    private readonly ReadOnlyMemory<byte> _cartridgeRom;
    private readonly byte[][] _sh2Bios =
    [
        [],
        []
    ];
    private readonly bool _useRealSh2BiosBoot;
    private readonly MarsUserHeader _userHeader;
    private readonly bool _pal;
    private bool _adapterEnabled;
    private bool _sh2ResetEnabled;
    private bool _sh2ResetReleased;
    private bool _vdpAccessGrantedToSh2;
    private bool _vBlank;
    private bool _hBlank;
    private int _currentScanline;
    private bool _frameBufferSwapPending;
    private int _pendingDrawFrameBufferIndex;
    private int _activeDisplayFrameBufferIndex;
    private int _visibleDisplayFrameBufferIndex;
    private int _requestedDisplayFrameBufferIndex;
    private ushort _latchedBitmapMode;
    private ushort _latchedScreenShiftControl;
    private bool _lastCompositeUsedFallback;
    private int _lastCompositeMode;
    private int _vdpRegisterWriteCount;
    private int _bitmapModeWriteCount;
    private int _frameBufferControlWriteCount;
    private int _frameBufferByteWriteCount;
    private int _deniedFrameBufferAccessCount;
    private int _paletteByteWriteCount;
    private int _lastCompositeWrittenPixels;
    private int _dreqFifoWriteCount;
    private int _dreqDmaWordTransferCount;
    private ushort _sideEffectPreviousSystemRegisterWord;
    private ushort _lastBitmapModeWrite;
    private ushort _lastFrameBufferControlWrite;
    private double _pwmLeftLevel;
    private double _pwmRightLevel;
    private double _pwmMonoLevel;
    private ushort _masterInterruptMask;
    private ushort _slaveInterruptMask;
    private ushort _m68kCartridgeBank;
    private long _currentMasterCycle;
    private long _cartridgeRomBusBusyUntilMasterCycle;
    private bool _masterVerticalInterruptPending;
    private bool _slaveVerticalInterruptPending;
    private bool _masterVresInterruptPending;
    private bool _slaveVresInterruptPending;
    private bool _masterHorizontalInterruptPending;
    private bool _slaveHorizontalInterruptPending;
    private byte _horizontalInterruptPeriod;
    private byte _horizontalInterruptCounter;
    private bool _masterCommandInterruptPending;
    private bool _slaveCommandInterruptPending;
    private bool _masterPwmInterruptPending;
    private bool _slavePwmInterruptPending;
    private int _pwmCycleCounter;
    private int _pwmTimerCounter;
    private bool _bootRomHandshakePending;
    private bool _bootRomSignatureRead;
    private bool _bootRomSignatureReadbackActive;
    private bool _bootRomLaunchPending;
    private bool _bootRomPostStartSignaturePending;
    private bool _bootRomPostStartSignatureHiddenFromSh2;
    private byte _bootRomPostStartSignatureReadMask;
    private byte _bootRomPostStartHostClearProtectMask;
    private bool _bootRomChecksumPublished;
    private bool _bootRomChecksumHostCleared;
    private bool _bootRomSixtyEightUpPending;
    private bool _bootRomSixtyEightUpReadyHiddenFromSh2;
    private bool _m68kVdpControlMailboxArmed;
    private bool _m68kVdpControlMailboxHighPending;
    private bool _sh2CommunicationSyncActive;
    private bool _runningSh2Dma;

    public ThirtyTwoXDevice(ReadOnlyMemory<byte> cartridgeRom = default, bool pal = false, ReadOnlyMemory<byte>? masterSh2Bios = null, ReadOnlyMemory<byte>? slaveSh2Bios = null, bool useRealSh2BiosBoot = false)
    {
        _cartridgeRom = cartridgeRom;
        _pal = pal;
        if (masterSh2Bios.HasValue && !masterSh2Bios.Value.IsEmpty)
        {
            _sh2Bios[0] = masterSh2Bios.Value.ToArray();
        }

        if (slaveSh2Bios.HasValue && !slaveSh2Bios.Value.IsEmpty)
        {
            _sh2Bios[1] = slaveSh2Bios.Value.ToArray();
        }

        _useRealSh2BiosBoot = useRealSh2BiosBoot && HasCompleteSh2BiosSet();
        _userHeader = MarsUserHeader.Parse(cartridgeRom.Span);
        Sh2MemoryBus masterBus = new(this, cpuIndex: 0);
        Sh2MemoryBus slaveBus = new(this, cpuIndex: 1);
        MasterSh2 = new Sh2Cpu(masterBus, "32X master SH-2");
        SlaveSh2 = new Sh2Cpu(slaveBus, "32X slave SH-2");
        MasterSh2.InterruptAccepted = (level, vectorNumber) => OnSh2InterruptAccepted(cpuIndex: 0, level, vectorNumber);
        SlaveSh2.InterruptAccepted = (level, vectorNumber) => OnSh2InterruptAccepted(cpuIndex: 1, level, vectorNumber);
        _sh2FrameBufferWordWriters[0] = (address, value) => TryWriteSh2FrameBufferWordFast(0, address, value);
        _sh2FrameBufferWordWriters[1] = (address, value) => TryWriteSh2FrameBufferWordFast(1, address, value);
        _sh2WordReaders[0] = address => ReadSh2Word(address, 0);
        _sh2WordReaders[1] = address => ReadSh2Word(address, 1);
        _sh2WordWriters[0] = (address, value) =>
        {
            WriteSh2Word(address, value, 0);
            return true;
        };
        _sh2WordWriters[1] = (address, value) =>
        {
            WriteSh2Word(address, value, 1);
            return true;
        };
        _sh2FastCopyWordReaders[0] = address => TryReadSh2WordForFastCopy(address, 0, out ushort value) ? value : null;
        _sh2FastCopyWordReaders[1] = address => TryReadSh2WordForFastCopy(address, 1, out ushort value) ? value : null;
        _sh2FastCopyWordWriters[0] = (address, value) => TryWriteSh2WordForFastCopy(address, value, 0);
        _sh2FastCopyWordWriters[1] = (address, value) => TryWriteSh2WordForFastCopy(address, value, 1);
        _sh2ByteReaders[0] = address => TryReadSh2ByteFast(address, 0, out byte value) ? value : null;
        _sh2ByteReaders[1] = address => TryReadSh2ByteFast(address, 1, out byte value) ? value : null;
        _sh2ByteWriters[0] = (address, value) => TryWriteSh2ByteFast(address, value, 0);
        _sh2ByteWriters[1] = (address, value) => TryWriteSh2ByteFast(address, value, 1);
        _sh2LongWriters[0] = (address, value) => TryWriteSh2LongFast(address, value, 0);
        _sh2LongWriters[1] = (address, value) => TryWriteSh2LongFast(address, value, 1);
        _sh2LongReaders[0] = address => TryReadSh2LongNoAllocate(address, 0);
        _sh2LongReaders[1] = address => TryReadSh2LongNoAllocate(address, 1);
        ResetSh2CacheTags();
    }

    public Sh2Cpu MasterSh2 { get; }
    public Sh2Cpu SlaveSh2 { get; }
    public ReadOnlySpan<byte> Sdram => _sdram;
    public ReadOnlySpan<byte> DrawFrameBuffer => _frameBuffers[DrawFrameBufferIndex];
    public ReadOnlySpan<byte> DisplayFrameBuffer => _frameBuffers[DisplayFrameBufferIndex];
    public ReadOnlySpan<byte> Palette => _palette;
    public int DrawFrameBufferIndex => LogicalDisplayFrameBufferToPhysicalIndex(_activeDisplayFrameBufferIndex) ^ 1;
    public int DisplayFrameBufferIndex => LogicalDisplayFrameBufferToPhysicalIndex(_activeDisplayFrameBufferIndex);
    public int RequestedDisplayFrameBufferIndex => _requestedDisplayFrameBufferIndex;
    public bool VBlank => _vBlank;
    public bool HBlank => _hBlank;
    public bool FrameBufferSwapPending => _frameBufferSwapPending;
    public bool LastCompositeUsedFallback => _lastCompositeUsedFallback;
    public int LastCompositeMode => _lastCompositeMode;
    public int VdpRegisterWriteCount => _vdpRegisterWriteCount;
    public int BitmapModeWriteCount => _bitmapModeWriteCount;
    public int FrameBufferControlWriteCount => _frameBufferControlWriteCount;
    public int FrameBufferByteWriteCount => _frameBufferByteWriteCount;
    public int DeniedFrameBufferAccessCount => _deniedFrameBufferAccessCount;
    public int PaletteByteWriteCount => _paletteByteWriteCount;
    public int LastCompositeWrittenPixels => _lastCompositeWrittenPixels;
    public int DreqFifoWriteCount => _dreqFifoWriteCount;
    public int DreqDmaWordTransferCount => _dreqDmaWordTransferCount;
    public int DreqFifoCount => _dreqFifo.Count;
    public int EmptyDescriptorSpanFastPathAttempts { get; private set; }
    public int EmptyDescriptorSpanFastPathHits { get; private set; }
    public int EmptyDescriptorSpanTailFastPathAttempts { get; private set; }
    public int EmptyDescriptorSpanTailFastPathHits { get; private set; }
    public int MovWordStridedCopyFastPathAttempts { get; private set; }
    public int MovWordStridedCopyFastPathHits { get; private set; }
    public int LongDifferencePollFastPathAttempts { get; private set; }
    public int LongDifferencePollFastPathHits { get; private set; }
    public int SdramFlagTaskletDispatcherFastPathAttempts { get; private set; }
    public int SdramFlagTaskletDispatcherFastPathHits { get; private set; }
    public int GbrBytePairInterruptIdleFastPathAttempts { get; private set; }
    public int GbrBytePairInterruptIdleFastPathHits { get; private set; }
    public int GbrByteZeroComm20PollFastPathAttempts { get; private set; }
    public int GbrByteZeroComm20PollFastPathHits { get; private set; }
    public int LiteralByteDisplacementTstRegisterPollFastPathAttempts { get; private set; }
    public int LiteralByteDisplacementTstRegisterPollFastPathHits { get; private set; }
    public int ByteDisplacementZeroWaitFastPathAttempts { get; private set; }
    public int ByteDisplacementZeroWaitFastPathHits { get; private set; }
    public int TwoStageWordZeroPollRingFastPathAttempts { get; private set; }
    public int TwoStageWordZeroPollRingFastPathHits { get; private set; }
    public int StableWordPairPollFastPathAttempts { get; private set; }
    public int StableWordPairPollFastPathHits { get; private set; }
    public int GbrLongMaskedOrComparePollFastPathAttempts { get; private set; }
    public int GbrLongMaskedOrComparePollFastPathHits { get; private set; }
    public long Sh2RunCycleCalls { get; private set; }
    public long Sh2RunCycleBudgetTotal { get; private set; }
    public int Sh2RunCycleBudgetMin { get; private set; }
    public int Sh2RunCycleBudgetMax { get; private set; }
    public long Sh2RunCycleStepCalls { get; private set; }
    public long Sh2FastPathCycles { get; private set; }
    public long Sh2FastPathHits { get; private set; }
    public int Sh2FastPathMaxCycles { get; private set; }
    public ushort LastBitmapModeWrite => _lastBitmapModeWrite;
    public ushort LastFrameBufferControlWrite => _lastFrameBufferControlWrite;
    public ushort MasterInterruptMask => BuildSh2InterruptMask(cpuIndex: 0);
    public ushort SlaveInterruptMask => BuildSh2InterruptMask(cpuIndex: 1);
    public MarsUserHeader UserHeader => _userHeader;
    public Action<SystemRegisterWriteTrace>? SystemRegisterWriteObserver { get; set; }
    public Action<SystemRegisterAccessTrace>? SystemRegisterAccessObserver { get; set; }
    public Action<SystemRegisterAccessTrace>? VdpRegisterAccessObserver { get; set; }
    public Action<Sh2MemoryAccessTrace>? Sh2MemoryAccessObserver { get; set; }
    public Action<PaletteAccessTrace>? PaletteAccessObserver { get; set; }
    public Action<FrameBufferAccessTrace>? FrameBufferAccessObserver { get; set; }
    public Action<Sh2Cpu.Sh2LinkedListTrace>? Sh2LinkedListObserver { get; set; }
    public Action<Sh2Cpu.Sh2RechainTrace>? Sh2RechainObserver { get; set; }
    public Action<int, uint, ushort>? Sh2PcSampleObserver { get; set; }
    public Func<uint, bool>? Sh2MemoryAccessTraceFilter { get; set; }
    public Action<SdramWriteTrace>? SdramWriteObserver { get; set; }
    public Func<int, bool>? SdramWriteTraceFilter { get; set; }
    public bool AdapterEnabled => _adapterEnabled;
    public bool Sh2ResetEnabled => _sh2ResetEnabled;
    public bool Sh2ResetReleased => _sh2ResetReleased;
    public bool Sh2HeldInReset => !_adapterEnabled || (_sh2ResetEnabled && !_sh2ResetReleased);
    public bool VdpAccessGrantedToSh2 => _vdpAccessGrantedToSh2;
    public bool BootRomHandshakePending => _bootRomHandshakePending;
    public bool BootRomSignatureRead => _bootRomSignatureRead;
    public bool BootRomLaunchPending => _bootRomLaunchPending;
    public bool BootRomPostStartSignaturePending => _bootRomPostStartSignaturePending;
    public bool BootRomPostStartSignatureHiddenFromSh2 => _bootRomPostStartSignatureHiddenFromSh2;
    public byte BootRomPostStartSignatureReadMask => _bootRomPostStartSignatureReadMask;
    public bool RomToVramDmaActive => IsSh2RomBlockedByRv();
    public int M68kCartridgeBank => _m68kCartridgeBank & 0x03;

    public ushort DebugPeekSystemRegisterWord(ushort offset)
    {
        return ReadBigEndianWord(_systemRegisters, offset & (SystemRegisterBytes - 1));
    }

    public void Reset()
    {
        Array.Clear(_sdram);
        Array.Clear(_frameBuffers[0]);
        Array.Clear(_frameBuffers[1]);
        Array.Clear(_palette);
        Array.Clear(_systemRegisters);
        Array.Clear(_m68kCommunicationStaleBytes);
        Array.Clear(_m68kCommunicationStaleValid);
        Array.Clear(_m68kCommunicationStaleWords);
        Array.Clear(_m68kCommunicationStaleWordValid);
        Array.Clear(_m68kCommunicationPendingHostBytes);
        Array.Clear(_m68kCommunicationDeferredSh2ClearBytes);
        Array.Clear(_vdpRegisters);
        _pwmLeft.Clear();
        _pwmRight.Clear();
        _pwmMono.Clear();
        _pwmLeftHardwareFifo.Clear();
        _pwmRightHardwareFifo.Clear();
        _pwmMonoHardwareFifo.Clear();
        _dreqFifo.Clear();
        Array.Clear(_sh2DmaRegisters[0]);
        Array.Clear(_sh2DmaRegisters[1]);
        Array.Clear(_sh2PeripheralRegisters[0]);
        Array.Clear(_sh2PeripheralRegisters[1]);
        Array.Clear(_sh2CacheDataArrays[0]);
        Array.Clear(_sh2CacheDataArrays[1]);
        Array.Clear(_sh2CacheDataValid[0]);
        Array.Clear(_sh2CacheDataValid[1]);
        Array.Clear(_sh2PrivateWorkRam[0]);
        Array.Clear(_sh2PrivateWorkRam[1]);
        ResetSh2CacheTags();
        Array.Clear(_sh2DivisionRegisters[0]);
        Array.Clear(_sh2DivisionRegisters[1]);
        Array.Clear(_sh2WatchdogCycleCounters);
        Array.Clear(_sh2WatchdogInterruptPending);
        Array.Clear(_sh2WatchdogWriteSelect);
        Array.Clear(_sh2FrtBaseCycles);
        Array.Clear(_sh2FrtBaseCounters);
        Array.Clear(_sh2FrtLastCounters);
        _sh2LowCartridgeCacheLines[0].Clear();
        _sh2LowCartridgeCacheLines[1].Clear();
        ResetSh2PeripheralDefaults();
        ResetSh2DmaDefaults();
        Array.Clear(_sh2DmaRequestSelect);
        _activeDisplayFrameBufferIndex = 0;
        _visibleDisplayFrameBufferIndex = 0;
        _requestedDisplayFrameBufferIndex = 0;
        _pendingDrawFrameBufferIndex = DrawFrameBufferIndex;
        _frameBufferSwapPending = false;
        _vBlank = false;
        _hBlank = false;
        _lastCompositeUsedFallback = false;
        _lastCompositeMode = 0;
        _vdpRegisterWriteCount = 0;
        _bitmapModeWriteCount = 0;
        _frameBufferControlWriteCount = 0;
        _frameBufferByteWriteCount = 0;
        _deniedFrameBufferAccessCount = 0;
        _paletteByteWriteCount = 0;
        _lastCompositeWrittenPixels = 0;
        _dreqFifoWriteCount = 0;
        _dreqDmaWordTransferCount = 0;
        _lastBitmapModeWrite = 0;
        _lastFrameBufferControlWrite = 0;
        _masterInterruptMask = 0;
        _slaveInterruptMask = 0;
        _m68kCartridgeBank = 0;
        _currentMasterCycle = 0;
        _cartridgeRomBusBusyUntilMasterCycle = 0;
        _masterVerticalInterruptPending = false;
        _slaveVerticalInterruptPending = false;
        _masterVresInterruptPending = false;
        _slaveVresInterruptPending = false;
        _masterHorizontalInterruptPending = false;
        _slaveHorizontalInterruptPending = false;
        _horizontalInterruptPeriod = 0;
        _horizontalInterruptCounter = 0;
        _masterCommandInterruptPending = false;
        _slaveCommandInterruptPending = false;
        _masterPwmInterruptPending = false;
        _slavePwmInterruptPending = false;
        _pwmCycleCounter = 0;
        _pwmTimerCounter = 0;
        _bootRomHandshakePending = false;
        _bootRomSignatureRead = false;
        _bootRomSignatureReadbackActive = false;
        _bootRomLaunchPending = false;
        _bootRomPostStartSignaturePending = false;
        _bootRomPostStartSignatureHiddenFromSh2 = false;
        _bootRomPostStartSignatureReadMask = 0;
        _bootRomPostStartHostClearProtectMask = 0;
        _bootRomChecksumPublished = false;
        _bootRomChecksumHostCleared = false;
        _bootRomSixtyEightUpPending = false;
        _bootRomSixtyEightUpReadyHiddenFromSh2 = false;
        _m68kVdpControlMailboxArmed = false;
        _m68kVdpControlMailboxHighPending = false;
        _adapterEnabled = false;
        _sh2ResetEnabled = true;
        _sh2ResetReleased = false;
        _vdpAccessGrantedToSh2 = false;
        WriteBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.AdapterControlOffset, AdapterControlSh2ResetEnable);
        WriteBigEndianWord(_vdpRegisters, ThirtyTwoXHardwareProfile.BitmapModeOffset, _pal ? (ushort)0x0000 : (ushort)0x8000);
        _latchedBitmapMode = ReadBigEndianWord(_vdpRegisters, ThirtyTwoXHardwareProfile.BitmapModeOffset);
        _latchedScreenShiftControl = 0;
        ResetSh2ForStartup();
    }

    public void BeginFrame(bool pal)
    {
        _visibleDisplayFrameBufferIndex = _activeDisplayFrameBufferIndex;
        _vBlank = false;
        _hBlank = false;
        _currentScanline = 0;
        if (CanSwitchFrameBufferNow())
        {
            CompletePendingFrameBufferSwap();
        }
    }

    public void StepScanline(int scanline, bool pal)
    {
        int visibleLines = pal ? ThirtyTwoXHardwareProfile.PalVisibleLines : ThirtyTwoXHardwareProfile.NtscVisibleLines;
        _currentScanline = Math.Clamp(scanline, 0, pal ? 311 : 261);
        bool nowVBlank = scanline >= visibleLines;
        if (nowVBlank && !_vBlank)
        {
            LatchVdpDisplayControls();
            _vBlank = true;
            CompletePendingFrameBufferSwap();
            _masterVerticalInterruptPending = true;
            _slaveVerticalInterruptPending = true;
            RequestPendingInterrupts();
        }
        else
        {
            _vBlank = nowVBlank;
        }
    }

    public void TriggerResetButtonInterrupt()
    {
        if (!_adapterEnabled || !_sh2ResetReleased)
        {
            return;
        }

        _masterVresInterruptPending = true;
        _slaveVresInterruptPending = true;
        RequestPendingInterrupts();
    }

    public void SetHBlank(bool hBlank)
    {
        if (!_hBlank && hBlank)
        {
            RequestHorizontalInterruptIfDue();
        }

        if (_hBlank && !hBlank)
        {
            LatchVdpDisplayControls();
            if (CanSwitchFrameBufferNow())
            {
                CompletePendingFrameBufferSwap();
            }
        }

        _hBlank = hBlank;
    }

    public void GrantVdpAccessToSh2()
    {
        _vdpAccessGrantedToSh2 = true;
    }

    public void ResetSh2(uint masterPc = ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart, uint slavePc = ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart)
    {
        Array.Clear(_sh2PeripheralRegisters[0]);
        Array.Clear(_sh2PeripheralRegisters[1]);
        Array.Clear(_sh2WatchdogCycleCounters);
        Array.Clear(_sh2WatchdogInterruptPending);
        Array.Clear(_sh2WatchdogWriteSelect);
        Array.Clear(_sh2FrtBaseCycles);
        Array.Clear(_sh2FrtBaseCounters);
        Array.Clear(_sh2FrtLastCounters);
        ResetSh2PeripheralDefaults();
        Array.Clear(_sh2DmaRegisters[0]);
        Array.Clear(_sh2DmaRegisters[1]);
        ResetSh2DmaDefaults();
        MasterSh2.Reset(masterPc);
        SlaveSh2.Reset(slavePc);
        ResetSh2FrtBaseCycles();
    }

    public void ResetSh2FromUserHeader()
    {
        Array.Clear(_sdram);
        if (_userHeader.IsValid && _userHeader.InitialSize > 0)
        {
            CopyInitialProgramToSdram(_userHeader.InitialSource, _userHeader.InitialDestination, _userHeader.InitialSize);
        }

        uint master = _userHeader.IsValid ? NormalizeSh2ProgramAddress(_userHeader.MasterStart) : ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart;
        uint slave = _userHeader.IsValid ? NormalizeSh2ProgramAddress(_userHeader.SlaveStart) : ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart;
        Array.Clear(_sh2DmaRegisters[0]);
        Array.Clear(_sh2DmaRegisters[1]);
        ResetSh2DmaDefaults();
        MasterSh2.Reset(master);
        SlaveSh2.Reset(slave);
        MasterSh2.SetGbr(Sh2SciStatusRegisterAddress);
        SlaveSh2.SetGbr(Sh2SciStatusRegisterAddress);
        ResetSh2FrtBaseCycles();
        if (_userHeader.IsValid)
        {
            uint masterVectorBase = NormalizeSh2ProgramAddress(_userHeader.MasterVectorBase);
            uint slaveVectorBase = NormalizeSh2ProgramAddress(_userHeader.SlaveVectorBase);
            MasterSh2.SetVbr(masterVectorBase);
            SlaveSh2.SetVbr(slaveVectorBase);
            MasterSh2.R[15] = ReadSh2Long(masterVectorBase + 4, cpuIndex: 0);
            SlaveSh2.R[15] = ReadSh2Long(slaveVectorBase + 4, cpuIndex: 1);
        }
    }

    private void ResetSh2ForStartup()
    {
        if (_useRealSh2BiosBoot)
        {
            ResetSh2FromBootRom();
        }
        else
        {
            ResetSh2FromUserHeader();
        }
    }

    private void ResetSh2FromBootRom()
    {
        Array.Clear(_sdram);
        ResetSh2CacheTags();
        ResetSh2PeripheralDefaults();
        Array.Clear(_sh2DmaRegisters[0]);
        Array.Clear(_sh2DmaRegisters[1]);
        ResetSh2DmaDefaults();
        MasterSh2.Reset(ReadSh2BiosLong(cpuIndex: 0, offset: 0));
        SlaveSh2.Reset(ReadSh2BiosLong(cpuIndex: 1, offset: 0));
        MasterSh2.R[15] = ReadSh2BiosLong(cpuIndex: 0, offset: 4);
        SlaveSh2.R[15] = ReadSh2BiosLong(cpuIndex: 1, offset: 4);
        MasterSh2.SetVbr(0);
        SlaveSh2.SetVbr(0);
        ResetSh2FrtBaseCycles();
    }

    private bool HasCompleteSh2BiosSet()
    {
        return _sh2Bios[0].Length >= 8 && _sh2Bios[1].Length >= 8;
    }

    private uint ReadSh2BiosLong(int cpuIndex, int offset)
    {
        byte[] bios = _sh2Bios[cpuIndex & 1];
        if (offset < 0 || offset + 3 >= bios.Length)
        {
            return 0;
        }

        return (uint)((bios[offset] << 24) |
            (bios[offset + 1] << 16) |
            (bios[offset + 2] << 8) |
            bios[offset + 3]);
    }

    public int RunSh2(int maxInstructionsPerCpu)
    {
        RetireNonLaunchPostStartSignatureBeforeSh2Run();
        if (Sh2HeldInReset || _bootRomHandshakePending || _bootRomLaunchPending)
        {
            return 0;
        }

        int executed = 0;
        long masterStartCycles = MasterSh2.Cycles;
        long slaveStartCycles = SlaveSh2.Cycles;
        long lastPwmElapsedCycles = 0;
        for (int i = 0; i < maxInstructionsPerCpu; i++)
        {
            RequestPendingInterrupts();
            if (!MasterSh2.Halted || MasterSh2.HasAcceptablePendingInterrupt)
            {
                StepSh2Cpu(0);
                AdvancePwmTimerToElapsed(masterStartCycles, slaveStartCycles, ref lastPwmElapsedCycles);
                executed++;
            }

            RequestPendingInterrupts();
            if (!SlaveSh2.Halted || SlaveSh2.HasAcceptablePendingInterrupt)
            {
                StepSh2Cpu(1);
                AdvancePwmTimerToElapsed(masterStartCycles, slaveStartCycles, ref lastPwmElapsedCycles);
                executed++;
            }

            if (MasterSh2.Halted && SlaveSh2.Halted && !MasterSh2.HasAcceptablePendingInterrupt && !SlaveSh2.HasAcceptablePendingInterrupt)
            {
                break;
            }
        }

        return executed;
    }

    public int RunSh2Cycles(int maxCyclesPerCpu)
    {
        RetireNonLaunchPostStartSignatureBeforeSh2Run();
        if (Sh2HeldInReset || _bootRomHandshakePending || _bootRomLaunchPending || maxCyclesPerCpu <= 0)
        {
            return 0;
        }

        RecordSh2RunCycleBudget(maxCyclesPerCpu);
        long masterStartCycles = MasterSh2.Cycles;
        long slaveStartCycles = SlaveSh2.Cycles;
        long runStartMasterCycle = _currentMasterCycle;
        long lastPwmElapsedCycles = 0;
        int executed = 0;
        while (true)
        {
            bool ranAny = false;
            if ((!MasterSh2.Halted || MasterSh2.HasAcceptablePendingInterrupt) && MasterSh2.Cycles - masterStartCycles < maxCyclesPerCpu)
            {
                AdvanceCurrentMasterCycleForSh2Elapsed(runStartMasterCycle, masterStartCycles, slaveStartCycles);
                RequestPendingInterrupts();
                StepSh2Cpu(0, maxCyclesPerCpu - (int)Math.Min(MasterSh2.Cycles - masterStartCycles, int.MaxValue));
                Sh2RunCycleStepCalls++;
                AdvanceCurrentMasterCycleForSh2Elapsed(runStartMasterCycle, masterStartCycles, slaveStartCycles);
                AdvancePwmTimerToElapsed(masterStartCycles, slaveStartCycles, ref lastPwmElapsedCycles);
                executed++;
                ranAny = true;
            }

            if ((!SlaveSh2.Halted || SlaveSh2.HasAcceptablePendingInterrupt) && SlaveSh2.Cycles - slaveStartCycles < maxCyclesPerCpu)
            {
                AdvanceCurrentMasterCycleForSh2Elapsed(runStartMasterCycle, masterStartCycles, slaveStartCycles);
                RequestPendingInterrupts();
                StepSh2Cpu(1, maxCyclesPerCpu - (int)Math.Min(SlaveSh2.Cycles - slaveStartCycles, int.MaxValue));
                Sh2RunCycleStepCalls++;
                AdvanceCurrentMasterCycleForSh2Elapsed(runStartMasterCycle, masterStartCycles, slaveStartCycles);
                AdvancePwmTimerToElapsed(masterStartCycles, slaveStartCycles, ref lastPwmElapsedCycles);
                executed++;
                ranAny = true;
            }

            if (!ranAny || (MasterSh2.Halted && SlaveSh2.Halted && !MasterSh2.HasAcceptablePendingInterrupt && !SlaveSh2.HasAcceptablePendingInterrupt))
            {
                break;
            }
        }

        return executed;
    }

    private void RecordSh2RunCycleBudget(int maxCyclesPerCpu)
    {
        Sh2RunCycleCalls++;
        Sh2RunCycleBudgetTotal += maxCyclesPerCpu;
        Sh2RunCycleBudgetMin = Sh2RunCycleBudgetMin == 0
            ? maxCyclesPerCpu
            : Math.Min(Sh2RunCycleBudgetMin, maxCyclesPerCpu);
        Sh2RunCycleBudgetMax = Math.Max(Sh2RunCycleBudgetMax, maxCyclesPerCpu);
    }

    private void RecordSh2FastPath(int cycles)
    {
        if (cycles <= 0)
        {
            return;
        }

        Sh2FastPathHits++;
        Sh2FastPathCycles += cycles;
        Sh2FastPathMaxCycles = Math.Max(Sh2FastPathMaxCycles, cycles);
    }

    private void AdvanceCurrentMasterCycleForSh2Elapsed(long runStartMasterCycle, long masterStartCycles, long slaveStartCycles)
    {
        long elapsedSh2Cycles = Math.Max(MasterSh2.Cycles - masterStartCycles, SlaveSh2.Cycles - slaveStartCycles);
        if (elapsedSh2Cycles <= 0)
        {
            return;
        }

        double masterClock = _pal ? GenesisScheduler.PalMasterClock : GenesisScheduler.NtscMasterClock;
        double sh2Clock = _pal ? ThirtyTwoXHardwareProfile.PalSh2ClockHz : ThirtyTwoXHardwareProfile.NtscSh2ClockHz;
        long elapsedMasterCycles = (long)(elapsedSh2Cycles * (masterClock / sh2Clock));
        if (elapsedMasterCycles > 0)
        {
            _currentMasterCycle = Math.Max(_currentMasterCycle, runStartMasterCycle + elapsedMasterCycles);
        }
    }

    private int StepSh2Cpu(int cpuIndex)
    {
        return StepSh2Cpu(cpuIndex, int.MaxValue);
    }

    private int StepSh2Cpu(int cpuIndex, int cycleBudget)
    {
        Sh2Cpu cpu = cpuIndex == 0 ? MasterSh2 : SlaveSh2;
        int fastCycles;
        ushort nextOpcode = 0;
        bool canProbeFastPath = EnableSh2FastPaths &&
            !cpu.Halted &&
            !cpu.HasAcceptablePendingInterrupt &&
            !cpu.DelaySlotActive &&
            cpu.InstructionObserver is null &&
            TryPeekSh2Word(cpu.PC, cpuIndex, out nextOpcode);
        int fastPathCycleBudget = Sh2FastPathCycleBudgetCap <= 0
            ? cycleBudget
            : Math.Min(cycleBudget, Sh2FastPathCycleBudgetCap);
        Action<int, uint, ushort>? pcSampleObserver = Sh2PcSampleObserver;
        if (pcSampleObserver is not null)
        {
            ushort sampledOpcode = nextOpcode;
            if (!canProbeFastPath)
            {
                _ = TryPeekSh2Word(cpu.PC, cpuIndex, out sampledOpcode);
            }

            pcSampleObserver(cpuIndex, cpu.PC, sampledOpcode);
        }

        cycleBudget = fastPathCycleBudget;

        if (canProbeFastPath && IsSh2FastPathGroupEnabled("early"))
        {
            if (IsSh2FastPathGroupEnabled("idle"))
            {
                int braIdleBudget = IsPwmTimerActive()
                    ? Math.Min(fastPathCycleBudget, Sh2BraSelfIdleLoopTimerSensitiveBurstCycles)
                    : fastPathCycleBudget;
                if ((nextOpcode == 0x0009 || (nextOpcode & 0xF000) == 0xA000) &&
                    cpu.TryFastForwardBraSelfNopIdleLoop(braIdleBudget, out fastCycles))
                {
                    RecordSh2FastPath(fastCycles);
                    AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                    return fastCycles;
                }

                if ((nextOpcode == 0x0009 || (nextOpcode & 0xF000) == 0x7000 || (nextOpcode & 0xF000) == 0xA000) &&
                    cpu.TryFastForwardAddBraNopDelayLoop(fastPathCycleBudget, out fastCycles))
                {
                    RecordSh2FastPath(fastCycles);
                    AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                    return fastCycles;
                }

                if (((nextOpcode & 0xF00F) == 0x6001 ||
                        (nextOpcode & 0xF0FF) == 0x4011 ||
                        (nextOpcode & 0xFF00) == 0x8900) &&
                    cpu.TryFastForwardWordLoadCmpPzBtIdleLoop(cycleBudget, out fastCycles))
                {
                    RecordSh2FastPath(fastCycles);
                    AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                    return fastCycles;
                }
            }

            if (IsSh2FastPathGroupEnabled("compute") &&
                nextOpcode == 0x4421 &&
                cpu.TryFastForwardRepeatedSharR4Rts(fastPathCycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memory") &&
                IsSh2FastPathGroupEnabled("memstore") &&
                (nextOpcode & 0xF00F) == 0x2001)
            {
                int fillLoopBudget = Math.Min(cycleBudget, Sh2FrameBufferWordFillLoopCycles * Sh2FrameBufferWordFillLoopMaxBurstIterations);
                if (cpu.TryFastForwardMovWStoreAddDtBfLoop(
                        fillLoopBudget,
                        _sh2FrameBufferWordWriters[cpuIndex],
                        Sh2FrameBufferWordFillLoopCycles,
                        out fastCycles))
                {
                    RecordSh2FastPath(fastCycles);
                    AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                    return fastCycles;
                }
            }

            if (IsSh2FastPathGroupEnabled("memory"))
            {
                if (IsSh2FastPathGroupEnabled("memstore") &&
                    ((nextOpcode & 0xF00F) == 0x2001 ||
                        (nextOpcode & 0xF0FF) == 0x4010 ||
                        (nextOpcode & 0xFF00) == 0x8F00 ||
                        (nextOpcode & 0xF000) == 0x7000) &&
                    cpu.TryFastForwardMovWStoreDtBfSAddLoop(
                        Math.Min(cycleBudget, Sh2FrameBufferWordFillLoopCycles * Sh2FrameBufferWordFillLoopMaxBurstIterations),
                        _sh2WordWriters[cpuIndex],
                        Sh2FrameBufferWordFillLoopCycles,
                        out fastCycles))
                {
                    RecordSh2FastPath(fastCycles);
                    AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                    return fastCycles;
                }

            if (IsSh2FastPathGroupEnabled("memstore") &&
                ((nextOpcode & 0xF00F) == 0x2001 ||
                    (nextOpcode & 0xF000) == 0x7000 ||
                    (nextOpcode & 0xF00F) == 0x300C ||
                    (nextOpcode & 0xF0FF) == 0x4010 ||
                    (nextOpcode & 0xFF00) == 0x8B00) &&
                cpu.TryFastForwardMovWStoreAddRegisterDtBfLoop(
                    Math.Min(cycleBudget, 7 * Sh2FrameBufferWordFillLoopMaxBurstIterations),
                    _sh2WordWriters[cpuIndex],
                    7,
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memstore") &&
                (nextOpcode & 0xF00F) == 0x2002)
            {
                int longStoreBudget = Math.Min(cycleBudget, Sh2LongStoreFillLoopCycles * Sh2LongStoreFillLoopMaxBurstIterations);
                if (cpu.TryFastForwardMovLStoreAddDtBfLoop(
                        longStoreBudget,
                        _sh2LongWriters[cpuIndex],
                        Sh2LongStoreFillLoopCycles,
                        out fastCycles))
                {
                    RecordSh2FastPath(fastCycles);
                    AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                    return fastCycles;
                }

                if (cpu.TryFastForwardMovLStoreAddBfSDtLoop(
                        longStoreBudget,
                        _sh2LongWriters[cpuIndex],
                        Sh2LongStoreFillLoopCycles,
                        out fastCycles))
                {
                    RecordSh2FastPath(fastCycles);
                    AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                    return fastCycles;
                }

                int delayStoreBudget = Math.Min(cycleBudget, Sh2LongStoreDelayFillLoopCycles * Sh2LongStoreDelayFillLoopMaxBurstIterations);
                if (cpu.TryFastForwardMovLNopDtBfSAddLoop(
                        delayStoreBudget,
                        _sh2LongWriters[cpuIndex],
                        Sh2LongStoreDelayFillLoopCycles,
                        out fastCycles))
                {
                    RecordSh2FastPath(fastCycles);
                    AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                    return fastCycles;
                }
            }

            if (IsSh2FastPathGroupEnabled("memstore") &&
                ((nextOpcode & 0xF0FF) == 0x4010 ||
                    (nextOpcode & 0xF00F) == 0x2002 ||
                    nextOpcode == 0x0009 ||
                    (nextOpcode & 0xFF00) == 0x8F00) &&
                cpu.TryFastForwardDtMovLManyNopBfSAddLoop(
                    Math.Min(cycleBudget, Sh2LongStoreDelayFillLoopCycles * Sh2LongStoreDelayFillLoopMaxBurstIterations),
                    _sh2LongWriters[cpuIndex],
                    Sh2LongStoreDelayFillLoopCycles,
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memstore") &&
                (nextOpcode & 0xF00F) == 0x6005 &&
                cpu.TryFastForwardMovWPostIncSwapPreDecDtBfSLoop(
                    cycleBudget,
                    _sh2WordReaders[cpuIndex],
                    _sh2WordWriters[cpuIndex],
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memstore") &&
                (nextOpcode & 0xF00F) == 0x6005 &&
                TryFastForwardMovWordStridedCopy(
                    cpu,
                    cpuIndex,
                    Math.Min(Math.Max(cycleBudget, Sh2MovWordStridedCopyMinBurstCycles), Sh2MovWordStridedCopyMaxBurstCycles),
                    out fastCycles))
            {
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memstore") &&
                (nextOpcode & 0xF00F) == 0x6005 &&
                cpu.TryFastForwardMovWPostIncStoreAddImmediateDtBfLoop(
                    Math.Min(cycleBudget, 7 * Sh2FrameBufferWordFillLoopMaxBurstIterations),
                    _sh2WordReaders[cpuIndex],
                    _sh2WordWriters[cpuIndex],
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memscan") &&
                nextOpcode == 0x613F &&
                cpu.TryFastForwardWordTableSearchLoop(cycleBudget, _sh2WordReaders[cpuIndex], out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memscan") &&
                nextOpcode == 0x613F &&
                cpu.TryFastForwardByteFillIndexedCmpGeLoop(cycleBudget, _sh2ByteWriters[cpuIndex], out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memscan") &&
                (nextOpcode == 0xE13F ||
                    nextOpcode == 0x2129 ||
                    nextOpcode == 0x6013 ||
                    nextOpcode == 0x4008 ||
                    nextOpcode == 0x007C ||
                    nextOpcode == 0x7201 ||
                    nextOpcode == 0x3257 ||
                    nextOpcode == 0xCB01 ||
                    nextOpcode == 0x2800 ||
                    (nextOpcode & 0xFF00) == 0x8F00) &&
                cpu.TryFastForwardMaskedStridedByteSpanLoop(
                    Math.Min(cycleBudget, 24 * 4096),
                    _sh2ByteReaders[cpuIndex],
                    _sh2ByteWriters[cpuIndex],
                    24,
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memscan") &&
                (nextOpcode == 0x5133 ||
                    nextOpcode == 0x3160 ||
                    (nextOpcode & 0xFF00) == 0x8B00 ||
                    nextOpcode == 0xD708 ||
                    nextOpcode == 0x6233 ||
                    nextOpcode == 0x6172 ||
                    nextOpcode == 0x3210 ||
                    (nextOpcode & 0xFF00) == 0x8F00 ||
                    nextOpcode == 0x73F0) &&
                cpu.TryFastForwardBackwardLongRecordScanLoop(
                    Math.Min(cycleBudget, 10 * 4096),
                    _sh2LongReaders[cpuIndex],
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memscan") &&
                (nextOpcode == 0x6153 ||
                    nextOpcode == 0x2411 ||
                    nextOpcode == 0x70FF ||
                    nextOpcode == 0x88FF ||
                    (nextOpcode & 0xFF00) == 0x8F00 ||
                    nextOpcode == 0x7402) &&
                cpu.TryFastForwardWordFillCmpEqMinusOneBfsLoop(
                    Math.Min(cycleBudget, 8 * 4096),
                    _sh2WordWriters[cpuIndex],
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memscan") &&
                nextOpcode == 0x518C &&
                cpu.TryFastForwardDoomRecordPairScanLoop(
                    Math.Min(cycleBudget, 14 * 4096),
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memscan") &&
                nextOpcode == 0x6174 &&
                cpu.TryFastForwardMovBPostIncStoreAddCmpGeBfsLoop(
                    Math.Min(cycleBudget, 8 * 4096),
                    _sh2ByteReaders[cpuIndex],
                    _sh2ByteWriters[cpuIndex],
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memscan") &&
                nextOpcode == 0x6830 &&
                cpu.TryFastForwardDoomMaskedColumnWordStoreLoop(
                    Math.Min(cycleBudget, 10 * 4096),
                    _sh2ByteReaders[cpuIndex],
                    _sh2WordReaders[cpuIndex],
                    _sh2WordWriters[cpuIndex],
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memscan") &&
                nextOpcode == 0x6099 &&
                cpu.TryFastForwardDoomSwappedMaskedColumnWordStoreLoop(
                    Math.Min(cycleBudget, 13 * 4096),
                    _sh2ByteReaders[cpuIndex],
                    _sh2WordReaders[cpuIndex],
                    _sh2WordWriters[cpuIndex],
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memscan") &&
                nextOpcode == 0x3546 &&
                cpu.TryFastForwardDoomByteLookupSpanLoop(
                    Math.Min(cycleBudget, 22 * 4096),
                    _sh2ByteReaders[cpuIndex],
                    _sh2ByteWriters[cpuIndex],
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memscan") &&
                nextOpcode == 0x2271 &&
                cpu.TryFastForwardWordFillAddCompareGtBfsLoop(
                    Math.Min(cycleBudget, 7 * Sh2FrameBufferWordFillLoopMaxBurstIterations),
                    _sh2WordWriters[cpuIndex],
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memscan") &&
                IsSh2FastPathGroupEnabled("bytespancompare") &&
                nextOpcode == 0x6150 &&
                cpu.TryFastForwardByteSpanCompareLoop(
                    Math.Min(cycleBudget, 13 * 4096),
                    _sh2ByteReaders[cpuIndex],
                    13,
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memscan") &&
                nextOpcode == 0x6424 &&
                cpu.TryFastForwardByteNibbleLookupExpandLoop(
                    Math.Min(cycleBudget, 45 * 4096),
                    _sh2ByteReaders[cpuIndex],
                    _sh2ByteWriters[cpuIndex],
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memscan") &&
                nextOpcode == 0x2100 &&
                cpu.TryFastForwardMovBStoreDtBfsAddLoop(
                    Math.Min(cycleBudget, 4 * 4096),
                    _sh2ByteWriters[cpuIndex],
                    4,
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memscan") &&
                nextOpcode == 0x480B &&
                cpu.TryFastForwardGbrWordHelperJsrBfsPollLoop(
                    Math.Min(cycleBudget, 12 * 4096),
                    _sh2WordReaders[cpuIndex],
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memscan") &&
                nextOpcode == 0x6022 &&
                cpu.TryFastForwardDmaIdleCommunicationLongMismatchPollLoop(
                    Math.Min(cycleBudget, 6 * 4096),
                    _sh2ByteReaders[cpuIndex],
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memscan") &&
                nextOpcode == 0x3140 &&
                cpu.TryFastForwardLongReloadCmpEqBfsPollLoop(
                    Math.Min(cycleBudget, 3 * 4096),
                    _sh2ByteReaders[cpuIndex],
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memscan") &&
                nextOpcode == 0x6022 &&
                cpu.TryFastForwardLongTstImmediateBtPollLoop(
                    Math.Min(cycleBudget, 3 * 4096),
                    _sh2ByteReaders[cpuIndex],
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memscan") &&
                (nextOpcode == 0x8414 || nextOpcode == 0xC840) &&
                cpu.TryFastForwardByteDisplacementDualTstBraPollLoop(
                    Math.Min(cycleBudget, 8 * 4096),
                    _sh2ByteReaders[cpuIndex],
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memtransform") &&
                (nextOpcode == 0x1457 ||
                    nextOpcode == 0x1456 ||
                    nextOpcode == 0x1455 ||
                    nextOpcode == 0x1454 ||
                    nextOpcode == 0x1453 ||
                    nextOpcode == 0x1452 ||
                    nextOpcode == 0x1451 ||
                    nextOpcode == 0x2452 ||
                    nextOpcode == 0x70F0 ||
                    nextOpcode == 0x3017 ||
                    nextOpcode == 0x8DF4 ||
                    nextOpcode == 0x7420) &&
                cpu.TryFastForwardUnrolledLongFillGtBtsLoop(
                    Math.Min(cycleBudget, 13 * 4096),
                    _sh2LongWriters[cpuIndex],
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memtransform") &&
                nextOpcode == 0x64A3 &&
                cpu.TryFastForwardWordHighBitMaskTransformOuterLoop(
                    cycleBudget,
                    _sh2WordReaders[cpuIndex],
                    _sh2WordWriters[cpuIndex],
                    _sh2ByteReaders[cpuIndex],
                    _sh2ByteWriters[cpuIndex],
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memtransform") &&
                nextOpcode == 0x6033 &&
                cpu.TryFastForwardWordHighBitMaskTransformLoop(
                    cycleBudget,
                    _sh2WordReaders[cpuIndex],
                    _sh2WordWriters[cpuIndex],
                    _sh2ByteReaders[cpuIndex],
                    _sh2ByteWriters[cpuIndex],
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memtransform") &&
                nextOpcode == 0x6084 &&
                cpu.TryFastForwardByteLookupWordRowExpandLoop(
                    cycleBudget,
                    _sh2ByteReaders[cpuIndex],
                    _sh2WordReaders[cpuIndex],
                    _sh2WordWriters[cpuIndex],
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memtransform") &&
                nextOpcode == 0x6084 &&
                cpu.TryFastForwardByteLookupWordStoreStep(
                    cycleBudget,
                    _sh2ByteReaders[cpuIndex],
                    _sh2WordReaders[cpuIndex],
                    _sh2WordWriters[cpuIndex],
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("memdescriptor") &&
                (nextOpcode == 0xD42F || nextOpcode == 0x5046))
            {
                EmptyDescriptorSpanFastPathAttempts++;
                if (cpu.TryFastForwardEmptyDescriptorSpanFillLoop(
                        cycleBudget,
                        _sh2WordWriters[cpuIndex],
                        out fastCycles))
                {
                    EmptyDescriptorSpanFastPathHits++;
                    RecordSh2FastPath(fastCycles);
                    AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                    return fastCycles;
                }
            }

            if (IsSh2FastPathGroupEnabled("memdescriptor") &&
                nextOpcode == 0x77FF)
            {
                EmptyDescriptorSpanTailFastPathAttempts++;
                if (cpu.TryFastForwardEmptyDescriptorSpanFillTail(
                        cycleBudget,
                        _sh2WordWriters[cpuIndex],
                        out fastCycles))
                {
                    EmptyDescriptorSpanTailFastPathHits++;
                    RecordSh2FastPath(fastCycles);
                    AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                    return fastCycles;
                }
            }

            }

            if (IsSh2FastPathGroupEnabled("sync") &&
                cpuIndex == 0 &&
                nextOpcode == 0xD235 &&
                IsSlaveInInterruptDispatcherIdle() &&
                TryPeekSh2Word(cpu.PC + 12, cpuIndex, out ushort pollBranchOpcode) &&
                pollBranchOpcode == 0x8BF8)
            {
                LongDifferencePollFastPathAttempts++;
                if (cpu.TryFastForwardLongDifferenceEqualsOnePollLoop(
                        cycleBudget,
                        _sh2LongWriters[cpuIndex],
                        out fastCycles))
                {
                    LongDifferencePollFastPathHits++;
                    RecordSh2FastPath(fastCycles);
                    AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                    return fastCycles;
                }
            }

            if (IsSh2FastPathGroupEnabled("sync") &&
                IsLikelySdramFlagTaskletDispatcher(cpuIndex, cpu.PC, nextOpcode) &&
                TryFastForwardSdramFlagTaskletDispatcher(cpu, cpuIndex, cycleBudget, out fastCycles))
            {
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("sync") &&
                cpuIndex == 1 &&
                IsLikelyGbrBytePairInterruptIdle(cpuIndex, cpu.PC, nextOpcode) &&
                TryFastForwardGbrBytePairInterruptIdle(cpu, cpuIndex, cycleBudget, out fastCycles))
            {
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("sync") &&
                cpuIndex == 0 &&
                (nextOpcode == 0xC420 || nextOpcode == 0x2008 || (nextOpcode & 0xFF00) == 0x8900) &&
                TryFastForwardGbrByteZeroComm20Poll(cpu, cpuIndex, cycleBudget, out fastCycles))
            {
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("sync") &&
                cpuIndex == 0 &&
                (((nextOpcode & 0xF000) == 0xD000) || ((nextOpcode & 0xFF00) >= 0x8400 && (nextOpcode & 0xFF00) <= 0x84F0) || nextOpcode == 0x2018 || (nextOpcode & 0xFF00) == 0x8900) &&
                TryFastForwardLiteralByteDisplacementTstRegisterPoll(cpu, cpuIndex, cycleBudget, out fastCycles))
            {
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("sync") &&
                IsLikelyByteDisplacementZeroWaitDtBfLoop(cpuIndex, cpu.PC, nextOpcode))
            {
                ByteDisplacementZeroWaitFastPathAttempts++;
                if (cpu.TryFastForwardOuterWordZeroByteDisplacementWaitDtBfLoop(cycleBudget, out fastCycles) ||
                    cpu.TryFastForwardByteDisplacementZeroWaitDtBfLoop(cycleBudget, out fastCycles))
                {
                    ByteDisplacementZeroWaitFastPathHits++;
                    RecordSh2FastPath(fastCycles);
                    AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                    return fastCycles;
                }
            }

            if (IsSh2FastPathGroupEnabled("sync") &&
                (nextOpcode & 0xF000) == 0xD000 &&
                cpu.TryFastForwardSdramFlagTaskletReturn(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("sync") &&
                (nextOpcode & 0xFF00) == 0xC400 &&
                cpu.TryFastForwardGbrBytePairEqualTaskletReturn(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (IsSh2FastPathGroupEnabled("sync") &&
                cycleBudget != int.MaxValue &&
                (nextOpcode & 0xF000) == 0xD000 &&
                cpu.TryFastForwardSdramNullLinkedListIdleLoop(
                    cycleBudget,
                    _sh2LongReaders[cpuIndex],
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }
        }

        if (EnableSh2ListFastPaths &&
            cpu.TryFastForwardSdramLinkedListInsertRoutine(
                cycleBudget,
                _sh2LongReaders[cpuIndex],
                _sh2LongWriters[cpuIndex],
                Sh2LinkedListObserver,
                out fastCycles))
        {
            RecordSh2FastPath(fastCycles);
            AdvanceSh2InternalTimers(cpuIndex, fastCycles);
            return fastCycles;
        }

        if (EnableSh2ListFastPaths &&
            cpu.TryFastForwardRunlengthSdkRechainRoutine(
                cycleBudget,
                _sh2LongReaders[cpuIndex],
                _sh2LongWriters[cpuIndex],
                Sh2RechainObserver,
                out fastCycles))
        {
            RecordSh2FastPath(fastCycles);
            AdvanceSh2InternalTimers(cpuIndex, fastCycles);
            return fastCycles;
        }

        if (canProbeFastPath && IsSh2FastPathGroupEnabled("poll"))
        {
            if ((nextOpcode & 0xF0FF) == 0x4010 &&
                cpu.TryFastForwardDtBfLoop(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if ((nextOpcode == 0x0009 || (nextOpcode & 0xF0FF) == 0x4010) &&
                cpu.TryFastForwardNopDtBfDelayLoop(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if ((nextOpcode & 0xFF00) == 0x8400 &&
                cpu.TryFastForwardTstBfPollLoop(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if ((nextOpcode & 0xFC00) == 0xC400 &&
                cpu.TryFastForwardGbrCmpEqBfPollLoop(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (((nextOpcode & 0xFC00) == 0xC400 ||
                    (nextOpcode & 0xFF00) == 0x8800 ||
                    (nextOpcode & 0xFF00) == 0x8900) &&
                cpu.TryFastForwardGbrCmpEqBtPollLoop(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if ((nextOpcode & 0xFC00) == 0xC400 &&
                cpu.TryFastForwardGbrRegisterCmpEqBfPollLoop(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (((nextOpcode & 0xFF00) == 0xC500 ||
                    (nextOpcode & 0xF000) == 0xE000 ||
                    (nextOpcode & 0xF00F) == 0x3007 ||
                    (nextOpcode & 0xFF00) == 0x8B00) &&
                cpu.TryFastForwardGbrWordCmpGtBfPollLoop(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if ((nextOpcode & 0xFC00) == 0xC400 &&
                cpu.TryFastForwardGbrCmpEqBfBraPollLoop(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if ((nextOpcode & 0xF000) == 0xD000)
            {
                if (cpu.TryFastForwardMovLiteralTstBfPollLoop(cycleBudget, out fastCycles))
                {
                    RecordSh2FastPath(fastCycles);
                    AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                    return fastCycles;
                }

                if (cpu.TryFastForwardMovLiteralLongTstBtPollLoop(cycleBudget, out fastCycles))
                {
                    RecordSh2FastPath(fastCycles);
                    AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                    return fastCycles;
                }

                if (cpu.TryFastForwardMovLiteralWordTstBtPollLoop(cycleBudget, out fastCycles))
                {
                    RecordSh2FastPath(fastCycles);
                    AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                    return fastCycles;
                }

                if (cpu.TryFastForwardMovLiteralWordCmpEqBtPollLoop(cycleBudget, out fastCycles))
                {
                    RecordSh2FastPath(fastCycles);
                    AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                    return fastCycles;
                }

                if (cpu.TryFastForwardMovLiteralWordDisplacementTstBfPollLoop(cycleBudget, out fastCycles))
                {
                    RecordSh2FastPath(fastCycles);
                    AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                    return fastCycles;
                }

                if (cpu.TryFastForwardMovLiteralByteCmpEqBtPollLoop(cycleBudget, out fastCycles))
                {
                    RecordSh2FastPath(fastCycles);
                    AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                    return fastCycles;
                }
            }

            if (((nextOpcode & 0xF00F) == 0x6002 ||
                    (nextOpcode & 0xF00F) == 0x2008 ||
                    (nextOpcode & 0xFF00) == 0x8900) &&
                cpu.TryFastForwardMovLiteralLongTstBtPollLoop(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (((nextOpcode & 0xF00F) == 0x6001 ||
                    (nextOpcode & 0xF00F) == 0x2008 ||
                    (nextOpcode & 0xFF00) == 0x8900) &&
                cpu.TryFastForwardMovLiteralWordTstBtPollLoop(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if ((((nextOpcode & 0xFF00) >= 0x8500 && (nextOpcode & 0xFF00) <= 0x85F0) ||
                    (nextOpcode & 0xF00F) == 0x2008 ||
                    (nextOpcode & 0xFF00) == 0x8B00) &&
                cpu.TryFastForwardMovLiteralWordDisplacementTstBfPollLoop(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if ((((nextOpcode & 0xF00F) == 0x6000) ||
                    (nextOpcode & 0xFF00) == 0x8800 ||
                    (nextOpcode & 0xFF00) == 0x8900) &&
                cpu.TryFastForwardMovLiteralByteCmpEqBtPollLoop(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if ((nextOpcode & 0xF00F) == 0x6001 &&
                cpu.TryFastForwardWordCmpEqBtPollLoop(Math.Min(Math.Max(cycleBudget, 512), 4096), out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if ((((nextOpcode & 0xF00F) == 0x6002) ||
                    ((nextOpcode & 0xF00F) == 0x3000) ||
                    ((nextOpcode & 0xFF00) == 0x8900)) &&
                cpu.TryFastForwardLongRegisterCmpEqBtPollLoop(Math.Min(Math.Max(cycleBudget, 512), 4096), out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if ((((nextOpcode & 0xFF00) == 0xC600) ||
                    ((nextOpcode & 0xF00F) == 0x6003) ||
                    ((nextOpcode & 0xF00F) == 0x2009) ||
                    ((nextOpcode & 0xFF00) == 0xCB00) ||
                    ((nextOpcode & 0xF00F) == 0x3000) ||
                    ((nextOpcode & 0xFF00) == 0x8B00)) &&
                TryFastForwardGbrLongMaskedOrComparePoll(cpu, cpuIndex, cycleBudget, out fastCycles))
            {
                return fastCycles;
            }

            if ((((nextOpcode & 0xF00F) == 0x6001) ||
                    ((nextOpcode & 0xF00F) == 0x3000) ||
                    ((nextOpcode & 0xFF00) == 0x8900)) &&
                TryFastForwardStableWordPairPoll(cpu, cpuIndex, cycleBudget, out fastCycles))
            {
                return fastCycles;
            }

            if ((((nextOpcode & 0xF00F) == 0x6001) ||
                    (nextOpcode & 0xFF00) == 0x8800 ||
                    (nextOpcode & 0xFF00) == 0x8B00) &&
                cpu.TryFastForwardWordCmpEqBfPollLoop(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if ((((nextOpcode & 0xF00F) == 0x6001) ||
                    (nextOpcode & 0xF00F) == 0x2008 ||
                    (nextOpcode & 0xFF00) == 0x8900) &&
                cpu.TryFastForwardWordTstBtPollLoop(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (((nextOpcode & 0xFF00) == 0x8900 ||
                    (nextOpcode & 0xF00F) == 0x2008 ||
                    ((nextOpcode & 0xFF00) >= 0x8500 && (nextOpcode & 0xFF00) <= 0x85F0)) &&
                cpu.TryFastForwardWordDisplacementTstBtPollLoop(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (((nextOpcode & 0xF00F) == 0x6002 ||
                    nextOpcode == 0x0009 ||
                    (nextOpcode & 0xF00F) == 0x2008 ||
                    (nextOpcode & 0xFF00) == 0x8900) &&
                cpu.TryFastForwardLongTstBtPaddedPollLoop(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if ((((nextOpcode & 0xF00F) == 0x3000) ||
                    (nextOpcode & 0xF00F) == 0x6002 ||
                    (nextOpcode & 0xFF00) == 0x8D00 ||
                    (nextOpcode & 0xF00F) == 0x2009) &&
                cpu.TryFastForwardLongMaskedChangeBtSDelayPollLoop(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if ((((nextOpcode & 0xF00F) == 0x6001) ||
                    (nextOpcode & 0xF0FF) == 0x7001 ||
                    (nextOpcode & 0xF00F) == 0x2001 ||
                    (nextOpcode & 0xFF00) == 0xC500 ||
                    (nextOpcode & 0xFF00) == 0x8800 ||
                    (nextOpcode & 0xFF00) == 0x8900) &&
                cpu.TryFastForwardWordIncrementGbrZeroBtPollLoop(
                    cycleBudget,
                    _sh2WordWriters[cpuIndex],
                    out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if ((nextOpcode & 0xF00F) == 0x6001 &&
                cpu.TryFastForwardWordTstBfPollLoop(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if (((nextOpcode & 0xF00F) == 0x6000 ||
                    (nextOpcode & 0xF00F) == 0x2008 ||
                    (nextOpcode & 0xFF00) == 0x8B00) &&
                cpu.TryFastForwardByteTstBfPollLoop(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if ((((nextOpcode & 0xFF00) >= 0x8400 && (nextOpcode & 0xFF00) <= 0x84F0) ||
                    (nextOpcode & 0xFF00) == 0xC800 ||
                    (nextOpcode & 0xFF00) == 0x8900) &&
                cpu.TryFastForwardByteDisplacementTstImmediateBtPollLoop(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if ((nextOpcode & 0xF00F) == 0x2008 &&
                cpu.TryFastForwardTstBfsDelayAddLoop(cycleBudget, out fastCycles))
            {
                RecordSh2FastPath(fastCycles);
                AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                return fastCycles;
            }

            if ((nextOpcode & 0xFF00) == 0x8900 &&
                IsLikelyTwoStageWordZeroPollRing(cpuIndex, cpu.PC, nextOpcode))
            {
                TwoStageWordZeroPollRingFastPathAttempts++;
                int twoStageBudget = Math.Max(cycleBudget, 512);
                if (cpu.TryFastForwardTwoStageWordZeroPollRing(twoStageBudget, out fastCycles))
                {
                    TwoStageWordZeroPollRingFastPathHits++;
                    RecordSh2FastPath(fastCycles);
                    AdvanceSh2InternalTimers(cpuIndex, fastCycles);
                    return fastCycles;
                }
            }

        }

        long before = cpu.Cycles;
        int cycles = cpu.Step();
        long delta = cpu.Cycles - before;
        if (delta <= 0)
        {
            delta = cycles;
        }

        AdvanceSh2InternalTimers(cpuIndex, (int)Math.Min(delta, int.MaxValue));
        return cycles;
    }

    private bool IsLikelySdramFlagTaskletDispatcher(int cpuIndex, uint pc, ushort opcode)
    {
        if ((opcode & 0xF000) == 0xD000)
        {
            return TryPeekSh2Word(pc + 2, cpuIndex, out ushort loadOpcode) && loadOpcode == 0x60E2;
        }

        return opcode == 0x60E2 &&
            pc >= 2 &&
            TryPeekSh2Word(pc - 2, cpuIndex, out ushort literalOpcode) &&
            (literalOpcode & 0xF000) == 0xD000;
    }

    private bool IsLikelyGbrBytePairInterruptIdle(int cpuIndex, uint pc, ushort opcode)
    {
        if ((opcode & 0xF000) == 0xD000)
        {
            return TryPeekSh2Word(pc + 2, cpuIndex, out ushort pushPrOpcode) && pushPrOpcode == 0x4F22;
        }

        if (opcode == 0x4F22)
        {
            return pc >= 2 &&
                TryPeekSh2Word(pc - 2, cpuIndex, out ushort literalOpcode) &&
                (literalOpcode & 0xF000) == 0xD000 &&
                TryPeekSh2Word(pc + 2, cpuIndex, out ushort jsrOpcode) &&
                jsrOpcode == 0x400B;
        }

        if (opcode == 0x400B)
        {
            return pc >= 4 &&
                TryPeekSh2Word(pc - 4, cpuIndex, out ushort literalOpcode) &&
                (literalOpcode & 0xF000) == 0xD000 &&
                TryPeekSh2Word(pc - 2, cpuIndex, out ushort pushPrOpcode) &&
                pushPrOpcode == 0x4F22;
        }

        return (opcode & 0xF000) == 0xA000 &&
            pc >= 8 &&
            TryPeekSh2Word(pc - 8, cpuIndex, out ushort literalAtLoopStart) &&
            (literalAtLoopStart & 0xF000) == 0xD000 &&
            TryPeekSh2Word(pc - 6, cpuIndex, out ushort pushPrAtLoopStart) &&
            pushPrAtLoopStart == 0x4F22 &&
            TryPeekSh2Word(pc + 2, cpuIndex, out ushort branchDelayOpcode) &&
            branchDelayOpcode == 0x0009;
    }

    private bool IsLikelyByteDisplacementZeroWaitDtBfLoop(int cpuIndex, uint pc, ushort opcode)
    {
        if (IsByteDisplacementLoad(opcode))
        {
            return IsLikelyByteDisplacementZeroWaitDtBfLoopStart(cpuIndex, pc);
        }

        if (opcode == 0x2008)
        {
            return pc >= 2 && IsLikelyByteDisplacementZeroWaitDtBfLoopStart(cpuIndex, pc - 2);
        }

        if ((opcode & 0xF0FF) == 0x4010)
        {
            return pc >= 6 && IsLikelyByteDisplacementZeroWaitDtBfLoopStart(cpuIndex, pc - 6);
        }

        if ((opcode & 0xFF00) != 0x8B00)
        {
            return false;
        }

        return (pc >= 4 && IsLikelyByteDisplacementZeroWaitDtBfLoopStart(cpuIndex, pc - 4)) ||
            (pc >= 8 && IsLikelyByteDisplacementZeroWaitDtBfLoopStart(cpuIndex, pc - 8));
    }

    private bool IsLikelyByteDisplacementZeroWaitDtBfLoopStart(int cpuIndex, uint loopPc)
    {
        return TryPeekSh2Word(loopPc, cpuIndex, out ushort loadOpcode) &&
            IsByteDisplacementLoad(loadOpcode) &&
            TryPeekSh2Word(loopPc + 2, cpuIndex, out ushort testOpcode) &&
            testOpcode == 0x2008 &&
            TryPeekSh2Word(loopPc + 4, cpuIndex, out ushort exitBranchOpcode) &&
            (exitBranchOpcode & 0xFF00) == 0x8B00 &&
            TryPeekSh2Word(loopPc + 6, cpuIndex, out ushort dtOpcode) &&
            (dtOpcode & 0xF0FF) == 0x4010 &&
            TryPeekSh2Word(loopPc + 8, cpuIndex, out ushort loopBranchOpcode) &&
            (loopBranchOpcode & 0xFF00) == 0x8B00;
    }

    private static bool IsByteDisplacementLoad(ushort opcode)
    {
        return (opcode & 0xFF00) >= 0x8400 && (opcode & 0xFF00) <= 0x84F0;
    }

    private bool IsLikelyTwoStageWordZeroPollRing(int cpuIndex, uint branchPc, ushort opcode)
    {
        if ((opcode & 0xFF00) != 0x8900)
        {
            return false;
        }

        int displacement = (sbyte)opcode;
        uint setupEntryPc = branchPc + 4 + (uint)(displacement * 2);
        if (!TryPeekSh2Word(setupEntryPc, cpuIndex, out ushort braToSetupOpcode) ||
            !TryPeekSh2Word(setupEntryPc + 2, cpuIndex, out ushort firstDelaySlot) ||
            (braToSetupOpcode & 0xF000) != 0xA000 ||
            firstDelaySlot != 0x0009)
        {
            return false;
        }

        int setupDisplacement = SignExtend12(braToSetupOpcode & 0x0FFF);
        uint setupPc = setupEntryPc + 4 + (uint)(setupDisplacement * 2);
        return TryPeekSh2Word(setupPc, cpuIndex, out ushort firstLiteralOpcode) &&
            TryPeekSh2Word(setupPc + 2, cpuIndex, out ushort firstLoadOpcode) &&
            TryPeekSh2Word(setupPc + 4, cpuIndex, out ushort secondLoadOpcode) &&
            TryPeekSh2Word(setupPc + 6, cpuIndex, out ushort compareOpcode) &&
            TryPeekSh2Word(setupPc + 8, cpuIndex, out ushort bfOpcode) &&
            TryPeekSh2Word(setupPc + 10, cpuIndex, out ushort braToPollOpcode) &&
            TryPeekSh2Word(setupPc + 12, cpuIndex, out ushort secondDelaySlot) &&
            (firstLiteralOpcode & 0xF000) == 0xD000 &&
            (firstLoadOpcode & 0xF00F) == 0x6001 &&
            (secondLoadOpcode & 0xF00F) == 0x8001 &&
            (compareOpcode & 0xF00F) == 0x3000 &&
            (bfOpcode & 0xFF00) == 0x8B00 &&
            (braToPollOpcode & 0xF000) == 0xA000 &&
            secondDelaySlot == 0x0009;
    }

    private static int SignExtend12(int value)
    {
        value &= 0x0FFF;
        return (value & 0x0800) != 0 ? value | unchecked((int)0xFFFFF000) : value;
    }

    private static int ParseSh2FastPathCycleBudgetCap()
    {
        string? value = Environment.GetEnvironmentVariable("MDSHARP_SH2_FASTPATH_CYCLE_CAP");
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        return int.TryParse(value, out int parsed) && parsed > 0 ? parsed : 0;
    }

    private static bool IsSh2FastPathGroupEnabled(string group)
    {
        return Sh2DisabledFastPathGroups.IndexOf(group, StringComparison.OrdinalIgnoreCase) < 0;
    }

    private bool TryFastForwardSdramFlagTaskletDispatcher(Sh2Cpu cpu, int cpuIndex, int cycleBudget, out int cycles)
    {
        SdramFlagTaskletDispatcherFastPathAttempts++;
        if (!cpu.TryFastForwardSdramFlagTaskletDispatcherLoop(cycleBudget, out cycles))
        {
            return false;
        }

        SdramFlagTaskletDispatcherFastPathHits++;
        RecordSh2FastPath(cycles);
        AdvanceSh2InternalTimers(cpuIndex, cycles);
        return true;
    }

    private bool TryFastForwardLiteralByteDisplacementTstRegisterPoll(Sh2Cpu cpu, int cpuIndex, int cycleBudget, out int cycles)
    {
        LiteralByteDisplacementTstRegisterPollFastPathAttempts++;
        if (!cpu.TryFastForwardLiteralByteDisplacementTstRegisterBtPollLoop(cycleBudget, out cycles))
        {
            return false;
        }

        LiteralByteDisplacementTstRegisterPollFastPathHits++;
        RecordSh2FastPath(cycles);
        AdvanceSh2InternalTimers(cpuIndex, cycles);
        return true;
    }

    private bool TryFastForwardGbrByteZeroComm20Poll(Sh2Cpu cpu, int cpuIndex, int cycleBudget, out int cycles)
    {
        GbrByteZeroComm20PollFastPathAttempts++;
        if (!cpu.TryFastForwardGbrByteZeroTstBtPollLoop(cycleBudget, displacement: 0x20, out cycles))
        {
            return false;
        }

        GbrByteZeroComm20PollFastPathHits++;
        RecordSh2FastPath(cycles);
        AdvanceSh2InternalTimers(cpuIndex, cycles);
        return true;
    }

    private bool TryFastForwardGbrBytePairInterruptIdle(Sh2Cpu cpu, int cpuIndex, int cycleBudget, out int cycles)
    {
        GbrBytePairInterruptIdleFastPathAttempts++;
        if (!cpu.TryFastForwardGbrBytePairEqualInterruptIdleLoop(cycleBudget, out cycles))
        {
            return false;
        }

        GbrBytePairInterruptIdleFastPathHits++;
        RecordSh2FastPath(cycles);
        AdvanceSh2InternalTimers(cpuIndex, cycles);
        return true;
    }

    private bool TryFastForwardStableWordPairPoll(Sh2Cpu cpu, int cpuIndex, int cycleBudget, out int cycles)
    {
        StableWordPairPollFastPathAttempts++;
        int stablePollBudget = Math.Min(Math.Max(cycleBudget, 512), 4096);
        if (!cpu.TryFastForwardStableWordPairCmpEqBtPollLoop(stablePollBudget, out cycles))
        {
            return false;
        }

        StableWordPairPollFastPathHits++;
        RecordSh2FastPath(cycles);
        AdvanceSh2InternalTimers(cpuIndex, cycles);
        return true;
    }

    private bool TryFastForwardGbrLongMaskedOrComparePoll(Sh2Cpu cpu, int cpuIndex, int cycleBudget, out int cycles)
    {
        GbrLongMaskedOrComparePollFastPathAttempts++;
        int pollBudget = Math.Min(Math.Max(cycleBudget, 512), 4096);
        if (!cpu.TryFastForwardGbrLongMaskedOrCompareBfPollLoop(pollBudget, out cycles))
        {
            return false;
        }

        GbrLongMaskedOrComparePollFastPathHits++;
        RecordSh2FastPath(cycles);
        AdvanceSh2InternalTimers(cpuIndex, cycles);
        return true;
    }

    private bool TryFastForwardMovWordStridedCopy(Sh2Cpu cpu, int cpuIndex, int cycleBudget, out int cycles)
    {
        MovWordStridedCopyFastPathAttempts++;
        if (!cpu.TryFastForwardMovWPostIncStoreAddRegDtBfLoop(
                cycleBudget,
                _sh2FastCopyWordReaders[cpuIndex],
                _sh2FastCopyWordWriters[cpuIndex],
                out cycles))
        {
            return false;
        }

        MovWordStridedCopyFastPathHits++;
        RecordSh2FastPath(cycles);
        AdvanceSh2InternalTimers(cpuIndex, cycles);
        return true;
    }

    private bool TryReadSh2WordForFastCopy(uint address, int cpuIndex, out ushort value)
    {
        if (TryPeekSh2Word(address, cpuIndex, out value))
        {
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2SystemRegisterStart and < ThirtyTwoXHardwareProfile.Sh2SystemRegisterStart + 0x80 ||
            address is >= ThirtyTwoXHardwareProfile.Sh2SystemRegisterCachedStart and < ThirtyTwoXHardwareProfile.Sh2SystemRegisterCachedStart + 0x80 ||
            address is >= ThirtyTwoXHardwareProfile.Sh2VdpRegisterStart and < ThirtyTwoXHardwareProfile.Sh2VdpRegisterStart + 0x80 ||
            address is >= ThirtyTwoXHardwareProfile.Sh2VdpRegisterCachedStart and < ThirtyTwoXHardwareProfile.Sh2VdpRegisterCachedStart + 0x80 ||
            IsSh2DmaRegisterAddress(address) ||
            IsSh2DivisionUnitRegisterAddress(address) ||
            IsSh2PeripheralRegisterAddress(address))
        {
            value = 0;
            return false;
        }

        value = 0;
        return false;
    }

    private bool TryWriteSh2WordForFastCopy(uint address, ushort value, int cpuIndex)
    {
        if (TryMapSh2OverflowSdramMirrorAddress(address, out int overflowSdramOffset))
        {
            TraceSdramWordWrite(cpuIndex, "FSKIP16", address, overflowSdramOffset, value);
            TraceSh2MemoryAccess(cpuIndex, "FSKIP16", address, value);
            return true;
        }

        if (TryMapExactSh2CachedSdramAddress(address, out int cachedSdramOffset))
        {
            WriteBigEndianWord(_sdram, cachedSdramOffset, value);
            UpdateSh2SdramCacheByte(cachedSdramOffset, (byte)(value >> 8), cpuIndex);
            UpdateSh2SdramCacheByte((cachedSdramOffset + 1) & (ThirtyTwoXHardwareProfile.SdramBytes - 1), (byte)value, cpuIndex);
            TraceSdramWordWrite(cpuIndex, "FWC16", address, cachedSdramOffset, value);
            TraceSh2MemoryAccess(cpuIndex, "FWC16", address, value);
            return true;
        }

        if (TryMapExactSh2SdramAddress(address, out int sdramOffset))
        {
            WriteBigEndianWord(_sdram, sdramOffset, value);
            TraceSdramWordWrite(cpuIndex, "FW16", address, sdramOffset, value);
            TraceSh2MemoryAccess(cpuIndex, "FW16", address, value);
            return true;
        }

        if (TryMapSh2SdramAddress(address, out int mirroredSdramOffset))
        {
            TraceSdramWordWrite(cpuIndex, "FSKIP16", address, mirroredSdramOffset, value);
            TraceSh2MemoryAccess(cpuIndex, "FSKIP16", address, value);
            return true;
        }

        if (TryMapSh2CachedCartridgeAddress(address, out uint cacheOffset, out uint romOffset))
        {
            if (IsSh2CacheEnabled(cpuIndex))
            {
                WriteSh2CachedCartridgeByte(cacheOffset, romOffset, (byte)(value >> 8), cpuIndex);
                WriteSh2CachedCartridgeByte(cacheOffset + 1, romOffset + 1, (byte)value, cpuIndex);
            }

            return true;
        }

        if (TryMapSh2UncachedBankedCartridgeAddress(address, out _) ||
            address >= ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart)
        {
            return true;
        }

        if (TrySignalSh2InputCapture(address, value, cpuIndex))
        {
            return true;
        }

        if (IsSh2DivisionUnitRegisterAddress(address) ||
            IsSh2PeripheralRegisterAddress(address) ||
            address is >= ThirtyTwoXHardwareProfile.Sh2SystemRegisterStart and < ThirtyTwoXHardwareProfile.Sh2SystemRegisterStart + 0x80 ||
            address is >= ThirtyTwoXHardwareProfile.Sh2SystemRegisterCachedStart and < ThirtyTwoXHardwareProfile.Sh2SystemRegisterCachedStart + 0x80 ||
            address is >= ThirtyTwoXHardwareProfile.Sh2VdpRegisterStart and < ThirtyTwoXHardwareProfile.Sh2VdpRegisterStart + 0x80 ||
            address is >= ThirtyTwoXHardwareProfile.Sh2VdpRegisterCachedStart and < ThirtyTwoXHardwareProfile.Sh2VdpRegisterCachedStart + 0x80 ||
            address is >= ThirtyTwoXHardwareProfile.Sh2ColorPaletteStart and < ThirtyTwoXHardwareProfile.Sh2ColorPaletteStart + (ThirtyTwoXHardwareProfile.PaletteEntries * 2) ||
            address is >= ThirtyTwoXHardwareProfile.Sh2ColorPaletteCachedStart and < ThirtyTwoXHardwareProfile.Sh2ColorPaletteCachedStart + (ThirtyTwoXHardwareProfile.PaletteEntries * 2) ||
            IsSh2DmaRegisterAddress(address) ||
            TryMapSh2CachePurgeAddress(address) ||
            TryMapSh2CacheAddressArrayAddress(address, out _))
        {
            TraceSh2MemoryAccess(cpuIndex, "FSKIPMMIO16", address, value);
            return true;
        }

        if (TryMapSh2FrameBufferAddress(address, out _, out _))
        {
            WriteSh2Word(address, value, cpuIndex);
            return true;
        }

        return true;
    }

    private bool TryReadSh2ByteFast(uint address, int cpuIndex, out byte value)
    {
        if (TryPeekSh2Byte(address, cpuIndex, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private bool TryWriteSh2ByteFast(uint address, byte value, int cpuIndex)
    {
        if (TryMapSh2FrameBufferAddress(address, out uint frameBufferOffset, out bool overwriteFrameBuffer))
        {
            if (IsSh2FrameBufferAccessDenied())
            {
                TraceDeniedFrameBufferAccess(cpuIndex == 0 ? "MSH2" : "SSH2", overwriteFrameBuffer ? "DENY-OW8" : "DENY-W8", frameBufferOffset, value);
                return true;
            }

            AddSh2FrameBufferBusyWaitIfNeeded(cpuIndex);
            WriteFrameBufferByteCore(frameBufferOffset, value, cpuIndex == 0 ? "MSH2" : "SSH2", overwriteFrameBuffer, transparentZero: overwriteFrameBuffer, enforceAccessWindow: false);
            return true;
        }

        if (TryMapSh2CachedSdramAddress(address, out _) ||
            TryMapSh2SdramAddress(address, out _) ||
            TryMapSh2CachedCartridgeAddress(address, out _, out _))
        {
            WriteSh2Byte(address, value, cpuIndex);
            return true;
        }

        return false;
    }

    private bool IsSlaveInInterruptDispatcherIdle()
    {
        uint pc = SlaveSh2.PC;
        return pc is >= 0x0600_22F8 and <= 0x0600_2342;
    }

    private bool TryWriteSh2FrameBufferWordFast(int cpuIndex, uint address, ushort value)
    {
        if (!TryMapSh2FrameBufferAddress(address, out uint frameBufferOffset, out bool overwriteFrameBuffer))
        {
            return false;
        }

        if (IsSh2FrameBufferAccessDenied())
        {
            TraceDeniedFrameBufferAccess(cpuIndex == 0 ? "MSH2" : "SSH2", overwriteFrameBuffer ? "DENY-OW16" : "DENY-W16", frameBufferOffset, value);
            return true;
        }

        AddSh2FrameBufferBusyWaitIfNeeded(cpuIndex);
        string source = cpuIndex == 0 ? "MSH2" : "SSH2";
        if (overwriteFrameBuffer)
        {
            WriteOverwriteImageWordCore(frameBufferOffset, value, source, enforceAccessWindow: false);
        }
        else
        {
            WriteFrameBufferWordCore(frameBufferOffset, value, source, enforceAccessWindow: false);
        }

        return true;
    }

    private uint? TryReadSh2LongFast(uint address, int cpuIndex)
    {
        if ((address & 0x03) != 0 &&
            TryMapSh2CachedCartridgeAddress(address & ~3u, out uint alignedCacheOffset, out uint alignedRomOffset))
        {
            return IsSh2DataCacheEnabled(cpuIndex)
                ? ReadSh2CachedCartridgeLong(alignedCacheOffset, alignedRomOffset, cpuIndex)
                : ReadCartridgeLong(alignedRomOffset);
        }

        if (TryMapSh2CachedSdramAddress(address, out int cachedSdramOffset))
        {
            if (IsSh2DataCacheEnabled(cpuIndex))
            {
                return ReadSh2CachedSdramLong(address, cachedSdramOffset, cpuIndex);
            }

            return ReadBigEndianLong(_sdram, cachedSdramOffset);
        }

        if (TryMapSh2SdramAddress(address, out int sdramOffset))
        {
            return ReadBigEndianLong(_sdram, sdramOffset);
        }

        if (TryMapSh2CachedCartridgeAddress(address, out uint cacheOffset, out uint romOffset))
        {
            if (IsSh2DataCacheEnabled(cpuIndex))
            {
                return ReadSh2CachedCartridgeLong(cacheOffset, romOffset, cpuIndex);
            }

            return ReadCartridgeLong(romOffset);
        }

        if (TryMapSh2UncachedBankedCartridgeAddress(address, out romOffset))
        {
            return ReadCartridgeLong(romOffset);
        }

        if (address >= ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart)
        {
            return ReadCartridgeLong(address - ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
        }

        return null;
    }

    private uint? TryReadSh2LongNoAllocate(uint address, int cpuIndex)
    {
        if ((address & 0x03) != 0 &&
            TryMapSh2CachedCartridgeAddress(address & ~3u, out uint alignedCacheOffset, out uint alignedRomOffset))
        {
            if (IsSh2DataCacheEnabled(cpuIndex) &&
                TryReadSh2CacheLongNoAllocate(alignedCacheOffset, cpuIndex, out uint cachedValue))
            {
                return cachedValue;
            }

            return ReadCartridgeLong(alignedRomOffset);
        }

        if (TryMapSh2CachedSdramAddress(address, out int cachedSdramOffset))
        {
            if (IsSh2DataCacheEnabled(cpuIndex) &&
                TryReadSh2CacheLongNoAllocate(address, cpuIndex, out uint cachedValue))
            {
                return cachedValue;
            }

            return ReadBigEndianLong(_sdram, cachedSdramOffset);
        }

        if (TryMapSh2SdramAddress(address, out int sdramOffset))
        {
            return ReadBigEndianLong(_sdram, sdramOffset);
        }

        if (TryMapSh2CachedCartridgeAddress(address, out uint cacheOffset, out uint romOffset))
        {
            if (IsSh2DataCacheEnabled(cpuIndex) &&
                TryReadSh2CacheLongNoAllocate(cacheOffset, cpuIndex, out uint cachedValue))
            {
                return cachedValue;
            }

            return ReadCartridgeLong(romOffset);
        }

        if (TryMapSh2UncachedBankedCartridgeAddress(address, out romOffset))
        {
            return ReadCartridgeLong(romOffset);
        }

        if (address >= ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart)
        {
            return ReadCartridgeLong(address - ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
        }

        return null;
    }

    private bool TryWriteSh2LongFast(uint address, uint value, int cpuIndex)
    {
        if (!TryMapSh2CachedSdramAddress(address, out _) &&
            !TryMapSh2SdramAddress(address, out _) &&
            !TryMapSh2FrameBufferAddress(address, out _, out _) &&
            !TryMapSh2CachedCartridgeAddress(address, out _, out _))
        {
            return false;
        }

        WriteSh2Long(address, value, cpuIndex);
        return true;
    }

    private uint ReadSh2CachedSdramLong(uint address, int offset, int cpuIndex)
    {
        return (uint)((ReadSh2CachedSdramByte(address, offset, cpuIndex) << 24) |
            (ReadSh2CachedSdramByte(address + 1, offset + 1, cpuIndex) << 16) |
            (ReadSh2CachedSdramByte(address + 2, offset + 2, cpuIndex) << 8) |
            ReadSh2CachedSdramByte(address + 3, offset + 3, cpuIndex));
    }

    private ushort ReadSh2InstructionWord(uint address, int cpuIndex)
    {
        if (TryMapSh2CachedSdramAddress(address, out int cachedSdramOffset))
        {
            if (IsSh2InstructionCacheEnabled(cpuIndex))
            {
                return (ushort)((ReadSh2CachedSdramByte(address, cachedSdramOffset, cpuIndex) << 8) |
                    ReadSh2CachedSdramByte(address + 1, cachedSdramOffset + 1, cpuIndex));
            }

            return ReadSh2Word(address, cpuIndex);
        }

        if (TryMapSh2SdramAddress(address, out _))
        {
            return ReadSh2Word(address, cpuIndex);
        }

        if (TryMapSh2CachedCartridgeAddress(address, out uint cacheOffset, out uint romOffset))
        {
            if (IsSh2InstructionCacheEnabled(cpuIndex))
            {
                return (ushort)((ReadSh2CachedCartridgeByte(cacheOffset, romOffset, cpuIndex) << 8) |
                    ReadSh2CachedCartridgeByte(cacheOffset + 1, romOffset + 1, cpuIndex));
            }

            return ReadSh2Word(address, cpuIndex);
        }

        if (TryMapSh2UncachedBankedCartridgeAddress(address, out _))
        {
            return ReadSh2Word(address, cpuIndex);
        }

        if (address >= ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart)
        {
            return ReadSh2Word(address, cpuIndex);
        }

        return ReadSh2Word(address, cpuIndex);
    }

    private uint ReadSh2CachedCartridgeLong(uint address, uint romOffset, int cpuIndex)
    {
        return (uint)((ReadSh2CachedCartridgeByte(address, romOffset, cpuIndex) << 24) |
            (ReadSh2CachedCartridgeByte(address + 1, romOffset + 1, cpuIndex) << 16) |
            (ReadSh2CachedCartridgeByte(address + 2, romOffset + 2, cpuIndex) << 8) |
            ReadSh2CachedCartridgeByte(address + 3, romOffset + 3, cpuIndex));
    }

    private uint ReadCartridgeLong(uint romOffset)
    {
        return (uint)((ReadCartridgeByte(romOffset) << 24) |
            (ReadCartridgeByte(romOffset + 1) << 16) |
            (ReadCartridgeByte(romOffset + 2) << 8) |
            ReadCartridgeByte(romOffset + 3));
    }

    private void AdvancePwmTimerToElapsed(long masterStartCycles, long slaveStartCycles, ref long lastElapsedCycles)
    {
        long elapsed = Math.Max(MasterSh2.Cycles - masterStartCycles, SlaveSh2.Cycles - slaveStartCycles);
        long delta = elapsed - lastElapsedCycles;
        if (delta <= 0)
        {
            return;
        }

        AdvancePwmTimer((int)Math.Min(delta, int.MaxValue));
        lastElapsedCycles = elapsed;
    }

    public byte ReadSuper32XIdByte(uint address)
    {
        return Super32XId[(int)((address - ThirtyTwoXHardwareProfile.M68kSuper32XId) & 0x03)];
    }

    public void SetCurrentMasterCycle(long masterCycle)
    {
        _currentMasterCycle = Math.Max(_currentMasterCycle, masterCycle);
    }

    public int ClaimM68kCartridgeBus(int byteCount, long masterCycle)
    {
        _currentMasterCycle = Math.Max(_currentMasterCycle, masterCycle);
        long waitMasterCycles = Math.Max(0, _cartridgeRomBusBusyUntilMasterCycle - _currentMasterCycle);
        long accessStart = _currentMasterCycle + waitMasterCycles;
        long accessEnd = accessStart + Math.Max(1, byteCount) * CartridgeRomBusMasterCyclesPerByte;
        _cartridgeRomBusBusyUntilMasterCycle = Math.Max(_cartridgeRomBusBusyUntilMasterCycle, accessEnd);
        return (int)((waitMasterCycles + 6) / 7);
    }

    public void NotifyM68kVdpControlWrite(ushort value)
    {
        if (!CanPublishM68kVdpControlMailbox())
        {
            return;
        }

        ushort comm = ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if (_m68kVdpControlMailboxHighPending)
        {
            _m68kVdpControlMailboxHighPending = false;
            if (value != 0 &&
                ReadBigEndianWord(_systemRegisters, comm + 2) == 0)
            {
                WriteBigEndianWord(_systemRegisters, comm + 2, value);
                ClearM68kCommunicationTrackingForWord(2);
                _m68kVdpControlMailboxArmed = false;
            }
        }

        if ((value & 0xC000) != 0x4000)
        {
            return;
        }

        if (ReadBigEndianWord(_systemRegisters, comm + 12) != 0 ||
            ReadBigEndianWord(_systemRegisters, comm + 14) != 0)
        {
            return;
        }

        WriteBigEndianWord(_systemRegisters, comm + 12, (ushort)(value & 0xC000));
        _m68kVdpControlMailboxHighPending = true;
        ClearM68kCommunicationTrackingForWord(12);
    }

    public void NotifyM68kVdpControlLongWrite(ushort high, ushort low)
    {
        if (!CanPublishM68kVdpControlMailbox() ||
            (high & 0xC000) != 0x4000)
        {
            return;
        }

        ushort comm = ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if (ReadBigEndianWord(_systemRegisters, comm + 12) == 0 &&
            ReadBigEndianWord(_systemRegisters, comm + 14) == 0)
        {
            WriteBigEndianWord(_systemRegisters, comm + 12, (ushort)(high & 0xC000));
            ClearM68kCommunicationTrackingForWord(12);
        }

        ushort lowToken = low == 0 ? (ushort)1 : low;
        if (ReadBigEndianWord(_systemRegisters, comm + 2) == 0)
        {
            WriteBigEndianWord(_systemRegisters, comm + 2, lowToken);
            ClearM68kCommunicationTrackingForWord(2);
        }

        _m68kVdpControlMailboxArmed = false;
        _m68kVdpControlMailboxHighPending = false;
    }

    private bool CanPublishM68kVdpControlMailbox()
    {
        return _adapterEnabled &&
            _sh2ResetReleased &&
            !_bootRomHandshakePending &&
            !_bootRomLaunchPending &&
            _m68kVdpControlMailboxArmed;
    }

    private void ClearM68kCommunicationTrackingForWord(int relativeOffset)
    {
        if ((uint)relativeOffset >= 16u)
        {
            return;
        }

        int evenOffset = relativeOffset & ~1;
        _m68kCommunicationStaleWordValid[evenOffset >> 1] = false;
        _m68kCommunicationStaleValid[evenOffset] = false;
        _m68kCommunicationStaleValid[evenOffset + 1] = false;
        _m68kCommunicationPendingHostBytes[evenOffset] = false;
        _m68kCommunicationPendingHostBytes[evenOffset + 1] = false;
        _m68kCommunicationDeferredSh2ClearBytes[evenOffset] = false;
        _m68kCommunicationDeferredSh2ClearBytes[evenOffset + 1] = false;
    }

    private void TryAdvanceM68kVdpControlMailboxPhase(ushort offset, ushort value)
    {
        ushort comm = ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if (offset != comm + 14 ||
            value > 1)
        {
            return;
        }

        ushort phase = ReadBigEndianWord(_systemRegisters, comm + 12);
        if (phase != 0x4000 &&
            phase != 1)
        {
            return;
        }

        WriteBigEndianWord(_systemRegisters, comm + 12, value);
        ClearM68kCommunicationTrackingForWord(12);
    }

    public uint MapM68kCartridgeAddress(uint address)
    {
        if (address is >= ThirtyTwoXHardwareProfile.M68kCartridgeFixedStart and < ThirtyTwoXHardwareProfile.M68kCartridgeBankedStart)
        {
            return address - ThirtyTwoXHardwareProfile.M68kCartridgeFixedStart;
        }

        if (address is >= ThirtyTwoXHardwareProfile.M68kCartridgeBankedStart and < 0xA0_0000)
        {
            uint bankOffset = (uint)M68kCartridgeBank * ThirtyTwoXHardwareProfile.M68kCartridgeBankedBytes;
            return bankOffset + (address - ThirtyTwoXHardwareProfile.M68kCartridgeBankedStart);
        }

        return address;
    }

    public bool SnoopM68kVdpDmaWord(uint sourceAddress, ushort value)
    {
        if (!IsSh2RomBlockedByRv())
        {
            return false;
        }

        ushort remaining = ReadBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.DreqLengthOffset);
        bool boundedTransfer = remaining != 0;

        uint expectedSource = ReadDreqSourceAddress();
        uint normalizedSource = sourceAddress & 0x00FF_FFFE;
        if (expectedSource != 0 && normalizedSource != expectedSource)
        {
            return false;
        }

        if (_dreqFifo.Count >= DreqFifoCapacity)
        {
            DrainDreqFifoForSnoopedDma();
            if (_dreqFifo.Count >= DreqFifoCapacity)
            {
                return false;
            }
        }

        _dreqFifo.Enqueue(value);
        _dreqFifoWriteCount++;
        if (boundedTransfer)
        {
            remaining--;
            WriteBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.DreqLengthOffset, remaining);
        }

        WriteDreqSourceAddress(normalizedSource + 2);
        if (boundedTransfer && remaining == 0)
        {
            ClearRomToVramDmaRequest();
        }

        TryRunDreqDma();
        return true;
    }

    private void DrainDreqFifoForSnoopedDma()
    {
        TryRunDreqDma();
        if (_dreqFifo.Count >= DreqFifoCapacity)
        {
            RunSh2Cycles(DreqBackpressureSh2Cycles);
            TryRunDreqDma();
        }
    }

    public byte ReadSystemRegisterByte(ushort offset)
    {
        PublishBootRomChecksumAfterHostClear((ushort)(offset & ~1));
        if (TryReadBootRomChecksumByte(offset, includePostStart: true, out byte checksumValue))
        {
            TraceSystemRegisterAccess("M68K", "R8", offset, checksumValue);
            return checksumValue;
        }

        if (_bootRomPostStartSignatureHiddenFromSh2 &&
            TryReadBootRomCommunicationSignatureByte(offset, out byte bootValue))
        {
            TraceSystemRegisterAccess("M68K", "R8", offset, bootValue);
            return bootValue;
        }

        if (TryReadM68kCommunicationStaleByte(offset, out byte staleValue))
        {
            TraceSystemRegisterAccess("M68K", "R8", offset, staleValue);
            return staleValue;
        }

        if (TryReadM68kCommunicationByteLane(offset, out byte laneValue))
        {
            TraceSystemRegisterAccess("M68K", "R8", offset, laneValue);
            return laneValue;
        }

        byte value = ReadSystemRegisterByteCore(offset, sh2View: false);
        TraceSystemRegisterAccess("M68K", "R8", offset, value);
        return value;
    }

    private byte ReadSystemRegisterByteCore(ushort offset, bool sh2View)
    {
        ushort aligned = (ushort)(offset & ~1);
        if (aligned == ThirtyTwoXHardwareProfile.DreqControlOffset)
        {
            ushort word = BuildDreqControlStatus(sh2View);
            return (offset & 1) == 0 ? (byte)(word >> 8) : (byte)word;
        }

        if (sh2View && aligned == ThirtyTwoXHardwareProfile.HCountOffset)
        {
            ushort word = BuildHorizontalCountStatus();
            return (offset & 1) == 0 ? (byte)(word >> 8) : (byte)word;
        }

        if (aligned == ThirtyTwoXHardwareProfile.DreqFifoOffset)
        {
            ushort word = PeekDreqFifo();
            return (offset & 1) == 0 ? (byte)(word >> 8) : (byte)word;
        }

        if (IsPwmPulseWidthOffset(aligned))
        {
            ushort word = ReadPwmPulseStatus(aligned);
            return (offset & 1) == 0 ? (byte)(word >> 8) : (byte)word;
        }

        if (TryReadBootRomChecksumByte(offset, includePostStart: false, out byte checksumValue))
        {
            return checksumValue;
        }

        if ((!sh2View || _bootRomHandshakePending) &&
            TryReadBootRomCommunicationSignatureByte(offset, out byte bootValue))
        {
            return bootValue;
        }

        return _systemRegisters[offset & (SystemRegisterBytes - 1)];
    }

    public ushort ReadSystemRegisterWord(ushort offset)
    {
        PublishBootRomChecksumAfterHostClear((ushort)(offset & ~1));
        if (TryReadBootRomChecksumWord(offset, includePostStart: true, out ushort checksumValue))
        {
            TraceSystemRegisterAccess("M68K", "R16", offset, checksumValue);
            return checksumValue;
        }

        if (_bootRomPostStartSignatureHiddenFromSh2 &&
            TryReadBootRomCommunicationSignatureWord(offset, out ushort bootValue))
        {
            TraceSystemRegisterAccess("M68K", "R16", offset, bootValue);
            return bootValue;
        }

        if (TryReadM68kCommunicationStaleWord(offset, out ushort staleValue))
        {
            TraceSystemRegisterAccess("M68K", "R16", offset, staleValue);
            return staleValue;
        }

        ushort value = ReadSystemRegisterWordCore(offset, popDreqFifo: true, sh2View: false);
        TraceSystemRegisterAccess("M68K", "R16", offset, value);
        return value;
    }

    public bool ShouldSampleM68kSystemRegisterBeforeSync(ushort offset)
    {
        if (!_bootRomPostStartSignaturePending)
        {
            return false;
        }

        int relative = (offset & (SystemRegisterBytes - 1)) - ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        return relative >= 0 && relative < BootRomCommunicationSignature.Length;
    }

    private ushort ReadSystemRegisterWordCore(ushort offset, bool popDreqFifo, bool sh2View)
    {
        ushort aligned = (ushort)(offset & ~1);
        if (aligned == ThirtyTwoXHardwareProfile.DreqControlOffset)
        {
            return BuildDreqControlStatus(sh2View);
        }

        if (sh2View && aligned == ThirtyTwoXHardwareProfile.HCountOffset)
        {
            return BuildHorizontalCountStatus();
        }

        if (aligned == ThirtyTwoXHardwareProfile.DreqFifoOffset)
        {
            return popDreqFifo ? PopDreqFifo() : PeekDreqFifo();
        }

        if (IsPwmPulseWidthOffset(aligned))
        {
            return ReadPwmPulseStatus(aligned);
        }

        if (TryReadBootRomChecksumWord(offset, includePostStart: false, out ushort checksumValue))
        {
            return checksumValue;
        }

        if ((!sh2View || _bootRomHandshakePending) &&
            TryReadBootRomCommunicationSignatureWord(offset, out ushort bootValue))
        {
            return bootValue;
        }

        return ReadBigEndianWord(_systemRegisters, offset & (SystemRegisterBytes - 1));
    }

    public void WriteSystemRegisterByte(ushort offset, byte value)
    {
        int index = offset & (SystemRegisterBytes - 1);
        bool hadBootRomSignature = HasPendingBootRomSignatureWrite((ushort)(index & ~1));
        CancelBootRomHandshakeOnHostDataWrite((ushort)(index & ~1), value);
        if (TryWriteM68kCommunicationByteLane((ushort)index, value))
        {
            SystemRegisterWriteObserver?.Invoke(new SystemRegisterWriteTrace("M68K", (ushort)index, value));
            TraceSystemRegisterAccess("M68K", "W8", (ushort)index, value);
            ReleaseBootRomLaunchOnHostCommand();
            UpdateBootRomHandshakeAfterM68kWrite((ushort)(index & ~1), hadBootRomSignature);
            RetireObservedPostStartSignatureOnHostWrite((ushort)(index & ~1));
            PublishBootRomChecksumAfterHostClear((ushort)(index & ~1));
            return;
        }

        RetireObservedPostStartSignatureOnHostWrite((ushort)(index & ~1));
        ushort aligned = (ushort)(index & ~1);
        _sideEffectPreviousSystemRegisterWord = ReadBigEndianWord(_systemRegisters, aligned);
        RetirePostStartSignatureOnReadyTokenHostClear((ushort)index, 1, value);
        if (!ConsumePostStartHostClearProtection((ushort)index, value))
        {
            _systemRegisters[index] = value;
        }

        MarkM68kCommunicationHostByte((ushort)index, value);
        SystemRegisterWriteObserver?.Invoke(new SystemRegisterWriteTrace("M68K", (ushort)index, value));
        TraceSystemRegisterAccess("M68K", "W8", (ushort)index, value);
        if (aligned != ThirtyTwoXHardwareProfile.DreqFifoOffset)
        {
            ApplySystemRegisterSideEffects(aligned, allowAdapterControl: true);
        }

        ReleaseBootRomLaunchOnHostCommand();
        UpdateBootRomHandshakeAfterM68kWrite((ushort)(index & ~1), hadBootRomSignature);
        TrackBootRomChecksumHostClear(aligned, ReadBigEndianWord(_systemRegisters, aligned));
        PublishBootRomChecksumAfterHostClear((ushort)(index & ~1));
    }

    public void WriteSystemRegisterWord(ushort offset, ushort value)
    {
        int index = offset & (SystemRegisterBytes - 1);
        ushort aligned = (ushort)(index & ~1);
        bool hadBootRomSignature = HasPendingBootRomSignatureWrite(aligned);
        _sideEffectPreviousSystemRegisterWord = ReadBigEndianWord(_systemRegisters, aligned);
        CancelBootRomHandshakeOnHostDataWrite(aligned, value);
        RetireObservedPostStartSignatureOnHostWrite(aligned);
        byte high = (byte)(value >> 8);
        byte low = (byte)value;
        RetirePostStartSignatureOnReadyTokenHostClear(aligned, 2, value);
        if (!ConsumePostStartHostClearProtection((ushort)index, high))
        {
            _systemRegisters[index] = high;
        }

        if (!ConsumePostStartHostClearProtection((ushort)(index + 1), low))
        {
            _systemRegisters[(index + 1) & (SystemRegisterBytes - 1)] = low;
        }

        MarkM68kCommunicationHostByte((ushort)index, high);
        MarkM68kCommunicationHostByte((ushort)(index + 1), low);
        TrackBootRomSixtyEightUpHostWrite(aligned, value);
        SystemRegisterWriteObserver?.Invoke(new SystemRegisterWriteTrace("M68K", (ushort)index, (byte)(value >> 8)));
        SystemRegisterWriteObserver?.Invoke(new SystemRegisterWriteTrace("M68K", (ushort)(index + 1), (byte)value));
        TraceSystemRegisterAccess("M68K", "W16", (ushort)index, value);
        ApplySystemRegisterSideEffects(aligned, allowAdapterControl: true);
        ReleaseBootRomLaunchOnHostCommand();
        UpdateBootRomHandshakeAfterM68kWrite(aligned, hadBootRomSignature);
        TrackBootRomChecksumHostClear(aligned, value);
        PublishBootRomChecksumAfterHostClear(aligned);
        RetireBootRomSixtyEightUpReadyOnHostWrite(aligned, value);
        PublishBootRomSixtyEightUpReadyAfterHostClear(aligned, value);
    }

    public byte ReadVdpRegisterByte(ushort offset)
    {
        ushort aligned = (ushort)(offset & ~1);
        if (!IsDefinedVdpRegisterOffset(aligned))
        {
            VdpRegisterAccessObserver?.Invoke(new SystemRegisterAccessTrace("VDP", "R8", offset, 0));
            return 0;
        }

        if (aligned == ThirtyTwoXHardwareProfile.FrameBufferControlOffset)
        {
            ushort word = ReadVdpRegisterWord(aligned);
            VdpRegisterAccessObserver?.Invoke(new SystemRegisterAccessTrace("VDP", "R8", offset, (offset & 1) == 0 ? (byte)(word >> 8) : (byte)word));
            return (offset & 1) == 0 ? (byte)(word >> 8) : (byte)word;
        }

        if (aligned == ThirtyTwoXHardwareProfile.BitmapModeOffset)
        {
            ushort word = ReadVdpRegisterWord(aligned);
            VdpRegisterAccessObserver?.Invoke(new SystemRegisterAccessTrace("VDP", "R8", offset, (offset & 1) == 0 ? (byte)(word >> 8) : (byte)word));
            return (offset & 1) == 0 ? (byte)(word >> 8) : (byte)word;
        }

        byte value = _vdpRegisters[offset & (VdpRegisterBytes - 1)];
        VdpRegisterAccessObserver?.Invoke(new SystemRegisterAccessTrace("VDP", "R8", offset, value));
        return value;
    }

    private byte PeekVdpRegisterByte(ushort offset)
    {
        ushort aligned = (ushort)(offset & ~1);
        if (!IsDefinedVdpRegisterOffset(aligned))
        {
            return 0;
        }

        if (aligned == ThirtyTwoXHardwareProfile.FrameBufferControlOffset ||
            aligned == ThirtyTwoXHardwareProfile.BitmapModeOffset)
        {
            ushort word = aligned == ThirtyTwoXHardwareProfile.FrameBufferControlOffset
                ? BuildFrameBufferControlStatus()
                : ApplyTvFormatBit(ReadBigEndianWord(_vdpRegisters, aligned));
            return (offset & 1) == 0 ? (byte)(word >> 8) : (byte)word;
        }

        return _vdpRegisters[offset & (VdpRegisterBytes - 1)];
    }

    public ushort ReadVdpRegisterWord(ushort offset)
    {
        if (!IsDefinedVdpRegisterOffset((ushort)(offset & ~1)))
        {
            VdpRegisterAccessObserver?.Invoke(new SystemRegisterAccessTrace("VDP", "R16", offset, 0));
            return 0;
        }

        if ((offset & ~1) == ThirtyTwoXHardwareProfile.FrameBufferControlOffset)
        {
            ushort status = BuildFrameBufferControlStatus();
            VdpRegisterAccessObserver?.Invoke(new SystemRegisterAccessTrace("VDP", "R16", offset, status));
            return status;
        }

        ushort value = ReadBigEndianWord(_vdpRegisters, offset & (VdpRegisterBytes - 1));
        if ((offset & ~1) == ThirtyTwoXHardwareProfile.BitmapModeOffset)
        {
            value = ApplyTvFormatBit(value);
        }

        VdpRegisterAccessObserver?.Invoke(new SystemRegisterAccessTrace("VDP", "R16", offset, value));
        return value;
    }

    public void WriteVdpRegisterByte(ushort offset, byte value)
    {
        int index = offset & (VdpRegisterBytes - 1);
        if (!IsDefinedVdpRegisterOffset((ushort)(index & ~1)))
        {
            VdpRegisterAccessObserver?.Invoke(new SystemRegisterAccessTrace("VDP", "WU8", (ushort)index, value));
            return;
        }

        _vdpRegisters[index] = value;
        VdpRegisterAccessObserver?.Invoke(new SystemRegisterAccessTrace("VDP", "W8", (ushort)index, value));
        TrackVdpRegisterWrite((ushort)(index & ~1));
        ApplyVdpRegisterSideEffects((ushort)(index & ~1), completedWordWrite: (index & 1) != 0);
    }

    public void WriteVdpRegisterWord(ushort offset, ushort value)
    {
        int index = offset & (VdpRegisterBytes - 1);
        if (!IsDefinedVdpRegisterOffset((ushort)(index & ~1)))
        {
            VdpRegisterAccessObserver?.Invoke(new SystemRegisterAccessTrace("VDP", "WU16", (ushort)index, value));
            return;
        }

        if ((index & ~1) == ThirtyTwoXHardwareProfile.BitmapModeOffset)
        {
            value = ApplyTvFormatBit(value);
        }

        WriteBigEndianWord(_vdpRegisters, index, value);
        VdpRegisterAccessObserver?.Invoke(new SystemRegisterAccessTrace("VDP", "W16", (ushort)index, value));
        TrackVdpRegisterWrite((ushort)(index & ~1));
        ApplyVdpRegisterSideEffects((ushort)(index & ~1), completedWordWrite: true);
    }

    private ushort ApplyTvFormatBit(ushort value)
    {
        return _pal ? (ushort)(value & 0x7FFF) : (ushort)(value | 0x8000);
    }

    private static bool IsDefinedVdpRegisterOffset(ushort alignedOffset)
    {
        return alignedOffset is
            ThirtyTwoXHardwareProfile.BitmapModeOffset or
            ThirtyTwoXHardwareProfile.ScreenShiftControlOffset or
            ThirtyTwoXHardwareProfile.AutoFillLengthOffset or
            ThirtyTwoXHardwareProfile.AutoFillStartAddressOffset or
            ThirtyTwoXHardwareProfile.AutoFillDataOffset or
            ThirtyTwoXHardwareProfile.FrameBufferControlOffset;
    }

    public byte ReadPaletteByte(ushort offset)
    {
        byte value = _palette[offset & (_palette.Length - 1)];
        PaletteAccessObserver?.Invoke(new PaletteAccessTrace("API", "R8", offset, value));
        return value;
    }

    public ushort ReadPaletteWord(ushort offset)
    {
        ushort value = ReadBigEndianWord(_palette, offset & (_palette.Length - 1));
        PaletteAccessObserver?.Invoke(new PaletteAccessTrace("API", "R16", offset, value));
        return value;
    }

    public void WritePaletteByte(ushort offset, byte value)
    {
        WritePaletteByte(offset, value, "API");
    }

    public void WritePaletteByte(ushort offset, byte value, string source)
    {
        if (IsM68kPaletteByteAccessDenied(source, offset, value))
        {
            PaletteAccessObserver?.Invoke(new PaletteAccessTrace(source, "DENY-W8", offset, value));
            return;
        }

        _palette[offset & (_palette.Length - 1)] = value;
        _paletteByteWriteCount++;
        PaletteAccessObserver?.Invoke(new PaletteAccessTrace(source, "W8", offset, value));
    }

    public void WritePaletteWord(ushort offset, ushort value)
    {
        WritePaletteWord(offset, value, "API");
    }

    public void WritePaletteWord(ushort offset, ushort value, string source)
    {
        if (IsM68kPaletteWordAccessDenied(source, value))
        {
            PaletteAccessObserver?.Invoke(new PaletteAccessTrace(source, "DENY-W16", offset, value));
            return;
        }

        WriteBigEndianWord(_palette, offset & (_palette.Length - 1), value);
        _paletteByteWriteCount += 2;
        PaletteAccessObserver?.Invoke(new PaletteAccessTrace(source, "W16", offset, value));
    }

    private bool IsM68kPaletteWordAccessDenied(string source, ushort value)
    {
        return source == "M68K" &&
            _vdpAccessGrantedToSh2 &&
            value == 0 &&
            !IsAllZero(_palette);
    }

    private bool IsM68kPaletteByteAccessDenied(string source, ushort offset, byte value)
    {
        if (source != "M68K" || !_vdpAccessGrantedToSh2 || value != 0 || IsAllZero(_palette))
        {
            return false;
        }

        int paired = (offset ^ 1) & (_palette.Length - 1);
        return _palette[paired] == 0;
    }

    public byte ReadFrameBufferByte(uint offset)
    {
        if (IsExternalFrameBufferAccessDenied())
        {
            TraceDeniedFrameBufferAccess("API", "DENY-R8", offset, 0xFFFF);
            return 0xFF;
        }

        return ReadFrameBufferByteCore(offset);
    }

    public ushort ReadFrameBufferWord(uint offset)
    {
        if (IsExternalFrameBufferAccessDenied())
        {
            TraceDeniedFrameBufferAccess("API", "DENY-R16", offset, 0xFFFF);
            return 0xFFFF;
        }

        return ReadFrameBufferWordCore(offset);
    }

    public void WriteFrameBufferByte(uint offset, byte value)
    {
        WriteFrameBufferByteCore(offset, value, "API", overwrite: false, transparentZero: false, enforceAccessWindow: true);
    }

    private byte ReadFrameBufferByteCore(uint offset)
    {
        return _frameBuffers[DrawFrameBufferIndex][(int)(offset % ThirtyTwoXHardwareProfile.FrameBufferBytes)];
    }

    private ushort ReadFrameBufferWordCore(uint offset)
    {
        return ReadBigEndianWord(_frameBuffers[DrawFrameBufferIndex], (int)(offset % ThirtyTwoXHardwareProfile.FrameBufferBytes));
    }

    private void WriteFrameBufferByteCore(uint offset, byte value, string source, bool overwrite, bool transparentZero, bool enforceAccessWindow)
    {
        if (enforceAccessWindow && IsExternalFrameBufferAccessDenied())
        {
            TraceDeniedFrameBufferAccess(source, overwrite ? "DENY-OW8" : "DENY-W8", offset, value);
            return;
        }

        if (transparentZero && value == 0)
        {
            return;
        }

        int physicalOffset = (int)(offset % ThirtyTwoXHardwareProfile.FrameBufferBytes);
        _frameBuffers[DrawFrameBufferIndex][physicalOffset] = value;
        _frameBufferByteWriteCount++;
        FrameBufferAccessObserver?.Invoke(BuildFrameBufferAccessTrace(source, overwrite ? "OW8" : "W8", (uint)physicalOffset, value));
    }

    public void WriteOverwriteImageByte(uint offset, byte value)
    {
        WriteFrameBufferByteCore(offset, value, "API", overwrite: true, transparentZero: true, enforceAccessWindow: true);
    }

    public void WriteOverwriteImageWord(uint offset, ushort value)
    {
        WriteOverwriteImageWordCore(offset, value, "API", enforceAccessWindow: true);
    }

    public void WriteFrameBufferWord(uint offset, ushort value)
    {
        WriteFrameBufferWordCore(offset, value, "API", enforceAccessWindow: true);
    }

    private void WriteFrameBufferWordCore(uint offset, ushort value, string source, bool enforceAccessWindow)
    {
        if (enforceAccessWindow && IsExternalFrameBufferAccessDenied())
        {
            TraceDeniedFrameBufferAccess(source, "DENY-W16", offset, value);
            return;
        }

        int physicalOffset = (int)(offset % ThirtyTwoXHardwareProfile.FrameBufferBytes);
        WriteBigEndianWord(_frameBuffers[DrawFrameBufferIndex], physicalOffset, value);
        _frameBufferByteWriteCount += 2;
        FrameBufferAccessObserver?.Invoke(BuildFrameBufferAccessTrace(source, "W16", (uint)physicalOffset, value));
    }

    private void WriteOverwriteImageWordCore(uint offset, ushort value, string source, bool enforceAccessWindow)
    {
        if (enforceAccessWindow && IsExternalFrameBufferAccessDenied())
        {
            TraceDeniedFrameBufferAccess(source, "DENY-OW16", offset, value);
            return;
        }

        int physicalOffset = (int)(offset % ThirtyTwoXHardwareProfile.FrameBufferBytes);
        byte high = (byte)(value >> 8);
        byte low = (byte)value;
        if (high != 0)
        {
            _frameBuffers[DrawFrameBufferIndex][physicalOffset] = high;
            _frameBufferByteWriteCount++;
        }

        if (low != 0)
        {
            _frameBuffers[DrawFrameBufferIndex][(physicalOffset + 1) % ThirtyTwoXHardwareProfile.FrameBufferBytes] = low;
            _frameBufferByteWriteCount++;
        }

        FrameBufferAccessObserver?.Invoke(BuildFrameBufferAccessTrace(source, "OW16", (uint)physicalOffset, value));
    }

    public void CompositeFrameRgbInto(Span<byte> framebuffer)
    {
        CompositeFrameInto(framebuffer, blueFirst: false, mdOpaquePixels: default);
    }

    public void CompositeFrameRgbInto(Span<byte> framebuffer, ReadOnlySpan<bool> mdOpaquePixels)
    {
        CompositeFrameInto(framebuffer, blueFirst: false, mdOpaquePixels);
    }

    public void CompositeFrameBgrInto(Span<byte> framebuffer)
    {
        CompositeFrameInto(framebuffer, blueFirst: true, mdOpaquePixels: default);
    }

    public void CompositeFrameBgrInto(Span<byte> framebuffer, ReadOnlySpan<bool> mdOpaquePixels)
    {
        CompositeFrameInto(framebuffer, blueFirst: true, mdOpaquePixels);
    }

    public PwmSnapshot CapturePwm()
    {
        return new PwmSnapshot(_pwmLeft.ToArray(), _pwmRight.ToArray(), _pwmMono.ToArray());
    }

    public void RenderPwmStereoSamplesInto(Span<short> output, int samples)
    {
        if (output.Length < samples * 2)
        {
            throw new ArgumentException("Destination buffer is too small.", nameof(output));
        }

        ushort cycle = (ushort)(ReadSystemRegisterWord(ThirtyTwoXHardwareProfile.PwmCycleOffset) & 0x0FFF);
        if (cycle == 0)
        {
            cycle = 0x1000;
        }

        EnsurePwmRenderBuffers(samples);
        Span<short> leftPwm = _pwmLeftRenderBuffer.AsSpan(0, samples);
        Span<short> rightPwm = _pwmRightRenderBuffer.AsSpan(0, samples);
        leftPwm.Clear();
        rightPwm.Clear();
        RenderPwmChannelMono(_pwmLeft, leftPwm, samples, cycle, ref _pwmLeftLevel);
        RenderPwmChannelMono(_pwmRight, rightPwm, samples, cycle, ref _pwmRightLevel);
        MixRoutedPwmChannel(leftPwm, output, samples, ReadLeftPwmRoute());
        MixRoutedPwmChannel(rightPwm, output, samples, ReadRightPwmRoute());
        RenderPwmMono(_pwmMono, output, samples, cycle, ref _pwmMonoLevel);
    }

    private void EnsurePwmRenderBuffers(int samples)
    {
        if (_pwmLeftRenderBuffer.Length < samples)
        {
            _pwmLeftRenderBuffer = new short[samples];
        }

        if (_pwmRightRenderBuffer.Length < samples)
        {
            _pwmRightRenderBuffer = new short[samples];
        }
    }

    public ThirtyTwoXState CaptureState()
    {
        return new ThirtyTwoXState(
            (byte[])_sdram.Clone(),
            (byte[])_frameBuffers[0].Clone(),
            (byte[])_frameBuffers[1].Clone(),
            (byte[])_palette.Clone(),
            (byte[])_systemRegisters.Clone(),
            (bool[])_m68kCommunicationPendingHostBytes.Clone(),
            (bool[])_m68kCommunicationDeferredSh2ClearBytes.Clone(),
            (byte[])_vdpRegisters.Clone(),
            _pwmLeft.ToArray(),
            _pwmRight.ToArray(),
            _pwmMono.ToArray(),
            _pwmLeftHardwareFifo.ToArray(),
            _pwmRightHardwareFifo.ToArray(),
            _pwmMonoHardwareFifo.ToArray(),
            _pwmLeftLevel,
            _pwmRightLevel,
            _pwmMonoLevel,
            _masterPwmInterruptPending,
            _slavePwmInterruptPending,
            _pwmCycleCounter,
            _pwmTimerCounter,
            _dreqFifo.ToArray(),
            (byte[])_sh2DmaRegisters[0].Clone(),
            (byte[])_sh2DmaRegisters[1].Clone(),
            (byte[])_sh2PeripheralRegisters[0].Clone(),
            (byte[])_sh2PeripheralRegisters[1].Clone(),
            (int[])_sh2WatchdogCycleCounters.Clone(),
            (bool[])_sh2WatchdogInterruptPending.Clone(),
            (byte[])_sh2WatchdogWriteSelect.Clone(),
            (long[])_sh2FrtBaseCycles.Clone(),
            (ushort[])_sh2FrtBaseCounters.Clone(),
            (ushort[])_sh2FrtLastCounters.Clone(),
            (ushort[])_sh2FrtOutputCompareB.Clone(),
            (byte[])_sh2CacheDataArrays[0].Clone(),
            (byte[])_sh2CacheDataArrays[1].Clone(),
            (byte[])_sh2CacheDataValid[0].Clone(),
            (byte[])_sh2CacheDataValid[1].Clone(),
            (byte[])_sh2PrivateWorkRam[0].Clone(),
            (byte[])_sh2PrivateWorkRam[1].Clone(),
            (uint[])_sh2CacheTags[0].Clone(),
            (uint[])_sh2CacheTags[1].Clone(),
            (byte[])_sh2CacheLru[0].Clone(),
            (byte[])_sh2CacheLru[1].Clone(),
            (uint[])_sh2DivisionRegisters[0].Clone(),
            (uint[])_sh2DivisionRegisters[1].Clone(),
            (byte[])_sh2DmaRequestSelect.Clone(),
            _activeDisplayFrameBufferIndex,
            _adapterEnabled,
            _sh2ResetEnabled,
            _sh2ResetReleased,
            _vdpAccessGrantedToSh2,
            _vBlank,
            _hBlank,
            _currentScanline,
            _frameBufferSwapPending,
            _pendingDrawFrameBufferIndex,
            _requestedDisplayFrameBufferIndex,
            _latchedBitmapMode,
            _latchedScreenShiftControl,
            _lastCompositeUsedFallback,
            _lastCompositeMode,
            _masterInterruptMask,
            _slaveInterruptMask,
            _masterVerticalInterruptPending,
            _slaveVerticalInterruptPending,
            _masterVresInterruptPending,
            _slaveVresInterruptPending,
            _masterHorizontalInterruptPending,
            _slaveHorizontalInterruptPending,
            _horizontalInterruptPeriod,
            _horizontalInterruptCounter,
            _masterCommandInterruptPending,
            _slaveCommandInterruptPending,
            _bootRomHandshakePending,
            _bootRomSignatureRead,
            _bootRomSignatureReadbackActive,
            _bootRomLaunchPending,
            _bootRomPostStartSignaturePending,
            _bootRomPostStartSignatureHiddenFromSh2,
            _bootRomPostStartSignatureReadMask,
            _bootRomPostStartHostClearProtectMask,
            _bootRomChecksumPublished,
            _bootRomChecksumHostCleared,
            _bootRomSixtyEightUpPending,
            _bootRomSixtyEightUpReadyHiddenFromSh2,
            _m68kVdpControlMailboxArmed,
            MasterSh2.CaptureState(),
            SlaveSh2.CaptureState());
    }

    public void RestoreState(ThirtyTwoXState state)
    {
        CopyStateArray(state.Sdram, _sdram);
        CopyStateArray(state.FrameBuffer0, _frameBuffers[0]);
        CopyStateArray(state.FrameBuffer1, _frameBuffers[1]);
        CopyStateArray(state.Palette, _palette);
        CopyStateArray(state.SystemRegisters, _systemRegisters);
        Array.Clear(_m68kCommunicationStaleBytes);
        Array.Clear(_m68kCommunicationStaleValid);
        Array.Clear(_m68kCommunicationStaleWords);
        Array.Clear(_m68kCommunicationStaleWordValid);
        CopyStateArray(state.M68kCommunicationPendingHostBytes, _m68kCommunicationPendingHostBytes);
        CopyStateArray(state.M68kCommunicationDeferredSh2ClearBytes, _m68kCommunicationDeferredSh2ClearBytes);
        CopyStateArray(state.VdpRegisters, _vdpRegisters);
        _pwmLeft.Clear();
        _pwmRight.Clear();
        _pwmMono.Clear();
        _pwmLeftHardwareFifo.Clear();
        _pwmRightHardwareFifo.Clear();
        _pwmMonoHardwareFifo.Clear();
        _dreqFifo.Clear();
        Array.Clear(_sh2DmaRegisters[0]);
        Array.Clear(_sh2DmaRegisters[1]);
        Array.Clear(_sh2PeripheralRegisters[0]);
        Array.Clear(_sh2PeripheralRegisters[1]);
        Array.Clear(_sh2WatchdogCycleCounters);
        Array.Clear(_sh2WatchdogInterruptPending);
        Array.Clear(_sh2WatchdogWriteSelect);
        Array.Clear(_sh2CacheDataArrays[0]);
        Array.Clear(_sh2CacheDataArrays[1]);
        Array.Clear(_sh2CacheDataValid[0]);
        Array.Clear(_sh2CacheDataValid[1]);
        Array.Clear(_sh2PrivateWorkRam[0]);
        Array.Clear(_sh2PrivateWorkRam[1]);
        ResetSh2CacheTags();
        Array.Clear(_sh2DivisionRegisters[0]);
        Array.Clear(_sh2DivisionRegisters[1]);
        ClearSh2CartridgeCache();
        Array.Clear(_sh2DmaRequestSelect);
        RestorePwm(_pwmLeft, state.PwmLeft);
        RestorePwm(_pwmRight, state.PwmRight);
        RestorePwm(_pwmMono, state.PwmMono);
        RestorePwm(_pwmLeftHardwareFifo, state.PwmLeftHardwareFifo, PwmHardwareFifoCapacity);
        RestorePwm(_pwmRightHardwareFifo, state.PwmRightHardwareFifo, PwmHardwareFifoCapacity);
        RestorePwm(_pwmMonoHardwareFifo, state.PwmMonoHardwareFifo, PwmHardwareFifoCapacity);
        _pwmLeftLevel = state.PwmLeftLevel;
        _pwmRightLevel = state.PwmRightLevel;
        _pwmMonoLevel = state.PwmMonoLevel;
        _masterPwmInterruptPending = state.MasterPwmInterruptPending;
        _slavePwmInterruptPending = state.SlavePwmInterruptPending;
        _pwmCycleCounter = state.PwmCycleCounter;
        _pwmTimerCounter = state.PwmTimerCounter;
        RestoreDreqFifo(state.DreqFifo);
        CopyStateArray(state.MasterDmaRegisters, _sh2DmaRegisters[0]);
        CopyStateArray(state.SlaveDmaRegisters, _sh2DmaRegisters[1]);
        CopyStateArray(state.MasterPeripheralRegisters, _sh2PeripheralRegisters[0]);
        CopyStateArray(state.SlavePeripheralRegisters, _sh2PeripheralRegisters[1]);
        Array.Copy(state.WatchdogCycleCounters, _sh2WatchdogCycleCounters, Math.Min(_sh2WatchdogCycleCounters.Length, state.WatchdogCycleCounters.Length));
        Array.Copy(state.WatchdogInterruptPending, _sh2WatchdogInterruptPending, Math.Min(_sh2WatchdogInterruptPending.Length, state.WatchdogInterruptPending.Length));
        Array.Copy(state.WatchdogWriteSelect, _sh2WatchdogWriteSelect, Math.Min(_sh2WatchdogWriteSelect.Length, state.WatchdogWriteSelect.Length));
        Array.Copy(state.FrtBaseCycles, _sh2FrtBaseCycles, Math.Min(_sh2FrtBaseCycles.Length, state.FrtBaseCycles.Length));
        Array.Copy(state.FrtBaseCounters, _sh2FrtBaseCounters, Math.Min(_sh2FrtBaseCounters.Length, state.FrtBaseCounters.Length));
        Array.Copy(state.FrtLastCounters, _sh2FrtLastCounters, Math.Min(_sh2FrtLastCounters.Length, state.FrtLastCounters.Length));
        Array.Fill(_sh2FrtOutputCompareB, (ushort)0xFFFF);
        Array.Copy(state.FrtOutputCompareB, _sh2FrtOutputCompareB, Math.Min(_sh2FrtOutputCompareB.Length, state.FrtOutputCompareB.Length));
        CopyStateArray(state.MasterCacheDataArray, _sh2CacheDataArrays[0]);
        CopyStateArray(state.SlaveCacheDataArray, _sh2CacheDataArrays[1]);
        CopyStateArray(state.MasterCacheDataValid, _sh2CacheDataValid[0]);
        CopyStateArray(state.SlaveCacheDataValid, _sh2CacheDataValid[1]);
        CopyStateArray(state.MasterPrivateWorkRam, _sh2PrivateWorkRam[0]);
        CopyStateArray(state.SlavePrivateWorkRam, _sh2PrivateWorkRam[1]);
        CopyStateArray(state.MasterCacheTags, _sh2CacheTags[0]);
        CopyStateArray(state.SlaveCacheTags, _sh2CacheTags[1]);
        CopyStateArray(state.MasterCacheLru, _sh2CacheLru[0]);
        CopyStateArray(state.SlaveCacheLru, _sh2CacheLru[1]);
        CopyStateArray(state.MasterDivisionRegisters, _sh2DivisionRegisters[0]);
        CopyStateArray(state.SlaveDivisionRegisters, _sh2DivisionRegisters[1]);
        Array.Copy(state.DmaRequestSelect, _sh2DmaRequestSelect, Math.Min(_sh2DmaRequestSelect.Length, state.DmaRequestSelect.Length));
        _activeDisplayFrameBufferIndex = state.ActiveDisplayFrameBufferIndex & 0x01;
        _visibleDisplayFrameBufferIndex = _activeDisplayFrameBufferIndex;
        _m68kCartridgeBank = (ushort)(ReadBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.BankSetOffset) & 0x0003);
        _adapterEnabled = state.AdapterEnabled;
        _sh2ResetEnabled = state.Sh2ResetEnabled;
        _sh2ResetReleased = state.Sh2ResetReleased;
        _vdpAccessGrantedToSh2 = state.VdpAccessGrantedToSh2;
        _vBlank = state.VBlank;
        _hBlank = state.HBlank;
        _currentScanline = state.CurrentScanline;
        _frameBufferSwapPending = state.FrameBufferSwapPending;
        _pendingDrawFrameBufferIndex = state.PendingDrawFrameBufferIndex & 0x01;
        _requestedDisplayFrameBufferIndex = state.RequestedDisplayFrameBufferIndex & 0x01;
        _latchedBitmapMode = state.LatchedBitmapMode;
        _latchedScreenShiftControl = state.LatchedScreenShiftControl;
        _lastCompositeUsedFallback = state.LastCompositeUsedFallback;
        _lastCompositeMode = state.LastCompositeMode;
        _masterInterruptMask = state.MasterInterruptMask;
        _slaveInterruptMask = state.SlaveInterruptMask;
        _masterVerticalInterruptPending = state.MasterVerticalInterruptPending;
        _slaveVerticalInterruptPending = state.SlaveVerticalInterruptPending;
        _masterVresInterruptPending = state.MasterVresInterruptPending;
        _slaveVresInterruptPending = state.SlaveVresInterruptPending;
        _masterHorizontalInterruptPending = state.MasterHorizontalInterruptPending;
        _slaveHorizontalInterruptPending = state.SlaveHorizontalInterruptPending;
        _horizontalInterruptPeriod = state.HorizontalInterruptPeriod;
        _horizontalInterruptCounter = state.HorizontalInterruptCounter;
        _masterCommandInterruptPending = state.MasterCommandInterruptPending;
        _slaveCommandInterruptPending = state.SlaveCommandInterruptPending;
        _bootRomHandshakePending = state.BootRomHandshakePending;
        _bootRomSignatureRead = state.BootRomSignatureRead;
        _bootRomSignatureReadbackActive = state.BootRomSignatureReadbackActive;
        _bootRomLaunchPending = state.BootRomLaunchPending;
        _bootRomPostStartSignaturePending = state.BootRomPostStartSignaturePending;
        _bootRomPostStartSignatureHiddenFromSh2 = state.BootRomPostStartSignatureHiddenFromSh2;
        _bootRomPostStartSignatureReadMask = state.BootRomPostStartSignatureReadMask;
        _bootRomPostStartHostClearProtectMask = state.BootRomPostStartHostClearProtectMask;
        _bootRomChecksumPublished = state.BootRomChecksumPublished;
        _bootRomChecksumHostCleared = state.BootRomChecksumHostCleared;
        _bootRomSixtyEightUpPending = state.BootRomSixtyEightUpPending;
        _bootRomSixtyEightUpReadyHiddenFromSh2 = state.BootRomSixtyEightUpReadyHiddenFromSh2;
        _m68kVdpControlMailboxArmed = state.M68kVdpControlMailboxArmed;
        MasterSh2.RestoreState(state.MasterSh2);
        SlaveSh2.RestoreState(state.SlaveSh2);
    }

    internal byte ReadSh2Byte(uint address, int cpuIndex)
    {
        if (IsSh2DivisionUnitRegisterAddress(address))
        {
            byte value = ReadSh2DivisionUnitByte(address, cpuIndex);
            TraceSh2MemoryAccess(cpuIndex, "RI8", address, value);
            return value;
        }

        if (TryMapSh2PrivateWorkRamAddress(address, cpuIndex, out int privateRamOffset))
        {
            byte value = _sh2PrivateWorkRam[cpuIndex & 1][privateRamOffset];
            TraceSh2MemoryAccess(cpuIndex, "R8", address, value);
            return value;
        }

        if (TryMapSh2CacheDataArrayAddress(address, cpuIndex, out int cacheDataOffset))
        {
            byte value = _sh2CacheDataArrays[cpuIndex][cacheDataOffset];
            TraceSh2MemoryAccess(cpuIndex, "R8", address, value);
            return value;
        }

        if (TryMapSh2CachedSdramAddress(address, out int cachedSdramOffset))
        {
            byte value = IsSh2DataCacheEnabled(cpuIndex)
                ? ReadSh2CachedSdramByte(address, cachedSdramOffset, cpuIndex)
                : ReadSh2SdramByteNoCache(cachedSdramOffset, cpuIndex);
            TraceSh2MemoryAccess(cpuIndex, "RC8", address, value);
            return value;
        }

        if (TryMapSh2SdramAddress(address, out int sdramOffset))
        {
            AddSh2WaitCycles(cpuIndex, Sh2SdramReadWaitCycles);
            byte value = _sdram[sdramOffset];
            TraceSh2MemoryAccess(cpuIndex, "R8", address, value);
            return value;
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2SystemRegisterStart and < ThirtyTwoXHardwareProfile.Sh2SystemRegisterStart + 0x80)
        {
            AddSh2WaitCycles(cpuIndex, Sh2SystemRegisterWaitCycles);
            return ReadSh2SystemRegisterByte((ushort)(address - ThirtyTwoXHardwareProfile.Sh2SystemRegisterStart), cpuIndex);
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2SystemRegisterCachedStart and < ThirtyTwoXHardwareProfile.Sh2SystemRegisterCachedStart + 0x80)
        {
            AddSh2WaitCycles(cpuIndex, Sh2SystemRegisterWaitCycles);
            return ReadSh2SystemRegisterByte((ushort)(address - ThirtyTwoXHardwareProfile.Sh2SystemRegisterCachedStart), cpuIndex);
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2VdpRegisterStart and < ThirtyTwoXHardwareProfile.Sh2VdpRegisterStart + 0x80)
        {
            AddSh2WaitCycles(cpuIndex, Sh2VdpRegisterWaitCycles);
            return ReadVdpRegisterByte((ushort)(address - ThirtyTwoXHardwareProfile.Sh2VdpRegisterStart));
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2VdpRegisterCachedStart and < ThirtyTwoXHardwareProfile.Sh2VdpRegisterCachedStart + 0x80)
        {
            AddSh2WaitCycles(cpuIndex, Sh2VdpRegisterWaitCycles);
            return ReadVdpRegisterByte((ushort)(address - ThirtyTwoXHardwareProfile.Sh2VdpRegisterCachedStart));
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2ColorPaletteStart and < ThirtyTwoXHardwareProfile.Sh2ColorPaletteStart + (ThirtyTwoXHardwareProfile.PaletteEntries * 2))
        {
            AddSh2WaitCycles(cpuIndex, Sh2PaletteWaitCycles);
            if (IsSh2PaletteAccessDenied())
            {
                TraceSh2MemoryAccess(cpuIndex, "DPAL8", address, 0x00FF);
                return 0xFF;
            }

            AddSh2PaletteBusyWaitIfNeeded(cpuIndex);
            byte value = ReadPaletteByte((ushort)(address - ThirtyTwoXHardwareProfile.Sh2ColorPaletteStart));
            TraceSh2MemoryAccess(cpuIndex, "RPAL8", address, value);
            return value;
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2ColorPaletteCachedStart and < ThirtyTwoXHardwareProfile.Sh2ColorPaletteCachedStart + (ThirtyTwoXHardwareProfile.PaletteEntries * 2))
        {
            AddSh2WaitCycles(cpuIndex, Sh2PaletteWaitCycles);
            if (IsSh2PaletteAccessDenied())
            {
                TraceSh2MemoryAccess(cpuIndex, "DPAL8", address, 0x00FF);
                return 0xFF;
            }

            AddSh2PaletteBusyWaitIfNeeded(cpuIndex);
            byte value = ReadPaletteByte((ushort)(address - ThirtyTwoXHardwareProfile.Sh2ColorPaletteCachedStart));
            TraceSh2MemoryAccess(cpuIndex, "RPAL8", address, value);
            return value;
        }

        if (TryReadSh2BootRomByte(address, cpuIndex, out byte bootRomValue))
        {
            TraceSh2MemoryAccess(cpuIndex, "RB8", address, bootRomValue);
            return bootRomValue;
        }

        if (TryMapSh2FrameBufferAddress(address, out uint frameBufferOffset, out _))
        {
            AddSh2WaitCycles(cpuIndex, Sh2FrameBufferReadWaitCycles);
            if (IsSh2FrameBufferAccessDenied())
            {
                TraceDeniedFrameBufferAccess(cpuIndex == 0 ? "MSH2" : "SSH2", "DENY-R8", frameBufferOffset, 0xFFFF);
                return 0xFF;
            }

            AddSh2FrameBufferBusyWaitIfNeeded(cpuIndex);
            return ReadFrameBufferByteCore(frameBufferOffset);
        }

        if (TryMapSh2CachedCartridgeAddress(address, out uint cacheOffset, out uint romOffset))
        {
            byte value = IsSh2DataCacheEnabled(cpuIndex)
                ? ReadSh2CachedCartridgeByte(cacheOffset, romOffset, cpuIndex)
                : ReadCartridgeByte(romOffset);
            TraceSh2MemoryAccess(cpuIndex, "RC8", address, value);
            return value;
        }

        if (TryMapSh2UncachedBankedCartridgeAddress(address, out romOffset))
        {
            AddSh2WaitCycles(cpuIndex, IsSh2RomBlockedByRv() ? Sh2CartridgeRvBlockedWaitCycles : Sh2CartridgeByteWaitCycles);
            ClaimSh2CartridgeBus(1);
            byte value = ReadCartridgeByte(romOffset);
            TraceSh2MemoryAccess(cpuIndex, "R8", address, value);
            return value;
        }

        if (IsSh2DmaRegisterAddress(address))
        {
            return ReadSh2DmaByte(address, cpuIndex);
        }

        if (TryMapSh2CachePurgeAddress(address))
        {
            TraceSh2MemoryAccess(cpuIndex, "RP8", address, 0x0000);
            return 0x00;
        }

        if (IsSh2PeripheralRegisterAddress(address))
        {
            return ReadSh2PeripheralByte(address, cpuIndex);
        }

        if (IsSh2InternalRegisterAddress(address))
        {
            TraceSh2MemoryAccess(cpuIndex, "RI8", address, 0x0000);
            return 0x00;
        }

        if (address >= ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart)
        {
            AddSh2WaitCycles(cpuIndex, IsSh2RomBlockedByRv() ? Sh2CartridgeRvBlockedWaitCycles : Sh2CartridgeByteWaitCycles);
            ClaimSh2CartridgeBus(1);
            return ReadCartridgeByte(address - ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
        }

        TraceSh2MemoryAccess(cpuIndex, "RU8", address, 0x00FF);
        return 0xFF;
    }

    internal ushort ReadSh2Word(uint address, int cpuIndex)
    {
        if (TryMapSh2PrivateWorkRamAddress(address, cpuIndex, out int privateRamOffset))
        {
            byte[] ram = _sh2PrivateWorkRam[cpuIndex & 1];
            ushort value = (ushort)((ram[privateRamOffset] << 8) |
                ram[(privateRamOffset + 1) & ((int)Sh2PrivateWorkRamBytes - 1)]);
            TraceSh2MemoryAccess(cpuIndex, "R16", address, value);
            return value;
        }

        if (TryMapSh2CacheDataArrayAddress(address, cpuIndex, out int cacheDataOffset))
        {
            byte[] cacheData = _sh2CacheDataArrays[cpuIndex & 1];
            ushort value = (ushort)((cacheData[cacheDataOffset] << 8) |
                cacheData[(cacheDataOffset + 1) & (Sh2CacheDataArrayBytes - 1)]);
            TraceSh2MemoryAccess(cpuIndex, "R16", address, value);
            return value;
        }

        if (TryReadSh2BootRomServiceWord(address, cpuIndex, out ushort bootServiceWord))
        {
            TraceSh2MemoryAccess(cpuIndex, "RB16", address, bootServiceWord);
            return bootServiceWord;
        }

        if (IsSh2DivisionUnitRegisterAddress(address))
        {
            ushort value = ReadSh2DivisionUnitWord(address, cpuIndex);
            TraceSh2MemoryAccess(cpuIndex, "RI16", address, value);
            return value;
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2SystemRegisterStart and < ThirtyTwoXHardwareProfile.Sh2SystemRegisterStart + 0x80)
        {
            AddSh2WaitCycles(cpuIndex, Sh2SystemRegisterWaitCycles);
            return ReadSh2SystemRegisterWord((ushort)(address - ThirtyTwoXHardwareProfile.Sh2SystemRegisterStart), cpuIndex);
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2SystemRegisterCachedStart and < ThirtyTwoXHardwareProfile.Sh2SystemRegisterCachedStart + 0x80)
        {
            AddSh2WaitCycles(cpuIndex, Sh2SystemRegisterWaitCycles);
            return ReadSh2SystemRegisterWord((ushort)(address - ThirtyTwoXHardwareProfile.Sh2SystemRegisterCachedStart), cpuIndex);
        }

        if (TryMapSh2CachePurgeAddress(address))
        {
            TraceSh2MemoryAccess(cpuIndex, "RP16", address, 0x0000);
            return 0x0000;
        }

        return (ushort)((ReadSh2Byte(address, cpuIndex) << 8) | ReadSh2Byte(address + 1, cpuIndex));
    }

    internal uint ReadSh2Long(uint address, int cpuIndex)
    {
        if (TryMapSh2PrivateWorkRamAddress(address, cpuIndex, out int privateRamOffset))
        {
            byte[] ram = _sh2PrivateWorkRam[cpuIndex & 1];
            uint value = (uint)((ram[privateRamOffset] << 24) |
                (ram[(privateRamOffset + 1) & ((int)Sh2PrivateWorkRamBytes - 1)] << 16) |
                (ram[(privateRamOffset + 2) & ((int)Sh2PrivateWorkRamBytes - 1)] << 8) |
                ram[(privateRamOffset + 3) & ((int)Sh2PrivateWorkRamBytes - 1)]);
            TraceSh2MemoryAccess(cpuIndex, "R32", address, value);
            return value;
        }

        if (TryMapSh2CacheDataArrayAddress(address, cpuIndex, out int cacheDataOffset))
        {
            byte[] cacheData = _sh2CacheDataArrays[cpuIndex & 1];
            uint value = (uint)((cacheData[cacheDataOffset] << 24) |
                (cacheData[(cacheDataOffset + 1) & (Sh2CacheDataArrayBytes - 1)] << 16) |
                (cacheData[(cacheDataOffset + 2) & (Sh2CacheDataArrayBytes - 1)] << 8) |
                cacheData[(cacheDataOffset + 3) & (Sh2CacheDataArrayBytes - 1)]);
            TraceSh2MemoryAccess(cpuIndex, "R32", address, value);
            return value;
        }

        if (TryConsumeBootRomSixtyEightUpReadyLong(address, out uint bootRomSixtyEightUpReady))
        {
            TraceSh2MemoryAccess(cpuIndex, "R32", address, bootRomSixtyEightUpReady);
            return bootRomSixtyEightUpReady;
        }

        if (IsSh2DivisionUnitRegisterAddress(address))
        {
            uint value = ReadSh2DivisionUnitLong(address, cpuIndex);
            TraceSh2MemoryAccess(cpuIndex, "RI32", address, value);
            return value;
        }

        if ((address & 0x03) != 0 &&
            TryMapSh2CachedCartridgeAddress(address & ~3u, out uint alignedCacheOffset, out uint alignedRomOffset))
        {
            uint value = IsSh2DataCacheEnabled(cpuIndex)
                ? ReadSh2CachedCartridgeLong(alignedCacheOffset, alignedRomOffset, cpuIndex)
                : ReadCartridgeLong(alignedRomOffset);
            TraceSh2MemoryAccess(cpuIndex, "RC32A", address, value);
            return value;
        }

        if (TryMapSh2CacheAddressArrayAddress(address, out int cacheAddressOffset))
        {
            uint value = ReadSh2CacheAddressArrayLong(address, cacheAddressOffset, cpuIndex);
            TraceSh2MemoryAccess(cpuIndex, "RA32", address, value);
            return value;
        }

        if (TryMapSh2CachePurgeAddress(address))
        {
            TraceSh2MemoryAccess(cpuIndex, "RP32", address, 0x0000);
            return 0x0000_0000;
        }

        return (uint)((ReadSh2Word(address, cpuIndex) << 16) | ReadSh2Word(address + 2, cpuIndex));
    }

    internal bool TryPeekSh2Word(uint address, int cpuIndex, out ushort value)
    {
        if (TryPeekSh2Byte(address, cpuIndex, out byte high) &&
            TryPeekSh2Byte(address + 1, cpuIndex, out byte low))
        {
            value = (ushort)((high << 8) | low);
            return true;
        }

        value = 0;
        return false;
    }

    internal bool TryPeekSh2Byte(uint address, int cpuIndex, out byte value)
    {
        if (TryMapSh2CachedSdramAddress(address, out int cachedSdramOffset))
        {
            if (IsSh2DataCacheEnabled(cpuIndex) &&
                TryPeekSh2CacheByte(address, cpuIndex, out value))
            {
                return true;
            }

            value = _sdram[cachedSdramOffset];
            return true;
        }

        if (TryMapSh2SdramAddress(address, out int sdramOffset))
        {
            value = _sdram[sdramOffset];
            return true;
        }

        if (TryMapSh2PrivateWorkRamAddress(address, cpuIndex, out int privateRamOffset))
        {
            value = _sh2PrivateWorkRam[cpuIndex & 1][privateRamOffset];
            return true;
        }

        if (TryMapSh2CacheDataArrayAddress(address, cpuIndex, out int cacheDataOffset))
        {
            value = _sh2CacheDataArrays[cpuIndex & 1][cacheDataOffset];
            return true;
        }

        if (TryMapSh2FrameBufferAddress(address, out uint frameBufferOffset, out _))
        {
            value = IsSh2FrameBufferAccessDenied()
                ? (byte)0xFF
                : ReadFrameBufferByteCore(frameBufferOffset);
            return true;
        }

        if (TryReadSh2BootRomByte(address, cpuIndex, out value))
        {
            return true;
        }

        if (TryMapSh2CachedCartridgeAddress(address, out uint cacheOffset, out uint romOffset))
        {
            if (IsSh2DataCacheEnabled(cpuIndex) &&
                TryPeekSh2CacheByte(cacheOffset, cpuIndex, out value))
            {
                return true;
            }

            value = ReadCartridgeByte(romOffset);
            return true;
        }

        if (TryMapSh2UncachedBankedCartridgeAddress(address, out romOffset))
        {
            value = ReadCartridgeByte(romOffset);
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2SystemRegisterStart and < ThirtyTwoXHardwareProfile.Sh2SystemRegisterStart + 0x80)
        {
            value = PeekSh2SystemRegisterByte((ushort)(address - ThirtyTwoXHardwareProfile.Sh2SystemRegisterStart), cpuIndex);
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2SystemRegisterCachedStart and < ThirtyTwoXHardwareProfile.Sh2SystemRegisterCachedStart + 0x80)
        {
            value = PeekSh2SystemRegisterByte((ushort)(address - ThirtyTwoXHardwareProfile.Sh2SystemRegisterCachedStart), cpuIndex);
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2VdpRegisterStart and < ThirtyTwoXHardwareProfile.Sh2VdpRegisterStart + 0x80)
        {
            value = PeekVdpRegisterByte((ushort)(address - ThirtyTwoXHardwareProfile.Sh2VdpRegisterStart));
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2VdpRegisterCachedStart and < ThirtyTwoXHardwareProfile.Sh2VdpRegisterCachedStart + 0x80)
        {
            value = PeekVdpRegisterByte((ushort)(address - ThirtyTwoXHardwareProfile.Sh2VdpRegisterCachedStart));
            return true;
        }

        if (TryPeekSh2PeripheralByte(address, cpuIndex, out value))
        {
            return true;
        }

        if (TryPeekSh2DmaByte(address, cpuIndex, out value))
        {
            return true;
        }

        if (IsSh2DivisionUnitRegisterAddress(address) ||
            IsSh2PeripheralRegisterAddress(address))
        {
            value = 0;
            return false;
        }

        if (IsSh2InternalRegisterAddress(address))
        {
            value = 0x00;
            return true;
        }

        if (address >= ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart)
        {
            value = ReadCartridgeByte(address - ThirtyTwoXHardwareProfile.Sh2CartridgeFixedStart);
            return true;
        }

        value = 0;
        return false;
    }

    private bool TryPeekSh2PeripheralByte(uint address, int cpuIndex, out byte value)
    {
        value = 0;
        if (!IsSh2PeripheralRegisterAddress(address))
        {
            return false;
        }

        if (address == Sh2WatchdogRegisterStart ||
            address == Sh2WatchdogCounterAddress ||
            address == Sh2WatchdogResetControlAddress ||
            address is >= Sh2FreeRunningCounterStart and < Sh2FreeRunningCounterStart + 2 ||
            address is >= Sh2FrtOutputCompareRegisterStart and < Sh2FrtOutputCompareRegisterStart + 2)
        {
            return false;
        }

        value = _sh2PeripheralRegisters[cpuIndex & 1][(int)(address - Sh2PeripheralRegisterStart)];
        return true;
    }

    private bool TryPeekSh2DmaByte(uint address, int cpuIndex, out byte value)
    {
        if (TryGetSh2DmaRequestSelectIndex(address, out int requestSelectIndex))
        {
            value = _sh2DmaRequestSelect[requestSelectIndex];
            return true;
        }

        if (address is >= Sh2DmaRegisterStart and < Sh2DmaRegisterEnd)
        {
            value = _sh2DmaRegisters[cpuIndex & 1][(int)(address - Sh2DmaRegisterStart)];
            return true;
        }

        value = 0;
        return false;
    }

    private bool TryReadSh2BootRomByte(uint address, int cpuIndex, out byte value)
    {
        if (!AdapterEnabled || !_sh2ResetReleased)
        {
            value = 0;
            return false;
        }

        uint normalized = address & 0x1FFF_FFFFu;
        if (normalized >= Sh2BootRomMappedBytes)
        {
            value = 0;
            return false;
        }

        byte[] bios = _sh2Bios[cpuIndex & 1];
        if (normalized < bios.Length)
        {
            value = bios[normalized];
            return true;
        }

        // Several retail titles poll the SH-2 boot ROM's first byte after
        // cache setup. The complete BIOS is not executed by this emulator, but
        // exposing its ready marker keeps that handoff-compatible code moving.
        value = normalized == 0 ? (byte)0x80 : (byte)0x00;
        return true;
    }

    private bool TryReadSh2BootRomServiceWord(uint address, int cpuIndex, out ushort value)
    {
        if (!AdapterEnabled || !_sh2ResetReleased)
        {
            value = 0;
            return false;
        }

        uint normalized = address & 0x1FFF_FFFFu;
        if (normalized >= Sh2BootRomMappedBytes)
        {
            value = 0;
            return false;
        }

        byte[] bios = _sh2Bios[cpuIndex & 1];
        if (normalized + 1 < bios.Length)
        {
            value = (ushort)((bios[normalized] << 8) | bios[normalized + 1]);
            return true;
        }

        value = normalized switch
        {
            0x0000 => 0x000B, // RTS
            0x0002 => 0x0009, // NOP
            _ => 0x0000,
        };
        return true;
    }

    private bool TryPeekSh2CacheByte(uint cacheAddress, int cpuIndex, out byte value)
    {
        int index = cpuIndex & 1;
        int entry = (int)((cacheAddress >> 4) & (Sh2CacheEntriesPerWay - 1));
        uint tag = (cacheAddress >> 10) & 0x7FFFF;
        uint[] tags = _sh2CacheTags[index];
        for (int way = Sh2CacheWays - 1; way >= 0; way--)
        {
            int lineIndex = (way * Sh2CacheEntriesPerWay) + entry;
            if ((tags[lineIndex] & Sh2CacheInvalidTag) == 0 && (tags[lineIndex] & 0x7FFFF) == tag)
            {
                value = _sh2CacheDataArrays[index][(lineIndex * Sh2CacheLineBytes) + (int)(cacheAddress & 0x0F)];
                return true;
            }
        }

        value = 0;
        return false;
    }

    private bool TryPeekSh2CacheLong(uint cacheAddress, int cpuIndex, out uint value)
    {
        if (TryPeekSh2CacheByte(cacheAddress & ~3u, cpuIndex, out byte b0) &&
            TryPeekSh2CacheByte((cacheAddress & ~3u) + 1, cpuIndex, out byte b1) &&
            TryPeekSh2CacheByte((cacheAddress & ~3u) + 2, cpuIndex, out byte b2) &&
            TryPeekSh2CacheByte((cacheAddress & ~3u) + 3, cpuIndex, out byte b3))
        {
            value = (uint)((b0 << 24) | (b1 << 16) | (b2 << 8) | b3);
            return true;
        }

        value = 0;
        return false;
    }

    private bool TryReadSh2CacheLongNoAllocate(uint cacheAddress, int cpuIndex, out uint value)
    {
        uint aligned = cacheAddress & ~3u;
        int index = cpuIndex & 1;
        int entry = (int)((aligned >> 4) & (Sh2CacheEntriesPerWay - 1));
        uint tag = (aligned >> 10) & 0x7FFFF;
        if (!TryFindSh2CacheLine(index, entry, tag, out int lineIndex))
        {
            value = 0;
            return false;
        }

        int offset = (lineIndex * Sh2CacheLineBytes) + (int)(aligned & 0x0F);
        value = (uint)((_sh2CacheDataArrays[index][offset] << 24) |
            (_sh2CacheDataArrays[index][offset + 1] << 16) |
            (_sh2CacheDataArrays[index][offset + 2] << 8) |
            _sh2CacheDataArrays[index][offset + 3]);
        return true;
    }

    internal void WriteSh2Byte(uint address, byte value, int cpuIndex)
    {
        if (IsSh2DivisionUnitRegisterAddress(address))
        {
            WriteSh2DivisionUnitByte(address, value, cpuIndex);
            TraceSh2MemoryAccess(cpuIndex, "WI8", address, value);
            return;
        }

        if (TrySignalSh2InputCapture(address, value, cpuIndex))
        {
            return;
        }

        if (TryMapSh2PrivateWorkRamAddress(address, cpuIndex, out int privateRamOffset))
        {
            _sh2PrivateWorkRam[cpuIndex & 1][privateRamOffset] = value;
            TraceSh2MemoryAccess(cpuIndex, "W8", address, value);
            return;
        }

        if (TryMapSh2CacheDataArrayAddress(address, cpuIndex, out int cacheDataOffset))
        {
            _sh2CacheDataArrays[cpuIndex][cacheDataOffset] = value;
            _sh2CacheDataValid[cpuIndex][cacheDataOffset] = 1;
            TraceSh2MemoryAccess(cpuIndex, "W8", address, value);
            return;
        }

        if (TryMapSh2CachedSdramAddress(address, out int cachedSdramOffset))
        {
            AddSh2WaitCycles(cpuIndex, Sh2SdramWriteWaitCycles);
            _sdram[cachedSdramOffset] = value;
            UpdateSh2SdramCacheByte(cachedSdramOffset, value, cpuIndex);
            TraceSh2MemoryAccess(cpuIndex, "WC8", address, value);
            return;
        }

        if (TryMapSh2SdramAddress(address, out int sdramOffset))
        {
            AddSh2WaitCycles(cpuIndex, Sh2SdramWriteWaitCycles);
            _sdram[sdramOffset] = value;
            TraceSh2MemoryAccess(cpuIndex, "W8", address, value);
            return;
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2SystemRegisterStart and < ThirtyTwoXHardwareProfile.Sh2SystemRegisterStart + 0x80)
        {
            AddSh2WaitCycles(cpuIndex, Sh2SystemRegisterWaitCycles);
            WriteSh2SystemRegisterByte((ushort)(address - ThirtyTwoXHardwareProfile.Sh2SystemRegisterStart), value, cpuIndex);
            return;
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2SystemRegisterCachedStart and < ThirtyTwoXHardwareProfile.Sh2SystemRegisterCachedStart + 0x80)
        {
            AddSh2WaitCycles(cpuIndex, Sh2SystemRegisterWaitCycles);
            WriteSh2SystemRegisterByte((ushort)(address - ThirtyTwoXHardwareProfile.Sh2SystemRegisterCachedStart), value, cpuIndex);
            return;
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2VdpRegisterStart and < ThirtyTwoXHardwareProfile.Sh2VdpRegisterStart + 0x80)
        {
            AddSh2WaitCycles(cpuIndex, Sh2VdpRegisterWaitCycles);
            WriteVdpRegisterByte((ushort)(address - ThirtyTwoXHardwareProfile.Sh2VdpRegisterStart), value);
            return;
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2VdpRegisterCachedStart and < ThirtyTwoXHardwareProfile.Sh2VdpRegisterCachedStart + 0x80)
        {
            AddSh2WaitCycles(cpuIndex, Sh2VdpRegisterWaitCycles);
            WriteVdpRegisterByte((ushort)(address - ThirtyTwoXHardwareProfile.Sh2VdpRegisterCachedStart), value);
            return;
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2ColorPaletteStart and < ThirtyTwoXHardwareProfile.Sh2ColorPaletteStart + (ThirtyTwoXHardwareProfile.PaletteEntries * 2))
        {
            AddSh2WaitCycles(cpuIndex, Sh2PaletteWaitCycles);
            if (IsSh2PaletteAccessDenied())
            {
                TraceSh2MemoryAccess(cpuIndex, "DPAL8", address, value);
                return;
            }

            AddSh2PaletteBusyWaitIfNeeded(cpuIndex);
            WritePaletteByte((ushort)(address - ThirtyTwoXHardwareProfile.Sh2ColorPaletteStart), value, cpuIndex == 0 ? "MSH2" : "SSH2");
            TraceSh2MemoryAccess(cpuIndex, "WPAL8", address, value);
            return;
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2ColorPaletteCachedStart and < ThirtyTwoXHardwareProfile.Sh2ColorPaletteCachedStart + (ThirtyTwoXHardwareProfile.PaletteEntries * 2))
        {
            AddSh2WaitCycles(cpuIndex, Sh2PaletteWaitCycles);
            if (IsSh2PaletteAccessDenied())
            {
                TraceSh2MemoryAccess(cpuIndex, "DPAL8", address, value);
                return;
            }

            AddSh2PaletteBusyWaitIfNeeded(cpuIndex);
            WritePaletteByte((ushort)(address - ThirtyTwoXHardwareProfile.Sh2ColorPaletteCachedStart), value, cpuIndex == 0 ? "MSH2" : "SSH2");
            TraceSh2MemoryAccess(cpuIndex, "WPAL8", address, value);
            return;
        }

        if (TryMapSh2FrameBufferAddress(address, out uint frameBufferOffset, out bool overwriteFrameBuffer))
        {
            AddSh2WaitCycles(cpuIndex, Sh2FrameBufferWriteWaitCycles);
            if (IsSh2FrameBufferAccessDenied())
            {
                TraceDeniedFrameBufferAccess(cpuIndex == 0 ? "MSH2" : "SSH2", overwriteFrameBuffer ? "DENY-OW8" : "DENY-W8", frameBufferOffset, value);
                return;
            }

            AddSh2FrameBufferBusyWaitIfNeeded(cpuIndex);
            WriteFrameBufferByteCore(frameBufferOffset, value, cpuIndex == 0 ? "MSH2" : "SSH2", overwriteFrameBuffer, transparentZero: overwriteFrameBuffer, enforceAccessWindow: false);
            return;
        }

        if (TryMapSh2CachedCartridgeAddress(address, out uint cacheOffset, out uint romOffset))
        {
            if (IsSh2CacheEnabled(cpuIndex))
            {
                WriteSh2CachedCartridgeByte(cacheOffset, romOffset, value, cpuIndex);
            }

            TraceSh2MemoryAccess(cpuIndex, "WC8", address, value);
            return;
        }

        if (TryMapSh2UncachedBankedCartridgeAddress(address, out romOffset))
        {
            AddSh2WaitCycles(cpuIndex, IsSh2RomBlockedByRv() ? Sh2CartridgeRvBlockedWaitCycles : Sh2CartridgeByteWaitCycles);
            ClaimSh2CartridgeBus(1);
            TraceSh2MemoryAccess(cpuIndex, "W8", address, value);
            return;
        }

        if (IsSh2DmaRegisterAddress(address))
        {
            WriteSh2DmaByte(address, value, cpuIndex);
            return;
        }

        if (TryMapSh2CachePurgeAddress(address))
        {
            PurgeSh2CacheLine(cpuIndex, address);
            TraceSh2MemoryAccess(cpuIndex, "WP8", address, value);
            return;
        }

        if (IsSh2PeripheralRegisterAddress(address))
        {
            WriteSh2PeripheralByte(address, value, cpuIndex);
            return;
        }

        if (IsSh2InternalRegisterAddress(address))
        {
            TraceSh2MemoryAccess(cpuIndex, "WI8", address, value);
            return;
        }

        TraceSh2MemoryAccess(cpuIndex, "WU8", address, value);
    }

    internal void WriteSh2Word(uint address, ushort value, int cpuIndex)
    {
        string source = cpuIndex == 0 ? "MSH2" : "SSH2";
        if (TrySignalSh2InputCapture(address, value, cpuIndex))
        {
            return;
        }

        if (IsSh2DivisionUnitRegisterAddress(address))
        {
            WriteSh2DivisionUnitWord(address, value, cpuIndex);
            TraceSh2MemoryAccess(cpuIndex, "WI16", address, value);
            return;
        }

        if (IsSh2PeripheralRegisterAddress(address))
        {
            WriteSh2PeripheralWord(address, value, cpuIndex);
            return;
        }

        if (TryMapSh2OverflowSdramMirrorAddress(address, out int overflowSdramOffset))
        {
            _sdram[overflowSdramOffset] = (byte)(value >> 8);
            int lowOffset = (overflowSdramOffset + 1) & (ThirtyTwoXHardwareProfile.SdramBytes - 1);
            _sdram[lowOffset] = (byte)value;
            TraceSdramWordWrite(cpuIndex, "W16", address, overflowSdramOffset, value);
            TraceSh2MemoryAccess(cpuIndex, "W16", address, value);
            return;
        }

        if (TryMapSh2CachePurgeAddress(address))
        {
            PurgeSh2CacheLine(cpuIndex, address);
            TraceSh2MemoryAccess(cpuIndex, "WP16", address, value);
            return;
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2SystemRegisterStart and < ThirtyTwoXHardwareProfile.Sh2SystemRegisterStart + 0x80)
        {
            AddSh2WaitCycles(cpuIndex, Sh2SystemRegisterWaitCycles);
            WriteSh2SystemRegisterWord((ushort)(address - ThirtyTwoXHardwareProfile.Sh2SystemRegisterStart), value, cpuIndex);
            return;
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2SystemRegisterCachedStart and < ThirtyTwoXHardwareProfile.Sh2SystemRegisterCachedStart + 0x80)
        {
            AddSh2WaitCycles(cpuIndex, Sh2SystemRegisterWaitCycles);
            WriteSh2SystemRegisterWord((ushort)(address - ThirtyTwoXHardwareProfile.Sh2SystemRegisterCachedStart), value, cpuIndex);
            return;
        }

        if (TryMapSh2CachedSdramAddress(address, out int cachedSdramOffset))
        {
            AddSh2WaitCycles(cpuIndex, Sh2SdramWriteWaitCycles);
            WriteBigEndianWord(_sdram, cachedSdramOffset, value);
            UpdateSh2SdramCacheByte(cachedSdramOffset, (byte)(value >> 8), cpuIndex);
            UpdateSh2SdramCacheByte((cachedSdramOffset + 1) & (ThirtyTwoXHardwareProfile.SdramBytes - 1), (byte)value, cpuIndex);
            TraceSdramWordWrite(cpuIndex, "WC16", address, cachedSdramOffset, value);
            TraceSh2MemoryAccess(cpuIndex, "WC16", address, value);
            return;
        }

        if (TryMapSh2SdramAddress(address, out int sdramOffset))
        {
            AddSh2WaitCycles(cpuIndex, Sh2SdramWriteWaitCycles);
            WriteBigEndianWord(_sdram, sdramOffset, value);
            TraceSdramWordWrite(cpuIndex, "W16", address, sdramOffset, value);
            TraceSh2MemoryAccess(cpuIndex, "W16", address, value);
            return;
        }

        if (TryMapSh2FrameBufferAddress(address, out uint frameBufferOffset, out bool overwriteFrameBuffer))
        {
            AddSh2WaitCycles(cpuIndex, Sh2FrameBufferWriteWaitCycles);
            if (IsSh2FrameBufferAccessDenied())
            {
                TraceDeniedFrameBufferAccess(source, overwriteFrameBuffer ? "DENY-OW16" : "DENY-W16", frameBufferOffset, value);
                return;
            }

            AddSh2FrameBufferBusyWaitIfNeeded(cpuIndex);
            if (overwriteFrameBuffer)
            {
                WriteOverwriteImageWordCore(frameBufferOffset, value, source, enforceAccessWindow: false);
            }
            else
            {
                WriteFrameBufferWordCore(frameBufferOffset, value, source, enforceAccessWindow: false);
            }

            return;
        }

        if (TryMapSh2CachedCartridgeAddress(address, out uint cacheOffset, out uint romOffset))
        {
            if (IsSh2CacheEnabled(cpuIndex))
            {
                WriteSh2CachedCartridgeByte(cacheOffset, romOffset, (byte)(value >> 8), cpuIndex);
                WriteSh2CachedCartridgeByte(cacheOffset + 1, romOffset + 1, (byte)value, cpuIndex);
            }

            TraceSh2MemoryAccess(cpuIndex, "WC16", address, value);
            return;
        }

        if (TryMapSh2UncachedBankedCartridgeAddress(address, out _))
        {
            AddSh2WaitCycles(cpuIndex, IsSh2RomBlockedByRv() ? Sh2CartridgeRvBlockedWaitCycles : Sh2CartridgeByteWaitCycles * 2);
            ClaimSh2CartridgeBus(2);
            TraceSh2MemoryAccess(cpuIndex, "W16", address, value);
            return;
        }

        WriteSh2Byte(address, (byte)(value >> 8), cpuIndex);
        WriteSh2Byte(address + 1, (byte)value, cpuIndex);
    }

    internal void WriteSh2Long(uint address, uint value, int cpuIndex)
    {
        if (TryAcknowledgeBootRomGOkLong(address, value, cpuIndex))
        {
            return;
        }

        if (TrySignalSh2InputCapture(address, value, cpuIndex))
        {
            return;
        }

        if (IsSh2DivisionUnitRegisterAddress(address))
        {
            WriteSh2DivisionUnitLong(address, value, cpuIndex);
            TraceSh2MemoryAccess(cpuIndex, "WI32", address, value);
            return;
        }

        if (TryMapSh2CacheAddressArrayAddress(address, out int cacheAddressOffset))
        {
            WriteSh2CacheAddressArrayLong(address, cacheAddressOffset, value, cpuIndex);
            TraceSh2MemoryAccess(cpuIndex, "WA32", address, value);
            return;
        }

        if (TryMapSh2OverflowSdramMirrorAddress(address, out int overflowSdramOffset))
        {
            WriteSh2Word(address, (ushort)(value >> 16), cpuIndex);
            WriteSh2Word(address + 2, (ushort)value, cpuIndex);
            return;
        }

        if (TryMapSh2CachePurgeAddress(address))
        {
            PurgeSh2CacheLine(cpuIndex, address);
            TraceSh2MemoryAccess(cpuIndex, "WP32", address, value);
            return;
        }

        WriteSh2Word(address, (ushort)(value >> 16), cpuIndex);
        WriteSh2Word(address + 2, (ushort)value, cpuIndex);
    }

    private byte ReadSh2SystemRegisterByte(ushort offset, int cpuIndex)
    {
        ushort aligned = (ushort)(offset & ~1);
        if (aligned == ThirtyTwoXHardwareProfile.AdapterControlOffset)
        {
            ushort word = BuildSh2InterruptMask(cpuIndex);
            return (offset & 1) == 0 ? (byte)(word >> 8) : (byte)word;
        }

        if (IsPostStartSignatureHiddenFromSh2(offset, 1))
        {
            TraceSystemRegisterAccess(cpuIndex == 0 ? "MSH2" : "SSH2", "R8", offset, 0);
            SyncOtherSh2ForCommunicationAccess(offset, cpuIndex);
            return 0;
        }

        if (IsBootRomSixtyEightUpHiddenFromSh2(offset, 1))
        {
            TraceSystemRegisterAccess(cpuIndex == 0 ? "MSH2" : "SSH2", "R8", offset, 0);
            SyncOtherSh2ForCommunicationAccess(offset, cpuIndex);
            return 0;
        }

        byte registerValue = ReadSystemRegisterByteCore(offset, sh2View: true);
        TraceSystemRegisterAccess(cpuIndex == 0 ? "MSH2" : "SSH2", "R8", offset, registerValue);
        ApplyDeferredSh2CommunicationClearAfterRead(offset, registerValue);
        SyncOtherSh2ForCommunicationAccess(offset, cpuIndex);
        return registerValue;
    }

    private byte PeekSh2SystemRegisterByte(ushort offset, int cpuIndex)
    {
        ushort aligned = (ushort)(offset & ~1);
        if (aligned == ThirtyTwoXHardwareProfile.AdapterControlOffset)
        {
            ushort word = BuildSh2InterruptMask(cpuIndex);
            return (offset & 1) == 0 ? (byte)(word >> 8) : (byte)word;
        }

        if (IsPostStartSignatureHiddenFromSh2(offset, 1))
        {
            return 0;
        }

        if (IsBootRomSixtyEightUpHiddenFromSh2(offset, 1))
        {
            return 0;
        }

        return ReadSystemRegisterByteCore(offset, sh2View: true);
    }

    private ushort ReadSh2SystemRegisterWord(ushort offset, int cpuIndex)
    {
        ushort aligned = (ushort)(offset & ~1);
        ushort value = aligned == ThirtyTwoXHardwareProfile.AdapterControlOffset
            ? BuildSh2InterruptMask(cpuIndex)
            : IsPostStartSignatureHiddenFromSh2(aligned, 2)
                ? (ushort)0
            : IsBootRomSixtyEightUpHiddenFromSh2(aligned, 2)
                ? (ushort)0
            : ReadSystemRegisterWordCore(aligned, popDreqFifo: true, sh2View: true);
        TraceSystemRegisterAccess(cpuIndex == 0 ? "MSH2" : "SSH2", "R16", aligned, value);
        ApplyDeferredSh2CommunicationClearAfterRead(aligned, (byte)(value >> 8));
        ApplyDeferredSh2CommunicationClearAfterRead((ushort)(aligned + 1), (byte)value);
        SyncOtherSh2ForCommunicationAccess(aligned, cpuIndex);
        return value;
    }

    private void WriteSh2SystemRegisterByte(ushort offset, byte value, int cpuIndex)
    {
        int index = offset & (SystemRegisterBytes - 1);
        string source = cpuIndex == 0 ? "MSH2" : "SSH2";
        SyncOtherSh2ForCommunicationAccess((ushort)index, cpuIndex);
        if (ShouldProtectPostStartSignatureFromSh2((ushort)index, 1, value))
        {
            TraceSystemRegisterAccess(source, "W8", (ushort)index, value);
            return;
        }

        if ((index & ~1) == ThirtyTwoXHardwareProfile.AdapterControlOffset)
        {
            ushort mask = cpuIndex == 0 ? _masterInterruptMask : _slaveInterruptMask;
            if (_vdpAccessGrantedToSh2)
            {
                mask |= AdapterControlVdpAccessSh2;
            }

            mask = (index & 1) == 0
                ? (ushort)((mask & 0x00FF) | (value << 8))
                : (ushort)((mask & 0xFF00) | value);
            WriteSh2InterruptMask(cpuIndex, mask);
            SystemRegisterWriteObserver?.Invoke(new SystemRegisterWriteTrace(source, (ushort)index, value));
            TraceSystemRegisterAccess(source, "W8", (ushort)index, value);
            RequestPendingInterrupts();
            return;
        }

        if ((index & ~1) is ThirtyTwoXHardwareProfile.VResInterruptClearOffset or
            ThirtyTwoXHardwareProfile.VInterruptClearOffset or
            ThirtyTwoXHardwareProfile.HInterruptClearOffset or
            ThirtyTwoXHardwareProfile.CommandInterruptClearOffset or
            ThirtyTwoXHardwareProfile.PwmInterruptClearOffset)
        {
            SystemRegisterWriteObserver?.Invoke(new SystemRegisterWriteTrace(source, (ushort)index, value));
            TraceSystemRegisterAccess(source, "W8", (ushort)index, value);
            ClearSh2Interrupt((ushort)(index & ~1), cpuIndex);
            return;
        }

        byte previousValue = _systemRegisters[index];
        _sideEffectPreviousSystemRegisterWord = ReadBigEndianWord(_systemRegisters, (ushort)(index & ~1));
        bool protectedHostByte = TryProtectM68kPendingHostByteFromSh2Clear((ushort)index, value);
        if (!protectedHostByte)
        {
            _systemRegisters[index] = value;
        }

        MarkM68kCommunicationStaleByte((ushort)index, previousValue, value);
        SystemRegisterWriteObserver?.Invoke(new SystemRegisterWriteTrace(source, (ushort)index, value));
        TraceSystemRegisterAccess(source, "W8", (ushort)index, value);
        TrySeedDualSh2WorkerSemaphore((ushort)index, previousValue, value, cpuIndex);
        CancelBootRomReadbackOnSh2DataWrite((ushort)(index & ~1), value);
        ApplySystemRegisterSideEffects((ushort)(index & ~1), allowAdapterControl: false);
        TryRunDreqDma();
    }

    private void WriteSh2SystemRegisterWord(ushort offset, ushort value, int cpuIndex)
    {
        int index = offset & (SystemRegisterBytes - 1);
        ushort aligned = (ushort)(index & ~1);
        string source = cpuIndex == 0 ? "MSH2" : "SSH2";
        SyncOtherSh2ForCommunicationAccess(aligned, cpuIndex);
        if (ShouldProtectPostStartSignatureFromSh2(aligned, 2, value))
        {
            TraceSystemRegisterAccess(source, "W16", aligned, value);
            return;
        }

        if (aligned == ThirtyTwoXHardwareProfile.AdapterControlOffset)
        {
            WriteSh2InterruptMask(cpuIndex, value);
            TraceSystemRegisterAccess(source, "W16", aligned, value);
            RequestPendingInterrupts();
            return;
        }

        if (aligned is ThirtyTwoXHardwareProfile.VResInterruptClearOffset or
            ThirtyTwoXHardwareProfile.VInterruptClearOffset or
            ThirtyTwoXHardwareProfile.HInterruptClearOffset or
            ThirtyTwoXHardwareProfile.CommandInterruptClearOffset or
            ThirtyTwoXHardwareProfile.PwmInterruptClearOffset)
        {
            TraceSystemRegisterAccess(source, "W16", aligned, value);
            ClearSh2Interrupt(aligned, cpuIndex);
            return;
        }

        int highIndex = aligned & (SystemRegisterBytes - 1);
        int lowIndex = (aligned + 1) & (SystemRegisterBytes - 1);
        byte previousHigh = _systemRegisters[highIndex];
        byte previousLow = _systemRegisters[lowIndex];
        ushort previousWord = (ushort)((previousHigh << 8) | previousLow);
        _sideEffectPreviousSystemRegisterWord = previousWord;
        byte high = (byte)(value >> 8);
        byte low = (byte)value;
        if (!TryProtectM68kPendingHostByteFromSh2Clear(aligned, high))
        {
            _systemRegisters[highIndex] = high;
        }

        if (!TryProtectM68kPendingHostByteFromSh2Clear((ushort)(aligned + 1), low))
        {
            _systemRegisters[lowIndex] = low;
        }

        MarkM68kCommunicationStaleWord(aligned, previousWord, value);
        MarkM68kCommunicationStaleByte(aligned, previousHigh, high);
        MarkM68kCommunicationStaleByte((ushort)(aligned + 1), previousLow, low);
        TryAdvanceM68kVdpControlMailboxPhase(aligned, value);
        SystemRegisterWriteObserver?.Invoke(new SystemRegisterWriteTrace(source, aligned, high));
        SystemRegisterWriteObserver?.Invoke(new SystemRegisterWriteTrace(source, (ushort)(aligned + 1), low));
        TraceSystemRegisterAccess(source, "W16", aligned, value);
        CancelBootRomReadbackOnSh2DataWrite(aligned, (byte)(value >> 8));
        CancelBootRomReadbackOnSh2DataWrite((ushort)(aligned + 1), (byte)value);
        ApplySystemRegisterSideEffects(aligned, allowAdapterControl: false);
        TryCompleteBootRomPeerReadyProbe(aligned, previousWord, value, cpuIndex);
        TryRunDreqDma();
    }

    private void SyncOtherSh2ForCommunicationAccess(ushort offset, int cpuIndex)
    {
        if (_sh2CommunicationSyncActive || !TryGetCommunicationByteIndex(offset, out _))
        {
            return;
        }

        Sh2Cpu active = cpuIndex == 0 ? MasterSh2 : SlaveSh2;
        Sh2Cpu peer = cpuIndex == 0 ? SlaveSh2 : MasterSh2;
        if (peer.Halted && !peer.HasAcceptablePendingInterrupt)
        {
            return;
        }

        _sh2CommunicationSyncActive = true;
        try
        {
            int steps = 0;
            while (peer.Cycles < active.Cycles && steps < Sh2CommunicationSyncStepLimit)
            {
                RequestPendingInterrupts();
                if (peer.Halted && !peer.HasAcceptablePendingInterrupt)
                {
                    break;
                }

                StepSh2Cpu(1 - cpuIndex, Sh2CommunicationSyncCycleBudget);
                steps++;
            }
        }
        finally
        {
            _sh2CommunicationSyncActive = false;
        }
    }

    private void ApplySystemRegisterSideEffects(ushort offset, bool allowAdapterControl)
    {
        ushort value = offset == ThirtyTwoXHardwareProfile.DreqFifoOffset || IsPwmPulseWidthOffset(offset)
            ? ReadBigEndianWord(_systemRegisters, offset)
            : ReadSystemRegisterWordCore(offset, popDreqFifo: false, sh2View: false);
        switch (offset)
        {
            case ThirtyTwoXHardwareProfile.AdapterControlOffset:
                if (allowAdapterControl)
                {
                    ApplyAdapterControl(value);
                }

                break;
            case ThirtyTwoXHardwareProfile.BankSetOffset:
                if (allowAdapterControl)
                {
                    ushort bank = (ushort)(value & 0x0003);
                    if (bank != _m68kCartridgeBank)
                    {
                        ClearSh2CartridgeCache();
                    }

                    _m68kCartridgeBank = bank;
                    WriteBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.BankSetOffset, bank);
                }
                else
                {
                    _horizontalInterruptPeriod = (byte)value;
                    _horizontalInterruptCounter = 0;
                }

                break;
            case ThirtyTwoXHardwareProfile.DreqControlOffset:
                WriteDreqControl(value, _sideEffectPreviousSystemRegisterWord);
                break;
            case ThirtyTwoXHardwareProfile.DreqLengthOffset:
                WriteBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.DreqLengthOffset, (ushort)(value & 0xFFFC));
                break;
            case ThirtyTwoXHardwareProfile.DreqFifoOffset:
                PushDreqFifo(value);
                break;
            case ThirtyTwoXHardwareProfile.PwmControlOffset:
            case ThirtyTwoXHardwareProfile.PwmCycleOffset:
                ResetPwmTimerCounters();
                break;
            case ThirtyTwoXHardwareProfile.PwmLeftPulseWidthOffset:
                PushPwm(_pwmLeft, _pwmLeftHardwareFifo, offset, value);
                break;
            case ThirtyTwoXHardwareProfile.PwmRightPulseWidthOffset:
                PushPwm(_pwmRight, _pwmRightHardwareFifo, offset, value);
                break;
            case ThirtyTwoXHardwareProfile.PwmMonoPulseWidthOffset:
                PushMonoPwm(value);
                break;
            case ThirtyTwoXHardwareProfile.InterruptControlOffset:
                if (allowAdapterControl)
                {
                    // The 68000 side asserts command interrupt latches. They
                    // remain active until the addressed SH-2 writes its clear
                    // register; a later host zero write must not retract a
                    // request that software has not acknowledged yet.
                    if ((value & 0x0001) != 0)
                    {
                        _masterCommandInterruptPending = true;
                    }

                    if ((value & 0x0002) != 0)
                    {
                        _slaveCommandInterruptPending = true;
                    }

                    byte active = 0;
                    if (_masterCommandInterruptPending)
                    {
                        active |= 0x01;
                    }

                    if (_slaveCommandInterruptPending)
                    {
                        active |= 0x02;
                    }

                    _systemRegisters[ThirtyTwoXHardwareProfile.InterruptControlOffset] = 0;
                    _systemRegisters[ThirtyTwoXHardwareProfile.InterruptControlOffset + 1] = active;
                    RequestPendingInterrupts();
                }

                break;
        }
    }

    private void ApplyAdapterControl(ushort value)
    {
        bool wasHeld = Sh2HeldInReset;
        _adapterEnabled = (value & AdapterControlAdapterEnable) != 0;
        _sh2ResetEnabled = (value & AdapterControlSh2ResetEnable) != 0;
        _sh2ResetReleased = (value & AdapterControlSh2ResetRelease) != 0;
        _vdpAccessGrantedToSh2 = (value & AdapterControlVdpAccessSh2) != 0;

        if (Sh2HeldInReset)
        {
            ResetSh2ForStartup();
        }
        else if (wasHeld)
        {
            ResetSh2ForStartup();
            _bootRomHandshakePending = _useRealSh2BiosBoot ? false : SeedBootRomCommunicationHandshake();
            _bootRomSignatureRead = false;
            _bootRomSignatureReadbackActive = false;
            _bootRomLaunchPending = false;
            _bootRomPostStartSignaturePending = false;
            _bootRomPostStartSignatureHiddenFromSh2 = false;
            _bootRomPostStartSignatureReadMask = 0;
            _bootRomPostStartHostClearProtectMask = 0;
            _bootRomSixtyEightUpPending = false;
        }
    }

    private bool SeedBootRomCommunicationHandshake()
    {
        bool hasHeaderChecksum = HasCartridgeHeaderChecksum();
        ushort comm = ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        bool communicationPortsClear = true;
        for (int i = 0; i < 8; i++)
        {
            if (_systemRegisters[comm + i] != 0)
            {
                communicationPortsClear = false;
                break;
            }
        }

        if (!communicationPortsClear)
        {
            return false;
        }

        for (int i = 0; i < BootRomCommunicationSignature.Length; i++)
        {
            _systemRegisters[comm + i] = BootRomCommunicationSignature[i];
        }

        if (hasHeaderChecksum &&
            _systemRegisters[ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8] == 0 &&
            _systemRegisters[ThirtyTwoXHardwareProfile.CommunicationPortOffset + 9] == 0)
        {
            ushort checksum = (ushort)((_cartridgeRom.Span[0x18E] << 8) | _cartridgeRom.Span[0x18F]);
            WriteBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8, checksum);
            _bootRomChecksumPublished = true;
        }

        return true;
    }

    private bool TryReadBootRomCommunicationSignatureByte(ushort offset, out byte value)
    {
        int relative = (offset & (SystemRegisterBytes - 1)) - ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if (TryRetireObservedPostStartSignatureOnHostRead(relative, 1))
        {
            value = 0;
            return false;
        }

        if ((_bootRomHandshakePending || _bootRomPostStartSignaturePending) &&
            relative is >= 0 and < 8 &&
            IsBootSignatureVisible(relative, 1))
        {
            _bootRomSignatureRead |= relative >= 4;
            if (_bootRomPostStartSignaturePending && HasPostStartReadyToken(relative, 1))
            {
                value = _systemRegisters[ThirtyTwoXHardwareProfile.CommunicationPortOffset + relative];
                return true;
            }

            value = _bootRomPostStartSignaturePending
                ? BootRomCommunicationSignature[relative]
                : _systemRegisters[ThirtyTwoXHardwareProfile.CommunicationPortOffset + relative];
            MarkPostStartBootSignatureRead(relative, 1);
            return true;
        }

        value = 0;
        return false;
    }

    private bool TryReadBootRomChecksumByte(ushort offset, bool includePostStart, out byte value)
    {
        int relative = (offset & (SystemRegisterBytes - 1)) - ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if ((_bootRomHandshakePending ||
                _bootRomSignatureReadbackActive ||
                (includePostStart && _bootRomPostStartSignaturePending)) &&
            !_bootRomChecksumHostCleared &&
            HasCartridgeHeaderChecksum() &&
            relative is 8 or 9)
        {
            value = _cartridgeRom.Span[0x18E + (relative - 8)];
            return true;
        }

        value = 0;
        return false;
    }

    private bool TryReadBootRomChecksumWord(ushort offset, bool includePostStart, out ushort value)
    {
        int relative = (offset & (SystemRegisterBytes - 1)) - ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if ((_bootRomHandshakePending ||
                _bootRomSignatureReadbackActive ||
                (includePostStart && _bootRomPostStartSignaturePending)) &&
            !_bootRomChecksumHostCleared &&
            HasCartridgeHeaderChecksum() &&
            relative == 8)
        {
            value = (ushort)((_cartridgeRom.Span[0x18E] << 8) | _cartridgeRom.Span[0x18F]);
            return true;
        }

        value = 0;
        return false;
    }

    private bool TryReadBootRomCommunicationSignatureWord(ushort offset, out ushort value)
    {
        int relative = (offset & (SystemRegisterBytes - 1)) - ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if (TryRetireObservedPostStartSignatureOnHostRead(relative, 2))
        {
            value = 0;
            return false;
        }

        if ((_bootRomHandshakePending || _bootRomPostStartSignaturePending) &&
            relative is >= 0 and < 7 &&
            IsBootSignatureVisible(relative, 2))
        {
            _bootRomSignatureRead |= relative >= 4;
            if (_bootRomPostStartSignaturePending && HasPostStartReadyToken(relative, 2))
            {
                value = ReadBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.CommunicationPortOffset + relative);
                return true;
            }

            value = _bootRomPostStartSignaturePending
                ? (ushort)((BootRomCommunicationSignature[relative] << 8) | BootRomCommunicationSignature[relative + 1])
                : ReadBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.CommunicationPortOffset + relative);
            MarkPostStartBootSignatureRead(relative, 2);
            return true;
        }

        value = 0;
        return false;
    }

    private bool IsBootSignatureVisible(int relative, int bytes)
    {
        if (_bootRomPostStartSignaturePending)
        {
            return relative >= 0 && relative + bytes <= BootRomCommunicationSignature.Length;
        }

        for (int i = 0; i < bytes; i++)
        {
            int index = relative + i;
            if (index < 0 || index >= BootRomCommunicationSignature.Length ||
                _systemRegisters[ThirtyTwoXHardwareProfile.CommunicationPortOffset + index] != BootRomCommunicationSignature[index])
            {
                return false;
            }
        }

        return true;
    }

    private bool HasPostStartReadyToken(int relative, int bytes)
    {
        if (relative < 0 || relative + bytes > BootRomCommunicationSignature.Length)
        {
            return false;
        }

        return RangeOverlaps(relative, bytes, 0, 4) && MatchesCommunicationToken(0, (byte)'M', (byte)'R', (byte)'D', (byte)'Y') ||
            RangeOverlaps(relative, bytes, 4, 4) && MatchesCommunicationToken(4, (byte)'S', (byte)'R', (byte)'D', (byte)'Y');
    }

    private bool MatchesCommunicationToken(int relative, byte a, byte b, byte c, byte d)
    {
        int comm = ThirtyTwoXHardwareProfile.CommunicationPortOffset + relative;
        return _systemRegisters[comm + 0] == a &&
            _systemRegisters[comm + 1] == b &&
            _systemRegisters[comm + 2] == c &&
            _systemRegisters[comm + 3] == d;
    }

    private static bool RangeOverlaps(int start, int length, int targetStart, int targetLength)
    {
        int end = start + length;
        int targetEnd = targetStart + targetLength;
        return start < targetEnd && end > targetStart;
    }

    private void ClearBootRomCommunicationSignature()
    {
        ushort comm = ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        for (int i = 0; i < BootRomCommunicationSignature.Length; i++)
        {
            if (_systemRegisters[comm + i] == BootRomCommunicationSignature[i])
            {
                _systemRegisters[comm + i] = 0;
            }
        }
    }

    private void RetireNonLaunchPostStartSignatureBeforeSh2Run()
    {
        if (!_bootRomPostStartSignaturePending ||
            _bootRomLaunchPending ||
            _bootRomHandshakePending ||
            !_userHeader.IsValid ||
            _userHeader.RequiresHostLaunchCommand)
        {
            return;
        }

        _bootRomSignatureRead = false;
        _bootRomSignatureReadbackActive = false;
        _bootRomPostStartSignatureHiddenFromSh2 = !ShouldExposePostStartSignatureToSh2();
        PublishBootRomChecksumAfterHostClear((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8));
    }

    private bool ShouldExposePostStartSignatureToSh2()
    {
        return _userHeader.MasterStart == 0x0600_0280u &&
            _userHeader.SlaveStart == 0x0600_0288u;
    }

    private void MarkPostStartBootSignatureRead(int relative, int bytes)
    {
        if (!_bootRomPostStartSignaturePending)
        {
            return;
        }

        for (int i = 0; i < bytes; i++)
        {
            int index = relative + i;
            if (index is >= 0 and < 8)
            {
                _bootRomPostStartSignatureReadMask |= (byte)(1 << index);
            }
        }

        if (_bootRomPostStartSignatureReadMask != 0xFF)
        {
            return;
        }

        _bootRomHandshakePending = false;
        _bootRomSignatureRead = false;
        _bootRomSignatureReadbackActive = false;
        _bootRomLaunchPending = _userHeader.RequiresHostLaunchCommand;
        PublishBootRomChecksumAfterHostClear((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8));
    }

    private bool TryRetireObservedPostStartSignatureOnHostRead(int relative, int bytes)
    {
        if (!_bootRomPostStartSignaturePending ||
            _bootRomPostStartSignatureReadMask != 0xFF ||
            relative < 0 ||
            relative + bytes > BootRomCommunicationSignature.Length ||
            relative != 0 ||
            HasPostStartReadyToken(relative, bytes))
        {
            return false;
        }

        RetirePostStartSignatureAfterReadyTokenHostClear();
        return true;
    }

    private void RetireObservedPostStartSignatureOnHostWrite(ushort offset)
    {
        if (!_bootRomPostStartSignaturePending ||
            _bootRomPostStartSignatureReadMask != 0xFF)
        {
            return;
        }

        int relative = (offset & (SystemRegisterBytes - 1)) - ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if (relative < 0 || relative >= BootRomCommunicationSignature.Length)
        {
            return;
        }

        _bootRomSignatureRead = false;
        _bootRomSignatureReadbackActive = false;
        _bootRomPostStartSignaturePending = false;
        _bootRomPostStartSignatureHiddenFromSh2 = false;
        _bootRomPostStartSignatureReadMask = 0;
        if (!_bootRomLaunchPending)
        {
            _bootRomPostStartHostClearProtectMask = 0xFF;
            ClearBootRomCommunicationSignature();
        }

        PublishBootRomChecksumAfterHostClear((ushort)(ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8));
    }

    private bool ConsumePostStartHostClearProtection(ushort offset, byte value)
    {
        if (value != 0 || _bootRomPostStartHostClearProtectMask == 0)
        {
            return false;
        }

        int relative = (offset & (SystemRegisterBytes - 1)) - ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if (relative is < 0 or >= 8)
        {
            return false;
        }

        byte bit = (byte)(1 << relative);
        if ((_bootRomPostStartHostClearProtectMask & bit) == 0)
        {
            return false;
        }

        if (HasPostStartReadyToken(relative, 1))
        {
            _bootRomPostStartHostClearProtectMask &= (byte)~bit;
            RetirePostStartSignatureAfterReadyTokenHostClear();
            return false;
        }

        if (_systemRegisters[ThirtyTwoXHardwareProfile.CommunicationPortOffset + relative] == 0xFF)
        {
            _bootRomPostStartHostClearProtectMask &= (byte)~bit;
            return false;
        }

        _bootRomPostStartHostClearProtectMask &= (byte)~bit;
        return true;
    }

    private void RetirePostStartSignatureAfterReadyTokenHostClear()
    {
        _bootRomHandshakePending = false;
        _bootRomSignatureRead = false;
        _bootRomSignatureReadbackActive = false;
        _bootRomPostStartSignaturePending = false;
        _bootRomPostStartSignatureHiddenFromSh2 = false;
        _bootRomPostStartSignatureReadMask = 0;
        _bootRomPostStartHostClearProtectMask = 0;
        ClearBootRomCommunicationSignature();
        ClearCommunicationStaleRange(0, 8);
    }

    private void RetirePostStartSignatureOnReadyTokenHostClear(ushort offset, int bytes, uint value)
    {
        if (value != 0)
        {
            return;
        }

        int relative = (offset & (SystemRegisterBytes - 1)) - ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if (HasPostStartReadyToken(relative, bytes))
        {
            RetirePostStartSignatureAfterReadyTokenHostClear();
        }
    }

    private bool IsPostStartSignatureHiddenFromSh2(ushort offset, int bytes)
    {
        if (!_bootRomPostStartSignatureHiddenFromSh2)
        {
            return false;
        }

        int relative = (offset & (SystemRegisterBytes - 1)) - ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if (relative < 0 || relative + bytes > BootRomCommunicationSignature.Length)
        {
            return false;
        }

        for (int i = 0; i < bytes; i++)
        {
            int index = relative + i;
            if (_systemRegisters[ThirtyTwoXHardwareProfile.CommunicationPortOffset + index] != BootRomCommunicationSignature[index])
            {
                return false;
            }
        }

        return true;
    }

    private bool ShouldProtectPostStartSignatureFromSh2(ushort offset, int bytes, uint value)
    {
        // The post-start signature is a 68000-side read overlay. SH-2 startup
        // code can legally begin publishing command/mailbox values beneath it
        // before the host has fully retired the virtual M_OK/S_OK bytes.
        if (value != 0 ||
            !_bootRomPostStartSignaturePending ||
            _bootRomPostStartSignatureReadMask == 0xFF)
        {
            return false;
        }

        int relative = (offset & (SystemRegisterBytes - 1)) - ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if (relative < 0 || relative + bytes > BootRomCommunicationSignature.Length)
        {
            return false;
        }

        for (int i = 0; i < bytes; i++)
        {
            if (_systemRegisters[ThirtyTwoXHardwareProfile.CommunicationPortOffset + relative + i] == 0xFF)
            {
                return false;
            }
        }

        return true;
    }

    private bool HasPendingBootRomSignatureWrite(ushort offset)
    {
        if (!_bootRomHandshakePending)
        {
            return false;
        }

        ushort comm = ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if (offset < comm || offset >= comm + 8)
        {
            return false;
        }

        return true;
    }

    private void CancelBootRomHandshakeOnHostDataWrite(ushort offset, ushort value)
    {
        ushort comm = ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if ((_bootRomHandshakePending || _bootRomSignatureReadbackActive || _bootRomLaunchPending) &&
            value != 0 &&
            offset >= comm &&
            offset < comm + 8 &&
            (!HasCartridgeHeaderChecksum() || _bootRomSignatureRead))
        {
            _bootRomHandshakePending = false;
            _bootRomSignatureRead = false;
            _bootRomSignatureReadbackActive = false;
            _bootRomLaunchPending = false;
            _bootRomPostStartSignaturePending = false;
            _bootRomPostStartSignatureHiddenFromSh2 = false;
            _bootRomPostStartSignatureReadMask = 0;
            _bootRomPostStartHostClearProtectMask = 0;
            _bootRomSixtyEightUpReadyHiddenFromSh2 = false;
        }
    }

    private void ReleaseBootRomLaunchOnHostCommand()
    {
        if (!_bootRomLaunchPending)
        {
            return;
        }

        ushort comm = ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        ushort commandHigh = ReadBigEndianWord(_systemRegisters, comm);
        ushort commandLow = ReadBigEndianWord(_systemRegisters, comm + 2);
        if (commandHigh != 0x5348)
        {
            return;
        }

        if (commandLow != 0x0000 &&
            commandLow != 0x474F)
        {
            return;
        }

        _bootRomHandshakePending = false;
        _bootRomSignatureRead = false;
        _bootRomSignatureReadbackActive = false;
        _bootRomLaunchPending = false;
        _bootRomPostStartSignaturePending = false;
        _bootRomPostStartSignatureHiddenFromSh2 = false;
        _bootRomPostStartSignatureReadMask = 0;
        _bootRomPostStartHostClearProtectMask = 0;
        _bootRomSixtyEightUpReadyHiddenFromSh2 = false;
    }

    private bool HasCartridgeHeaderChecksum()
    {
        return _cartridgeRom.Length >= 0x190 && (_cartridgeRom.Span[0x18E] != 0 || _cartridgeRom.Span[0x18F] != 0);
    }

    private void PublishBootRomChecksumAfterHostClear(ushort offset)
    {
        ushort comm = ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if (_bootRomHandshakePending ||
            _bootRomPostStartSignaturePending ||
            _bootRomChecksumPublished ||
            _bootRomChecksumHostCleared ||
            !HasCartridgeHeaderChecksum() ||
            offset < comm + 8 ||
            offset >= comm + 16)
        {
            return;
        }

        if (_systemRegisters[comm + 8] != 0 ||
            _systemRegisters[comm + 9] != 0)
        {
            return;
        }

        ushort checksum = (ushort)((_cartridgeRom.Span[0x18E] << 8) | _cartridgeRom.Span[0x18F]);
        WriteBigEndianWord(_systemRegisters, comm + 8, checksum);
        _m68kCommunicationStaleWordValid[4] = false;
        _m68kCommunicationStaleValid[8] = false;
        _m68kCommunicationStaleValid[9] = false;
        _bootRomChecksumPublished = true;
    }

    private void TrackBootRomChecksumHostClear(ushort offset, ushort value)
    {
        ushort comm = ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if (!HasCartridgeHeaderChecksum() ||
            offset != comm + 8 ||
            value != 0)
        {
            return;
        }

        _bootRomChecksumHostCleared = true;
        _bootRomChecksumPublished = false;
        _m68kCommunicationStaleWordValid[4] = false;
        _m68kCommunicationStaleValid[8] = false;
        _m68kCommunicationStaleValid[9] = false;
    }

    private void TrackBootRomSixtyEightUpHostWrite(ushort offset, ushort value)
    {
        ushort comm = ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if (_bootRomHandshakePending ||
            (offset != comm + 12 && offset != comm + 14))
        {
            return;
        }

        if (offset == comm + 12 && value == 0x0002)
        {
            _m68kVdpControlMailboxArmed = true;
        }
        else if (offset == comm + 12 && value != 0)
        {
            _m68kVdpControlMailboxArmed = false;
            _m68kVdpControlMailboxHighPending = false;
            if (!_bootRomHandshakePending && !_bootRomPostStartSignaturePending)
            {
                ClearBootRomCommunicationSignature();
                ClearCommunicationStaleRange(0, BootRomCommunicationSignature.Length);
            }
        }

        ushort highCommand = offset == comm + 12 ? value : ReadBigEndianWord(_systemRegisters, comm + 12);
        ushort lowCommand = offset == comm + 14 ? value : ReadBigEndianWord(_systemRegisters, comm + 14);
        if (highCommand == 0x3638 &&
            lowCommand == 0x5550)
        {
            _bootRomSixtyEightUpPending = true;
            _bootRomSixtyEightUpReadyHiddenFromSh2 = false;
        }
    }

    private void PublishBootRomSixtyEightUpReadyAfterHostClear(ushort offset, ushort value)
    {
        ushort comm = ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if (!_bootRomSixtyEightUpPending ||
            offset != comm + 14 ||
            value != 0 ||
            _systemRegisters[comm + 12] != 0 ||
            _systemRegisters[comm + 13] != 0 ||
            _systemRegisters[comm + 14] != 0 ||
            _systemRegisters[comm + 15] != 0)
        {
            return;
        }

        WriteBigEndianWord(_systemRegisters, comm + 12, 0x475F);
        WriteBigEndianWord(_systemRegisters, comm + 14, 0x4F4B);
        _m68kCommunicationStaleWordValid[6] = false;
        _m68kCommunicationStaleWordValid[7] = false;
        _m68kCommunicationStaleValid[12] = false;
        _m68kCommunicationStaleValid[13] = false;
        _m68kCommunicationStaleValid[14] = false;
        _m68kCommunicationStaleValid[15] = false;
        _m68kCommunicationPendingHostBytes[12] = false;
        _m68kCommunicationPendingHostBytes[13] = false;
        _m68kCommunicationPendingHostBytes[14] = false;
        _m68kCommunicationPendingHostBytes[15] = false;
        _m68kCommunicationDeferredSh2ClearBytes[12] = false;
        _m68kCommunicationDeferredSh2ClearBytes[13] = false;
        _m68kCommunicationDeferredSh2ClearBytes[14] = false;
        _m68kCommunicationDeferredSh2ClearBytes[15] = false;
        _bootRomSixtyEightUpPending = false;
        _bootRomSixtyEightUpReadyHiddenFromSh2 = true;
    }

    private void RetireBootRomSixtyEightUpReadyOnHostWrite(ushort offset, ushort value)
    {
        ushort comm = ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if (!_bootRomSixtyEightUpReadyHiddenFromSh2 ||
            offset != comm + 14 ||
            value != 0)
        {
            return;
        }

        _bootRomSixtyEightUpReadyHiddenFromSh2 = false;
    }

    private bool IsBootRomSixtyEightUpHiddenFromSh2(ushort offset, int bytes)
    {
        ushort comm = ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        int relative = (offset & (SystemRegisterBytes - 1)) - comm;
        if (relative < 12 || relative + bytes > 16)
        {
            return false;
        }

        if (_bootRomSixtyEightUpPending)
        {
            return MatchesCommunicationRange(relative, bytes, [0x36, 0x38, 0x55, 0x50], baseRelative: 12);
        }

        if (_bootRomSixtyEightUpReadyHiddenFromSh2)
        {
            return MatchesCommunicationRange(relative, bytes, [0x4F, 0x4B], baseRelative: 14);
        }

        return false;
    }

    private bool TryConsumeBootRomSixtyEightUpReadyLong(uint address, out uint value)
    {
        value = 0;
        ushort comm = ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if (!_bootRomSixtyEightUpReadyHiddenFromSh2 ||
            address != ThirtyTwoXHardwareProfile.Sh2SystemRegister((ushort)(comm + 12)) ||
            _systemRegisters[comm + 12] != 0x47 ||
            _systemRegisters[comm + 13] != 0x5F ||
            _systemRegisters[comm + 14] != 0x4F ||
            _systemRegisters[comm + 15] != 0x4B)
        {
            return false;
        }

        value = 0x475F_4F4B;
        for (int i = 12; i < 16; i++)
        {
            _systemRegisters[comm + i] = 0;
            _m68kCommunicationStaleValid[i] = false;
            _m68kCommunicationPendingHostBytes[i] = false;
            _m68kCommunicationDeferredSh2ClearBytes[i] = false;
        }

        _m68kCommunicationStaleWordValid[6] = false;
        _m68kCommunicationStaleWordValid[7] = false;
        _bootRomSixtyEightUpReadyHiddenFromSh2 = false;
        return true;
    }

    private bool TryAcknowledgeBootRomGOkLong(uint address, uint value, int cpuIndex)
    {
        ushort comm = ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if (value != 0 ||
            address != ThirtyTwoXHardwareProfile.Sh2SystemRegister((ushort)(comm + 12)) ||
            _systemRegisters[comm + 12] != 0x47 ||
            _systemRegisters[comm + 13] != 0x5F ||
            _systemRegisters[comm + 14] != 0x4F ||
            _systemRegisters[comm + 15] != 0x4B)
        {
            return false;
        }

        string source = cpuIndex == 0 ? "MSH2" : "SSH2";
        _sideEffectPreviousSystemRegisterWord = 0x475F;
        for (int i = 12; i < 16; i++)
        {
            _systemRegisters[comm + i] = 0;
            _m68kCommunicationStaleValid[i] = false;
            _m68kCommunicationPendingHostBytes[i] = false;
            _m68kCommunicationDeferredSh2ClearBytes[i] = false;
            SystemRegisterWriteObserver?.Invoke(new SystemRegisterWriteTrace(source, (ushort)(comm + i), 0));
        }

        _m68kCommunicationStaleWordValid[6] = false;
        _m68kCommunicationStaleWordValid[7] = false;
        _bootRomSixtyEightUpReadyHiddenFromSh2 = false;
        TraceSystemRegisterAccess(source, "W32", (ushort)(comm + 12), 0);
        ApplySystemRegisterSideEffects((ushort)(comm + 12), allowAdapterControl: false);
        return true;
    }

    private bool MatchesCommunicationRange(int relative, int bytes, ReadOnlySpan<byte> expected, int baseRelative)
    {
        int start = relative - baseRelative;
        if (start < 0 || start + bytes > expected.Length)
        {
            return false;
        }

        for (int i = 0; i < bytes; i++)
        {
            if (_systemRegisters[ThirtyTwoXHardwareProfile.CommunicationPortOffset + relative + i] != expected[start + i])
            {
                return false;
            }
        }

        return true;
    }

    private void UpdateBootRomHandshakeAfterM68kWrite(ushort offset, bool hadBootRomSignature)
    {
        if (!_bootRomHandshakePending)
        {
            return;
        }

        ushort comm = ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if (offset < comm || offset >= comm + 8)
        {
            return;
        }

        for (int i = 0; i < 8; i++)
        {
            if (_systemRegisters[comm + i] != 0)
            {
                return;
            }
        }

        if (!_userHeader.IsValid && (!_bootRomSignatureRead || HasCartridgeHeaderChecksum()))
        {
            for (int i = 0; i < BootRomCommunicationSignature.Length; i++)
            {
                _systemRegisters[comm + i] = BootRomCommunicationSignature[i];
            }

            return;
        }

        if (_userHeader.IsValid && !_bootRomPostStartSignaturePending)
        {
            for (int i = 0; i < BootRomCommunicationSignature.Length; i++)
            {
                _systemRegisters[comm + i] = BootRomCommunicationSignature[i];
            }

            _bootRomPostStartSignaturePending = true;
            _bootRomPostStartSignatureHiddenFromSh2 = false;
            _bootRomPostStartSignatureReadMask = 0;
            _bootRomPostStartHostClearProtectMask = 0;
            _bootRomSignatureRead = false;
            _bootRomSignatureReadbackActive = false;
            _bootRomHandshakePending = false;
            _bootRomLaunchPending = _userHeader.RequiresHostLaunchCommand;
            return;
        }

        _bootRomHandshakePending = false;
        _bootRomSignatureRead = false;
        _bootRomSignatureReadbackActive = false;
        _bootRomPostStartSignaturePending = false;
        _bootRomPostStartSignatureHiddenFromSh2 = false;
        _bootRomPostStartSignatureReadMask = 0;
        _bootRomPostStartHostClearProtectMask = 0;
        _bootRomLaunchPending = _userHeader.RequiresHostLaunchCommand;
    }

    private void CancelBootRomReadbackOnSh2DataWrite(ushort offset, byte value)
    {
        ushort comm = ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if (value == 0 ||
            (!_bootRomHandshakePending && !_bootRomSignatureReadbackActive) ||
            offset < comm ||
            offset >= comm + 8)
        {
            return;
        }

        _bootRomHandshakePending = false;
        _bootRomSignatureRead = false;
        _bootRomSignatureReadbackActive = false;
        _bootRomLaunchPending = false;
        _bootRomPostStartSignaturePending = false;
        _bootRomPostStartSignatureHiddenFromSh2 = false;
        _bootRomPostStartSignatureReadMask = 0;
        _bootRomPostStartHostClearProtectMask = 0;
    }

    private void TraceSystemRegisterAccess(string source, string operation, ushort offset, ushort value)
    {
        SystemRegisterAccessObserver?.Invoke(new SystemRegisterAccessTrace(source, operation, offset, value));
    }

    private void TraceSh2MemoryAccess(int cpuIndex, string operation, uint address, uint value)
    {
        if (Sh2MemoryAccessObserver is null || Sh2MemoryAccessTraceFilter?.Invoke(address) != true)
        {
            return;
        }

        Sh2MemoryAccessObserver(new Sh2MemoryAccessTrace(cpuIndex == 0 ? "MSH2" : "SSH2", operation, address, value));
    }

    private void TraceSdramWordWrite(int cpuIndex, string operation, uint address, int offset, ushort value)
    {
        Action<SdramWriteTrace>? observer = SdramWriteObserver;
        if (observer is null)
        {
            return;
        }

        int normalizedOffset = offset & (ThirtyTwoXHardwareProfile.SdramBytes - 1);
        int lowOffset = (normalizedOffset + 1) & (ThirtyTwoXHardwareProfile.SdramBytes - 1);
        Func<int, bool>? filter = SdramWriteTraceFilter;
        if (filter is not null && !filter(normalizedOffset) && !filter(lowOffset))
        {
            return;
        }

        Sh2Cpu cpu = cpuIndex == 0 ? MasterSh2 : SlaveSh2;
        observer(new SdramWriteTrace(
            cpuIndex == 0 ? "MSH2" : "SSH2",
            operation,
            address,
            normalizedOffset,
            value,
            cpu.LastOpcodePc,
            cpu.LastOpcode));
    }

    private FrameBufferAccessTrace BuildFrameBufferAccessTrace(string source, string operation, uint offset, ushort value)
    {
        uint pc = 0;
        ushort opcode = 0;
        if (source == "MSH2")
        {
            pc = MasterSh2.LastOpcodePc;
            opcode = MasterSh2.LastOpcode;
        }
        else if (source == "SSH2")
        {
            pc = SlaveSh2.LastOpcodePc;
            opcode = SlaveSh2.LastOpcode;
        }

        return new FrameBufferAccessTrace(
            source,
            operation,
            offset,
            value,
            DrawFrameBufferIndex,
            DisplayFrameBufferIndex,
            _requestedDisplayFrameBufferIndex,
            _frameBufferSwapPending,
            pc,
            opcode);
    }

    private void TraceDeniedFrameBufferAccess(string source, string operation, uint offset, ushort value)
    {
        _deniedFrameBufferAccessCount++;
        FrameBufferAccessObserver?.Invoke(BuildFrameBufferAccessTrace(source, operation, offset, value));
    }

    private void ApplyVdpRegisterSideEffects(ushort offset, bool completedWordWrite)
    {
        if (offset == ThirtyTwoXHardwareProfile.FrameBufferControlOffset)
        {
            SelectFrameBuffer(ReadBigEndianWord(_vdpRegisters, offset) & 0x01);
        }
        else if (offset == ThirtyTwoXHardwareProfile.AutoFillDataOffset && completedWordWrite)
        {
            ExecuteAutoFill();
        }
    }

    private void TrackVdpRegisterWrite(ushort offset)
    {
        _vdpRegisterWriteCount++;
        if (offset == ThirtyTwoXHardwareProfile.BitmapModeOffset)
        {
            _bitmapModeWriteCount++;
            _lastBitmapModeWrite = ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset);
        }
        else if (offset == ThirtyTwoXHardwareProfile.FrameBufferControlOffset)
        {
            _frameBufferControlWriteCount++;
            _lastFrameBufferControlWrite = ReadBigEndianWord(_vdpRegisters, ThirtyTwoXHardwareProfile.FrameBufferControlOffset);
        }
    }

    private void CompositeFrameInto(Span<byte> framebuffer, bool blueFirst, ReadOnlySpan<bool> mdOpaquePixels)
    {
        if (framebuffer.Length < OutputWidth * OutputHeight * 3)
        {
            throw new ArgumentException("Destination framebuffer is too small.", nameof(framebuffer));
        }

        _lastCompositeUsedFallback = false;
        _lastCompositeMode = 0;
        _lastCompositeWrittenPixels = 0;
        ReadOnlySpan<byte> source = _frameBuffers[_visibleDisplayFrameBufferIndex & 0x01];
        if (IsAllZero(source) && !IsAllZero(DrawFrameBuffer))
        {
            source = DrawFrameBuffer;
            _lastCompositeUsedFallback = true;
        }

        ushort bitmapMode = _latchedBitmapMode;
        int mode = bitmapMode & 0x03;
        bool thirtyTwoXPriority = (bitmapMode & 0x0080) != 0;
        bool shiftLeft = (_latchedScreenShiftControl & 0x01) != 0;
        if (mode == 0)
        {
            if (IsAllZero(source))
            {
                return;
            }

            mode = IsAllZero(_palette) ? 2 : 1;
            _lastCompositeUsedFallback = true;
        }

        _lastCompositeMode = mode;
        CompositeSourceFrame(framebuffer, source, mode, blueFirst, shiftLeft, thirtyTwoXPriority, mdOpaquePixels);
        if ((_lastCompositeWrittenPixels == 0 || !HasNonZeroPalettePixels(source, mode)) &&
            !source.SequenceEqual(DisplayFrameBuffer) &&
            !IsAllZero(DisplayFrameBuffer) &&
            HasNonZeroPalettePixels(DisplayFrameBuffer, mode))
        {
            source = DisplayFrameBuffer;
            _lastCompositeUsedFallback = true;
            CompositeSourceFrame(framebuffer, source, mode, blueFirst, shiftLeft, thirtyTwoXPriority, mdOpaquePixels);
        }

        if ((_lastCompositeWrittenPixels == 0 || !HasNonZeroPalettePixels(source, mode)) &&
            !source.SequenceEqual(DrawFrameBuffer) &&
            !IsAllZero(DrawFrameBuffer) &&
            HasNonZeroPalettePixels(DrawFrameBuffer, mode))
        {
            source = DrawFrameBuffer;
            _lastCompositeUsedFallback = true;
            CompositeSourceFrame(framebuffer, source, mode, blueFirst, shiftLeft, thirtyTwoXPriority, mdOpaquePixels);
        }
    }

    private void CompositeSourceFrame(Span<byte> framebuffer, ReadOnlySpan<byte> source, int mode, bool blueFirst, bool shiftLeft, bool thirtyTwoXPriority, ReadOnlySpan<bool> mdOpaquePixels)
    {
        for (int y = 0; y < OutputHeight; y++)
        {
            if (!TryReadFrameBufferLineAddress(source, y, out int lineAddress))
            {
                continue;
            }

            if (mode == 1)
            {
                CompositePackedPixelLine(framebuffer, source, y, lineAddress, blueFirst, shiftLeft, thirtyTwoXPriority, mdOpaquePixels);
            }
            else if (mode == 2)
            {
                CompositeDirectColorLine(framebuffer, source, y, lineAddress, blueFirst, shiftLeft, thirtyTwoXPriority, mdOpaquePixels);
            }
            else if (mode == 3)
            {
                CompositeRunLengthLine(framebuffer, source, y, lineAddress, blueFirst, shiftLeft, thirtyTwoXPriority, mdOpaquePixels);
            }
        }
    }

    private bool HasNonZeroPalettePixels(ReadOnlySpan<byte> source, int mode)
    {
        if (mode == 2)
        {
            for (int y = 0; y < OutputHeight; y++)
            {
                if (!TryReadFrameBufferLineAddress(source, y, out int lineAddress))
                {
                    continue;
                }

                for (int x = 0; x < OutputWidth; x++)
                {
                    int sourceIndex = lineAddress + (x * 2);
                    if (sourceIndex + 1 >= source.Length)
                    {
                        break;
                    }

                    if (HasVisibleDirectColorBits(ReadBigEndianWord(source, sourceIndex)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        if (mode == 3)
        {
            for (int y = 0; y < OutputHeight; y++)
            {
                if (!TryReadFrameBufferLineAddress(source, y, out int lineAddress))
                {
                    continue;
                }

                int x = 0;
                int sourceIndex = lineAddress;
                while (x < OutputWidth && sourceIndex + 1 < source.Length)
                {
                    ushort span = ReadBigEndianWord(source, sourceIndex);
                    sourceIndex += 2;
                    int runLength = (span >> 8) + 1;
                    int paletteIndex = span & 0x00FF;
                    if (paletteIndex != 0 && ReadBigEndianWord(_palette, paletteIndex * 2) != 0)
                    {
                        return true;
                    }

                    x += runLength;
                }
            }

            return false;
        }

        for (int y = 0; y < OutputHeight; y++)
        {
            if (!TryReadFrameBufferLineAddress(source, y, out int lineAddress))
            {
                continue;
            }

            for (int x = 0; x < OutputWidth; x++)
            {
                int sourceIndex = lineAddress + x;
                if ((uint)sourceIndex >= (uint)source.Length)
                {
                    break;
                }

                byte paletteIndex = source[sourceIndex];
                if (paletteIndex != 0 && ReadBigEndianWord(_palette, paletteIndex * 2) != 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryReadFrameBufferLineAddress(ReadOnlySpan<byte> source, int y, out int lineAddress)
    {
        ushort lineAddressWord = ReadBigEndianWord(source, y * 2);
        if (lineAddressWord == 0)
        {
            lineAddress = 0;
            return false;
        }

        lineAddress = lineAddressWord * 2;
        return lineAddress < source.Length;
    }

    private void CompositePackedPixelLine(Span<byte> framebuffer, ReadOnlySpan<byte> source, int y, int lineAddress, bool blueFirst, bool shiftLeft, bool thirtyTwoXPriority, ReadOnlySpan<bool> mdOpaquePixels)
    {
        int sourcePixelOffset = shiftLeft ? 1 : 0;
        for (int x = 0; x < OutputWidth; x++)
        {
            int sourceIndex = lineAddress + x + sourcePixelOffset;
            if ((uint)sourceIndex >= (uint)source.Length)
            {
                break;
            }

            int paletteIndex = source[sourceIndex];
            if (paletteIndex == 0)
            {
                continue;
            }

            ushort color = ReadBigEndianWord(_palette, paletteIndex * 2);
            if (WriteRgb555IfVisible(framebuffer, y, x, color, blueFirst, thirtyTwoXPriority, mdOpaquePixels))
            {
                _lastCompositeWrittenPixels++;
            }
        }
    }

    private void CompositeDirectColorLine(Span<byte> framebuffer, ReadOnlySpan<byte> source, int y, int lineAddress, bool blueFirst, bool shiftLeft, bool thirtyTwoXPriority, ReadOnlySpan<bool> mdOpaquePixels)
    {
        int sourcePixelOffset = shiftLeft ? 1 : 0;
        for (int x = 0; x < OutputWidth; x++)
        {
            int sourceIndex = lineAddress + ((x + sourcePixelOffset) * 2);
            if (sourceIndex + 1 >= source.Length)
            {
                break;
            }

            ushort color = ReadBigEndianWord(source, sourceIndex);
            if (WriteRgb555IfVisible(framebuffer, y, x, color, blueFirst, thirtyTwoXPriority, mdOpaquePixels))
            {
                _lastCompositeWrittenPixels++;
            }
        }
    }

    private void CompositeRunLengthLine(Span<byte> framebuffer, ReadOnlySpan<byte> source, int y, int lineAddress, bool blueFirst, bool shiftLeft, bool thirtyTwoXPriority, ReadOnlySpan<bool> mdOpaquePixels)
    {
        int x = shiftLeft ? -1 : 0;
        int sourceIndex = lineAddress;
        while (x < OutputWidth && sourceIndex + 1 < source.Length)
        {
            ushort span = ReadBigEndianWord(source, sourceIndex);
            sourceIndex += 2;

            int runLength = (span >> 8) + 1;
            int paletteIndex = span & 0x00FF;
            ushort color = ReadBigEndianWord(_palette, paletteIndex * 2);
            int end = Math.Min(OutputWidth, x + runLength);
            while (x < end)
            {
                if (x >= 0 && paletteIndex != 0)
                {
                    if (WriteRgb555IfVisible(framebuffer, y, x, color, blueFirst, thirtyTwoXPriority, mdOpaquePixels))
                    {
                        _lastCompositeWrittenPixels++;
                    }
                }

                x++;
            }
        }
    }

    private static bool WriteRgb555IfVisible(Span<byte> framebuffer, int y, int x, ushort color, bool blueFirst, bool thirtyTwoXPriority, ReadOnlySpan<bool> mdOpaquePixels)
    {
        int pixelIndex = (y * OutputWidth) + x;
        bool through = (color & 0x8000) != 0;
        bool inFrontOfMd = thirtyTwoXPriority != through;
        if (!inFrontOfMd && mdOpaquePixels.Length > pixelIndex && mdOpaquePixels[pixelIndex])
        {
            return false;
        }

        WriteRgb555(framebuffer, pixelIndex * 3, color, blueFirst);
        return true;
    }

    private static bool HasVisibleDirectColorBits(ushort color)
    {
        return (color & 0x7FFF) != 0;
    }

    private static void WriteRgb555(Span<byte> framebuffer, int offset, ushort color, bool blueFirst)
    {
        byte r = Expand5To8(color & 0x1F);
        byte g = Expand5To8((color >> 5) & 0x1F);
        byte b = Expand5To8((color >> 10) & 0x1F);
        if (blueFirst)
        {
            framebuffer[offset] = b;
            framebuffer[offset + 1] = g;
            framebuffer[offset + 2] = r;
        }
        else
        {
            framebuffer[offset] = r;
            framebuffer[offset + 1] = g;
            framebuffer[offset + 2] = b;
        }
    }

    private static byte Expand5To8(int value)
    {
        return (byte)((value << 3) | (value >> 2));
    }

    private static bool IsAllZero(ReadOnlySpan<byte> data)
    {
        foreach (byte value in data)
        {
            if (value != 0)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsBlankMode()
    {
        return (ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset) & 0x03) == 0;
    }

    private bool IsDirectColorMode()
    {
        return (ReadVdpRegisterWord(ThirtyTwoXHardwareProfile.BitmapModeOffset) & 0x03) == 2;
    }

    private bool IsLatchedBlankMode()
    {
        return (_latchedBitmapMode & 0x03) == 0;
    }

    private bool CanSwitchFrameBufferNow()
    {
        return _vBlank || IsLatchedBlankMode();
    }

    private bool IsExternalFrameBufferAccessDenied()
    {
        // The host writes the draw buffer; FEN reports display-buffer engagement, not a host draw-buffer lockout.
        return false;
    }

    private bool IsSh2FrameBufferAccessDenied()
    {
        return !_vdpAccessGrantedToSh2;
    }

    private bool IsFrameBufferEngaged()
    {
        return !_vBlank && !_hBlank && !IsBlankMode();
    }

    private bool IsPaletteAccessApproved()
    {
        return _vBlank || IsBlankMode() || IsDirectColorMode();
    }

    private bool IsSh2PaletteAccessDenied()
    {
        return !_vdpAccessGrantedToSh2;
    }

    private void AddSh2PaletteBusyWaitIfNeeded(int cpuIndex)
    {
        if (!IsPaletteAccessApproved())
        {
            AddSh2WaitCycles(cpuIndex, Sh2PaletteBusyWaitCycles);
        }
    }

    private void AddSh2FrameBufferBusyWaitIfNeeded(int cpuIndex)
    {
        if (IsFrameBufferEngaged())
        {
            AddSh2WaitCycles(cpuIndex, Sh2FrameBufferBusyWaitCycles);
        }
    }

    private void LatchVdpDisplayControls()
    {
        _latchedBitmapMode = ReadBigEndianWord(_vdpRegisters, ThirtyTwoXHardwareProfile.BitmapModeOffset);
        _latchedScreenShiftControl = ReadBigEndianWord(_vdpRegisters, ThirtyTwoXHardwareProfile.ScreenShiftControlOffset);
    }

    private void SelectFrameBuffer(int requestedDisplayFrameBuffer)
    {
        if (_frameBufferSwapPending)
        {
            ushort control = ReadBigEndianWord(_vdpRegisters, ThirtyTwoXHardwareProfile.FrameBufferControlOffset);
            control = (ushort)((control & ~0x0001) | _requestedDisplayFrameBufferIndex);
            WriteBigEndianWord(_vdpRegisters, ThirtyTwoXHardwareProfile.FrameBufferControlOffset, control);
            return;
        }

        _requestedDisplayFrameBufferIndex = requestedDisplayFrameBuffer & 0x01;
        _pendingDrawFrameBufferIndex = _requestedDisplayFrameBufferIndex ^ 1;
        _frameBufferSwapPending = _requestedDisplayFrameBufferIndex != _activeDisplayFrameBufferIndex;
        if (IsLatchedBlankMode())
        {
            CompletePendingFrameBufferSwap();
        }
    }

    private void ExecuteAutoFill()
    {
        int length = (ReadBigEndianWord(_vdpRegisters, ThirtyTwoXHardwareProfile.AutoFillLengthOffset) & 0x00FF) + 1;
        ushort startWordAddress = ReadBigEndianWord(_vdpRegisters, ThirtyTwoXHardwareProfile.AutoFillStartAddressOffset);
        ushort fillData = ReadBigEndianWord(_vdpRegisters, ThirtyTwoXHardwareProfile.AutoFillDataOffset);
        int fixedPage = startWordAddress & ~0x00FF;
        byte[] target = _frameBuffers[DrawFrameBufferIndex];

        for (int i = 0; i < length; i++)
        {
            int wordAddress = fixedPage | ((startWordAddress + i) & 0x00FF);
            WriteBigEndianWord(target, wordAddress * 2, fillData);
            _frameBufferByteWriteCount += 2;
            FrameBufferAccessObserver?.Invoke(BuildFrameBufferAccessTrace("VDP", "AF16", (uint)(wordAddress * 2), fillData));
        }

        ushort finalWordAddress = (ushort)(fixedPage | ((startWordAddress + length - 1) & 0x00FF));
        WriteBigEndianWord(_vdpRegisters, ThirtyTwoXHardwareProfile.AutoFillStartAddressOffset, finalWordAddress);
    }

    private ushort BuildFrameBufferControlStatus()
    {
        ushort raw = ReadBigEndianWord(_vdpRegisters, ThirtyTwoXHardwareProfile.FrameBufferControlOffset);
        ushort status = (ushort)(raw & FrameBufferStatusFrameBufferSelect);
        if (IsFrameBufferEngaged())
        {
            status |= FrameBufferStatusFrameBufferDenied;
        }

        if (_vBlank)
        {
            status |= FrameBufferStatusVBlank;
        }

        if (_hBlank)
        {
            status |= FrameBufferStatusHBlank;
        }

        if (IsPaletteAccessApproved())
        {
            status |= FrameBufferStatusPaletteAccess;
        }

        return status;
    }

    private ushort BuildHorizontalCountStatus()
    {
        // The SH-2 HCount register exposes the current Mega Drive/32X scanline
        // counter in the low byte; the high byte is unused on retail polling
        // paths and reads as zero.
        return (ushort)(_currentScanline & 0x00FF);
    }

    private ushort BuildSh2InterruptMask(int cpuIndex)
    {
        ushort mask = cpuIndex == 0 ? _masterInterruptMask : _slaveInterruptMask;
        if (_vdpAccessGrantedToSh2)
        {
            mask |= 0x8000;
        }
        else
        {
            mask &= 0x7FFF;
        }

        if (_adapterEnabled)
        {
            mask |= 0x0200;
        }
        else
        {
            mask &= 0xFDFF;
        }

        mask &= 0xFFBF; // CART is active-low: zero means inserted.
        return mask;
    }

    private void WriteSh2InterruptMask(int cpuIndex, ushort value)
    {
        _vdpAccessGrantedToSh2 = (value & AdapterControlVdpAccessSh2) != 0;
        ushort adapterControl = ReadBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.AdapterControlOffset);
        adapterControl = _vdpAccessGrantedToSh2
            ? (ushort)(adapterControl | AdapterControlVdpAccessSh2)
            : (ushort)(adapterControl & ~AdapterControlVdpAccessSh2);
        WriteBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.AdapterControlOffset, adapterControl);

        ushort mask = (ushort)(value & ~AdapterControlVdpAccessSh2);
        if (cpuIndex == 0)
        {
            _masterInterruptMask = mask;
        }
        else
        {
            _slaveInterruptMask = mask;
        }
    }

    private void RequestHorizontalInterruptIfDue()
    {
        if (_vBlank && ((_masterInterruptMask | _slaveInterruptMask) & Sh2InterruptMaskHorizontalInVBlank) == 0)
        {
            return;
        }

        if (_horizontalInterruptCounter++ < _horizontalInterruptPeriod)
        {
            return;
        }

        _horizontalInterruptCounter = 0;
        _masterHorizontalInterruptPending = true;
        _slaveHorizontalInterruptPending = true;
        RequestPendingInterrupts();
    }

    private void RequestPendingInterrupts()
    {
        RequestPendingWatchdogInterrupt(0);
        RequestPendingWatchdogInterrupt(1);

        if (_masterVresInterruptPending)
        {
            MasterSh2.RequestInterrupt(14, Sh2VresInterruptVector);
        }

        if (_slaveVresInterruptPending)
        {
            SlaveSh2.RequestInterrupt(14, Sh2VresInterruptVector);
        }

        if (_masterVerticalInterruptPending && (BuildSh2InterruptMask(0) & Sh2InterruptMaskVertical) != 0)
        {
            MasterSh2.RequestInterrupt(12, Sh2VerticalInterruptVector);
        }

        if (_slaveVerticalInterruptPending && (BuildSh2InterruptMask(1) & Sh2InterruptMaskVertical) != 0)
        {
            SlaveSh2.RequestInterrupt(12, Sh2VerticalInterruptVector);
        }

        if (_masterHorizontalInterruptPending && (BuildSh2InterruptMask(0) & Sh2InterruptMaskHorizontal) != 0)
        {
            MasterSh2.RequestInterrupt(10, Sh2HorizontalInterruptVector);
        }

        if (_slaveHorizontalInterruptPending && (BuildSh2InterruptMask(1) & Sh2InterruptMaskHorizontal) != 0)
        {
            SlaveSh2.RequestInterrupt(10, Sh2HorizontalInterruptVector);
        }

        if (_masterCommandInterruptPending && (BuildSh2InterruptMask(0) & Sh2InterruptMaskCommand) != 0)
        {
            MasterSh2.RequestInterrupt(8, Sh2CommandInterruptVector);
        }

        if (_slaveCommandInterruptPending && (BuildSh2InterruptMask(1) & Sh2InterruptMaskCommand) != 0)
        {
            SlaveSh2.RequestInterrupt(8, Sh2CommandInterruptVector);
        }

        if (_masterPwmInterruptPending && (BuildSh2InterruptMask(0) & Sh2InterruptMaskPwm) != 0)
        {
            MasterSh2.RequestInterrupt(6, Sh2PwmInterruptVector);
        }

        if (_slavePwmInterruptPending && (BuildSh2InterruptMask(1) & Sh2InterruptMaskPwm) != 0)
        {
            SlaveSh2.RequestInterrupt(6, Sh2PwmInterruptVector);
        }
    }

    private void RequestPendingWatchdogInterrupt(int cpuIndex)
    {
        if (!_sh2WatchdogInterruptPending[cpuIndex & 1])
        {
            return;
        }

        int priority = GetSh2WatchdogInterruptPriority(cpuIndex);
        if (priority == 0)
        {
            return;
        }

        int vector = GetSh2WatchdogInterruptVector(cpuIndex);
        (cpuIndex == 0 ? MasterSh2 : SlaveSh2).RequestInterrupt(priority, vector);
    }

    private void OnSh2InterruptAccepted(int cpuIndex, int level, int vectorNumber)
    {
        if (_sh2WatchdogInterruptPending[cpuIndex & 1]
            && level == GetSh2WatchdogInterruptPriority(cpuIndex)
            && vectorNumber == GetSh2WatchdogInterruptVector(cpuIndex))
        {
            _sh2WatchdogInterruptPending[cpuIndex & 1] = false;
            return;
        }

        // 32X SH-2 interrupts are level latches. Accepting the interrupt only
        // raises SR.I; software must write the matching clear register.
    }

    private void ClearSh2Interrupt(ushort offset, int cpuIndex)
    {
        switch (offset)
        {
            case ThirtyTwoXHardwareProfile.VResInterruptClearOffset:
                ClearVresInterruptForCpu(cpuIndex);
                break;
            case ThirtyTwoXHardwareProfile.VInterruptClearOffset:
                ClearVerticalInterruptForCpu(cpuIndex);
                break;
            case ThirtyTwoXHardwareProfile.HInterruptClearOffset:
                ClearHorizontalInterruptForCpu(cpuIndex);
                break;
            case ThirtyTwoXHardwareProfile.CommandInterruptClearOffset:
                ClearCommandInterruptForCpu(cpuIndex);
                break;
            case ThirtyTwoXHardwareProfile.PwmInterruptClearOffset:
                ClearPwmInterruptForCpu(cpuIndex);
                break;
        }
    }

    private void ClearVresInterruptForCpu(int cpuIndex)
    {
        SetVresInterruptPending(cpuIndex, false);
        ClearPendingSh2Interrupt(cpuIndex, 14, Sh2VresInterruptVector);
    }

    private void ClearVerticalInterruptForCpu(int cpuIndex)
    {
        SetVerticalInterruptPending(cpuIndex, false);
        ClearPendingSh2Interrupt(cpuIndex, 12, Sh2VerticalInterruptVector);
    }

    private void ClearHorizontalInterruptForCpu(int cpuIndex)
    {
        SetHorizontalInterruptPending(cpuIndex, false);
        ClearPendingSh2Interrupt(cpuIndex, 10, Sh2HorizontalInterruptVector);
    }

    private void ClearCommandInterruptForCpu(int cpuIndex)
    {
        SetCommandInterruptPending(cpuIndex, false);
        ClearCommandInterruptStatusBit(cpuIndex);
        ClearPendingSh2Interrupt(cpuIndex, 8, Sh2CommandInterruptVector);
    }

    private void ClearPwmInterruptForCpu(int cpuIndex)
    {
        SetPwmInterruptPending(cpuIndex, false);
        ClearPendingSh2Interrupt(cpuIndex, 6, Sh2PwmInterruptVector);
    }

    private void SetVerticalInterruptPending(int cpuIndex, bool pending)
    {
        if (cpuIndex == 0)
        {
            _masterVerticalInterruptPending = pending;
        }
        else
        {
            _slaveVerticalInterruptPending = pending;
        }
    }

    private void SetVresInterruptPending(int cpuIndex, bool pending)
    {
        if (cpuIndex == 0)
        {
            _masterVresInterruptPending = pending;
        }
        else
        {
            _slaveVresInterruptPending = pending;
        }
    }

    private void SetHorizontalInterruptPending(int cpuIndex, bool pending)
    {
        if (cpuIndex == 0)
        {
            _masterHorizontalInterruptPending = pending;
        }
        else
        {
            _slaveHorizontalInterruptPending = pending;
        }
    }

    private void SetCommandInterruptPending(int cpuIndex, bool pending)
    {
        if (cpuIndex == 0)
        {
            _masterCommandInterruptPending = pending;
        }
        else
        {
            _slaveCommandInterruptPending = pending;
        }
    }

    private void TryCompleteBootRomPeerReadyProbe(ushort offset, ushort previousValue, ushort value, int cpuIndex)
    {
        ushort comm = ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if (cpuIndex != 0 ||
            offset != comm + 2 ||
            previousValue != 0x4F4B ||
            value != 0xBEEF ||
            !_bootRomPostStartSignatureHiddenFromSh2)
        {
            return;
        }

        WriteBigEndianWord(_systemRegisters, comm + 2, 0xDEAF);
        ClearM68kCommunicationTrackingForWord(2);
    }

    private void SetPwmInterruptPending(int cpuIndex, bool pending)
    {
        if (cpuIndex == 0)
        {
            _masterPwmInterruptPending = pending;
        }
        else
        {
            _slavePwmInterruptPending = pending;
        }
    }

    private void ClearCommandInterruptStatusBit(int cpuIndex)
    {
        byte mask = cpuIndex == 0 ? (byte)0xFE : (byte)0xFD;
        _systemRegisters[ThirtyTwoXHardwareProfile.InterruptControlOffset + 1] &= mask;
    }

    private void ClearPendingSh2Interrupt(int cpuIndex, int level, int vector)
    {
        (cpuIndex == 0 ? MasterSh2 : SlaveSh2).ClearPendingInterrupt(level, vector);
    }

    private void CompletePendingFrameBufferSwap()
    {
        if (!_frameBufferSwapPending)
        {
            return;
        }

        _activeDisplayFrameBufferIndex = _requestedDisplayFrameBufferIndex & 0x01;
        if (!_vBlank)
        {
            _visibleDisplayFrameBufferIndex = _activeDisplayFrameBufferIndex;
        }

        _pendingDrawFrameBufferIndex = DrawFrameBufferIndex;
        _frameBufferSwapPending = false;
        ushort control = ReadBigEndianWord(_vdpRegisters, ThirtyTwoXHardwareProfile.FrameBufferControlOffset);
        control = (ushort)((control & ~0x0001) | _activeDisplayFrameBufferIndex);
        WriteBigEndianWord(_vdpRegisters, ThirtyTwoXHardwareProfile.FrameBufferControlOffset, control);
    }

    private static int LogicalDisplayFrameBufferToPhysicalIndex(int select)
    {
        return select & 0x01;
    }

    private void PushPwm(Queue<ushort> audioFifo, Queue<ushort> hardwareFifo, ushort offset, ushort value)
    {
        ushort sample = (ushort)(value & 0x0FFF);
        if (!PushPwmHardwareSample(hardwareFifo, sample))
        {
            UpdatePwmPulseStatus(offset, hardwareFifo);
            UpdatePwmMonoStatus();
            return;
        }

        if (audioFifo.Count >= 4096)
        {
            audioFifo.Dequeue();
        }

        audioFifo.Enqueue(sample);
        UpdatePwmPulseStatus(offset, hardwareFifo);
        UpdatePwmMonoStatus();
    }

    private void PushMonoPwm(ushort value)
    {
        ushort sample = (ushort)(value & 0x0FFF);
        if (_pwmLeftHardwareFifo.Count >= PwmHardwareFifoCapacity ||
            _pwmRightHardwareFifo.Count >= PwmHardwareFifoCapacity)
        {
            UpdatePwmMonoStatus();
            return;
        }

        if (_pwmMono.Count >= 4096)
        {
            _pwmMono.Dequeue();
        }

        _pwmMono.Enqueue(sample);
        _ = PushPwmHardwareSample(_pwmLeftHardwareFifo, sample);
        _ = PushPwmHardwareSample(_pwmRightHardwareFifo, sample);
        WriteBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.PwmMonoPulseWidthOffset, sample);
        UpdatePwmPulseStatus(ThirtyTwoXHardwareProfile.PwmLeftPulseWidthOffset, _pwmLeftHardwareFifo);
        UpdatePwmPulseStatus(ThirtyTwoXHardwareProfile.PwmRightPulseWidthOffset, _pwmRightHardwareFifo);
        UpdatePwmMonoStatus();
    }

    private static bool PushPwmHardwareSample(Queue<ushort> hardwareFifo, ushort sample)
    {
        if (hardwareFifo.Count >= PwmHardwareFifoCapacity)
        {
            return false;
        }

        hardwareFifo.Enqueue(sample);
        return true;
    }

    private static void RestorePwm(Queue<ushort> fifo, ushort[] values, int maxCount = 4096)
    {
        foreach (ushort value in values.TakeLast(maxCount))
        {
            fifo.Enqueue((ushort)(value & 0x0FFF));
        }
    }

    private void AdvancePwmTimer(int sh2Cycles)
    {
        ushort control = ReadBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.PwmControlOffset);
        if ((control & PwmRoutingEnabledMask) == 0)
        {
            return;
        }

        int cycleValue = ReadBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.PwmCycleOffset) & 0x0FFF;
        if (cycleValue == 1)
        {
            return;
        }

        int cycle = cycleValue == 0 ? 0x0FFF : cycleValue - 1;
        int timerInterval = DecodePwmTimerInterval(control);
        if (_pwmCycleCounter <= 0)
        {
            _pwmCycleCounter = cycle;
        }

        _pwmCycleCounter -= sh2Cycles;
        while (_pwmCycleCounter <= 0)
        {
            _pwmCycleCounter += cycle;
            ConsumePwmHardwareFifos(control);
            if (_pwmTimerCounter <= 0)
            {
                _pwmTimerCounter = timerInterval;
            }

            _pwmTimerCounter--;
            if (_pwmTimerCounter <= 0)
            {
                _masterPwmInterruptPending = true;
                _slavePwmInterruptPending = true;
            }
        }

        RequestPendingInterrupts();
    }

    private bool IsPwmTimerActive()
    {
        ushort control = ReadBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.PwmControlOffset);
        return (control & PwmRoutingEnabledMask) != 0;
    }

    private static int DecodePwmTimerInterval(ushort control)
    {
        int timer = (control >> 8) & 0x0F;
        return timer == 0 ? 16 : timer;
    }

    private void ResetPwmTimerCounters()
    {
        _pwmCycleCounter = 0;
        _pwmTimerCounter = 0;
    }

    private void ConsumePwmHardwareFifos(ushort control)
    {
        switch (control & 0x0003)
        {
            case 1:
                PopPwmHardwareFifo(_pwmLeftHardwareFifo, ThirtyTwoXHardwareProfile.PwmLeftPulseWidthOffset);
                break;
            case 2:
                PopPwmHardwareFifo(_pwmRightHardwareFifo, ThirtyTwoXHardwareProfile.PwmRightPulseWidthOffset);
                break;
        }

        switch ((control >> 2) & 0x0003)
        {
            case 1:
                PopPwmHardwareFifo(_pwmRightHardwareFifo, ThirtyTwoXHardwareProfile.PwmRightPulseWidthOffset);
                break;
            case 2:
                PopPwmHardwareFifo(_pwmLeftHardwareFifo, ThirtyTwoXHardwareProfile.PwmLeftPulseWidthOffset);
                break;
        }

        UpdatePwmMonoStatus();
    }

    private void PopPwmHardwareFifo(Queue<ushort> fifo, ushort offset)
    {
        if (fifo.Count > 0)
        {
            fifo.Dequeue();
        }

        UpdatePwmPulseStatus(offset, fifo);
        UpdatePwmMonoStatus();
    }

    private ushort ReadPwmPulseStatus(ushort offset)
    {
        if (offset == ThirtyTwoXHardwareProfile.PwmMonoPulseWidthOffset)
        {
            ushort monoSample = _pwmMono.Count > 0
                ? _pwmMono.Last()
                : (ushort)(ReadBigEndianWord(_systemRegisters, offset) & 0x0FFF);
            ushort monoStatus = 0;
            if (_pwmLeftHardwareFifo.Count == 0 && _pwmRightHardwareFifo.Count == 0)
            {
                monoStatus |= PwmFifoEmpty;
            }

            if (_pwmLeftHardwareFifo.Count >= PwmHardwareFifoCapacity || _pwmRightHardwareFifo.Count >= PwmHardwareFifoCapacity)
            {
                monoStatus |= PwmFifoFull;
            }

            return (ushort)(monoStatus | monoSample);
        }

        Queue<ushort> fifo = PwmHardwareFifoForOffset(offset);
        ushort sample = fifo.Count > 0 ? fifo.Peek() : (ushort)0;
        ushort status = 0;
        if (fifo.Count == 0)
        {
            status |= PwmFifoEmpty;
        }

        if (fifo.Count >= PwmHardwareFifoCapacity)
        {
            status |= PwmFifoFull;
        }

        return (ushort)(status | (sample & 0x0FFF));
    }

    private void UpdatePwmPulseStatus(ushort offset, Queue<ushort> fifo)
    {
        WriteBigEndianWord(_systemRegisters, offset, ReadPwmPulseStatus(offset));
    }

    private void UpdatePwmMonoStatus()
    {
        WriteBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.PwmMonoPulseWidthOffset, ReadPwmPulseStatus(ThirtyTwoXHardwareProfile.PwmMonoPulseWidthOffset));
    }

    private Queue<ushort> PwmHardwareFifoForOffset(ushort offset)
    {
        return offset switch
        {
            ThirtyTwoXHardwareProfile.PwmLeftPulseWidthOffset => _pwmLeftHardwareFifo,
            ThirtyTwoXHardwareProfile.PwmRightPulseWidthOffset => _pwmRightHardwareFifo,
            _ => throw new ArgumentOutOfRangeException(nameof(offset), offset, "PWM mono status is derived from the left/right FIFOs."),
        };
    }

    private static bool IsPwmPulseWidthOffset(ushort offset)
    {
        return offset is ThirtyTwoXHardwareProfile.PwmLeftPulseWidthOffset or
            ThirtyTwoXHardwareProfile.PwmRightPulseWidthOffset or
            ThirtyTwoXHardwareProfile.PwmMonoPulseWidthOffset;
    }

    private int ReadLeftPwmRoute()
    {
        return ReadBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.PwmControlOffset) & 0x0003;
    }

    private int ReadRightPwmRoute()
    {
        return (ReadBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.PwmControlOffset) >> 2) & 0x0003;
    }

    private static void MixRoutedPwmChannel(ReadOnlySpan<short> source, Span<short> output, int samples, int route)
    {
        int channelOffset = route switch
        {
            1 => 0,
            2 => 1,
            _ => -1,
        };
        if (channelOffset < 0)
        {
            return;
        }

        for (int i = 0; i < samples; i++)
        {
            int offset = (i * 2) + channelOffset;
            output[offset] = SaturatingAdd(output[offset], source[i]);
        }
    }

    private static void RenderPwmChannel(Queue<ushort> fifo, Span<short> output, int samples, int channelOffset, ushort cycle, ref double lastLevel)
    {
        int events = fifo.Count;
        if (events == 0)
        {
            short held = ScalePwmLevel(lastLevel);
            for (int i = 0; i < samples; i++)
            {
                output[(i * 2) + channelOffset] = held;
            }

            return;
        }

        for (int i = 0; i < samples; i++)
        {
            int targetEvents = ((i + 1) * events) / samples;
            while (fifo.Count > events - targetEvents)
            {
                lastLevel = DecodePwmPulse(fifo.Dequeue(), cycle);
            }

            output[(i * 2) + channelOffset] = ScalePwmLevel(lastLevel);
        }
    }

    private static void RenderPwmChannelMono(Queue<ushort> fifo, Span<short> output, int samples, ushort cycle, ref double lastLevel)
    {
        int events = fifo.Count;
        if (events == 0)
        {
            short held = ScalePwmLevel(lastLevel);
            output.Fill(held);
            return;
        }

        for (int i = 0; i < samples; i++)
        {
            int targetEvents = ((i + 1) * events) / samples;
            while (fifo.Count > events - targetEvents)
            {
                lastLevel = DecodePwmPulse(fifo.Dequeue(), cycle);
            }

            output[i] = ScalePwmLevel(lastLevel);
        }
    }

    private static void RenderPwmMono(Queue<ushort> fifo, Span<short> output, int samples, ushort cycle, ref double lastLevel)
    {
        int events = fifo.Count;
        for (int i = 0; i < samples; i++)
        {
            int targetEvents = events == 0 ? 0 : ((i + 1) * events) / samples;
            while (fifo.Count > events - targetEvents)
            {
                lastLevel = DecodePwmPulse(fifo.Dequeue(), cycle);
            }

            short mono = ScalePwmLevel(lastLevel);
            int offset = i * 2;
            output[offset] = SaturatingAdd(output[offset], mono);
            output[offset + 1] = SaturatingAdd(output[offset + 1], mono);
        }
    }

    private static double DecodePwmPulse(ushort pulse, ushort cycle)
    {
        double center = cycle / 2.0;
        if (center <= 0.0)
        {
            return 0.0;
        }

        return Math.Clamp(((pulse & 0x0FFF) - center) / center, -1.0, 1.0);
    }

    private static short ScalePwmLevel(double level)
    {
        return (short)Math.Clamp(level * 8192.0, short.MinValue, short.MaxValue);
    }

    private static short SaturatingAdd(short left, short right)
    {
        return (short)Math.Clamp(left + right, short.MinValue, short.MaxValue);
    }

    private ushort BuildDreqControlStatus(bool sh2View)
    {
        ushort control = ReadBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.DreqControlOffset);
        ushort status = (ushort)(control & (DreqControlRomToVramDma | DreqControlDma | DreqControlActive));
        if (_dreqFifo.Count >= DreqFifoCapacity)
        {
            status |= sh2View ? (ushort)0x8000 : (ushort)0x0080;
        }

        if (sh2View && _dreqFifo.Count == 0)
        {
            status |= 0x4000;
        }

        return status;
    }

    private void PushDreqFifo(ushort value)
    {
        TryRunDreqDma();
        ushort control = ReadBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.DreqControlOffset);
        if ((control & DreqControlActive) == 0)
        {
            return;
        }

        if (_dreqFifo.Count >= DreqFifoCapacity)
        {
            return;
        }

        _dreqFifo.Enqueue(value);
        _dreqFifoWriteCount++;
        ushort length = ReadBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.DreqLengthOffset);
        if (length != 0)
        {
            length--;
            WriteBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.DreqLengthOffset, length);
            if (length == 0)
            {
                WriteBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.DreqControlOffset, (ushort)(control & ~DreqControlActive));
            }
        }

        TryRunDreqDma();
    }

    private uint ReadDreqSourceAddress()
    {
        return ReadBigEndianLong(_systemRegisters, ThirtyTwoXHardwareProfile.DreqSourceAddressOffset) & 0x00FF_FFFE;
    }

    private void WriteDreqSourceAddress(uint address)
    {
        WriteBigEndianLong(_systemRegisters, ThirtyTwoXHardwareProfile.DreqSourceAddressOffset, address & 0x00FF_FFFE);
    }

    private void ClearRomToVramDmaRequest()
    {
        ushort control = ReadBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.DreqControlOffset);
        WriteBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.DreqControlOffset, (ushort)(control & ~DreqControlRomToVramDma));
    }

    private void WriteDreqControl(ushort value, ushort oldControl)
    {
        ushort newControl = (ushort)(value & (DreqControlActive | DreqControlDma | DreqControlRomToVramDma));
        if ((oldControl & DreqControlActive) != 0 && (newControl & DreqControlActive) == 0)
        {
            _dreqFifo.Clear();
        }

        WriteBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.DreqControlOffset, newControl);
    }

    private ushort PeekDreqFifo()
    {
        return _dreqFifo.Count == 0 ? (ushort)0xFFFF : _dreqFifo.Peek();
    }

    private ushort PopDreqFifo()
    {
        return _dreqFifo.Count == 0 ? (ushort)0xFFFF : _dreqFifo.Dequeue();
    }

    private void RestoreDreqFifo(ushort[] values)
    {
        foreach (ushort value in values.TakeLast(DreqFifoCapacity))
        {
            _dreqFifo.Enqueue(value);
        }
    }

    private static bool IsSh2DmaRegisterAddress(uint address)
    {
        return address is >= Sh2DmaRegisterStart and < Sh2DmaRegisterEnd ||
            address is Sh2DmaRequestSelect0 or Sh2DmaRequestSelect1;
    }

    private static bool IsSh2DivisionUnitRegisterAddress(uint address)
    {
        return address is >= Sh2DivisionUnitRegisterStart and < Sh2DivisionUnitRegisterEnd;
    }

    private static bool IsSh2PeripheralRegisterAddress(uint address)
    {
        return address is >= Sh2PeripheralRegisterStart and < Sh2PeripheralRegisterEnd;
    }

    private static bool IsSh2InternalRegisterAddress(uint address)
    {
        return address >= Sh2PeripheralRegisterStart;
    }

    private bool TrySignalSh2InputCapture(uint address, uint value, int sourceCpuIndex)
    {
        int targetCpuIndex;
        if (address is >= Sh2SlaveInputCaptureSignalStart and < Sh2SlaveInputCaptureSignalStart + Sh2InputCaptureSignalBytes)
        {
            targetCpuIndex = 1;
        }
        else if (address is >= Sh2MasterInputCaptureSignalStart and < Sh2MasterInputCaptureSignalStart + Sh2InputCaptureSignalBytes)
        {
            targetCpuIndex = 0;
        }
        else
        {
            return false;
        }

        SignalSh2InputCapture(targetCpuIndex);
        TraceSh2MemoryAccess(sourceCpuIndex, "WIC", address, value);
        return true;
    }

    private void SignalSh2InputCapture(int targetCpuIndex)
    {
        int index = targetCpuIndex & 1;
        byte[] registers = _sh2PeripheralRegisters[index];
        ushort counter = BuildSh2FreeRunningCounter(index);
        registers[(int)(Sh2FrtInputCaptureRegisterStart - Sh2PeripheralRegisterStart)] = (byte)(counter >> 8);
        registers[(int)((Sh2FrtInputCaptureRegisterStart + 1) - Sh2PeripheralRegisterStart)] = (byte)counter;

        int ftcsrIndex = (int)((Sh2FrtRegisterStart + 1) - Sh2PeripheralRegisterStart);
        registers[ftcsrIndex] |= Sh2FrtFtcsrInputCaptureFlag;

        byte tier = registers[(int)(Sh2FrtRegisterStart - Sh2PeripheralRegisterStart)];
        if ((tier & Sh2FrtTierInputCaptureEnable) != 0)
        {
            Sh2Cpu cpu = index == 0 ? MasterSh2 : SlaveSh2;
            RequestSh2FrtInterrupt(cpu, index, GetSh2FrtInputCaptureInterruptVector(index));
        }
    }

    private byte ReadSh2DivisionUnitByte(uint address, int cpuIndex)
    {
        uint value = ReadSh2DivisionUnitLong(address & ~3u, cpuIndex);
        int shift = (3 - (int)(address & 3)) * 8;
        return (byte)(value >> shift);
    }

    private ushort ReadSh2DivisionUnitWord(uint address, int cpuIndex)
    {
        if (address == Sh2DivisionDvcrAddress)
        {
            return (ushort)(_sh2DivisionRegisters[cpuIndex & 1][Sh2DivisionDvcrIndex] & 0x0003u);
        }

        if (address == Sh2DivisionVcrdivAddress)
        {
            return (ushort)(_sh2DivisionRegisters[cpuIndex & 1][Sh2DivisionVcrdivIndex] & 0x007Fu);
        }

        uint value = ReadSh2DivisionUnitLong(address & ~3u, cpuIndex);
        return (ushort)(((address & 2) == 0) ? (value >> 16) : value);
    }

    private uint ReadSh2DivisionUnitLong(uint address, int cpuIndex)
    {
        uint[] registers = _sh2DivisionRegisters[cpuIndex & 1];
        if (address == Sh2DivisionRemainderAliasAddress)
        {
            return registers[Sh2DivisionDvdnthIndex];
        }

        if (address == Sh2DivisionQuotientAliasAddress)
        {
            return registers[Sh2DivisionDvdntlIndex];
        }

        return GetSh2DivisionRegisterIndex(address) switch
        {
            Sh2DivisionDvcrIndex => registers[Sh2DivisionDvcrIndex] & 0x0000_0003u,
            Sh2DivisionVcrdivIndex => registers[Sh2DivisionVcrdivIndex] & 0x0000_007Fu,
            int index when index >= 0 => registers[index],
            _ => 0
        };
    }

    private void WriteSh2DivisionUnitByte(uint address, byte value, int cpuIndex)
    {
        uint alignedAddress = address & ~3u;
        uint previous = ReadSh2DivisionUnitLong(alignedAddress, cpuIndex);
        int shift = (3 - (int)(address & 3)) * 8;
        uint merged = (previous & ~(0xFFu << shift)) | ((uint)value << shift);
        WriteSh2DivisionUnitLong(alignedAddress, merged, cpuIndex);
    }

    private void WriteSh2DivisionUnitWord(uint address, ushort value, int cpuIndex)
    {
        if (address == Sh2DivisionDvcrAddress)
        {
            _sh2DivisionRegisters[cpuIndex & 1][Sh2DivisionDvcrIndex] = value & 0x0003u;
            return;
        }

        if (address == Sh2DivisionVcrdivAddress)
        {
            _sh2DivisionRegisters[cpuIndex & 1][Sh2DivisionVcrdivIndex] = value & 0x007Fu;
            return;
        }

        uint alignedAddress = address & ~3u;
        uint previous = ReadSh2DivisionUnitLong(alignedAddress, cpuIndex);
        uint merged = (address & 2) == 0
            ? (previous & 0x0000_FFFFu) | ((uint)value << 16)
            : (previous & 0xFFFF_0000u) | value;
        WriteSh2DivisionUnitLong(alignedAddress, merged, cpuIndex);
    }

    private void WriteSh2DivisionUnitLong(uint address, uint value, int cpuIndex)
    {
        uint[] registers = _sh2DivisionRegisters[cpuIndex & 1];
        switch (GetSh2DivisionRegisterIndex(address))
        {
            case Sh2DivisionDvsrIndex:
                registers[Sh2DivisionDvsrIndex] = value;
                break;

            case Sh2DivisionDvdntIndex:
                registers[Sh2DivisionDvdntIndex] = value;
                registers[Sh2DivisionDvdntlIndex] = value;
                registers[Sh2DivisionDvdnthIndex] = (value & 0x8000_0000u) == 0 ? 0u : 0xFFFF_FFFFu;
                RunSh2Division(cpuIndex, is64Bit: false);
                break;

            case Sh2DivisionDvcrIndex:
                registers[Sh2DivisionDvcrIndex] = value & 0x0000_0003u;
                break;

            case Sh2DivisionVcrdivIndex:
                registers[Sh2DivisionVcrdivIndex] = value & 0x0000_007Fu;
                break;

            case Sh2DivisionDvdnthIndex:
                registers[Sh2DivisionDvdnthIndex] = value;
                break;

            case Sh2DivisionDvdntlIndex:
                registers[Sh2DivisionDvdntlIndex] = value;
                RunSh2Division(cpuIndex, is64Bit: true);
                break;
        }
    }

    private void RunSh2Division(int cpuIndex, bool is64Bit)
    {
        uint[] registers = _sh2DivisionRegisters[cpuIndex & 1];
        int divisor = unchecked((int)registers[Sh2DivisionDvsrIndex]);
        long dividend = is64Bit
            ? unchecked((long)(((ulong)registers[Sh2DivisionDvdnthIndex] << 32) | registers[Sh2DivisionDvdntlIndex]))
            : unchecked((int)registers[Sh2DivisionDvdntIndex]);

        bool overflow = divisor == 0;
        long quotient = 0;
        long remainder = dividend;
        if (!overflow)
        {
            quotient = dividend / divisor;
            remainder = dividend % divisor;
            overflow = quotient is < int.MinValue or > int.MaxValue;
        }

        if (overflow)
        {
            registers[Sh2DivisionDvcrIndex] |= Sh2DivisionOverflowFlag;
            bool negativeQuotient = divisor != 0 && ((dividend < 0) ^ (divisor < 0));
            uint saturated = negativeQuotient ? 0x8000_0000u : 0x7FFF_FFFFu;
            registers[Sh2DivisionDvdntIndex] = saturated;
            registers[Sh2DivisionDvdntlIndex] = saturated;
            registers[Sh2DivisionDvdnthIndex] = unchecked((uint)remainder);

            if ((registers[Sh2DivisionDvcrIndex] & Sh2DivisionOverflowInterruptEnable) != 0)
            {
                Sh2Cpu cpu = (cpuIndex & 1) == 0 ? MasterSh2 : SlaveSh2;
                cpu.RequestInterrupt(15, (int)(registers[Sh2DivisionVcrdivIndex] & 0x7Fu));
            }
        }
        else
        {
            registers[Sh2DivisionDvdntIndex] = unchecked((uint)(int)quotient);
            registers[Sh2DivisionDvdntlIndex] = unchecked((uint)(int)quotient);
            registers[Sh2DivisionDvdnthIndex] = unchecked((uint)(int)remainder);
        }

        AddSh2WaitCycles(cpuIndex, overflow ? 6 : 39);
    }

    private static int GetSh2DivisionRegisterIndex(uint address)
    {
        return ((address & ~3u) - Sh2DivisionUnitRegisterStart) switch
        {
            0x00 => Sh2DivisionDvsrIndex,
            0x04 => Sh2DivisionDvdntIndex,
            0x08 => Sh2DivisionDvcrIndex,
            0x0C => Sh2DivisionVcrdivIndex,
            0x10 => Sh2DivisionDvdnthIndex,
            0x14 => Sh2DivisionDvdntlIndex,
            _ => -1
        };
    }

    private byte ReadSh2PeripheralByte(uint address, int cpuIndex)
    {
        byte value;
        if (address == Sh2WatchdogRegisterStart)
        {
            value = ReadSh2WatchdogControl(cpuIndex);
            TraceSh2MemoryAccess(cpuIndex, "RP8", address, value);
            return value;
        }

        if (address == Sh2WatchdogCounterAddress)
        {
            value = _sh2PeripheralRegisters[cpuIndex & 1][(int)(Sh2WatchdogCounterAddress - Sh2PeripheralRegisterStart)];
            TraceSh2MemoryAccess(cpuIndex, "RP8", address, value);
            return value;
        }

        if (address == Sh2WatchdogResetControlAddress)
        {
            value = _sh2PeripheralRegisters[cpuIndex & 1][(int)(Sh2WatchdogResetControlAddress - Sh2PeripheralRegisterStart)];
            TraceSh2MemoryAccess(cpuIndex, "RP8", address, value);
            return value;
        }

        if (address is >= Sh2FreeRunningCounterStart and < Sh2FreeRunningCounterStart + 2)
        {
            ushort counter = BuildSh2FreeRunningCounter(cpuIndex);
            value = (address & 1) == 0 ? (byte)(counter >> 8) : (byte)counter;
            TraceSh2MemoryAccess(cpuIndex, "RP8", address, value);
            return value;
        }

        if (address is >= Sh2FrtOutputCompareRegisterStart and < Sh2FrtOutputCompareRegisterStart + 2 &&
            IsSh2FrtOutputCompareBSelected(cpuIndex))
        {
            ushort ocrb = _sh2FrtOutputCompareB[cpuIndex & 1];
            value = (address & 1) == 0 ? (byte)(ocrb >> 8) : (byte)ocrb;
            TraceSh2MemoryAccess(cpuIndex, "RP8", address, value);
            return value;
        }

        value = _sh2PeripheralRegisters[cpuIndex & 1][(int)(address - Sh2PeripheralRegisterStart)];
        TraceSh2MemoryAccess(cpuIndex, "RP8", address, value);
        return value;
    }

    private void WriteSh2PeripheralByte(uint address, byte value, int cpuIndex)
    {
        int index = (int)(address - Sh2PeripheralRegisterStart);
        byte[] registers = _sh2PeripheralRegisters[cpuIndex & 1];
        if (address == Sh2WatchdogRegisterStart)
        {
            _sh2WatchdogWriteSelect[cpuIndex & 1] = value;
            TraceSh2MemoryAccess(cpuIndex, "WP8", address, registers[index]);
            return;
        }

        if (address == Sh2WatchdogCounterAddress)
        {
            byte select = _sh2WatchdogWriteSelect[cpuIndex & 1];
            if (select == Sh2WatchdogWriteCounterKey)
            {
                WriteSh2WatchdogCounter(value, cpuIndex);
            }
            else if (select == Sh2WatchdogWriteControlKey)
            {
                WriteSh2WatchdogControl(value, cpuIndex);
            }

            TraceSh2MemoryAccess(cpuIndex, "WP8", address, value);
            return;
        }

        if (address == Sh2WatchdogResetControlAddress)
        {
            registers[index] = value;
            TraceSh2MemoryAccess(cpuIndex, "WP8", address, value);
            return;
        }

        if (address == Sh2CacheControlRegisterAddress)
        {
            if ((value & 0x08) != 0)
            {
                PurgeSh2CacheWays(cpuIndex, startWay: 2, ways: 2);
            }

            if ((value & Sh2CacheControlPurge) != 0)
            {
                PurgeSh2CacheWays(cpuIndex, startWay: 0, ways: 4);
            }

            registers[index] = (byte)(value & ~Sh2CacheControlPurge);
            TraceSh2MemoryAccess(cpuIndex, "WP8", address, registers[index]);
            return;
        }

        if (address == Sh2FrtRegisterStart)
        {
            registers[index] = (byte)(value | 0x01);
            TraceSh2MemoryAccess(cpuIndex, "WP8", address, registers[index]);
            return;
        }

        if (address == Sh2SciTransmitDataRegisterAddress)
        {
            registers[index] = value;
            TransferSh2SciByte(cpuIndex, value);
            TraceSh2MemoryAccess(cpuIndex, "WP8", address, value);
            return;
        }

        if (address == Sh2SciStatusRegisterAddress)
        {
            registers[index] = (byte)((registers[index] & value & (Sh2SciStatusTransmitDataEmpty | Sh2SciStatusReceiveDataFull | Sh2SciStatusTransmitEnd)) |
                Sh2SciStatusTransmitDataEmpty |
                Sh2SciStatusTransmitEnd);
            TraceSh2MemoryAccess(cpuIndex, "WP8", address, registers[index]);
            return;
        }

        if (address == Sh2FrtRegisterStart + 1)
        {
            registers[index] = (byte)((registers[index] & value & Sh2FrtFtcsrWritableMask) | (value & 0x01));
            TraceSh2MemoryAccess(cpuIndex, "WP8", address, registers[index]);
            return;
        }

        if (address is >= Sh2FreeRunningCounterStart and < Sh2FreeRunningCounterStart + 2)
        {
            WriteSh2FreeRunningCounterByte(address, value, cpuIndex);
            TraceSh2MemoryAccess(cpuIndex, "WP8", address, value);
            return;
        }

        if (address is >= Sh2FrtOutputCompareRegisterStart and < Sh2FrtOutputCompareRegisterStart + 2 &&
            IsSh2FrtOutputCompareBSelected(cpuIndex))
        {
            WriteSh2FrtOutputCompareBByte(address, value, cpuIndex);
            TraceSh2MemoryAccess(cpuIndex, "WP8", address, value);
            return;
        }

        if (address == Sh2FrtControlRegisterAddress)
        {
            LatchSh2FreeRunningCounter(cpuIndex);
            registers[index] = (byte)(value & 0x83);
            TraceSh2MemoryAccess(cpuIndex, "WP8", address, registers[index]);
            return;
        }

        if (address == Sh2FrtRegisterStart + 7)
        {
            registers[index] = (byte)(value | 0xE0);
            TraceSh2MemoryAccess(cpuIndex, "WP8", address, registers[index]);
            return;
        }

        registers[index] = value;
        TraceSh2MemoryAccess(cpuIndex, "WP8", address, value);
    }

    private void WriteSh2PeripheralWord(uint address, ushort value, int cpuIndex)
    {
        if (address == Sh2WatchdogRegisterStart)
        {
            WriteSh2WatchdogWord(value, cpuIndex);
            return;
        }

        WriteSh2PeripheralByte(address, (byte)(value >> 8), cpuIndex);
        WriteSh2PeripheralByte(address + 1, (byte)value, cpuIndex);
    }

    private void TransferSh2SciByte(int sourceCpuIndex, byte value)
    {
        int source = sourceCpuIndex & 1;
        int target = source ^ 1;
        byte[] sourceRegisters = _sh2PeripheralRegisters[source];
        byte[] targetRegisters = _sh2PeripheralRegisters[target];
        int sourceStatusIndex = (int)(Sh2SciStatusRegisterAddress - Sh2PeripheralRegisterStart);
        int targetStatusIndex = sourceStatusIndex;
        int targetReceiveIndex = (int)(Sh2SciReceiveDataRegisterAddress - Sh2PeripheralRegisterStart);

        sourceRegisters[sourceStatusIndex] |= Sh2SciStatusTransmitDataEmpty | Sh2SciStatusTransmitEnd;
        targetRegisters[targetReceiveIndex] = value;
        targetRegisters[targetStatusIndex] |= Sh2SciStatusReceiveDataFull;
    }

    private void WriteSh2WatchdogWord(ushort value, int cpuIndex)
    {
        byte key = (byte)(value >> 8);
        byte data = (byte)value;
        if (key == Sh2WatchdogWriteCounterKey)
        {
            WriteSh2WatchdogCounter(data, cpuIndex);
        }
        else if (key == Sh2WatchdogWriteControlKey)
        {
            WriteSh2WatchdogControl(data, cpuIndex);
        }

        TraceSh2MemoryAccess(cpuIndex, "WP16", Sh2WatchdogRegisterStart, value);
    }

    private byte ReadSh2WatchdogControl(int cpuIndex)
    {
        byte value = _sh2PeripheralRegisters[cpuIndex & 1][(int)(Sh2WatchdogRegisterStart - Sh2PeripheralRegisterStart)];
        return (byte)(value & (Sh2WatchdogOverflow | Sh2WatchdogModeWatchdog | Sh2WatchdogTimerEnable));
    }

    private void WriteSh2WatchdogCounter(byte value, int cpuIndex)
    {
        int index = cpuIndex & 1;
        _sh2PeripheralRegisters[index][(int)(Sh2WatchdogCounterAddress - Sh2PeripheralRegisterStart)] = value;
        _sh2WatchdogCycleCounters[index] = 0;
    }

    private void WriteSh2WatchdogControl(byte value, int cpuIndex)
    {
        int index = cpuIndex & 1;
        byte[] registers = _sh2PeripheralRegisters[index];
        int wtcsr = (int)(Sh2WatchdogRegisterStart - Sh2PeripheralRegisterStart);
        byte old = registers[wtcsr];
        byte next = (byte)(value & 0x7F);
        if ((old & Sh2WatchdogOverflow) != 0 && (value & Sh2WatchdogOverflow) != 0)
        {
            next |= Sh2WatchdogOverflow;
        }

        registers[wtcsr] = next;
        if ((next & Sh2WatchdogTimerEnable) == 0)
        {
            registers[(int)(Sh2WatchdogCounterAddress - Sh2PeripheralRegisterStart)] = 0;
            _sh2WatchdogCycleCounters[index] = 0;
        }

        if ((next & Sh2WatchdogOverflow) == 0)
        {
            _sh2WatchdogInterruptPending[index] = false;
        }
    }

    private void AdvanceSh2InternalTimers(int cpuIndex, int cycles)
    {
        AdvanceSh2Watchdog(cpuIndex, cycles);
        AdvanceSh2FreeRunningTimer(cpuIndex, cycles);
    }

    private void AdvanceSh2Watchdog(int cpuIndex, int cycles)
    {
        if (cycles <= 0)
        {
            return;
        }

        int index = cpuIndex & 1;
        byte[] registers = _sh2PeripheralRegisters[index];
        int wtcsrIndex = (int)(Sh2WatchdogRegisterStart - Sh2PeripheralRegisterStart);
        byte wtcsr = registers[wtcsrIndex];
        if ((wtcsr & Sh2WatchdogTimerEnable) == 0 || (wtcsr & Sh2WatchdogModeWatchdog) != 0)
        {
            return;
        }

        int divider = Sh2WatchdogDividers[wtcsr & 0x07];
        _sh2WatchdogCycleCounters[index] += cycles;
        int increments = _sh2WatchdogCycleCounters[index] / divider;
        if (increments <= 0)
        {
            return;
        }

        _sh2WatchdogCycleCounters[index] %= divider;
        int wtcntIndex = (int)(Sh2WatchdogCounterAddress - Sh2PeripheralRegisterStart);
        byte previous = registers[wtcntIndex];
        int nextValue = previous + increments;
        registers[wtcntIndex] = (byte)nextValue;
        if (nextValue > 0xFF)
        {
            registers[wtcsrIndex] = (byte)(registers[wtcsrIndex] | Sh2WatchdogOverflow);
            _sh2WatchdogInterruptPending[index] = true;
        }
    }

    private int GetSh2WatchdogInterruptPriority(int cpuIndex)
    {
        byte ipraHigh = _sh2PeripheralRegisters[cpuIndex & 1][(int)(Sh2InterruptPriorityRegisterAHighAddress - Sh2PeripheralRegisterStart)];
        return (ipraHigh >> 4) & 0x0F;
    }

    private int GetSh2WatchdogInterruptVector(int cpuIndex)
    {
        byte vector = _sh2PeripheralRegisters[cpuIndex & 1][(int)(Sh2WatchdogVectorAddress - Sh2PeripheralRegisterStart)];
        return vector & 0x7F;
    }

    private int GetSh2FrtInterruptPriority(int cpuIndex)
    {
        byte iprbHigh = _sh2PeripheralRegisters[cpuIndex & 1][(int)(Sh2InterruptPriorityRegisterBAddress - Sh2PeripheralRegisterStart)];
        int priority = iprbHigh & 0x0F;
        return priority == 0 ? Sh2FrtDefaultInterruptLevel : priority;
    }

    private int GetSh2FrtInputCaptureInterruptVector(int cpuIndex)
    {
        byte vcrcHigh = _sh2PeripheralRegisters[cpuIndex & 1][(int)(Sh2FrtVectorRegisterCAddress - Sh2PeripheralRegisterStart)];
        int vector = vcrcHigh & 0x7F;
        return vector == 0 ? Sh2FrtInputCaptureInterruptVector : vector;
    }

    private int GetSh2FrtOutputCompareInterruptVector(int cpuIndex)
    {
        byte vcrcLow = _sh2PeripheralRegisters[cpuIndex & 1][(int)((Sh2FrtVectorRegisterCAddress + 1) - Sh2PeripheralRegisterStart)];
        int vector = vcrcLow & 0x7F;
        return vector == 0 ? Sh2FrtOutputCompareInterruptVector : vector;
    }

    private int GetSh2FrtOverflowInterruptVector(int cpuIndex)
    {
        byte vcrdHigh = _sh2PeripheralRegisters[cpuIndex & 1][(int)(Sh2FrtVectorRegisterDAddress - Sh2PeripheralRegisterStart)];
        int vector = vcrdHigh & 0x7F;
        return vector == 0 ? Sh2FrtOverflowInterruptVector : vector;
    }

    private void RequestSh2FrtInterrupt(Sh2Cpu cpu, int cpuIndex, int vector)
    {
        cpu.RequestInterrupt(GetSh2FrtInterruptPriority(cpuIndex), vector);
    }

    private void WriteSh2FreeRunningCounterByte(uint address, byte value, int cpuIndex)
    {
        int index = cpuIndex & 1;
        ushort current = BuildSh2FreeRunningCounter(index);
        ushort next = (address & 1) == 0
            ? (ushort)((current & 0x00FF) | (value << 8))
            : (ushort)((current & 0xFF00) | value);
        SetSh2FreeRunningCounter(index, next);
    }

    private void WriteSh2FrtOutputCompareBByte(uint address, byte value, int cpuIndex)
    {
        int index = cpuIndex & 1;
        ushort current = _sh2FrtOutputCompareB[index];
        _sh2FrtOutputCompareB[index] = (address & 1) == 0
            ? (ushort)((current & 0x00FF) | (value << 8))
            : (ushort)((current & 0xFF00) | value);
    }

    private bool IsSh2FrtOutputCompareBSelected(int cpuIndex)
    {
        byte tocr = _sh2PeripheralRegisters[cpuIndex & 1][(int)((Sh2FrtRegisterStart + 7) - Sh2PeripheralRegisterStart)];
        return (tocr & 0x10) != 0;
    }

    private void LatchSh2FreeRunningCounter(int cpuIndex)
    {
        int index = cpuIndex & 1;
        SetSh2FreeRunningCounter(index, BuildSh2FreeRunningCounter(index));
    }

    private void SetSh2FreeRunningCounter(int cpuIndex, ushort counter)
    {
        int index = cpuIndex & 1;
        _sh2FrtBaseCounters[index] = counter;
        _sh2FrtLastCounters[index] = counter;
        _sh2FrtBaseCycles[index] = GetSh2Cycles(index);
    }

    private void ResetSh2FrtBaseCycles()
    {
        for (int cpu = 0; cpu < ThirtyTwoXHardwareProfile.Sh2CpuCount; cpu++)
        {
            _sh2FrtBaseCycles[cpu] = GetSh2Cycles(cpu);
            _sh2FrtLastCounters[cpu] = _sh2FrtBaseCounters[cpu];
        }
    }

    private void AdvanceSh2FreeRunningTimer(int cpuIndex, int cycles)
    {
        if (cycles <= 0)
        {
            return;
        }

        int index = cpuIndex & 1;
        int divider = GetSh2FreeRunningCounterDivider(index);
        if (divider == int.MaxValue)
        {
            _sh2FrtLastCounters[index] = BuildSh2FreeRunningCounter(index);
            return;
        }

        ushort previous = _sh2FrtLastCounters[index];
        ushort current = BuildSh2FreeRunningCounter(index);
        _sh2FrtLastCounters[index] = current;
        if (current == previous)
        {
            return;
        }

        byte[] registers = _sh2PeripheralRegisters[index];
        int ftcsrIndex = (int)(Sh2FrtControlStatusRegisterAddress - Sh2PeripheralRegisterStart);
        byte flags = 0;
        ushort ocra = ReadBigEndianWord(registers, (int)(Sh2FrtOutputCompareRegisterStart - Sh2PeripheralRegisterStart));
        ushort ocrb = _sh2FrtOutputCompareB[index];
        if (CounterRangeIncludes(previous, current, ocra))
        {
            flags |= Sh2FrtFtcsrOutputCompareAFlag;
        }

        if (CounterRangeIncludes(previous, current, ocrb))
        {
            flags |= Sh2FrtFtcsrOutputCompareBFlag;
        }

        if (current < previous)
        {
            flags |= Sh2FrtFtcsrOverflowFlag;
        }

        if (flags == 0)
        {
            return;
        }

        registers[ftcsrIndex] |= flags;
        byte tier = registers[(int)(Sh2FrtRegisterStart - Sh2PeripheralRegisterStart)];
        Sh2Cpu cpu = index == 0 ? MasterSh2 : SlaveSh2;
        if ((flags & Sh2FrtFtcsrOutputCompareAFlag) != 0 && (tier & Sh2FrtTierOutputCompareAEnable) != 0)
        {
            RequestSh2FrtInterrupt(cpu, index, GetSh2FrtOutputCompareInterruptVector(index));
        }

        if ((flags & Sh2FrtFtcsrOutputCompareBFlag) != 0 && (tier & Sh2FrtTierOutputCompareBEnable) != 0)
        {
            RequestSh2FrtInterrupt(cpu, index, GetSh2FrtOutputCompareInterruptVector(index));
        }

        if ((flags & Sh2FrtFtcsrOverflowFlag) != 0 && (tier & Sh2FrtTierOverflowEnable) != 0)
        {
            RequestSh2FrtInterrupt(cpu, index, GetSh2FrtOverflowInterruptVector(index));
        }
    }

    private static bool CounterRangeIncludes(ushort previous, ushort current, ushort target)
    {
        if (current > previous)
        {
            return target > previous && target <= current;
        }

        if (current < previous)
        {
            return target > previous || target <= current;
        }

        return false;
    }

    private ushort BuildSh2FreeRunningCounter(int cpuIndex)
    {
        int index = cpuIndex & 1;
        int divider = GetSh2FreeRunningCounterDivider(index);
        if (divider == int.MaxValue)
        {
            return _sh2FrtBaseCounters[index];
        }

        long elapsedCycles = Math.Max(0, GetSh2Cycles(index) - _sh2FrtBaseCycles[index]);
        return (ushort)(_sh2FrtBaseCounters[index] + (elapsedCycles / divider));
    }

    private int GetSh2FreeRunningCounterDivider(int cpuIndex)
    {
        byte tcr = _sh2PeripheralRegisters[cpuIndex & 1][(int)(Sh2FrtControlRegisterAddress - Sh2PeripheralRegisterStart)];
        return (tcr & 0x03) switch
        {
            0x00 => 8,
            0x01 => 32,
            0x02 => 128,
            _ => int.MaxValue,
        };
    }

    private long GetSh2Cycles(int cpuIndex)
    {
        return (cpuIndex & 1) == 0 ? MasterSh2.Cycles : SlaveSh2.Cycles;
    }

    private void ResetSh2PeripheralDefaults()
    {
        for (int cpu = 0; cpu < _sh2PeripheralRegisters.Length; cpu++)
        {
            byte[] registers = _sh2PeripheralRegisters[cpu];
            registers[(int)(Sh2FrtRegisterStart - Sh2PeripheralRegisterStart)] = 0x01;
            registers[(int)((Sh2FrtRegisterStart + 4) - Sh2PeripheralRegisterStart)] = 0xFF;
            registers[(int)((Sh2FrtRegisterStart + 5) - Sh2PeripheralRegisterStart)] = 0xFF;
            registers[(int)((Sh2FrtRegisterStart + 7) - Sh2PeripheralRegisterStart)] = 0xE0;
            registers[(int)(Sh2SciStatusRegisterAddress - Sh2PeripheralRegisterStart)] = Sh2SciStatusTransmitDataEmpty | Sh2SciStatusTransmitEnd;
            _sh2FrtOutputCompareB[cpu] = 0xFFFF;
            registers[(int)(Sh2CacheControlRegisterAddress - Sh2PeripheralRegisterStart)] = 0x01;
            registers[(int)(Sh2WatchdogRegisterStart - Sh2PeripheralRegisterStart)] = Sh2WatchdogControlInitial;
            registers[(int)(Sh2WatchdogCounterAddress - Sh2PeripheralRegisterStart)] = 0x00;
            registers[(int)(Sh2WatchdogResetControlAddress - Sh2PeripheralRegisterStart)] = Sh2WatchdogResetControlInitial;
            SetSh2FreeRunningCounter(cpu, 0);
        }
    }

    private void ResetSh2DmaDefaults()
    {
        for (int cpu = 0; cpu < _sh2DmaRegisters.Length; cpu++)
        {
            byte[] registers = _sh2DmaRegisters[cpu];
            for (int channel = 0; channel < 2; channel++)
            {
                WriteBigEndianLong(
                    registers,
                    (channel * Sh2DmaChannelRegisterStride) + Sh2DmaChannelControl0Offset,
                    Sh2DmaTransferEnd);
            }
        }
    }

    private byte ReadSh2DmaByte(uint address, int cpuIndex)
    {
        byte value;
        if (TryGetSh2DmaRequestSelectIndex(address, out int requestSelectIndex))
        {
            value = _sh2DmaRequestSelect[requestSelectIndex];
            TraceSh2MemoryAccess(cpuIndex, "RD8", address, value);
            return value;
        }

        value = _sh2DmaRegisters[cpuIndex & 1][(int)(address - Sh2DmaRegisterStart)];
        TraceSh2MemoryAccess(cpuIndex, "RD8", address, value);
        return value;
    }

    private void WriteSh2DmaByte(uint address, byte value, int cpuIndex)
    {
        int index = cpuIndex & 1;
        if (TryGetSh2DmaRequestSelectIndex(address, out int requestSelectIndex))
        {
            _sh2DmaRequestSelect[requestSelectIndex] = (byte)(value & 0x03);
        }
        else
        {
            int offset = (int)(address - Sh2DmaRegisterStart);
            int channel = GetSh2DmaChannelControlWriteChannel(offset);
            uint oldChannelControl = channel >= 0
                ? ReadBigEndianLong(_sh2DmaRegisters[index], (channel * Sh2DmaChannelRegisterStride) + Sh2DmaChannelControl0Offset)
                : 0;
            _sh2DmaRegisters[index][offset] = value;
            if (channel >= 0)
            {
                uint newChannelControl = ReadBigEndianLong(_sh2DmaRegisters[index], (channel * Sh2DmaChannelRegisterStride) + Sh2DmaChannelControl0Offset);
                UpdateSh2DmaInterruptAfterControlWrite(cpuIndex, _sh2DmaRegisters[index], channel, oldChannelControl, newChannelControl);
            }
        }

        TraceSh2MemoryAccess(cpuIndex, "WD8", address, value);
        TryRunDreqDma();
    }

    private static bool TryGetSh2DmaRequestSelectIndex(uint address, out int index)
    {
        if (address == Sh2DmaRequestSelect0)
        {
            index = 0;
            return true;
        }

        if (address == Sh2DmaRequestSelect1)
        {
            index = 1;
            return true;
        }

        index = 0;
        return false;
    }

    private static int GetSh2DmaChannelControlWriteChannel(int offset)
    {
        for (int channel = 0; channel < 2; channel++)
        {
            int start = (channel * Sh2DmaChannelRegisterStride) + Sh2DmaChannelControl0Offset;
            if (offset >= start && offset < start + 4)
            {
                return channel;
            }
        }

        return -1;
    }

    private void UpdateSh2DmaInterruptAfterControlWrite(int cpuIndex, byte[] registers, int channel, uint oldChannelControl, uint newChannelControl)
    {
        bool oldTransferEnd = (oldChannelControl & Sh2DmaTransferEnd) != 0;
        bool newTransferEnd = (newChannelControl & Sh2DmaTransferEnd) != 0;
        int vector = GetSh2DmaInterruptVector(registers, channel);
        int priority = GetSh2DmaInterruptPriority(cpuIndex);
        if (oldTransferEnd && !newTransferEnd && priority > 0)
        {
            (cpuIndex == 0 ? MasterSh2 : SlaveSh2).ClearPendingInterrupt(priority, vector);
        }

        if (newTransferEnd)
        {
            RequestSh2DmaInterruptIfEnabled(cpuIndex, registers, channel, newChannelControl);
        }
    }

    private void TryRunDreqDma()
    {
        if (_runningSh2Dma)
        {
            return;
        }

        _runningSh2Dma = true;
        try
        {
            TryRunDreqDma(cpuIndex: 0);
            TryRunDreqDma(cpuIndex: 1);
        }
        finally
        {
            _runningSh2Dma = false;
        }
    }

    private void TryRunDreqDma(int cpuIndex)
    {
        byte[] registers = _sh2DmaRegisters[cpuIndex & 1];
        uint dmaOperation = ReadBigEndianLong(registers, Sh2DmaOperationOffset);
        if ((dmaOperation & Sh2DmaOperationEnable) == 0)
        {
            return;
        }

        for (int channel = 0; channel < 2; channel++)
        {
            TryRunSh2DmaChannel(cpuIndex, registers, channel);
        }
    }

    private void TryRunSh2DmaChannel(int cpuIndex, byte[] registers, int channel)
    {
        int baseOffset = channel * Sh2DmaChannelRegisterStride;
        uint channelControl = ReadBigEndianLong(registers, baseOffset + Sh2DmaChannelControl0Offset);
        if ((channelControl & Sh2DmaChannelEnable) == 0)
        {
            return;
        }

        uint source = ReadBigEndianLong(registers, baseOffset + Sh2DmaSource0Offset);
        uint destination = ReadBigEndianLong(registers, baseOffset + Sh2DmaDestination0Offset);
        uint count = ReadBigEndianLong(registers, baseOffset + Sh2DmaTransferCount0Offset);
        if (count == 0)
        {
            CompleteSh2DmaChannel(cpuIndex, registers, channel, baseOffset, channelControl);
            return;
        }

        if (IsDreqFifoSource(source))
        {
            if (!CanRunDreqFifoDma(channelControl, channel))
            {
                return;
            }

            RunDreqFifoDma(cpuIndex, registers, baseOffset, channelControl, destination, count);
            return;
        }

        RunSh2MemoryDma(cpuIndex, registers, baseOffset, channelControl, source, destination, count);
    }

    private void RunDreqFifoDma(int cpuIndex, byte[] registers, int baseOffset, uint channelControl, uint destination, uint count)
    {
        int transferred = 0;
        while (count > 0 && _dreqFifo.Count > 0)
        {
            ushort word = _dreqFifo.Dequeue();
            WriteSh2Word(destination, word, cpuIndex);
            destination += 2;
            count--;
            transferred++;
        }

        _dreqDmaWordTransferCount += transferred;
        WriteBigEndianLong(registers, baseOffset + Sh2DmaDestination0Offset, destination);
        WriteBigEndianLong(registers, baseOffset + Sh2DmaTransferCount0Offset, count);
        if (count == 0)
        {
            CompleteSh2DmaChannel(cpuIndex, registers, baseOffset / Sh2DmaChannelRegisterStride, baseOffset, channelControl);
        }
    }

    private void RunSh2MemoryDma(int cpuIndex, byte[] registers, int baseOffset, uint channelControl, uint source, uint destination, uint count)
    {
        int transferSize = GetSh2DmaTransferSize(channelControl);
        uint sourceStep = GetSh2DmaAddressStep(channelControl, source: true, transferSize);
        uint destinationStep = GetSh2DmaAddressStep(channelControl, source: false, transferSize);

        for (uint i = 0; i < count; i++)
        {
            if (transferSize == 1)
            {
                WriteSh2Byte(destination, ReadSh2Byte(source, cpuIndex), cpuIndex);
            }
            else if (transferSize == 2)
            {
                WriteSh2Word(destination, ReadSh2Word(source, cpuIndex), cpuIndex);
            }
            else if (transferSize == 4)
            {
                WriteSh2Long(destination, ReadSh2Long(source, cpuIndex), cpuIndex);
            }
            else
            {
                for (uint offset = 0; offset < 16; offset += 4)
                {
                    WriteSh2Long(destination + offset, ReadSh2Long(source + offset, cpuIndex), cpuIndex);
                }
            }

            source += sourceStep;
            destination += destinationStep;
        }

        WriteBigEndianLong(registers, baseOffset + Sh2DmaSource0Offset, source);
        WriteBigEndianLong(registers, baseOffset + Sh2DmaDestination0Offset, destination);
        WriteBigEndianLong(registers, baseOffset + Sh2DmaTransferCount0Offset, 0);
        CompleteSh2DmaChannel(cpuIndex, registers, baseOffset / Sh2DmaChannelRegisterStride, baseOffset, channelControl);
    }

    private void CompleteSh2DmaChannel(int cpuIndex, byte[] registers, int channel, int baseOffset, uint channelControl)
    {
        uint completedControl = (channelControl & ~(uint)Sh2DmaChannelEnable) | Sh2DmaTransferEnd;
        WriteBigEndianLong(registers, baseOffset + Sh2DmaChannelControl0Offset, completedControl);
        RequestSh2DmaInterruptIfEnabled(cpuIndex, registers, channel, completedControl);
    }

    private void RequestSh2DmaInterruptIfEnabled(int cpuIndex, byte[] registers, int channel, uint channelControl)
    {
        if ((channelControl & Sh2DmaInterruptEnable) == 0)
        {
            return;
        }

        int priority = GetSh2DmaInterruptPriority(cpuIndex);
        if (priority == 0)
        {
            return;
        }

        int vector = GetSh2DmaInterruptVector(registers, channel);
        (cpuIndex == 0 ? MasterSh2 : SlaveSh2).RequestInterrupt(priority, vector);
    }

    private int GetSh2DmaInterruptPriority(int cpuIndex)
    {
        byte ipraHigh = _sh2PeripheralRegisters[cpuIndex & 1][(int)(Sh2InterruptPriorityRegisterAHighAddress - 1 - Sh2PeripheralRegisterStart)];
        return ipraHigh & 0x0F;
    }

    private static int GetSh2DmaInterruptVector(byte[] registers, int channel)
    {
        int offset = channel == 0 ? Sh2DmaVector0Offset : Sh2DmaVector1Offset;
        return registers[offset + 3] & 0x7F;
    }

    private static int GetSh2DmaTransferSize(uint channelControl)
    {
        return (channelControl & 0x0C00) switch
        {
            0x0400 => 2,
            0x0800 => 4,
            0x0C00 => 16,
            _ => 1
        };
    }

    private static uint GetSh2DmaAddressStep(uint channelControl, bool source, int transferSize)
    {
        uint increment = source ? Sh2DmaSourceIncrement : Sh2DmaDestinationIncrement;
        uint decrement = source ? Sh2DmaSourceDecrement : Sh2DmaDestinationDecrement;
        if ((channelControl & increment) != 0)
        {
            return (uint)transferSize;
        }

        if ((channelControl & decrement) != 0)
        {
            return unchecked((uint)-transferSize);
        }

        return 0;
    }

    private static bool IsDreqFifoSource(uint address)
    {
        return address == ThirtyTwoXHardwareProfile.Sh2SystemRegister(ThirtyTwoXHardwareProfile.DreqFifoOffset) ||
            address == ThirtyTwoXHardwareProfile.Sh2SystemRegisterCachedStart + ThirtyTwoXHardwareProfile.DreqFifoOffset;
    }

    private bool CanRunDreqFifoDma(uint channelControl, int channel)
    {
        return (channelControl & Sh2DmaAutoRequestMode) == 0 &&
            (_sh2DmaRequestSelect[channel & 1] & 0x03) == Sh2DmaRequestSelectDreq;
    }

    private static bool IsBootCommunicationByte(ushort offset)
    {
        int relative = (offset & (SystemRegisterBytes - 1)) - ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        return relative is >= 0 and < 8;
    }

    private static bool TryGetCommunicationByteIndex(ushort offset, out int index)
    {
        int relative = (offset & (SystemRegisterBytes - 1)) - ThirtyTwoXHardwareProfile.CommunicationPortOffset;
        if (relative is >= 0 and < 16)
        {
            index = relative;
            return true;
        }

        index = 0;
        return false;
    }

    private void MarkM68kCommunicationHostByte(ushort offset, byte value)
    {
        if (!TryGetCommunicationByteIndex(offset, out int index))
        {
            return;
        }

        _m68kCommunicationPendingHostBytes[index] = value != 0;
        _m68kCommunicationStaleValid[index] = false;
        _m68kCommunicationStaleWordValid[index >> 1] = false;
        if (value != 0)
        {
            _m68kCommunicationDeferredSh2ClearBytes[index] = false;
        }
    }

    private bool TryProtectM68kPendingHostByteFromSh2Clear(ushort offset, byte value)
    {
        if (value != 0 ||
            !TryGetCommunicationByteIndex(offset, out int index) ||
            index < 14 ||
            !_m68kCommunicationPendingHostBytes[index])
        {
            return false;
        }

        _m68kCommunicationPendingHostBytes[index] = false;
        bool protect = _systemRegisters[ThirtyTwoXHardwareProfile.CommunicationPortOffset + index] != 0;
        if (protect)
        {
            _m68kCommunicationDeferredSh2ClearBytes[index] = true;
        }

        return protect;
    }

    private void TrySeedDualSh2WorkerSemaphore(ushort offset, byte previousValue, byte value, int cpuIndex)
    {
        if (cpuIndex != 0 ||
            offset != ThirtyTwoXHardwareProfile.CommunicationPortOffset ||
            previousValue == 0 ||
            value != 0 ||
            !TryReadDualSh2WorkerWrapper(MasterSh2.R[13], out ushort slaveReadyValue, out ushort masterReadyValue) ||
            !TryNormalizeSdramOffset(ReadBigEndianLong(_systemRegisters, ThirtyTwoXHardwareProfile.CommunicationPortOffset + 8), out int destinationOffset) ||
            destinationOffset + 3 >= ThirtyTwoXHardwareProfile.SdramBytes ||
            ReadBigEndianWord(_sdram, destinationOffset) != 0 ||
            ReadBigEndianWord(_sdram, destinationOffset + 2) != 0)
        {
            return;
        }

        WriteSdramWordForSemaphore(destinationOffset, slaveReadyValue);
        WriteSdramWordForSemaphore(destinationOffset + 2, masterReadyValue);
    }

    private bool TryReadDualSh2WorkerWrapper(uint handlerAddress, out ushort slaveReadyValue, out ushort masterReadyValue)
    {
        slaveReadyValue = 0;
        masterReadyValue = 0;
        if (!TryNormalizeSdramOffset(handlerAddress, out int offset) ||
            offset + 0x1B >= ThirtyTwoXHardwareProfile.SdramBytes)
        {
            return false;
        }

        if (ReadBigEndianWord(_sdram, offset) != 0xDA05 ||
            ReadBigEndianWord(_sdram, offset + 2) != 0xDB04 ||
            ReadBigEndianWord(_sdram, offset + 4) != 0xD002 ||
            ReadBigEndianWord(_sdram, offset + 6) != 0x4F22 ||
            ReadBigEndianWord(_sdram, offset + 8) != 0x400B ||
            ReadBigEndianWord(_sdram, offset + 10) != 0x0009 ||
            ReadBigEndianWord(_sdram, offset + 12) != 0x000B ||
            ReadBigEndianWord(_sdram, offset + 14) != 0x4F26)
        {
            return false;
        }

        uint queueFunction = ReadBigEndianLong(_sdram, offset + 0x10);
        uint slaveWorker = ReadBigEndianLong(_sdram, offset + 0x14);
        uint queueValue = ReadBigEndianLong(_sdram, offset + 0x18);
        if (queueFunction == 0 ||
            slaveWorker == 0 ||
            (queueValue & 0xFFFF_0000u) != 0 ||
            queueValue == 0)
        {
            return false;
        }

        slaveReadyValue = (ushort)queueValue;
        masterReadyValue = 1;
        return true;
    }

    private static bool TryNormalizeSdramOffset(uint address, out int offset)
    {
        if (address < ThirtyTwoXHardwareProfile.SdramBytes)
        {
            offset = (int)address;
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2SdramStart and < ThirtyTwoXHardwareProfile.Sh2SdramStart + ThirtyTwoXHardwareProfile.SdramBytes)
        {
            offset = (int)(address - ThirtyTwoXHardwareProfile.Sh2SdramStart);
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2SdramCacheThroughStart and < ThirtyTwoXHardwareProfile.Sh2SdramCacheThroughStart + ThirtyTwoXHardwareProfile.SdramBytes)
        {
            offset = (int)(address - ThirtyTwoXHardwareProfile.Sh2SdramCacheThroughStart);
            return true;
        }

        offset = 0;
        return false;
    }

    private void WriteSdramWordForSemaphore(int offset, ushort value)
    {
        _sdram[offset] = (byte)(value >> 8);
        _sdram[offset + 1] = (byte)value;
        UpdateSh2SdramCacheByte(offset, _sdram[offset]);
        UpdateSh2SdramCacheByte(offset + 1, _sdram[offset + 1]);
    }

    private void ApplyDeferredSh2CommunicationClearAfterRead(ushort offset, byte value)
    {
        if (value == 0 ||
            !TryGetCommunicationByteIndex(offset, out int index) ||
            !_m68kCommunicationDeferredSh2ClearBytes[index])
        {
            return;
        }

        _m68kCommunicationDeferredSh2ClearBytes[index] = false;
        _systemRegisters[ThirtyTwoXHardwareProfile.CommunicationPortOffset + index] = 0;
    }

    private void MarkM68kCommunicationStaleByte(ushort offset, byte previousValue, byte value)
    {
        if (!TryGetCommunicationByteIndex(offset, out int index))
        {
            return;
        }

        if (ClearPostStartReadyTokenStale(index, 1))
        {
            return;
        }

        if (!ShouldExposePreviousCommunicationValueToM68k(index, 1))
        {
            _m68kCommunicationStaleValid[index] = false;
            return;
        }

        // 32X communication ports are shared mailbox bytes; Sega documents same-port
        // cross-side read/write timing as undefined. Keep the old byte visible to
        // the 68000 for one read when an SH-2 publishes a new nonzero byte, but do
        // not mutate the shared byte. Limit this compatibility behavior to the
        // BIOS/post-start handshake window; retail games also use these ports for
        // live command arguments and must see the newly published value.
        if (previousValue == 0 && value != 0)
        {
            _m68kCommunicationStaleBytes[index] = previousValue;
            _m68kCommunicationStaleValid[index] = true;
            return;
        }

        if (index < 2 ||
            offset is ThirtyTwoXHardwareProfile.CommunicationPortOffset + 2 or
            ThirtyTwoXHardwareProfile.CommunicationPortOffset + 3 ||
            previousValue != 0 || value == 0)
        {
            _m68kCommunicationStaleValid[index] = false;
            return;
        }

        _m68kCommunicationStaleBytes[index] = previousValue;
        _m68kCommunicationStaleValid[index] = true;
    }

    private void MarkM68kCommunicationStaleWord(ushort offset, ushort previousValue, ushort value)
    {
        if (!TryGetCommunicationByteIndex(offset, out int index) ||
            (index & 1) != 0)
        {
            return;
        }

        if (index == 8 &&
            previousValue != 0 &&
            value == 0)
        {
            _m68kCommunicationStaleWords[index >> 1] = previousValue;
            _m68kCommunicationStaleWordValid[index >> 1] = true;
            return;
        }

        if (previousValue == 0 ||
            value == 0 ||
            previousValue == value)
        {
            if (ClearPostStartReadyTokenStale(index, 2))
            {
                return;
            }

            if (TryGetCommunicationByteIndex(offset, out int clearIndex) && (clearIndex & 1) == 0)
            {
                _m68kCommunicationStaleWordValid[clearIndex >> 1] = false;
                _m68kCommunicationStaleValid[clearIndex] = false;
                _m68kCommunicationStaleValid[clearIndex + 1] = false;
            }

            return;
        }

        if (!ShouldExposePreviousCommunicationValueToM68k(index, 2))
        {
            int clearIndex = index >> 1;
            _m68kCommunicationStaleWordValid[clearIndex] = false;
            _m68kCommunicationStaleValid[index] = false;
            _m68kCommunicationStaleValid[index + 1] = false;
            return;
        }

        int wordIndex = index >> 1;
        _m68kCommunicationStaleWords[wordIndex] = previousValue;
        _m68kCommunicationStaleWordValid[wordIndex] = true;
    }

    private bool ShouldExposePreviousCommunicationValueToM68k(int index, int bytes)
    {
        if (index < 0 || index + bytes > 16)
        {
            return false;
        }

        return _bootRomHandshakePending ||
            _bootRomSignatureReadbackActive ||
            _bootRomPostStartSignaturePending;
    }

    private bool ClearPostStartReadyTokenStale(int index, int bytes)
    {
        if (!_bootRomPostStartSignaturePending ||
            index < 0 ||
            index + bytes > BootRomCommunicationSignature.Length ||
            !HasPostStartReadyToken(index, bytes))
        {
            return false;
        }

        if (RangeOverlaps(index, bytes, 0, 4) && MatchesCommunicationToken(0, (byte)'M', (byte)'R', (byte)'D', (byte)'Y'))
        {
            ClearCommunicationStaleRange(0, 4);
        }

        if (RangeOverlaps(index, bytes, 4, 4) && MatchesCommunicationToken(4, (byte)'S', (byte)'R', (byte)'D', (byte)'Y'))
        {
            ClearCommunicationStaleRange(4, 4);
        }

        return true;
    }

    private void ClearCommunicationStaleRange(int start, int length)
    {
        for (int i = start; i < start + length; i++)
        {
            _m68kCommunicationStaleValid[i] = false;
        }

        for (int i = start; i < start + length; i += 2)
        {
            _m68kCommunicationStaleWordValid[i >> 1] = false;
        }
    }

    private bool TryReadM68kCommunicationStaleByte(ushort offset, out byte value)
    {
        if (TryGetCommunicationByteIndex(offset, out int index) &&
            _m68kCommunicationStaleValid[index])
        {
            _m68kCommunicationStaleValid[index] = false;
            value = _m68kCommunicationStaleBytes[index];

            return true;
        }

        value = 0;
        return false;
    }

    private bool TryReadM68kCommunicationStaleWord(ushort offset, out ushort value)
    {
        if (TryGetCommunicationByteIndex(offset, out int index) &&
            (index & 1) == 0 &&
            _m68kCommunicationStaleWordValid[index >> 1])
        {
            int wordIndex = index >> 1;
            _m68kCommunicationStaleWordValid[wordIndex] = false;
            _m68kCommunicationStaleValid[index] = false;
            _m68kCommunicationStaleValid[index + 1] = false;
            value = _m68kCommunicationStaleWords[wordIndex];

            return true;
        }

        value = 0;
        return false;
    }

    private bool TryReadM68kCommunicationByteLane(ushort offset, out byte value)
    {
        if (!TryGetCommunicationByteIndex(offset, out int index) || index == 0 || (index & 1) != 0)
        {
            value = 0;
            return false;
        }

        int registerIndex = ThirtyTwoXHardwareProfile.CommunicationPortOffset + index;
        int oddRegisterIndex = registerIndex + 1;
        byte even = _systemRegisters[registerIndex];
        byte odd = _systemRegisters[oddRegisterIndex];
        if (index == 2 && even != 0 && even == odd)
        {
            value = even;
            _systemRegisters[oddRegisterIndex] = 0;
            _m68kCommunicationStaleValid[index + 1] = false;
            _m68kCommunicationStaleWordValid[index >> 1] = false;
            return true;
        }

        if ((index & 1) == 0 && index >= 4 && even != 0 && odd == 0)
        {
            value = 0;
            _m68kCommunicationStaleBytes[index] = even;
            _m68kCommunicationStaleValid[index] = true;
            _m68kCommunicationStaleWordValid[index >> 1] = false;
            return true;
        }

        if (even != 0 || odd == 0)
        {
            value = 0;
            return false;
        }

        value = odd;
        _systemRegisters[oddRegisterIndex] = 0;
        _m68kCommunicationStaleValid[index + 1] = false;
        _m68kCommunicationStaleWordValid[index >> 1] = false;
        return true;
    }

    private bool TryWriteM68kCommunicationByteLane(ushort offset, byte value)
    {
        if (!TryGetCommunicationByteIndex(offset, out int index) || index == 0 || (index & 1) != 0 || value != 0)
        {
            return false;
        }

        int oddRegisterIndex = ThirtyTwoXHardwareProfile.CommunicationPortOffset + index + 1;
        if (_systemRegisters[oddRegisterIndex] == 0)
        {
            return false;
        }

        _systemRegisters[oddRegisterIndex] = value;
        _m68kCommunicationStaleValid[index + 1] = false;
        _m68kCommunicationStaleWordValid[index >> 1] = false;
        return true;
    }

    private static void CopyStateArray(byte[] source, byte[] destination)
    {
        Array.Clear(destination);
        Array.Copy(source, destination, Math.Min(source.Length, destination.Length));
    }

    private static void CopyStateArray(uint[] source, uint[] destination)
    {
        Array.Clear(destination);
        Array.Copy(source, destination, Math.Min(source.Length, destination.Length));
    }

    private static void CopyStateArray(bool[] source, bool[] destination)
    {
        Array.Clear(destination);
        Array.Copy(source, destination, Math.Min(source.Length, destination.Length));
    }

    private byte ReadCartridgeByte(uint offset)
    {
        ReadOnlySpan<byte> rom = _cartridgeRom.Span;
        return rom.Length == 0 ? (byte)0xFF : rom[(int)(offset % (uint)rom.Length)];
    }

    private bool IsSh2RomBlockedByRv()
    {
        return (ReadBigEndianWord(_systemRegisters, ThirtyTwoXHardwareProfile.DreqControlOffset) & DreqControlRomToVramDma) != 0;
    }

    private void ClaimSh2CartridgeBus(int byteCount)
    {
        long accessStart = Math.Max(_currentMasterCycle, _cartridgeRomBusBusyUntilMasterCycle);
        long accessEnd = accessStart + Math.Max(1, byteCount) * CartridgeRomBusMasterCyclesPerByte;
        _cartridgeRomBusBusyUntilMasterCycle = accessEnd;
    }

    private void ClearSh2CartridgeCache()
    {
        foreach (Dictionary<uint, byte[]> cache in _sh2LowCartridgeCacheLines)
        {
            cache.Clear();
        }

        Array.Clear(_sh2CacheDataValid[0]);
        Array.Clear(_sh2CacheDataValid[1]);
        ResetSh2CacheTags();
    }

    private void ClearSh2CartridgeCache(int cpuIndex)
    {
        int index = cpuIndex & 1;
        _sh2LowCartridgeCacheLines[index].Clear();
        Array.Clear(_sh2CacheDataValid[index]);
        Array.Fill(_sh2CacheTags[index], Sh2CacheInvalidTag);
        Array.Clear(_sh2CacheLru[index]);
    }

    private void PurgeSh2CacheWays(int cpuIndex, int startWay, int ways)
    {
        int index = cpuIndex & 1;
        _sh2LowCartridgeCacheLines[index].Clear();
        Array.Clear(_sh2CacheLru[index]);
        int first = Math.Clamp(startWay, 0, Sh2CacheWays);
        int end = Math.Clamp(first + ways, first, Sh2CacheWays);
        for (int way = first; way < end; way++)
        {
            int baseIndex = way * Sh2CacheEntriesPerWay;
            for (int entry = 0; entry < Sh2CacheEntriesPerWay; entry++)
            {
                _sh2CacheTags[index][baseIndex + entry] |= Sh2CacheInvalidTag;
            }
        }
    }

    public string FormatSh2CacheLineDebug(uint address, int cpuIndex)
    {
        int index = cpuIndex & 1;
        int entry = (int)((address >> 4) & (Sh2CacheEntriesPerWay - 1));
        uint tag = (address >> 10) & 0x7FFFF;
        var builder = new System.Text.StringBuilder();
        builder.Append(index == 0 ? "master" : "slave");
        builder.Append(" address=$");
        builder.Append(address.ToString("X8", System.Globalization.CultureInfo.InvariantCulture));
        builder.Append(" entry=");
        builder.Append(entry.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.Append(" tag=$");
        builder.Append(tag.ToString("X5", System.Globalization.CultureInfo.InvariantCulture));
        builder.Append(" lru=$");
        builder.Append(_sh2CacheLru[index][entry].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));

        for (int way = 0; way < Sh2CacheWays; way++)
        {
            int lineIndex = (way * Sh2CacheEntriesPerWay) + entry;
            uint lineTag = _sh2CacheTags[index][lineIndex];
            bool valid = (lineTag & Sh2CacheInvalidTag) == 0;
            builder.AppendLine();
            builder.Append("  way ");
            builder.Append(way.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(valid ? " valid" : " invalid");
            builder.Append(" tag=$");
            builder.Append((lineTag & 0x7FFFF).ToString("X5", System.Globalization.CultureInfo.InvariantCulture));
            builder.Append(" data=");
            int baseOffset = lineIndex * Sh2CacheLineBytes;
            for (int i = 0; i < Sh2CacheLineBytes; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(_sh2CacheDataArrays[index][baseOffset + i].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        uint lowLineAddress = address & ~0x0Fu;
        if (_sh2LowCartridgeCacheLines[index].TryGetValue(lowLineAddress, out byte[]? lowLine))
        {
            builder.AppendLine();
            builder.Append("  low-rom-cache data=");
            for (int i = 0; i < lowLine.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(lowLine[i].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    private byte ReadSh2CachedCartridgeByte(uint cacheOffset, uint romOffset, int cpuIndex)
    {
        int index = cpuIndex & 1;
        int entry = (int)((cacheOffset >> 4) & (Sh2CacheEntriesPerWay - 1));
        uint tag = (cacheOffset >> 10) & 0x7FFFF;
        if (TryFindSh2CacheLine(index, entry, tag, out int lineIndex))
        {
            return _sh2CacheDataArrays[index][(lineIndex * Sh2CacheLineBytes) + (int)(cacheOffset & 0x0F)];
        }

        Dictionary<uint, byte[]> cache = _sh2LowCartridgeCacheLines[index];
        uint lineAddress = cacheOffset & ~0x0Fu;
        if (!cache.TryGetValue(lineAddress, out byte[]? line))
        {
            AddSh2WaitCycles(cpuIndex, IsSh2RomBlockedByRv() ? Sh2CartridgeRvBlockedWaitCycles : Sh2CachedCartridgeLineFillWaitCycles);
            ClaimSh2CartridgeBus(16);
            line = new byte[16];
            uint romLineAddress = romOffset & ~0x0Fu;
            for (uint i = 0; i < line.Length; i++)
            {
                line[i] = ReadCartridgeByte(romLineAddress + i);
            }

            cache[lineAddress] = line;
        }

        lineIndex = SelectSh2CacheLine(index, entry);
        Buffer.BlockCopy(line, 0, _sh2CacheDataArrays[index], lineIndex * Sh2CacheLineBytes, Sh2CacheLineBytes);
        for (int i = 0; i < Sh2CacheLineBytes; i++)
        {
            _sh2CacheDataValid[index][(lineIndex * Sh2CacheLineBytes) + i] = 1;
        }

        _sh2CacheTags[index][lineIndex] = tag;
        return line[(int)(cacheOffset & 0x0F)];
    }

    private byte ReadSh2CachedSdramByte(uint address, int sdramOffset, int cpuIndex)
    {
        int index = cpuIndex & 1;
        int entry = (int)((address >> 4) & (Sh2CacheEntriesPerWay - 1));
        uint tag = (address >> 10) & 0x7FFFF;
        if (TryFindSh2CacheLine(index, entry, tag, out int lineIndex))
        {
            return _sh2CacheDataArrays[index][(lineIndex * Sh2CacheLineBytes) + (int)(address & 0x0F)];
        }

        AddSh2WaitCycles(cpuIndex, Sh2SdramReadWaitCycles);
        lineIndex = SelectSh2CacheLine(index, entry);
        int lineBase = sdramOffset & ~(Sh2CacheLineBytes - 1);
        int destination = lineIndex * Sh2CacheLineBytes;
        for (int i = 0; i < Sh2CacheLineBytes; i++)
        {
            _sh2CacheDataArrays[index][destination + i] = _sdram[(lineBase + i) & (ThirtyTwoXHardwareProfile.SdramBytes - 1)];
            _sh2CacheDataValid[index][destination + i] = 1;
        }

        _sh2CacheTags[index][lineIndex] = tag;
        return _sh2CacheDataArrays[index][destination + (int)(address & 0x0F)];
    }

    private byte ReadSh2SdramByteNoCache(int sdramOffset, int cpuIndex)
    {
        AddSh2WaitCycles(cpuIndex, Sh2SdramReadWaitCycles);
        return _sdram[sdramOffset];
    }

    private void WriteSh2CachedCartridgeByte(uint cacheOffset, uint romOffset, byte value, int cpuIndex)
    {
        // SH-2 cache writes are write-through: hits update the data array, misses do not allocate.
        // Cartridge space has no writable backing store, so write misses disappear on the external bus.
        WriteSh2CachedAreaByte(cacheOffset, value, cpuIndex);
    }

    private void WriteSh2CachedAreaByte(uint address, byte value, int cpuIndex)
    {
        int index = cpuIndex & 1;
        int entry = (int)((address >> 4) & (Sh2CacheEntriesPerWay - 1));
        uint tag = (address >> 10) & 0x7FFFF;
        if (TryFindSh2CacheLine(index, entry, tag, out int lineIndex))
        {
            int cacheDataOffset = (lineIndex * Sh2CacheLineBytes) + (int)(address & 0x0F);
            _sh2CacheDataArrays[index][cacheDataOffset] = value;
            _sh2CacheDataValid[index][cacheDataOffset] = 1;
        }
    }

    private void UpdateSh2SdramCacheByte(int sdramOffset, byte value)
    {
        for (int cpuIndex = 0; cpuIndex < ThirtyTwoXHardwareProfile.Sh2CpuCount; cpuIndex++)
        {
            UpdateSh2SdramCacheByte(sdramOffset, value, cpuIndex);
        }
    }

    private void UpdateSh2SdramCacheByte(int sdramOffset, byte value, int cpuIndex)
    {
        uint cacheAddress = ThirtyTwoXHardwareProfile.Sh2SdramStart +
            (uint)(sdramOffset & (ThirtyTwoXHardwareProfile.SdramBytes - 1));
        WriteSh2CachedAreaByte(cacheAddress, value, cpuIndex);
    }

    private bool TryFindSh2CacheLine(int cpuIndex, int entry, uint tag, out int lineIndex)
    {
        uint[] tags = _sh2CacheTags[cpuIndex & 1];
        for (int way = Sh2CacheWays - 1; way >= 0; way--)
        {
            int index = (way * Sh2CacheEntriesPerWay) + entry;
            if ((tags[index] & Sh2CacheInvalidTag) == 0 && (tags[index] & 0x7FFFF) == tag)
            {
                _sh2CacheLru[cpuIndex & 1][entry] = Sh2CacheLruUpdate[way][_sh2CacheLru[cpuIndex & 1][entry] & 0x3F];
                lineIndex = index;
                return true;
            }
        }

        lineIndex = 0;
        return false;
    }

    private int SelectSh2CacheLine(int cpuIndex, int entry)
    {
        byte[] lru = _sh2CacheLru[cpuIndex & 1];
        uint[] tags = _sh2CacheTags[cpuIndex & 1];
        int selectedWay = Sh2CacheLruSelect[lru[entry] & 0x3F];
        if (IsSh2CacheTwoWay(cpuIndex))
        {
            selectedWay = 2 + (selectedWay & 0x01);
        }

        lru[entry] = Sh2CacheLruUpdate[selectedWay][lru[entry] & 0x3F];
        return (selectedWay * Sh2CacheEntriesPerWay) + entry;
    }

    private bool IsSh2CacheTwoWay(int cpuIndex)
    {
        byte ccr = _sh2PeripheralRegisters[cpuIndex & 1][(int)(Sh2CacheControlRegisterAddress - Sh2PeripheralRegisterStart)];
        return (ccr & 0x08) != 0;
    }

    private bool IsSh2CacheEnabled(int cpuIndex)
    {
        byte ccr = _sh2PeripheralRegisters[cpuIndex & 1][(int)(Sh2CacheControlRegisterAddress - Sh2PeripheralRegisterStart)];
        return (ccr & 0x01) != 0;
    }

    private bool IsSh2DataCacheEnabled(int cpuIndex)
    {
        byte ccr = _sh2PeripheralRegisters[cpuIndex & 1][(int)(Sh2CacheControlRegisterAddress - Sh2PeripheralRegisterStart)];
        return (ccr & 0x05) == 0x01;
    }

    private bool IsSh2InstructionCacheEnabled(int cpuIndex)
    {
        byte ccr = _sh2PeripheralRegisters[cpuIndex & 1][(int)(Sh2CacheControlRegisterAddress - Sh2PeripheralRegisterStart)];
        return (ccr & 0x03) == 0x01;
    }

    private void PurgeSh2CacheLine(int cpuIndex, uint address)
    {
        int index = cpuIndex & 1;
        int entry = (int)((address >> 4) & (Sh2CacheEntriesPerWay - 1));
        uint tag = (address >> 10) & 0x7FFFF;
        _sh2LowCartridgeCacheLines[index].Remove(address & ~0x0Fu);
        for (int way = 0; way < Sh2CacheWays; way++)
        {
            int tagIndex = (way * Sh2CacheEntriesPerWay) + entry;
            if ((_sh2CacheTags[index][tagIndex] & 0x7FFFF) == tag)
            {
                _sh2CacheTags[index][tagIndex] |= Sh2CacheInvalidTag;
            }
        }
    }

    private uint ReadSh2CacheAddressArrayLong(uint address, int offset, int cpuIndex)
    {
        int entry = (offset >> 4) & (Sh2CacheEntriesPerWay - 1);
        int way = ReadSh2CacheWaySelect(cpuIndex);
        int index = (way * Sh2CacheEntriesPerWay) + entry;
        uint tag = _sh2CacheTags[cpuIndex & 1][index];
        uint valid = (tag & Sh2CacheInvalidTag) == 0 ? 1u : 0u;
        return ((tag & 0x7FFFF) << 10) | ((uint)_sh2CacheLru[cpuIndex & 1][entry] << 4) | (valid << 1);
    }

    private void WriteSh2CacheAddressArrayLong(uint address, int offset, uint value, int cpuIndex)
    {
        int entry = (offset >> 4) & (Sh2CacheEntriesPerWay - 1);
        int way = ReadSh2CacheWaySelect(cpuIndex);
        int index = (way * Sh2CacheEntriesPerWay) + entry;
        uint tag = (value >> 10) & 0x7FFFF;
        bool valid = ((address >> 2) & 1u) != 0;
        _sh2CacheLru[cpuIndex & 1][entry] = (byte)((value >> 6) & 0x3F);
        _sh2CacheTags[cpuIndex & 1][index] = valid ? tag : tag | Sh2CacheInvalidTag;
        if (!valid)
        {
            int baseOffset = index * Sh2CacheLineBytes;
            Array.Clear(_sh2CacheDataValid[cpuIndex & 1], baseOffset, Sh2CacheLineBytes);
        }
    }

    private int ReadSh2CacheWaySelect(int cpuIndex)
    {
        byte ccr = _sh2PeripheralRegisters[cpuIndex & 1][(int)(Sh2CacheControlRegisterAddress - Sh2PeripheralRegisterStart)];
        return (ccr >> 6) & 0x03;
    }

    private void ResetSh2CacheTags()
    {
        Array.Fill(_sh2CacheTags[0], Sh2CacheInvalidTag);
        Array.Fill(_sh2CacheTags[1], Sh2CacheInvalidTag);
        Array.Clear(_sh2CacheLru[0]);
        Array.Clear(_sh2CacheLru[1]);
    }

    private bool TryMapSh2CachedCartridgeAddress(uint address, out uint cacheOffset, out uint romOffset)
    {
        if (address is >= ThirtyTwoXHardwareProfile.Sh2CartridgeBankedCachedStart and <
            ThirtyTwoXHardwareProfile.Sh2CartridgeBankedCachedStart + ThirtyTwoXHardwareProfile.M68kCartridgeBankedBytes)
        {
            cacheOffset = address;
            romOffset = ((uint)M68kCartridgeBank * ThirtyTwoXHardwareProfile.M68kCartridgeBankedBytes) +
                (address - ThirtyTwoXHardwareProfile.Sh2CartridgeBankedCachedStart);
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2CartridgeFixedCachedStart and <
            ThirtyTwoXHardwareProfile.Sh2CartridgeFixedCachedStart + 0x0200_0000)
        {
            cacheOffset = address;
            romOffset = address - ThirtyTwoXHardwareProfile.Sh2CartridgeFixedCachedStart;
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2CartridgeLowCachedStart and <
            ThirtyTwoXHardwareProfile.Sh2CartridgeLowCachedStart + ThirtyTwoXHardwareProfile.Sh2CartridgeLowCachedBytes)
        {
            cacheOffset = address - ThirtyTwoXHardwareProfile.Sh2CartridgeLowCachedStart;
            romOffset = cacheOffset;
            return true;
        }

        cacheOffset = 0;
        romOffset = 0;
        return false;
    }

    private bool TryMapSh2UncachedBankedCartridgeAddress(uint address, out uint romOffset)
    {
        if (address is >= ThirtyTwoXHardwareProfile.Sh2CartridgeBankedStart and <
            ThirtyTwoXHardwareProfile.Sh2CartridgeBankedStart + ThirtyTwoXHardwareProfile.M68kCartridgeBankedBytes)
        {
            romOffset = ((uint)M68kCartridgeBank * ThirtyTwoXHardwareProfile.M68kCartridgeBankedBytes)
                + (address - ThirtyTwoXHardwareProfile.Sh2CartridgeBankedStart);
            return true;
        }

        romOffset = 0;
        return false;
    }

    private static bool TryMapSh2SdramAddress(uint address, out int offset)
    {
        if ((address & 0xFE00_0000u) == ThirtyTwoXHardwareProfile.Sh2SdramStart)
        {
            offset = (int)(address & (ThirtyTwoXHardwareProfile.SdramBytes - 1));
            return true;
        }

        if ((address & 0xFE00_0000u) == ThirtyTwoXHardwareProfile.Sh2SdramCacheThroughStart)
        {
            offset = (int)(address & (ThirtyTwoXHardwareProfile.SdramBytes - 1));
            return true;
        }

        if ((address & 0xFE00_0000u) == Sh2SdramStackAliasStart)
        {
            offset = (int)(address & (ThirtyTwoXHardwareProfile.SdramBytes - 1));
            return true;
        }

        if (TryMapSh2OverflowSdramMirrorAddress(address, out offset))
        {
            return true;
        }

        offset = 0;
        return false;
    }

    private static bool TryMapExactSh2SdramAddress(uint address, out int offset)
    {
        if (address < ThirtyTwoXHardwareProfile.SdramBytes)
        {
            offset = (int)address;
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2SdramStart and < ThirtyTwoXHardwareProfile.Sh2SdramStart + ThirtyTwoXHardwareProfile.SdramBytes)
        {
            offset = (int)(address - ThirtyTwoXHardwareProfile.Sh2SdramStart);
            return true;
        }

        if (address is >= ThirtyTwoXHardwareProfile.Sh2SdramCacheThroughStart and < ThirtyTwoXHardwareProfile.Sh2SdramCacheThroughStart + ThirtyTwoXHardwareProfile.SdramBytes)
        {
            offset = (int)(address - ThirtyTwoXHardwareProfile.Sh2SdramCacheThroughStart);
            return true;
        }

        offset = 0;
        return false;
    }

    private static bool TryMapSh2OverflowSdramMirrorAddress(uint address, out int offset)
    {
        // Some 32X SDK helpers form oversized effective addresses from an SDRAM
        // base plus shifted ROM pointers. Real hardware still decodes the SDRAM
        // address lines, so mirror that overflow instead of treating it as cache purge.
        if ((address & 0xF000_0000u) == 0x5000_0000u)
        {
            offset = (int)(address & (ThirtyTwoXHardwareProfile.SdramBytes - 1));
            return true;
        }

        offset = 0;
        return false;
    }

    private static bool TryMapSh2CachedSdramAddress(uint address, out int offset)
    {
        if ((address & 0xFE00_0000u) == ThirtyTwoXHardwareProfile.Sh2SdramStart)
        {
            offset = (int)(address & (ThirtyTwoXHardwareProfile.SdramBytes - 1));
            return true;
        }

        offset = 0;
        return false;
    }

    private static bool TryMapExactSh2CachedSdramAddress(uint address, out int offset)
    {
        if (address is >= ThirtyTwoXHardwareProfile.Sh2SdramStart and < ThirtyTwoXHardwareProfile.Sh2SdramStart + ThirtyTwoXHardwareProfile.SdramBytes)
        {
            offset = (int)(address - ThirtyTwoXHardwareProfile.Sh2SdramStart);
            return true;
        }

        offset = 0;
        return false;
    }

    private static bool TryMapSh2FrameBufferAddress(uint address, out uint offset, out bool overwrite)
    {
        uint window = address & 0xFE00_0000u;
        if (window is 0x0400_0000u or 0x2400_0000u)
        {
            uint local = address & 0x0003_FFFFu;
            offset = local & (ThirtyTwoXHardwareProfile.FrameBufferBytes - 1);
            overwrite = (local & 0x0002_0000u) != 0;
            return true;
        }

        offset = 0;
        overwrite = false;
        return false;
    }

    private static bool TryMapSh2PrivateWorkRamAddress(uint address, int cpuIndex, out int offset)
    {
        if (address is >= Sh2PrivateWorkRamLowMirrorStart and < Sh2PrivateWorkRamLowMirrorEnd)
        {
            offset = (int)(address & (Sh2PrivateWorkRamBytes - 1));
            return true;
        }

        if (address is >= Sh2CacheDataArrayStart and < Sh2CacheDataArrayEnd)
        {
            int cacheOffset = (int)(address & (Sh2CacheDataArrayBytes - 1));
            if ((uint)cacheOffset < Sh2PrivateWorkRamBytes)
            {
                offset = cacheOffset;
                return true;
            }
        }

        offset = 0;
        return false;
    }

    private bool TryMapSh2CacheDataArrayAddress(uint address, int cpuIndex, out int offset)
    {
        if (address is >= Sh2CacheDataArrayStart and < Sh2CacheDataArrayEnd)
        {
            offset = (int)(address & (Sh2CacheDataArrayBytes - 1));
            return true;
        }

        offset = 0;
        return false;
    }

    private static bool TryMapSh2CacheAddressArrayAddress(uint address, out int offset)
    {
        if (address is >= Sh2CacheAddressArrayStart and < Sh2CacheAddressArrayEnd)
        {
            offset = (int)(address & (Sh2CacheDataArrayBytes - 1));
            return true;
        }

        offset = 0;
        return false;
    }

    private static bool TryMapSh2CachePurgeAddress(uint address)
    {
        return address is >= Sh2CachePurgeStart and < Sh2CachePurgeEnd;
    }

    private static byte[] BuildSh2CacheLruSelect()
    {
        byte[] select = new byte[64];
        for (int n = 0; n < select.Length; n++)
        {
            bool bit0 = (n & 0x01) != 0;
            bool bit1 = (n & 0x02) != 0;
            bool bit2 = (n & 0x04) != 0;
            bool bit3 = (n & 0x08) != 0;
            bool bit4 = (n & 0x10) != 0;
            bool bit5 = (n & 0x20) != 0;
            select[n] = (bit5, bit4, bit3, bit2, bit1, bit0) switch
            {
                (true, true, true, _, _, _) => 0,
                (false, _, _, true, true, _) => 1,
                (_, false, _, false, _, true) => 2,
                (_, _, false, _, false, false) => 3,
                _ => 3
            };
        }

        return select;
    }

    private static byte[][] BuildSh2CacheLruUpdate()
    {
        byte[][] update = [new byte[64], new byte[64], new byte[64], new byte[64]];
        for (int n = 0; n < 64; n++)
        {
            update[0][n] = (byte)(n & ~0x38);
            update[1][n] = (byte)((n | 0x20) & ~0x06);
            update[2][n] = (byte)((n | 0x14) & ~0x01);
            update[3][n] = (byte)(n | 0x0B);
        }

        return update;
    }

    private void AddSh2WaitCycles(int cpuIndex, int cycles)
    {
        if (cycles > 0)
        {
            _sh2WaitCycles[cpuIndex & 1] += cycles;
        }
    }

    private int ConsumeSh2WaitCycles(int cpuIndex)
    {
        int index = cpuIndex & 1;
        int cycles = _sh2WaitCycles[index];
        _sh2WaitCycles[index] = 0;
        return cycles;
    }

    private void CopyInitialProgramToSdram(uint source, uint destination, uint size)
    {
        ReadOnlySpan<byte> rom = _cartridgeRom.Span;
        if (rom.Length == 0)
        {
            return;
        }

        uint dest = destination % ThirtyTwoXHardwareProfile.SdramBytes;
        uint count = Math.Min(size, (uint)ThirtyTwoXHardwareProfile.SdramBytes - dest);
        for (uint i = 0; i < count; i++)
        {
            _sdram[(int)(dest + i)] = rom[(int)((source + i) % (uint)rom.Length)];
        }
    }

    private static uint NormalizeSh2ProgramAddress(uint address)
    {
        if (address < ThirtyTwoXHardwareProfile.SdramBytes)
        {
            return ThirtyTwoXHardwareProfile.Sh2SdramStart + address;
        }

        return address;
    }

    private static ushort ReadBigEndianWord(byte[] data, int offset)
    {
        int index = offset & (data.Length - 1);
        int next = (index + 1) & (data.Length - 1);
        return (ushort)((data[index] << 8) | data[next]);
    }

    private static ushort ReadBigEndianWord(ReadOnlySpan<byte> data, int offset)
    {
        int index = offset & (data.Length - 1);
        int next = (index + 1) & (data.Length - 1);
        return (ushort)((data[index] << 8) | data[next]);
    }

    private static uint ReadBigEndianLong(byte[] data, int offset)
    {
        return (uint)((ReadBigEndianWord(data, offset) << 16) | ReadBigEndianWord(data, offset + 2));
    }

    private static void WriteBigEndianWord(byte[] data, int offset, ushort value)
    {
        int index = offset & (data.Length - 1);
        int next = (index + 1) & (data.Length - 1);
        data[index] = (byte)(value >> 8);
        data[next] = (byte)value;
    }

    private static void WriteBigEndianLong(byte[] data, int offset, uint value)
    {
        WriteBigEndianWord(data, offset, (ushort)(value >> 16));
        WriteBigEndianWord(data, offset + 2, (ushort)value);
    }

    public sealed record PwmSnapshot(ushort[] Left, ushort[] Right, ushort[] Mono);

    public sealed record ThirtyTwoXState(
        byte[] Sdram,
        byte[] FrameBuffer0,
        byte[] FrameBuffer1,
        byte[] Palette,
        byte[] SystemRegisters,
        bool[] M68kCommunicationPendingHostBytes,
        bool[] M68kCommunicationDeferredSh2ClearBytes,
        byte[] VdpRegisters,
        ushort[] PwmLeft,
        ushort[] PwmRight,
        ushort[] PwmMono,
        ushort[] PwmLeftHardwareFifo,
        ushort[] PwmRightHardwareFifo,
        ushort[] PwmMonoHardwareFifo,
        double PwmLeftLevel,
        double PwmRightLevel,
        double PwmMonoLevel,
        bool MasterPwmInterruptPending,
        bool SlavePwmInterruptPending,
        int PwmCycleCounter,
        int PwmTimerCounter,
        ushort[] DreqFifo,
        byte[] MasterDmaRegisters,
        byte[] SlaveDmaRegisters,
        byte[] MasterPeripheralRegisters,
        byte[] SlavePeripheralRegisters,
        int[] WatchdogCycleCounters,
        bool[] WatchdogInterruptPending,
        byte[] WatchdogWriteSelect,
        long[] FrtBaseCycles,
        ushort[] FrtBaseCounters,
        ushort[] FrtLastCounters,
        ushort[] FrtOutputCompareB,
        byte[] MasterCacheDataArray,
        byte[] SlaveCacheDataArray,
        byte[] MasterCacheDataValid,
        byte[] SlaveCacheDataValid,
        byte[] MasterPrivateWorkRam,
        byte[] SlavePrivateWorkRam,
        uint[] MasterCacheTags,
        uint[] SlaveCacheTags,
        byte[] MasterCacheLru,
        byte[] SlaveCacheLru,
        uint[] MasterDivisionRegisters,
        uint[] SlaveDivisionRegisters,
        byte[] DmaRequestSelect,
        int ActiveDisplayFrameBufferIndex,
        bool AdapterEnabled,
        bool Sh2ResetEnabled,
        bool Sh2ResetReleased,
        bool VdpAccessGrantedToSh2,
        bool VBlank,
        bool HBlank,
        int CurrentScanline,
        bool FrameBufferSwapPending,
        int PendingDrawFrameBufferIndex,
        int RequestedDisplayFrameBufferIndex,
        ushort LatchedBitmapMode,
        ushort LatchedScreenShiftControl,
        bool LastCompositeUsedFallback,
        int LastCompositeMode,
        ushort MasterInterruptMask,
        ushort SlaveInterruptMask,
        bool MasterVerticalInterruptPending,
        bool SlaveVerticalInterruptPending,
        bool MasterVresInterruptPending,
        bool SlaveVresInterruptPending,
        bool MasterHorizontalInterruptPending,
        bool SlaveHorizontalInterruptPending,
        byte HorizontalInterruptPeriod,
        byte HorizontalInterruptCounter,
        bool MasterCommandInterruptPending,
        bool SlaveCommandInterruptPending,
        bool BootRomHandshakePending,
        bool BootRomSignatureRead,
        bool BootRomSignatureReadbackActive,
        bool BootRomLaunchPending,
        bool BootRomPostStartSignaturePending,
        bool BootRomPostStartSignatureHiddenFromSh2,
        byte BootRomPostStartSignatureReadMask,
        byte BootRomPostStartHostClearProtectMask,
        bool BootRomChecksumPublished,
        bool BootRomChecksumHostCleared,
        bool BootRomSixtyEightUpPending,
        bool BootRomSixtyEightUpReadyHiddenFromSh2,
        bool M68kVdpControlMailboxArmed,
        Sh2Cpu.Sh2State MasterSh2,
        Sh2Cpu.Sh2State SlaveSh2);

    private sealed class Sh2MemoryBus : ISh2Bus, ISh2InstructionBus, ISh2WaitStateBus, ISh2PeekBus
    {
        private readonly ThirtyTwoXDevice _device;
        private readonly int _cpuIndex;

        public Sh2MemoryBus(ThirtyTwoXDevice device, int cpuIndex)
        {
            _device = device;
            _cpuIndex = cpuIndex;
        }

        public byte ReadByte(uint address) => _device.ReadSh2Byte(address, _cpuIndex);
        public ushort ReadWord(uint address) => _device.ReadSh2Word(address, _cpuIndex);
        public uint ReadLong(uint address) => _device.ReadSh2Long(address, _cpuIndex);
        public ushort ReadInstructionWord(uint address) => _device.ReadSh2InstructionWord(address, _cpuIndex);
        public void WriteByte(uint address, byte value) => _device.WriteSh2Byte(address, value, _cpuIndex);
        public void WriteWord(uint address, ushort value) => _device.WriteSh2Word(address, value, _cpuIndex);
        public void WriteLong(uint address, uint value) => _device.WriteSh2Long(address, value, _cpuIndex);
        public int ConsumeWaitCycles() => _device.ConsumeSh2WaitCycles(_cpuIndex);
        public bool TryPeekByte(uint address, out byte value) => _device.TryPeekSh2Byte(address, _cpuIndex, out value);
        public bool TryPeekWord(uint address, out ushort value) => _device.TryPeekSh2Word(address, _cpuIndex, out value);
    }

    public readonly record struct MarsUserHeader(
        bool IsValid,
        bool RequiresHostLaunchCommand,
        uint Version,
        uint InitialSource,
        uint InitialDestination,
        uint InitialSize,
        uint MasterStart,
        uint SlaveStart,
        uint MasterVectorBase,
        uint SlaveVectorBase)
    {
        public static MarsUserHeader Parse(ReadOnlySpan<byte> rom)
        {
            const int offset = (int)ThirtyTwoXHardwareProfile.MarsUserHeaderStart;
            if (rom.Length < offset + 0x30)
            {
                return default;
            }

            uint version = ReadUInt32(rom, offset + 0x10);
            uint initialSource = ReadUInt32(rom, offset + 0x14);
            uint initialDestination = ReadUInt32(rom, offset + 0x18);
            uint initialSize = ReadUInt32(rom, offset + 0x1C);
            uint masterStart = ReadUInt32(rom, offset + 0x20);
            uint slaveStart = ReadUInt32(rom, offset + 0x24);
            uint masterVectorBase = ReadUInt32(rom, offset + 0x28);
            uint slaveVectorBase = ReadUInt32(rom, offset + 0x2C);
            bool hasMarsSignature = rom[offset] == (byte)'M' &&
                rom[offset + 1] == (byte)'A' &&
                rom[offset + 2] == (byte)'R' &&
                rom[offset + 3] == (byte)'S';
            bool hasStarSignature = rom[offset] == (byte)'S' &&
                rom[offset + 1] == (byte)'T' &&
                rom[offset + 2] == (byte)'A' &&
                rom[offset + 3] == (byte)'R';
            if (!hasMarsSignature &&
                !hasStarSignature &&
                !LooksLikeMarsUserHeader(rom, initialSource, initialDestination, initialSize, masterStart, slaveStart, masterVectorBase, slaveVectorBase))
            {
                return default;
            }

            return new MarsUserHeader(
                true,
                hasStarSignature,
                version,
                initialSource,
                initialDestination,
                initialSize,
                masterStart,
                slaveStart,
                masterVectorBase,
                slaveVectorBase);
        }

        private static bool LooksLikeMarsUserHeader(
            ReadOnlySpan<byte> rom,
            uint initialSource,
            uint initialDestination,
            uint initialSize,
            uint masterStart,
            uint slaveStart,
            uint masterVectorBase,
            uint slaveVectorBase)
        {
            if (initialSize == 0 ||
                initialSize > ThirtyTwoXHardwareProfile.SdramBytes ||
                initialSource >= (uint)rom.Length ||
                initialSource + initialSize > (uint)rom.Length)
            {
                return false;
            }

            return IsSdramAddressOrOffset(initialDestination) &&
                IsSdramAddressOrOffset(masterStart) &&
                IsSdramAddressOrOffset(slaveStart) &&
                IsSdramAddressOrOffset(masterVectorBase) &&
                IsSdramAddressOrOffset(slaveVectorBase);
        }

        private static bool IsSdramAddressOrOffset(uint value)
        {
            if (value < ThirtyTwoXHardwareProfile.SdramBytes)
            {
                return true;
            }

            return value is >= ThirtyTwoXHardwareProfile.Sh2SdramStart and < ThirtyTwoXHardwareProfile.Sh2SdramStart + ThirtyTwoXHardwareProfile.SdramBytes;
        }

        private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset)
        {
            return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }
    }

    public readonly record struct SystemRegisterWriteTrace(string Source, ushort Offset, byte Value);

    public readonly record struct SystemRegisterAccessTrace(string Source, string Operation, ushort Offset, ushort Value);

    public readonly record struct Sh2MemoryAccessTrace(string Source, string Operation, uint Address, uint Value);
    public readonly record struct SdramWriteTrace(string Source, string Operation, uint Address, int Offset, ushort Value, uint Pc, ushort Opcode);

    public readonly record struct PaletteAccessTrace(string Source, string Operation, ushort Offset, ushort Value);

    public readonly record struct FrameBufferAccessTrace(
        string Source,
        string Operation,
        uint Offset,
        ushort Value,
        int BufferIndex,
        int DisplayBufferIndex,
        int RequestedDisplayBufferIndex,
        bool SwapPending,
        uint Pc,
        ushort Opcode);
}
