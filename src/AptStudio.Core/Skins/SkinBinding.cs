using System.Buffers.Binary;
using AptStudio.Core.Containers;

namespace AptStudio.Core.Skins;

public sealed record SkinBinding(
    ulong BlockHash,
    uint EntryCount,
    IReadOnlyList<ulong> Entries,
    ulong SkeletonRoot,
    uint LinkSize)
{
    public const int MaxEntries = 4096;
    public const uint MinLinkSize = 0x18;

    public bool HasSkeleton => SkeletonRoot != 0;

    public IReadOnlyList<ulong> MeshEntries => HasSkeleton
        ? Entries.Skip(1).ToArray()
        : Entries;

    public static SkinBinding Read(Stream stream, string archivePath, DpcEntry row)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(row);
        if (row.ClassHash != SkinHead.SkinClass)
        {
            throw new InvalidDataException(
                $"{Path.GetFileName(archivePath)} 0x{row.NameHash:X16}: class is not Skin_Z.");
        }

        string source = $"{Path.GetFileName(archivePath)} 0x{row.NameHash:X16}";
        byte[] header = ReadExactly(stream, row.BodyOffset, SkinHead.HeaderSize, source);
        SkinHead head = SkinHead.Parse(header, source);
        if (head.NameHash != row.NameHash)
        {
            throw new InvalidDataException(
                $"{source}: body name 0x{head.NameHash:X16} != row name 0x{row.NameHash:X16}.");
        }

        byte[] block = ReadExactly(
            stream,
            row.BodyOffset + SkinHead.HeaderSize,
            (int)head.LinkSize,
            source);
        return Parse(block, head.LinkSize, source);
    }

    public static SkinBinding Parse(ReadOnlySpan<byte> block, uint linkSize, string source)
    {
        if (linkSize < MinLinkSize || block.Length < linkSize)
        {
            throw new InvalidDataException(
                $"{source}: skin link block is {block.Length} bytes, need at least {MinLinkSize}.");
        }

        uint entryCount = BinaryPrimitives.ReadUInt32LittleEndian(block[0x0C..]);
        long rootAt = 0x10L + (long)entryCount * 8;
        if (entryCount > MaxEntries || rootAt + 8 > linkSize)
        {
            throw new InvalidDataException(
                $"{source}: {entryCount} skin link entries do not fit in {linkSize} bytes.");
        }

        ulong[] entries = new ulong[entryCount];
        for (int index = 0; index < entries.Length; index++)
        {
            entries[index] = BinaryPrimitives.ReadUInt64LittleEndian(
                block[(0x10 + index * 8)..]);
        }

        ulong root = BinaryPrimitives.ReadUInt64LittleEndian(block[(int)rootAt..]);
        if (root != 0 && (entries.Length == 0 || entries[0] != root))
        {
            throw new InvalidDataException(
                $"{source}: skeleton root 0x{root:X16} is not the first link entry.");
        }

        return new SkinBinding(
            BinaryPrimitives.ReadUInt64LittleEndian(block),
            entryCount,
            entries,
            root,
            linkSize);
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
