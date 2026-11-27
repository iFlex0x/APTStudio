using System.IO.Compression;
using System.Text;
using AptStudio.Core.Images;

namespace AptStudio.Core.Tests.Images;

public sealed class PngWriterTests
{
    [Fact]
    public void EncodesSignatureAndChunks()
    {
        byte[] png = PngWriter.EncodeRgba([255, 0, 0, 255, 0, 255, 0, 255], 2, 1);

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        Assert.Equal("IHDR", Encoding.ASCII.GetString(png, 12, 4));
        Assert.Equal(13u, ReadBigEndian(png, 8));
        Assert.Equal(2u, ReadBigEndian(png, 16));
        Assert.Equal(1u, ReadBigEndian(png, 20));
        Assert.Equal(8, png[24]);
        Assert.Equal(6, png[25]);
        Assert.Equal("IEND", Encoding.ASCII.GetString(png, png.Length - 8, 4));
    }

    [Fact]
    public void IdatDecompressesToFilteredRows()
    {
        byte[] png = PngWriter.EncodeRgba([255, 0, 0, 255, 0, 255, 0, 255], 2, 1);
        int idat = IndexOf(png, "IDAT"u8);
        int length = (int)ReadBigEndian(png, idat - 4);
        byte[] compressed = png[(idat + 4)..(idat + 4 + length)];

        using var input = new MemoryStream(compressed);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zlib.CopyTo(output);

        Assert.Equal(new byte[] { 0, 255, 0, 0, 255, 0, 255, 0, 255 }, output.ToArray());
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    public void RejectsInvalidDimensions(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PngWriter.EncodeRgba([], width, height));
    }

    [Fact]
    public void RejectsWrappedBufferArithmetic()
    {
        // 65536 * 16384 * 4 wraps to 0 in 32-bit arithmetic, which would match an empty buffer.
        ArgumentException error = Assert.Throws<ArgumentException>(
            () => PngWriter.EncodeRgba([], 65536, 16384));

        Assert.Equal("rgba", error.ParamName);
    }

    [Fact]
    public void RejectsMismatchedBuffer()
    {
        Assert.Throws<ArgumentException>(() => PngWriter.EncodeRgba(new byte[3], 1, 1));
    }

    [Fact]
    public void Crc32KnownAnswer()
    {
        Assert.Equal(0xCBF43926u, Crc32.Compute("123456789"u8));
    }

    private static uint ReadBigEndian(byte[] data, int offset) =>
        (uint)(data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3]);

    private static int IndexOf(byte[] data, ReadOnlySpan<byte> value)
    {
        for (int i = 0; i <= data.Length - value.Length; i++)
        {
            if (data.AsSpan(i, value.Length).SequenceEqual(value))
            {
                return i;
            }
        }

        return -1;
    }
}
