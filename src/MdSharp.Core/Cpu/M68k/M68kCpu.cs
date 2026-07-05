using MdSharp.Core.Bus;

namespace MdSharp.Core.Cpu.M68k;

public sealed class M68kCpu
{
    private readonly IMemoryBus _bus;
    private readonly IInstructionTraceSink? _traceSink;
    private const int IllegalInstructionVector = 4;
    private const int DivideByZeroVector = 5;
    private const int ChkInstructionVector = 6;
    private const int PrivilegeViolationVector = 8;
    private const int LineAEmulatorVector = 10;
    private const int LineFEmulatorVector = 11;
    private const uint AddressMask = 0x00FF_FFFF;

    public M68kCpu(IMemoryBus bus)
    {
        _bus = bus;
        _traceSink = bus as IInstructionTraceSink;
    }

    private enum OperandSize
    {
        Byte,
        Word,
        Long,
    }

    private enum LogicOperation
    {
        And,
        Or,
        Eor,
    }

    private sealed class M68kCpuException(int vector) : Exception
    {
        public int Vector { get; } = vector;
    }

    public uint[] D { get; } = new uint[8];
    public uint[] A { get; } = new uint[8];
    public uint PC { get; private set; }
    public ushort SR { get; private set; }
    public bool Stopped { get; private set; }
    public long Cycles { get; private set; }
    public uint USP { get; private set; }
    public bool TraceEnabled { get; set; }
    public bool HistoryEnabled { get; set; }
    public Action<M68kInstructionTrace>? InstructionObserver { get; set; }
    public Action<M68kInterruptTrace>? InterruptObserver { get; set; }
    public Action<M68kExceptionTrace>? ExceptionObserver { get; set; }
    public Func<uint, ushort, bool>? LineFInstructionOverride { get; set; }
    public Func<uint, ushort, bool>? TrapInstructionOverride { get; set; }
    public bool AllocationProfilingEnabled
    {
        get => _allocationProfilingEnabled;
        set
        {
            _allocationProfilingEnabled = value;
            if (value)
            {
                EnsureAllocationProfile();
            }
        }
    }
    public IReadOnlyDictionary<int, long> ExceptionCounts => _exceptionCounts;
    public IReadOnlyList<string> ExceptionTrace => _exceptionTrace;
    public IReadOnlyCollection<string> RecentInstructionTrace => _recentInstructionTrace;

    private readonly Dictionary<int, long> _exceptionCounts = new();
    private readonly List<string> _exceptionTrace = new();
    private readonly Queue<string> _recentInstructionTrace = new();
    private bool _allocationProfilingEnabled;
    private long[]? _allocatedBytesByOpcode;
    private int[]? _allocationSamplesByOpcode;
    private uint _currentOpcodeAddress;
    private ushort _currentOpcode;

    public void Reset()
    {
        Array.Clear(D);
        Array.Clear(A);
        uint initialStack = _bus.ReadLong(0);
        uint initialPc = _bus.ReadLong(4);
        if (initialStack == 0x444E_4C44 && initialPc == 0)
        {
            initialStack = _bus.ReadLong(8);
            initialPc = _bus.ReadLong(12);
        }

        A[7] = initialStack;
        PC = NormalizePc(initialPc);
        SR = 0x2700;
        USP = 0;
        _exceptionCounts.Clear();
        _exceptionTrace.Clear();
        _recentInstructionTrace.Clear();
        Stopped = false;
        Cycles = 0;
    }

    public M68kState CaptureState()
    {
        return new M68kState((uint[])D.Clone(), (uint[])A.Clone(), PC, SR, Stopped, Cycles, USP);
    }

    public void RestoreState(M68kState state)
    {
        Array.Copy(state.D, D, Math.Min(D.Length, state.D.Length));
        Array.Copy(state.A, A, Math.Min(A.Length, state.A.Length));
        PC = NormalizePc(state.PC);
        SR = state.SR;
        Stopped = state.Stopped;
        Cycles = state.Cycles;
        USP = state.USP;
    }

    public void AddWaitCycles(int cycles)
    {
        if (cycles > 0)
        {
            Cycles += cycles;
        }
    }

    public int Step()
    {
        if (Stopped)
        {
            Cycles += 4;
            return 4;
        }

        uint opcodeAddress = PC;
        if (_traceSink is not null)
        {
            _traceSink.CurrentM68kPc = opcodeAddress;
        }

        ushort opcode = FetchWord();
        _currentOpcodeAddress = opcodeAddress;
        _currentOpcode = opcode;

        if (TraceEnabled || HistoryEnabled)
        {
            if (_recentInstructionTrace.Count >= 65536)
            {
                _recentInstructionTrace.Dequeue();
            }

            _recentInstructionTrace.Enqueue(
                $"pc=${opcodeAddress:X6} opcode=${opcode:X4} ext0=${_bus.ReadWord(opcodeAddress + 2):X4} ext1=${_bus.ReadWord(opcodeAddress + 4):X4} ext2=${_bus.ReadWord(opcodeAddress + 6):X4} sr=${SR:X4} sp=${A[7]:X8} " +
                $"d0=${D[0]:X8} d1=${D[1]:X8} d2=${D[2]:X8} d3=${D[3]:X8} " +
                $"a0=${A[0]:X8} a1=${A[1]:X8} a2=${A[2]:X8} a3=${A[3]:X8} " +
                $"a4=${A[4]:X8} a5=${A[5]:X8} a6=${A[6]:X8}");
        }

        int cycles;
        try
        {
            if (AllocationProfilingEnabled)
            {
                long[] allocatedBytesByOpcode = _allocatedBytesByOpcode!;
                int[] allocationSamplesByOpcode = _allocationSamplesByOpcode!;
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                cycles = Execute(opcode, opcodeAddress);
                allocatedBytesByOpcode[opcode] += GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                allocationSamplesByOpcode[opcode]++;
            }
            else
            {
                cycles = Execute(opcode, opcodeAddress);
            }
        }
        catch (M68kCpuException ex)
        {
            EnterException(ex.Vector);
            cycles = 34;
        }

        Cycles += cycles;
        InstructionObserver?.Invoke(new M68kInstructionTrace(opcodeAddress, opcode, PC, SR, A[7], D[0], D[1], D[2], D[3], D[4], D[5], D[6], D[7], A[0], A[1], A[2], A[3], A[4], A[5], A[6], cycles));
        return cycles;
    }

    public bool TryFastForwardMoveBytePostIncrementDbfLoop(int cycleBudget, out int cycles, out int instructionCount)
    {
        cycles = 0;
        instructionCount = 0;
        if (Stopped || TraceEnabled || InstructionObserver is not null || cycleBudget < 18)
        {
            return false;
        }

        ushort move = _bus.ReadWord(PC);
        if ((move & 0xF1F8) != 0x10C0)
        {
            return false;
        }

        ushort dbcc = _bus.ReadWord(PC + 2);
        if ((dbcc & 0xFFF8) != 0x51C8)
        {
            return false;
        }

        short displacement = (short)_bus.ReadWord(PC + 4);
        if (displacement != -4)
        {
            return false;
        }

        int counterRegister = dbcc & 0x07;
        ushort counter = (ushort)(D[counterRegister] & 0xFFFF);
        if (counter == 0)
        {
            return false;
        }

        int maxTakenIterations = cycleBudget / 18;
        int iterations = Math.Min(counter, maxTakenIterations);
        if (iterations <= 0)
        {
            return false;
        }

        int sourceRegister = move & 0x07;
        int addressRegister = (move >> 9) & 0x07;
        byte value = (byte)D[sourceRegister];
        uint address = A[addressRegister];
        for (int i = 0; i < iterations; i++)
        {
            _bus.WriteByte(address, value);
            address = unchecked(address + 1);
        }

        A[addressRegister] = address;
        D[counterRegister] = (D[counterRegister] & 0xFFFF_0000u) | (ushort)(counter - iterations);
        cycles = iterations * 18;
        instructionCount = iterations * 2;
        Cycles += cycles;
        return true;
    }

    public bool TryFastForwardAddWordPostIncrementNestedDbfLoop(int cycleBudget, out int cycles, out int instructionCount)
    {
        const int CyclesPerInnerIteration = 18;
        cycles = 0;
        instructionCount = 0;
        if (Stopped || TraceEnabled || InstructionObserver is not null || cycleBudget < CyclesPerInnerIteration)
        {
            return false;
        }

        uint pc = PC;
        ushort add = _bus.ReadWord(pc);
        if ((add & 0xF1F8) != 0xD058)
        {
            return false;
        }

        ushort innerDbf = _bus.ReadWord(pc + 2);
        ushort outerDbf = _bus.ReadWord(pc + 6);
        if ((innerDbf & 0xFFF8) != 0x51C8 || (outerDbf & 0xFFF8) != 0x51C8)
        {
            return false;
        }

        short innerDisplacement = (short)_bus.ReadWord(pc + 4);
        short outerDisplacement = (short)_bus.ReadWord(pc + 8);
        if (NormalizePc(unchecked(pc + 4u + (uint)innerDisplacement)) != pc ||
            NormalizePc(unchecked(pc + 8u + (uint)outerDisplacement)) != pc)
        {
            return false;
        }

        int destinationRegister = (add >> 9) & 0x07;
        int addressRegister = add & 0x07;
        int innerCounterRegister = innerDbf & 0x07;
        int outerCounterRegister = outerDbf & 0x07;
        if (innerCounterRegister == outerCounterRegister)
        {
            return false;
        }

        int maxIterations = Math.Min(cycleBudget / CyclesPerInnerIteration, 4096);
        if (maxIterations <= 0)
        {
            return false;
        }

        uint address = A[addressRegister];
        ushort accumulator = (ushort)D[destinationRegister];
        bool completed = false;
        int iterations = 0;
        for (; iterations < maxIterations; iterations++)
        {
            accumulator = unchecked((ushort)(accumulator + _bus.ReadWord(address)));
            address = unchecked(address + 2);

            ushort innerCounter = unchecked((ushort)((D[innerCounterRegister] & 0xFFFF) - 1));
            D[innerCounterRegister] = (D[innerCounterRegister] & 0xFFFF_0000u) | innerCounter;
            if (innerCounter != 0xFFFF)
            {
                continue;
            }

            ushort outerCounter = unchecked((ushort)((D[outerCounterRegister] & 0xFFFF) - 1));
            D[outerCounterRegister] = (D[outerCounterRegister] & 0xFFFF_0000u) | outerCounter;
            if (outerCounter == 0xFFFF)
            {
                completed = true;
                iterations++;
                break;
            }
        }

        if (iterations <= 0)
        {
            return false;
        }

        A[addressRegister] = address;
        D[destinationRegister] = (D[destinationRegister] & 0xFFFF_0000u) | accumulator;
        PC = completed ? NormalizePc(pc + 10) : pc;
        cycles = iterations * CyclesPerInnerIteration;
        instructionCount = iterations * 2;
        Cycles += cycles;
        return true;
    }

