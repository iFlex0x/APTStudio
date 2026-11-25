using AptStudio.Core.Games;
using AptStudio.Core.Textures;

namespace AptStudio.Core.Tests.Textures;

public sealed class TextureCatalogTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "aptstudio-tests", Guid.NewGuid().ToString("N"));
    private readonly string _dataRoot;
    private readonly string _cacheRoot;

    public TextureCatalogTests()
    {
        _dataRoot = Path.Combine(_root, "DATAS");
        _cacheRoot = Path.Combine(_root, "cache");
        Directory.CreateDirectory(_dataRoot);
        Directory.CreateDirectory(Path.Combine(_root, "FONT"));
        Directory.CreateDirectory(Path.Combine(_root, "LEVELS"));
        File.WriteAllBytes(Path.Combine(_root, "Resonance.exe"), []);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void LoadsLocalArchive()
    {
        WriteArchive("SHARED.DPC", DpcTestData.Build());

        TextureCatalog catalog = TextureCatalog.Load(Detect(), cacheRoot: _cacheRoot);

        CatalogArchive archive = Assert.Single(catalog.Archives);
        Assert.False(archive.IsExternal);
        Assert.Equal(1, archive.TextureCount);
        Assert.False(archive.FromCache);
        TextureEntry texture = Assert.Single(catalog.Textures);
        Assert.Equal(0x1111UL, texture.Head.NameHash);
        Assert.Empty(catalog.Failures);
        Assert.Equal(1, catalog.PreviewableCount);
    }

    [Fact]
    public void SkipsExternalArchiveBodies()
    {
        WriteArchive("SHARED.DPC", DpcTestData.Build());
        WriteArchive("EXTERNAL.DPC", DpcTestData.Build(
            resourceBlockOffset: 0, resourceBlockSize: 0));

        TextureCatalog catalog = TextureCatalog.Load(Detect(), cacheRoot: _cacheRoot);

        CatalogArchive external = Assert.Single(
            catalog.Archives, archive => archive.IsExternal);
        Assert.Equal(0, external.TextureCount);
        Assert.Equal(1, external.ExternalBitmapRows);
        Assert.Single(catalog.Textures);
    }

    [Fact]
    public void ReusesCacheOnSecondLoad()
    {
        WriteArchive("SHARED.DPC", DpcTestData.Build());
        GameProfile profile = Detect();

        TextureCatalog first = TextureCatalog.Load(profile, cacheRoot: _cacheRoot);
        TextureCatalog second = TextureCatalog.Load(profile, cacheRoot: _cacheRoot);

        Assert.False(Assert.Single(first.Archives).FromCache);
        Assert.True(Assert.Single(second.Archives).FromCache);
        Assert.Equal(0x1111UL, Assert.Single(second.Textures).Head.NameHash);
    }

    [Fact]
    public void KeepsGoodArchivesWhenOneFails()
    {
        WriteArchive("BAD.DPC", new byte[16]);
        WriteArchive("GOOD.DPC", DpcTestData.Build());

        TextureCatalog catalog = TextureCatalog.Load(Detect(), cacheRoot: _cacheRoot);

        Assert.Single(catalog.Failures);
        Assert.Contains("BAD.DPC", catalog.Failures[0]);
        Assert.Single(catalog.Archives);
        Assert.Equal(2, catalog.FileCount);
        Assert.Single(catalog.Textures);
    }

    [Fact]
    public void ExportsPng()
    {
        WriteArchive("SHARED.DPC", DpcTestData.Build());
        TextureCatalog catalog = TextureCatalog.Load(Detect(), cacheRoot: _cacheRoot);
        string output = Path.Combine(_root, "out", "eye.png");

        DecodedTexture image = TextureExport.ExportPng(Assert.Single(catalog.Textures), output);

        Assert.Equal(4, image.Width);
        Assert.Equal(4, image.Height);
        byte[] png = File.ReadAllBytes(output);
        Assert.True(png.Length > 8);
        Assert.Equal<byte[]>([137, 80, 78, 71, 13, 10, 26, 10], png[..8]);
    }

    private GameProfile Detect()
    {
        Assert.True(GameProfile.TryDetect(_root, out GameProfile? profile, out string? error), error);
        return Assert.IsType<GameProfile>(profile);
    }

    private void WriteArchive(string name, byte[] bytes) =>
        File.WriteAllBytes(Path.Combine(_dataRoot, name), bytes);
}
