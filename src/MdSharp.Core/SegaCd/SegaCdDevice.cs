using MdSharp.Core.Cpu.M68k;

namespace MdSharp.Core.SegaCd;

public sealed class SegaCdDevice
{
    private const byte PostBootCommand23AckBudget = 2;

    private const byte MainControlSubResetRelease = 0x01;
    private const byte MainControlSubBusRequest = 0x02;
    private const byte MainInterruptSubCpuLevel2 = 0x01;
    private const uint MainControlLowOffset = 0x01;
    private const byte WordRamReturnToMain = 0x01;
    private const byte WordRamMainAssignsToSub = 0x02;
    private const byte WordRamOneMegMode = 0x04;
    private const byte WordRamGraphicsPriorityMask = 0x18;
    private const byte MainProgramRamBankMask = 0xC0;
    private const byte CddHockBit = 0x04;
    private const byte CdcTransferReadyBit = 0x40;
    private const byte CdcTransferDataBit = 0x80;
    private const uint MainInterruptOffset = 0x00;
    private const uint WordRamModeLowOffset = 0x03;
    private const uint CdcTransferOffset = 0x04;
    private const uint CdcAddressOffset = 0x05;
    private const uint CdcRegisterOffset = 0x07;
    private const uint CdcDataOffset = 0x08;
    private const uint SubInterruptMaskOffset = 0x33;
    private const uint GfxStampSizeLowOffset = 0x59;
    private const uint GfxStampMapAddressOffset = 0x5A;
    private const uint GfxBufferVCellsLowOffset = 0x5D;
    private const uint GfxBufferAddressOffset = 0x5E;
    private const uint GfxBufferOffsetLowOffset = 0x61;
    private const uint GfxBufferHDotsOffset = 0x62;
    private const uint GfxBufferVDotsOffset = 0x64;
    private const uint GfxTraceVectorOffset = 0x66;
    private const uint GfxTraceVectorLowOffset = 0x67;
    private const uint CddControlOffset = 0x37;
    private const uint CddStatusStart = 0x38;
    private const uint CddCommandStart = 0x42;
    private const uint MainToSubFlagOffset = 0x0E;
    private const uint SubToMainFlagOffset = 0x0F;
    private const uint MainWorkRamBootHelperStart = 0x00FF_0000;
    private const uint CbtReadSectorStartProgramRamOffset = 0x5B34;
    private const uint CbtReadSectorCountProgramRamOffset = 0x5B38;
    private const uint CbtReadSectorLoopCountProgramRamOffset = 0x5B3C;
    private const uint CbtBootHeaderReadyProgramRamOffset = 0x5A05;
    private const uint CdcRingBufferProgramRamOffset = 0x5A44;
    private const uint RingBufferReadStepOffset = 0x00;
    private const uint RingBufferWriteStepOffset = 0x02;
    private const uint RingBufferReadPtrOffset = 0x04;
    private const uint RingBufferWritePtrOffset = 0x06;
    private const uint RingBufferSizeOffset = 0x0A;
    private const uint RingBufferDataOffset = 0x10;
    private const int CdcPacketBytes = 4 + 2048 + 288;
    private const int CddPacketBytes = 10;
    private const int CdAudioSamplesPerSector = 588;
    private const int CdAudioSectorBytes = CdAudioSamplesPerSector * 4;
    private const int PcmChannelCount = 8;
    private const int PcmStreamClockDivider = 384;
    private const int CddInterruptLevel = 4;
    private const int CdcInterruptLevel = 5;
    private const int InitialProgramSectorCount = 16;
    private const int InitialProgramDiscHeaderBytes = 0x200;
    private const int BootHeaderIpOffsetField = 0x30;
    private const int BootHeaderIpLengthField = 0x34;
    private const int BootHeaderSpOffsetField = 0x40;
    private const int BootHeaderSpLengthField = 0x44;
    private const int BootHeaderMinimumIpBytes = 0x600;
    private const int SystemProgramRamLoadOffset = 0x6000;
    private const int GenericBootReservedWordRamBytes = InitialProgramSectorCount * 2048;
    private const int SonicCdMmdHeaderBytes = 0x100;
    private const string SonicCdIpxModuleFileName = "IPX___.MMD;1";
    private const string SonicCdSpxModuleFileName = "SPX___.BIN;1";
    private const int SonicCdSpxProgramRamOffset = 0xB800;
    private const uint SonicCdSpxEntryPoint = 0x0000_C000;
    private const uint SonicCdSpxStackPointer = 0x0001_0000;
    private const uint SonicCdSystemLoadFileEntry = 0x0000_7800;
    private const uint SonicCdSystemFileFuncEntry = 0x0000_7880;
    private const uint SonicCdBramSubDoneWait = 0x0001_0094;
    private const uint SonicCdTitleSubMainLoop = 0x0001_01CE;
    private const uint SonicCdSpxCommandWait = 0x0000_C026;
    private const uint SonicCdSpxCommandWaitSecondRead = 0x0000_C02A;
    private const uint SonicCdSubIrq2VectorOffset = 0x0000_0068;
    private const uint SonicCdSubLevel2Stub = 0x0000_5F7C;
    private const uint SonicCdSubUserCall2Stub = 0x0000_5F34;
    private const uint SonicCdSubSpIrq2Handler = 0x0000_7700;
    private const ushort SonicCdFileStatusOk = 100;
    private const ushort SonicCdFileFuncInit = 0;
    private const ushort SonicCdFileFuncOperation = 1;
    private const ushort SonicCdFileFuncStatus = 2;
    private const ushort SonicCdFileFuncGetFiles = 3;
    private const ushort SonicCdFileFuncLoadFile = 4;
    private const ushort SonicCdFileFuncFindFile = 5;
    private const ushort SonicCdFileFuncReset = 7;
    private const ushort SonicCdSubCommandFadeCdda = 0x000E;
    private const ushort SonicCdSubCommandPlayR1AMusic = 0x000F;
    private const ushort SonicCdSubCommandPlayR8DMusic = 0x0021;
    private const ushort SonicCdSubCommandPlayTitleMusic = 0x0029;
    private const ushort SonicCdSubCommandPlayGameOverMusic = 0x002D;
    private const ushort SonicCdSubCommandTestR1AMusic = 0x002E;
    private const ushort SonicCdSubCommandTestEndingMusic = 0x0052;
    private const double CddInterruptHz = 75.0;
    private const int CddLeadInFrames = 150;
    private const byte CddStatusIdle = 0x00;
    private const byte CddStatusStop = 0x09;
    private const byte CddStatusPlay = 0x01;
    private const byte CddStatusSeek = 0x02;
    private const byte CddStatusReady = 0x04;
    private const byte CddStatusOpen = 0x05;
    private const byte CddStatusToc = 0x09;
    private const byte CddStatusNoDisc = 0x0B;
    private const uint CbtFlagsProgramRamOffset = 0x5B24;
    private const byte CbtIpLoadPending = 0x10;

    private readonly byte[] _bios;
    private readonly byte[] _programRam = new byte[SegaCdHardwareProfile.ProgramRamBytes];
    private readonly byte[] _wordRam = new byte[SegaCdHardwareProfile.WordRamBytes];
    private readonly byte[] _backupRam = new byte[SegaCdHardwareProfile.BackupRamBytes];
    private readonly byte[] _pcmRam = new byte[SegaCdHardwareProfile.PcmRamBytes];
    private readonly byte[] _initialProgramRaw = new byte[InitialProgramSectorCount * 2048];
    private readonly byte[] _mainRegisters = new byte[SegaCdHardwareProfile.RegisterBytes];
    private readonly byte[] _mainToSubCommand = new byte[16];
    private readonly byte[] _subToMainStatus = new byte[16];
    private readonly byte[] _cdcRegisters = new byte[16];
    private readonly byte[] _cdcPacket = new byte[CdcPacketBytes];
    private readonly byte[] _cddaSector = new byte[CdAudioSectorBytes];
    private readonly PcmChannel[] _pcmChannels = new PcmChannel[PcmChannelCount];
    private readonly SegaCdSubBus _subBus;
    private byte _mainCommunicationFlags;
    private byte _subCommunicationFlags;
    private bool _genericBootMainFlag7PulseYieldPending;
    private bool _genericBootMainFlag7SubReadEdgePending;
    private double _cddInterruptCycleCarry;
    private double _cdcInterruptCycleCarry;
    private byte _cddStatusCode;
    private bool _cddStatusReady;
    private bool _cddResponseLatched;
    private int _cddSeekTicksRemaining;
    private byte _cdcAddress;
    private int _cdcPacketOffset;
    private int _cdcPacketLength;
    private int _currentCdcLba;
    private int _bootReadStartLba = -1;
    private int _bootReadSectorCount;
    private bool _bootReadStreamActive;
    private int _bootReadBulkStagedLba = -1;
    private int _bootReadBulkStagedCount;
    private int _initialProgramRawLength;
    private bool _cdcRunning;
    private int _cddaLba;
    private int _cddaSectorLba;
    private int _cddaSectorSampleIndex;
    private bool _cddaPlaying;
    private byte _pcmControlChannel;
    private ushort _pcmWriteBank;
    private bool _pcmEnabled;
    private double _pcmRenderCycleCarry;
    private bool _stickySubFlag6;
    private int _subFlag7BootClearCycles;
    private byte _bootReadyFlagClearReadsUntilReady;
    private bool _genericBootReadyFollowUpFlagPending;
    private bool _genericBootReadyEdgeReadPending;
    private bool _usesSonicCdPostBootHandoffKnown;
    private bool _usesSonicCdPostBootHandoff;
    private byte _pendingSubInterruptLevels;
    private byte _wordRamModeBits;
    private bool _wordRamOwnedByMain = true;
    private bool _suppressBootStatusUntilMainCommand;
    private bool _discTypeCommPacketPending;
    private bool _discTypeCommPacketReadyAfterClearObserved;
    private byte _discTypeCommPacketClearReadsUntilReady;
    private bool _discTypeCommPacketSyntheticEdgeUsed;
    private bool _mainBootIpOverrideAllowed = true;
    private int _pendingIpCommand23ResponseCycles;
    private byte _syntheticCommand23AckCount;
    private long _stridedCopyFastPathAttempts;
    private long _stridedCopyFastPathHits;
    private long _stridedCopyFastPathCycles;
    private int _sonicCdMmdHandoffStageAttempts;
    private int _sonicCdMmdHandoffStageSuccesses;
    private int _sonicCdMmdHandoffLastFailure;
    private int _sonicCdSpxStageAttempts;
    private int _sonicCdSpxStageSuccesses;
    private int _sonicCdSpxStageLastFailure;
    private int _sonicCdLoadFileHleAttempts;
    private int _sonicCdLoadFileHleSuccesses;
    private int _sonicCdLoadFileHleLastFailure;
    private string _sonicCdLoadFileHleLastFileName = string.Empty;
    private uint _sonicCdLoadFileHleLastDestination;
    private uint _sonicCdLoadFileHleLastReturnAddress;

    public SegaCdDevice(ReadOnlyMemory<byte> bios, SegaCdRegion region, DiscImage? disc = null)
    {
        if (bios.Length is not (SegaCdHardwareProfile.BiosSize or SegaCdHardwareProfile.ExtendedBiosSize))
        {
            throw new ArgumentException("Sega CD BIOS must be 128 KiB or 256 KiB.", nameof(bios));
        }

        _bios = bios.ToArray();
        Region = region;
        Disc = disc;
        _subBus = new SegaCdSubBus(this);
        SubCpu = new M68kCpu(_subBus);
        Array.Fill<byte>(_backupRam, 0xFF);
    }

    public SegaCdRegion Region { get; }
    public DiscImage? Disc { get; }
    public M68kCpu SubCpu { get; }
    public bool SubBiosMapped { get; private set; } = true;
    public bool SubCpuResetReleased { get; private set; }
    public bool SubCpuBusRequested { get; private set; }
    public bool SubCpuRunnable => SubCpuResetReleased && !SubCpuBusRequested;
    public ReadOnlySpan<byte> Bios => _bios;
    public ReadOnlySpan<byte> ProgramRam => _programRam;
    public ReadOnlySpan<byte> WordRam => _wordRam;
    public ReadOnlySpan<byte> BackupRam => _backupRam;
    public ReadOnlySpan<byte> PcmRam => _pcmRam;
    public ReadOnlySpan<byte> MainRegisters => _mainRegisters;
    public ReadOnlySpan<byte> CdcRegisters => _cdcRegisters;
    public int DebugCurrentCdcLba => _currentCdcLba;
    public int DebugCdcPacketOffset => _cdcPacketOffset;
    public int DebugCdcPacketLength => _cdcPacketLength;
    public byte DebugCddStatusCode => _cddStatusCode;
    public bool DebugCddaPlaying => _cddaPlaying;
    public int DebugCddaLba => _cddaLba;
    public int DebugCddaSectorLba => _cddaSectorLba;
    public int DebugCddaSectorSampleIndex => _cddaSectorSampleIndex;
    public int DebugBootReadStartLba => _bootReadStartLba;
    public int DebugBootReadSectorCount => _bootReadSectorCount;
    public bool DebugBootReadStreamActive => _bootReadStreamActive;
    public bool DebugCdcRunning => _cdcRunning;
    public int DebugSubFlag7BootClearCycles => _subFlag7BootClearCycles;
    public long DebugStridedCopyFastPathAttempts => _stridedCopyFastPathAttempts;
    public long DebugStridedCopyFastPathHits => _stridedCopyFastPathHits;
    public long DebugStridedCopyFastPathCycles => _stridedCopyFastPathCycles;
    public int DebugSonicCdMmdHandoffStageAttempts => _sonicCdMmdHandoffStageAttempts;
    public int DebugSonicCdMmdHandoffStageSuccesses => _sonicCdMmdHandoffStageSuccesses;
    public int DebugSonicCdMmdHandoffLastFailure => _sonicCdMmdHandoffLastFailure;
    public int DebugSonicCdSpxStageAttempts => _sonicCdSpxStageAttempts;
    public int DebugSonicCdSpxStageSuccesses => _sonicCdSpxStageSuccesses;
    public int DebugSonicCdSpxStageLastFailure => _sonicCdSpxStageLastFailure;
    public int DebugSonicCdLoadFileHleAttempts => _sonicCdLoadFileHleAttempts;
    public int DebugSonicCdLoadFileHleSuccesses => _sonicCdLoadFileHleSuccesses;
    public int DebugSonicCdLoadFileHleLastFailure => _sonicCdLoadFileHleLastFailure;
    public string DebugSonicCdLoadFileHleLastFileName => _sonicCdLoadFileHleLastFileName;
    public uint DebugSonicCdLoadFileHleLastDestination => _sonicCdLoadFileHleLastDestination;
    public uint DebugSonicCdLoadFileHleLastReturnAddress => _sonicCdLoadFileHleLastReturnAddress;
    public bool DebugWordRamOwnedByMain => _wordRamOwnedByMain;
    public byte DebugWordRamModeBits => _wordRamModeBits;
    public byte DebugMainWordRamModeRegister => CurrentMainWordRamModeRegister();
    public byte DebugSubWordRamModeRegister => CurrentSubWordRamModeRegister();
    public bool ShouldYieldMainForGenericBootReadyPoll =>
        Disc is not null &&
        _mainBootIpOverrideAllowed &&
        !UsesSonicCdPostBootHandoff() &&
        IsGenericBootSubStatusWaitActive();

    public bool TryConsumeGenericBootMainFlag7PulseYield()
    {
        if (!_genericBootMainFlag7PulseYieldPending ||
            Disc is null ||
            !_mainBootIpOverrideAllowed ||
            UsesSonicCdPostBootHandoff() ||
            (_mainCommunicationFlags & 0x80) == 0)
        {
            return false;
        }

        _genericBootMainFlag7PulseYieldPending = false;
        return true;
    }

    public bool MainBootIpOverrideAllowed => _mainBootIpOverrideAllowed;
    public bool MainBootIpDescriptorOverrideAvailable => _mainBootIpOverrideAllowed && Disc is not null && MainBootIpDescriptorLayoutLooksReady();
    public byte MainBiosDiscType => DiscTypeForMainBios();
    public byte MainBiosFirstTrack => Disc is { Tracks.Count: > 0 } ? ToBcdByte(Disc.Tracks[0].Number) : (byte)0x00;
    public byte MainBiosLastTrack => Disc is { Tracks.Count: > 0 } ? ToBcdByte(Disc.Tracks[^1].Number) : (byte)0x00;
    public Action<CddCommandTrace>? CddCommandObserver { get; set; }
    public Action<SegaCdRegisterTrace>? RegisterObserver { get; set; }
    public Action<SegaCdSubMemoryWriteTrace>? SubMemoryWriteObserver { get; set; }

    public void Reset()
    {
        Array.Clear(_programRam);
        Array.Clear(_wordRam);
        Array.Clear(_pcmRam);
        Array.Clear(_pcmChannels);
        Array.Clear(_mainRegisters);
        Array.Clear(_mainToSubCommand);
        Array.Clear(_subToMainStatus);
        SubBiosMapped = true;
        SubCpuResetReleased = false;
        SubCpuBusRequested = false;
        _cddInterruptCycleCarry = 0.0;
        _cdcInterruptCycleCarry = 0.0;
        _cddStatusCode = Disc is null ? CddStatusNoDisc : CddStatusIdle;
        _cddStatusReady = false;
        _cddResponseLatched = false;
        _cddSeekTicksRemaining = 0;
        Array.Clear(_cdcRegisters);
        Array.Clear(_cdcPacket);
        _cdcAddress = 0;
        _cdcPacketOffset = 0;
        _cdcPacketLength = 0;
        _currentCdcLba = 0;
        _bootReadStartLba = -1;
        _bootReadSectorCount = 0;
        _bootReadStreamActive = false;
        _bootReadBulkStagedLba = -1;
        _bootReadBulkStagedCount = 0;
        _initialProgramRawLength = 0;
        Array.Clear(_initialProgramRaw);
        _cdcRunning = false;
        _cddaLba = 0;
        _cddaSectorLba = int.MinValue;
        _cddaSectorSampleIndex = 0;
        _cddaPlaying = false;
        Array.Clear(_cddaSector);
        _pcmControlChannel = 0;
        _pcmWriteBank = 0;
        _pcmEnabled = false;
        _pcmRenderCycleCarry = 0.0;
        _stickySubFlag6 = false;
        _subFlag7BootClearCycles = 0;
        _bootReadyFlagClearReadsUntilReady = 0;
        _genericBootReadyFollowUpFlagPending = false;
        _genericBootReadyEdgeReadPending = false;
        _pendingSubInterruptLevels = 0;
        _wordRamModeBits = 0;
        _wordRamOwnedByMain = true;
        _suppressBootStatusUntilMainCommand = false;
        _discTypeCommPacketPending = false;
        _discTypeCommPacketReadyAfterClearObserved = false;
        _discTypeCommPacketClearReadsUntilReady = 0;
        _discTypeCommPacketSyntheticEdgeUsed = false;
        _mainBootIpOverrideAllowed = true;
        _genericBootMainFlag7PulseYieldPending = false;
        _genericBootMainFlag7SubReadEdgePending = false;
        _pendingIpCommand23ResponseCycles = 0;
        _syntheticCommand23AckCount = 0;
        _stridedCopyFastPathAttempts = 0;
        _stridedCopyFastPathHits = 0;
        _stridedCopyFastPathCycles = 0;
        _sonicCdMmdHandoffStageAttempts = 0;
        _sonicCdMmdHandoffStageSuccesses = 0;
        _sonicCdMmdHandoffLastFailure = 0;
        _sonicCdSpxStageAttempts = 0;
        _sonicCdSpxStageSuccesses = 0;
        _sonicCdSpxStageLastFailure = 0;
        _sonicCdLoadFileHleAttempts = 0;
        _sonicCdLoadFileHleSuccesses = 0;
        _sonicCdLoadFileHleLastFailure = 0;
        SetMainCommunicationFlags(0);
        SetSubCommunicationFlags(0);
        UpdateWordRamModeRegister();
        SubCpu.Reset();
    }

    public void UnmapSubBios()
    {
        SubBiosMapped = false;
    }

    public byte ReadBiosByte(uint address)
    {
        return _bios[address % (uint)_bios.Length];
    }

    public ushort ReadBiosWord(uint address)
    {
        return (ushort)((ReadBiosByte(address) << 8) | ReadBiosByte(address + 1));
    }

    public byte ReadProgramRamByte(uint address)
    {
        return _programRam[address & (SegaCdHardwareProfile.ProgramRamBytes - 1)];
    }

    public void WriteProgramRamByte(uint address, byte value)
    {
        uint maskedAddress = address & (SegaCdHardwareProfile.ProgramRamBytes - 1);
        _programRam[maskedAddress] = value;
        if (maskedAddress != CbtFlagsProgramRamOffset)
        {
            return;
        }

        if ((value & (CbtIpLoadPending | 0x08)) != 0)
        {
            _bootReadStreamActive = true;
        }

        if ((value & CbtIpLoadPending) == 0 && _subFlag7BootClearCycles > 1)
        {
            _subFlag7BootClearCycles = 1;
        }
    }

    public byte ReadWordRamByte(uint address)
    {
        return _wordRam[address & (SegaCdHardwareProfile.WordRamBytes - 1)];
    }

    public void WriteWordRamByte(uint address, byte value)
    {
        _wordRam[address & (SegaCdHardwareProfile.WordRamBytes - 1)] = value;
    }

    public byte ReadMainWordRamByte(uint address, uint mainPc = uint.MaxValue)
    {
        EnsureInitialProgramVisibleToMainIfNeeded(address);
        if (ShouldReadGenericBootInitialProgramLinear(address, mainPc))
        {
            return address < _initialProgramRawLength
                ? _initialProgramRaw[(int)address]
                : _wordRam[MapOneMegPhysicalToLinearAddress(address)];
        }

        ForceGenericBootInitialProgramMainViewIfNeeded(address);
        return _wordRam[MapMainWordRamAddress(address)];
    }

