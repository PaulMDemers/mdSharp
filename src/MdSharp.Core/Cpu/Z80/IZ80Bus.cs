namespace MdSharp.Core.Cpu.Z80;

public interface IZ80Bus
{
    byte ReadByte(ushort address);
    void WriteByte(ushort address, byte value);
}
