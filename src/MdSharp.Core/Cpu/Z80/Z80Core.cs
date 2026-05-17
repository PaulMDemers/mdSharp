namespace MdSharp.Core.Cpu.Z80;

public sealed class Z80Core
{
    private const byte FlagS = 0x80;
    private const byte FlagZ = 0x40;
    private const byte FlagH = 0x10;
    private const byte FlagP = 0x04;
    private const byte FlagN = 0x02;
    private const byte FlagC = 0x01;

    public byte A { get; private set; }
    public byte F { get; private set; }
    public byte B { get; private set; }
    public byte C { get; private set; }
    public byte D { get; private set; }
    public byte E { get; private set; }
    public byte H { get; private set; }
    public byte L { get; private set; }
    public byte AlternateA { get; private set; }
    public byte AlternateF { get; private set; }
    public byte AlternateB { get; private set; }
    public byte AlternateC { get; private set; }
    public byte AlternateD { get; private set; }
    public byte AlternateE { get; private set; }
    public byte AlternateH { get; private set; }
    public byte AlternateL { get; private set; }
    public byte I { get; private set; }
    public byte R { get; private set; }
    public ushort IX { get; private set; }
    public ushort IY { get; private set; }
    public ushort PC { get; private set; }
    public ushort SP { get; private set; }
    public long Cycles { get; private set; }
    public bool ResetAsserted { get; private set; } = true;
    public bool BusRequested { get; private set; }
    public bool Halted { get; private set; }
    public bool InterruptsEnabled => _iff1;
    public byte InterruptMode => _interruptMode;
    public bool LastStepAcceptedInterrupt { get; private set; }

    private bool _iff1;
    private bool _iff2;
    private int _interruptEnableDelay;
    private byte _interruptMode = 1;

    private ushort BC
    {
        get => (ushort)((B << 8) | C);
        set { B = (byte)(value >> 8); C = (byte)value; }
    }

    private ushort DE
    {
        get => (ushort)((D << 8) | E);
        set { D = (byte)(value >> 8); E = (byte)value; }
    }

    private ushort HL
    {
        get => (ushort)((H << 8) | L);
        set { H = (byte)(value >> 8); L = (byte)value; }
    }

    public void Reset()
    {
        A = F = B = C = D = E = H = L = 0;
        AlternateA = AlternateF = AlternateB = AlternateC = AlternateD = AlternateE = AlternateH = AlternateL = 0;
        I = R = 0;
        IX = IY = 0;
        PC = 0;
        SP = 0xFFFF;
        Cycles = 0;
        ResetAsserted = true;
        BusRequested = false;
        Halted = false;
        _iff1 = false;
        _iff2 = false;
        _interruptEnableDelay = 0;
        _interruptMode = 1;
    }

    public void SetLines(bool resetAsserted, bool busRequested)
    {
        if (ResetAsserted && !resetAsserted)
        {
            A = F = B = C = D = E = H = L = 0;
            AlternateA = AlternateF = AlternateB = AlternateC = AlternateD = AlternateE = AlternateH = AlternateL = 0;
            I = R = 0;
            IX = IY = 0;
            PC = 0;
            SP = 0xFFFF;
            Halted = false;
            _iff1 = false;
            _iff2 = false;
            _interruptEnableDelay = 0;
            _interruptMode = 1;
        }

        ResetAsserted = resetAsserted;
        BusRequested = busRequested;
    }

    public int Step(int cycleBudget, Func<ushort, byte> read, Action<ushort, byte> write, bool interruptPending = false)
    {
        return Step(cycleBudget, new DelegateZ80Bus(read, write), interruptPending);
    }

    public int Step(int cycleBudget, IZ80Bus bus, bool interruptPending = false)
    {
        if (ResetAsserted || BusRequested)
        {
            return 0;
        }

        int consumed = 0;
        do
        {
            int cycles = StepInstruction(bus, interruptPending);
            consumed += cycles;
        }
        while (consumed < cycleBudget);

        return consumed;
    }

    public int StepInstruction(Func<ushort, byte> read, Action<ushort, byte> write, bool interruptPending = false)
    {
        return StepInstruction(new DelegateZ80Bus(read, write), interruptPending);
    }

    public int StepInstruction(IZ80Bus bus, bool interruptPending = false)
    {
        LastStepAcceptedInterrupt = false;
        if (ResetAsserted || BusRequested)
        {
            return 0;
        }

        int cycles;
        if (interruptPending && _iff1 && _interruptEnableDelay == 0)
        {
            cycles = AcceptMaskableInterrupt(bus);
        }
        else
        {
            cycles = Halted ? 4 : Execute(bus);
            if (_interruptEnableDelay > 0)
            {
                _interruptEnableDelay--;
            }
        }

        Cycles += cycles;
        return cycles;
    }

    private int AcceptMaskableInterrupt(IZ80Bus bus)
    {
        Halted = false;
        LastStepAcceptedInterrupt = true;
        _iff1 = false;
        _iff2 = false;
        Push(bus, PC);

        if (_interruptMode == 2)
        {
            ushort vectorAddress = (ushort)((I << 8) | 0xFF);
            PC = (ushort)(bus.ReadByte(vectorAddress) | (bus.ReadByte((ushort)(vectorAddress + 1)) << 8));
            return 19;
        }

        PC = 0x0038;
        return 13;
    }

