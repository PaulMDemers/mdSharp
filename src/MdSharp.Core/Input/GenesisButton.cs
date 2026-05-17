namespace MdSharp.Core.Input;

[Flags]
public enum GenesisButton
{
    None = 0,
    Up = 1 << 0,
    Down = 1 << 1,
    Left = 1 << 2,
    Right = 1 << 3,
    A = 1 << 4,
    B = 1 << 5,
    C = 1 << 6,
    Start = 1 << 7,
    X = 1 << 8,
    Y = 1 << 9,
    Z = 1 << 10,
    Mode = 1 << 11,
}
