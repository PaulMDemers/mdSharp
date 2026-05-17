namespace MdSharp.Core.Input;

public sealed class SegaTeamPlayerAdapter
{
    private readonly ThreeButtonController[] _controllers;
    private byte _state = 0x60;
    private int _counter;

    public SegaTeamPlayerAdapter(ThreeButtonController[] controllers)
    {
        _controllers = controllers;
    }

    public byte ReadData()
    {
        int trAsTl = (_state & 0x20) >> 1;
        int value = _counter switch
        {
            0 => trAsTl | 0x03,
            1 => trAsTl | 0x0F,
            2 or 3 => trAsTl,
            >= 4 and <= 7 => trAsTl | PadType(_counter - 4),
            _ => trAsTl | ReadPadNibble(_counter - 8),
        };

        return (byte)(value & 0x3F);
    }

    public void WriteData(byte data, byte mask)
    {
        byte next = (byte)((_state & ~mask) | (data & mask));
        if ((next & 0x40) != 0)
        {
            _counter = 0;
        }
        else if (((_state ^ next) & 0x60) != 0)
        {
            _counter++;
        }

        _state = next;
    }

    public void Reset()
    {
        _state = 0x60;
        _counter = 0;
    }

    private int PadType(int controllerIndex)
    {
        if ((uint)controllerIndex >= _controllers.Length)
        {
            return 0x0F;
        }

        return _controllers[controllerIndex].SixButtonEnabled ? 0x01 : 0x00;
    }

    private int ReadPadNibble(int tableIndex)
    {
        (int controllerIndex, int phase) = ReadTableEntry(tableIndex);
        if (controllerIndex < 0)
        {
            return 0x0F;
        }

        GenesisButton pressed = _controllers[controllerIndex].Pressed;
        int value = 0x0F;
        switch (phase)
        {
            case 0:
                if (IsPressed(pressed, GenesisButton.Up)) value &= ~0x01;
                if (IsPressed(pressed, GenesisButton.Down)) value &= ~0x02;
                if (IsPressed(pressed, GenesisButton.Left)) value &= ~0x04;
                if (IsPressed(pressed, GenesisButton.Right)) value &= ~0x08;
                break;
            case 1:
                if (IsPressed(pressed, GenesisButton.B)) value &= ~0x01;
                if (IsPressed(pressed, GenesisButton.C)) value &= ~0x02;
                if (IsPressed(pressed, GenesisButton.A)) value &= ~0x04;
                if (IsPressed(pressed, GenesisButton.Start)) value &= ~0x08;
                break;
            default:
                if (IsPressed(pressed, GenesisButton.Z)) value &= ~0x01;
                if (IsPressed(pressed, GenesisButton.Y)) value &= ~0x02;
                if (IsPressed(pressed, GenesisButton.X)) value &= ~0x04;
                if (IsPressed(pressed, GenesisButton.Mode)) value &= ~0x08;
                break;
        }

        return value;
    }

    private (int ControllerIndex, int Phase) ReadTableEntry(int tableIndex)
    {
        int index = 0;
        for (int controllerIndex = 0; controllerIndex < _controllers.Length; controllerIndex++)
        {
            int phaseCount = _controllers[controllerIndex].SixButtonEnabled ? 3 : 2;
            for (int phase = 0; phase < phaseCount; phase++)
            {
                if (index == tableIndex)
                {
                    return (controllerIndex, phase);
                }

                index++;
            }
        }

        return (-1, -1);
    }

    private static bool IsPressed(GenesisButton pressed, GenesisButton button)
    {
        return (pressed & button) != 0;
    }
}
