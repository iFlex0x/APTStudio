using System.Buffers.Binary;
using System.Text;
using AptStudio.Core.Containers;
using AptStudio.Core.Textures;

namespace AptStudio.Core.Tests.Textures;

public sealed class TextureScannerTests : IDisposable
{
    private const int FileLength = 10240;
    private const int BlockTable = 100 * 32;
    private const int ResourcesMap = 128 * 32;
    private const int RowCountOffset = ResourcesMap + 40 + 52 * 28;
    private const int RowsOffset = RowCountOffset + 4;
    private const int BodyOffset = 300 * 32;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "aptstudio-tests", Guid.NewGuid().ToString("N"));

    public TextureScannerTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void ScansBitmapRow()
    {
        DpcArchive archive = DpcArchive.Open(WriteFile(BuildDpc()));

        IReadOnlyList<TextureEntry> textures = TextureScanner.Scan(archive);

        TextureEntry texture = Assert.Single(textures);
        Assert.Equal(0x1111UL, texture.Head.NameHash);
        Assert.Equal(4, texture.Head.Width);
        Assert.Equal(4, texture.Head.Height);
        Assert.Equal(1, texture.Head.MipCount);
        Assert.Equal(TextureCodec.Bc1, texture.Head.Format.Codec);
        Assert.Equal(BodyOffset + BitmapHead.HeaderSize, texture.PayloadOffset);
    }

    [Fact]
    public void RejectsRowExtentMismatch()
    {
        DpcArchive archive = DpcArchive.Open(WriteFile(BuildDpc(compressed: 131)));

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            TextureScanner.Scan(archive));

        Assert.Contains("payload ends at", error.Message);
    }

    [Fact]
    public void SkipsNonBitmapRows()
    {
        DpcArchive archive = DpcArchive.Open(WriteFile(BuildDpc(classHash: 0x2222)));

        Assert.Empty(TextureScanner.Scan(archive));
    }

    [Fact]
    public void RejectsExternalArchive()
    {
        DpcArchive archive = DpcArchive.Open(WriteFile(BuildDpc(
            resourceBlockOffset: 0,
            resourceBlockSize: 0)));

        Assert.Equal(BodyLocation.External, archive.BodyLocation);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            TextureScanner.Scan(archive));

        Assert.Contains("external", error.Message);
    }

    private string WriteFile(byte[] data)
    {
        string path = Path.Combine(_root, "test.dpc");
        File.WriteAllBytes(path, data);
        return path;
    }

    private static byte[] BuildDpc(
        ulong classHash = BitmapHead.ClassHash,
        uint compressed = 132,
        uint resourceBlockOffset = 300,
        uint resourceBlockSize = 64)
    {
        byte[] data = new byte[FileLength];
        byte[] banner = Encoding.ASCII.GetBytes("v2.128.58.19 - test");
        Array.Copy(banner, data, banner.Length);

        int header = DpcHeader.Offset;
        data[header] = 1;
        WriteU32(data, header + 1, 2);
        WriteU32(data, header + 5, 100);
        WriteU32(data, header + 9, resourceBlockSize);
        WriteU32(data, header + 13, resourceBlockOffset);
        WriteU32(data, header + 17, 0);
        WriteU32(data, header + 21, 0);

        WriteU32(data, BlockTable, 1);
        WriteU32(data, BlockTable + 4 + 24, 128);
        WriteU32(data, ResourcesMap, 1);
        WriteU32(data, ResourcesMap + 4, 300);
        WriteU32(data, ResourcesMap + 40, 1);
        WriteU32(data, RowCountOffset, 1);

        int row = RowsOffset;
        WriteU64(data, row, 0x1111);
        WriteU64(data, row + 8, classHash);
        WriteU64(data, row + 16, 0x3333);
        WriteU32(data, row + 24, 300);
        WriteU32(data, row + 28, compressed);
        WriteU32(data, row + 32, 0);
        WriteU32(data, row + 36, 132);
        WriteU16(data, row + 40, 0);
        WriteU16(data, row + 42, 0);

        byte[] bitmap = BitmapTestData.Header(4, 4, 1, 8, 0x001B);
        Array.Copy(bitmap, 0, data, BodyOffset, bitmap.Length);
        for (int i = 0; i < 8; i++)
        {
            data[BodyOffset + BitmapHead.HeaderSize + i] = 0xAA;
        }

        return data;
    }

    private static void WriteU16(byte[] data, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), value);

    private static void WriteU32(byte[] data, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);

    private static void WriteU64(byte[] data, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(offset), value);
}
