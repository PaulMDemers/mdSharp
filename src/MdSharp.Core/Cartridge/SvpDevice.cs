namespace MdSharp.Core.Cartridge;

public sealed class SvpDevice
{
    private const int DramWords = 0x10000;
    private const int IramWords = 0x10000;
    private const int FlagL = 1 << 12;
    private const int FlagZ = 1 << 13;
    private const int FlagV = 1 << 14;
    private const int FlagN = 1 << 15;
    private const uint PmcHaveAddress = 0x0001;
    private const uint PmcSet = 0x0002;
    private const uint Hang = 0x1000;
    private const uint WaitPm0 = 0x2000;
    private const uint Wait30Fe06 = 0x4000;
    private const uint Wait30Fe08 = 0x8000;
    private const uint WaitMask = 0xF000;

    private enum Register
    {
        Gr0 = 0,
        X = 1,
        Y = 2,
        A = 3,
        St = 4,
        Stack = 5,
        Pc = 6,
        P = 7,
        Pm0 = 8,
        Pm1 = 9,
        Pm2 = 10,
        Xst = 11,
        Pm4 = 12,
        Pmc = 14,
        Al = 15,
    }

    private readonly byte[] _rom;
    private readonly ushort[] _iram = new ushort[IramWords];
    private readonly ushort[] _ram = new ushort[512];
    private readonly ushort[] _dram = new ushort[DramWords];
    private readonly uint[] _gr = new uint[16];
    private readonly byte[] _pointers = new byte[8];
    private readonly ushort[] _stack = new ushort[6];
    private readonly uint[,] _pmac = new uint[2, 6];
    private readonly ulong[] _pmacWriteCounts = new ulong[4];
    private readonly ulong[] _pmacReadCounts = new ulong[3];
    private readonly Dictionary<DramWriteTraceKey, DramWriteTraceAccumulator> _dramWriteTraces = new();
    private readonly DramWriteSample[] _dramWriteSamples = new DramWriteSample[4096];
    private int _dramWriteSampleIndex;
    private ulong _dramWriteSampleSequence;
    private uint _emuStatus;
    private int _pc;
    private int _cycles;
    private ushort _lastOp;
    private int _lastOpByteOffset;
    private uint _unhandledOpcodeCount;
    private ushort _lastUnhandledOpcode;
    private int _lastUnhandledPc;

    public SvpDevice(ReadOnlyMemory<byte> rom)
    {
        _rom = rom.ToArray();
        Reset();
    }

    public bool IsWaiting => (_emuStatus & WaitMask) != 0;
    public ushort HostStatus => High((int)Register.Pm0);
    public ushort HostResult => High((int)Register.Xst);
    public uint UnhandledOpcodeCount => _unhandledOpcodeCount;
    public ushort LastUnhandledOpcode => _lastUnhandledOpcode;
    public int LastUnhandledPc => _lastUnhandledPc;
    public bool EnableDramWriteDiagnostics { get; set; }
    public bool SetZeroFlagOnMld { get; set; }
    public bool ClearPmcOnAnyAlRead { get; set; }
    public bool ReturnZeroOnAlRead { get; set; }
    public bool RequireBlindPmacSet { get; set; } = true;
    public bool UseModuloOnPointerWrites { get; set; }
    public bool UseMameCycleTiming { get; set; }
    public Func<int, bool>? InstructionTraceFilter { get; set; }
    public Action<SvpInstructionTrace>? InstructionObserver { get; set; }
    public Action<SvpPmIoTrace>? PmIoObserver { get; set; }
    public Action<SvpPointerTrace>? PointerObserver { get; set; }
    public PmacDiagnostics PmacStats => new(
        _pmacWriteCounts[0],
        _pmacWriteCounts[1],
        _pmacWriteCounts[2],
        _pmacWriteCounts[3],
        _pmacReadCounts[0],
        _pmacReadCounts[1],
        _pmacReadCounts[2]);

    public IReadOnlyList<DramWriteDiagnostic> GetDramWriteDiagnostics(uint sourceAddress, int lengthWords, int maxEntries = 8)
    {
        if (!EnableDramWriteDiagnostics)
        {
            return Array.Empty<DramWriteDiagnostic>();
        }

        if (!TryMapAddressToDramWord(sourceAddress & 0x00FF_FFFE, out int startWord))
        {
            return Array.Empty<DramWriteDiagnostic>();
        }

        int endWord = Math.Min(0xFFFF, startWord + Math.Max(0, lengthWords) - 1);
        int startBucket = startWord >> 8;
        int endBucket = endWord >> 8;
        return _dramWriteTraces
            .Where(item => item.Key.Bucket >= startBucket && item.Key.Bucket <= endBucket)
            .OrderByDescending(item => item.Value.Count)
            .ThenBy(item => item.Key.Bucket)
            .Take(Math.Max(0, maxEntries))
            .Select(item => new DramWriteDiagnostic(
                item.Key.Bucket << 8,
                item.Key.Pc,
                (ushort)item.Key.Opcode,
                (ushort)item.Key.Mode,
                item.Key.Kind.ToString(),
                item.Key.Overwrite,
                item.Value.Count,
                item.Value.LastWordAddress,
                item.Value.LastValue))
            .ToArray();
    }

    public IReadOnlyList<DramWriteSample> GetRecentDramWriteSamples(uint sourceAddress, int lengthWords, int maxEntries = 32)
    {
        if (!EnableDramWriteDiagnostics)
        {
            return Array.Empty<DramWriteSample>();
        }

        if (!TryMapAddressToDramWord(sourceAddress & 0x00FF_FFFE, out int startWord))
        {
            return Array.Empty<DramWriteSample>();
        }

        int endWord = Math.Min(0xFFFF, startWord + Math.Max(0, lengthWords) - 1);
        List<DramWriteSample> samples = new(Math.Max(0, maxEntries));
        for (int offset = 1; offset <= _dramWriteSamples.Length && samples.Count < maxEntries; offset++)
        {
            int index = (_dramWriteSampleIndex - offset) & (_dramWriteSamples.Length - 1);
            DramWriteSample sample = _dramWriteSamples[index];
            if (sample.Sequence == 0)
            {
                continue;
            }

            int wordAddress = sample.WordAddress & 0xFFFF;
            if (wordAddress < startWord || wordAddress > endWord)
            {
                continue;
            }

            samples.Add(sample);
        }

        samples.Reverse();
        return samples;
    }

    public void Reset()
    {
        Array.Clear(_iram);
        Array.Clear(_ram);
        Array.Clear(_dram);
        Array.Clear(_gr);
        Array.Clear(_pointers);
        Array.Clear(_stack);
        Array.Clear(_pmac);
        Array.Clear(_pmacWriteCounts);
        Array.Clear(_pmacReadCounts);
        Array.Clear(_dramWriteSamples);
        _dramWriteTraces.Clear();
        _dramWriteSampleIndex = 0;
        _dramWriteSampleSequence = 0;
        int copyBytes = Math.Min(0x20000, _rom.Length);
        for (int byteOffset = 0x800; byteOffset + 1 < copyBytes; byteOffset += 2)
        {
            _iram[byteOffset >> 1] = ReadRomWordByByteOffset(byteOffset);
        }

        _emuStatus = 0;
        _gr[(int)Register.Gr0] = 0xFFFF_0000;
        SetHigh((int)Register.Pc, 0x0400);
        SetHigh((int)Register.Stack, 0);
        SetHigh((int)Register.St, 0);
        _pc = 0x0400;
        _unhandledOpcodeCount = 0;
        _lastUnhandledOpcode = 0;
        _lastUnhandledPc = 0;
    }