    public bool TryMapMainProgramRamAddress(uint address, out uint offset)
    {
        offset = 0;
        bool inLowMirror = address is >= SegaCdHardwareProfile.MainProgramRamMirrorLowStart and <= SegaCdHardwareProfile.MainProgramRamMirrorLowEndInclusive;
        bool inHighMirror = address is >= SegaCdHardwareProfile.MainProgramRamMirrorHighStart and <= SegaCdHardwareProfile.MainProgramRamMirrorHighEndInclusive;
        if ((!inLowMirror && !inHighMirror) ||
            (address & 0x03_0000) < SegaCdHardwareProfile.MainProgramRamWindowStart)
        {
            return false;
        }

        uint bankBase = (uint)(_wordRamModeBits & MainProgramRamBankMask) << 11;
        offset = (bankBase + (address & (SegaCdHardwareProfile.MainProgramRamVisibleBankBytes - 1))) &
            (SegaCdHardwareProfile.ProgramRamBytes - 1);
        return true;
    }

    public bool TryReadMainBootIpEntryLong(uint address, out uint value)
    {
        value = 0;
        if (Disc is null || !MainBootIpDescriptorLayoutLooksReady())
        {
            return false;
        }

        if (address == 2)
        {
            value = 0x00FF_0000u;
            return true;
        }

        if (address != 8)
        {
            return false;
        }

        short displacement = unchecked((short)((ReadMainWordRamByteRaw(10) << 8) | ReadMainWordRamByteRaw(11)));
        value = (uint)(0x00FF_000A + displacement) & 0x00FF_FFFF;
        return true;
    }

    public bool TryReadMainBootIpCopyLengthWord(uint address, out ushort value)
    {
        value = 0;
        if (Disc is null ||
            address != 6 ||
            !MainBootIpDescriptorLayoutLooksReady())
        {
            return false;
        }

        ushort stagedLength = (ushort)((ReadMainWordRamByteRaw(6) << 8) | ReadMainWordRamByteRaw(7));
        ushort fallbackLength = (ushort)(((InitialProgramSectorCount * 2048) / 4) - 1);
        value = stagedLength is > 0 and < 0x2000 ? stagedLength : fallbackLength;
        return true;
    }

    public bool TryReadMainBootIpPayloadLong(uint address, out uint value)
    {
        value = 0;
        if (Disc is null || address < 0x100)
        {
            return false;
        }

        uint sourceOffset = address - 0x100;
        if (sourceOffset + 3 >= InitialProgramSectorCount * 2048 ||
            !MainBootIpDescriptorLayoutLooksReady())
        {
            return false;
        }

        value =
            ((uint)ReadMainWordRamByteRaw(sourceOffset) << 24) |
            ((uint)ReadMainWordRamByteRaw(sourceOffset + 1) << 16) |
            ((uint)ReadMainWordRamByteRaw(sourceOffset + 2) << 8) |
            ReadMainWordRamByteRaw(sourceOffset + 3);
        return true;
    }

    public void DisableMainBootIpOverride()
    {
        _mainBootIpOverrideAllowed = false;
    }

    private bool MainBootIpDescriptorLayoutLooksReady()
    {
        return EntrySignatureLooksReady(
                ReadMainWordRamByteRaw(0),
                ReadMainWordRamByteRaw(1),
                ReadMainWordRamByteRaw(2),
                ReadMainWordRamByteRaw(3)) &&
            ReadMainWordRamByteRaw(4) == 0x4E &&
            ReadMainWordRamByteRaw(5) == 0xB8 &&
            ReadMainWordRamByteRaw(8) == 0x60 &&
            ReadMainWordRamByteRaw(9) == 0x00;
    }

    public void WriteMainWordRamByte(uint address, byte value)
    {
        _wordRam[MapMainWordRamAddress(address)] = value;
    }

    public bool TryHandleSonicCdIpxWordRamWaitHle(ushort command)
    {
        ushort pendingCommand = CurrentMainToSubCommandWord();
        if (pendingCommand != 0)
        {
            command = pendingCommand;
        }

        if (Disc is null ||
            !PostBootMmdHandoffStaged() ||
            _wordRamOwnedByMain ||
            command == 0 ||
            !TryResolveSonicCdMainMmdFile(command, out string fileName))
        {
            return false;
        }

        _sonicCdLoadFileHleAttempts++;
        if (!Disc.TryReadIso9660File(fileName, out byte[] fileData))
        {
            _sonicCdLoadFileHleLastFailure = 3;
            return false;
        }

        StageSonicCdMainMmdInWordRam(fileData);
        _wordRamOwnedByMain = true;
        _wordRamModeBits = (byte)((_wordRamModeBits & unchecked((byte)~(WordRamOneMegMode | WordRamMainAssignsToSub))) |
            WordRamReturnToMain);
        UpdateWordRamModeRegister();

        _subToMainStatus[0] = (byte)(command >> 8);
        _subToMainStatus[1] = (byte)command;
        UpdateCommunicationWindowRegisters();
        ParkSubCpuAtSonicCdSpxCommandWait();

        _sonicCdLoadFileHleSuccesses++;
        _sonicCdLoadFileHleLastFailure = 0;
        _sonicCdLoadFileHleLastFileName = fileName;
        _sonicCdLoadFileHleLastDestination = SegaCdHardwareProfile.SubWordRamStart;
        _sonicCdLoadFileHleLastReturnAddress = SubCpu.PC;
        return true;
    }

    public bool TryReturnSonicCdIpxWordRamToMainHle()
    {
        if (Disc is null ||
            !PostBootMmdHandoffStaged() ||
            _wordRamOwnedByMain ||
            CurrentMainToSubCommandWord() != 0)
        {
            return false;
        }

        _wordRamOwnedByMain = true;
        _wordRamModeBits |= WordRamReturnToMain;
        UpdateWordRamModeRegister();
        return true;
    }

    private ushort CurrentMainToSubCommandWord()
    {
        return (ushort)((_mainToSubCommand[0] << 8) | _mainToSubCommand[1]);
    }

    public byte ReadSubOneMegWordRamByte(uint address)
    {
        return _wordRam[MapSubOneMegWordRamAddress(address)];
    }

    public void WriteSubOneMegWordRamByte(uint address, byte value)
    {
        _wordRam[MapSubOneMegWordRamAddress(address)] = value;
    }

    public byte ReadBackupRamByte(uint address)
    {
        return _backupRam[address & (SegaCdHardwareProfile.BackupRamBytes - 1)];
    }

    public void WriteBackupRamByte(uint address, byte value)
    {
        _backupRam[address & (SegaCdHardwareProfile.BackupRamBytes - 1)] = value;
    }

    public byte ReadPcmRamByte(uint address)
    {
        return _pcmRam[address & (SegaCdHardwareProfile.PcmRamBytes - 1)];
    }

    public void WritePcmRamByte(uint address, byte value)
    {
        _pcmRam[address & (SegaCdHardwareProfile.PcmRamBytes - 1)] = value;
    }

    public byte ReadPcmMappedByte(uint offset)
    {
        if ((offset & 1) == 0)
        {
            return 0xFF;
        }

        uint chipOffset = (offset - 1) >> 1;
        if (chipOffset is >= 0x0010 and <= 0x001F)
        {
            int channel = (int)((chipOffset & 0x0E) >> 1);
            int shift = (chipOffset & 1) == 0 ? 11 : 19;
            return (byte)(_pcmChannels[channel].Address >> shift);
        }

        if (chipOffset is >= 0x1000 and <= 0x1FFF)
        {
            return _pcmRam[(_pcmWriteBank | (ushort)(chipOffset & 0x0FFF)) & (SegaCdHardwareProfile.PcmRamBytes - 1)];
        }

        return 0xFF;
    }

    public void WritePcmMappedByte(uint offset, byte value)
    {
        if ((offset & 1) == 0)
        {
            return;
        }

        uint chipOffset = (offset - 1) >> 1;
        if (chipOffset <= 0x0008)
        {
            WritePcmRegister((int)chipOffset, value);
            return;
        }

        if (chipOffset is >= 0x1000 and <= 0x1FFF)
        {
            _pcmRam[(_pcmWriteBank | (ushort)(chipOffset & 0x0FFF)) & (SegaCdHardwareProfile.PcmRamBytes - 1)] = value;
        }
    }

    public byte ReadMainRegisterByte(uint offset, uint mainPc = uint.MaxValue)
    {
        uint maskedOffset = offset & (SegaCdHardwareProfile.RegisterBytes - 1);
        AdvanceIpCommand23ResponseOnMainStatusPoll(maskedOffset);
        byte value = _mainRegisters[maskedOffset];
        if (maskedOffset == MainToSubFlagOffset)
        {
            value = _mainCommunicationFlags;
        }
        else if (maskedOffset == SubToMainFlagOffset)
        {
            value = _subCommunicationFlags;
        }
        else if (maskedOffset == WordRamModeLowOffset)
        {
            value = CurrentMainWordRamModeRegister();
        }
        else if (maskedOffset is >= 0x10 and <= 0x1F)
        {
            value = _mainToSubCommand[maskedOffset - 0x10];
        }
        else if (maskedOffset is >= 0x20 and <= 0x2F)
        {
            value = _subToMainStatus[maskedOffset - 0x20];
        }

        value = ClearConsumedGenericBootDiscTypeReadyFlagIfNeeded(maskedOffset, value, mainPc);
        value = ForceGenericBootReadyEdgeReadIfNeeded(maskedOffset, value, mainPc);
        value = ForceGenericBootWordRamRendezvousFlagReadIfNeeded(maskedOffset, value, mainPc);
        value = ForceGenericBootCdcServiceReadyEdgeReadIfNeeded(maskedOffset, value, mainPc);
        value = ForceGenericBootDiscTypeSecondReadyEdgeIfNeeded(maskedOffset, value, mainPc);
        TraceRegister("main-read8", maskedOffset, value);
        ClearGenericSubReadyFlagAfterMainReadIfNeeded(maskedOffset, value);
        RaiseBootReadyFlagAfterClearObservedIfNeeded(maskedOffset, value);
        ClearGenericBootReadyEdgeAfterMainReadIfNeeded(maskedOffset, value, mainPc);
        RaiseGenericBootReadyFollowUpFlagIfNeeded(maskedOffset, mainPc);
        RearmDiscTypeCommPacketAfterClearObservedIfNeeded(maskedOffset, value);
        return value;
    }

    public ushort ReadMainRegisterWord(uint offset)
    {
        return (ushort)((ReadMainRegisterByte(offset) << 8) | ReadMainRegisterByte(offset + 1));
    }

    public byte ReadSubRegisterByte(uint offset)
    {
        uint maskedOffset = offset & (SegaCdHardwareProfile.RegisterBytes - 1);
        byte value = _mainRegisters[maskedOffset];
        if (maskedOffset == MainToSubFlagOffset)
        {
            value = _mainCommunicationFlags;
        }
        else if (maskedOffset == SubToMainFlagOffset)
        {
            value = _subCommunicationFlags;
        }
        else if (maskedOffset == MainControlLowOffset && SubCpuResetReleased)
        {
            value |= MainControlSubResetRelease;
        }
        else if (maskedOffset == WordRamModeLowOffset)
        {
            value = CurrentSubWordRamModeRegister();
        }
        else if (maskedOffset is >= 0x10 and <= 0x1F)
        {
            value = _mainToSubCommand[maskedOffset - 0x10];
        }
        else if (maskedOffset is >= 0x20 and <= 0x2F)
        {
            value = _subToMainStatus[maskedOffset - 0x20];
        }
        else if (maskedOffset == CddControlOffset)
        {
            value = (byte)(value & CddHockBit);
        }
        else if (maskedOffset == CdcTransferOffset)
        {
            value = CurrentCdcTransferRegister();
        }
        else if (maskedOffset == CdcRegisterOffset)
        {
            value = ReadCdcRegister();
        }

        value = ForceGenericBootMainFlag7SubReadEdgeIfNeeded(maskedOffset, value);
        TraceRegister("read8", maskedOffset, value);
        if (maskedOffset == CddStatusStart + CddPacketBytes - 1)
        {
            _cddResponseLatched = false;
        }

        return value;
    }

    public ushort ReadSubRegisterWord(uint offset)
    {
        if ((offset & (SegaCdHardwareProfile.RegisterBytes - 1)) == CdcDataOffset)
        {
            ushort value = ReadCdcDataWord();
            TraceRegister("read16", CdcDataOffset, value);
            return value;
        }

        return (ushort)((ReadSubRegisterByte(offset) << 8) | ReadSubRegisterByte(offset + 1));
    }

    public void WriteMainRegisterByte(uint offset, byte value)
    {
        uint maskedOffset = offset & (SegaCdHardwareProfile.RegisterBytes - 1);
        if (maskedOffset == MainToSubFlagOffset)
        {
            byte previousFlags = _mainCommunicationFlags;
            SetMainCommunicationFlags(value);
            TraceRegister("main-write8", maskedOffset, value);
            if ((value & 0x04) != 0 && (((previousFlags & 0x04) == 0) || !DiscTypeCommPacketPresent()))
            {
                PublishDiscTypeCommPacketIfNeeded(value);
            }
            else if ((previousFlags & 0x04) != 0 && (value & 0x04) == 0)
            {
                ClearDiscTypeCommPacketStatusIfPresent();
            }

            ClearGenericBootReadyFlagOnMainAckIfNeeded(value);
            ScheduleSubFlag7BootClearIfNeeded(_subCommunicationFlags);
            ClearStickySubFlag6AfterMainRamRequestClearIfNeeded(previousFlags, value);
            return;
        }

        if (maskedOffset == SubToMainFlagOffset)
        {
            TraceRegister("main-write8-ignored", maskedOffset, value);
            return;
        }

        if (maskedOffset is >= 0x10 and <= 0x1F)
        {
            byte previousCommandValue = _mainToSubCommand[maskedOffset - 0x10];
            _mainToSubCommand[maskedOffset - 0x10] = value;
            _suppressBootStatusUntilMainCommand = false;
            ClearPostBootCommand23AckOnCommandClearIfNeeded(maskedOffset, value, previousCommandValue);
            ClearSonicCdBramInitAckOnCommandClearIfNeeded(maskedOffset, value, previousCommandValue);
            ClearDiscTypeCommPacketOnCommandClearIfNeeded(maskedOffset, value, previousCommandValue);
            ClearDiscTypeCommPacketOnSplitCommandStartIfNeeded(maskedOffset, value);
            ScheduleIpCommand23ResponseIfNeeded(maskedOffset, value);
            AcknowledgePostBootCommand23IfNeeded(maskedOffset, value);
            if (value != 0)
            {
                SetMainCommunicationFlags((byte)(_mainCommunicationFlags | 0x80));
                if ((_mainRegisters[MainInterruptOffset] & MainInterruptSubCpuLevel2) != 0)
                {
                    QueueSubInterrupt(2);
                }
            }
        }

        if (maskedOffset == MainInterruptOffset)
        {
            if ((value & MainInterruptSubCpuLevel2) != 0)
            {
                QueueSubInterrupt(2);
            }

            value = (byte)(value & ~MainInterruptSubCpuLevel2);
        }

        _mainRegisters[maskedOffset] = value;
        TraceRegister("main-write8", maskedOffset, value);

        if (maskedOffset == WordRamModeLowOffset)
        {
            WriteMainWordRamMode(value);
        }

        if (maskedOffset == MainControlLowOffset)
        {
            ApplyMainControlLowByte(value);
        }

    }

    public void WriteMainRegisterWord(uint offset, ushort value)
    {
        WriteMainRegisterByte(offset, (byte)(value >> 8));
        WriteMainRegisterByte(offset + 1, (byte)value);
    }

    public void WriteSubRegisterByte(uint offset, byte value)
    {
        uint maskedOffset = offset & (SegaCdHardwareProfile.RegisterBytes - 1);
        if (maskedOffset == MainToSubFlagOffset)
        {
            TraceRegister("write8-ignored", maskedOffset, value);
            return;
        }

        if (maskedOffset == SubToMainFlagOffset)
        {
            SetSubCommunicationFlags(value);
            TraceRegister("write8", maskedOffset, value);
            AcknowledgePendingPostBootCommand23OnSubReadyIfNeeded(value);
            PublishDiscTypeCommPacketIfNeeded(value);
            CompleteWordRamRendezvousIfNeeded(value);
            ScheduleSubFlag7BootClearIfNeeded(value);
            if (_stickySubFlag6)
            {
                SetSubCommunicationFlags((byte)(_subCommunicationFlags | 0x40));
                AcknowledgePendingPostBootCommand23OnSubReadyIfNeeded(_subCommunicationFlags);
            }

            return;
        }

        if (_suppressBootStatusUntilMainCommand && maskedOffset is >= 0x20 and <= 0x2F)
        {
            TraceRegister("write8-suppressed", maskedOffset, value);
            return;
        }

        if ((_discTypeCommPacketPending || _discTypeCommPacketReadyAfterClearObserved) &&
            _mainBootIpOverrideAllowed &&
            (_mainCommunicationFlags & 0x02) == 0 &&
            DiscTypeCommPacketPresent() &&
            maskedOffset is >= 0x20 and <= 0x23)
        {
            TraceRegister("write8-suppressed-disc-type", maskedOffset, value);
            return;
        }

        if (maskedOffset is >= 0x20 and <= 0x2F)
        {
            _subToMainStatus[maskedOffset - 0x20] = value;
        }

        _mainRegisters[maskedOffset] = value;
        TraceRegister("write8", maskedOffset, value);
        if (_discTypeCommPacketPending && maskedOffset is >= 0x20 and <= 0x23 && !DiscTypeCommPacketPresent())
        {
            ClearDiscTypeCommPacketReadyFlag();
        }

        if (maskedOffset == MainControlLowOffset)
        {
            _mainRegisters[maskedOffset] = CurrentSubResetRegister();
            return;
        }

        if (maskedOffset == CdcAddressOffset)
        {
            _cdcAddress = (byte)(value & 0x0F);
            return;
        }

        if (maskedOffset == CdcRegisterOffset)
        {
            WriteCdcRegister(value);
            return;
        }

        if (maskedOffset == CdcTransferOffset)
        {
            _mainRegisters[maskedOffset] = value;
            return;
        }

        if (maskedOffset == GfxTraceVectorLowOffset)
        {
            RunGraphicsOperation();
            QueueSubInterruptIfEnabled(1);
        }

        if (maskedOffset == CddControlOffset && (value & CddHockBit) != 0)
        {
            if (!_cddStatusReady)
            {
                RefreshCddStatusRegisters();
                RaiseSubToMainFlag(0x01);
                QueueSubInterrupt(CddInterruptLevel);
            }

            return;
        }

        if (maskedOffset == WordRamModeLowOffset)
        {
            WriteSubWordRamMode(value);
            return;
        }

        if (maskedOffset == CddCommandStart + CddPacketBytes - 1)
        {
            ProcessCddCommand();
        }
    }

    public void WriteSubRegisterWord(uint offset, ushort value)
    {
        WriteSubRegisterByte(offset, (byte)(value >> 8));
        WriteSubRegisterByte(offset + 1, (byte)value);
    }

    public void PulseMainVBlankInterrupt()
    {
        QueueSubInterruptIfEnabled(2);
    }

    public SegaCdState CaptureState()
    {
        return new SegaCdState(
            (byte[])_programRam.Clone(),
            (byte[])_wordRam.Clone(),
            (byte[])_backupRam.Clone(),
            (byte[])_pcmRam.Clone(),
            (byte[])_mainRegisters.Clone(),
            (byte[])_mainToSubCommand.Clone(),
            (byte[])_subToMainStatus.Clone(),
            SubBiosMapped,
            SubCpuResetReleased,
            SubCpuBusRequested,
            _cddInterruptCycleCarry,
            _cdcInterruptCycleCarry,
            _cddStatusCode,
            _cddStatusReady,
            _cddResponseLatched,
            _cddSeekTicksRemaining,
            (byte[])_cdcRegisters.Clone(),
            (byte[])_cdcPacket.Clone(),
            _cdcAddress,
            _cdcPacketOffset,
            _cdcPacketLength,
            _currentCdcLba,
            _bootReadStartLba,
            _bootReadSectorCount,
            _bootReadStreamActive,
            _cdcRunning,
            _cddaLba,
            _cddaSectorLba,
            _cddaSectorSampleIndex,
            _cddaPlaying,
            (byte[])_cddaSector.Clone(),
            CapturePcmChannels(),
            _pcmControlChannel,
            _pcmWriteBank,
            _pcmEnabled,
            _pcmRenderCycleCarry,
            _stickySubFlag6,
            _subFlag7BootClearCycles,
            _bootReadyFlagClearReadsUntilReady,
            _genericBootReadyFollowUpFlagPending,
            _genericBootReadyEdgeReadPending,
            _genericBootMainFlag7PulseYieldPending,
            _genericBootMainFlag7SubReadEdgePending,
            _pendingSubInterruptLevels,
            _wordRamModeBits,
            _wordRamOwnedByMain,
            _suppressBootStatusUntilMainCommand,
            _mainCommunicationFlags,
            _subCommunicationFlags,
            _discTypeCommPacketPending,
            _discTypeCommPacketReadyAfterClearObserved,
            _discTypeCommPacketClearReadsUntilReady,
            _discTypeCommPacketSyntheticEdgeUsed,
            _mainBootIpOverrideAllowed,
            _syntheticCommand23AckCount,
            SubCpu.CaptureState());
    }

