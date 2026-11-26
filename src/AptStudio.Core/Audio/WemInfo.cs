using System.Buffers.Binary;
using System.Text;

namespace AptStudio.Core.Audio;

public sealed record WemInfo(
    ushort FormatTag,
    ushort Channels,
    uint SampleRate,
    uint SampleCount,
    string? Cue)
{
    public double DurationSeconds =>
        SampleRate == 0 ? 0 : (double)SampleCount / SampleRate;

    public string Codec => FormatTag switch
    {
        0x0001 => "PCM",
        0x0002 => "ADPCM",
        0x0166 => "XMA2",
        0xFFFF => "Vorbis",
        _ => $"0x{FormatTag:X4}",
    };

    /// <summary>
    /// Reads the header of a WEM. Returns null when the body is not a RIFF
    /// wave, which happens for the plugin objects in SHARED_MEDIA.
    /// </summary>
    public static WemInfo? TryRead(Stream stream, long offset, long length, string source)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (length < 12)
        {
            throw new InvalidDataException($"{source}: WEM is {length} bytes.");
        }

        byte[] riff = ReadExactly(stream, offset, 12, source);
        if (riff[0] != (byte)'R' || riff[1] != (byte)'I' || riff[2] != (byte)'F' || riff[3] != (byte)'F'
            || riff[8] != (byte)'W' || riff[9] != (byte)'A'
            || riff[10] != (byte)'V' || riff[11] != (byte)'E')
        {
            return null;
        }

        long end = offset + length;
        long at = offset + 12;
        ushort formatTag = 0;
        ushort channels = 0;
        uint sampleRate = 0;
        uint sampleCount = 0;
        bool foundFormat = false;
        string? cue = null;
        byte[] chunk = new byte[8];
        int chunkCount = 0;
        while (at + 8 <= end && chunkCount++ < 32)
        {
            stream.Seek(at, SeekOrigin.Begin);
            stream.ReadExactly(chunk, 0, 8);
            string tag = Encoding.ASCII.GetString(chunk, 0, 4);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(chunk.AsSpan(4));
            long body = at + 8;
            if (body + size > end)
            {
                throw new InvalidDataException(
                    $"{source}: chunk {tag} at {at} runs past the WEM.");
            }

            if (tag == "fmt ")
            {
                byte[] format = ReadExactly(stream, body, (int)Math.Min(size, 128), source);
                if (format.Length < 16)
                {
                    throw new InvalidDataException($"{source}: fmt chunk is {format.Length} bytes.");
                }

                formatTag = BinaryPrimitives.ReadUInt16LittleEndian(format);
                channels = BinaryPrimitives.ReadUInt16LittleEndian(format.AsSpan(2));
                sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(format.AsSpan(4));
                if (format.Length >= 28)
                {
                    sampleCount = BinaryPrimitives.ReadUInt32LittleEndian(format.AsSpan(24));
                }

                foundFormat = true;
            }
            else if (tag == "LIST" && cue is null)
            {
                cue = ReadCue(stream, body, (int)Math.Min(size, 4096), source);
            }
            else if (tag == "data" && foundFormat)
            {
                break;
            }

            at = body + size + (size & 1);
        }

        if (!foundFormat || formatTag == 0)
        {
            throw new InvalidDataException($"{source}: no measured fmt chunk.");
        }

        return new WemInfo(formatTag, channels, sampleRate, sampleCount, cue);
    }

    private static string? ReadCue(Stream stream, long offset, int size, string source)
    {
        byte[] list = ReadExactly(stream, offset, size, source);
        if (list.Length < 4
            || list[0] != (byte)'a' || list[1] != (byte)'d'
            || list[2] != (byte)'t' || list[3] != (byte)'l')
        {
            return null;
        }

        int at = 4;
        while (at + 8 <= list.Length)
        {
            string tag = Encoding.ASCII.GetString(list, at, 4);
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(list.AsSpan(at + 4));
            if (at + 8L + length > list.Length)
            {
                return null;
            }

            if (tag == "labl" && length > 5)
            {
                string text = Encoding.UTF8
                    .GetString(list, at + 12, (int)length - 4)
                    .Split('\0')[0]
                    .Trim();
                // VO files carry a "v1.5" label that names the exporter, not the clip.
                if (text.Length > 0 && !(text[0] == 'v' && text.Contains('.') && text[1..].All(c => char.IsAsciiDigit(c) || c == '.')))
                {
                    return text;
                }
            }

            at += 8 + (int)length + ((int)length & 1);
        }

        return null;
    }

    private static byte[] ReadExactly(Stream stream, long offset, int count, string source)
    {
        byte[] buffer = new byte[count];
        stream.Seek(offset, SeekOrigin.Begin);
        int read = 0;
        while (read < count)
        {
            int n = stream.Read(buffer, read, count - read);
            if (n == 0)
            {
                throw new InvalidDataException($"{source}: short read at {offset}.");
            }

            read += n;
        }

        return buffer;
    }
}