    public SvpState CaptureState()
    {
        uint[] pmac = new uint[_pmac.Length];
        int index = 0;
        for (int row = 0; row < _pmac.GetLength(0); row++)
        {
            for (int column = 0; column < _pmac.GetLength(1); column++)
            {
                pmac[index++] = _pmac[row, column];
            }
        }

        return new SvpState(
            (ushort[])_iram.Clone(),
            (ushort[])_ram.Clone(),
            (ushort[])_dram.Clone(),
            (uint[])_gr.Clone(),
            (byte[])_pointers.Clone(),
            (ushort[])_stack.Clone(),
            pmac,
            _emuStatus,
            _pc,
            _cycles,
            _lastOp,
            _lastOpByteOffset,
            _unhandledOpcodeCount,
            _lastUnhandledOpcode,
            _lastUnhandledPc);
    }

    public void RestoreState(SvpState state)
    {
        Array.Copy(state.Iram, _iram, Math.Min(_iram.Length, state.Iram.Length));
        Array.Copy(state.Ram, _ram, Math.Min(_ram.Length, state.Ram.Length));
        Array.Copy(state.Dram, _dram, Math.Min(_dram.Length, state.Dram.Length));
        Array.Copy(state.Gr, _gr, Math.Min(_gr.Length, state.Gr.Length));
        Array.Copy(state.Pointers, _pointers, Math.Min(_pointers.Length, state.Pointers.Length));
        Array.Copy(state.Stack, _stack, Math.Min(_stack.Length, state.Stack.Length));

        int index = 0;
        for (int row = 0; row < _pmac.GetLength(0); row++)
        {
            for (int column = 0; column < _pmac.GetLength(1); column++)
            {
                _pmac[row, column] = index < state.Pmac.Length ? state.Pmac[index] : 0;
                index++;
            }
        }

        _emuStatus = state.EmuStatus;
        _pc = state.Pc & 0xFFFF;
        _cycles = state.Cycles;
        _lastOp = state.LastOp;
        _lastOpByteOffset = state.LastOpByteOffset;
        _unhandledOpcodeCount = state.UnhandledOpcodeCount;
        _lastUnhandledOpcode = state.LastUnhandledOpcode;
        _lastUnhandledPc = state.LastUnhandledPc;
    }

    public byte ReadByte(uint address)
    {
        ushort value = ReadWord(address & 0x00FF_FFFE);
        return (address & 1) == 0 ? (byte)(value >> 8) : (byte)value;
    }

    public ushort ReadWord(uint address)
    {
        address &= 0x00FF_FFFE;
        if (address is >= 0x30_0000 and <= 0x31_FFFE)
        {
            return _dram[(address & 0x1_FFFE) >> 1];
        }

        if (address is >= 0x39_0000 and <= 0x39_FFFE)
        {
            uint offset = (address - 0x39_0000) >> 1;
            uint mapped = (offset & 0x7001) | ((offset & 0x003E) << 6) | ((offset & 0x0FC0) >> 5);
            return _dram[mapped & 0xFFFF];
        }

        if (address is >= 0x3A_0000 and <= 0x3A_FFFE)
        {
            uint offset = (address - 0x3A_0000) >> 1;
            uint mapped = (offset & 0x7801) | ((offset & 0x001E) << 6) | ((offset & 0x07E0) >> 4);
            return _dram[mapped & 0xFFFF];
        }

        if (address is >= 0xA1_5000 and <= 0xA1_5009)
        {
            uint offset = address & 0xFF;
            if ((offset & 0xFC) == 0x00)
            {
                return High((int)Register.Xst);
            }

            if ((offset & 0xFE) == 0x04)
            {
                ushort value = High((int)Register.Pm0);
                SetHigh((int)Register.Pm0, (ushort)(value & ~1));
                return value;
            }
        }

        return 0xFFFF;
    }

    public void WriteByte(uint address, byte value)
    {
        address &= 0x00FF_FFFF;
        if (address is >= 0x30_0000 and <= 0x31_FFFF)
        {
            int index = (int)((address & 0x1_FFFE) >> 1);
            ushort current = _dram[index];
            _dram[index] = (address & 1) == 0
                ? (ushort)((current & 0x00FF) | (value << 8))
                : (ushort)((current & 0xFF00) | value);
            ClearDramWait(address & 0x00FF_FFFE, _dram[index]);
            return;
        }

        if (address is >= 0xA1_5000 and <= 0xA1_5003)
        {
            ushort current = High((int)Register.Xst);
            ushort next = (address & 1) == 0
                ? (ushort)((current & 0x00FF) | (value << 8))
                : (ushort)((current & 0xFF00) | value);
            WriteHostCommand(next);
        }
    }

    public void WriteWord(uint address, ushort value)
    {
        address &= 0x00FF_FFFE;
        if (address is >= 0x30_0000 and <= 0x31_FFFE)
        {
            _dram[(address & 0x1_FFFE) >> 1] = value;
            ClearDramWait(address, value);
            return;
        }

        if (address is >= 0xA1_5000 and <= 0xA1_5003 && ((address & 0xFD) == 0))
        {
            WriteHostCommand(value);
        }
    }

    public void Run(int cycles)
    {
        if ((_emuStatus & Hang) != 0 || cycles <= 0)
        {
            return;
        }

        _pc = High((int)Register.Pc);
        _cycles = cycles;
        while (_cycles-- > 0 && (_emuStatus & WaitMask) == 0)
        {
            int opPc = _pc;
            ushort op = Fetch();
            _lastOp = op;
            _lastOpByteOffset = opPc << 1;
            Action<SvpInstructionTrace>? observer = InstructionObserver;
            Func<int, bool>? filter = InstructionTraceFilter;
            bool trace = observer is not null && (filter is null || filter(_lastOpByteOffset));
            SvpInstructionSnapshot before = trace ? CaptureInstructionSnapshot() : default;
            ushort nextWord = trace ? _iram[_pc & 0xFFFF] : (ushort)0;
            Execute(op);
            if (trace)
            {
                observer!(new SvpInstructionTrace(_lastOpByteOffset, op, nextWord, before, CaptureInstructionSnapshot()));
            }
        }

        ReadP();
        SetHigh((int)Register.Pc, (ushort)_pc);
    }

