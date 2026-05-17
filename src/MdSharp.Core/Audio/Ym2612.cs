namespace MdSharp.Core.Audio;

public sealed class Ym2612
{
    private const int DefaultClockHz = 7_670_454;
    private const int BusyMasterCycles = 128;
    private const double FmOutputScale = 5_792.0;
    private const int FmOutputMin = -8_192;
    private const int FmOutputMax = 8_191;
    private const double OperatorOutputScale = 8_191.0;
    private const int OperatorMuteAttenuation = 0x340;
    private const double AttenuationStepDb = 0.09375;
    private const int DacOutputScale = 85;
    private static readonly double DacLowPassCutoffHz = Math.Max(100.0, ReadTuning("MDSHARP_YM_DAC_LOW_PASS_HZ", 6000.0));
    private static readonly double DacHighPassCutoffHz = Math.Max(0.0, ReadTuning("MDSHARP_YM_DAC_HIGH_PASS_HZ", 0.0));
    private static readonly double PhaseModulationScale = Math.Tau * ReadTuning("MDSHARP_YM_PHASE_MOD_SCALE", 4.0);
    private static readonly double PhaseModulationSoftLimit = ReadTuning("MDSHARP_YM_PHASE_MOD_SOFT_LIMIT", 2.5);
    private static readonly double FeedbackShift = ReadTuning("MDSHARP_YM_FEEDBACK_SHIFT", 8.0);
    private static readonly double AttackScale = ReadTuning("MDSHARP_YM_ATTACK_SCALE", 1.25);
    private static readonly double DecayScale = ReadTuning("MDSHARP_YM_DECAY_SCALE", 1.0);
    private static readonly double SustainScale = ReadTuning("MDSHARP_YM_SUSTAIN_SCALE", 1.0);
    private static readonly double ReleaseScale = ReadTuning("MDSHARP_YM_RELEASE_SCALE", 1.0);
    private static readonly double AttackCurveDivisor = Math.Max(1.0, ReadTuning("MDSHARP_YM_ATTACK_CURVE_DIVISOR", 128.0));
    private static readonly bool UseTableOperatorOutput = ReadBoolTuning("MDSHARP_YM_TABLE_OUTPUT", false);
    private static readonly int[] LogSineAttenuationTable = BuildLogSineAttenuationTable();
    private static readonly double[] AttenuationAmplitudeTable = BuildAttenuationAmplitudeTable();
    private static readonly double[] LfoFrequencies = [3.98, 5.56, 6.02, 6.37, 6.88, 9.63, 48.1, 72.2];
    private static readonly double[] PmsSemitoneDepth = [0.0, 0.034, 0.067, 0.10, 0.14, 0.20, 0.40, 0.80];
    private static readonly double[] AmsDbDepth = [0.0, 1.4, 5.9, 11.8];
    private static readonly int[,] DetuneTable =
    {
        { 0, 0, 1, 2 }, { 0, 0, 1, 2 }, { 0, 0, 1, 2 }, { 0, 0, 1, 2 },
        { 0, 1, 2, 2 }, { 0, 1, 2, 3 }, { 0, 1, 2, 3 }, { 0, 1, 2, 3 },
        { 0, 1, 2, 4 }, { 0, 1, 3, 4 }, { 0, 1, 3, 4 }, { 0, 1, 3, 5 },
        { 0, 2, 4, 5 }, { 0, 2, 4, 6 }, { 0, 2, 4, 6 }, { 0, 2, 5, 7 },
        { 0, 2, 5, 8 }, { 0, 3, 6, 8 }, { 0, 3, 6, 9 }, { 0, 3, 7, 10 },
        { 0, 4, 8, 11 }, { 0, 4, 8, 12 }, { 0, 4, 9, 13 }, { 0, 5, 10, 14 },
        { 0, 5, 11, 16 }, { 0, 6, 12, 17 }, { 0, 6, 13, 19 }, { 0, 7, 14, 20 },
        { 0, 8, 16, 22 }, { 0, 8, 16, 22 }, { 0, 8, 16, 22 }, { 0, 8, 16, 22 },
    };

    private readonly byte[,] _registers = new byte[2, 256];
    private readonly byte[] _selected = new byte[2];
    private int _timerACounter;
    private int _timerBCounter;
    private byte _status;
    private byte _dacSample;
    private bool _dacEnabled;
    private double _dacFilteredSample;
    private double _dacHighPassInput;
    private double _dacHighPassOutput;
    private readonly byte[] _keyOn = new byte[6];
    private readonly int[] _channelFNumbers = new int[6];
    private readonly int[] _channelBlocks = new int[6];
    private readonly int[] _channel3SpecialFNumbers = new int[3];
    private readonly int[] _channel3SpecialBlocks = new int[3];
    private readonly uint[] _phase = new uint[24];
    private readonly int[] _operatorEnvelope = new int[24];
    private readonly double[] _operatorEnvelopeRemainder = new double[24];
    private readonly byte[] _operatorStage = new byte[24];
    private readonly bool[] _ssgInverted = new bool[24];
    private readonly bool[] _ssgHolding = new bool[24];
    private readonly double[] _feedback = new double[6];
    private readonly double[] _feedbackPrevious = new double[6];
    private readonly double[] _algorithmMemory = new double[6];
    private readonly byte[,] _audioInitialRegisters = new byte[2, 256];
    private readonly byte[] _audioInitialSelected = new byte[2];
    private readonly byte[] _audioInitialKeyOn = new byte[6];
    private readonly int[] _audioInitialChannelFNumbers = new int[6];
    private readonly int[] _audioInitialChannelBlocks = new int[6];
    private readonly int[] _audioInitialChannel3SpecialFNumbers = new int[3];
    private readonly int[] _audioInitialChannel3SpecialBlocks = new int[3];
    private readonly uint[] _audioInitialPhase = new uint[24];
    private readonly int[] _audioInitialOperatorEnvelope = new int[24];
    private readonly double[] _audioInitialOperatorEnvelopeRemainder = new double[24];
    private readonly byte[] _audioInitialOperatorStage = new byte[24];
    private readonly bool[] _audioInitialSsgInverted = new bool[24];
    private readonly bool[] _audioInitialSsgHolding = new bool[24];
    private readonly double[] _audioInitialFeedback = new double[6];
    private readonly double[] _audioInitialFeedbackPrevious = new double[6];
    private readonly double[] _audioInitialAlgorithmMemory = new double[6];
    private readonly List<WriteEvent> _writeEvents = new();
    private int _writeEventOrder;
    private double _lfoPhase;
    private long _audioFrameStartCycle;
    private long _audioFrameEndCycle = 1;
    private long _busyUntilMasterCycle;
    private int _audioInitialTimerACounter;
    private int _audioInitialTimerBCounter;
    private byte _audioInitialStatus;
    private byte _audioInitialDacSample;
    private bool _audioInitialDacEnabled;
    private double _audioInitialDacFilteredSample;
    private double _audioInitialDacHighPassInput;
    private double _audioInitialDacHighPassOutput;
    private double _audioInitialLfoPhase;
    private long _audioInitialBusyUntilMasterCycle;
    private bool _audioFrameInitialStateValid;
    private bool _recordingFrame;

    public byte Status => ReadStatus();
    public byte SelectedAddress(int port) => _selected[port & 1];
    public bool DacEnabled => _dacEnabled;
    public byte DacSample => _dacSample;
    public bool InterruptActive => (_status & 0x03) != 0;

    public Ym2612()
    {
        Reset();
    }

