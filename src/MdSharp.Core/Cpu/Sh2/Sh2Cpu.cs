namespace MdSharp.Core.Cpu.Sh2;

public sealed class Sh2Cpu
{
    private const uint TBit = 0x0000_0001;
    private const uint SBit = 0x0000_0002;
    private const uint QBit = 0x0000_0100;
    private const uint MBit = 0x0000_0200;
    private const uint SrWritableMask = TBit | SBit | 0x0000_00F0 | QBit | MBit;
    private const int GeneralIllegalInstructionVector = 4;
    private const int SlotIllegalInstructionVector = 6;

    private readonly ISh2Bus _bus;
    private readonly string _name;
    private uint? _delaySlotPcRelativeBase;
    private int _delaySlotWaitCycles;

    public Sh2Cpu(ISh2Bus bus, string name)
    {
        _bus = bus;
        _name = name;
    }

    public uint[] R { get; } = new uint[16];
    public uint[] BankedR { get; } = new uint[8];
    public uint PC { get; private set; }
    public uint PR { get; private set; }
    public uint GBR { get; private set; }
    public uint VBR { get; private set; }
    public uint MACH { get; private set; }
    public uint MACL { get; private set; }
    public uint SR { get; private set; }
    public long Cycles { get; private set; }
    public bool Halted { get; private set; }
    public ushort LastOpcode { get; private set; }
    public uint LastOpcodePc { get; private set; }
    public int UnhandledOpcodeCount { get; private set; }
    public ushort LastUnhandledOpcode { get; private set; }
    public uint LastUnhandledOpcodePc { get; private set; }
    public bool DelaySlotActive { get; private set; }
    public int PendingInterruptLevel { get; private set; }
    public int PendingInterruptVectorNumber { get; private set; }
    public bool HasAcceptablePendingInterrupt => PendingInterruptLevel != 0 && PendingInterruptLevel > ((SR >> 4) & 0x0F);
    public string Name => _name;
    public Action<int, int>? InterruptAccepted { get; set; }
    public Action<Sh2InstructionTrace>? InstructionObserver { get; set; }
    public Action<Sh2InterruptTrace>? InterruptObserver { get; set; }

    public void Reset(uint pc = 0)
    {
        Array.Clear(R);
        Array.Clear(BankedR);
        PC = pc;
        PR = 0;
        GBR = 0;
        VBR = 0;
        MACH = 0;
        MACL = 0;
        SR = 0x0000_00F0;
        Cycles = 0;
        Halted = false;
        LastOpcode = 0;
        LastOpcodePc = 0;
        UnhandledOpcodeCount = 0;
        LastUnhandledOpcode = 0;
        LastUnhandledOpcodePc = 0;
        DelaySlotActive = false;
        _delaySlotWaitCycles = 0;
        PendingInterruptLevel = 0;
        PendingInterruptVectorNumber = 0;
    }

    public void SetVbr(uint value)
    {
        VBR = value;
    }

    public void RequestInterrupt(int level, int? vectorNumber = null)
    {
        if (level is < 1 or > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }

        if (level >= PendingInterruptLevel)
        {
            PendingInterruptLevel = level;
            PendingInterruptVectorNumber = vectorNumber ?? (64 + level);
        }
    }

    public void ClearPendingInterrupt(int level, int vectorNumber)
    {
        if (PendingInterruptLevel == level && PendingInterruptVectorNumber == vectorNumber)
        {
            PendingInterruptLevel = 0;
            PendingInterruptVectorNumber = 0;
        }
    }

    public int Run(int maxInstructions)
    {
        int executed = 0;
        while ((!Halted || HasAcceptablePendingInterrupt) && executed < maxInstructions)
        {
            Step();
            executed++;
        }

        return executed;
    }

    public bool TryFastForwardDtBfLoop(int maxCycles, out int cycles)
    {
        cycles = 0;
        if (maxCycles < 2 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null)
        {
            return false;
        }

        if (_bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc, out ushort dtOpcode))
        {
            return false;
        }

        if ((dtOpcode & 0xF0FF) != 0x4010)
        {
            return false;
        }

        if (!peekBus.TryPeekWord(loopPc + 2, out ushort branchOpcode))
        {
            return false;
        }

        if ((branchOpcode & 0xFF00) != 0x8B00)
        {
            return false;
        }

        int displacement = (sbyte)branchOpcode;
        uint target = loopPc + 6 + (uint)(displacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        int register = (dtOpcode >> 8) & 0x0F;
        uint count = R[register];
        uint maxIterations = (uint)(maxCycles / 2);
        uint iterations = count == 0 ? maxIterations : Math.Min(count, maxIterations);
        if (iterations == 0)
        {
            return false;
        }

        R[register] = count - iterations;
        cycles = checked((int)(iterations * 2));
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 2;

        if (count != 0 && iterations == count)
        {
            SetT(true);
            PC = loopPc + 4;
        }
        else
        {
            SetT(false);
            PC = loopPc;
        }

        return true;
    }

    public bool TryFastForwardNopDtBfDelayLoop(int maxCycles, out int cycles)
    {
        cycles = 0;
        if (maxCycles < 3 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null)
        {
            return false;
        }

        if (_bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        int nopCount = 0;
        while (nopCount < 8)
        {
            if (!peekBus.TryPeekWord(loopPc + (uint)(nopCount * 2), out ushort opcode))
            {
                return false;
            }

            if (opcode != 0x0009)
            {
                break;
            }

            nopCount++;
        }

        if (nopCount == 0)
        {
            return false;
        }

        uint dtPc = loopPc + (uint)(nopCount * 2);
        if (!peekBus.TryPeekWord(dtPc, out ushort dtOpcode) ||
            (dtOpcode & 0xF0FF) != 0x4010 ||
            !peekBus.TryPeekWord(dtPc + 2, out ushort branchOpcode) ||
            (branchOpcode & 0xFF00) != 0x8B00)
        {
            return false;
        }

        int displacement = (sbyte)branchOpcode;
        uint target = dtPc + 6 + (uint)(displacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        int register = (dtOpcode >> 8) & 0x0F;
        uint count = R[register];
        int cyclesPerIteration = nopCount + 2;
        uint maxIterations = (uint)(maxCycles / cyclesPerIteration);
        uint iterations = count == 0 ? maxIterations : Math.Min(count, maxIterations);
        if (iterations == 0)
        {
            return false;
        }

        R[register] = count - iterations;
        cycles = checked((int)(iterations * cyclesPerIteration));
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = dtPc + 2;

        if (count != 0 && iterations == count)
        {
            SetT(true);
            PC = dtPc + 4;
        }
        else
        {
            SetT(false);
            PC = loopPc;
        }

        return true;
    }

    public bool TryFastForwardMovWStoreAddDtBfLoop(int maxCycles, Func<uint, ushort, bool> writeWord, int cyclesPerIteration, out int cycles)
    {
        cycles = 0;
        if (maxCycles < cyclesPerIteration ||
            cyclesPerIteration <= 0 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null)
        {
            return false;
        }

        if (_bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc, out ushort storeOpcode) ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort addOpcode) ||
            !peekBus.TryPeekWord(loopPc + 4, out ushort dtOpcode) ||
            !peekBus.TryPeekWord(loopPc + 6, out ushort branchOpcode))
        {
            return false;
        }

        if ((storeOpcode & 0xF00F) != 0x2001 ||
            (addOpcode & 0xF000) != 0x7000 ||
            (dtOpcode & 0xF0FF) != 0x4010 ||
            (branchOpcode & 0xFF00) != 0x8B00)
        {
            return false;
        }

        int addressRegister = (storeOpcode >> 8) & 0x0F;
        int sourceRegister = (storeOpcode >> 4) & 0x0F;
        int addRegister = (addOpcode >> 8) & 0x0F;
        int addImmediate = (sbyte)(byte)addOpcode;
        if (addRegister != addressRegister || addImmediate != 2)
        {
            return false;
        }

        int displacement = (sbyte)branchOpcode;
        uint target = loopPc + 10 + (uint)(displacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        int countRegister = (dtOpcode >> 8) & 0x0F;
        uint count = R[countRegister];
        if (count == 0)
        {
            return false;
        }

        uint maxIterations = (uint)(maxCycles / cyclesPerIteration);
        uint iterations = Math.Min(count, maxIterations);
        if (iterations == 0)
        {
            return false;
        }

        uint address = R[addressRegister];
        ushort value = (ushort)R[sourceRegister];
        uint completed = 0;
        while (completed < iterations)
        {
            if (!writeWord(address, value))
            {
                break;
            }

            completed++;
            address += 2;
        }

        if (completed == 0)
        {
            return false;
        }

        R[addressRegister] += completed * 2;
        R[countRegister] = count - completed;
        cycles = checked((int)(completed * (uint)cyclesPerIteration));
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 6;

        if (completed == count)
        {
            SetT(true);
            PC = loopPc + 8;
        }
        else
        {
            SetT(false);
            PC = loopPc;
        }

        return true;
    }

    public bool TryFastForwardMovLStoreAddDtBfLoop(int maxCycles, Func<uint, uint, bool> writeLong, int cyclesPerIteration, out int cycles)
    {
        cycles = 0;
        if (maxCycles < cyclesPerIteration ||
            cyclesPerIteration <= 0 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc, out ushort storeOpcode) ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort addOpcode) ||
            !peekBus.TryPeekWord(loopPc + 4, out ushort dtOpcode) ||
            !peekBus.TryPeekWord(loopPc + 6, out ushort branchOpcode))
        {
            return false;
        }

        if ((storeOpcode & 0xF00F) != 0x2002 ||
            (addOpcode & 0xF000) != 0x7000 ||
            (dtOpcode & 0xF0FF) != 0x4010 ||
            (branchOpcode & 0xFF00) != 0x8B00)
        {
            return false;
        }

        int addressRegister = (storeOpcode >> 8) & 0x0F;
        int sourceRegister = (storeOpcode >> 4) & 0x0F;
        int addRegister = (addOpcode >> 8) & 0x0F;
        int addImmediate = (sbyte)(byte)addOpcode;
        if (addRegister != addressRegister || addImmediate != 4)
        {
            return false;
        }

        int displacement = (sbyte)branchOpcode;
        uint target = loopPc + 10 + (uint)(displacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        int countRegister = (dtOpcode >> 8) & 0x0F;
        uint count = R[countRegister];
        if (count == 0)
        {
            return false;
        }

        uint maxIterations = (uint)(maxCycles / cyclesPerIteration);
        uint iterations = Math.Min(count, maxIterations);
        if (iterations == 0)
        {
            return false;
        }

        uint address = R[addressRegister];
        uint value = R[sourceRegister];
        uint completed = 0;
        while (completed < iterations)
        {
            if (!writeLong(address, value))
            {
                break;
            }

            completed++;
            address += 4;
        }

        if (completed == 0)
        {
            return false;
        }

        R[addressRegister] += completed * 4;
        R[countRegister] = count - completed;
        cycles = checked((int)(completed * (uint)cyclesPerIteration));
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 6;

        if (completed == count)
        {
            SetT(true);
            PC = loopPc + 8;
        }
        else
        {
            SetT(false);
            PC = loopPc;
        }

        return true;
    }

