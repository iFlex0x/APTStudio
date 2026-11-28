using System.Drawing;
using AptStudio.Core.Containers;
using AptStudio.Core.Textures;
using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class TextureExportTests
{
    [Fact]
    public void ExportButtonFollowsMip0Capability()
    {
        using TextureInspector inspector = new();
        (TextureOccurrence supported, TextureEntry supportedEntry) = Texture(0x001B);
        (TextureOccurrence cube, TextureEntry cubeEntry) =
            Texture(0x001B, kind: BitmapKind.Cube, surfaces: 6);
        Button export = FindExport(inspector);

        inspector.ShowEmpty();
        Assert.False(export.Enabled);

        inspector.SetTexture(
            supported, supportedEntry, [supported], 0, 0, ChannelView.Rgb, true, 1f, PointF.Empty, false);
        Assert.True(export.Enabled);

        inspector.SetTexture(
            cube, cubeEntry, [cube], 0, 0, ChannelView.Rgb, true, 1f, PointF.Empty, false);
        Assert.False(export.Enabled);

        inspector.ShowEmpty();
        Assert.False(export.Enabled);

        inspector.SetTexture(
            supported, supportedEntry, [supported], 0, 0, ChannelView.Rgb, true, 1f, PointF.Empty, false);
        Assert.True(export.Enabled);
    }

    [Fact]
    public void ExportStaysDisabledWhileAWriteIsInFlight()
    {
        using TextureInspector inspector = new();
        (TextureOccurrence occurrence, TextureEntry entry) = Texture(0x001B);
        Button export = FindExport(inspector);
        inspector.SetTexture(
            occurrence, entry, [occurrence], 0, 0,
            ChannelView.Rgb, true, 1f, PointF.Empty, false);
        Assert.True(export.Enabled);

        inspector.SetExporting(true);

        Assert.False(export.Enabled);
        Assert.Equal("Exporting...", export.Text);

        inspector.SetTexture(
            occurrence, entry, [occurrence], 0, 0,
            ChannelView.Rgb, true, 1f, PointF.Empty, false);
        Assert.False(export.Enabled);

        inspector.SetExporting(false);

        Assert.True(export.Enabled);
        Assert.Equal("Export PNG", export.Text);
    }

    [Fact]
    public void ExportBlockedStateKeepsBusyTextAndRecovers()
    {
        using TextureInspector inspector = new();
        (TextureOccurrence occurrence, TextureEntry entry) = Texture(0x001B);
        Button export = FindExport(inspector);
        inspector.SetTexture(
            occurrence, entry, [occurrence], 0, 0,
            ChannelView.Rgb, true, 1f, PointF.Empty, false);
        Assert.True(export.Enabled);

        inspector.SetExportBlocked(true);

        Assert.False(export.Enabled);
        Assert.Equal("Export PNG", export.Text);
        Assert.Contains("Another export", export.AccessibleDescription);

        inspector.SetExporting(true);

        Assert.False(export.Enabled);
        Assert.Equal("Exporting...", export.Text);
        Assert.Contains("already being written", export.AccessibleDescription);

        inspector.SetExportBlocked(false);

        Assert.False(export.Enabled);
        Assert.Equal("Exporting...", export.Text);

        inspector.SetExporting(false);

        Assert.True(export.Enabled);
        Assert.Equal("Export PNG", export.Text);
    }

    [Fact]
    public void FailureMessageNamesTheTextureFileAndReason()
    {
        (TextureOccurrence occurrence, _) = Texture(0x001B);

        string message = ExportFailure.DescribeTexture(
            occurrence,
            @"C:\exports\A_0000000000001111.png",
            "Refusing to write inside the game folder.");

        Assert.Contains("The mip 0 PNG was not written.", message);
        Assert.Contains("0x0000000000001111", message);
        Assert.Contains("A.DPC", message);
        Assert.Contains(@"C:\exports\A_0000000000001111.png", message);
        Assert.Contains("Refusing to write inside the game folder.", message);
    }

    private static Button FindExport(Control parent)
    {
        return FindExportOrNull(parent)
            ?? throw new InvalidOperationException("No Export PNG button in the inspector.");
    }

    private static Button? FindExportOrNull(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is Button { Text: "Export PNG" } button)
            {
                return button;
            }

            if (FindExportOrNull(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static (TextureOccurrence Occurrence, TextureEntry Entry) Texture(
        ushort key,
        BitmapKind kind = BitmapKind.Flat,
        int surfaces = 1)
    {
        BitmapHead head = new(
            0x1111,
            0x2222,
            kind,
            4,
            4,
            surfaces,
            1,
            BitmapStorage.Raw,
            8,
            12,
            0,
            63,
            key,
            TextureFormats.FromKey(key),
            BitmapHead.HeaderSize);
        DpcEntry row = new(0x1111, BitmapHead.ClassHash, 0, 0, 0, 0, 0, 0, 0);
        TextureEntry entry = new(@"C:\game\DATAS\A.DPC", row, head);
        TextureOccurrence occurrence = new(
            0x1111, @"C:\game\DATAS\A.DPC", @"C:\game\DATAS\A.DPC", 0, head);
        return (occurrence, entry);
    }
}
