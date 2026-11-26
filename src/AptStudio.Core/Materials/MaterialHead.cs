using System.Buffers.Binary;

namespace AptStudio.Core.Materials;

public enum MaterialStorage : byte
{
    Raw = 0,
    Lz4 = 2,
}

public sealed record MaterialHead(
    ulong NameHash,
    ulong LinkHash,
    ulong ClassHash,
    uint BodySize,
    uint InfoSize,
    uint UnpackedSize,
    uint StoredSize,
    ushort PaddingSize,
    MaterialStorage Storage)
{
    public const int HeaderSize = 0x33;
    public const ulong MaterialClass = 0x310BB7F1CFC1D4FF;
    public const ulong AnimClass = 0x41BB11CBB6797865;

    public long PayloadOffset => HeaderSize + InfoSize + PaddingSize;

    public long TotalSize => HeaderSize + BodySize;

    public bool IsAnimation => ClassHash == AnimClass;

    public static MaterialHead Parse(ReadOnlySpan<byte> header, string source)
    {
        if (header.Length < HeaderSize)
        {
            throw new InvalidDataException(
                $"{source}: material header is {header.Length} bytes, need {HeaderSize}.");
        }

        ulong classHash = BinaryPrimitives.ReadUInt64LittleEndian(header);
        if (classHash is not (MaterialClass or AnimClass))
        {
            throw new InvalidDataException(
                $"{source}: class 0x{classHash:X16} is not Material_Z.");
        }

        uint bodySize = BinaryPrimitives.ReadUInt32LittleEndian(header[0x20..]);
        uint infoSize = BinaryPrimitives.ReadUInt32LittleEndian(header[0x24..]);
        uint unpackedSize = BinaryPrimitives.ReadUInt32LittleEndian(header[0x28..]);
        uint storedSize = BinaryPrimitives.ReadUInt32LittleEndian(header[0x2C..]);
        ushort paddingSize = BinaryPrimitives.ReadUInt16LittleEndian(header[0x30..]);
        byte storageRaw = header[0x32];
        if (storageRaw is not (0 or 2))
        {
            throw new InvalidDataException($"{source}: storage {storageRaw}, expected 0 or 2.");
        }

        MaterialStorage storage = (MaterialStorage)storageRaw;
        uint payloadSize = storage == MaterialStorage.Raw ? unpackedSize : storedSize;
        if (storage == MaterialStorage.Lz4 && storedSize <= 8)
        {
            throw new InvalidDataException(
                $"{source}: LZ4 stored size {storedSize} is too small.");
        }

        if (bodySize != (long)infoSize + paddingSize + payloadSize)
        {
            throw new InvalidDataException(
                $"{source}: body size {bodySize} != info {infoSize} + pad {paddingSize} + payload {payloadSize}.");
        }

        return new MaterialHead(
            BinaryPrimitives.ReadUInt64LittleEndian(header[0x08..]),
            BinaryPrimitives.ReadUInt64LittleEndian(header[0x10..]),
            classHash,
            bodySize,
            infoSize,
            unpackedSize,
            storedSize,
            paddingSize,
            storage);
    }
}