    private int Execute(IZ80Bus bus)
    {
        byte opcode = Fetch(bus);
        switch (opcode)
        {
            case 0x00:
                return 4;
            case 0x01:
                BC = FetchWord(bus);
                return 10;
            case 0x02:
                bus.WriteByte(BC, A);
                return 7;
            case 0x03:
                BC++;
                return 6;
            case 0x07:
                RotateAccumulator(left: true, throughCarry: false);
                return 4;
            case 0x08:
                (A, AlternateA) = (AlternateA, A);
                (F, AlternateF) = (AlternateF, F);
                return 4;
            case 0x09:
                AddHl(BC);
                return 11;
            case 0x0A:
                A = bus.ReadByte(BC);
                return 7;
            case 0x0B:
                BC--;
                return 6;
            case 0x0F:
                RotateAccumulator(left: false, throughCarry: false);
                return 4;
            case 0x11:
                DE = FetchWord(bus);
                return 10;
            case 0x12:
                bus.WriteByte(DE, A);
                return 7;
            case 0x13:
                DE++;
                return 6;
            case 0x17:
                RotateAccumulator(left: true, throughCarry: true);
                return 4;
            case 0x19:
                AddHl(DE);
                return 11;
            case 0x1A:
                A = bus.ReadByte(DE);
                return 7;
            case 0x1B:
                DE--;
                return 6;
            case 0x1F:
                RotateAccumulator(left: false, throughCarry: true);
                return 4;
            case 0x21:
                HL = FetchWord(bus);
                return 10;
            case 0x22:
            {
                ushort address = FetchWord(bus);
                bus.WriteByte(address, L);
                bus.WriteByte((ushort)(address + 1), H);
                return 16;
            }
            case 0x23:
                HL++;
                return 6;
            case 0x29:
                AddHl(HL);
                return 11;
            case 0x2A:
            {
                ushort address = FetchWord(bus);
                L = bus.ReadByte(address);
                H = bus.ReadByte((ushort)(address + 1));
                return 16;
            }
            case 0x2B:
                HL--;
                return 6;
            case 0x27:
                DecimalAdjustAccumulator();
                return 4;
            case 0x2F:
                A = (byte)~A;
                F = (byte)((F & (FlagS | FlagZ | FlagP | FlagC)) | FlagH | FlagN);
                return 4;
            case 0x31:
                SP = FetchWord(bus);
                return 10;
            case 0x33:
                SP++;
                return 6;
            case 0x37:
                F = (byte)((F & (FlagS | FlagZ | FlagP)) | FlagC);
                return 4;
            case 0x3F:
            {
                bool carry = (F & FlagC) != 0;
                F = (byte)((F & (FlagS | FlagZ | FlagP)) | (carry ? FlagH : 0) | (carry ? 0 : FlagC));
                return 4;
            }
            case 0x39:
                AddHl(SP);
                return 11;
            case 0x3B:
                SP--;
                return 6;
            case 0x3E:
                A = Fetch(bus);
                return 7;
            case 0x06:
                B = Fetch(bus);
                return 7;
            case 0x0E:
                C = Fetch(bus);
                return 7;
            case 0x16:
                D = Fetch(bus);
                return 7;
            case 0x1E:
                E = Fetch(bus);
                return 7;
            case 0x26:
                H = Fetch(bus);
                return 7;
            case 0x2E:
                L = Fetch(bus);
                return 7;
            case 0x32:
                bus.WriteByte(FetchWord(bus), A);
                return 13;
            case 0x3A:
                A = bus.ReadByte(FetchWord(bus));
                return 13;
            case 0x36:
                bus.WriteByte(HL, Fetch(bus));
                return 10;
            case 0xCB:
                return ExecuteCb(bus);
            case 0xED:
                return ExecuteEd(bus);
            case 0xDD:
            case 0xFD:
                return ExecuteIndex(opcode == 0xDD, bus);
            case 0xC0:
            case 0xC8:
            case 0xD0:
            case 0xD8:
            case 0xE0:
            case 0xE8:
            case 0xF0:
            case 0xF8:
                if (!TestCondition((opcode >> 3) & 0x07))
                {
                    return 5;
                }

                PC = Pop(bus);
                return 11;
            case 0xC1:
                BC = Pop(bus);
                return 10;
            case 0xD1:
                DE = Pop(bus);
                return 10;
            case 0xE1:
                HL = Pop(bus);
                return 10;
            case 0xE3:
            {
                ushort value = (ushort)(bus.ReadByte(SP) | (bus.ReadByte((ushort)(SP + 1)) << 8));
                bus.WriteByte(SP, L);
                bus.WriteByte((ushort)(SP + 1), H);
                HL = value;
                return 19;
            }
            case 0xF1:
            {
                ushort af = Pop(bus);
                A = (byte)(af >> 8);
                F = (byte)(af & 0xD7);
                return 10;
            }
            case 0xC2:
            case 0xCA:
            case 0xD2:
            case 0xDA:
            case 0xE2:
            case 0xEA:
            case 0xF2:
            case 0xFA:
            {
                ushort target = FetchWord(bus);
                if (TestCondition((opcode >> 3) & 0x07))
                {
                    PC = target;
                }

                return 10;
            }
            case 0xAF:
                A ^= A;
                SetLogicFlags(A);
                return 4;
            case 0xA7:
                SetLogicFlags(A);
                return 4;
            case 0xC4:
            case 0xCC:
            case 0xD4:
            case 0xDC:
            case 0xE4:
            case 0xEC:
            case 0xF4:
            case 0xFC:
            {
                ushort target = FetchWord(bus);
                if (!TestCondition((opcode >> 3) & 0x07))
                {
                    return 10;
                }

                Push(bus, PC);
                PC = target;
                return 17;
            }
            case 0xC5:
                Push(bus, BC);
                return 11;
            case 0xD5:
                Push(bus, DE);
                return 11;
            case 0xE5:
                Push(bus, HL);
                return 11;
            case 0xF5:
                Push(bus, (ushort)((A << 8) | F));
                return 11;
            case 0xC6:
                Add(Fetch(bus));
                return 7;
            case 0xCE:
                AddWithCarry(Fetch(bus));
                return 7;
            case 0xD6:
                Subtract(Fetch(bus));
                return 7;
            case 0xDE:
                SubtractWithCarry(Fetch(bus));
                return 7;
            case 0xE6:
                A &= Fetch(bus);
                SetLogicFlags(A, halfCarry: true);
                return 7;
            case 0xEE:
                A ^= Fetch(bus);
                SetLogicFlags(A);
                return 7;
            case 0xF6:
                A |= Fetch(bus);
                SetLogicFlags(A);
                return 7;
            case 0xFE:
                Compare(Fetch(bus));
                return 7;
            case 0xC3:
                PC = FetchWord(bus);
                return 10;
            case 0x18:
            {
                sbyte offset = (sbyte)Fetch(bus);
                PC = unchecked((ushort)(PC + offset));
                return 12;
            }
            case 0x20:
                return JumpRelativeIf(bus, (F & FlagZ) == 0);
            case 0x28:
                return JumpRelativeIf(bus, (F & FlagZ) != 0);
            case 0x30:
                return JumpRelativeIf(bus, (F & FlagC) == 0);
            case 0x38:
                return JumpRelativeIf(bus, (F & FlagC) != 0);
            case 0x10:
                B--;
                sbyte displacement = (sbyte)Fetch(bus);
                if (B != 0)
                {
                    PC = unchecked((ushort)(PC + displacement));
                    return 13;
                }

                return 8;
            case 0xCD:
            {
                ushort target = FetchWord(bus);
                Push(bus, PC);
                PC = target;
                return 17;
            }
            case 0xC9:
                PC = Pop(bus);
                return 10;
            case 0xD3:
                _ = Fetch(bus);
                return 11;
            case 0xD9:
                (B, AlternateB) = (AlternateB, B);
                (C, AlternateC) = (AlternateC, C);
                (D, AlternateD) = (AlternateD, D);
                (E, AlternateE) = (AlternateE, E);
                (H, AlternateH) = (AlternateH, H);
                (L, AlternateL) = (AlternateL, L);
                return 4;
            case 0xDB:
                _ = Fetch(bus);
                A = 0xFF;
                SetLogicFlags(A);
                return 11;
            case 0xE9:
                PC = HL;
                return 4;
            case 0xEB:
                (DE, HL) = (HL, DE);
                return 4;
            case 0xF3:
                _iff1 = false;
                _iff2 = false;
                _interruptEnableDelay = 0;
                return 4;
            case 0xFB:
                _iff1 = true;
                _iff2 = true;
                _interruptEnableDelay = 2;
                return 4;
            case 0xF9:
                SP = HL;
                return 6;
            case 0x76:
                Halted = true;
                return 4;
            default:
                if ((opcode & 0xC0) == 0x40)
                {
                    WriteRegister(opcode >> 3 & 0x07, ReadRegister(opcode & 0x07, bus), bus);
                    return (opcode & 0x07) == 6 || ((opcode >> 3) & 0x07) == 6 ? 7 : 4;
                }

                if ((opcode & 0xC7) == 0x04)
                {
                    int register = (opcode >> 3) & 0x07;
                    byte value = (byte)(ReadRegister(register, bus) + 1);
                    WriteRegister(register, value, bus);
                    SetIncDecFlags(value);
                    return register == 6 ? 11 : 4;
                }

                if ((opcode & 0xC7) == 0x05)
                {
                    int register = (opcode >> 3) & 0x07;
                    byte value = (byte)(ReadRegister(register, bus) - 1);
                    WriteRegister(register, value, bus);
                    SetIncDecFlags(value, subtract: true);
                    return register == 6 ? 11 : 4;
                }

                if ((opcode & 0xF8) == 0x80)
                {
                    Add(ReadRegister(opcode & 0x07, bus));
                    return (opcode & 0x07) == 6 ? 7 : 4;
                }

                if ((opcode & 0xF8) == 0x88)
                {
                    AddWithCarry(ReadRegister(opcode & 0x07, bus));
                    return (opcode & 0x07) == 6 ? 7 : 4;
                }

                if ((opcode & 0xF8) == 0x90)
                {
                    Subtract(ReadRegister(opcode & 0x07, bus));
                    return (opcode & 0x07) == 6 ? 7 : 4;
                }

                if ((opcode & 0xF8) == 0x98)
                {
                    SubtractWithCarry(ReadRegister(opcode & 0x07, bus));
                    return (opcode & 0x07) == 6 ? 7 : 4;
                }

                if ((opcode & 0xF8) == 0xA0)
                {
                    A &= ReadRegister(opcode & 0x07, bus);
                    SetLogicFlags(A, halfCarry: true);
                    return (opcode & 0x07) == 6 ? 7 : 4;
                }

                if ((opcode & 0xF8) == 0xA8)
                {
                    A ^= ReadRegister(opcode & 0x07, bus);
                    SetLogicFlags(A);
                    return (opcode & 0x07) == 6 ? 7 : 4;
                }

                if ((opcode & 0xF8) == 0xB0)
                {
                    A |= ReadRegister(opcode & 0x07, bus);
                    SetLogicFlags(A);
                    return (opcode & 0x07) == 6 ? 7 : 4;
                }

                if ((opcode & 0xF8) == 0xB8)
                {
                    Compare(ReadRegister(opcode & 0x07, bus));
                    return (opcode & 0x07) == 6 ? 7 : 4;
                }

                if ((opcode & 0xC7) == 0xC7)
                {
                    Push(bus, PC);
                    PC = (ushort)(opcode & 0x38);
                    return 11;
                }

                Halted = true;
                return 4;
        }
    }

