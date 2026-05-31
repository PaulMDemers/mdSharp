namespace MdSharp.Core.Video;

public enum VdpDebugLayer
{
    PlaneA,
    PlaneB,
    Sprites,
}

public enum VdpPlaneSourceKind
{
    Plane,
    Window,
    OutsideActiveDisplay,
    DisplayDisabled,
}

public sealed class Vdp
{
    public const int ScreenWidth = 320;
    public const int ScreenHeight = 224;
    public const int NtscScanlines = 262;
    public const int PalScanlines = 313;
    private const int MaxSprites = 80;

    private readonly byte[] _vram = new byte[64 * 1024];
    private readonly byte[] _visibleFrameVram = new byte[64 * 1024];
    private readonly byte[][] _lineVram = new byte[ScreenHeight][];
    private readonly int[] _lineVramSourceLine = new int[ScreenHeight];
    private readonly bool[] _lineVramCaptured = new bool[ScreenHeight];
    private readonly ushort[] _cram = new ushort[64];
    private readonly ushort[] _vsram = new ushort[40];
    private readonly ushort[,] _lineVsram = new ushort[ScreenHeight, 40];
    private readonly bool[] _lineVsramCaptured = new bool[ScreenHeight];
    private readonly ushort[,] _lineHscroll = new ushort[ScreenHeight, 2];
    private readonly bool[] _lineHscrollCaptured = new bool[ScreenHeight];
    private readonly ushort[,] _lineCram = new ushort[ScreenHeight, 64];
    private readonly bool[] _lineCramCaptured = new bool[ScreenHeight];
    private readonly byte[] _lineRegisters = new byte[ScreenHeight * 32];
    private readonly bool[] _lineRegistersCaptured = new bool[ScreenHeight];
    private readonly SpriteInstance[] _lineSpriteSnapshots = new SpriteInstance[ScreenHeight * MaxSprites];
    private readonly int[] _lineSpriteSnapshotCounts = new int[ScreenHeight];
    private readonly bool[] _lineSpriteSnapshotCaptured = new bool[ScreenHeight];
    private readonly LayerPixel[] _lineSpritePixelSnapshots = new LayerPixel[ScreenHeight * ScreenWidth];
    private readonly uint[] _lineSpritePackedPixelSnapshots = new uint[ScreenHeight * ScreenWidth];
    private readonly bool[] _lineSpritePixelSnapshotCaptured = new bool[ScreenHeight];
    private readonly byte[] _registers = new byte[32];
    private readonly byte[] _renderSavedRegisters = new byte[32];
    private readonly List<string> _traceEvents = new();
    private readonly List<RegisterWrite> _registerWrites = new();
    private readonly List<ControlCommand> _controlCommands = new();
    private readonly List<DmaEvent> _dmaEvents = new();
    private readonly List<ushort> _directColorSamples = new();
    private readonly Rgb[] _renderPalette = new Rgb[64];
    private readonly Rgb[] _renderShadowPalette = new Rgb[64];
    private readonly Rgb[] _renderHighlightPalette = new Rgb[64];
    private readonly LineSprite[] _renderLineSprites = new LineSprite[20];
    private readonly LineSprite[] _renderEvenLineSprites = new LineSprite[20];
    private readonly LineSprite[] _renderOddLineSprites = new LineSprite[20];
    private readonly LineSprite[] _captureLineSprites = new LineSprite[20];
    private readonly LayerPixel[] _linePlaneB = new LayerPixel[ScreenWidth];
    private readonly LayerPixel[] _linePlaneA = new LayerPixel[ScreenWidth];
    private readonly LayerPixel[] _lineSprites = new LayerPixel[ScreenWidth];
    private readonly LayerPixel[] _captureLineSpritePixels = new LayerPixel[ScreenWidth];
    private readonly uint[] _linePlaneBPacked = new uint[ScreenWidth];
    private readonly uint[] _linePlaneAPacked = new uint[ScreenWidth];
    private readonly uint[] _lineSpritesPacked = new uint[ScreenWidth];
    private readonly uint[] _captureLineSpritePackedPixels = new uint[ScreenWidth];
    private readonly bool[] _lastFrameOpaquePixels = new bool[ScreenWidth * ScreenHeight];
    private readonly bool[] _lastFramePriorityPixels = new bool[ScreenWidth * ScreenHeight];
    private readonly int[] _linePlaneAVScroll = new int[20];
    private readonly int[] _linePlaneBVScroll = new int[20];
    private readonly SpriteInstance[] _renderSprites = new SpriteInstance[MaxSprites];
    private readonly SpriteInstance[] _captureSprites = new SpriteInstance[MaxSprites];
    private readonly int[] _spriteVisitMarks = new int[MaxSprites];
    private readonly int[] _spriteCollisionMarks = new int[ScreenWidth];

    private bool _pendingControl;
    private ushort _firstControlWord;
    private uint _address;
    private byte _code;
    private int _vramWritesTraced;
    private int _cramWritesTraced;
    private DmaRequest? _pendingDmaFill;
    private int _directColorCaptureRemaining;
    private bool _capturingDirectColorDma;
    private bool _visibleFrameVramCaptured;
    private byte[]? _renderVram;
    private int _vramGeneration;
    private int _lineVramLastGeneration;
    private int _lineVramLastSourceLine;
    private int _scanline;
    private int _hintCounter;
    private int _fifoWords;
    private bool _oddFrame;
    private bool _vBlank;
    private bool _vInterruptPending;
    private bool _hInterruptPending;
    private bool _hBlank;
    private bool _spriteOverflow;
    private bool _spriteCollision;

    public Vdp()
    {
        for (int i = 0; i < _lineVram.Length; i++)
        {
            _lineVram[i] = new byte[64 * 1024];
        }
    }
    private bool _commandJustCompleted;
    private int _spriteVisitStamp;
    private int _spriteCollisionStamp;

    public ReadOnlySpan<byte> Vram => _vram;
    public ReadOnlySpan<ushort> Cram => _cram;
    public ReadOnlySpan<ushort> Vsram => _vsram;
    public ReadOnlySpan<byte> Registers => _registers;
    public IReadOnlyList<string> TraceEvents => _traceEvents;
    public IReadOnlyList<RegisterWrite> RegisterWrites => _registerWrites;
    public IReadOnlyList<ControlCommand> ControlCommands => _controlCommands;
    public IReadOnlyList<DmaEvent> DmaEvents => _dmaEvents;
    public IReadOnlyList<ushort> DirectColorSamples => _directColorSamples;
    public bool TraceEnabled { get; set; }
    public int TraceLimit { get; set; } = 512;
    public ushort Status { get; private set; } = 0x3400;
    public int CurrentScanline => _scanline;
    public int FifoWords => _fifoWords;
    public bool VInterruptPending => _vInterruptPending;
    public bool VInterruptLineActive => _vInterruptPending && _vBlank;
    public bool HInterruptPending => _hInterruptPending;
    public int DmaCycleDebt { get; private set; }
    public bool UseLineVramSnapshots { get; set; } = true;
    public bool CollectRenderPerformance { get; set; }
    public RenderPerformanceCounters LastRenderPerformance { get; private set; }
    public int? LastRenderFallbackNameTableBase { get; private set; }
    public int? LastRenderFallbackTileStart { get; private set; }
    public string LastRenderMode { get; private set; } = "planes";
    public ReadOnlySpan<bool> LastFrameOpaquePixels => _lastFrameOpaquePixels;
    public ReadOnlySpan<bool> LastFramePriorityPixels => _lastFramePriorityPixels;

    public byte AutoIncrement => _registers[15];

    public readonly record struct DmaRequest(uint SourceAddress, int LengthWords, byte Code, uint DestinationAddress, byte Mode);
    public readonly record struct RegisterWrite(int Register, byte PreviousValue, byte Value);
    public readonly record struct ControlCommand(byte Code, uint Address, ushort FirstWord, ushort SecondWord);
    public readonly record struct DmaEvent(byte Mode, byte Code, uint SourceAddress, uint DestinationAddress, int LengthWords, string Operation);

    [Flags]
    public enum Interrupts
    {
        None = 0,
        Horizontal = 1,
        Vertical = 2,
    }

    public ushort ReadDataPort()
    {
        ushort value = _code switch
        {
            0x00 => ReadVramWord(_address),
            0x08 => _cram[(_address / 2) & 0x3F],
            0x04 => _vsram[(_address / 2) % 40],
            _ => 0,
        };

        IncrementAddress();
        return value;
    }

    public void WriteDataPort(ushort value)
    {
        if (_pendingDmaFill is DmaRequest request)
        {
            _pendingDmaFill = null;
            RunDmaFill(request, value);
            return;
        }

        switch (_code & 0x0F)
        {
            case 0x01:
                WriteVramWord(_address, value);
                EnqueueFifoWord();
                if (TraceEnabled)
                {
                    TraceLimited(ref _vramWritesTraced, 64, $"VDP DATA VRAM[${_address & 0xFFFF:X4}] <= ${value:X4} code=${_code:X2}");
                }
                break;
            case 0x03:
                _cram[(_address / 2) & 0x3F] = (ushort)(value & 0x0EEE);
                EnqueueFifoWord();
                if (TraceEnabled)
                {
                    TraceLimited(ref _cramWritesTraced, 64, $"VDP DATA CRAM[{(_address / 2) & 0x3F:D2}] <= ${value & 0x0EEE:X4} addr=${_address & 0xFFFF:X4}");
                }
                break;
            case 0x05:
                _vsram[(_address / 2) % 40] = (ushort)(value & 0x07FF);
                EnqueueFifoWord();
                if (TraceEnabled)
                {
                    Trace($"VDP DATA VSRAM[{(_address / 2) % 40:D2}] <= ${value & 0x07FF:X4} addr=${_address & 0xFFFF:X4}");
                }
                break;
        }

        IncrementAddress();
    }

    public ushort ReadControlPort()
    {
        UpdateStatusFlags();
        ushort status = Status;
        _pendingControl = false;
        if (!_vBlank)
        {
            _vInterruptPending = false;
        }

        _hInterruptPending = false;
        _spriteOverflow = false;
        _spriteCollision = false;
        UpdateStatusFlags();
        return status;
    }

    public ushort ReadHvCounter()
    {
        return ReadHvCounter(horizontalMasterCycleOffset: 0, masterCyclesPerScanline: 1);
    }

    public ushort ReadHvCounter(int horizontalMasterCycleOffset, int masterCyclesPerScanline)
    {
        int visibleLines = IsPalTiming() ? 240 : 224;
        int totalLines = IsPalTiming() ? PalScanlines : NtscScanlines;
        int v = _scanline < visibleLines ? _scanline : _scanline - (totalLines - 256);
        v &= 0xFF;
        int baseH = IsH40() ? 0xA4 : 0x84;
        int lineCycles = Math.Max(1, masterCyclesPerScanline);
        int offset = Math.Clamp(horizontalMasterCycleOffset, 0, lineCycles - 1);
        int h = (baseH + ((offset * 256) / lineCycles)) & 0xFF;
        return (ushort)((v << 8) | h);
    }

    public void WriteControlPort(ushort value)
    {
        _commandJustCompleted = false;
        if ((value & 0xC000) == 0x8000)
        {
            int register = (value >> 8) & 0x1F;
            byte previous = _registers[register];
            _registers[register] = (byte)value;
            AddBounded(_registerWrites, new RegisterWrite(register, previous, (byte)value));
            if (TraceEnabled)
            {
                Trace($"VDP REG {register:D2} <= ${value & 0xFF:X2}");
            }
            _pendingControl = false;
            return;
        }

        if (!_pendingControl)
        {
            _firstControlWord = value;
            _pendingControl = true;
            _address = (uint)(value & 0x3FFF);
            _code = (byte)((value >> 14) & 0x03);
            if (TraceEnabled)
            {
                Trace($"VDP CTRL first=${value:X4} partialCode=${_code:X2} partialAddr=${_address:X4}");
            }
            return;
        }

        _address = (uint)((_firstControlWord & 0x3FFF) | ((value & 0x0003) << 14));
        _code = (byte)(((_firstControlWord >> 14) & 0x03) | ((value >> 2) & 0x3C));
        AddBounded(_controlCommands, new ControlCommand(_code, _address, _firstControlWord, value));
        if (TraceEnabled)
        {
            Trace($"VDP CTRL second=${value:X4} code=${_code:X2} addr=${_address:X4}");
        }
        _pendingControl = false;
        _commandJustCompleted = true;
    }

    public bool TryDequeueDmaRequest(out DmaRequest request)
    {
        if (!_commandJustCompleted)
        {
            request = default;
            return false;
        }

        _commandJustCompleted = false;
        bool dmaEnabled = (_registers[1] & 0x10) != 0;
        bool dmaCommand = (_code & 0x20) != 0;
        if (!dmaEnabled || !dmaCommand)
        {
            request = default;
            return false;
        }

        int length = _registers[19] | (_registers[20] << 8);
        if (length == 0)
        {
            length = 0x10000;
        }

        byte mode = (byte)((_registers[23] >> 6) & 0x03);
        uint source = (uint)(((_registers[23] & 0x7F) << 17) | (_registers[22] << 9) | (_registers[21] << 1));
        request = new DmaRequest(source, length, _code, _address, mode);
        if (TraceEnabled)
        {
            Trace($"VDP DMA request mode={mode} source=${source:X6} dest=${_address:X4} words={length} code=${_code:X2}");
        }
        return true;
    }

    public void BeginDmaMemoryCopy(DmaRequest request)
    {
        AddDmaEvent(request, "68k-to-vdp");
        BeginDirectColorCaptureIfNeeded(request);
    }

    public void BeginDmaFill(DmaRequest request)
    {
        _pendingDmaFill = request;
        AddDmaEvent(request, "fill-armed", chargeDmaCycles: false);
        if (TraceEnabled)
        {
            Trace($"VDP DMA fill armed dest=${request.DestinationAddress:X4} words={request.LengthWords} code=${request.Code:X2}");
        }
    }

