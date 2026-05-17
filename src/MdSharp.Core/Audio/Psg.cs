namespace MdSharp.Core.Audio;

public sealed class Psg
{
    private const int DefaultClockHz = 3_579_545;
    private static readonly int OversampleFactor = Math.Clamp(ReadIntTuning("MDSHARP_PSG_OVERSAMPLE", 1), 1, 4);
    private static readonly bool OversampleHighFrequencyOnly = ReadBoolTuning("MDSHARP_PSG_OVERSAMPLE_HIGH_ONLY", false);
    private static readonly int OversampleHighFrequencyPeriod = Math.Clamp(ReadIntTuning("MDSHARP_PSG_OVERSAMPLE_PERIOD_MAX", 128), 1, 0x3FF);
    private static readonly int[] VolumeTable =
    [
        4096, 3254, 2584, 2053, 1631, 1295, 1029, 817,
        649, 516, 410, 325, 258, 205, 163, 0,
    ];

    private readonly byte[] _registers = new byte[8];
    private readonly int[] _tonePeriods = new int[3];
    private readonly int[] _toneCounters = new int[4];
    private readonly bool[] _toneOutputs = new bool[4];
    private readonly byte[] _audioInitialRegisters = new byte[8];
    private readonly int[] _audioInitialTonePeriods = new int[3];
    private readonly int[] _audioInitialToneCounters = new int[4];
    private readonly bool[] _audioInitialToneOutputs = new bool[4];
    private readonly List<PsgEvent> _events = new();
    private int _eventOrder;
    private ushort _noiseShift = 0x4000;
    private int _latchedRegister;
    private ushort _audioInitialNoiseShift;
    private int _audioInitialLatchedRegister;
    private double _audioInitialTickRemainder;
    private bool _audioFrameInitialStateValid;
    private long _audioFrameStartCycle;
    private long _audioFrameEndCycle = 1;
    private double _tickRemainder;
    private bool _recordingFrame;

    public ReadOnlySpan<byte> Registers => _registers;
    public ushort NoiseShift => _noiseShift;

    public Psg()
    {
        Reset();
    }