    private void Execute(ushort op)
    {
        uint value;
        switch (op >> 9)
        {
            case 0x00:
                if (op == 0)
                {
                    break;
                }

                if (op == (((int)Register.A << 4) | (int)Register.P))
                {
                    ReadP();
                    _gr[(int)Register.A] = _gr[(int)Register.P];
                }
                else
                {
                    value = RegRead(op & 0x0F);
                    RegWrite((op & 0xF0) >> 4, value);
                }

                break;
            case 0x01:
                value = Ptr1Read(op);
                RegWrite((op & 0xF0) >> 4, value);
                break;
            case 0x02:
                value = RegRead((op & 0xF0) >> 4);
                Ptr1Write(op, value);
                break;
            case 0x04:
                RegWrite((op & 0xF0) >> 4, Fetch());
                ConsumeMameExtraCycles(1);
                break;
            case 0x05:
                value = Ptr2Read(op);
                RegWrite((op & 0xF0) >> 4, value);
                ConsumeMameExtraCycles(2);
                break;
            case 0x06:
                Ptr1Write(op, Fetch());
                ConsumeMameExtraCycles(1);
                break;
            case 0x07:
                _ram[op & 0x01FF] = High((int)Register.A);
                break;
            case 0x09:
                RegWrite((op & 0xF0) >> 4, _pointers[(op & 3) | ((op >> 6) & 4)]);
                break;
            case 0x0A:
                _pointers[(op & 3) | ((op >> 6) & 4)] = (byte)RegRead((op & 0xF0) >> 4);
                break;
            case >= 0x0C and <= 0x0F:
                _pointers[(op >> 8) & 7] = (byte)op;
                break;
            case 0x24:
                if (Condition(op))
                {
                    ushort newPc = Fetch();
                    WriteStack((ushort)_pc);
                    WritePc(newPc);
                }
                else
                {
                    Fetch();
                    ConsumeMameExtraCycles(1);
                }
                break;
            case 0x25:
                RegWrite((op & 0xF0) >> 4, _iram[High((int)Register.A)]);
                ConsumeMameExtraCycles(2);
                break;
            case 0x26:
                if (Condition(op))
                {
                    WritePc(Fetch());
                }
                else
                {
                    Fetch();
                    ConsumeMameExtraCycles(1);
                }
                break;
            case 0x48:
                if (Condition(op))
                {
                    switch (op & 7)
                    {
                        case 2: _gr[(int)Register.A] = (uint)((int)_gr[(int)Register.A] >> 1); break;
                        case 3: _gr[(int)Register.A] <<= 1; break;
                        case 6: _gr[(int)Register.A] = (uint)-(int)_gr[(int)Register.A]; break;
                        case 7:
                            if ((int)_gr[(int)Register.A] < 0)
                            {
                                _gr[(int)Register.A] = (uint)-(int)_gr[(int)Register.A];
                            }

                            break;
                    }

                    UpdateAccumulatorZn();
                }

                break;
            case 0x1B:
                ReadP();
                _gr[(int)Register.A] -= _gr[(int)Register.P];
                UpdateAccumulatorZn();
                SetHigh((int)Register.X, (ushort)Ptr1ReadRaw(op & 3, 0, (op << 1) & 0x18));
                SetHigh((int)Register.Y, (ushort)Ptr1ReadRaw((op >> 4) & 3, 4, (op >> 3) & 0x18));
                break;
            case 0x4B:
                ReadP();
                _gr[(int)Register.A] += _gr[(int)Register.P];
                UpdateAccumulatorZn();
                SetHigh((int)Register.X, (ushort)Ptr1ReadRaw(op & 3, 0, (op << 1) & 0x18));
                SetHigh((int)Register.Y, (ushort)Ptr1ReadRaw((op >> 4) & 3, 4, (op >> 3) & 0x18));
                break;
            case 0x5B:
                _gr[(int)Register.A] = 0;
                ushort mldStatus = (ushort)(High((int)Register.St) & 0x0FFF);
                if (SetZeroFlagOnMld)
                {
                    mldStatus |= FlagZ;
                }

                SetHigh((int)Register.St, mldStatus);
                SetHigh((int)Register.X, (ushort)Ptr1ReadRaw(op & 3, 0, (op << 1) & 0x18));
                SetHigh((int)Register.Y, (ushort)Ptr1ReadRaw((op >> 4) & 3, 4, (op >> 3) & 0x18));
                break;
            case 0x10: if (!OpCheck32(op, SubA32)) { SubA(RegRead(op & 0x0F)); } break;
            case 0x30: if (!OpCheck32(op, CmpA32)) { CmpA(RegRead(op & 0x0F)); } break;
            case 0x40: if (!OpCheck32(op, AddA32)) { AddA(RegRead(op & 0x0F)); } break;
            case 0x50: if (!OpCheck32(op, AndA32)) { AndA(RegRead(op & 0x0F)); } break;
            case 0x60: if (!OpCheck32(op, OrA32)) { OrA(RegRead(op & 0x0F)); } break;
            case 0x70: if (!OpCheck32(op, EorA32)) { EorA(RegRead(op & 0x0F)); } break;
            case 0x11: SubA(Ptr1Read(op)); break;
            case 0x31: CmpA(Ptr1Read(op)); break;
            case 0x41: AddA(Ptr1Read(op)); break;
            case 0x51: AndA(Ptr1Read(op)); break;
            case 0x61: OrA(Ptr1Read(op)); break;
            case 0x71: EorA(Ptr1Read(op)); break;
            case 0x03: LoadA(_ram[op & 0x01FF]); break;
            case 0x13: SubA(_ram[op & 0x01FF]); break;
            case 0x33: CmpA(_ram[op & 0x01FF]); break;
            case 0x43: AddA(_ram[op & 0x01FF]); break;
            case 0x53: AndA(_ram[op & 0x01FF]); break;
            case 0x63: OrA(_ram[op & 0x01FF]); break;
            case 0x73: EorA(_ram[op & 0x01FF]); break;
            case 0x14: SubA(Fetch()); ConsumeMameExtraCycles(1); break;
            case 0x34: CmpA(Fetch()); ConsumeMameExtraCycles(1); break;
            case 0x44: AddA(Fetch()); ConsumeMameExtraCycles(1); break;
            case 0x54: AndA(Fetch()); ConsumeMameExtraCycles(1); break;
            case 0x64: OrA(Fetch()); ConsumeMameExtraCycles(1); break;
            case 0x74: EorA(Fetch()); ConsumeMameExtraCycles(1); break;
            case 0x15: SubA(Ptr2Read(op)); ConsumeMameExtraCycles(2); break;
            case 0x35: CmpA(Ptr2Read(op)); ConsumeMameExtraCycles(2); break;
            case 0x45: AddA(Ptr2Read(op)); ConsumeMameExtraCycles(2); break;
            case 0x55: AndA(Ptr2Read(op)); ConsumeMameExtraCycles(2); break;
            case 0x65: OrA(Ptr2Read(op)); ConsumeMameExtraCycles(2); break;
            case 0x75: EorA(Ptr2Read(op)); ConsumeMameExtraCycles(2); break;
            case 0x19: SubA(_pointers[(op & 3) | ((op >> 6) & 4)]); break;
            case 0x39: CmpA(_pointers[(op & 3) | ((op >> 6) & 4)]); break;
            case 0x49: AddA(_pointers[(op & 3) | ((op >> 6) & 4)]); break;
            case 0x59: AndA(_pointers[(op & 3) | ((op >> 6) & 4)]); break;
            case 0x69: OrA(_pointers[(op & 3) | ((op >> 6) & 4)]); break;
            case 0x79: EorA(_pointers[(op & 3) | ((op >> 6) & 4)]); break;
            case 0x1C: SubA(op & 0xFFu); break;
            case 0x3C: CmpA(op & 0xFFu); break;
            case 0x4C: AddA(op & 0xFFu); break;
            case 0x5C: AndA(op & 0xFFu); break;
            case 0x6C: OrA(op & 0xFFu); break;
            case 0x7C: EorA(op & 0xFFu); break;
            default:
                _unhandledOpcodeCount++;
                _lastUnhandledOpcode = op;
                _lastUnhandledPc = _lastOpByteOffset;
                break;
        }
    }

