namespace MdSharp.Core.Cartridge;

internal sealed class SerialEeprom
{
    private readonly byte[] _memory;
    private readonly int _sizeMask;
    private readonly uint _sdaInAddress;
    private readonly int _sdaInBit;
    private readonly uint _sdaOutAddress;
    private readonly int _sdaOutBit;
    private readonly uint _sclAddress;
    private readonly int _sclBit;
    private readonly int _addressBytes;
    private readonly int _commandAddressBits;
    private readonly int _pageMask;

    private EepromPhase _phase;
    private EepromPhase _phaseAfterAck;
    private bool _ackClockSeen;
    private bool _masterAckClockSeen;
    private bool _lastSda = true;
    private bool _lastScl = true;
    private bool _sdaOut = true;
    private int _shift;
    private int _receivedBits;
    private int _address;
    private int _pendingAddress;
    private int _receivedAddressBytes;
    private int _writePageBase;
    private int _sendByte;
    private int _sendBit;

    public SerialEeprom(byte[] memory, int sizeMask, uint sdaInAddress, int sdaInBit, uint sdaOutAddress, int sdaOutBit, uint sclAddress, int sclBit, int addressBytes = 0, int commandAddressBits = 7, int pageMask = 0x03)
    {
        _memory = memory;
        _sizeMask = sizeMask;
        _sdaInAddress = sdaInAddress & 0x00FF_FFFF;
        _sdaInBit = sdaInBit;
        _sdaOutAddress = sdaOutAddress & 0x00FF_FFFF;
        _sdaOutBit = sdaOutBit;
        _sclAddress = sclAddress & 0x00FF_FFFF;
        _sclBit = sclBit;
        _addressBytes = addressBytes;
        _commandAddressBits = commandAddressBits;
        _pageMask = pageMask;
    }

    public int MemorySize => _sizeMask + 1;

    public bool HandlesAddress(uint address)
    {
        address &= 0x00FF_FFFF;
        return address == _sdaInAddress || address == _sdaOutAddress || address == _sclAddress;
    }

    public byte ReadByte(uint address, byte openBus = 0xFF)
    {
        address &= 0x00FF_FFFF;
        if (address != _sdaOutAddress)
        {
            return openBus;
        }

        byte mask = (byte)(1 << _sdaOutBit);
        return _sdaOut ? (byte)(openBus | mask) : (byte)(openBus & ~mask);
    }

    public void WriteByte(uint address, byte value)
    {
        address &= 0x00FF_FFFF;
        bool sda = address == _sdaInAddress ? (value & (1 << _sdaInBit)) != 0 : _lastSda;
        bool scl = address == _sclAddress ? (value & (1 << _sclBit)) != 0 : _lastScl;
        WriteLines(sda, scl);
    }

    public void WriteWord(uint address, ushort value)
    {
        address &= 0x00FF_FFFF;
        uint highAddress = address;
        uint lowAddress = (address + 1) & 0x00FF_FFFF;
        byte high = (byte)(value >> 8);
        byte low = (byte)value;
        bool sda = _lastSda;
        bool scl = _lastScl;
        if (highAddress == _sdaInAddress)
        {
            sda = (high & (1 << _sdaInBit)) != 0;
        }
        else if (lowAddress == _sdaInAddress)
        {
            sda = (low & (1 << _sdaInBit)) != 0;
        }

        if (highAddress == _sclAddress)
        {
            scl = (high & (1 << _sclBit)) != 0;
        }
        else if (lowAddress == _sclAddress)
        {
            scl = (low & (1 << _sclBit)) != 0;
        }

        WriteLines(sda, scl);
    }

    private void WriteLines(bool sda, bool scl)
    {
        if (_lastScl && scl && _lastSda && !sda)
        {
            Start();
        }
        else if (_lastScl && scl && !_lastSda && sda)
        {
            Stop();
        }
        else if (!_lastScl && scl)
        {
            RisingEdge(sda);
        }
        else if (_lastScl && !scl)
        {
            FallingEdge(sda);
        }

        _lastSda = sda;
        _lastScl = scl;
    }

    private void Start()
    {
        _phase = EepromPhase.ReceiveCommand;
        _phaseAfterAck = EepromPhase.Idle;
        _ackClockSeen = false;
        _masterAckClockSeen = false;
        _receivedBits = 0;
        _shift = 0;
        _sdaOut = true;
    }

