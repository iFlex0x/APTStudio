using AptStudio.Core.Containers;
using AptStudio.Core.Textures;

namespace AptStudio.Core.Tests.Textures;

public sealed class TextureScannerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aptstudio-tests", Guid.NewGuid().ToString("N"));

    public TextureScannerTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void ScansBitmapRow()
    {
        DpcArchive archive = DpcArchive.Open(WriteFile(DpcTestData.Build()));

        IReadOnlyList<TextureEntry> textures = TextureScanner.Scan(archive);

        TextureEntry texture = Assert.Single(textures);
        Assert.Equal(0x1111UL, texture.Head.NameHash);
        Assert.Equal(4, texture.Head.Width);
        Assert.Equal(4, texture.Head.Height);
        Assert.Equal(1, texture.Head.MipCount);
        Assert.Equal(TextureCodec.Bc1, texture.Head.Format.Codec);
        Assert.Equal(DpcTestData.BodyOffset + BitmapHead.HeaderSize, texture.PayloadOffset);
    }

    [Fact]
    public void ScansLz4Row()
    {
        DpcArchive archive = DpcArchive.Open(WriteFile(DpcTestData.Build(
            storage: BitmapStorage.Lz4,
            compSize: 20)));

        TextureEntry texture = Assert.Single(TextureScanner.Scan(archive));

        Assert.Equal(BitmapStorage.Lz4, texture.Head.Storage);
        Assert.Equal(20u, texture.Head.CompressedSize);
    }

    [Fact]
    public void RejectsShortRow()
    {
        DpcArchive archive = DpcArchive.Open(WriteFile(DpcTestData.Build(rowSize: 100)));

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            TextureScanner.Scan(archive));

        Assert.Contains("body is 100 bytes", error.Message);
    }

    [Fact]
    public void RejectsRowExtentMismatch()
    {
        DpcArchive archive = DpcArchive.Open(WriteFile(DpcTestData.Build(rowSize: 131)));

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            TextureScanner.Scan(archive));

        Assert.Contains("payload ends at", error.Message);
    }

    [Fact]
    public void RejectsBodyNameMismatch()
    {
        DpcArchive archive = DpcArchive.Open(WriteFile(DpcTestData.Build(bodyName: 0x9999)));

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            TextureScanner.Scan(archive));

        Assert.Contains("body name", error.Message);
    }

    [Fact]
    public void SkipsNonBitmapRows()
    {
        DpcArchive archive = DpcArchive.Open(WriteFile(DpcTestData.Build(classHash: 0x2222)));

        Assert.Empty(TextureScanner.Scan(archive));
    }

    [Fact]
    public void RejectsExternalArchive()
    {
        DpcArchive archive = DpcArchive.Open(WriteFile(DpcTestData.Build(
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
}
