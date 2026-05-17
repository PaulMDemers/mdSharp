namespace MdSharp.Core.Bus;

public interface IMemoryBus
{
    byte ReadByte(uint address);
    void WriteByte(uint address, byte value);

    ushort ReadWord(uint address)
    {
        uint normalized = address & 0x00FF_FFFF;
        return (ushort)((ReadByte(normalized) << 8) | ReadByte(normalized + 1));
    }

    uint ReadLong(uint address)
    {
        return (uint)((ReadWord(address) << 16) | ReadWord(address + 2));
    }

    void WriteWord(uint address, ushort value)
    {
        uint normalized = address & 0x00FF_FFFF;
        WriteByte(normalized, (byte)(value >> 8));
        WriteByte(normalized + 1, (byte)value);
    }

    void WriteLong(uint address, uint value)
    {
        WriteWord(address, (ushort)(value >> 16));
        WriteWord(address + 2, (ushort)value);
    }
}
