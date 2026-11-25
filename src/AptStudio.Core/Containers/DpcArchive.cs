using System.Buffers.Binary;
using System.Text;

namespace AptStudio.Core.Containers;

public enum BodyLocation
{
    Local,
    External
}

public sealed class DpcArchive
{
    public const string SupportedVersion = "v2.128.58.19";
    public const int SectorSize = 32;
    public const int NormalEntrySize = 44;
    public const int CommonEntrySize = 68;

    private const int BannerLength = 64;
    private const int MapPrefixSize = 40;
    private const int DataDescriptionSize = 28;
    private const int DataDescriptionSlots = 52;
    private const int MaxBlockCount = 1024;
    private const int MaxRowCount = 200_000;

    private DpcArchive(
        string path,
        long length,
        DpcHeader header,
        IReadOnlyList<DpcEntry> entries,
        BodyLocation bodyLocation)
    {
        Path = path;
        Length = length;
        Header = header;
        Entries = entries;
        BodyLocation = bodyLocation;
    }

    public string Path { get; }

    public long Length { get; }

    public DpcHeader Header { get; }

    public IReadOnlyList<DpcEntry> Entries { get; }

    public BodyLocation BodyLocation { get; }

    public static DpcArchive Open(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is empty.", nameof(path));
        }

        string fullPath = System.IO.Path.GetFullPath(path);
        using FileStream stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        long length = stream.Length;
        string banner = Encoding.ASCII.GetString(ReadAt(stream, 0, BannerLength)).TrimEnd('\0');
        if (!banner.StartsWith(SupportedVersion + " ", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{System.IO.Path.GetFileName(fullPath)}: unsupported version '{banner}'.");
        }

        DpcHeader header = DpcHeader.Parse(ReadAt(stream, DpcHeader.Offset, DpcHeader.Size));
        List<DpcEntry> entries = header.Kind switch
        {
            DpcKind.Rtc or DpcKind.Normal => ReadNormalEntries(stream, header),
            DpcKind.Common => ReadCommonEntries(stream, header),
            _ => throw new InvalidDataException($"{System.IO.Path.GetFileName(fullPath)}: unknown type {header.KindRaw}."),
        };

        bool ownsBodies = header.Kind == DpcKind.Common
            || (header.ResourceBlockOffset != 0 && header.ResourceBlockSize != 0);
        BodyLocation bodyLocation = ownsBodies ? BodyLocation.Local : BodyLocation.External;
        if (ownsBodies)
        {
            ValidateExtents(entries, length, fullPath);
        }

        return new DpcArchive(fullPath, length, header, entries, bodyLocation);
    }

    private static List<DpcEntry> ReadNormalEntries(FileStream stream, DpcHeader header)
    {
        string name = System.IO.Path.GetFileName(stream.Name);
        if (header.BlockDescriptionOffset == 0)
        {
            throw new InvalidDataException($"{name}: block description offset is zero.");
        }

        long blockTable = (long)header.BlockDescriptionOffset * SectorSize;
        uint blockCount = ReadUInt32(stream, blockTable);
        if (blockCount == 0 || blockCount > MaxBlockCount)
        {
            throw new InvalidDataException($"{name}: block count {blockCount} is out of range.");
        }

        byte[] blocks = ReadAt(stream, blockTable + 4, checked((int)blockCount * 32));
        List<DpcEntry> entries = new();
        for (int i = 0; i < blockCount; i++)
        {
            uint mapOffset = BinaryPrimitives.ReadUInt32LittleEndian(blocks.AsSpan(i * 32 + 24));
            if (mapOffset == 0)
            {
                throw new InvalidDataException($"{name}: block {i} has no resource map.");
            }

            ReadResources(stream, (long)mapOffset * SectorSize, entries, name);
        }

        return entries;
    }

    private static void ReadResources(FileStream stream, long mapOffset, List<DpcEntry> entries, string name)
    {
        byte[] prefix = ReadAt(stream, mapOffset, MapPrefixSize);
        uint dataCount = BinaryPrimitives.ReadUInt32LittleEndian(prefix);
        if (dataCount > DataDescriptionSlots)
        {
            throw new InvalidDataException($"{name}: data count {dataCount} exceeds {DataDescriptionSlots} slots.");
        }

        long countOffset = mapOffset + MapPrefixSize + DataDescriptionSlots * DataDescriptionSize;
        uint rowCount = ReadUInt32(stream, countOffset);
        if (rowCount > MaxRowCount)
        {
            throw new InvalidDataException($"{name}: row count {rowCount} is out of range.");
        }

        byte[] rows = ReadAt(stream, countOffset + 4, checked((int)rowCount * NormalEntrySize));
        for (int i = 0; i < rowCount; i++)
        {
            entries.Add(ParseNormalEntry(rows, i * NormalEntrySize));
        }
    }