    public void RestoreState(SegaCdState state)
    {
        CopyInto(state.ProgramRam, _programRam);
        CopyInto(state.WordRam, _wordRam);
        CopyInto(state.BackupRam, _backupRam);
        CopyInto(state.PcmRam, _pcmRam);
        CopyInto(state.MainRegisters, _mainRegisters);
        CopyInto(state.MainToSubCommand, _mainToSubCommand);
        CopyInto(state.SubToMainStatus, _subToMainStatus);
        SubBiosMapped = state.SubBiosMapped;
        SubCpuResetReleased = state.SubCpuResetReleased;
        SubCpuBusRequested = state.SubCpuBusRequested;
        _cddInterruptCycleCarry = state.CddInterruptCycleCarry;
        _cdcInterruptCycleCarry = state.CdcInterruptCycleCarry;
        _cddStatusCode = state.CddStatusCode;
        _cddStatusReady = state.CddStatusReady;
        _cddResponseLatched = state.CddResponseLatched;
        _cddSeekTicksRemaining = state.CddSeekTicksRemaining;
        CopyInto(state.CdcRegisters, _cdcRegisters);
        CopyInto(state.CdcPacket, _cdcPacket);
        _cdcAddress = state.CdcAddress;
        _cdcPacketOffset = state.CdcPacketOffset;
        _cdcPacketLength = state.CdcPacketLength;
        _currentCdcLba = state.CurrentCdcLba;
        _bootReadStartLba = state.BootReadStartLba;
        _bootReadSectorCount = state.BootReadSectorCount;
        _bootReadStreamActive = state.BootReadStreamActive;
        _cdcRunning = state.CdcRunning;
        _cddaLba = state.CddaLba;
        _cddaSectorLba = state.CddaSectorLba;
        _cddaSectorSampleIndex = state.CddaSectorSampleIndex;
        _cddaPlaying = state.CddaPlaying;
        CopyInto(state.CddaSector, _cddaSector);
        RestorePcmChannels(state.PcmChannels);
        _pcmControlChannel = state.PcmControlChannel;
        _pcmWriteBank = state.PcmWriteBank;
        _pcmEnabled = state.PcmEnabled;
        _pcmRenderCycleCarry = state.PcmRenderCycleCarry;
        _stickySubFlag6 = state.StickySubFlag6;
        _subFlag7BootClearCycles = state.SubFlag7BootClearCycles;
        _bootReadyFlagClearReadsUntilReady = state.BootReadyFlagClearReadsUntilReady;
        _genericBootReadyFollowUpFlagPending = state.GenericBootReadyFollowUpFlagPending;
        _genericBootReadyEdgeReadPending = state.GenericBootReadyEdgeReadPending;
        _genericBootMainFlag7PulseYieldPending = state.GenericBootMainFlag7PulseYieldPending;
        _genericBootMainFlag7SubReadEdgePending = state.GenericBootMainFlag7SubReadEdgePending;
        _pendingSubInterruptLevels = state.PendingSubInterruptLevels;
        _wordRamModeBits = state.WordRamModeBits;
        _wordRamOwnedByMain = state.WordRamOwnedByMain;
        _suppressBootStatusUntilMainCommand = state.SuppressBootStatusUntilMainCommand;
        _mainCommunicationFlags = state.MainCommunicationFlags;
        _subCommunicationFlags = state.SubCommunicationFlags;
        _discTypeCommPacketPending = state.DiscTypeCommPacketPending;
        _discTypeCommPacketReadyAfterClearObserved = state.DiscTypeCommPacketReadyAfterClearObserved;
        _discTypeCommPacketClearReadsUntilReady = state.DiscTypeCommPacketClearReadsUntilReady;
        _discTypeCommPacketSyntheticEdgeUsed = state.DiscTypeCommPacketSyntheticEdgeUsed;
        _mainBootIpOverrideAllowed = state.MainBootIpOverrideAllowed;
        _syntheticCommand23AckCount = state.SyntheticCommand23AckCount;
        UpdateCommunicationFlagRegisters();
        UpdateCommunicationWindowRegisters();
        UpdateWordRamModeRegister();
        SubCpu.RestoreState(state.SubCpu);
    }

    public int RunSubCpuCycles(int cycleBudget, Func<bool>? shouldAbort = null)
    {
        if (cycleBudget <= 0 || !SubCpuRunnable)
        {
            return 0;
        }

        int executed = 0;
        while (executed < cycleBudget)
        {
            if (shouldAbort?.Invoke() == true)
            {
                return -1;
            }

            ServicePendingSubInterrupts();
            SeedBootCdcRingFromSubBiosLoopIfNeeded();
            if (TryHandleSonicCdSystemLoadFileHle(out int hleCycles))
            {
                executed += hleCycles;
                AdvanceSubFlag7BootClear(hleCycles);
                continue;
            }

            if (TryHandleSonicCdSystemIrq2Hle(out int irq2Cycles))
            {
                executed += irq2Cycles;
                AdvanceSubFlag7BootClear(irq2Cycles);
                continue;
            }

            if (TryHandleSonicCdSystemFileFuncHle(out int fileFuncCycles))
            {
                executed += fileFuncCycles;
                AdvanceSubFlag7BootClear(fileFuncCycles);
                continue;
            }

            if (TryHandleSonicCdSpxCommandWaitHle(out int spxCommandCycles))
            {
                executed += spxCommandCycles;
                AdvanceSubFlag7BootClear(spxCommandCycles);
                continue;
            }

            if (TryHandleSonicCdBramSubDoneWaitHle(out int bramSubCycles))
            {
                executed += bramSubCycles;
                AdvanceSubFlag7BootClear(bramSubCycles);
                continue;
            }

            if (TryHandleSonicCdTitleSubDoneHle(out int titleDoneCycles))
            {
                executed += titleDoneCycles;
                AdvanceSubFlag7BootClear(titleDoneCycles);
                continue;
            }

            if (TryHandleSonicCdTitleSubReadyHle(out int titleSubCycles))
            {
                executed += titleSubCycles;
                AdvanceSubFlag7BootClear(titleSubCycles);
                continue;
            }

            if (TryHandleSonicCdTitleSubIdleHle(out int titleIdleCycles))
            {
                executed += titleIdleCycles;
                AdvanceSubFlag7BootClear(titleIdleCycles);
                continue;
            }

            if (TryHandleSonicCdBiosIrq2WaitHle(out int irq2WaitCycles))
            {
                executed += irq2WaitCycles;
                AdvanceSubFlag7BootClear(irq2WaitCycles);
                continue;
            }

            if (TryHandleGenericSegaCdBiosIrq2WaitHle(out int genericIrq2WaitCycles))
            {
                executed += genericIrq2WaitCycles;
                AdvanceSubFlag7BootClear(genericIrq2WaitCycles);
                continue;
            }

            if (TryHandleGenericSegaCdBiosStatusWaitHle(out int genericBiosWaitCycles))
            {
                executed += genericBiosWaitCycles;
                AdvanceSubFlag7BootClear(genericBiosWaitCycles);
                continue;
            }

            if (TryHandleGenericSegaCdWordRamAssignWaitHle(out int genericWordRamWaitCycles))
            {
                executed += genericWordRamWaitCycles;
                AdvanceSubFlag7BootClear(genericWordRamWaitCycles);
                continue;
            }

            _stridedCopyFastPathAttempts++;
            if (SubCpu.TryFastForwardMoveBytePostIncrementStridedCopyDbfLoop(cycleBudget - executed, IsSubFastByteCopyAddress, out int stridedCopyCycles, out _))
            {
                _stridedCopyFastPathHits++;
                _stridedCopyFastPathCycles += stridedCopyCycles;
                executed += stridedCopyCycles;
                AdvanceSubFlag7BootClear(stridedCopyCycles);
                continue;
            }

            if (SubCpu.TryFastForwardMoveBytePostIncrementCopyDbfLoop(cycleBudget - executed, IsSubFastByteCopyAddress, out int copyCycles, out _))
            {
                executed += copyCycles;
                AdvanceSubFlag7BootClear(copyCycles);
                continue;
            }

            if (SubCpu.TryFastForwardAddWordPostIncrementNestedDbfLoop(cycleBudget - executed, out int fastCycles, out _))
            {
                executed += fastCycles;
                AdvanceSubFlag7BootClear(fastCycles);
                continue;
            }

            int cycles = SubCpu.Step();
            if (cycles <= 0)
            {
                break;
            }

            executed += cycles;
            AdvanceSubFlag7BootClear(cycles);
        }

        AdvanceCddInterrupts(executed);
        AdvanceCdcInterrupts(executed);
        AdvanceIpCommand23Response(executed);
        return executed;
    }

    private static bool IsSubFastByteCopyAddress(uint address)
    {
        address &= 0x00FF_FFFF;
        return address is >= SegaCdHardwareProfile.SubProgramRamStart and <= SegaCdHardwareProfile.SubProgramRamEndInclusive ||
            address is >= SegaCdHardwareProfile.SubWordRamStart and <= SegaCdHardwareProfile.SubWordRamEndInclusive ||
            address is >= SegaCdHardwareProfile.SubWordRam1MStart and <= SegaCdHardwareProfile.SubWordRam1MEndInclusive ||
            address is >= SegaCdHardwareProfile.SubBackupRamStart and <= SegaCdHardwareProfile.SubBackupRamEndInclusive ||
            address is >= SegaCdHardwareProfile.SubPcmRamStart and <= SegaCdHardwareProfile.SubPcmRamEndInclusive;
    }

    public void RenderCddaStereoSamplesInto(Span<short> output, int samples)
    {
        if (samples <= 0)
        {
            return;
        }

        if (output.Length < samples * 2)
        {
            throw new ArgumentException("CD-DA output buffer is too small.", nameof(output));
        }

        if (!_cddaPlaying || Disc is null || _cddStatusCode != CddStatusPlay)
        {
            return;
        }

        for (int sample = 0; sample < samples; sample++)
        {
            if (!EnsureCddaSectorLoaded())
            {
                StopCddaPlayback(CddStatusReady);
                break;
            }

            int sectorOffset = _cddaSectorSampleIndex * 4;
            short left = ReadLittleEndianInt16(_cddaSector, sectorOffset);
            short right = ReadLittleEndianInt16(_cddaSector, sectorOffset + 2);
            int outputIndex = sample * 2;
            output[outputIndex] = AddSamples(output[outputIndex], left);
            output[outputIndex + 1] = AddSamples(output[outputIndex + 1], right);
            AdvanceCddaSample();
        }

        _currentCdcLba = _cddaLba;
    }

    public void RenderPcmStereoSamplesInto(Span<short> output, int samples, int sampleRate = 44_100)
    {
        if (samples <= 0)
        {
            return;
        }

        if (output.Length < samples * 2)
        {
            throw new ArgumentException("PCM output buffer is too small.", nameof(output));
        }

        if (!_pcmEnabled)
        {
            return;
        }

        double pcmSamplesPerOutputSample = (SegaCdHardwareProfile.SubCpuClockHz / (double)PcmStreamClockDivider) / sampleRate;
        for (int sampleIndex = 0; sampleIndex < samples; sampleIndex++)
        {
            int left = 0;
            int right = 0;
            for (int channel = 0; channel < PcmChannelCount; channel++)
            {
                PcmChannel pcm = _pcmChannels[channel];
                if (!pcm.Enabled)
                {
                    continue;
                }

                int sample = DecodePcmSample(PeekPcmChannelSample(ref pcm, channel));
                int leftLevel = (pcm.Pan & 0x0F) * pcm.Envelope;
                int rightLevel = ((pcm.Pan >> 4) & 0x0F) * pcm.Envelope;
                left += (sample * leftLevel) >> 5;
                right += (sample * rightLevel) >> 5;
                _pcmChannels[channel] = pcm;
            }

            int outputIndex = sampleIndex * 2;
            output[outputIndex] = AddSamples(output[outputIndex], (short)Math.Clamp(left, short.MinValue, short.MaxValue));
            output[outputIndex + 1] = AddSamples(output[outputIndex + 1], (short)Math.Clamp(right, short.MinValue, short.MaxValue));

            _pcmRenderCycleCarry += pcmSamplesPerOutputSample;
            int advances = (int)_pcmRenderCycleCarry;
            if (advances <= 0)
            {
                continue;
            }

            _pcmRenderCycleCarry -= advances;
            AdvancePcmChannels(advances);
        }
    }

    private static void CopyInto(byte[] source, byte[] destination)
    {
        Array.Clear(destination);
        Array.Copy(source, destination, Math.Min(source.Length, destination.Length));
    }

    private PcmChannelState[] CapturePcmChannels()
    {
        PcmChannelState[] state = new PcmChannelState[PcmChannelCount];
        for (int i = 0; i < PcmChannelCount; i++)
        {
            PcmChannel channel = _pcmChannels[i];
            state[i] = new PcmChannelState(
                channel.Enabled,
                channel.Envelope,
                channel.Pan,
                channel.Start,
                channel.Address,
                channel.Step,
                channel.LoopStart);
        }

        return state;
    }

    private void RestorePcmChannels(PcmChannelState[] state)
    {
        Array.Clear(_pcmChannels);
        for (int i = 0; i < Math.Min(PcmChannelCount, state.Length); i++)
        {
            PcmChannelState channel = state[i];
            _pcmChannels[i] = new PcmChannel
            {
                Enabled = channel.Enabled,
                Envelope = channel.Envelope,
                Pan = channel.Pan,
                Start = channel.Start,
                Address = channel.Address,
                Step = channel.Step,
                LoopStart = channel.LoopStart,
            };
        }
    }

    private void WritePcmRegister(int register, byte value)
    {
        if (register < 0 || register > 0x08)
        {
            return;
        }

        ref PcmChannel channel = ref _pcmChannels[_pcmControlChannel & 0x07];
        switch (register)
        {
            case 0x00:
                channel.Envelope = value;
                break;
            case 0x01:
                channel.Pan = value;
                break;
            case 0x02:
                channel.Step = (ushort)((channel.Step & 0xFF00) | value);
                break;
            case 0x03:
                channel.Step = (ushort)((channel.Step & 0x00FF) | (value << 8));
                break;
            case 0x04:
                channel.LoopStart = (ushort)((channel.LoopStart & 0xFF00) | value);
                break;
            case 0x05:
                channel.LoopStart = (ushort)((channel.LoopStart & 0x00FF) | (value << 8));
                break;
            case 0x06:
                channel.Start = value;
                if (!channel.Enabled)
                {
                    channel.Address = (uint)value << 19;
                }

                break;
            case 0x07:
                _pcmEnabled = (value & 0x80) != 0;
                if ((value & 0x40) != 0)
                {
                    _pcmControlChannel = (byte)(value & 0x07);
                }
                else
                {
                    _pcmWriteBank = (ushort)((value & 0x0F) << 12);
                }

                break;
            case 0x08:
                for (int i = 0; i < PcmChannelCount; i++)
                {
                    bool enabled = ((value >> i) & 1) == 0;
                    _pcmChannels[i].Enabled = enabled;
                    if (!enabled)
                    {
                        _pcmChannels[i].Address = (uint)_pcmChannels[i].Start << 19;
                    }
                }

                break;
        }
    }

    private byte PeekPcmChannelSample(ref PcmChannel channel, int channelIndex)
    {
        byte sample = _pcmRam[(channel.Address >> 11) & (SegaCdHardwareProfile.PcmRamBytes - 1)];
        if (sample != 0xFF)
        {
            return sample;
        }

        channel.Address = (uint)channel.LoopStart << 11;
        sample = _pcmRam[(channel.Address >> 11) & (SegaCdHardwareProfile.PcmRamBytes - 1)];
        if (sample == 0xFF)
        {
            channel.Enabled = false;
            _pcmChannels[channelIndex] = channel;
            return 0x80;
        }

        return sample;
    }

    private void AdvancePcmChannels(int samples)
    {
        for (int channelIndex = 0; channelIndex < PcmChannelCount; channelIndex++)
        {
            PcmChannel channel = _pcmChannels[channelIndex];
            if (!channel.Enabled)
            {
                continue;
            }

            for (int i = 0; i < samples; i++)
            {
                _ = PeekPcmChannelSample(ref channel, channelIndex);
                if (!channel.Enabled)
                {
                    break;
                }

                channel.Address += channel.Step;
            }

            _pcmChannels[channelIndex] = channel;
        }
    }

    private static int DecodePcmSample(byte sample)
    {
        int magnitude = sample & 0x7F;
        return (sample & 0x80) != 0 ? magnitude : -magnitude;
    }

    private void ApplyMainControlLowByte(byte value)
    {
        bool nextResetReleased = (value & MainControlSubResetRelease) != 0;
        bool resetRisingEdge = !SubCpuResetReleased && nextResetReleased;
        SubCpuResetReleased = nextResetReleased;
        SubCpuBusRequested = (value & MainControlSubBusRequest) != 0;
        if (resetRisingEdge)
        {
            SubBiosMapped = false;
            SubCpu.Reset();
        }
        else if (!SubCpuResetReleased)
        {
            SubBiosMapped = true;
        }
    }

    private byte CurrentSubResetRegister()
    {
        byte value = 0;
        if (SubCpuResetReleased)
        {
            value |= MainControlSubResetRelease;
        }

        if (SubCpuBusRequested)
        {
            value |= MainControlSubBusRequest;
        }

        return value;
    }

    private byte CurrentMainWordRamModeRegister()
    {
        byte value = (byte)(_wordRamModeBits & (WordRamMainAssignsToSub | WordRamGraphicsPriorityMask | MainProgramRamBankMask));
        if (!_wordRamOwnedByMain)
        {
            value |= WordRamOneMegMode;
        }

        if (_wordRamOwnedByMain)
        {
            value |= WordRamReturnToMain;
        }

        return value;
    }

    private byte CurrentSubWordRamModeRegister()
    {
        byte value = (byte)(_wordRamModeBits & (WordRamOneMegMode | WordRamMainAssignsToSub | WordRamGraphicsPriorityMask | MainProgramRamBankMask));
        if ((_wordRamModeBits & WordRamOneMegMode) != 0)
        {
            value |= (byte)(_wordRamModeBits & WordRamReturnToMain);
        }
        else if (_wordRamOwnedByMain)
        {
            value |= WordRamReturnToMain;
        }

        return value;
    }

    private int MapMainWordRamAddress(uint address)
    {
        if ((_wordRamModeBits & WordRamOneMegMode) == 0)
        {
            return (int)(address & (SegaCdHardwareProfile.WordRamBytes - 1));
        }

        return MapOneMegWordRamAddress(address, (_wordRamModeBits & WordRamReturnToMain) != 0);
    }

    private int MapSubOneMegWordRamAddress(uint address)
    {
        return MapOneMegWordRamAddress(address, (_wordRamModeBits & WordRamReturnToMain) == 0);
    }

    private static int MapOneMegWordRamAddress(uint address, bool select)
    {
        uint bankBase = select ? (uint)SegaCdHardwareProfile.WordRamBytes / 2u : 0;
        return (int)((bankBase + (address & ((uint)SegaCdHardwareProfile.WordRamBytes / 2u - 1))) &
            (SegaCdHardwareProfile.WordRamBytes - 1));
    }

    private void RearrangeWordRamForModeChange(bool toOneMegMode)
    {
        byte[] source = (byte[])_wordRam.Clone();
        int bankBytes = SegaCdHardwareProfile.WordRamBytes / 2;
        int bankWords = bankBytes / 2;

        for (int word = 0; word < bankWords; word++)
        {
            if (toOneMegMode)
            {
                int sourcePair = word * 4;
                int bank0 = word * 2;
                int bank1 = bankBytes + bank0;
                _wordRam[bank0] = source[sourcePair];
                _wordRam[bank0 + 1] = source[sourcePair + 1];
                _wordRam[bank1] = source[sourcePair + 2];
                _wordRam[bank1 + 1] = source[sourcePair + 3];
            }
            else
            {
                int targetPair = word * 4;
                int bank0 = word * 2;
                int bank1 = bankBytes + bank0;
                _wordRam[targetPair] = source[bank0];
                _wordRam[targetPair + 1] = source[bank0 + 1];
                _wordRam[targetPair + 2] = source[bank1];
                _wordRam[targetPair + 3] = source[bank1 + 1];
            }
        }
    }

    private void UpdateWordRamModeRegister()
    {
        _mainRegisters[WordRamModeLowOffset] = CurrentMainWordRamModeRegister();
    }

    private void WriteMainWordRamMode(byte value)
    {
        bool wasOneMegMode = (_wordRamModeBits & WordRamOneMegMode) != 0;
        bool willBeOneMegMode = (value & WordRamOneMegMode) != 0;
        if (wasOneMegMode != willBeOneMegMode)
        {
            RearrangeWordRamForModeChange(willBeOneMegMode);
        }

        _wordRamModeBits = (byte)((_wordRamModeBits & WordRamReturnToMain) |
            (value & (WordRamOneMegMode | WordRamMainAssignsToSub | WordRamGraphicsPriorityMask | MainProgramRamBankMask)));
        if ((_wordRamModeBits & WordRamOneMegMode) == 0 && (value & WordRamMainAssignsToSub) != 0)
        {
            _wordRamOwnedByMain = false;
        }
        else if ((_wordRamModeBits & WordRamOneMegMode) == 0 && (value & WordRamReturnToMain) != 0)
        {
            _wordRamOwnedByMain = true;
        }

        UpdateWordRamModeRegister();
    }

    private void WriteSubWordRamMode(byte value)
    {
        bool wasOneMegMode = (_wordRamModeBits & WordRamOneMegMode) != 0;
        bool willBeOneMegMode = (value & WordRamOneMegMode) != 0;
        if (wasOneMegMode != willBeOneMegMode)
        {
            RearrangeWordRamForModeChange(willBeOneMegMode);
        }

        bool selectChanged = (_wordRamModeBits & WordRamReturnToMain) != (value & WordRamReturnToMain);
        _wordRamModeBits = (byte)((_wordRamModeBits & (WordRamMainAssignsToSub | WordRamGraphicsPriorityMask | MainProgramRamBankMask)) |
            (value & (WordRamOneMegMode | WordRamReturnToMain | WordRamGraphicsPriorityMask | MainProgramRamBankMask)));
        if (selectChanged)
        {
            _wordRamModeBits &= unchecked((byte)~WordRamMainAssignsToSub);
        }

        if ((value & WordRamReturnToMain) != 0)
        {
            _wordRamOwnedByMain = true;
        }

        UpdateWordRamModeRegister();
    }

