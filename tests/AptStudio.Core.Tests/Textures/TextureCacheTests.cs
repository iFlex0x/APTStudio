using System.Buffers.Binary;
using AptStudio.Core.Textures;

namespace AptStudio.Core.Tests.Textures;

public sealed class TextureCacheTests : IDisposable
{
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
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan("APTEXCACHE".Length), 999);
        File.WriteAllBytes(cacheFile, bytes);

        IReadOnlyList<TextureEntry> textures = TextureCache.LoadOrScan(
            archive, false, out bool fromCache, cacheRoot);

        Assert.False(fromCache);
        Assert.Single(textures);
    }

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