    private byte Fetch(IZ80Bus bus)
    {
        byte value = bus.ReadByte(PC);
        PC++;
        R = (byte)((R & 0x80) | ((R + 1) & 0x7F));
        return value;
    }

    private ushort FetchWord(IZ80Bus bus)
    {
        byte low = Fetch(bus);
        byte high = Fetch(bus);
        return (ushort)(low | (high << 8));
    }

    private byte ReadRegister(int register, IZ80Bus bus)
    {
        return register switch
        {
            0 => B,
            1 => C,
            2 => D,
            3 => E,
            4 => H,
            5 => L,
            6 => bus.ReadByte(HL),
            7 => A,
            _ => 0,
        };
    }

    private void WriteRegister(int register, byte value, IZ80Bus bus)
    {
        switch (register)
        {
            case 0: B = value; break;
            case 1: C = value; break;
            case 2: D = value; break;
            case 3: E = value; break;
            case 4: H = value; break;
            case 5: L = value; break;
            case 6: bus.WriteByte(HL, value); break;
            case 7: A = value; break;
        }
    }

    private int JumpRelativeIf(IZ80Bus bus, bool condition)
    {
        sbyte displacement = (sbyte)Fetch(bus);
        if (!condition)
        {
            return 7;
        }

        PC = unchecked((ushort)(PC + displacement));
        return 12;
    }

