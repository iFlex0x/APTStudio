using System.Buffers.Binary;

namespace AptStudio.Core.Meshes;

public enum MeshStorage : byte
{
    Raw = 0,
    Lz4 = 2,
}

public sealed record MeshHead(
    ulong NameHash,
    ulong LinkHash,
    uint LinkSize,
    uint BodySize,
    uint CompressedSize,
    ushort Version,
    MeshStorage Storage,
    long PayloadOffset)
{
    public const int HeaderSize = 0x40;
    public const ulong ClassHash = 0x7703A856BA5776DD;

    public long PayloadEnd =>
        PayloadOffset + (Storage == MeshStorage.Lz4 ? CompressedSize : BodySize);

    public static MeshHead Parse(ReadOnlySpan<byte> header, string source)
    {
        if (header.Length < HeaderSize)
        {
            throw new InvalidDataException(
                $"{source}: mesh header is {header.Length} bytes, need {HeaderSize}.");
        }

        ulong classHash = BinaryPrimitives.ReadUInt64LittleEndian(header);
        if (classHash != ClassHash)
        {
            throw new InvalidDataException($"{source}: class 0x{classHash:X16} is not Mesh_Z.");
        }

        uint linkSize = BinaryPrimitives.ReadUInt32LittleEndian(header[0x24..]);
        uint bodySize = BinaryPrimitives.ReadUInt32LittleEndian(header[0x28..]);
        uint compressedSize = BinaryPrimitives.ReadUInt32LittleEndian(header[0x2C..]);
        ushort version = BinaryPrimitives.ReadUInt16LittleEndian(header[0x30..]);
        byte storageRaw = header[0x32];
        if (storageRaw is not (0 or 2))
        {
            throw new InvalidDataException($"{source}: storage {storageRaw}, expected 0 or 2.");
        }

        int extra = version switch
        {
            3 or 5 or 7 => 0,
            9 => 4,
            11 or 15 => 8,
            _ => throw new InvalidDataException($"{source}: mesh version {version} is not measured."),
        };

        if (bodySize == 0)
        {
            throw new InvalidDataException($"{source}: body size is zero.");
        }

        MeshStorage storage = (MeshStorage)storageRaw;
        if (storage == MeshStorage.Raw && compressedSize != 0)
        {
            throw new InvalidDataException($"{source}: raw storage has compressed size {compressedSize}.");
        }

        if (storage == MeshStorage.Lz4 && compressedSize <= 8)
        {
            throw new InvalidDataException(
                $"{source}: LZ4 compressed size {compressedSize} is too small.");
        }

        long payloadOffset = (0x33L + extra + linkSize + 7) & ~7L;
        return new MeshHead(
            BinaryPrimitives.ReadUInt64LittleEndian(header[0x08..]),
            BinaryPrimitives.ReadUInt64LittleEndian(header[0x10..]),
            linkSize,
            bodySize,
            compressedSize,
            version,
            storage,
            payloadOffset);
    }
}
