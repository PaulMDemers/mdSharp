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
    private readonly int[] _pendingInterruptVectorsByLevel = new int[16];
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
        ClearAllPendingInterrupts();
    }

    public void SetVbr(uint value)
    {
        VBR = value;
    }

    public void SetGbr(uint value)
    {
        GBR = value;
    }

    public void RequestInterrupt(int level, int? vectorNumber = null)
    {
        if (level is < 1 or > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }

        _pendingInterruptVectorsByLevel[level] = vectorNumber ?? (64 + level);
        RefreshPendingInterruptView();
    }

    public void ClearPendingInterrupt(int level, int vectorNumber)
    {
        if (level is < 1 or > 15)
        {
            return;
        }

        if (_pendingInterruptVectorsByLevel[level] == vectorNumber)
        {
            _pendingInterruptVectorsByLevel[level] = 0;
            RefreshPendingInterruptView();
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

    public bool TryFastForwardBraSelfNopIdleLoop(int maxCycles, out int cycles)
    {
        cycles = 0;
        if (maxCycles < 2 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc, out ushort branchOpcode) ||
            (branchOpcode & 0xF000) != 0xA000 ||
            BranchWordTarget(loopPc, branchOpcode) != loopPc ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort delaySlotOpcode) ||
            delaySlotOpcode != 0x0009)
        {
            bool atBranch = false;
            if (peekBus.TryPeekWord(loopPc, out ushort firstNopOpcode) &&
                firstNopOpcode == 0x0009 &&
                peekBus.TryPeekWord(loopPc + 2, out ushort secondNopOpcode) &&
                secondNopOpcode == 0x0009 &&
                peekBus.TryPeekWord(loopPc + 4, out branchOpcode) &&
                (branchOpcode & 0xF000) == 0xA000 &&
                BranchWordTarget(loopPc + 4, branchOpcode) == loopPc &&
                peekBus.TryPeekWord(loopPc + 6, out delaySlotOpcode) &&
                delaySlotOpcode == 0x0009)
            {
                const int TwoNopBranchNopCyclesPerIteration = 4;
                if (maxCycles < TwoNopBranchNopCyclesPerIteration)
                {
                    return false;
                }

                cycles = maxCycles - (maxCycles % TwoNopBranchNopCyclesPerIteration);
                Cycles += cycles;
                LastOpcode = delaySlotOpcode;
                LastOpcodePc = loopPc + 6;
                PC = loopPc;
                return true;
            }

            if (loopPc >= 2 &&
                peekBus.TryPeekWord(loopPc - 2, out firstNopOpcode) &&
                firstNopOpcode == 0x0009 &&
                peekBus.TryPeekWord(loopPc, out secondNopOpcode) &&
                secondNopOpcode == 0x0009 &&
                peekBus.TryPeekWord(loopPc + 2, out branchOpcode) &&
                (branchOpcode & 0xF000) == 0xA000 &&
                BranchWordTarget(loopPc + 2, branchOpcode) == loopPc - 2 &&
                peekBus.TryPeekWord(loopPc + 4, out delaySlotOpcode) &&
                delaySlotOpcode == 0x0009)
            {
                loopPc -= 2;
                const int TwoNopBranchNopCyclesPerIteration = 4;
                if (maxCycles < 1 + TwoNopBranchNopCyclesPerIteration)
                {
                    return false;
                }

                cycles = 1 + ((maxCycles - 1) - ((maxCycles - 1) % TwoNopBranchNopCyclesPerIteration));
                Cycles += cycles;
                LastOpcode = delaySlotOpcode;
                LastOpcodePc = loopPc + 6;
                PC = loopPc;
                return true;
            }

            if (loopPc >= 4 &&
                peekBus.TryPeekWord(loopPc - 4, out firstNopOpcode) &&
                firstNopOpcode == 0x0009 &&
                peekBus.TryPeekWord(loopPc - 2, out secondNopOpcode) &&
                secondNopOpcode == 0x0009 &&
                peekBus.TryPeekWord(loopPc, out branchOpcode) &&
                (branchOpcode & 0xF000) == 0xA000 &&
                BranchWordTarget(loopPc, branchOpcode) == loopPc - 4 &&
                peekBus.TryPeekWord(loopPc + 2, out delaySlotOpcode) &&
                delaySlotOpcode == 0x0009)
            {
                loopPc -= 4;
                const int TwoNopBranchNopCyclesPerIteration = 4;
                if (maxCycles < 3 + TwoNopBranchNopCyclesPerIteration)
                {
                    return false;
                }

                cycles = 3 + ((maxCycles - 3) - ((maxCycles - 3) % TwoNopBranchNopCyclesPerIteration));
                Cycles += cycles;
                LastOpcode = delaySlotOpcode;
                LastOpcodePc = loopPc + 6;
                PC = loopPc;
                return true;
            }

            if (peekBus.TryPeekWord(loopPc, out firstNopOpcode) &&
                firstNopOpcode == 0x0009 &&
                peekBus.TryPeekWord(loopPc + 2, out branchOpcode) &&
                (branchOpcode & 0xF000) == 0xA000 &&
                BranchWordTarget(loopPc + 2, branchOpcode) == loopPc &&
                peekBus.TryPeekWord(loopPc + 4, out delaySlotOpcode) &&
                delaySlotOpcode == 0x0009)
            {
                atBranch = false;
            }
            else if (loopPc >= 2 &&
                peekBus.TryPeekWord(loopPc - 2, out firstNopOpcode) &&
                firstNopOpcode == 0x0009 &&
                peekBus.TryPeekWord(loopPc, out branchOpcode) &&
                (branchOpcode & 0xF000) == 0xA000 &&
                BranchWordTarget(loopPc, branchOpcode) == loopPc - 2 &&
                peekBus.TryPeekWord(loopPc + 2, out delaySlotOpcode) &&
                delaySlotOpcode == 0x0009)
            {
                loopPc -= 2;
                atBranch = true;
            }
            else
            {
                return false;
            }

            const int NopBranchNopCyclesPerIteration = 3;
            int entryCycles = atBranch ? 2 : 0;
            if (maxCycles < entryCycles + NopBranchNopCyclesPerIteration)
            {
                return false;
            }

            cycles = entryCycles + ((maxCycles - entryCycles) - ((maxCycles - entryCycles) % NopBranchNopCyclesPerIteration));
            Cycles += cycles;
            LastOpcode = delaySlotOpcode;
            LastOpcodePc = loopPc + 4;
            PC = loopPc;
            return true;
        }

        const int CyclesPerIteration = 2;
        cycles = maxCycles - (maxCycles % CyclesPerIteration);
        Cycles += cycles;
        LastOpcode = delaySlotOpcode;
        LastOpcodePc = loopPc + 2;
        PC = loopPc;
        return true;
    }

    public bool TryFastForwardAddBraNopDelayLoop(int maxCycles, out int cycles)
    {
        cycles = 0;
        if (maxCycles < 4 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!TryResolveAddBraNopDelayLoop(peekBus, ref loopPc, out ushort addOpcode, out ushort branchOpcode))
        {
            return false;
        }

        const int CyclesPerIteration = 4;
        const int MaxBurstCycles = 4096;
        int budget = Math.Min(maxCycles, MaxBurstCycles);
        int iterations = budget / CyclesPerIteration;
        if (iterations <= 0)
        {
            return false;
        }

        int register = (addOpcode >> 8) & 0x0F;
        int immediate = (sbyte)(addOpcode & 0xFF);
        R[register] = unchecked(R[register] + (uint)(immediate * iterations));
        cycles = iterations * CyclesPerIteration;
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 2;
        PC = loopPc;
        return true;
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
        bool startedAtBranch = false;
        if (!peekBus.TryPeekWord(loopPc, out ushort dtOpcode))
        {
            if (loopPc < 2 ||
                !peekBus.TryPeekWord(loopPc - 2, out dtOpcode))
            {
                return false;
            }

            loopPc -= 2;
            startedAtBranch = true;
        }

        if ((dtOpcode & 0xF0FF) != 0x4010)
        {
            if (loopPc < 2 ||
                !peekBus.TryPeekWord(loopPc - 2, out dtOpcode) ||
                (dtOpcode & 0xF0FF) != 0x4010)
            {
                return false;
            }

            loopPc -= 2;
            startedAtBranch = true;
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

        if (startedAtBranch && PC != loopPc + 2)
        {
            return false;
        }

        if (startedAtBranch && (SR & TBit) != 0)
        {
            return false;
        }

        int register = (dtOpcode >> 8) & 0x0F;
        uint count = R[register];
        int entryCycles = startedAtBranch ? 1 : 0;
        if (maxCycles <= entryCycles)
        {
            return false;
        }

        uint maxIterations = startedAtBranch
            ? (uint)((maxCycles - entryCycles) / 2)
            : (uint)(maxCycles / 2);
        if (startedAtBranch && maxIterations > 0)
        {
            maxIterations--;
        }

        uint iterations = count == 0 ? maxIterations : Math.Min(count, maxIterations);
        if (iterations == 0)
        {
            return false;
        }

        R[register] = count - iterations;
        cycles = checked(entryCycles + (int)(iterations * 2));
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
            if (startedAtBranch)
            {
                Cycles += maxCycles - cycles;
                cycles = maxCycles;
            }
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

    public bool TryFastForwardMovWStoreDtBfSAddLoop(int maxCycles, Func<uint, ushort, bool> writeWord, int cyclesPerIteration, out int cycles)
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
        bool matched = false;
        ushort storeOpcode = 0;
        ushort dtOpcode = 0;
        ushort branchOpcode = 0;
        ushort addOpcode = 0;
        ReadOnlySpan<int> offsets = [0, -2, -4, -6];
        foreach (int offset in offsets)
        {
            if (offset < 0 && PC < (uint)-offset)
            {
                continue;
            }

            uint candidate = unchecked(PC + (uint)offset);
            if (!peekBus.TryPeekWord(candidate, out ushort firstOpcode) ||
                !peekBus.TryPeekWord(candidate + 2, out ushort secondOpcode) ||
                !peekBus.TryPeekWord(candidate + 4, out ushort thirdOpcode) ||
                !peekBus.TryPeekWord(candidate + 6, out ushort fourthOpcode) ||
                (thirdOpcode & 0xFF00) != 0x8F00 ||
                BranchByteTarget(candidate + 4, thirdOpcode) != candidate)
            {
                continue;
            }

            if ((firstOpcode & 0xF00F) == 0x2001 &&
                (secondOpcode & 0xF0FF) == 0x4010 &&
                (fourthOpcode & 0xF000) == 0x7000)
            {
                loopPc = candidate;
                storeOpcode = firstOpcode;
                dtOpcode = secondOpcode;
                branchOpcode = thirdOpcode;
                addOpcode = fourthOpcode;
                matched = true;
                break;
            }

            if ((firstOpcode & 0xF0FF) == 0x4010 &&
                (secondOpcode & 0xF00F) == 0x2001 &&
                (fourthOpcode & 0xF000) == 0x7000)
            {
                loopPc = candidate;
                dtOpcode = firstOpcode;
                storeOpcode = secondOpcode;
                branchOpcode = thirdOpcode;
                addOpcode = fourthOpcode;
                matched = true;
                break;
            }
        }

        if (!matched)
        {
            return false;
        }

        int addressRegister = (storeOpcode >> 8) & 0x0F;
        int sourceRegister = (storeOpcode >> 4) & 0x0F;
        int countRegister = (dtOpcode >> 8) & 0x0F;
        int addRegister = (addOpcode >> 8) & 0x0F;
        int addImmediate = (sbyte)(byte)addOpcode;
        if (addRegister != addressRegister || addImmediate != 2)
        {
            return false;
        }

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
        LastOpcodePc = loopPc + 4;

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

    public bool TryFastForwardMovWStoreAddRegisterDtBfLoop(int maxCycles, Func<uint, ushort, bool> writeWord, int cyclesPerIteration, out int cycles)
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

        if (!TryFindFiveWordLoop(peekBus, [0, -2, -4, -6, -8], out uint loopPc, out ushort storeOpcode, out ushort addAddressOpcode, out ushort addValueOpcode, out ushort dtOpcode, out ushort branchOpcode) ||
            (storeOpcode & 0xF00F) != 0x2001 ||
            (addAddressOpcode & 0xF000) != 0x7000 ||
            (addValueOpcode & 0xF00F) != 0x300C ||
            (dtOpcode & 0xF0FF) != 0x4010 ||
            (branchOpcode & 0xFF00) != 0x8B00)
        {
            return false;
        }

        int addressRegister = (storeOpcode >> 8) & 0x0F;
        int valueRegister = (storeOpcode >> 4) & 0x0F;
        int addAddressRegister = (addAddressOpcode >> 8) & 0x0F;
        int addImmediate = (sbyte)(byte)addAddressOpcode;
        int addValueDestination = (addValueOpcode >> 8) & 0x0F;
        int stepRegister = (addValueOpcode >> 4) & 0x0F;
        int countRegister = (dtOpcode >> 8) & 0x0F;
        if (addAddressRegister != addressRegister ||
            addImmediate != 2 ||
            addValueDestination != valueRegister)
        {
            return false;
        }

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
        uint value = R[valueRegister];
        uint step = R[stepRegister];
        uint completed = 0;
        while (completed < iterations)
        {
            if (!writeWord(address, (ushort)value))
            {
                break;
            }

            completed++;
            address += 2;
            value += step;
        }

        if (completed == 0)
        {
            return false;
        }

        R[addressRegister] += completed * 2;
        R[valueRegister] += step * completed;
        R[countRegister] = count - completed;
        cycles = checked((int)(completed * (uint)cyclesPerIteration));
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 8;

        if (completed == count)
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

    public bool TryFastForwardMovLStoreAddBfSDtLoop(int maxCycles, Func<uint, uint, bool> writeLong, int cyclesPerIteration, out int cycles)
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
            !peekBus.TryPeekWord(loopPc + 4, out ushort branchOpcode) ||
            !peekBus.TryPeekWord(loopPc + 6, out ushort dtOpcode))
        {
            return false;
        }

        if ((storeOpcode & 0xF00F) != 0x2002 ||
            (addOpcode & 0xF000) != 0x7000 ||
            (branchOpcode & 0xFF00) != 0x8F00 ||
            (dtOpcode & 0xF0FF) != 0x4010)
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
        uint target = loopPc + 8 + (uint)(displacement * 2);
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
        LastOpcode = dtOpcode;
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

    public bool TryFastForwardWordTableSearchLoop(int maxCycles, Func<uint, ushort?> readWord, out int cycles)
    {
        cycles = 0;
        const int CyclesPerIteration = 11;
        if (maxCycles < CyclesPerIteration ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc + 0, out ushort extsIndexOpcode) ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort movIndexOpcode) ||
            !peekBus.TryPeekWord(loopPc + 4, out ushort addIndexOpcode) ||
            !peekBus.TryPeekWord(loopPc + 6, out ushort loadOpcode) ||
            !peekBus.TryPeekWord(loopPc + 8, out ushort extuValueOpcode) ||
            !peekBus.TryPeekWord(loopPc + 10, out ushort cmpEqOpcode) ||
            !peekBus.TryPeekWord(loopPc + 12, out ushort foundBranchOpcode) ||
            !peekBus.TryPeekWord(loopPc + 14, out ushort incrementOpcode) ||
            !peekBus.TryPeekWord(loopPc + 16, out ushort extsLimitOpcode) ||
            !peekBus.TryPeekWord(loopPc + 18, out ushort cmpGtOpcode) ||
            !peekBus.TryPeekWord(loopPc + 20, out ushort loopBranchOpcode))
        {
            return false;
        }

        if ((extsIndexOpcode & 0xF0FF) != 0x603F ||
            (movIndexOpcode & 0xF00F) != 0x6003 ||
            (addIndexOpcode & 0xF00F) != 0x300C ||
            (loadOpcode & 0xF00F) != 0x000D ||
            (extuValueOpcode & 0xF0FF) != 0x601D ||
            (cmpEqOpcode & 0xF00F) != 0x3000 ||
            (foundBranchOpcode & 0xFF00) != 0x8900 ||
            (incrementOpcode & 0xF000) != 0x7000 ||
            (extsLimitOpcode & 0xF0FF) != 0x603F ||
            (cmpGtOpcode & 0xF00F) != 0x3007 ||
            (loopBranchOpcode & 0xFF00) != 0x8B00)
        {
            return false;
        }

        int indexRegister = (extsIndexOpcode >> 4) & 0x0F;
        int signedIndexRegister = (extsIndexOpcode >> 8) & 0x0F;
        int movSourceRegister = (movIndexOpcode >> 4) & 0x0F;
        int doubledIndexRegister = (movIndexOpcode >> 8) & 0x0F;
        int addSourceRegister = (addIndexOpcode >> 4) & 0x0F;
        int addDestinationRegister = (addIndexOpcode >> 8) & 0x0F;
        int valueRegister = (loadOpcode >> 8) & 0x0F;
        const int offsetRegister = 0;
        int tableBaseRegister = (loadOpcode >> 4) & 0x0F;
        int extuSourceRegister = (extuValueOpcode >> 4) & 0x0F;
        int extuDestinationRegister = (extuValueOpcode >> 8) & 0x0F;
        int compareExpectedRegister = (cmpEqOpcode >> 4) & 0x0F;
        int compareValueRegister = (cmpEqOpcode >> 8) & 0x0F;
        int incrementRegister = (incrementOpcode >> 8) & 0x0F;
        int incrementValue = (sbyte)(byte)incrementOpcode;
        int limitSignedRegister = (extsLimitOpcode >> 8) & 0x0F;
        int limitSourceRegister = (extsLimitOpcode >> 4) & 0x0F;
        int cmpGtLimitRegister = (cmpGtOpcode >> 4) & 0x0F;
        int cmpGtIndexRegister = (cmpGtOpcode >> 8) & 0x0F;

        if (signedIndexRegister != movSourceRegister ||
            signedIndexRegister != addSourceRegister ||
            doubledIndexRegister != addDestinationRegister ||
            doubledIndexRegister != offsetRegister ||
            valueRegister != signedIndexRegister ||
            valueRegister != extuSourceRegister ||
            valueRegister != extuDestinationRegister ||
            valueRegister != compareValueRegister ||
            compareExpectedRegister == valueRegister ||
            incrementRegister != indexRegister ||
            incrementValue != 1 ||
            limitSignedRegister != signedIndexRegister ||
            limitSourceRegister != indexRegister ||
            cmpGtIndexRegister != signedIndexRegister)
        {
            return false;
        }

        int foundDisplacement = (sbyte)foundBranchOpcode;
        uint foundTarget = loopPc + 16 + (uint)(foundDisplacement * 2);
        int loopDisplacement = (sbyte)loopBranchOpcode;
        uint loopTarget = loopPc + 24 + (uint)(loopDisplacement * 2);
        if (loopTarget != loopPc || foundTarget != loopPc + 22)
        {
            return false;
        }

        uint maxIterations = (uint)(maxCycles / CyclesPerIteration);
        if (maxIterations == 0)
        {
            return false;
        }

        uint completed = 0;
        bool found = false;
        bool exhausted = false;
        uint currentIndex = R[indexRegister];
        uint tableBase = R[tableBaseRegister];
        uint expected = R[compareExpectedRegister];
        uint limit = R[cmpGtLimitRegister];
        uint lastOffset = R[doubledIndexRegister];
        uint lastValue = R[valueRegister];
        uint lastSignedIndex = R[signedIndexRegister];

        while (completed < maxIterations)
        {
            lastSignedIndex = SignExtend16((ushort)currentIndex);
            lastOffset = lastSignedIndex + lastSignedIndex;
            ushort? word = readWord(tableBase + lastOffset);
            if (word is null)
            {
                break;
            }

            lastValue = word.Value;
            completed++;

            if (lastValue == expected)
            {
                found = true;
                break;
            }

            currentIndex++;
            lastSignedIndex = SignExtend16((ushort)currentIndex);
            if (SignedGreaterThan(lastSignedIndex, limit))
            {
                exhausted = true;
                break;
            }
        }

        if (completed == 0)
        {
            return false;
        }

        R[indexRegister] = currentIndex;
        R[doubledIndexRegister] = lastOffset;
        R[valueRegister] = found ? lastValue : lastSignedIndex;
        cycles = checked((int)(completed * CyclesPerIteration));
        Cycles += cycles;

        if (found || exhausted)
        {
            SetT(true);
            PC = loopPc + 22;
            LastOpcode = found ? foundBranchOpcode : loopBranchOpcode;
            LastOpcodePc = found ? loopPc + 12 : loopPc + 20;
        }
        else
        {
            SetT(false);
            PC = loopPc;
            LastOpcode = loopBranchOpcode;
            LastOpcodePc = loopPc + 20;
        }

        return true;
    }

    public bool TryFastForwardWordHighBitMaskTransformLoop(
        int maxCycles,
        Func<uint, ushort?> readWord,
        Func<uint, ushort, bool> writeWord,
        Func<uint, byte?> readByte,
        Func<uint, byte, bool> writeByte,
        out int cycles)
    {
        cycles = 0;
        const int CyclesPerIteration = 31;
        const int LoopBytes = 62;
        if (maxCycles < CyclesPerIteration ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        ReadOnlySpan<ushort> expected =
        [
            0x6033, 0x303C, 0x068D, 0xD11C, 0x2169, 0x2118, 0x890A, 0x6173,
            0x4711, 0x8900, 0x7107, 0x4121, 0x4121, 0x4121, 0x319C, 0x6210,
            0x224B, 0x2120, 0x614C, 0x6413, 0x4401, 0x911F, 0x2619, 0x6033,
            0x303C, 0x0865, 0x7301, 0x7501, 0xE107, 0x3517, 0x8BE0
        ];

        for (int i = 0; i < expected.Length; i++)
        {
            if (!peekBus.TryPeekWord(loopPc + (uint)(i * 2), out ushort opcode) ||
                opcode != expected[i])
            {
                return false;
            }
        }

        if (!TryPeekPcRelativeLong(peekBus, loopPc + 6, displacement: 0x1C, out uint highBitMask) ||
            highBitMask != 0x0000_8000 ||
            !TryPeekPcRelativeWord(peekBus, loopPc + 42, displacement: 0x1F, out ushort clearHighBitMask) ||
            clearHighBitMask != 0x7FFF)
        {
            return false;
        }

        uint maxIterations = (uint)(maxCycles / CyclesPerIteration);
        if (maxIterations == 0)
        {
            return false;
        }

        uint completed = 0;
        bool exhausted = false;
        while (completed < maxIterations)
        {
            if (SignedGreaterThan(R[5], 7))
            {
                exhausted = true;
                break;
            }

            uint wordOffset = R[3] + R[3];
            uint wordAddress = R[8] + wordOffset;
            ushort? sourceWord = readWord(wordAddress);
            if (sourceWord is null)
            {
                break;
            }

            R[0] = wordOffset;
            R[6] = (uint)(short)sourceWord.Value;
            R[1] = R[6] & 0x0000_8000u;
            bool highBitSet = R[1] != 0;
            SetT(!highBitSet);

            if (highBitSet)
            {
                R[1] = R[7];
                SetT((int)R[7] >= 0);
                if (!IsTSet())
                {
                    R[1] += 7;
                }

                R[1] = (uint)((int)R[1] >> 3);
                R[1] += R[9];
                byte? maskByte = readByte(R[1]);
                if (maskByte is null)
                {
                    break;
                }

                R[2] = (uint)(sbyte)maskByte.Value;
                R[2] |= R[4];
                if (!writeByte(R[1], (byte)R[2]))
                {
                    break;
                }
            }

            R[1] = R[4] & 0xFF;
            R[4] = R[1];
            SetT((R[4] & 1) != 0);
            R[4] >>= 1;
            R[1] = 0x0000_7FFF;
            R[6] &= R[1];
            R[0] = R[3] + R[3];
            if (!writeWord(R[8] + R[0], (ushort)R[6]))
            {
                break;
            }

            R[3]++;
            R[5]++;
            R[1] = 7;
            SetT(SignedGreaterThan(R[5], R[1]));
            completed++;
            if (IsTSet())
            {
                exhausted = true;
                break;
            }
        }

        if (completed == 0)
        {
            return false;
        }

        cycles = checked((int)(completed * CyclesPerIteration));
        Cycles += cycles;
        LastOpcode = 0x8BE0;
        LastOpcodePc = loopPc + 60;
        PC = exhausted ? loopPc + LoopBytes : loopPc;
        if (!exhausted)
        {
            SetT(false);
        }

        return true;
    }

    public bool TryFastForwardWordHighBitMaskTransformOuterLoop(
        int maxCycles,
        Func<uint, ushort?> readWord,
        Func<uint, ushort, bool> writeWord,
        Func<uint, byte?> readByte,
        Func<uint, byte, bool> writeByte,
        out int cycles)
    {
        cycles = 0;
        const int CyclesPerOuterIteration = 332;
        const int InnerIterations = 8;
        const int LoopBytes = 96;
        if (maxCycles < CyclesPerOuterIteration ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        ReadOnlySpan<ushort> expected =
        [
            0x64A3, 0x6173, 0x4711, 0x8900, 0x7107, 0x6013, 0x4021, 0x4021,
            0x4021, 0xE100, 0x0914, 0xE500, 0x6373, 0x6033, 0x303C, 0x068D,
            0xD11C, 0x2169, 0x2118, 0x890A, 0x6173, 0x4711, 0x8900, 0x7107,
            0x4121, 0x4121, 0x4121, 0x319C, 0x6210, 0x224B, 0x2120, 0x614C,
            0x6413, 0x4401, 0x911F, 0x2619, 0x6033, 0x303C, 0x0865, 0x7301,
            0x7501, 0xE107, 0x3517, 0x8BE0, 0x7708, 0xD10E, 0x3717, 0x8BCF
        ];

        for (int i = 0; i < expected.Length; i++)
        {
            if (!peekBus.TryPeekWord(loopPc + (uint)(i * 2), out ushort opcode) ||
                opcode != expected[i])
            {
                return false;
            }
        }

        if (!TryPeekPcRelativeLong(peekBus, loopPc + 32, displacement: 0x1C, out uint highBitMask) ||
            !TryPeekPcRelativeWord(peekBus, loopPc + 68, displacement: 0x1F, out ushort clearHighBitMask) ||
            !TryPeekPcRelativeLong(peekBus, loopPc + 90, displacement: 0x0E, out uint outerLimit) ||
            highBitMask != 0x0000_8000 ||
            clearHighBitMask != 0x7FFF)
        {
            return false;
        }

        uint maxOuterIterations = (uint)(maxCycles / CyclesPerOuterIteration);
        uint completed = 0;
        bool exhausted = false;
        while (completed < maxOuterIterations)
        {
            R[4] = R[10];
            R[1] = R[7];
            SetT((int)R[7] >= 0);
            if (!IsTSet())
            {
                R[1] += 7;
            }

            R[0] = (uint)((int)R[1] >> 3);
            R[1] = 0;
            if (!writeByte(R[9] + R[0], 0))
            {
                break;
            }

            R[5] = 0;
            R[3] = R[7];
            for (int i = 0; i < InnerIterations; i++)
            {
                uint wordOffset = R[3] + R[3];
                ushort? sourceWord = readWord(R[8] + wordOffset);
                if (sourceWord is null)
                {
                    goto Done;
                }

                R[0] = wordOffset;
                R[6] = (uint)(short)sourceWord.Value;
                R[1] = R[6] & highBitMask;
                bool highBitSet = R[1] != 0;
                SetT(!highBitSet);
                if (highBitSet)
                {
                    R[1] = R[7];
                    SetT((int)R[7] >= 0);
                    if (!IsTSet())
                    {
                        R[1] += 7;
                    }

                    R[1] = (uint)((int)R[1] >> 3);
                    R[1] += R[9];
                    byte? maskByte = readByte(R[1]);
                    if (maskByte is null)
                    {
                        goto Done;
                    }

                    R[2] = (uint)(sbyte)maskByte.Value;
                    R[2] |= R[4];
                    if (!writeByte(R[1], (byte)R[2]))
                    {
                        goto Done;
                    }
                }

                R[1] = R[4] & 0xFF;
                R[4] = R[1];
                SetT((R[4] & 1) != 0);
                R[4] >>= 1;
                R[1] = clearHighBitMask;
                R[6] &= R[1];
                R[0] = R[3] + R[3];
                if (!writeWord(R[8] + R[0], (ushort)R[6]))
                {
                    goto Done;
                }

                R[3]++;
                R[5]++;
                R[1] = 7;
                SetT(SignedGreaterThan(R[5], R[1]));
            }

            R[7] += 8;
            R[1] = outerLimit;
            SetT(SignedGreaterThan(R[7], R[1]));
            completed++;
            if (IsTSet())
            {
                exhausted = true;
                break;
            }
        }

Done:
        if (completed == 0)
        {
            return false;
        }

        cycles = checked((int)(completed * CyclesPerOuterIteration));
        Cycles += cycles;
        LastOpcode = 0x8BCF;
        LastOpcodePc = loopPc + 94;
        PC = exhausted ? loopPc + LoopBytes : loopPc;
        if (!exhausted)
        {
            SetT(false);
        }

        return true;
    }

    public bool TryFastForwardByteFillIndexedCmpGeLoop(
        int maxCycles,
        Func<uint, byte, bool> writeByte,
        out int cycles)
    {
        cycles = 0;
        const int CyclesPerIteration = 9;
        const int LoopBytes = 14;
        if (maxCycles < CyclesPerIteration ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        ReadOnlySpan<ushort> expected = [0x613F, 0x316C, 0x2170, 0x7301, 0x613F, 0x3123, 0x8BF8];
        for (int i = 0; i < expected.Length; i++)
        {
            if (!peekBus.TryPeekWord(loopPc + (uint)(i * 2), out ushort opcode) ||
                opcode != expected[i])
            {
                return false;
            }
        }

        uint maxIterations = (uint)(maxCycles / CyclesPerIteration);
        uint completed = 0;
        bool exhausted = false;
        while (completed < maxIterations)
        {
            R[1] = (uint)(short)(ushort)R[3];
            R[1] += R[6];
            if (!writeByte(R[1], (byte)R[7]))
            {
                break;
            }

            R[3]++;
            R[1] = (uint)(short)(ushort)R[3];
            SetT((int)R[1] >= (int)R[2]);
            completed++;
            if (IsTSet())
            {
                exhausted = true;
                break;
            }
        }

        if (completed == 0)
        {
            return false;
        }

        cycles = checked((int)(completed * CyclesPerIteration));
        Cycles += cycles;
        LastOpcode = 0x8BF8;
        LastOpcodePc = loopPc + 12;
        PC = exhausted ? loopPc + LoopBytes : loopPc;
        if (!exhausted)
        {
            SetT(false);
        }

        return true;
    }

    public bool TryFastForwardMovBStoreDtBfsAddLoop(
        int maxCycles,
        Func<uint, byte, bool> writeByte,
        int cyclesPerIteration,
        out int cycles)
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
        if (!MatchesMovBStoreDtBfsAddPattern(peekBus, loopPc) ||
            R[2] == 0)
        {
            return false;
        }

        uint maxIterations = (uint)(maxCycles / cyclesPerIteration);
        uint iterations = Math.Min(R[2], maxIterations);
        byte value = (byte)R[0];
        uint address = R[1];
        uint completed = 0;
        while (completed < iterations)
        {
            if (!writeByte(address, value))
            {
                break;
            }

            address++;
            completed++;
        }

        if (completed == 0)
        {
            return false;
        }

        R[1] = address;
        R[2] -= completed;
        bool finished = R[2] == 0;
        SetT(finished);
        cycles = checked((int)(completed * (uint)cyclesPerIteration));
        Cycles += cycles;
        LastOpcode = 0x8FFC;
        LastOpcodePc = loopPc + 4;
        PC = finished ? loopPc + 8 : loopPc;
        return true;
    }

    private static bool MatchesMovBStoreDtBfsAddPattern(ISh2PeekBus peekBus, uint loopPc)
    {
        ReadOnlySpan<ushort> expected = [0x2100, 0x4210, 0x8FFC, 0x7101];
        return MatchesInstructionSequence(peekBus, loopPc, expected);
    }

    public bool TryFastForwardGbrWordHelperJsrBfsPollLoop(
        int maxCycles,
        Func<uint, ushort?> readWord,
        out int cycles)
    {
        const int CyclesPerIteration = 12;
        cycles = 0;
        if (maxCycles < CyclesPerIteration ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        uint helperPc = R[8];
        if (!MatchesGbrWordHelperJsrBfsPollPattern(peekBus, loopPc) ||
            !MatchesGbrWordHelperRoutine(peekBus, helperPc))
        {
            return false;
        }

        ushort? polled = readWord(GBR + 0x22);
        if (polled is null || polled.Value == 0)
        {
            return false;
        }

        int iterations = Math.Max(1, maxCycles / CyclesPerIteration);
        R[0] = polled.Value;
        R[4] = 2;
        R[5] = 7;
        PR = loopPc + 4;
        SetT(false);
        cycles = iterations * CyclesPerIteration;
        Cycles += cycles;
        LastOpcode = 0x8FFB;
        LastOpcodePc = loopPc + 6;
        PC = loopPc;
        return true;
    }

    private static bool MatchesGbrWordHelperJsrBfsPollPattern(ISh2PeekBus peekBus, uint loopPc)
    {
        ReadOnlySpan<ushort> expected = [0x480B, 0xE401, 0x2008, 0x8FFB, 0xE507];
        return MatchesInstructionSequence(peekBus, loopPc, expected);
    }

    private static bool MatchesGbrWordHelperRoutine(ISh2PeekBus peekBus, uint helperPc)
    {
        ReadOnlySpan<ushort> expected = [0x0012, 0x7020, 0x4400, 0x004D, 0x000B, 0x600D];
        return MatchesInstructionSequence(peekBus, helperPc, expected);
    }

    public bool TryFastForwardLongTstImmediateBtPollLoop(
        int maxCycles,
        Func<uint, byte?> readByte,
        out int cycles)
    {
        const int CyclesPerIteration = 3;
        cycles = 0;
        if (maxCycles < CyclesPerIteration ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!MatchesLongTstImmediateBtPollPattern(peekBus, loopPc))
        {
            return false;
        }

        byte? b0 = readByte(R[2]);
        byte? b1 = readByte(R[2] + 1);
        byte? b2 = readByte(R[2] + 2);
        byte? b3 = readByte(R[2] + 3);
        if (b0 is null || b1 is null || b2 is null || b3 is null)
        {
            return false;
        }

        uint value = (uint)((b0.Value << 24) | (b1.Value << 16) | (b2.Value << 8) | b3.Value);
        if ((value & 2) != 0)
        {
            return false;
        }

        int iterations = Math.Max(1, maxCycles / CyclesPerIteration);
        R[0] = value;
        SetT(true);
        cycles = iterations * CyclesPerIteration;
        Cycles += cycles;
        LastOpcode = 0x89FC;
        LastOpcodePc = loopPc + 4;
        PC = loopPc;
        return true;
    }

    private static bool MatchesLongTstImmediateBtPollPattern(ISh2PeekBus peekBus, uint loopPc)
    {
        ReadOnlySpan<ushort> expected = [0x6022, 0xC802, 0x89FC];
        return MatchesInstructionSequence(peekBus, loopPc, expected);
    }

    public bool TryFastForwardDmaIdleCommunicationLongMismatchPollLoop(
        int maxCycles,
        Func<uint, byte?> readByte,
        out int cycles)
    {
        const int CyclesPerIteration = 6;
        cycles = 0;
        if (maxCycles < CyclesPerIteration ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!MatchesDmaIdleCommunicationLongMismatchPollPattern(peekBus, loopPc))
        {
            return false;
        }

        uint? dmaControl = TryReadBigEndianLong(readByte, R[2]);
        uint? communication = TryReadBigEndianLong(readByte, GBR + 0x20);
        if (dmaControl is null ||
            communication is null ||
            (dmaControl.Value & 2) == 0 ||
            communication.Value == R[1])
        {
            return false;
        }

        int iterations = Math.Max(1, maxCycles / CyclesPerIteration);
        R[0] = communication.Value;
        SetT(false);
        cycles = iterations * CyclesPerIteration;
        Cycles += cycles;
        LastOpcode = 0x8BF9;
        LastOpcodePc = loopPc + 0x0A;
        PC = loopPc;
        return true;
    }

    private static bool MatchesDmaIdleCommunicationLongMismatchPollPattern(ISh2PeekBus peekBus, uint loopPc)
    {
        ReadOnlySpan<ushort> expected = [0x6022, 0xC802, 0x89FC, 0xC608, 0x3100, 0x8BF9];
        return MatchesInstructionSequence(peekBus, loopPc, expected);
    }

    public bool TryFastForwardLongReloadCmpEqBfsPollLoop(
        int maxCycles,
        Func<uint, byte?> readByte,
        out int cycles)
    {
        const int CyclesPerIteration = 3;
        cycles = 0;
        if (maxCycles < CyclesPerIteration ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!MatchesLongReloadCmpEqBfsPollPattern(peekBus, loopPc))
        {
            return false;
        }

        uint? current = TryReadBigEndianLong(readByte, R[0]);
        if (current is null || current.Value == R[4])
        {
            return false;
        }

        int iterations = Math.Max(1, maxCycles / CyclesPerIteration);
        R[1] = current.Value;
        SetT(false);
        cycles = iterations * CyclesPerIteration;
        Cycles += cycles;
        LastOpcode = 0x8FFD;
        LastOpcodePc = loopPc + 2;
        PC = loopPc;
        return true;
    }

    private static bool MatchesLongReloadCmpEqBfsPollPattern(ISh2PeekBus peekBus, uint loopPc)
    {
        ReadOnlySpan<ushort> expected = [0x3140, 0x8FFD, 0x6102];
        return MatchesInstructionSequence(peekBus, loopPc, expected);
    }

    private static uint? TryReadBigEndianLong(Func<uint, byte?> readByte, uint address)
    {
        byte? b0 = readByte(address);
        byte? b1 = readByte(address + 1);
        byte? b2 = readByte(address + 2);
        byte? b3 = readByte(address + 3);
        if (b0 is null || b1 is null || b2 is null || b3 is null)
        {
            return null;
        }

        return (uint)((b0.Value << 24) | (b1.Value << 16) | (b2.Value << 8) | b3.Value);
    }

    public bool TryFastForwardByteDisplacementDualTstBraPollLoop(
        int maxCycles,
        Func<uint, byte?> readByte,
        out int cycles)
    {
        const int CyclesPerIteration = 8;
        cycles = 0;
        if (maxCycles < CyclesPerIteration ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!MatchesByteDisplacementDualTstBraPollPattern(peekBus, loopPc))
        {
            if (PC < 6)
            {
                return false;
            }

            loopPc = PC - 6;
            if (!MatchesByteDisplacementDualTstBraPollPattern(peekBus, loopPc) ||
                PC != loopPc + 6)
            {
                return false;
            }
        }

        byte? value = readByte(R[1] + 4);
        if (value is null || (value.Value & 0x78) != 0)
        {
            return false;
        }

        int iterations = Math.Max(1, maxCycles / CyclesPerIteration);
        R[0] = (uint)(sbyte)value.Value;
        SetT(true);
        cycles = iterations * CyclesPerIteration;
        Cycles += cycles;
        LastOpcode = 0xAFF9;
        LastOpcodePc = loopPc + 0x0A;
        PC = loopPc;
        return true;
    }

    private static bool MatchesByteDisplacementDualTstBraPollPattern(ISh2PeekBus peekBus, uint loopPc)
    {
        ReadOnlySpan<ushort> expected = [0x8414, 0xC838, 0x8B0E, 0xC840, 0x8B04, 0xAFF9, 0x0009];
        return MatchesInstructionSequence(peekBus, loopPc, expected);
    }

    public bool TryFastForwardByteLookupWordRowExpandLoop(
        int maxCycles,
        Func<uint, byte?> readByte,
        Func<uint, ushort?> readWord,
        Func<uint, ushort, bool> writeWord,
        out int cycles)
    {
        cycles = 0;
        const int RowCycles = 127;
        const int BytesPerRow = 8;
        const int LoopBytes = 0x7E;
        if (maxCycles < RowCycles ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        for (int i = 0; i < BytesPerRow; i++)
        {
            uint pc = loopPc + (uint)(i * 14);
            if (!peekBus.TryPeekWord(pc, out ushort loadByte) ||
                !peekBus.TryPeekWord(pc + 2, out ushort extendByte) ||
                !peekBus.TryPeekWord(pc + 4, out ushort shiftLeft) ||
                !peekBus.TryPeekWord(pc + 6, out ushort lookupWord) ||
                !peekBus.TryPeekWord(pc + 8, out ushort orMask) ||
                !peekBus.TryPeekWord(pc + 10, out ushort storeWord) ||
                !peekBus.TryPeekWord(pc + 12, out ushort advanceDestination) ||
                loadByte != 0x6084 ||
                extendByte != 0x600C ||
                shiftLeft != 0x4000 ||
                lookupWord != 0x00CD ||
                orMask != 0x20DB ||
                storeWord != 0x2E01 ||
                advanceDestination != 0x3E7C)
            {
                return false;
            }
        }

        uint tailPc = loopPc + 112;
        if (!peekBus.TryPeekWord(tailPc, out ushort addStride) ||
            !peekBus.TryPeekWord(tailPc + 2, out ushort dtCounter) ||
            !peekBus.TryPeekWord(tailPc + 4, out ushort exitBranch) ||
            !peekBus.TryPeekWord(tailPc + 6, out ushort branchDelay) ||
            !peekBus.TryPeekWord(tailPc + 8, out ushort loadLoopTarget) ||
            !peekBus.TryPeekWord(tailPc + 10, out ushort jumpLoopTarget) ||
            !peekBus.TryPeekWord(tailPc + 12, out ushort jumpDelay) ||
            addStride != 0x3E6C ||
            dtCounter != 0x4910 ||
            exitBranch != 0x8D03 ||
            branchDelay != 0x0009 ||
            loadLoopTarget != 0xD005 ||
            jumpLoopTarget != 0x402B ||
            jumpDelay != 0x0009 ||
            !TryPeekPcRelativeLong(peekBus, tailPc + 8, displacement: 0x05, out uint loopTarget) ||
            loopTarget != loopPc)
        {
            return false;
        }

        uint maxRows = (uint)(maxCycles / RowCycles);
        if (maxRows == 0)
        {
            return false;
        }

        uint completed = 0;
        bool exhausted = false;
        while (completed < maxRows)
        {
            for (int i = 0; i < BytesPerRow; i++)
            {
                byte? sourceByte = readByte(R[8]);
                if (sourceByte is null)
                {
                    goto Done;
                }

                R[8]++;
                R[0] = sourceByte.Value;
                SetT(false);
                R[0] <<= 1;
                ushort? lookup = readWord(R[12] + R[0]);
                if (lookup is null)
                {
                    goto Done;
                }

                R[0] = (uint)(short)lookup.Value;
                R[0] |= R[13];
                if (!writeWord(R[14], (ushort)R[0]))
                {
                    goto Done;
                }

                R[14] += R[7];
            }

            R[14] += R[6];
            R[9]--;
            SetT(R[9] == 0);
            completed++;
            if (IsTSet())
            {
                exhausted = true;
                break;
            }

            R[0] = loopPc;
        }

Done:
        if (completed == 0)
        {
            return false;
        }

        cycles = checked((int)(completed * RowCycles));
        Cycles += cycles;
        LastOpcode = exhausted ? branchDelay : jumpDelay;
        LastOpcodePc = exhausted ? tailPc + 6 : tailPc + 12;
        PC = exhausted ? loopPc + LoopBytes : loopPc;
        if (!exhausted)
        {
            SetT(false);
            R[0] = loopPc;
        }

        return true;
    }

    public bool TryFastForwardByteLookupWordStoreStep(
        int maxCycles,
        Func<uint, byte?> readByte,
        Func<uint, ushort?> readWord,
        Func<uint, ushort, bool> writeWord,
        out int cycles)
    {
        cycles = 0;
        const int StepCycles = 25;
        if (maxCycles < StepCycles ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint stepPc = PC;
        if (!peekBus.TryPeekWord(stepPc, out ushort loadByte) ||
            !peekBus.TryPeekWord(stepPc + 2, out ushort extendByte) ||
            !peekBus.TryPeekWord(stepPc + 4, out ushort shiftLeft) ||
            !peekBus.TryPeekWord(stepPc + 6, out ushort lookupWord) ||
            !peekBus.TryPeekWord(stepPc + 8, out ushort orMask) ||
            !peekBus.TryPeekWord(stepPc + 10, out ushort storeWord) ||
            !peekBus.TryPeekWord(stepPc + 12, out ushort advanceDestination) ||
            loadByte != 0x6084 ||
            extendByte != 0x600C ||
            shiftLeft != 0x4000 ||
            lookupWord != 0x00CD ||
            orMask != 0x20DB ||
            storeWord != 0x2E01 ||
            advanceDestination != 0x3E7C)
        {
            return false;
        }

        byte? sourceByte = readByte(R[8]);
        if (sourceByte is null)
        {
            return false;
        }

        R[8]++;
        R[0] = sourceByte.Value;
        SetT(false);
        R[0] <<= 1;
        ushort? lookup = readWord(R[12] + R[0]);
        if (lookup is null)
        {
            return false;
        }

        R[0] = (uint)(short)lookup.Value;
        R[0] |= R[13];
        if (!writeWord(R[14], (ushort)R[0]))
        {
            return false;
        }

        R[14] += R[7];
        cycles = StepCycles;
        Cycles += cycles;
        LastOpcode = 0x3E7C;
        LastOpcodePc = stepPc + 12;
        PC = stepPc + 14;
        return true;
    }

    public bool TryFastForwardMaskedStridedByteSpanLoop(
        int maxCycles,
        Func<uint, byte?> readByte,
        Func<uint, byte, bool> writeByte,
        int cyclesPerIteration,
        out int cycles)
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
        bool found = false;
        for (int back = 0; back <= 24; back += 2)
        {
            if (back != 0 && PC < (uint)back)
            {
                break;
            }

            uint candidate = PC - (uint)back;
            if (MatchesMaskedStridedByteSpanPattern(peekBus, candidate))
            {
                loopPc = candidate;
                found = true;
                break;
            }
        }

        if (!found)
        {
            return false;
        }

        uint counter = R[2];
        uint limit = R[5];
        if ((int)counter > (int)limit)
        {
            return false;
        }

        uint remaining = limit - counter + 1;
        uint maxIterations = (uint)(maxCycles / cyclesPerIteration);
        uint iterations = Math.Min(remaining, maxIterations);
        if (iterations == 0)
        {
            return false;
        }

        uint sourceBase = R[7];
        uint destination = R[8];
        uint completed = 0;
        byte output = 0;
        while (completed < iterations)
        {
            uint sourceOffset = ((counter + completed) & 0x3Fu) << 6;
            byte? source = readByte(sourceBase + sourceOffset);
            if (source is null)
            {
                break;
            }

            output = (byte)(source.Value | 0x01);
            if (!writeByte(destination + completed, output))
            {
                break;
            }

            completed++;
        }

        if (completed == 0)
        {
            return false;
        }

        uint newCounter = counter + completed;
        R[0] = (uint)(sbyte)output;
        R[0] |= 1;
        R[1] = (newCounter - 1) & 0x3Fu;
        R[2] = newCounter;
        R[8] = destination + completed;
        bool finished = (int)newCounter > (int)limit;
        SetT(finished);
        cycles = checked((int)(completed * (uint)cyclesPerIteration));
        Cycles += cycles;
        LastOpcode = 0x7801;
        LastOpcodePc = loopPc + 0x18;
        PC = finished ? loopPc + 0x1A : loopPc;
        return true;
    }

    private static bool MatchesMaskedStridedByteSpanPattern(ISh2PeekBus peekBus, uint loopPc)
    {
        ReadOnlySpan<ushort> expected =
        [
            0xE13F, 0x2129, 0x6013, 0x4008, 0x4008, 0x4008, 0x007C,
            0x7201, 0x3257, 0xCB01, 0x2800, 0x8FF3, 0x7801
        ];
        return MatchesInstructionSequence(peekBus, loopPc, expected);
    }

    public bool TryFastForwardBackwardLongRecordScanLoop(
        int maxCycles,
        Func<uint, uint?> readLong,
        out int cycles)
    {
        const int CyclesPerIteration = 10;
        cycles = 0;
        if (maxCycles < CyclesPerIteration ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        bool found = false;
        for (int back = 0; back <= 0x2C; back += 2)
        {
            if (back != 0 && PC < (uint)back)
            {
                break;
            }

            uint candidate = PC - (uint)back;
            if (MatchesBackwardLongRecordScanPattern(peekBus, candidate))
            {
                loopPc = candidate;
                found = true;
                break;
            }
        }

        if (!found)
        {
            return false;
        }

        if (!TryPeekPcRelativeLong(peekBus, loopPc + 0x22, 0x08, out uint sentinelAddress) ||
            readLong(sentinelAddress) is not uint sentinel)
        {
            return false;
        }

        uint current = R[3];
        uint target = R[6];
        uint maxIterations = (uint)(maxCycles / CyclesPerIteration);
        uint completed = 0;
        bool exhausted = false;
        while (completed < maxIterations)
        {
            if (readLong(current + 12) is not uint recordValue)
            {
                break;
            }

            if (recordValue == target)
            {
                break;
            }

            uint comparedPointer = current;
            current -= 16;
            completed++;
            exhausted = comparedPointer == sentinel;
            if (exhausted)
            {
                break;
            }
        }

        if (completed == 0)
        {
            return false;
        }

        R[1] = sentinel;
        R[2] = current + 16;
        R[3] = current;
        R[7] = sentinelAddress;
        SetT(exhausted);
        cycles = checked((int)(completed * CyclesPerIteration));
        Cycles += cycles;
        LastOpcode = 0x73F0;
        LastOpcodePc = loopPc + 0x2C;
        PC = exhausted ? loopPc + 0x2E : loopPc;
        return true;
    }

    private static bool MatchesBackwardLongRecordScanPattern(ISh2PeekBus peekBus, uint loopPc)
    {
        ReadOnlySpan<ushort> head = [0x5133, 0x3160, 0x8B0D, 0x5132, 0x2149, 0x3150, 0x8B09];
        ReadOnlySpan<ushort> tail = [0xD708, 0x6233, 0x6172, 0x3210, 0x8FE9, 0x73F0];
        return MatchesInstructionSequence(peekBus, loopPc, head) &&
            MatchesInstructionSequence(peekBus, loopPc + 0x22, tail);
    }

    public bool TryFastForwardWordFillCmpEqMinusOneBfsLoop(
        int maxCycles,
        Func<uint, ushort, bool> writeWord,
        out int cycles)
    {
        const int SetupCycles = 1;
        const int TakenIterationCycles = 8;
        const int FinalIterationCycles = 7;
        cycles = 0;
        if (maxCycles < FinalIterationCycles ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint tailPc = PC;
        bool includeSetup = false;
        if (MatchesWordFillCmpEqMinusOneBfsPattern(peekBus, PC + 2))
        {
            if (!peekBus.TryPeekWord(PC, out ushort setupOpcode) ||
                (setupOpcode & 0xF00F) != 0x6003)
            {
                return false;
            }

            int setupDestination = (setupOpcode >> 8) & 0x0F;
            int setupSource = (setupOpcode >> 4) & 0x0F;
            if (setupDestination != 1 || setupSource != 5)
            {
                return false;
            }

            tailPc = PC + 2;
            includeSetup = true;
        }
        else if (!MatchesWordFillCmpEqMinusOneBfsPattern(peekBus, tailPc))
        {
            for (int back = 2; back <= 8; back += 2)
            {
                if (PC < (uint)back)
                {
                    break;
                }

                uint candidate = PC - (uint)back;
                if (MatchesWordFillCmpEqMinusOneBfsPattern(peekBus, candidate))
                {
                    tailPc = candidate;
                    break;
                }
            }

            if (!MatchesWordFillCmpEqMinusOneBfsPattern(peekBus, tailPc))
            {
                return false;
            }
        }

        if (includeSetup && maxCycles <= SetupCycles)
        {
            return false;
        }

        uint remaining = R[0] + 1;
        if (remaining == 0)
        {
            return false;
        }

        int availableCycles = includeSetup ? maxCycles - SetupCycles : maxCycles;
        uint maxIterations;
        long completionCycles = ((long)(remaining - 1) * TakenIterationCycles) + FinalIterationCycles;
        if (completionCycles <= availableCycles)
        {
            maxIterations = remaining;
        }
        else
        {
            maxIterations = (uint)(availableCycles / TakenIterationCycles);
            if (maxIterations == 0 && availableCycles >= FinalIterationCycles)
            {
                maxIterations = 1;
            }
        }

        uint iterations = Math.Min(remaining, maxIterations);
        if (iterations == 0)
        {
            return false;
        }

        ushort value = (ushort)(includeSetup ? R[5] : R[1]);
        uint address = R[4];
        uint completed = 0;
        while (completed < iterations)
        {
            if (!writeWord(address, value))
            {
                break;
            }

            address += 2;
            completed++;
        }

        if (completed == 0)
        {
            return false;
        }

        if (includeSetup)
        {
            R[1] = R[5];
        }

        R[0] -= completed;
        R[4] = address;
        bool finished = completed == remaining;
        SetT(finished);
        cycles = includeSetup ? SetupCycles : 0;
        cycles += finished
            ? checked((int)(((completed - 1) * TakenIterationCycles) + FinalIterationCycles))
            : checked((int)(completed * TakenIterationCycles));
        Cycles += cycles;
        LastOpcode = 0x7402;
        LastOpcodePc = tailPc + 8;
        PC = finished ? tailPc + 10 : tailPc;
        return true;
    }

    private static bool MatchesWordFillCmpEqMinusOneBfsPattern(ISh2PeekBus peekBus, uint tailPc)
    {
        ReadOnlySpan<ushort> expected = [0x2411, 0x70FF, 0x88FF, 0x8FFB, 0x7402];
        return MatchesInstructionSequence(peekBus, tailPc, expected);
    }

    public bool TryFastForwardWordFillAddCompareGtBfsLoop(
        int maxCycles,
        Func<uint, ushort, bool> writeWord,
        out int cycles)
    {
        const int CyclesPerIteration = 7;
        cycles = 0;
        if (maxCycles < CyclesPerIteration ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!MatchesWordFillAddCompareGtBfsPattern(peekBus, loopPc) ||
            (int)R[1] > (int)R[3])
        {
            return false;
        }

        uint remaining = (uint)((int)R[3] - (int)R[1] + 1);
        uint maxIterations = (uint)(maxCycles / CyclesPerIteration);
        uint iterations = Math.Min(remaining, maxIterations);
        if (iterations == 0)
        {
            return false;
        }

        ushort value = (ushort)R[7];
        uint address = R[2];
        uint completed = 0;
        while (completed < iterations)
        {
            if (!writeWord(address, value))
            {
                break;
            }

            address += 2;
            completed++;
        }

        if (completed == 0)
        {
            return false;
        }

        R[1] += completed;
        R[2] = address;
        bool finished = (int)R[1] > (int)R[3];
        SetT(finished);
        cycles = checked((int)(completed * CyclesPerIteration));
        Cycles += cycles;
        LastOpcode = 0x8FFB;
        LastOpcodePc = loopPc + 6;
        PC = finished ? loopPc + 10 : loopPc;
        return true;
    }

    private static bool MatchesWordFillAddCompareGtBfsPattern(ISh2PeekBus peekBus, uint loopPc)
    {
        ReadOnlySpan<ushort> expected = [0x2271, 0x7101, 0x3137, 0x8FFB, 0x7202];
        return MatchesInstructionSequence(peekBus, loopPc, expected);
    }

    public bool TryFastForwardByteSpanCompareLoop(
        int maxCycles,
        Func<uint, byte?> readByte,
        int cyclesPerMatchedIteration,
        out int cycles)
    {
        cycles = 0;
        if (maxCycles < cyclesPerMatchedIteration ||
            cyclesPerMatchedIteration <= 0 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!MatchesByteSpanComparePattern(peekBus, loopPc))
        {
            return false;
        }

        uint source = R[5];
        uint destination = R[4];
        uint count = R[6];
        if (count == 0)
        {
            return false;
        }

        byte currentDestination = (byte)R[2];
        uint maxIterations = (uint)(maxCycles / cyclesPerMatchedIteration);
        uint completed = 0;
        while (completed < maxIterations)
        {
            byte? sourceByte = readByte(source);
            if (sourceByte is null)
            {
                break;
            }

            R[1] = (uint)(sbyte)sourceByte.Value;
            SetT(R[1] == 0);
            if (sourceByte.Value == 0)
            {
                SetT(currentDestination == sourceByte.Value);
                cycles = checked((int)(completed * (uint)cyclesPerMatchedIteration) + 4);
                Cycles += cycles;
                LastOpcode = 0x8D09;
                LastOpcodePc = loopPc + 4;
                PC = loopPc + 0x1A;
                return true;
            }

            SetT(currentDestination == sourceByte.Value);
            destination++;
            if (currentDestination != sourceByte.Value)
            {
                cycles = checked((int)(completed * (uint)cyclesPerMatchedIteration) + 6);
                Cycles += cycles;
                LastOpcode = 0x8F0E;
                LastOpcodePc = loopPc + 8;
                R[4] = destination;
                PC = loopPc + 0x28;
                return true;
            }

            count--;
            SetT(count == 0);
            source++;
            if (count == 0)
            {
                cycles = checked((int)(completed * (uint)cyclesPerMatchedIteration) + 10);
                Cycles += cycles;
                LastOpcode = 0x8D08;
                LastOpcodePc = loopPc + 0x10;
                R[4] = destination;
                R[5] = source;
                R[6] = count;
                PC = loopPc + 0x24;
                return true;
            }

            byte? nextDestinationByte = readByte(destination);
            if (nextDestinationByte is null)
            {
                break;
            }

            currentDestination = nextDestinationByte.Value;
            R[2] = (uint)(sbyte)currentDestination;
            SetT(currentDestination == 0);
            completed++;
            if (currentDestination == 0)
            {
                cycles = checked((int)(completed * (uint)cyclesPerMatchedIteration));
                Cycles += cycles;
                LastOpcode = 0x8BF2;
                LastOpcodePc = loopPc + 0x18;
                R[4] = destination;
                R[5] = source;
                R[6] = count;
                PC = loopPc + 0x1A;
                return true;
            }

            R[4] = destination;
            R[5] = source;
            R[6] = count;
        }

        if (completed == 0)
        {
            return false;
        }

        cycles = checked((int)(completed * (uint)cyclesPerMatchedIteration));
        Cycles += cycles;
        LastOpcode = 0x8BF2;
        LastOpcodePc = loopPc + 0x18;
        PC = loopPc;
        return true;
    }

    private static bool MatchesByteSpanComparePattern(ISh2PeekBus peekBus, uint loopPc)
    {
        ReadOnlySpan<ushort> expected =
        [
            0x6150, 0x2118, 0x8D09, 0x3210, 0x8F0E, 0x7401, 0x76FF,
            0x2668, 0x8D08, 0x7501, 0x6240, 0x2228, 0x8BF2
        ];
        return MatchesInstructionSequence(peekBus, loopPc, expected);
    }

    public bool TryFastForwardByteNibbleLookupExpandLoop(
        int maxCycles,
        Func<uint, byte?> readByte,
        Func<uint, byte, bool> writeByte,
        out int cycles)
    {
        const int MinCyclesPerIteration = 21;
        const int SdramLookupReadWaitCycles = 12;
        cycles = 0;
        if (maxCycles < MinCyclesPerIteration ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!MatchesByteNibbleLookupExpandPattern(peekBus, loopPc))
        {
            return false;
        }

        uint remaining = R[1];
        if (remaining == 0)
        {
            return false;
        }

        uint tableBase = R[0];
        uint source = R[2];
        uint destination = R[3];
        byte zeroSubstitute = (byte)R[6];
        uint completed = 0;
        int accumulatedCycles = 0;
        byte lastHigh = 0;
        byte lastLow = 0;

        while (completed < remaining)
        {
            byte? packed = readByte(source);
            if (packed is null)
            {
                break;
            }

            int highNibble = packed.Value >> 4;
            int lowNibble = packed.Value & 0x0F;
            int iterationCycles = MinCyclesPerIteration;

            byte? high = highNibble >= 8 ? zeroSubstitute : readByte(tableBase + (uint)highNibble);
            if (high is null)
            {
                break;
            }

            if (highNibble < 8)
            {
                iterationCycles += SdramLookupReadWaitCycles;
            }

            byte? low = lowNibble >= 8 ? zeroSubstitute : readByte(tableBase + (uint)lowNibble);
            if (low is null)
            {
                break;
            }

            if (lowNibble < 8)
            {
                iterationCycles += SdramLookupReadWaitCycles;
            }

            if (accumulatedCycles + iterationCycles > maxCycles)
            {
                break;
            }

            if (!writeByte(destination, high.Value) ||
                !writeByte(destination + 1, low.Value))
            {
                break;
            }

            lastHigh = high.Value;
            lastLow = low.Value;
            source++;
            destination += 2;
            completed++;
            accumulatedCycles += iterationCycles;
        }

        if (completed == 0)
        {
            return false;
        }

        R[1] = remaining - completed;
        R[2] = source;
        R[3] = destination;
        R[4] = (uint)(sbyte)lastHigh;
        R[5] = (uint)(sbyte)lastLow;
        R[7] = 8;

        bool finished = R[1] == 0;
        SetT(finished);
        cycles = accumulatedCycles;
        Cycles += cycles;
        LastOpcode = 0x8BE9;
        LastOpcodePc = loopPc + 0x2A;
        PC = finished ? loopPc + 0x2C : loopPc;
        return true;
    }

    private static bool MatchesByteNibbleLookupExpandPattern(ISh2PeekBus peekBus, uint loopPc)
    {
        ReadOnlySpan<ushort> expected =
        [
            0x6424, 0xE50F, 0x2549, 0x4409, 0x4409, 0xE708, 0x2478, 0x8901,
            0xA001, 0x6463, 0x044C, 0x2340, 0x7301, 0x2578, 0x8901, 0xA001,
            0x6563, 0x055C, 0x2350, 0x7301, 0x4110, 0x8BE9
        ];
        return MatchesInstructionSequence(peekBus, loopPc, expected);
    }

    public bool TryFastForwardUnrolledLongFillGtBtsLoop(
        int maxCycles,
        Func<uint, uint, bool> writeLong,
        out int cycles)
    {
        const int CyclesPerIteration = 13;
        cycles = 0;
        if (maxCycles < CyclesPerIteration ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        bool found = false;
        for (int back = 0; back <= 0x16; back += 2)
        {
            if (back != 0 && PC < (uint)back)
            {
                break;
            }

            uint candidate = PC - (uint)back;
            if (MatchesUnrolledLongFillGtBtsPattern(peekBus, candidate))
            {
                loopPc = candidate;
                found = true;
                break;
            }
        }

        if (!found || R[1] != 15 || (int)R[0] <= 15)
        {
            return false;
        }

        uint requested = (R[0] - 15) / 16;
        uint maxIterations = (uint)(maxCycles / CyclesPerIteration);
        uint iterations = Math.Min(requested, maxIterations);
        if (iterations == 0)
        {
            return false;
        }

        uint address = R[4];
        uint value = R[5];
        uint completed = 0;
        while (completed < iterations)
        {
            if (!writeLong(address + 28, value) ||
                !writeLong(address + 24, value) ||
                !writeLong(address + 20, value) ||
                !writeLong(address + 16, value) ||
                !writeLong(address + 12, value) ||
                !writeLong(address + 8, value) ||
                !writeLong(address + 4, value) ||
                !writeLong(address, value))
            {
                break;
            }

            address += 32;
            completed++;
        }

        if (completed == 0)
        {
            return false;
        }

        R[0] -= completed * 16;
        R[4] = address;
        bool branchTaken = (int)R[0] > 15;
        SetT(branchTaken);
        cycles = checked((int)(completed * CyclesPerIteration));
        Cycles += cycles;
        LastOpcode = 0x7420;
        LastOpcodePc = loopPc + 0x16;
        PC = branchTaken ? loopPc : loopPc + 0x18;
        return true;
    }

    private static bool MatchesUnrolledLongFillGtBtsPattern(ISh2PeekBus peekBus, uint loopPc)
    {
        ReadOnlySpan<ushort> expected =
        [
            0x1457, 0x1456, 0x1455, 0x1454, 0x1453, 0x1452,
            0x1451, 0x2452, 0x70F0, 0x3017, 0x8DF4, 0x7420
        ];
        return MatchesInstructionSequence(peekBus, loopPc, expected);
    }

    public bool TryFastForwardMovLNopDtBfSAddLoop(int maxCycles, Func<uint, uint, bool> writeLong, int cyclesPerIteration, out int cycles)
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

        uint storePc = PC;
        if (!peekBus.TryPeekWord(storePc, out ushort storeOpcode) ||
            (storeOpcode & 0xF00F) != 0x2002)
        {
            return false;
        }

        uint pc = storePc + 2;
        int leadingNops = 0;
        while (leadingNops < 16)
        {
            if (!peekBus.TryPeekWord(pc, out ushort possibleNop))
            {
                return false;
            }

            if (possibleNop != 0x0009)
            {
                break;
            }

            leadingNops++;
            pc += 2;
        }

        if (leadingNops == 0 ||
            !peekBus.TryPeekWord(pc, out ushort dtOpcode) ||
            (dtOpcode & 0xF0FF) != 0x4010)
        {
            return false;
        }

        uint branchPc = pc + 2;
        int trailingNops = 0;
        while (trailingNops < 16)
        {
            if (!peekBus.TryPeekWord(branchPc, out ushort possibleNop))
            {
                return false;
            }

            if (possibleNop != 0x0009)
            {
                break;
            }

            trailingNops++;
            branchPc += 2;
        }

        if (!peekBus.TryPeekWord(branchPc, out ushort branchOpcode) ||
            (branchOpcode & 0xFF00) != 0x8F00 ||
            !peekBus.TryPeekWord(branchPc + 2, out ushort addOpcode) ||
            (addOpcode & 0xF000) != 0x7000)
        {
            return false;
        }

        int addressRegister = (storeOpcode >> 8) & 0x0F;
        int sourceRegister = (storeOpcode >> 4) & 0x0F;
        int countRegister = (dtOpcode >> 8) & 0x0F;
        int addRegister = (addOpcode >> 8) & 0x0F;
        int addImmediate = (sbyte)(byte)addOpcode;
        if (addRegister != addressRegister || addImmediate != 4)
        {
            return false;
        }

        int displacement = (sbyte)branchOpcode;
        uint target = branchPc + 4 + (uint)(displacement * 2);
        if (target != storePc)
        {
            return false;
        }

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
        LastOpcode = addOpcode;
        LastOpcodePc = branchPc + 2;

        if (completed == count)
        {
            SetT(true);
            PC = branchPc + 4;
        }
        else
        {
            SetT(false);
            PC = storePc;
        }

        return true;
    }

    public bool TryFastForwardDtMovLManyNopBfSAddLoop(int maxCycles, Func<uint, uint, bool> writeLong, int cyclesPerIteration, out int cycles)
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
        ushort dtOpcode = 0;
        ushort storeOpcode = 0;
        ushort branchOpcode = 0;
        ushort addOpcode = 0;
        uint branchPc = 0;
        bool found = false;
        for (int back = 0; back <= 36; back += 2)
        {
            if (back != 0 && PC < (uint)back)
            {
                break;
            }

            uint candidate = PC - (uint)back;
            if (!TryReadDtMovLManyNopBfSAddPattern(peekBus, candidate, out dtOpcode, out storeOpcode, out branchOpcode, out addOpcode, out branchPc))
            {
                continue;
            }

            loopPc = candidate;
            found = true;
            break;
        }

        if (!found)
        {
            return false;
        }

        int countRegister = (dtOpcode >> 8) & 0x0F;
        int addressRegister = (storeOpcode >> 8) & 0x0F;
        int sourceRegister = (storeOpcode >> 4) & 0x0F;
        int addRegister = (addOpcode >> 8) & 0x0F;
        int addImmediate = (sbyte)(byte)addOpcode;
        if (addRegister != addressRegister || addImmediate != 4)
        {
            return false;
        }

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

        R[addressRegister] = address;
        R[countRegister] = count - completed;
        cycles = checked((int)(completed * (uint)cyclesPerIteration));
        Cycles += cycles;
        LastOpcode = addOpcode;
        LastOpcodePc = branchPc + 2;

        if (completed == count)
        {
            SetT(true);
            PC = branchPc + 4;
        }
        else
        {
            SetT(false);
            PC = loopPc;
        }

        return true;
    }

    private static bool TryReadDtMovLManyNopBfSAddPattern(
        ISh2PeekBus peekBus,
        uint loopPc,
        out ushort dtOpcode,
        out ushort storeOpcode,
        out ushort branchOpcode,
        out ushort addOpcode,
        out uint branchPc)
    {
        dtOpcode = 0;
        storeOpcode = 0;
        branchOpcode = 0;
        addOpcode = 0;
        branchPc = 0;
        if (!peekBus.TryPeekWord(loopPc, out dtOpcode) ||
            (dtOpcode & 0xF0FF) != 0x4010 ||
            !peekBus.TryPeekWord(loopPc + 2, out storeOpcode) ||
            (storeOpcode & 0xF00F) != 0x2002)
        {
            return false;
        }

        uint pc = loopPc + 4;
        int nops = 0;
        while (nops < 16)
        {
            if (!peekBus.TryPeekWord(pc, out ushort possibleNop))
            {
                return false;
            }

            if (possibleNop != 0x0009)
            {
                break;
            }

            nops++;
            pc += 2;
        }

        if (nops == 0 ||
            !peekBus.TryPeekWord(pc, out branchOpcode) ||
            (branchOpcode & 0xFF00) != 0x8F00 ||
            !peekBus.TryPeekWord(pc + 2, out addOpcode) ||
            (addOpcode & 0xF000) != 0x7000)
        {
            return false;
        }

        int displacement = (sbyte)branchOpcode;
        uint target = pc + 4 + (uint)(displacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        branchPc = pc;
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

        int effectiveMaxCycles = maxCycles;
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

    public bool TryFastForwardMovWPostIncStoreAddImmediateDtBfLoop(
        int maxCycles,
        Func<uint, ushort?> readWord,
        Func<uint, ushort, bool> writeWord,
        out int cycles)
    {
        cycles = 0;
        const int TakenIterationCycles = 6;
        const int FinalIterationCycles = 4;
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
            (addOpcode & 0xF000) != 0x7000 ||
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
        int countRegister = (dtOpcode >> 8) & 0x0F;
        int addImmediate = (sbyte)(addOpcode & 0xFF);
        if (storeSource != valueRegister ||
            addDestination != storeDestination ||
            addImmediate != 2)
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
        uint destination = R[storeDestination];
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
            destination += 2;
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

    public bool TryFastForwardEmptyDescriptorSpanFillLoop(int maxCycles, Func<uint, ushort, bool> writeWord, out int cycles)
    {
        const int CyclesPerIteration = 6;
        cycles = 0;
        if (maxCycles < CyclesPerIteration ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        ushort[] prologueExpected = [0xD42F, 0xE304, 0x6593, 0x5046, 0x88FF, 0x893B];
        ushort[] scanHeadExpected = [0x5046, 0x88FF, 0x893B];
        ushort[] tailExpected =
        [
            0x4310, 0x8FBE, 0x742C, 0x4519, 0x4619, 0x655F,
            0x666F, 0x4515, 0x8901, 0xA003, 0xE501, 0x35A7,
            0x8B00, 0x65A3, 0x2851, 0x7802, 0x77FF, 0x4715,
            0x89AA
        ];

        bool atPrologue = MatchesInstructionSequence(peekBus, loopPc, prologueExpected) &&
            MatchesInstructionSequence(peekBus, loopPc + 0x84, tailExpected);
        bool atScanLoop = false;
        if (!atPrologue)
        {
            atScanLoop = MatchesInstructionSequence(peekBus, loopPc, scanHeadExpected);
            if (!atScanLoop)
            {
                return false;
            }

            loopPc -= 6;
            if (!MatchesInstructionSequence(peekBus, loopPc + 0x84, tailExpected))
            {
                return false;
            }
        }

        uint descriptorBase = atPrologue
            ? _bus.ReadLong(((loopPc + 4) & ~3u) + (0x2Fu * 4u))
            : R[4] - ((4u - Math.Clamp(R[3], 1u, 4u)) * 44u);
        if (descriptorBase == 0 ||
            R[7] == 0 ||
            R[8] == 0)
        {
            return false;
        }

        uint firstDescriptor = atPrologue ? 0u : 4u - Math.Clamp(R[3], 1u, 4u);
        for (uint descriptor = firstDescriptor; descriptor < 4; descriptor++)
        {
            if (_bus.ReadLong(descriptorBase + (descriptor * 44u) + 24u) != 0xFFFF_FFFFu)
            {
                return false;
            }
        }

        int spanWord = (short)(ushort)(R[9] >> 8);
        int maximum = (int)R[10];
        if (spanWord <= 0 || spanWord > maximum)
        {
            return false;
        }

        uint requested = atPrologue
            ? Math.Min(R[7], int.MaxValue / CyclesPerIteration)
            : 1u;
        uint iterations = Math.Min(requested, (uint)(maxCycles / CyclesPerIteration));
        if (iterations == 0)
        {
            return false;
        }

        uint address = R[8];
        ushort value = (ushort)spanWord;
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

        R[3] = 0;
        R[4] = descriptorBase + (4u * 44u);
        R[5] = (uint)spanWord;
        R[6] = (uint)(short)(ushort)(R[6] >> 8);
        R[8] += completed * 2;
        R[7] -= completed;
        cycles = checked((int)(completed * CyclesPerIteration));
        Cycles += cycles;
        LastOpcode = 0x89AA;
        LastOpcodePc = loopPc + 0xA8;

        if (R[7] == 0)
        {
            SetT(false);
            PC = loopPc + 0xAA;
        }
        else
        {
            SetT(true);
            PC = loopPc;
        }

        return true;
    }

    public bool TryFastForwardEmptyDescriptorSpanFillTail(int maxCycles, Func<uint, ushort, bool> writeWord, out int cycles)
    {
        const int CyclesPerTail = 4;
        const int CyclesPerAdditionalWrite = 6;
        cycles = 0;
        if (maxCycles < CyclesPerTail ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus ||
            R[7] == 0)
        {
            return false;
        }

        uint loopPc = PC - 0xA4;
        ushort[] tailRemainderExpected = [0x77FF, 0x4715, 0x89AA];
        ushort[] prologueExpected = [0xD42F, 0xE304, 0x6593, 0x5046, 0x88FF, 0x893B];
        if (!MatchesInstructionSequence(peekBus, PC, tailRemainderExpected) ||
            !MatchesInstructionSequence(peekBus, loopPc, prologueExpected))
        {
            return false;
        }

        ushort value = (ushort)(R[5] & 0xFFFF);
        uint remainingAfterCurrent = R[7] - 1;
        uint maximumAdditional = maxCycles <= CyclesPerTail
            ? 0u
            : (uint)((maxCycles - CyclesPerTail) / CyclesPerAdditionalWrite);
        uint writes = Math.Min(remainingAfterCurrent, maximumAdditional);
        uint address = R[8];
        for (uint i = 0; i < writes; i++)
        {
            if (!writeWord(address, value))
            {
                writes = i;
                break;
            }

            address += 2;
        }

        R[7] -= 1 + writes;
        R[8] += writes * 2;
        cycles = CyclesPerTail + checked((int)(writes * CyclesPerAdditionalWrite));
        Cycles += cycles;
        LastOpcode = 0x89AA;
        LastOpcodePc = PC + 4;
        if (R[7] == 0)
        {
            SetT(false);
            PC = loopPc + 0xAA;
        }
        else
        {
            SetT(true);
            PC = loopPc;
        }

        return true;
    }

    public bool TryFastForwardLongDifferenceEqualsOnePollLoop(int maxCycles, Func<uint, uint, bool> writeLong, out int cycles)
    {
        cycles = 0;
        if (maxCycles < 8 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        ushort[] expected = [0xD235, 0x6122, 0xD233, 0x6022, 0x3018, 0x8801, 0x8BF8];
        if (!MatchesInstructionSequence(peekBus, loopPc, expected))
        {
            return false;
        }

        uint completionAddress = _bus.ReadLong(((loopPc + 4) & ~3u) + (0x35u * 4u));
        uint sourceAddress = _bus.ReadLong(((loopPc + 8) & ~3u) + (0x33u * 4u));
        uint completion = _bus.ReadLong(completionAddress);
        uint source = _bus.ReadLong(sourceAddress);
        uint delta = source - completion;
        if (delta <= 1)
        {
            return false;
        }

        uint published = source - 1;
        if (!writeLong(completionAddress, published))
        {
            return false;
        }

        R[0] = 1;
        R[1] = published;
        R[2] = sourceAddress;
        SetT(true);
        PC = loopPc + 0x0E;
        LastOpcode = 0x8BF8;
        LastOpcodePc = loopPc + 0x0C;
        cycles = 8;
        Cycles += cycles;
        return true;
    }

    private static bool MatchesInstructionSequence(ISh2PeekBus peekBus, uint pc, ReadOnlySpan<ushort> expected)
    {
        for (int i = 0; i < expected.Length; i++)
        {
            if (!peekBus.TryPeekWord(pc + (uint)(i * 2), out ushort opcode) ||
                opcode != expected[i])
            {
                return false;
            }
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
        int effectiveMaxCycles = maxCycles;
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

    public bool TryFastForwardGbrCmpEqBtPollLoop(int maxCycles, out int cycles)
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

        if (!TryFindThreeWordPollLoop(peekBus, [0, -2, -4], out uint loopPc, out ushort loadOpcode, out ushort compareOpcode, out ushort branchOpcode) ||
            (branchOpcode & 0xFF00) != 0x8900 ||
            (compareOpcode & 0xFF00) != 0x8800)
        {
            return false;
        }

        uint address;
        uint value;
        if ((loadOpcode & 0xFF00) == 0xC400)
        {
            address = GBR + (uint)(loadOpcode & 0xFF);
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

            value = ((uint)high << 16) | low;
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

    public bool TryFastForwardSdramNullLinkedListIdleLoop(int maxCycles, Func<uint, uint?> readLong, out int cycles)
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
            !peekBus.TryPeekWord(loopPc + 2, out ushort headLoadOpcode) ||
            !peekBus.TryPeekWord(loopPc + 4, out ushort nextLoadOpcode) ||
            !peekBus.TryPeekWord(loopPc + 6, out ushort testOpcode) ||
            !peekBus.TryPeekWord(loopPc + 8, out ushort branchOpcode))
        {
            return false;
        }

        if ((literalOpcode & 0xF000) != 0xD000 ||
            (headLoadOpcode & 0xF00F) != 0x6002 ||
            (nextLoadOpcode & 0xF00F) != 0x5001 ||
            (testOpcode & 0xF00F) != 0x2008 ||
            (branchOpcode & 0xFF00) != 0x8F00)
        {
            return false;
        }

        int baseRegister = (literalOpcode >> 8) & 0x0F;
        int headSourceRegister = (headLoadOpcode >> 4) & 0x0F;
        int headValueRegister = (headLoadOpcode >> 8) & 0x0F;
        int nextBaseRegister = (nextLoadOpcode >> 4) & 0x0F;
        int nextValueRegister = (nextLoadOpcode >> 8) & 0x0F;
        int testN = (testOpcode >> 8) & 0x0F;
        int testM = (testOpcode >> 4) & 0x0F;
        if (headSourceRegister != baseRegister ||
            nextBaseRegister != baseRegister ||
            nextValueRegister != baseRegister ||
            testN != headValueRegister ||
            testM != headValueRegister)
        {
            return false;
        }

        uint branchTarget = BranchByteTarget(loopPc + 8, branchOpcode);
        if (branchTarget <= loopPc + 10)
        {
            return false;
        }

        uint baseAddress = ReadPcRelativeLongLiteral(peekBus, loopPc, literalOpcode);
        uint? headNullable = readLong(baseAddress);
        uint? nextNullable = readLong(baseAddress + 4);
        if (!headNullable.HasValue ||
            !nextNullable.HasValue ||
            headNullable.Value != 0 ||
            nextNullable.Value != 0xFFFF_FFFFu)
        {
            return false;
        }

        uint? braPc = null;
        for (uint pc = loopPc + 10; pc < branchTarget; pc += 2)
        {
            if (!peekBus.TryPeekWord(pc, out ushort opcode))
            {
                return false;
            }

            if ((opcode & 0xF000) == 0xA000)
            {
                if (BranchWordTarget(pc, opcode) != loopPc ||
                    pc + 2 >= branchTarget ||
                    !peekBus.TryPeekWord(pc + 2, out ushort delayOpcode) ||
                    delayOpcode != 0x0009)
                {
                    return false;
                }

                braPc = pc;
                break;
            }

            if (opcode != 0x0009)
            {
                return false;
            }
        }

        if (!braPc.HasValue)
        {
            return false;
        }

        cycles = maxCycles;
        Cycles += cycles;
        R[baseRegister] = baseAddress;
        R[headValueRegister] = 0;
        SetT(true);
        PC = loopPc;
        LastOpcode = 0x0009;
        LastOpcodePc = braPc.Value + 2;
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

        ushort loadOpcode = 0;
        ushort compareOpcode = 0;
        ushort branchOpcode = 0;
        bool found = false;
        if (!startAtLoad)
        {
            found = peekBus.TryPeekWord(loopPc, out loadOpcode) &&
                peekBus.TryPeekWord(loopPc + 2, out compareOpcode) &&
                peekBus.TryPeekWord(loopPc + 4, out branchOpcode);
        }
        else
        {
            ReadOnlySpan<int> offsets = [0, -2, -4];
            foreach (int offset in offsets)
            {
                if (offset < 0 && PC < (uint)-offset)
                {
                    continue;
                }

                uint candidate = (uint)(PC + offset);
                if (peekBus.TryPeekWord(candidate, out ushort candidateLoadOpcode) &&
                    peekBus.TryPeekWord(candidate + 2, out ushort candidateCompareOpcode) &&
                    peekBus.TryPeekWord(candidate + 4, out ushort candidateBranchOpcode) &&
                    (candidateLoadOpcode & 0xFF00) == 0xC500 &&
                    (candidateCompareOpcode & 0xF00F) == 0x3007 &&
                    (candidateBranchOpcode & 0xFF00) == 0x8B00)
                {
                    loopPc = candidate;
                    loadOpcode = candidateLoadOpcode;
                    compareOpcode = candidateCompareOpcode;
                    branchOpcode = candidateBranchOpcode;
                    startAtLoad = offset == 0;
                    found = true;
                    break;
                }
            }
        }

        if (!found)
        {
            return false;
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

    public bool TryFastForwardMovLiteralLongTstBtPollLoop(int maxCycles, out int cycles)
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
        if (!TryReadMovLiteralLongTstBtPattern(peekBus, loopPc, out ushort literalOpcode, out ushort loadOpcode, out ushort branchOpcode))
        {
            if (PC < 2 || !TryReadMovLiteralLongTstBtPattern(peekBus, PC - 2, out literalOpcode, out loadOpcode, out branchOpcode))
            {
                if (PC < 4 || !TryReadMovLiteralLongTstBtPattern(peekBus, PC - 4, out literalOpcode, out loadOpcode, out branchOpcode))
                {
                    if (PC < 6 || !TryReadMovLiteralLongTstBtPattern(peekBus, PC - 6, out literalOpcode, out loadOpcode, out branchOpcode))
                    {
                        return false;
                    }

                    loopPc = PC - 6;
                }
                else
                {
                    loopPc = PC - 4;
                }
            }
            else
            {
                loopPc = PC - 2;
            }
        }

        int literalRegister = (literalOpcode >> 8) & 0x0F;
        int loadDestination = (loadOpcode >> 8) & 0x0F;
        int loadSource = (loadOpcode >> 4) & 0x0F;
        if (loadSource != literalRegister || loadDestination != 0)
        {
            return false;
        }

        int displacement = (sbyte)branchOpcode;
        uint target = loopPc + 10 + (uint)(displacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        uint address = ReadPcRelativeLongLiteral(peekBus, loopPc, literalOpcode);
        if (!TryPeekLong(peekBus, address, out uint value) || value != 0)
        {
            return false;
        }

        R[literalRegister] = address;
        R[0] = 0;
        SetT(true);
        PC = loopPc;
        cycles = Math.Min(maxCycles, MaxBurstCycles);
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 6;
        return true;
    }

    private static bool TryReadMovLiteralLongTstBtPattern(
        ISh2PeekBus peekBus,
        uint pc,
        out ushort literalOpcode,
        out ushort loadOpcode,
        out ushort branchOpcode)
    {
        literalOpcode = 0;
        loadOpcode = 0;
        branchOpcode = 0;
        if (!peekBus.TryPeekWord(pc, out literalOpcode) ||
            !peekBus.TryPeekWord(pc + 2, out loadOpcode) ||
            !peekBus.TryPeekWord(pc + 4, out ushort testOpcode) ||
            !peekBus.TryPeekWord(pc + 6, out branchOpcode))
        {
            return false;
        }

        return (literalOpcode & 0xF000) == 0xD000 &&
            (loadOpcode & 0xF00F) == 0x6002 &&
            (testOpcode & 0xF00F) == 0x2008 &&
            (branchOpcode & 0xFF00) == 0x8900;
    }

    public bool TryFastForwardMovLiteralWordTstBtPollLoop(int maxCycles, out int cycles)
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
        if (!TryReadMovLiteralWordTstBtPattern(peekBus, loopPc, out ushort literalOpcode, out ushort loadOpcode, out ushort testOpcode, out ushort branchOpcode))
        {
            if (PC < 2 || !TryReadMovLiteralWordTstBtPattern(peekBus, PC - 2, out literalOpcode, out loadOpcode, out testOpcode, out branchOpcode))
            {
                if (PC < 4 || !TryReadMovLiteralWordTstBtPattern(peekBus, PC - 4, out literalOpcode, out loadOpcode, out testOpcode, out branchOpcode))
                {
                    if (PC < 6 || !TryReadMovLiteralWordTstBtPattern(peekBus, PC - 6, out literalOpcode, out loadOpcode, out testOpcode, out branchOpcode))
                    {
                        return false;
                    }

                    loopPc = PC - 6;
                }
                else
                {
                    loopPc = PC - 4;
                }
            }
            else
            {
                loopPc = PC - 2;
            }
        }

        int literalRegister = (literalOpcode >> 8) & 0x0F;
        int loadDestination = (loadOpcode >> 8) & 0x0F;
        int loadSource = (loadOpcode >> 4) & 0x0F;
        int testN = (testOpcode >> 8) & 0x0F;
        int testM = (testOpcode >> 4) & 0x0F;
        if (loadSource != literalRegister ||
            testN != loadDestination ||
            testM != loadDestination)
        {
            return false;
        }

        int displacement = (sbyte)branchOpcode;
        uint target = loopPc + 10 + (uint)(displacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        uint address = ReadPcRelativeLongLiteral(peekBus, loopPc, literalOpcode);
        if (!peekBus.TryPeekWord(address, out ushort value) || value != 0)
        {
            return false;
        }

        R[literalRegister] = address;
        R[loadDestination] = 0;
        SetT(true);
        PC = loopPc;
        cycles = Math.Min(maxCycles, MaxBurstCycles);
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 6;
        return true;
    }

    private static bool TryReadMovLiteralWordTstBtPattern(
        ISh2PeekBus peekBus,
        uint pc,
        out ushort literalOpcode,
        out ushort loadOpcode,
        out ushort testOpcode,
        out ushort branchOpcode)
    {
        literalOpcode = 0;
        loadOpcode = 0;
        testOpcode = 0;
        branchOpcode = 0;
        if (!peekBus.TryPeekWord(pc, out literalOpcode) ||
            !peekBus.TryPeekWord(pc + 2, out loadOpcode) ||
            !peekBus.TryPeekWord(pc + 4, out testOpcode) ||
            !peekBus.TryPeekWord(pc + 6, out branchOpcode))
        {
            return false;
        }

        return (literalOpcode & 0xF000) == 0xD000 &&
            (loadOpcode & 0xF00F) == 0x6001 &&
            (testOpcode & 0xF00F) == 0x2008 &&
            (branchOpcode & 0xFF00) == 0x8900;
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

    public bool TryFastForwardMovLiteralWordDisplacementTstBfPollLoop(int maxCycles, out int cycles)
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
        if (!TryReadMovLiteralWordDisplacementTstBfPattern(peekBus, loopPc, out ushort literalOpcode, out ushort loadOpcode, out ushort testOpcode, out ushort branchOpcode))
        {
            if (PC < 2 || !TryReadMovLiteralWordDisplacementTstBfPattern(peekBus, PC - 2, out literalOpcode, out loadOpcode, out testOpcode, out branchOpcode))
            {
                if (PC < 4 || !TryReadMovLiteralWordDisplacementTstBfPattern(peekBus, PC - 4, out literalOpcode, out loadOpcode, out testOpcode, out branchOpcode))
                {
                    if (PC < 6 || !TryReadMovLiteralWordDisplacementTstBfPattern(peekBus, PC - 6, out literalOpcode, out loadOpcode, out testOpcode, out branchOpcode))
                    {
                        return false;
                    }

                    loopPc = PC - 6;
                }
                else
                {
                    loopPc = PC - 4;
                }
            }
            else
            {
                loopPc = PC - 2;
            }
        }

        int literalRegister = (literalOpcode >> 8) & 0x0F;
        int loadSource = (loadOpcode >> 4) & 0x0F;
        int testDestination = (testOpcode >> 8) & 0x0F;
        int testSource = (testOpcode >> 4) & 0x0F;
        if (testDestination != 0 ||
            testSource != literalRegister)
        {
            return false;
        }

        int displacement = (sbyte)branchOpcode;
        uint target = loopPc + 10 + (uint)(displacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        uint mask = ReadPcRelativeLongLiteral(peekBus, loopPc, literalOpcode);
        uint address = R[loadSource] + (uint)((loadOpcode & 0x0F) * 2);
        if (!peekBus.TryPeekWord(address, out ushort wordValue))
        {
            return false;
        }

        uint loadedValue = (uint)(short)wordValue;
        if ((loadedValue & mask) == 0)
        {
            return false;
        }

        R[literalRegister] = mask;
        R[0] = loadedValue;
        SetT(false);
        PC = loopPc;
        cycles = Math.Min(maxCycles, MaxBurstCycles);
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 6;
        return true;
    }

    private static bool TryReadMovLiteralWordDisplacementTstBfPattern(
        ISh2PeekBus peekBus,
        uint pc,
        out ushort literalOpcode,
        out ushort loadOpcode,
        out ushort testOpcode,
        out ushort branchOpcode)
    {
        literalOpcode = 0;
        loadOpcode = 0;
        testOpcode = 0;
        branchOpcode = 0;
        if (!peekBus.TryPeekWord(pc, out literalOpcode) ||
            !peekBus.TryPeekWord(pc + 2, out loadOpcode) ||
            !peekBus.TryPeekWord(pc + 4, out testOpcode) ||
            !peekBus.TryPeekWord(pc + 6, out branchOpcode))
        {
            return false;
        }

        return (literalOpcode & 0xF000) == 0xD000 &&
            (loadOpcode & 0xFF00) >= 0x8500 &&
            (loadOpcode & 0xFF00) <= 0x85F0 &&
            (testOpcode & 0xF00F) == 0x2008 &&
            (branchOpcode & 0xFF00) == 0x8B00;
    }

    public bool TryFastForwardMovLiteralByteCmpEqBtPollLoop(int maxCycles, out int cycles)
    {
        const int CyclesPerIteration = 5;
        const int MaxBurstCycles = 4096;

        cycles = 0;
        if (maxCycles < CyclesPerIteration ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!TryReadMovLiteralByteCmpEqBtPattern(peekBus, loopPc, out ushort literalOpcode, out ushort loadOpcode, out ushort compareOpcode, out ushort branchOpcode))
        {
            if (PC < 2 || !TryReadMovLiteralByteCmpEqBtPattern(peekBus, PC - 2, out literalOpcode, out loadOpcode, out compareOpcode, out branchOpcode))
            {
                if (PC < 4 || !TryReadMovLiteralByteCmpEqBtPattern(peekBus, PC - 4, out literalOpcode, out loadOpcode, out compareOpcode, out branchOpcode))
                {
                    if (PC < 6 || !TryReadMovLiteralByteCmpEqBtPattern(peekBus, PC - 6, out literalOpcode, out loadOpcode, out compareOpcode, out branchOpcode))
                    {
                        return false;
                    }

                    loopPc = PC - 6;
                }
                else
                {
                    loopPc = PC - 4;
                }
            }
            else
            {
                loopPc = PC - 2;
            }
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

        uint address = ReadPcRelativeLongLiteral(peekBus, loopPc, literalOpcode);
        if (!peekBus.TryPeekByte(address, out byte byteValue))
        {
            return false;
        }

        byte immediate = (byte)compareOpcode;
        if (byteValue != immediate)
        {
            return false;
        }

        int boundedCycles = Math.Min(maxCycles, MaxBurstCycles);
        int iterations = boundedCycles / CyclesPerIteration;
        if (iterations <= 0)
        {
            return false;
        }

        R[literalRegister] = address;
        R[0] = (uint)(sbyte)byteValue;
        SetT(true);
        PC = loopPc;
        cycles = iterations * CyclesPerIteration;
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 6;
        return true;
    }

    private static bool TryReadMovLiteralByteCmpEqBtPattern(
        ISh2PeekBus peekBus,
        uint pc,
        out ushort literalOpcode,
        out ushort loadOpcode,
        out ushort compareOpcode,
        out ushort branchOpcode)
    {
        literalOpcode = 0;
        loadOpcode = 0;
        compareOpcode = 0;
        branchOpcode = 0;
        if (!peekBus.TryPeekWord(pc, out literalOpcode) ||
            !peekBus.TryPeekWord(pc + 2, out loadOpcode) ||
            !peekBus.TryPeekWord(pc + 4, out compareOpcode) ||
            !peekBus.TryPeekWord(pc + 6, out branchOpcode))
        {
            return false;
        }

        return (literalOpcode & 0xF000) == 0xD000 &&
            (loadOpcode & 0xF00F) == 0x6000 &&
            (compareOpcode & 0xFF00) == 0x8800 &&
            (branchOpcode & 0xFF00) == 0x8900;
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

    public bool TryFastForwardSdramFlagTaskletDispatcherLoop(int maxCycles, out int cycles)
    {
        const int MaxBurstCycles = 65536;
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
        if (!TryReadSdramFlagTaskletDispatcher(peekBus, loopPc, out ushort branchOpcode, out uint pointerAddress, out uint taskletPc))
        {
            if (PC < 2 ||
                !TryReadSdramFlagTaskletDispatcher(peekBus, PC - 2, out branchOpcode, out pointerAddress, out taskletPc))
            {
                return false;
            }

            loopPc = PC - 2;
        }

        if (!TryReadReadySdramFlagTasklet(peekBus, taskletPc, out ushort flagWord, out uint secondAddress, out uint currentValue, out uint compareValue))
        {
            return false;
        }

        R[0] = SignExtend16(flagWord);
        R[1] = currentValue;
        R[2] = compareValue;
        R[14] = secondAddress;
        SetT(true);
        PC = loopPc;
        cycles = Math.Min(maxCycles, MaxBurstCycles);
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 10;
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

    public bool TryFastForwardGbrBytePairEqualInterruptIdleLoop(int maxCycles, out int cycles)
    {
        const int MaxBurstCycles = 8192;
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
        if (!TryReadGbrBytePairEqualInterruptIdleLoop(peekBus, loopPc, out ushort branchOpcode, out uint routinePc, out ushort firstLoadOpcode, out ushort secondLoadOpcode))
        {
            if (PC < 2 ||
                !TryReadGbrBytePairEqualInterruptIdleLoop(peekBus, PC - 2, out branchOpcode, out routinePc, out firstLoadOpcode, out secondLoadOpcode))
            {
                if (PC < 4 ||
                    !TryReadGbrBytePairEqualInterruptIdleLoop(peekBus, PC - 4, out branchOpcode, out routinePc, out firstLoadOpcode, out secondLoadOpcode))
                {
                    if (PC < 8 ||
                        !TryReadGbrBytePairEqualInterruptIdleLoop(peekBus, PC - 8, out branchOpcode, out routinePc, out firstLoadOpcode, out secondLoadOpcode))
                    {
                        return false;
                    }

                    loopPc = PC - 8;
                }
                else
                {
                    loopPc = PC - 4;
                }
            }
            else
            {
                loopPc = PC - 2;
            }
        }

        uint firstAddress = GBR + (uint)(firstLoadOpcode & 0x00FF);
        uint secondAddress = GBR + (uint)(secondLoadOpcode & 0x00FF);
        if (!peekBus.TryPeekByte(firstAddress, out byte firstValue) ||
            !peekBus.TryPeekByte(secondAddress, out byte secondValue) ||
            firstValue != secondValue)
        {
            return false;
        }

        R[0] = SignExtend8(secondValue);
        R[1] = SignExtend8(firstValue);
        SetT(true);
        PC = loopPc;
        cycles = Math.Min(maxCycles, MaxBurstCycles);
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 8;
        return true;
    }

    public bool TryFastForwardGbrByteZeroTstBtPollLoop(int maxCycles, byte displacement, out int cycles)
    {
        const int MaxBurstCycles = 1024;
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
        if (!TryReadGbrByteZeroTstBtPollLoop(peekBus, loopPc, out ushort loadOpcode, out ushort branchOpcode))
        {
            if (PC < 2 ||
                !TryReadGbrByteZeroTstBtPollLoop(peekBus, PC - 2, out loadOpcode, out branchOpcode))
            {
                if (PC < 4 ||
                    !TryReadGbrByteZeroTstBtPollLoop(peekBus, PC - 4, out loadOpcode, out branchOpcode))
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

        if ((byte)loadOpcode != displacement)
        {
            return false;
        }

        uint address = GBR + displacement;
        if (!peekBus.TryPeekByte(address, out byte value) ||
            value != 0)
        {
            return false;
        }

        R[0] = 0;
        SetT(true);
        PC = loopPc;
        cycles = Math.Min(maxCycles, MaxBurstCycles);
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 4;
        return true;
    }

    public bool TryFastForwardLiteralByteDisplacementTstRegisterBtPollLoop(int maxCycles, out int cycles)
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

        uint loadPc = PC;
        if (!TryReadLiteralByteDisplacementTstRegisterBtPollLoop(peekBus, loadPc, out ushort baseLiteralOpcode, out ushort maskLiteralOpcode, out ushort loadOpcode, out ushort testOpcode, out ushort branchOpcode) &&
            peekBus.TryPeekWord(PC, out ushort possibleBaseLiteralOpcode) &&
            peekBus.TryPeekWord(PC + 2, out ushort possibleMaskLiteralOpcode) &&
            (possibleBaseLiteralOpcode & 0xF000) == 0xD000 &&
            (possibleMaskLiteralOpcode & 0xF000) == 0xD000 &&
            TryReadLiteralByteDisplacementTstRegisterBtPollLoop(peekBus, PC + 4, out baseLiteralOpcode, out maskLiteralOpcode, out loadOpcode, out testOpcode, out branchOpcode))
        {
            loadPc = PC + 4;
        }
        else if (!TryReadLiteralByteDisplacementTstRegisterBtPollLoop(peekBus, loadPc, out baseLiteralOpcode, out maskLiteralOpcode, out loadOpcode, out testOpcode, out branchOpcode))
        {
            if (PC < 2 ||
                !TryReadLiteralByteDisplacementTstRegisterBtPollLoop(peekBus, PC - 2, out baseLiteralOpcode, out maskLiteralOpcode, out loadOpcode, out testOpcode, out branchOpcode))
            {
                if (PC < 4 ||
                    !TryReadLiteralByteDisplacementTstRegisterBtPollLoop(peekBus, PC - 4, out baseLiteralOpcode, out maskLiteralOpcode, out loadOpcode, out testOpcode, out branchOpcode))
                {
                    if (PC < 6 ||
                        !TryReadLiteralByteDisplacementTstRegisterBtPollLoop(peekBus, PC - 6, out baseLiteralOpcode, out maskLiteralOpcode, out loadOpcode, out testOpcode, out branchOpcode))
                    {
                        if (PC < 8 ||
                            !TryReadLiteralByteDisplacementTstRegisterBtPollLoop(peekBus, PC - 8, out baseLiteralOpcode, out maskLiteralOpcode, out loadOpcode, out testOpcode, out branchOpcode))
                        {
                            return false;
                        }

                        loadPc = PC - 8;
                    }
                    else
                    {
                        loadPc = PC - 6;
                    }
                }
                else
                {
                    loadPc = PC - 4;
                }
            }
            else
            {
                loadPc = PC - 2;
            }
        }

        int baseRegister = (loadOpcode >> 4) & 0x0F;
        int maskRegister = (testOpcode >> 4) & 0x0F;
        uint baseAddress = ReadPcRelativeLongLiteral(peekBus, loadPc - 4, baseLiteralOpcode);
        uint mask = ReadPcRelativeLongLiteral(peekBus, loadPc - 2, maskLiteralOpcode);
        uint address = baseAddress + (uint)(loadOpcode & 0x0F);
        if (!peekBus.TryPeekByte(address, out byte byteValue) ||
            (((uint)(sbyte)byteValue) & mask) != 0)
        {
            return false;
        }

        R[baseRegister] = baseAddress;
        R[maskRegister] = mask;
        R[0] = SignExtend8(byteValue);
        SetT(true);
        PC = loadPc;
        cycles = Math.Min(maxCycles, MaxBurstCycles);
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loadPc + 4;
        return true;
    }

    public bool TryFastForwardByteDisplacementZeroWaitDtBfLoop(int maxCycles, out int cycles)
    {
        const int CyclesPerIteration = 6;
        const int MaxBurstCycles = CyclesPerIteration * 32_768;
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
        if (!TryReadByteDisplacementZeroWaitDtBfLoop(peekBus, loopPc, out ushort loadOpcode, out ushort exitBranchOpcode, out ushort dtOpcode, out ushort loopBranchOpcode))
        {
            if (PC < 2 ||
                !TryReadByteDisplacementZeroWaitDtBfLoop(peekBus, PC - 2, out loadOpcode, out exitBranchOpcode, out dtOpcode, out loopBranchOpcode))
            {
                if (PC < 4 ||
                    !TryReadByteDisplacementZeroWaitDtBfLoop(peekBus, PC - 4, out loadOpcode, out exitBranchOpcode, out dtOpcode, out loopBranchOpcode))
                {
                    if (PC < 6 ||
                        !TryReadByteDisplacementZeroWaitDtBfLoop(peekBus, PC - 6, out loadOpcode, out exitBranchOpcode, out dtOpcode, out loopBranchOpcode))
                    {
                        if (PC < 8 ||
                            !TryReadByteDisplacementZeroWaitDtBfLoop(peekBus, PC - 8, out loadOpcode, out exitBranchOpcode, out dtOpcode, out loopBranchOpcode))
                        {
                            return false;
                        }

                        loopPc = PC - 8;
                    }
                    else
                    {
                        loopPc = PC - 6;
                    }
                }
                else
                {
                    loopPc = PC - 4;
                }
            }
            else
            {
                loopPc = PC - 2;
            }
        }

        int baseRegister = (loadOpcode >> 4) & 0x0F;
        int countRegister = (dtOpcode >> 8) & 0x0F;
        uint address = R[baseRegister] + (uint)(loadOpcode & 0x0F);
        if (!peekBus.TryPeekByte(address, out byte value) ||
            value != 0)
        {
            return false;
        }

        uint count = R[countRegister];
        if (count == 0)
        {
            return false;
        }

        uint maxIterations = (uint)(Math.Min(maxCycles, MaxBurstCycles) / CyclesPerIteration);
        if (maxIterations == 0)
        {
            return false;
        }

        uint iterations = Math.Min(count, maxIterations);
        R[0] = 0;
        R[countRegister] = count - iterations;
        bool completedLoop = iterations == count;
        SetT(completedLoop);
        cycles = checked((int)(iterations * CyclesPerIteration));
        Cycles += cycles;
        LastOpcode = completedLoop ? loopBranchOpcode : dtOpcode;
        LastOpcodePc = completedLoop ? loopPc + 8 : loopPc + 6;
        PC = completedLoop ? loopPc + 10 : loopPc;
        return true;
    }

    public bool TryFastForwardOuterWordZeroByteDisplacementWaitDtBfLoop(int maxCycles, out int cycles)
    {
        const int CyclesPerIteration = 9;
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

        if (!TryFindByteDisplacementZeroWaitDtBfLoop(peekBus, PC, out uint byteLoopPc, out ushort loadOpcode, out ushort exitBranchOpcode, out ushort dtOpcode, out ushort loopBranchOpcode))
        {
            return false;
        }

        uint outerPc = BranchByteTarget(byteLoopPc + 8, loopBranchOpcode);
        if (outerPc >= byteLoopPc ||
            !TryReadOuterWordZeroGate(peekBus, outerPc, byteLoopPc, out ushort outerLoadOpcode, out ushort outerBranchOpcode))
        {
            return false;
        }

        int outerLoadDestination = (outerLoadOpcode >> 8) & 0x0F;
        uint outerAddress = ((outerPc + 4) & 0xFFFF_FFFCu) + (uint)((outerLoadOpcode & 0xFF) * 2);
        if (!peekBus.TryPeekWord(outerAddress, out ushort outerWord) ||
            outerWord != 0)
        {
            return false;
        }

        int baseRegister = (loadOpcode >> 4) & 0x0F;
        int countRegister = (dtOpcode >> 8) & 0x0F;
        uint address = R[baseRegister] + (uint)(loadOpcode & 0x0F);
        if (!peekBus.TryPeekByte(address, out byte value) ||
            value != 0)
        {
            return false;
        }

        uint count = R[countRegister];
        if (count == 0)
        {
            return false;
        }

        uint maxIterations = (uint)(Math.Min(maxCycles, MaxBurstCycles) / CyclesPerIteration);
        if (maxIterations == 0)
        {
            return false;
        }

        uint iterations = Math.Min(count, maxIterations);
        R[outerLoadDestination] = 0;
        R[0] = 0;
        R[countRegister] = count - iterations;
        bool completedLoop = iterations == count;
        SetT(completedLoop);
        cycles = checked((int)(iterations * CyclesPerIteration));
        Cycles += cycles;
        LastOpcode = completedLoop ? loopBranchOpcode : dtOpcode;
        LastOpcodePc = completedLoop ? byteLoopPc + 8 : byteLoopPc + 6;
        PC = completedLoop ? byteLoopPc + 10 : outerPc;
        return true;
    }

    private static bool TryReadSdramFlagTaskletDispatcher(
        ISh2PeekBus peekBus,
        uint loopPc,
        out ushort branchOpcode,
        out uint pointerAddress,
        out uint taskletPc)
    {
        branchOpcode = 0;
        pointerAddress = 0;
        taskletPc = 0;
        if (!peekBus.TryPeekWord(loopPc, out ushort literalOpcode) ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort loadOpcode) ||
            !peekBus.TryPeekWord(loopPc + 4, out ushort pushPrOpcode) ||
            !peekBus.TryPeekWord(loopPc + 6, out ushort jsrOpcode) ||
            !peekBus.TryPeekWord(loopPc + 8, out ushort jsrDelayOpcode) ||
            !peekBus.TryPeekWord(loopPc + 10, out branchOpcode) ||
            !peekBus.TryPeekWord(loopPc + 12, out ushort branchDelayOpcode))
        {
            return false;
        }

        if ((literalOpcode & 0xF000) != 0xD000 ||
            loadOpcode != 0x60E2 ||
            pushPrOpcode != 0x4F22 ||
            jsrOpcode != 0x400B ||
            jsrDelayOpcode != 0x0009 ||
            (branchOpcode & 0xF000) != 0xA000 ||
            branchDelayOpcode != 0x0009 ||
            BranchWordTarget(loopPc + 10, branchOpcode) != loopPc)
        {
            return false;
        }

        pointerAddress = ReadPcRelativeLongLiteral(peekBus, loopPc, literalOpcode);
        return TryPeekLong(peekBus, pointerAddress, out taskletPc);
    }

    private static bool TryReadGbrBytePairEqualInterruptIdleLoop(
        ISh2PeekBus peekBus,
        uint loopPc,
        out ushort branchOpcode,
        out uint routinePc,
        out ushort firstLoadOpcode,
        out ushort secondLoadOpcode)
    {
        branchOpcode = 0;
        routinePc = 0;
        firstLoadOpcode = 0;
        secondLoadOpcode = 0;
        if (!peekBus.TryPeekWord(loopPc, out ushort literalOpcode) ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort pushPrOpcode) ||
            !peekBus.TryPeekWord(loopPc + 4, out ushort jsrOpcode) ||
            !peekBus.TryPeekWord(loopPc + 6, out ushort jsrDelayOpcode) ||
            !peekBus.TryPeekWord(loopPc + 8, out branchOpcode) ||
            !peekBus.TryPeekWord(loopPc + 10, out ushort branchDelayOpcode))
        {
            return false;
        }

        if ((literalOpcode & 0xF000) != 0xD000 ||
            pushPrOpcode != 0x4F22 ||
            jsrOpcode != 0x400B ||
            jsrDelayOpcode != 0x0009 ||
            (branchOpcode & 0xF000) != 0xA000 ||
            branchDelayOpcode != 0x0009 ||
            BranchWordTarget(loopPc + 8, branchOpcode) != loopPc)
        {
            return false;
        }

        routinePc = ReadPcRelativeLongLiteral(peekBus, loopPc, literalOpcode);
        if (!peekBus.TryPeekWord(routinePc, out firstLoadOpcode) ||
            !peekBus.TryPeekWord(routinePc + 2, out ushort moveOpcode) ||
            !peekBus.TryPeekWord(routinePc + 4, out secondLoadOpcode) ||
            !peekBus.TryPeekWord(routinePc + 6, out ushort compareOpcode) ||
            !peekBus.TryPeekWord(routinePc + 8, out ushort branchTakenOpcode))
        {
            return false;
        }

        if ((firstLoadOpcode & 0xFF00) != 0xC400 ||
            moveOpcode != 0x6103 ||
            (secondLoadOpcode & 0xFF00) != 0xC400 ||
            compareOpcode != 0x3100 ||
            (branchTakenOpcode & 0xFF00) != 0x8900)
        {
            return false;
        }

        uint returnPc = BranchByteTarget(routinePc + 8, branchTakenOpcode);
        return peekBus.TryPeekWord(returnPc, out ushort rtsOpcode) &&
            peekBus.TryPeekWord(returnPc + 2, out ushort restorePrOpcode) &&
            rtsOpcode == 0x000B &&
            restorePrOpcode == 0x4F26;
    }

    private static bool TryReadGbrByteZeroTstBtPollLoop(ISh2PeekBus peekBus, uint loopPc, out ushort loadOpcode, out ushort branchOpcode)
    {
        loadOpcode = 0;
        branchOpcode = 0;
        if (!peekBus.TryPeekWord(loopPc, out loadOpcode) ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort testOpcode) ||
            !peekBus.TryPeekWord(loopPc + 4, out branchOpcode))
        {
            return false;
        }

        if ((loadOpcode & 0xFF00) != 0xC400 ||
            testOpcode != 0x2008 ||
            (branchOpcode & 0xFF00) != 0x8900)
        {
            return false;
        }

        return BranchByteTarget(loopPc + 4, branchOpcode) == loopPc;
    }

    private static bool TryReadByteDisplacementZeroWaitDtBfLoop(
        ISh2PeekBus peekBus,
        uint loopPc,
        out ushort loadOpcode,
        out ushort exitBranchOpcode,
        out ushort dtOpcode,
        out ushort loopBranchOpcode)
    {
        loadOpcode = 0;
        exitBranchOpcode = 0;
        dtOpcode = 0;
        loopBranchOpcode = 0;
        if (!peekBus.TryPeekWord(loopPc, out loadOpcode) ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort testOpcode) ||
            !peekBus.TryPeekWord(loopPc + 4, out exitBranchOpcode) ||
            !peekBus.TryPeekWord(loopPc + 6, out dtOpcode) ||
            !peekBus.TryPeekWord(loopPc + 8, out loopBranchOpcode))
        {
            return false;
        }

        if ((loadOpcode & 0xFF00) < 0x8400 ||
            (loadOpcode & 0xFF00) > 0x84F0 ||
            testOpcode != 0x2008 ||
            (exitBranchOpcode & 0xFF00) != 0x8B00 ||
            (dtOpcode & 0xF0FF) != 0x4010 ||
            (loopBranchOpcode & 0xFF00) != 0x8B00)
        {
            return false;
        }

        int exitDisplacement = (sbyte)exitBranchOpcode;
        int loopDisplacement = (sbyte)loopBranchOpcode;
        uint exitTarget = loopPc + 8 + (uint)(exitDisplacement * 2);
        uint loopTarget = loopPc + 12 + (uint)(loopDisplacement * 2);
        return exitTarget != loopPc &&
            loopTarget == loopPc;
    }

    private static bool TryFindByteDisplacementZeroWaitDtBfLoop(
        ISh2PeekBus peekBus,
        uint pc,
        out uint loopPc,
        out ushort loadOpcode,
        out ushort exitBranchOpcode,
        out ushort dtOpcode,
        out ushort loopBranchOpcode)
    {
        ReadOnlySpan<int> offsets = [0, -2, -4, -6, -8];
        foreach (int offset in offsets)
        {
            if (offset < 0 && pc < (uint)-offset)
            {
                continue;
            }

            uint candidate = (uint)(pc + offset);
            if (TryReadByteDisplacementZeroWaitDtBfLoopWithoutLoopTarget(peekBus, candidate, out loadOpcode, out exitBranchOpcode, out dtOpcode, out loopBranchOpcode))
            {
                loopPc = candidate;
                return true;
            }
        }

        loopPc = 0;
        loadOpcode = 0;
        exitBranchOpcode = 0;
        dtOpcode = 0;
        loopBranchOpcode = 0;
        return false;
    }

    private static bool TryReadByteDisplacementZeroWaitDtBfLoopWithoutLoopTarget(
        ISh2PeekBus peekBus,
        uint loopPc,
        out ushort loadOpcode,
        out ushort exitBranchOpcode,
        out ushort dtOpcode,
        out ushort loopBranchOpcode)
    {
        loadOpcode = 0;
        exitBranchOpcode = 0;
        dtOpcode = 0;
        loopBranchOpcode = 0;
        if (!peekBus.TryPeekWord(loopPc, out loadOpcode) ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort testOpcode) ||
            !peekBus.TryPeekWord(loopPc + 4, out exitBranchOpcode) ||
            !peekBus.TryPeekWord(loopPc + 6, out dtOpcode) ||
            !peekBus.TryPeekWord(loopPc + 8, out loopBranchOpcode))
        {
            return false;
        }

        if ((loadOpcode & 0xFF00) < 0x8400 ||
            (loadOpcode & 0xFF00) > 0x84F0 ||
            testOpcode != 0x2008 ||
            (exitBranchOpcode & 0xFF00) != 0x8B00 ||
            (dtOpcode & 0xF0FF) != 0x4010 ||
            (loopBranchOpcode & 0xFF00) != 0x8B00)
        {
            return false;
        }

        uint exitTarget = BranchByteTarget(loopPc + 4, exitBranchOpcode);
        return exitTarget != loopPc;
    }

    private static bool TryReadOuterWordZeroGate(ISh2PeekBus peekBus, uint outerPc, uint targetPc, out ushort loadOpcode, out ushort branchOpcode)
    {
        loadOpcode = 0;
        branchOpcode = 0;
        if (!peekBus.TryPeekWord(outerPc, out loadOpcode) ||
            !peekBus.TryPeekWord(outerPc + 2, out ushort testOpcode) ||
            !peekBus.TryPeekWord(outerPc + 4, out branchOpcode))
        {
            return false;
        }

        return (loadOpcode & 0xF000) == 0x9000 &&
            (testOpcode & 0xF00F) == 0x2008 &&
            ((testOpcode >> 8) & 0x0F) == ((loadOpcode >> 8) & 0x0F) &&
            ((testOpcode >> 4) & 0x0F) == ((loadOpcode >> 8) & 0x0F) &&
            (branchOpcode & 0xFF00) == 0x8900 &&
            BranchByteTarget(outerPc + 4, branchOpcode) == targetPc;
    }

    private static bool TryReadLiteralByteDisplacementTstRegisterBtPollLoop(
        ISh2PeekBus peekBus,
        uint loadPc,
        out ushort baseLiteralOpcode,
        out ushort maskLiteralOpcode,
        out ushort loadOpcode,
        out ushort testOpcode,
        out ushort branchOpcode)
    {
        baseLiteralOpcode = 0;
        maskLiteralOpcode = 0;
        loadOpcode = 0;
        testOpcode = 0;
        branchOpcode = 0;
        if (loadPc < 4 ||
            !peekBus.TryPeekWord(loadPc - 4, out baseLiteralOpcode) ||
            !peekBus.TryPeekWord(loadPc - 2, out maskLiteralOpcode) ||
            !peekBus.TryPeekWord(loadPc, out loadOpcode) ||
            !peekBus.TryPeekWord(loadPc + 2, out testOpcode) ||
            !peekBus.TryPeekWord(loadPc + 4, out branchOpcode))
        {
            return false;
        }

        if ((baseLiteralOpcode & 0xF000) != 0xD000 ||
            (maskLiteralOpcode & 0xF000) != 0xD000 ||
            (loadOpcode & 0xFF00) < 0x8400 ||
            (loadOpcode & 0xFF00) > 0x84F0 ||
            (testOpcode & 0xF00F) != 0x2008 ||
            (branchOpcode & 0xFF00) != 0x8900)
        {
            return false;
        }

        int baseRegister = (loadOpcode >> 4) & 0x0F;
        int maskRegister = (testOpcode >> 4) & 0x0F;
        if (((baseLiteralOpcode >> 8) & 0x0F) != baseRegister ||
            ((maskLiteralOpcode >> 8) & 0x0F) != maskRegister ||
            (testOpcode >> 8 & 0x0F) != 0 ||
            maskRegister == 0 ||
            BranchByteTarget(loadPc + 4, branchOpcode) != loadPc)
        {
            return false;
        }

        return true;
    }

    private static bool TryReadReadySdramFlagTasklet(
        ISh2PeekBus peekBus,
        uint pc,
        out ushort flagWord,
        out uint secondAddress,
        out uint currentValue,
        out uint compareValue)
    {
        flagWord = 0;
        secondAddress = 0;
        currentValue = 0;
        compareValue = 0;
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
        if (!peekBus.TryPeekWord(firstAddress, out flagWord))
        {
            return false;
        }

        byte mask = (byte)testOpcode;
        if (((byte)flagWord & mask) == 0)
        {
            return false;
        }

        secondAddress = ReadPcRelativeLongLiteral(peekBus, pc + 8, secondLiteralOpcode);
        compareValue = ReadPcRelativeLongLiteral(peekBus, pc + 12, compareLiteralOpcode);
        return TryPeekLong(peekBus, secondAddress, out currentValue) && currentValue == compareValue;
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

    public bool TryFastForwardStableWordPairCmpEqBtPollLoop(int maxCycles, out int cycles)
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

        if (!TryFindThreeWordPollLoop(peekBus, [0, -2, -4], out uint loopPc, out ushort loadOpcode, out ushort compareOpcode, out ushort branchOpcode) ||
            (loadOpcode & 0xF00F) != 0x6001 ||
            (compareOpcode & 0xF00F) != 0x3000 ||
            (branchOpcode & 0xFF00) != 0x8900)
        {
            return false;
        }

        int loadDestination = (loadOpcode >> 8) & 0x0F;
        int loadSource = (loadOpcode >> 4) & 0x0F;
        int compareLeft = (compareOpcode >> 8) & 0x0F;
        int compareRight = (compareOpcode >> 4) & 0x0F;
        if (compareLeft != loadDestination)
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

        uint extendedValue = (uint)(int)(short)wordValue;
        if (extendedValue != R[compareRight])
        {
            return false;
        }

        R[loadDestination] = extendedValue;
        SetT(true);
        PC = loopPc;
        cycles = maxCycles;
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 4;
        return true;
    }

    public bool TryFastForwardLongRegisterCmpEqBtPollLoop(int maxCycles, out int cycles)
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

        if (!TryFindThreeWordPollLoop(peekBus, [0, -2, -4], out uint loopPc, out ushort loadOpcode, out ushort compareOpcode, out ushort branchOpcode) ||
            (loadOpcode & 0xF00F) != 0x6002 ||
            (compareOpcode & 0xF00F) != 0x3000 ||
            (branchOpcode & 0xFF00) != 0x8900)
        {
            return false;
        }

        int loadDestination = (loadOpcode >> 8) & 0x0F;
        int loadSource = (loadOpcode >> 4) & 0x0F;
        int compareLeft = (compareOpcode >> 8) & 0x0F;
        int compareRight = (compareOpcode >> 4) & 0x0F;
        int compareRegister;
        if (compareLeft == loadDestination)
        {
            compareRegister = compareRight;
        }
        else if (compareRight == loadDestination)
        {
            compareRegister = compareLeft;
        }
        else
        {
            return false;
        }

        if (!TryPeekLong(peekBus, R[loadSource], out uint longValue) ||
            longValue != R[compareRegister])
        {
            return false;
        }

        R[loadDestination] = longValue;
        SetT(true);
        PC = loopPc;
        cycles = maxCycles;
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 4;
        return true;
    }

    public bool TryFastForwardWordCmpEqBfPollLoop(int maxCycles, out int cycles)
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

        if (!TryFindThreeWordPollLoop(peekBus, [0, -2, -4], out uint loopPc, out ushort loadOpcode, out ushort compareOpcode, out ushort branchOpcode) ||
            (loadOpcode & 0xF00F) != 0x6001 ||
            (compareOpcode & 0xFF00) != 0x8800 ||
            (branchOpcode & 0xFF00) != 0x8B00 ||
            BranchByteTarget(loopPc + 4, branchOpcode) != loopPc)
        {
            return false;
        }

        int loadDestination = (loadOpcode >> 8) & 0x0F;
        int loadSource = (loadOpcode >> 4) & 0x0F;
        if (loadDestination != 0 ||
            !peekBus.TryPeekWord(R[loadSource], out ushort wordValue))
        {
            return false;
        }

        byte immediate = (byte)compareOpcode;
        bool equal = (uint)(short)wordValue == (uint)(sbyte)immediate;
        if (equal)
        {
            return false;
        }

        R[loadDestination] = (uint)(short)wordValue;
        SetT(false);
        PC = loopPc;
        cycles = maxCycles;
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 4;
        return true;
    }

    public bool TryFastForwardWordLoadCmpPzBtIdleLoop(int maxCycles, out int cycles)
    {
        const int CyclesPerIteration = 3;
        cycles = 0;
        if (maxCycles < CyclesPerIteration ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        if (!TryFindThreeWordPollLoop(peekBus, [0, -2, -4], out uint loopPc, out ushort loadOpcode, out ushort compareOpcode, out ushort branchOpcode) ||
            (loadOpcode & 0xF00F) != 0x6001 ||
            (compareOpcode & 0xF0FF) != 0x4011 ||
            (branchOpcode & 0xFF00) != 0x8900 ||
            BranchByteTarget(loopPc + 4, branchOpcode) != loopPc)
        {
            return false;
        }

        int loadDestination = (loadOpcode >> 8) & 0x0F;
        int loadSource = (loadOpcode >> 4) & 0x0F;
        int compareRegister = (compareOpcode >> 8) & 0x0F;
        if ((R[compareRegister] & 0x8000_0000u) != 0 ||
            !peekBus.TryPeekWord(R[loadSource], out ushort wordValue))
        {
            return false;
        }

        R[loadDestination] = (uint)(short)wordValue;
        SetT(true);
        PC = loopPc;
        cycles = maxCycles - (maxCycles % CyclesPerIteration);
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 4;
        return true;
    }

    public bool TryFastForwardWordTstBtPollLoop(int maxCycles, out int cycles)
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

        if (!TryFindThreeWordPollLoop(peekBus, [0, -2, -4], out uint loopPc, out ushort loadOpcode, out ushort testOpcode, out ushort branchOpcode) ||
            (loadOpcode & 0xF00F) != 0x6001 ||
            (testOpcode & 0xF00F) != 0x2008 ||
            (branchOpcode & 0xFF00) != 0x8900 ||
            BranchByteTarget(loopPc + 4, branchOpcode) != loopPc)
        {
            return false;
        }

        int loadDestination = (loadOpcode >> 8) & 0x0F;
        int loadSource = (loadOpcode >> 4) & 0x0F;
        int testSource = (testOpcode >> 4) & 0x0F;
        int testDestination = (testOpcode >> 8) & 0x0F;
        if (testSource != loadDestination &&
            testDestination != loadDestination)
        {
            return false;
        }

        int maskRegister = testSource == loadDestination ? testDestination : testSource;
        if (!peekBus.TryPeekWord(R[loadSource], out ushort wordValue))
        {
            return false;
        }

        uint loadedValue = (uint)(short)wordValue;
        uint maskValue = maskRegister == loadDestination ? loadedValue : R[maskRegister];
        if ((loadedValue & maskValue) != 0)
        {
            return false;
        }

        R[loadDestination] = loadedValue;
        SetT(true);
        PC = loopPc;
        cycles = maxCycles;
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 4;
        return true;
    }

    public bool TryFastForwardMovLiteralWordLoadCmpPzBtIdleLoop(int maxCycles, out int cycles)
    {
        const int CyclesPerIteration = 4;
        const int MaxBurstCycles = CyclesPerIteration;
        cycles = 0;
        if (maxCycles < CyclesPerIteration ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        if (!TryFindFourWordPollLoop(
                peekBus,
                [0, -2, -4, -6],
                out uint loopPc,
                out ushort literalOpcode,
                out ushort loadOpcode,
                out ushort compareOpcode,
                out ushort branchOpcode) ||
            (literalOpcode & 0xF000) != 0xD000 ||
            (loadOpcode & 0xF00F) != 0x6001 ||
            (compareOpcode & 0xF0FF) != 0x4011 ||
            (branchOpcode & 0xFF00) != 0x8900 ||
            BranchByteTarget(loopPc + 6, branchOpcode) != loopPc)
        {
            return false;
        }

        int literalDestination = (literalOpcode >> 8) & 0x0F;
        int loadDestination = (loadOpcode >> 8) & 0x0F;
        int loadSource = (loadOpcode >> 4) & 0x0F;
        int compareRegister = (compareOpcode >> 8) & 0x0F;
        if (literalDestination != loadSource ||
            loadDestination != loadSource ||
            compareRegister != loadDestination)
        {
            return false;
        }

        uint address = ReadPcRelativeLongLiteral(peekBus, loopPc, literalOpcode);
        if (!peekBus.TryPeekWord(address, out ushort wordValue))
        {
            return false;
        }

        uint extendedValue = (uint)(int)(short)wordValue;
        if ((extendedValue & 0x8000_0000u) != 0)
        {
            return false;
        }

        R[literalDestination] = address;
        R[loadDestination] = extendedValue;
        SetT(true);
        PC = loopPc;
        int burstBudget = Math.Min(maxCycles, MaxBurstCycles);
        cycles = burstBudget - (burstBudget % CyclesPerIteration);
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 6;
        return true;
    }

    public bool TryFastForwardWordDisplacementTstBtPollLoop(int maxCycles, out int cycles)
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
        ushort loadOpcode = 0;
        ushort testOpcode = 0;
        ushort branchOpcode = 0;
        bool found = false;
        ReadOnlySpan<int> offsets = [0, -2, -4];
        foreach (int offset in offsets)
        {
            if (offset < 0 && PC < (uint)-offset)
            {
                continue;
            }

            uint candidate = (uint)(PC + offset);
            if (peekBus.TryPeekWord(candidate, out ushort candidateLoadOpcode) &&
                peekBus.TryPeekWord(candidate + 2, out ushort candidateTestOpcode) &&
                peekBus.TryPeekWord(candidate + 4, out ushort candidateBranchOpcode) &&
                (candidateLoadOpcode & 0xFF00) >= 0x8500 &&
                (candidateLoadOpcode & 0xFF00) <= 0x85F0 &&
                (candidateTestOpcode & 0xF00F) == 0x2008 &&
                (candidateBranchOpcode & 0xFF00) == 0x8900)
            {
                loopPc = candidate;
                loadOpcode = candidateLoadOpcode;
                testOpcode = candidateTestOpcode;
                branchOpcode = candidateBranchOpcode;
                found = true;
                break;
            }
        }

        if (!found)
        {
            return false;
        }

        if ((loadOpcode & 0xFF00) < 0x8500 ||
            (loadOpcode & 0xFF00) > 0x85F0 ||
            (testOpcode & 0xF00F) != 0x2008 ||
            (branchOpcode & 0xFF00) != 0x8900)
        {
            return false;
        }

        int loadSource = (loadOpcode >> 4) & 0x0F;
        int testLeft = (testOpcode >> 8) & 0x0F;
        int testRight = (testOpcode >> 4) & 0x0F;
        if (testLeft != 0 && testRight != 0)
        {
            return false;
        }

        int maskRegister = testLeft == 0 ? testRight : testLeft;
        int displacement = (sbyte)branchOpcode;
        uint target = loopPc + 8 + (uint)(displacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        uint address = R[loadSource] + (uint)((loadOpcode & 0x0F) * 2);
        if (!peekBus.TryPeekWord(address, out ushort wordValue))
        {
            return false;
        }

        uint loadedValue = (uint)(int)(short)wordValue;
        uint maskValue = maskRegister == 0 ? loadedValue : R[maskRegister];
        if ((loadedValue & maskValue) != 0)
        {
            return false;
        }

        R[0] = loadedValue;
        SetT(true);
        PC = loopPc;
        cycles = maxCycles;
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 4;
        return true;
    }

    public bool TryFastForwardLongTstBtPaddedPollLoop(int maxCycles, out int cycles)
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
        if (!TryReadLongTstBtPaddedPattern(peekBus, loopPc, out ushort loadOpcode, out ushort branchOpcode))
        {
            if (PC < 2 || !TryReadLongTstBtPaddedPattern(peekBus, PC - 2, out loadOpcode, out branchOpcode))
            {
                if (PC < 4 || !TryReadLongTstBtPaddedPattern(peekBus, PC - 4, out loadOpcode, out branchOpcode))
                {
                    if (PC < 6 || !TryReadLongTstBtPaddedPattern(peekBus, PC - 6, out loadOpcode, out branchOpcode))
                    {
                        return false;
                    }

                    loopPc = PC - 6;
                }
                else
                {
                    loopPc = PC - 4;
                }
            }
            else
            {
                loopPc = PC - 2;
            }
        }

        int loadDestination = (loadOpcode >> 8) & 0x0F;
        int loadSource = (loadOpcode >> 4) & 0x0F;
        if (loadDestination != 0)
        {
            return false;
        }

        int displacement = (sbyte)branchOpcode;
        uint target = loopPc + 10 + (uint)(displacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        if (!TryPeekLong(peekBus, R[loadSource], out uint longValue) ||
            longValue != 0)
        {
            return false;
        }

        R[0] = 0;
        SetT(true);
        PC = loopPc;
        cycles = Math.Min(maxCycles, MaxBurstCycles);
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 6;
        return true;
    }

    public bool TryFastForwardLongMaskedChangeBtSDelayPollLoop(int maxCycles, out int cycles)
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
        if (!TryReadLongMaskedChangeBtSDelayPollPattern(peekBus, loopPc, out ushort compareOpcode, out ushort loadOpcode, out ushort branchOpcode, out ushort andOpcode))
        {
            if (PC < 2 || !TryReadLongMaskedChangeBtSDelayPollPattern(peekBus, PC - 2, out compareOpcode, out loadOpcode, out branchOpcode, out andOpcode))
            {
                if (PC < 4 || !TryReadLongMaskedChangeBtSDelayPollPattern(peekBus, PC - 4, out compareOpcode, out loadOpcode, out branchOpcode, out andOpcode))
                {
                    if (PC < 6 || !TryReadLongMaskedChangeBtSDelayPollPattern(peekBus, PC - 6, out compareOpcode, out loadOpcode, out branchOpcode, out andOpcode))
                    {
                        return false;
                    }

                    loopPc = PC - 6;
                }
                else
                {
                    loopPc = PC - 4;
                }
            }
            else
            {
                loopPc = PC - 2;
            }
        }

        int compareLeft = (compareOpcode >> 8) & 0x0F;
        int compareRight = (compareOpcode >> 4) & 0x0F;
        int loadDestination = (loadOpcode >> 8) & 0x0F;
        int loadSource = (loadOpcode >> 4) & 0x0F;
        int andDestination = (andOpcode >> 8) & 0x0F;
        int andSource = (andOpcode >> 4) & 0x0F;
        if (loadDestination != compareRight ||
            andDestination != loadDestination ||
            !TryPeekLong(peekBus, R[loadSource], out uint longValue))
        {
            return false;
        }

        uint maskedValue = longValue & R[andSource];
        if (maskedValue != R[compareLeft])
        {
            return false;
        }

        R[loadDestination] = maskedValue;
        SetT(true);
        PC = loopPc;
        cycles = Math.Min(maxCycles, MaxBurstCycles);
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 4;
        return true;
    }

    public bool TryFastForwardGbrLongMaskedOrCompareBfPollLoop(int maxCycles, out int cycles)
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
        if (!TryReadGbrLongMaskedOrCompareBfPollPattern(peekBus, loopPc, out ushort loadOpcode, out ushort copyOpcode, out ushort andOpcode, out ushort orOpcode, out ushort compareOpcode, out ushort branchOpcode))
        {
            if (PC < 2 || !TryReadGbrLongMaskedOrCompareBfPollPattern(peekBus, PC - 2, out loadOpcode, out copyOpcode, out andOpcode, out orOpcode, out compareOpcode, out branchOpcode))
            {
                if (PC < 4 || !TryReadGbrLongMaskedOrCompareBfPollPattern(peekBus, PC - 4, out loadOpcode, out copyOpcode, out andOpcode, out orOpcode, out compareOpcode, out branchOpcode))
                {
                    if (PC < 6 || !TryReadGbrLongMaskedOrCompareBfPollPattern(peekBus, PC - 6, out loadOpcode, out copyOpcode, out andOpcode, out orOpcode, out compareOpcode, out branchOpcode))
                    {
                        if (PC < 8 || !TryReadGbrLongMaskedOrCompareBfPollPattern(peekBus, PC - 8, out loadOpcode, out copyOpcode, out andOpcode, out orOpcode, out compareOpcode, out branchOpcode))
                        {
                            if (PC < 10 || !TryReadGbrLongMaskedOrCompareBfPollPattern(peekBus, PC - 10, out loadOpcode, out copyOpcode, out andOpcode, out orOpcode, out compareOpcode, out branchOpcode))
                            {
                                return false;
                            }

                            loopPc = PC - 10;
                        }
                        else
                        {
                            loopPc = PC - 8;
                        }
                    }
                    else
                    {
                        loopPc = PC - 6;
                    }
                }
                else
                {
                    loopPc = PC - 4;
                }
            }
            else
            {
                loopPc = PC - 2;
            }
        }

        int copyDestination = (copyOpcode >> 8) & 0x0F;
        int copySource = (copyOpcode >> 4) & 0x0F;
        int andDestination = (andOpcode >> 8) & 0x0F;
        int andSource = (andOpcode >> 4) & 0x0F;
        int compareLeft = (compareOpcode >> 8) & 0x0F;
        int compareRight = (compareOpcode >> 4) & 0x0F;
        if (copySource != 0 ||
            andDestination != 0 ||
            compareLeft != 0)
        {
            return false;
        }

        uint address = GBR + (uint)((loadOpcode & 0xFF) * 4);
        if (!TryPeekLong(peekBus, address, out uint rawValue))
        {
            return false;
        }

        uint computedValue = (rawValue & R[andSource]) | (byte)orOpcode;
        if (computedValue == R[compareRight])
        {
            return false;
        }

        R[0] = computedValue;
        R[copyDestination] = rawValue;
        SetT(false);
        PC = loopPc;
        cycles = Math.Min(maxCycles, MaxBurstCycles);
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 10;
        return true;
    }

    public bool TryFastForwardWordIncrementGbrZeroBtPollLoop(int maxCycles, Func<uint, ushort, bool> writeWord, out int cycles)
    {
        const int MaxBurstCycles = 4096;
        const int CyclesPerIteration = 12;

        cycles = 0;
        if (maxCycles < CyclesPerIteration ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!TryReadWordIncrementGbrZeroBtPollPattern(peekBus, loopPc, out ushort loadOpcode, out ushort addOpcode, out ushort storeOpcode, out ushort guardLoadOpcode, out ushort compareOpcode, out ushort branchOpcode))
        {
            if (PC < 2 || !TryReadWordIncrementGbrZeroBtPollPattern(peekBus, PC - 2, out loadOpcode, out addOpcode, out storeOpcode, out guardLoadOpcode, out compareOpcode, out branchOpcode))
            {
                if (PC < 4 || !TryReadWordIncrementGbrZeroBtPollPattern(peekBus, PC - 4, out loadOpcode, out addOpcode, out storeOpcode, out guardLoadOpcode, out compareOpcode, out branchOpcode))
                {
                    if (PC < 6 || !TryReadWordIncrementGbrZeroBtPollPattern(peekBus, PC - 6, out loadOpcode, out addOpcode, out storeOpcode, out guardLoadOpcode, out compareOpcode, out branchOpcode))
                    {
                        if (PC < 8 || !TryReadWordIncrementGbrZeroBtPollPattern(peekBus, PC - 8, out loadOpcode, out addOpcode, out storeOpcode, out guardLoadOpcode, out compareOpcode, out branchOpcode))
                        {
                            if (PC < 10 || !TryReadWordIncrementGbrZeroBtPollPattern(peekBus, PC - 10, out loadOpcode, out addOpcode, out storeOpcode, out guardLoadOpcode, out compareOpcode, out branchOpcode))
                            {
                                return false;
                            }

                            loopPc = PC - 10;
                        }
                        else
                        {
                            loopPc = PC - 8;
                        }
                    }
                    else
                    {
                        loopPc = PC - 6;
                    }
                }
                else
                {
                    loopPc = PC - 4;
                }
            }
            else
            {
                loopPc = PC - 2;
            }
        }

        int loadDestination = (loadOpcode >> 8) & 0x0F;
        int loadSource = (loadOpcode >> 4) & 0x0F;
        int addDestination = (addOpcode >> 8) & 0x0F;
        int storeDestination = (storeOpcode >> 8) & 0x0F;
        int storeSource = (storeOpcode >> 4) & 0x0F;
        if (loadDestination != addDestination ||
            storeSource != addDestination ||
            storeDestination != loadSource ||
            loadDestination != 0 ||
            (compareOpcode & 0x00FF) != 0)
        {
            return false;
        }

        uint guardAddress = GBR + (uint)((guardLoadOpcode & 0x00FF) * 2);
        if (!peekBus.TryPeekWord(guardAddress, out ushort guardValue) ||
            guardValue != 0 ||
            !peekBus.TryPeekWord(R[loadSource], out ushort counter))
        {
            return false;
        }

        int boundedCycles = Math.Min(maxCycles, MaxBurstCycles);
        int iterations = Math.Max(1, boundedCycles / CyclesPerIteration);
        ushort newCounter = (ushort)(counter + iterations);
        if (!writeWord(R[loadSource], newCounter))
        {
            return false;
        }

        R[loadDestination] = 0;
        SetT(true);
        PC = loopPc;
        cycles = iterations * CyclesPerIteration;
        Cycles += cycles;
        LastOpcode = branchOpcode;
        LastOpcodePc = loopPc + 10;
        return true;
    }

    private static bool TryReadLongTstBtPaddedPattern(ISh2PeekBus peekBus, uint pc, out ushort loadOpcode, out ushort branchOpcode)
    {
        loadOpcode = 0;
        branchOpcode = 0;
        if (!peekBus.TryPeekWord(pc, out loadOpcode) ||
            !peekBus.TryPeekWord(pc + 2, out ushort nopOpcode) ||
            !peekBus.TryPeekWord(pc + 4, out ushort testOpcode) ||
            !peekBus.TryPeekWord(pc + 6, out branchOpcode))
        {
            return false;
        }

        return (loadOpcode & 0xF00F) == 0x6002 &&
            nopOpcode == 0x0009 &&
            (testOpcode & 0xF00F) == 0x2008 &&
            (branchOpcode & 0xFF00) == 0x8900;
    }

    private static bool TryReadLongMaskedChangeBtSDelayPollPattern(
        ISh2PeekBus peekBus,
        uint pc,
        out ushort compareOpcode,
        out ushort loadOpcode,
        out ushort branchOpcode,
        out ushort andOpcode)
    {
        compareOpcode = 0;
        loadOpcode = 0;
        branchOpcode = 0;
        andOpcode = 0;
        if (!peekBus.TryPeekWord(pc, out compareOpcode) ||
            !peekBus.TryPeekWord(pc + 2, out loadOpcode) ||
            !peekBus.TryPeekWord(pc + 4, out branchOpcode) ||
            !peekBus.TryPeekWord(pc + 6, out andOpcode))
        {
            return false;
        }

        return (compareOpcode & 0xF00F) == 0x3000 &&
            (loadOpcode & 0xF00F) == 0x6002 &&
            (branchOpcode & 0xFF00) == 0x8D00 &&
            (andOpcode & 0xF00F) == 0x2009 &&
            BranchByteTarget(pc + 4, branchOpcode) == pc;
    }

    private static bool TryReadWordIncrementGbrZeroBtPollPattern(
        ISh2PeekBus peekBus,
        uint pc,
        out ushort loadOpcode,
        out ushort addOpcode,
        out ushort storeOpcode,
        out ushort guardLoadOpcode,
        out ushort compareOpcode,
        out ushort branchOpcode)
    {
        loadOpcode = 0;
        addOpcode = 0;
        storeOpcode = 0;
        guardLoadOpcode = 0;
        compareOpcode = 0;
        branchOpcode = 0;
        if (!peekBus.TryPeekWord(pc, out loadOpcode) ||
            !peekBus.TryPeekWord(pc + 2, out addOpcode) ||
            !peekBus.TryPeekWord(pc + 4, out storeOpcode) ||
            !peekBus.TryPeekWord(pc + 6, out guardLoadOpcode) ||
            !peekBus.TryPeekWord(pc + 8, out compareOpcode) ||
            !peekBus.TryPeekWord(pc + 10, out branchOpcode))
        {
            return false;
        }

        return (loadOpcode & 0xF00F) == 0x6001 &&
            (addOpcode & 0xF0FF) == 0x7001 &&
            (storeOpcode & 0xF00F) == 0x2001 &&
            (guardLoadOpcode & 0xFF00) == 0xC500 &&
            (compareOpcode & 0xFF00) == 0x8800 &&
            (branchOpcode & 0xFF00) == 0x8900 &&
            BranchByteTarget(pc + 10, branchOpcode) == pc;
    }

    private static bool TryReadGbrLongMaskedOrCompareBfPollPattern(
        ISh2PeekBus peekBus,
        uint pc,
        out ushort loadOpcode,
        out ushort copyOpcode,
        out ushort andOpcode,
        out ushort orOpcode,
        out ushort compareOpcode,
        out ushort branchOpcode)
    {
        loadOpcode = 0;
        copyOpcode = 0;
        andOpcode = 0;
        orOpcode = 0;
        compareOpcode = 0;
        branchOpcode = 0;
        if (!peekBus.TryPeekWord(pc, out loadOpcode) ||
            !peekBus.TryPeekWord(pc + 2, out copyOpcode) ||
            !peekBus.TryPeekWord(pc + 4, out andOpcode) ||
            !peekBus.TryPeekWord(pc + 6, out orOpcode) ||
            !peekBus.TryPeekWord(pc + 8, out compareOpcode) ||
            !peekBus.TryPeekWord(pc + 10, out branchOpcode))
        {
            return false;
        }

        return (loadOpcode & 0xFF00) == 0xC600 &&
            (copyOpcode & 0xF00F) == 0x6003 &&
            (andOpcode & 0xF00F) == 0x2009 &&
            (orOpcode & 0xFF00) == 0xCB00 &&
            (compareOpcode & 0xF00F) == 0x3000 &&
            (branchOpcode & 0xFF00) == 0x8B00 &&
            BranchByteTarget(pc + 10, branchOpcode) == pc;
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

        if (!TryFindThreeWordPollLoop(peekBus, [0, -2, -4], out uint loopPc, out ushort loadOpcode, out ushort testOpcode, out ushort branchOpcode) ||
            (loadOpcode & 0xF00F) != 0x6001 ||
            (testOpcode & 0xF00F) != 0x2008 ||
            (branchOpcode & 0xFF00) != 0x8B00)
        {
            return false;
        }

        int loadDestination = (loadOpcode >> 8) & 0x0F;
        int loadSource = (loadOpcode >> 4) & 0x0F;
        int testLeft = (testOpcode >> 8) & 0x0F;
        int testRight = (testOpcode >> 4) & 0x0F;
        if (testLeft != loadDestination &&
            testRight != loadDestination)
        {
            return false;
        }

        int maskRegister = testLeft == loadDestination ? testRight : testLeft;

        if (BranchByteTarget(loopPc + 4, branchOpcode) != loopPc)
        {
            return false;
        }

        if (!peekBus.TryPeekWord(R[loadSource], out ushort wordValue))
        {
            return false;
        }

        uint loadedValue = (uint)(short)wordValue;
        uint maskValue = maskRegister == loadDestination ? loadedValue : R[maskRegister];
        if ((loadedValue & maskValue) == 0)
        {
            return false;
        }

        R[loadDestination] = loadedValue;
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
        ushort loadOpcode = 0;
        ushort testOpcode = 0;
        ushort branchOpcode = 0;
        bool found = false;
        ReadOnlySpan<int> offsets = [0, -2, -4];
        foreach (int offset in offsets)
        {
            if (offset < 0 && PC < (uint)-offset)
            {
                continue;
            }

            uint candidate = (uint)(PC + offset);
            if (peekBus.TryPeekWord(candidate, out ushort candidateLoadOpcode) &&
                peekBus.TryPeekWord(candidate + 2, out ushort candidateTestOpcode) &&
                peekBus.TryPeekWord(candidate + 4, out ushort candidateBranchOpcode) &&
                (candidateLoadOpcode & 0xF00F) == 0x6000 &&
                (candidateTestOpcode & 0xF00F) == 0x2008 &&
                (candidateBranchOpcode & 0xFF00) == 0x8B00)
            {
                loopPc = candidate;
                loadOpcode = candidateLoadOpcode;
                testOpcode = candidateTestOpcode;
                branchOpcode = candidateBranchOpcode;
                found = true;
                break;
            }
        }

        if (!found)
        {
            return false;
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

    public bool TryFastForwardByteDisplacementTstImmediateBtPollLoop(int maxCycles, out int cycles)
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
        ushort loadOpcode = 0;
        ushort testOpcode = 0;
        ushort branchOpcode = 0;
        bool found = false;
        ReadOnlySpan<int> offsets = [0, -2, -4];
        foreach (int offset in offsets)
        {
            if (offset < 0 && PC < (uint)-offset)
            {
                continue;
            }

            uint candidate = (uint)(PC + offset);
            if (peekBus.TryPeekWord(candidate, out ushort candidateLoadOpcode) &&
                peekBus.TryPeekWord(candidate + 2, out ushort candidateTestOpcode) &&
                peekBus.TryPeekWord(candidate + 4, out ushort candidateBranchOpcode) &&
                (candidateLoadOpcode & 0xFF00) >= 0x8400 &&
                (candidateLoadOpcode & 0xFF00) <= 0x84F0 &&
                (candidateTestOpcode & 0xFF00) == 0xC800 &&
                (candidateBranchOpcode & 0xFF00) == 0x8900)
            {
                loopPc = candidate;
                loadOpcode = candidateLoadOpcode;
                testOpcode = candidateTestOpcode;
                branchOpcode = candidateBranchOpcode;
                found = true;
                break;
            }
        }

        if (!found)
        {
            return false;
        }

        if ((loadOpcode & 0xFF00) < 0x8400 ||
            (loadOpcode & 0xFF00) > 0x84F0 ||
            (testOpcode & 0xFF00) != 0xC800 ||
            (branchOpcode & 0xFF00) != 0x8900)
        {
            return false;
        }

        int loadSource = (loadOpcode >> 4) & 0x0F;
        int displacement = (sbyte)branchOpcode;
        uint target = loopPc + 8 + (uint)(displacement * 2);
        if (target != loopPc)
        {
            return false;
        }

        uint address = R[loadSource] + (uint)(loadOpcode & 0x0F);
        if (!peekBus.TryPeekByte(address, out byte byteValue))
        {
            return false;
        }

        byte mask = (byte)testOpcode;
        bool zero = (byteValue & mask) == 0;
        if (!zero)
        {
            return false;
        }

        R[0] = (uint)(sbyte)byteValue;
        SetT(true);
        PC = loopPc;
        cycles = maxCycles;
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

    public bool TryFastForwardDoomRecordPairScanLoop(int maxCycles, out int cycles)
    {
        const int CyclesPerUnmatchedRecord = 14;
        cycles = 0;
        if (maxCycles < CyclesPerUnmatchedRecord ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc + 0x00, out ushort loadFirst) ||
            loadFirst != 0x518C ||
            !peekBus.TryPeekWord(loopPc + 0x02, out ushort compareFirst) ||
            compareFirst != 0x31A0 ||
            !peekBus.TryPeekWord(loopPc + 0x04, out ushort branchFirstEqual) ||
            branchFirstEqual != 0x8902 ||
            !peekBus.TryPeekWord(loopPc + 0x06, out ushort loadSecond) ||
            loadSecond != 0x518D ||
            !peekBus.TryPeekWord(loopPc + 0x08, out ushort compareSecond) ||
            compareSecond != 0x31A0 ||
            !peekBus.TryPeekWord(loopPc + 0x0A, out ushort branchSecondNotEqual) ||
            branchSecondNotEqual != 0x8B0C ||
            !peekBus.TryPeekWord(loopPc + 0x26, out ushort incrementIndex) ||
            incrementIndex != 0x7901 ||
            !peekBus.TryPeekWord(loopPc + 0x28, out ushort loadLimitAddress) ||
            loadLimitAddress != 0xD116 ||
            !peekBus.TryPeekWord(loopPc + 0x2A, out ushort loadLimit) ||
            loadLimit != 0x6112 ||
            !peekBus.TryPeekWord(loopPc + 0x2C, out ushort compareLimit) ||
            compareLimit != 0x3913 ||
            !peekBus.TryPeekWord(loopPc + 0x2E, out ushort loopBranch) ||
            loopBranch != 0x8FE7 ||
            !peekBus.TryPeekWord(loopPc + 0x30, out ushort advanceRecord) ||
            advanceRecord != 0x7844)
        {
            return false;
        }

        uint limitLiteralAddress = ((loopPc + 0x28 + 4) & 0xFFFF_FFFCu) + 0x16u * 4u;
        if (!TryPeekLong(peekBus, limitLiteralAddress, out uint limitAddress) ||
            !TryPeekLong(peekBus, limitAddress, out uint limit))
        {
            return false;
        }

        uint record = R[8];
        uint index = R[9];
        uint needle = R[10];
        if (index >= limit)
        {
            return false;
        }

        int maxIterations = Math.Max(1, maxCycles / CyclesPerUnmatchedRecord);
        int iterations = 0;
        while (iterations < maxIterations && index < limit)
        {
            if (!TryPeekLong(peekBus, record + 0x30, out uint first) ||
                !TryPeekLong(peekBus, record + 0x34, out uint second))
            {
                break;
            }

            if (first == needle || second == needle)
            {
                break;
            }

            index++;
            record += 0x44;
            iterations++;
        }

        if (iterations == 0)
        {
            return false;
        }

        R[8] = record;
        R[9] = index;
        R[1] = limit;
        cycles = iterations * CyclesPerUnmatchedRecord;
        Cycles += cycles;
        if (index >= limit)
        {
            SetT(true);
            PC = loopPc + 0x32;
        }
        else
        {
            SetT(false);
            PC = loopPc;
        }

        LastOpcode = advanceRecord;
        LastOpcodePc = loopPc + 0x30;
        return true;
    }

    public bool TryFastForwardMovBPostIncStoreAddCmpGeBfsLoop(int maxCycles, Func<uint, byte?> readByte, Func<uint, byte, bool> writeByte, out int cycles)
    {
        const int CyclesPerIteration = 8;
        cycles = 0;
        if (maxCycles < CyclesPerIteration ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc + 0, out ushort loadOpcode) ||
            loadOpcode != 0x6174 ||
            !peekBus.TryPeekWord(loopPc + 2, out ushort incrementCountOpcode) ||
            incrementCountOpcode != 0x7201 ||
            !peekBus.TryPeekWord(loopPc + 4, out ushort compareOpcode) ||
            compareOpcode != 0x3203 ||
            !peekBus.TryPeekWord(loopPc + 6, out ushort storeOpcode) ||
            storeOpcode != 0x2410 ||
            !peekBus.TryPeekWord(loopPc + 8, out ushort branchOpcode) ||
            branchOpcode != 0x8FFA ||
            !peekBus.TryPeekWord(loopPc + 10, out ushort incrementDestinationOpcode) ||
            incrementDestinationOpcode != 0x7401)
        {
            return false;
        }

        uint limit = R[0];
        uint count = R[2];
        if (count >= limit)
        {
            return false;
        }

        int maxIterations = Math.Max(1, maxCycles / CyclesPerIteration);
        int iterations = 0;
        while (iterations < maxIterations && count < limit)
        {
            byte? value = readByte(R[7]);
            if (!value.HasValue || !writeByte(R[4], value.Value))
            {
                break;
            }

            R[1] = value.Value;
            R[7]++;
            R[2] = ++count;
            R[4]++;
            iterations++;
        }

        if (iterations == 0)
        {
            return false;
        }

        bool complete = count >= limit;
        SetT(complete);
        PC = complete ? loopPc + 12 : loopPc;
        cycles = iterations * CyclesPerIteration;
        Cycles += cycles;
        LastOpcode = incrementDestinationOpcode;
        LastOpcodePc = loopPc + 10;
        return true;
    }

    public bool TryFastForwardDoomMaskedColumnWordStoreLoop(
        int maxCycles,
        Func<uint, byte?> readByte,
        Func<uint, ushort?> readWord,
        Func<uint, ushort, bool> writeWord,
        out int cycles)
    {
        const int CyclesPerIteration = 10;
        cycles = 0;
        if (maxCycles < CyclesPerIteration ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc + 0x00, out ushort loadByte) ||
            loadByte != 0x6830 ||
            !peekBus.TryPeekWord(loopPc + 0x02, out ushort addCarryX) ||
            addCarryX != 0x3D7E ||
            !peekBus.TryPeekWord(loopPc + 0x04, out ushort addCarrySource) ||
            addCarrySource != 0x33AE ||
            !peekBus.TryPeekWord(loopPc + 0x06, out ushort shiftIndex) ||
            shiftIndex != 0x4800 ||
            !peekBus.TryPeekWord(loopPc + 0x08, out ushort lookupWord) ||
            lookupWord != 0x088D ||
            !peekBus.TryPeekWord(loopPc + 0x0A, out ushort decrementCount) ||
            decrementCount != 0x4C10 ||
            !peekBus.TryPeekWord(loopPc + 0x0C, out ushort storeWord) ||
            storeWord != 0x2981 ||
            !peekBus.TryPeekWord(loopPc + 0x0E, out ushort branch) ||
            branch != 0x8FF7 ||
            !peekBus.TryPeekWord(loopPc + 0x10, out ushort advanceDestination) ||
            advanceDestination != 0x395C)
        {
            return false;
        }

        if (R[12] == 0)
        {
            return false;
        }

        int maxIterations = Math.Max(1, maxCycles / CyclesPerIteration);
        int iterations = 0;
        while (iterations < maxIterations && R[12] != 0)
        {
            byte? source = readByte(R[3]);
            if (!source.HasValue)
            {
                break;
            }

            R[8] = SignExtend8(source.Value);
            ExecuteAddc(n: 13, m: 7);
            ExecuteAddc(n: 3, m: 10);
            SetT((R[8] & 0x8000_0000u) != 0);
            R[8] <<= 1;
            ushort? mapped = readWord(R[0] + R[8]);
            if (!mapped.HasValue)
            {
                break;
            }

            R[8] = SignExtend16(mapped.Value);
            R[12]--;
            bool complete = R[12] == 0;
            SetT(complete);
            if (!writeWord(R[9], (ushort)R[8]))
            {
                break;
            }

            R[9] += R[5];
            iterations++;
            if (complete)
            {
                break;
            }
        }

        if (iterations == 0)
        {
            return false;
        }

        bool completed = R[12] == 0;
        PC = completed ? loopPc + 0x12 : loopPc;
        cycles = iterations * CyclesPerIteration;
        Cycles += cycles;
        LastOpcode = advanceDestination;
        LastOpcodePc = loopPc + 0x10;
        return true;
    }

    public bool TryFastForwardDoomSteppedMaskedColumnWordStoreLoop(
        int maxCycles,
        Func<uint, byte?> readByte,
        Func<uint, ushort?> readWord,
        Func<uint, ushort, bool> writeWord,
        out int cycles)
    {
        const int CyclesPerIteration = 10;
        cycles = 0;
        if (maxCycles < CyclesPerIteration ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc + 0x00, out ushort loadByte) ||
            loadByte != 0x6810 ||
            !peekBus.TryPeekWord(loopPc + 0x02, out ushort addCarryX) ||
            addCarryX != 0x334E ||
            !peekBus.TryPeekWord(loopPc + 0x04, out ushort addCarrySource) ||
            addCarrySource != 0x315E ||
            !peekBus.TryPeekWord(loopPc + 0x06, out ushort shiftIndex) ||
            shiftIndex != 0x4800 ||
            !peekBus.TryPeekWord(loopPc + 0x08, out ushort lookupWord) ||
            lookupWord != 0x088D ||
            !peekBus.TryPeekWord(loopPc + 0x0A, out ushort decrementCount) ||
            decrementCount != 0x4610 ||
            !peekBus.TryPeekWord(loopPc + 0x0C, out ushort storeWord) ||
            storeWord != 0x2281 ||
            !peekBus.TryPeekWord(loopPc + 0x0E, out ushort branch) ||
            branch != 0x8FF7 ||
            !peekBus.TryPeekWord(loopPc + 0x10, out ushort advanceDestination) ||
            advanceDestination != 0x327C)
        {
            return false;
        }

        if (R[6] == 0)
        {
            return false;
        }

        int maxIterations = Math.Max(1, maxCycles / CyclesPerIteration);
        int iterations = 0;
        while (iterations < maxIterations && R[6] != 0)
        {
            byte? source = readByte(R[1]);
            if (!source.HasValue)
            {
                break;
            }

            R[8] = SignExtend8(source.Value);
            ExecuteAddc(n: 3, m: 4);
            ExecuteAddc(n: 1, m: 5);
            SetT((R[8] & 0x8000_0000u) != 0);
            R[8] <<= 1;

            ushort? mapped = readWord(R[0] + R[8]);
            if (!mapped.HasValue)
            {
                break;
            }

            R[8] = SignExtend16(mapped.Value);
            R[6]--;
            bool complete = R[6] == 0;
            SetT(complete);
            if (!writeWord(R[2], (ushort)R[8]))
            {
                break;
            }

            R[2] += R[7];
            iterations++;
            if (complete)
            {
                break;
            }
        }

        if (iterations == 0)
        {
            return false;
        }

        bool completed = R[6] == 0;
        PC = completed ? loopPc + 0x12 : loopPc;
        cycles = iterations * CyclesPerIteration;
        Cycles += cycles;
        LastOpcode = branch;
        LastOpcodePc = loopPc + 0x0E;
        return true;
    }

    public bool TryFastForwardDoomSwappedMaskedColumnWordStoreLoop(
        int maxCycles,
        Func<uint, byte?> readByte,
        Func<uint, ushort?> readWord,
        Func<uint, ushort, bool> writeWord,
        out int cycles)
    {
        const int TakenCyclesPerIteration = 13;
        const int FinalCycles = 12;
        cycles = 0;
        if (maxCycles < FinalCycles ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc + 0x00, out ushort swapX) ||
            swapX != 0x6099 ||
            !peekBus.TryPeekWord(loopPc + 0x02, out ushort maskX) ||
            maskX != 0x2049 ||
            !peekBus.TryPeekWord(loopPc + 0x04, out ushort maskY) ||
            maskY != 0x2B59 ||
            !peekBus.TryPeekWord(loopPc + 0x06, out ushort combine) ||
            combine != 0x20BB ||
            !peekBus.TryPeekWord(loopPc + 0x08, out ushort loadByte) ||
            loadByte != 0x001C ||
            !peekBus.TryPeekWord(loopPc + 0x0A, out ushort stepX) ||
            stepX != 0x39AC ||
            !peekBus.TryPeekWord(loopPc + 0x0C, out ushort stepY) ||
            stepY != 0x378C ||
            !peekBus.TryPeekWord(loopPc + 0x0E, out ushort shiftIndex) ||
            shiftIndex != 0x4000 ||
            !peekBus.TryPeekWord(loopPc + 0x10, out ushort lookupWord) ||
            lookupWord != 0x003D ||
            !peekBus.TryPeekWord(loopPc + 0x12, out ushort decrementCount) ||
            decrementCount != 0x4610 ||
            !peekBus.TryPeekWord(loopPc + 0x14, out ushort storeWord) ||
            storeWord != 0x2205 ||
            !peekBus.TryPeekWord(loopPc + 0x16, out ushort branch) ||
            branch != 0x8FF3 ||
            !peekBus.TryPeekWord(loopPc + 0x18, out ushort delaySwapY) ||
            delaySwapY != 0x6B79)
        {
            return false;
        }

        if (R[6] == 0)
        {
            return false;
        }

        int iterations = 0;
        while (R[6] != 0)
        {
            bool finalIteration = R[6] == 1;
            int iterationCycles = finalIteration ? FinalCycles : TakenCyclesPerIteration;
            if (cycles + iterationCycles > maxCycles)
            {
                break;
            }

            R[0] = (R[9] << 16) | (R[9] >> 16);
            R[0] &= R[4];
            R[11] &= R[5];
            R[0] |= R[11];

            byte? source = readByte(R[0] + R[1]);
            if (!source.HasValue)
            {
                break;
            }

            R[0] = SignExtend8(source.Value);
            R[9] += R[10];
            R[7] += R[8];
            SetT((R[0] & 0x8000_0000u) != 0);
            R[0] <<= 1;

            ushort? mapped = readWord(R[0] + R[3]);
            if (!mapped.HasValue)
            {
                break;
            }

            R[0] = SignExtend16(mapped.Value);
            R[6]--;
            bool complete = R[6] == 0;
            SetT(complete);

            uint destination = R[2] - 2;
            if (!writeWord(destination, (ushort)R[0]))
            {
                break;
            }

            R[2] = destination;
            cycles += iterationCycles;
            iterations++;

            if (complete)
            {
                PC = loopPc + 0x18;
                break;
            }

            R[11] = (R[7] << 16) | (R[7] >> 16);
            PC = loopPc;
        }

        if (iterations == 0)
        {
            cycles = 0;
            return false;
        }

        Cycles += cycles;
        LastOpcode = branch;
        LastOpcodePc = loopPc + 0x16;
        return true;
    }

    public bool TryFastForwardRepeatedSharR4Rts(int maxCycles, out int cycles)
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

        uint pc = PC;
        int shiftCount = 0;
        while (shiftCount < 32 &&
            peekBus.TryPeekWord(pc + (uint)(shiftCount * 2), out ushort shiftOpcode) &&
            shiftOpcode == 0x4421)
        {
            shiftCount++;
        }

        if (shiftCount == 0 ||
            !peekBus.TryPeekWord(pc + (uint)(shiftCount * 2), out ushort rtsOpcode) ||
            rtsOpcode != 0x000B ||
            !peekBus.TryPeekWord(pc + (uint)((shiftCount + 1) * 2), out ushort delayOpcode) ||
            delayOpcode != 0x4421)
        {
            return false;
        }

        int requiredCycles = shiftCount + 2;
        if (maxCycles < requiredCycles)
        {
            return false;
        }

        for (int i = 0; i < shiftCount + 1; i++)
        {
            SetT((R[4] & 0x8000_0000u) != 0);
            R[4] = (uint)((int)R[4] >> 1);
        }

        PC = PR;
        cycles = requiredCycles;
        Cycles += cycles;
        LastOpcode = rtsOpcode;
        LastOpcodePc = pc + (uint)(shiftCount * 2);
        return true;
    }

    public bool TryFastForwardDoomByteLookupSpanLoop(
        int maxCycles,
        Func<uint, byte?> readByte,
        Func<uint, byte, bool> writeByte,
        out int cycles)
    {
        cycles = 0;
        if (maxCycles < 13 ||
            Halted ||
            HasAcceptablePendingInterrupt ||
            DelaySlotActive ||
            InstructionObserver is not null ||
            _bus is not ISh2PeekBus peekBus)
        {
            return false;
        }

        uint loopPc = PC;
        if (!peekBus.TryPeekWord(loopPc + 0x00, out ushort compareFirst) ||
            compareFirst != 0x3546 ||
            !peekBus.TryPeekWord(loopPc + 0x02, out ushort branchFirst) ||
            branchFirst != 0x8D07 ||
            !peekBus.TryPeekWord(loopPc + 0x04, out ushort copyIndex) ||
            copyIndex != 0x6233 ||
            !peekBus.TryPeekWord(loopPc + 0x06, out ushort subtractWindow) ||
            subtractWindow != 0x72E1 ||
            !peekBus.TryPeekWord(loopPc + 0x08, out ushort loadWindowLimit) ||
            loadWindowLimit != 0x911D ||
            !peekBus.TryPeekWord(loopPc + 0x0A, out ushort compareWindow) ||
            compareWindow != 0x3216 ||
            !peekBus.TryPeekWord(loopPc + 0x0C, out ushort branchWrite) ||
            branchWrite != 0x8D02 ||
            !peekBus.TryPeekWord(loopPc + 0x0E, out ushort setOne) ||
            setOne != 0xE101 ||
            !peekBus.TryPeekWord(loopPc + 0x10, out ushort branchSkipWrite) ||
            branchSkipWrite != 0xA009 ||
            !peekBus.TryPeekWord(loopPc + 0x12, out ushort nop) ||
            nop != 0x0009 ||
            !peekBus.TryPeekWord(loopPc + 0x14, out ushort setMask) ||
            setMask != 0xE13F ||
            !peekBus.TryPeekWord(loopPc + 0x16, out ushort maskIndex) ||
            maskIndex != 0x2139 ||
            !peekBus.TryPeekWord(loopPc + 0x18, out ushort copyMaskedIndex) ||
            copyMaskedIndex != 0x6013 ||
            !peekBus.TryPeekWord(loopPc + 0x1A, out ushort shift0) ||
            shift0 != 0x4008 ||
            !peekBus.TryPeekWord(loopPc + 0x1C, out ushort shift1) ||
            shift1 != 0x4008 ||
            !peekBus.TryPeekWord(loopPc + 0x1E, out ushort shift2) ||
            shift2 != 0x4008 ||
            !peekBus.TryPeekWord(loopPc + 0x20, out ushort loadLookup) ||
            loadLookup != 0x006C ||
            !peekBus.TryPeekWord(loopPc + 0x22, out ushort orOne) ||
            orOne != 0xCB01 ||
            !peekBus.TryPeekWord(loopPc + 0x24, out ushort storeByte) ||
            storeByte != 0x2800 ||
            !peekBus.TryPeekWord(loopPc + 0x26, out ushort incrementIndex) ||
            incrementIndex != 0x7301 ||
            !peekBus.TryPeekWord(loopPc + 0x28, out ushort loadLoopLimit) ||
            loadLoopLimit != 0x910E ||
            !peekBus.TryPeekWord(loopPc + 0x2A, out ushort compareLoopLimit) ||
            compareLoopLimit != 0x3317 ||
            !peekBus.TryPeekWord(loopPc + 0x2C, out ushort branchLoop) ||
            branchLoop != 0x8FE8 ||
            !peekBus.TryPeekWord(loopPc + 0x2E, out ushort advanceDestination) ||
            advanceDestination != 0x7801)
        {
            return false;
        }

        uint windowLimitLiteralAddress = PcRelativeBase(loopPc + 0x08) + 0x1Du * 2u;
        uint loopLimitLiteralAddress = PcRelativeBase(loopPc + 0x28) + 0x0Eu * 2u;
        if (!peekBus.TryPeekWord(windowLimitLiteralAddress, out ushort windowLimitWord) ||
            !peekBus.TryPeekWord(loopLimitLiteralAddress, out ushort loopLimitWord))
        {
            return false;
        }

        uint windowLimit = SignExtend16(windowLimitWord);
        uint loopLimit = SignExtend16(loopLimitWord);
        int iterations = 0;
        while (cycles < maxCycles)
        {
            bool firstWritePath = R[5] > R[4];
            uint nextR2 = R[3];
            uint nextR1 = R[1];
            bool writePixel;
            int prefixCycles;
            if (firstWritePath)
            {
                writePixel = true;
                prefixCycles = 3;
            }
            else
            {
                nextR2 = R[3] + SignExtend8(0xE1);
                nextR1 = windowLimit;
                writePixel = nextR2 > nextR1;
                prefixCycles = writePixel ? 8 : 10;
                if (writePixel)
                {
                    nextR1 = 1;
                }
                else
                {
                    nextR1 = 1;
                }
            }

            uint incrementedR3 = R[3] + 1;
            bool continueLoop = (int)incrementedR3 <= (int)loopLimit;
            int iterationCycles = prefixCycles + (writePixel ? 9 : 0) + 3 + (continueLoop ? 2 : 1);
            if (cycles + iterationCycles > maxCycles)
            {
                break;
            }

            byte output = 0;
            if (writePixel)
            {
                uint maskedIndex = R[3] & 0x3Fu;
                uint lookupOffset = maskedIndex << 6;
                byte? lookup = readByte(R[6] + lookupOffset);
                if (!lookup.HasValue ||
                    !writeByte(R[8], (byte)(lookup.Value | 0x01)))
                {
                    break;
                }

                output = (byte)(lookup.Value | 0x01);
            }

            SetT(firstWritePath);
            R[2] = nextR2;
            R[1] = writePixel ? 0x3Fu : nextR1;
            if (writePixel)
            {
                R[1] &= R[3];
                R[0] = R[1] << 6;
                R[0] = SignExtend8(output);
            }

            R[3] = incrementedR3;
            R[1] = loopLimit;
            SetT((int)R[3] > (int)R[1]);
            cycles += iterationCycles;
            iterations++;

            if (continueLoop)
            {
                R[8]++;
                PC = loopPc;
                continue;
            }

            PC = loopPc + 0x2E;
            break;
        }

        if (iterations == 0)
        {
            cycles = 0;
            return false;
        }

        Cycles += cycles;
        LastOpcode = branchLoop;
        LastOpcodePc = loopPc + 0x2C;
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
        ushort opcode = _bus is ISh2InstructionBus instructionBus
            ? instructionBus.ReadInstructionWord(pc)
            : _bus.ReadWord(pc);
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
        ClearAllPendingInterrupts();
        if (state.PendingInterruptLevel is >= 1 and <= 15)
        {
            _pendingInterruptVectorsByLevel[state.PendingInterruptLevel] = state.PendingInterruptVectorNumber != 0
                ? state.PendingInterruptVectorNumber
                : 64 + state.PendingInterruptLevel;
            RefreshPendingInterruptView();
        }
    }

    private int AcceptPendingInterrupt()
    {
        int level = FindHighestAcceptablePendingInterruptLevel();
        if (level == 0)
        {
            return 0;
        }

        int vectorNumber = _pendingInterruptVectorsByLevel[level] != 0 ? _pendingInterruptVectorsByLevel[level] : 64 + level;
        _pendingInterruptVectorsByLevel[level] = 0;
        RefreshPendingInterruptView();
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

    private void ClearAllPendingInterrupts()
    {
        Array.Clear(_pendingInterruptVectorsByLevel);
        PendingInterruptLevel = 0;
        PendingInterruptVectorNumber = 0;
    }

    private void RefreshPendingInterruptView()
    {
        for (int level = 15; level >= 1; level--)
        {
            int vector = _pendingInterruptVectorsByLevel[level];
            if (vector != 0)
            {
                PendingInterruptLevel = level;
                PendingInterruptVectorNumber = vector;
                return;
            }
        }

        PendingInterruptLevel = 0;
        PendingInterruptVectorNumber = 0;
    }

    private int FindHighestAcceptablePendingInterruptLevel()
    {
        int srLevel = (int)((SR >> 4) & 0x0F);
        for (int level = 15; level > srLevel; level--)
        {
            if (_pendingInterruptVectorsByLevel[level] != 0)
            {
                return level;
            }
        }

        return 0;
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

    private bool IsTSet()
    {
        return (SR & TBit) != 0;
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

    private static bool TryPeekPcRelativeLong(ISh2PeekBus peekBus, uint opcodePc, byte displacement, out uint value)
    {
        uint literalAddress = ((opcodePc + 4) & 0xFFFF_FFFCu) + (uint)(displacement * 4);
        return TryPeekLong(peekBus, literalAddress, out value);
    }

    private static bool TryPeekPcRelativeWord(ISh2PeekBus peekBus, uint opcodePc, byte displacement, out ushort value)
    {
        uint literalAddress = opcodePc + 4 + (uint)(displacement * 2);
        return peekBus.TryPeekWord(literalAddress, out value);
    }

    private static uint BranchByteTarget(uint branchPc, ushort branchOpcode)
    {
        return branchPc + 4 + (uint)(((sbyte)branchOpcode) * 2);
    }

    private static bool TryResolveAddBraNopDelayLoop(ISh2PeekBus peekBus, ref uint loopPc, out ushort addOpcode, out ushort branchOpcode)
    {
        addOpcode = 0;
        branchOpcode = 0;

        for (int offset = 0; offset >= -4; offset -= 2)
        {
            if (offset < 0 && loopPc < (uint)-offset)
            {
                continue;
            }

            uint candidate = unchecked(loopPc + (uint)offset);
            if (!peekBus.TryPeekWord(candidate, out ushort candidateAddOpcode) ||
                (candidateAddOpcode & 0xF000) != 0x7000 ||
                !peekBus.TryPeekWord(candidate + 2, out ushort candidateBranchOpcode) ||
                (candidateBranchOpcode & 0xF000) != 0xA000 ||
                BranchWordTarget(candidate + 2, candidateBranchOpcode) != candidate ||
                !peekBus.TryPeekWord(candidate + 4, out ushort delaySlotOpcode) ||
                delaySlotOpcode != 0x0009)
            {
                continue;
            }

            loopPc = candidate;
            addOpcode = candidateAddOpcode;
            branchOpcode = candidateBranchOpcode;
            return true;
        }

        return false;
    }

    private bool TryFindThreeWordPollLoop(
        ISh2PeekBus peekBus,
        ReadOnlySpan<int> offsets,
        out uint loopPc,
        out ushort firstOpcode,
        out ushort secondOpcode,
        out ushort branchOpcode)
    {
        foreach (int offset in offsets)
        {
            if (offset < 0 && PC < (uint)-offset)
            {
                continue;
            }

            uint candidate = unchecked(PC + (uint)offset);
            if (peekBus.TryPeekWord(candidate, out firstOpcode) &&
                peekBus.TryPeekWord(candidate + 2, out secondOpcode) &&
                peekBus.TryPeekWord(candidate + 4, out branchOpcode) &&
                BranchByteTarget(candidate + 4, branchOpcode) == candidate)
            {
                loopPc = candidate;
                return true;
            }
        }

        loopPc = 0;
        firstOpcode = 0;
        secondOpcode = 0;
        branchOpcode = 0;
        return false;
    }

    private bool TryFindFourWordPollLoop(
        ISh2PeekBus peekBus,
        ReadOnlySpan<int> offsets,
        out uint loopPc,
        out ushort firstOpcode,
        out ushort secondOpcode,
        out ushort thirdOpcode,
        out ushort branchOpcode)
    {
        foreach (int offset in offsets)
        {
            if (offset < 0 && PC < (uint)-offset)
            {
                continue;
            }

            uint candidate = unchecked(PC + (uint)offset);
            if (peekBus.TryPeekWord(candidate, out firstOpcode) &&
                peekBus.TryPeekWord(candidate + 2, out secondOpcode) &&
                peekBus.TryPeekWord(candidate + 4, out thirdOpcode) &&
                peekBus.TryPeekWord(candidate + 6, out branchOpcode) &&
                BranchByteTarget(candidate + 6, branchOpcode) == candidate)
            {
                loopPc = candidate;
                return true;
            }
        }

        loopPc = 0;
        firstOpcode = 0;
        secondOpcode = 0;
        thirdOpcode = 0;
        branchOpcode = 0;
        return false;
    }

    private bool TryFindFiveWordLoop(
        ISh2PeekBus peekBus,
        ReadOnlySpan<int> offsets,
        out uint loopPc,
        out ushort firstOpcode,
        out ushort secondOpcode,
        out ushort thirdOpcode,
        out ushort fourthOpcode,
        out ushort branchOpcode)
    {
        foreach (int offset in offsets)
        {
            if (offset < 0 && PC < (uint)-offset)
            {
                continue;
            }

            uint candidate = unchecked(PC + (uint)offset);
            if (peekBus.TryPeekWord(candidate, out firstOpcode) &&
                peekBus.TryPeekWord(candidate + 2, out secondOpcode) &&
                peekBus.TryPeekWord(candidate + 4, out thirdOpcode) &&
                peekBus.TryPeekWord(candidate + 6, out fourthOpcode) &&
                peekBus.TryPeekWord(candidate + 8, out branchOpcode) &&
                BranchByteTarget(candidate + 8, branchOpcode) == candidate)
            {
                loopPc = candidate;
                return true;
            }
        }

        loopPc = 0;
        firstOpcode = 0;
        secondOpcode = 0;
        thirdOpcode = 0;
        fourthOpcode = 0;
        branchOpcode = 0;
        return false;
    }

    private static uint BranchWordTarget(uint branchPc, ushort branchOpcode)
    {
        return branchPc + 4 + (uint)(((short)(branchOpcode << 4) >> 4) * 2);
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

    private static bool SignedGreaterThan(uint left, uint right)
    {
        return (int)left > (int)right;
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
