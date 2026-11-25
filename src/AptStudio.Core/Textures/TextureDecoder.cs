using System.Buffers.Binary;
using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using K4os.Compression.LZ4;

namespace AptStudio.Core.Textures;

public sealed record DecodedTexture(int Width, int Height, byte[] Rgba);

public static class TextureDecoder
{
    public static DecodedTexture DecodeMip0(Stream stream, TextureEntry texture)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(texture);
        BitmapHead head = texture.Head;
        if (head.Kind == BitmapKind.Volume)
        {
            throw new NotSupportedException($"{texture.Row.NameHash:X16}: volume layout is not decoded.");
        }

        if (head.Kind == BitmapKind.Cube)
        {
            throw new NotSupportedException($"{texture.Row.NameHash:X16}: cube textures are not decoded.");
        }

        if (head.Surfaces != 1)
        {
            throw new NotSupportedException(
                $"{texture.Row.NameHash:X16}: {head.Surfaces} surfaces are not decoded.");
        }

        CompressionFormat format = head.Format.Codec switch
        {
            TextureCodec.Bc1 => CompressionFormat.Bc1,
            TextureCodec.Bc3 => CompressionFormat.Bc3,
            _ => throw new NotSupportedException(
                $"{texture.Row.NameHash:X16}: codec 0x{head.Format.Key:X2} is not decoded."),
        };

        byte[] chain = ReadChain(stream, texture);
        int mipBytes = (int)TextureFormats.MipBytes(head.Format.Family, head.Width, head.Height);
        if (mipBytes > chain.Length)
        {
            throw new InvalidDataException(
                $"{texture.Row.NameHash:X16}: mip 0 needs {mipBytes} bytes, chain has {chain.Length}.");
        }

        ColorRgba32[] pixels = new BcDecoder().DecodeRaw(
            chain[..mipBytes], head.Width, head.Height, format);
        byte[] rgba = new byte[pixels.Length * 4];
        for (int i = 0; i < pixels.Length; i++)
        {
            rgba[i * 4] = pixels[i].r;
            rgba[i * 4 + 1] = pixels[i].g;
            rgba[i * 4 + 2] = pixels[i].b;
            rgba[i * 4 + 3] = pixels[i].a;
        }

        return new DecodedTexture(head.Width, head.Height, rgba);
    }

    private static byte[] ReadChain(Stream stream, TextureEntry texture)
    {
        BitmapHead head = texture.Head;
        if (head.StoredSize > int.MaxValue)
        {
            throw new InvalidDataException(
                $"{texture.Row.NameHash:X16}: stored size {head.StoredSize} is too large.");
        }

        if (head.Storage == BitmapStorage.Raw)
        {
            return ReadExactly(stream, texture.PayloadOffset, (int)head.StoredSize, texture);
        }

        byte[] frame = ReadExactly(stream, texture.PayloadOffset, 8, texture);
        uint decompressed = BinaryPrimitives.ReadUInt32LittleEndian(frame);
        uint compressed = BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(4));
        if (decompressed != head.BodySize)
        {
            throw new InvalidDataException(
                $"{texture.Row.NameHash:X16}: decompressed {decompressed} != body {head.BodySize}.");
        }

        if (compressed != head.CompressedSize)
        {
            throw new InvalidDataException(
                $"{texture.Row.NameHash:X16}: compressed {compressed} != header {head.CompressedSize}.");
        }

        if (compressed > int.MaxValue)
        {
            throw new InvalidDataException(
                $"{texture.Row.NameHash:X16}: compressed size {compressed} is too large.");
        }

        byte[] block = ReadExactly(stream, texture.PayloadOffset + 8, (int)compressed - 8, texture);
        byte[] body = new byte[decompressed];
        int written = LZ4Codec.Decode(block, body);
        if (written != body.Length)
        {
            throw new InvalidDataException(
                $"{texture.Row.NameHash:X16}: LZ4 wrote {written}, expected {body.Length}.");
        }

        return body.AsSpan(0, (int)head.StoredSize).ToArray();
    }

    private static byte[] ReadExactly(Stream stream, long offset, int count, TextureEntry texture)
    {
        byte[] buffer = new byte[count];
        stream.Seek(offset, SeekOrigin.Begin);
        int read = 0;
        while (read < count)
        {
            int n = stream.Read(buffer, read, count - read);
            if (n == 0)
            {
                throw new InvalidDataException(
                    $"{texture.Row.NameHash:X16}: short read at {offset}.");
            }

            read += n;
        }

        return buffer;
    }
}
