using System.Buffers.Binary;
using System.Text;
using AptStudio.Core.Textures;

namespace AptStudio.Core.Tests.Textures;

public sealed class TextureCacheTests : IDisposable
{
    private const int MagicLength = 10;
    private const int FormatVersionOffset = MagicLength;
    private const int ParserVersionOffset = FormatVersionOffset + 4;
    private const int ChecksumSize = 32;
    private const int HeaderBeforePath = 20;
    private const int HeaderAfterPath = 52;
    private const int RecordSize = 74;
    private const int EntryKindOffset = 44;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aptstudio-tests", Guid.NewGuid().ToString("N"));

    public TextureCacheTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void RoundTripsArchive()
    {
        string archive = WriteFile(DpcTestData.Build());
        string cacheRoot = Path.Combine(_root, "cache");

        IReadOnlyList<TextureEntry> first = TextureCache.LoadOrScan(
            archive, false, out bool firstCached, cacheRoot);
        IReadOnlyList<TextureEntry> second = TextureCache.LoadOrScan(
            archive, false, out bool secondCached, cacheRoot);

        Assert.False(firstCached);
        Assert.True(secondCached);
        Assert.Equal(first, second);
    }

    [Fact]
    public void RebuildsAfterArchiveChanges()
    {
        string archive = WriteFile(DpcTestData.Build());
        string cacheRoot = Path.Combine(_root, "cache");
        TextureCache.LoadOrScan(archive, false, out _, cacheRoot);

        File.AppendAllText(archive, "x");
        IReadOnlyList<TextureEntry> textures = TextureCache.LoadOrScan(
            archive, false, out bool fromCache, cacheRoot);

        Assert.False(fromCache);
        Assert.Single(textures);
    }

    [Fact]
    public void RebuildsOnCorruptCache()
    {
        string archive = WriteFile(DpcTestData.Build());
        string cacheRoot = Path.Combine(_root, "cache");
        TextureCache.LoadOrScan(archive, false, out _, cacheRoot);
        string cacheFile = Assert.Single(Directory.GetFiles(cacheRoot));
        File.WriteAllText(cacheFile, "garbage");

        IReadOnlyList<TextureEntry> textures = TextureCache.LoadOrScan(
            archive, false, out bool fromCache, cacheRoot);

        Assert.False(fromCache);
        Assert.Single(textures);
    }

    [Fact]
    public void RebuildsOnVersionChange()
    {
        string archive = WriteFile(DpcTestData.Build());
        string cacheRoot = Path.Combine(_root, "cache");
        TextureCache.LoadOrScan(archive, false, out _, cacheRoot);
        string cacheFile = Assert.Single(Directory.GetFiles(cacheRoot));
        byte[] bytes = File.ReadAllBytes(cacheFile);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(FormatVersionOffset), 999);
        File.WriteAllBytes(cacheFile, bytes);

        IReadOnlyList<TextureEntry> textures = TextureCache.LoadOrScan(
            archive, false, out bool fromCache, cacheRoot);