    public bool TryFastForwardMoveLongRegisterQuadFillDbfLoop(
        int cycleBudget,
        Func<uint, bool> canFastForwardAddress,
        out int cycles,
        out int instructionCount)
    {
        const int CyclesPerIteration = 58;
        cycles = 0;
        instructionCount = 0;
        if (Stopped || TraceEnabled || InstructionObserver is not null || cycleBudget < CyclesPerIteration)
        {
            return false;
        }

        uint pc = PC;
        uint basePc = pc;
        ushort move = _bus.ReadWord(basePc);
        int startMoveIndex = 0;
        bool patternAtCurrentPc = (move & 0xF1F8) == 0x20C0 &&
            _bus.ReadWord(basePc + 2) == move &&
            _bus.ReadWord(basePc + 4) == move &&
            _bus.ReadWord(basePc + 6) == move;
        if (!patternAtCurrentPc)
        {
            bool found = false;
            for (int candidateIndex = 1; candidateIndex < 4; candidateIndex++)
            {
                uint candidateBase = NormalizePc(pc - (uint)(candidateIndex * 2));
                ushort candidateMove = _bus.ReadWord(candidateBase);
                if ((candidateMove & 0xF1F8) == 0x20C0 &&
                    _bus.ReadWord(candidateBase + 2) == candidateMove &&
                    _bus.ReadWord(candidateBase + 4) == candidateMove &&
                    _bus.ReadWord(candidateBase + 6) == candidateMove)
                {
                    basePc = candidateBase;
                    move = candidateMove;
                    startMoveIndex = candidateIndex;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        ushort dbcc = _bus.ReadWord(basePc + 8);
        if ((dbcc & 0xFFF8) != 0x51C8)
        {
            return false;
        }

        short displacement = (short)_bus.ReadWord(basePc + 10);
        if (NormalizePc(unchecked(basePc + 10u + (uint)displacement)) != basePc)
        {
            return false;
        }

        int sourceRegister = move & 0x07;
        int addressRegister = (move >> 9) & 0x07;
        int counterRegister = dbcc & 0x07;
        if (sourceRegister == counterRegister)
        {
            return false;
        }

        ushort counter = (ushort)(D[counterRegister] & 0xFFFF);
        if (counter == 0)
        {
            return false;
        }

        uint address = A[addressRegister];
        if (!canFastForwardAddress(address & AddressMask))
        {
            return false;
        }

        int remainingMovesThisIteration = 4 - startMoveIndex;
        int completionCycles = counter == 0
            ? (remainingMovesThisIteration * 12) + 14
            : (remainingMovesThisIteration * 12) + 10 + ((counter - 1) * CyclesPerIteration) + 62;
        bool completesLoop = cycleBudget >= completionCycles;
        int iterations = completesLoop ? counter + 1 : Math.Min(counter, cycleBudget / CyclesPerIteration);
        if (startMoveIndex != 0 && !completesLoop)
        {
            return false;
        }

        if (iterations <= 0)
        {
            return false;
        }

        uint value = D[sourceRegister];
        if (startMoveIndex != 0 && completesLoop)
        {
            for (int j = startMoveIndex; j < 4; j++)
            {
                _bus.WriteLong(address, value);
                address = unchecked(address + 4);
            }

            iterations--;
        }

        for (int i = 0; i < iterations; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                _bus.WriteLong(address, value);
                address = unchecked(address + 4);
            }
        }

        A[addressRegister] = address;
        D[counterRegister] = (D[counterRegister] & 0xFFFF_0000u) |
            (completesLoop ? 0xFFFFu : (ushort)(counter - iterations));
        if (completesLoop)
        {
            PC = NormalizePc(basePc + 12);
        }

        cycles = completesLoop ? completionCycles : iterations * CyclesPerIteration;
        instructionCount = iterations * 5;
        Cycles += cycles;
        return true;
    }

    public bool TryFastForwardMoveBytePostIncrementCopyDbfLoop(
        int cycleBudget,
        Func<uint, bool> canFastForwardAddress,
        out int cycles,
        out int instructionCount)
    {
        const int CyclesPerIteration = 18;
        cycles = 0;
        instructionCount = 0;
        if (Stopped || TraceEnabled || InstructionObserver is not null || cycleBudget < CyclesPerIteration)
        {
            return false;
        }

        ushort move = _bus.ReadWord(PC);
        if ((move & 0xF1F8) != 0x10D8)
        {
            return false;
        }

        ushort dbcc = _bus.ReadWord(PC + 2);
        if ((dbcc & 0xFFF8) != 0x51C8)
        {
            return false;
        }

        short displacement = (short)_bus.ReadWord(PC + 4);
        if (NormalizePc(unchecked(PC + 4u + (uint)displacement)) != PC)
        {
            return false;
        }

        int counterRegister = dbcc & 0x07;
        ushort counter = (ushort)(D[counterRegister] & 0xFFFF);
        if (counter == 0)
        {
            return false;
        }

        int sourceAddressRegister = move & 0x07;
        int destinationAddressRegister = (move >> 9) & 0x07;
        uint sourceAddress = A[sourceAddressRegister];
        uint destinationAddress = A[destinationAddressRegister];
        if (!canFastForwardAddress(sourceAddress & AddressMask) ||
            !canFastForwardAddress(destinationAddress & AddressMask))
        {
            return false;
        }

        int maxTakenIterations = cycleBudget / CyclesPerIteration;
        int iterations = Math.Min(counter, maxTakenIterations);
        if (iterations <= 0)
        {
            return false;
        }

        for (int i = 0; i < iterations; i++)
        {
            byte value = _bus.ReadByte(sourceAddress);
            _bus.WriteByte(destinationAddress, value);
            sourceAddress = unchecked(sourceAddress + 1);
            destinationAddress = unchecked(destinationAddress + 1);
        }

        A[sourceAddressRegister] = sourceAddress;
        A[destinationAddressRegister] = destinationAddress;
        D[counterRegister] = (D[counterRegister] & 0xFFFF_0000u) | (ushort)(counter - iterations);
        cycles = iterations * CyclesPerIteration;
        instructionCount = iterations * 2;
        Cycles += cycles;
        return true;
    }

    public bool TryFastForwardMoveLongPostIncrementCopyDbfLoop(
        int cycleBudget,
        Func<uint, bool> canFastForwardAddress,
        out int cycles,
        out int instructionCount)
    {
        const int CyclesPerIteration = 18;
        cycles = 0;
        instructionCount = 0;
        if (Stopped || TraceEnabled || InstructionObserver is not null || cycleBudget < CyclesPerIteration)
        {
            return false;
        }

        ushort move = _bus.ReadWord(PC);
        if ((move & 0xF1F8) != 0x20D8)
        {
            return false;
        }

        ushort dbcc = _bus.ReadWord(PC + 2);
        if ((dbcc & 0xFFF8) != 0x51C8)
        {
            return false;
        }

        short displacement = (short)_bus.ReadWord(PC + 4);
        if (NormalizePc(unchecked(PC + 4u + (uint)displacement)) != PC)
        {
            return false;
        }

        int counterRegister = dbcc & 0x07;
        ushort counter = (ushort)(D[counterRegister] & 0xFFFF);
        if (counter == 0)
        {
            return false;
        }

        int sourceAddressRegister = move & 0x07;
        int destinationAddressRegister = (move >> 9) & 0x07;
        uint sourceAddress = A[sourceAddressRegister];
        uint destinationAddress = A[destinationAddressRegister];
        if (!canFastForwardAddress(sourceAddress & AddressMask) ||
            !canFastForwardAddress(destinationAddress & AddressMask))
        {
            return false;
        }

        int maxTakenIterations = cycleBudget / CyclesPerIteration;
        int iterations = Math.Min(counter, maxTakenIterations);
        if (iterations <= 0)
        {
            return false;
        }

        for (int i = 0; i < iterations; i++)
        {
            uint value = _bus.ReadLong(sourceAddress);
            _bus.WriteLong(destinationAddress, value);
            sourceAddress = unchecked(sourceAddress + 4);
            destinationAddress = unchecked(destinationAddress + 4);
        }

        A[sourceAddressRegister] = sourceAddress;
        A[destinationAddressRegister] = destinationAddress;
        D[counterRegister] = (D[counterRegister] & 0xFFFF_0000u) | (ushort)(counter - iterations);
        cycles = iterations * CyclesPerIteration;
        instructionCount = iterations * 2;
        Cycles += cycles;
        return true;
    }

    public bool TryFastForwardMoveLongPostIncrementCopyDbfLoopToFallthrough(
        int maxIterations,
        Func<uint, bool> canFastForwardAddress,
        out int cycles,
        out int instructionCount,
        bool trustCurrentInstructionPattern = false,
        ushort trustedMoveWord = 0x24D9,
        ushort trustedDbccWord = 0x51CF,
        short trustedDisplacement = -4)
    {
        const int TakenIterationCycles = 18;
        const int FallthroughIterationCycles = 22;
        cycles = 0;
        instructionCount = 0;
        if (Stopped || TraceEnabled || InstructionObserver is not null)
        {
            return false;
        }

        ushort move = trustCurrentInstructionPattern ? trustedMoveWord : _bus.ReadWord(PC);
        if ((move & 0xF1F8) != 0x20D8)
        {
            return false;
        }

        ushort dbcc = trustCurrentInstructionPattern ? trustedDbccWord : _bus.ReadWord(PC + 2);
        if ((dbcc & 0xFFF8) != 0x51C8)
        {
            return false;
        }

        short displacement = trustCurrentInstructionPattern ? trustedDisplacement : (short)_bus.ReadWord(PC + 4);
        if (NormalizePc(unchecked(PC + 4u + (uint)displacement)) != PC)
        {
            return false;
        }

        int counterRegister = dbcc & 0x07;
        ushort counter = (ushort)(D[counterRegister] & 0xFFFF);
        int iterations = counter + 1;
        if (iterations <= 0 || iterations > maxIterations)
        {
            return false;
        }

        int sourceAddressRegister = move & 0x07;
        int destinationAddressRegister = (move >> 9) & 0x07;
        uint sourceAddress = A[sourceAddressRegister];
        uint destinationAddress = A[destinationAddressRegister];
        if (!canFastForwardAddress(sourceAddress & AddressMask) ||
            !canFastForwardAddress(destinationAddress & AddressMask))
        {
            return false;
        }

        for (int i = 0; i < iterations; i++)
        {
            uint value = _bus.ReadLong(sourceAddress);
            _bus.WriteLong(destinationAddress, value);
            sourceAddress = unchecked(sourceAddress + 4);
            destinationAddress = unchecked(destinationAddress + 4);
        }

        A[sourceAddressRegister] = sourceAddress;
        A[destinationAddressRegister] = destinationAddress;
        D[counterRegister] = (D[counterRegister] & 0xFFFF_0000u) | 0xFFFFu;
        PC = NormalizePc(PC + 6);
        cycles = counter * TakenIterationCycles + FallthroughIterationCycles;
        instructionCount = iterations * 2;
        Cycles += cycles;
        return true;
    }

    public bool TryFastForwardMoveBytePostIncrementStridedCopyDbfLoop(
        int cycleBudget,
        Func<uint, bool> canFastForwardAddress,
        out int cycles,
        out int instructionCount)
    {
        const int CyclesPerIteration = 26;
        cycles = 0;
        instructionCount = 0;
        if (Stopped || TraceEnabled || InstructionObserver is not null || cycleBudget < CyclesPerIteration)
        {
            return false;
        }

        ushort move = _bus.ReadWord(PC);
        if ((move & 0xF1F8) != 0x10D8)
        {
            return false;
        }

        ushort addq = _bus.ReadWord(PC + 2);
        int destinationAddressRegister = (move >> 9) & 0x07;
        if (addq != (ushort)(0x5248 | destinationAddressRegister))
        {
            return false;
        }

        ushort dbcc = _bus.ReadWord(PC + 4);
        if ((dbcc & 0xFFF8) != 0x51C8)
        {
            return false;
        }

        short displacement = (short)_bus.ReadWord(PC + 6);
        if (NormalizePc(unchecked(PC + 6u + (uint)displacement)) != PC)
        {
            return false;
        }

        int counterRegister = dbcc & 0x07;
        ushort counter = (ushort)(D[counterRegister] & 0xFFFF);
        if (counter == 0)
        {
            return false;
        }

        int sourceAddressRegister = move & 0x07;
        uint sourceAddress = A[sourceAddressRegister];
        uint destinationAddress = A[destinationAddressRegister];
        if (!canFastForwardAddress(sourceAddress & AddressMask) ||
            !canFastForwardAddress(destinationAddress & AddressMask))
        {
            return false;
        }

        int maxTakenIterations = cycleBudget / CyclesPerIteration;
        int iterations = Math.Min(counter, maxTakenIterations);
        if (iterations <= 0)
        {
            return false;
        }

        for (int i = 0; i < iterations; i++)
        {
            byte value = _bus.ReadByte(sourceAddress);
            _bus.WriteByte(destinationAddress, value);
            sourceAddress = unchecked(sourceAddress + 1);
            destinationAddress = unchecked(destinationAddress + 2);
        }

        A[sourceAddressRegister] = sourceAddress;
        A[destinationAddressRegister] = destinationAddress;
        D[counterRegister] = (D[counterRegister] & 0xFFFF_0000u) | (ushort)(counter - iterations);
        cycles = iterations * CyclesPerIteration;
        instructionCount = iterations * 3;
        Cycles += cycles;
        return true;
    }

    public bool TryFastForwardShiftRegisterBitReaderLoop(int cycleBudget, out int cycles, out int instructionCount)
    {
        const int NoRefillCycles = 56;
        const int RefillCycles = 76;
        cycles = 0;
        instructionCount = 0;
        if (Stopped || TraceEnabled || InstructionObserver is not null || cycleBudget < NoRefillCycles)
        {
            return false;
        }

        uint pc = PC;
        if (_bus.ReadWord(pc) != 0x4A44 ||
            _bus.ReadWord(pc + 2) != 0x6606 ||
            _bus.ReadWord(pc + 4) != 0x1A10 ||
            _bus.ReadWord(pc + 6) != 0x5388 ||
            _bus.ReadWord(pc + 8) != 0x7808 ||
            _bus.ReadWord(pc + 10) != 0xE305 ||
            _bus.ReadWord(pc + 12) != 0xE350 ||
            _bus.ReadWord(pc + 14) != 0x5344 ||
            _bus.ReadWord(pc + 16) != 0x5341 ||
            _bus.ReadWord(pc + 18) != 0x6AEC)
        {
            return false;
        }

        short bitCounter = unchecked((short)(D[1] & 0xFFFF));
        if (bitCounter < 0)
        {
            return false;
        }

        bool completed = false;
        while (bitCounter >= 0)
        {
            ushort d4 = (ushort)D[4];
            int iterationCycles = d4 == 0 ? RefillCycles : NoRefillCycles;
            if (cycles + iterationCycles > cycleBudget)
            {
                break;
            }

            if (d4 == 0)
            {
                byte refill = _bus.ReadByte(A[0]);
                D[5] = (D[5] & 0xFFFF_FF00u) | refill;
                A[0] = unchecked(A[0] - 1);
                d4 = 8;
                D[4] = (D[4] & 0xFFFF_0000u) | d4;
                instructionCount += 3;
            }

            byte d5 = (byte)D[5];
            bool extend = (d5 & 0x80) != 0;
            d5 = (byte)(d5 << 1);
            D[5] = (D[5] & 0xFFFF_FF00u) | d5;

            ushort d0 = (ushort)D[0];
            d0 = (ushort)((d0 << 1) | (extend ? 1 : 0));
            D[0] = (D[0] & 0xFFFF_0000u) | d0;

            d4--;
            D[4] = (D[4] & 0xFFFF_0000u) | d4;

            ushort oldD1 = (ushort)D[1];
            ushort newD1 = unchecked((ushort)(oldD1 - 1));
            D[1] = (D[1] & 0xFFFF_0000u) | newD1;
            SetSubFlags(oldD1, 1, newD1, OperandSize.Word);

            cycles += iterationCycles;
            instructionCount += 7;
            bitCounter = unchecked((short)newD1);
            if (bitCounter < 0)
            {
                completed = true;
                break;
            }
        }

        if (cycles == 0)
        {
            return false;
        }

        PC = completed ? NormalizePc(pc + 20) : pc;
        Cycles += cycles;
        return true;
    }

    public bool TryFastForwardWordPairCompareSubroutineDbfLoop(int cycleBudget, out int cycles, out int instructionCount)
    {
        const int CyclesPerTakenEqualIteration = 76;
        cycles = 0;
        instructionCount = 0;
        if (Stopped || TraceEnabled || InstructionObserver is not null || cycleBudget < CyclesPerTakenEqualIteration)
        {
            return false;
        }

        uint loopPc = PC;
        if (_bus.ReadWord(loopPc) != 0x6100)
        {
            return false;
        }

        short subroutineDisplacement = (short)_bus.ReadWord(loopPc + 2);
        uint helperPc = NormalizePc(unchecked(loopPc + 2u + (uint)subroutineDisplacement));
        ushort dbf = _bus.ReadWord(loopPc + 4);
        if ((dbf & 0xFFF8) != 0x51C8)
        {
            return false;
        }

        short dbfDisplacement = (short)_bus.ReadWord(loopPc + 6);
        if (NormalizePc(unchecked(loopPc + 6u + (uint)dbfDisplacement)) != loopPc)
        {
            return false;
        }

        if (_bus.ReadWord(helperPc + 0) != 0x3018 ||
            _bus.ReadWord(helperPc + 2) != 0x3219 ||
            _bus.ReadWord(helperPc + 4) != 0x3800 ||
            _bus.ReadWord(helperPc + 6) != 0xB240 ||
            _bus.ReadWord(helperPc + 8) != 0x6700)
        {
            return false;
        }

        short equalBranchDisplacement = (short)_bus.ReadWord(helperPc + 10);
        uint equalTarget = NormalizePc(unchecked(helperPc + 10u + (uint)equalBranchDisplacement));
        if (_bus.ReadWord(equalTarget) != 0x4E75)
        {
            return false;
        }

        int counterRegister = dbf & 0x07;
        ushort counter = (ushort)(D[counterRegister] & 0xFFFF);
        if (counter == 0)
        {
            return false;
        }

        int maxIterations = Math.Min(counter, cycleBudget / CyclesPerTakenEqualIteration);
        uint a0 = A[0];
        uint a1 = A[1];
        ushort lastWord = 0;
        int iterations = 0;
        while (iterations < maxIterations)
        {
            ushort left = _bus.ReadWord(a0);
            ushort right = _bus.ReadWord(a1);
            if (left != right)
            {
                break;
            }

            lastWord = left;
            a0 = unchecked(a0 + 2);
            a1 = unchecked(a1 + 2);
            iterations++;
        }

        if (iterations == 0)
        {
            return false;
        }

        A[0] = a0;
        A[1] = a1;
        D[0] = (D[0] & 0xFFFF_0000u) | lastWord;
        D[1] = (D[1] & 0xFFFF_0000u) | lastWord;
        D[4] = (D[4] & 0xFFFF_0000u) | lastWord;
        D[counterRegister] = (D[counterRegister] & 0xFFFF_0000u) | unchecked((ushort)(counter - iterations));
        SetSubFlags(lastWord, lastWord, 0, OperandSize.Word);

        cycles = iterations * CyclesPerTakenEqualIteration;
        instructionCount = iterations * 8;
        Cycles += cycles;
        return true;
    }

    public bool TryFastForwardMoveWordAbsoluteDbfLoop(
        int cycleBudget,
        Func<uint, bool> canFastForwardAddress,
        out int cycles,
        out int instructionCount)
    {
        const int CyclesPerIteration = 18;
        cycles = 0;
        instructionCount = 0;
        if (Stopped || TraceEnabled || InstructionObserver is not null || cycleBudget < CyclesPerIteration)
        {
            return false;
        }

        ushort move = _bus.ReadWord(PC);
        if ((move & 0xF1F8) != 0x31C0)
        {
            return false;
        }

        uint address = _bus.ReadLong(PC + 2) & AddressMask;
        if (!canFastForwardAddress(address))
        {
            return false;
        }

        ushort dbcc = _bus.ReadWord(PC + 6);
        if ((dbcc & 0xFFF8) != 0x51C8)
        {
            return false;
        }

        short displacement = (short)_bus.ReadWord(PC + 8);
        if (NormalizePc(unchecked(PC + 8u + (uint)displacement)) != PC)
        {
            return false;
        }

        int counterRegister = dbcc & 0x07;
        ushort counter = (ushort)(D[counterRegister] & 0xFFFF);
        if (counter == 0)
        {
            return false;
        }

        int maxTakenIterations = cycleBudget / CyclesPerIteration;
        int iterations = Math.Min(counter, maxTakenIterations);
        if (iterations <= 0)
        {
            return false;
        }

        int sourceRegister = move & 0x07;
        ushort value = (ushort)D[sourceRegister];
        for (int i = 0; i < iterations; i++)
        {
            _bus.WriteWord(address, value);
        }

        D[counterRegister] = (D[counterRegister] & 0xFFFF_0000u) | (ushort)(counter - iterations);
        cycles = iterations * CyclesPerIteration;
        instructionCount = iterations * 2;
        Cycles += cycles;
        return true;
    }

    public bool TryFastForwardLongAbsoluteTstBneWaitLoop(
        int cycleBudget,
        Func<uint, bool> canFastForwardAddress,
        out int cycles,
        out int instructionCount)
    {
        const int CyclesPerIteration = 14;
        cycles = 0;
        instructionCount = 0;
        if (Stopped || TraceEnabled || InstructionObserver is not null || cycleBudget < CyclesPerIteration)
        {
            return false;
        }

        uint pc = PC;
        ushort testOpcode = _bus.ReadWord(pc);
        OperandSize size;
        if (testOpcode == 0x4A79)
        {
            size = OperandSize.Word;
        }
        else if (testOpcode == 0x4AB9)
        {
            size = OperandSize.Long;
        }
        else
        {
            return false;
        }

        uint address = (_bus.ReadLong(pc + 2) & AddressMask);
        if (!canFastForwardAddress(address))
        {
            return false;
        }

        ushort branch = _bus.ReadWord(pc + 6);
        if ((branch & 0xFF00) != 0x6600)
        {
            return false;
        }

        sbyte displacement = unchecked((sbyte)(branch & 0x00FF));
        if (displacement == 0 || NormalizePc(pc + 8 + (uint)displacement) != pc)
        {
            return false;
        }

        uint value = size == OperandSize.Long ? _bus.ReadLong(address) : _bus.ReadWord(address);
        if (value == 0)
        {
            return false;
        }

        SR = (ushort)(SR & ~0x000F);
        uint signBit = size == OperandSize.Long ? 0x8000_0000u : 0x8000u;
        if ((value & signBit) != 0)
        {
            SR |= 0x0008;
        }

        int iterations = cycleBudget / CyclesPerIteration;
        cycles = iterations * CyclesPerIteration;
        instructionCount = iterations * 2;
        Cycles += cycles;
        return true;
    }

    public bool TryFastForwardLongAbsoluteCmpBeqWaitLoop(
        int cycleBudget,
        Func<uint, bool> canFastForwardAddress,
        out int cycles,
        out int instructionCount)
    {
        const int CyclesPerIteration = 16;
        cycles = 0;
        instructionCount = 0;
        if (Stopped || TraceEnabled || InstructionObserver is not null || cycleBudget < CyclesPerIteration)
        {
            return false;
        }

        uint pc = PC;
        ushort compare = _bus.ReadWord(pc);
        if ((compare & 0xF1FF) != 0xB0B9)
        {
            return false;
        }

        uint address = _bus.ReadLong(pc + 2) & AddressMask;
        if (!canFastForwardAddress(address))
        {
            return false;
        }

        ushort branch = _bus.ReadWord(pc + 6);
        if ((branch & 0xFF00) != 0x6700)
        {
            return false;
        }

        sbyte displacement = unchecked((sbyte)(branch & 0x00FF));
        if (displacement == 0 || NormalizePc(pc + 8 + (uint)displacement) != pc)
        {
            return false;
        }

        int register = (compare >> 9) & 0x07;
        uint value = _bus.ReadLong(address);
        if (value != D[register])
        {
            return false;
        }

        SR = (ushort)(SR & ~0x000F);
        SR |= 0x0004;
        int iterations = cycleBudget / CyclesPerIteration;
        cycles = iterations * CyclesPerIteration;
        instructionCount = iterations * 2;
        Cycles += cycles;
        return true;
    }

    public bool TryFastForwardBtstRegisterDbccLoop(int cycleBudget, out int cycles, out int instructionCount)
    {
        const int TakenIterationCycles = 16;
        const int ConditionExitCycles = 18;
        const int CounterExitCycles = 20;
        cycles = 0;
        instructionCount = 0;
        if (Stopped || TraceEnabled || InstructionObserver is not null || cycleBudget < TakenIterationCycles)
        {
            return false;
        }

        uint pc = PC;
        ushort btst = _bus.ReadWord(pc);
        if ((btst & 0xF1F8) != 0x0100)
        {
            return false;
        }

        ushort dbcc = _bus.ReadWord(pc + 2);
        if ((dbcc & 0xF0F8) != 0x50C8)
        {
            return false;
        }

        short displacement = (short)_bus.ReadWord(pc + 4);
        if (NormalizePc(unchecked(pc + 4u + (uint)displacement)) != pc)
        {
            return false;
        }

        int bitRegister = (btst >> 9) & 0x07;
        int dataRegister = btst & 0x07;
        int condition = (dbcc >> 8) & 0x0F;
        int counterRegister = dbcc & 0x07;

        bool completed = false;
        while (cycles + TakenIterationCycles <= cycleBudget)
        {
            int bit = (int)(D[bitRegister] & 0x1F);
            uint mask = 1u << bit;
            SetZ((D[dataRegister] & mask) == 0);

            if (ConditionTrue(condition))
            {
                if (cycles == 0 || cycles + ConditionExitCycles > cycleBudget)
                {
                    break;
                }

                cycles += ConditionExitCycles;
                instructionCount += 2;
                PC = NormalizePc(pc + 6);
                completed = true;
                break;
            }

            ushort counter = unchecked((ushort)((D[counterRegister] & 0xFFFF) - 1));
            D[counterRegister] = (D[counterRegister] & 0xFFFF_0000u) | counter;
            if (counter == 0xFFFF)
            {
                if (cycles + CounterExitCycles > cycleBudget)
                {
                    D[counterRegister] = (D[counterRegister] & 0xFFFF_0000u);
                    break;
                }

                cycles += CounterExitCycles;
                instructionCount += 2;
                PC = NormalizePc(pc + 6);
                completed = true;
                break;
            }

            cycles += TakenIterationCycles;
            instructionCount += 2;
        }

        if (cycles == 0)
        {
            return false;
        }

        if (!completed)
        {
            PC = pc;
        }

        Cycles += cycles;
        return true;
    }

    public void ClearAllocationProfile()
    {
        EnsureAllocationProfile();
        Array.Clear(_allocatedBytesByOpcode!);
        Array.Clear(_allocationSamplesByOpcode!);
    }

    public IEnumerable<M68kOpcodeAllocation> GetAllocationProfile()
    {
        if (_allocatedBytesByOpcode is null || _allocationSamplesByOpcode is null)
        {
            yield break;
        }

        for (int opcode = 0; opcode < _allocatedBytesByOpcode.Length; opcode++)
        {
            long allocated = _allocatedBytesByOpcode[opcode];
            int samples = _allocationSamplesByOpcode[opcode];
            if (allocated > 0 || samples > 0)
            {
                yield return new M68kOpcodeAllocation((ushort)opcode, samples, allocated);
            }
        }
    }

    private void EnsureAllocationProfile()
    {
        _allocatedBytesByOpcode ??= new long[0x10000];
        _allocationSamplesByOpcode ??= new int[0x10000];
    }

    public bool RequestInterrupt(int level)
    {
        if (level <= 0 || level > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }

        int mask = (SR >> 8) & 0x7;
        if (level <= mask)
        {
            return false;
        }

        Stopped = false;
        uint oldPc = PC;
        ushort oldSr = SR;
        if ((SR & 0x2000) == 0)
        {
            uint userStack = A[7];
            A[7] = USP;
            USP = userStack;
        }

        SR = (ushort)((SR | 0x2000) & 0xF8FF);
        SR = (ushort)(SR | (level << 8));
        PushLong(PC);
        PushWord(oldSr);
        PC = NormalizePc(_bus.ReadLong((uint)(level + 24) * 4));
        InterruptObserver?.Invoke(new M68kInterruptTrace(level, level + 24, oldPc, PC, oldSr, SR, A[7]));
        return true;
    }

    private void EnterException(int vector)
    {
        _exceptionCounts.TryGetValue(vector, out long count);
        _exceptionCounts[vector] = count + 1;
        uint framePc = PC;
        uint frameSp = A[7];
        if (_exceptionTrace.Count < 128)
        {
            _exceptionTrace.Add($"vector={vector} pc=${_currentOpcodeAddress:X6} opcode=${_currentOpcode:X4} framePc=${framePc:X8} sp=${frameSp:X8}");
            if ((TraceEnabled || HistoryEnabled) && count < 8 && _exceptionCounts.Count <= 4)
            {
                foreach (string recent in _recentInstructionTrace.TakeLast(96))
                {
                    _exceptionTrace.Add($"  {recent}");
                }
            }
        }

        ushort oldSr = SR;
        if ((SR & 0x2000) == 0)
        {
            uint userStack = A[7];
            A[7] = USP;
            USP = userStack;
        }

        SR |= 0x2000;
        PushLong(PC);
        PushWord(oldSr);
        PC = NormalizePc(_bus.ReadLong((uint)vector * 4));
        Stopped = false;
        ExceptionObserver?.Invoke(new M68kExceptionTrace(vector, _currentOpcodeAddress, _currentOpcode, framePc, PC, oldSr, SR, A[7], D[0], D[1], D[2], D[3], A[0], A[1], A[2], A[3], A[4], A[5], A[6]));
    }

    private int Execute(ushort opcode, uint opcodeAddress)
    {
        if ((opcode & 0xF000) == 0xA000)
        {
            EnterException(LineAEmulatorVector);
            return 34;
        }

        if ((opcode & 0xF000) == 0xF000)
        {
            if (LineFInstructionOverride?.Invoke(opcodeAddress, opcode) == true)
            {
                return 4;
            }

            EnterException(LineFEmulatorVector);
            return 34;
        }

        if (opcode == 0x4E70)
        {
            if ((SR & 0x2000) == 0)
            {
                EnterException(PrivilegeViolationVector);
                return 34;
            }

            return 132;
        }

        if (opcode == 0x4E71)
        {
            return 4;
        }

        if (opcode == 0x4E75)
        {
            PC = NormalizePc(PopLong());
            return 16;
        }

        if (opcode == 0x4E73)
        {
            ushort restoredSr = PopWord();
            uint restoredPc = PopLong();
            SetStatusRegister(restoredSr);
            PC = NormalizePc(restoredPc);
            return 20;
        }

        if (opcode == 0x4E77)
        {
            ushort restoredCcr = PopWord();
            PC = NormalizePc(PopLong());
            SR = (ushort)((SR & 0xFFE0) | (restoredCcr & 0x001F));
            return 20;
        }

        if ((opcode & 0xFFF8) == 0x4E50)
        {
            int register = opcode & 0x7;
            PushLong(A[register]);
            A[register] = A[7];
            A[7] = unchecked((uint)(A[7] + (short)FetchWord()));
            return 16;
        }

        if ((opcode & 0xFFF8) == 0x4E58)
        {
            int register = opcode & 0x7;
            A[7] = A[register];
            A[register] = PopLong();
            return 12;
        }

        if ((opcode & 0xFFF0) == 0x4E40)
        {
            if (TrapInstructionOverride?.Invoke(opcodeAddress, opcode) == true)
            {
                return 4;
            }

            EnterException(32 + (opcode & 0xF));
            return 34;
        }

        if (opcode == 0x4E72)
        {
            SetStatusRegister(FetchWord());
            Stopped = true;
            return 4;
        }

        if ((opcode & 0xFFF8) == 0x4E60)
        {
            if ((SR & 0x2000) == 0)
            {
                EnterException(PrivilegeViolationVector);
                return 34;
            }

            USP = A[opcode & 0x7];
            return 4;
        }

        if ((opcode & 0xFFF8) == 0x4E68)
        {
            if ((SR & 0x2000) == 0)
            {
                EnterException(PrivilegeViolationVector);
                return 34;
            }

            A[opcode & 0x7] = USP;
            return 4;
        }

        if (opcode == 0x4EF9)
        {
            PC = NormalizePc(FetchLong());
            return 12;
        }

        if (opcode == 0x4EB9)
        {
            uint target = FetchLong();
            PushLong(PC);
            PC = NormalizePc(target);
            return 20;
        }

        if ((opcode & 0xFFC0) == 0x4E80)
        {
            uint target = CalculateEffectiveAddress((opcode >> 3) & 0x7, opcode & 0x7);
            PushLong(PC);
            PC = NormalizePc(target);
            return 16;
        }

        if ((opcode & 0xFFC0) == 0x4EC0)
        {
            PC = NormalizePc(CalculateEffectiveAddress((opcode >> 3) & 0x7, opcode & 0x7));
            return 8;
        }

        if ((opcode & 0xFFF8) == 0x4840)
        {
            int register = opcode & 0x7;
            D[register] = ((D[register] & 0xFFFF) << 16) | (D[register] >> 16);
            SetNz(D[register], OperandSize.Long);
            ClearVc();
            return 4;
        }

        if ((opcode & 0xFFC0) == 0x4840)
        {
            PushLong(CalculateEffectiveAddress((opcode >> 3) & 0x7, opcode & 0x7));
            return 12;
        }

        if ((opcode & 0xFFF8) == 0x4880)
        {
            int register = opcode & 0x7;
            uint extended = (ushort)(short)(sbyte)(byte)D[register];
            D[register] = (D[register] & 0xFFFF_0000) | extended;
            SetNz(D[register] & 0xFFFF, OperandSize.Word);
            ClearVc();
            return 4;
        }

        if ((opcode & 0xFFF8) == 0x48C0)
        {
            int register = opcode & 0x7;
            D[register] = SignExtendWord((ushort)D[register]);
            SetNz(D[register], OperandSize.Long);
            ClearVc();
            return 4;
        }

        if ((opcode & 0xF138) == 0x0108)
        {
            return ExecuteMovep(opcode);
        }

        if ((opcode & 0xFFC0) == 0x44C0)
        {
            uint value = ReadEffectiveAddress((opcode >> 3) & 0x7, opcode & 0x7, OperandSize.Word);
            SR = (ushort)((SR & 0xFFE0u) | (value & 0x001Fu));
            return 12;
        }

        if ((opcode & 0xFFC0) == 0x46C0)
        {
            SetStatusRegister((ushort)ReadEffectiveAddress((opcode >> 3) & 0x7, opcode & 0x7, OperandSize.Word));
            return 12;
        }

        if ((opcode & 0xFFC0) is 0x4600 or 0x4640 or 0x4680)
        {
            return ExecuteNot(opcode);
        }

        if ((opcode & 0xFFC0) == 0x40C0)
        {
            WriteEffectiveAddress((opcode >> 3) & 0x7, opcode & 0x7, OperandSize.Word, SR);
            return 6;
        }

        if ((opcode & 0xFFC0) is 0x4400 or 0x4440 or 0x4480)
        {
            return ExecuteNeg(opcode);
        }

        if ((opcode & 0xFF80) == 0x4880)
        {
            return ExecuteMovem(opcode, registersToMemory: true);
        }

        if ((opcode & 0xFF80) == 0x4C80)
        {
            return ExecuteMovem(opcode, registersToMemory: false);
        }

        if ((opcode & 0xF1C0) == 0x41C0)
        {
            int register = (opcode >> 9) & 0x7;
            A[register] = CalculateEffectiveAddress((opcode >> 3) & 0x7, opcode & 0x7);
            return 8;
        }

        if ((opcode & 0xF1C0) == 0x4180)
        {
            return ExecuteChk(opcode);
        }

        if ((opcode & 0xF000) is 0x1000 or 0x2000 or 0x3000)
        {
            return ExecuteMove(opcode);
        }

        if ((opcode & 0xF000) == 0xE000)
        {
            return ExecuteShiftRotate(opcode);
        }

        if ((opcode & 0xF100) == 0x7000)
        {
            int register = (opcode >> 9) & 0x7;
            D[register] = unchecked((uint)(int)(sbyte)(opcode & 0xFF));
            SetNz(D[register], OperandSize.Long);
            ClearVc();
            return 4;
        }

        if ((opcode & 0xF0F8) == 0x50C8)
        {
            return ExecuteDbcc(opcode);
        }

        if ((opcode & 0xF0C0) == 0x50C0)
        {
            return ExecuteSetCondition(opcode);
        }

        if ((opcode & 0xF100) == 0x5000)
        {
            return ExecuteAddSubQuick(opcode, subtract: false);
        }

        if ((opcode & 0xF100) == 0x5100)
        {
            return ExecuteAddSubQuick(opcode, subtract: true);
        }

        if (opcode == 0x023C)
        {
            SR = (ushort)((SR & 0xFFE0u) | (FetchWord() & 0x001Fu));
            return 20;
        }

        if (opcode == 0x027C)
        {
            SetStatusRegister((ushort)(SR & FetchWord()));
            return 20;
        }

        if ((opcode & 0xFF00) is 0x0200 or 0x0240 or 0x0280)
        {
            return ExecuteImmediateLogic(opcode, LogicOperation.And);
        }

        if (opcode == 0x003C)
        {
            SR = (ushort)((SR & 0xFFE0u) | ((SR | FetchWord()) & 0x001Fu));
            return 20;
        }

        if (opcode == 0x007C)
        {
            SetStatusRegister((ushort)(SR | FetchWord()));
            return 20;
        }

        if ((opcode & 0xFF00) is 0x0000 or 0x0040 or 0x0080)
        {
            return ExecuteImmediateLogic(opcode, LogicOperation.Or);
        }

        if (opcode == 0x0A3C)
        {
            SR = (ushort)((SR & 0xFFE0u) | ((SR ^ FetchWord()) & 0x001Fu));
            return 20;
        }

        if (opcode == 0x0A7C)
        {
            SetStatusRegister((ushort)(SR ^ FetchWord()));
            return 20;
        }

        if ((opcode & 0xFF00) is 0x0A00 or 0x0A40 or 0x0A80)
        {
            return ExecuteImmediateLogic(opcode, LogicOperation.Eor);
        }

        if ((opcode & 0xFF00) is 0x0400 or 0x0440 or 0x0480)
        {
            return ExecuteImmediateAddSub(opcode, subtract: true);
        }

        if ((opcode & 0xFF00) is 0x0600 or 0x0640 or 0x0680)
        {
            return ExecuteImmediateAddSub(opcode, subtract: false);
        }

        if ((opcode & 0xFF00) == 0x0800)
        {
            return ExecuteBitImmediate(opcode);
        }

        if ((opcode & 0xF100) == 0x0100)
        {
            return ExecuteBitDynamic(opcode);
        }

        if ((opcode & 0xFF00) is 0x0C00 or 0x0C40 or 0x0C80)
        {
            return ExecuteCompareImmediate(opcode);
        }

        if ((opcode & 0xFFC0) is 0x4200 or 0x4240 or 0x4280)
        {
            return ExecuteClear(opcode);
        }

        if ((opcode & 0xFFC0) is 0x4000 or 0x4040 or 0x4080)
        {
            return ExecuteNegx(opcode);
        }

        if ((opcode & 0xFFC0) is 0x4A00 or 0x4A40 or 0x4A80)
        {
            return ExecuteTest(opcode);
        }

        if (opcode == 0x4AFC)
        {
            EnterException(IllegalInstructionVector);
            return 34;
        }

        if ((opcode & 0xFFC0) == 0x4AC0)
        {
            return ExecuteTas(opcode);
        }

        if ((opcode & 0xF000) == 0x6000)
        {
            return ExecuteBranch(opcode);
        }

        if ((opcode & 0xF1FF) == 0x41F9)
        {
            int register = (opcode >> 9) & 0x7;
            A[register] = FetchLong();
            return 12;
        }

        if ((opcode & 0xF000) == 0x9000)
        {
            return ExecuteAddSub(opcode, subtract: true);
        }

        if ((opcode & 0xF000) == 0x8000)
        {
            if ((opcode & 0xF1F0) == 0x8100)
            {
                return ExecuteBcd(opcode, subtract: true);
            }

            if (((opcode >> 6) & 0x7) is 3 or 7)
            {
                return ExecuteDivide(opcode);
            }

            return ExecuteOr(opcode);
        }

        if ((opcode & 0xF000) == 0xB000)
        {
            return ExecuteCompare(opcode);
        }

        if ((opcode & 0xF000) == 0xC000)
        {
            if ((opcode & 0xF1F0) == 0xC100)
            {
                return ExecuteBcd(opcode, subtract: false);
            }

            if ((opcode & 0xF1F8) is 0xC140 or 0xC148 or 0xC188)
            {
                return ExecuteExchange(opcode);
            }

            if (((opcode >> 6) & 0x7) is 3 or 7)
            {
                return ExecuteMultiply(opcode);
            }

            return ExecuteAnd(opcode);
        }

        if ((opcode & 0xF000) == 0xD000)
        {
            return ExecuteAddSub(opcode, subtract: false);
        }

        if (opcode == 0x23FC)
        {
            uint value = FetchLong();
            uint address = FetchLong();
            _bus.WriteLong(address, value);
            return 28;
        }

        if (opcode == 0x33FC)
        {
            ushort value = FetchWord();
            uint address = FetchLong();
            _bus.WriteWord(address, value);
            return 24;
        }

        if (opcode == 0x13FC)
        {
            byte value = (byte)FetchWord();
            uint address = FetchLong();
            _bus.WriteByte(address, value);
            return 20;
        }

        EnterException(IllegalInstructionVector);
        return 34;
    }

    private int ExecuteMovem(ushort opcode, bool registersToMemory)
    {
        OperandSize size = (opcode & 0x0040) != 0 ? OperandSize.Long : OperandSize.Word;
        int mode = (opcode >> 3) & 0x7;
        int register = opcode & 0x7;
        ushort mask = FetchWord();

        if (registersToMemory)
        {
            if (mode == 4)
            {
                uint originalAddressRegister = A[register];
                for (int bit = 0; bit < 16; bit++)
                {
                    if ((mask & (1 << bit)) == 0)
                    {
                        continue;
                    }

                    A[register] -= SizeBytes(size);
                    WriteMemory(A[register], size, ReadMovemPredecrementRegister(bit, size, register, originalAddressRegister));
                }

                return 8 + CountBits(mask) * 4;
            }

            uint address = CalculateEffectiveAddress(mode, register);
            for (int bit = 0; bit < 16; bit++)
            {
                if ((mask & (1 << bit)) == 0)
                {
                    continue;
                }

                WriteMemory(address, size, ReadMovemRegister(bit, size));
                address += SizeBytes(size);
            }

            return 8 + CountBits(mask) * 4;
        }

        uint readAddress = CalculateEffectiveAddress(mode, register);
        uint finalAddress = readAddress;
        for (int bit = 0; bit < 16; bit++)
        {
            if ((mask & (1 << bit)) == 0)
            {
                continue;
            }

            uint value = ReadMemory(readAddress, size);
            WriteMovemRegister(bit, size == OperandSize.Word ? SignExtendWord((ushort)value) : value);
            readAddress += SizeBytes(size);
            finalAddress = readAddress;
        }

        if (mode == 3)
        {
            A[register] = finalAddress;
        }

        return 12 + CountBits(mask) * 4;
    }

    private int ExecuteMove(ushort opcode)
    {
        OperandSize size = ((opcode >> 12) & 0x3) switch
        {
            1 => OperandSize.Byte,
            2 => OperandSize.Long,
            3 => OperandSize.Word,
            _ => throw new M68kException($"Invalid MOVE size in opcode ${opcode:X4}"),
        };

        int sourceMode = (opcode >> 3) & 0x7;
        int sourceRegister = opcode & 0x7;
        int destinationMode = (opcode >> 6) & 0x7;
        int destinationRegister = (opcode >> 9) & 0x7;
        uint value = ReadEffectiveAddress(sourceMode, sourceRegister, size);

        if (destinationMode == 1)
        {
            if (size == OperandSize.Byte)
            {
                throw new M68kCpuException(IllegalInstructionVector);
            }

            A[destinationRegister] = size == OperandSize.Word ? SignExtendWord((ushort)value) : value;
            return 8;
        }

        WriteEffectiveAddress(destinationMode, destinationRegister, size, value);
        SetNz(value, size);
        ClearVc();
        return 8;
    }

    private int ExecuteImmediateLogic(ushort opcode, LogicOperation operation)
    {
        OperandSize size = DecodeSizeBits((opcode >> 6) & 0x3);
        int mode = (opcode >> 3) & 0x7;
        int register = opcode & 0x7;
        uint immediate = ReadImmediate(size);
        uint current = ReadWritableEffectiveAddress(mode, register, size, out WritableTarget target);
        uint result = operation switch
        {
            LogicOperation.And => current & immediate,
            LogicOperation.Or => current | immediate,
            LogicOperation.Eor => current ^ immediate,
            _ => throw new M68kException("Unsupported logical immediate operation"),
        };

        WriteWritableTarget(target, result);
        SetNz(result, size);
        ClearVc();
        return 8;
    }

    private int ExecuteMovep(ushort opcode)
    {
        int dataRegister = (opcode >> 9) & 0x7;
        int addressRegister = opcode & 0x7;
        int opmode = (opcode >> 6) & 0x7;
        uint address = unchecked((uint)(A[addressRegister] + (short)FetchWord()));

        if (opmode == 4)
        {
            uint value = (uint)((_bus.ReadByte(address) << 8) | _bus.ReadByte(address + 2));
            WriteDataRegister(dataRegister, OperandSize.Word, value);
            return 16;
        }

        if (opmode == 5)
        {
            uint value = (uint)((_bus.ReadByte(address) << 24)
                | (_bus.ReadByte(address + 2) << 16)
                | (_bus.ReadByte(address + 4) << 8)
                | _bus.ReadByte(address + 6));
            D[dataRegister] = value;
            return 24;
        }

        uint source = D[dataRegister];
        if (opmode == 6)
        {
            _bus.WriteByte(address, (byte)(source >> 8));
            _bus.WriteByte(address + 2, (byte)source);
            return 16;
        }

        if (opmode == 7)
        {
            _bus.WriteByte(address, (byte)(source >> 24));
            _bus.WriteByte(address + 2, (byte)(source >> 16));
            _bus.WriteByte(address + 4, (byte)(source >> 8));
            _bus.WriteByte(address + 6, (byte)source);
            return 24;
        }

        throw new M68kCpuException(IllegalInstructionVector);
    }

    private int ExecuteImmediateAddSub(ushort opcode, bool subtract)
    {
        OperandSize size = DecodeSizeBits((opcode >> 6) & 0x3);
        int mode = (opcode >> 3) & 0x7;
        int register = opcode & 0x7;
        uint source = ReadImmediate(size);
        uint destination = ReadWritableEffectiveAddress(mode, register, size, out WritableTarget target);
        uint result = subtract ? destination - source : destination + source;

        WriteWritableTarget(target, result);
        if (subtract)
        {
            SetSubFlags(destination, source, result, size);
        }
        else
        {
            SetAddFlags(destination, source, result, size);
        }

        return 8;
    }

    private int ExecuteCompareImmediate(ushort opcode)
    {
        OperandSize size = DecodeSizeBits((opcode >> 6) & 0x3);
        int mode = (opcode >> 3) & 0x7;
        int register = opcode & 0x7;
        uint immediate = ReadImmediate(size);
        uint destination = ReadEffectiveAddress(mode, register, size);

        SetSubFlags(destination, immediate, unchecked(destination - immediate), size);
        return 8;
    }

    private int ExecuteBitImmediate(ushort opcode)
    {
        int bit = FetchWord() & 0x1F;
        int mode = (opcode >> 3) & 0x7;
        int register = opcode & 0x7;
        OperandSize size = mode == 0 ? OperandSize.Long : OperandSize.Byte;
        int effectiveBit = mode == 0 ? bit : bit & 7;
        uint mask = 1u << effectiveBit;
        int operation = (opcode >> 6) & 0x03;

        if (operation == 0)
        {
            uint value = ReadEffectiveAddress(mode, register, size);
            SetZ((value & mask) == 0);
            return 8;
        }

        uint current = ReadWritableEffectiveAddress(mode, register, size, out WritableTarget target);
        SetZ((current & mask) == 0);
        uint result = operation switch
        {
            1 => current ^ mask,
            2 => current & ~mask,
            3 => current | mask,
            _ => current,
        };

        WriteWritableTarget(target, result);
        return 8;
    }

    private int ExecuteBitDynamic(ushort opcode)
    {
        int bitRegister = (opcode >> 9) & 0x7;
        int mode = (opcode >> 3) & 0x7;
        int register = opcode & 0x7;
        int bit = (int)(D[bitRegister] & 0x1F);
        OperandSize size = mode == 0 ? OperandSize.Long : OperandSize.Byte;
        int effectiveBit = mode == 0 ? bit : bit & 7;
        uint mask = 1u << effectiveBit;
        int operation = (opcode >> 6) & 0x03;

        if (operation == 0)
        {
            uint value = ReadEffectiveAddress(mode, register, size);
            SetZ((value & mask) == 0);
            return 6;
        }

        uint current = ReadWritableEffectiveAddress(mode, register, size, out WritableTarget target);
        SetZ((current & mask) == 0);
        uint result = operation switch
        {
            1 => current ^ mask,
            2 => current & ~mask,
            3 => current | mask,
            _ => current,
        };

        WriteWritableTarget(target, result);
        return 6;
    }

    private int ExecuteClear(ushort opcode)
    {
        OperandSize size = DecodeSizeBits((opcode >> 6) & 0x3);
        WriteEffectiveAddress((opcode >> 3) & 0x7, opcode & 0x7, size, 0);
        SetNz(0, size);
        ClearVc();
        return 8;
    }

    private int ExecuteNegx(ushort opcode)
    {
        OperandSize size = DecodeSizeBits((opcode >> 6) & 0x3);
        int mode = (opcode >> 3) & 0x7;
        int register = opcode & 0x7;
        uint destination = ReadWritableEffectiveAddress(mode, register, size, out WritableTarget target);
        uint extend = (SR & 0x0010) != 0 ? 1u : 0u;
        uint source = extend;
        uint result = unchecked(0u - destination - extend);
        WriteWritableTarget(target, result);
        SetSubFlags(0, destination + source, result, size);
        return 8;
    }

    private int ExecuteNeg(ushort opcode)
    {
        OperandSize size = DecodeSizeBits((opcode >> 6) & 0x3);
        int mode = (opcode >> 3) & 0x7;
        int register = opcode & 0x7;
        uint destination = ReadWritableEffectiveAddress(mode, register, size, out WritableTarget target);
        uint result = unchecked(0u - destination);
        WriteWritableTarget(target, result);
        SetSubFlags(0, destination, result, size);
        return 8;
    }

    private int ExecuteNot(ushort opcode)
    {
        OperandSize size = DecodeSizeBits((opcode >> 6) & 0x3);
        int mode = (opcode >> 3) & 0x7;
        int register = opcode & 0x7;
        uint result = ~ReadWritableEffectiveAddress(mode, register, size, out WritableTarget target);
        WriteWritableTarget(target, result);
        SetNz(result, size);
        ClearVc();
        return 8;
    }

    private int ExecuteCompare(ushort opcode)
    {
        int register = (opcode >> 9) & 0x7;
        int opmode = (opcode >> 6) & 0x7;
        int mode = (opcode >> 3) & 0x7;
        int eaRegister = opcode & 0x7;

        if (opmode is 0 or 1 or 2)
        {
            OperandSize size = DecodeSizeBits(opmode);
            uint source = ReadEffectiveAddress(mode, eaRegister, size);
            uint destination = ReadDataRegister(register, size);
            SetSubFlags(destination, source, unchecked(destination - source), size);
            return 6;
        }

        if (opmode is 3 or 7)
        {
            OperandSize size = opmode == 3 ? OperandSize.Word : OperandSize.Long;
            uint source = ReadEffectiveAddress(mode, eaRegister, size);
            if (size == OperandSize.Word)
            {
                source = SignExtendWord((ushort)source);
            }

            uint destination = A[register];
            SetSubFlags(destination, source, unchecked(destination - source), OperandSize.Long);
            return 6;
        }

        if (opmode is 4 or 5 or 6)
        {
            OperandSize size = DecodeSizeBits(opmode - 4);
            if (mode == 1)
            {
                uint source = ReadPostIncrement(eaRegister, size);
                uint destination = ReadPostIncrement(register, size);
                SetSubFlags(destination, source, unchecked(destination - source), size);
                return 12;
            }

            uint eorSource = ReadDataRegister(register, size);
            uint eorDestination = ReadWritableEffectiveAddress(mode, eaRegister, size, out WritableTarget target);
            uint result = eorDestination ^ eorSource;
            WriteWritableTarget(target, result);
            SetNz(result, size);
            ClearVc();
            return 8;
        }

        throw new M68kException($"Unsupported CMP opcode ${opcode:X4}");
    }

    private int ExecuteAddSub(ushort opcode, bool subtract)
    {
        int register = (opcode >> 9) & 0x7;
        int opmode = (opcode >> 6) & 0x7;
        int mode = (opcode >> 3) & 0x7;
        int eaRegister = opcode & 0x7;

        if (opmode is 4 or 5 or 6 && mode is 0 or 1)
        {
            return ExecuteAddSubX(register, eaRegister, opmode, memoryMode: mode == 1, subtract);
        }

        if (opmode is 0 or 1 or 2)
        {
            OperandSize size = DecodeSizeBits(opmode);
            uint source = ReadEffectiveAddress(mode, eaRegister, size);
            uint destination = ReadDataRegister(register, size);
            uint result = subtract ? destination - source : destination + source;
            WriteDataRegister(register, size, result);
            if (subtract)
            {
                SetSubFlags(destination, source, result, size);
            }
            else
            {
                SetAddFlags(destination, source, result, size);
            }

            return 8;
        }

        if (opmode is 3 or 7)
        {
            OperandSize size = opmode == 3 ? OperandSize.Word : OperandSize.Long;
            uint source = ReadEffectiveAddress(mode, eaRegister, size);
            if (size == OperandSize.Word)
            {
                source = SignExtendWord((ushort)source);
            }

            A[register] = subtract ? A[register] - source : A[register] + source;
            return 8;
        }

        if (opmode is 4 or 5 or 6)
        {
            OperandSize size = DecodeSizeBits(opmode - 4);
            uint source = ReadDataRegister(register, size);
            uint destination = ReadWritableEffectiveAddress(mode, eaRegister, size, out WritableTarget target);
            uint result = subtract ? destination - source : destination + source;
            WriteWritableTarget(target, result);
            if (subtract)
            {
                SetSubFlags(destination, source, result, size);
            }
            else
            {
                SetAddFlags(destination, source, result, size);
            }

            return 12;
        }

        throw new M68kException($"Unsupported {(subtract ? "SUB" : "ADD")} opcode ${opcode:X4}");
    }

    private int ExecuteAddSubX(int destinationRegister, int sourceRegister, int opmode, bool memoryMode, bool subtract)
    {
        OperandSize size = DecodeSizeBits(opmode - 4);
        uint source;
        uint destination;
        uint destinationAddress = 0;
        if (memoryMode)
        {
            A[sourceRegister] -= AddressRegisterIncrement(sourceRegister, size);
            A[destinationRegister] -= AddressRegisterIncrement(destinationRegister, size);
            source = ReadMemory(A[sourceRegister], size);
            destinationAddress = A[destinationRegister];
            destination = ReadMemory(destinationAddress, size);
        }
        else
        {
            source = ReadDataRegister(sourceRegister, size);
            destination = ReadDataRegister(destinationRegister, size);
        }

        uint extend = (uint)((SR & 0x0010) != 0 ? 1 : 0);
        uint mask = SizeMask(size);
        uint result = subtract
            ? unchecked(destination - source - extend)
            : unchecked(destination + source + extend);

        if (memoryMode)
        {
            WriteMemory(destinationAddress, size, result);
        }
        else
        {
            WriteDataRegister(destinationRegister, size, result);
        }

        SetAddSubXFlags(destination, source, result, extend, size, subtract);
        return memoryMode ? 18 : 8;
    }

    private int ExecuteDivide(ushort opcode)
    {
        int register = (opcode >> 9) & 0x7;
        bool signed = ((opcode >> 6) & 0x7) == 7;
        uint divisorRaw = ReadEffectiveAddress((opcode >> 3) & 0x7, opcode & 0x7, OperandSize.Word);
        ushort divisorWord = (ushort)divisorRaw;

        if (divisorWord == 0)
        {
            throw new M68kCpuException(DivideByZeroVector);
        }

        if (signed)
        {
            int dividend = unchecked((int)D[register]);
            int divisor = (short)divisorWord;
            int quotient = dividend / divisor;
            int remainder = dividend % divisor;
            if (quotient is < short.MinValue or > short.MaxValue)
            {
                SR |= 0x0002;
                return 140;
            }

            D[register] = (uint)(((ushort)remainder << 16) | ((ushort)quotient));
            SetNz((uint)(ushort)quotient, OperandSize.Word);
            ClearVc();
            return 158;
        }

        uint quotientUnsigned = D[register] / divisorWord;
        uint remainderUnsigned = D[register] % divisorWord;
        if (quotientUnsigned > 0xFFFF)
        {
            SR |= 0x0002;
            return 140;
        }

        D[register] = (remainderUnsigned << 16) | quotientUnsigned;
        SetNz(quotientUnsigned, OperandSize.Word);
        ClearVc();
        return 140;
    }

    private int ExecuteBcd(ushort opcode, bool subtract)
    {
        int sourceRegister = opcode & 0x7;
        int destinationRegister = (opcode >> 9) & 0x7;
        bool memoryMode = (opcode & 0x0008) != 0;
        byte source;
        byte destination;
        uint destinationAddress = 0;

        if (memoryMode)
        {
            A[sourceRegister] -= AddressRegisterIncrement(sourceRegister, OperandSize.Byte);
            A[destinationRegister] -= AddressRegisterIncrement(destinationRegister, OperandSize.Byte);
            source = _bus.ReadByte(A[sourceRegister]);
            destinationAddress = A[destinationRegister];
            destination = _bus.ReadByte(destinationAddress);
        }
        else
        {
            source = (byte)D[sourceRegister];
            destination = (byte)D[destinationRegister];
        }

        byte result = subtract
            ? SubtractBcd(destination, source)
            : AddBcd(destination, source);

        if (memoryMode)
        {
            _bus.WriteByte(destinationAddress, result);
        }
        else
        {
            WriteDataRegister(destinationRegister, OperandSize.Byte, result);
        }

        return memoryMode ? 18 : 6;
    }

    private byte AddBcd(byte destination, byte source)
    {
        int extend = (SR & 0x0010) != 0 ? 1 : 0;
        int corrected = destination + source + extend;
        if (((destination & 0x0F) + (source & 0x0F) + extend) > 9)
        {
            corrected += 0x06;
        }

        bool carry = corrected > 0x99;
        if (carry)
        {
            corrected += 0x60;
        }

        byte result = (byte)corrected;
        bool overflow = (~(destination ^ source) & (destination ^ result) & 0x80) != 0;
        SetBcdFlags(result, carry, overflow);
        return result;
    }

    private byte SubtractBcd(byte destination, byte source)
    {
        int extend = (SR & 0x0010) != 0 ? 1 : 0;
        int corrected = destination - source - extend;
        if (((destination & 0x0F) - (source & 0x0F) - extend) < 0)
        {
            corrected -= 0x06;
        }

        bool borrow = corrected < 0;
        if (borrow)
        {
            corrected -= 0x60;
        }

        byte result = (byte)corrected;
        bool overflow = ((destination ^ source) & (destination ^ result) & 0x80) != 0;
        SetBcdFlags(result, borrow, overflow);
        return result;
    }

    private void SetBcdFlags(byte result, bool carry, bool overflow)
    {
        bool previousZero = (SR & 0x0004) != 0;
        SR = (ushort)(SR & ~0x001F);
        if (carry)
        {
            SR |= 0x0011;
        }

        if (overflow)
        {
            SR |= 0x0002;
        }

        if (previousZero && result == 0)
        {
            SR |= 0x0004;
        }

        if ((result & 0x80) != 0)
        {
            SR |= 0x0008;
        }
    }

    private int ExecuteShiftRotate(ushort opcode)
    {
        if ((opcode & 0x00C0) == 0x00C0)
        {
            return ExecuteMemoryShift(opcode);
        }

        int register = opcode & 0x7;
        OperandSize size = DecodeSizeBits((opcode >> 6) & 0x3);
        bool countFromRegister = (opcode & 0x0020) != 0;
        int count = countFromRegister ? (int)(D[(opcode >> 9) & 0x7] & 0x3F) : (opcode >> 9) & 0x7;
        if (!countFromRegister && count == 0)
        {
            count = 8;
        }

        if (count == 0)
        {
            return 6;
        }

        int operation = (opcode >> 3) & 0x3;
        bool left = (opcode & 0x0100) != 0;
        uint value = ReadDataRegister(register, size);
        uint result = ApplyShiftRotate(value, count, size, operation, left);
        WriteDataRegister(register, size, result);
        SetNz(result, size);
        return 6 + (count * 2);
    }

    private int ExecuteMemoryShift(ushort opcode)
    {
        int mode = (opcode >> 3) & 0x7;
        int register = opcode & 0x7;
        int operation = (opcode >> 9) & 0x3;
        bool left = (opcode & 0x0100) != 0;
        uint value = ReadWritableEffectiveAddress(mode, register, OperandSize.Word, out WritableTarget target);
        uint result = ApplyShiftRotate(value, 1, OperandSize.Word, operation, left);
        WriteWritableTarget(target, result);
        SetNz(result, OperandSize.Word);
        return 8;
    }

    private uint ApplyShiftRotate(uint value, int count, OperandSize size, int operation, bool left)
    {
        uint mask = SizeMask(size);
        uint sign = SignBit(size);
        value &= mask;
        if (count == 0)
        {
            return value;
        }

        uint result = value;
        bool carry = false;
        bool overflow = false;
        bool extend = (SR & 0x0010) != 0;
        for (int i = 0; i < count; i++)
        {
            switch (operation)
            {
                case 0:
                    bool oldSign = (result & sign) != 0;
                    carry = left ? (result & sign) != 0 : (result & 1) != 0;
                    result = left ? (result << 1) & mask : (uint)(SignExtendForSize(result, size) >> 1);
                    if (left && oldSign != ((result & sign) != 0))
                    {
                        overflow = true;
                    }

                    break;
                case 1:
                    carry = left ? (result & sign) != 0 : (result & 1) != 0;
                    result = left ? (result << 1) & mask : result >> 1;
                    break;
                case 2:
                    bool oldExtend = extend;
                    carry = left ? (result & sign) != 0 : (result & 1) != 0;
                    result = left
                        ? ((result << 1) | (oldExtend ? 1u : 0u)) & mask
                        : (result >> 1) | (oldExtend ? sign : 0u);
                    extend = carry;
                    break;
                case 3:
                    carry = left ? (result & sign) != 0 : (result & 1) != 0;
                    result = left
                        ? ((result << 1) | (carry ? 1u : 0u)) & mask
                        : (result >> 1) | (carry ? sign : 0u);
                    break;
            }
        }

        SR = operation switch
        {
            3 => (ushort)(SR & ~0x0003),
            _ => (ushort)(SR & ~0x0013),
        };

        if (overflow)
        {
            SR |= 0x0002;
        }

        if (carry)
        {
            SR |= operation == 3 ? (ushort)0x0001 : (ushort)0x0011;
        }

        return result & mask;
    }

    private static int SignExtendForSize(uint value, OperandSize size)
    {
        return size switch
        {
            OperandSize.Byte => unchecked((sbyte)(value & 0xFF)),
            OperandSize.Word => unchecked((short)(value & 0xFFFF)),
            _ => unchecked((int)value),
        };
    }

    private int ExecuteOr(ushort opcode)
    {
        int register = (opcode >> 9) & 0x7;
        int opmode = (opcode >> 6) & 0x7;
        int mode = (opcode >> 3) & 0x7;
        int eaRegister = opcode & 0x7;

        if (opmode is 0 or 1 or 2)
        {
            OperandSize size = DecodeSizeBits(opmode);
            uint result = ReadDataRegister(register, size) | ReadEffectiveAddress(mode, eaRegister, size);
            WriteDataRegister(register, size, result);
            SetNz(result, size);
            ClearVc();
            return 4;
        }

        if (opmode is 4 or 5 or 6)
        {
            OperandSize size = DecodeSizeBits(opmode - 4);
            uint result = ReadWritableEffectiveAddress(mode, eaRegister, size, out WritableTarget target) | ReadDataRegister(register, size);
            WriteWritableTarget(target, result);
            SetNz(result, size);
            ClearVc();
            return 8;
        }

        throw new M68kException($"Unsupported OR opcode ${opcode:X4}");
    }

    private int ExecuteAnd(ushort opcode)
    {
        int register = (opcode >> 9) & 0x7;
        int opmode = (opcode >> 6) & 0x7;
        int mode = (opcode >> 3) & 0x7;
        int eaRegister = opcode & 0x7;

        if (opmode is 0 or 1 or 2)
        {
            OperandSize size = DecodeSizeBits(opmode);
            uint result = ReadDataRegister(register, size) & ReadEffectiveAddress(mode, eaRegister, size);
            WriteDataRegister(register, size, result);
            SetNz(result, size);
            ClearVc();
            return 4;
        }

        if (opmode is 4 or 5 or 6)
        {
            OperandSize size = DecodeSizeBits(opmode - 4);
            uint result = ReadWritableEffectiveAddress(mode, eaRegister, size, out WritableTarget target) & ReadDataRegister(register, size);
            WriteWritableTarget(target, result);
            SetNz(result, size);
            ClearVc();
            return 8;
        }

        throw new M68kException($"Unsupported AND opcode ${opcode:X4}");
    }

    private int ExecuteMultiply(ushort opcode)
    {
        int register = (opcode >> 9) & 0x7;
        int opmode = (opcode >> 6) & 0x7;
        int mode = (opcode >> 3) & 0x7;
        int eaRegister = opcode & 0x7;
        ushort source = (ushort)ReadEffectiveAddress(mode, eaRegister, OperandSize.Word);
        uint result = opmode == 3
            ? source * (D[register] & 0xFFFFu)
            : unchecked((uint)((short)source * (int)(short)(D[register] & 0xFFFF)));

        D[register] = result;
        SetNz(result, OperandSize.Long);
        ClearVc();
        return 70;
    }

    private int ExecuteExchange(ushort opcode)
    {
        int rx = (opcode >> 9) & 0x7;
        int ry = opcode & 0x7;
        uint temp;
        switch (opcode & 0xF1F8)
        {
            case 0xC140:
                temp = D[rx];
                D[rx] = D[ry];
                D[ry] = temp;
                break;
            case 0xC148:
                temp = A[rx];
                A[rx] = A[ry];
                A[ry] = temp;
                break;
            case 0xC188:
                temp = D[rx];
                D[rx] = A[ry];
                A[ry] = temp;
                break;
        }

        return 6;
    }

    private int ExecuteTest(ushort opcode)
    {
        OperandSize size = DecodeSizeBits((opcode >> 6) & 0x3);
        int mode = (opcode >> 3) & 0x7;
        int register = opcode & 0x7;
        uint value = ReadEffectiveAddress(mode, register, size);

        SetNz(value, size);
        ClearVc();
        return 4;
    }

    private int ExecuteTas(ushort opcode)
    {
        int mode = (opcode >> 3) & 0x7;
        int register = opcode & 0x7;
        uint value = ReadWritableEffectiveAddress(mode, register, OperandSize.Byte, out WritableTarget target);
        SetNz(value, OperandSize.Byte);
        ClearVc();
        WriteWritableTarget(target, value | 0x80);
        return 14;
    }

    private int ExecuteDbcc(ushort opcode)
    {
        int condition = (opcode >> 8) & 0xF;
        int register = opcode & 0x7;
        uint displacementAddress = PC;
        short displacement = (short)FetchWord();

        if (ConditionTrue(condition))
        {
            return 12;
        }

        ushort counter = (ushort)((D[register] & 0xFFFF) - 1);
        D[register] = (D[register] & 0xFFFF_0000) | counter;
        if (counter != 0xFFFF)
        {
            PC = NormalizePc(unchecked((uint)(displacementAddress + displacement)));
            return 10;
        }

        return 14;
    }

    private int ExecuteSetCondition(ushort opcode)
    {
        int condition = (opcode >> 8) & 0xF;
        int mode = (opcode >> 3) & 0x7;
        int register = opcode & 0x7;
        CalculateWritableTarget(mode, register, OperandSize.Byte, out WritableTarget target);
        WriteWritableTarget(target, ConditionTrue(condition) ? 0xFFu : 0u);
        return mode == 0 ? 4 : 8;
    }

    private int ExecuteChk(ushort opcode)
    {
        int register = (opcode >> 9) & 0x7;
        int mode = (opcode >> 3) & 0x7;
        int eaRegister = opcode & 0x7;
        short bound = (short)(ushort)ReadEffectiveAddress(mode, eaRegister, OperandSize.Word);
        short value = (short)(D[register] & 0xFFFF);
        if (value < 0 || value > bound)
        {
            SetNz((uint)(ushort)value, OperandSize.Word);
            throw new M68kCpuException(ChkInstructionVector);
        }

        return 10;
    }

    private int ExecuteAddSubQuick(ushort opcode, bool subtract)
    {
        int amount = (opcode >> 9) & 0x7;
        if (amount == 0)
        {
            amount = 8;
        }

        OperandSize size = DecodeSizeBits((opcode >> 6) & 0x3);
        int mode = (opcode >> 3) & 0x7;
        int register = opcode & 0x7;

        if (mode == 1)
        {
            A[register] = subtract ? A[register] - (uint)amount : A[register] + (uint)amount;
            return 8;
        }

        uint destination = ReadWritableEffectiveAddress(mode, register, size, out WritableTarget target);
        uint result = subtract ? destination - (uint)amount : destination + (uint)amount;
        WriteWritableTarget(target, result);

        if (subtract)
        {
            SetSubFlags(destination, (uint)amount, result, size);
        }
        else
        {
            SetAddFlags(destination, (uint)amount, result, size);
        }

        return 8;
    }

    private int ExecuteBranch(ushort opcode)
    {
        int condition = (opcode >> 8) & 0xF;
        int displacement = (sbyte)(opcode & 0xFF);
        uint displacementBase = PC;
        if ((opcode & 0xFF) == 0)
        {
            displacementBase = PC;
            displacement = (short)FetchWord();
        }

        bool take = condition == 1 || ConditionTrue(condition);
        if (take)
        {
            if (condition == 1)
            {
                PushLong(PC);
            }

            PC = NormalizePc(unchecked((uint)(displacementBase + displacement)));
        }

        return take ? 10 : 8;
    }

    private uint ReadEffectiveAddress(int mode, int register, OperandSize size)
    {
        return mode switch
        {
            0 => ReadDataRegister(register, size),
            1 when size != OperandSize.Byte => ReadAddressRegister(register, size),
            1 => throw new M68kCpuException(IllegalInstructionVector),
            2 => ReadMemory(A[register], size),
            3 => ReadPostIncrement(register, size),
            4 => ReadPreDecrement(register, size),
            5 => ReadMemory(unchecked((uint)(A[register] + (short)FetchWord())), size),
            6 => ReadMemory(CalculateIndexedAddress(A[register]), size),
            7 when register == 0 => ReadMemory((uint)(short)FetchWord(), size),
            7 when register == 1 => ReadMemory(FetchLong(), size),
            7 when register == 2 => ReadMemory(CalculatePcDisplacementAddress(), size),
            7 when register == 3 => ReadMemory(CalculateIndexedAddress(PC), size),
            7 when register == 4 => ReadImmediate(size),
            7 when register is >= 5 and <= 7 => throw new M68kCpuException(IllegalInstructionVector),
            _ => throw new M68kException($"Unsupported read effective address mode {mode}, register {register}, size {size}"),
        };
    }

    private uint ReadWritableEffectiveAddress(int mode, int register, OperandSize size, out WritableTarget target)
    {
        switch (mode)
        {
            case 0:
                target = WritableTarget.DataRegister(register, size);
                return ReadDataRegister(register, size);
            case 1:
                throw new M68kCpuException(IllegalInstructionVector);
            case 2:
                return ReadWritableMemory(A[register], size, out target);
            case 3:
            {
                uint address = A[register];
                A[register] += AddressRegisterIncrement(register, size);
                return ReadWritableMemory(address, size, out target);
            }
            case 4:
            {
                A[register] -= AddressRegisterIncrement(register, size);
                return ReadWritableMemory(A[register], size, out target);
            }
            case 5:
                return ReadWritableMemory(unchecked((uint)(A[register] + (short)FetchWord())), size, out target);
            case 6:
                return ReadWritableMemory(CalculateIndexedAddress(A[register]), size, out target);
            case 7 when register == 0:
                return ReadWritableMemory((uint)(short)FetchWord(), size, out target);
            case 7 when register == 1:
                return ReadWritableMemory(FetchLong(), size, out target);
            case 7:
                throw new M68kCpuException(IllegalInstructionVector);
            default:
                throw new M68kException($"Unsupported writable effective address mode {mode}, register {register}, size {size}");
        }
    }

    private uint ReadWritableMemory(uint address, OperandSize size, out WritableTarget target)
    {
        target = WritableTarget.Memory(address, size);
        return ReadMemory(address, size);
    }

    private void CalculateWritableTarget(int mode, int register, OperandSize size, out WritableTarget target)
    {
        switch (mode)
        {
            case 0:
                target = WritableTarget.DataRegister(register, size);
                break;
            case 1:
                throw new M68kCpuException(IllegalInstructionVector);
            case 2:
                target = WritableTarget.Memory(A[register], size);
                break;
            case 3:
            {
                uint address = A[register];
                A[register] += AddressRegisterIncrement(register, size);
                target = WritableTarget.Memory(address, size);
                break;
            }
            case 4:
                A[register] -= AddressRegisterIncrement(register, size);
                target = WritableTarget.Memory(A[register], size);
                break;
            case 5:
                target = WritableTarget.Memory(unchecked((uint)(A[register] + (short)FetchWord())), size);
                break;
            case 6:
                target = WritableTarget.Memory(CalculateIndexedAddress(A[register]), size);
                break;
            case 7 when register == 0:
                target = WritableTarget.Memory((uint)(short)FetchWord(), size);
                break;
            case 7 when register == 1:
                target = WritableTarget.Memory(FetchLong(), size);
                break;
            case 7:
                throw new M68kCpuException(IllegalInstructionVector);
            default:
                throw new M68kException($"Unsupported writable effective address mode {mode}, register {register}, size {size}");
        }
    }

    private void WriteWritableTarget(WritableTarget target, uint value)
    {
        if (target.IsDataRegister)
        {
            WriteDataRegister(target.Register, target.Size, value);
            return;
        }

        WriteMemory(target.Address, target.Size, value);
    }

    private void WriteEffectiveAddress(int mode, int register, OperandSize size, uint value)
    {
        switch (mode)
        {
            case 0:
                WriteDataRegister(register, size, value);
                break;
            case 1 when size != OperandSize.Byte:
                A[register] = size == OperandSize.Word ? SignExtendWord((ushort)value) : value;
                break;
            case 1:
                throw new M68kCpuException(IllegalInstructionVector);
            case 2:
                WriteMemory(A[register], size, value);
                break;
            case 3:
                WriteMemory(A[register], size, value);
                A[register] += AddressRegisterIncrement(register, size);
                break;
            case 4:
                A[register] -= AddressRegisterIncrement(register, size);
                WriteMemory(A[register], size, value);
                break;
            case 5:
                WriteMemory(unchecked((uint)(A[register] + (short)FetchWord())), size, value);
                break;
            case 6:
                WriteMemory(CalculateIndexedAddress(A[register]), size, value);
                break;
            case 7 when register == 0:
                WriteMemory((uint)(short)FetchWord(), size, value);
                break;
            case 7 when register == 1:
                WriteMemory(FetchLong(), size, value);
                break;
            case 7 when register is >= 2 and <= 7:
                throw new M68kCpuException(IllegalInstructionVector);
            default:
                throw new M68kException($"Unsupported write effective address mode {mode}, register {register}, size {size}");
        }
    }

    private uint CalculateEffectiveAddress(int mode, int register)
    {
        return mode switch
        {
            2 => A[register],
            3 => A[register],
            4 => A[register] - 1,
            5 => unchecked((uint)(A[register] + (short)FetchWord())),
            6 => CalculateIndexedAddress(A[register]),
            7 when register == 0 => (uint)(short)FetchWord(),
            7 when register == 1 => FetchLong(),
            7 when register == 2 => CalculatePcDisplacementAddress(),
            7 when register == 3 => CalculateIndexedAddress(PC),
            _ => throw new M68kCpuException(IllegalInstructionVector),
        };
    }

    private uint CalculatePcDisplacementAddress()
    {
        uint extensionAddress = PC;
        return unchecked((uint)(extensionAddress + (short)FetchWord()));
    }

    private uint CalculateIndexedAddress(uint baseAddress)
    {
        uint extensionAddress = PC;
        ushort extension = FetchWord();
        int displacement = (sbyte)(extension & 0xFF);
        int indexRegister = (extension >> 12) & 0x7;
        bool addressRegister = (extension & 0x8000) != 0;
        bool longIndex = (extension & 0x0800) != 0;
        uint index = addressRegister ? A[indexRegister] : D[indexRegister];
        if (!longIndex)
        {
            index = SignExtendWord((ushort)index);
        }

        _ = extensionAddress;
        return unchecked((uint)(baseAddress + index + displacement));
    }

    private uint ReadPostIncrement(int register, OperandSize size)
    {
        uint value = ReadMemory(A[register], size);
        A[register] += AddressRegisterIncrement(register, size);
        return value;
    }

    private uint ReadPreDecrement(int register, OperandSize size)
    {
        A[register] -= AddressRegisterIncrement(register, size);
        return ReadMemory(A[register], size);
    }

    private uint ReadDataRegister(int register, OperandSize size)
    {
        return size switch
        {
            OperandSize.Byte => D[register] & 0xFF,
            OperandSize.Word => D[register] & 0xFFFF,
            OperandSize.Long => D[register],
            _ => throw new ArgumentOutOfRangeException(nameof(size)),
        };
    }

    private uint ReadAddressRegister(int register, OperandSize size)
    {
        return size switch
        {
            OperandSize.Word => A[register] & 0xFFFF,
            OperandSize.Long => A[register],
            _ => throw new M68kException("Address registers do not support byte access"),
        };
    }

    private void WriteDataRegister(int register, OperandSize size, uint value)
    {
        D[register] = size switch
        {
            OperandSize.Byte => (D[register] & 0xFFFF_FF00) | (value & 0xFF),
            OperandSize.Word => (D[register] & 0xFFFF_0000) | (value & 0xFFFF),
            OperandSize.Long => value,
            _ => throw new ArgumentOutOfRangeException(nameof(size)),
        };
    }

    private uint ReadMovemRegister(int bit, OperandSize size)
    {
        uint value = bit < 8 ? D[bit] : A[bit - 8];
        return size == OperandSize.Word ? value & 0xFFFF : value;
    }

    private uint ReadMovemPredecrementRegister(int bit, OperandSize size, int effectiveAddressRegister, uint originalAddressRegister)
    {
        int reversed = 15 - bit;
        uint value;
        if (reversed < 8)
        {
            value = D[reversed];
        }
        else
        {
            int addressRegister = reversed - 8;
            value = addressRegister == effectiveAddressRegister ? originalAddressRegister : A[addressRegister];
        }

        return size == OperandSize.Word ? value & 0xFFFF : value;
    }

    private void WriteMovemRegister(int bit, uint value)
    {
        if (bit < 8)
        {
            D[bit] = value;
        }
        else
        {
            A[bit - 8] = value;
        }
    }

    private uint ReadMemory(uint address, OperandSize size)
    {
        return size switch
        {
            OperandSize.Byte => _bus.ReadByte(address),
            OperandSize.Word => _bus.ReadWord(address),
            OperandSize.Long => _bus.ReadLong(address),
            _ => throw new ArgumentOutOfRangeException(nameof(size)),
        };
    }

    private void WriteMemory(uint address, OperandSize size, uint value)
    {
        switch (size)
        {
            case OperandSize.Byte:
                _bus.WriteByte(address, (byte)value);
                break;
            case OperandSize.Word:
                _bus.WriteWord(address, (ushort)value);
                break;
            case OperandSize.Long:
                _bus.WriteLong(address, value);
                break;
        }
    }

    private uint ReadImmediate(OperandSize size)
    {
        return size switch
        {
            OperandSize.Byte => (byte)FetchWord(),
            OperandSize.Word => FetchWord(),
            OperandSize.Long => FetchLong(),
            _ => throw new ArgumentOutOfRangeException(nameof(size)),
        };
    }

    private static OperandSize DecodeSizeBits(int bits)
    {
        return bits switch
        {
            0 => OperandSize.Byte,
            1 => OperandSize.Word,
            2 => OperandSize.Long,
            _ => throw new M68kCpuException(IllegalInstructionVector),
        };
    }

    private static uint AddressRegisterIncrement(int register, OperandSize size)
    {
        if (size == OperandSize.Byte && register == 7)
        {
            return 2;
        }

        return size switch
        {
            OperandSize.Byte => 1,
            OperandSize.Word => 2,
            OperandSize.Long => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(size)),
        };
    }

    private static uint SizeBytes(OperandSize size)
    {
        return size switch
        {
            OperandSize.Byte => 1,
            OperandSize.Word => 2,
            OperandSize.Long => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(size)),
        };
    }

