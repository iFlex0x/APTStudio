using AptStudio.Core.Materials;
using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class MaterialSummaryCacheTests
{
    [Fact]
    public void ResetClearsRowsAndRejectsOldBuild()
    {
        MaterialSummaryCache cache = new();
        MaterialListResult result = Result(0xAAAAAAAAAAAAAAAA, 3);
        int generation = cache.Generation;

        Assert.True(cache.TryStore(generation, "VENICE.DPC", result));
        Assert.NotNull(cache.GetRows("VENICE.DPC"));
        Assert.Equal(3, cache.DecodeCount);

        cache.Reset();

        Assert.Null(cache.GetRows("VENICE.DPC"));
        Assert.Equal(0, cache.DecodeCount);
        Assert.False(cache.TryStore(generation, "VENICE.DPC", result));
        Assert.Null(cache.GetRows("VENICE.DPC"));
    }

    [Fact]
    public void NewGenerationStoresAfterReset()
    {
        MaterialSummaryCache cache = new();
        cache.Reset();
        int generation = cache.Generation;

        Assert.True(cache.TryStore(generation, "VENICE.DPC", Result(0xBBBBBBBBBBBBBBBB, 2)));

        Assert.Single(cache.GetRows("VENICE.DPC")!);
        Assert.Equal(2, cache.DecodeCount);
    }

    [Fact]
    public void ReadsAndFiltersDoNotAddDecodes()
    {
        MaterialSummaryCache cache = new();
        int generation = cache.Generation;
        Assert.True(cache.TryStore(
            generation, "VENICE.DPC", Result(0xCCCCCCCCCCCCCCCC, 3)));
        IReadOnlyList<MaterialRow> rows = cache.GetRows("VENICE.DPC")!;

        for (int attempt = 0; attempt < 3; attempt++)
        {
            Assert.NotNull(cache.GetRows("VENICE.DPC"));
            Assert.Single(MaterialListBuilder.Filter(rows, "CODE_EYE"));
        }

        Assert.Equal(3, cache.DecodeCount);
    }

    [Fact]
    public void EmptyPathReadsTheAllRowsCache()
    {
        MaterialSummaryCache cache = new();
        int generation = cache.Generation;
        Assert.Null(cache.GetRows(""));
        Assert.True(cache.TryStoreAll(generation, [Result(0xDDDDDDDDDDDDDDDD, 1).Rows[0]]));

        MaterialRow row = Assert.Single(cache.GetRows("")!);
        Assert.Equal(0xDDDDDDDDDDDDDDDDUL, row.Hash);
    }

    private static MaterialListResult Result(ulong hash, int decodes)
    {
        MaterialEntry entry = new(
            @"C:\game\DATAS\VENICE.DPC",
            new MaterialHit(0, hash, 0, MaterialHead.MaterialClass, 0));
        MaterialRow row = new(entry, "CODE_EYE", 3, MaterialRowState.Ok, "");
        return new MaterialListResult([row], decodes);
    }
}
