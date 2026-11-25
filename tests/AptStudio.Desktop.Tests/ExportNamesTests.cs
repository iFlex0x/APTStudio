namespace AptStudio.Desktop.Tests;

public sealed class ExportNamesTests
{
    [Fact]
    public void SuffixesWithinTheSameBatch()
    {
        WithFolder(folder =>
        {
            HashSet<string> used = new(StringComparer.OrdinalIgnoreCase);
            string first = ExportNames.Unique(folder, @"C:\game\DATAS\COMMON.DPC", 0x141D914EDBC2BBAC, used);
            string second = ExportNames.Unique(folder, @"C:\game\DATAS\COMMON.DPC", 0x141D914EDBC2BBAC, used);
            Assert.Equal("COMMON_141D914EDBC2BBAC.png", first);
            Assert.Equal("COMMON_141D914EDBC2BBAC_2.png", second);
        });
    }

    [Fact]
    public void SuffixesWhenTheFileExists()
    {
        WithFolder(folder =>
        {
            HashSet<string> used = new(StringComparer.OrdinalIgnoreCase);
            string first = ExportNames.Unique(folder, @"C:\game\DATAS\COMMON.DPC", 0x50532C6A40E48002, used);
            File.WriteAllText(Path.Combine(folder, first), "");
            string second = ExportNames.Unique(
                folder, @"C:\game\DATAS\COMMON.DPC", 0x50532C6A40E48002, new HashSet<string>());
            Assert.Equal("COMMON_50532C6A40E48002_2.png", second);
        });
    }

    [Fact]
    public void KeepsArchivesApart()
    {
        WithFolder(folder =>
        {
            HashSet<string> used = new(StringComparer.OrdinalIgnoreCase);
            string common = ExportNames.Unique(folder, @"C:\game\DATAS\COMMON.DPC", 0x141D914EDBC2BBAC, used);
            string shared = ExportNames.Unique(folder, @"C:\game\DATAS\SHARED.DPC", 0x141D914EDBC2BBAC, used);
            Assert.Equal("COMMON_141D914EDBC2BBAC.png", common);
            Assert.Equal("SHARED_141D914EDBC2BBAC.png", shared);
        });
    }

    private static void WithFolder(Action<string> action)
    {
        string folder = Path.Combine(
            Path.GetTempPath(), "aptstudio-names-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            action(folder);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