        Assert.False(fromCache);
        Assert.Single(textures);
    }

    [Fact]
    public void RebuildsOnParserVersionChange()
    {
        string archive = WriteFile(DpcTestData.Build());
        string cacheRoot = Path.Combine(_root, "cache");
        TextureCache.LoadOrScan(archive, false, out _, cacheRoot);
        string cacheFile = Assert.Single(Directory.GetFiles(cacheRoot));
        byte[] bytes = File.ReadAllBytes(cacheFile);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(ParserVersionOffset), 999);
        File.WriteAllBytes(cacheFile, bytes);

        IReadOnlyList<TextureEntry> textures = TextureCache.LoadOrScan(
            archive, false, out bool fromCache, cacheRoot);

        Assert.False(fromCache);
        Assert.Single(textures);
    }

    [Fact]
    public void RebuildsOnBadEntry()
    {
        string archive = WriteFile(DpcTestData.Build());
        string cacheRoot = Path.Combine(_root, "cache");
        TextureCache.LoadOrScan(archive, false, out _, cacheRoot);
        string cacheFile = Assert.Single(Directory.GetFiles(cacheRoot));
        byte[] bytes = File.ReadAllBytes(cacheFile);
        bytes[EntryStart(archive) + EntryKindOffset] = 0xFF;
        File.WriteAllBytes(cacheFile, bytes);

        IReadOnlyList<TextureEntry> textures = TextureCache.LoadOrScan(
            archive, false, out bool fromCache, cacheRoot);

        Assert.False(fromCache);
        Assert.Equal(BitmapKind.Flat, Assert.Single(textures).Head.Kind);
    }

    [Fact]
    public void RebuildsOnTruncatedCache()
    {
        string archive = WriteFile(DpcTestData.Build());
        string cacheRoot = Path.Combine(_root, "cache");
        TextureCache.LoadOrScan(archive, false, out _, cacheRoot);
        string cacheFile = Assert.Single(Directory.GetFiles(cacheRoot));
        byte[] bytes = File.ReadAllBytes(cacheFile);
        File.WriteAllBytes(cacheFile, bytes[..^10]);

        IReadOnlyList<TextureEntry> textures = TextureCache.LoadOrScan(
            archive, false, out bool fromCache, cacheRoot);

        Assert.False(fromCache);
        Assert.Single(textures);
    }

    [Fact]
    public void RebuildsOnLyingCount()
    {
        string archive = WriteFile(DpcTestData.Build());
        string cacheRoot = Path.Combine(_root, "cache");
        TextureCache.LoadOrScan(archive, false, out _, cacheRoot);
        string cacheFile = Assert.Single(Directory.GetFiles(cacheRoot));
        byte[] bytes = File.ReadAllBytes(cacheFile);
        int countOffset = EntryStart(archive) - ChecksumSize;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(countOffset), 1_000_000);
        File.WriteAllBytes(cacheFile, bytes);

        IReadOnlyList<TextureEntry> textures = TextureCache.LoadOrScan(
            archive, false, out bool fromCache, cacheRoot);

        Assert.False(fromCache);
        Assert.Single(textures);
    }

    [Fact]
    public void RebuildsOnTrailingBytes()
    {
        string archive = WriteFile(DpcTestData.Build());
        string cacheRoot = Path.Combine(_root, "cache");
        TextureCache.LoadOrScan(archive, false, out _, cacheRoot);
        string cacheFile = Assert.Single(Directory.GetFiles(cacheRoot));
        File.AppendAllText(cacheFile, "x");

        IReadOnlyList<TextureEntry> textures = TextureCache.LoadOrScan(
            archive, false, out bool fromCache, cacheRoot);

        Assert.False(fromCache);
        Assert.Single(textures);
    }

    [Fact]
    public void RebuildsOnTimestampChange()
    {
        string archive = WriteFile(DpcTestData.Build());
        string cacheRoot = Path.Combine(_root, "cache");
        TextureCache.LoadOrScan(archive, false, out _, cacheRoot);
        DateTime cached = File.GetLastWriteTimeUtc(archive);
        File.SetLastWriteTimeUtc(archive, cached.AddSeconds(-10));

        IReadOnlyList<TextureEntry> textures = TextureCache.LoadOrScan(
            archive, false, out bool fromCache, cacheRoot);

        Assert.False(fromCache);
        Assert.Single(textures);
    }

    [Fact]
    public void RoundTripsDistinctFields()
    {
        string archive = WriteFile(DpcTestData.Build(
            storage: BitmapStorage.Lz4,
            stored: 16,
            compSize: 20,
            width: 8,
            height: 4,
            decompressed: 99,
            u2: 7,
            u3: 9));
        string cacheRoot = Path.Combine(_root, "cache");

        TextureCache.LoadOrScan(archive, false, out _, cacheRoot);
        IReadOnlyList<TextureEntry> textures = TextureCache.LoadOrScan(
            archive, false, out bool fromCache, cacheRoot);

        Assert.True(fromCache);
        string cacheFile = Assert.Single(Directory.GetFiles(cacheRoot));
        Assert.Equal(EntryStart(archive) + RecordSize, new FileInfo(cacheFile).Length);
        TextureEntry texture = Assert.Single(textures);
        Assert.Equal(0x1111UL, texture.Row.NameHash);
        Assert.Equal(0x3333UL, texture.Row.LinkHash);
        Assert.Equal(0x2222UL, texture.Head.LinkHash);
        Assert.Equal(300u, texture.Row.Offset);
        Assert.Equal(99u, texture.Row.DecompressedSize);
        Assert.Equal(7, texture.Row.U2);
        Assert.Equal(9, texture.Row.U3);
        Assert.Equal(BitmapStorage.Lz4, texture.Head.Storage);
        Assert.Equal(8, texture.Head.Width);
        Assert.Equal(4, texture.Head.Height);
        Assert.Equal(16u, texture.Head.StoredSize);
        Assert.Equal(20u, texture.Head.BodySize);
        Assert.Equal(20u, texture.Head.CompressedSize);
        Assert.Equal(BitmapHead.HeaderSize, texture.Head.PayloadOffset);
    }

    [Fact]
    public void RoundTripsVolumeKind()
    {
        string archive = WriteFile(DpcTestData.Build(
            key: 0x0013, kind: BitmapKind.Volume, width: 17, height: 0));
        string cacheRoot = Path.Combine(_root, "cache");

        TextureCache.LoadOrScan(archive, false, out _, cacheRoot);
        IReadOnlyList<TextureEntry> textures = TextureCache.LoadOrScan(
            archive, false, out bool fromCache, cacheRoot);

        Assert.True(fromCache);
        TextureEntry texture = Assert.Single(textures);
        Assert.Equal(BitmapKind.Volume, texture.Head.Kind);
        Assert.Equal(17, texture.Head.Width);
        Assert.Equal(0, texture.Head.Height);
    }

    private static int EntryStart(string archivePath) =>
        HeaderBeforePath + Encoding.UTF8.GetByteCount(Path.GetFullPath(archivePath)) + HeaderAfterPath;

    [Fact]
    public void ForcesRebuild()
    {
        string archive = WriteFile(DpcTestData.Build());
        string cacheRoot = Path.Combine(_root, "cache");
        TextureCache.LoadOrScan(archive, false, out _, cacheRoot);

        IReadOnlyList<TextureEntry> textures = TextureCache.LoadOrScan(
            archive, true, out bool fromCache, cacheRoot);

        Assert.False(fromCache);
        Assert.Single(textures);
    }

    [Fact]
    public void RoundTripsWideHeaderValues()
    {
        string archive = WriteFile(DpcTestData.Build(
            key: 0x002A, width: 70000, height: 70000, mips: 32, surfaces: 300));
        string cacheRoot = Path.Combine(_root, "cache");

        IReadOnlyList<TextureEntry> first = TextureCache.LoadOrScan(
            archive, false, out _, cacheRoot);
        IReadOnlyList<TextureEntry> second = TextureCache.LoadOrScan(
            archive, false, out bool fromCache, cacheRoot);

        Assert.True(fromCache);
        Assert.Equal(first, second);
        Assert.Equal(70000, second[0].Head.Width);
        Assert.Equal(70000, second[0].Head.Height);
        Assert.Equal(300, second[0].Head.Surfaces);
        Assert.Equal(32, second[0].Head.MipCount);
    }

    [Fact]
    public void ToleratesUnwritableCache()
    {
        string archive = WriteFile(DpcTestData.Build());
        string cacheRoot = Path.Combine(_root, "cache-file");
        File.WriteAllText(cacheRoot, "not a directory");

        IReadOnlyList<TextureEntry> textures = TextureCache.LoadOrScan(
            archive, false, out bool fromCache, cacheRoot);

        Assert.False(fromCache);
        Assert.Single(textures);
    }

    private string WriteFile(byte[] data)
    {
        string path = Path.Combine(_root, "test.dpc");
        File.WriteAllBytes(path, data);
        return path;
    }
}