    private static DpcEntry ParseNormalEntry(ReadOnlySpan<byte> rows, int offset)
    {
        ReadOnlySpan<byte> row = rows.Slice(offset, NormalEntrySize);
        return new DpcEntry(
            BinaryPrimitives.ReadUInt64LittleEndian(row),
            BinaryPrimitives.ReadUInt64LittleEndian(row[8..]),
            BinaryPrimitives.ReadUInt64LittleEndian(row[16..]),
            BinaryPrimitives.ReadUInt32LittleEndian(row[24..]),
            BinaryPrimitives.ReadUInt32LittleEndian(row[28..]),
            BinaryPrimitives.ReadUInt32LittleEndian(row[32..]),
            BinaryPrimitives.ReadUInt32LittleEndian(row[36..]),
            BinaryPrimitives.ReadUInt16LittleEndian(row[40..]),
            BinaryPrimitives.ReadUInt16LittleEndian(row[42..]));
    }

    private static List<DpcEntry> ReadCommonEntries(FileStream stream, DpcHeader header)
    {
        string name = System.IO.Path.GetFileName(stream.Name);
        if (header.MapSize == 0 || header.MapOffset == 0)
        {
            throw new InvalidDataException($"{name}: common file has no map.");
        }

        long mapStart = (long)header.MapOffset * SectorSize;
        long mapLength = (long)header.MapSize * SectorSize;
        if (mapStart + mapLength > stream.Length)
        {
            throw new InvalidDataException($"{name}: map region is outside the file.");
        }

        uint count = ReadUInt32(stream, mapStart);
        if (count > MaxRowCount || 4L + (long)count * CommonEntrySize > mapLength)
        {
            throw new InvalidDataException($"{name}: common map count {count} does not fit {mapLength} bytes.");
        }

        byte[] records = ReadAt(stream, mapStart + 4, checked((int)count * CommonEntrySize));
        List<DpcEntry> entries = new((int)count);
        for (int i = 0; i < count; i++)
        {
            entries.Add(ParseCommonEntry(records, i * CommonEntrySize));
        }

        return entries;
    }

    private static DpcEntry ParseCommonEntry(ReadOnlySpan<byte> records, int offset)
    {
        ReadOnlySpan<byte> record = records.Slice(offset, CommonEntrySize);
        return new DpcEntry(
            BinaryPrimitives.ReadUInt64LittleEndian(record),
            BinaryPrimitives.ReadUInt64LittleEndian(record[16..]),
            BinaryPrimitives.ReadUInt64LittleEndian(record[24..]),
            BinaryPrimitives.ReadUInt32LittleEndian(record[32..]),
            BinaryPrimitives.ReadUInt32LittleEndian(record[36..]),
            BinaryPrimitives.ReadUInt32LittleEndian(record[40..]),
            BinaryPrimitives.ReadUInt32LittleEndian(record[44..]),
            BinaryPrimitives.ReadUInt16LittleEndian(record[48..]),
            BinaryPrimitives.ReadUInt16LittleEndian(record[50..]));
    }

    private static void ValidateExtents(IReadOnlyList<DpcEntry> entries, long length, string path)
    {
        string name = System.IO.Path.GetFileName(path);
        for (int i = 0; i < entries.Count; i++)
        {
            DpcEntry entry = entries[i];
            long end = entry.BodyOffset + entry.CompressedSize;
            if (end > length)
            {
                throw new InvalidDataException(
                    $"{name}: row {i} name 0x{entry.NameHash:X16} ends at {end}, file is {length} bytes.");
            }
        }
    }

    private static uint ReadUInt32(FileStream stream, long offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(ReadAt(stream, offset, 4));
    }

    private static byte[] ReadAt(FileStream stream, long offset, int length)
    {
        if (offset < 0 || length < 0 || offset + length > stream.Length)
        {
            throw new InvalidDataException(
                $"{System.IO.Path.GetFileName(stream.Name)}: read of {length} bytes at {offset} is outside the file.");
        }

        byte[] buffer = new byte[length];
        stream.Seek(offset, SeekOrigin.Begin);
        int read = 0;
        while (read < length)
        {
            int n = stream.Read(buffer, read, length - read);
            if (n <= 0)
            {
                throw new EndOfStreamException(
                    $"{System.IO.Path.GetFileName(stream.Name)}: short read at {offset}.");
            }

            read += n;
        }

        return buffer;
    }
}