    public void RunDmaVramCopy(DmaRequest request)
    {
        uint source = request.SourceAddress & 0xFFFF;
        AddDmaEvent(request, "vram-copy");
        for (int i = 0; i < request.LengthWords; i++)
        {
            WriteDmaWord(ReadVramWord(source));
            source = (source + 2) & 0xFFFF;
        }
    }

    public void WriteDmaWord(ushort value)
    {
        CaptureDirectColorSample(value);
        WriteDataPort(value);
    }

    public void TraceExternal(string message)
    {
        Trace(message);
    }

    private void AddDmaEvent(DmaRequest request, string operation, bool chargeDmaCycles = true)
    {
        AddBounded(_dmaEvents, new DmaEvent(request.Mode, request.Code, request.SourceAddress, request.DestinationAddress, request.LengthWords, operation));
        if (chargeDmaCycles)
        {
            DmaCycleDebt += Math.Max(1, request.LengthWords * 2);
        }
    }

    private void RunDmaFill(DmaRequest request, ushort value)
    {
        _address = request.DestinationAddress;
        _code = request.Code;
        WriteDataPort(value);
        AddDmaEvent(request, TraceEnabled ? $"fill value=${value:X4}" : "fill");
        byte fill = (byte)value;
        for (int i = 0; i < request.LengthWords; i++)
        {
            WriteDmaFillByte(fill);
        }
    }

    private void WriteDmaFillByte(byte value)
    {
        if ((_code & 0x0F) == 0x01)
        {
            _vram[_address & 0xFFFF] = value;
            _vramGeneration++;
            IncrementAddress();
        }
    }

    public void BeginFrame(bool pal)
    {
        _scanline = 0;
        _hintCounter = _registers[10];
        _oddFrame = !_oddFrame;
        _hBlank = false;
        _vBlank = false;
        _hInterruptPending = false;
        Status = (ushort)(Status & ~0x000C);
        Array.Clear(_lineVsramCaptured);
        Array.Clear(_lineHscrollCaptured);
        Array.Clear(_lineCramCaptured);
        Array.Clear(_lineRegistersCaptured);
        Array.Clear(_lineVramCaptured);
        _lineVramLastGeneration = -1;
        _lineVramLastSourceLine = -1;
        Array.Clear(_lineSpriteSnapshotCaptured);
        Array.Clear(_lineSpriteSnapshotCounts);
        Array.Clear(_lineSpritePixelSnapshotCaptured);
        _visibleFrameVramCaptured = false;
        if (pal)
        {
            Status |= 0x0001;
        }
        else
        {
            Status &= unchecked((ushort)~0x0001);
        }

        UpdateStatusFlags();
    }

    public Interrupts StepScanline(int scanline, bool pal)
    {
        _scanline = scanline;
        _hBlank = false;
        CaptureLineRegisters(scanline);
        if (!IsLineVramCaptured(scanline))
        {
            CaptureLineVram(scanline);
        }
        CaptureLineVsram(scanline);
        CaptureLineHscroll(scanline);
        CaptureLineCram(scanline);
        CaptureLineSprites(scanline);
        int visibleLines = pal ? 240 : 224;
        int totalLines = pal ? PalScanlines : NtscScanlines;
        Interrupts interrupts = Interrupts.None;

        if (scanline == visibleLines)
        {
            CaptureVisibleFrameVram();
            _vBlank = true;
            _vInterruptPending = true;
            interrupts |= Interrupts.Vertical;
        }
        else if (scanline == 0)
        {
            _vBlank = false;
        }

        if (scanline < visibleLines)
        {
            if (_hintCounter <= 0)
            {
                _hintCounter = _registers[10];
                _hInterruptPending = true;
                interrupts |= Interrupts.Horizontal;
            }
            else
            {
                _hintCounter--;
            }
        }

        if (scanline == totalLines - 1)
        {
            _vBlank = false;
            _hInterruptPending = false;
        }

        DrainFifo(words: 8);
        UpdateStatusFlags();
        return interrupts;
    }

    public void SetHBlank(bool active)
    {
        _hBlank = active;
        UpdateStatusFlags();
    }

    public void AcknowledgeM68kInterrupt(int level)
    {
        switch (level)
        {
            case 4:
                _hInterruptPending = false;
                break;
            case 6:
                // The 68k interrupt acknowledge accepts the IRQ line, but software can
                // still observe the V interrupt status latch through the control port.
                break;
            default:
                return;
        }

        UpdateStatusFlags();
    }

    private void CaptureLineVsram(int scanline)
    {
        if ((uint)scanline >= ScreenHeight)
        {
            return;
        }

        for (int i = 0; i < _vsram.Length; i++)
        {
            _lineVsram[scanline, i] = _vsram[i];
        }

        _lineVsramCaptured[scanline] = true;
    }

    private void CaptureLineVram(int scanline)
    {
        if ((uint)scanline >= ScreenHeight)
        {
            return;
        }

        if (_lineVramLastGeneration != _vramGeneration || _lineVramLastSourceLine < 0)
        {
            Array.Copy(_vram, _lineVram[scanline], _vram.Length);
            _lineVramSourceLine[scanline] = scanline;
            _lineVramLastGeneration = _vramGeneration;
            _lineVramLastSourceLine = scanline;
        }
        else
        {
            _lineVramSourceLine[scanline] = _lineVramLastSourceLine;
        }

        _lineVramCaptured[scanline] = true;
    }

    public void CaptureLineVramForDmaTiming(int scanline)
    {
        if (!UseLineVramSnapshots || IsLineVramCaptured(scanline))
        {
            return;
        }

        CaptureLineVram(scanline);
    }

    private bool IsLineVramCaptured(int scanline)
    {
        return (uint)scanline < ScreenHeight && _lineVramCaptured[scanline];
    }

    private void CaptureLineRegisters(int scanline)
    {
        if ((uint)scanline >= ScreenHeight)
        {
            return;
        }

        Array.Copy(_registers, 0, _lineRegisters, scanline * 32, 32);
        _lineRegistersCaptured[scanline] = true;
    }

    private void CaptureLineHscroll(int scanline)
    {
        if ((uint)scanline >= ScreenHeight)
        {
            return;
        }

        _lineHscroll[scanline, 0] = ReadHorizontalScrollWord(PlaneKind.PlaneA, scanline);
        _lineHscroll[scanline, 1] = ReadHorizontalScrollWord(PlaneKind.PlaneB, scanline);
        _lineHscrollCaptured[scanline] = true;
    }

    private void CaptureLineCram(int scanline)
    {
        if ((uint)scanline >= ScreenHeight)
        {
            return;
        }

        for (int i = 0; i < _cram.Length; i++)
        {
            _lineCram[scanline, i] = _cram[i];
        }

        _lineCramCaptured[scanline] = true;
    }

    private void CaptureLineSprites(int scanline)
    {
        if ((uint)scanline >= ScreenHeight)
        {
            return;
        }

        int count = GatherSprites(_captureSprites);
        int offset = scanline * MaxSprites;
        Array.Copy(_captureSprites, 0, _lineSpriteSnapshots, offset, count);
        _lineSpriteSnapshotCounts[scanline] = count;
        _lineSpriteSnapshotCaptured[scanline] = true;

        if (!IsInterlaceDoubleResolution())
        {
            int activeWidth = IsH40() ? 320 : 256;
            int lineSpriteCount = GatherLineSprites(_captureSprites, count, scanline, _captureLineSprites);
            RenderSpriteLine(_captureLineSpritePixels, activeWidth, scanline, scanline, _captureLineSprites, lineSpriteCount);
            Array.Copy(_captureLineSpritePixels, 0, _lineSpritePixelSnapshots, scanline * ScreenWidth, ScreenWidth);
            RenderSpriteLinePacked(_captureLineSpritePackedPixels, activeWidth, scanline, scanline, _captureLineSprites, lineSpriteCount);
            Array.Copy(_captureLineSpritePackedPixels, 0, _lineSpritePackedPixelSnapshots, scanline * ScreenWidth, ScreenWidth);
            _lineSpritePixelSnapshotCaptured[scanline] = true;
        }
    }

    private void CaptureVisibleFrameVram()
    {
        Array.Copy(_vram, _visibleFrameVram, _vram.Length);
        _visibleFrameVramCaptured = true;
    }

    private byte[] SelectRenderVramForLine(int lineY)
    {
        return UseLineVramSnapshots && (uint)lineY < ScreenHeight && _lineVramCaptured[lineY]
            ? _lineVram[_lineVramSourceLine[lineY]]
            : _visibleFrameVramCaptured
                ? _visibleFrameVram
                : _vram;
    }

    public byte[] RenderFrameRgb()
    {
        byte[] framebuffer = new byte[ScreenWidth * ScreenHeight * 3];
        RenderFrameRgbInto(framebuffer);
        return framebuffer;
    }

    public void RenderFrameRgbInto(byte[] framebuffer)
    {
        RenderFrameInto(framebuffer, FramePixelOrder.Rgb);
    }

    public void RenderFrameBgrInto(byte[] framebuffer)
    {
        RenderFrameInto(framebuffer, FramePixelOrder.Bgr);
    }

    private void RenderFrameInto(byte[] framebuffer, FramePixelOrder pixelOrder)
    {
        if (framebuffer.Length != ScreenWidth * ScreenHeight * 3)
        {
            throw new ArgumentException("Unexpected Genesis framebuffer size.", nameof(framebuffer));
        }

        LastRenderFallbackNameTableBase = null;
        LastRenderFallbackTileStart = null;
        LastRenderMode = "planes";
        LastRenderPerformance = default;
        Array.Clear(_lastFrameOpaquePixels);
        Array.Clear(_lastFramePriorityPixels);

        if (!IsDisplayEnabled())
        {
            int background = _registers[7] & 0x3F;
            if (CollectRenderPerformance)
            {
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                Fill(framebuffer, DecodeColor(background), pixelOrder);
                LastRenderPerformance = new RenderPerformanceCounters(DisplayFillTicks: System.Diagnostics.Stopwatch.GetTimestamp() - start);
            }
            else
            {
                Fill(framebuffer, DecodeColor(background), pixelOrder);
            }

            LastRenderMode = "display-disabled";
            return;
        }

        _renderVram = _visibleFrameVramCaptured ? _visibleFrameVram : _vram;
        try
        {
            if (HasDirectColorDmaFrame())
            {
                if (CollectRenderPerformance)
                {
                    long start = System.Diagnostics.Stopwatch.GetTimestamp();
                    _ = RenderDirectColorDmaFrame(framebuffer, pixelOrder);
                    LastRenderPerformance = new RenderPerformanceCounters(DirectColorTicks: System.Diagnostics.Stopwatch.GetTimestamp() - start);
                }
                else
                {
                    _ = RenderDirectColorDmaFrame(framebuffer, pixelOrder);
                }
            }
            else
            {
                _ = RenderCompositedLayers(framebuffer, pixelOrder);
            }
        }
        finally
        {
            _renderVram = null;
        }
    }

