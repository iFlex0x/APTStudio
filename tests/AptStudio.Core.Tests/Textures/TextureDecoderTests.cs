using System.Buffers.Binary;
using AptStudio.Core.Containers;
using AptStudio.Core.Textures;
using K4os.Compression.LZ4;

namespace AptStudio.Core.Tests.Textures;

public sealed class TextureDecoderTests
{
    private static readonly byte[] Bc1Red = [0x00, 0xF8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
    private static readonly byte[] Bc1Green = [0xE0, 0x07, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
    private static readonly byte[] Bc3RedOpaque =
        [0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, .. Bc1Red];

    [Fact]
    public void DecodesBc1Mip0()
    {
        (TextureEntry texture, byte[] bytes) = Build(Bc1Red);

        DecodedTexture image = TextureDecoder.DecodeMip0(new MemoryStream(bytes), texture);

        Assert.Equal(4, image.Width);
        Assert.Equal(4, image.Height);
        Assert.Equal(64, image.Rgba.Length);
        for (int i = 0; i < image.Rgba.Length; i += 4)
        {
            Assert.Equal(255, image.Rgba[i]);
            Assert.Equal(0, image.Rgba[i + 1]);
            Assert.Equal(0, image.Rgba[i + 2]);
            Assert.Equal(255, image.Rgba[i + 3]);
        }
    }

    [Fact]
    public void DecodesBc3Mip0()
    {
        (TextureEntry texture, byte[] bytes) = Build(Bc3RedOpaque, key: 0x001D);

        DecodedTexture image = TextureDecoder.DecodeMip0(new MemoryStream(bytes), texture);

        Assert.Equal(4, image.Width);
        Assert.Equal(4, image.Height);
        Assert.Equal(255, image.Rgba[0]);
        Assert.Equal(0, image.Rgba[1]);
        Assert.Equal(0, image.Rgba[2]);
        Assert.Equal(255, image.Rgba[3]);
    }

    [Fact]
    public void DecodesLz4()
    {
        (TextureEntry texture, byte[] bytes) = Build(Bc1Red, storage: BitmapStorage.Lz4);

        DecodedTexture image = TextureDecoder.DecodeMip0(new MemoryStream(bytes), texture);

        Assert.Equal(255, image.Rgba[0]);
        Assert.Equal(0, image.Rgba[1]);
        Assert.Equal(0, image.Rgba[2]);
        Assert.Equal(255, image.Rgba[3]);
    }

    [Fact]
    public void DecodesFirstMipOnly()
    {
        (TextureEntry texture, byte[] bytes) = Build([.. Bc1Red, .. Bc1Green], mips: 2);

        DecodedTexture image = TextureDecoder.DecodeMip0(new MemoryStream(bytes), texture);

        Assert.Equal(255, image.Rgba[0]);
        Assert.Equal(0, image.Rgba[1]);
    }

    [Fact]
    public void RejectsUnknownCodec()
    {
        (TextureEntry texture, byte[] bytes) = Build(Bc1Red, key: 0x001E);

        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            TextureDecoder.DecodeMip0(new MemoryStream(bytes), texture));

        Assert.Contains("0x1E", error.Message);
    }

    [Fact]
    public void RejectsVolume()
    {
        (TextureEntry texture, byte[] bytes) = Build(Bc1Red, key: 0x0013, kind: BitmapKind.Volume);

        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            TextureDecoder.DecodeMip0(new MemoryStream(bytes), texture));

        Assert.Contains("volume", error.Message);
    }

    [Fact]
    public void RejectsMultipleSurfaces()
    {
        (TextureEntry texture, byte[] bytes) = Build([.. Bc1Red, .. Bc1Red], surfaces: 2);

        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            TextureDecoder.DecodeMip0(new MemoryStream(bytes), texture));

        Assert.Contains("2 surfaces", error.Message);
    }

    [Fact]
    public void RejectsCube()
    {
        (TextureEntry texture, byte[] bytes) = Build(
            new byte[48], kind: BitmapKind.Cube, surfaces: 6);

        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            TextureDecoder.DecodeMip0(new MemoryStream(bytes), texture));

        Assert.Contains("cube", error.Message);
    }

    [Fact]
    public void RejectsLz4DecompressedSizeMismatch()
    {
        (TextureEntry texture, byte[] bytes) = Build(Bc1Red, storage: BitmapStorage.Lz4);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(BitmapHead.HeaderSize), 999);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            TextureDecoder.DecodeMip0(new MemoryStream(bytes), texture));

        Assert.Contains("decompressed", error.Message);
    }

    [Fact]
    public void RejectsShortPayload()
    {
        (TextureEntry texture, byte[] bytes) = Build(Bc1Red);
        byte[] truncated = bytes[..(BitmapHead.HeaderSize + 4)];

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            TextureDecoder.DecodeMip0(new MemoryStream(truncated), texture));

        Assert.Contains("short read", error.Message);
    }

    private static (TextureEntry Texture, byte[] Bytes) Build(
        byte[] chain,
        ushort key = 0x001B,
        BitmapStorage storage = BitmapStorage.Raw,
        BitmapKind kind = BitmapKind.Flat,
        int surfaces = 1,
        int width = 4,
        int height = 4,
        int mips = 1)
    {
        uint stored = (uint)chain.Length;
        uint compSize = 0;
        byte[] bytes;
        if (storage == BitmapStorage.Raw)
        {
            bytes = new byte[BitmapHead.HeaderSize + stored + 4];
            chain.CopyTo(bytes, BitmapHead.HeaderSize);
        }
        else
        {
            byte[] body = new byte[stored + 4];
            chain.CopyTo(body, 0);
            byte[] block = new byte[LZ4Codec.MaximumOutputSize(body.Length)];
            int blockLength = LZ4Codec.Encode(body, block);
            compSize = (uint)(blockLength + 8);
            bytes = new byte[BitmapHead.HeaderSize + compSize];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(BitmapHead.HeaderSize), (uint)body.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(BitmapHead.HeaderSize + 4), compSize);
            block.AsSpan(0, blockLength).CopyTo(bytes.AsSpan(BitmapHead.HeaderSize + 8));
        }

        byte[] header = BitmapTestData.Header(
            width, height, mips, stored, key,
            kind: kind, storage: storage, surfaces: surfaces, compressed: compSize);
        header.CopyTo(bytes, 0);

        BitmapHead head = BitmapHead.Parse(header, "test");
        DpcEntry row = new(
            0x1111, BitmapHead.ClassHash, 0x3333, 0,
            (uint)bytes.Length, 0, stored + 4, 0, 0);
        return (new TextureEntry("test.dpc", row, head), bytes);
    }
}
