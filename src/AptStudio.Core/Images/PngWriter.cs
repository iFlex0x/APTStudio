using System.Buffers.Binary;
using System.IO.Compression;

namespace AptStudio.Core.Images;

public static class PngWriter
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static byte[] EncodeRgba(ReadOnlySpan<byte> rgba, int width, int height)
    {
        using var output = new MemoryStream();
        WriteRgba(output, rgba, width, height);
        return output.ToArray();
    }

    public static void WriteRgba(Stream output, ReadOnlySpan<byte> rgba, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (rgba.Length % 4 != 0 || rgba.Length / 4 != (long)width * height)
        {
            throw new ArgumentException("RGBA buffer length does not match the image size.", nameof(rgba));
        }

        output.Write(Signature);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header[..4], width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..8], height);
        header[8] = 8;
        header[9] = 6;
        header[10] = 0;
        header[11] = 0;
        header[12] = 0;
        WriteChunk(output, "IHDR"u8, header);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            int stride = width * 4;
            for (int y = 0; y < height; y++)
            {
                zlib.WriteByte(0);
                zlib.Write(rgba.Slice(y * stride, stride));
            }
        }

        WriteChunk(output, "IDAT"u8, compressed.GetBuffer().AsSpan(0, (int)compressed.Length));
        WriteChunk(output, "IEND"u8, ReadOnlySpan<byte>.Empty);
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, data.Length);
        output.Write(buffer);
        output.Write(type);
        output.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, Crc32.Compute(type, data));
        output.Write(buffer);
    }
}