    private ushort Fetch()
    {
        ushort value = _iram[_pc & 0xFFFF];
        _pc = (_pc + 1) & 0xFFFF;
        return value;
    }

    private ushort High(int register) => (uint)register < _gr.Length ? (ushort)(_gr[register] >> 16) : (ushort)0;

    private ushort Low(int register) => (uint)register < _gr.Length ? (ushort)_gr[register] : (ushort)0;

    private void SetHigh(int register, ushort value)
    {
        if ((uint)register >= _gr.Length)
        {
            return;
        }

        _gr[register] = (_gr[register] & 0x0000_FFFFu) | ((uint)value << 16);
    }

    private void SetLow(int register, ushort value)
    {
        if ((uint)register >= _gr.Length)
        {
            return;
        }

        _gr[register] = (_gr[register] & 0xFFFF_0000u) | value;
    }

    private uint RegRead(int register)
    {
        return register <= 4 ? High(register) : register switch
        {
            (int)Register.Stack => ReadStack(),
            (int)Register.Pc => (ushort)_pc,
            (int)Register.P => ReadP(),
            (int)Register.Pm0 => ReadPm0(),
            (int)Register.Pm1 => ReadPm(1),
            (int)Register.Pm2 => ReadPm(2),
            (int)Register.Xst => ReadXst(),
            (int)Register.Pm4 => ReadPm4(),
            (int)Register.Pmc => ReadPmc(),
            (int)Register.Al => ReadAl(),
            _ => 0,
        };
    }

    private void RegWrite(int register, uint value)
    {
        ushort word = (ushort)value;
        if (register is > 0 and < 4)
        {
            SetHigh(register, word);
            return;
        }

        switch (register)
        {
            case (int)Register.St: SetHigh(register, word); break;
            case (int)Register.Stack: WriteStack(word); break;
            case (int)Register.Pc: WritePc(word); break;
            case (int)Register.Pm0: WritePm(0, word); break;
            case (int)Register.Pm1: WritePm(1, word); break;
            case (int)Register.Pm2: WritePm(2, word); break;
            case (int)Register.Xst: WriteXst(word); break;
            case (int)Register.Pm4: WritePm(4, word); break;
            case (int)Register.Pmc: WritePmc(word); break;
            case (int)Register.Al: SetLow((int)Register.A, word); break;
        }
    }

    private uint ReadStack()
    {
        ushort stack = High((int)Register.Stack);
        stack--;
        if ((short)stack < 0)
        {
            stack = 5;
        }

        SetHigh((int)Register.Stack, stack);
        return _stack[stack];
    }

    private void WriteStack(ushort value)
    {
        ushort stack = High((int)Register.Stack);
        if (stack >= _stack.Length)
        {
            stack = 0;
        }

        _stack[stack] = value;
        SetHigh((int)Register.Stack, (ushort)(stack + 1));
    }

    private void WritePc(ushort value)
    {
        _pc = value;
        ConsumeExtraCycles(1);
    }

    private void ConsumeExtraCycles(int cycles)
    {
        _cycles -= cycles;
    }

    private void ConsumeMameExtraCycles(int cycles)
    {
        if (UseMameCycleTiming)
        {
            ConsumeExtraCycles(cycles);
        }
    }

    private uint ReadP()
    {
        int x = (short)High((int)Register.X);
        int y = (short)High((int)Register.Y);
        _gr[(int)Register.P] = (uint)(x * y * 2);
        return High((int)Register.P);
    }

    private uint ReadPm0()
    {
        uint value = PmIo(0, write: false, 0);
        if (value != uint.MaxValue)
        {
            return value;
        }

        value = High((int)Register.Pm0);
        if ((value & 2) == 0 && (_lastOpByteOffset == 0x0800 || _lastOpByteOffset == 0x1851E))
        {
            _emuStatus |= WaitPm0;
        }

        SetHigh((int)Register.Pm0, (ushort)(High((int)Register.Pm0) & ~2));
        return value;
    }

    private uint ReadPm(int register)
    {
        uint value = PmIo(register, write: false, 0);
        return value == uint.MaxValue ? High((int)Register.Pm0 + register) : value;
    }

    private void WritePm(int register, ushort value)
    {
        if (PmIo(register, write: true, value) == uint.MaxValue)
        {
            SetHigh((int)Register.Pm0 + register, value);
        }
    }

    private uint ReadXst()
    {
        uint value = PmIo(3, write: false, 0);
        return value == uint.MaxValue ? High((int)Register.Xst) : value;
    }

    private void WriteXst(ushort value)
    {
        if (PmIo(3, write: true, value) != uint.MaxValue)
        {
            return;
        }

        SetHigh((int)Register.Pm0, (ushort)(High((int)Register.Pm0) | 1));
        SetHigh((int)Register.Xst, value);
    }

    private uint ReadPm4()
    {
        uint value = PmIo(4, write: false, 0);
        if (value == 0)
        {
            if (_lastOpByteOffset == 0x0854)
            {
                _emuStatus |= Wait30Fe08;
            }
            else if (_lastOpByteOffset == 0x4F12)
            {
                _emuStatus |= Wait30Fe06;
            }
        }

        return value == uint.MaxValue ? High((int)Register.Pm4) : value;
    }

    private uint ReadPmc()
    {
        if ((_emuStatus & PmcHaveAddress) != 0)
        {
            _emuStatus |= PmcSet;
            _emuStatus &= ~PmcHaveAddress;
            ushort addr = Low((int)Register.Pmc);
            return (uint)(((addr << 4) & 0xFFF0) | ((addr >> 4) & 0x000F));
        }

        _emuStatus |= PmcHaveAddress;
        return Low((int)Register.Pmc);
    }

    private void WritePmc(ushort value)
    {
        if ((_emuStatus & PmcHaveAddress) != 0)
        {
            _emuStatus |= PmcSet;
            _emuStatus &= ~PmcHaveAddress;
            SetHigh((int)Register.Pmc, value);
        }
        else
        {
            _emuStatus |= PmcHaveAddress;
            SetLow((int)Register.Pmc, value);
        }
    }

    private uint ReadAl()
    {
        if (ClearPmcOnAnyAlRead || _lastOp == 0x000F)
        {
            _emuStatus &= ~(PmcSet | PmcHaveAddress);
        }

        if (ReturnZeroOnAlRead)
        {
            return 0;
        }

        return Low((int)Register.A);
    }

