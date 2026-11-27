using AptStudio.Core.Materials;
using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class MaterialListTests
{
    [Fact]
    public void BuildKeepsZeroSlotMaterialAsEmpty()
    {
        MaterialEntry entry = Entry(0x2EF96EC7369CDEAE, 0x2EF96EC7369CDEAE);

        MaterialListResult result = MaterialListBuilder.Build(
            [entry],
            _ => ("CODE_DIFFUSE", 0));

        MaterialRow row = Assert.Single(result.Rows);
        Assert.Equal(MaterialRowState.Empty, row.State);
        Assert.Equal(0, row.SlotCount);
        Assert.Equal("CODE_DIFFUSE", row.DisplayCode);
        Assert.Equal("0", row.DisplaySlots);
        Assert.Equal(1, result.DecodeCount);
    }

    [Fact]
    public void BuildKeepsDecodeFailureAsReadError()
    {
        MaterialEntry good = Entry(0x1111111111111111, 1);
        MaterialEntry bad = Entry(0x2222222222222222, 2);

        MaterialListResult result = MaterialListBuilder.Build(
            [good, bad],
            entry => entry.NameHash == bad.NameHash
                ? throw new InvalidDataException("short read at 42")
                : ("CODE_EYE", 3));

        Assert.Equal(2, result.Rows.Count);
        MaterialRow failed = result.Rows.Single(row => row.Entry == bad);
        Assert.Equal(MaterialRowState.Error, failed.State);
        Assert.Equal("read error", failed.DisplayCode);
        Assert.Equal("-", failed.DisplaySlots);
        Assert.Contains("short read at 42", failed.Error, StringComparison.Ordinal);
        Assert.Equal(2, result.DecodeCount);
    }

    [Fact]
    public void FilterMatchesShaderCodeHashAndArchive()
    {
        MaterialEntry eye = Entry(0xECE8A8E11B7F30B8, 3);
        MaterialEntry diffuse = Entry(0x0123456789ABCDEF, 4);
        MaterialListResult result = MaterialListBuilder.Build(
            [eye, diffuse],
            entry => entry.NameHash == eye.NameHash
                ? ("CODE_EYE", 3)
                : ("CODE_DIFFUSE", 6));

        Assert.Single(MaterialListBuilder.Filter(result.Rows, "CODE_EYE"));
        Assert.Single(MaterialListBuilder.Filter(result.Rows, "eye"));
        Assert.Single(MaterialListBuilder.Filter(result.Rows, "ece8a8e1"));
        Assert.Equal(2, MaterialListBuilder.Filter(result.Rows, "VENICE").Count);
        Assert.Equal(2, MaterialListBuilder.Filter(result.Rows, "").Count);
        Assert.Empty(MaterialListBuilder.Filter(result.Rows, "CODE_HAIR"));
    }

    [Fact]
    public void FilterKeepsEmptyAndErrorRows()
    {
        MaterialEntry empty = Entry(0x2EF96EC7369CDEAE, 1);
        MaterialEntry bad = Entry(0x2222222222222222, 2);
        MaterialListResult result = MaterialListBuilder.Build(
            [empty, bad],
            entry => entry.NameHash == bad.NameHash
                ? throw new InvalidDataException("bad body")
                : ("CODE_DIFFUSE", 0));

        IReadOnlyList<MaterialRow> rows = MaterialListBuilder.Filter(result.Rows, "");
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, row => row.State == MaterialRowState.Empty);
        Assert.Contains(rows, row => row.State == MaterialRowState.Error);
    }

    [Fact]
    public void BuildSortsByHashAndReportsProgress()
    {
        MaterialEntry later = Entry(0xBBBBBBBBBBBBBBBB, 2);
        MaterialEntry earlier = Entry(0xAAAAAAAAAAAAAAAA, 1);
        List<int> progress = new();

        MaterialListResult result = MaterialListBuilder.Build(
            [later, earlier],
            _ => ("CODE_EYE", 1),
            progress: new SyncProgress<int>(progress.Add));

        Assert.Equal([earlier, later], result.Rows.Select(row => row.Entry));
        Assert.Equal([2], progress);
    }

    [Fact]
    public void BuildHonorsCancellation()
    {
        MaterialEntry entry = Entry(0xAAAAAAAAAAAAAAAA, 1);
        using CancellationTokenSource cancel = new();
        cancel.Cancel();

        Assert.Throws<OperationCanceledException>(() => MaterialListBuilder.Build(
            [entry],
            _ => ("CODE_EYE", 1),
            cancel.Token));
    }

    private static MaterialEntry Entry(ulong nameHash, long offset) => new(
        @"C:\game\DATAS\VENICE.DPC",
        new MaterialHit(offset, nameHash, 0, MaterialHead.MaterialClass, 0));

    private sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
