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
        Assert.False(archive.FromCache);
        TextureEntry texture = Assert.Single(archive.Textures);
        Assert.Equal(0x1111UL, texture.Head.NameHash);
        Assert.Empty(catalog.Failures);
        Assert.Equal(1, catalog.FileCount);
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
        Assert.Empty(external.Textures);
        Assert.Equal(1, external.ExternalBitmapReferences);
        Assert.Single(catalog.Textures);
    }

    [Fact]
    public void LoadsSecondaryRows()
    {
        WriteArchive("SHARED.DPC", DpcTestData.Build(
            secondaryCount: 1,
            secondaryName: 0x2222,
            secondaryOffset: 310));

        TextureCatalog catalog = TextureCatalog.Load(Detect(), cacheRoot: _cacheRoot);

        Assert.Equal(2, catalog.Textures.Count);
        Assert.Contains(catalog.Textures, texture => texture.Head.NameHash == 0x2222);
        Assert.Empty(catalog.Failures);
    }

    [Fact]
    public void IndexesLevelSubfolders()
    {
        string level = Path.Combine(_root, "LEVELS", "GREECE");
        Directory.CreateDirectory(level);
        File.WriteAllBytes(Path.Combine(level, "GREECE.DPC"), DpcTestData.Build(name: 0x3333));

        TextureCatalog catalog = TextureCatalog.Load(Detect(), cacheRoot: _cacheRoot);

        CatalogArchive archive = Assert.Single(catalog.Archives);
        Assert.Equal("GREECE.DPC", Path.GetFileName(archive.Path));
        Assert.Equal(0x3333UL, Assert.Single(catalog.Textures).Head.NameHash);
        Assert.Equal(1, catalog.FileCount);
    }

    [Fact]
    public void CountsSecondaryRowsAsExternal()
    {
        WriteArchive("EXTERNAL.DPC", DpcTestData.Build(
            resourceBlockOffset: 0,
            resourceBlockSize: 0,
            secondaryCount: 1));

        TextureCatalog catalog = TextureCatalog.Load(Detect(), cacheRoot: _cacheRoot);

        CatalogArchive external = Assert.Single(catalog.Archives);
        Assert.True(external.IsExternal);
        Assert.Equal(2, external.ExternalBitmapReferences);
        Assert.Empty(catalog.Textures);
    }

    [Fact]
    public void ReusesCacheWithSecondaryRows()
    {
        WriteArchive("SHARED.DPC", DpcTestData.Build(
            secondaryCount: 1,
            secondaryName: 0x2222,
            secondaryOffset: 310));
        GameProfile profile = Detect();

        TextureCatalog first = TextureCatalog.Load(profile, cacheRoot: _cacheRoot);
        TextureCatalog second = TextureCatalog.Load(profile, cacheRoot: _cacheRoot);

        Assert.Equal(2, first.Textures.Count);
        Assert.False(Assert.Single(first.Archives).FromCache);
        Assert.True(Assert.Single(second.Archives).FromCache);
        Assert.Equal(2, second.Textures.Count);
        Assert.Contains(second.Textures, texture => texture.Head.NameHash == 0x2222);
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
    public void KeepsArchiveAndTextureOrder()
    {
        WriteArchive("A.DPC", DpcTestData.Build(name: 0xAAAA));
        WriteArchive("B.DPC", DpcTestData.Build(name: 0xBBBB));

        TextureCatalog catalog = TextureCatalog.Load(Detect(), cacheRoot: _cacheRoot);

        Assert.Equal(
            new[] { "A.DPC", "B.DPC" },
            catalog.Archives.Select(archive => Path.GetFileName(archive.Path)).ToArray());
        Assert.Equal(
            new ulong[] { 0xAAAA, 0xBBBB },
            catalog.Archives.SelectMany(archive => archive.Textures)
                .Select(texture => texture.Head.NameHash).ToArray());
        Assert.Equal(
            new ulong[] { 0xAAAA, 0xBBBB },
            catalog.Textures.Select(texture => texture.Head.NameHash).ToArray());
    }

    [Fact]
    public void ReportsProgressPerArchive()
    {
        WriteArchive("A.DPC", DpcTestData.Build(name: 0xAAAA));
        WriteArchive("B.DPC", DpcTestData.Build(name: 0xBBBB));
        ListProgress progress = new();

        TextureCatalog.Load(Detect(), cacheRoot: _cacheRoot, progress: progress);

        Assert.Equal(2, progress.Items.Count);
        Assert.Equal([1, 2], progress.Items.Select(item => item.Index).ToArray());
        Assert.All(progress.Items, item => Assert.Equal(2, item.Total));
        Assert.Equal(
            new[] { "A.DPC", "B.DPC" },
            progress.Items.Select(item => Path.GetFileName(item.ArchivePath)).ToArray());
    }

    [Fact]
    public void ThrowsWhenCancelled()
    {
        WriteArchive("SHARED.DPC", DpcTestData.Build());
        using CancellationTokenSource cancel = new();
        cancel.Cancel();

        Assert.Throws<OperationCanceledException>(() => TextureCatalog.Load(
            Detect(), cacheRoot: _cacheRoot, cancellationToken: cancel.Token));
    }

    [Fact]
    public void ThrowsWhenCancelledBeforeAnyArchive()
    {
        using CancellationTokenSource cancel = new();
        cancel.Cancel();

        Assert.Throws<OperationCanceledException>(() => TextureCatalog.Load(
            Detect(), cacheRoot: _cacheRoot, cancellationToken: cancel.Token));
    }

    [Fact]
    public void ReportsDataRootFailure()
    {
        WriteArchive("SHARED.DPC", DpcTestData.Build());
        GameProfile profile = Detect();
        string font = Path.Combine(_root, "FONT");
        Directory.Delete(font);

        TextureCatalog catalog = TextureCatalog.Load(profile, cacheRoot: _cacheRoot);

        CatalogFailure failure = Assert.Single(catalog.Failures);
        Assert.Equal(font, failure.Path);
        Assert.Single(catalog.Archives);
        Assert.Single(catalog.Textures);
        Assert.Equal(1, catalog.FileCount);
    }

    [Fact]
    public void CountsOnlyDecodableRowsAsPreviewable()
    {
        WriteArchive("A_BC1.DPC", DpcTestData.Build());
        WriteArchive("B_BC3.DPC", DpcTestData.Build(key: 0x001D, stored: 16));
        WriteArchive("C_VOLUME.DPC", DpcTestData.Build(key: 0x0013, kind: BitmapKind.Volume));
        WriteArchive("D_UNKNOWN.DPC", DpcTestData.Build(key: 0x001E));
        WriteArchive("E_MULTI.DPC", DpcTestData.Build(stored: 16, surfaces: 2));
        WriteArchive("F_CUBE.DPC", DpcTestData.Build(stored: 48, kind: BitmapKind.Cube, surfaces: 6));

        TextureCatalog catalog = TextureCatalog.Load(Detect(), cacheRoot: _cacheRoot);

        Assert.Equal(6, catalog.Textures.Count);
        Assert.Equal(2, catalog.PreviewableCount);
    }

    [Fact]
    public void KeepsGoodArchivesWhenOneFails()
    {
        WriteArchive("BAD.DPC", new byte[16]);
        WriteArchive("GOOD.DPC", DpcTestData.Build());

        TextureCatalog catalog = TextureCatalog.Load(Detect(), cacheRoot: _cacheRoot);

        CatalogFailure failure = Assert.Single(catalog.Failures);
        Assert.Equal("BAD.DPC", Path.GetFileName(failure.Path));
        Assert.Contains("read of", failure.Message);
        Assert.DoesNotContain("BAD.DPC: BAD.DPC", failure.Message);
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

        Assert.Equal(0, image.Mip);
        Assert.Equal(4, image.Width);
        Assert.Equal(4, image.Height);
        byte[] png = File.ReadAllBytes(output);
        Assert.True(png.Length > 8);
        Assert.Equal<byte[]>([137, 80, 78, 71, 13, 10, 26, 10], png[..8]);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(output)!, "*.tmp"));
    }

    private GameProfile Detect()
    {
        Assert.True(GameProfile.TryDetect(_root, out GameProfile? profile, out string? error), error);
        return Assert.IsType<GameProfile>(profile);
    }

    private void WriteArchive(string name, byte[] bytes) =>
        File.WriteAllBytes(Path.Combine(_dataRoot, name), bytes);

    private sealed class ListProgress : IProgress<CatalogProgress>
    {
        public List<CatalogProgress> Items { get; } = new();

        public void Report(CatalogProgress value) => Items.Add(value);
    }
}