    private uint PmIo(int register, bool write, uint data)
    {
        Action<SvpPmIoTrace>? pmIoObserver = PmIoObserver;
        if ((_emuStatus & PmcSet) != 0)
        {
            if (RequireBlindPmacSet && ((_lastOp & 0xFF0F) != 0) && ((_lastOp & 0xFFF0) != 0))
            {
                EmitPmIoTrace(pmIoObserver, register, write, "RejectedPmacSet", 0, 0, 0, data, 0, 0, _gr[(int)Register.Pmc], _gr[(int)Register.Pmc]);
                _emuStatus &= ~PmcSet;
                return 0;
            }

            uint pmc = _gr[(int)Register.Pmc];
            _pmac[write ? 1 : 0, register] = _gr[(int)Register.Pmc];
            _emuStatus &= ~PmcSet;
            EmitPmIoTrace(pmIoObserver, register, write, "PmacSet", (ushort)(pmc >> 16), (ushort)pmc, (ushort)pmc, data, 0, 0, pmc, pmc);
            return 0;
        }

        if ((_emuStatus & PmcHaveAddress) != 0)
        {
            _emuStatus &= ~PmcHaveAddress;
        }

        if (register == 4 || (High((int)Register.St) & 0x60) != 0)
        {
            if (write)
            {
                uint pmacBefore = _pmac[1, register];
                int addr = (int)(_pmac[1, register] & 0xFFFF);
                int mode = (int)(_pmac[1, register] >> 16);
                if ((mode & 0x43FF) == 0x0018)
                {
                    _pmacWriteCounts[(int)PmacWriteKind.DramLinear]++;
                    ushort previous = _dram[addr & 0xFFFF];
                    WriteDramWord(addr, (ushort)data, overwrite: (mode & 0x0400) != 0);
                    ushort stored = _dram[addr & 0xFFFF];
                    TrackDramWrite(addr, mode, (ushort)data, previous, stored, PmacWriteKind.DramLinear);
                    _pmac[1, register] = (uint)(_pmac[1, register] + GetIncrement(mode));
                    EmitPmIoTrace(pmIoObserver, register, write, nameof(PmacWriteKind.DramLinear), (ushort)mode, addr, (int)_pmac[1, register], data, previous, stored, pmacBefore, _pmac[1, register]);
                }
                else if ((mode & 0xFBFF) == 0x4018)
                {
                    _pmacWriteCounts[(int)PmacWriteKind.DramCell]++;
                    ushort previous = _dram[addr & 0xFFFF];
                    WriteDramWord(addr, (ushort)data, overwrite: (mode & 0x0400) != 0);
                    ushort stored = _dram[addr & 0xFFFF];
                    TrackDramWrite(addr, mode, (ushort)data, previous, stored, PmacWriteKind.DramCell);
                    _pmac[1, register] += (uint)((addr & 1) != 0 ? 31 : 1);
                    EmitPmIoTrace(pmIoObserver, register, write, nameof(PmacWriteKind.DramCell), (ushort)mode, addr, (int)_pmac[1, register], data, previous, stored, pmacBefore, _pmac[1, register]);
                }
                else if ((mode & 0x47FF) == 0x001C)
                {
                    _pmacWriteCounts[(int)PmacWriteKind.Iram]++;
                    ushort previous = _iram[addr & 0x03FF];
                    _iram[addr & 0x03FF] = (ushort)data;
                    _pmac[1, register] = (uint)(_pmac[1, register] + GetIncrement(mode));
                    EmitPmIoTrace(pmIoObserver, register, write, nameof(PmacWriteKind.Iram), (ushort)mode, addr, (int)_pmac[1, register], data, previous, (ushort)data, pmacBefore, _pmac[1, register]);
                }
                else
                {
                    _pmacWriteCounts[(int)PmacWriteKind.Unhandled]++;
                    EmitPmIoTrace(pmIoObserver, register, write, nameof(PmacWriteKind.Unhandled), (ushort)mode, addr, addr, data, 0, 0, pmacBefore, pmacBefore);
                }
            }
            else
            {
                uint pmacBefore = _pmac[0, register];
                int addr = (int)(_pmac[0, register] & 0xFFFF);
                int mode = (int)(_pmac[0, register] >> 16);
                if ((mode & 0xFFF0) == 0x0800)
                {
                    _pmacReadCounts[(int)PmacReadKind.Rom]++;
                    _pmac[0, register]++;
                    data = ReadRomWordByWordOffset((uint)(addr | ((mode & 0x0F) << 16)));
                    EmitPmIoTrace(pmIoObserver, register, write, nameof(PmacReadKind.Rom), (ushort)mode, addr, (int)_pmac[0, register], data, 0, (ushort)data, pmacBefore, _pmac[0, register]);
                }
                else if ((mode & 0x47FF) == 0x0018)
                {
                    _pmacReadCounts[(int)PmacReadKind.Dram]++;
                    data = _dram[addr & 0xFFFF];
                    _pmac[0, register] = (uint)(_pmac[0, register] + GetIncrement(mode));
                    EmitPmIoTrace(pmIoObserver, register, write, nameof(PmacReadKind.Dram), (ushort)mode, addr, (int)_pmac[0, register], data, (ushort)data, (ushort)data, pmacBefore, _pmac[0, register]);
                }
                else
                {
                    _pmacReadCounts[(int)PmacReadKind.Unhandled]++;
                    data = 0;
                    EmitPmIoTrace(pmIoObserver, register, write, nameof(PmacReadKind.Unhandled), (ushort)mode, addr, addr, data, 0, 0, pmacBefore, pmacBefore);
                }
            }

            _gr[(int)Register.Pmc] = _pmac[write ? 1 : 0, register];
            return data;
        }

        return uint.MaxValue;
    }

    private void EmitPmIoTrace(Action<SvpPmIoTrace>? observer, int register, bool write, string kind, ushort mode, int addressBefore, int addressAfter, uint data, ushort previousValue, ushort storedValue, uint pmacBefore, uint pmacAfter)
    {
        observer?.Invoke(new SvpPmIoTrace(
            _lastOpByteOffset,
            _lastOp,
            register,
            write,
            mode,
            addressBefore & 0xFFFF,
            addressAfter & 0xFFFF,
            (ushort)data,
            previousValue,
            storedValue,
            pmacBefore,
            pmacAfter,
            _emuStatus,
            _gr[(int)Register.A],
            High((int)Register.X),
            High((int)Register.Y),
            High((int)Register.St),
            PackPointers(),
            kind));
    }

    private void WriteDramWord(int wordAddress, ushort value, bool overwrite)
    {
        int index = wordAddress & 0xFFFF;
        if (overwrite)
        {
            ushort current = _dram[index];
            if ((value & 0xF000) != 0) current = (ushort)((current & ~0xF000) | (value & 0xF000));
            if ((value & 0x0F00) != 0) current = (ushort)((current & ~0x0F00) | (value & 0x0F00));
            if ((value & 0x00F0) != 0) current = (ushort)((current & ~0x00F0) | (value & 0x00F0));
            if ((value & 0x000F) != 0) current = (ushort)((current & ~0x000F) | (value & 0x000F));
            _dram[index] = current;
        }
        else
        {
            _dram[index] = value;
        }
    }