    public bool TryFastForwardMovWPostIncSwapPreDecDtBfSLoop(
        int maxCycles,
        Func<uint, ushort?> readWord,
        Func<uint, ushort, bool> writeWord,
        out int cycles)
    {
        cycles = 0;
        const int TakenIterationCycles = 5;
        const int FinalIterationCycles = 4;
        if (maxCycles < FinalIterationCycles ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc, out ushort loadOpcode) ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort dtOpcode) ||
            !peekBus.TryPeekWord(loopPc + 4, out ushort swapOpcode) ||
            !peekBus.TryPeekWord(loopPc + 6, out ushort branchOpcode) ||
            !peekBus.TryPeekWord(loopPc + 8, out ushort storeOpcode))
        {
            return false;
        }

        if ((loadOpcode & 0xF00F) != 0x6005 ||
            (dtOpcode & 0xF0FF) != 0x4010 ||
            (swapOpcode & 0xF0FF) != 0x6008 ||
            (branchOpcode & 0xFF00) != 0x8F00 ||
            (storeOpcode & 0xF00F) != 0x2005)
        {
            return false;
        }

        int valueRegister = (loadOpcode >> 8) & 0x0F;
        int sourceRegister = (loadOpcode >> 4) & 0x0F;
        int countRegister = (dtOpcode >> 8) & 0x0F;
        int swapDestination = (swapOpcode >> 8) & 0x0F;
        int swapSource = (swapOpcode >> 4) & 0x0F;
        int destinationRegister = (storeOpcode >> 8) & 0x0F;
        int storeSource = (storeOpcode >> 4) & 0x0F;
        if (swapDestination != valueRegister ||
            swapSource != valueRegister ||
            storeSource != valueRegister)
        {
            return false;
        }

        int displacement = (sbyte)branchOpcode;
        uint target = loopPc + 10 + (uint)(displacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        uint count = R[countRegister];
        if (count == 0)
        {
            return false;
        }

        uint maxIterations = (uint)(maxCycles / TakenIterationCycles);
        if (maxIterations == 0 && maxCycles >= FinalIterationCycles)
        {
            maxIterations = 1;
        }

        uint iterations = Math.Min(count, maxIterations);
        if (iterations == 0)
        {
            return false;
        }

        uint source = R[sourceRegister];
        uint destination = R[destinationRegister];
        uint completed = 0;
        ushort value = 0;
        while (completed < iterations)
        {
            ushort? read = readWord(source);
            if (read is null)
            {
                break;
            }

            source += 2;
            value = SwapByteWord(read.Value);
            uint remaining = count - completed - 1;
            if (remaining != 0)
            {
                destination -= 2;
                if (!writeWord(destination, value))
                {
                    break;
                }
            }

            completed++;
        }

        if (completed == 0)
        {
            return false;
        }

        R[sourceRegister] = source;
        R[destinationRegister] = destination;
        R[valueRegister] = SignExtend16(value);
        R[countRegister] = count - completed;
        bool completedLoop = completed == count;
        cycles = completedLoop
            ? checked((int)(((completed - 1) * TakenIterationCycles) + FinalIterationCycles))
            : checked((int)(completed * TakenIterationCycles));
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 6;

        if (completedLoop)
        {
            SetT(true);
            PC = loopPc + 8;
        }
        else
        {
            SetT(false);
            PC = loopPc;
        }

        return true;
    }

    public bool TryFastForwardMovWPostIncStoreAddRegDtBfLoop(
        int maxCycles,
        Func<uint, ushort?> readWord,
        Func<uint, ushort, bool> writeWord,
        out int cycles)
    {
        cycles = 0;
        const int TakenIterationCycles = 6;
        const int FinalIterationCycles = 4;
        const int MinimumBurstCycles = TakenIterationCycles * 4096;
        if (maxCycles <= 0 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc, out ushort loadOpcode) ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort storeOpcode) ||
            !peekBus.TryPeekWord(loopPc + 4, out ushort addOpcode) ||
            !peekBus.TryPeekWord(loopPc + 6, out ushort dtOpcode) ||
            !peekBus.TryPeekWord(loopPc + 8, out ushort branchOpcode))
        {
            return false;
        }

        if ((loadOpcode & 0xF00F) != 0x6005 ||
            (storeOpcode & 0xF00F) != 0x2001 ||
            (addOpcode & 0xF00F) != 0x300C ||
            (dtOpcode & 0xF0FF) != 0x4010 ||
            (branchOpcode & 0xFF00) != 0x8B00)
        {
            return false;
        }

        int valueRegister = (loadOpcode >> 8) & 0x0F;
        int sourceRegister = (loadOpcode >> 4) & 0x0F;
        int storeDestination = (storeOpcode >> 8) & 0x0F;
        int storeSource = (storeOpcode >> 4) & 0x0F;
        int addDestination = (addOpcode >> 8) & 0x0F;
        int addSource = (addOpcode >> 4) & 0x0F;
        int countRegister = (dtOpcode >> 8) & 0x0F;
        if (valueRegister != 0 ||
            storeSource != valueRegister ||
            addDestination != storeDestination)
        {
            return false;
        }

        int displacement = (sbyte)branchOpcode;
        uint target = loopPc + 12 + (uint)(displacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        uint count = R[countRegister];
        if (count == 0)
        {
            return false;
        }

        int effectiveMaxCycles = Math.Max(maxCycles, MinimumBurstCycles);
        uint maxIterations = (uint)(effectiveMaxCycles / TakenIterationCycles);
        if (maxIterations == 0 && effectiveMaxCycles >= FinalIterationCycles)
        {
            maxIterations = 1;
        }

        uint iterations = Math.Min(count, maxIterations);
        if (iterations == 0)
        {
            return false;
        }

        uint source = R[sourceRegister];
        uint destination = R[storeDestination];
        uint stride = R[addSource];
        uint completed = 0;
        ushort value = 0;
        while (completed < iterations)
        {
            ushort? read = readWord(source);
            if (read is null)
            {
                break;
            }

            value = read.Value;
            if (!writeWord(destination, value))
            {
                break;
            }

            source += 2;
            destination += stride;
            completed++;
        }

        if (completed == 0)
        {
            return false;
        }

        R[sourceRegister] = source;
        R[storeDestination] = destination;
        R[valueRegister] = SignExtend16(value);
        R[countRegister] = count - completed;
        bool completedLoop = completed == count;
        cycles = completedLoop
            ? checked((int)(((completed - 1) * TakenIterationCycles) + FinalIterationCycles))
            : checked((int)(completed * TakenIterationCycles));
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 8;

        if (completedLoop)
        {
            SetT(true);
            PC = loopPc + 10;
        }
        else
        {
            SetT(false);
            PC = loopPc;
        }

        return true;
    }

    public bool TryFastForwardSdramLinkedListInsertRoutine(
        int maxCycles,
        Func<uint, uint?> readLong,
        Func<uint, uint, bool> writeLong,
        Action<Sh2LinkedListTrace>? observer,
        out int cycles)
    {
        cycles = 0;
        const int HalfLoopCycles = 5;
        const int UpdateCycles = 8;
        const int MaxNodes = 4096;
        if (maxCycles <= 0 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC switch
        {
            _ when PC >= 18 && MatchesLinkedListLoop(peekBus, PC - 18) => PC - 18,
            _ when PC >= 16 && MatchesLinkedListLoop(peekBus, PC - 16) => PC - 16,
            _ when PC >= 14 && MatchesLinkedListLoop(peekBus, PC - 14) => PC - 14,
            _ when PC >= 12 && MatchesLinkedListLoop(peekBus, PC - 12) => PC - 12,
            _ when PC >= 10 && MatchesLinkedListLoop(peekBus, PC - 10) => PC - 10,
            _ when PC >= 8 && MatchesLinkedListLoop(peekBus, PC - 8) => PC - 8,
            _ when PC >= 6 && MatchesLinkedListLoop(peekBus, PC - 6) => PC - 6,
            _ when PC >= 4 && MatchesLinkedListLoop(peekBus, PC - 4) => PC - 4,
            _ when PC >= 2 && MatchesLinkedListLoop(peekBus, PC - 2) => PC - 2,
            _ when MatchesLinkedListLoop(peekBus, PC) => PC,
            _ => 0,
        };

        if (loopPc == 0 ||
            !peekBus.TryPeekWord(loopPc + 20, out ushort readNext) ||
            !peekBus.TryPeekWord(loopPc + 22, out ushort storeNewNext) ||
            !peekBus.TryPeekWord(loopPc + 24, out ushort storeNewPrev) ||
            !peekBus.TryPeekWord(loopPc + 26, out ushort storeOldPrev) ||
            !peekBus.TryPeekWord(loopPc + 28, out ushort rts) ||
            !peekBus.TryPeekWord(loopPc + 30, out ushort storePrevNext) ||
            readNext != 0x5120 ||
            storeNewNext != 0x1410 ||
            storeNewPrev != 0x1421 ||
            storeOldPrev != 0x1141 ||
            rts != 0x000B ||
            storePrevNext != 0x1240)
        {
            return false;
        }

        uint threshold = R[0];
        int consumed = 0;
        bool isRunlengthSdkRoutine = IsRunlengthSdkLinkedListRoutine(loopPc);
        bool secondHalf = PC == loopPc + 10;
        if (PC == loopPc + 2 || PC == loopPc + 4)
        {
            uint node = R[1];
            uint? next = readLong(node + 4);
            if (next is null ||
                !LooksLikeRunlengthNode(node, R[4]) ||
                !LooksLikeRunlengthNode(next.Value, R[4]) ||
                !LooksLikeRunlengthNode(R[4], R[4]))
            {
                if (isRunlengthSdkRoutine && LooksLikeRunlengthNode(R[4], R[4]))
                {
                    return TryCompleteLinkedListNoOp(loopPc, consumed, R[2], out cycles);
                }

                return false;
            }

            R[2] = node;
            R[1] = next.Value;
            bool initialMatch = (int)R[3] >= (int)threshold;
            SetT(initialMatch);
            consumed += PC == loopPc + 2 ? 3 : 2;
            LastOpcode = 0x8904;
            LastOpcodePc = loopPc + 8;
            observer?.Invoke(new Sh2LinkedListTrace(_name, loopPc + 8, "initial-resume", threshold, R[2], R[1], R[3], initialMatch, Cycles + consumed, R[4]));
            if (initialMatch)
            {
                return TryCompleteLinkedListInsert(loopPc, consumed, R[2], out cycles);
            }

            secondHalf = true;
        }
        else if (PC == loopPc + 6)
        {
            bool initialMatch = (int)R[3] >= (int)threshold;
            SetT(initialMatch);
            consumed++;
            LastOpcode = 0x8904;
            LastOpcodePc = loopPc + 8;
            observer?.Invoke(new Sh2LinkedListTrace(_name, loopPc + 8, "initial", threshold, R[2], R[1], R[3], initialMatch, Cycles + consumed, R[4]));
            if (initialMatch)
            {
                return TryCompleteLinkedListInsert(loopPc, consumed, R[2], out cycles);
            }

            secondHalf = true;
        }
        else if (PC == loopPc + 8)
        {
            bool initialMatch = (SR & TBit) != 0;
            consumed++;
            LastOpcode = 0x8904;
            LastOpcodePc = loopPc + 8;
            observer?.Invoke(new Sh2LinkedListTrace(_name, loopPc + 8, "initial-branch", threshold, R[2], R[1], R[3], initialMatch, Cycles + consumed, R[4]));
            if (initialMatch)
            {
                return TryCompleteLinkedListInsert(loopPc, consumed, R[2], out cycles);
            }

            secondHalf = true;
        }
        else if (PC == loopPc + 12 || PC == loopPc + 14)
        {
            uint node = R[1];
            uint? next = readLong(node + 4);
            if (next is null ||
                !LooksLikeRunlengthNode(node, R[4]) ||
                !LooksLikeRunlengthNode(next.Value, R[4]) ||
                !LooksLikeRunlengthNode(R[4], R[4]))
            {
                if (isRunlengthSdkRoutine && LooksLikeRunlengthNode(R[4], R[4]))
                {
                    return TryCompleteLinkedListNoOp(loopPc, consumed, R[2], out cycles);
                }

                return false;
            }

            R[2] = node;
            R[1] = next.Value;
            bool secondMatch = (int)R[3] >= (int)threshold;
            SetT(secondMatch);
            consumed += PC == loopPc + 12 ? 3 : 2;
            LastOpcode = 0x8BF5;
            LastOpcodePc = loopPc + 18;
            observer?.Invoke(new Sh2LinkedListTrace(_name, loopPc + 18, "second-resume", threshold, R[2], R[1], R[3], secondMatch, Cycles + consumed, R[4]));
            if (secondMatch)
            {
                return TryCompleteLinkedListInsert(loopPc, consumed, R[2], out cycles);
            }
        }
        else if (PC == loopPc + 16)
        {
            bool secondMatch = (int)R[3] >= (int)threshold;
            SetT(secondMatch);
            consumed++;
            LastOpcode = 0x8BF5;
            LastOpcodePc = loopPc + 18;
            observer?.Invoke(new Sh2LinkedListTrace(_name, loopPc + 18, "second-compare", threshold, R[2], R[1], R[3], secondMatch, Cycles + consumed, R[4]));
            if (secondMatch)
            {
                return TryCompleteLinkedListInsert(loopPc, consumed, R[2], out cycles);
            }
        }
        else if (PC == loopPc + 18)
        {
            bool secondMatch = (SR & TBit) != 0;
            consumed++;
            LastOpcode = 0x8BF5;
            LastOpcodePc = loopPc + 18;
            observer?.Invoke(new Sh2LinkedListTrace(_name, loopPc + 18, "second-branch", threshold, R[2], R[1], R[3], secondMatch, Cycles + consumed, R[4]));
            if (secondMatch)
            {
                return TryCompleteLinkedListInsert(loopPc, consumed, R[2], out cycles);
            }
        }

        HashSet<uint> visitedNodes = [];
        bool visitedNewNode = false;
        for (int nodes = 0; nodes < MaxNodes; nodes++)
        {
            if (!secondHalf)
            {
                uint node = R[1];
                if (!LooksLikeRunlengthNode(node, R[4]) ||
                    !LooksLikeRunlengthNode(R[4], R[4]))
                {
                    if (isRunlengthSdkRoutine && LooksLikeRunlengthNode(R[4], R[4]))
                    {
                        return TryCompleteLinkedListNoOp(loopPc, consumed, R[2], out cycles);
                    }

                    return false;
                }

                if (!visitedNodes.Add(node))
                {
                    observer?.Invoke(new Sh2LinkedListTrace(_name, loopPc + 8, "cycle", threshold, R[2], node, R[3], true, Cycles + consumed, R[4]));
                    if (visitedNewNode)
                    {
                        return TryCompleteLinkedListNoOp(loopPc, consumed, node, out cycles);
                    }

                    return TryCompleteLinkedListInsert(loopPc, consumed, node, out cycles);
                }

                visitedNewNode |= node == R[4];
                uint? value = readLong(node + 8);
                uint? next = readLong(node + 4);
                if (value is null ||
                    next is null ||
                    !LooksLikeRunlengthNode(next.Value, R[4]))
                {
                    if (isRunlengthSdkRoutine)
                    {
                        return TryCompleteLinkedListNoOp(loopPc, consumed, node, out cycles);
                    }

                    return false;
                }

                R[3] = value.Value;
                R[2] = node;
                R[1] = next.Value;
                bool match = (int)R[3] >= (int)threshold;
                SetT(match);
                consumed += HalfLoopCycles;
                LastOpcode = 0x8904;
                LastOpcodePc = loopPc + 8;
                observer?.Invoke(new Sh2LinkedListTrace(_name, loopPc + 8, "first", threshold, node, R[1], R[3], match, Cycles + consumed, R[4]));
                if (match)
                {
                    return TryCompleteLinkedListInsert(loopPc, consumed, R[2], out cycles);
                }
            }

            secondHalf = false;
            uint secondNode = R[1];
            if (!LooksLikeRunlengthNode(secondNode, R[4]) ||
                !LooksLikeRunlengthNode(R[4], R[4]))
            {
                if (isRunlengthSdkRoutine && LooksLikeRunlengthNode(R[4], R[4]))
                {
                    return TryCompleteLinkedListNoOp(loopPc, consumed, R[2], out cycles);
                }

                return false;
            }

            if (!visitedNodes.Add(secondNode))
            {
                observer?.Invoke(new Sh2LinkedListTrace(_name, loopPc + 18, "cycle", threshold, R[2], secondNode, R[3], true, Cycles + consumed, R[4]));
                if (visitedNewNode)
                {
                    return TryCompleteLinkedListNoOp(loopPc, consumed, secondNode, out cycles);
                }

                return TryCompleteLinkedListInsert(loopPc, consumed, secondNode, out cycles);
            }

            visitedNewNode |= secondNode == R[4];
            uint? secondValue = readLong(secondNode + 8);
            uint? secondNext = readLong(secondNode + 4);
            if (secondValue is null ||
                secondNext is null ||
                !LooksLikeRunlengthNode(secondNext.Value, R[4]))
            {
                if (isRunlengthSdkRoutine)
                {
                    return TryCompleteLinkedListNoOp(loopPc, consumed, secondNode, out cycles);
                }

                return false;
            }

            R[3] = secondValue.Value;
            R[2] = secondNode;
            R[1] = secondNext.Value;
            bool secondMatch = (int)R[3] >= (int)threshold;
            SetT(secondMatch);
            consumed += HalfLoopCycles;
            LastOpcode = 0x8BF5;
            LastOpcodePc = loopPc + 18;
            observer?.Invoke(new Sh2LinkedListTrace(_name, loopPc + 18, "second", threshold, secondNode, R[1], R[3], secondMatch, Cycles + consumed, R[4]));
            if (secondMatch)
            {
                return TryCompleteLinkedListInsert(loopPc, consumed, R[2], out cycles);
            }
        }

        return false;

        bool TryCompleteLinkedListNoOp(uint basePc, int searchCycles, uint current, out int completedCycles)
        {
            completedCycles = 0;
            if (current == R[4])
            {
                return false;
            }

            observer?.Invoke(new Sh2LinkedListTrace(
                _name,
                basePc + 28,
                "noop",
                threshold,
                current,
                R[1],
                R[3],
                true,
                Cycles + searchCycles,
                R[4],
                current,
                0,
                0,
                0,
                0,
                0,
                Completed: true,
                NoOp: true,
                RegisterR1: R[1],
                RegisterR2: R[2],
                RegisterR3: R[3],
                RegisterR4: R[4]));
            R[2] = current;
            PC = PR;
            LastOpcode = 0x000B;
            LastOpcodePc = basePc + 28;
            completedCycles = searchCycles + UpdateCycles;
            Cycles += completedCycles;
            return true;
        }

        bool TryCompleteLinkedListInsert(uint basePc, int searchCycles, uint current, out int completedCycles)
        {
            completedCycles = 0;
            uint newNode = R[4];
            uint? oldPrevious = readLong(current);
            if (oldPrevious is null ||
                !LooksLikeRunlengthNode(current, newNode) ||
                !LooksLikeRunlengthNode(newNode, newNode) ||
                !LooksLikeRunlengthNode(oldPrevious.Value, newNode))
            {
                return false;
            }

            if (current == newNode)
            {
                return false;
            }

            if (oldPrevious.Value == newNode)
            {
                observer?.Invoke(new Sh2LinkedListTrace(
                    _name,
                    basePc + 28,
                    "already-linked",
                    threshold,
                    current,
                    oldPrevious.Value,
                    R[3],
                    true,
                    Cycles + searchCycles,
                    newNode,
                    current,
                    oldPrevious.Value,
                    NoOp: true,
                    Completed: true,
                    RegisterR1: R[1],
                    RegisterR2: R[2],
                    RegisterR3: R[3],
                    RegisterR4: R[4]));
                R[1] = oldPrevious.Value;
                R[2] = current;
                PC = PR;
                LastOpcode = 0x000B;
                LastOpcodePc = basePc + 28;
                completedCycles = searchCycles + UpdateCycles;
                Cycles += completedCycles;
                return true;
            }

            if (!writeLong(newNode, oldPrevious.Value) ||
                !writeLong(newNode + 4, current) ||
                !writeLong(oldPrevious.Value + 4, newNode) ||
                !writeLong(current, newNode))
            {
                return false;
            }

            observer?.Invoke(new Sh2LinkedListTrace(
                _name,
                basePc + 28,
                "insert",
                threshold,
                current,
                oldPrevious.Value,
                R[3],
                true,
                Cycles + searchCycles,
                newNode,
                current,
                oldPrevious.Value,
                oldPrevious.Value,
                current,
                newNode,
                newNode,
                Completed: true,
                RegisterR1: R[1],
                RegisterR2: R[2],
                RegisterR3: R[3],
                RegisterR4: R[4]));
            R[1] = oldPrevious.Value;
            R[2] = current;
            PC = PR;
            LastOpcode = 0x000B;
            LastOpcodePc = basePc + 28;
            completedCycles = searchCycles + UpdateCycles;
            Cycles += completedCycles;
            return true;
        }

        static bool LooksLikeRunlengthNode(uint address, uint newNode)
        {
            if (address == 0)
            {
                return true;
            }

            if ((address & 0x0000_0001u) == 0 &&
                (address & 0xFE00_0000u) == 0x0600_0000u)
            {
                return true;
            }

            return false;
        }

        static bool IsRunlengthSdkLinkedListRoutine(uint loopPc)
        {
            return loopPc == 0x0603_040Cu;
        }
    }

    public bool TryFastForwardRunlengthSdkRechainRoutine(
        int maxCycles,
        Func<uint, uint?> readLong,
        Func<uint, uint, bool> writeLong,
        Action<Sh2RechainTrace>? observer,
        out int cycles)
    {
        cycles = 0;
        const uint RoutinePc = 0x0603_044Cu;
        const int MaxIterations = 4096;
        if (maxCycles <= 0 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus ||
            (PC != RoutinePc && PC is < RoutinePc + 0x10 or > RoutinePc + 0x18))
        {
            return false;
        }

        if (!MatchesRunlengthSdkRechainRoutine(peekBus, RoutinePc))
        {
            return false;
        }

        int consumed = 0;
        if (PC != RoutinePc)
        {
            return false;
        }

        HashSet<(uint Current, uint Tail, uint InsertPrevious, uint InsertNext)> visited = [];
        for (int i = 0; i < MaxIterations && consumed < maxCycles; i++)
        {
            uint current = R[3];
            if (!LooksLikeRunlengthRechainNode(current) ||
                !visited.Add((current, R[5], R[7], R[8])))
            {
                return CompleteNoOp(consumed, out cycles);
            }

            uint? currentValue = readLong(current + 8);
            uint? next = readLong(current + 4);
            uint? previous = readLong(current);
            if (currentValue is null ||
                next is null ||
                previous is null ||
                !LooksLikeRunlengthRechainNode(next.Value) ||
                !LooksLikeRunlengthRechainNode(previous.Value))
            {
                return CompleteNoOp(consumed, out cycles);
            }

            R[6] = currentValue.Value;
            R[4] = next.Value;
            R[1] = previous.Value;
            bool firstMatch = (int)R[9] >= (int)R[6];
            SetT(firstMatch);
            consumed += 4;

            if (!writeLong(R[1] + 4, R[4]) ||
                !writeLong(R[4], R[1]))
            {
                return false;
            }

            observer?.Invoke(new Sh2RechainTrace(
                _name,
                RoutinePc + 0x0C,
                "unlink",
                current,
                previous.Value,
                next.Value,
                currentValue.Value,
                R[5],
                R[7],
                R[8],
                R[9],
                firstMatch,
                Cycles + consumed,
                WritePreviousNext: R[4],
                WriteNextPrevious: R[1]));

            consumed += 2;
            if (!firstMatch)
            {
                consumed++;
                bool foundInsertPoint;
                do
                {
                    R[7] = R[8];
                    uint? insertNext = readLong(R[8] + 4);
                    if (insertNext is null ||
                        !LooksLikeRunlengthRechainNode(insertNext.Value))
                    {
                        return CompleteNoOp(consumed, out cycles);
                    }

                    R[8] = insertNext.Value;
                    uint? insertValue = readLong(R[8] + 8);
                    if (insertValue is null)
                    {
                        return CompleteNoOp(consumed, out cycles);
                    }

                    R[9] = insertValue.Value;
                    foundInsertPoint = (int)R[9] >= (int)R[6];
                    SetT(foundInsertPoint);
                    consumed += 5;
                    observer?.Invoke(new Sh2RechainTrace(
                        _name,
                        RoutinePc + 0x18,
                        "walk",
                        current,
                        R[1],
                        R[4],
                        R[6],
                        R[5],
                        R[7],
                        R[8],
                        R[9],
                        foundInsertPoint,
                        Cycles + consumed));

                    if (consumed >= maxCycles)
                    {
                        return CompleteNoOp(consumed, out cycles);
                    }
                }
                while (!foundInsertPoint);
            }
            else
            {
                consumed++;
            }

            if (!writeLong(R[7] + 4, current) ||
                !writeLong(R[8], current) ||
                !writeLong(current, R[7]))
            {
                return false;
            }

            R[7] = current;
            if (!writeLong(current + 4, R[8]))
            {
                return false;
            }

            observer?.Invoke(new Sh2RechainTrace(
                _name,
                RoutinePc + 0x24,
                "insert",
                current,
                R[1],
                R[4],
                R[6],
                R[5],
                R[7],
                R[8],
                R[9],
                true,
                Cycles + consumed,
                WritePreviousNext: current,
                WriteNextPrevious: current,
                WriteCurrentPrevious: R[7],
                WriteCurrentNext: R[8]));

            bool done = R[4] == R[5];
            SetT(done);
            consumed += 7;
            R[3] = R[4];
            consumed += 3;
            if (done)
            {
                PC = PR;
                LastOpcode = 0x000B;
                LastOpcodePc = RoutinePc + 0x28;
                cycles = consumed + 2;
                Cycles += cycles;
                return true;
            }

        }

        return CompleteNoOp(consumed, out cycles);

        bool CompleteNoOp(int consumedCycles, out int completedCycles)
        {
            PC = PR;
            LastOpcode = 0x000B;
            LastOpcodePc = RoutinePc + 0x28;
            completedCycles = Math.Max(1, consumedCycles + 2);
            Cycles += completedCycles;
            return true;
        }

        static bool LooksLikeRunlengthRechainNode(uint address)
        {
            return address == 0 ||
                ((address & 0x0000_0001u) == 0 &&
                (address & 0xFE00_0000u) == 0x0600_0000u);
        }
    }

    private static bool MatchesRunlengthSdkRechainRoutine(ISh2PeekBus peekBus, uint pc)
    {
        ushort[] opcodes =
        [
            0x5632, 0x5431, 0x3963, 0x5130,
            0x1141, 0x1410, 0x8905, 0x0009,
            0x6783, 0x5881, 0x5982, 0x3967,
            0x8BFA, 0x1731, 0x1830, 0x1370,
            0x6733, 0x1381, 0x3540, 0x8FEB,
            0x6343, 0x000B
        ];

        for (int i = 0; i < opcodes.Length; i++)
        {
            if (!peekBus.TryPeekWord(pc + (uint)(i * 2), out ushort opcode) ||
                opcode != opcodes[i])
            {
                return false;
            }
        }

        return true;
    }

    public bool TryFastForwardSdramLinkedListCmpGeLoop(int maxCycles, Func<uint, uint?> readLong, Action<Sh2LinkedListTrace>? observer, out int cycles)
    {
        cycles = 0;
        const int FirstHalfCycles = 5;
        const int FullLoopCycles = 10;
        const int MinimumBurstCycles = FullLoopCycles * 1024;
        if (maxCycles <= 0 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        bool startAtSecondHalf = false;
        bool startAtFirstCompare = false;
        if (!peekBus.TryPeekWord(loopPc, out ushort loadValueA) ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort moveNodeA) ||
            !peekBus.TryPeekWord(loopPc + 4, out ushort loadNextA) ||
            !peekBus.TryPeekWord(loopPc + 6, out ushort compareA) ||
            !peekBus.TryPeekWord(loopPc + 8, out ushort branchTrue) ||
            !peekBus.TryPeekWord(loopPc + 10, out ushort loadValueB) ||
            !peekBus.TryPeekWord(loopPc + 12, out ushort moveNodeB) ||
            !peekBus.TryPeekWord(loopPc + 14, out ushort loadNextB) ||
            !peekBus.TryPeekWord(loopPc + 16, out ushort compareB) ||
            !peekBus.TryPeekWord(loopPc + 18, out ushort branchFalse))
        {
            loopPc = PC - 10;
            startAtSecondHalf = true;
            if (!peekBus.TryPeekWord(loopPc, out loadValueA) ||
                !peekBus.TryPeekWord(loopPc + 2, out moveNodeA) ||
                !peekBus.TryPeekWord(loopPc + 4, out loadNextA) ||
                !peekBus.TryPeekWord(loopPc + 6, out compareA) ||
                !peekBus.TryPeekWord(loopPc + 8, out branchTrue) ||
                !peekBus.TryPeekWord(loopPc + 10, out loadValueB) ||
                !peekBus.TryPeekWord(loopPc + 12, out moveNodeB) ||
                !peekBus.TryPeekWord(loopPc + 14, out loadNextB) ||
                !peekBus.TryPeekWord(loopPc + 16, out compareB) ||
                !peekBus.TryPeekWord(loopPc + 18, out branchFalse))
            {
                loopPc = PC - 6;
                startAtSecondHalf = false;
                startAtFirstCompare = true;
                if (!peekBus.TryPeekWord(loopPc, out loadValueA) ||
                    !peekBus.TryPeekWord(loopPc + 2, out moveNodeA) ||
                    !peekBus.TryPeekWord(loopPc + 4, out loadNextA) ||
                    !peekBus.TryPeekWord(loopPc + 6, out compareA) ||
                    !peekBus.TryPeekWord(loopPc + 8, out branchTrue) ||
                    !peekBus.TryPeekWord(loopPc + 10, out loadValueB) ||
                    !peekBus.TryPeekWord(loopPc + 12, out moveNodeB) ||
                    !peekBus.TryPeekWord(loopPc + 14, out loadNextB) ||
                    !peekBus.TryPeekWord(loopPc + 16, out compareB) ||
                    !peekBus.TryPeekWord(loopPc + 18, out branchFalse))
                {
                    return false;
                }
            }
        }

        if (loadValueA != 0x5312 ||
            moveNodeA != 0x6213 ||
            loadNextA != 0x5111 ||
            compareA != 0x3303 ||
            branchTrue != 0x8904 ||
            loadValueB != 0x5312 ||
            moveNodeB != 0x6213 ||
            loadNextB != 0x5111 ||
            compareB != 0x3303 ||
            branchFalse != 0x8BF5)
        {
            return false;
        }

        uint exitPc = loopPc + 20;
        int effectiveMaxCycles = Math.Max(maxCycles, MinimumBurstCycles);
        int consumed = 0;
        uint threshold = R[0];
        if (startAtFirstCompare)
        {
            bool initialMatch = (int)R[3] >= (int)threshold;
            SetT(initialMatch);
            consumed = 1;
            LastOpcode = branchTrue;
            LastOpcodePc = loopPc + 8;
            observer?.Invoke(new Sh2LinkedListTrace(_name, loopPc + 8, "initial", threshold, R[2], R[1], R[3], initialMatch, Cycles + consumed, R[4]));
            if (initialMatch)
            {
                PC = exitPc;
                cycles = consumed;
                Cycles += cycles;
                return true;
            }

            startAtSecondHalf = true;
        }

        while (consumed + FirstHalfCycles <= effectiveMaxCycles)
        {
            if (!startAtSecondHalf)
            {
                uint node = R[1];
                uint? value = readLong(node + 8);
                uint? next = readLong(node + 4);
                if (value is null || next is null)
                {
                    break;
                }

                R[3] = value.Value;
                R[2] = node;
                R[1] = next.Value;
                bool match = (int)R[3] >= (int)threshold;
                SetT(match);
                consumed += FirstHalfCycles;
                LastOpcode = branchTrue;
                LastOpcodePc = loopPc + 8;
                observer?.Invoke(new Sh2LinkedListTrace(_name, loopPc + 8, "first", threshold, node, R[1], R[3], match, Cycles + consumed, R[4]));
                if (match)
                {
                    PC = exitPc;
                    cycles = consumed;
                    Cycles += cycles;
                    return true;
                }

                if (consumed + FirstHalfCycles > effectiveMaxCycles)
                {
                    PC = loopPc + 10;
                    cycles = consumed;
                    Cycles += cycles;
                    return true;
                }
            }

            startAtSecondHalf = false;
            uint secondNode = R[1];
            uint? secondValue = readLong(secondNode + 8);
            uint? secondNext = readLong(secondNode + 4);
            if (secondValue is null || secondNext is null)
            {
                break;
            }

            R[3] = secondValue.Value;
            R[2] = secondNode;
            R[1] = secondNext.Value;
            bool secondMatch = (int)R[3] >= (int)threshold;
            SetT(secondMatch);
            consumed += FirstHalfCycles;
            LastOpcode = branchFalse;
            LastOpcodePc = loopPc + 18;
            PC = secondMatch ? exitPc : loopPc;
            observer?.Invoke(new Sh2LinkedListTrace(_name, loopPc + 18, "second", threshold, secondNode, R[1], R[3], secondMatch, Cycles + consumed, R[4]));
            if (secondMatch)
            {
                cycles = consumed;
                Cycles += cycles;
                return true;
            }
        }

        if (consumed == 0)
        {
            return false;
        }

        PC = loopPc;
        cycles = consumed - (consumed % FullLoopCycles);
        if (cycles <= 0)
        {
            cycles = consumed;
        }

        Cycles += cycles;
        return true;
    }

    private static bool MatchesLinkedListLoop(ISh2PeekBus peekBus, uint loopPc)
    {
        return peekBus.TryPeekWord(loopPc, out ushort loadValueA) &&
            peekBus.TryPeekWord(loopPc + 2, out ushort moveNodeA) &&
            peekBus.TryPeekWord(loopPc + 4, out ushort loadNextA) &&
            peekBus.TryPeekWord(loopPc + 6, out ushort compareA) &&
            peekBus.TryPeekWord(loopPc + 8, out ushort branchTrue) &&
            peekBus.TryPeekWord(loopPc + 10, out ushort loadValueB) &&
            peekBus.TryPeekWord(loopPc + 12, out ushort moveNodeB) &&
            peekBus.TryPeekWord(loopPc + 14, out ushort loadNextB) &&
            peekBus.TryPeekWord(loopPc + 16, out ushort compareB) &&
            peekBus.TryPeekWord(loopPc + 18, out ushort branchFalse) &&
            loadValueA == 0x5312 &&
            moveNodeA == 0x6213 &&
            loadNextA == 0x5111 &&
            compareA == 0x3303 &&
            branchTrue == 0x8904 &&
            loadValueB == 0x5312 &&
            moveNodeB == 0x6213 &&
            loadNextB == 0x5111 &&
            compareB == 0x3303 &&
            branchFalse == 0x8BF5;
    }

    public bool TryFastForwardTstBfPollLoop(int maxCycles, out int cycles)
    {
        cycles = 0;
        if (maxCycles <= 0 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc, out ushort loadOpcode))
        {
            return false;
        }

        if ((loadOpcode & 0xFF00) != 0x8400)
        {
            return false;
        }

        if (!peekBus.TryPeekWord(loopPc + 2, out ushort testOpcode) ||
            !peekBus.TryPeekWord(loopPc + 4, out ushort branchOpcode))
        {
            return false;
        }

        if ((testOpcode & 0xFF00) != 0xC800 ||
            (branchOpcode & 0xFF00) != 0x8B00)
        {
            return false;
        }

        int displacement = (sbyte)branchOpcode;
        uint target = loopPc + 8 + (uint)(displacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        int register = (loadOpcode >> 4) & 0x0F;
        uint address = R[register] + (uint)(loadOpcode & 0x0F);
        if (!peekBus.TryPeekByte(address, out byte value))
        {
            return false;
        }

        byte mask = (byte)testOpcode;
        bool zero = (value & mask) == 0;
        if (zero)
        {
            return false;
        }

        cycles = maxCycles;
        Cycles += cycles;
        R[0] = (uint)(sbyte)value;
        SetT(false);
        PC = loopPc;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 4;
        return true;
    }

    public bool TryFastForwardGbrCmpEqBfPollLoop(int maxCycles, out int cycles)
    {
        cycles = 0;
        if (maxCycles <= 0 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc, out ushort loadOpcode) ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort compareOpcode) ||
            !peekBus.TryPeekWord(loopPc + 4, out ushort branchOpcode))
        {
            return false;
        }

        if ((branchOpcode & 0xFF00) != 0x8B00 ||
            (compareOpcode & 0xFF00) != 0x8800)
        {
            return false;
        }

        int displacement = (sbyte)branchOpcode;
        uint target = loopPc + 8 + (uint)(displacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        uint address;
        uint value;
        if ((loadOpcode & 0xFF00) == 0xC400)
        {
            address = GBR + (uint)((loadOpcode & 0xFF) * 1);
            if (!peekBus.TryPeekByte(address, out byte byteValue))
            {
                return false;
            }

            value = (uint)(sbyte)byteValue;
        }
        else if ((loadOpcode & 0xFF00) == 0xC500)
        {
            address = GBR + (uint)((loadOpcode & 0xFF) * 2);
            if (!peekBus.TryPeekWord(address, out ushort wordValue))
            {
                return false;
            }

            value = (uint)(short)wordValue;
        }
        else if ((loadOpcode & 0xFF00) == 0xC600)
        {
            address = GBR + (uint)((loadOpcode & 0xFF) * 4);
            if (!peekBus.TryPeekWord(address, out ushort high) ||
                !peekBus.TryPeekWord(address + 2, out ushort low))
            {
                return false;
            }

            value = (uint)((high << 16) | low);
        }
        else
        {
            return false;
        }

        byte immediate = (byte)compareOpcode;
        bool equal = value == (uint)(sbyte)immediate;
        if (equal)
        {
            return false;
        }

        cycles = maxCycles;
        Cycles += cycles;
        R[0] = value;
        SetT(false);
        PC = loopPc;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 4;
        return true;
    }

    public bool TryFastForwardGbrRegisterCmpEqBfPollLoop(int maxCycles, out int cycles)
    {
        cycles = 0;
        if (maxCycles <= 0 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc, out ushort loadOpcode) ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort compareOpcode) ||
            !peekBus.TryPeekWord(loopPc + 4, out ushort branchOpcode))
        {
            return false;
        }

        if ((branchOpcode & 0xFF00) != 0x8B00 ||
            (compareOpcode & 0xF00F) != 0x3000)
        {
            return false;
        }

        int compareN = (compareOpcode >> 8) & 0x0F;
        int compareM = (compareOpcode >> 4) & 0x0F;
        if (compareM != 0 && compareN != 0)
        {
            return false;
        }

        int displacement = (sbyte)branchOpcode;
        uint target = loopPc + 8 + (uint)(displacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        uint value;
        if ((loadOpcode & 0xFF00) == 0xC400)
        {
            uint address = GBR + (uint)(loadOpcode & 0xFF);
            if (!peekBus.TryPeekByte(address, out byte byteValue))
            {
                return false;
            }

            value = (uint)(sbyte)byteValue;
        }
        else if ((loadOpcode & 0xFF00) == 0xC500)
        {
            uint address = GBR + (uint)((loadOpcode & 0xFF) * 2);
            if (!peekBus.TryPeekWord(address, out ushort wordValue))
            {
                return false;
            }

            value = (uint)(short)wordValue;
        }
        else if ((loadOpcode & 0xFF00) == 0xC600)
        {
            uint address = GBR + (uint)((loadOpcode & 0xFF) * 4);
            if (!peekBus.TryPeekWord(address, out ushort high) ||
                !peekBus.TryPeekWord(address + 2, out ushort low))
            {
                return false;
            }

            value = (uint)((high << 16) | low);
        }
        else
        {
            return false;
        }

        R[0] = value;
        bool equal = R[compareN] == R[compareM];
        if (equal)
        {
            return false;
        }

        SetT(false);
        PC = loopPc;
        cycles = maxCycles;
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 4;
        return true;
    }

    public bool TryFastForwardGbrWordCmpGtBfPollLoop(int maxCycles, out int cycles)
    {
        cycles = 0;
        if (maxCycles <= 0 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        bool startAtLoad = true;
        ushort setupOpcode = 0;
        if (peekBus.TryPeekWord(PC, out ushort possibleSetupOpcode) &&
            (possibleSetupOpcode & 0xF000) == 0xE000 &&
            peekBus.TryPeekWord(PC + 2, out ushort possibleLoadOpcode) &&
            (possibleLoadOpcode & 0xFF00) == 0xC500)
        {
            setupOpcode = possibleSetupOpcode;
            loopPc = PC + 2;
            startAtLoad = false;
        }

        if (!peekBus.TryPeekWord(loopPc, out ushort loadOpcode) ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort compareOpcode) ||
            !peekBus.TryPeekWord(loopPc + 4, out ushort branchOpcode))
        {
            if (PC < 2 ||
                !peekBus.TryPeekWord(PC - 2, out loadOpcode) ||
                !peekBus.TryPeekWord(PC, out compareOpcode) ||
                !peekBus.TryPeekWord(PC + 2, out branchOpcode))
            {
                if (PC < 4 ||
                    !peekBus.TryPeekWord(PC - 4, out loadOpcode) ||
                    !peekBus.TryPeekWord(PC - 2, out compareOpcode) ||
                    !peekBus.TryPeekWord(PC, out branchOpcode))
                {
                    return false;
                }

                loopPc = PC - 4;
                startAtLoad = false;
            }
            else
            {
                loopPc = PC - 2;
                startAtLoad = false;
            }
        }

        if ((loadOpcode & 0xFF00) != 0xC500 ||
            (compareOpcode & 0xF00F) != 0x3007 ||
            (branchOpcode & 0xFF00) != 0x8B00)
        {
            return false;
        }

        int compareN = (compareOpcode >> 8) & 0x0F;
        int compareM = (compareOpcode >> 4) & 0x0F;
        if (compareN != 0 && compareM != 0)
        {
            return false;
        }

        int displacement = (sbyte)branchOpcode;
        uint target = loopPc + 8 + (uint)(displacement * 2);
        uint restartPc = loopPc;
        if (target != loopPc)
        {
            if (target != loopPc - 2 ||
                (setupOpcode == 0 && !peekBus.TryPeekWord(target, out setupOpcode)) ||
                (setupOpcode & 0xF000) != 0xE000 ||
                ((setupOpcode >> 8) & 0x0F) != compareM)
            {
                return false;
            }

            restartPc = target;
        }

        uint address = GBR + (uint)((loadOpcode & 0xFF) * 2);
        if (!peekBus.TryPeekWord(address, out ushort wordValue))
        {
            return false;
        }

        uint value = (uint)(short)wordValue;
        if (restartPc != loopPc)
        {
            R[compareM] = (uint)(sbyte)setupOpcode;
        }

        R[0] = value;
        bool greaterThan = (int)R[compareN] > (int)R[compareM];
        if (greaterThan)
        {
            return false;
        }

        SetT(false);
        PC = restartPc;
        cycles = Math.Max(1, maxCycles - (startAtLoad ? 0 : 1));
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 4;
        return true;
    }

    public bool TryFastForwardGbrCmpEqBfBraPollLoop(int maxCycles, out int cycles)
    {
        cycles = 0;
        if (maxCycles <= 0 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc, out ushort loadOpcode) ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort compareOpcode) ||
            !peekBus.TryPeekWord(loopPc + 4, out ushort branchFalseOpcode))
        {
            return false;
        }

        if ((branchFalseOpcode & 0xFF00) != 0x8B00 ||
            (compareOpcode & 0xFF00) != 0x8800)
        {
            return false;
        }

        int branchFalseDisplacement = (sbyte)branchFalseOpcode;
        uint exitPc = loopPc + 8 + (uint)(branchFalseDisplacement * 2);
        if (exitPc <= loopPc + 8 || exitPc > loopPc + 32)
        {
            return false;
        }

        uint braPc = exitPc - 4;
        for (uint pc = loopPc + 6; pc < braPc; pc += 2)
        {
            if (!peekBus.TryPeekWord(pc, out ushort nopOpcode) || nopOpcode != 0x0009)
            {
                return false;
            }
        }

        if (!peekBus.TryPeekWord(braPc, out ushort braOpcode) ||
            (braOpcode & 0xF000) != 0xA000 ||
            !peekBus.TryPeekWord(braPc + 2, out ushort delayOpcode) ||
            delayOpcode != 0x0009)
        {
            return false;
        }

        int braDisplacement = SignExtend12(braOpcode & 0x0FFF);
        uint target = braPc + 4 + (uint)(braDisplacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        uint value;
        if ((loadOpcode & 0xFF00) == 0xC400)
        {
            uint address = GBR + (uint)(loadOpcode & 0xFF);
            if (!peekBus.TryPeekByte(address, out byte byteValue))
            {
                return false;
            }

            value = (uint)(sbyte)byteValue;
        }
        else if ((loadOpcode & 0xFF00) == 0xC500)
        {
            uint address = GBR + (uint)((loadOpcode & 0xFF) * 2);
            if (!peekBus.TryPeekWord(address, out ushort wordValue))
            {
                return false;
            }

            value = (uint)(short)wordValue;
        }
        else if ((loadOpcode & 0xFF00) == 0xC600)
        {
            uint address = GBR + (uint)((loadOpcode & 0xFF) * 4);
            if (!peekBus.TryPeekWord(address, out ushort high) ||
                !peekBus.TryPeekWord(address + 2, out ushort low))
            {
                return false;
            }

            value = (uint)((high << 16) | low);
        }
        else
        {
            return false;
        }

        byte immediate = (byte)compareOpcode;
        bool equal = value == (uint)(sbyte)immediate;
        if (!equal)
        {
            return false;
        }

        cycles = maxCycles;
        Cycles += cycles;
        R[0] = value;
        SetT(true);
        PC = loopPc;
        LastOpcode = braOpcode;
        LastOpcodePc = braPc;
        return true;
    }

    public bool TryFastForwardMovLiteralTstBfPollLoop(int maxCycles, out int cycles)
    {
        cycles = 0;
        if (maxCycles <= 0 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc, out ushort literalOpcode) ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort moveOpcode) ||
            !peekBus.TryPeekWord(loopPc + 4, out ushort tstOpcode) ||
            !peekBus.TryPeekWord(loopPc + 6, out ushort branchOpcode))
        {
            return false;
        }

        if ((literalOpcode & 0xF000) != 0xD000 ||
            (moveOpcode & 0xF00F) != 0x6003 ||
            (tstOpcode & 0xF00F) != 0x2008 ||
            (branchOpcode & 0xFF00) != 0x8B00)
        {
            return false;
        }

        int literalRegister = (literalOpcode >> 8) & 0x0F;
        int moveDestination = (moveOpcode >> 8) & 0x0F;
        int moveSource = (moveOpcode >> 4) & 0x0F;
        int tstDestination = (tstOpcode >> 8) & 0x0F;
        int tstSource = (tstOpcode >> 4) & 0x0F;
        if (moveSource != literalRegister ||
            tstDestination != moveDestination ||
            tstSource != moveDestination)
        {
            return false;
        }

        int displacement = (sbyte)branchOpcode;
        uint target = loopPc + 10 + (uint)(displacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        uint literalAddress = ((loopPc + 4) & 0xFFFF_FFFCu) + (uint)((literalOpcode & 0xFF) * 4);
        if (!peekBus.TryPeekWord(literalAddress, out ushort high) ||
            !peekBus.TryPeekWord(literalAddress + 2, out ushort low))
        {
            return false;
        }

        uint value = (uint)((high << 16) | low);
        if (value == 0)
        {
            return false;
        }

        R[literalRegister] = value;
        R[moveDestination] = value;
        SetT(false);
        PC = loopPc;
        cycles = maxCycles;
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 6;
        return true;
    }

    public bool TryFastForwardMovLiteralWordCmpEqBtPollLoop(int maxCycles, out int cycles)
    {
        cycles = 0;
        if (maxCycles <= 0 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc, out ushort literalOpcode) ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort loadOpcode) ||
            !peekBus.TryPeekWord(loopPc + 4, out ushort compareOpcode) ||
            !peekBus.TryPeekWord(loopPc + 6, out ushort branchOpcode))
        {
            return false;
        }

        if ((literalOpcode & 0xF000) != 0xD000 ||
            (loadOpcode & 0xF00F) != 0x6001 ||
            (compareOpcode & 0xFF00) != 0x8800 ||
            (branchOpcode & 0xFF00) != 0x8900)
        {
            return false;
        }

        int literalRegister = (literalOpcode >> 8) & 0x0F;
        int loadDestination = (loadOpcode >> 8) & 0x0F;
        int loadSource = (loadOpcode >> 4) & 0x0F;
        if (loadSource != literalRegister ||
            loadDestination != 0)
        {
            return false;
        }

        int displacement = (sbyte)branchOpcode;
        uint target = loopPc + 10 + (uint)(displacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        uint literalAddress = ((loopPc + 4) & 0xFFFF_FFFCu) + (uint)((literalOpcode & 0xFF) * 4);
        if (!peekBus.TryPeekWord(literalAddress, out ushort high) ||
            !peekBus.TryPeekWord(literalAddress + 2, out ushort low))
        {
            return false;
        }

        uint address = (uint)((high << 16) | low);
        if (!peekBus.TryPeekWord(address, out ushort wordValue))
        {
            return false;
        }

        byte immediate = (byte)compareOpcode;
        bool equal = (uint)(short)wordValue == (uint)(sbyte)immediate;
        if (!equal)
        {
            return false;
        }

        R[literalRegister] = address;
        R[0] = (uint)(short)wordValue;
        SetT(true);
        PC = loopPc;
        cycles = maxCycles;
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 6;
        return true;
    }

    public bool TryFastForwardSdramFlagTaskletReturn(int maxCycles, out int cycles)
    {
        const int TaskletCycles = 10;
        cycles = 0;
        if (maxCycles < TaskletCycles ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint pc = PC;
        if (!peekBus.TryPeekWord(pc, out ushort firstLiteralOpcode) ||
            !peekBus.TryPeekWord(pc + 2, out ushort firstLoadOpcode) ||
            !peekBus.TryPeekWord(pc + 4, out ushort testOpcode) ||
            !peekBus.TryPeekWord(pc + 6, out ushort firstBranchOpcode) ||
            !peekBus.TryPeekWord(pc + 8, out ushort secondLiteralOpcode) ||
            !peekBus.TryPeekWord(pc + 10, out ushort secondLoadOpcode) ||
            !peekBus.TryPeekWord(pc + 12, out ushort compareLiteralOpcode) ||
            !peekBus.TryPeekWord(pc + 14, out ushort compareOpcode) ||
            !peekBus.TryPeekWord(pc + 16, out ushort secondBranchOpcode))
        {
            return false;
        }

        if ((firstLiteralOpcode & 0xF000) != 0xD000 ||
            firstLoadOpcode != 0x6011 ||
            (testOpcode & 0xFF00) != 0xC800 ||
            (firstBranchOpcode & 0xFF00) != 0x8900 ||
            (secondLiteralOpcode & 0xF000) != 0xD000 ||
            secondLoadOpcode != 0x61E2 ||
            (compareLiteralOpcode & 0xF000) != 0xD000 ||
            compareOpcode != 0x3210 ||
            (secondBranchOpcode & 0xFF00) != 0x8900)
        {
            return false;
        }

        uint firstAddress = ReadPcRelativeLongLiteral(peekBus, pc, firstLiteralOpcode);
        if (!peekBus.TryPeekWord(firstAddress, out ushort flagWord))
        {
            return false;
        }

        byte mask = (byte)testOpcode;
        bool firstBranchTaken = ((byte)flagWord & mask) == 0;
        uint rtsPc = BranchByteTarget(pc + 6, firstBranchOpcode);
        if (!firstBranchTaken)
        {
            uint secondAddress = ReadPcRelativeLongLiteral(peekBus, pc + 8, secondLiteralOpcode);
            uint compareValue = ReadPcRelativeLongLiteral(peekBus, pc + 12, compareLiteralOpcode);
            if (!TryPeekLong(peekBus, secondAddress, out uint currentValue) ||
                currentValue != compareValue)
            {
                return false;
            }

            rtsPc = BranchByteTarget(pc + 16, secondBranchOpcode);
            R[14] = secondAddress;
            R[1] = currentValue;
            R[2] = compareValue;
        }

        if (!MatchesRtsLdsPrReturn(peekBus, rtsPc, out uint returnPc, out uint restoredPr))
        {
            return false;
        }

        R[(firstLiteralOpcode >> 8) & 0x0F] = firstAddress;
        R[0] = SignExtend16(flagWord);
        SetT(true);
        PC = returnPc;
        PR = restoredPr;
        cycles = TaskletCycles;
        Cycles += cycles;
        LastOpcode = 0x000B;
        LastOpcodePc = rtsPc;
        return true;
    }

    public bool TryFastForwardGbrBytePairEqualTaskletReturn(int maxCycles, out int cycles)
    {
        const int TaskletCycles = 7;
        cycles = 0;
        if (maxCycles < TaskletCycles ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint pc = PC;
        if (!peekBus.TryPeekWord(pc, out ushort firstLoadOpcode) ||
            !peekBus.TryPeekWord(pc + 2, out ushort moveOpcode) ||
            !peekBus.TryPeekWord(pc + 4, out ushort secondLoadOpcode) ||
            !peekBus.TryPeekWord(pc + 6, out ushort compareOpcode) ||
            !peekBus.TryPeekWord(pc + 8, out ushort branchOpcode))
        {
            return false;
        }

        if ((firstLoadOpcode & 0xFF00) != 0xC400 ||
            moveOpcode != 0x6103 ||
            (secondLoadOpcode & 0xFF00) != 0xC400 ||
            compareOpcode != 0x3100 ||
            (branchOpcode & 0xFF00) != 0x8900)
        {
            return false;
        }

        uint firstAddress = GBR + (uint)(firstLoadOpcode & 0x00FF);
        uint secondAddress = GBR + (uint)(secondLoadOpcode & 0x00FF);
        if (!peekBus.TryPeekByte(firstAddress, out byte firstValue) ||
            !peekBus.TryPeekByte(secondAddress, out byte secondValue) ||
            firstValue != secondValue)
        {
            return false;
        }

        uint rtsPc = BranchByteTarget(pc + 8, branchOpcode);
        if (!MatchesRtsLdsPrReturn(peekBus, rtsPc, out uint returnPc, out uint restoredPr))
        {
            return false;
        }

        uint signedFirst = SignExtend8(firstValue);
        uint signedSecond = SignExtend8(secondValue);
        R[0] = signedSecond;
        R[1] = signedFirst;
        SetT(true);
        PC = returnPc;
        PR = restoredPr;
        cycles = TaskletCycles;
        Cycles += cycles;
        LastOpcode = 0x000B;
        LastOpcodePc = rtsPc;
        return true;
    }

    public bool TryFastForwardWordCmpEqBtPollLoop(int maxCycles, out int cycles)
    {
        cycles = 0;
        if (maxCycles <= 0 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc, out ushort loadOpcode) ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort compareOpcode) ||
            !peekBus.TryPeekWord(loopPc + 4, out ushort branchOpcode))
        {
            return false;
        }

        if ((loadOpcode & 0xF00F) != 0x6001 ||
            (compareOpcode & 0xFF00) != 0x8800 ||
            (branchOpcode & 0xFF00) != 0x8900)
        {
            return false;
        }

        int loadDestination = (loadOpcode >> 8) & 0x0F;
        int loadSource = (loadOpcode >> 4) & 0x0F;
        if (loadDestination != 0)
        {
            return false;
        }

        int displacement = (sbyte)branchOpcode;
        uint target = loopPc + 8 + (uint)(displacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        if (!peekBus.TryPeekWord(R[loadSource], out ushort wordValue))
        {
            return false;
        }

        byte immediate = (byte)compareOpcode;
        bool equal = (uint)(short)wordValue == (uint)(sbyte)immediate;
        if (!equal)
        {
            return false;
        }

        R[0] = (uint)(short)wordValue;
        SetT(true);
        PC = loopPc;
        cycles = maxCycles;
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 4;
        return true;
    }

    public bool TryFastForwardWordTstBfPollLoop(int maxCycles, out int cycles)
    {
        cycles = 0;
        if (maxCycles <= 0 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc, out ushort loadOpcode) ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort testOpcode) ||
            !peekBus.TryPeekWord(loopPc + 4, out ushort branchOpcode))
        {
            return false;
        }

        if ((loadOpcode & 0xF00F) != 0x6001 ||
            (testOpcode & 0xF00F) != 0x2008 ||
            (branchOpcode & 0xFF00) != 0x8B00)
        {
            return false;
        }

        int loadDestination = (loadOpcode >> 8) & 0x0F;
        int loadSource = (loadOpcode >> 4) & 0x0F;
        int testLeft = (testOpcode >> 8) & 0x0F;
        int testRight = (testOpcode >> 4) & 0x0F;
        if (loadDestination != 0 ||
            testLeft != 0 ||
            testRight != 0)
        {
            return false;
        }

        int displacement = (sbyte)branchOpcode;
        uint target = loopPc + 8 + (uint)(displacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        if (!peekBus.TryPeekWord(R[loadSource], out ushort wordValue) ||
            wordValue == 0)
        {
            return false;
        }

        R[0] = (uint)(short)wordValue;
        SetT(false);
        PC = loopPc;
        cycles = maxCycles;
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 4;
        return true;
    }

    public bool TryFastForwardByteTstBfPollLoop(int maxCycles, out int cycles)
    {
        const int MaxBurstCycles = 4096;
        cycles = 0;
        if (maxCycles <= 0 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc, out ushort loadOpcode) ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort testOpcode) ||
            !peekBus.TryPeekWord(loopPc + 4, out ushort branchOpcode))
        {
            if (PC < 2 ||
                !peekBus.TryPeekWord(PC - 2, out loadOpcode) ||
                !peekBus.TryPeekWord(PC, out testOpcode) ||
                !peekBus.TryPeekWord(PC + 2, out branchOpcode))
            {
                if (PC < 4 ||
                    !peekBus.TryPeekWord(PC - 4, out loadOpcode) ||
                    !peekBus.TryPeekWord(PC - 2, out testOpcode) ||
                    !peekBus.TryPeekWord(PC, out branchOpcode))
                {
                    return false;
                }

                loopPc = PC - 4;
            }
            else
            {
                loopPc = PC - 2;
            }
        }

        if ((loadOpcode & 0xF00F) != 0x6000 ||
            (testOpcode & 0xF00F) != 0x2008 ||
            (branchOpcode & 0xFF00) != 0x8B00)
        {
            return false;
        }

        int loadDestination = (loadOpcode >> 8) & 0x0F;
        int loadSource = (loadOpcode >> 4) & 0x0F;
        int testLeft = (testOpcode >> 8) & 0x0F;
        int testRight = (testOpcode >> 4) & 0x0F;
        if (loadDestination != 0 ||
            testLeft != 0 ||
            testRight != 0)
        {
            return false;
        }

        int displacement = (sbyte)branchOpcode;
        uint target = loopPc + 8 + (uint)(displacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        if (!peekBus.TryPeekByte(R[loadSource], out byte byteValue) ||
            byteValue == 0)
        {
            return false;
        }

        R[0] = (uint)(sbyte)byteValue;
        SetT(false);
        PC = loopPc;
        cycles = Math.Min(maxCycles, MaxBurstCycles);
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 4;
        return true;
    }

    public bool TryFastForwardTstBfsDelayAddLoop(int maxCycles, out int cycles)
    {
        cycles = 0;
        if (maxCycles < 3 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc, out ushort testOpcode) ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort branchOpcode) ||
            !peekBus.TryPeekWord(loopPc + 4, out ushort addOpcode))
        {
            return false;
        }

        if ((testOpcode & 0xF00F) != 0x2008 ||
            (branchOpcode & 0xFF00) != 0x8F00 ||
            (addOpcode & 0xF0FF) != 0x70FF)
        {
            return false;
        }

        int testLeft = (testOpcode >> 8) & 0x0F;
        int testRight = (testOpcode >> 4) & 0x0F;
        int addRegister = (addOpcode >> 8) & 0x0F;
        if (testLeft != testRight ||
            addRegister != testLeft)
        {
            return false;
        }

        int displacement = (sbyte)branchOpcode;
        uint target = loopPc + 6 + (uint)(displacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        uint count = R[addRegister];
        if (count == 0)
        {
            return false;
        }

        uint maxIterations = (uint)(maxCycles / 3);
        uint iterations = Math.Min(count, maxIterations);
        if (iterations == 0)
        {
            return false;
        }

        R[addRegister] = count - iterations;
        SetT(false);
        PC = loopPc;
        cycles = checked((int)(iterations * 3));
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 2;
        return true;
    }

    public bool TryFastForwardTwoStageWordZeroPollRing(int maxCycles, out int cycles)
    {
        cycles = 0;
        if (maxCycles <= 0 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            (SR & TBit) == 0 ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint branchPc = PC;
        if (!peekBus.TryPeekWord(branchPc, out ushort btOpcode) ||
            (btOpcode & 0xFF00) != 0x8900)
        {
            return false;
        }

        int btDisplacement = (sbyte)btOpcode;
        uint braToSetupPc = branchPc + 4 + (uint)(btDisplacement * 2);
        if (!peekBus.TryPeekWord(braToSetupPc, out ushort braToSetupOpcode) ||
            !peekBus.TryPeekWord(braToSetupPc + 2, out ushort firstDelaySlot) ||
            (braToSetupOpcode & 0xF000) != 0xA000 ||
            firstDelaySlot != 0x0009)
        {
            return false;
        }

        int setupDisplacement = SignExtend12(braToSetupOpcode & 0x0FFF);
        uint setupPc = braToSetupPc + 4 + (uint)(setupDisplacement * 2);
        if (!peekBus.TryPeekWord(setupPc + 0, out ushort firstLiteralOpcode) ||
            !peekBus.TryPeekWord(setupPc + 2, out ushort firstLoadOpcode) ||
            !peekBus.TryPeekWord(setupPc + 4, out ushort secondLoadOpcode) ||
            !peekBus.TryPeekWord(setupPc + 6, out ushort compareOpcode) ||
            !peekBus.TryPeekWord(setupPc + 8, out ushort bfOpcode) ||
            !peekBus.TryPeekWord(setupPc + 10, out ushort braToPollOpcode) ||
            !peekBus.TryPeekWord(setupPc + 12, out ushort secondDelaySlot))
        {
            return false;
        }

        if ((firstLiteralOpcode & 0xF000) != 0xD000 ||
            (firstLoadOpcode & 0xF00F) != 0x6001 ||
            (secondLoadOpcode & 0xF00F) != 0x8001 ||
            (compareOpcode & 0xF00F) != 0x3000 ||
            (bfOpcode & 0xFF00) != 0x8B00 ||
            (braToPollOpcode & 0xF000) != 0xA000 ||
            secondDelaySlot != 0x0009)
        {
            return false;
        }

        int addressRegister = (firstLiteralOpcode >> 8) & 0x0F;
        int firstDestination = (firstLoadOpcode >> 8) & 0x0F;
        int firstAddressRegister = (firstLoadOpcode >> 4) & 0x0F;
        int secondDestination = 0;
        int secondAddressRegister = (secondLoadOpcode >> 4) & 0x0F;
        int compareLeft = (compareOpcode >> 8) & 0x0F;
        int compareRight = (compareOpcode >> 4) & 0x0F;
        if (firstAddressRegister != addressRegister ||
            secondAddressRegister != addressRegister ||
            compareLeft != firstDestination ||
            compareRight != secondDestination)
        {
            return false;
        }

        int bfDisplacement = (sbyte)bfOpcode;
        uint bfTarget = setupPc + 12 + (uint)(bfDisplacement * 2);
        if (bfTarget == setupPc + 10)
        {
            return false;
        }

        int pollDisplacement = SignExtend12(braToPollOpcode & 0x0FFF);
        uint pollPc = setupPc + 14 + (uint)(pollDisplacement * 2);
        if (!peekBus.TryPeekWord(pollPc + 0, out ushort pollLiteralOpcode) ||
            !peekBus.TryPeekWord(pollPc + 2, out ushort pollLoadOpcode) ||
            !peekBus.TryPeekWord(pollPc + 4, out ushort testOpcode))
        {
            return false;
        }

        if ((pollLiteralOpcode & 0xF000) != 0xD000 ||
            (pollLoadOpcode & 0xF00F) != 0x6001 ||
            (testOpcode & 0xF00F) != 0x2008)
        {
            return false;
        }

        int pollAddressRegister = (pollLiteralOpcode >> 8) & 0x0F;
        int pollDestination = (pollLoadOpcode >> 8) & 0x0F;
        int pollLoadAddressRegister = (pollLoadOpcode >> 4) & 0x0F;
        int testLeft = (testOpcode >> 8) & 0x0F;
        int testRight = (testOpcode >> 4) & 0x0F;
        if (pollLoadAddressRegister != pollAddressRegister ||
            testLeft != pollDestination ||
            testRight != pollDestination ||
            pollAddressRegister != addressRegister)
        {
            return false;
        }

        uint firstLiteralAddress = ((setupPc + 4) & 0xFFFF_FFFCu) + (uint)((firstLiteralOpcode & 0xFF) * 4);
        uint pollLiteralAddress = ((pollPc + 4) & 0xFFFF_FFFCu) + (uint)((pollLiteralOpcode & 0xFF) * 4);
        if (!peekBus.TryPeekWord(firstLiteralAddress, out ushort firstHigh) ||
            !peekBus.TryPeekWord(firstLiteralAddress + 2, out ushort firstLow) ||
            !peekBus.TryPeekWord(pollLiteralAddress, out ushort pollHigh) ||
            !peekBus.TryPeekWord(pollLiteralAddress + 2, out ushort pollLow))
        {
            return false;
        }

        uint firstAddress = (uint)((firstHigh << 16) | firstLow);
        uint pollAddress = (uint)((pollHigh << 16) | pollLow);
        if (!peekBus.TryPeekWord(firstAddress, out ushort firstValue) ||
            !peekBus.TryPeekWord(firstAddress + 2, out ushort secondValue) ||
            !peekBus.TryPeekWord(pollAddress, out ushort pollValue) ||
            firstValue != secondValue ||
            pollValue != 0)
        {
            return false;
        }

        R[addressRegister] = pollAddress;
        R[firstDestination] = firstValue;
        R[secondDestination] = 0;
        SetT(true);
        PC = branchPc;
        cycles = Math.Min(maxCycles, 512);
        Cycles += cycles;
        LastOpcode = testOpcode;
        LastOpcodePc = pollPc + 4;
        return true;
    }

    public int Step()
    {
        uint pc = PC;
        int interruptCycles = AcceptPendingInterrupt();
        if (interruptCycles > 0)
        {
            Cycles += interruptCycles;
            return interruptCycles;
        }

        if (Halted)
        {
            return 1;
        }

        pc = PC;
        ushort opcode = _bus.ReadWord(pc);
        PC += 2;
        LastOpcode = opcode;
        LastOpcodePc = pc;
        Action<Sh2InstructionTrace>? observer = InstructionObserver;
        uint beforeSr = 0;
        uint beforeR0 = 0;
        uint beforeR1 = 0;
        uint beforeR2 = 0;
        uint beforeR3 = 0;
        uint beforeR4 = 0;
        uint beforeR5 = 0;
        uint beforeR6 = 0;
        uint beforeR7 = 0;
        uint beforeR8 = 0;
        uint beforeR9 = 0;
        uint beforeR10 = 0;
        uint beforeR11 = 0;
        uint beforeR12 = 0;
        uint beforeR13 = 0;
        uint beforeR14 = 0;
        uint beforeR15 = 0;
        uint beforePr = 0;
        uint beforeGbr = 0;
        uint beforeVbr = 0;
        long beforeCycles = 0;
        if (observer is not null)
        {
            beforeSr = SR;
            beforeR0 = R[0];
            beforeR1 = R[1];
            beforeR2 = R[2];
            beforeR3 = R[3];
            beforeR4 = R[4];
            beforeR5 = R[5];
            beforeR6 = R[6];
            beforeR7 = R[7];
            beforeR8 = R[8];
            beforeR9 = R[9];
            beforeR10 = R[10];
            beforeR11 = R[11];
            beforeR12 = R[12];
            beforeR13 = R[13];
            beforeR14 = R[14];
            beforeR15 = R[15];
            beforePr = PR;
            beforeGbr = GBR;
            beforeVbr = VBR;
            beforeCycles = Cycles;
        }

        _delaySlotWaitCycles = 0;
        int cycles = Execute(opcode, pc);
        cycles += _delaySlotWaitCycles;
        if (_bus is ISh2WaitStateBus waitStateBus)
        {
            cycles += waitStateBus.ConsumeWaitCycles();
        }

        Cycles += cycles;
        if (observer is not null)
        {
            observer(new Sh2InstructionTrace(
                _name,
                pc,
                opcode,
                PC,
                beforeSr,
                beforeR0,
                beforeR1,
                beforeR2,
                beforeR3,
                beforeR4,
                beforeR5,
                beforeR6,
                beforeR7,
                beforeR8,
                beforeR9,
                beforeR10,
                beforeR11,
                beforeR12,
                beforeR13,
                beforeR14,
                beforeR15,
                SR,
                R[0],
                R[1],
                R[2],
                R[3],
                R[4],
                R[5],
                R[6],
                R[7],
                R[8],
                R[9],
                R[10],
                R[11],
                R[12],
                R[13],
                R[14],
                R[15],
                beforePr,
                beforeGbr,
                beforeVbr,
                PR,
                GBR,
                VBR,
                beforeCycles,
                Cycles,
                cycles,
                DelaySlot: false));
        }

        return cycles;
    }

    public Sh2State CaptureState()
    {
        return new Sh2State((uint[])R.Clone(), (uint[])BankedR.Clone(), PC, PR, GBR, VBR, MACH, MACL, SR, Cycles, Halted, LastOpcode, LastOpcodePc, UnhandledOpcodeCount, DelaySlotActive, PendingInterruptLevel, PendingInterruptVectorNumber);
    }

    public void RestoreState(Sh2State state)
    {
        Array.Clear(R);
        Array.Copy(state.R, R, Math.Min(R.Length, state.R.Length));
        Array.Clear(BankedR);
        Array.Copy(state.BankedR, BankedR, Math.Min(BankedR.Length, state.BankedR.Length));
        PC = state.PC;
        PR = state.PR;
        GBR = state.GBR;
        VBR = state.VBR;
        MACH = state.MACH;
        MACL = state.MACL;
        SR = state.SR;
        Cycles = state.Cycles;
        Halted = state.Halted;
        LastOpcode = state.LastOpcode;
        LastOpcodePc = state.LastOpcodePc;
        UnhandledOpcodeCount = state.UnhandledOpcodeCount;
        DelaySlotActive = state.DelaySlotActive;
        PendingInterruptLevel = state.PendingInterruptLevel;
        PendingInterruptVectorNumber = state.PendingInterruptVectorNumber;
    }

    private int AcceptPendingInterrupt()
    {
        if (PendingInterruptLevel == 0 || PendingInterruptLevel <= ((SR >> 4) & 0x0F))
        {
            return 0;
        }

        int level = PendingInterruptLevel;
        int vectorNumber = PendingInterruptVectorNumber != 0 ? PendingInterruptVectorNumber : 64 + level;
        PendingInterruptLevel = 0;
        PendingInterruptVectorNumber = 0;
        uint sp = R[15];
        sp -= 4;
        _bus.WriteLong(sp, SR);
        sp -= 4;
        _bus.WriteLong(sp, PC);
        R[15] = sp;
        SetSr((SR & 0xFFFF_FF0Fu) | (uint)(level << 4));
        PC = _bus.ReadLong(VBR + (uint)(vectorNumber * 4));
        Halted = false;
        InterruptObserver?.Invoke(new Sh2InterruptTrace(_name, level, vectorNumber, PC, SR, R[15]));
        InterruptAccepted?.Invoke(level, vectorNumber);
        return 5;
    }

    private int Execute(ushort opcode, uint opcodePc)
    {
        int high = opcode >> 12;
        int n = (opcode >> 8) & 0x0F;
        int m = (opcode >> 4) & 0x0F;
        int low = opcode & 0x0F;

        switch (opcode)
        {
            case 0x0008:
                SR &= ~TBit;
                return 1;
            case 0x0009:
                return 1;
            case 0x000B:
                BranchWithDelaySlot(PR);
                return 2;
            case 0x002B:
                ExecuteRte();
                return 4;
            case 0x0018:
                SR |= TBit;
                return 1;
            case 0x0019:
                SR &= ~(MBit | QBit | TBit);
                return 1;
            case 0x0028:
                MACH = 0;
                MACL = 0;
                return 1;
            case 0x001B:
                Halted = true;
                return 1;
        }

        if ((opcode & 0xF0FF) == 0x400B)
        {
            PR = opcodePc + 4;
            BranchWithDelaySlot(R[n]);
            return 2;
        }

        if ((opcode & 0xF0FF) == 0x402B)
        {
            BranchWithDelaySlot(R[n]);
            return 2;
        }

        if ((opcode & 0xF0FF) == 0x0003)
        {
            PR = opcodePc + 4;
            BranchWithDelaySlot(opcodePc + 4 + R[n]);
            return 2;
        }

        if ((opcode & 0xF0FF) == 0x0023)
        {
            BranchWithDelaySlot(opcodePc + 4 + R[n]);
            return 2;
        }

        if ((opcode & 0xF0FF) == 0x0002)
        {
            R[n] = SR;
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x0012)
        {
            R[n] = GBR;
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x0022)
        {
            R[n] = VBR;
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x000A)
        {
            R[n] = MACH;
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x001A)
        {
            R[n] = MACL;
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x002A)
        {
            R[n] = PR;
            return 1;
        }

        if ((opcode & 0xF08F) == 0x0082)
        {
            R[n] = BankedR[m & 0x07];
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x0029)
        {
            R[n] = (SR & TBit) != 0 ? 1u : 0u;
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x400E)
        {
            SetSr(R[n]);
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x401E)
        {
            GBR = R[n];
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x402E)
        {
            VBR = R[n];
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x400A)
        {
            MACH = R[n];
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x401A)
        {
            MACL = R[n];
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x402A)
        {
            PR = R[n];
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x4002)
        {
            R[n] -= 4;
            _bus.WriteLong(R[n], MACH);
            return 2;
        }

        if ((opcode & 0xF0FF) == 0x4012)
        {
            R[n] -= 4;
            _bus.WriteLong(R[n], MACL);
            return 2;
        }

        if ((opcode & 0xF0FF) == 0x4022)
        {
            R[n] -= 4;
            _bus.WriteLong(R[n], PR);
            return 2;
        }

        if ((opcode & 0xF0FF) == 0x4003)
        {
            R[n] -= 4;
            _bus.WriteLong(R[n], SR);
            return 2;
        }

        if ((opcode & 0xF0FF) == 0x4013)
        {
            R[n] -= 4;
            _bus.WriteLong(R[n], GBR);
            return 2;
        }

        if ((opcode & 0xF0FF) == 0x4023)
        {
            R[n] -= 4;
            _bus.WriteLong(R[n], VBR);
            return 2;
        }

        if ((opcode & 0xF08F) == 0x4083)
        {
            R[n] -= 4;
            _bus.WriteLong(R[n], BankedR[m & 0x07]);
            return 2;
        }

        if ((opcode & 0xF0FF) == 0x4006)
        {
            MACH = _bus.ReadLong(R[n]);
            R[n] += 4;
            return 2;
        }

        if ((opcode & 0xF0FF) == 0x4016)
        {
            MACL = _bus.ReadLong(R[n]);
            R[n] += 4;
            return 2;
        }

        if ((opcode & 0xF0FF) == 0x4026)
        {
            PR = _bus.ReadLong(R[n]);
            R[n] += 4;
            return 2;
        }

        if ((opcode & 0xF0FF) == 0x4007)
        {
            SetSr(_bus.ReadLong(R[n]));
            R[n] += 4;
            return 2;
        }

        if ((opcode & 0xF0FF) == 0x4017)
        {
            GBR = _bus.ReadLong(R[n]);
            R[n] += 4;
            return 2;
        }

        if ((opcode & 0xF0FF) == 0x4027)
        {
            VBR = _bus.ReadLong(R[n]);
            R[n] += 4;
            return 2;
        }

        if ((opcode & 0xF08F) == 0x4087)
        {
            BankedR[m & 0x07] = _bus.ReadLong(R[n]);
            R[n] += 4;
            return 2;
        }

        if ((opcode & 0xF08F) == 0x408E)
        {
            BankedR[m & 0x07] = R[n];
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x4015)
        {
            ComparePl(R[n]);
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x4011)
        {
            ComparePz(R[n]);
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x4010)
        {
            uint value = R[n] - 1;
            R[n] = value;
            SetT(value == 0);
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x4000)
        {
            SetT((R[n] & 0x8000_0000u) != 0);
            R[n] <<= 1;
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x4001)
        {
            SetT((R[n] & 0x0000_0001u) != 0);
            R[n] >>= 1;
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x4020)
        {
            SetT((R[n] & 0x8000_0000u) != 0);
            R[n] <<= 1;
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x4021)
        {
            SetT((R[n] & 0x8000_0000u) != 0);
            R[n] = (uint)((int)R[n] >> 1);
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x4004)
        {
            SetT((R[n] & 0x8000_0000u) != 0);
            R[n] = (R[n] << 1) | (R[n] >> 31);
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x4024)
        {
            uint oldT = SR & TBit;
            SetT((R[n] & 0x8000_0000u) != 0);
            R[n] = (R[n] << 1) | oldT;
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x4005)
        {
            SetT((R[n] & 0x0000_0001u) != 0);
            R[n] = (R[n] >> 1) | (R[n] << 31);
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x4025)
        {
            uint oldT = SR & TBit;
            SetT((R[n] & 0x0000_0001u) != 0);
            R[n] = (R[n] >> 1) | (oldT << 31);
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x4008)
        {
            R[n] <<= 2;
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x4009)
        {
            R[n] >>= 2;
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x4018)
        {
            R[n] <<= 8;
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x4019)
        {
            R[n] >>= 8;
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x4028)
        {
            R[n] <<= 16;
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x4029)
        {
            R[n] >>= 16;
            return 1;
        }

        if ((opcode & 0xF0FF) == 0x401B)
        {
            byte value = _bus.ReadByte(R[n]);
            SetT(value == 0);
            _bus.WriteByte(R[n], (byte)(value | 0x80));
            return 4;
        }

        if ((opcode & 0xF00F) == 0x400C)
        {
            ExecuteShad(n, m);
            return 1;
        }

        if ((opcode & 0xF00F) == 0x400D)
        {
            ExecuteShld(n, m);
            return 1;
        }

        if ((opcode & 0xF00F) == 0x400F)
        {
            ExecuteMacWord(n, m);
            return 3;
        }

        if ((opcode & 0xF00F) == 0x6008)
        {
            R[n] = SwapByteWord(R[m]);
            return 1;
        }

        if ((opcode & 0xF00F) == 0x6009)
        {
            R[n] = (R[m] << 16) | (R[m] >> 16);
            return 1;
        }

        if ((opcode & 0xF00F) == 0x600A)
        {
            uint result = unchecked(0u - R[m] - (SR & TBit));
            SetT(result > R[n]);
            R[n] = result;
            return 1;
        }

        if ((opcode & 0xF00F) == 0x600B)
        {
            R[n] = unchecked(0u - R[m]);
            return 1;
        }

        if ((opcode & 0xF00F) == 0x600C)
        {
            R[n] = (byte)R[m];
            return 1;
        }

        if ((opcode & 0xF00F) == 0x600D)
        {
            R[n] = (ushort)R[m];
            return 1;
        }

        if ((opcode & 0xF00F) == 0x600E)
        {
            R[n] = (uint)(sbyte)(byte)R[m];
            return 1;
        }

        if ((opcode & 0xF00F) == 0x600F)
        {
            R[n] = (uint)(short)(ushort)R[m];
            return 1;
        }

        if ((opcode & 0xF00F) == 0x0004)
        {
            _bus.WriteByte(R[0] + R[n], (byte)R[m]);
            return 1;
        }

        if ((opcode & 0xF00F) == 0x0005)
        {
            _bus.WriteWord(R[0] + R[n], (ushort)R[m]);
            return 1;
        }

        if ((opcode & 0xF00F) == 0x0006)
        {
            _bus.WriteLong(R[0] + R[n], R[m]);
            return 1;
        }

        if ((opcode & 0xF00F) == 0x0007)
        {
            MACL = unchecked(R[n] * R[m]);
            return 2;
        }

        if ((opcode & 0xF00F) == 0x000F)
        {
            ExecuteMacLong(n, m);
            return 3;
        }

        if ((opcode & 0xF00F) == 0x000C)
        {
            R[n] = SignExtend8(_bus.ReadByte(R[0] + R[m]));
            return 1;
        }

        if ((opcode & 0xF00F) == 0x000D)
        {
            R[n] = SignExtend16(_bus.ReadWord(R[0] + R[m]));
            return 1;
        }

        if ((opcode & 0xF00F) == 0x000E)
        {
            R[n] = _bus.ReadLong(R[0] + R[m]);
            return 1;
        }

        return high switch
        {
            0x1 => ExecuteMovLongRegisterToDisplacement(opcode, n, m),
            0x2 => ExecuteStoreRegister(opcode, n, m, low),
            0x3 => ExecuteArithmetic(opcode, n, m, low),
            0x5 => ExecuteMovLongDisplacementToRegister(opcode, n, m),
            0x6 => ExecuteLoadOrMoveRegister(opcode, n, m, low),
            0x7 => ExecuteAddImmediate(opcode, n),
            0x8 => ExecuteBranchByte(opcode, opcodePc),
            0x9 => ExecuteMovWordPcRelative(opcode, n, opcodePc),
            0xA => ExecuteBra(opcode, opcodePc),
            0xB => ExecuteBsr(opcode, opcodePc),
            0xC => ExecuteSystemImmediate(opcode, opcodePc),
            0xD => ExecuteMovLongPcRelative(opcode, n, opcodePc),
            0xE => ExecuteMovImmediate(opcode, n),
            _ => Unhandled(opcode, opcodePc),
        };
    }

    private int ExecuteMovLongRegisterToDisplacement(ushort opcode, int n, int m)
    {
        int displacement = opcode & 0x0F;
        _bus.WriteLong(R[n] + (uint)(displacement * 4), R[m]);
        return 1;
    }

    private int ExecuteStoreRegister(ushort opcode, int n, int m, int low)
    {
        switch (low)
        {
            case 0x0:
                _bus.WriteByte(R[n], (byte)R[m]);
                return 1;
            case 0x1:
                _bus.WriteWord(R[n], (ushort)R[m]);
                return 1;
            case 0x2:
                _bus.WriteLong(R[n], R[m]);
                return 1;
            case 0x4:
                _bus.WriteByte(R[n] - 1, (byte)R[m]);
                R[n] -= 1;
                return 1;
            case 0x5:
                _bus.WriteWord(R[n] - 2, (ushort)R[m]);
                R[n] -= 2;
                return 1;
            case 0x6:
                _bus.WriteLong(R[n] - 4, R[m]);
                R[n] -= 4;
                return 1;
            case 0x7:
                ExecuteDiv0S(n, m);
                return 1;
            case 0x8:
                SetT((R[n] & R[m]) == 0);
                return 1;
            case 0x9:
                R[n] &= R[m];
                return 1;
            case 0xA:
                R[n] ^= R[m];
                return 1;
            case 0xB:
                R[n] |= R[m];
                return 1;
            case 0xC:
                SetT(((R[n] ^ R[m]) & 0xFF000000u) == 0 ||
                    ((R[n] ^ R[m]) & 0x00FF0000u) == 0 ||
                    ((R[n] ^ R[m]) & 0x0000FF00u) == 0 ||
                    ((R[n] ^ R[m]) & 0x000000FFu) == 0);
                return 1;
            case 0xD:
                R[n] = (R[n] >> 16) | (R[m] << 16);
                return 1;
            case 0xE:
                MACL = (uint)((ushort)R[n] * (ushort)R[m]);
                return 2;
            case 0xF:
                MACL = (uint)((short)(ushort)R[n] * (short)(ushort)R[m]);
                return 2;
            default:
                return Unhandled(opcode, LastOpcodePc);
        }
    }

    private int ExecuteArithmetic(ushort opcode, int n, int m, int low)
    {
        switch (low)
        {
            case 0x0:
                CompareEq(R[m], R[n]);
                return 1;
            case 0x2:
                CompareHs(R[m], R[n]);
                return 1;
            case 0x3:
                CompareGe(R[m], R[n]);
                return 1;
            case 0x4:
                ExecuteDiv1(n, m);
                return 1;
            case 0x5:
            {
                ulong result = (ulong)R[n] * R[m];
                MACH = (uint)(result >> 32);
                MACL = (uint)result;
                return 2;
            }
            case 0x6:
                CompareHi(R[m], R[n]);
                return 1;
            case 0x7:
                CompareGt(R[m], R[n]);
                return 1;
            case 0x8:
                R[n] -= R[m];
                return 1;
            case 0xA:
                ExecuteSubc(n, m);
                return 1;
            case 0xB:
                ExecuteSubv(n, m);
                return 1;
            case 0xC:
                R[n] += R[m];
                return 1;
            case 0xD:
            {
                long result = (long)(int)R[n] * (long)(int)R[m];
                MACH = (uint)(result >> 32);
                MACL = (uint)result;
                return 2;
            }
            case 0xE:
                ExecuteAddc(n, m);
                return 1;
            case 0xF:
                ExecuteAddv(n, m);
                return 1;
            default:
                return Unhandled(opcode, LastOpcodePc);
        }
    }

    private int ExecuteMovLongDisplacementToRegister(ushort opcode, int n, int m)
    {
        int displacement = opcode & 0x0F;
        R[n] = _bus.ReadLong(R[m] + (uint)(displacement * 4));
        return 1;
    }

    private int ExecuteLoadOrMoveRegister(ushort opcode, int n, int m, int low)
    {
        switch (low)
        {
            case 0x0:
                R[n] = SignExtend8(_bus.ReadByte(R[m]));
                return 1;
            case 0x1:
                R[n] = SignExtend16(_bus.ReadWord(R[m]));
                return 1;
            case 0x2:
                R[n] = _bus.ReadLong(R[m]);
                return 1;
            case 0x3:
                R[n] = R[m];
                return 1;
            case 0x4:
                R[n] = SignExtend8(_bus.ReadByte(R[m]));
                if (n != m)
                {
                    R[m] += 1;
                }

                return 1;
            case 0x5:
                R[n] = SignExtend16(_bus.ReadWord(R[m]));
                if (n != m)
                {
                    R[m] += 2;
                }

                return 1;
            case 0x6:
                R[n] = _bus.ReadLong(R[m]);
                if (n != m)
                {
                    R[m] += 4;
                }

                return 1;
            case 0x7:
                R[n] = ~R[m];
                return 1;
            case 0xA:
                ExecuteNegc(n, m);
                return 1;
            case 0xC:
                R[n] = SignExtend8(_bus.ReadByte(R[0] + R[m]));
                return 1;
            case 0xD:
                R[n] = SignExtend16(_bus.ReadWord(R[0] + R[m]));
                return 1;
            case 0xE:
                R[n] = _bus.ReadLong(R[0] + R[m]);
                return 1;
            default:
                return Unhandled(opcode, LastOpcodePc);
        }
    }

    private int ExecuteAddImmediate(ushort opcode, int n)
    {
        R[n] += SignExtend8((byte)opcode);
        return 1;
    }

    private int ExecuteBranchByte(ushort opcode, uint opcodePc)
    {
        byte displacement = (byte)opcode;
        int m = (opcode >> 4) & 0x0F;
        switch ((opcode >> 8) & 0x0F)
        {
            case 0x0:
                _bus.WriteByte(R[m] + (uint)(displacement & 0x0F), (byte)R[0]);
                return 1;
            case 0x1:
                _bus.WriteWord(R[m] + (uint)((displacement & 0x0F) * 2), (ushort)R[0]);
                return 1;
            case 0x4:
                R[0] = SignExtend8(_bus.ReadByte(R[m] + (uint)(displacement & 0x0F)));
                return 1;
            case 0x5:
                R[0] = SignExtend16(_bus.ReadWord(R[m] + (uint)((displacement & 0x0F) * 2)));
                return 1;
            case 0x8:
                CompareEq(SignExtend8((byte)opcode), R[0]);
                return 1;
            case 0x9:
                if ((SR & TBit) != 0)
                {
                    PC = opcodePc + 4 + (uint)(SignExtend8(displacement) * 2);
                }

                return 1;
            case 0xB:
                if ((SR & TBit) == 0)
                {
                    PC = opcodePc + 4 + (uint)(SignExtend8(displacement) * 2);
                }

                return 1;
            case 0xD:
                if ((SR & TBit) != 0)
                {
                    BranchWithDelaySlot(opcodePc + 4 + (uint)(SignExtend8(displacement) * 2));
                    return 2;
                }

                return 1;
            case 0xF:
                if ((SR & TBit) == 0)
                {
                    BranchWithDelaySlot(opcodePc + 4 + (uint)(SignExtend8(displacement) * 2));
                    return 2;
                }

                return 1;
            default:
                return Unhandled(opcode, opcodePc);
        }
    }

    private int ExecuteMovWordPcRelative(ushort opcode, int n, uint opcodePc)
    {
        uint address = PcRelativeBase(opcodePc) + (uint)((byte)opcode * 2);
        R[n] = SignExtend16(_bus.ReadWord(address));
        return 1;
    }

    private int ExecuteBra(ushort opcode, uint opcodePc)
    {
        BranchWithDelaySlot(opcodePc + 4 + (uint)(SignExtend12(opcode & 0x0FFF) * 2));
        return 2;
    }

    private int ExecuteBsr(ushort opcode, uint opcodePc)
    {
        PR = opcodePc + 4;
        BranchWithDelaySlot(opcodePc + 4 + (uint)(SignExtend12(opcode & 0x0FFF) * 2));
        return 2;
    }

    private int ExecuteSystemImmediate(ushort opcode, uint opcodePc)
    {
        switch ((opcode >> 8) & 0x0F)
        {
            case 0x0:
                _bus.WriteByte(GBR + (byte)opcode, (byte)R[0]);
                return 1;
            case 0x1:
                _bus.WriteWord(GBR + (uint)((byte)opcode * 2), (ushort)R[0]);
                return 1;
            case 0x2:
                _bus.WriteLong(GBR + (uint)((byte)opcode * 4), R[0]);
                return 1;
            case 0x3:
                ExecuteTrapa((byte)opcode);
                return 8;
            case 0x4:
                R[0] = SignExtend8(_bus.ReadByte(GBR + (byte)opcode));
                return 1;
            case 0x5:
                R[0] = SignExtend16(_bus.ReadWord(GBR + (uint)((byte)opcode * 2)));
                return 1;
            case 0x6:
                R[0] = _bus.ReadLong(GBR + (uint)((byte)opcode * 4));
                return 1;
            case 0x7:
                R[0] = (PcRelativeLongBase(opcodePc) + ((uint)((byte)opcode) * 4)) & 0xFFFF_FFFC;
                return 1;
            case 0x8:
                SetT((R[0] & (byte)opcode) == 0);
                return 1;
            case 0x9:
                R[0] &= (byte)opcode;
                return 1;
            case 0xA:
                R[0] ^= (byte)opcode;
                return 1;
            case 0xB:
                R[0] |= (byte)opcode;
                return 1;
            case 0xC:
                SetT((_bus.ReadByte(R[0] + GBR) & (byte)opcode) == 0);
                return 1;
            case 0xD:
                _bus.WriteByte(R[0] + GBR, (byte)(_bus.ReadByte(R[0] + GBR) & (byte)opcode));
                return 1;
            case 0xE:
                _bus.WriteByte(R[0] + GBR, (byte)(_bus.ReadByte(R[0] + GBR) ^ (byte)opcode));
                return 1;
            case 0xF:
                _bus.WriteByte(R[0] + GBR, (byte)(_bus.ReadByte(R[0] + GBR) | (byte)opcode));
                return 1;
            default:
                return Unhandled(opcode, opcodePc);
        }
    }

    private int ExecuteMovLongPcRelative(ushort opcode, int n, uint opcodePc)
    {
        uint address = PcRelativeLongBase(opcodePc) + (uint)((byte)opcode * 4);
        R[n] = _bus.ReadLong(address);
        return 1;
    }

    private int ExecuteMovImmediate(ushort opcode, int n)
    {
        R[n] = SignExtend8((byte)opcode);
        return 1;
    }

    private void ExecuteTrapa(byte vector)
    {
        R[15] -= 4;
        _bus.WriteLong(R[15], SR);
        R[15] -= 4;
        _bus.WriteLong(R[15], PC);
        PC = _bus.ReadLong(VBR + (uint)(vector * 4));
    }

    private void ExecuteRte()
    {
        uint target = _bus.ReadLong(R[15]);
        R[15] += 4;
        uint restoredSr = _bus.ReadLong(R[15]);
        R[15] += 4;
        SetSr(restoredSr);
        BranchWithDelaySlot(target);
    }

    private void ExecuteDiv0S(int n, int m)
    {
        bool mSign = (R[m] & 0x8000_0000u) != 0;
        bool qSign = (R[n] & 0x8000_0000u) != 0;
        SetM(mSign);
        SetQ(qSign);
        SetT(mSign != qSign);
    }

    private void ExecuteDiv1(int n, int m)
    {
        bool oldQ = (SR & QBit) != 0;
        bool mBit = (SR & MBit) != 0;
        bool qBit;

        qBit = (R[n] & 0x8000_0000u) != 0;
        R[n] = (R[n] << 1) | (SR & TBit);

        if (!oldQ)
        {
            if (!mBit)
            {
                uint before = R[n];
                R[n] -= R[m];
                bool borrow = R[n] > before;
                qBit = !qBit ? borrow : !borrow;
            }
            else
            {
                uint before = R[n];
                R[n] += R[m];
                bool carry = R[n] < before;
                qBit = !qBit ? !carry : carry;
            }
        }
        else
        {
            if (!mBit)
            {
                uint before = R[n];
                R[n] += R[m];
                bool carry = R[n] < before;
                qBit = !qBit ? carry : !carry;
            }
            else
            {
                uint before = R[n];
                R[n] -= R[m];
                bool borrow = R[n] > before;
                qBit = !qBit ? !borrow : borrow;
            }
        }

        SetQ(qBit);
        SetT(qBit == mBit);
    }

    private void ExecuteAddc(int n, int m)
    {
        uint temp = R[n] + R[m];
        bool carry = temp < R[n];
        uint result = temp + (SR & TBit);
        if (result < temp)
        {
            carry = true;
        }

        R[n] = result;
        SetT(carry);
    }

    private void ExecuteAddv(int n, int m)
    {
        int left = (int)R[n];
        int right = (int)R[m];
        int result = unchecked(left + right);
        R[n] = (uint)result;
        SetT(((left ^ result) & (right ^ result) & unchecked((int)0x8000_0000)) != 0);
    }

    private void ExecuteSubc(int n, int m)
    {
        uint temp = R[n] - R[m];
        bool borrow = R[n] < R[m];
        uint result = temp - (SR & TBit);
        if (temp < result)
        {
            borrow = true;
        }

        R[n] = result;
        SetT(borrow);
    }

    private void ExecuteSubv(int n, int m)
    {
        int left = (int)R[n];
        int right = (int)R[m];
        int result = unchecked(left - right);
        R[n] = (uint)result;
        SetT(((left ^ right) & (left ^ result) & unchecked((int)0x8000_0000)) != 0);
    }

    private void ExecuteNegc(int n, int m)
    {
        uint temp = 0u - R[m];
        bool borrow = R[m] != 0;
        uint result = temp - (SR & TBit);
        if (temp < result)
        {
            borrow = true;
        }

        R[n] = result;
        SetT(borrow);
    }

    private int Unhandled(ushort opcode, uint opcodePc)
    {
        UnhandledOpcodeCount++;
        LastUnhandledOpcode = opcode;
        LastUnhandledOpcodePc = opcodePc;
        if (StartException(GeneralIllegalInstructionVector, PC))
        {
            return 5;
        }

        Halted = true;
        throw new Sh2Exception($"{_name} unsupported opcode ${opcode:X4} at ${opcodePc:X8}");
    }

    private void CompareEq(uint left, uint right)
    {
        SetT(left == right);
    }

    private void CompareHs(uint left, uint right)
    {
        SetT(right >= left);
    }

    private void CompareHi(uint left, uint right)
    {
        SetT(right > left);
    }

    private void CompareGe(uint left, uint right)
    {
        SetT((int)right >= (int)left);
    }

    private void CompareGt(uint left, uint right)
    {
        SetT((int)right > (int)left);
    }

    private void ComparePz(uint value)
    {
        SetT((int)value >= 0);
    }

    private void ComparePl(uint value)
    {
        SetT((int)value > 0);
    }

    private void BranchWithDelaySlot(uint target)
    {
        if (DelaySlotActive)
        {
            StartException(SlotIllegalInstructionVector, PC);
            return;
        }

        uint delaySlotPc = PC;
        ushort delayOpcode = _bus.ReadWord(delaySlotPc);
        PC += 2;
        _delaySlotPcRelativeBase = target + 2;
        Action<Sh2InstructionTrace>? observer = InstructionObserver;
        uint beforeSr = 0;
        uint beforeR0 = 0;
        uint beforeR1 = 0;
        uint beforeR2 = 0;
        uint beforeR3 = 0;
        uint beforeR4 = 0;
        uint beforeR5 = 0;
        uint beforeR6 = 0;
        uint beforeR7 = 0;
        uint beforeR8 = 0;
        uint beforeR9 = 0;
        uint beforeR10 = 0;
        uint beforeR11 = 0;
        uint beforeR12 = 0;
        uint beforeR13 = 0;
        uint beforeR14 = 0;
        uint beforeR15 = 0;
        uint beforePr = 0;
        uint beforeGbr = 0;
        uint beforeVbr = 0;
        long beforeCycles = 0;
        if (observer is not null)
        {
            beforeSr = SR;
            beforeR0 = R[0];
            beforeR1 = R[1];
            beforeR2 = R[2];
            beforeR3 = R[3];
            beforeR4 = R[4];
            beforeR5 = R[5];
            beforeR6 = R[6];
            beforeR7 = R[7];
            beforeR8 = R[8];
            beforeR9 = R[9];
            beforeR10 = R[10];
            beforeR11 = R[11];
            beforeR12 = R[12];
            beforeR13 = R[13];
            beforeR14 = R[14];
            beforeR15 = R[15];
            beforePr = PR;
            beforeGbr = GBR;
            beforeVbr = VBR;
            beforeCycles = Cycles;
        }

        DelaySlotActive = true;
        int delayCycles = 0;
        try
        {
            delayCycles = Execute(delayOpcode, delaySlotPc);
            if (_bus is ISh2WaitStateBus waitStateBus)
            {
                int waitCycles = waitStateBus.ConsumeWaitCycles();
                delayCycles += waitCycles;
                _delaySlotWaitCycles += waitCycles;
            }
        }
        finally
        {
            DelaySlotActive = false;
            _delaySlotPcRelativeBase = null;
        }

        if (observer is not null)
        {
            observer(new Sh2InstructionTrace(
                _name,
                delaySlotPc,
                delayOpcode,
                PC,
                beforeSr,
                beforeR0,
                beforeR1,
                beforeR2,
                beforeR3,
                beforeR4,
                beforeR5,
                beforeR6,
                beforeR7,
                beforeR8,
                beforeR9,
                beforeR10,
                beforeR11,
                beforeR12,
                beforeR13,
                beforeR14,
                beforeR15,
                SR,
                R[0],
                R[1],
                R[2],
                R[3],
                R[4],
                R[5],
                R[6],
                R[7],
                R[8],
                R[9],
                R[10],
                R[11],
                R[12],
                R[13],
                R[14],
                R[15],
                beforePr,
                beforeGbr,
                beforeVbr,
                PR,
                GBR,
                VBR,
                beforeCycles,
                beforeCycles + delayCycles,
                delayCycles,
                DelaySlot: true));
        }

        PC = target;
    }

    private bool StartException(int vector, uint returnPc)
    {
        uint handler = _bus.ReadLong(VBR + (uint)(vector * 4));
        if (handler is 0x0000_0000 or 0xFFFF_FFFF)
        {
            return false;
        }

        R[15] -= 4;
        _bus.WriteLong(R[15], SR);
        R[15] -= 4;
        _bus.WriteLong(R[15], returnPc);
        PC = handler;
        Halted = false;
        return true;
    }

    private void ExecuteShad(int n, int m)
    {
        if ((R[m] & 0x8000_0000u) == 0)
        {
            R[n] <<= (int)(R[m] & 0x1F);
            return;
        }

        int count = 32 - (int)(R[m] & 0x1F);
        if (count >= 32)
        {
            R[n] = (R[n] & 0x8000_0000u) != 0 ? 0xFFFF_FFFFu : 0u;
            return;
        }

        R[n] = (uint)((int)R[n] >> count);
    }

    private void ExecuteShld(int n, int m)
    {
        if ((R[m] & 0x8000_0000u) == 0)
        {
            R[n] <<= (int)(R[m] & 0x1F);
            return;
        }

        int count = 32 - (int)(R[m] & 0x1F);
        R[n] = count >= 32 ? 0u : R[n] >> count;
    }

    private void ExecuteMacLong(int n, int m)
    {
        int left = (int)_bus.ReadLong(R[n]);
        R[n] += 4;
        int right = (int)_bus.ReadLong(R[m]);
        R[m] += 4;

        long accumulator = unchecked((long)(((ulong)MACH << 32) | MACL));
        long result = accumulator + ((long)left * right);
        if ((SR & SBit) != 0)
        {
            result = Math.Clamp(result, -140737488355328L, 140737488355327L);
        }

        MACH = (uint)(result >> 32);
        MACL = (uint)result;
    }

    private void ExecuteMacWord(int n, int m)
    {
        short left = (short)_bus.ReadWord(R[n]);
        R[n] += 2;
        short right = (short)_bus.ReadWord(R[m]);
        R[m] += 2;

        if ((SR & SBit) != 0)
        {
            long result = (int)MACL + (left * right);
            if (result > int.MaxValue)
            {
                MACL = 0x7FFF_FFFFu;
                MACH |= 1;
                return;
            }

            if (result < int.MinValue)
            {
                MACL = 0x8000_0000u;
                MACH |= 1;
                return;
            }

            MACL = (uint)(int)result;
            return;
        }

        long accumulator = unchecked((long)(((ulong)MACH << 32) | MACL));
        long mac = accumulator + (left * right);
        MACH = (uint)(mac >> 32);
        MACL = (uint)mac;
    }

    private void SetT(bool value)
    {
        if (value)
        {
            SR |= TBit;
        }
        else
        {
            SR &= ~TBit;
        }
    }

    private bool MatchesRtsLdsPrReturn(ISh2PeekBus peekBus, uint rtsPc, out uint returnPc, out uint restoredPr)
    {
        returnPc = 0;
        restoredPr = 0;
        if (!peekBus.TryPeekWord(rtsPc, out ushort rtsOpcode) ||
            !peekBus.TryPeekWord(rtsPc + 2, out ushort delayOpcode) ||
            rtsOpcode != 0x000B ||
            delayOpcode != 0x4F26 ||
            !TryPeekLong(peekBus, R[15], out restoredPr))
        {
            return false;
        }

        returnPc = PR;
        R[15] += 4;
        return true;
    }

    private static bool TryPeekLong(ISh2PeekBus peekBus, uint address, out uint value)
    {
        value = 0;
        if (!peekBus.TryPeekWord(address, out ushort high) ||
            !peekBus.TryPeekWord(address + 2, out ushort low))
        {
            return false;
        }

        value = ((uint)high << 16) | low;
        return true;
    }

    private static uint ReadPcRelativeLongLiteral(ISh2PeekBus peekBus, uint opcodePc, ushort opcode)
    {
        uint literalAddress = ((opcodePc + 4) & 0xFFFF_FFFCu) + (uint)((opcode & 0x00FF) * 4);
        return TryPeekLong(peekBus, literalAddress, out uint value) ? value : 0;
    }

    private static uint BranchByteTarget(uint branchPc, ushort branchOpcode)
    {
        return branchPc + 4 + (uint)(((sbyte)branchOpcode) * 2);
    }

    private void SetSr(uint value)
    {
        SR = value & SrWritableMask;
    }

    private uint PcRelativeBase(uint opcodePc)
    {
        return _delaySlotPcRelativeBase ?? opcodePc + 4;
    }

    private uint PcRelativeLongBase(uint opcodePc)
    {
        return PcRelativeBase(opcodePc) & 0xFFFF_FFFCu;
    }

    private void SetM(bool value)
    {
        if (value)
        {
            SR |= MBit;
        }
        else
        {
            SR &= ~MBit;
        }
    }

    private void SetQ(bool value)
    {
        if (value)
        {
            SR |= QBit;
        }
        else
        {
            SR &= ~QBit;
        }
    }

    private static uint SignExtend8(byte value)
    {
        return (uint)(sbyte)value;
    }

    private static uint SignExtend16(ushort value)
    {
        return (uint)(short)value;
    }

    private static int SignExtend12(int value)
    {
        return (value & 0x0800) != 0 ? value | unchecked((int)0xFFFF_F000) : value;
    }

    private static ushort SwapByteWord(ushort value)
    {
        return (ushort)(((value & 0x00FF) << 8) | ((value & 0xFF00) >> 8));
    }

    private static uint SwapByteWord(uint value)
    {
        return ((value & 0x0000_00FFu) << 8) | ((value & 0x0000_FF00u) >> 8) | (value & 0xFFFF_0000u);
    }

    public sealed record Sh2State(
        uint[] R,
        uint[] BankedR,
        uint PC,
        uint PR,
        uint GBR,
        uint VBR,
        uint MACH,
        uint MACL,
        uint SR,
        long Cycles,
        bool Halted,
        ushort LastOpcode,
        uint LastOpcodePc,
        int UnhandledOpcodeCount,
        bool DelaySlotActive,
        int PendingInterruptLevel,
        int PendingInterruptVectorNumber);

    public sealed record Sh2InstructionTrace(
        string Cpu,
        uint Pc,
        ushort Opcode,
        uint NextPc,
        uint BeforeSr,
        uint BeforeR0,
        uint BeforeR1,
        uint BeforeR2,
        uint BeforeR3,
        uint BeforeR4,
        uint BeforeR5,
        uint BeforeR6,
        uint BeforeR7,
        uint BeforeR8,
        uint BeforeR9,
        uint BeforeR10,
        uint BeforeR11,
        uint BeforeR12,
        uint BeforeR13,
        uint BeforeR14,
        uint BeforeR15,
        uint Sr,
        uint R0,
        uint R1,
        uint R2,
        uint R3,
        uint R4,
        uint R5,
        uint R6,
        uint R7,
        uint R8,
        uint R9,
        uint R10,
        uint R11,
        uint R12,
        uint R13,
        uint R14,
        uint R15,
        uint BeforePr,
        uint BeforeGbr,
        uint BeforeVbr,
        uint Pr,
        uint Gbr,
        uint Vbr,
        long BeforeCycles,
        long Cycles,
        int StepCycles,
        bool DelaySlot);

    public sealed record Sh2LinkedListTrace(
        string Cpu,
        uint Pc,
        string Half,
        uint Threshold,
        uint Node,
        uint Next,
        uint Value,
        bool Match,
        long Cycles,
        uint NewNode,
        uint Current = 0,
        uint OldPrevious = 0,
        uint WriteNewNext = 0,
        uint WriteNewPrev = 0,
        uint WriteOldPrevNext = 0,
        uint WriteCurrentPrev = 0,
        bool Completed = false,
        bool NoOp = false,
        uint RegisterR1 = 0,
        uint RegisterR2 = 0,
        uint RegisterR3 = 0,
        uint RegisterR4 = 0);

    public sealed record Sh2RechainTrace(
        string Cpu,
        uint Pc,
        string Phase,
        uint Current,
        uint Previous,
        uint Next,
        uint CurrentValue,
        uint Tail,
        uint InsertPrevious,
        uint InsertNext,
        uint InsertValue,
        bool Match,
        long Cycles,
        uint WritePreviousNext = 0,
        uint WriteNextPrevious = 0,
        uint WriteCurrentPrevious = 0,
        uint WriteCurrentNext = 0);

    public sealed record Sh2InterruptTrace(
        string Cpu,
        int Level,
        int VectorNumber,
        uint HandlerPc,
        uint Sr,
        uint R15);
}