    private static bool TryResolveSonicCdMainMmdFile(ushort command, out string fileName)
    {
        fileName = command switch
        {
            0x0001 => "R11A__.MMD;1",
            0x0002 => "R11B__.MMD;1",
            0x0003 => "R11C__.MMD;1",
            0x0004 => "R11D__.MMD;1",
            0x0005 => "MDINIT.MMD;1",
            0x0006 => "STSEL_.MMD;1",
            0x0007 => "R12A__.MMD;1",
            0x0008 => "R12B__.MMD;1",
            0x0009 => "R12C__.MMD;1",
            0x000A => "R12D__.MMD;1",
            0x000B => "TITLEM.MMD;1",
            0x000C => "WARP__.MMD;1",
            0x000D => "ATTACK.MMD;1",
            0x0023 => "IPX___.MMD;1",
            0x0024 => "DEMO43C.MMD;1",
            0x0025 => "DEMO82A.MMD;1",
            0x0026 => "SOSEL_.MMD;1",
            0x0028 => "R31A__.MMD;1",
            0x0029 => "R31B__.MMD;1",
            0x002A => "R31C__.MMD;1",
            0x002B => "R31D__.MMD;1",
            0x002C => "R32A__.MMD;1",
            0x002D => "R32B__.MMD;1",
            0x002E => "R32C__.MMD;1",
            0x002F => "R32D__.MMD;1",
            0x0030 => "R33C__.MMD;1",
            0x0031 => "R33D__.MMD;1",
            0x0032 => "R13C__.MMD;1",
            0x0033 => "R13D__.MMD;1",
            0x0034 => "R41A__.MMD;1",
            0x0035 => "R41B__.MMD;1",
            0x0036 => "R41C__.MMD;1",
            0x0037 => "R41D__.MMD;1",
            0x0038 => "R42A__.MMD;1",
            0x0039 => "R42B__.MMD;1",
            0x003A => "R42C__.MMD;1",
            0x003B => "R42D__.MMD;1",
            0x003C => "R43C__.MMD;1",
            0x003D => "R43D__.MMD;1",
            0x003E => "R51A__.MMD;1",
            0x003F => "R51B__.MMD;1",
            0x0040 => "R51C__.MMD;1",
            0x0041 => "R51D__.MMD;1",
            0x0042 => "R52A__.MMD;1",
            0x0043 => "R52B__.MMD;1",
            0x0044 => "R52C__.MMD;1",
            0x0045 => "R52D__.MMD;1",
            0x0046 => "R53C__.MMD;1",
            0x0047 => "R53D__.MMD;1",
            0x0048 => "R61A__.MMD;1",
            0x0049 => "R61B__.MMD;1",
            0x004A => "R61C__.MMD;1",
            0x004B => "R61D__.MMD;1",
            0x004C => "R62A__.MMD;1",
            0x004D => "R62B__.MMD;1",
            0x004E => "R62C__.MMD;1",
            0x004F => "R62D__.MMD;1",
            0x0050 => "R63C__.MMD;1",
            0x0051 => "R63D__.MMD;1",
            0x0052 => "R71A__.MMD;1",
            0x0053 => "R71B__.MMD;1",
            0x0054 => "R71C__.MMD;1",
            0x0055 => "R71D__.MMD;1",
            0x0056 => "R72A__.MMD;1",
            0x0057 => "R72B__.MMD;1",
            0x0058 => "R72C__.MMD;1",
            0x0059 => "R72D__.MMD;1",
            0x005A => "R73C__.MMD;1",
            0x005B => "R73D__.MMD;1",
            0x005C => "R81A__.MMD;1",
            0x005D => "R81B__.MMD;1",
            0x005E => "R81C__.MMD;1",
            0x005F => "R81D__.MMD;1",
            0x0060 => "R82A__.MMD;1",
            0x0061 => "R82B__.MMD;1",
            0x0062 => "R82C__.MMD;1",
            0x0063 => "R82D__.MMD;1",
            0x0064 => "R83C__.MMD;1",
            0x0065 => "R83D__.MMD;1",
            0x0075 => "SPMM__.MMD;1",
            0x0081 => "PLANET_M.MMD;1",
            0x0084 => "DEMO11A.MMD;1",
            0x0085 => "VM____.MMD;1",
            0x0089 => "BRAMINIT.MMD;1",
            0x008D => "THANKS_M.MMD;1",
            0x008E => "BRAMMAIN.MMD;1",
            0x0093 or 0x0094 => "ENDING.MMD;1",
            0x00C8 => "NISI.MMD;1",
            0x00C9 => "SPEEND.MMD;1",
            0x00CA => "DUMMY0.MMD;1",
            0x00CB => "DUMMY1.MMD;1",
            0x00CC => "DUMMY2.MMD;1",
            0x00CD => "DUMMY3.MMD;1",
            0x00CE => "DUMMY4.MMD;1",
            0x00CF => "DUMMY5.MMD;1",
            0x00D0 => "DUMMY6.MMD;1",
            0x00D1 => "DUMMY7.MMD;1",
            0x00D2 => "DUMMY8.MMD;1",
            0x00D3 => "DUMMY9.MMD;1",
            0x00D4 => "PTEST.MMD;1",
            0x00D7 => "OPEN_M.MMD;1",
            0x00D8 => "COME__.MMD;1",
            _ => string.Empty
        };

        return fileName.Length != 0;
    }

    private void StageSonicCdMainMmdInWordRam(ReadOnlySpan<byte> fileData)
    {
        if ((_wordRamModeBits & WordRamOneMegMode) != 0)
        {
            int bankBytes = SegaCdHardwareProfile.WordRamBytes / 2;
            for (int i = 0; i < bankBytes; i++)
            {
                _wordRam[MapOneMegWordRamAddress((uint)i, select: true)] = 0;
            }

            int copyLength = Math.Min(fileData.Length, bankBytes);
            for (int i = 0; i < copyLength; i++)
            {
                _wordRam[MapOneMegWordRamAddress((uint)i, select: true)] = fileData[i];
            }

            return;
        }

        Array.Clear(_wordRam);
        fileData[..Math.Min(fileData.Length, _wordRam.Length)].CopyTo(_wordRam);
    }

    private void ProcessCddCommand()
    {
        int command = _mainRegisters[CddCommandStart] & 0x0F;
        int parameter = (_mainRegisters[CddCommandStart + 3] & 0x0F);
        if (command != 0x00)
        {
            _cddResponseLatched = false;
        }

        switch (command)
        {
            case 0x00:
                if (_cddResponseLatched)
                {
                    return;
                }

                RefreshCddStatusRegisters();
                RaiseSubToMainFlag(0x01);
                QueueCddInterruptIfEnabled();
                return;
            case 0x01:
                _cddStatusCode = Disc is null ? CddStatusNoDisc : CddStatusIdle;
                _cddSeekTicksRemaining = 0;
                StopCddaPlayback(_cddStatusCode);
                RefreshCddStatusRegisters(_cddStatusCode);
                if (Disc is not null)
                {
                    _cddStatusCode = Disc.Tracks.Count > 0 && Disc.Tracks[0].Kind == DiscTrackKind.Data
                        ? CddStatusReady
                        : CddStatusStop;
                }

                _cddResponseLatched = true;
                RaiseSubToMainFlag(0x01);
                QueueCddInterruptIfEnabled();
                return;
            case 0x02:
                RefreshCddTocStatus(parameter);
                QueueCddInterruptIfEnabled();
                return;
            case 0x03:
                _cddStatusCode = Disc is null ? CddStatusNoDisc : CddStatusPlay;
                _cddSeekTicksRemaining = 0;
                int playLba = CddCommandMsfFrames() - CddLeadInFrames;
                _currentCdcLba = playLba;
                StartCddaPlayback(playLba);
                _cdcRunning = Disc is not null && !IsAudioLba(playLba);
                break;
            case 0x04:
                _cddStatusCode = Disc is null ? CddStatusNoDisc : CddStatusSeek;
                _cddSeekTicksRemaining = Disc is null ? 0 : 2;
                _currentCdcLba = CddCommandMsfFrames() - CddLeadInFrames;
                _cdcRunning = false;
                StopCddaPlayback(_cddStatusCode);
                break;
            case 0x06:
                _cddStatusCode = Disc is null ? CddStatusNoDisc : CddStatusReady;
                _cddSeekTicksRemaining = 0;
                _cdcRunning = false;
                StopCddaPlayback(_cddStatusCode);
                break;
            case 0x0C:
                _cddStatusCode = Disc is null ? CddStatusNoDisc : CddStatusIdle;
                _cddSeekTicksRemaining = 0;
                StopCddaPlayback(_cddStatusCode);
                RefreshCddStatusRegisters(_cddStatusCode);
                if (Disc is not null)
                {
                    _cddStatusCode = Disc.Tracks.Count > 0 && Disc.Tracks[0].Kind == DiscTrackKind.Data
                        ? CddStatusReady
                        : CddStatusStop;
                }

                _cddResponseLatched = true;
                RaiseSubToMainFlag(0x01);
                QueueCddInterruptIfEnabled();
                return;
            case 0x0D:
                _cddStatusCode = CddStatusOpen;
                _cddSeekTicksRemaining = 0;
                _cdcRunning = false;
                StopCddaPlayback(_cddStatusCode);
                break;
        }

        RefreshCddStatusRegisters();
        RaiseSubToMainFlag(0x01);
        QueueCddInterruptIfEnabled();
    }

    private void RefreshCddStatusRegisters(byte? statusOverride = null, byte parameter = 0)
    {
        byte status = statusOverride ?? _cddStatusCode;
        ClearCddStatusBytes();
        _mainRegisters[CddStatusStart] = (byte)(status & 0x0F);
        _mainRegisters[CddStatusStart + 1] = (byte)(parameter & 0x0F);
        _mainRegisters[CddStatusStart + 2] = 0;
        _mainRegisters[CddStatusStart + 3] = 0;
        WriteCddChecksum(CddStatusStart);
        _cddStatusReady = true;
        _cddResponseLatched = false;
    }

    private void RefreshCddTocStatus(int request)
    {
        if (Disc is null)
        {
            RefreshCddStatusRegisters(CddStatusNoDisc, (byte)request);
            return;
        }

        ClearCddStatusBytes();
        byte reportStatus = request <= 0x02 ? CurrentCddLocationStatus() : CddStatusToc;
        _mainRegisters[CddStatusStart] = reportStatus;
        _mainRegisters[CddStatusStart + 1] = (byte)(request & 0x0F);
        DiscTrack currentTrack = CurrentCddTrack();
        switch (request)
        {
            case 0x00:
                WriteCddMsf(CddStatusStart + 2, _currentCdcLba + CddLeadInFrames);
                _mainRegisters[CddStatusStart + 8] = TrackFlags(currentTrack);
                break;
            case 0x01:
                WriteCddMsf(CddStatusStart + 2, _currentCdcLba - currentTrack.StartLba);
                _mainRegisters[CddStatusStart + 8] = TrackFlags(currentTrack);
                break;
            case 0x02:
                WriteCddBcdPair(CddStatusStart + 2, currentTrack.Number);
                break;
            case 0x03:
                WriteCddMsf(CddStatusStart + 2, Disc.LeadOutLba + CddLeadInFrames);
                break;
            case 0x04:
                WriteCddBcdPair(CddStatusStart + 2, Disc.Tracks[0].Number);
                WriteCddBcdPair(CddStatusStart + 4, Disc.Tracks[^1].Number);
                break;
            case 0x05:
                int trackNumber = ((_mainRegisters[CddCommandStart + 4] & 0x0F) * 10) + (_mainRegisters[CddCommandStart + 5] & 0x0F);
                DiscTrack? track = Disc.Tracks.FirstOrDefault(candidate => candidate.Number == trackNumber);
                if (track is null)
                {
                    _mainRegisters[CddStatusStart + 1] = 0x0F;
                    break;
                }

                WriteCddMsf(CddStatusStart + 2, track.StartLba + CddLeadInFrames);
                if (track.Kind == DiscTrackKind.Data)
                {
                    _mainRegisters[CddStatusStart + 6] |= 0x08;
                }

                _mainRegisters[CddStatusStart + 8] = (byte)(track.Number % 10);
                break;
            case 0x06:
                break;
            default:
                _mainRegisters[CddStatusStart + 1] = 0x0F;
                break;
        }

        WriteCddChecksum(CddStatusStart);
        _cddStatusReady = true;
        _cddResponseLatched = true;
        RaiseSubToMainFlag(0x01);
        if (Disc is not null && request > 0x02)
        {
            _cddStatusCode = CddStatusReady;
        }
    }

    private byte CurrentCddLocationStatus()
    {
        return _cddStatusCode is CddStatusPlay or CddStatusSeek ? _cddStatusCode : CddStatusReady;
    }

    private DiscTrack CurrentCddTrack()
    {
        if (Disc is not null && Disc.TryGetTrackForLba(_currentCdcLba, out DiscTrack track))
        {
            return track;
        }

        return Disc!.Tracks[0];
    }

    private void StartCddaPlayback(int lba)
    {
        _cddaLba = Math.Max(0, lba);
        _cddaSectorLba = int.MinValue;
        _cddaSectorSampleIndex = 0;
        _cddaPlaying = Disc is not null &&
            Disc.TryGetTrackForLba(_cddaLba, out DiscTrack track) &&
            track.Kind == DiscTrackKind.Audio;
    }

    private void StopCddaPlayback(byte status)
    {
        _cddaPlaying = false;
        _cddaSectorLba = int.MinValue;
        _cddaSectorSampleIndex = 0;
        _cddStatusCode = status;
    }

    private bool IsAudioLba(int lba)
    {
        return Disc is not null &&
            Disc.TryGetTrackForLba(Math.Max(0, lba), out DiscTrack track) &&
            track.Kind == DiscTrackKind.Audio;
    }

    private bool EnsureCddaSectorLoaded()
    {
        if (Disc is null ||
            !Disc.TryGetTrackForLba(_cddaLba, out DiscTrack track) ||
            track.Kind != DiscTrackKind.Audio)
        {
            return false;
        }

        if (_cddaSectorLba == _cddaLba)
        {
            return true;
        }

        if (!Disc.TryReadAudioSector2352(_cddaLba, _cddaSector))
        {
            return false;
        }

        _cddaSectorLba = _cddaLba;
        _cddaSectorSampleIndex = Math.Clamp(_cddaSectorSampleIndex, 0, CdAudioSamplesPerSector - 1);
        return true;
    }

    private void AdvanceCddaSample()
    {
        _cddaSectorSampleIndex++;
        if (_cddaSectorSampleIndex < CdAudioSamplesPerSector)
        {
            return;
        }

        _cddaSectorSampleIndex = 0;
        _cddaLba++;
        _cddaSectorLba = int.MinValue;
    }

    private static short ReadLittleEndianInt16(byte[] data, int offset)
    {
        return unchecked((short)(data[offset] | (data[offset + 1] << 8)));
    }

    private static short AddSamples(short current, short sample)
    {
        return (short)Math.Clamp(current + sample, short.MinValue, short.MaxValue);
    }

    private void ClearCddStatusBytes()
    {
        for (int i = 0; i < CddPacketBytes; i++)
        {
            _mainRegisters[CddStatusStart + i] = 0;
        }
    }

    private void WriteCddChecksum(uint packetStart)
    {
        int sum = 0;
        for (int i = 0; i < CddPacketBytes - 1; i++)
        {
            sum += _mainRegisters[packetStart + i] & 0x0F;
        }

        _mainRegisters[packetStart + CddPacketBytes - 1] = (byte)((~sum) & 0x0F);
    }

    private void WriteCddMsf(uint offset, int frames)
    {
        frames = Math.Max(0, frames);
        int minutes = frames / (60 * 75);
        int seconds = (frames / 75) % 60;
        int frame = frames % 75;
        WriteCddBcdPair(offset, minutes);
        WriteCddBcdPair(offset + 2, seconds);
        WriteCddBcdPair(offset + 4, frame);
    }

    private int CddCommandMsfFrames()
    {
        int minutes = BcdPairToInt(CddCommandStart + 2);
        int seconds = BcdPairToInt(CddCommandStart + 4);
        int frames = BcdPairToInt(CddCommandStart + 6);
        return (((minutes * 60) + Math.Clamp(seconds, 0, 59)) * 75) + Math.Clamp(frames, 0, 74);
    }

    private int BcdPairToInt(uint offset)
    {
        return ((_mainRegisters[offset] & 0x0F) * 10) + (_mainRegisters[offset + 1] & 0x0F);
    }

    private void WriteCddBcdPair(uint offset, int value)
    {
        value = Math.Clamp(value, 0, 99);
        _mainRegisters[offset] = (byte)(value / 10);
        _mainRegisters[offset + 1] = (byte)(value % 10);
    }

    private static byte TrackFlags(DiscTrack track)
    {
        return track.Kind == DiscTrackKind.Data ? (byte)0x04 : (byte)0x00;
    }

    private byte CurrentCdcTransferRegister()
    {
        byte value = (byte)(_mainRegisters[CdcTransferOffset] & 0x0F);
        if (_cdcPacketLength > 0)
        {
            value |= CdcTransferReadyBit;
        }

        if (_cdcPacketLength > 0 && _cdcPacketOffset >= _cdcPacketLength)
        {
            value |= CdcTransferDataBit;
        }

        return value;
    }

    private byte ReadCdcRegister()
    {
        EnsureCdcPacketPrepared();
        byte value = _cdcAddress switch
        {
            0x01 => 0x00,
            0x02 => (byte)(_cdcPacketLength & 0xFF),
            0x03 => (byte)(_cdcPacketLength >> 8),
            0x04 => _cdcPacket[0],
            0x05 => _cdcPacket[1],
            0x06 => _cdcPacket[2],
            0x07 => _cdcPacket[3],
            0x08 => 0x00,
            0x09 => 0x00,
            0x0A => 0x00,
            0x0B => 0x00,
            0x0C => 0x80,
            0x0D => 0x00,
            0x0E => 0x00,
            0x0F => 0x00,
            _ => 0x00,
        };

        bool completedStatusWindow = _cdcAddress == 0x0F;
        _cdcAddress = (byte)((_cdcAddress + 1) & 0x0F);
        if (completedStatusWindow && _currentCdcLba < 0 && _cdcPacketLength <= 4)
        {
            AckCdcPacket();
        }

        return value;
    }

    private void WriteCdcRegister(byte value)
    {
        byte address = _cdcAddress;
        _cdcRegisters[address] = value;
        _cdcAddress = (byte)((_cdcAddress + 1) & 0x0F);

        switch (address)
        {
            case 0x06:
                TriggerCdcTransfer();
                break;
            case 0x07:
                AckCdcPacket();
                break;
            case 0x0F:
                ResetCdc();
                break;
        }
    }

    private ushort ReadCdcDataWord()
    {
        EnsureCdcPacketPrepared();
        byte high = ReadCdcDataPortByte();
        byte low = ReadCdcDataPortByte();
        if (_cdcPacketLength > 0 && _cdcPacketOffset >= _cdcPacketLength)
        {
            AckCdcPacket();
            if (!TryGetBootReadRequest(out _, out _))
            {
                _bootReadStreamActive = false;
            }
        }

        return (ushort)((high << 8) | low);
    }

    private byte ReadCdcDataPortByte()
    {
        if (_cdcPacketOffset >= _cdcPacketLength)
        {
            return 0x00;
        }

        int source = _cdcRegisters[0x04] | (_cdcRegisters[0x05] << 8);
        byte value = source >= 0 && source < _cdcPacket.Length ? _cdcPacket[source] : (byte)0x00;
        source = (source + 1) & 0xFFFF;
        _cdcRegisters[0x04] = (byte)source;
        _cdcRegisters[0x05] = (byte)(source >> 8);
        _cdcPacketOffset++;
        return value;
    }

    private byte ReadCdcPacketByte(int source)
    {
        return source >= 0 && source < _cdcPacket.Length ? _cdcPacket[source] : (byte)0x00;
    }

    private void TriggerCdcTransfer()
    {
        PrepareCdcPacket();
        int destination = _mainRegisters[CdcTransferOffset] & 0x07;
        if (destination is not (4 or 5 or 7))
        {
            return;
        }

        int length = _cdcPacketLength;
        int requestedLength = _cdcRegisters[0x02] | (_cdcRegisters[0x03] << 8);
        if (requestedLength > 0)
        {
            length = Math.Min(length, requestedLength + 1);
        }

        int source = _cdcRegisters[0x04] | (_cdcRegisters[0x05] << 8);
        int target = _cdcRegisters[0x08] | (_cdcRegisters[0x09] << 8);
        for (int i = 0; i < length; i++)
        {
            byte value = ReadCdcPacketByte((source + i) & 0xFFFF);
            switch (destination)
            {
                case 4:
                    WritePcmMappedByte((uint)(target + i), value);
                    break;
                case 5:
                    WriteProgramRamByte((uint)(target + i), value);
                    break;
                case 7:
                    if ((_wordRamModeBits & WordRamOneMegMode) != 0)
                    {
                        WriteSubOneMegWordRamByte((uint)(target + i), value);
                    }
                    else
                    {
                        WriteWordRamByte((uint)(target + i), value);
                    }

                    break;
            }
        }

        _cdcRegisters[0x04] = (byte)((source + length) & 0xFF);
        _cdcRegisters[0x05] = (byte)(((source + length) >> 8) & 0xFF);
        _cdcPacketOffset = Math.Min(_cdcPacketLength, length);
        QueueSubInterruptIfEnabled(CdcInterruptLevel);
        AckCdcPacket();
    }

