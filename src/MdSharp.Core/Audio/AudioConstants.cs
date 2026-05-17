namespace MdSharp.Core.Audio;

public static class AudioConstants
{
    public const int DefaultSampleRate = 44_100;
    public const int StereoChannels = 2;
    public static readonly double YmMixLevel = ReadTuning("MDSHARP_YM_MIX_LEVEL", 0.85);
    public static readonly double PsgMixLevel = ReadTuning("MDSHARP_PSG_MIX_LEVEL", 1.05);
    private static readonly double[] YmChannelMixLevels = BuildChannelMixLevels("MDSHARP_YM_CH", [1.0, 0.65, 1.0, 1.0, 1.0, 0.70]);
    private static readonly double[] PsgChannelMixLevels = BuildChannelMixLevels("MDSHARP_PSG_CH", [1.0, 0.70, 1.0, 1.0]);
    public static readonly double MasterMixLevel = ReadTuning("MDSHARP_MASTER_MIX_LEVEL", 1.45);
    public static readonly double PsgLowPassCutoffHz = ReadTuning("MDSHARP_PSG_LOW_PASS_HZ", 4_500.0);
    public static readonly double BassShelfCutoffHz = ReadTuning("MDSHARP_BASS_SHELF_HZ", 160.0);
    public static readonly double BassShelfGain = ReadTuning("MDSHARP_BASS_SHELF_GAIN", 0.12);
    public static readonly double OutputLowPassCutoffHz = ReadTuning("MDSHARP_OUTPUT_LOW_PASS_HZ", 12_000.0);
    public static readonly double OutputSoftLimitThreshold = ReadTuning("MDSHARP_OUTPUT_SOFT_LIMIT", 30_000.0);

    private static double ReadTuning(string name, double fallback)
    {
        string? value = Environment.GetEnvironmentVariable(name);
        return double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : fallback;
    }

    private static double[] BuildChannelMixLevels(string prefix, double[] defaults)
    {
        double[] levels = (double[])defaults.Clone();
        for (int channel = 0; channel < levels.Length; channel++)
        {
            levels[channel] = Math.Max(0.0, ReadTuning($"{prefix}{channel + 1}_MIX_LEVEL", levels[channel]));
        }

        return levels;
    }

    public static double YmChannelMixLevel(int channel)
    {
        return (uint)channel < YmChannelMixLevels.Length ? YmChannelMixLevels[channel] : 1.0;
    }

    public static double PsgChannelMixLevel(int channel)
    {
        return (uint)channel < PsgChannelMixLevels.Length ? PsgChannelMixLevels[channel] : 1.0;
    }

    public static short ClampSample(double sample)
    {
        return (short)Math.Clamp(Math.Round(sample), short.MinValue, short.MaxValue);
    }

    public static short LimitOutputSample(double sample)
    {
        double sign = Math.Sign(sample);
        double magnitude = Math.Abs(sample);
        if (magnitude <= OutputSoftLimitThreshold)
        {
            return ClampSample(sample);
        }

        double headroom = short.MaxValue - OutputSoftLimitThreshold;
        if (headroom <= 0.0)
        {
            return ClampSample(sample);
        }

        double excess = magnitude - OutputSoftLimitThreshold;
        double limited = OutputSoftLimitThreshold + (headroom * (1.0 - Math.Exp(-excess / headroom)));
        return ClampSample(sign * limited);
    }
}
