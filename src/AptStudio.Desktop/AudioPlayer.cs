using System.Runtime.InteropServices;
using NVorbis;

namespace AptStudio.Desktop;

/// <summary>
/// Plays an Ogg stream through winmm. Two rotating buffers keep conversion
/// and device writes independent; the caller only sees play, stop, position.
/// </summary>
internal sealed class AudioPlayer : IDisposable
{
    private const int BufferFrames = 4096;
    private const int BufferCount = 2;
    private const uint WaveMapper = unchecked((uint)-1);
    private const uint WhdrDone = 0x01;

    private readonly object _sync = new();
    private Thread? _thread;
    private IntPtr _device;
    private volatile bool _stop;
    private volatile bool _playing;
    private long _framesDone;
    private int _sampleRate;
    private bool _disposed;

    public bool IsPlaying => _playing;

    public TimeSpan Position
    {
        get
        {
            long frames = Interlocked.Read(ref _framesDone);
            int rate = Volatile.Read(ref _sampleRate);
            return rate == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds((double)frames / rate);
        }
    }

    public void Play(byte[] ogg)
    {
        ArgumentNullException.ThrowIfNull(ogg);
        ObjectDisposedException.ThrowIf(_disposed, this);
        Stop();

        MemoryStream stream = new(ogg, writable: false);
        VorbisReader reader;
        try
        {
            reader = new VorbisReader(stream);
        }
        catch
        {
            stream.Dispose();
            throw;
        }

        int rate = reader.SampleRate;
        int channels = reader.Channels;
        WAVEFORMATEX format = new()
        {
            FormatTag = 1,
            Channels = (ushort)channels,
            SamplesPerSec = (uint)rate,
            AvgBytesPerSec = (uint)(rate * channels * 2),
            BlockAlign = (ushort)(channels * 2),
            BitsPerSample = 16,
            Size = 0,
        };

        int result = WaveOutOpen(out IntPtr device, WaveMapper, ref format, IntPtr.Zero, IntPtr.Zero, 0);
        if (result != 0)
        {
            reader.Dispose();
            stream.Dispose();
            throw new InvalidOperationException($"Audio device open failed ({result}).");
        }

        List<IntPtr> headers = new();
        List<IntPtr> buffers = new();
        int headerSize = Marshal.SizeOf<WAVEHDR>();
        int chunkBytes = BufferFrames * channels * 2;
        for (int i = 0; i < BufferCount; i++)
        {
            WAVEHDR header = new()
            {
                Data = Marshal.AllocHGlobal(chunkBytes),
                BufferLength = (uint)chunkBytes,
            };
            IntPtr pointer = Marshal.AllocHGlobal(headerSize);
            Marshal.StructureToPtr(header, pointer, false);
            if (WaveOutPrepareHeader(device, pointer, headerSize) != 0)
            {
                Marshal.FreeHGlobal(pointer);
                Marshal.FreeHGlobal(header.Data);
                Cleanup(device, headers, buffers);
                reader.Dispose();
                stream.Dispose();
                throw new InvalidOperationException("Audio buffer preparation failed.");
            }

            headers.Add(pointer);
            buffers.Add(header.Data);
        }

        lock (_sync)
        {
            _device = device;
            _sampleRate = rate;
            _framesDone = 0;
            _stop = false;
            _playing = true;
        }

        Thread thread = new(() => Run(reader, stream, device, headers, buffers, channels, rate, chunkBytes))
        {
            IsBackground = true,
            Name = "APTStudio audio",
        };
        _thread = thread;
        thread.Start();
    }

    public void Stop()
    {
        Thread? thread;
        lock (_sync)
        {
            _stop = true;
            thread = _thread;
            _thread = null;
            if (_device != IntPtr.Zero)
            {
                WaveOutReset(_device);
            }
        }

        if (thread is not null && thread != Thread.CurrentThread)
        {
            thread.Join(2000);
        }

        _playing = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }

    private void Run(
        VorbisReader reader,
        MemoryStream stream,
        IntPtr device,
        List<IntPtr> headers,
        List<IntPtr> buffers,
        int channels,
        int rate,
        int chunkBytes)
    {
        float[] samples = new float[BufferFrames * channels];
        short[] pcm = new short[BufferFrames * channels];
        int[] queuedFrames = new int[BufferCount];
        int[] queued = new int[BufferCount];
        int next = 0;
        int headerSize = Marshal.SizeOf<WAVEHDR>();
        try
        {
            while (!_stop)
            {
                if (queued[next] != 0)
                {
                    if (!IsDone(headers[next]))
                    {
                        Thread.Sleep(4);
                        continue;
                    }

                    Interlocked.Add(ref _framesDone, queuedFrames[next]);
                    queued[next] = 0;
                }

                int read = reader.ReadSamples(samples, 0, samples.Length);
                if (read == 0)
                {
                    break;
                }

                int frames = read / channels;
                if (frames == 0)
                {
                    continue;
                }

                int total = frames * channels;
                for (int i = 0; i < total; i++)
                {
                    pcm[i] = (short)Math.Clamp(samples[i] * 32767f, -32768f, 32767f);
                }

                int bytes = total * 2;
                Marshal.Copy(pcm, 0, buffers[next], total);
                WAVEHDR header = Marshal.PtrToStructure<WAVEHDR>(headers[next]);
                header.BufferLength = (uint)bytes;
                header.Flags &= ~WhdrDone;
                Marshal.StructureToPtr(header, headers[next], false);
                if (WaveOutWrite(device, headers[next], headerSize) != 0)
                {
                    break;
                }

                queuedFrames[next] = frames;
                queued[next] = 1;
                next = (next + 1) % BufferCount;
            }

            if (!_stop)
            {
                bool pending = true;
                while (pending && !_stop)
                {
                    pending = false;
                    for (int i = 0; i < BufferCount; i++)
                    {
                        if (queued[i] == 0)
                        {
                            continue;
                        }

                        if (IsDone(headers[i]))
                        {
                            Interlocked.Add(ref _framesDone, queuedFrames[i]);
                            queued[i] = 0;
                        }
                        else
                        {
                            pending = true;
                        }
                    }

                    if (pending)
                    {
                        Thread.Sleep(10);
                    }
                }
            }
        }
        finally
        {
            lock (_sync)
            {
                if (_device == device)
                {
                    WaveOutReset(device);
                    _device = IntPtr.Zero;
                }
            }

            Cleanup(device, headers, buffers);
            reader.Dispose();
            stream.Dispose();
            _playing = false;
        }
    }

    private static bool IsDone(IntPtr header)
    {
        WAVEHDR value = Marshal.PtrToStructure<WAVEHDR>(header);
        return (value.Flags & WhdrDone) != 0;
    }

    private static void Cleanup(IntPtr device, List<IntPtr> headers, List<IntPtr> buffers)
    {
        int headerSize = Marshal.SizeOf<WAVEHDR>();
        foreach (IntPtr header in headers)
        {
            WaveOutUnprepareHeader(device, header, headerSize);
            Marshal.FreeHGlobal(header);
        }

        foreach (IntPtr buffer in buffers)
        {
            Marshal.FreeHGlobal(buffer);
        }

        WaveOutClose(device);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WAVEFORMATEX
    {
        public ushort FormatTag;
        public ushort Channels;
        public uint SamplesPerSec;
        public uint AvgBytesPerSec;
        public ushort BlockAlign;
        public ushort BitsPerSample;
        public ushort Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WAVEHDR
    {
        public IntPtr Data;
        public uint BufferLength;
        public uint BytesRecorded;
        public IntPtr User;
        public uint Flags;
        public uint Loops;
        public IntPtr Next;
        public IntPtr Reserved;
    }

    [DllImport("winmm.dll", EntryPoint = "waveOutOpen")]
    private static extern int WaveOutOpen(
        out IntPtr device,
        uint deviceId,
        ref WAVEFORMATEX format,
        IntPtr callback,
        IntPtr instance,
        int flags);

    [DllImport("winmm.dll", EntryPoint = "waveOutPrepareHeader")]
    private static extern int WaveOutPrepareHeader(IntPtr device, IntPtr header, int size);

    [DllImport("winmm.dll", EntryPoint = "waveOutUnprepareHeader")]
    private static extern int WaveOutUnprepareHeader(IntPtr device, IntPtr header, int size);

    [DllImport("winmm.dll", EntryPoint = "waveOutWrite")]
    private static extern int WaveOutWrite(IntPtr device, IntPtr header, int size);

    [DllImport("winmm.dll", EntryPoint = "waveOutReset")]
    private static extern int WaveOutReset(IntPtr device);

    [DllImport("winmm.dll", EntryPoint = "waveOutClose")]
    private static extern int WaveOutClose(IntPtr device);
}