    private void Push(IZ80Bus bus, ushort value)
    {
        SP--;
        bus.WriteByte(SP, (byte)(value >> 8));
        SP--;
        bus.WriteByte(SP, (byte)value);
    }

    private ushort Pop(IZ80Bus bus)
    {
        byte low = bus.ReadByte(SP++);
        byte high = bus.ReadByte(SP++);
        return (ushort)(low | (high << 8));
    }

    private int ExecuteCb(IZ80Bus bus)
    {
        byte opcode = Fetch(bus);
        int register = opcode & 0x07;
        int operation = opcode >> 6;
        int bit = (opcode >> 3) & 0x07;
        byte value = ReadRegister(register, bus);

        if (operation == 0)
        {
            byte result = bit switch
            {
                0 => Rotate(value, left: true, throughCarry: false),
                1 => Rotate(value, left: false, throughCarry: false),
                2 => Rotate(value, left: true, throughCarry: true),
                3 => Rotate(value, left: false, throughCarry: true),
                4 => ShiftLeftArithmetic(value),
                5 => ShiftRightArithmetic(value),
                6 => ShiftLeftArithmetic(value),
                _ => ShiftRightLogical(value),
            };
            WriteRegister(register, result, bus);
            return register == 6 ? 15 : 8;
        }

        if (operation == 1)
        {
            TestBit(value, bit);
            return register == 6 ? 12 : 8;
        }

        byte mask = (byte)(1 << bit);
        byte updated = operation == 2 ? (byte)(value & ~mask) : (byte)(value | mask);
        WriteRegister(register, updated, bus);
        return register == 6 ? 15 : 8;
    }

    private int ExecuteEd(IZ80Bus bus)
    {
        byte opcode = Fetch(bus);
        switch (opcode)
        {
            case 0x45:
            case 0x4D:
                PC = Pop(bus);
                _iff1 = _iff2;
                return 14;
            case 0x46:
            case 0x4E:
            case 0x66:
            case 0x6E:
                _interruptMode = 0;
                return 8;
            case 0x56:
            case 0x76:
                _interruptMode = 1;
                return 8;
            case 0x5E:
            case 0x7E:
                _interruptMode = 2;
                return 8;
            case 0x47:
                I = A;
                return 9;
            case 0x4F:
                R = A;
                return 9;
            case 0x57:
                A = I;
                SetAccumulatorFromInterruptRegisterFlags(A);
                return 9;
            case 0x5F:
                A = R;
                SetAccumulatorFromInterruptRegisterFlags(A);
                return 9;
            case 0x44:
            case 0x4C:
            case 0x54:
            case 0x5C:
            case 0x64:
            case 0x6C:
            case 0x74:
            case 0x7C:
                NegateAccumulator();
                return 8;
            case 0x42:
            case 0x52:
            case 0x62:
            case 0x72:
                SbcHl(ReadRegisterPair((opcode >> 4) & 0x03));
                return 15;
            case 0x4A:
            case 0x5A:
            case 0x6A:
            case 0x7A:
                AdcHl(ReadRegisterPair((opcode >> 4) & 0x03));
                return 15;
            case 0x43:
            case 0x53:
            case 0x63:
            case 0x73:
            {
                ushort address = FetchWord(bus);
                ushort value = ReadRegisterPair((opcode >> 4) & 0x03);
                bus.WriteByte(address, (byte)value);
                bus.WriteByte((ushort)(address + 1), (byte)(value >> 8));
                return 20;
            }
            case 0x4B:
            case 0x5B:
            case 0x6B:
            case 0x7B:
            {
                ushort address = FetchWord(bus);
                WriteRegisterPair((opcode >> 4) & 0x03, (ushort)(bus.ReadByte(address) | (bus.ReadByte((ushort)(address + 1)) << 8)));
                return 20;
            }
            case 0xA0:
                return BlockTransfer(bus, decrement: false, repeat: false);
            case 0xA8:
                return BlockTransfer(bus, decrement: true, repeat: false);
            case 0xB0:
                return BlockTransfer(bus, decrement: false, repeat: true);
            case 0xB8:
                return BlockTransfer(bus, decrement: true, repeat: true);
            default:
                Halted = true;
                return 8;
        }
    }