    private void TrackDramWrite(int wordAddress, int mode, ushort value, ushort previousValue, ushort storedValue, PmacWriteKind kind)
    {
        if (!EnableDramWriteDiagnostics)
        {
            return;
        }

        DramWriteTraceKey key = new(
            (wordAddress & 0xFFFF) >> 8,
            _lastOpByteOffset,
            _lastOp,
            mode & 0xFFFF,
            kind,
            (mode & 0x0400) != 0);
        _dramWriteTraces.TryGetValue(key, out DramWriteTraceAccumulator accumulator);
        accumulator.Count++;
        accumulator.LastWordAddress = wordAddress & 0xFFFF;
        accumulator.LastValue = value;
        _dramWriteTraces[key] = accumulator;

        _dramWriteSamples[_dramWriteSampleIndex] = new DramWriteSample(
            ++_dramWriteSampleSequence,
            wordAddress & 0xFFFF,
            _lastOpByteOffset,
            _lastOp,
            (ushort)(mode & 0xFFFF),
            kind.ToString(),
            (mode & 0x0400) != 0,
            previousValue,
            value,
            storedValue,
            High((int)Register.A),
            Low((int)Register.A),
            High((int)Register.X),
            High((int)Register.Y),
            High((int)Register.St));
        _dramWriteSampleIndex = (_dramWriteSampleIndex + 1) & (_dramWriteSamples.Length - 1);
    }

    private static bool TryMapAddressToDramWord(uint address, out int wordAddress)
    {
        address &= 0x00FF_FFFE;
        if (address is >= 0x30_0000 and <= 0x31_FFFE)
        {
            wordAddress = (int)((address & 0x1_FFFE) >> 1);
            return true;
        }

        if (address is >= 0x39_0000 and <= 0x39_FFFE)
        {
            uint offset = (address - 0x39_0000) >> 1;
            wordAddress = (int)((offset & 0x7001) | ((offset & 0x003E) << 6) | ((offset & 0x0FC0) >> 5));
            return true;
        }

        if (address is >= 0x3A_0000 and <= 0x3A_FFFE)
        {
            uint offset = (address - 0x3A_0000) >> 1;
            wordAddress = (int)((offset & 0x7801) | ((offset & 0x001E) << 6) | ((offset & 0x07E0) >> 4));
            return true;
        }

        wordAddress = 0;
        return false;
    }

    private static int GetIncrement(int mode)
    {
        int increment = (mode >> 11) & 7;
        if (increment == 0)
        {
            return 0;
        }

        if (increment != 7)
        {
            increment--;
        }

        int value = 1 << increment;
        return (mode & 0x8000) != 0 ? -value : value;
    }

    private uint Ptr1Read(ushort op)
    {
        return Ptr1ReadRaw(op & 3, (op >> 6) & 4, (op << 1) & 0x18);
    }

    private uint Ptr1ReadRaw(int ri, int isj2, int modi3)
    {
        int t = ri | isj2 | modi3;
        int bankBase = (t & 4) == 0 ? 0 : 0x100;
        int pointer = (t & 4) == 0 ? t & 3 : 4 + (t & 3);
        byte pointerBefore = _pointers[pointer];
        int ramAddress;
        ushort value;
        switch (t)
        {
            case 0x00:
            case 0x01:
            case 0x02:
            case 0x04:
            case 0x05:
            case 0x06:
                ramAddress = bankBase + _pointers[pointer];
                value = _ram[ramAddress];
                EmitPointerTrace("Ptr1Read", t, bankBase, pointer, pointerBefore, _pointers[pointer], ramAddress, value);
                return value;
            case 0x03:
            case 0x07:
                ramAddress = bankBase;
                value = _ram[ramAddress];
                EmitPointerTrace("Ptr1ReadFixed0", t, bankBase, pointer, pointerBefore, _pointers[pointer], ramAddress, value);
                return value;
            case 0x08:
            case 0x09:
            case 0x0A:
            case 0x0C:
            case 0x0D:
            case 0x0E:
                ramAddress = bankBase + _pointers[pointer]++;
                value = _ram[ramAddress];
                EmitPointerTrace("Ptr1ReadPostIncrement", t, bankBase, pointer, pointerBefore, _pointers[pointer], ramAddress, value);
                return value;
            case 0x0B:
            case 0x0F:
                ramAddress = bankBase + 1;
                value = _ram[ramAddress];
                EmitPointerTrace("Ptr1ReadFixed1", t, bankBase, pointer, pointerBefore, _pointers[pointer], ramAddress, value);
                return value;
            case 0x10:
            case 0x11:
            case 0x12:
            case 0x14:
            case 0x15:
            case 0x16:
            {
                ramAddress = bankBase + _pointers[pointer];
                value = _ram[ramAddress];
                MovePointer(pointer, -1);
                EmitPointerTrace("Ptr1ReadDecrement", t, bankBase, pointer, pointerBefore, _pointers[pointer], ramAddress, value);
                return value;
            }
            case 0x13:
            case 0x17:
                ramAddress = bankBase + 2;
                value = _ram[ramAddress];
                EmitPointerTrace("Ptr1ReadFixed2", t, bankBase, pointer, pointerBefore, _pointers[pointer], ramAddress, value);
                return value;
            case 0x18:
            case 0x19:
            case 0x1A:
            case 0x1C:
            case 0x1D:
            case 0x1E:
            {
                ramAddress = bankBase + _pointers[pointer];
                value = _ram[ramAddress];
                MovePointer(pointer, 1);
                EmitPointerTrace("Ptr1ReadIncrement", t, bankBase, pointer, pointerBefore, _pointers[pointer], ramAddress, value);
                return value;
            }
            case 0x1B:
            case 0x1F:
                ramAddress = bankBase + 3;
                value = _ram[ramAddress];
                EmitPointerTrace("Ptr1ReadFixed3", t, bankBase, pointer, pointerBefore, _pointers[pointer], ramAddress, value);
                return value;
            default:
                EmitPointerTrace("Ptr1ReadUnhandled", t, bankBase, pointer, pointerBefore, _pointers[pointer], 0, 0);
                return 0;
        }
    }