    private void EnsureCdcPacketPrepared()
    {
        if (_cdcRunning && TryGetBootReadRequest(out int bootReadLba, out int bootReadSectorCount))
        {
            _bootReadStreamActive = true;
            bool requestChanged = bootReadLba != _bootReadStartLba || bootReadSectorCount != _bootReadSectorCount;
            bool outsideRequest = _currentCdcLba < bootReadLba || _currentCdcLba >= bootReadLba + bootReadSectorCount;
            if (requestChanged || outsideRequest)
            {
                _bootReadStartLba = bootReadLba;
                _bootReadSectorCount = bootReadSectorCount;
                _currentCdcLba = bootReadLba;
                _cdcPacketOffset = 0;
                _cdcPacketLength = 0;
            }
        }

        if (_cdcRunning && _cdcPacketLength == 0)
        {
            PrepareCdcPacket();
        }
    }

    private void SeedBootCdcRingFromSubBiosLoopIfNeeded()
    {
        if (!TryGetActiveBootReadRequest(out int bootReadLba, out int bootReadSectorCount))
        {
            return;
        }

        _bootReadStreamActive = true;
        bool requestChanged = bootReadLba != _bootReadStartLba || bootReadSectorCount != _bootReadSectorCount;
        bool outsideRequest = _currentCdcLba < bootReadLba || _currentCdcLba >= bootReadLba + bootReadSectorCount;
        if (requestChanged || outsideRequest)
        {
            _bootReadStartLba = bootReadLba;
            _bootReadSectorCount = bootReadSectorCount;
            _currentCdcLba = bootReadLba;
            _cdcPacketOffset = 0;
            _cdcPacketLength = 0;
        }

        StageBootCdcPayloadRangeInWordRamIfNeeded(bootReadLba, bootReadSectorCount);
        if (_cdcPacketLength == 0)
        {
            PrepareCdcPacket(publishRingEntry: false, useCachedBootRead: true);
        }

        PublishBootCdcRingEntryIfNeeded(consumePacket: true);
    }

    private void PrepareCdcPacket(bool publishRingEntry = true, bool useCachedBootRead = false)
    {
        SyncCdcLbaFromBootReadIfNeeded(useCachedBootRead);
        Array.Clear(_cdcPacket);
        WriteCdcHeader(_currentCdcLba);
        _cdcPacketLength = 4 + 2048;
        if (Disc is not null && Disc.TryReadDataSector2048(_currentCdcLba, _cdcPacket.AsSpan(4, 2048)))
        {
            _cdcRunning = true;
            StageBootCdcPayloadInWordRamIfNeeded();
        }
        else
        {
            _cdcPacketLength = 4;
        }

        int requestedLength = _cdcRegisters[0x02] | (_cdcRegisters[0x03] << 8);
        if (requestedLength > 0)
        {
            _cdcPacketLength = Math.Min(_cdcPacketLength, Math.Min(requestedLength + 1, _cdcPacket.Length));
        }

        _cdcPacketOffset = 0;
        if (publishRingEntry)
        {
            PublishBootCdcRingEntryIfNeeded();
        }
    }

    private void StageBootCdcPayloadInWordRamIfNeeded()
    {
        int bootReadLba;
        int bootReadSectorCount;
        if (!TryGetBootReadRequest(out bootReadLba, out bootReadSectorCount))
        {
            if (!_bootReadStreamActive || _bootReadStartLba < 0 || _bootReadSectorCount <= 0)
            {
                return;
            }

            bootReadLba = _bootReadStartLba;
            bootReadSectorCount = _bootReadSectorCount;
        }

        int sectorIndex = _currentCdcLba - bootReadLba;
        if (sectorIndex < 0 || sectorIndex >= bootReadSectorCount)
        {
            return;
        }

        int offset = sectorIndex * 2048;
        if (offset < GenericBootReservedWordRamBytes || offset + 2048 > _wordRam.Length)
        {
            return;
        }

        ReadOnlySpan<byte> payload = _cdcPacket.AsSpan(4, 2048);
        if ((_wordRamModeBits & WordRamOneMegMode) != 0)
        {
            for (int i = 0; i < payload.Length; i++)
            {
                WriteSubOneMegWordRamByte((uint)(offset + i), payload[i]);
            }

            return;
        }

        payload.CopyTo(_wordRam.AsSpan(offset, 2048));
    }

    private void StageBootCdcPayloadRangeInWordRamIfNeeded(int bootReadLba, int bootReadSectorCount)
    {
        if (Disc is null ||
            bootReadLba < 0 ||
            bootReadSectorCount <= 0 ||
            (_bootReadBulkStagedLba == bootReadLba && _bootReadBulkStagedCount >= bootReadSectorCount))
        {
            return;
        }

        int sectorLimit = Math.Min(bootReadSectorCount, SegaCdHardwareProfile.WordRamBytes / 2048);
        byte[] sector = new byte[2048];
        for (int sectorIndex = 0; sectorIndex < sectorLimit; sectorIndex++)
        {
            if (!Disc.TryReadDataSector2048(bootReadLba + sectorIndex, sector))
            {
                break;
            }

            int offset = sectorIndex * 2048;
            if (offset < GenericBootReservedWordRamBytes)
            {
                continue;
            }

            if ((_wordRamModeBits & WordRamOneMegMode) != 0)
            {
                for (int i = 0; i < sector.Length; i++)
                {
                    WriteSubOneMegWordRamByte((uint)(offset + i), sector[i]);
                }
            }
            else
            {
                sector.CopyTo(_wordRam.AsSpan(offset, sector.Length));
            }
        }

        _bootReadBulkStagedLba = bootReadLba;
        _bootReadBulkStagedCount = bootReadSectorCount;
    }

    private void PublishBootCdcRingEntryIfNeeded(bool consumePacket = false)
    {
        if (!TryGetActiveBootReadRequest(out int bootReadLba, out int bootReadSectorCount))
        {
            return;
        }

        int sectorIndex = _currentCdcLba - bootReadLba;
        if (sectorIndex < 0 || sectorIndex >= bootReadSectorCount)
        {
            return;
        }

        uint ringBase = CdcRingBufferProgramRamOffset;
        ushort readPtr = ReadProgramRamWord(ringBase + RingBufferReadPtrOffset);
        ushort writePtr = ReadProgramRamWord(ringBase + RingBufferWritePtrOffset);
        ushort writeStep = ReadProgramRamWord(ringBase + RingBufferWriteStepOffset);
        ushort bufferSize = ReadProgramRamWord(ringBase + RingBufferSizeOffset);
        if (writeStep == 0 || bufferSize == 0 || writePtr >= bufferSize)
        {
            return;
        }

        ushort nextWrite = (ushort)(writePtr + writeStep);
        if (nextWrite >= bufferSize)
        {
            nextWrite -= bufferSize;
        }

        if (nextWrite == readPtr)
        {
            return;
        }

        uint entry = ringBase + RingBufferDataOffset + writePtr;
        WriteProgramRamLong(entry, ((uint)_cdcPacket[0] << 24) |
            ((uint)_cdcPacket[1] << 16) |
            ((uint)_cdcPacket[2] << 8) |
            _cdcPacket[3]);
        WriteProgramRamWord(entry + 4, 0);
        WriteProgramRamByte(entry + 6, 0);
        WriteProgramRamByte(entry + 7, 0);

        WriteProgramRamWord(ringBase + RingBufferWritePtrOffset, nextWrite);
        if (consumePacket)
        {
            AckCdcPacket();
        }
    }

    private void SyncCdcLbaFromBootReadIfNeeded(bool useCachedBootRead)
    {
        if (useCachedBootRead && TryGetCachedBootReadRequest(out _, out _))
        {
            return;
        }

        if (TryGetBootReadRequest(out int bootReadLba, out int bootReadSectorCount))
        {
            _bootReadStreamActive = true;
            bool requestChanged = bootReadLba != _bootReadStartLba || bootReadSectorCount != _bootReadSectorCount;
            bool outsideRequest = _currentCdcLba < bootReadLba || _currentCdcLba >= bootReadLba + bootReadSectorCount;
            if (requestChanged || outsideRequest)
            {
                _bootReadStartLba = bootReadLba;
                _bootReadSectorCount = bootReadSectorCount;
                _currentCdcLba = bootReadLba;
            }
        }
    }

    private bool TryGetActiveBootReadRequest(out int lba, out int sectorCount)
    {
        if (TryGetCachedBootReadRequest(out lba, out sectorCount))
        {
            return true;
        }

        return TryGetBootReadRequest(out lba, out sectorCount);
    }

    private bool TryGetCachedBootReadRequest(out int lba, out int sectorCount)
    {
        lba = _bootReadStartLba;
        sectorCount = _bootReadSectorCount;
        if (!_bootReadStreamActive ||
            lba < 0 ||
            sectorCount <= 0 ||
            Disc is null ||
            !Disc.TryGetTrackForLba(lba, out DiscTrack track) ||
            track.Kind != DiscTrackKind.Data)
        {
            lba = 0;
            sectorCount = 0;
            return false;
        }

        int maxReadable = Math.Max(1, track.EndLbaExclusive - lba);
        sectorCount = Math.Min(sectorCount, Math.Min(maxReadable, 1024));
        return true;
    }

    private bool TryGetBootReadRequest(out int lba, out int sectorCount)
    {
        lba = 0;
        sectorCount = 0;
        byte cbtFlags = _programRam[CbtFlagsProgramRamOffset];
        bool cbtFlaggedRead = (cbtFlags & (CbtIpLoadPending | 0x08)) != 0;
        bool bootReadWindowActive = Disc is not null &&
            !UsesSonicCdPostBootHandoff() &&
            (SubCpu.PC & 0x00FF_FFFFu) is >= 0x0000_3A00u and <= 0x0000_3E90u;
        if (!cbtFlaggedRead && !bootReadWindowActive)
        {
            return false;
        }

        uint requestedSectorCount = NormalizeBootReadSectorCount(ReadProgramRamLong(CbtReadSectorCountProgramRamOffset));
        ushort requestedLoopCount = ReadProgramRamWord(CbtReadSectorLoopCountProgramRamOffset);
        if (requestedSectorCount == 0 && bootReadWindowActive && requestedLoopCount is > 0 and < 0x0800)
        {
            requestedSectorCount = NormalizeBootReadLoopCount(requestedLoopCount);
        }

        if (requestedSectorCount == 0)
        {
            ushort requestedSectorCountWord = NormalizeBootReadLoopCount(ReadProgramRamWord(CbtReadSectorCountProgramRamOffset));
            if (requestedSectorCountWord == 0)
            {
                return false;
            }

            requestedSectorCount = requestedSectorCountWord;
        }

        uint sectorStart = ReadProgramRamLong(CbtReadSectorStartProgramRamOffset);
        if (sectorStart > int.MaxValue)
        {
            return false;
        }

        lba = (int)sectorStart;
        if (Disc is null ||
            !Disc.TryGetTrackForLba(lba, out DiscTrack track) ||
            track.Kind != DiscTrackKind.Data)
        {
            return false;
        }

        uint maxReadable = (uint)Math.Max(1, track.EndLbaExclusive - lba);
        sectorCount = (int)Math.Min(requestedSectorCount, Math.Min(maxReadable, 1024u));
        return true;
    }

    private static uint NormalizeBootReadSectorCount(uint value)
    {
        if (value >= 2048 && (value & 0x07FF) == 0)
        {
            return value / 2048;
        }

        return value;
    }

    private static ushort NormalizeBootReadLoopCount(ushort value)
    {
        return value;
    }

    private void WriteCdcHeader(int lba)
    {
        int absoluteFrames = Math.Max(0, lba + CddLeadInFrames);
        int minutes = absoluteFrames / (60 * 75);
        int seconds = (absoluteFrames / 75) % 60;
        int frames = absoluteFrames % 75;
        _cdcPacket[0] = ToBcdByte(minutes);
        _cdcPacket[1] = ToBcdByte(seconds);
        _cdcPacket[2] = ToBcdByte(frames);
        _cdcPacket[3] = 0x01;
    }

    private static byte ToBcdByte(int value)
    {
        value = Math.Clamp(value, 0, 99);
        return (byte)(((value / 10) << 4) | (value % 10));
    }

    private void AckCdcPacket()
    {
        if (_cdcPacketLength > 0)
        {
            _currentCdcLba++;
        }

        _cdcPacketOffset = 0;
        _cdcPacketLength = 0;
    }

    private void ResetCdc()
    {
        Array.Clear(_cdcRegisters);
        _cdcAddress = 0;
        _cdcPacketOffset = 0;
        _cdcPacketLength = 0;
        _bootReadStreamActive = false;
    }

    private void RunGraphicsOperation()
    {
        byte stampConfig = _mainRegisters[GfxStampSizeLowOffset];
        int layout = (stampConfig >> 1) & 0x03;
        bool repeatMap = (stampConfig & 0x01) != 0;
        bool largeStamp = (stampConfig & 0x02) != 0;
        int dotMask;
        int stampShift;
        int mapShift;
        int mapAddressMask;

        switch (layout)
        {
            case 0:
                dotMask = 0x07FFFF;
                stampShift = 15;
                mapShift = 4;
                mapAddressMask = 0x3FE00;
                break;
            case 1:
                dotMask = 0x07FFFF;
                stampShift = 16;
                mapShift = 3;
                mapAddressMask = 0x3FF80;
                break;
            case 2:
                dotMask = 0x7FFFFF;
                stampShift = 15;
                mapShift = 8;
                mapAddressMask = 0x20000;
                break;
            default:
                dotMask = 0x7FFFFF;
                stampShift = 16;
                mapShift = 7;
                mapAddressMask = 0x38000;
                break;
        }

        int width = ReadRegisterWord(GfxBufferHDotsOffset) & 0x01FF;
        int lines = ReadRegisterWord(GfxBufferVDotsOffset) & 0x00FF;
        if (width <= 0 || lines <= 0)
        {
            FinishGraphicsOperation();
            return;
        }

        uint traceAddress = (uint)((ReadRegisterWord(GfxTraceVectorOffset) << 2) & 0x3FFF8);
        uint mapBase = (uint)((ReadRegisterWord(GfxStampMapAddressOffset) << 2) & mapAddressMask);
        uint bufferIndex = (uint)(((ReadRegisterWord(GfxBufferAddressOffset) << 3) & 0x7FFC0) + (_mainRegisters[GfxBufferOffsetLowOffset] & 0x3F));
        uint bufferColumnAdvance = (uint)((((_mainRegisters[GfxBufferVCellsLowOffset] & 0x1F) + 1) << 6) - 7);
        int stampMask = largeStamp ? 0x7FC : 0x7FF;
        int stampPixels = largeStamp ? 32 : 16;
        int cellsPerStamp = largeStamp ? 4 : 2;
        int localMask = stampPixels - 1;

        for (int line = 0; line < lines; line++)
        {
            uint lineTrace = (traceAddress + (uint)(line * 8)) & 0x3FFFF;
            uint x = (uint)(ReadWordRamWord(lineTrace) << 8);
            uint y = (uint)(ReadWordRamWord(lineTrace + 2) << 8);
            int xStep = (short)ReadWordRamWord(lineTrace + 4);
            int yStep = (short)ReadWordRamWord(lineTrace + 6);
            uint lineBufferIndex = bufferIndex;

            for (int pixel = 0; pixel < width; pixel++)
            {
                byte output = 0;
                uint sampleX = repeatMap ? x & (uint)dotMask : x & 0x00FF_FFFF;
                uint sampleY = repeatMap ? y & (uint)dotMask : y & 0x00FF_FFFF;
                if (((sampleX | sampleY) & ~(uint)dotMask) == 0)
                {
                    uint mapIndex = (sampleX >> stampShift) | ((sampleY >> stampShift) << mapShift);
                    ushort stampData = ReadWordRamWord(mapBase + (mapIndex << 1));
                    uint stampIndex = (uint)((stampData & stampMask) << 8);
                    if (stampIndex != 0)
                    {
                        int transform = (stampData >> 13) & 0x07;
                        int localX = (int)((sampleX >> 11) & (uint)localMask);
                        int localY = (int)((sampleY >> 11) & (uint)localMask);
                        ApplyStampTransform(transform, localMask, ref localX, ref localY);

                        int cellColumn = localX >> 3;
                        int cellRow = localY >> 3;
                        int cellOffset = cellRow + (cellColumn * cellsPerStamp);
                        int pixelOffset = (localX & 0x07) + ((localY & 0x07) << 3);
                        uint pixelIndex = stampIndex | (uint)(cellOffset << 6) | (uint)pixelOffset;
                        byte packed = ReadWordRamByte(pixelIndex >> 1);
                        output = (byte)((pixelIndex & 1) == 0 ? packed >> 4 : packed & 0x0F);
                    }
                }

                uint outputAddress = (lineBufferIndex >> 1) & 0x3FFFF;
                byte previous = ReadWordRamByte(outputAddress);
                byte packedOutput = (byte)((lineBufferIndex & 1) == 0
                    ? ((output << 4) | (previous & 0x0F))
                    : ((previous & 0xF0) | output));
                WriteWordRamByte(outputAddress, ApplyGraphicsPriority(previous, packedOutput));

                if ((lineBufferIndex & 0x07) != 0x07)
                {
                    lineBufferIndex++;
                }
                else
                {
                    lineBufferIndex += bufferColumnAdvance;
                }

                x = unchecked(x + (uint)xStep);
                y = unchecked(y + (uint)yStep);
            }

            bufferIndex += 8;
        }

        FinishGraphicsOperation();
    }

    private void FinishGraphicsOperation()
    {
        _mainRegisters[GfxBufferVDotsOffset] = 0;
        _mainRegisters[GfxBufferVDotsOffset + 1] = 0;
        _mainRegisters[0x58] = (byte)(_mainRegisters[0x58] & 0x7F);
    }

    private byte ApplyGraphicsPriority(byte previous, byte output)
    {
        return ((_mainRegisters[WordRamModeLowOffset] >> 3) & 0x03) switch
        {
            1 => (byte)(SelectNibble(previous, output, high: true, preferPrevious: true) |
                SelectNibble(previous, output, high: false, preferPrevious: true)),
            2 => (byte)(SelectNibble(previous, output, high: true, preferPrevious: false) |
                SelectNibble(previous, output, high: false, preferPrevious: false)),
            3 => previous,
            _ => output,
        };
    }

    private static int SelectNibble(byte previous, byte output, bool high, bool preferPrevious)
    {
        int mask = high ? 0xF0 : 0x0F;
        int previousNibble = previous & mask;
        int outputNibble = output & mask;
        if (preferPrevious)
        {
            return previousNibble != 0 ? previousNibble : outputNibble;
        }

        return outputNibble != 0 ? outputNibble : previousNibble;
    }

    private static void ApplyStampTransform(int transform, int mask, ref int x, ref int y)
    {
        if ((transform & 0x04) != 0)
        {
            x ^= mask;
        }

        if ((transform & 0x02) != 0)
        {
            x ^= mask;
            y ^= mask;
        }

        if ((transform & 0x01) != 0)
        {
            (x, y) = (y ^ mask, x);
        }
    }

    private void CompleteWordRamRendezvousIfNeeded(byte subFlags)
    {
        const byte subFlag6 = 0x40;
        const byte mainRamRequest = 0x04;
        if ((subFlags & subFlag6) != 0)
        {
            _stickySubFlag6 = true;
            _wordRamOwnedByMain = true;
            if (Disc is not null &&
                _mainBootIpOverrideAllowed &&
                !UsesSonicCdPostBootHandoff() &&
                BootWordRamEntryLooksReady())
            {
                if ((_wordRamModeBits & WordRamOneMegMode) != 0)
                {
                    RearrangeWordRamForModeChange(toOneMegMode: false);
                }

                _wordRamModeBits = (byte)((_wordRamModeBits & unchecked((byte)~(WordRamOneMegMode | WordRamMainAssignsToSub))) |
                    WordRamReturnToMain);
            }
            else
            {
                _wordRamModeBits |= WordRamReturnToMain;
            }

            _mainRegisters[WordRamModeLowOffset] = CurrentMainWordRamModeRegister();
            SetMainCommunicationFlags((byte)(_mainCommunicationFlags & unchecked((byte)~mainRamRequest)));
        }
    }

    private void ScheduleSubFlag7BootClearIfNeeded(byte subFlags)
    {
        const byte subFlag7 = 0x80;
        if ((subFlags & subFlag7) == 0)
        {
            _subFlag7BootClearCycles = 0;
            return;
        }

        if ((_mainCommunicationFlags & subFlag7) != 0 && _subFlag7BootClearCycles == 0)
        {
            _subFlag7BootClearCycles = Math.Max(1, SegaCdHardwareProfile.SubCpuClockHz / 75);
        }
    }

    private void AdvanceSubFlag7BootClear(int executedCycles)
    {
        if (_subFlag7BootClearCycles <= 0 || executedCycles <= 0)
        {
            return;
        }

        _subFlag7BootClearCycles -= executedCycles;
        if (_subFlag7BootClearCycles > 0)
        {
            return;
        }

        bool bootTransferPending = _bootReadStreamActive ||
            (_programRam[CbtFlagsProgramRamOffset] & CbtIpLoadPending) != 0;
        if ((_programRam[CbtFlagsProgramRamOffset] & CbtIpLoadPending) != 0)
        {
            _subFlag7BootClearCycles = Math.Max(1, SegaCdHardwareProfile.SubCpuClockHz / 75);
            return;
        }

        if (Disc is not null && bootTransferPending && !PostBootMmdHandoffStaged() && !BootWordRamEntryLooksReady())
        {
            StageInitialProgramInWordRamIfNeeded();
            _subFlag7BootClearCycles = Math.Max(1, SegaCdHardwareProfile.SubCpuClockHz / 75);
            return;
        }

        if (Disc is not null && !PostBootMmdHandoffStaged() && !BootWordRamEntryLooksReady())
        {
            StageInitialProgramInWordRamIfNeeded();
        }

        _subFlag7BootClearCycles = 0;
        _bootReadyFlagClearReadsUntilReady = 64;
        bool sonicPostBootHandoff = UsesSonicCdPostBootHandoff();
        bool noDiscBootClear = Disc is null && (_programRam[CbtFlagsProgramRamOffset] & CbtIpLoadPending) == 0;
        byte bootClearMask = (sonicPostBootHandoff || noDiscBootClear) ? (byte)0x82 : (byte)0x02;
        SetSubCommunicationFlags((byte)(_subCommunicationFlags & unchecked((byte)~bootClearMask)));
        if (!sonicPostBootHandoff && Disc is not null)
        {
            _subFlag7BootClearCycles = -1;
        }

        if (sonicPostBootHandoff)
        {
            RaiseSubToMainFlag(0x40);
            CompleteWordRamRendezvousIfNeeded(_subCommunicationFlags);
        }
    }

