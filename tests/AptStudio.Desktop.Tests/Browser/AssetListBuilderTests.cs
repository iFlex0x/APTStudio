using AptStudio.Core.Assets;
using AptStudio.Core.Containers;
using AptStudio.Core.Skeletons;
using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class AssetListBuilderTests
{
    [Fact]
    public void AllArchivesKeepsBothMeshOccurrencesOfTheSameHash()
    {
        List<AssetRow> rows = AssetListBuilder.Meshes(
            [
                Entry(0xAAAA, AssetClass.Mesh, "A.DPC", 1),
                Entry(0xAAAA, AssetClass.Mesh, "B.DPC", 2),
            ],
            "",
            "");

        Assert.Equal(2, rows.Count);
        Assert.NotEqual(rows[0].Key, rows[1].Key);
        Assert.Equal(["0x000000000000AAAA", "A.DPC"], rows[0].Cells);
        Assert.Equal(["0x000000000000AAAA", "B.DPC"], rows[1].Cells);
    }

    [Fact]
    public void AllArchivesKeepsBothSkinOccurrencesOfTheSameHash()
    {
        List<AssetRow> rows = AssetListBuilder.Skins(
            [
                Entry(0xBBBB, AssetClass.Skin, "A.DPC", 3),
                Entry(0xBBBB, AssetClass.Skin, "B.DPC", 4),
            ],
            "",
            "");

        Assert.Equal(2, rows.Count);
        Assert.NotEqual(rows[0].Key, rows[1].Key);
        Assert.Equal("A.DPC", rows[0].Cells[1]);
        Assert.Equal("B.DPC", rows[1].Cells[1]);
    }

    [Fact]
    public void AllArchivesKeepsBothAnimationOccurrencesOfTheSameHash()
    {
        List<AssetRow> rows = AssetListBuilder.Animations(
            [
                Entry(0xCCCC, AssetClass.Animation, "A.DPC", 5),
                Entry(0xCCCC, AssetClass.Animation, "B.DPC", 6),
            ],
            new Dictionary<ulong, int> { [0xCCCC] = 2 },
            "",
            "");

        Assert.Equal(2, rows.Count);
        Assert.NotEqual(rows[0].Key, rows[1].Key);
        Assert.Equal("A.DPC", rows[0].Cells[1]);
        Assert.Equal("B.DPC", rows[1].Cells[1]);
        Assert.Equal("2", rows[0].Cells[2]);
    }

    [Fact]
    public void AllArchivesKeepsBothSkeletonOccurrencesOfTheSameHash()
    {
        List<AssetRow> rows = AssetListBuilder.Skeletons(
            [
                new SkeletonEntry("A.DPC", new SkeletonHit(10, 0xDDDD, 0, 0, 0, 0, 1, 0)),
                new SkeletonEntry("B.DPC", new SkeletonHit(20, 0xDDDD, 0, 0, 0, 0, 1, 0)),
            ],
            "",
            "");

        Assert.Equal(2, rows.Count);
        Assert.NotEqual(rows[0].Key, rows[1].Key);
        Assert.Equal("A.DPC", rows[0].Cells[1]);
        Assert.Equal("B.DPC", rows[1].Cells[1]);
    }

    [Fact]
    public void ArchiveScopeAndQueryFilterTheRows()
    {
        List<AssetEntry> assets =
        [
            Entry(0xAAAA, AssetClass.Mesh, "A.DPC", 1),
            Entry(0xBBBB, AssetClass.Mesh, "A.DPC", 2),
            Entry(0xAAAA, AssetClass.Mesh, "B.DPC", 3),
        ];

        Assert.Equal(2, AssetListBuilder.Meshes(assets, "A.DPC", "").Count);
        Assert.Equal(2, AssetListBuilder.Meshes(assets, "", "AAAA").Count);
        Assert.Single(AssetListBuilder.Meshes(assets, "", "B.DPC"));
        Assert.Single(AssetListBuilder.Meshes(assets, "A.DPC", "BBBB"));
    }

    private static AssetEntry Entry(
        ulong nameHash, ulong classHash, string archive, uint offset) => new(
        archive,
        archive,
        false,
        new DpcEntry(nameHash, classHash, 0, offset, 0, 0, 0, 0, 0),
        null,
        null);
}