    private void Ptr1Write(ushort op, uint data)
    {
        int t = (op & 3) | ((op >> 6) & 4) | ((op << 1) & 0x18);
        int bankBase = (t & 4) == 0 ? 0 : 0x100;
        int pointer = (t & 4) == 0 ? t & 3 : 4 + (t & 3);
        byte pointerBefore = _pointers[pointer];
        ushort value = (ushort)data;
        int ramAddress;
        switch (t)
        {
            case 0x00:
            case 0x01:
            case 0x02:
            case 0x04:
            case 0x05:
            case 0x06:
                ramAddress = bankBase + _pointers[pointer];
                _ram[ramAddress] = value;
                EmitPointerTrace("Ptr1Write", t, bankBase, pointer, pointerBefore, _pointers[pointer], ramAddress, value);
                break;
            case 0x03:
            case 0x07:
                ramAddress = bankBase;
                _ram[ramAddress] = value;
                EmitPointerTrace("Ptr1WriteFixed0", t, bankBase, pointer, pointerBefore, _pointers[pointer], ramAddress, value);
                break;
            case 0x08:
            case 0x09:
            case 0x0A:
            case 0x0C:
            case 0x0D:
            case 0x0E:
                ramAddress = bankBase + _pointers[pointer]++;
                _ram[ramAddress] = value;
                EmitPointerTrace("Ptr1WritePostIncrement", t, bankBase, pointer, pointerBefore, _pointers[pointer], ramAddress, value);
                break;
            case 0x0B:
            case 0x0F:
                ramAddress = bankBase + 1;
                _ram[ramAddress] = value;
                EmitPointerTrace("Ptr1WriteFixed1", t, bankBase, pointer, pointerBefore, _pointers[pointer], ramAddress, value);
                break;
            case 0x10:
            case 0x11:
            case 0x12:
            case 0x14:
            case 0x15:
            case 0x16:
                ramAddress = bankBase + _pointers[pointer];
                _ram[ramAddress] = value;
                MovePointerForWrite(pointer, -1);
                EmitPointerTrace("Ptr1WriteDecrement", t, bankBase, pointer, pointerBefore, _pointers[pointer], ramAddress, value);
                break;
            case 0x13:
            case 0x17:
                ramAddress = bankBase + 2;
                _ram[ramAddress] = value;
                EmitPointerTrace("Ptr1WriteFixed2", t, bankBase, pointer, pointerBefore, _pointers[pointer], ramAddress, value);
                break;
            case 0x1B:
            case 0x1F:
                ramAddress = bankBase + 3;
                _ram[ramAddress] = value;
                EmitPointerTrace("Ptr1WriteFixed3", t, bankBase, pointer, pointerBefore, _pointers[pointer], ramAddress, value);
                break;
            case 0x18:
            case 0x19:
            case 0x1A:
            case 0x1C:
            case 0x1D:
            case 0x1E:
                ramAddress = bankBase + _pointers[pointer];
                _ram[ramAddress] = value;
                MovePointerForWrite(pointer, 1);
                EmitPointerTrace("Ptr1WriteIncrement", t, bankBase, pointer, pointerBefore, _pointers[pointer], ramAddress, value);
                break;
        }
    }

    private void MovePointerForWrite(int pointer, int delta)
    {
        if (UseModuloOnPointerWrites)
        {
            MovePointer(pointer, delta);
            return;
        }

        _pointers[pointer] = (byte)(_pointers[pointer] + delta);
    }

    private uint Ptr2Read(ushort op)
    {
        int t = (op & 3) | ((op >> 6) & 4) | ((op << 1) & 0x18);
        int bankBase = (t & 4) == 0 ? 0 : 0x100;
        int pointer = (t & 4) == 0 ? t & 3 : 4 + (t & 3);
        byte pointerBefore = _pointers[pointer];
        int memoryVector;
        int ramAddress;
        switch (t)
        {
            case 0x00:
            case 0x01:
            case 0x02:
            case 0x04:
            case 0x05:
            case 0x06:
                ramAddress = bankBase + _pointers[pointer];
                memoryVector = _ram[ramAddress]++;
                break;
            case 0x03:
            case 0x07:
                ramAddress = bankBase;
                memoryVector = _ram[ramAddress]++;
                break;
            case 0x0B:
            case 0x0F:
                ramAddress = bankBase + 1;
                memoryVector = _ram[ramAddress]++;
                break;
            case 0x13:
            case 0x17:
                ramAddress = bankBase + 2;
                memoryVector = _ram[ramAddress]++;
                break;
            case 0x1B:
            case 0x1F:
                ramAddress = bankBase + 3;
                memoryVector = _ram[ramAddress]++;
                break;
            default:
                EmitPointerTrace("Ptr2ReadUnhandled", t, bankBase, pointer, pointerBefore, _pointers[pointer], 0, 0, null, null);
                return 0;
        }

        ushort value = _iram[memoryVector & 0xFFFF];
        EmitPointerTrace("Ptr2Read", t, bankBase, pointer, pointerBefore, _pointers[pointer], ramAddress, (ushort)_ram[ramAddress], memoryVector, value);
        return value;
    }

    private void EmitPointerTrace(string operation, int modifier, int bankBase, int pointer, byte pointerBefore, byte pointerAfter, int ramAddress, ushort value, int? indirectAddress = null, ushort? indirectValue = null)
    {
        PointerObserver?.Invoke(new SvpPointerTrace(
            _lastOpByteOffset,
            _lastOp,
            operation,
            modifier,
            bankBase,
            pointer,
            pointerBefore,
            pointerAfter,
            ramAddress & 0x01FF,
            value,
            indirectAddress.HasValue ? indirectAddress.Value & 0xFFFF : -1,
            indirectValue ?? 0,
            High((int)Register.St) & 7,
            _emuStatus,
            _gr[(int)Register.A],
            High((int)Register.X),
            High((int)Register.Y),
            High((int)Register.St),
            _gr[(int)Register.P],
            PackPointers()));
    }

    private void MovePointer(int pointer, int delta)
    {
        int rpl = High((int)Register.St) & 7;
        if (rpl == 0)
        {
            _pointers[pointer] = (byte)(_pointers[pointer] + delta);
            return;
        }

        int mask = (1 << rpl) - 1;
        _pointers[pointer] = (byte)((_pointers[pointer] & ~mask) | ((_pointers[pointer] + delta) & mask));
    }

    private bool Condition(ushort op)
    {
        return (op & 0xF0) switch
        {
            0x00 => true,
            0x50 => ((High((int)Register.St) ^ (op << 5)) & FlagZ) == 0,
            0x70 => ((High((int)Register.St) ^ (op << 7)) & FlagN) == 0,
            _ => false,
        };
    }

    private bool OpCheck32(ushort op, Action<uint> operation)
    {
        int source = op & 0x0F;
        if (source == (int)Register.P)
        {
            ReadP();
            operation(_gr[(int)Register.P]);
            return true;
        }

        if (source == (int)Register.A)
        {
            operation(_gr[(int)Register.A]);
            return true;
        }

        return false;
    }

    private void LoadA(uint value)
    {
        SetHigh((int)Register.A, (ushort)value);
    }

    private void SubA(uint value)
    {
        _gr[(int)Register.A] -= value << 16;
        UpdateLzvn();
    }

    private void SubA32(uint value)
    {
        _gr[(int)Register.A] -= value;
        UpdateLzvn();
    }

    private void CmpA(uint value)
    {
        uint temp = _gr[(int)Register.A] - (value << 16);
        UpdateLzvnFrom(temp);
    }

    private void CmpA32(uint value)
    {
        uint temp = _gr[(int)Register.A] - value;
        UpdateLzvnFrom(temp);
    }

    private void AddA(uint value)
    {
        _gr[(int)Register.A] += value << 16;
        UpdateLzvn();
    }

    private void AddA32(uint value)
    {
        _gr[(int)Register.A] += value;
        UpdateLzvn();
    }

