using System.Runtime.InteropServices;

namespace MdSharp.Desktop;

internal sealed class WaveOutAudio : IDisposable
{
    private const int WaveMapper = -1;
    private const int CallbackNull = 0;
    private const int WhdrDone = 0x00000001;
    private const int StartupSilenceMilliseconds = 50;

    private readonly List<PendingBuffer> _pending = new();
    private readonly int _sampleRate;
    private readonly ushort _channels;
    private IntPtr _device;
    private bool _disposed;

    public WaveOutAudio(int sampleRate = 44_100, ushort channels = 2)
    {
        _sampleRate = sampleRate;
        _channels = channels;
        ushort blockAlign = (ushort)(channels * sizeof(short));
        WaveFormat format = new()
        {
            FormatTag = 1,
            Channels = channels,
            SamplesPerSec = sampleRate,
            BitsPerSample = 16,
            BlockAlign = blockAlign,
            AvgBytesPerSec = sampleRate * blockAlign,
            Size = 0,
        };

        int result = waveOutOpen(out _device, WaveMapper, ref format, IntPtr.Zero, IntPtr.Zero, CallbackNull);
        if (result != 0)
        {
            throw new InvalidOperationException($"waveOutOpen failed with code {result}.");
        }

        QueueSilence(StartupSilenceMilliseconds);
    }

    public void Queue(short[] samples)
    {
        Queue(samples, samples.Length);
    }

    public void Queue(short[] samples, int length)
    {
        if (_disposed || length <= 0)
        {
            return;
        }

        CleanupFinished();
        if (_pending.Count >= 8)
        {
            return;
        }

        int clampedLength = Math.Min(length, samples.Length);
        int bytes = clampedLength * sizeof(short);
        IntPtr data = Marshal.AllocHGlobal(bytes);
        Marshal.Copy(samples, 0, data, clampedLength);

        WaveHeader header = new()
        {
            Data = data,
            BufferLength = bytes,
        };

        IntPtr headerPtr = Marshal.AllocHGlobal(Marshal.SizeOf<WaveHeader>());
        Marshal.StructureToPtr(header, headerPtr, false);

        int prepare = waveOutPrepareHeader(_device, headerPtr, Marshal.SizeOf<WaveHeader>());
        if (prepare != 0)
        {
            Marshal.FreeHGlobal(headerPtr);
            Marshal.FreeHGlobal(data);
            return;
        }

        int write = waveOutWrite(_device, headerPtr, Marshal.SizeOf<WaveHeader>());
        if (write != 0)
        {
            waveOutUnprepareHeader(_device, headerPtr, Marshal.SizeOf<WaveHeader>());
            Marshal.FreeHGlobal(headerPtr);
            Marshal.FreeHGlobal(data);
            return;
        }

        _pending.Add(new PendingBuffer(data, headerPtr));
    }

    private void QueueSilence(int milliseconds)
    {
        int samples = Math.Max(1, _sampleRate * milliseconds / 1000);
        Queue(new short[samples * _channels]);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_device != IntPtr.Zero)
        {
            waveOutReset(_device);
            foreach (PendingBuffer buffer in _pending)
            {
                Free(buffer);
            }

            _pending.Clear();
            waveOutClose(_device);
            _device = IntPtr.Zero;
        }
    }

    private void CleanupFinished()
    {
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            WaveHeader header = Marshal.PtrToStructure<WaveHeader>(_pending[i].Header);
            if ((header.Flags & WhdrDone) == 0)
            {
                continue;
            }

            Free(_pending[i]);
            _pending.RemoveAt(i);
        }
    }

    private void Free(PendingBuffer buffer)
    {
        waveOutUnprepareHeader(_device, buffer.Header, Marshal.SizeOf<WaveHeader>());
        Marshal.FreeHGlobal(buffer.Header);
        Marshal.FreeHGlobal(buffer.Data);
    }

    [DllImport("winmm.dll")]
    private static extern int waveOutOpen(out IntPtr waveOut, int deviceId, ref WaveFormat format, IntPtr callback, IntPtr instance, int flags);

    [DllImport("winmm.dll")]
    private static extern int waveOutPrepareHeader(IntPtr waveOut, IntPtr header, int headerSize);

    [DllImport("winmm.dll")]
    private static extern int waveOutWrite(IntPtr waveOut, IntPtr header, int headerSize);

    [DllImport("winmm.dll")]
    private static extern int waveOutUnprepareHeader(IntPtr waveOut, IntPtr header, int headerSize);

    [DllImport("winmm.dll")]
    private static extern int waveOutReset(IntPtr waveOut);

    [DllImport("winmm.dll")]
    private static extern int waveOutClose(IntPtr waveOut);

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveFormat
    {
        public ushort FormatTag;
        public ushort Channels;
        public int SamplesPerSec;
        public int AvgBytesPerSec;
        public ushort BlockAlign;
        public ushort BitsPerSample;
        public ushort Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public IntPtr Data;
        public int BufferLength;
        public int BytesRecorded;
        public IntPtr User;
        public int Flags;
        public int Loops;
        public IntPtr Next;
        public IntPtr Reserved;
    }

    private readonly record struct PendingBuffer(IntPtr Data, IntPtr Header);
}
