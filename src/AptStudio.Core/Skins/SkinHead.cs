using System.Buffers.Binary;

namespace AptStudio.Core.Skins;

public enum SkinStorage : byte
{
    Raw = 0,
    Lz4 = 2,
}

public sealed record SkinHead(
    ulong ClassHash,
    ulong NameHash,
    ulong LinkHash,
    uint LinkSize,
    uint BodySize,
    uint StoredSize,
    ushort Version,
    SkinStorage Storage)
{
    public const int HeaderSize = 0x33;
    public const ulong SkinClass = 0x53E24BDB95306A09;

    public int ExtraSize => Version switch
    {
        3 => 3,
        11 => 11,
        _ => throw new InvalidDataException($"skin version {Version} is not measured."),
    };

    public int PayloadOffset => HeaderSize + (int)LinkSize + ExtraSize;

    public static SkinHead Parse(ReadOnlySpan<byte> header, string source)
    {
        if (header.Length < HeaderSize)
        {
            throw new InvalidDataException(
                $"{source}: skin header is {header.Length} bytes, need {HeaderSize}.");
        }

        ulong classHash = BinaryPrimitives.ReadUInt64LittleEndian(header);
        if (classHash != SkinClass)
        {
            throw new InvalidDataException($"{source}: class 0x{classHash:X16} is not Skin_Z.");
        }

        ushort version = BinaryPrimitives.ReadUInt16LittleEndian(header[0x30..]);
        if (version is not (3 or 11))
        {
            throw new InvalidDataException(
                $"{source}: skin version {version} is not measured.");
        }

        SkinStorage storage = (SkinStorage)header[0x32];
        if (storage != SkinStorage.Lz4)
        {
            throw new InvalidDataException(
                $"{source}: skin storage {storage} is not measured.");
        }

        return new SkinHead(
            classHash,
            BinaryPrimitives.ReadUInt64LittleEndian(header[0x08..]),
            BinaryPrimitives.ReadUInt64LittleEndian(header[0x10..]),
            BinaryPrimitives.ReadUInt32LittleEndian(header[0x24..]),
            BinaryPrimitives.ReadUInt32LittleEndian(header[0x28..]),
            BinaryPrimitives.ReadUInt32LittleEndian(header[0x2C..]),
            version,
            storage);
    }
}
