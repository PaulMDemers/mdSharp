namespace MdSharp.Core.Input;

public sealed class ThreeButtonController
{
    private const long SixButtonHandshakeResetMasterCycles = 100_000;

    private bool _thHigh = true;
    private int _sixButtonStep;
    private long _lastWriteMasterCycle;

    public GenesisButton Pressed { get; set; }
    public bool SixButtonEnabled { get; set; }

    public byte ReadData()
    {
        if (SixButtonEnabled)
        {
            if (!_thHigh && _sixButtonStep >= 6)
            {
                return ReadSixButtonSignature();
            }

            if (_thHigh && _sixButtonStep >= 7)
            {
                return ReadSixButtonExtraButtons();
            }
        }

        byte value = 0x3F;

        if (IsPressed(GenesisButton.Up))
        {
            value &= unchecked((byte)~0x01);
        }

        if (IsPressed(GenesisButton.Down))
        {
            value &= unchecked((byte)~0x02);
        }

        if (_thHigh)
        {
            if (IsPressed(GenesisButton.Left))
            {
                value &= unchecked((byte)~0x04);
            }

            if (IsPressed(GenesisButton.Right))
            {
                value &= unchecked((byte)~0x08);
            }

            if (IsPressed(GenesisButton.B))
            {
                value &= unchecked((byte)~0x10);
            }

            if (IsPressed(GenesisButton.C))
            {
                value &= unchecked((byte)~0x20);
            }
        }
        else
        {
            value &= unchecked((byte)~0x0C);

            if (IsPressed(GenesisButton.A))
            {
                value &= unchecked((byte)~0x10);
            }

            if (IsPressed(GenesisButton.Start))
            {
                value &= unchecked((byte)~0x20);
            }
        }

        return (byte)(value | (_thHigh ? 0x40 : 0x00));
    }

    public void WriteData(byte value, long masterCycle = 0)
    {
        bool thHigh = (value & 0x40) != 0;
        if (SixButtonEnabled)
        {
            if (_lastWriteMasterCycle != 0 &&
                masterCycle != 0 &&
                masterCycle - _lastWriteMasterCycle > SixButtonHandshakeResetMasterCycles)
            {
                _sixButtonStep = 0;
            }

            AdvanceSixButtonHandshake(thHigh);
            _lastWriteMasterCycle = masterCycle;
        }
        else
        {
            _sixButtonStep = 0;
            _lastWriteMasterCycle = 0;
        }

        _thHigh = thHigh;
    }

    public void WriteControl(byte value)
    {
        _thHigh = (value & 0x40) != 0;
    }

    public void ResetProtocol()
    {
        _thHigh = true;
        _sixButtonStep = 0;
        _lastWriteMasterCycle = 0;
    }

    private void AdvanceSixButtonHandshake(bool thHigh)
    {
        if (_sixButtonStep == 0)
        {
            _sixButtonStep = thHigh ? 1 : 2;
            return;
        }

        bool expectedHigh = (_sixButtonStep & 1) == 0;
        if (thHigh == expectedHigh)
        {
            _sixButtonStep = Math.Min(_sixButtonStep + 1, 7);
            return;
        }

        _sixButtonStep = thHigh ? 1 : 2;
    }

    private byte ReadSixButtonSignature()
    {
        byte value = 0x30;
        if (IsPressed(GenesisButton.A))
        {
            value &= unchecked((byte)~0x10);
        }

        if (IsPressed(GenesisButton.Start))
        {
            value &= unchecked((byte)~0x20);
        }

        return value;
    }

    private byte ReadSixButtonExtraButtons()
    {
        byte value = 0x3F;
        if (IsPressed(GenesisButton.Z))
        {
            value &= unchecked((byte)~0x01);
        }

        if (IsPressed(GenesisButton.Y))
        {
            value &= unchecked((byte)~0x02);
        }

        if (IsPressed(GenesisButton.X))
        {
            value &= unchecked((byte)~0x04);
        }

        if (IsPressed(GenesisButton.Mode))
        {
            value &= unchecked((byte)~0x08);
        }

        if (IsPressed(GenesisButton.B))
        {
            value &= unchecked((byte)~0x10);
        }

        if (IsPressed(GenesisButton.C))
        {
            value &= unchecked((byte)~0x20);
        }

        return (byte)(value | 0x40);
    }

    private bool IsPressed(GenesisButton button)
    {
        return (Pressed & button) != 0;
    }
}