    private int ExecuteIndex(bool ix, IZ80Bus bus)
    {
        byte opcode = Fetch(bus);
        ushort index = ix ? IX : IY;
        void StoreIndex(ushort value)
        {
            if (ix)
            {
                IX = value;
            }
            else
            {
                IY = value;
            }
        }

        byte ReadIndexedRegister(int register)
        {
            return register switch
            {
                0 => B,
                1 => C,
                2 => D,
                3 => E,
                4 => (byte)(index >> 8),
                5 => (byte)index,
                7 => A,
                _ => throw new InvalidOperationException("Indexed memory register requires a displacement."),
            };
        }

        void WriteIndexedRegister(int register, byte value)
        {
            switch (register)
            {
                case 0:
                    B = value;
                    break;
                case 1:
                    C = value;
                    break;
                case 2:
                    D = value;
                    break;
                case 3:
                    E = value;
                    break;
                case 4:
                    index = (ushort)((index & 0x00FF) | (value << 8));
                    StoreIndex(index);
                    break;
                case 5:
                    index = (ushort)((index & 0xFF00) | value);
                    StoreIndex(index);
                    break;
                case 7:
                    A = value;
                    break;
                default:
                    throw new InvalidOperationException("Indexed memory register requires a displacement.");
            }
        }

        void ApplyAlu(byte value, int operation)
        {
            switch (operation)
            {
                case 0: Add(value); break;
                case 1: AddWithCarry(value); break;
                case 2: Subtract(value); break;
                case 3: SubtractWithCarry(value); break;
                case 4: A &= value; SetLogicFlags(A, halfCarry: true); break;
                case 5: A ^= value; SetLogicFlags(A); break;
                case 6: A |= value; SetLogicFlags(A); break;
                case 7: Compare(value); break;
            }
        }

        ushort IndexedAddress()
        {
            return unchecked((ushort)(index + (sbyte)Fetch(bus)));
        }

        switch (opcode)
        {
            case 0x21:
                StoreIndex(FetchWord(bus));
                return 14;
            case 0x22:
            {
                ushort address = FetchWord(bus);
                bus.WriteByte(address, (byte)index);
                bus.WriteByte((ushort)(address + 1), (byte)(index >> 8));
                return 20;
            }
            case 0x2A:
            {
                ushort address = FetchWord(bus);
                StoreIndex((ushort)(bus.ReadByte(address) | (bus.ReadByte((ushort)(address + 1)) << 8)));
                return 20;
            }
            case 0x23:
                StoreIndex((ushort)(index + 1));
                return 10;
            case 0x2B:
                StoreIndex((ushort)(index - 1));
                return 10;
            case 0x09:
                StoreIndex(AddIndex(index, BC));
                return 15;
            case 0x19:
                StoreIndex(AddIndex(index, DE));
                return 15;
            case 0x29:
                StoreIndex(AddIndex(index, index));
                return 15;
            case 0x39:
                StoreIndex(AddIndex(index, SP));
                return 15;
            case 0xE1:
                StoreIndex(Pop(bus));
                return 14;
            case 0xE3:
            {
                ushort value = (ushort)(bus.ReadByte(SP) | (bus.ReadByte((ushort)(SP + 1)) << 8));
                bus.WriteByte(SP, (byte)index);
                bus.WriteByte((ushort)(SP + 1), (byte)(index >> 8));
                StoreIndex(value);
                return 23;
            }
            case 0xE5:
                Push(bus, index);
                return 15;
            case 0xE9:
                PC = index;
                return 8;
            case 0xF9:
                SP = index;
                return 10;
            case 0x34:
            {
                ushort address = IndexedAddress();
                byte value = (byte)(bus.ReadByte(address) + 1);
                bus.WriteByte(address, value);
                SetIncDecFlags(value);
                return 23;
            }
            case 0x35:
            {
                ushort address = IndexedAddress();
                byte value = (byte)(bus.ReadByte(address) - 1);
                bus.WriteByte(address, value);
                SetIncDecFlags(value, subtract: true);
                return 23;
            }
            case 0x36:
            {
                ushort address = IndexedAddress();
                bus.WriteByte(address, Fetch(bus));
                return 19;
            }
            case 0xCB:
                return ExecuteIndexCb(index, bus);
            default:
                if (opcode == 0x76)
                {
                    Halted = true;
                    return 8;
                }

                if ((opcode & 0xC7) == 0x04)
                {
                    int register = (opcode >> 3) & 0x07;
                    if (register == 6)
                    {
                        ushort address = IndexedAddress();
                        byte value = (byte)(bus.ReadByte(address) + 1);
                        bus.WriteByte(address, value);
                        SetIncDecFlags(value);
                        return 23;
                    }

                    byte registerValue = (byte)(ReadIndexedRegister(register) + 1);
                    WriteIndexedRegister(register, registerValue);
                    SetIncDecFlags(registerValue);
                    return 8;
                }

                if ((opcode & 0xC7) == 0x05)
                {
                    int register = (opcode >> 3) & 0x07;
                    if (register == 6)
                    {
                        ushort address = IndexedAddress();
                        byte value = (byte)(bus.ReadByte(address) - 1);
                        bus.WriteByte(address, value);
                        SetIncDecFlags(value, subtract: true);
                        return 23;
                    }

                    byte registerValue = (byte)(ReadIndexedRegister(register) - 1);
                    WriteIndexedRegister(register, registerValue);
                    SetIncDecFlags(registerValue, subtract: true);
                    return 8;
                }

                if ((opcode & 0xC7) == 0x06)
                {
                    int register = (opcode >> 3) & 0x07;
                    if (register == 6)
                    {
                        ushort address = IndexedAddress();
                        bus.WriteByte(address, Fetch(bus));
                        return 19;
                    }

                    WriteIndexedRegister(register, Fetch(bus));
                    return 11;
                }

                if (IsLoadRegisterFromIndexed(opcode))
                {
                    WriteRegister((opcode >> 3) & 0x07, bus.ReadByte(IndexedAddress()), bus);
                    return 19;
                }

                if (IsLoadIndexedFromRegister(opcode))
                {
                    ushort address = IndexedAddress();
                    bus.WriteByte(address, ReadRegister(opcode & 0x07, bus));
                    return 19;
                }

                if ((opcode & 0xC0) == 0x40)
                {
                    int destination = (opcode >> 3) & 0x07;
                    int source = opcode & 0x07;
                    WriteIndexedRegister(destination, ReadIndexedRegister(source));
                    return 8;
                }

                if ((opcode & 0xC0) == 0x80)
                {
                    int source = opcode & 0x07;
                    byte value = source == 6 ? bus.ReadByte(IndexedAddress()) : ReadIndexedRegister(source);
                    ApplyAlu(value, (opcode >> 3) & 0x07);
                    return source == 6 ? 19 : 8;
                }

                if ((opcode & 0xC7) == 0x86)
                {
                    byte value = bus.ReadByte(IndexedAddress());
                    ApplyAlu(value, (opcode >> 3) & 0x07);
                    return 19;
                }

                Halted = true;
                return 8;
        }
    }

