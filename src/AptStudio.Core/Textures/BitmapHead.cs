using System.Buffers.Binary;

namespace AptStudio.Core.Textures;

public enum BitmapKind : byte
{
    Flat = 0,
    Volume = 1,
    Cube = 2,
}

public enum BitmapStorage : byte
{
    Raw = 0,
    Lz4 = 2,
}

public sealed record BitmapHead(
    ulong NameHash,
    ulong LinkHash,
    BitmapKind Kind,
    int Width,
    int Height,
    int Surfaces,
    int MipCount,
    BitmapStorage Storage,
    uint StoredSize,
    uint BodySize,
    uint CompressedSize,
    uint LinkSize,
    ushort FormatKeyRaw,
    TextureFormat Format,
    long PayloadOffset)
{
    // Link header is 63 bytes on every measured row, so the fixed head read is 0x78.
    public const int HeaderSize = 0x78;
    public const uint LinkHeaderSize = 63;
    public const ulong ClassHash = 0xE9659CD1C3F3326D;

    public static BitmapHead Parse(ReadOnlySpan<byte> header, string source)
    {
        if (header.Length < HeaderSize)
        {
            throw new InvalidDataException($"{source}: bitmap header is {header.Length} bytes, need {HeaderSize}.");
        }

        ulong classHash = BinaryPrimitives.ReadUInt64LittleEndian(header);
        if (classHash != ClassHash)
        {
            throw new InvalidDataException($"{source}: class 0x{classHash:X16} is not Bitmap_Z.");
        }

        uint linkSize = BinaryPrimitives.ReadUInt32LittleEndian(header[0x24..]);
        uint bodySize = BinaryPrimitives.ReadUInt32LittleEndian(header[0x28..]);
        uint compressedSize = BinaryPrimitives.ReadUInt32LittleEndian(header[0x2C..]);
        ushort version = BinaryPrimitives.ReadUInt16LittleEndian(header[0x30..]);
        byte storageRaw = header[0x32];
        byte kindRaw = header[0x3F];
        int width = BinaryPrimitives.ReadUInt16LittleEndian(header[0x42..]) * 256 + header[0x41];
        int height = BinaryPrimitives.ReadUInt16LittleEndian(header[0x46..]) * 256 + header[0x45];
        uint h256 = BinaryPrimitives.ReadUInt32LittleEndian(header[0x4C..]);
        uint stored = BinaryPrimitives.ReadUInt32LittleEndian(header[0x51..]);
        int mipCount = header[0x61] + 1;
        ushort formatKey = BinaryPrimitives.ReadUInt16LittleEndian(header[0x64..]);

        if (version != 6)
        {
            throw new InvalidDataException($"{source}: bitmap version {version}, expected 6.");
        }

        if (storageRaw is not (0 or 2))
        {
            throw new InvalidDataException($"{source}: storage {storageRaw}, expected 0 or 2.");
        }

        if (kindRaw > 2)
        {
            throw new InvalidDataException($"{source}: kind {kindRaw}, expected 0, 1, or 2.");
        }

        if (linkSize != LinkHeaderSize)
        {
            throw new InvalidDataException($"{source}: link header size {linkSize}, expected {LinkHeaderSize}.");
        }

        if (stored == 0)
        {
            throw new InvalidDataException($"{source}: stored size is zero.");
        }

        if ((ulong)stored + 4 != bodySize)
        {
            throw new InvalidDataException($"{source}: body size {bodySize} != stored {stored} + 4.");
        }

        BitmapStorage storage = (BitmapStorage)storageRaw;
        if (storage == BitmapStorage.Raw && compressedSize != 0)
        {
            throw new InvalidDataException($"{source}: raw storage has compressed size {compressedSize}.");
        }

        if (storage == BitmapStorage.Lz4 && compressedSize <= 8)
        {
            throw new InvalidDataException($"{source}: LZ4 compressed size {compressedSize} is too small.");
        }

        if (width <= 0)
        {
            throw new InvalidDataException($"{source}: width is zero.");
        }

        if (mipCount > 32)
        {
            throw new InvalidDataException($"{source}: mip count {mipCount} is out of range.");
        }

        BitmapKind kind = (BitmapKind)kindRaw;
        int surfaces = (int)(h256 >> 8);
        if (surfaces == 0)
        {
            throw new InvalidDataException($"{source}: surface count is zero.");
        }

        TextureFormat format = TextureFormats.FromKey(formatKey);
        long payloadOffset = (0x33L + linkSize + 7) & ~7L;

        if (kind is BitmapKind.Flat or BitmapKind.Cube)
        {
            if (height <= 0)
            {
                throw new InvalidDataException($"{source}: height is zero.");
            }

            if (format.Family == TextureFamily.Volume)
            {
                throw new InvalidDataException($"{source}: volume format 0x{format.Key:X2} on kind {kind}.");
            }

            if (format.Family != TextureFamily.Unknown)
            {
                long chain = ChainBytes(format.Family, width, height, mipCount);
                if (chain > long.MaxValue / surfaces)
                {
                    throw new InvalidDataException($"{source}: chain size overflows.");
                }

                long expected = chain * surfaces;
                if (expected != stored)
                {
                    throw new InvalidDataException(
                        $"{source}: chain {expected} != stored {stored} for {width}x{height} x{surfaces}, {mipCount} mips, {format.DisplayName}.");
                }
            }
        }

        return new BitmapHead(
            BinaryPrimitives.ReadUInt64LittleEndian(header[0x08..]),
            BinaryPrimitives.ReadUInt64LittleEndian(header[0x10..]),
            kind,
            width,
            height,
            surfaces,
            mipCount,
            storage,
            stored,
            bodySize,
            compressedSize,
            linkSize,
            formatKey,
            format,
            payloadOffset);
    }

    private static long ChainBytes(TextureFamily family, int width, int height, int mipCount)
    {
        long total = 0;
        int w = width;
        int h = height;
        for (int i = 0; i < mipCount; i++)
        {
            total += TextureFormats.MipBytes(family, w, h);
            w = Math.Max(1, w >> 1);
            h = Math.Max(1, h >> 1);
        }

        return total;
    }
}