    private void StageInitialProgramInWordRamIfNeeded()
    {
        StageInitialProgramInWordRam(force: false, resetStatusWindow: true);
    }

    private void StageInitialProgramInWordRam(bool force, bool resetStatusWindow = true)
    {
        if (Disc is null || (!force && BootWordRamEntryLooksReady()))
        {
            return;
        }

        StageSystemProgramInProgramRamIfNeeded();

        byte[] initialProgram = new byte[InitialProgramSectorCount * 2048];
        Span<byte> sector = stackalloc byte[2048];
        int bytesLoaded = 0;
        for (int sectorIndex = 0; bytesLoaded < initialProgram.Length; sectorIndex++)
        {
            if (!Disc.TryReadDataSector2048(sectorIndex, sector))
            {
                break;
            }

            int sectorDiscOffset = sectorIndex * 2048;
            int copyStart = Math.Max(0, InitialProgramDiscHeaderBytes - sectorDiscOffset);
            if (copyStart >= sector.Length)
            {
                continue;
            }

            int copyLength = Math.Min(sector.Length - copyStart, initialProgram.Length - bytesLoaded);
            sector.Slice(copyStart, copyLength).CopyTo(initialProgram.AsSpan(bytesLoaded, copyLength));
            bytesLoaded += copyLength;
        }

        initialProgram.AsSpan(0, bytesLoaded).CopyTo(_wordRam);
        initialProgram.AsSpan(0, bytesLoaded).CopyTo(_initialProgramRaw);
        if (bytesLoaded < _initialProgramRaw.Length)
        {
            Array.Clear(_initialProgramRaw, bytesLoaded, _initialProgramRaw.Length - bytesLoaded);
        }

        _initialProgramRawLength = bytesLoaded;
        if (resetStatusWindow)
        {
            Array.Clear(_subToMainStatus);
            UpdateCommunicationWindowRegisters();
            _suppressBootStatusUntilMainCommand = true;
        }

        for (int i = 0; i < bytesLoaded; i++)
        {
            byte value = initialProgram[i];
            uint address = (uint)i;
            _wordRam[MapOneMegWordRamAddress(address, select: false)] = value;
            _wordRam[MapOneMegWordRamAddress(address, select: true)] = value;
        }
    }

    private void StageSystemProgramInProgramRamIfNeeded()
    {
        if (Disc is null ||
            SubProgramHeaderLooksReady())
        {
            return;
        }

        Span<byte> header = stackalloc byte[2048];
        if (!Disc.TryReadDataSector2048(0, header) ||
            !header[..14].SequenceEqual("SEGADISCSYSTEM"u8))
        {
            return;
        }

        int spOffset = (int)ReadBigEndianLong(header, BootHeaderSpOffsetField);
        int spLength = (int)ReadBigEndianLong(header, BootHeaderSpLengthField);
        if (spOffset <= 0 ||
            spLength <= 0 ||
            spLength > SegaCdHardwareProfile.ProgramRamBytes - SystemProgramRamLoadOffset)
        {
            return;
        }

        LoadDiscUserData(spOffset, _programRam.AsSpan(SystemProgramRamLoadOffset, spLength));
    }

    private bool SubProgramHeaderLooksReady()
    {
        ReadOnlySpan<byte> header = _programRam.AsSpan(SystemProgramRamLoadOffset);
        return
            header.Length >= 4 &&
            header[0] == (byte)'M' &&
            header[1] == (byte)'A' &&
            header[2] == (byte)'I' &&
            header[3] == (byte)'N';
    }

    private void LoadDiscUserData(int discByteOffset, Span<byte> destination)
    {
        Span<byte> sector = stackalloc byte[2048];
        int bytesLoaded = 0;
        while (bytesLoaded < destination.Length)
        {
            int absoluteOffset = discByteOffset + bytesLoaded;
            int sectorIndex = absoluteOffset / 2048;
            int sectorOffset = absoluteOffset % 2048;
            if (!Disc!.TryReadDataSector2048(sectorIndex, sector))
            {
                destination[bytesLoaded..].Clear();
                return;
            }

            int copyLength = Math.Min(2048 - sectorOffset, destination.Length - bytesLoaded);
            sector.Slice(sectorOffset, copyLength).CopyTo(destination.Slice(bytesLoaded, copyLength));
            bytesLoaded += copyLength;
        }
    }

    private bool BootWordRamEntryLooksReady()
    {
        return EntrySignatureLooksReady(
                _wordRam[0],
                _wordRam[1],
                _wordRam[2],
                _wordRam[3]) ||
            MainWordRamEntryLooksReady();
    }

    private void EnsureInitialProgramVisibleToMainIfNeeded(uint address)
    {
        if (Disc is null ||
            !_mainBootIpOverrideAllowed ||
            PostBootMmdHandoffStaged() ||
            _cdcRunning ||
            _bootReadStreamActive ||
            address >= 0x400 ||
            MainWordRamEntryLooksReady())
        {
            return;
        }

        StageInitialProgramInWordRam(force: true, resetStatusWindow: false);
    }

    private void ForceGenericBootInitialProgramMainViewIfNeeded(uint address)
    {
        if (Disc is null ||
            UsesSonicCdPostBootHandoff() ||
            !_mainBootIpOverrideAllowed ||
            address >= InitialProgramSectorCount * 2048 ||
            !BootWordRamEntryLooksReady())
        {
            return;
        }

        if (_wordRamOwnedByMain && (_wordRamModeBits & WordRamOneMegMode) == 0)
        {
            return;
        }

        _wordRamOwnedByMain = true;
        _wordRamModeBits = (byte)((_wordRamModeBits & unchecked((byte)~(WordRamOneMegMode | WordRamMainAssignsToSub))) |
            WordRamReturnToMain);
        UpdateWordRamModeRegister();
    }

    private bool ShouldReadGenericBootInitialProgramLinear(uint address, uint mainPc)
    {
        return Disc is not null &&
            !UsesSonicCdPostBootHandoff() &&
            IsGenericBootInitialProgramCopyPc(mainPc) &&
            address < InitialProgramSectorCount * 2048 &&
            (InitialProgramRawEntryLooksReady() ||
             ((_wordRamModeBits & WordRamOneMegMode) != 0 && LinearInitialProgramEntryLooksReady()));
    }

    private static bool IsGenericBootInitialProgramCopyPc(uint mainPc)
    {
        return mainPc is >= 0x00FF_101Eu and <= 0x00FF_1028u or 0x0000_51F2u;
    }

    private static int MapOneMegPhysicalToLinearAddress(uint address)
    {
        uint bankBytes = (uint)SegaCdHardwareProfile.WordRamBytes / 2u;
        uint rawOffset = address & (uint)(SegaCdHardwareProfile.WordRamBytes - 1);
        uint wordPairOffset = ((rawOffset >> 2) << 1) | (rawOffset & 1u);
        if ((rawOffset & 2u) != 0)
        {
            wordPairOffset += bankBytes;
        }

        return (int)(wordPairOffset & (uint)(SegaCdHardwareProfile.WordRamBytes - 1));
    }

    private bool LinearInitialProgramEntryLooksReady()
    {
        return EntrySignatureLooksReady(
                _wordRam[MapOneMegPhysicalToLinearAddress(0)],
                _wordRam[MapOneMegPhysicalToLinearAddress(1)],
                _wordRam[MapOneMegPhysicalToLinearAddress(2)],
                _wordRam[MapOneMegPhysicalToLinearAddress(3)]) &&
            _wordRam[MapOneMegPhysicalToLinearAddress(4)] == 0x4E &&
            _wordRam[MapOneMegPhysicalToLinearAddress(5)] == 0xB8 &&
            _wordRam[MapOneMegPhysicalToLinearAddress(8)] == 0x60 &&
            _wordRam[MapOneMegPhysicalToLinearAddress(9)] == 0x00;
    }

    private bool InitialProgramRawEntryLooksReady()
    {
        return _initialProgramRawLength >= 10 &&
            EntrySignatureLooksReady(
                _initialProgramRaw[0],
                _initialProgramRaw[1],
                _initialProgramRaw[2],
                _initialProgramRaw[3]) &&
            _initialProgramRaw[4] == 0x4E &&
            _initialProgramRaw[5] == 0xB8 &&
            _initialProgramRaw[8] == 0x60 &&
            _initialProgramRaw[9] == 0x00;
    }

    private bool MainWordRamEntryLooksReady()
    {
        return EntrySignatureLooksReady(
            ReadMainWordRamByteRaw(0),
            ReadMainWordRamByteRaw(1),
            ReadMainWordRamByteRaw(2),
            ReadMainWordRamByteRaw(3));
    }

    private byte ReadMainWordRamByteRaw(uint address)
    {
        return _wordRam[MapMainWordRamAddress(address)];
    }

    private bool PostBootMmdHandoffStaged()
    {
        return _sonicCdMmdHandoffStageSuccesses > 0;
    }

    private bool UsesSonicCdPostBootHandoff()
    {
        if (!_usesSonicCdPostBootHandoffKnown)
        {
            _usesSonicCdPostBootHandoff = Disc is not null && Disc.TryReadIso9660File(SonicCdIpxModuleFileName, out _);
            _usesSonicCdPostBootHandoffKnown = true;
        }

        return _usesSonicCdPostBootHandoff;
    }

    private static bool EntrySignatureLooksReady(byte b0, byte b1, byte b2, byte b3)
    {
        return b0 == 0x43 && b1 == 0xFA && (b2 != 0 || b3 != 0);
    }

    private void PublishDiscTypeCommPacketIfNeeded(byte requestFlags)
    {
        const byte ramRequest = 0x04;
        if (Disc is null ||
            !_mainBootIpOverrideAllowed ||
            (requestFlags & ramRequest) == 0 ||
            PostBootCommand23AckPresent())
        {
            return;
        }

        bool packetAlreadyPresent = DiscTypeCommPacketPresent();
        if (packetAlreadyPresent && _discTypeCommPacketSyntheticEdgeUsed)
        {
            return;
        }

        if (!packetAlreadyPresent)
        {
            _subToMainStatus[0] = 0x00;
            _subToMainStatus[1] = 0x04;
            _subToMainStatus[2] = 0x00;
            _subToMainStatus[3] = DiscTypeForMainBios();
            UpdateCommunicationWindowRegisters();
            _discTypeCommPacketSyntheticEdgeUsed = false;
        }

        _discTypeCommPacketPending = true;
        _discTypeCommPacketReadyAfterClearObserved = false;
        _discTypeCommPacketClearReadsUntilReady = 0;
        RaiseSubToMainFlag(0x02);
    }

    private byte DiscTypeForMainBios()
    {
        if (Disc is null || Disc.Tracks.Count == 0)
        {
            return 0x00;
        }

        if (Disc.Tracks[0].Kind == DiscTrackKind.Data)
        {
            return 0x04;
        }

        return Disc.Tracks.Any(track => track.Kind == DiscTrackKind.Data) ? (byte)0x03 : (byte)0x01;
    }

    private bool DiscTypeCommPacketPresent()
    {
        return _subToMainStatus[0] == 0x00 &&
            _subToMainStatus[1] == 0x04 &&
            _subToMainStatus[2] == 0x00 &&
            _subToMainStatus[3] != 0x00;
    }

    private void ClearDiscTypeCommPacketReadyFlag()
    {
        _discTypeCommPacketPending = false;
        SetSubCommunicationFlags((byte)(_subCommunicationFlags & unchecked((byte)~0x02)));
    }

    private byte ClearConsumedGenericBootDiscTypeReadyFlagIfNeeded(uint offset, byte value, uint mainPc)
    {
        if (offset != SubToMainFlagOffset ||
            (value & 0x02) == 0 ||
            Disc is null ||
            !_mainBootIpOverrideAllowed ||
            UsesSonicCdPostBootHandoff() ||
            !DiscTypeCommPacketPresent() ||
            _mainToSubCommand[0] != 0 ||
            _mainToSubCommand[1] != 0 ||
            !IsGenericBootDiscTypeReadyClearPc(mainPc))
        {
            return value;
        }

        _discTypeCommPacketPending = false;
        _discTypeCommPacketReadyAfterClearObserved = false;
        _discTypeCommPacketClearReadsUntilReady = 0;
        SetSubCommunicationFlagsRaw((byte)(_subCommunicationFlags & unchecked((byte)~0x02)));
        return (byte)(value & unchecked((byte)~0x02));
    }

    private static bool IsGenericBootDiscTypeReadyClearPc(uint mainPc)
    {
        return mainPc is >= 0x0000_1288u and <= 0x0000_1290u or
            >= 0x0000_10ECu and <= 0x0000_10F0u;
    }

    private byte ForceGenericBootDiscTypeSecondReadyEdgeIfNeeded(uint offset, byte value, uint mainPc)
    {
        if (offset != SubToMainFlagOffset ||
            (value & 0x02) != 0 ||
            Disc is null ||
            !_mainBootIpOverrideAllowed ||
            UsesSonicCdPostBootHandoff() ||
            mainPc is < 0x0000_1290u or > 0x0000_1298u)
        {
            return value;
        }

        SetSubCommunicationFlagsRaw((byte)(_subCommunicationFlags | 0x02));
        return (byte)(value | 0x02);
    }

    private void ClearDiscTypeCommPacketStatusIfPresent()
    {
        if (!DiscTypeCommPacketPresent())
        {
            return;
        }

        _subToMainStatus[0] = 0;
        _subToMainStatus[1] = 0;
        _subToMainStatus[2] = 0;
        _subToMainStatus[3] = 0;
        _discTypeCommPacketPending = false;
        _discTypeCommPacketReadyAfterClearObserved = false;
        _discTypeCommPacketClearReadsUntilReady = 0;
        _discTypeCommPacketSyntheticEdgeUsed = false;
        SetSubCommunicationFlags((byte)(_subCommunicationFlags & unchecked((byte)~0x02)));
        UpdateCommunicationWindowRegisters();
    }

    private void ClearDiscTypeCommPacketOnCommandClearIfNeeded(uint offset, byte value, byte previousValue)
    {
        if (value != 0 ||
            Disc is null ||
            (_mainCommunicationFlags & 0x80) == 0 ||
            !DiscTypeCommPacketPresent())
        {
            return;
        }

        if (offset == 0x11 && previousValue == 0x23)
        {
            ClearDiscTypeCommPacketStatusIfPresent();
            return;
        }

        if (offset != 0x10)
        {
            return;
        }

        if (previousValue == 0x23 || _mainToSubCommand[1] == 0x23)
        {
            RaiseSubToMainFlag(0x02);
            return;
        }

        ClearDiscTypeCommPacketStatusIfPresent();
    }

    private void ScheduleIpCommand23ResponseIfNeeded(uint offset, byte value)
    {
        if (offset != 0x11 ||
            value != 0x23 ||
            Disc is null ||
            !_mainBootIpOverrideAllowed ||
            _mainToSubCommand[0] != 0)
        {
            return;
        }

        _pendingIpCommand23ResponseCycles = Math.Max(1, SegaCdHardwareProfile.SubCpuClockHz / 75);
    }

    private bool PostBootCommand23AckPresent()
    {
        return _subToMainStatus[0] == 0x00 &&
            _subToMainStatus[1] == 0x01 &&
            _subToMainStatus[2] == 0x00 &&
            _subToMainStatus[3] == 0x00;
    }

    private void AcknowledgePostBootCommand23IfNeeded(uint offset, byte value)
    {
        if (offset != 0x11 ||
            value != 0x23 ||
            Disc is null ||
            _syntheticCommand23AckCount != 0 ||
            !BootWordRamEntryLooksReady() ||
            _mainToSubCommand[0] != 0)
        {
            return;
        }

        PublishPostBootCommand23Ack();
    }

    private void AcknowledgePendingPostBootCommand23OnSubReadyIfNeeded(byte subFlags)
    {
        if (Disc is null ||
            (subFlags & 0x80) == 0 ||
            _syntheticCommand23AckCount != 1 ||
            _syntheticCommand23AckCount >= PostBootCommand23AckBudget ||
            _mainToSubCommand[0] != 0 ||
            _mainToSubCommand[1] != 0x23 ||
            PostBootCommand23AckPresent())
        {
            return;
        }

        if (!StageSonicCdPostBootMmdHandoffIfNeeded())
        {
            return;
        }

        PublishPostBootCommand23Ack();
    }

    private bool StageSonicCdPostBootMmdHandoffIfNeeded()
    {
        _sonicCdMmdHandoffStageAttempts++;
        if (Disc is null)
        {
            _sonicCdMmdHandoffLastFailure = 1;
            return false;
        }

        if (!MainWordRamEntryLooksReady())
        {
            _sonicCdMmdHandoffLastFailure = 2;
            return false;
        }

        if (!Disc.TryReadIso9660File(SonicCdIpxModuleFileName, out byte[] module))
        {
            _sonicCdMmdHandoffLastFailure = 3;
            return false;
        }

        if (module.Length < SonicCdMmdHeaderBytes)
        {
            _sonicCdMmdHandoffLastFailure = 4;
            return false;
        }

        uint destination = ReadBigEndianLong(module, 2);
        ushort byteLength = ReadBigEndianWord(module, 6);
        uint entry = ReadBigEndianLong(module, 8);
        if ((destination & 0x00FF_0000u) != 0x00FF_0000u ||
            (entry & 0x00FF_0000u) != 0x00FF_0000u ||
            byteLength == 0 ||
            SonicCdMmdHeaderBytes + byteLength > module.Length)
        {
            _sonicCdMmdHandoffLastFailure = 5;
            return false;
        }

        Array.Clear(_wordRam);
        int copyLength = Math.Min(module.Length, _wordRam.Length);
        module.AsSpan(0, copyLength).CopyTo(_wordRam);
        int oneMegCopyLength = Math.Min(copyLength, SegaCdHardwareProfile.WordRamBytes / 2);
        for (int i = 0; i < oneMegCopyLength; i++)
        {
            byte value = module[i];
            uint address = (uint)i;
            _wordRam[MapOneMegWordRamAddress(address, select: false)] = value;
            _wordRam[MapOneMegWordRamAddress(address, select: true)] = value;
        }

        _mainBootIpOverrideAllowed = false;
        _sonicCdMmdHandoffStageSuccesses++;
        _sonicCdMmdHandoffLastFailure = 0;
        StageSonicCdSpxProgramExtensionIfNeeded();
        return true;
    }

    private bool StageSonicCdSpxProgramExtensionIfNeeded()
    {
        _sonicCdSpxStageAttempts++;
        if (Disc is null)
        {
            _sonicCdSpxStageLastFailure = 1;
            return false;
        }

        if (!Disc.TryReadIso9660File(SonicCdSpxModuleFileName, out byte[] module))
        {
            _sonicCdSpxStageLastFailure = 2;
            return false;
        }

        if (module.Length == 0 || SonicCdSpxProgramRamOffset + module.Length > _programRam.Length)
        {
            _sonicCdSpxStageLastFailure = 3;
            return false;
        }

        module.CopyTo(_programRam.AsSpan(SonicCdSpxProgramRamOffset));
        SeedSonicCdSpxInterruptStubs();
        M68kCpu.M68kState state = SubCpu.CaptureState();
        uint[] a = (uint[])state.A.Clone();
        a[7] = SonicCdSpxStackPointer;
        SubCpu.RestoreState(new M68kCpu.M68kState(
            (uint[])state.D.Clone(),
            a,
            SonicCdSpxEntryPoint,
            0x2000,
            false,
            state.Cycles,
            state.USP));
        _sonicCdSpxStageSuccesses++;
        _sonicCdSpxStageLastFailure = 0;
        return true;
    }

    private void SeedSonicCdSpxInterruptStubs()
    {
        WriteProgramRamLong(SonicCdSubIrq2VectorOffset, SonicCdSubLevel2Stub);

        WriteProgramRamWord(SonicCdSubUserCall2Stub, 0x4EF9);
        WriteProgramRamLong(SonicCdSubUserCall2Stub + 2, SonicCdSubSpIrq2Handler);

        WriteProgramRamWord(SonicCdSubLevel2Stub, 0x4EB9);
        WriteProgramRamLong(SonicCdSubLevel2Stub + 2, SonicCdSubUserCall2Stub);
        WriteProgramRamWord(SonicCdSubLevel2Stub + 6, 0x4E73);
    }

    private bool TryHandleSonicCdSystemLoadFileHle(out int cycles)
    {
        cycles = 0;
        if (SubCpu.PC != SonicCdSystemLoadFileEntry)
        {
            return false;
        }

        _sonicCdLoadFileHleAttempts++;
        if (Disc is null || !PostBootMmdHandoffStaged())
        {
            _sonicCdLoadFileHleLastFailure = 1;
            return false;
        }

        M68kCpu.M68kState state = SubCpu.CaptureState();
        if (!TryReadSubCString(state.A[0], 64, out string fileName) ||
            fileName.Length == 0)
        {
            _sonicCdLoadFileHleLastFailure = 2;
            return false;
        }

        if (!Disc.TryReadIso9660File(fileName, out byte[] fileData))
        {
            _sonicCdLoadFileHleLastFailure = 3;
            return false;
        }

        if (!TryCopyToSubAddress(state.A[1], fileData))
        {
            _sonicCdLoadFileHleLastFailure = 4;
            return false;
        }

        if (!TryReadSubLong(state.A[7], out uint returnAddress))
        {
            _sonicCdLoadFileHleLastFailure = 5;
            return false;
        }

        uint[] d = (uint[])state.D.Clone();
        uint[] a = (uint[])state.A.Clone();
        a[7] = (a[7] + 4) & 0x00FF_FFFF;
        SubCpu.RestoreState(new M68kCpu.M68kState(
            d,
            a,
            returnAddress,
            state.SR,
            false,
            state.Cycles,
            state.USP));
        _sonicCdLoadFileHleSuccesses++;
        _sonicCdLoadFileHleLastFailure = 0;
        _sonicCdLoadFileHleLastFileName = fileName;
        _sonicCdLoadFileHleLastDestination = state.A[1];
        _sonicCdLoadFileHleLastReturnAddress = returnAddress;
        cycles = 256 + Math.Max(1, fileData.Length / 64);
        return true;
    }