    private int ExecuteIndexCb(ushort index, IZ80Bus bus)
    {
        ushort address = unchecked((ushort)(index + (sbyte)Fetch(bus)));
        byte opcode = Fetch(bus);
        int operation = opcode >> 6;
        int bit = (opcode >> 3) & 0x07;
        int register = opcode & 0x07;
        byte value = bus.ReadByte(address);

        if (operation == 0)
        {
            byte result = bit switch
            {
                0 => Rotate(value, left: true, throughCarry: false),
                1 => Rotate(value, left: false, throughCarry: false),
                2 => Rotate(value, left: true, throughCarry: true),
                3 => Rotate(value, left: false, throughCarry: true),
                4 => ShiftLeftArithmetic(value),
                5 => ShiftRightArithmetic(value),
                6 => ShiftLeftArithmetic(value),
                _ => ShiftRightLogical(value),
            };
            bus.WriteByte(address, result);
            if (register != 6)
            {
                WriteRegister(register, result, bus);
            }

            return 23;
        }

        if (operation == 1)
        {
            TestBit(value, bit);
            return 20;
        }

        byte mask = (byte)(1 << bit);
        byte updated = operation == 2 ? (byte)(value & ~mask) : (byte)(value | mask);
        bus.WriteByte(address, updated);
        if (register != 6)
        {
            WriteRegister(register, updated, bus);
        }

        return 23;
    }

    private ushort AddIndex(ushort left, ushort right)
    {
        int result = left + right;
        F = (byte)(F & (FlagS | FlagZ | FlagP));
        if (((left ^ right ^ result) & 0x1000) != 0) F |= FlagH;
        if (result > 0xFFFF) F |= FlagC;
        return (ushort)result;
    }

    private int BlockTransfer(IZ80Bus bus, bool decrement, bool repeat)
    {
        bus.WriteByte(DE, bus.ReadByte(HL));
        HL = (ushort)(HL + (decrement ? -1 : 1));
        DE = (ushort)(DE + (decrement ? -1 : 1));
        BC--;

        F = (byte)(F & (FlagS | FlagZ | FlagC));
        if (BC != 0)
        {
            F |= FlagP;
            if (repeat)
            {
                PC -= 2;
                return 21;
            }
        }

        return 16;
    }

    private ushort ReadRegisterPair(int pair)
    {
        return pair switch
        {
            0 => BC,
            1 => DE,
            2 => HL,
            _ => SP,
        };
    }

    private void WriteRegisterPair(int pair, ushort value)
    {
        switch (pair)
        {
            case 0: BC = value; break;
            case 1: DE = value; break;
            case 2: HL = value; break;
            default: SP = value; break;
        }
    }

    private void AdcHl(ushort value)
    {
        int carry = (F & FlagC) != 0 ? 1 : 0;
        int result = HL + value + carry;
        SetAdd16Flags(HL, value, result);
        HL = (ushort)result;
    }

