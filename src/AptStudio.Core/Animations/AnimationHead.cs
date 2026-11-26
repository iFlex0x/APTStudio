using System.Buffers.Binary;

namespace AptStudio.Core.Animations;

public enum AnimationStorage : byte
{
    Raw = 0,
    Lz4 = 2,
}

public sealed record AnimationHead(
    ulong ClassHash,
    ulong NameHash,
    ulong LinkHash,
    uint LinkSize,
    uint BodySize,
    uint StoredSize,
    ushort ExtraSize,
    AnimationStorage Storage)
{
    public const int HeaderSize = 0x33;
    public const ulong AnimationClass = 0x622C802CCCFE934F;

    public int PayloadOffset => HeaderSize + (int)LinkSize + ExtraSize;

    public static AnimationHead Parse(ReadOnlySpan<byte> header, string source)
    {
        if (header.Length < HeaderSize)
        {
            throw new InvalidDataException(
                $"{source}: animation header is {header.Length} bytes, need {HeaderSize}.");
        }

        ulong classHash = BinaryPrimitives.ReadUInt64LittleEndian(header);
        if (classHash != AnimationClass)
        {
            throw new InvalidDataException($"{source}: class 0x{classHash:X16} is not Animation_Z.");
        }

        ushort extraSize = BinaryPrimitives.ReadUInt16LittleEndian(header[0x30..]);
        if (extraSize > 15)
        {
            throw new InvalidDataException(
                $"{source}: animation extra size {extraSize} is not measured.");
        }

        AnimationStorage storage = (AnimationStorage)header[0x32];
        if (storage is not (AnimationStorage.Raw or AnimationStorage.Lz4))
        {
            throw new InvalidDataException(
                $"{source}: animation storage {storage} is not measured.");
        }

        uint bodySize = BinaryPrimitives.ReadUInt32LittleEndian(header[0x28..]);
        uint storedSize = BinaryPrimitives.ReadUInt32LittleEndian(header[0x2C..]);
        if (storage == AnimationStorage.Lz4 && storedSize <= 8)
        {
            throw new InvalidDataException(
                $"{source}: LZ4 stored size {storedSize} is too small.");
        }

        return new AnimationHead(
            classHash,
            BinaryPrimitives.ReadUInt64LittleEndian(header[0x08..]),
            BinaryPrimitives.ReadUInt64LittleEndian(header[0x10..]),
            BinaryPrimitives.ReadUInt32LittleEndian(header[0x24..]),
            bodySize,
            storedSize,
            extraSize,
            storage);
    }
}