    public byte[] RenderTileAtlasRgb(int columns = 32, int rows = 16, int startTile = 0)
    {
        int width = columns * 8;
        int height = rows * 8;
        byte[] framebuffer = new byte[width * height * 3];

        for (int tile = 0; tile < columns * rows; tile++)
        {
            int tileX = (tile % columns) * 8;
            int tileY = (tile / columns) * 8;
            int tileIndex = startTile + tile;

            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    int color = ReadRawTilePixel(tileIndex, x, y);
                    byte shade = (byte)(color * 17);
                    int offset = (((tileY + y) * width) + tileX + x) * 3;
                    framebuffer[offset] = shade;
                    framebuffer[offset + 1] = shade;
                    framebuffer[offset + 2] = shade;
                }
            }
        }

        return framebuffer;
    }

    public byte[] RenderDebugLayerRgb(VdpDebugLayer layer)
    {
        byte[] framebuffer = new byte[ScreenWidth * ScreenHeight * 3];
        RenderDebugLayerRgbInto(framebuffer, layer);
        return framebuffer;
    }

    public VdpPlanePixelTrace TracePlanePixel(VdpDebugLayer layer, int screenX, int screenY)
    {
        if (layer is not (VdpDebugLayer.PlaneA or VdpDebugLayer.PlaneB))
        {
            throw new ArgumentException("Only PlaneA and PlaneB are supported by plane pixel tracing.", nameof(layer));
        }

        if (!IsDisplayEnabled())
        {
            return VdpPlanePixelTrace.Disabled(layer, screenX, screenY);
        }

        _renderVram = _visibleFrameVramCaptured ? _visibleFrameVram : _vram;
        Array.Copy(_registers, _renderSavedRegisters, _registers.Length);
        Array.Clear(_lastFrameOpaquePixels);
        Array.Clear(_lastFramePriorityPixels);
        try
        {
            int lineY = Math.Clamp(screenY, 0, ScreenHeight - 1);
            _renderVram = SelectRenderVramForLine(lineY);
            ApplyCapturedRegistersForLine(lineY);
            (int planeWidthTiles, int planeHeightTiles) = GetPlaneSize();
            int activeWidth = IsH40() ? 320 : 256;
            int xOffset = IsH40() ? 0 : 32;
            int planeX = screenX - xOffset;
            if ((uint)planeX >= activeWidth)
            {
                return VdpPlanePixelTrace.Outside(layer, screenX, screenY, activeWidth, xOffset, planeWidthTiles, planeHeightTiles);
            }

            BuildLineVerticalScroll(lineY);
            if (layer == VdpDebugLayer.PlaneA && IsWindowActiveAt(planeX, lineY, activeWidth))
            {
                return TraceWindowPixel(layer, screenX, screenY, planeX, lineY, activeWidth, xOffset, planeWidthTiles, planeHeightTiles);
            }

            PlaneKind plane = layer == VdpDebugLayer.PlaneB ? PlaneKind.PlaneB : PlaneKind.PlaneA;
            int nameTableBase = plane == PlaneKind.PlaneB ? GetPlaneBNameTableBase() : GetPlaneANameTableBase();
            int scrollX = GetHorizontalScroll(plane, lineY);
            int scrollY = GetVerticalScroll(plane, planeX, lineY);
            int tileHeight = TileHeightPixels();
            int sourceX = (planeX - scrollX) & ((planeWidthTiles * 8) - 1);
            int sourceY = (lineY + scrollY) & ((planeHeightTiles * tileHeight) - 1);
            int tileX = sourceX >> 3;
            int tileY = tileHeight == 16 ? sourceY >> 4 : sourceY >> 3;
            int nameTableAddress = nameTableBase + ((tileY * planeWidthTiles + tileX) * 2);
            ushort name = ReadVramWord((uint)nameTableAddress);
            return BuildPlanePixelTrace(layer, VdpPlaneSourceKind.Plane, screenX, screenY, planeX, lineY, activeWidth, xOffset, planeWidthTiles, planeHeightTiles, nameTableBase, nameTableAddress, name, scrollX, scrollY, sourceX, sourceY);
        }
        finally
        {
            Array.Copy(_renderSavedRegisters, _registers, _registers.Length);
            _renderVram = null;
        }
    }

    public VdpDiagnostics GetDiagnostics()
    {
        (int planeWidthTiles, int planeHeightTiles) = GetPlaneSize();
        return new VdpDiagnostics(
            GetPlaneANameTableBase(),
            GetPlaneBNameTableBase(),
            GetSpriteAttributeTableBase(),
            planeWidthTiles,
            planeHeightTiles,
            IsH40() ? 40 : 32,
            ScoreNameTable(GetPlaneANameTableBase()),
            ScoreNameTable(GetPlaneBNameTableBase()),
            FindBestFallbackNameTableBase(),
            ScoreNameTable(FindBestFallbackNameTableBase()),
            FindFirstNonzeroTile(),
            CountNonzeroTiles(),
            CountLikelySprites(),
            CountRegisterWrites(2),
            CountRegisterWrites(4),
            CountRegisterWrites(5),
            _controlCommands.Count == 0 ? (byte)0 : _controlCommands[^1].Code,
            _controlCommands.Count == 0 ? 0u : _controlCommands[^1].Address,
            _dmaEvents.Count,
            _directColorSamples.Count,
            _fifoWords,
            _spriteOverflow,
            _spriteCollision);
    }

    public VdpState CaptureState()
    {
        return new VdpState(
            (byte[])_vram.Clone(),
            (ushort[])_cram.Clone(),
            (ushort[])_vsram.Clone(),
            (byte[])_registers.Clone(),
            Status,
            _scanline,
            _hintCounter,
            _fifoWords,
            _oddFrame,
            _vBlank,
            _vInterruptPending,
            _hInterruptPending,
            _hBlank,
            _spriteOverflow,
            _spriteCollision,
            _address,
            _code,
            _directColorSamples.ToArray(),
            DmaCycleDebt);
    }

    public byte[] CaptureVisibleFrameVramSnapshot()
    {
        return (byte[])(_visibleFrameVramCaptured ? _visibleFrameVram : _vram).Clone();
    }

    public void RestoreState(VdpState state)
    {
        Array.Copy(state.Vram, _vram, Math.Min(_vram.Length, state.Vram.Length));
        Array.Copy(state.Cram, _cram, Math.Min(_cram.Length, state.Cram.Length));
        Array.Copy(state.Vsram, _vsram, Math.Min(_vsram.Length, state.Vsram.Length));
        Array.Copy(state.Registers, _registers, Math.Min(_registers.Length, state.Registers.Length));
        Status = state.Status;
        _scanline = state.Scanline;
        _hintCounter = state.HintCounter;
        _fifoWords = state.FifoWords;
        _oddFrame = state.OddFrame;
        _vBlank = state.VBlank;
        _vInterruptPending = state.VInterruptPending;
        _hInterruptPending = state.HInterruptPending;
        _hBlank = state.HBlank;
        _spriteOverflow = state.SpriteOverflow;
        _spriteCollision = state.SpriteCollision;
        _address = state.Address;
        _code = state.Code;
        _directColorSamples.Clear();
        _directColorSamples.AddRange(state.DirectColorSamples);
        DmaCycleDebt = state.DmaCycleDebt;
        UpdateStatusFlags();
    }

    private ushort ReadVramWord(uint address)
    {
        byte[] vram = _renderVram ?? _vram;
        int offset = (int)(address & 0xFFFF);
        return (ushort)((vram[offset] << 8) | vram[(offset + 1) & 0xFFFF]);
    }

    private void WriteVramWord(uint address, ushort value)
    {
        int offset = (int)(address & 0xFFFF);
        _vram[offset] = (byte)(value >> 8);
        _vram[(offset + 1) & 0xFFFF] = (byte)value;
        _vramGeneration++;
    }

    private void IncrementAddress()
    {
        _address = (_address + AutoIncrement) & 0xFFFF;
    }

    private void EnqueueFifoWord()
    {
        _fifoWords = Math.Min(4, _fifoWords + 1);
        UpdateStatusFlags();
    }

    private void DrainFifo(int words)
    {
        _fifoWords = Math.Max(0, _fifoWords - words);
    }

    public int ConsumeDmaCycleDebt(int cycles)
    {
        int consumed = Math.Min(Math.Max(0, cycles), DmaCycleDebt);
        DmaCycleDebt -= consumed;
        return consumed;
    }

    private void UpdateStatusFlags()
    {
        Status = (ushort)(Status & ~(0x03EC));
        if (_fifoWords == 0)
        {
            Status |= 0x0200;
        }
        else if (_fifoWords >= 4)
        {
            Status |= 0x0100;
        }

        if (_vBlank)
        {
            Status |= 0x0008;
        }

        if (_vInterruptPending)
        {
            Status |= 0x0080;
        }

        if (_spriteOverflow)
        {
            Status |= 0x0040;
        }

        if (_spriteCollision)
        {
            Status |= 0x0020;
        }

        if (_hBlank)
        {
            Status |= 0x0004;
        }

        if (_oddFrame)
        {
            Status |= 0x0010;
        }
    }

    private void Trace(string message)
    {
        if (!TraceEnabled || _traceEvents.Count >= TraceLimit)
        {
            return;
        }

        _traceEvents.Add(message);
    }

    private void TraceLimited(ref int count, int limit, string message)
    {
        if (count++ >= limit)
        {
            return;
        }

        Trace(message);
    }

    private static void AddBounded<T>(List<T> list, T value, int limit = 512)
    {
        if (list.Count >= limit)
        {
            list.RemoveAt(0);
        }

        list.Add(value);
    }

    private int RenderCompositedLayers(byte[] framebuffer, FramePixelOrder pixelOrder)
    {
        bool collectRenderPerformance = CollectRenderPerformance;
        RenderPerformanceAccumulator renderPerformance = default;
        Array.Copy(_registers, _renderSavedRegisters, _registers.Length);
        try
        {
            int spriteCount;
            if (collectRenderPerformance)
            {
                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                spriteCount = GatherSprites(_renderSprites);
                AnalyzeSpriteStatus(_renderSprites, spriteCount);
                renderPerformance.SpriteGatherTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
            }
            else
            {
                spriteCount = GatherSprites(_renderSprites);
                AnalyzeSpriteStatus(_renderSprites, spriteCount);
            }

            int drawn = 0;

            for (int y = 0; y < ScreenHeight; y++)
            {
                if (collectRenderPerformance)
                {
                    long start = System.Diagnostics.Stopwatch.GetTimestamp();
                    _renderVram = SelectRenderVramForLine(y);
                    ApplyCapturedRegistersForLine(y);
                    renderPerformance.SnapshotTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
                }
                else
                {
                    _renderVram = SelectRenderVramForLine(y);
                    ApplyCapturedRegistersForLine(y);
                }

                (int planeWidthTiles, int planeHeightTiles) = GetPlaneSize();
                int activeWidth = IsH40() ? 320 : 256;
                int xOffset = IsH40() ? 0 : 32;
                if (collectRenderPerformance)
                {
                    long start = System.Diagnostics.Stopwatch.GetTimestamp();
                    spriteCount = LoadRenderSpritesForLine(y);
                    renderPerformance.SpriteGatherTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;

                    start = System.Diagnostics.Stopwatch.GetTimestamp();
                    BuildRenderPalette(y);
                    renderPerformance.PaletteTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;

                    start = System.Diagnostics.Stopwatch.GetTimestamp();
                    BuildLineVerticalScroll(y);
                    renderPerformance.ScrollTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
                }
                else
                {
                    spriteCount = LoadRenderSpritesForLine(y);
                    BuildRenderPalette(y);
                    BuildLineVerticalScroll(y);
                }

                Rgb background = _renderPalette[_registers[7] & 0x3F];
                int planeAHScroll = GetHorizontalScroll(PlaneKind.PlaneA, y);
                int planeBHScroll = GetHorizontalScroll(PlaneKind.PlaneB, y);
                bool shadowHighlightEnabled = IsShadowHighlightEnabled();
                int evenLineSpriteCount = 0;
                int oddLineSpriteCount = 0;
                int lineSpriteCount = 0;
                int evenY = y * 2;
                int oddY = evenY + 1;
                if (IsInterlaceDoubleResolution())
                {
                    if (collectRenderPerformance)
                    {
                        long start = System.Diagnostics.Stopwatch.GetTimestamp();
                        evenLineSpriteCount = GatherLineSprites(_renderSprites, spriteCount, evenY, _renderEvenLineSprites);
                        oddLineSpriteCount = GatherLineSprites(_renderSprites, spriteCount, oddY, _renderOddLineSprites);
                        renderPerformance.SpriteGatherTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
                    }
                    else
                    {
                        evenLineSpriteCount = GatherLineSprites(_renderSprites, spriteCount, evenY, _renderEvenLineSprites);
                        oddLineSpriteCount = GatherLineSprites(_renderSprites, spriteCount, oddY, _renderOddLineSprites);
                    }
                }
                else
                {
                    if (collectRenderPerformance)
                    {
                        long start = System.Diagnostics.Stopwatch.GetTimestamp();
                        lineSpriteCount = GatherLineSprites(_renderSprites, spriteCount, y, _renderLineSprites);
                        renderPerformance.SpriteGatherTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
                    }
                    else
                    {
                        lineSpriteCount = GatherLineSprites(_renderSprites, spriteCount, y, _renderLineSprites);
                    }
                }

                if (xOffset > 0)
                {
                    if (collectRenderPerformance)
                    {
                        long start = System.Diagnostics.Stopwatch.GetTimestamp();
                        FillLineSegment(framebuffer, y, 0, xOffset, background, pixelOrder);
                        FillLineSegment(framebuffer, y, xOffset + activeWidth, ScreenWidth - (xOffset + activeWidth), background, pixelOrder);
                        renderPerformance.BorderTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
                    }
                    else
                    {
                        FillLineSegment(framebuffer, y, 0, xOffset, background, pixelOrder);
                        FillLineSegment(framebuffer, y, xOffset + activeWidth, ScreenWidth - (xOffset + activeWidth), background, pixelOrder);
                    }
                }

                int pixelOffset = ((y * ScreenWidth) + xOffset) * 3;
                if (IsInterlaceDoubleResolution())
                {
                    long compositingStart = collectRenderPerformance ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                    for (int x = 0; x < activeWidth; x++)
                    {
                        Rgb even = ReadCompositedColor(x, y, evenY, planeWidthTiles, planeHeightTiles, planeAHScroll, planeBHScroll, _renderEvenLineSprites, evenLineSpriteCount, background, shadowHighlightEnabled, out bool evenVisible);
                        WriteColor(framebuffer, pixelOffset, even, pixelOrder);
                        _lastFrameOpaquePixels[(y * ScreenWidth) + xOffset + x] = evenVisible;
                        _lastFramePriorityPixels[(y * ScreenWidth) + xOffset + x] = false;
                        pixelOffset += 3;
                        drawn += evenVisible ? 1 : 0;
                    }

                    if (collectRenderPerformance)
                    {
                        renderPerformance.CompositingTicks += System.Diagnostics.Stopwatch.GetTimestamp() - compositingStart;
                    }

                    continue;
                }

                if (collectRenderPerformance)
                {
                    long start = System.Diagnostics.Stopwatch.GetTimestamp();
                    RenderPlaneLinePacked(_linePlaneBPacked, activeWidth, GetPlaneBNameTableBase(), y, y, planeWidthTiles, planeHeightTiles, PlaneKind.PlaneB, planeBHScroll);
                    renderPerformance.PlaneBTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;

                    start = System.Diagnostics.Stopwatch.GetTimestamp();
                    RenderPlaneOrWindowLinePacked(_linePlaneAPacked, activeWidth, y, y, planeWidthTiles, planeHeightTiles, planeAHScroll);
                    renderPerformance.PlaneAWindowTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
                }
                else
                {
                    RenderPlaneLinePacked(_linePlaneBPacked, activeWidth, GetPlaneBNameTableBase(), y, y, planeWidthTiles, planeHeightTiles, PlaneKind.PlaneB, planeBHScroll);
                    RenderPlaneOrWindowLinePacked(_linePlaneAPacked, activeWidth, y, y, planeWidthTiles, planeHeightTiles, planeAHScroll);
                }

                if (_lineSpritePixelSnapshotCaptured[y])
                {
                    if (collectRenderPerformance)
                    {
                        long start = System.Diagnostics.Stopwatch.GetTimestamp();
                        Array.Copy(_lineSpritePackedPixelSnapshots, y * ScreenWidth, _lineSpritesPacked, 0, ScreenWidth);
                        renderPerformance.SpriteRenderTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
                    }
                    else
                    {
                        Array.Copy(_lineSpritePackedPixelSnapshots, y * ScreenWidth, _lineSpritesPacked, 0, ScreenWidth);
                    }
                }
                else
                {
                    if (collectRenderPerformance)
                    {
                        long start = System.Diagnostics.Stopwatch.GetTimestamp();
                        RenderSpriteLinePacked(_lineSpritesPacked, activeWidth, y, y, _renderLineSprites, lineSpriteCount);
                        renderPerformance.SpriteRenderTicks += System.Diagnostics.Stopwatch.GetTimestamp() - start;
                    }
                    else
                    {
                        RenderSpriteLinePacked(_lineSpritesPacked, activeWidth, y, y, _renderLineSprites, lineSpriteCount);
                    }
                }

                long lineCompositingStart = collectRenderPerformance ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                for (int x = 0; x < activeWidth; x++)
                {
                    if (!shadowHighlightEnabled)
                    {
                        uint planeB = _linePlaneBPacked[x];
                        uint planeA = _linePlaneAPacked[x];
                        uint fastSprite = _lineSpritesPacked[x];
                        uint fastPixel = 0;

                        if ((planeB & (PackedVisibleBit | PackedPriorityBit)) == PackedVisibleBit)
                        {
                            fastPixel = planeB;
                        }

                        if ((planeA & (PackedVisibleBit | PackedPriorityBit)) == PackedVisibleBit)
                        {
                            fastPixel = planeA;
                        }

                        if ((fastSprite & (PackedVisibleBit | PackedPriorityBit)) == PackedVisibleBit)
                        {
                            fastPixel = fastSprite;
                        }

                        if ((planeB & (PackedVisibleBit | PackedPriorityBit)) == (PackedVisibleBit | PackedPriorityBit))
                        {
                            fastPixel = planeB;
                        }

                        if ((planeA & (PackedVisibleBit | PackedPriorityBit)) == (PackedVisibleBit | PackedPriorityBit))
                        {
                            fastPixel = planeA;
                        }

                        if ((fastSprite & (PackedVisibleBit | PackedPriorityBit)) == (PackedVisibleBit | PackedPriorityBit))
                        {
                            fastPixel = fastSprite;
                        }

                        Rgb fastColor = (fastPixel & PackedVisibleBit) != 0 ? _renderPalette[(fastPixel >> PackedPaletteShift) & 0x3F] : background;
                        WriteColor(framebuffer, pixelOffset, fastColor, pixelOrder);
                        _lastFrameOpaquePixels[(y * ScreenWidth) + xOffset + x] = (fastPixel & PackedVisibleBit) != 0;
                        _lastFramePriorityPixels[(y * ScreenWidth) + xOffset + x] = (fastPixel & (PackedVisibleBit | PackedPriorityBit)) == (PackedVisibleBit | PackedPriorityBit);
                        pixelOffset += 3;
                        drawn += (fastPixel & PackedVisibleBit) != 0 ? 1 : 0;
                        continue;
                    }

                    uint planeBPixel = _linePlaneBPacked[x];
                    uint planeAPixel = _linePlaneAPacked[x];
                    uint sprite = _lineSpritesPacked[x];
                    bool spriteEffect = IsShadowHighlightEffectPacked(sprite, shadowHighlightEnabled);
                    uint pixel = 0;

                    ApplyPackedLayer(ref pixel, planeBPixel, highPriorityPass: false);
                    ApplyPackedLayer(ref pixel, planeAPixel, highPriorityPass: false);
                    if (!spriteEffect)
                    {
                        ApplyPackedLayer(ref pixel, sprite, highPriorityPass: false);
                    }

                    ApplyPackedLayer(ref pixel, planeBPixel, highPriorityPass: true);
                    ApplyPackedLayer(ref pixel, planeAPixel, highPriorityPass: true);
                    if (!spriteEffect)
                    {
                        ApplyPackedLayer(ref pixel, sprite, highPriorityPass: true);
                    }

                    ShadowHighlightShade shade = ResolveShadowHighlightShadePacked(planeAPixel, planeBPixel, sprite, spriteEffect, shadowHighlightEnabled);
                    Rgb color = PackedVisible(pixel) ? ApplyShade(_renderPalette[PackedPaletteIndex(pixel)], shade) : ApplyShade(background, shade);
                    WriteColor(framebuffer, pixelOffset, color, pixelOrder);
                    _lastFrameOpaquePixels[(y * ScreenWidth) + xOffset + x] = PackedVisible(pixel);
                    _lastFramePriorityPixels[(y * ScreenWidth) + xOffset + x] = PackedVisible(pixel) && PackedPriority(pixel);
                    pixelOffset += 3;
                    drawn += PackedVisible(pixel) ? 1 : 0;
                }

                if (collectRenderPerformance)
                {
                    renderPerformance.CompositingTicks += System.Diagnostics.Stopwatch.GetTimestamp() - lineCompositingStart;
                }
            }

            if (collectRenderPerformance)
            {
                LastRenderPerformance = renderPerformance.ToCounters();
            }

            return drawn;
        }
        finally
        {
            Array.Copy(_renderSavedRegisters, _registers, _registers.Length);
        }
    }

    private void RenderDebugLayerRgbInto(byte[] framebuffer, VdpDebugLayer layer)
    {
        if (framebuffer.Length != ScreenWidth * ScreenHeight * 3)
        {
            throw new ArgumentException("framebuffer must be 320x224 RGB", nameof(framebuffer));
        }

        Array.Clear(framebuffer);
        if (!IsDisplayEnabled())
        {
            return;
        }

        _renderVram = _visibleFrameVramCaptured ? _visibleFrameVram : _vram;
        Array.Copy(_registers, _renderSavedRegisters, _registers.Length);
        try
        {
            int spriteCount = GatherSprites(_renderSprites);
            for (int y = 0; y < ScreenHeight; y++)
            {
                _renderVram = SelectRenderVramForLine(y);
                ApplyCapturedRegistersForLine(y);
                (int planeWidthTiles, int planeHeightTiles) = GetPlaneSize();
                int activeWidth = IsH40() ? 320 : 256;
                int xOffset = IsH40() ? 0 : 32;
                BuildRenderPalette(y);
                BuildLineVerticalScroll(y);
                int planeAHScroll = GetHorizontalScroll(PlaneKind.PlaneA, y);
                int planeBHScroll = GetHorizontalScroll(PlaneKind.PlaneB, y);
                LayerPixel[] line = layer switch
                {
                    VdpDebugLayer.PlaneA => _linePlaneA,
                    VdpDebugLayer.PlaneB => _linePlaneB,
                    _ => _lineSprites,
                };

                if (layer == VdpDebugLayer.PlaneA)
                {
                    RenderPlaneOrWindowLine(line, activeWidth, y, y, planeWidthTiles, planeHeightTiles, planeAHScroll);
                }
                else if (layer == VdpDebugLayer.PlaneB)
                {
                    RenderPlaneLine(line, activeWidth, GetPlaneBNameTableBase(), y, y, planeWidthTiles, planeHeightTiles, PlaneKind.PlaneB, planeBHScroll);
                }
                else
                {
                    spriteCount = LoadRenderSpritesForLine(y);
                    int lineSpriteCount = GatherLineSprites(_renderSprites, spriteCount, y, _renderLineSprites);
                    RenderSpriteLine(line, activeWidth, y, y, _renderLineSprites, lineSpriteCount);
                }

                int offset = ((y * ScreenWidth) + xOffset) * 3;
                for (int x = 0; x < activeWidth; x++)
                {
                    Rgb color = line[x].Visible ? DecodePixelColor(line[x], ShadowHighlightShade.Normal) : default;
                    WriteColor(framebuffer, offset, color, FramePixelOrder.Rgb);
                    offset += 3;
                }
            }
        }
        finally
        {
            Array.Copy(_renderSavedRegisters, _registers, _registers.Length);
            _renderVram = null;
        }
    }

    private Rgb ReadCompositedColor(
        int planeX,
        int lineY,
        int sourceY,
        int planeWidthTiles,
        int planeHeightTiles,
        int planeAHScroll,
        int planeBHScroll,
        LineSprite[] lineSprites,
        int lineSpriteCount,
        Rgb background,
        bool shadowHighlightEnabled,
        out bool visible)
    {
        LayerPixel pixel = default;
        LayerPixel planeB = ReadPlanePixel(GetPlaneBNameTableBase(), planeX, lineY, sourceY, planeWidthTiles, planeHeightTiles, PlaneKind.PlaneB, planeBHScroll);
        LayerPixel planeA = ReadPlaneOrWindowPixel(planeX, lineY, sourceY, planeWidthTiles, planeHeightTiles, planeAHScroll);
        LayerPixel sprite = ReadSpritePixel(lineSprites, lineSpriteCount, planeX, lineY, sourceY);
        bool spriteEffect = IsShadowHighlightEffect(sprite, shadowHighlightEnabled);

        ApplyLayer(ref pixel, planeB, highPriorityPass: false);
        ApplyLayer(ref pixel, planeA, highPriorityPass: false);
        if (!spriteEffect)
        {
            ApplyLayer(ref pixel, sprite, highPriorityPass: false);
        }

        ApplyLayer(ref pixel, planeB, highPriorityPass: true);
        ApplyLayer(ref pixel, planeA, highPriorityPass: true);
        if (!spriteEffect)
        {
            ApplyLayer(ref pixel, sprite, highPriorityPass: true);
        }

        visible = pixel.Visible;
        ShadowHighlightShade shade = ResolveShadowHighlightShade(planeA, planeB, sprite, spriteEffect, shadowHighlightEnabled);
        return pixel.Visible ? DecodePixelColor(pixel, shade) : ApplyShade(background, shade);
    }

    private int LoadRenderSpritesForLine(int lineY)
    {
        int row = Math.Clamp(lineY, 0, ScreenHeight - 1);
        if (!_lineSpriteSnapshotCaptured[row])
        {
            return GatherSprites(_renderSprites);
        }

        int count = _lineSpriteSnapshotCounts[row];
        Array.Copy(_lineSpriteSnapshots, row * MaxSprites, _renderSprites, 0, count);
        return count;
    }

    private void ApplyCapturedRegistersForLine(int lineY)
    {
        int row = Math.Clamp(lineY, 0, ScreenHeight - 1);
        if (_lineRegistersCaptured[row])
        {
            Array.Copy(_lineRegisters, row * 32, _registers, 0, _registers.Length);
        }
    }

    private static void ApplyLayer(ref LayerPixel output, LayerPixel candidate, bool highPriorityPass)
    {
        if (!candidate.Visible || candidate.Priority != highPriorityPass)
        {
            return;
        }

        output = candidate;
    }

    private const uint PackedVisibleBit = 0x0001;
    private const uint PackedPriorityBit = 0x0002;
    private const uint PackedSpriteBit = 0x0004;
    private const int PackedPaletteShift = 4;
    private const int PackedColorShift = 10;

    private static uint PackPixel(bool visible, int paletteIndex, bool priority, int colorIndex, bool sprite)
    {
        uint value = ((uint)(paletteIndex & 0x3F) << PackedPaletteShift) | ((uint)(colorIndex & 0x0F) << PackedColorShift);
        if (visible)
        {
            value |= PackedVisibleBit;
        }

        if (priority)
        {
            value |= PackedPriorityBit;
        }

        if (sprite)
        {
            value |= PackedSpriteBit;
        }

        return value;
    }

    private static bool PackedVisible(uint pixel)
    {
        return (pixel & PackedVisibleBit) != 0;
    }

    private static bool PackedPriority(uint pixel)
    {
        return (pixel & PackedPriorityBit) != 0;
    }

    private static bool PackedSprite(uint pixel)
    {
        return (pixel & PackedSpriteBit) != 0;
    }

    private static int PackedPaletteIndex(uint pixel)
    {
        return (int)((pixel >> PackedPaletteShift) & 0x3F);
    }

    private static int PackedColorIndex(uint pixel)
    {
        return (int)((pixel >> PackedColorShift) & 0x0F);
    }

    private static void ApplyPackedLayer(ref uint output, uint candidate, bool highPriorityPass)
    {
        if (!PackedVisible(candidate) || PackedPriority(candidate) != highPriorityPass)
        {
            return;
        }

        output = candidate;
    }

    private LayerPixel ReadPlaneOrWindowPixel(int screenX, int lineY, int screenY, int planeWidthTiles, int planeHeightTiles, int planeAHScroll)
    {
        if (IsWindowPixel(screenX, lineY))
        {
            return ReadWindowPixel(screenX, screenY);
        }

        return ReadPlanePixel(GetPlaneANameTableBase(), screenX, lineY, screenY, planeWidthTiles, planeHeightTiles, PlaneKind.PlaneA, planeAHScroll);
    }

    private void RenderPlaneLine(LayerPixel[] output, int activeWidth, int nameTableBase, int lineY, int screenY, int planeWidthTiles, int planeHeightTiles, PlaneKind plane, int scrollX)
    {
        Array.Clear(output, 0, activeWidth);
        int tileHeight = TileHeightPixels();
        int sourceXMask = (planeWidthTiles * 8) - 1;
        int sourceYMask = (planeHeightTiles * tileHeight) - 1;
        bool verticalScrollPerColumn = (_registers[11] & 0x04) != 0;

        for (int x = 0; x < activeWidth;)
        {
            int scrollY = GetVerticalScroll(plane, x, lineY);
            int sourceX = (x - scrollX) & sourceXMask;
            int sourceY = (screenY + scrollY) & sourceYMask;
            int tileX = sourceX >> 3;
            int tileY = tileHeight == 16 ? sourceY >> 4 : sourceY >> 3;
            ushort name = ReadVramWord((uint)(nameTableBase + ((tileY * planeWidthTiles + tileX) * 2)));
            int pixelX = sourceX & 7;
            int pixelY = sourceY & (tileHeight - 1);
            int count = Math.Min(activeWidth - x, 8 - pixelX);
            if (verticalScrollPerColumn)
            {
                count = Math.Min(count, 16 - (x & 15));
            }

            DrawTileSpan(output, x, count, name, pixelX, pixelY);
            x += count;
        }
    }

    private void RenderPlaneLinePacked(uint[] output, int activeWidth, int nameTableBase, int lineY, int screenY, int planeWidthTiles, int planeHeightTiles, PlaneKind plane, int scrollX)
    {
        Array.Clear(output, 0, activeWidth);
        int tileHeight = TileHeightPixels();
        int sourceXMask = (planeWidthTiles * 8) - 1;
        int sourceYMask = (planeHeightTiles * tileHeight) - 1;
        bool verticalScrollPerColumn = (_registers[11] & 0x04) != 0;

        for (int x = 0; x < activeWidth;)
        {
            int scrollY = GetVerticalScroll(plane, x, lineY);
            int sourceX = (x - scrollX) & sourceXMask;
            int sourceY = (screenY + scrollY) & sourceYMask;
            int tileX = sourceX >> 3;
            int tileY = tileHeight == 16 ? sourceY >> 4 : sourceY >> 3;
            ushort name = ReadVramWord((uint)(nameTableBase + ((tileY * planeWidthTiles + tileX) * 2)));
            int pixelX = sourceX & 7;
            int pixelY = sourceY & (tileHeight - 1);
            int count = Math.Min(activeWidth - x, 8 - pixelX);
            if (verticalScrollPerColumn)
            {
                count = Math.Min(count, 16 - (x & 15));
            }

            DrawTileSpanPacked(output, x, count, name, pixelX, pixelY);
            x += count;
        }
    }

    private void RenderPlaneOrWindowLine(LayerPixel[] output, int activeWidth, int lineY, int screenY, int planeWidthTiles, int planeHeightTiles, int planeAHScroll)
    {
        Array.Clear(output, 0, activeWidth);
        WindowLineInfo window = BuildWindowLineInfo(lineY, activeWidth);
        if (window.VerticalActive)
        {
            RenderWindowSegment(output, 0, activeWidth, screenY);
            return;
        }

        if (!window.HorizontalActive)
        {
            RenderPlaneLine(output, activeWidth, GetPlaneANameTableBase(), lineY, screenY, planeWidthTiles, planeHeightTiles, PlaneKind.PlaneA, planeAHScroll);
            return;
        }

        if (window.HorizontalFromRight)
        {
            int boundary = Math.Clamp(window.HorizontalBoundary, 0, activeWidth);
            RenderPlaneLineSegment(output, 0, boundary, GetPlaneANameTableBase(), lineY, screenY, planeWidthTiles, planeHeightTiles, PlaneKind.PlaneA, planeAHScroll);
            RenderWindowSegment(output, boundary, activeWidth - boundary, screenY);
        }
        else
        {
            int boundary = Math.Clamp(window.HorizontalBoundary, 0, activeWidth);
            RenderWindowSegment(output, 0, boundary, screenY);
            RenderPlaneLineSegment(output, boundary, activeWidth - boundary, GetPlaneANameTableBase(), lineY, screenY, planeWidthTiles, planeHeightTiles, PlaneKind.PlaneA, planeAHScroll);
        }
    }

    private void RenderPlaneOrWindowLinePacked(uint[] output, int activeWidth, int lineY, int screenY, int planeWidthTiles, int planeHeightTiles, int planeAHScroll)
    {
        Array.Clear(output, 0, activeWidth);
        WindowLineInfo window = BuildWindowLineInfo(lineY, activeWidth);
        if (window.VerticalActive)
        {
            RenderWindowSegmentPacked(output, 0, activeWidth, screenY);
            return;
        }

        if (!window.HorizontalActive)
        {
            RenderPlaneLinePacked(output, activeWidth, GetPlaneANameTableBase(), lineY, screenY, planeWidthTiles, planeHeightTiles, PlaneKind.PlaneA, planeAHScroll);
            return;
        }

        if (window.HorizontalFromRight)
        {
            int boundary = Math.Clamp(window.HorizontalBoundary, 0, activeWidth);
            RenderPlaneLineSegmentPacked(output, 0, boundary, GetPlaneANameTableBase(), lineY, screenY, planeWidthTiles, planeHeightTiles, PlaneKind.PlaneA, planeAHScroll);
            RenderWindowSegmentPacked(output, boundary, activeWidth - boundary, screenY);
        }
        else
        {
            int boundary = Math.Clamp(window.HorizontalBoundary, 0, activeWidth);
            RenderWindowSegmentPacked(output, 0, boundary, screenY);
            RenderPlaneLineSegmentPacked(output, boundary, activeWidth - boundary, GetPlaneANameTableBase(), lineY, screenY, planeWidthTiles, planeHeightTiles, PlaneKind.PlaneA, planeAHScroll);
        }
    }

    private void RenderPlaneLineSegment(LayerPixel[] output, int startX, int width, int nameTableBase, int lineY, int screenY, int planeWidthTiles, int planeHeightTiles, PlaneKind plane, int scrollX)
    {
        int endX = startX + width;
        int tileHeight = TileHeightPixels();
        int sourceXMask = (planeWidthTiles * 8) - 1;
        int sourceYMask = (planeHeightTiles * tileHeight) - 1;
        bool verticalScrollPerColumn = (_registers[11] & 0x04) != 0;

        for (int x = startX; x < endX;)
        {
            int scrollY = GetVerticalScroll(plane, x, lineY);
            int sourceX = (x - scrollX) & sourceXMask;
            int sourceY = (screenY + scrollY) & sourceYMask;
            int tileX = sourceX >> 3;
            int tileY = tileHeight == 16 ? sourceY >> 4 : sourceY >> 3;
            ushort name = ReadVramWord((uint)(nameTableBase + ((tileY * planeWidthTiles + tileX) * 2)));
            int pixelX = sourceX & 7;
            int pixelY = sourceY & (tileHeight - 1);
            int count = Math.Min(endX - x, 8 - pixelX);
            if (verticalScrollPerColumn)
            {
                count = Math.Min(count, 16 - (x & 15));
            }

            DrawTileSpan(output, x, count, name, pixelX, pixelY);
            x += count;
        }
    }

    private void RenderPlaneLineSegmentPacked(uint[] output, int startX, int width, int nameTableBase, int lineY, int screenY, int planeWidthTiles, int planeHeightTiles, PlaneKind plane, int scrollX)
    {
        int endX = startX + width;
        int tileHeight = TileHeightPixels();
        int sourceXMask = (planeWidthTiles * 8) - 1;
        int sourceYMask = (planeHeightTiles * tileHeight) - 1;
        bool verticalScrollPerColumn = (_registers[11] & 0x04) != 0;

        for (int x = startX; x < endX;)
        {
            int scrollY = GetVerticalScroll(plane, x, lineY);
            int sourceX = (x - scrollX) & sourceXMask;
            int sourceY = (screenY + scrollY) & sourceYMask;
            int tileX = sourceX >> 3;
            int tileY = tileHeight == 16 ? sourceY >> 4 : sourceY >> 3;
            ushort name = ReadVramWord((uint)(nameTableBase + ((tileY * planeWidthTiles + tileX) * 2)));
            int pixelX = sourceX & 7;
            int pixelY = sourceY & (tileHeight - 1);
            int count = Math.Min(endX - x, 8 - pixelX);
            if (verticalScrollPerColumn)
            {
                count = Math.Min(count, 16 - (x & 15));
            }

            DrawTileSpanPacked(output, x, count, name, pixelX, pixelY);
            x += count;
        }
    }

    private void RenderWindowSegment(LayerPixel[] output, int startX, int width, int screenY)
    {
        int endX = startX + width;
        int activeCells = IsH40() ? 40 : 32;
        int tableCells = IsH40() ? 64 : 32;
        int tileHeight = TileHeightPixels();
        int cellY = Math.Clamp(tileHeight == 16 ? screenY >> 4 : screenY >> 3, 0, 31);
        int pixelY = screenY & (tileHeight - 1);
        int nameTableBase = GetWindowNameTableBase();

        for (int x = startX; x < endX;)
        {
            int cellX = Math.Clamp(x >> 3, 0, activeCells - 1);
            ushort name = ReadVramWord((uint)(nameTableBase + ((cellY * tableCells + cellX) * 2)));
            int pixelX = x & 7;
            int count = Math.Min(endX - x, 8 - pixelX);
            DrawTileSpan(output, x, count, name, pixelX, pixelY);
            x += count;
        }
    }

    private void RenderWindowSegmentPacked(uint[] output, int startX, int width, int screenY)
    {
        int endX = startX + width;
        int activeCells = IsH40() ? 40 : 32;
        int tableCells = IsH40() ? 64 : 32;
        int tileHeight = TileHeightPixels();
        int cellY = Math.Clamp(tileHeight == 16 ? screenY >> 4 : screenY >> 3, 0, 31);
        int pixelY = screenY & (tileHeight - 1);
        int nameTableBase = GetWindowNameTableBase();

        for (int x = startX; x < endX;)
        {
            int cellX = Math.Clamp(x >> 3, 0, activeCells - 1);
            ushort name = ReadVramWord((uint)(nameTableBase + ((cellY * tableCells + cellX) * 2)));
            int pixelX = x & 7;
            int count = Math.Min(endX - x, 8 - pixelX);
            DrawTileSpanPacked(output, x, count, name, pixelX, pixelY);
            x += count;
        }
    }

    private void DrawTileSpan(LayerPixel[] output, int startX, int count, ushort name, int pixelX, int pixelY)
    {
        int palette = (name >> 13) & 0x03;
        bool priority = (name & 0x8000) != 0;
        for (int i = 0; i < count; i++)
        {
            int colorIndex = ReadTilePixel(name, pixelX + i, pixelY);
            output[startX + i] = new LayerPixel(colorIndex != 0, ((palette * 16) + colorIndex) & 0x3F, priority, colorIndex, false);
        }
    }

    private void DrawTileSpanPacked(uint[] output, int startX, int count, ushort name, int pixelX, int pixelY)
    {
        int palette = (name >> 13) & 0x03;
        int paletteBase = palette * 16;
        uint priorityBit = (name & 0x8000) != 0 ? PackedPriorityBit : 0;
        for (int i = 0; i < count; i++)
        {
            int colorIndex = ReadTilePixel(name, pixelX + i, pixelY);
            output[startX + i] = priorityBit
                | (colorIndex != 0 ? PackedVisibleBit : 0)
                | ((uint)((paletteBase + colorIndex) & 0x3F) << PackedPaletteShift)
                | ((uint)colorIndex << PackedColorShift);
        }
    }

    private void RenderSpriteLine(LayerPixel[] output, int activeWidth, int lineY, int sourceY, LineSprite[] lineSprites, int lineSpriteCount)
    {
        Array.Clear(output, 0, activeWidth);
        for (int i = 0; i < lineSpriteCount; i++)
        {
            LineSprite lineSprite = lineSprites[i];
            SpriteInstance sprite = lineSprite.Sprite;
            int startX = Math.Max(0, sprite.X);
            int endX = Math.Min(activeWidth, sprite.X + lineSprite.VisiblePixels);
            for (int screenX = startX; screenX < endX; screenX++)
            {
                if (output[screenX].Visible)
                {
                    continue;
                }

                output[screenX] = ReadSingleSpritePixel(sprite, screenX, lineY, sourceY);
            }
        }
    }

    private void RenderSpriteLinePacked(uint[] output, int activeWidth, int lineY, int sourceY, LineSprite[] lineSprites, int lineSpriteCount)
    {
        Array.Clear(output, 0, activeWidth);
        for (int i = 0; i < lineSpriteCount; i++)
        {
            LineSprite lineSprite = lineSprites[i];
            SpriteInstance sprite = lineSprite.Sprite;
            int startX = Math.Max(0, sprite.X);
            int endX = Math.Min(activeWidth, sprite.X + lineSprite.VisiblePixels);
            for (int screenX = startX; screenX < endX; screenX++)
            {
                if (PackedVisible(output[screenX]))
                {
                    continue;
                }

                output[screenX] = ReadSingleSpritePackedPixel(sprite, screenX, lineY, sourceY);
            }
        }
    }

    private LayerPixel ReadPlanePixel(int nameTableBase, int screenX, int lineY, int screenY, int planeWidthTiles, int planeHeightTiles, PlaneKind plane, int scrollX)
    {
        int scrollY = GetVerticalScroll(plane, screenX, lineY);
        int tileHeight = TileHeightPixels();
        int sourceX = (screenX - scrollX) & ((planeWidthTiles * 8) - 1);
        int sourceY = (screenY + scrollY) & ((planeHeightTiles * tileHeight) - 1);
        int tileX = sourceX >> 3;
        int tileY = tileHeight == 16 ? sourceY >> 4 : sourceY >> 3;
        ushort name = ReadVramWord((uint)(nameTableBase + ((tileY * planeWidthTiles + tileX) * 2)));
        return ReadTileLayerPixel(name, sourceX & 7, sourceY & (tileHeight - 1));
    }

    private LayerPixel ReadWindowPixel(int screenX, int screenY)
    {
        int activeCells = IsH40() ? 40 : 32;
        int tableCells = IsH40() ? 64 : 32;
        int tileHeight = TileHeightPixels();
        int cellX = Math.Clamp(screenX / 8, 0, activeCells - 1);
        int cellY = Math.Clamp(tileHeight == 16 ? screenY >> 4 : screenY >> 3, 0, 31);
        ushort name = ReadVramWord((uint)(GetWindowNameTableBase() + ((cellY * tableCells + cellX) * 2)));
        return ReadTileLayerPixel(name, screenX & 7, screenY & (tileHeight - 1));
    }

    private bool IsWindowActiveAt(int planeX, int lineY, int activeWidth)
    {
        WindowLineInfo window = BuildWindowLineInfo(lineY, activeWidth);
        if (window.VerticalActive)
        {
            return true;
        }

        if (!window.HorizontalActive)
        {
            return false;
        }

        int boundary = Math.Clamp(window.HorizontalBoundary, 0, activeWidth);
        return window.HorizontalFromRight ? planeX >= boundary : planeX < boundary;
    }

    private VdpPlanePixelTrace TraceWindowPixel(VdpDebugLayer layer, int screenX, int screenY, int planeX, int lineY, int activeWidth, int xOffset, int planeWidthTiles, int planeHeightTiles)
    {
        int activeCells = IsH40() ? 40 : 32;
        int tableCells = IsH40() ? 64 : 32;
        int tileHeight = TileHeightPixels();
        int cellX = Math.Clamp(planeX >> 3, 0, activeCells - 1);
        int cellY = Math.Clamp(tileHeight == 16 ? lineY >> 4 : lineY >> 3, 0, 31);
        int nameTableBase = GetWindowNameTableBase();
        int nameTableAddress = nameTableBase + ((cellY * tableCells + cellX) * 2);
        ushort name = ReadVramWord((uint)nameTableAddress);
        return BuildPlanePixelTrace(layer, VdpPlaneSourceKind.Window, screenX, screenY, planeX, lineY, activeWidth, xOffset, planeWidthTiles, planeHeightTiles, nameTableBase, nameTableAddress, name, 0, 0, planeX, lineY);
    }

    private VdpPlanePixelTrace BuildPlanePixelTrace(
        VdpDebugLayer layer,
        VdpPlaneSourceKind sourceKind,
        int screenX,
        int screenY,
        int planeX,
        int lineY,
        int activeWidth,
        int xOffset,
        int planeWidthTiles,
        int planeHeightTiles,
        int nameTableBase,
        int nameTableAddress,
        ushort name,
        int scrollX,
        int scrollY,
        int sourceX,
        int sourceY)
    {
        int tileHeight = TileHeightPixels();
        int pixelX = sourceX & 7;
        int pixelY = sourceY & (tileHeight - 1);
        int tileIndex = name & 0x07FF;
        bool hflip = (name & 0x0800) != 0;
        bool vflip = (name & 0x1000) != 0;
        int effectivePixelX = hflip ? 7 - pixelX : pixelX;
        int effectivePixelY = vflip ? tileHeight - 1 - pixelY : pixelY;
        int tileAddress = (tileIndex * TileBytes()) + (effectivePixelY * 4) + (effectivePixelX / 2);
        byte[] vram = _renderVram ?? _vram;
        byte packed = vram[tileAddress & 0xFFFF];
        int colorIndex = (effectivePixelX & 1) == 0 ? packed >> 4 : packed & 0x0F;
        int palette = (name >> 13) & 0x03;
        bool priority = (name & 0x8000) != 0;
        return new VdpPlanePixelTrace(
            layer,
            sourceKind,
            screenX,
            screenY,
            planeX,
            lineY,
            activeWidth,
            xOffset,
            planeWidthTiles,
            planeHeightTiles,
            scrollX,
            scrollY,
            sourceX,
            sourceY,
            nameTableBase,
            nameTableAddress & 0xFFFF,
            name,
            tileIndex,
            tileAddress & 0xFFFF,
            pixelX,
            pixelY,
            effectivePixelX,
            effectivePixelY,
            palette,
            priority,
            colorIndex,
            packed);
    }

    private LayerPixel ReadTileLayerPixel(ushort name, int pixelX, int pixelY)
    {
        int colorIndex = ReadTilePixel(name, pixelX, pixelY);
        int palette = (name >> 13) & 0x03;
        return new LayerPixel(colorIndex != 0, ((palette * 16) + colorIndex) & 0x3F, (name & 0x8000) != 0, colorIndex, false);
    }

    private int RenderPlaneFallback(byte[] framebuffer, int nameTableBase, FramePixelOrder pixelOrder = FramePixelOrder.Rgb)
    {
        (int planeWidthTiles, int planeHeightTiles) = GetPlaneSize();
        int activeWidth = IsH40() ? 320 : 256;
        int xOffset = IsH40() ? 0 : 32;
        int tileHeight = TileHeightPixels();
        int drawn = 0;

        for (int y = 0; y < ScreenHeight; y++)
        {
            int sourceY = GetRenderSourceY(y);
            int tileY = (sourceY / tileHeight) % planeHeightTiles;
            int pixelY = sourceY % tileHeight;

            for (int x = 0; x < activeWidth; x++)
            {
                int tileX = (x / 8) % planeWidthTiles;
                int pixelX = x & 7;
                ushort name = ReadVramWord((uint)(nameTableBase + ((tileY * planeWidthTiles + tileX) * 2)));
                int colorIndex = ReadTilePixel(name, pixelX, pixelY);
                if (colorIndex == 0)
                {
                    continue;
                }

                int palette = (name >> 13) & 0x3;
                WritePixel(framebuffer, x + xOffset, y, DecodeColor((palette * 16) + colorIndex, y), pixelOrder);
                drawn++;
            }
        }

        return drawn;
    }

    private int GatherSprites(SpriteInstance[] sprites)
    {
        int tableBase = GetSpriteAttributeTableBase();
        int maxSprites = IsH40() ? 80 : 64;
        int spriteIndex = 0;
        int count = 0;
        _spriteVisitStamp++;
        if (_spriteVisitStamp == 0)
        {
            Array.Clear(_spriteVisitMarks);
            _spriteVisitStamp = 1;
        }

        for (int spriteCount = 0; spriteCount < maxSprites; spriteCount++)
        {
            if ((uint)spriteIndex >= maxSprites || _spriteVisitMarks[spriteIndex] == _spriteVisitStamp)
            {
                break;
            }

            _spriteVisitMarks[spriteIndex] = _spriteVisitStamp;
            int entry = tableBase + (spriteIndex * 8);
            ushort rawY = ReadVramWord((uint)entry);
            ushort sizeLink = ReadVramWord((uint)(entry + 2));
            ushort attributes = ReadVramWord((uint)(entry + 4));
            ushort rawX = ReadVramWord((uint)(entry + 6));

            int widthTiles = ((sizeLink >> 10) & 0x03) + 1;
            int heightTiles = ((sizeLink >> 8) & 0x03) + 1;
            int x = (rawX & 0x01FF) - 128;
            bool interlaceDouble = IsInterlaceDoubleResolution();
            int yOrigin = interlaceDouble ? 256 : 128;
            int yMask = interlaceDouble ? 0x03FF : 0x01FF;
            int y = (rawY & yMask) - yOrigin;
            bool mask = (rawX & 0x01FF) == 0;
            sprites[count++] = new SpriteInstance(x, y, widthTiles, heightTiles, attributes, mask);

            int next = sizeLink & 0x7F;
            if (next == 0)
            {
                break;
            }

            spriteIndex = next;
        }

        return count;
    }

    private int GatherLineSprites(SpriteInstance[] sprites, int spriteCount, int sourceY, LineSprite[] lineSprites)
    {
        int maxSpritesPerLine = IsH40() ? 20 : 16;
        int maxSpritePixelsPerLine = IsH40() ? 320 : 256;
        int spriteCellHeight = TileHeightPixels();
        int spritePixels = 0;
        bool lowPrioritySpritesMasked = false;
        int lineSpriteCount = 0;

        for (int i = 0; i < spriteCount; i++)
        {
            SpriteInstance sprite = sprites[i];
            int heightPixels = sprite.HeightTiles * spriteCellHeight;
            if (sourceY < sprite.Y || sourceY >= sprite.Y + heightPixels)
            {
                continue;
            }

            if (sprite.Mask)
            {
                if (lineSpriteCount > 0)
                {
                    lowPrioritySpritesMasked = true;
                }

                continue;
            }

            if (lowPrioritySpritesMasked && !sprite.Priority)
            {
                continue;
            }

            int widthPixels = sprite.WidthTiles * 8;
            int remainingPixels = maxSpritePixelsPerLine - spritePixels;
            if (lineSpriteCount >= maxSpritesPerLine || remainingPixels <= 0)
            {
                break;
            }

            int visiblePixels = Math.Min(widthPixels, remainingPixels);
            lineSprites[lineSpriteCount++] = new LineSprite(sprite, visiblePixels);
            spritePixels += widthPixels;
            if (visiblePixels < widthPixels)
            {
                break;
            }
        }

        return lineSpriteCount;
    }

    private LayerPixel ReadSpritePixel(LineSprite[] sprites, int spriteCount, int screenX, int lineY, int sourceY)
    {
        int tileHeight = TileHeightPixels();
        for (int i = 0; i < spriteCount; i++)
        {
            LineSprite lineSprite = sprites[i];
            SpriteInstance sprite = lineSprite.Sprite;
            int spriteLineY = IsInterlaceDoubleResolution() ? sourceY : lineY;
            int localX = screenX - sprite.X;
            int localY = spriteLineY - sprite.Y;
            int widthPixels = sprite.WidthTiles * 8;
            int heightPixels = sprite.HeightTiles * tileHeight;
            if ((uint)localX >= (uint)widthPixels || localX >= lineSprite.VisiblePixels || (uint)localY >= (uint)heightPixels)
            {
                continue;
            }

            ushort attributes = sprite.Attributes;
            int baseTile = attributes & 0x07FF;
            bool hflip = (attributes & 0x0800) != 0;
            bool vflip = (attributes & 0x1000) != 0;
            int palette = (attributes >> 13) & 0x03;
            int tileX = localX / 8;
            int tileY = localY / tileHeight;
            int pixelX = localX & 7;
            int pixelY = localY % tileHeight;
            int sourceTileX = hflip ? sprite.WidthTiles - 1 - tileX : tileX;
            int sourceTileY = vflip ? sprite.HeightTiles - 1 - tileY : tileY;
            int tileIndex = (baseTile + GetSpriteTileOffset(sprite, sourceTileX, sourceTileY)) & 0x07FF;
            ushort tileName = (ushort)(tileIndex | (attributes & 0x9800));
            int colorIndex = ReadTilePixel(tileName, pixelX, pixelY);
            if (colorIndex == 0)
            {
                continue;
            }

            return new LayerPixel(true, ((palette * 16) + colorIndex) & 0x3F, (attributes & 0x8000) != 0, colorIndex, true);
        }

        return default;
    }

    private void AnalyzeSpriteStatus(SpriteInstance[] sprites, int spriteCount)
    {
        _spriteOverflow = false;
        _spriteCollision = false;
        int maxSpritesPerLine = IsH40() ? 20 : 16;
        int spriteCellHeight = TileHeightPixels();
        int scanlines = IsInterlaceDoubleResolution() ? ScreenHeight * 2 : ScreenHeight;

        for (int y = 0; y < scanlines; y++)
        {
            int spritesOnLine = 0;
            bool trackCollision = !_spriteCollision;
            int collisionStamp = 0;
            if (trackCollision)
            {
                _spriteCollisionStamp++;
                if (_spriteCollisionStamp == 0)
                {
                    Array.Clear(_spriteCollisionMarks);
                    _spriteCollisionStamp = 1;
                }

                collisionStamp = _spriteCollisionStamp;
            }

            for (int i = 0; i < spriteCount; i++)
            {
                SpriteInstance sprite = sprites[i];
                if (sprite.Mask)
                {
                    if (y >= sprite.Y && y < sprite.Y + (sprite.HeightTiles * spriteCellHeight))
                    {
                        if (spritesOnLine > 0)
                        {
                            break;
                        }
                    }

                    continue;
                }

                int localY = y - sprite.Y;
                int heightPixels = sprite.HeightTiles * spriteCellHeight;
                if ((uint)localY >= (uint)heightPixels)
                {
                    continue;
                }

                spritesOnLine++;
                if (spritesOnLine > maxSpritesPerLine)
                {
                    _spriteOverflow = true;
                }

                if (!trackCollision)
                {
                    continue;
                }

                int startX = Math.Max(0, sprite.X);
                int endX = Math.Min(ScreenWidth, sprite.X + (sprite.WidthTiles * 8));
                for (int x = startX; x < endX; x++)
                {
                    LayerPixel pixel = ReadSingleSpritePixel(sprite, x, y, y);
                    if (!pixel.Visible)
                    {
                        continue;
                    }

                    if (_spriteCollisionMarks[x] == collisionStamp)
                    {
                        _spriteCollision = true;
                        trackCollision = false;
                        break;
                    }

                    _spriteCollisionMarks[x] = collisionStamp;
                }
            }

            if (_spriteOverflow && _spriteCollision)
            {
                break;
            }
        }

        UpdateStatusFlags();
    }

    private LayerPixel ReadSingleSpritePixel(SpriteInstance sprite, int screenX, int lineY, int sourceY)
    {
        int localX = screenX - sprite.X;
        int tileHeight = TileHeightPixels();
        int spriteLineY = IsInterlaceDoubleResolution() ? sourceY : lineY;
        int localY = spriteLineY - sprite.Y;
        int widthPixels = sprite.WidthTiles * 8;
        int heightPixels = sprite.HeightTiles * tileHeight;
        if ((uint)localX >= (uint)widthPixels || (uint)localY >= (uint)heightPixels)
        {
            return default;
        }

        ushort attributes = sprite.Attributes;
        int baseTile = attributes & 0x07FF;
        bool hflip = (attributes & 0x0800) != 0;
        bool vflip = (attributes & 0x1000) != 0;
        int palette = (attributes >> 13) & 0x03;
        int tileX = localX / 8;
        int tileY = localY / tileHeight;
        int pixelX = localX & 7;
        int pixelY = localY % tileHeight;
        int sourceTileX = hflip ? sprite.WidthTiles - 1 - tileX : tileX;
        int sourceTileY = vflip ? sprite.HeightTiles - 1 - tileY : tileY;
        int tileIndex = (baseTile + GetSpriteTileOffset(sprite, sourceTileX, sourceTileY)) & 0x07FF;
        ushort tileName = (ushort)(tileIndex | (attributes & 0x9800));
        int colorIndex = ReadTilePixel(tileName, pixelX, pixelY);
        return colorIndex == 0
            ? default
            : new LayerPixel(true, ((palette * 16) + colorIndex) & 0x3F, (attributes & 0x8000) != 0, colorIndex, true);
    }

    private uint ReadSingleSpritePackedPixel(SpriteInstance sprite, int screenX, int lineY, int sourceY)
    {
        int localX = screenX - sprite.X;
        int tileHeight = TileHeightPixels();
        int spriteLineY = IsInterlaceDoubleResolution() ? sourceY : lineY;
        int localY = spriteLineY - sprite.Y;
        int widthPixels = sprite.WidthTiles * 8;
        int heightPixels = sprite.HeightTiles * tileHeight;
        if ((uint)localX >= (uint)widthPixels || (uint)localY >= (uint)heightPixels)
        {
            return 0;
        }

        ushort attributes = sprite.Attributes;
        int baseTile = attributes & 0x07FF;
        bool hflip = (attributes & 0x0800) != 0;
        bool vflip = (attributes & 0x1000) != 0;
        int palette = (attributes >> 13) & 0x03;
        int tileX = localX / 8;
        int tileY = localY / tileHeight;
        int pixelX = localX & 7;
        int pixelY = localY % tileHeight;
        int sourceTileX = hflip ? sprite.WidthTiles - 1 - tileX : tileX;
        int sourceTileY = vflip ? sprite.HeightTiles - 1 - tileY : tileY;
        int tileIndex = (baseTile + GetSpriteTileOffset(sprite, sourceTileX, sourceTileY)) & 0x07FF;
        ushort tileName = (ushort)(tileIndex | (attributes & 0x9800));
        int colorIndex = ReadTilePixel(tileName, pixelX, pixelY);
        return colorIndex == 0
            ? 0
            : PackedVisibleBit
                | PackedSpriteBit
                | ((attributes & 0x8000) != 0 ? PackedPriorityBit : 0)
                | ((uint)(((palette * 16) + colorIndex) & 0x3F) << PackedPaletteShift)
                | ((uint)colorIndex << PackedColorShift);
    }

    private int FindBestFallbackNameTableBase()
    {
        int bestBase = 0;
        int bestScore = 0;
        for (int candidate = 0; candidate < 0x10000; candidate += 0x400)
        {
            int score = ScoreNameTable(candidate);
            if (score > bestScore)
            {
                bestScore = score;
                bestBase = candidate;
            }
        }

        return bestBase;
    }

    private static int GetSpriteTileOffset(SpriteInstance sprite, int sourceTileX, int sourceTileY)
    {
        return (sourceTileX * sprite.HeightTiles) + sourceTileY;
    }

    private int ScoreNameTable(int nameTableBase)
    {
        (int planeWidthTiles, _) = GetPlaneSize();
        int score = 0;
        for (int cell = 0; cell < planeWidthTiles * 28; cell++)
        {
            ushort name = ReadVramWord((uint)(nameTableBase + (cell * 2)));
            int tileIndex = name & 0x07FF;
            if (TileHasPixels(tileIndex))
            {
                score++;
            }
        }

        return score;
    }

    private bool TileHasPixels(int tileIndex)
    {
        int address = tileIndex * 32;
        for (int i = 0; i < 32; i++)
        {
            if (_vram[(address + i) & 0xFFFF] != 0)
            {
                return true;
            }
        }

        return false;
    }

    private int FindFirstNonzeroTile()
    {
        for (int tile = 0; tile < 2048; tile++)
        {
            if (TileHasPixels(tile))
            {
                return tile;
            }
        }

        return 0;
    }

    private int CountNonzeroTiles()
    {
        int count = 0;
        for (int tile = 0; tile < 2048; tile++)
        {
            if (TileHasPixels(tile))
            {
                count++;
            }
        }

        return count;
    }

    private int CountLikelySprites()
    {
        int tableBase = GetSpriteAttributeTableBase();
        int maxSprites = IsH40() ? 80 : 64;
        int count = 0;
        bool[] visited = new bool[maxSprites];
        int spriteIndex = 0;

        for (int spriteCount = 0; spriteCount < maxSprites; spriteCount++)
        {
            if ((uint)spriteIndex >= maxSprites || visited[spriteIndex])
            {
                break;
            }

            visited[spriteIndex] = true;
            int entry = tableBase + (spriteIndex * 8);
            ushort rawY = ReadVramWord((uint)entry);
            ushort sizeLink = ReadVramWord((uint)(entry + 2));
            ushort attributes = ReadVramWord((uint)(entry + 4));
            ushort rawX = ReadVramWord((uint)(entry + 6));
            if ((rawY | sizeLink | attributes | rawX) != 0)
            {
                count++;
            }

            int next = sizeLink & 0x7F;
            if (next == 0)
            {
                break;
            }

            spriteIndex = next;
        }

        return count;
    }

    private int CountRegisterWrites(int register)
    {
        int count = 0;
        foreach (RegisterWrite write in _registerWrites)
        {
            if (write.Register == register)
            {
                count++;
            }
        }

        return count;
    }

    private void RenderRawTileFallback(byte[] framebuffer, int startTile)
    {
        Fill(framebuffer, new Rgb(0, 0, 0));
        int columns = ScreenWidth / 8;
        int rows = ScreenHeight / 8;
        for (int tile = 0; tile < columns * rows; tile++)
        {
            int tileIndex = (startTile + tile) & 0x07FF;
            int tileX = (tile % columns) * 8;
            int tileY = (tile / columns) * 8;
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    int color = ReadRawTilePixel(tileIndex, x, y);
                    if (color == 0)
                    {
                        continue;
                    }

                    byte shade = (byte)(color * 17);
                    WritePixel(framebuffer, tileX + x, tileY + y, new Rgb(shade, shade, shade));
                }
            }
        }
    }

    private static bool HasVisiblePixels(byte[] framebuffer, Rgb background)
    {
        for (int i = 0; i < framebuffer.Length; i += 3)
        {
            if (framebuffer[i] != background.R || framebuffer[i + 1] != background.G || framebuffer[i + 2] != background.B)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasDirectColorDmaFrame()
    {
        return _directColorSamples.Count >= ScreenHeight * 64;
    }

    private bool IsDisplayEnabled()
    {
        return (_registers[1] & 0x40) != 0;
    }

    private int RenderDirectColorDmaFrame(byte[] framebuffer, FramePixelOrder pixelOrder)
    {
        LastRenderMode = "direct-color-dma";
        Fill(framebuffer, new Rgb(0, 0, 0), pixelOrder);
        int samplesPerLine = Math.Max(1, _directColorSamples.Count / ScreenHeight);
        int visibleSamples = Math.Min(ScreenWidth / 2, samplesPerLine);
        int drawn = 0;

        for (int y = 0; y < ScreenHeight; y++)
        {
            int row = y * samplesPerLine;
            for (int sampleX = 0; sampleX < visibleSamples; sampleX++)
            {
                int index = row + sampleX;
                if (index >= _directColorSamples.Count)
                {
                    break;
                }

                Rgb color = DecodeRawColor(_directColorSamples[index]);
                int x = sampleX * 2;
                WritePixel(framebuffer, x, y, color, pixelOrder);
                WritePixel(framebuffer, x + 1, y, color, pixelOrder);
                if (color.R != 0 || color.G != 0 || color.B != 0)
                {
                    drawn += 2;
                }
            }
        }

        return drawn;
    }

    private void BeginDirectColorCaptureIfNeeded(DmaRequest request)
    {
        _capturingDirectColorDma = (request.Code & 0x0F) == 0x03 && request.DestinationAddress < 0x80 && request.LengthWords >= ScreenHeight * 64;
        _directColorCaptureRemaining = _capturingDirectColorDma ? request.LengthWords : 0;
        if (_capturingDirectColorDma)
        {
            _directColorSamples.Clear();
        }
    }

    private void CaptureDirectColorSample(ushort value)
    {
        if (!_capturingDirectColorDma || _directColorCaptureRemaining <= 0)
        {
            return;
        }

        if (_directColorSamples.Count < 128 * 1024)
        {
            _directColorSamples.Add((ushort)(value & 0x0EEE));
        }

        _directColorCaptureRemaining--;
        if (_directColorCaptureRemaining == 0)
        {
            _capturingDirectColorDma = false;
        }
    }

    private int ReadTilePixel(ushort name, int x, int y)
    {
        int tileIndex = name & 0x07FF;
        bool hflip = (name & 0x0800) != 0;
        bool vflip = (name & 0x1000) != 0;
        int tileHeight = TileHeightPixels();
        int pixelX = hflip ? 7 - x : x;
        int pixelY = vflip ? tileHeight - 1 - y : y;
        int tileAddress = (tileIndex * TileBytes()) + (pixelY * 4) + (pixelX / 2);
        byte[] vram = _renderVram ?? _vram;
        byte packed = vram[tileAddress & 0xFFFF];
        return (pixelX & 1) == 0 ? packed >> 4 : packed & 0x0F;
    }

    private int GetHorizontalScroll(PlaneKind plane, int screenY)
    {
        int row = Math.Clamp(screenY, 0, ScreenHeight - 1);
        if (_lineHscrollCaptured[row])
        {
            int planeIndex = plane == PlaneKind.PlaneB ? 1 : 0;
            return SignExtend11(_lineHscroll[row, planeIndex]);
        }

        return SignExtend11(ReadHorizontalScrollWord(plane, screenY));
    }

    private ushort ReadHorizontalScrollWord(PlaneKind plane, int screenY)
    {
        int tableBase = (_registers[13] & 0x3F) << 10;
        int mode = _registers[11] & 0x03;
        int scrollLine = screenY;
        int row = mode switch
        {
            0x01 => (scrollLine & 0x07) * 4,
            0x02 => (scrollLine & ~0x07) * 4,
            0x03 => scrollLine * 4,
            _ => 0,
        };

        int offset = tableBase + row + (plane == PlaneKind.PlaneB ? 2 : 0);
        return ReadVramWord((uint)offset);
    }

    private int GetVerticalScroll(PlaneKind plane, int screenX, int lineY)
    {
        int column = ((_registers[11] & 0x04) == 0) ? 0 : Math.Clamp(screenX / 16, 0, 19);
        return plane == PlaneKind.PlaneB ? _linePlaneBVScroll[column] : _linePlaneAVScroll[column];
    }

    private void BuildLineVerticalScroll(int lineY)
    {
        int row = Math.Clamp(lineY, 0, ScreenHeight - 1);
        bool useSnapshot = _lineVsramCaptured[row];
        if ((_registers[11] & 0x04) == 0)
        {
            _linePlaneAVScroll[0] = SignExtend11(useSnapshot ? _lineVsram[row, 0] : _vsram[0]);
            _linePlaneBVScroll[0] = SignExtend11(useSnapshot ? _lineVsram[row, 1] : _vsram[1]);
            return;
        }

        for (int column = 0; column < 20; column++)
        {
            int index = column * 2;
            _linePlaneAVScroll[column] = SignExtend11(useSnapshot ? _lineVsram[row, index] : _vsram[index]);
            _linePlaneBVScroll[column] = SignExtend11(useSnapshot ? _lineVsram[row, index + 1] : _vsram[index + 1]);
        }
    }

    private int GetVerticalScrollSlow(PlaneKind plane, int screenX, int lineY)
    {
        int baseIndex = plane == PlaneKind.PlaneB ? 1 : 0;
        int row = Math.Clamp(lineY, 0, ScreenHeight - 1);
        bool useSnapshot = _lineVsramCaptured[row];
        if ((_registers[11] & 0x04) == 0)
        {
            return SignExtend11(useSnapshot ? _lineVsram[row, baseIndex] : _vsram[baseIndex]);
        }

        int column = Math.Clamp(screenX / 16, 0, 19);
        int index = (column * 2) + baseIndex;
        return SignExtend11(useSnapshot ? _lineVsram[row, index] : _vsram[index]);
    }

    private bool IsWindowPixel(int screenX, int screenY)
    {
        int horizontalCell = _registers[17] & 0x1F;
        bool fromRight = (_registers[17] & 0x80) != 0;
        int verticalCell = _registers[18] & 0x1F;
        bool fromBottom = (_registers[18] & 0x80) != 0;
        bool inHorizontal = fromRight ? screenX >= horizontalCell * 8 : screenX < horizontalCell * 8;
        bool inVertical = fromBottom ? screenY >= verticalCell * 8 : screenY < verticalCell * 8;
        return inHorizontal || inVertical;
    }

    private WindowLineInfo BuildWindowLineInfo(int screenY, int activeWidth)
    {
        int horizontalBoundary = (_registers[17] & 0x1F) * 8;
        bool fromRight = (_registers[17] & 0x80) != 0;
        int verticalBoundary = (_registers[18] & 0x1F) * 8;
        bool fromBottom = (_registers[18] & 0x80) != 0;
        bool horizontalActive = fromRight ? horizontalBoundary < activeWidth : horizontalBoundary > 0;
        bool verticalActive = fromBottom ? screenY >= verticalBoundary : screenY < verticalBoundary;
        return new WindowLineInfo(horizontalActive, horizontalBoundary, fromRight, verticalActive);
    }

    private static int SignExtend11(ushort value)
    {
        int scroll = value & 0x07FF;
        return (scroll & 0x0400) != 0 ? scroll - 0x0800 : scroll;
    }

    private int ReadRawTilePixel(int tileIndex, int x, int y)
    {
        int tileAddress = (tileIndex * 32) + (y * 4) + (x / 2);
        byte packed = _vram[tileAddress & 0xFFFF];
        return (x & 1) == 0 ? packed >> 4 : packed & 0x0F;
    }

    private static void Fill(byte[] framebuffer, Rgb color, FramePixelOrder pixelOrder = FramePixelOrder.Rgb)
    {
        for (int i = 0; i < framebuffer.Length; i += 3)
        {
            WriteColor(framebuffer, i, color, pixelOrder);
        }
    }

    private static void FillLineSegment(byte[] framebuffer, int y, int x, int width, Rgb color, FramePixelOrder pixelOrder = FramePixelOrder.Rgb)
    {
        int offset = ((y * ScreenWidth) + x) * 3;
        int end = offset + (width * 3);
        for (int i = offset; i < end; i += 3)
        {
            WriteColor(framebuffer, i, color, pixelOrder);
        }
    }

    private static void WritePixel(byte[] framebuffer, int x, int y, Rgb color, FramePixelOrder pixelOrder = FramePixelOrder.Rgb)
    {
        if ((uint)x >= ScreenWidth || (uint)y >= ScreenHeight)
        {
            return;
        }

        int offset = ((y * ScreenWidth) + x) * 3;
        WriteColor(framebuffer, offset, color, pixelOrder);
    }

    private static void WriteColor(byte[] framebuffer, int offset, Rgb color, FramePixelOrder pixelOrder)
    {
        if (pixelOrder == FramePixelOrder.Bgr)
        {
            framebuffer[offset] = color.B;
            framebuffer[offset + 1] = color.G;
            framebuffer[offset + 2] = color.R;
            return;
        }

        framebuffer[offset] = color.R;
        framebuffer[offset + 1] = color.G;
        framebuffer[offset + 2] = color.B;
    }

    private Rgb DecodePixelColor(LayerPixel pixel, ShadowHighlightShade shade)
    {
        int paletteIndex = pixel.PaletteIndex & 0x3F;
        return ApplyShade(_renderPalette[paletteIndex], shade);
    }

    private Rgb ApplyShade(Rgb color, ShadowHighlightShade shade)
    {
        return shade switch
        {
            ShadowHighlightShade.Shadow => Shadow(color),
            ShadowHighlightShade.Highlight => Highlight(color),
            _ => color,
        };
    }

    private static ShadowHighlightShade ResolveShadowHighlightShade(
        LayerPixel planeA,
        LayerPixel planeB,
        LayerPixel sprite,
        bool spriteEffect,
        bool shadowHighlightEnabled)
    {
        if (!shadowHighlightEnabled)
        {
            return ShadowHighlightShade.Normal;
        }

        bool effectBlockedByHighPlane = spriteEffect
            && !sprite.Priority
            && ((planeA.Visible && planeA.Priority) || (planeB.Visible && planeB.Priority));
        bool effectApplies = spriteEffect && !effectBlockedByHighPlane;
        bool anyHighPriority = planeA.Priority || planeB.Priority || (sprite.Visible && sprite.Priority && !effectBlockedByHighPlane);
        bool spriteColor14Quirk = sprite.Visible && sprite.ColorIndex == 14;
        ShadowHighlightShade shade = !anyHighPriority && !spriteColor14Quirk ? ShadowHighlightShade.Shadow : ShadowHighlightShade.Normal;

        if (!effectApplies)
        {
            return shade;
        }

        int colorIndex = sprite.ColorIndex & 0x0F;
        if (colorIndex == 14)
        {
            return shade == ShadowHighlightShade.Shadow ? ShadowHighlightShade.Normal : ShadowHighlightShade.Highlight;
        }

        return ShadowHighlightShade.Shadow;
    }

    private static bool IsShadowHighlightEffect(LayerPixel pixel, bool shadowHighlightEnabled)
    {
        return shadowHighlightEnabled && pixel.IsSprite && (pixel.PaletteIndex & 0x30) == 0x30 && (pixel.ColorIndex == 14 || pixel.ColorIndex == 15);
    }

    private static ShadowHighlightShade ResolveShadowHighlightShadePacked(
        uint planeA,
        uint planeB,
        uint sprite,
        bool spriteEffect,
        bool shadowHighlightEnabled)
    {
        if (!shadowHighlightEnabled)
        {
            return ShadowHighlightShade.Normal;
        }

        bool effectBlockedByHighPlane = spriteEffect
            && !PackedPriority(sprite)
            && ((PackedVisible(planeA) && PackedPriority(planeA)) || (PackedVisible(planeB) && PackedPriority(planeB)));
        bool effectApplies = spriteEffect && !effectBlockedByHighPlane;
        bool anyHighPriority = PackedPriority(planeA) || PackedPriority(planeB) || (PackedVisible(sprite) && PackedPriority(sprite) && !effectBlockedByHighPlane);
        bool spriteColor14Quirk = PackedVisible(sprite) && PackedColorIndex(sprite) == 14;
        ShadowHighlightShade shade = !anyHighPriority && !spriteColor14Quirk ? ShadowHighlightShade.Shadow : ShadowHighlightShade.Normal;

        if (!effectApplies)
        {
            return shade;
        }

        int colorIndex = PackedColorIndex(sprite);
        if (colorIndex == 14)
        {
            return shade == ShadowHighlightShade.Shadow ? ShadowHighlightShade.Normal : ShadowHighlightShade.Highlight;
        }

        return ShadowHighlightShade.Shadow;
    }

    private static bool IsShadowHighlightEffectPacked(uint pixel, bool shadowHighlightEnabled)
    {
        return shadowHighlightEnabled && PackedSprite(pixel) && (PackedPaletteIndex(pixel) & 0x30) == 0x30 && (PackedColorIndex(pixel) == 14 || PackedColorIndex(pixel) == 15);
    }

    private void BuildRenderPalette(int lineY)
    {
        bool useSnapshot = (uint)lineY < ScreenHeight && _lineCramCaptured[lineY];
        for (int i = 0; i < 64; i++)
        {
            Rgb color = DecodeRawColor(useSnapshot ? _lineCram[lineY, i] : _cram[i]);
            _renderPalette[i] = color;
            _renderShadowPalette[i] = Shadow(color);
            _renderHighlightPalette[i] = Highlight(color);
        }
    }

    private Rgb DecodeColor(int paletteIndex, int lineY = -1)
    {
        int index = paletteIndex & 0x3F;
        if ((uint)lineY < ScreenHeight && _lineCramCaptured[lineY])
        {
            return DecodeRawColor(_lineCram[lineY, index]);
        }

        return DecodeRawColor(_cram[index]);
    }

    private Rgb DecodeRawColor(ushort value)
    {
        byte r = Expand3To8((value >> 1) & 0x7);
        byte g = Expand3To8((value >> 5) & 0x7);
        byte b = Expand3To8((value >> 9) & 0x7);
        return new Rgb(r, g, b);
    }

    private bool IsShadowHighlightEnabled()
    {
        return (_registers[12] & 0x08) != 0;
    }

    private static Rgb Shadow(Rgb color)
    {
        return new Rgb((byte)(color.R / 2), (byte)(color.G / 2), (byte)(color.B / 2));
    }

    private static Rgb Highlight(Rgb color)
    {
        return new Rgb(
            (byte)Math.Min(255, color.R + ((255 - color.R) / 2)),
            (byte)Math.Min(255, color.G + ((255 - color.G) / 2)),
            (byte)Math.Min(255, color.B + ((255 - color.B) / 2)));
    }

    private static byte Expand3To8(int value)
    {
        return (byte)((value << 5) | (value << 2) | (value >> 1));
    }

    private int GetPlaneANameTableBase()
    {
        return (_registers[2] & 0x38) << 10;
    }

    private int GetPlaneBNameTableBase()
    {
        return (_registers[4] & 0x07) << 13;
    }

    private int GetSpriteAttributeTableBase()
    {
        return (IsH40() ? _registers[5] & 0x7E : _registers[5] & 0x7F) << 9;
    }

    private int GetWindowNameTableBase()
    {
        return (IsH40() ? _registers[3] & 0x3C : _registers[3] & 0x3E) << 10;
    }

    private (int Width, int Height) GetPlaneSize()
    {
        int width = DecodePlaneDimension(_registers[16] & 0x03);
        int height = DecodePlaneDimension((_registers[16] >> 4) & 0x03);
        return (width, height);
    }

    private static int DecodePlaneDimension(int value)
    {
        return value switch
        {
            0 => 32,
            1 => 64,
            3 => 128,
            _ => 32,
        };
    }

    private bool IsH40()
    {
        return (_registers[12] & 0x01) != 0;
    }

    private bool IsInterlaceDoubleResolution()
    {
        return (_registers[12] & 0x06) == 0x06;
    }

    private int GetRenderSourceY(int screenY)
    {
        return IsInterlaceDoubleResolution() ? screenY * 2 : screenY;
    }

    private int TileHeightPixels()
    {
        return IsInterlaceDoubleResolution() ? 16 : 8;
    }

    private int TileBytes()
    {
        return IsInterlaceDoubleResolution() ? 64 : 32;
    }

    private bool IsPalTiming()
    {
        return (Status & 0x0001) != 0;
    }

    private enum PlaneKind
    {
        PlaneA,
        PlaneB,
    }

    private enum FramePixelOrder
    {
        Rgb,
        Bgr,
    }

    private enum ShadowHighlightShade
    {
        Normal,
        Shadow,
        Highlight,
    }

    private readonly record struct Rgb(byte R, byte G, byte B);
    private readonly record struct LayerPixel(bool Visible, int PaletteIndex, bool Priority, int ColorIndex, bool IsSprite);
    private readonly record struct WindowLineInfo(bool HorizontalActive, int HorizontalBoundary, bool HorizontalFromRight, bool VerticalActive);
    private readonly record struct LineSprite(SpriteInstance Sprite, int VisiblePixels);
    private readonly record struct SpriteInstance(int X, int Y, int WidthTiles, int HeightTiles, ushort Attributes, bool Mask)
    {
        public bool Priority => (Attributes & 0x8000) != 0;
    }

    public readonly record struct RenderPerformanceCounters(
        long SnapshotTicks = 0,
        long PaletteTicks = 0,
        long ScrollTicks = 0,
        long SpriteGatherTicks = 0,
        long PlaneBTicks = 0,
        long PlaneAWindowTicks = 0,
        long SpriteRenderTicks = 0,
        long CompositingTicks = 0,
        long BorderTicks = 0,
        long DisplayFillTicks = 0,
        long DirectColorTicks = 0);

    private struct RenderPerformanceAccumulator
    {
        public long SnapshotTicks;
        public long PaletteTicks;
        public long ScrollTicks;
        public long SpriteGatherTicks;
        public long PlaneBTicks;
        public long PlaneAWindowTicks;
        public long SpriteRenderTicks;
        public long CompositingTicks;
        public long BorderTicks;

        public readonly RenderPerformanceCounters ToCounters()
        {
            return new RenderPerformanceCounters(
                SnapshotTicks,
                PaletteTicks,
                ScrollTicks,
                SpriteGatherTicks,
                PlaneBTicks,
                PlaneAWindowTicks,
                SpriteRenderTicks,
                CompositingTicks,
                BorderTicks);
        }
    }

    public readonly record struct VdpDiagnostics(
        int PlaneANameTableBase,
        int PlaneBNameTableBase,
        int SpriteAttributeTableBase,
        int PlaneWidthTiles,
        int PlaneHeightTiles,
        int ActiveCells,
        int PlaneAScore,
        int PlaneBScore,
        int BestFallbackNameTableBase,
        int BestFallbackNameTableScore,
        int FirstNonzeroTile,
        int NonzeroTileCount,
        int LikelySpriteCount,
        int PlaneARegisterWriteCount,
        int PlaneBRegisterWriteCount,
        int SpriteAttributeRegisterWriteCount,
        byte LastCommandCode,
        uint LastCommandAddress,
        int DmaEventCount,
        int DirectColorSampleCount,
        int FifoWords,
        bool SpriteOverflow,
        bool SpriteCollision);

    public readonly record struct VdpPlanePixelTrace(
        VdpDebugLayer Layer,
        VdpPlaneSourceKind SourceKind,
        int ScreenX,
        int ScreenY,
        int PlaneX,
        int LineY,
        int ActiveWidth,
        int XOffset,
        int PlaneWidthTiles,
        int PlaneHeightTiles,
        int ScrollX,
        int ScrollY,
        int SourceX,
        int SourceY,
        int NameTableBase,
        int NameTableAddress,
        ushort Name,
        int TileIndex,
        int TileAddress,
        int PixelX,
        int PixelY,
        int EffectivePixelX,
        int EffectivePixelY,
        int Palette,
        bool Priority,
        int ColorIndex,
        byte PackedByte)
    {
        public static VdpPlanePixelTrace Disabled(VdpDebugLayer layer, int screenX, int screenY)
        {
            return new VdpPlanePixelTrace(layer, VdpPlaneSourceKind.DisplayDisabled, screenX, screenY, 0, Math.Clamp(screenY, 0, ScreenHeight - 1), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, 0, 0);
        }

        public static VdpPlanePixelTrace Outside(VdpDebugLayer layer, int screenX, int screenY, int activeWidth, int xOffset, int planeWidthTiles, int planeHeightTiles)
        {
            return new VdpPlanePixelTrace(layer, VdpPlaneSourceKind.OutsideActiveDisplay, screenX, screenY, screenX - xOffset, Math.Clamp(screenY, 0, ScreenHeight - 1), activeWidth, xOffset, planeWidthTiles, planeHeightTiles, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, false, 0, 0);
        }
    }

    public sealed record VdpState(
        byte[] Vram,
        ushort[] Cram,
        ushort[] Vsram,
        byte[] Registers,
        ushort Status,
        int Scanline,
        int HintCounter,
        int FifoWords,
        bool OddFrame,
        bool VBlank,
        bool VInterruptPending,
        bool HInterruptPending,
        bool HBlank,
        bool SpriteOverflow,
        bool SpriteCollision,
        uint Address,
        byte Code,
        ushort[] DirectColorSamples,
        int DmaCycleDebt);
}