    private void AndA(uint value)
    {
        _gr[(int)Register.A] &= value << 16;
        UpdateAccumulatorZn();
    }

    private void AndA32(uint value)
    {
        _gr[(int)Register.A] &= value;
        UpdateAccumulatorZn();
    }

    private void OrA(uint value)
    {
        _gr[(int)Register.A] |= value << 16;
        UpdateAccumulatorZn();
    }

    private void OrA32(uint value)
    {
        _gr[(int)Register.A] |= value;
        UpdateAccumulatorZn();
    }

    private void EorA(uint value)
    {
        _gr[(int)Register.A] ^= value << 16;
        UpdateAccumulatorZn();
    }

    private void EorA32(uint value)
    {
        _gr[(int)Register.A] ^= value;
        UpdateAccumulatorZn();
    }

    private void UpdateAccumulatorZn()
    {
        ushort st = (ushort)(High((int)Register.St) & ~(FlagZ | FlagN));
        uint a = _gr[(int)Register.A];
        if (a == 0)
        {
            st |= FlagZ;
        }
        else
        {
            st |= (ushort)((a >> 16) & FlagN);
        }

        SetHigh((int)Register.St, st);
    }

    private void UpdateLzvn()
    {
        UpdateLzvnFrom(_gr[(int)Register.A]);
    }

    private void UpdateLzvnFrom(uint value)
    {
        ushort st = (ushort)(High((int)Register.St) & ~(FlagL | FlagZ | FlagV | FlagN));
        if (value == 0)
        {
            st |= FlagZ;
        }
        else
        {
            st |= (ushort)((value >> 16) & FlagN);
        }

        SetHigh((int)Register.St, st);
    }

    private ushort ReadRomWordByByteOffset(int byteOffset)
    {
        if ((uint)(byteOffset + 1) >= _rom.Length)
        {
            return 0xFFFF;
        }

        return (ushort)((_rom[byteOffset] << 8) | _rom[byteOffset + 1]);
    }

    private SvpInstructionSnapshot CaptureInstructionSnapshot()
    {
        return new SvpInstructionSnapshot(
            _pc & 0xFFFF,
            _cycles,
            _emuStatus,
            _gr[(int)Register.X],
            _gr[(int)Register.Y],
            _gr[(int)Register.A],
            _gr[(int)Register.St],
            _gr[(int)Register.P],
            _gr[(int)Register.Pm0],
            _gr[(int)Register.Pm1],
            _gr[(int)Register.Pm2],
            _gr[(int)Register.Xst],
            _gr[(int)Register.Pm4],
            _gr[(int)Register.Pmc],
            PackPointers());
    }

    private ulong PackPointers()
    {
        ulong packed = 0;
        for (int i = 0; i < _pointers.Length; i++)
        {
            packed |= (ulong)_pointers[i] << (i * 8);
        }

        return packed;
    }

    private ushort ReadRomWordByWordOffset(uint wordOffset)
    {
        uint byteOffset = wordOffset << 1;
        if (byteOffset + 1 >= _rom.Length)
        {
            return 0xFFFF;
        }

        return (ushort)((_rom[byteOffset] << 8) | _rom[byteOffset + 1]);
    }

    private void WriteHostCommand(ushort value)
    {
        SetHigh((int)Register.Xst, value);
        SetHigh((int)Register.Pm0, (ushort)(High((int)Register.Pm0) | 2));
        _emuStatus &= ~WaitPm0;
    }

    private void ClearDramWait(uint address, ushort value)
    {
        if (value == 0)
        {
            return;
        }

        if (address == 0x30_FE06)
        {
            _emuStatus &= ~Wait30Fe06;
        }
        else if (address == 0x30_FE08)
        {
            _emuStatus &= ~Wait30Fe08;
        }
    }

    public sealed record SvpState(
        ushort[] Iram,
        ushort[] Ram,
        ushort[] Dram,
        uint[] Gr,
        byte[] Pointers,
        ushort[] Stack,
        uint[] Pmac,
        uint EmuStatus,
        int Pc,
        int Cycles,
        ushort LastOp,
        int LastOpByteOffset,
        uint UnhandledOpcodeCount,
        ushort LastUnhandledOpcode,
        int LastUnhandledPc);

    public readonly record struct PmacDiagnostics(
        ulong DramLinearWrites,
        ulong DramCellWrites,
        ulong IramWrites,
        ulong UnhandledWrites,
        ulong RomReads,
        ulong DramReads,
        ulong UnhandledReads);

    public readonly record struct DramWriteDiagnostic(
        int BucketStartWord,
        int Pc,
        ushort Opcode,
        ushort Mode,
        string Kind,
        bool Overwrite,
        ulong Count,
        int LastWordAddress,
        ushort LastValue);

    public readonly record struct DramWriteSample(
        ulong Sequence,
        int WordAddress,
        int Pc,
        ushort Opcode,
        ushort Mode,
        string Kind,
        bool Overwrite,
        ushort PreviousValue,
        ushort WrittenValue,
        ushort StoredValue,
        ushort AccumulatorHigh,
        ushort AccumulatorLow,
        ushort X,
        ushort Y,
        ushort Status);

    public readonly record struct SvpInstructionTrace(
        int Pc,
        ushort Opcode,
        ushort NextWord,
        SvpInstructionSnapshot Before,
        SvpInstructionSnapshot After);

    public readonly record struct SvpInstructionSnapshot(
        int Pc,
        int CyclesRemaining,
        uint EmuStatus,
        uint X,
        uint Y,
        uint A,
        uint St,
        uint P,
        uint Pm0,
        uint Pm1,
        uint Pm2,
        uint Xst,
        uint Pm4,
        uint Pmc,
        ulong Pointers);

    public readonly record struct SvpPmIoTrace(
        int Pc,
        ushort Opcode,
        int Register,
        bool Write,
        ushort Mode,
        int AddressBefore,
        int AddressAfter,
        ushort Data,
        ushort PreviousValue,
        ushort StoredValue,
        uint PmacBefore,
        uint PmacAfter,
        uint EmuStatus,
        uint A,
        ushort X,
        ushort Y,
        ushort St,
        ulong Pointers,
        string Kind);

    public readonly record struct SvpPointerTrace(
        int Pc,
        ushort Opcode,
        string Operation,
        int Modifier,
        int BankBase,
        int Pointer,
        byte PointerBefore,
        byte PointerAfter,
        int RamAddress,
        ushort Value,
        int IndirectAddress,
        ushort IndirectValue,
        int Rpl,
        uint EmuStatus,
        uint A,
        ushort X,
        ushort Y,
        ushort St,
        uint P,
        ulong Pointers);

    private readonly record struct DramWriteTraceKey(
        int Bucket,
        int Pc,
        int Opcode,
        int Mode,
        PmacWriteKind Kind,
        bool Overwrite);

    private struct DramWriteTraceAccumulator
    {
        public ulong Count;
        public int LastWordAddress;
        public ushort LastValue;
    }

    private enum PmacWriteKind
    {
        DramLinear,
        DramCell,
        Iram,
        Unhandled,
    }

    private enum PmacReadKind
    {
        Rom,
        Dram,
        Unhandled,
    }
}
