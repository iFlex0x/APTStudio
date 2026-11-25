using AptStudio.Core.Assets;
using AptStudio.Core.Games;
using AptStudio.Core.Tests.Textures;

namespace AptStudio.Core.Tests.Assets;

public sealed class AssetCatalogTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "aptstudio-tests", Guid.NewGuid().ToString("N"));
    private readonly string _dataRoot;

    public AssetCatalogTests()
    {
        _dataRoot = Path.Combine(_root, "DATAS");
        Directory.CreateDirectory(_dataRoot);
        Directory.CreateDirectory(Path.Combine(_root, "FONT"));
        Directory.CreateDirectory(Path.Combine(_root, "LEVELS"));
        File.WriteAllBytes(Path.Combine(_root, "Resonance.exe"), []);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void IndexesEveryClass()
    {
        WriteArchive("SHARED.DPC", AssetTestData.BuildMany());

        AssetCatalog catalog = AssetCatalog.Load(Detect());

        Assert.Equal(4, catalog.Entries.Count);
        Assert.Equal(4, catalog.Classes.Count);
        Assert.Equal(1, catalog.BitmapCount);
        Assert.Equal(1, catalog.PreviewableCount);
        Assert.Equal(0, catalog.BodyErrorCount);
        Assert.Equal(1, catalog.FileCount);
        Assert.Empty(catalog.Failures);

        AssetArchive archive = Assert.Single(catalog.Archives);
        Assert.False(archive.IsExternal);
        Assert.Equal(4, archive.EntryCount);
        Assert.Equal(1, archive.BitmapCount);

        AssetEntry bitmap = Assert.Single(
            catalog.Entries, entry => entry.ClassHash == AssetClass.Bitmap);
        Assert.Equal("Bitmap_Z", bitmap.ClassName);
        Assert.False(bitmap.IsExternal);
        Assert.NotNull(bitmap.Bitmap);
        Assert.Null(bitmap.BodyError);
        Assert.True(bitmap.IsPreviewable);

        Assert.Single(catalog.Entries, entry => entry.ClassHash == AssetClass.Mesh);
        Assert.Single(catalog.Entries, entry => entry.ClassHash == AssetClass.Skin);
        Assert.Single(catalog.Entries, entry => entry.ClassHash == AssetClass.Animation);
        Assert.All(
            catalog.Entries.Where(entry => entry.ClassHash != AssetClass.Bitmap),
            entry =>
            {
                Assert.Null(entry.Bitmap);
                Assert.False(entry.IsPreviewable);
            });

        Assert.Equal(4, catalog.Classes.Sum(item => item.Count));
        Assert.Contains(catalog.Classes, item => item.Name == "Mesh_Z" && item.Count == 1);
    }

    [Fact]
    public void ResolvesExternalBitmapIntoCommon()
    {
        WriteArchive("COMMON.DPC", AssetTestData.BuildCommon());
        WriteArchive("EXTERNAL.DPC", DpcTestData.Build(
            resourceBlockOffset: 0,
            resourceBlockSize: 0));

        AssetCatalog catalog = AssetCatalog.Load(Detect());

        Assert.Equal(2, catalog.Entries.Count);
        Assert.Equal(2, catalog.Archives.Count);
        AssetEntry external = Assert.Single(catalog.Entries, entry => entry.IsExternal);
        Assert.Equal("EXTERNAL.DPC", Path.GetFileName(external.ArchivePath));
        Assert.Equal("COMMON.DPC", Path.GetFileName(external.BodyArchivePath));
        Assert.NotNull(external.Bitmap);
        Assert.Null(external.BodyError);
        Assert.Equal(2, catalog.PreviewableCount);
        Assert.Equal(0, catalog.BodyErrorCount);
    }

    [Fact]
    public void ReportsMissingCommon()
    {
        WriteArchive("EXTERNAL.DPC", DpcTestData.Build(
            resourceBlockOffset: 0,
            resourceBlockSize: 0));

        AssetCatalog catalog = AssetCatalog.Load(Detect());

        AssetEntry entry = Assert.Single(catalog.Entries);
        Assert.Null(entry.Bitmap);
        Assert.NotNull(entry.BodyError);
        Assert.Contains("COMMON.DPC", entry.BodyError);
        Assert.Equal(1, catalog.BodyErrorCount);
        Assert.Equal(0, catalog.PreviewableCount);
    }

    [Fact]
    public void ReportsBodyNameMismatch()
    {
        WriteArchive("SHARED.DPC", DpcTestData.Build(bodyName: 0x9999));

        AssetCatalog catalog = AssetCatalog.Load(Detect());

        AssetEntry entry = Assert.Single(catalog.Entries);
        Assert.Null(entry.Bitmap);
        Assert.NotNull(entry.BodyError);
        Assert.Contains("body name", entry.BodyError);
        Assert.Equal(1, catalog.BodyErrorCount);
    }

    [Fact]
    public void IncludesRtcAndRteRoots()
    {
        Directory.CreateDirectory(Path.Combine(_root, "RTC"));
        Directory.CreateDirectory(Path.Combine(_root, "RTE"));
        File.WriteAllBytes(
            Path.Combine(_root, "RTC", "CUT.DPC"), AssetTestData.BuildMany());
        File.WriteAllBytes(
            Path.Combine(_root, "RTE", "RES.DPC"), DpcTestData.Build(name: 0x5555));

        GameProfile profile = Detect();
        AssetCatalog catalog = AssetCatalog.Load(profile);

        Assert.Equal(["DATAS", "FONT", "LEVELS", "RTC", "RTE"], profile.AssetRoots);
        Assert.Equal(5, catalog.Entries.Count);
        Assert.Equal(2, catalog.FileCount);
        Assert.Equal(2, catalog.BitmapCount);
        Assert.Equal(2, catalog.PreviewableCount);
        Assert.Equal(0, catalog.BodyErrorCount);
    }

    [Fact]
    public void LoadsSecondaryRows()
    {
        WriteArchive("SHARED.DPC", DpcTestData.Build(
            secondaryCount: 1,
            secondaryName: 0x2222,
            secondaryOffset: 310));

        AssetCatalog catalog = AssetCatalog.Load(Detect());

        Assert.Equal(2, catalog.Entries.Count);
        Assert.Equal(2, catalog.BitmapCount);
        Assert.Equal(2, catalog.PreviewableCount);
        Assert.Equal(0, catalog.BodyErrorCount);
        Assert.Contains(catalog.Entries, entry => entry.NameHash == 0x2222);
    }

    [Fact]
    public void KeepsGoodArchivesWhenOneFails()
    {
        WriteArchive("BAD.DPC", new byte[16]);
        WriteArchive("GOOD.DPC", AssetTestData.BuildMany());

        AssetCatalog catalog = AssetCatalog.Load(Detect());

        Assert.Single(catalog.Failures);
        Assert.Equal("BAD.DPC", Path.GetFileName(catalog.Failures[0].Path));
        Assert.Equal(4, catalog.Entries.Count);
        Assert.Equal(2, catalog.FileCount);
    }

    [Fact]
    public void ThrowsWhenCancelled()
    {
        WriteArchive("SHARED.DPC", AssetTestData.BuildMany());
        using CancellationTokenSource cancel = new();
        cancel.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            AssetCatalog.Load(Detect(), cancellationToken: cancel.Token));
    }

    private GameProfile Detect()
    {
        Assert.True(GameProfile.TryDetect(_root, out GameProfile? profile, out string? error), error);
        return Assert.IsType<GameProfile>(profile);
    }

    private void WriteArchive(string name, byte[] bytes) =>
        File.WriteAllBytes(Path.Combine(_dataRoot, name), bytes);
}
