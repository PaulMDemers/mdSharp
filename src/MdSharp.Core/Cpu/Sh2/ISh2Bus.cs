namespace MdSharp.Core.Cpu.Sh2;

public interface ISh2Bus
{
    byte ReadByte(uint address);
    ushort ReadWord(uint address);
    uint ReadLong(uint address);
    void WriteByte(uint address, byte value);
    void WriteWord(uint address, ushort value);
    void WriteLong(uint address, uint value);
}

public interface ISh2WaitStateBus
{
    int ConsumeWaitCycles();
}

public interface ISh2PeekBus
{
    bool TryPeekByte(uint address, out byte value);
    bool TryPeekWord(uint address, out ushort value);
}
