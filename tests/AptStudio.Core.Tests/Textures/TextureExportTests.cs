using AptStudio.Core.Containers;
using AptStudio.Core.Textures;

namespace AptStudio.Core.Tests.Textures;

public sealed class TextureExportTests : IDisposable
{
    private readonly string _archive = Path.Combine(
        Path.GetTempPath(), $"apts-{Guid.NewGuid():N}.dpc");

    public TextureExportTests() => File.WriteAllBytes(_archive, DpcTestData.Build());

    public void Dispose() => File.Delete(_archive);

    [Fact]
    public void WritesPngForLongDestinationName()
    {
        TextureEntry texture = Texture();
        int budget = 259 - Path.GetTempPath().Length;
        Assert.True(budget >= 220, "the temp folder leaves no room for the long-name case.");
        string name = new string('x', budget - 37) + "-" + Guid.NewGuid().ToString("N") + ".png";
        string target = Path.Combine(Path.GetTempPath(), name);
        try
        {
            DecodedTexture image = TextureExport.ExportPng(texture, target);

            Assert.Equal(4, image.Width);
            Assert.Equal(4, image.Height);
            Assert.True(File.Exists(target));
            Assert.Equal([0x89, 0x50, 0x4E, 0x47], File.ReadAllBytes(target)[..4]);
            Assert.Empty(Directory.EnumerateFiles(Path.GetTempPath(), ".apts-*.tmp"));
        }
        finally
        {
            File.Delete(target);
        }
    }

    [Fact]
    public void ReplacesExistingFile()
    {
        TextureEntry texture = Texture();
        string target = Path.Combine(Path.GetTempPath(), $"apts-{Guid.NewGuid():N}.png");
        try
        {
            File.WriteAllText(target, "old");

            TextureExport.ExportPng(texture, target);
            long first = new FileInfo(target).Length;
            TextureExport.ExportPng(texture, target);

            Assert.Equal(first, new FileInfo(target).Length);
            Assert.Equal([0x89, 0x50, 0x4E, 0x47], File.ReadAllBytes(target)[..4]);
            Assert.Empty(Directory.EnumerateFiles(Path.GetTempPath(), ".apts-*.tmp"));
        }
        finally
        {
            File.Delete(target);
        }
    }

    [Fact]
    public void WritePngMatchesExportPngBytes()
    {
        TextureEntry texture = Texture();
        string first = Path.Combine(Path.GetTempPath(), $"apts-{Guid.NewGuid():N}-a.png");
        string second = Path.Combine(Path.GetTempPath(), $"apts-{Guid.NewGuid():N}-b.png");
        try
        {
            TextureExport.ExportPng(texture, first);
            TextureExport.WritePng(texture, second);

            Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
            Assert.Empty(Directory.EnumerateFiles(Path.GetTempPath(), ".apts-*.tmp"));
        }
        finally
        {
            File.Delete(first);
            File.Delete(second);
        }
    }

    [Fact]
    public void MissingSourceWithEmptyPathRejectsTheDestinationFirst()
    {
        TextureEntry texture = Texture();
        TextureEntry missing = new(
            Path.Combine(Path.GetTempPath(), $"apts-missing-{Guid.NewGuid():N}.dpc"),
            texture.Row,
            texture.Head);

        ArgumentException export = Assert.Throws<ArgumentException>(
            () => TextureExport.ExportPng(missing, ""));
        Assert.Contains("output path is empty", export.Message);

        ArgumentException write = Assert.Throws<ArgumentException>(
            () => TextureExport.WritePng(missing, "  "));
        Assert.Contains("output path is empty", write.Message);
    }

    private TextureEntry Texture()
    {
        DpcArchive archive = DpcArchive.Open(_archive);
        return TextureScanner.Scan(archive)[0];
    }
}
