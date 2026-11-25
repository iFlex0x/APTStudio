using System.Buffers.Binary;
using System.Text;
using AptStudio.Core.Containers;

namespace AptStudio.Core.Tests.Containers;

public sealed class DpcArchiveTests : IDisposable
{
    private const int FileLength = 9664;
    private const int BlockTable = 100 * 32;
    private const int ResourcesMap = 128 * 32;
    private const int RowCountOffset = ResourcesMap + 40 + 52 * 28;
    private const int RowsOffset = RowCountOffset + 4;
    private const int BodiesOffset = 300 * 32;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "aptstudio-tests", Guid.NewGuid().ToString("N"));

    public DpcArchiveTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void ParsesNormalRows()
    {
        DpcArchive archive = DpcArchive.Open(WriteFile(BuildNormal()));

        Assert.Equal(FileLength, archive.Length);
        Assert.Equal(DpcKind.Normal, archive.Header.Kind);
        Assert.Equal(2u, archive.Header.OnePle);
        Assert.Equal(100u, archive.Header.BlockDescriptionOffset);
        Assert.Equal(BodyLocation.Local, archive.BodyLocation);
        Assert.Equal(2, archive.Entries.Count);

        DpcEntry first = archive.Entries[0];
        Assert.Equal(0x1111UL, first.NameHash);
        Assert.Equal(0x2222UL, first.ClassHash);
        Assert.Equal(0x3333UL, first.LinkHash);
        Assert.Equal(300u, first.Offset);
        Assert.Equal(BodiesOffset, first.BodyOffset);
        Assert.Equal(16u, first.CompressedSize);
        Assert.Equal(7u, first.Flags);
        Assert.Equal(32u, first.DecompressedSize);
        Assert.Equal((ushort)1, first.U2);
        Assert.Equal((ushort)2, first.U3);
    }

    [Fact]
    public void ParsesSecondaryRows()
    {
        DpcArchive archive = DpcArchive.Open(WriteFile(BuildNormal(
            secondaryCount: 1,
            secondaryName: 0x4444,
            secondaryClass: 0x5555,
            secondaryOffset: 301,
            secondaryCompressed: 8)));

        Assert.Equal(2, archive.Entries.Count);
        DpcEntry entry = Assert.Single(archive.SecondaryEntries);
        Assert.Equal(0x4444UL, entry.NameHash);
        Assert.Equal(0x5555UL, entry.ClassHash);
        Assert.Equal(0x6666UL, entry.LinkHash);
        Assert.Equal(301u, entry.Offset);
        Assert.Equal(301L * 32, entry.BodyOffset);
        Assert.Equal(8u, entry.CompressedSize);
        Assert.Equal(9u, entry.Flags);
        Assert.Equal(40u, entry.DecompressedSize);
        Assert.Equal((ushort)3, entry.U2);
        Assert.Equal((ushort)4, entry.U3);
    }

    [Fact]
    public void RejectsSecondaryExtentPastEnd()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            DpcArchive.Open(WriteFile(BuildNormal(
                secondaryCount: 1,
                secondaryOffset: 300,
                secondaryCompressed: 1000))));

        Assert.Contains("0x0000000000004444", error.Message);
        Assert.Contains("resources2 row 0", error.Message);
    }

    [Fact]
    public void ReportsTailCountOutsideFile()
    {
        byte[] data = BuildNormal(secondaryCount: 1);
        int rowsEnd = RowsOffset + 2 * DpcArchive.NormalEntrySize;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(rowsEnd + 8), 0x0FFFFFFF);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            DpcArchive.Open(WriteFile(data)));

        Assert.Contains("block 0", error.Message);
        Assert.Contains("resources2 first entries", error.Message);
    }

    [Fact]
    public void ParsesZeroRows()
    {
        DpcArchive archive = DpcArchive.Open(WriteFile(BuildNormal(rowCount: 0)));

        Assert.Empty(archive.Entries);
        Assert.Equal(BodyLocation.Local, archive.BodyLocation);
    }

    [Fact]
    public void MapOnlyFileIsExternal()
    {
        DpcArchive archive = DpcArchive.Open(WriteFile(BuildNormal(
            resourceBlockOffset: 0,
            resourceBlockSize: 0,
            firstRowOffset: 100_000)));

        Assert.Equal(BodyLocation.External, archive.BodyLocation);
        Assert.Equal(2, archive.Entries.Count);
        Assert.Equal(3_200_000L, archive.Entries[0].BodyOffset);
    }

    [Fact]
    public void RejectsWrongVersion()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            DpcArchive.Open(WriteFile(BuildNormal(banner: "v9.9.9.9 - test"))));

        Assert.Contains("version", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsVersionLookalike()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            DpcArchive.Open(WriteFile(BuildNormal(banner: "v2.128.58.190 - test"))));

        Assert.Contains("version", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsUnknownType()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            DpcArchive.Open(WriteFile(BuildNormal(type: 3))));

        Assert.Contains("type", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsLocalExtentPastEnd()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            DpcArchive.Open(WriteFile(BuildNormal(firstCompressed: 1000))));

        Assert.Contains("0x0000000000001111", error.Message);
    }

    [Fact]
    public void RejectsExtentOneBytePastEnd()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            DpcArchive.Open(WriteFile(BuildNormal(rowCount: 1, firstCompressed: 65))));

        Assert.Contains("test.dpc", error.Message);
        Assert.Contains("0x0000000000001111", error.Message);
    }

    [Fact]
    public void AcceptsExtentAtEnd()
    {
        DpcArchive archive = DpcArchive.Open(WriteFile(BuildNormal(rowCount: 1, firstCompressed: 64)));

        Assert.Single(archive.Entries);
    }

    [Fact]
    public void RejectsZeroBlocks()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            DpcArchive.Open(WriteFile(BuildNormal(blockCount: 0))));

        Assert.Contains("block count", error.Message);
    }

    [Fact]
    public void RejectsTooManyBlocks()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            DpcArchive.Open(WriteFile(BuildNormal(blockCount: 1025))));

        Assert.Contains("block count", error.Message);
    }

    [Fact]
    public void RejectsTooManyRows()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            DpcArchive.Open(WriteFile(BuildNormal(rowCount: 0, declaredRowCount: 200_001))));

        Assert.Contains("row count", error.Message);
    }

    [Fact]
    public void ParsesCommonMap()
    {
        DpcArchive archive = DpcArchive.Open(WriteFile(BuildCommon()));

        Assert.Equal(DpcKind.Common, archive.Header.Kind);
        Assert.Equal(BodyLocation.Local, archive.BodyLocation);
        Assert.Single(archive.Entries);

        DpcEntry entry = archive.Entries[0];
        Assert.Equal(0xAAAAUL, entry.NameHash);
        Assert.Equal(0xBBBBUL, entry.ClassHash);
        Assert.Equal(0xCCCCUL, entry.LinkHash);
        Assert.Equal(210u, entry.Offset);
        Assert.Equal(8u, entry.CompressedSize);
        Assert.Equal(5u, entry.Flags);
        Assert.Equal(16u, entry.DecompressedSize);
        Assert.Equal((ushort)2, entry.U2);
        Assert.Equal((ushort)512, entry.U3);
    }

    [Fact]
    public void RejectsCommonMapTooSmall()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            DpcArchive.Open(WriteFile(BuildCommon(mapSize: 1))));

        Assert.Contains("count", error.Message);
    }

    [Fact]
    public void RejectsCommonExtentPastEnd()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            DpcArchive.Open(WriteFile(BuildCommon(compressed: 1000))));

        Assert.Contains("0x000000000000AAAA", error.Message);
    }

    private string WriteFile(byte[] data)
    {
        string path = Path.Combine(_root, "test.dpc");
        File.WriteAllBytes(path, data);
        return path;
    }

    private static byte[] BuildNormal(
        uint rowCount = 2,
        uint? declaredRowCount = null,
        uint blockCount = 1,
        byte type = 1,
        uint resourceBlockOffset = 300,
        uint resourceBlockSize = 64,
        uint firstCompressed = 16,
        uint firstRowOffset = 300,
        string banner = "v2.128.58.19 - test",
        uint secondaryCount = 0,
        ulong secondaryName = 0x4444,
        ulong secondaryClass = 0x5555,
        uint secondaryOffset = 300,
        uint secondaryCompressed = 8)
    {
        byte[] data = new byte[FileLength];
        WriteBanner(data, banner);

        int header = DpcHeader.Offset;
        data[header] = type;
        WriteU32(data, header + 1, 2);
        WriteU32(data, header + 5, 100);
        WriteU32(data, header + 9, resourceBlockSize);
        WriteU32(data, header + 13, resourceBlockOffset);
        WriteU32(data, header + 17, 0);
        WriteU32(data, header + 21, 0);

        WriteU32(data, BlockTable, blockCount);
        if (blockCount == 1)
        {
            WriteU32(data, BlockTable + 4 + 24, 128);
        }

        uint count = declaredRowCount ?? rowCount;
        WriteU32(data, ResourcesMap, 1);
        WriteU32(data, ResourcesMap + 4, 300);
        WriteU32(data, ResourcesMap + 40, count);
        WriteU32(data, RowCountOffset, count);

        for (int i = 0; i < (int)rowCount; i++)
        {
            int row = RowsOffset + i * DpcArchive.NormalEntrySize;
            WriteU64(data, row, 0x1111UL + (ulong)i);
            WriteU64(data, row + 8, 0x2222UL + (ulong)i);
            WriteU64(data, row + 16, 0x3333UL + (ulong)i);
            WriteU32(data, row + 24, firstRowOffset + (uint)i);
            WriteU32(data, row + 28, i == 0 ? firstCompressed : 8);
            WriteU32(data, row + 32, 7u + (uint)i);
            WriteU32(data, row + 36, 32);
            WriteU16(data, row + 40, (ushort)(1 + i));
            WriteU16(data, row + 42, 2);
        }

        WriteU64(data, BodiesOffset, 0x2222);
        WriteU64(data, BodiesOffset + 8, 0x1111);
        WriteU64(data, BodiesOffset + 16, 0x3333);
        WriteU64(data, BodiesOffset + 32, 0x2223);
        WriteU64(data, BodiesOffset + 40, 0x1112);
        WriteU64(data, BodiesOffset + 48, 0x3334);

        int rowsEnd = RowsOffset + (int)rowCount * DpcArchive.NormalEntrySize;
        WriteU64(data, rowsEnd, 0);
        WriteU32(data, rowsEnd + 8, 0);
        WriteU32(data, rowsEnd + 12, 0);
        WriteU32(data, rowsEnd + 16, secondaryCount);
        for (int i = 0; i < secondaryCount; i++)
        {
            int record = rowsEnd + 20 + i * 45;
            WriteU64(data, record, secondaryName);
            WriteU64(data, record + 8, secondaryClass);
            WriteU64(data, record + 16, 0x6666);
            WriteU32(data, record + 24, secondaryOffset);
            WriteU32(data, record + 28, secondaryCompressed);
            WriteU32(data, record + 32, 9);
            WriteU32(data, record + 36, 40);
            WriteU16(data, record + 40, 3);
            WriteU16(data, record + 42, 4);
        }

        return data;
    }

    private static byte[] BuildCommon(
        uint mapSize = 3,
        uint mapOffset = 200,
        uint bodyOffset = 210,
        uint compressed = 8)
    {
        byte[] data = new byte[6800];
        WriteBanner(data, "v2.128.58.19 - test");

        int header = DpcHeader.Offset;
        data[header] = 2;
        WriteU32(data, header + 1, 0);
        WriteU32(data, header + 5, 0);
        WriteU32(data, header + 9, 0);
        WriteU32(data, header + 13, 0);
        WriteU32(data, header + 17, mapSize);
        WriteU32(data, header + 21, mapOffset);

        int map = (int)mapOffset * 32;
        WriteU32(data, map, 1);

        int record = map + 4;
        WriteU64(data, record, 0xAAAA);
        WriteU64(data, record + 8, 0xAAAA);
        WriteU64(data, record + 16, 0xBBBB);
        WriteU64(data, record + 24, 0xCCCC);
        WriteU32(data, record + 32, bodyOffset);
        WriteU32(data, record + 36, compressed);
        WriteU32(data, record + 40, 5);
        WriteU32(data, record + 44, 16);
        WriteU16(data, record + 48, 2);
        WriteU16(data, record + 50, 512);

        WriteU64(data, (int)bodyOffset * 32, 0xBBBB);
        WriteU64(data, (int)bodyOffset * 32 + 8, 0xAAAA);

        return data;
    }

    private static void WriteBanner(byte[] data, string banner)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(banner);
        Array.Copy(bytes, data, Math.Min(bytes.Length, 64));
    }

    private static void WriteU16(byte[] data, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), value);

    private static void WriteU32(byte[] data, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);

    private static void WriteU64(byte[] data, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(offset), value);
}