    private void SbcHl(ushort value)
    {
        int carry = (F & FlagC) != 0 ? 1 : 0;
        int result = HL - value - carry;
        SetSub16Flags(HL, value, result);
        HL = (ushort)result;
    }

    private static bool IsLoadRegisterFromIndexed(byte opcode)
    {
        return opcode is 0x46 or 0x4E or 0x56 or 0x5E or 0x66 or 0x6E or 0x7E;
    }

    private static bool IsLoadIndexedFromRegister(byte opcode)
    {
        return opcode is 0x70 or 0x71 or 0x72 or 0x73 or 0x74 or 0x75 or 0x77;
    }

    private bool TestCondition(int condition)
    {
        return condition switch
        {
            0 => (F & FlagZ) == 0,
            1 => (F & FlagZ) != 0,
            2 => (F & FlagC) == 0,
            3 => (F & FlagC) != 0,
            4 => (F & FlagP) == 0,
            5 => (F & FlagP) != 0,
            6 => (F & FlagS) == 0,
            _ => (F & FlagS) != 0,
        };
    }

    private void Add(byte value)
    {
        int result = A + value;
        F = 0;
        if ((result & 0xFF) == 0) F |= FlagZ;
        if ((result & 0x80) != 0) F |= FlagS;
        if (((A ^ value ^ result) & 0x10) != 0) F |= FlagH;
        if (((~(A ^ value) & (A ^ result)) & 0x80) != 0) F |= FlagP;
        if (result > 0xFF) F |= FlagC;
        A = (byte)result;
    }

    private void AddWithCarry(byte value)
    {
        int carry = (F & FlagC) != 0 ? 1 : 0;
        int result = A + value + carry;
        F = 0;
        if ((result & 0xFF) == 0) F |= FlagZ;
        if ((result & 0x80) != 0) F |= FlagS;
        if (((A ^ value ^ result) & 0x10) != 0) F |= FlagH;
        if (((~(A ^ value) & (A ^ result)) & 0x80) != 0) F |= FlagP;
        if (result > 0xFF) F |= FlagC;
        A = (byte)result;
    }

    private void Subtract(byte value)
    {
        int result = A - value;
        F = FlagN;
        if ((result & 0xFF) == 0) F |= FlagZ;
        if ((result & 0x80) != 0) F |= FlagS;
        if (((A ^ value ^ result) & 0x10) != 0) F |= FlagH;
        if ((((A ^ value) & (A ^ result)) & 0x80) != 0) F |= FlagP;
        if (result < 0) F |= FlagC;
        A = (byte)result;
    }

    private void SubtractWithCarry(byte value)
    {
        int carry = (F & FlagC) != 0 ? 1 : 0;
        int result = A - value - carry;
        F = FlagN;
        if ((result & 0xFF) == 0) F |= FlagZ;
        if ((result & 0x80) != 0) F |= FlagS;
        if (((A ^ value ^ result) & 0x10) != 0) F |= FlagH;
        if ((((A ^ value) & (A ^ result)) & 0x80) != 0) F |= FlagP;
        if (result < 0) F |= FlagC;
        A = (byte)result;
    }

    private void Compare(byte value)
    {
        byte old = A;
        Subtract(value);
        A = old;
    }

    private void NegateAccumulator()
    {
        byte original = A;
        int result = -original;
        A = (byte)result;
        F = FlagN;
        if (A == 0) F |= FlagZ;
        if ((A & 0x80) != 0) F |= FlagS;
        if ((original & 0x0F) != 0) F |= FlagH;
        if (original == 0x80) F |= FlagP;
        if (original != 0) F |= FlagC;
    }

    private void DecimalAdjustAccumulator()
    {
        int correction = 0;
        bool subtract = (F & FlagN) != 0;
        bool carry = (F & FlagC) != 0;
        bool halfCarry = (F & FlagH) != 0;

        if (halfCarry || (!subtract && (A & 0x0F) > 9))
        {
            correction |= 0x06;
        }

        if (carry || (!subtract && A > 0x99))
        {
            correction |= 0x60;
            carry = true;
        }

        A = (byte)(subtract ? A - correction : A + correction);
        F = (byte)(subtract ? FlagN : 0);
        if (A == 0) F |= FlagZ;
        if ((A & 0x80) != 0) F |= FlagS;
        if (Parity(A)) F |= FlagP;
        if (carry) F |= FlagC;
    }

    private void AddHl(ushort value)
    {
        int result = HL + value;
        F = (byte)(F & (FlagS | FlagZ | FlagP));
        if (((HL ^ value ^ result) & 0x1000) != 0) F |= FlagH;
        if (result > 0xFFFF) F |= FlagC;
        HL = (ushort)result;
    }

    private void SetAdd16Flags(ushort destination, ushort source, int result)
    {
        ushort value = (ushort)result;
        F = 0;
        if (value == 0) F |= FlagZ;
        if ((value & 0x8000) != 0) F |= FlagS;
        if (((destination ^ source ^ result) & 0x1000) != 0) F |= FlagH;
        if (((~(destination ^ source) & (destination ^ result)) & 0x8000) != 0) F |= FlagP;
        if (result > 0xFFFF) F |= FlagC;
    }

    private void SetSub16Flags(ushort destination, ushort source, int result)
    {
        ushort value = (ushort)result;
        F = FlagN;
        if (value == 0) F |= FlagZ;
        if ((value & 0x8000) != 0) F |= FlagS;
        if (((destination ^ source ^ result) & 0x1000) != 0) F |= FlagH;
        if ((((destination ^ source) & (destination ^ result)) & 0x8000) != 0) F |= FlagP;
        if (result < 0) F |= FlagC;
    }