    private void Stop()
    {
        _phase = EepromPhase.Idle;
        _phaseAfterAck = EepromPhase.Idle;
        _ackClockSeen = false;
        _masterAckClockSeen = false;
        _sdaOut = true;
    }

    private void RisingEdge(bool sda)
    {
        switch (_phase)
        {
            case EepromPhase.ReceiveCommand:
            case EepromPhase.ReceiveAddress:
            case EepromPhase.ReceiveData:
                ReceiveBit(sda);
                break;
            case EepromPhase.Ack:
                _ackClockSeen = true;
                _sdaOut = false;
                break;
            case EepromPhase.MasterAck:
                _masterAckClockSeen = true;
                break;
        }
    }

    private void FallingEdge(bool sda)
    {
        switch (_phase)
        {
            case EepromPhase.Ack when _ackClockSeen:
                _phase = _phaseAfterAck;
                _ackClockSeen = false;
                _sdaOut = true;
                if (_phase == EepromPhase.SendData)
                {
                    PrepareSendByte();
                }

                break;
            case EepromPhase.SendData:
                AdvanceSendBit();
                break;
            case EepromPhase.MasterAck when _masterAckClockSeen:
                _masterAckClockSeen = false;
                if (sda)
                {
                    _phase = EepromPhase.Idle;
                    _sdaOut = true;
                }
                else
                {
                    _address = (_address + 1) & _sizeMask;
                    PrepareSendByte();
                }

                break;
        }
    }

    private void ReceiveBit(bool sda)
    {
        _shift = ((_shift << 1) | (sda ? 1 : 0)) & 0xFF;
        _receivedBits++;
        if (_receivedBits < 8)
        {
            return;
        }

        byte value = (byte)_shift;
        _receivedBits = 0;
        _shift = 0;

        if (_phase == EepromPhase.ReceiveCommand)
        {
            ReceiveCommand(value);
        }
        else if (_phase == EepromPhase.ReceiveAddress)
        {
            ReceiveAddress(value);
        }
        else
        {
            _memory[_address & _sizeMask] = value;
            AdvanceWriteAddress();
            _phaseAfterAck = EepromPhase.ReceiveData;
        }

        _phase = EepromPhase.Ack;
        _ackClockSeen = false;
        _sdaOut = false;
    }

    private void ReceiveCommand(byte value)
    {
        bool read = (value & 1) != 0;
        int commandAddress = _commandAddressBits <= 0 ? 0 : (value >> 1) & ((1 << _commandAddressBits) - 1);
        if (read)
        {
            if (_addressBytes == 0)
            {
                _address = commandAddress & _sizeMask;
            }

            _phaseAfterAck = EepromPhase.SendData;
            return;
        }

        if (_addressBytes == 0)
        {
            _address = commandAddress & _sizeMask;
            _writePageBase = _address & ~_pageMask;
            _phaseAfterAck = EepromPhase.ReceiveData;
            return;
        }

        _pendingAddress = commandAddress;
        _receivedAddressBytes = 0;
        _phaseAfterAck = EepromPhase.ReceiveAddress;
    }

    private void ReceiveAddress(byte value)
    {
        _pendingAddress = ((_pendingAddress << 8) | value) & _sizeMask;
        _receivedAddressBytes++;
        if (_receivedAddressBytes < _addressBytes)
        {
            _phaseAfterAck = EepromPhase.ReceiveAddress;
            return;
        }

        _address = _pendingAddress & _sizeMask;
        _writePageBase = _address & ~_pageMask;
        _phaseAfterAck = EepromPhase.ReceiveData;
    }

    private void AdvanceWriteAddress()
    {
        if (_pageMask > 0)
        {
            _address = _writePageBase | ((_address + 1) & _pageMask);
            _address &= _sizeMask;
            return;
        }

        _address = (_address + 1) & _sizeMask;
    }

    private void PrepareSendByte()
    {
        _sendByte = _memory[_address & _sizeMask];
        _sendBit = 7;
        _phase = EepromPhase.SendData;
        _sdaOut = ((_sendByte >> _sendBit) & 1) != 0;
    }

    private void AdvanceSendBit()
    {
        _sendBit--;
        if (_sendBit >= 0)
        {
            _sdaOut = ((_sendByte >> _sendBit) & 1) != 0;
            return;
        }

        _phase = EepromPhase.MasterAck;
        _sdaOut = true;
    }

    private enum EepromPhase
    {
        Idle,
        ReceiveCommand,
        ReceiveAddress,
        ReceiveData,
        Ack,
        SendData,
        MasterAck,
    }
}