    private static int ReadIntTuning(string name, int fallback)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int parsed)
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

    public void Reset()
    {
        Array.Clear(_registers);
        _registers[1] = 0x0F;
        _registers[3] = 0x0F;
        _registers[5] = 0x0F;
        _registers[7] = 0x0F;
        Array.Clear(_tonePeriods);
        Array.Clear(_toneCounters);
        Array.Clear(_toneOutputs);
        Array.Clear(_audioInitialRegisters);
        _audioInitialRegisters[1] = 0x0F;
        _audioInitialRegisters[3] = 0x0F;
        _audioInitialRegisters[5] = 0x0F;
        _audioInitialRegisters[7] = 0x0F;
        Array.Clear(_audioInitialTonePeriods);
        Array.Clear(_audioInitialToneCounters);
        Array.Clear(_audioInitialToneOutputs);
        _events.Clear();
        _eventOrder = 0;
        _noiseShift = 0x4000;
        _latchedRegister = 0;
        _audioInitialNoiseShift = 0x4000;
        _audioInitialLatchedRegister = 0;
        _audioInitialTickRemainder = 0.0;
        _audioFrameInitialStateValid = false;
        _audioFrameStartCycle = 0;
        _audioFrameEndCycle = 1;
        _tickRemainder = 0.0;
        _recordingFrame = false;
    }

    public void BeginAudioFrame(long startCycle, long endCycle)
    {
        _audioFrameStartCycle = startCycle;
        _audioFrameEndCycle = Math.Max(startCycle + 1, endCycle);
        SaveAudioFrameInitialState();
        _events.Clear();
        _eventOrder = 0;
        _recordingFrame = true;
    }

    public void Write(byte value)
    {
        Write(value, _audioFrameEndCycle);
    }

    public void Write(byte value, long masterCycle)
    {
        ApplyWrite(value);
        QueueEvent(value, masterCycle);
    }

    private void ApplyWrite(byte value)
    {
        if ((value & 0x80) != 0)
        {
            _latchedRegister = (value >> 4) & 0x07;
            _registers[_latchedRegister] = (byte)((_registers[_latchedRegister] & 0xF0) | (value & 0x0F));
            if ((_latchedRegister & 1) == 0 && _latchedRegister < 6)
            {
                int channel = _latchedRegister / 2;
                _tonePeriods[channel] = (_tonePeriods[channel] & 0x3F0) | (value & 0x0F);
            }
            else if (_latchedRegister == 6)
            {
                ResetNoise();
            }

            return;
        }

        if ((_latchedRegister & 1) == 0 && _latchedRegister < 6)
        {
            _registers[_latchedRegister] = (byte)((_registers[_latchedRegister] & 0x0F) | ((value & 0x3F) << 4));
            int channel = _latchedRegister / 2;
            _tonePeriods[channel] = (_tonePeriods[channel] & 0x00F) | ((value & 0x3F) << 4);
        }
        else if ((_latchedRegister & 1) != 0)
        {
            _registers[_latchedRegister] = (byte)(value & 0x0F);
        }
        else if (_latchedRegister == 6)
        {
            _registers[_latchedRegister] = (byte)(value & 0x07);
            ResetNoise();
        }
    }

    public short[] RenderMonoSamples(int samples, int sampleRate = AudioConstants.DefaultSampleRate, int clockHz = DefaultClockHz)
    {
        short[] output = new short[samples];
        RenderMonoSamplesInto(output, samples, sampleRate, clockHz);
        return output;
    }

    public void RenderMonoSamplesInto(Span<short> output, int samples, int sampleRate = AudioConstants.DefaultSampleRate, int clockHz = DefaultClockHz)
    {
        if (output.Length < samples)
        {
            throw new ArgumentException("Output buffer is too small.", nameof(output));
        }

        if (_audioFrameInitialStateValid)
        {
            RestoreAudioFrameInitialState();
        }

        SortEvents();
        int eventIndex = 0;
        int oversample = OversampleFactor;
        double ticksPerSubSample = clockHz / (sampleRate * oversample * 16.0);
        int[] channelSums = new int[4];
        int[] channelLast = new int[4];
        for (int i = 0; i < samples; i++)
        {
            Array.Clear(channelSums);
            Array.Clear(channelLast);
            for (int subSample = 0; subSample < oversample; subSample++)
            {
                long sampleCycle = SubSampleCycle(i, subSample, samples, oversample);
                ApplyEventsThrough(sampleCycle, ref eventIndex);
                int ticks = AdvanceTicks(ticksPerSubSample);
                for (int channel = 0; channel < 3; channel++)
                {
                    StepToneChannel(channel, ticks);
                    int channelSample = ChannelSample(channel);
                    channelSums[channel] += channelSample;
                    channelLast[channel] = channelSample;
                }

                StepNoise(ticks);
                int noiseSample = ChannelSample(3);
                channelSums[3] += noiseSample;
                channelLast[3] = noiseSample;
            }

            int mixed = MixOversampledChannels(channelSums, channelLast, oversample);
            output[i] = (short)Math.Clamp(mixed, short.MinValue, short.MaxValue);
        }

        while (eventIndex < _events.Count)
        {
            ApplyWrite(_events[eventIndex].Value);
            eventIndex++;
        }

        _recordingFrame = false;
        _audioFrameInitialStateValid = false;
    }

    public void RenderMonoChannelStemsInto(Span<short> output, int samples, int sampleRate = AudioConstants.DefaultSampleRate, int clockHz = DefaultClockHz)
    {
        const int channelCount = 4;
        int required = samples * channelCount;
        if (output.Length < required)
        {
            throw new ArgumentException("Output buffer is too small.", nameof(output));
        }

        output[..required].Clear();
        if (_audioFrameInitialStateValid)
        {
            RestoreAudioFrameInitialState();
        }

        SortEvents();
        int eventIndex = 0;
        int oversample = OversampleFactor;
        double ticksPerSubSample = clockHz / (sampleRate * oversample * 16.0);
        int[] channelSums = new int[4];
        int[] channelLast = new int[4];
        for (int i = 0; i < samples; i++)
        {
            Array.Clear(channelSums);
            Array.Clear(channelLast);
            for (int subSample = 0; subSample < oversample; subSample++)
            {
                long sampleCycle = SubSampleCycle(i, subSample, samples, oversample);
                ApplyEventsThrough(sampleCycle, ref eventIndex);
                int ticks = AdvanceTicks(ticksPerSubSample);
                for (int channel = 0; channel < 3; channel++)
                {
                    StepToneChannel(channel, ticks);
                    int channelSample = ChannelSample(channel);
                    channelSums[channel] += channelSample;
                    channelLast[channel] = channelSample;
                }

                StepNoise(ticks);
                int noiseSample = ChannelSample(3);
                channelSums[3] += noiseSample;
                channelLast[3] = noiseSample;
            }

            for (int channel = 0; channel < channelCount; channel++)
            {
                int sample = MixOversampledChannel(channel, channelSums[channel], channelLast[channel], oversample);
                output[(channel * samples) + i] = (short)Math.Clamp(sample, short.MinValue, short.MaxValue);
            }
        }

        while (eventIndex < _events.Count)
        {
            ApplyWrite(_events[eventIndex].Value);
            eventIndex++;
        }

        _recordingFrame = false;
        _audioFrameInitialStateValid = false;
    }

    public short[] RenderStereoSamples(int samples, int sampleRate = AudioConstants.DefaultSampleRate, int clockHz = DefaultClockHz)
    {
        short[] output = new short[samples * AudioConstants.StereoChannels];
        if (_audioFrameInitialStateValid)
        {
            RestoreAudioFrameInitialState();
        }

        SortEvents();
        int eventIndex = 0;
        int oversample = OversampleFactor;
        double ticksPerSubSample = clockHz / (sampleRate * oversample * 16.0);
        int[] channelSums = new int[4];
        int[] channelLast = new int[4];
        for (int i = 0; i < samples; i++)
        {
            Array.Clear(channelSums);
            Array.Clear(channelLast);
            for (int subSample = 0; subSample < oversample; subSample++)
            {
                long sampleCycle = SubSampleCycle(i, subSample, samples, oversample);
                ApplyEventsThrough(sampleCycle, ref eventIndex);
                int ticks = AdvanceTicks(ticksPerSubSample);
                for (int channel = 0; channel < 3; channel++)
                {
                    StepToneChannel(channel, ticks);
                    int channelSample = ChannelSample(channel);
                    channelSums[channel] += channelSample;
                    channelLast[channel] = channelSample;
                }

                StepNoise(ticks);
                int noiseSample = ChannelSample(3);
                channelSums[3] += noiseSample;
                channelLast[3] = noiseSample;
            }

            int mixed = MixOversampledChannels(channelSums, channelLast, oversample);
            short sample = (short)Math.Clamp(mixed, short.MinValue, short.MaxValue);
            output[i * 2] = sample;
            output[(i * 2) + 1] = sample;
        }

        while (eventIndex < _events.Count)
        {
            ApplyWrite(_events[eventIndex].Value);
            eventIndex++;
        }

        _recordingFrame = false;
        _audioFrameInitialStateValid = false;
        return output;
    }

    public int TonePeriod(int channel)
    {
        if ((uint)channel >= 3)
        {
            throw new ArgumentOutOfRangeException(nameof(channel));
        }

        int period = _tonePeriods[channel] & 0x3FF;
        return period == 0 ? 1 : period;
    }

    public double ToneFrequencyHz(int channel, int clockHz = DefaultClockHz)
    {
        return clockHz / (32.0 * TonePeriod(channel));
    }

    public int Volume(int channel)
    {
        if ((uint)channel >= 4)
        {
            throw new ArgumentOutOfRangeException(nameof(channel));
        }

        return _registers[(channel * 2) + 1] & 0x0F;
    }

    public PsgChannelSnapshot[] GetChannelSnapshots()
    {
        return
        [
            new PsgChannelSnapshot(0, TonePeriod(0), ToneFrequencyHz(0), Volume(0), ChannelAmplitude(0), _toneOutputs[0], _toneCounters[0], _registers[0], _registers[1]),
            new PsgChannelSnapshot(1, TonePeriod(1), ToneFrequencyHz(1), Volume(1), ChannelAmplitude(1), _toneOutputs[1], _toneCounters[1], _registers[2], _registers[3]),
            new PsgChannelSnapshot(2, TonePeriod(2), ToneFrequencyHz(2), Volume(2), ChannelAmplitude(2), _toneOutputs[2], _toneCounters[2], _registers[4], _registers[5]),
            new PsgChannelSnapshot(3, NoisePeriod(), 0.0, Volume(3), ChannelAmplitude(3), _toneOutputs[3], _toneCounters[3], _registers[6], _registers[7]),
        ];
    }

    public PsgNoiseSnapshot GetNoiseSnapshot()
    {
        int control = _registers[6] & 0x07;
        return new PsgNoiseSnapshot(control, (control & 0x04) != 0, control & 0x03, NoisePeriod(), _noiseShift);
    }

    private void StepToneChannel(int channel, int ticks)
    {
        int period = TonePeriod(channel);
        _toneCounters[channel] -= ticks;
        while (_toneCounters[channel] <= 0)
        {
            _toneCounters[channel] += period;
            _toneOutputs[channel] = !_toneOutputs[channel];
        }
    }

    private void StepNoise(int ticks)
    {
        int noise = _registers[6] & 0x07;
        int period = NoisePeriod();

        _toneCounters[3] -= ticks;
        while (_toneCounters[3] <= 0)
        {
            _toneCounters[3] += Math.Max(1, period);
            int feedbackBit = (noise & 0x04) != 0
                ? ((_noiseShift ^ (_noiseShift >> 3)) & 1)
                : (_noiseShift & 1);
            int feedback = feedbackBit != 0 ? 0x4000 : 0;
            _noiseShift = (ushort)((_noiseShift >> 1) | feedback);
            _toneOutputs[3] = (_noiseShift & 1) != 0;
        }
    }

    private void ResetNoise()
    {
        _noiseShift = 0x4000;
        _toneOutputs[3] = false;
        _toneCounters[3] = Math.Max(1, NoisePeriod());
    }

    private int NoisePeriod()
    {
        int noise = _registers[6] & 0x07;
        return (noise & 0x03) switch
        {
            0 => 0x10,
            1 => 0x20,
            2 => 0x40,
            _ => TonePeriod(2),
        };
    }

    private int ChannelAmplitude(int channel)
    {
        int volume = Volume(channel);
        if (volume >= 15)
        {
            return 0;
        }

        return VolumeTable[volume];
    }

    private int ChannelSample(int channel)
    {
        int amplitude = ChannelAmplitude(channel);
        return _toneOutputs[channel] ? amplitude : -amplitude;
    }

    private int AdvanceTicks(double ticksPerSubSample)
    {
        _tickRemainder += ticksPerSubSample;
        int ticks = Math.Max(1, (int)_tickRemainder);
        _tickRemainder -= ticks;
        return ticks;
    }

    private int MixOversampledChannels(int[] channelSums, int[] channelLast, int oversample)
    {
        double mixed = 0.0;
        for (int channel = 0; channel < 4; channel++)
        {
            mixed += MixOversampledChannel(channel, channelSums[channel], channelLast[channel], oversample) * AudioConstants.PsgChannelMixLevel(channel);
        }

        return (int)Math.Clamp(Math.Round(mixed), short.MinValue, short.MaxValue);
    }

    private int MixOversampledChannel(int channel, int sum, int last, int oversample)
    {
        if (oversample <= 1 || (OversampleHighFrequencyOnly && !ShouldAverageChannel(channel)))
        {
            return last;
        }

        return (int)Math.Round(sum / (double)oversample);
    }

    private bool ShouldAverageChannel(int channel)
    {
        return channel < 3 && TonePeriod(channel) <= OversampleHighFrequencyPeriod;
    }

    private void ApplyEventsThrough(long sampleCycle, ref int eventIndex)
    {
        while (eventIndex < _events.Count && _events[eventIndex].MasterCycle <= sampleCycle)
        {
            ApplyWrite(_events[eventIndex].Value);
            eventIndex++;
        }
    }

    private void QueueEvent(byte value, long masterCycle)
    {
        if (!_recordingFrame)
        {
            return;
        }

        long clampedCycle = Math.Clamp(masterCycle, _audioFrameStartCycle, _audioFrameEndCycle - 1);
        _events.Add(new PsgEvent(clampedCycle, _eventOrder++, value));
    }

    private void SortEvents()
    {
        if (_events.Count > 1)
        {
            _events.Sort(static (left, right) =>
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

    private long SubSampleCycle(int sampleIndex, int subSampleIndex, int sampleCount, int oversample)
    {
        long span = Math.Max(1, _audioFrameEndCycle - _audioFrameStartCycle);
        long subSample = ((long)sampleIndex * oversample) + subSampleIndex;
        long subSampleCount = Math.Max(1L, (long)sampleCount * oversample);
        return _audioFrameStartCycle + ((span * subSample) / subSampleCount);
    }

    public PsgState CaptureState()
    {
        return new PsgState((byte[])_registers.Clone(), (int[])_tonePeriods.Clone(), (int[])_toneCounters.Clone(), (bool[])_toneOutputs.Clone(), _noiseShift, _latchedRegister, _tickRemainder);
    }

    public void RestoreState(PsgState state)
    {
        Array.Copy(state.Registers, _registers, Math.Min(_registers.Length, state.Registers.Length));
        Array.Copy(state.TonePeriods, _tonePeriods, Math.Min(_tonePeriods.Length, state.TonePeriods.Length));
        Array.Copy(state.ToneCounters, _toneCounters, Math.Min(_toneCounters.Length, state.ToneCounters.Length));
        Array.Copy(state.ToneOutputs, _toneOutputs, Math.Min(_toneOutputs.Length, state.ToneOutputs.Length));
        _noiseShift = state.NoiseShift;
        _latchedRegister = state.LatchedRegister;
        _tickRemainder = state.TickRemainder;
        _audioFrameInitialStateValid = false;
    }

    private void SaveAudioFrameInitialState()
    {
        Array.Copy(_registers, _audioInitialRegisters, _registers.Length);
        Array.Copy(_tonePeriods, _audioInitialTonePeriods, _tonePeriods.Length);
        Array.Copy(_toneCounters, _audioInitialToneCounters, _toneCounters.Length);
        Array.Copy(_toneOutputs, _audioInitialToneOutputs, _toneOutputs.Length);
        _audioInitialNoiseShift = _noiseShift;
        _audioInitialLatchedRegister = _latchedRegister;
        _audioInitialTickRemainder = _tickRemainder;
        _audioFrameInitialStateValid = true;
    }

    private void RestoreAudioFrameInitialState()
    {
        Array.Copy(_audioInitialRegisters, _registers, _registers.Length);
        Array.Copy(_audioInitialTonePeriods, _tonePeriods, _tonePeriods.Length);
        Array.Copy(_audioInitialToneCounters, _toneCounters, _toneCounters.Length);
        Array.Copy(_audioInitialToneOutputs, _toneOutputs, _toneOutputs.Length);
        _noiseShift = _audioInitialNoiseShift;
        _latchedRegister = _audioInitialLatchedRegister;
        _tickRemainder = _audioInitialTickRemainder;
    }

    public sealed record PsgState(byte[] Registers, int[] TonePeriods, int[] ToneCounters, bool[] ToneOutputs, ushort NoiseShift, int LatchedRegister, double TickRemainder);
    public readonly record struct PsgChannelSnapshot(int Channel, int Period, double FrequencyHz, int Volume, int Amplitude, bool OutputHigh, int Counter, byte ToneOrNoiseRegister, byte VolumeRegister);
    public readonly record struct PsgNoiseSnapshot(int Control, bool WhiteNoise, int PeriodMode, int Period, ushort Shift);
    private readonly record struct PsgEvent(long MasterCycle, int Order, byte Value);
}