    private static int CountBits(ushort value)
    {
        int count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }

        return count;
    }

    private bool ConditionTrue(int condition)
    {
        bool c = (SR & 0x0001) != 0;
        bool v = (SR & 0x0002) != 0;
        bool z = (SR & 0x0004) != 0;
        bool n = (SR & 0x0008) != 0;

        return condition switch
        {
            0 => true,
            1 => false,
            2 => !c && !z,
            3 => c || z,
            4 => !c,
            5 => c,
            6 => !z,
            7 => z,
            8 => !v,
            9 => v,
            10 => !n,
            11 => n,
            12 => n == v,
            13 => n != v,
            14 => !z && n == v,
            15 => z || n != v,
            _ => false,
        };
    }

    private ushort FetchWord()
    {
        ushort value = _bus.ReadWord(PC);
        PC = NormalizePc(PC + 2);
        return value;
    }

    private uint FetchLong()
    {
        uint value = _bus.ReadLong(PC);
        PC = NormalizePc(PC + 4);
        return value;
    }

    private void PushWord(ushort value)
    {
        A[7] -= 2;
        _bus.WriteWord(A[7], value);
    }

    private void PushLong(uint value)
    {
        A[7] -= 4;
        _bus.WriteLong(A[7], value);
    }

    private ushort PopWord()
    {
        ushort value = _bus.ReadWord(A[7]);
        A[7] += 2;
        return value;
    }

    private uint PopLong()
    {
        uint value = _bus.ReadLong(A[7]);
        A[7] += 4;
        return value;
    }

    private void SetNz(uint value, OperandSize size)
    {
        uint mask = size switch
        {
            OperandSize.Byte => 0xFF,
            OperandSize.Word => 0xFFFF,
            OperandSize.Long => 0xFFFF_FFFF,
            _ => throw new ArgumentOutOfRangeException(nameof(size)),
        };

        uint negativeBit = size switch
        {
            OperandSize.Byte => 0x80,
            OperandSize.Word => 0x8000,
            OperandSize.Long => 0x8000_0000,
            _ => throw new ArgumentOutOfRangeException(nameof(size)),
        };

        value &= mask;
        SR = (ushort)(SR & ~0x000C);
        if (value == 0)
        {
            SR |= 0x0004;
        }
        else if ((value & negativeBit) != 0)
        {
            SR |= 0x0008;
        }
    }

    private void SetAddFlags(uint destination, uint source, uint result, OperandSize size)
    {
        uint mask = SizeMask(size);
        uint sign = SignBit(size);
        destination &= mask;
        source &= mask;
        result &= mask;

        bool n = (result & sign) != 0;
        bool z = result == 0;
        bool v = (~(destination ^ source) & (result ^ destination) & sign) != 0;
        bool c = (((destination & source) | (~result & (destination | source))) & sign) != 0;
        SetFlags(n, z, v, c);
    }

    private void SetSubFlags(uint destination, uint source, uint result, OperandSize size)
    {
        uint mask = SizeMask(size);
        uint sign = SignBit(size);
        destination &= mask;
        source &= mask;
        result &= mask;

        bool n = (result & sign) != 0;
        bool z = result == 0;
        bool v = ((destination ^ source) & (result ^ destination) & sign) != 0;
        bool c = (((~destination & source) | (result & ~destination) | (result & source)) & sign) != 0;
        SetFlags(n, z, v, c);
    }

    private void SetAddSubXFlags(uint destination, uint source, uint result, uint extend, OperandSize size, bool subtract)
    {
        uint mask = SizeMask(size);
        uint sign = SignBit(size);
        destination &= mask;
        source &= mask;
        result &= mask;

        bool n = (result & sign) != 0;
        bool z = result == 0 && (SR & 0x0004) != 0;
        long signedDestination = SignExtendSized(destination, size);
        long signedSource = SignExtendSized(source, size);
        long signedResult = subtract
            ? signedDestination - signedSource - extend
            : signedDestination + signedSource + extend;
        bool v = signedResult < MinSigned(size) || signedResult > MaxSigned(size);
        bool c = subtract
            ? (ulong)destination < (ulong)source + extend
            : (ulong)destination + source + extend > mask;
        SetFlags(n, z, v, c);
    }

    private void SetZ(bool zero)
    {
        SR = zero ? (ushort)(SR | 0x0004) : (ushort)(SR & ~0x0004);
    }

    private void SetStatusRegister(ushort value)
    {
        bool wasSupervisor = (SR & 0x2000) != 0;
        bool willBeSupervisor = (value & 0x2000) != 0;

        if (wasSupervisor != willBeSupervisor)
        {
            uint activeStack = A[7];
            A[7] = USP;
            USP = activeStack;
        }

        SR = value;
    }

    private void SetFlags(bool n, bool z, bool v, bool c)
    {
        SR = (ushort)(SR & ~0x001F);
        if (c)
        {
            SR |= 0x0011;
        }

        if (v)
        {
            SR |= 0x0002;
        }

        if (z)
        {
            SR |= 0x0004;
        }

        if (n)
        {
            SR |= 0x0008;
        }
    }

    private void ClearVc()
    {
        SR = (ushort)(SR & ~0x0003);
    }

    private static uint SizeMask(OperandSize size)
    {
        return size switch
        {
            OperandSize.Byte => 0xFF,
            OperandSize.Word => 0xFFFF,
            OperandSize.Long => 0xFFFF_FFFF,
            _ => throw new ArgumentOutOfRangeException(nameof(size)),
        };
    }

    private static uint SignBit(OperandSize size)
    {
        return size switch
        {
            OperandSize.Byte => 0x80,
            OperandSize.Word => 0x8000,
            OperandSize.Long => 0x8000_0000,
            _ => throw new ArgumentOutOfRangeException(nameof(size)),
        };
    }

    private static long SignExtendSized(uint value, OperandSize size)
    {
        return size switch
        {
            OperandSize.Byte => (sbyte)(value & 0xFF),
            OperandSize.Word => (short)(value & 0xFFFF),
            OperandSize.Long => unchecked((int)value),
            _ => throw new ArgumentOutOfRangeException(nameof(size)),
        };
    }

    private static long MinSigned(OperandSize size)
    {
        return size switch
        {
            OperandSize.Byte => sbyte.MinValue,
            OperandSize.Word => short.MinValue,
            OperandSize.Long => int.MinValue,
            _ => throw new ArgumentOutOfRangeException(nameof(size)),
        };
    }

    private static long MaxSigned(OperandSize size)
    {
        return size switch
        {
            OperandSize.Byte => sbyte.MaxValue,
            OperandSize.Word => short.MaxValue,
            OperandSize.Long => int.MaxValue,
            _ => throw new ArgumentOutOfRangeException(nameof(size)),
        };
    }

    private static uint SignExtendWord(ushort value)
    {
        return unchecked((uint)(int)(short)value);
    }

    private static uint NormalizePc(uint value)
    {
        return value & AddressMask;
    }

    private readonly record struct WritableTarget(bool IsDataRegister, int Register, uint Address, OperandSize Size)
    {
        public static WritableTarget DataRegister(int register, OperandSize size)
        {
            return new WritableTarget(true, register, 0, size);
        }

        public static WritableTarget Memory(uint address, OperandSize size)
        {
            return new WritableTarget(false, 0, address, size);
        }
    }

    public readonly record struct M68kOpcodeAllocation(ushort Opcode, int Samples, long AllocatedBytes);
    public sealed record M68kState(uint[] D, uint[] A, uint PC, ushort SR, bool Stopped, long Cycles, uint USP);
    public readonly record struct M68kInstructionTrace(
        uint Pc,
        ushort Opcode,
        uint NextPc,
        ushort Sr,
        uint StackPointer,
        uint D0,
        uint D1,
        uint D2,
        uint D3,
        uint D4,
        uint D5,
        uint D6,
        uint D7,
        uint A0,
        uint A1,
        uint A2,
        uint A3,
        uint A4,
        uint A5,
        uint A6,
        int Cycles);
    public readonly record struct M68kInterruptTrace(int Level, int Vector, uint ReturnPc, uint HandlerPc, ushort OldSr, ushort NewSr, uint StackPointer);
    public readonly record struct M68kExceptionTrace(
        int Vector,
        uint OpcodePc,
        ushort Opcode,
        uint FramePc,
        uint HandlerPc,
        ushort OldSr,
        ushort NewSr,
        uint StackPointer,
        uint D0,
        uint D1,
        uint D2,
        uint D3,
        uint A0,
        uint A1,
        uint A2,
        uint A3,
        uint A4,
        uint A5,
        uint A6);
}
