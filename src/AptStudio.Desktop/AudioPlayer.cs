// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

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
    private Session? _session;
    private string? _error;
    private bool _disposed;

    public bool IsPlaying
    {
        get
        {
            lock (_sync)
            {
                return _session is not null;
            }
        }
    }

    public TimeSpan Position
    {
        get
        {
            Session? session;
            lock (_sync)
            {
                session = _session;
            }

            if (session is null || session.SampleRate == 0)
            {
                return TimeSpan.Zero;
            }

            long frames = Interlocked.Read(ref session.FramesDone);
            return TimeSpan.FromSeconds((double)frames / session.SampleRate);
        }
    }

    public string? TakeError()
    {
        lock (_sync)
        {
            string? error = _error;
            _error = null;
            return error;
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
        catch (Exception ex)
        {
            stream.Dispose();
            throw new InvalidDataException($"Ogg decode failed: {ex.Message}", ex);
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

        int headerSize = Marshal.SizeOf<WAVEHDR>();
        int chunkBytes = BufferFrames * channels * 2;
        List<IntPtr> headers = new();
        List<IntPtr> buffers = new();
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

        Session session;
        lock (_sync)
        {
            session = new Session(device, reader.SampleRate, reader.Channels);
            _session = session;
            _error = null;
        }

        Thread thread = new(() => Run(session, reader, stream, headers, buffers, chunkBytes))
        {
            IsBackground = true,
            Name = "APTStudio audio",
        };
        session.Thread = thread;
        thread.Start();
    }

    public void Stop()
    {
        Session? session;
        lock (_sync)
        {
            session = _session;
            if (session is not null)
            {
                session.Stop = true;
                _session = null;
            }
        }

        if (session is null)
        {
            return;
        }

        if (session.Device != IntPtr.Zero)
        {
            WaveOutReset(session.Device);
        }

        Thread? thread = session.Thread;
        if (thread is not null && thread != Thread.CurrentThread)
        {
            thread.Join(2000);
        }
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
        Session session,
        VorbisReader reader,
        MemoryStream stream,
        List<IntPtr> headers,
        List<IntPtr> buffers,
        int chunkBytes)
    {
        float[] samples = new float[BufferFrames * session.Channels];
        short[] pcm = new short[BufferFrames * session.Channels];
        int[] queuedFrames = new int[BufferCount];
        int[] queued = new int[BufferCount];
        int next = 0;
        int headerSize = Marshal.SizeOf<WAVEHDR>();
        try
        {
            while (!session.Stop)
            {
                if (queued[next] != 0)
                {
                    if (!IsDone(headers[next]))
                    {
                        Thread.Sleep(4);
                        continue;
                    }

                    Interlocked.Add(ref session.FramesDone, queuedFrames[next]);
                    queued[next] = 0;
                }

                int read = reader.ReadSamples(samples, 0, samples.Length);
                if (read == 0)
                {
                    break;
                }

                int frames = read / session.Channels;
                if (frames == 0)
                {
                    continue;
                }

                int total = frames * session.Channels;
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
                if (WaveOutWrite(session.Device, headers[next], headerSize) != 0)
                {
                    break;
                }

                queuedFrames[next] = frames;
                queued[next] = 1;
                next = (next + 1) % BufferCount;
            }

            if (!session.Stop)
            {
                bool pending = true;
                while (pending && !session.Stop)
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
                            Interlocked.Add(ref session.FramesDone, queuedFrames[i]);
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
        catch (Exception ex)
        {
            if (!session.Stop)
            {
                lock (_sync)
                {
                    if (ReferenceEquals(_session, session))
                    {
                        _error = $"Playback failed: {ex.Message}";
                    }
                }
            }
        }
        finally
        {
            if (session.Device != IntPtr.Zero)
            {
                WaveOutReset(session.Device);
                Cleanup(session.Device, headers, buffers);
                session.Device = IntPtr.Zero;
            }

            reader.Dispose();
            stream.Dispose();
            lock (_sync)
            {
                if (ReferenceEquals(_session, session))
                {
                    _session = null;
                }
            }
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

    private sealed class Session(IntPtr device, int sampleRate, int channels)
    {
        public volatile bool Stop;

        public IntPtr Device = device;

        public int SampleRate = sampleRate;

        public int Channels = channels;

        public long FramesDone;

        public Thread? Thread;
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
