using System.Drawing;
using AptStudio.Core.Assets;
using AptStudio.Core.Containers;
using AptStudio.Core.Textures;
using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class TextureListBuilderTests
{
    [Fact]
    public void AllArchivesKeepsEverySourceArchive()
    {
        AssetEntry greece = Entry("GREECE.DPC", 0x1111, 10);
        AssetEntry alps = Entry("ALPS.DPC", 0x1111, 10);

        List<AssetRow> rows = TextureListBuilder.Build(
            [greece, alps], State(), CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => row.Cells[1] == "GREECE.DPC");
        Assert.Contains(rows, row => row.Cells[1] == "ALPS.DPC");
        Assert.NotEqual(rows[0].Key, rows[1].Key);
    }

    [Fact]
    public void QuerySearchesAcrossArchives()
    {
        AssetEntry greece = Entry("GREECE.DPC", 0x1111, 10);
        AssetEntry alps = Entry("ALPS.DPC", 0x2222, 20);

        List<AssetRow> rows = TextureListBuilder.Build(
            [greece, alps], State(query: "ALPS"), CancellationToken.None);

        AssetRow row = Assert.Single(rows);
        Assert.Equal("ALPS.DPC", row.Cells[1]);
    }

    [Fact]
    public void SingleArchiveStillFilters()
    {
        AssetEntry greece = Entry("GREECE.DPC", 0x1111, 10);
        AssetEntry alps = Entry("ALPS.DPC", 0x2222, 20);

        List<AssetRow> rows = TextureListBuilder.Build(
            [greece, alps], State(archive: greece.ArchivePath), CancellationToken.None);

        AssetRow row = Assert.Single(rows);
        Assert.Equal("GREECE.DPC", row.Cells[1]);
    }

    [Fact]
    public void ArchiveSortOrdersByArchiveThenOffset()
    {
        AssetEntry zed = Entry("Z.DPC", 0x1111, 10);
        AssetEntry alpha20 = Entry("A.DPC", 0x2222, 20);
        AssetEntry alpha10 = Entry("A.DPC", 0x3333, 10);

        List<AssetRow> rows = TextureListBuilder.Build(
            [zed, alpha20, alpha10], State(), CancellationToken.None);

        Assert.Equal(["A.DPC", "A.DPC", "Z.DPC"], rows.Select(row => row.Cells[1]));
        Assert.Equal(10u, ((AssetEntry)rows[0].Tag!).Row.Offset);
        Assert.Equal(20u, ((AssetEntry)rows[1].Tag!).Row.Offset);
    }

    private static BrowserState State(
        string archive = "",
        string query = "",
        TextureSort sort = TextureSort.Archive,
        bool descending = false) =>
        new(
            BrowserView.Textures,
            archive,
            query,
            TextureFilter.All,
            null,
            0,
            0,
            ChannelView.Rgb,
            true,
            1f,
            PointF.Empty,
            null,
            0,
            null,
            null,
            "",
            sort,
            descending);

    private static AssetEntry Entry(string archive, ulong hash, uint offset)
    {
        BitmapHead head = Head();
        return new AssetEntry(
            $@"C:\game\DATAS\{archive}",
            @"C:\game\DATAS\COMMON.DPC",
            !archive.Equals("COMMON.DPC", StringComparison.OrdinalIgnoreCase),
            new DpcEntry(hash, BitmapHead.ClassHash, 0, offset, BitmapHead.HeaderSize, 0, 0, 0, 0),
            head,
            null);
    }

    private static BitmapHead Head()
    {
        ushort key = 0x1B;
        return new BitmapHead(
            0,
            0,
            BitmapKind.Flat,
            512,
            512,
            1,
            10,
            BitmapStorage.Raw,
            0,
            4,
            0,
            BitmapHead.LinkHeaderSize,
            key,
            TextureFormats.FromKey(key),
            BitmapHead.HeaderSize);
    }
}