    private void RotateAccumulator(bool left, bool throughCarry)
    {
        byte result = Rotate(A, left, throughCarry);
        bool carry = (F & FlagC) != 0;
        A = result;
        F = (byte)((F & (FlagS | FlagZ | FlagP)) | (carry ? FlagC : 0));
    }

    private byte Rotate(byte value, bool left, bool throughCarry)
    {
        int carryIn = throughCarry && (F & FlagC) != 0 ? 1 : 0;
        bool carryOut = left ? (value & 0x80) != 0 : (value & 0x01) != 0;
        byte result = left
            ? (byte)((value << 1) | (throughCarry ? carryIn : (carryOut ? 1 : 0)))
            : (byte)((value >> 1) | (throughCarry ? carryIn << 7 : (carryOut ? 0x80 : 0)));
        SetLogicFlags(result);
        if (carryOut) F |= FlagC;
        return result;
    }

    private byte ShiftLeftArithmetic(byte value)
    {
        bool carry = (value & 0x80) != 0;
        byte result = (byte)(value << 1);
        SetLogicFlags(result);
        if (carry) F |= FlagC;
        return result;
    }

    private byte ShiftRightArithmetic(byte value)
    {
        bool carry = (value & 0x01) != 0;
        byte result = (byte)((value >> 1) | (value & 0x80));
        SetLogicFlags(result);
        if (carry) F |= FlagC;
        return result;
    }

    private byte ShiftRightLogical(byte value)
    {
        bool carry = (value & 0x01) != 0;
        byte result = (byte)(value >> 1);
        SetLogicFlags(result);
        if (carry) F |= FlagC;
        return result;
    }

    private void TestBit(byte value, int bit)
    {
        byte carry = (byte)(F & FlagC);
        F = (byte)(carry | FlagH);
        if ((value & (1 << bit)) == 0) F |= FlagZ | FlagP;
        if (bit == 7 && (value & 0x80) != 0) F |= FlagS;
    }

    private void SetLogicFlags(byte value, bool halfCarry = false)
    {
        F = 0;
        if (halfCarry) F |= FlagH;
        if (value == 0) F |= FlagZ;
        if ((value & 0x80) != 0) F |= FlagS;
        if (Parity(value)) F |= FlagP;
    }

    private void SetAccumulatorFromInterruptRegisterFlags(byte value)
    {
        F = (byte)(F & FlagC);
        if (value == 0) F |= FlagZ;
        if ((value & 0x80) != 0) F |= FlagS;
        if (_iff2) F |= FlagP;
    }

    private void SetIncDecFlags(byte value, bool subtract = false)
    {
        F = (byte)(F & FlagC);
        if (subtract) F |= FlagN;
        if (value == 0) F |= FlagZ;
        if ((value & 0x80) != 0) F |= FlagS;
        if (value == (subtract ? 0x7F : 0x80)) F |= FlagP;
    }

    private static bool Parity(byte value)
    {
        value ^= (byte)(value >> 4);
        value &= 0x0F;
        return ((0x6996 >> value) & 1) == 0;
    }

    public Z80State CaptureState()
    {
        return new Z80State(A, F, B, C, D, E, H, L, AlternateA, AlternateF, AlternateB, AlternateC, AlternateD, AlternateE, AlternateH, AlternateL, I, R, IX, IY, PC, SP, Cycles, ResetAsserted, BusRequested, Halted, _iff1, _iff2, _interruptEnableDelay, _interruptMode);
    }

    public void RestoreState(Z80State state)
    {
        A = state.A;
        F = state.F;
        B = state.B;
        C = state.C;
        D = state.D;
        E = state.E;
        H = state.H;
        L = state.L;
        AlternateA = state.AlternateA;
        AlternateF = state.AlternateF;
        AlternateB = state.AlternateB;
        AlternateC = state.AlternateC;
        AlternateD = state.AlternateD;
        AlternateE = state.AlternateE;
        AlternateH = state.AlternateH;
        AlternateL = state.AlternateL;
        I = state.I;
        R = state.R;
        IX = state.IX;
        IY = state.IY;
        PC = state.PC;
        SP = state.SP;
        Cycles = state.Cycles;
        ResetAsserted = state.ResetAsserted;
        BusRequested = state.BusRequested;
        Halted = state.Halted;
        _iff1 = state.Iff1;
        _iff2 = state.Iff2;
        _interruptEnableDelay = state.InterruptEnableDelay;
        _interruptMode = state.InterruptMode;
    }

    private sealed class DelegateZ80Bus(Func<ushort, byte> read, Action<ushort, byte> write) : IZ80Bus
    {
        public byte ReadByte(ushort address)
        {
            return read(address);
        }

        public void WriteByte(ushort address, byte value)
        {
            write(address, value);
        }
    }

    public sealed record Z80State(byte A, byte F, byte B, byte C, byte D, byte E, byte H, byte L, byte AlternateA, byte AlternateF, byte AlternateB, byte AlternateC, byte AlternateD, byte AlternateE, byte AlternateH, byte AlternateL, byte I, byte R, ushort IX, ushort IY, ushort PC, ushort SP, long Cycles, bool ResetAsserted, bool BusRequested, bool Halted, bool Iff1, bool Iff2, int InterruptEnableDelay, byte InterruptMode);
}