    private static double ReadTuning(string name, double fallback)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : fallback;
    }

    private static bool ReadBoolTuning(string name, bool fallback)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    private static double LowPassAlpha(double cutoffHz, int sampleRate)
    {
        double rc = 1.0 / (Math.Tau * cutoffHz);
        double dt = 1.0 / Math.Max(1, sampleRate);
        return dt / (rc + dt);
    }

    private static double HighPassFeedback(double cutoffHz, int sampleRate)
    {
        if (cutoffHz <= 0.0)
        {
            return 0.0;
        }

        return Math.Exp(-Math.Tau * cutoffHz / Math.Max(1, sampleRate));
    }

    private static int[] BuildLogSineAttenuationTable()
    {
        int[] table = new int[1024];
        for (int i = 0; i < table.Length; i++)
        {
            double angle = ((i + 0.5) / table.Length) * Math.Tau;
            double magnitude = Math.Max(1e-12, Math.Abs(Math.Sin(angle)));
            table[i] = Math.Clamp((int)Math.Round((-20.0 * Math.Log10(magnitude)) / AttenuationStepDb), 0, 4095);
        }

        return table;
    }

    private static double[] BuildAttenuationAmplitudeTable()
    {
        double[] table = new double[4096];
        for (int i = 0; i < table.Length; i++)
        {
            table[i] = Math.Pow(10.0, -(i * AttenuationStepDb) / 20.0);
        }

        return table;
    }

    public void Reset()
    {
        Array.Clear(_registers);
        Array.Clear(_selected);
        _timerACounter = 0;
        _timerBCounter = 0;
        _status = 0;
        _dacSample = 0x80;
        _dacEnabled = false;
        _dacFilteredSample = 0.0;
        _dacHighPassInput = 0.0;
        _dacHighPassOutput = 0.0;
        Array.Clear(_keyOn);
        Array.Clear(_channelFNumbers);
        Array.Clear(_channelBlocks);
        Array.Clear(_channel3SpecialFNumbers);
        Array.Clear(_channel3SpecialBlocks);
        Array.Clear(_phase);
        Array.Fill(_operatorEnvelope, 1024);
        Array.Clear(_operatorEnvelopeRemainder);
        Array.Fill(_operatorStage, (byte)3);
        Array.Clear(_ssgInverted);
        Array.Clear(_ssgHolding);
        Array.Clear(_feedback);
        Array.Clear(_feedbackPrevious);
        Array.Clear(_algorithmMemory);
        Array.Clear(_audioInitialRegisters);
        Array.Clear(_audioInitialSelected);
        Array.Clear(_audioInitialKeyOn);
        Array.Clear(_audioInitialChannelFNumbers);
        Array.Clear(_audioInitialChannelBlocks);
        Array.Clear(_audioInitialChannel3SpecialFNumbers);
        Array.Clear(_audioInitialChannel3SpecialBlocks);
        Array.Clear(_audioInitialPhase);
        Array.Fill(_audioInitialOperatorEnvelope, 1024);
        Array.Clear(_audioInitialOperatorEnvelopeRemainder);
        Array.Fill(_audioInitialOperatorStage, (byte)3);
        Array.Clear(_audioInitialSsgInverted);
        Array.Clear(_audioInitialSsgHolding);
        Array.Clear(_audioInitialFeedback);
        Array.Clear(_audioInitialFeedbackPrevious);
        Array.Clear(_audioInitialAlgorithmMemory);
        _writeEvents.Clear();
        _writeEventOrder = 0;
        _lfoPhase = 0.0;
        _audioFrameStartCycle = 0;
        _audioFrameEndCycle = 1;
        _busyUntilMasterCycle = 0;
        _audioInitialTimerACounter = 0;
        _audioInitialTimerBCounter = 0;
        _audioInitialStatus = 0;
        _audioInitialDacSample = 0x80;
        _audioInitialDacEnabled = false;
        _audioInitialDacFilteredSample = 0.0;
        _audioInitialDacHighPassInput = 0.0;
        _audioInitialDacHighPassOutput = 0.0;
        _audioInitialLfoPhase = 0.0;
        _audioInitialBusyUntilMasterCycle = 0;
        _audioFrameInitialStateValid = false;
        _recordingFrame = false;
    }

    public void WriteAddress(int port, byte address)
    {
        _selected[port & 1] = address;
    }

    public void BeginAudioFrame(long startCycle, long endCycle)
    {
        _audioFrameStartCycle = startCycle;
        _audioFrameEndCycle = Math.Max(startCycle + 1, endCycle);
        SaveAudioFrameInitialState();
        _writeEvents.Clear();
        _writeEventOrder = 0;
        _recordingFrame = true;
    }

    public void WriteData(int port, byte value)
    {
        WriteData(port, value, _audioFrameEndCycle);
    }

    public void WriteData(int port, byte value, long masterCycle)
    {
        int bank = port & 1;
        byte address = _selected[bank];
        ApplyRegisterWrite(bank, address, value);
        QueueWriteEvent(bank, address, value, masterCycle);
        _busyUntilMasterCycle = Math.Max(_busyUntilMasterCycle, masterCycle + BusyMasterCycles);
    }

    private void ApplyRegisterWrite(int bank, byte address, byte value)
    {
        byte previous = _registers[bank, address];
        _registers[bank, address] = value;

        if (bank == 0 && address == 0x27)
        {
            ApplyTimerControl(value, previous);
        }
        else if (bank == 0 && address == 0x28)
        {
            int channel = value & 0x03;
            if (channel < 3)
            {
                if ((value & 0x04) != 0)
                {
                    channel += 3;
                }

                SetKeyOn(channel, (byte)(value >> 4));
            }
        }
        else if (bank == 0 && address == 0x2A)
        {
            _dacSample = value;
        }
        else if (bank == 0 && address == 0x2B)
        {
            bool wasEnabled = _dacEnabled;
            _dacEnabled = (value & 0x80) != 0;
            if (!wasEnabled && _dacEnabled)
            {
                _dacFilteredSample = (_dacSample - 128) * DacOutputScale;
                _dacHighPassInput = _dacFilteredSample;
                _dacHighPassOutput = 0.0;
            }
        }
        else if (address is >= 0xA0 and <= 0xA2)
        {
            LatchChannelFrequency(bank, address - 0xA0);
        }
        else if (bank == 0 && address is >= 0xA8 and <= 0xAA)
        {
            LatchChannel3SpecialFrequency(address - 0xA8);
        }
        else if (bank == 0 && address is 0x24 or 0x25)
        {
            if ((_registers[0, 0x27] & 0x01) != 0)
            {
                ReloadTimerA();
            }
        }
        else if (bank == 0 && address == 0x26)
        {
            if ((_registers[0, 0x27] & 0x02) != 0)
            {
                ReloadTimerB();
            }
        }
    }

    private void LatchChannelFrequency(int bank, int slot)
    {
        int channel = (bank * 3) + slot;
        _channelFNumbers[channel] = ((_registers[bank, 0xA4 + slot] & 0x07) << 8) | _registers[bank, 0xA0 + slot];
        _channelBlocks[channel] = (_registers[bank, 0xA4 + slot] >> 3) & 0x07;
    }

    private void LatchChannel3SpecialFrequency(int register)
    {
        _channel3SpecialFNumbers[register] = ((_registers[0, 0xAC + register] & 0x07) << 8) | _registers[0, 0xA8 + register];
        _channel3SpecialBlocks[register] = (_registers[0, 0xAC + register] >> 3) & 0x07;
    }

    public byte ReadRegister(int port, byte address)
    {
        return _registers[port & 1, address];
    }

    public byte ReadStatus()
    {
        return _status;
    }

    public byte ReadStatus(long masterCycle)
    {
        return (byte)(_status | (masterCycle < _busyUntilMasterCycle ? 0x80 : 0x00));
    }

    public int TimerACounter => _timerACounter;
    public int TimerBCounter => _timerBCounter;

    public void Step(int cycles)
    {
        byte control = _registers[0, 0x27];
        if ((control & 0x01) != 0)
        {
            _timerACounter -= cycles;
            while (_timerACounter <= 0)
            {
                ReloadTimerA();
                if ((control & 0x04) != 0)
                {
                    _status |= 0x01;
                }

                if ((control & 0x80) != 0)
                {
                    SetKeyOn(2, 0x0F);
                }
            }
        }

        if ((control & 0x02) != 0)
        {
            _timerBCounter -= cycles;
            while (_timerBCounter <= 0)
            {
                ReloadTimerB();
                if ((control & 0x08) != 0)
                {
                    _status |= 0x02;
                }
            }
        }
    }

    private void ApplyTimerControl(byte value, byte previous)
    {
        if ((value & 0x10) != 0)
        {
            _status = (byte)(_status & ~0x01);
        }

        if ((value & 0x20) != 0)
        {
            _status = (byte)(_status & ~0x02);
        }

        if ((value & 0x01) != 0 && ((previous & 0x01) == 0 || _timerACounter <= 0))
        {
            ReloadTimerA();
        }

        if ((value & 0x02) != 0 && ((previous & 0x02) == 0 || _timerBCounter <= 0))
        {
            ReloadTimerB();
        }
    }

    private void ReloadTimerA()
    {
        int value = ((_registers[0, 0x24] << 2) | (_registers[0, 0x25] & 0x03)) & 0x03FF;
        _timerACounter = Math.Max(1, (1024 - value) * 144);
    }

    private void ReloadTimerB()
    {
        int value = _registers[0, 0x26];
        _timerBCounter = Math.Max(1, (256 - value) * 2_304);
    }

    public short[] RenderMonoSamples(int samples, int sampleRate = AudioConstants.DefaultSampleRate, int clockHz = DefaultClockHz)
    {
        short[] stereo = RenderStereoSamples(samples, sampleRate, clockHz);
        short[] output = new short[samples];
        for (int i = 0; i < samples; i++)
        {
            output[i] = (short)Math.Clamp((stereo[i * 2] + stereo[(i * 2) + 1]) / 2, short.MinValue, short.MaxValue);
        }

        return output;
    }

    public short[] RenderStereoSamples(int samples, int sampleRate = AudioConstants.DefaultSampleRate, int clockHz = DefaultClockHz, long[]? channelEnergy = null)
    {
        short[] output = new short[samples * AudioConstants.StereoChannels];
        RenderStereoSamplesInto(output, samples, sampleRate, clockHz, channelEnergy);
        return output;
    }

    public void RenderStereoSamplesInto(Span<short> output, int samples, int sampleRate = AudioConstants.DefaultSampleRate, int clockHz = DefaultClockHz, long[]? channelEnergy = null)
    {
        if (output.Length < samples * AudioConstants.StereoChannels)
        {
            throw new ArgumentException("Output buffer is too small.", nameof(output));
        }

        byte finalSelected0 = _selected[0];
        byte finalSelected1 = _selected[1];
        int finalTimerACounter = _timerACounter;
        int finalTimerBCounter = _timerBCounter;
        byte finalStatus = _status;
        long finalBusyUntilMasterCycle = _busyUntilMasterCycle;
        if (_audioFrameInitialStateValid)
        {
            RestoreAudioFrameInitialState();
        }

        SortWriteEvents();
        double dacLowPassAlpha = LowPassAlpha(DacLowPassCutoffHz, sampleRate);
        double dacHighPassFeedback = HighPassFeedback(DacHighPassCutoffHz, sampleRate);
        int eventIndex = 0;
        for (int i = 0; i < samples; i++)
        {
            long sampleCycle = SampleCycle(i, samples);
            while (eventIndex < _writeEvents.Count && _writeEvents[eventIndex].MasterCycle <= sampleCycle)
            {
                ApplyRegisterWrite(_writeEvents[eventIndex].Bank, _writeEvents[eventIndex].Address, _writeEvents[eventIndex].Value);
                eventIndex++;
            }

            double lfo = StepLfo(sampleRate);
            double left = 0.0;
            double right = 0.0;
            for (int channel = 0; channel < 6; channel++)
            {
                (bool panLeft, bool panRight) = ChannelPanning(channel);
                int channelSample;
                if (_dacEnabled && channel == 5)
                {
                    channelSample = RenderDacSample(dacLowPassAlpha, dacHighPassFeedback);
                }
                else
                {
                    channelSample = RenderFmChannel(channel, sampleRate, clockHz, lfo);
                }

                if (channelEnergy is not null && channel < channelEnergy.Length)
                {
                    channelEnergy[channel] += Math.Abs(channelSample);
                }

                double mixedChannelSample = channelSample * AudioConstants.YmChannelMixLevel(channel);
                if (panLeft)
                {
                    left += mixedChannelSample;
                }

                if (panRight)
                {
                    right += mixedChannelSample;
                }
            }

            output[i * 2] = AudioConstants.ClampSample(left);
            output[(i * 2) + 1] = AudioConstants.ClampSample(right);
        }

        while (eventIndex < _writeEvents.Count)
        {
            ApplyRegisterWrite(_writeEvents[eventIndex].Bank, _writeEvents[eventIndex].Address, _writeEvents[eventIndex].Value);
            eventIndex++;
        }

        RestoreNonAudioRuntimeState(finalSelected0, finalSelected1, finalTimerACounter, finalTimerBCounter, finalStatus, finalBusyUntilMasterCycle);
        _recordingFrame = false;
        _audioFrameInitialStateValid = false;
    }

    public void RenderStereoChannelStemsInto(Span<short> output, int samples, int sampleRate = AudioConstants.DefaultSampleRate, int clockHz = DefaultClockHz, long[]? channelEnergy = null)
    {
        const int channelCount = 6;
        int required = samples * channelCount * AudioConstants.StereoChannels;
        if (output.Length < required)
        {
            throw new ArgumentException("Output buffer is too small.", nameof(output));
        }

        output[..required].Clear();
        byte finalSelected0 = _selected[0];
        byte finalSelected1 = _selected[1];
        int finalTimerACounter = _timerACounter;
        int finalTimerBCounter = _timerBCounter;
        byte finalStatus = _status;
        long finalBusyUntilMasterCycle = _busyUntilMasterCycle;
        if (_audioFrameInitialStateValid)
        {
            RestoreAudioFrameInitialState();
        }

        SortWriteEvents();
        double dacLowPassAlpha = LowPassAlpha(DacLowPassCutoffHz, sampleRate);
        double dacHighPassFeedback = HighPassFeedback(DacHighPassCutoffHz, sampleRate);
        int eventIndex = 0;
        for (int i = 0; i < samples; i++)
        {
            long sampleCycle = SampleCycle(i, samples);
            while (eventIndex < _writeEvents.Count && _writeEvents[eventIndex].MasterCycle <= sampleCycle)
            {
                ApplyRegisterWrite(_writeEvents[eventIndex].Bank, _writeEvents[eventIndex].Address, _writeEvents[eventIndex].Value);
                eventIndex++;
            }

            double lfo = StepLfo(sampleRate);
            for (int channel = 0; channel < channelCount; channel++)
            {
                (bool panLeft, bool panRight) = ChannelPanning(channel);
                int channelSample = _dacEnabled && channel == 5
                    ? RenderDacSample(dacLowPassAlpha, dacHighPassFeedback)
                    : RenderFmChannel(channel, sampleRate, clockHz, lfo);

                if (channelEnergy is not null && channel < channelEnergy.Length)
                {
                    channelEnergy[channel] += Math.Abs(channelSample);
                }

                int offset = ((channel * samples) + i) * AudioConstants.StereoChannels;
                output[offset] = panLeft ? (short)Math.Clamp(channelSample, short.MinValue, short.MaxValue) : (short)0;
                output[offset + 1] = panRight ? (short)Math.Clamp(channelSample, short.MinValue, short.MaxValue) : (short)0;
            }
        }

        while (eventIndex < _writeEvents.Count)
        {
            ApplyRegisterWrite(_writeEvents[eventIndex].Bank, _writeEvents[eventIndex].Address, _writeEvents[eventIndex].Value);
            eventIndex++;
        }

        RestoreNonAudioRuntimeState(finalSelected0, finalSelected1, finalTimerACounter, finalTimerBCounter, finalStatus, finalBusyUntilMasterCycle);
        _recordingFrame = false;
        _audioFrameInitialStateValid = false;
    }

    private void QueueWriteEvent(int bank, byte address, byte value, long masterCycle)
    {
        if (!_recordingFrame)
        {
            return;
        }

        long clampedCycle = Math.Clamp(masterCycle, _audioFrameStartCycle, _audioFrameEndCycle - 1);
        _writeEvents.Add(new WriteEvent(clampedCycle, _writeEventOrder++, bank, address, value));
    }

    private void SortWriteEvents()
    {
        if (_writeEvents.Count > 1)
        {
            _writeEvents.Sort(static (left, right) =>
            {
                int cycle = left.MasterCycle.CompareTo(right.MasterCycle);
                return cycle != 0 ? cycle : left.Order.CompareTo(right.Order);
            });
        }
    }

    private long SampleCycle(int sampleIndex, int sampleCount)
    {
        long span = Math.Max(1, _audioFrameEndCycle - _audioFrameStartCycle);
        return _audioFrameStartCycle + ((span * sampleIndex) / Math.Max(1, sampleCount));
    }

    private int RenderFmChannel(int channel, int sampleRate, int clockHz, double lfo)
    {
        if (IsFmChannelSilent(channel))
        {
            return 0;
        }

        uint baseIncrement = ChannelIncrement(channel, sampleRate, clockHz, lfo);
        if (baseIncrement == 0)
        {
            return 0;
        }

        int bank = channel / 3;
        int slot = channel % 3;
        int algorithm = _registers[bank, 0xB0 + slot] & 0x07;

        double feedback = ChannelFeedback(channel);
        double op0 = OperatorSample(channel, 0, baseIncrement, feedback, lfo, sampleRate);
        double op1;
        double op2;
        double op3;
        double output;
        switch (algorithm)
        {
            case 0:
                // S1 -> S3 -> S2 -> S4
                op2 = OperatorSample(channel, 2, baseIncrement, op0, lfo, sampleRate);
                op1 = OperatorSample(channel, 1, baseIncrement, op2, lfo, sampleRate);
                output = OperatorSample(channel, 3, baseIncrement, op1, lfo, sampleRate);
                break;
            case 1:
                // S1 + S3 -> S2 -> S4
                op2 = OperatorSample(channel, 2, baseIncrement, 0.0, lfo, sampleRate);
                op1 = OperatorSample(channel, 1, baseIncrement, op0 + op2, lfo, sampleRate);
                output = OperatorSample(channel, 3, baseIncrement, op1, lfo, sampleRate);
                break;
            case 2:
                // S1 -> S4, S3 -> S2 -> S4
                op2 = OperatorSample(channel, 2, baseIncrement, 0.0, lfo, sampleRate);
                op1 = OperatorSample(channel, 1, baseIncrement, op2, lfo, sampleRate);
                output = OperatorSample(channel, 3, baseIncrement, op0 + op1, lfo, sampleRate);
                break;
            case 3:
                // S1 -> S3 -> S4, S2 -> S4
                op2 = OperatorSample(channel, 2, baseIncrement, op0, lfo, sampleRate);
                op1 = OperatorSample(channel, 1, baseIncrement, 0.0, lfo, sampleRate);
                output = OperatorSample(channel, 3, baseIncrement, op1 + op2, lfo, sampleRate);
                break;
            case 4:
                op1 = OperatorSample(channel, 1, baseIncrement, op0, lfo, sampleRate);
                op2 = OperatorSample(channel, 2, baseIncrement, 0.0, lfo, sampleRate);
                op3 = OperatorSample(channel, 3, baseIncrement, op2, lfo, sampleRate);
                output = op1 + op3;
                break;
            case 5:
                // S1 -> S2, S3, and S4
                op1 = OperatorSample(channel, 1, baseIncrement, op0, lfo, sampleRate);
                op2 = OperatorSample(channel, 2, baseIncrement, op0, lfo, sampleRate);
                op3 = OperatorSample(channel, 3, baseIncrement, op0, lfo, sampleRate);
                output = op1 + op2 + op3;
                break;
            case 6:
                op1 = OperatorSample(channel, 1, baseIncrement, op0, lfo, sampleRate);
                op2 = OperatorSample(channel, 2, baseIncrement, 0.0, lfo, sampleRate);
                op3 = OperatorSample(channel, 3, baseIncrement, 0.0, lfo, sampleRate);
                output = op1 + op2 + op3;
                break;
            default:
                output = op0
                    + OperatorSample(channel, 1, baseIncrement, 0.0, lfo, sampleRate)
                    + OperatorSample(channel, 2, baseIncrement, 0.0, lfo, sampleRate)
                    + OperatorSample(channel, 3, baseIncrement, 0.0, lfo, sampleRate);
                break;
        }

        _feedbackPrevious[channel] = _feedback[channel];
        _feedback[channel] = op0;
        _algorithmMemory[channel] = 0.0;
        return (int)Math.Clamp(output * FmOutputScale, FmOutputMin, FmOutputMax);
    }

    private bool IsFmChannelSilent(int channel)
    {
        if (_keyOn[channel] != 0)
        {
            return false;
        }

        int operatorBase = channel * 4;
        return _operatorEnvelope[operatorBase] >= 1024
            && _operatorEnvelope[operatorBase + 1] >= 1024
            && _operatorEnvelope[operatorBase + 2] >= 1024
            && _operatorEnvelope[operatorBase + 3] >= 1024;
    }

    private void SetKeyOn(int channel, byte value)
    {
        byte previous = _keyOn[channel];
        _keyOn[channel] = value;
        for (int op = 0; op < 4; op++)
        {
            byte mask = KeyOnMask(op);
            int index = (channel * 4) + op;
            if ((value & mask) != 0 && (previous & mask) == 0)
            {
                _operatorStage[index] = 0;
                _operatorEnvelope[index] = 1024;
                _operatorEnvelopeRemainder[index] = 0.0;
                _phase[index] = 0;
                _ssgInverted[index] = false;
                _ssgHolding[index] = false;
            }
            else if ((value & mask) == 0 && (previous & mask) != 0)
            {
                _operatorStage[index] = 3;
                _operatorEnvelopeRemainder[index] = 0.0;
                _ssgHolding[index] = false;
            }
        }
    }

    private double OperatorSample(int channel, int op, uint baseIncrement, double modulation, double lfo, int sampleRate)
    {
        int index = (channel * 4) + op;
        int bank = channel / 3;
        int slot = channel % 3;
        byte keyMask = KeyOnMask(op);
        bool keyed = (_keyOn[channel] & keyMask) != 0;
        int envelope = StepEnvelope(channel, op, keyed, sampleRate);
        if (envelope >= 1024)
        {
            return 0.0;
        }

        int attenuation = OperatorAttenuation(bank, slot, op, envelope, lfo);
        double sample = UseTableOperatorOutput
            ? OperatorTableOutput(_phase[index], modulation, attenuation)
            : QuantizeOperatorOutput(Math.Sin(OperatorAngle(_phase[index], modulation)) * OperatorGain(attenuation));
        _phase[index] += OperatorIncrement(channel, op, baseIncrement, sampleRate);
        return sample;
    }

    private static double QuantizeOperatorOutput(double sample)
    {
        return Math.Clamp(Math.Round(sample * OperatorOutputScale), -OperatorOutputScale, OperatorOutputScale) / OperatorOutputScale;
    }

    private static double OperatorAngle(uint phase, double modulation)
    {
        int phase10 = (int)(phase >> 22);
        double shapedModulation = Math.Tanh(modulation / PhaseModulationSoftLimit) * PhaseModulationSoftLimit;
        int modulation10 = (int)Math.Round((shapedModulation * PhaseModulationScale / Math.Tau) * 1024.0);
        int tableIndex = (phase10 + modulation10) & 0x3FF;
        return ((tableIndex + 0.5) / 1024.0) * Math.Tau;
    }

    private static double OperatorTableOutput(uint phase, double modulation, int attenuation)
    {
        int phase10 = (int)(phase >> 22);
        double shapedModulation = Math.Tanh(modulation / PhaseModulationSoftLimit) * PhaseModulationSoftLimit;
        int modulation10 = (int)Math.Round((shapedModulation * PhaseModulationScale / Math.Tau) * 1024.0);
        int tableIndex = (phase10 + modulation10) & 0x3FF;
        int totalAttenuation = Math.Clamp(attenuation + LogSineAttenuationTable[tableIndex], 0, AttenuationAmplitudeTable.Length - 1);
        if (totalAttenuation >= OperatorMuteAttenuation)
        {
            return 0.0;
        }

        double sample = AttenuationAmplitudeTable[totalAttenuation];
        return tableIndex >= 512 ? -sample : sample;
    }

    private int StepEnvelope(int channel, int op, bool keyed, int sampleRate)
    {
        int index = (channel * 4) + op;
        int bank = channel / 3;
        int slot = channel % 3;
        int offset = OperatorRegisterOffset(op) + slot;
        byte attackRegister = _registers[bank, 0x50 + offset];
        int attack = attackRegister & 0x1F;
        int rateScale = (attackRegister >> 6) & 0x03;
        int decay = _registers[bank, 0x60 + offset] & 0x1F;
        int sustainRate = _registers[bank, 0x70 + offset] & 0x1F;
        int sustainLevel = SustainAttenuation((_registers[bank, 0x80 + offset] >> 4) & 0x0F);
        int release = _registers[bank, 0x80 + offset] & 0x0F;

        if (!keyed)
        {
            _operatorStage[index] = 3;
        }

        switch (_operatorStage[index])
        {
            case 0:
                if (attack > 0)
                {
                    _operatorEnvelope[index] = MoveAttackEnvelope(index, EnvelopeStep(channel, op, attack, rateScale, sampleRate, attack: true) * AttackScale);
                    if (_operatorEnvelope[index] <= 0)
                    {
                        _operatorStage[index] = 1;
                        _operatorEnvelopeRemainder[index] = 0.0;
                    }
                }

                break;
            case 1:
                if (decay > 0)
                {
                    _operatorEnvelope[index] = MoveEnvelope(index, EnvelopeStep(channel, op, decay, rateScale, sampleRate, attack: false) * DecayScale, sustainLevel, rising: true);
                    if (_operatorEnvelope[index] >= sustainLevel)
                    {
                        _operatorStage[index] = 2;
                        _operatorEnvelopeRemainder[index] = 0.0;
                    }
                }

                break;
            case 2:
                if (sustainRate > 0)
                {
                    _operatorEnvelope[index] = MoveEnvelope(index, EnvelopeStep(channel, op, sustainRate, rateScale, sampleRate, attack: false) * SustainScale, 1024, rising: true);
                    if (_operatorEnvelope[index] >= 1024)
                    {
                        HandleSsgCycle(index, bank, offset);
                    }
                }

                break;
            default:
                _operatorEnvelope[index] = MoveEnvelope(index, EnvelopeStep(channel, op, (release * 2) + 1, rateScale, sampleRate, attack: false, preScaledRate: true) * ReleaseScale, 1024, rising: true);
                break;
        }

        return ApplySsgEnvelope(index, bank, offset, _operatorEnvelope[index]);
    }

    private int MoveEnvelope(int index, double amount, int limit, bool rising)
    {
        if (amount <= 0.0)
        {
            return _operatorEnvelope[index];
        }

        _operatorEnvelopeRemainder[index] += amount;
        int whole = (int)_operatorEnvelopeRemainder[index];
        if (whole <= 0)
        {
            return _operatorEnvelope[index];
        }

        _operatorEnvelopeRemainder[index] -= whole;
        return rising
            ? Math.Min(limit, _operatorEnvelope[index] + whole)
            : Math.Max(limit, _operatorEnvelope[index] - whole);
    }

    private int MoveAttackEnvelope(int index, double amount)
    {
        if (amount <= 0.0)
        {
            return _operatorEnvelope[index];
        }

        double scaled = amount * Math.Max(1.0, _operatorEnvelope[index] / AttackCurveDivisor);
        _operatorEnvelopeRemainder[index] += scaled;
        int whole = (int)_operatorEnvelopeRemainder[index];
        if (whole <= 0)
        {
            return _operatorEnvelope[index];
        }

        _operatorEnvelopeRemainder[index] -= whole;
        return Math.Max(0, _operatorEnvelope[index] - whole);
    }

    private double EnvelopeStep(int channel, int op, int rate, int rateScale, int sampleRate, bool attack, bool preScaledRate = false)
    {
        if (rate <= 0)
        {
            return 0.0;
        }

        int keyScale = KeyScaleRate(channel, op, rateScale);
        int chipRate = Math.Clamp((preScaledRate ? rate : rate * 2) + keyScale, 0, 63);
        if (chipRate == 0)
        {
            return 0.0;
        }

        int shift = Math.Max(0, 11 - (chipRate / 4));
        double envelopeClock = DefaultClockHz / 144.0 / 3.0;
        double updatesPerSecond = envelopeClock / (1 << shift);
        double increment = chipRate switch
        {
            >= 60 => 8.0,
            >= 56 => 4.0,
            >= 52 => 2.0,
            _ => 1.0,
        };

        if (attack)
        {
            increment *= chipRate >= 56 ? 8.0 : 4.0;
        }

        return updatesPerSecond * increment / Math.Max(1, sampleRate);
    }

    private int KeyScaleRate(int channel, int op, int rateScale)
    {
        if (rateScale <= 0)
        {
            return 0;
        }

        return KeyCode(channel, op) >> Math.Max(0, 3 - rateScale);
    }

    private int KeyCode(int channel, int op)
    {
        int fnum = ChannelFNumber(channel, op);
        int block = ChannelBlock(channel, op);
        int noteCode = (fnum >> 7) switch
        {
            <= 6 => 0,
            7 => 1,
            8 => 2,
            _ => 3,
        };

        return Math.Clamp((block << 2) | noteCode, 0, 31);
    }

    private static int SustainAttenuation(int sustainLevel)
    {
        if (sustainLevel >= 15)
        {
            return 0x3E0;
        }

        return Math.Clamp(sustainLevel << 5, 0, 0x3FF);
    }

    private void HandleSsgCycle(int operatorIndex, int bank, int registerOffset)
    {
        int ssg = _registers[bank, 0x90 + registerOffset] & 0x0F;
        if ((ssg & 0x08) == 0)
        {
            return;
        }

        bool alternate = (ssg & 0x02) != 0;
        bool hold = (ssg & 0x01) != 0;
        if (alternate)
        {
            _ssgInverted[operatorIndex] = !_ssgInverted[operatorIndex];
        }

        if (hold)
        {
            _ssgHolding[operatorIndex] = true;
            _operatorEnvelope[operatorIndex] = 1024;
        }
        else
        {
            _operatorEnvelope[operatorIndex] = 0;
        }
    }

    private double ChannelFeedback(int channel)
    {
        int bank = channel / 3;
        int slot = channel % 3;
        int feedbackLevel = (_registers[bank, 0xB0 + slot] >> 3) & 0x07;
        if (feedbackLevel == 0)
        {
            return 0.0;
        }

        return ((_feedback[channel] + _feedbackPrevious[channel]) * 0.5) * Math.Pow(2.0, feedbackLevel - FeedbackShift);
    }

    private double OperatorGain(int attenuation)
    {
        if (attenuation >= OperatorMuteAttenuation)
        {
            return 0.0;
        }

        double attenuationDb = Math.Clamp(attenuation * AttenuationStepDb, 0.0, 160.0);
        return Math.Pow(10.0, -attenuationDb / 20.0);
    }

    private int OperatorAttenuation(int bank, int slot, int op, int envelope, double lfo)
    {
        int offset = OperatorRegisterOffset(op) + slot;
        int totalLevel = _registers[bank, 0x40 + offset] & 0x7F;
        double attenuation = Math.Min(0x3FF, envelope + (totalLevel << 3));
        if ((_registers[bank, 0x60 + offset] & 0x80) != 0)
        {
            int ams = (_registers[bank, 0xB4 + slot] >> 4) & 0x03;
            double depthDb = AmsDbDepth[ams];
            attenuation -= (lfo * depthDb) / AttenuationStepDb;
        }

        return Math.Clamp((int)Math.Round(attenuation), 0, 4095);
    }

    private uint OperatorIncrement(int channel, int op, uint baseIncrement, int sampleRate)
    {
        int bank = channel / 3;
        int slot = channel % 3;
        int registerOffset = OperatorRegisterOffset(op) + slot;
        int multiply = _registers[bank, 0x30 + registerOffset] & 0x0F;
        int detune = (_registers[bank, 0x30 + registerOffset] >> 4) & 0x07;
        double multiplier = multiply == 0 ? 0.5 : multiply;
        double increment = Channel3SpecialIncrement(channel, op, sampleRate) ?? baseIncrement;
        increment += DetunePhaseIncrement(channel, op, detune, sampleRate);
        increment *= multiplier;
        return (uint)Math.Clamp(increment, 1.0, uint.MaxValue);
    }

    private double DetunePhaseIncrement(int channel, int op, int detune, int sampleRate)
    {
        int magnitude = detune & 0x03;
        if (magnitude == 0)
        {
            return 0.0;
        }

        int detuneStep = DetuneTable[KeyCode(channel, op), magnitude];
        if (detuneStep == 0)
        {
            return 0.0;
        }

        double frequencyDelta = detuneStep * DefaultClockHz / (144.0 * 1_048_576.0);
        double phaseDelta = frequencyDelta * uint.MaxValue / Math.Max(1, sampleRate);
        return (detune & 0x04) != 0 ? -phaseDelta : phaseDelta;
    }

    private uint? Channel3SpecialIncrement(int channel, int op, int sampleRate)
    {
        if (channel != 2 || (_registers[0, 0x27] & 0xC0) == 0)
        {
            return null;
        }

        int register = Channel3SpecialRegisterIndex(op);
        if (register < 0)
        {
            return null;
        }

        int fnum = _channel3SpecialFNumbers[register];
        if (fnum == 0)
        {
            return null;
        }

        int block = _channel3SpecialBlocks[register];
        return FrequencyIncrement(fnum, block, sampleRate, DefaultClockHz, 0.0);
    }

    private int ChannelBlock(int channel, int op)
    {
        if (channel == 2 && (_registers[0, 0x27] & 0xC0) != 0)
        {
            int register = Channel3SpecialRegisterIndex(op);
            if (register >= 0)
            {
                return _channel3SpecialBlocks[register];
            }
        }

        return _channelBlocks[channel];
    }

    private int ChannelFNumber(int channel, int op)
    {
        if (channel == 2 && (_registers[0, 0x27] & 0xC0) != 0)
        {
            int register = Channel3SpecialRegisterIndex(op);
            if (register >= 0)
            {
                return _channel3SpecialFNumbers[register];
            }
        }

        return _channelFNumbers[channel];
    }

    private static int OperatorRegisterOffset(int op)
    {
        return op switch
        {
            0 => 0x00,
            1 => 0x08,
            2 => 0x04,
            _ => 0x0C,
        };
    }

    private static int Channel3SpecialRegisterIndex(int op)
    {
        return op switch
        {
            0 => 1, // S1: $A9/$AD
            1 => 2, // S2: $AA/$AE
            2 => 0, // S3: $A8/$AC
            _ => -1, // S4 uses the normal channel 3 frequency registers.
        };
    }

    private static byte KeyOnMask(int op)
    {
        return op switch
        {
            0 => 0x01,
            1 => 0x02,
            2 => 0x04,
            _ => 0x08,
        };
    }

    private uint ChannelIncrement(int channel, int sampleRate, int clockHz, double lfo)
    {
        int bank = channel / 3;
        int slot = channel % 3;
        int fnum = _channelFNumbers[channel];
        if (fnum == 0)
        {
            return 0;
        }

        int block = _channelBlocks[channel];
        int pms = _registers[bank, 0xB4 + slot] & 0x07;
        return FrequencyIncrement(fnum, block, sampleRate, clockHz, lfo * PmsSemitoneDepth[pms]);
    }

    private double StepLfo(int sampleRate)
    {
        byte lfoRegister = _registers[0, 0x22];
        if ((lfoRegister & 0x08) == 0)
        {
            return 0.0;
        }

        double frequency = LfoFrequencies[lfoRegister & 0x07];
        _lfoPhase += frequency / Math.Max(1, sampleRate);
        _lfoPhase -= Math.Floor(_lfoPhase);
        return Math.Sin(_lfoPhase * Math.Tau);
    }

    private (bool Left, bool Right) ChannelPanning(int channel)
    {
        int bank = channel / 3;
        int slot = channel % 3;
        byte pan = _registers[bank, 0xB4 + slot];
        return pan == 0 ? (true, true) : ((pan & 0x80) != 0, (pan & 0x40) != 0);
    }

    private int ApplySsgEnvelope(int operatorIndex, int bank, int registerOffset, int envelope)
    {
        int ssg = _registers[bank, 0x90 + registerOffset] & 0x0F;
        if ((ssg & 0x08) == 0)
        {
            return envelope;
        }

        bool attackInvert = (ssg & 0x04) != 0;
        bool hold = (ssg & 0x01) != 0;
        bool invert = attackInvert ^ _ssgInverted[operatorIndex];
        int shaped = invert ? 1024 - envelope : envelope;
        if (hold && _ssgHolding[operatorIndex])
        {
            shaped = invert ? 0 : 1024;
        }

        return Math.Clamp(shaped, 0, 1024);
    }

    private static uint FrequencyIncrement(int fnum, int block, int sampleRate, int clockHz, double semitoneOffset)
    {
        if (fnum == 0)
        {
            return 0;
        }

        double octaveScale = Math.Pow(2.0, block - 1);
        double frequency = fnum * octaveScale * clockHz / (144.0 * 1_048_576.0);
        if (semitoneOffset != 0.0)
        {
            frequency *= Math.Pow(2.0, semitoneOffset / 12.0);
        }

        double increment = frequency * uint.MaxValue / Math.Max(1, sampleRate);
        return (uint)Math.Clamp(increment, 1.0, uint.MaxValue);
    }

    public Ym2612ChannelSnapshot[] GetChannelSnapshots()
    {
        Ym2612ChannelSnapshot[] snapshots = new Ym2612ChannelSnapshot[6];
        for (int channel = 0; channel < snapshots.Length; channel++)
        {
            int bank = channel / 3;
            int slot = channel % 3;
            int fnum = _channelFNumbers[channel];
            int block = _channelBlocks[channel];
            int algorithm = _registers[bank, 0xB0 + slot] & 0x07;
            int feedback = (_registers[bank, 0xB0 + slot] >> 3) & 0x07;
            int pms = _registers[bank, 0xB4 + slot] & 0x07;
            int ams = (_registers[bank, 0xB4 + slot] >> 4) & 0x03;
            int[] totalLevels = new int[4];
            int[] envelopes = new int[4];
            int[] stages = new int[4];
            int[] attackRates = new int[4];
            int[] decayRates = new int[4];
            int[] sustainRates = new int[4];
            int[] releaseRates = new int[4];
            int[] multipliers = new int[4];
            int[] detunes = new int[4];
            for (int op = 0; op < 4; op++)
            {
                int offset = OperatorRegisterOffset(op) + slot;
                int index = (channel * 4) + op;
                byte detuneMultiplier = _registers[bank, 0x30 + offset];
                multipliers[op] = detuneMultiplier & 0x0F;
                detunes[op] = (detuneMultiplier >> 4) & 0x07;
                totalLevels[op] = _registers[bank, 0x40 + offset] & 0x7F;
                envelopes[op] = _operatorEnvelope[index];
                stages[op] = _operatorStage[index];
                attackRates[op] = _registers[bank, 0x50 + offset] & 0x1F;
                decayRates[op] = _registers[bank, 0x60 + offset] & 0x1F;
                sustainRates[op] = _registers[bank, 0x70 + offset] & 0x1F;
                releaseRates[op] = _registers[bank, 0x80 + offset] & 0x0F;
            }

            snapshots[channel] = new Ym2612ChannelSnapshot(channel, _keyOn[channel], algorithm, feedback, fnum, block, totalLevels, envelopes, stages, attackRates, decayRates, sustainRates, releaseRates, multipliers, detunes, pms, ams);
        }

        return snapshots;
    }

    public Ym2612State CaptureState()
    {
        byte[,] registers = (byte[,])_registers.Clone();
        return new Ym2612State(registers, (byte[])_selected.Clone(), _timerACounter, _timerBCounter, _status, _dacSample, _dacEnabled, (byte[])_keyOn.Clone(), (int[])_channelFNumbers.Clone(), (int[])_channelBlocks.Clone(), (int[])_channel3SpecialFNumbers.Clone(), (int[])_channel3SpecialBlocks.Clone(), (uint[])_phase.Clone(), (int[])_operatorEnvelope.Clone(), (double[])_operatorEnvelopeRemainder.Clone(), (byte[])_operatorStage.Clone(), DoubleArrayToInt(_feedback), DoubleArrayToInt(_feedbackPrevious), DoubleArrayToInt(_algorithmMemory), (bool[])_ssgInverted.Clone(), (bool[])_ssgHolding.Clone(), _lfoPhase, _busyUntilMasterCycle, _dacFilteredSample, _dacHighPassInput, _dacHighPassOutput);
    }

    public void RestoreState(Ym2612State state)
    {
        Array.Copy(state.Registers, _registers, Math.Min(_registers.Length, state.Registers.Length));
        Array.Copy(state.Selected, _selected, Math.Min(_selected.Length, state.Selected.Length));
        _timerACounter = state.TimerACounter;
        _timerBCounter = state.TimerBCounter;
        _status = state.Status;
        _dacSample = state.DacSample;
        _dacEnabled = state.DacEnabled;
        _busyUntilMasterCycle = state.BusyUntilMasterCycle;
        Array.Copy(state.KeyOn, _keyOn, Math.Min(_keyOn.Length, state.KeyOn.Length));
        Array.Copy(state.ChannelFNumbers, _channelFNumbers, Math.Min(_channelFNumbers.Length, state.ChannelFNumbers.Length));
        Array.Copy(state.ChannelBlocks, _channelBlocks, Math.Min(_channelBlocks.Length, state.ChannelBlocks.Length));
        Array.Copy(state.Channel3SpecialFNumbers, _channel3SpecialFNumbers, Math.Min(_channel3SpecialFNumbers.Length, state.Channel3SpecialFNumbers.Length));
        Array.Copy(state.Channel3SpecialBlocks, _channel3SpecialBlocks, Math.Min(_channel3SpecialBlocks.Length, state.Channel3SpecialBlocks.Length));
        Array.Copy(state.Phase, _phase, Math.Min(_phase.Length, state.Phase.Length));
        Array.Copy(state.OperatorEnvelope, _operatorEnvelope, Math.Min(_operatorEnvelope.Length, state.OperatorEnvelope.Length));
        Array.Copy(state.OperatorEnvelopeRemainder, _operatorEnvelopeRemainder, Math.Min(_operatorEnvelopeRemainder.Length, state.OperatorEnvelopeRemainder.Length));
        Array.Copy(state.OperatorStage, _operatorStage, Math.Min(_operatorStage.Length, state.OperatorStage.Length));
        Array.Copy(state.SsgInverted, _ssgInverted, Math.Min(_ssgInverted.Length, state.SsgInverted.Length));
        Array.Copy(state.SsgHolding, _ssgHolding, Math.Min(_ssgHolding.Length, state.SsgHolding.Length));
        for (int i = 0; i < Math.Min(_feedback.Length, state.Feedback.Length); i++)
        {
            _feedback[i] = state.Feedback[i] / 32768.0;
        }

        for (int i = 0; i < Math.Min(_feedbackPrevious.Length, state.FeedbackPrevious.Length); i++)
        {
            _feedbackPrevious[i] = state.FeedbackPrevious[i] / 32768.0;
        }

        for (int i = 0; i < Math.Min(_algorithmMemory.Length, state.AlgorithmMemory.Length); i++)
        {
            _algorithmMemory[i] = state.AlgorithmMemory[i] / 32768.0;
        }

        _lfoPhase = state.LfoPhase;
        _dacFilteredSample = state.DacFilteredSample;
        _dacHighPassInput = state.DacHighPassInput;
        _dacHighPassOutput = state.DacHighPassOutput;
        _audioFrameInitialStateValid = false;
    }

    private void SaveAudioFrameInitialState()
    {
        Array.Copy(_registers, _audioInitialRegisters, _registers.Length);
        Array.Copy(_selected, _audioInitialSelected, _selected.Length);
        Array.Copy(_keyOn, _audioInitialKeyOn, _keyOn.Length);
        Array.Copy(_channelFNumbers, _audioInitialChannelFNumbers, _channelFNumbers.Length);
        Array.Copy(_channelBlocks, _audioInitialChannelBlocks, _channelBlocks.Length);
        Array.Copy(_channel3SpecialFNumbers, _audioInitialChannel3SpecialFNumbers, _channel3SpecialFNumbers.Length);
        Array.Copy(_channel3SpecialBlocks, _audioInitialChannel3SpecialBlocks, _channel3SpecialBlocks.Length);
        Array.Copy(_phase, _audioInitialPhase, _phase.Length);
        Array.Copy(_operatorEnvelope, _audioInitialOperatorEnvelope, _operatorEnvelope.Length);
        Array.Copy(_operatorEnvelopeRemainder, _audioInitialOperatorEnvelopeRemainder, _operatorEnvelopeRemainder.Length);
        Array.Copy(_operatorStage, _audioInitialOperatorStage, _operatorStage.Length);
        Array.Copy(_ssgInverted, _audioInitialSsgInverted, _ssgInverted.Length);
        Array.Copy(_ssgHolding, _audioInitialSsgHolding, _ssgHolding.Length);
        Array.Copy(_feedback, _audioInitialFeedback, _feedback.Length);
        Array.Copy(_feedbackPrevious, _audioInitialFeedbackPrevious, _feedbackPrevious.Length);
        Array.Copy(_algorithmMemory, _audioInitialAlgorithmMemory, _algorithmMemory.Length);
        _audioInitialTimerACounter = _timerACounter;
        _audioInitialTimerBCounter = _timerBCounter;
        _audioInitialStatus = _status;
        _audioInitialDacSample = _dacSample;
        _audioInitialDacEnabled = _dacEnabled;
        _audioInitialDacFilteredSample = _dacFilteredSample;
        _audioInitialDacHighPassInput = _dacHighPassInput;
        _audioInitialDacHighPassOutput = _dacHighPassOutput;
        _audioInitialLfoPhase = _lfoPhase;
        _audioInitialBusyUntilMasterCycle = _busyUntilMasterCycle;
        _audioFrameInitialStateValid = true;
    }

    private void RestoreAudioFrameInitialState()
    {
        Array.Copy(_audioInitialRegisters, _registers, _registers.Length);
        Array.Copy(_audioInitialSelected, _selected, _selected.Length);
        Array.Copy(_audioInitialKeyOn, _keyOn, _keyOn.Length);
        Array.Copy(_audioInitialChannelFNumbers, _channelFNumbers, _channelFNumbers.Length);
        Array.Copy(_audioInitialChannelBlocks, _channelBlocks, _channelBlocks.Length);
        Array.Copy(_audioInitialChannel3SpecialFNumbers, _channel3SpecialFNumbers, _channel3SpecialFNumbers.Length);
        Array.Copy(_audioInitialChannel3SpecialBlocks, _channel3SpecialBlocks, _channel3SpecialBlocks.Length);
        Array.Copy(_audioInitialPhase, _phase, _phase.Length);
        Array.Copy(_audioInitialOperatorEnvelope, _operatorEnvelope, _operatorEnvelope.Length);
        Array.Copy(_audioInitialOperatorEnvelopeRemainder, _operatorEnvelopeRemainder, _operatorEnvelopeRemainder.Length);
        Array.Copy(_audioInitialOperatorStage, _operatorStage, _operatorStage.Length);
        Array.Copy(_audioInitialSsgInverted, _ssgInverted, _ssgInverted.Length);
        Array.Copy(_audioInitialSsgHolding, _ssgHolding, _ssgHolding.Length);
        Array.Copy(_audioInitialFeedback, _feedback, _feedback.Length);
        Array.Copy(_audioInitialFeedbackPrevious, _feedbackPrevious, _feedbackPrevious.Length);
        Array.Copy(_audioInitialAlgorithmMemory, _algorithmMemory, _algorithmMemory.Length);
        _timerACounter = _audioInitialTimerACounter;
        _timerBCounter = _audioInitialTimerBCounter;
        _status = _audioInitialStatus;
        _dacSample = _audioInitialDacSample;
        _dacEnabled = _audioInitialDacEnabled;
        _dacFilteredSample = _audioInitialDacFilteredSample;
        _dacHighPassInput = _audioInitialDacHighPassInput;
        _dacHighPassOutput = _audioInitialDacHighPassOutput;
        _lfoPhase = _audioInitialLfoPhase;
        _busyUntilMasterCycle = _audioInitialBusyUntilMasterCycle;
    }

    private void RestoreNonAudioRuntimeState(byte selected0, byte selected1, int timerACounter, int timerBCounter, byte status, long busyUntilMasterCycle)
    {
        _selected[0] = selected0;
        _selected[1] = selected1;
        _timerACounter = timerACounter;
        _timerBCounter = timerBCounter;
        _status = status;
        _busyUntilMasterCycle = busyUntilMasterCycle;
    }

    private static int[] DoubleArrayToInt(double[] values)
    {
        int[] converted = new int[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            converted[i] = (int)Math.Clamp(values[i] * 32768.0, int.MinValue, int.MaxValue);
        }

        return converted;
    }

    private int RenderDacSample(double lowPassAlpha, double highPassFeedback)
    {
        double target = (_dacSample - 128) * DacOutputScale;
        _dacFilteredSample += (target - _dacFilteredSample) * lowPassAlpha;
        if (highPassFeedback <= 0.0)
        {
            return (int)Math.Round(_dacFilteredSample);
        }

        double output = _dacFilteredSample - _dacHighPassInput + (highPassFeedback * _dacHighPassOutput);
        _dacHighPassInput = _dacFilteredSample;
        _dacHighPassOutput = output;
        return (int)Math.Round(output);
    }

    public sealed record Ym2612ChannelSnapshot(int Channel, byte KeyOn, int Algorithm, int Feedback, int FNumber, int Block, int[] TotalLevels, int[] Envelopes, int[] Stages, int[] AttackRates, int[] DecayRates, int[] SustainRates, int[] ReleaseRates, int[] Multipliers, int[] Detunes, int PhaseModulationSensitivity, int AmplitudeModulationSensitivity);
    public sealed record Ym2612State(byte[,] Registers, byte[] Selected, int TimerACounter, int TimerBCounter, byte Status, byte DacSample, bool DacEnabled, byte[] KeyOn, int[] ChannelFNumbers, int[] ChannelBlocks, int[] Channel3SpecialFNumbers, int[] Channel3SpecialBlocks, uint[] Phase, int[] OperatorEnvelope, double[] OperatorEnvelopeRemainder, byte[] OperatorStage, int[] Feedback, int[] FeedbackPrevious, int[] AlgorithmMemory, bool[] SsgInverted, bool[] SsgHolding, double LfoPhase, long BusyUntilMasterCycle, double DacFilteredSample, double DacHighPassInput, double DacHighPassOutput);
    private readonly record struct WriteEvent(long MasterCycle, int Order, int Bank, byte Address, byte Value);
}