    private bool TryHandleSonicCdSystemIrq2Hle(out int cycles)
    {
        cycles = 0;
        if (SubCpu.PC != SonicCdSubSpIrq2Handler)
        {
            return false;
        }

        if (Disc is null || !PostBootMmdHandoffStaged())
        {
            return false;
        }

        M68kCpu.M68kState state = SubCpu.CaptureState();
        if (!TryReadSubLong(state.A[7], out uint returnAddress))
        {
            return false;
        }

        _subToMainStatus[0] = _mainToSubCommand[0];
        _subToMainStatus[1] = _mainToSubCommand[1];
        UpdateCommunicationWindowRegisters();

        uint[] a = (uint[])state.A.Clone();
        a[7] = (a[7] + 4) & 0x00FF_FFFF;
        SubCpu.RestoreState(new M68kCpu.M68kState(
            (uint[])state.D.Clone(),
            a,
            returnAddress,
            state.SR,
            false,
            state.Cycles,
            state.USP));
        cycles = 48;
        return true;
    }

    private bool TryHandleSonicCdSystemFileFuncHle(out int cycles)
    {
        cycles = 0;
        if (SubCpu.PC != SonicCdSystemFileFuncEntry)
        {
            return false;
        }

        if (Disc is null || !PostBootMmdHandoffStaged())
        {
            return false;
        }

        M68kCpu.M68kState state = SubCpu.CaptureState();
        ushort function = (ushort)(state.D[0] & 0xFFFF);
        uint[] d = (uint[])state.D.Clone();
        ushort sr = state.SR;
        int extraCycles = 0;

        if (function == SonicCdFileFuncLoadFile)
        {
            _sonicCdLoadFileHleAttempts++;
            if (!TryReadSubCString(state.A[0], 64, out string fileName) ||
                fileName.Length == 0)
            {
                _sonicCdLoadFileHleLastFailure = 2;
                return false;
            }

            if (!Disc.TryReadIso9660File(fileName, out byte[] fileData))
            {
                _sonicCdLoadFileHleLastFailure = 3;
                return false;
            }

            if (!TryCopyToSubAddress(state.A[1], fileData))
            {
                _sonicCdLoadFileHleLastFailure = 4;
                return false;
            }

            _sonicCdLoadFileHleSuccesses++;
            _sonicCdLoadFileHleLastFailure = 0;
            _sonicCdLoadFileHleLastFileName = fileName;
            _sonicCdLoadFileHleLastDestination = state.A[1];
            extraCycles = Math.Max(1, fileData.Length / 256);
        }
        else if (function is SonicCdFileFuncStatus or SonicCdFileFuncFindFile)
        {
            d[0] = (d[0] & 0xFFFF_0000) | SonicCdFileStatusOk;
            sr = (ushort)(sr & ~0x0001);
        }
        else if (function is not (SonicCdFileFuncInit or SonicCdFileFuncOperation or SonicCdFileFuncGetFiles or SonicCdFileFuncReset))
        {
            return false;
        }

        if (!TryReadSubLong(state.A[7], out uint returnAddress))
        {
            _sonicCdLoadFileHleLastFailure = 5;
            return false;
        }

        uint[] a = (uint[])state.A.Clone();
        a[7] = (a[7] + 4) & 0x00FF_FFFF;
        SubCpu.RestoreState(new M68kCpu.M68kState(
            d,
            a,
            returnAddress,
            sr,
            false,
            state.Cycles,
            state.USP));
        cycles = 24 + extraCycles;
        return true;
    }

    private bool TryHandleSonicCdSpxCommandWaitHle(out int cycles)
    {
        cycles = 0;
        if (SubCpu.PC is not (SonicCdSpxCommandWait or SonicCdSpxCommandWaitSecondRead) ||
            Disc is null ||
            !PostBootMmdHandoffStaged())
        {
            return false;
        }

        ushort command = CurrentMainToSubCommandWord();
        ushort status = (ushort)((_subToMainStatus[0] << 8) | _subToMainStatus[1]);
        if (command == 0)
        {
            if (status != 0)
            {
                _subToMainStatus[0] = 0;
                _subToMainStatus[1] = 0;
                UpdateCommunicationWindowRegisters();
                cycles = 24;
                return true;
            }

            cycles = 256;
            SubCpu.AddWaitCycles(cycles);
            return true;
        }

        if (!TryHandleSonicCdSpxAudioCommand(command))
        {
            return false;
        }

        _subToMainStatus[0] = _mainToSubCommand[0];
        _subToMainStatus[1] = _mainToSubCommand[1];
        UpdateCommunicationWindowRegisters();
        cycles = 96;
        return true;
    }

    private bool TryHandleSonicCdSpxAudioCommand(ushort command)
    {
        if (command == SonicCdSubCommandFadeCdda)
        {
            StopCddaPlayback(CddStatusReady);
            RefreshCddStatusRegisters(CddStatusReady);
            RaiseSubToMainFlag(0x01);
            QueueCddInterruptIfEnabled();
            return true;
        }

        if (!TryResolveSonicCdSpxCddaTrack(command, out int trackNumber) ||
            !TryStartCddaTrack(trackNumber))
        {
            return false;
        }

        return true;
    }

    private static bool TryResolveSonicCdSpxCddaTrack(ushort command, out int trackNumber)
    {
        trackNumber = command switch
        {
            >= SonicCdSubCommandPlayR1AMusic and <= SonicCdSubCommandPlayR8DMusic =>
                command - SonicCdSubCommandPlayR1AMusic + 3,
            >= SonicCdSubCommandPlayTitleMusic and <= SonicCdSubCommandPlayGameOverMusic =>
                command - SonicCdSubCommandPlayTitleMusic + 0x1D,
            >= SonicCdSubCommandTestR1AMusic and <= SonicCdSubCommandTestEndingMusic =>
                command - SonicCdSubCommandTestR1AMusic + 3,
            _ => 0,
        };
        return trackNumber > 0;
    }

    private bool TryStartCddaTrack(int trackNumber)
    {
        if (Disc is null)
        {
            return false;
        }

        DiscTrack? track = Disc.Tracks.FirstOrDefault(candidate => candidate.Number == trackNumber);
        if (track is null || track.Kind != DiscTrackKind.Audio)
        {
            return false;
        }

        _cddStatusCode = CddStatusPlay;
        _cddSeekTicksRemaining = 0;
        _currentCdcLba = track.StartLba;
        _cdcRunning = false;
        StartCddaPlayback(track.StartLba);
        RefreshCddStatusRegisters(CddStatusPlay);
        RaiseSubToMainFlag(0x01);
        QueueCddInterruptIfEnabled();
        return true;
    }

    private bool TryHandleSonicCdBramSubDoneWaitHle(out int cycles)
    {
        cycles = 0;
        if (SubCpu.PC != SonicCdBramSubDoneWait ||
            !string.Equals(_sonicCdLoadFileHleLastFileName, "BRAMSUB.BIN;1", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        M68kCpu.M68kState state = SubCpu.CaptureState();
        SubCpu.RestoreState(new M68kCpu.M68kState(
            (uint[])state.D.Clone(),
            (uint[])state.A.Clone(),
            SonicCdSpxCommandWait,
            state.SR,
            false,
            state.Cycles,
            state.USP));
        cycles = 64;
        return true;
    }

    private bool TryHandleSonicCdTitleSubReadyHle(out int cycles)
    {
        cycles = 0;
        if (SubCpu.PC != SonicCdTitleSubMainLoop ||
            !string.Equals(_sonicCdLoadFileHleLastFileName, "TITLES.BIN;1", StringComparison.OrdinalIgnoreCase) ||
            _wordRamOwnedByMain)
        {
            return false;
        }

        _wordRamOwnedByMain = true;
        _wordRamModeBits |= WordRamReturnToMain;
        UpdateWordRamModeRegister();
        cycles = 8;
        return true;
    }

    private bool TryHandleSonicCdTitleSubDoneHle(out int cycles)
    {
        cycles = 0;
        if (SubCpu.PC != SonicCdTitleSubMainLoop ||
            !string.Equals(_sonicCdLoadFileHleLastFileName, "TITLES.BIN;1", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if ((_mainCommunicationFlags & 0x01) != 0)
        {
            SetSubCommunicationFlags((byte)(_subCommunicationFlags | 0x01));
            cycles = 16;
            SubCpu.AddWaitCycles(cycles);
            return true;
        }

        if ((_subCommunicationFlags & 0x01) == 0)
        {
            return false;
        }

        ParkSubCpuAtSonicCdSpxCommandWait();
        SetSubCommunicationFlags((byte)(_subCommunicationFlags & unchecked((byte)~0x01)));
        cycles = 96;
        return true;
    }

    private void ParkSubCpuAtSonicCdSpxCommandWait()
    {
        M68kCpu.M68kState state = SubCpu.CaptureState();
        SubCpu.RestoreState(new M68kCpu.M68kState(
            (uint[])state.D.Clone(),
            (uint[])state.A.Clone(),
            SonicCdSpxCommandWait,
            state.SR,
            false,
            state.Cycles,
            state.USP));
    }

    private bool TryHandleSonicCdTitleSubIdleHle(out int cycles)
    {
        cycles = 0;
        if (SubCpu.PC != SonicCdTitleSubMainLoop ||
            !string.Equals(_sonicCdLoadFileHleLastFileName, "TITLES.BIN;1", StringComparison.OrdinalIgnoreCase) ||
            (_mainCommunicationFlags & 0x01) != 0 ||
            _mainToSubCommand[2] != 0)
        {
            return false;
        }

        cycles = 1024;
        SubCpu.AddWaitCycles(cycles);
        return true;
    }

    private bool TryHandleSonicCdBiosIrq2WaitHle(out int cycles)
    {
        cycles = 0;
        if (Disc is null ||
            !PostBootMmdHandoffStaged() ||
            SubCpu.PC is not (0x0000_05E8 or 0x0000_05EE) ||
            (_programRam[0x5EA4] & 0x01) == 0)
        {
            return false;
        }

        _programRam[0x5EA4] &= 0xFE;
        cycles = 8;
        return true;
    }

    private bool TryHandleGenericSegaCdBiosIrq2WaitHle(out int cycles)
    {
        cycles = 0;
        if (Disc is null ||
            UsesSonicCdPostBootHandoff() ||
            !_mainBootIpOverrideAllowed ||
            SubCpu.PC is not (0x0000_05E8 or 0x0000_05EE) ||
            (_programRam[0x5EA4] & 0x01) == 0 ||
            (_mainCommunicationFlags & 0x80) == 0)
        {
            return false;
        }

        _programRam[0x5EA4] &= 0xFE;
        cycles = 8;
        return true;
    }

    private bool TryHandleGenericSegaCdBiosStatusWaitHle(out int cycles)
    {
        cycles = 0;
        if (!IsGenericBootSubStatusWaitActive())
        {
            return false;
        }

        M68kCpu.M68kState state = SubCpu.CaptureState();
        uint statusAddress = (state.A[6] + 3) & (SegaCdHardwareProfile.ProgramRamBytes - 1);
        if (state.A[6] >= SegaCdHardwareProfile.ProgramRamBytes ||
            _programRam[statusAddress] == 0)
        {
            return false;
        }

        _programRam[statusAddress] = 0;
        _genericBootReadyFollowUpFlagPending = true;
        _genericBootReadyEdgeReadPending = true;
        SetSubCommunicationFlagsRaw(0x80);
        if (SubCpu.PC == 0x0000_6136)
        {
            SubCpu.RestoreState(new M68kCpu.M68kState(
                (uint[])state.D.Clone(),
                (uint[])state.A.Clone(),
                0x0000_6132,
                state.SR,
                state.Stopped,
                state.Cycles,
                state.USP));
        }

        cycles = 8;
        return true;
    }

    private bool TryHandleGenericSegaCdWordRamAssignWaitHle(out int cycles)
    {
        cycles = 0;
        if (Disc is null ||
            UsesSonicCdPostBootHandoff() ||
            !_mainBootIpOverrideAllowed ||
            SubCpu.PC != 0x0000_79DE ||
            (CurrentSubWordRamModeRegister() & WordRamMainAssignsToSub) != 0 ||
            !SubProgramRamBytesMatch(0x0000_79DE, 0x08, 0x39, 0x00, 0x01, 0x00, 0xFF, 0x80, 0x03, 0x67, 0xEE))
        {
            return false;
        }

        _wordRamOwnedByMain = false;
        _wordRamModeBits = (byte)((_wordRamModeBits & unchecked((byte)~WordRamReturnToMain)) | WordRamMainAssignsToSub);
        UpdateWordRamModeRegister();
        cycles = 8;
        return true;
    }

    private bool SubProgramRamBytesMatch(uint address, params byte[] bytes)
    {
        if (address + bytes.Length > SegaCdHardwareProfile.ProgramRamBytes)
        {
            return false;
        }

        for (int i = 0; i < bytes.Length; i++)
        {
            if (_programRam[(address + (uint)i) & (SegaCdHardwareProfile.ProgramRamBytes - 1)] != bytes[i])
            {
                return false;
            }
        }

        return true;
    }

    private bool IsGenericBootSubStatusWaitActive()
    {
        if (Disc is null ||
            UsesSonicCdPostBootHandoff() ||
            !_mainBootIpOverrideAllowed ||
            SubCpu.PC is not (0x0000_6132 or 0x0000_6136))
        {
            return false;
        }

        M68kCpu.M68kState state = SubCpu.CaptureState();
        uint statusAddress = (state.A[6] + 3) & (SegaCdHardwareProfile.ProgramRamBytes - 1);
        return state.A[6] < SegaCdHardwareProfile.ProgramRamBytes &&
            _programRam[statusAddress] != 0;
    }

    private bool TryReadSubCString(uint address, int maxLength, out string value)
    {
        Span<byte> buffer = stackalloc byte[Math.Min(maxLength, 128)];
        int length = 0;
        while (length < buffer.Length)
        {
            if (!TryReadSubByte(address + (uint)length, out byte b))
            {
                value = string.Empty;
                return false;
            }

            if (b == 0)
            {
                break;
            }

            buffer[length++] = b;
        }

        value = System.Text.Encoding.ASCII.GetString(buffer[..length]).Trim();
        return true;
    }

    private bool TryCopyToSubAddress(uint destination, ReadOnlySpan<byte> data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            if (!TryWriteSubByte(destination + (uint)i, data[i]))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryReadSubLong(uint address, out uint value)
    {
        value = 0;
        if (!TryReadSubByte(address, out byte b0) ||
            !TryReadSubByte(address + 1, out byte b1) ||
            !TryReadSubByte(address + 2, out byte b2) ||
            !TryReadSubByte(address + 3, out byte b3))
        {
            return false;
        }

        value = ((uint)b0 << 24) | ((uint)b1 << 16) | ((uint)b2 << 8) | b3;
        return true;
    }

    private bool TryReadSubByte(uint address, out byte value)
    {
        address &= 0x00FF_FFFF;
        if (address <= SegaCdHardwareProfile.SubProgramRamEndInclusive)
        {
            value = ReadProgramRamByte(address);
            return true;
        }

        if (address is >= SegaCdHardwareProfile.SubWordRamStart and <= SegaCdHardwareProfile.SubWordRamEndInclusive)
        {
            value = ReadWordRamByte(address - SegaCdHardwareProfile.SubWordRamStart);
            return true;
        }

        if (address is >= SegaCdHardwareProfile.SubWordRam1MStart and <= SegaCdHardwareProfile.SubWordRam1MEndInclusive)
        {
            value = ReadSubOneMegWordRamByte(address - SegaCdHardwareProfile.SubWordRam1MStart);
            return true;
        }

        if (address is >= SegaCdHardwareProfile.SubBackupRamStart and <= SegaCdHardwareProfile.SubBackupRamEndInclusive)
        {
            value = ReadBackupRamByte(address - SegaCdHardwareProfile.SubBackupRamStart);
            return true;
        }

        value = 0;
        return false;
    }

    private bool TryWriteSubByte(uint address, byte value)
    {
        address &= 0x00FF_FFFF;
        if (address <= SegaCdHardwareProfile.SubProgramRamEndInclusive)
        {
            WriteProgramRamByte(address, value);
            return true;
        }

        if (address is >= SegaCdHardwareProfile.SubWordRamStart and <= SegaCdHardwareProfile.SubWordRamEndInclusive)
        {
            WriteWordRamByte(address - SegaCdHardwareProfile.SubWordRamStart, value);
            return true;
        }

        if (address is >= SegaCdHardwareProfile.SubWordRam1MStart and <= SegaCdHardwareProfile.SubWordRam1MEndInclusive)
        {
            WriteSubOneMegWordRamByte(address - SegaCdHardwareProfile.SubWordRam1MStart, value);
            return true;
        }

        if (address is >= SegaCdHardwareProfile.SubBackupRamStart and <= SegaCdHardwareProfile.SubBackupRamEndInclusive)
        {
            WriteBackupRamByte(address - SegaCdHardwareProfile.SubBackupRamStart, value);
            return true;
        }

        if (address is >= SegaCdHardwareProfile.SubPcmRamStart and <= SegaCdHardwareProfile.SubPcmRamEndInclusive)
        {
            WritePcmMappedByte(address - SegaCdHardwareProfile.SubPcmRamStart, value);
            return true;
        }

        return false;
    }

    private void PublishPostBootCommand23Ack()
    {
        _subToMainStatus[0] = 0x00;
        _subToMainStatus[1] = 0x01;
        _subToMainStatus[2] = 0x00;
        _subToMainStatus[3] = 0x00;
        _syntheticCommand23AckCount++;
        _discTypeCommPacketPending = false;
        _discTypeCommPacketReadyAfterClearObserved = false;
        _discTypeCommPacketClearReadsUntilReady = 0;
        _discTypeCommPacketSyntheticEdgeUsed = false;
        UpdateCommunicationWindowRegisters();
        RaiseSubToMainFlag(0x02);
    }

    private void ClearPostBootCommand23AckOnCommandClearIfNeeded(uint offset, byte value, byte previousValue)
    {
        if (value != 0 ||
            !PostBootCommand23AckPresent())
        {
            return;
        }

        if ((offset == 0x11 && previousValue == 0x23) ||
            (offset == 0x10 && (_mainToSubCommand[1] == 0x23 || previousValue == 0x23)))
        {
            _subToMainStatus[0] = 0x00;
            _subToMainStatus[1] = 0x00;
            _subToMainStatus[2] = 0x00;
            _subToMainStatus[3] = 0x00;
            SetSubCommunicationFlags((byte)(_subCommunicationFlags & unchecked((byte)~0x02)));
            UpdateCommunicationWindowRegisters();
        }
    }

    private void ClearSonicCdBramInitAckOnCommandClearIfNeeded(uint offset, byte value, byte previousValue)
    {
        if (value != 0 ||
            _subToMainStatus[0] != 0x00 ||
            _subToMainStatus[1] != 0x87)
        {
            return;
        }

        if ((offset == 0x11 && previousValue == 0x87) ||
            (offset == 0x10 && (_mainToSubCommand[1] == 0x87 || previousValue == 0x87)))
        {
            _subToMainStatus[0] = 0x00;
            _subToMainStatus[1] = 0x00;
            UpdateCommunicationWindowRegisters();
        }
    }

    private void AdvanceIpCommand23ResponseOnMainStatusPoll(uint offset)
    {
        if (_pendingIpCommand23ResponseCycles <= 0 ||
            offset is < 0x20 or > 0x23)
        {
            return;
        }

        AdvanceIpCommand23Response(_pendingIpCommand23ResponseCycles);
    }

    private void AdvanceIpCommand23Response(int executedCycles)
    {
        if (_pendingIpCommand23ResponseCycles <= 0 || executedCycles <= 0)
        {
            return;
        }

        _pendingIpCommand23ResponseCycles -= executedCycles;
        if (_pendingIpCommand23ResponseCycles > 0)
        {
            return;
        }

        _pendingIpCommand23ResponseCycles = 0;
        if (_mainToSubCommand[0] != 0 ||
            _mainToSubCommand[1] != 0x23 ||
            BootWordRamEntryLooksReady() ||
            PostBootCommand23AckPresent())
        {
            return;
        }

        _subToMainStatus[0] = 0x00;
        _subToMainStatus[1] = 0x04;
        _subToMainStatus[2] = 0x00;
        _subToMainStatus[3] = DiscTypeForMainBios();
        UpdateCommunicationWindowRegisters();
        RaiseSubToMainFlag(0x02);
    }

    private void ClearDiscTypeCommPacketOnSplitCommandStartIfNeeded(uint offset, byte value)
    {
        if (offset <= 0x10 ||
            offset > 0x1F ||
            value == 0 ||
            _mainBootIpOverrideAllowed ||
            !DiscTypeCommPacketPresent())
        {
            return;
        }

        ClearDiscTypeCommPacketStatusIfPresent();
    }

    private void RearmDiscTypeCommPacketAfterClearObservedIfNeeded(uint offset, byte value)
    {
        if (offset != SubToMainFlagOffset ||
            !_discTypeCommPacketReadyAfterClearObserved ||
            _discTypeCommPacketPending ||
            !DiscTypeCommPacketPresent() ||
            BootWordRamEntryLooksReady() ||
            (_mainCommunicationFlags & 0x02) != 0 ||
            (value & 0x02) != 0)
        {
            return;
        }

        if (_discTypeCommPacketClearReadsUntilReady > 1)
        {
            _discTypeCommPacketClearReadsUntilReady--;
            return;
        }

        _discTypeCommPacketReadyAfterClearObserved = false;
        _discTypeCommPacketClearReadsUntilReady = 0;
        _discTypeCommPacketSyntheticEdgeUsed = true;
        _discTypeCommPacketPending = true;
        RaiseSubToMainFlag(0x02);
    }

    private void TraceRegister(string operation, uint offset, int value)
    {
        if (RegisterObserver is null)
        {
            return;
        }

        if (offset is <= 0x4B)
        {
            RegisterObserver(new SegaCdRegisterTrace(operation, offset, value & (operation.EndsWith("16", StringComparison.Ordinal) ? 0xFFFF : 0xFF)));
        }
    }

    private ushort ReadRegisterWord(uint offset)
    {
        return (ushort)((_mainRegisters[offset & (SegaCdHardwareProfile.RegisterBytes - 1)] << 8) |
            _mainRegisters[(offset + 1) & (SegaCdHardwareProfile.RegisterBytes - 1)]);
    }

    private ushort ReadWordRamWord(uint address)
    {
        return (ushort)((ReadWordRamByte(address) << 8) | ReadWordRamByte(address + 1));
    }

    private static ushort ReadBigEndianWord(ReadOnlySpan<byte> source, int offset)
    {
        return (ushort)((source[offset] << 8) | source[offset + 1]);
    }

    private static uint ReadBigEndianLong(ReadOnlySpan<byte> source, int offset)
    {
        return ((uint)source[offset] << 24) |
            ((uint)source[offset + 1] << 16) |
            ((uint)source[offset + 2] << 8) |
            source[offset + 3];
    }

    private ushort ReadProgramRamWord(uint address)
    {
        return (ushort)((ReadProgramRamByte(address) << 8) | ReadProgramRamByte(address + 1));
    }

    private uint ReadProgramRamLong(uint address)
    {
        return ((uint)ReadProgramRamByte(address) << 24) |
            ((uint)ReadProgramRamByte(address + 1) << 16) |
            ((uint)ReadProgramRamByte(address + 2) << 8) |
            ReadProgramRamByte(address + 3);
    }

    private void WriteProgramRamWord(uint address, ushort value)
    {
        WriteProgramRamByte(address, (byte)(value >> 8));
        WriteProgramRamByte(address + 1, (byte)value);
    }

    private void WriteProgramRamLong(uint address, uint value)
    {
        WriteProgramRamByte(address, (byte)(value >> 24));
        WriteProgramRamByte(address + 1, (byte)(value >> 16));
        WriteProgramRamByte(address + 2, (byte)(value >> 8));
        WriteProgramRamByte(address + 3, (byte)value);
    }

    private void AdvanceCddInterrupts(int executedCycles)
    {
        if (executedCycles <= 0 || (_mainRegisters[CddControlOffset] & CddHockBit) == 0)
        {
            return;
        }

        _cddInterruptCycleCarry += executedCycles;
        double cyclesPerInterrupt = SegaCdHardwareProfile.SubCpuClockHz / CddInterruptHz;
        while (_cddInterruptCycleCarry >= cyclesPerInterrupt)
        {
            _cddInterruptCycleCarry -= cyclesPerInterrupt;
            AdvanceCddSeekTick();
            AdvanceCddReadTick();
            if (!_cddResponseLatched)
            {
                RefreshCddStatusRegisters();
            }

            QueueSubInterrupt(CddInterruptLevel);
        }
    }

    private void AdvanceCddSeekTick()
    {
        if (_cddStatusCode != CddStatusSeek || _cddSeekTicksRemaining <= 0)
        {
            return;
        }

        _cddSeekTicksRemaining--;
        if (_cddSeekTicksRemaining == 0)
        {
            _cddStatusCode = Disc is null ? CddStatusNoDisc : CddStatusReady;
        }
    }

    private void AdvanceCddReadTick()
    {
        if (_cddStatusCode != CddStatusPlay || Disc is null)
        {
            return;
        }

        _currentCdcLba++;
        if (!Disc.TryGetTrackForLba(Math.Max(0, _currentCdcLba), out DiscTrack track) ||
            _currentCdcLba >= track.EndLbaExclusive)
        {
            StopCddaPlayback(CddStatusReady);
        }
    }

    private void AdvanceCdcInterrupts(int executedCycles)
    {
        if (executedCycles <= 0 || !_cdcRunning || (_mainRegisters[SubInterruptMaskOffset] & (1 << CdcInterruptLevel)) == 0)
        {
            return;
        }

        _cdcInterruptCycleCarry += executedCycles;
        double cyclesPerInterrupt = SegaCdHardwareProfile.SubCpuClockHz / CddInterruptHz;
        while (_cdcInterruptCycleCarry >= cyclesPerInterrupt)
        {
            _cdcInterruptCycleCarry -= cyclesPerInterrupt;
            EnsureCdcPacketPrepared();
            QueueSubInterrupt(CdcInterruptLevel);
        }
    }

    private void QueueCddInterruptIfEnabled()
    {
        int commandLba = CddCommandMsfFrames() - CddLeadInFrames;
        CddCommandObserver?.Invoke(new CddCommandTrace(
            _mainRegisters[CddCommandStart] & 0x0F,
            _mainRegisters[CddCommandStart + 3] & 0x0F,
            ((_mainRegisters[CddCommandStart + 4] & 0x0F) * 10) + (_mainRegisters[CddCommandStart + 5] & 0x0F),
            _mainRegisters[CddStatusStart] & 0x0F,
            _mainRegisters[CddStatusStart + 1] & 0x0F,
            _mainRegisters[CddStatusStart + 2] & 0x0F,
            _mainRegisters[CddStatusStart + 3] & 0x0F,
            _mainRegisters[CddStatusStart + 4] & 0x0F,
            _mainRegisters[CddStatusStart + 5] & 0x0F,
            _mainRegisters[CddStatusStart + 6] & 0x0F,
            _mainRegisters[CddStatusStart + 7] & 0x0F,
            _mainRegisters[CddStatusStart + 8] & 0x0F,
            _mainRegisters[CddStatusStart + 9] & 0x0F,
            commandLba,
            IsAudioLba(commandLba),
            _cddaPlaying,
            _cddaLba,
            _currentCdcLba));
    }

    private void RaiseSubToMainFlag(byte mask)
    {
        SetSubCommunicationFlags((byte)(_subCommunicationFlags | mask));
    }

    private void RaiseBootReadyFlagAfterClearObservedIfNeeded(uint offset, byte value)
    {
        if (_bootReadyFlagClearReadsUntilReady == 0 ||
            offset != SubToMainFlagOffset ||
            (value & 0x02) != 0)
        {
            return;
        }

        _bootReadyFlagClearReadsUntilReady--;
        if (_bootReadyFlagClearReadsUntilReady == 0)
        {
            if (Disc is not null && !UsesSonicCdPostBootHandoff() && _mainBootIpOverrideAllowed)
            {
                _genericBootReadyFollowUpFlagPending = true;
                SetSubCommunicationFlagsRaw((byte)((_subCommunicationFlags & unchecked((byte)~0x03)) | 0x80));
                return;
            }

            RaiseSubToMainFlag(0x02);
        }
    }

    private void ClearGenericSubReadyFlagAfterMainReadIfNeeded(uint offset, byte value)
    {
        if (_subFlag7BootClearCycles >= 0 ||
            offset != SubToMainFlagOffset ||
            UsesSonicCdPostBootHandoff() ||
            (value & 0x80) == 0)
        {
            return;
        }

        _subFlag7BootClearCycles = 0;
        if (Disc is not null && _mainBootIpOverrideAllowed)
        {
            byte preservedStatus = _genericBootReadyFollowUpFlagPending
                ? (byte)(value & 0x3F)
                : (byte)(value & 0x3E);
            SetSubCommunicationFlags(preservedStatus);
            return;
        }

        SetSubCommunicationFlags((byte)(_subCommunicationFlags & unchecked((byte)~0xC1)));
    }

    private void RaiseGenericBootReadyFollowUpFlagIfNeeded(uint offset, uint mainPc)
    {
        if (!_genericBootReadyFollowUpFlagPending ||
            offset != SubToMainFlagOffset ||
            mainPc == uint.MaxValue ||
            mainPc >= MainWorkRamBootHelperStart)
        {
            return;
        }

        _genericBootReadyFollowUpFlagPending = false;
        SetSubCommunicationFlagsRaw((byte)(_subCommunicationFlags | 0x02));
    }

    private byte ForceGenericBootReadyEdgeReadIfNeeded(uint offset, byte value, uint mainPc)
    {
        if (offset != SubToMainFlagOffset ||
            mainPc < MainWorkRamBootHelperStart ||
            !_genericBootReadyFollowUpFlagPending)
        {
            return value;
        }

        if (_genericBootReadyEdgeReadPending)
        {
            _genericBootReadyEdgeReadPending = false;
            return (byte)(0x80 | (_subCommunicationFlags & 0x3F));
        }

        return value;
    }

    private byte ForceGenericBootMainFlag7SubReadEdgeIfNeeded(uint offset, byte value)
    {
        if (!_genericBootMainFlag7SubReadEdgePending ||
            offset != MainToSubFlagOffset ||
            Disc is null ||
            !_mainBootIpOverrideAllowed ||
            UsesSonicCdPostBootHandoff())
        {
            return value;
        }

        uint subPc = SubCpu.PC & 0x00FF_FFFFu;
        if (subPc is < 0x0000_6000u or > 0x0000_6200u)
        {
            return value;
        }

        _genericBootMainFlag7SubReadEdgePending = false;
        return (byte)(value | 0x80);
    }

    private void ClearGenericBootReadyEdgeAfterMainReadIfNeeded(uint offset, byte value, uint mainPc)
    {
        if (!_genericBootReadyFollowUpFlagPending ||
            offset != SubToMainFlagOffset ||
            mainPc < MainWorkRamBootHelperStart ||
            (value & 0x80) == 0)
        {
            return;
        }

        SetSubCommunicationFlagsRaw((byte)(_subCommunicationFlags & unchecked((byte)~0x80)));
    }

    private void ClearGenericBootReadyFlagOnMainAckIfNeeded(byte mainFlags)
    {
        if (Disc is null ||
            UsesSonicCdPostBootHandoff() ||
            !_mainBootIpOverrideAllowed ||
            (mainFlags & 0x01) == 0 ||
            (_subCommunicationFlags & 0x02) == 0)
        {
            return;
        }

        SetSubCommunicationFlags((byte)(_subCommunicationFlags & unchecked((byte)~0x02)));
    }

    private byte ForceGenericBootWordRamRendezvousFlagReadIfNeeded(uint offset, byte value, uint mainPc)
    {
        const byte subFlag6 = 0x40;
        const byte mainRamRequest = 0x04;
        if (offset != SubToMainFlagOffset ||
            Disc is null ||
            UsesSonicCdPostBootHandoff() ||
            !_mainBootIpOverrideAllowed ||
            _subFlag7BootClearCycles > 0 ||
            mainPc == uint.MaxValue ||
            mainPc >= MainWorkRamBootHelperStart ||
            (_mainCommunicationFlags & mainRamRequest) == 0 ||
            (value & subFlag6) != 0 ||
            !GenericBootWordRamRendezvousReady(mainPc))
        {
            return value;
        }

        _wordRamOwnedByMain = true;
        _wordRamModeBits = (byte)((_wordRamModeBits & unchecked((byte)~(WordRamOneMegMode | WordRamMainAssignsToSub))) |
            WordRamReturnToMain);
        UpdateWordRamModeRegister();
        _stickySubFlag6 = false;
        SetSubCommunicationFlagsRaw((byte)(_subCommunicationFlags & unchecked((byte)~subFlag6)));
        return (byte)(value | subFlag6);
    }

    private bool GenericBootWordRamRendezvousReady(uint mainPc)
    {
        if (BootWordRamEntryLooksReady())
        {
            return true;
        }

        return _bootReadStreamActive &&
            _bootReadSectorCount > 0 &&
            mainPc is >= 0x0000_132Cu and <= 0x0000_1334u;
    }

    private byte ForceGenericBootCdcServiceReadyEdgeReadIfNeeded(uint offset, byte value, uint mainPc)
    {
        const byte subReady = 0x80;
        if (offset != SubToMainFlagOffset ||
            (value & subReady) != 0 ||
            Disc is null ||
            UsesSonicCdPostBootHandoff() ||
            !_bootReadStreamActive ||
            _bootReadSectorCount <= 0 ||
            !GenericBootCdcStreamHasReadyWork() ||
            mainPc is not (0x00FF_05C6u or 0x00FF_05CEu))
        {
            return value;
        }

        uint subPc = SubCpu.PC & 0x00FF_FFFFu;
        if (subPc is < 0x0001_8C04u or > 0x0001_8C08u)
        {
            return value;
        }

        return (byte)(value | subReady);
    }

    private bool GenericBootCdcStreamHasReadyWork()
    {
        if (_cdcPacketLength > 0)
        {
            return true;
        }

        if (_bootReadStartLba < 0 || _currentCdcLba < _bootReadStartLba)
        {
            return false;
        }

        long bootReadEndLba = (long)_bootReadStartLba + _bootReadSectorCount;
        return _currentCdcLba < bootReadEndLba;
    }

    private void ClearStickySubFlag6AfterMainRamRequestClearIfNeeded(byte previousMainFlags, byte currentMainFlags)
    {
        const byte subFlag6 = 0x40;
        const byte mainRamRequest = 0x04;
        if (!_stickySubFlag6 ||
            (previousMainFlags & mainRamRequest) == 0 ||
            (currentMainFlags & mainRamRequest) != 0)
        {
            return;
        }

        _stickySubFlag6 = false;
        SetSubCommunicationFlagsRaw((byte)(_subCommunicationFlags & unchecked((byte)~subFlag6)));
    }

    private void SetMainCommunicationFlags(byte value)
    {
        byte previousFlags = _mainCommunicationFlags;
        _mainCommunicationFlags = value;
        if (Disc is not null &&
            _mainBootIpOverrideAllowed &&
            !UsesSonicCdPostBootHandoff() &&
            (previousFlags & 0x80) == 0 &&
            (value & 0x80) != 0)
        {
            _genericBootMainFlag7PulseYieldPending = true;
            _genericBootMainFlag7SubReadEdgePending = true;
        }
        else if ((value & 0x80) == 0)
        {
            _genericBootMainFlag7PulseYieldPending = false;
        }

        UpdateCommunicationFlagRegisters();
        TraceRegister("main-flag", MainToSubFlagOffset, value);
        SyncDiscTypeCommPacketReadyFlag();
    }

    private void SetSubCommunicationFlags(byte value)
    {
        if (_bootReadyFlagClearReadsUntilReady == 0 &&
            _discTypeCommPacketPending &&
            (_mainCommunicationFlags & 0x02) == 0)
        {
            value |= 0x02;
        }

        _subCommunicationFlags = value;
        UpdateCommunicationFlagRegisters();
        TraceRegister("sub-flag", SubToMainFlagOffset, value);
    }

    private void SetSubCommunicationFlagsRaw(byte value)
    {
        _subCommunicationFlags = value;
        UpdateCommunicationFlagRegisters();
        TraceRegister("sub-flag", SubToMainFlagOffset, value);
    }

    private void SyncDiscTypeCommPacketReadyFlag()
    {
        if (!_discTypeCommPacketPending || (_mainCommunicationFlags & 0x02) == 0)
        {
            return;
        }

        ClearDiscTypeCommPacketReadyFlag();
        if (DiscTypeCommPacketPresent() && !_discTypeCommPacketSyntheticEdgeUsed)
        {
            _discTypeCommPacketReadyAfterClearObserved = true;
            _discTypeCommPacketClearReadsUntilReady = 2;
        }
    }

    private void UpdateCommunicationFlagRegisters()
    {
        _mainRegisters[MainToSubFlagOffset] = _mainCommunicationFlags;
        _mainRegisters[SubToMainFlagOffset] = _subCommunicationFlags;
    }

    private void UpdateCommunicationWindowRegisters()
    {
        Array.Copy(_mainToSubCommand, 0, _mainRegisters, 0x10, _mainToSubCommand.Length);
        Array.Copy(_subToMainStatus, 0, _mainRegisters, 0x20, _subToMainStatus.Length);
    }

    private void QueueSubInterrupt(int level)
    {
        if (level is <= 0 or > 7)
        {
            return;
        }

        _pendingSubInterruptLevels |= (byte)(1 << level);
    }

    private void QueueSubInterruptIfEnabled(int level)
    {
        if (level is <= 0 or > 7)
        {
            return;
        }

        if ((_mainRegisters[SubInterruptMaskOffset] & (1 << level)) == 0)
        {
            return;
        }

        QueueSubInterrupt(level);
    }

    private void ServicePendingSubInterrupts()
    {
        int mask = (SubCpu.SR >> 8) & 0x07;
        for (int level = 7; level > mask; level--)
        {
            byte bit = (byte)(1 << level);
            if ((_pendingSubInterruptLevels & bit) == 0)
            {
                continue;
            }

            if (level == 2 && ShouldRefreshSonicCdIrq2Bridge())
            {
                SeedSonicCdSpxInterruptStubs();
            }
            else if (level == 2 && ShouldRefreshGenericBootIrq2Bridge())
            {
                SeedGenericBootIrq2Bridge();
            }

            if (SubCpu.RequestInterrupt(level))
            {
                _pendingSubInterruptLevels &= (byte)~bit;
            }

            return;
        }
    }

    private bool ShouldRefreshSonicCdIrq2Bridge()
    {
        return Disc is not null &&
            PostBootMmdHandoffStaged() &&
            _sonicCdSpxStageSuccesses > 0;
    }

    private bool ShouldRefreshGenericBootIrq2Bridge()
    {
        if (Disc is null ||
            !_mainBootIpOverrideAllowed ||
            UsesSonicCdPostBootHandoff() ||
            ReadProgramRamWord(SonicCdSubUserCall2Stub) != 0x4EF9)
        {
            return false;
        }

        uint callback = ReadProgramRamLong(SonicCdSubUserCall2Stub + 2);
        return callback is >= SystemProgramRamLoadOffset and < SegaCdHardwareProfile.ProgramRamBytes;
    }

    private void SeedGenericBootIrq2Bridge()
    {
        WriteProgramRamLong(SonicCdSubIrq2VectorOffset, SonicCdSubLevel2Stub);
        WriteProgramRamWord(SonicCdSubLevel2Stub, 0x4EB9);
        WriteProgramRamLong(SonicCdSubLevel2Stub + 2, SonicCdSubUserCall2Stub);
        WriteProgramRamWord(SonicCdSubLevel2Stub + 6, 0x4E73);
    }

    public sealed record SegaCdState(
        byte[] ProgramRam,
        byte[] WordRam,
        byte[] BackupRam,
        byte[] PcmRam,
        byte[] MainRegisters,
        byte[] MainToSubCommand,
        byte[] SubToMainStatus,
        bool SubBiosMapped,
        bool SubCpuResetReleased,
        bool SubCpuBusRequested,
        double CddInterruptCycleCarry,
        double CdcInterruptCycleCarry,
        byte CddStatusCode,
        bool CddStatusReady,
        bool CddResponseLatched,
        int CddSeekTicksRemaining,
        byte[] CdcRegisters,
        byte[] CdcPacket,
        byte CdcAddress,
        int CdcPacketOffset,
        int CdcPacketLength,
        int CurrentCdcLba,
        int BootReadStartLba,
        int BootReadSectorCount,
        bool BootReadStreamActive,
        bool CdcRunning,
        int CddaLba,
        int CddaSectorLba,
        int CddaSectorSampleIndex,
        bool CddaPlaying,
        byte[] CddaSector,
        PcmChannelState[] PcmChannels,
        byte PcmControlChannel,
        ushort PcmWriteBank,
        bool PcmEnabled,
        double PcmRenderCycleCarry,
        bool StickySubFlag6,
        int SubFlag7BootClearCycles,
        byte BootReadyFlagClearReadsUntilReady,
        bool GenericBootReadyFollowUpFlagPending,
        bool GenericBootReadyEdgeReadPending,
        bool GenericBootMainFlag7PulseYieldPending,
        bool GenericBootMainFlag7SubReadEdgePending,
        byte PendingSubInterruptLevels,
        byte WordRamModeBits,
        bool WordRamOwnedByMain,
        bool SuppressBootStatusUntilMainCommand,
        byte MainCommunicationFlags,
        byte SubCommunicationFlags,
        bool DiscTypeCommPacketPending,
        bool DiscTypeCommPacketReadyAfterClearObserved,
        byte DiscTypeCommPacketClearReadsUntilReady,
        bool DiscTypeCommPacketSyntheticEdgeUsed,
        bool MainBootIpOverrideAllowed,
        byte SyntheticCommand23AckCount,
        M68kCpu.M68kState SubCpu);

    public sealed record CddCommandTrace(
        int Command,
        int Request,
        int TrackNumber,
        int Status,
        int Report,
        int Rs2,
        int Rs3,
        int Rs4,
        int Rs5,
        int Rs6,
        int Rs7,
        int Rs8,
        int Checksum,
        int CommandLba,
        bool CommandLbaIsAudio,
        bool CddaPlaying,
        int CddaLba,
        int CurrentCdcLba);

    public sealed record SegaCdRegisterTrace(
        string Operation,
        uint Offset,
        int Value);

    public sealed record SegaCdSubMemoryWriteTrace(
        uint Pc,
        uint Address,
        byte Value);

    public sealed record PcmChannelState(
        bool Enabled,
        byte Envelope,
        byte Pan,
        byte Start,
        uint Address,
        ushort Step,
        ushort LoopStart);

    private struct PcmChannel
    {
        public bool Enabled;
        public byte Envelope;
        public byte Pan;
        public byte Start;
        public uint Address;
        public ushort Step;
        public ushort LoopStart;
    }
}
