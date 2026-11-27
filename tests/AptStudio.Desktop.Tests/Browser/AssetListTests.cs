using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class AssetListTests
{
    [Fact]
    public void ArrowKeysMoveSelectionAndEnterRaisesOpen()
    {
        using AssetList list = new();
        list.SetRows([Row("a"), Row("b"), Row("c")], null);
        list.SelectedIndex = 0;
        int opened = 0;
        list.OpenRequested += (_, _) => opened++;

        list.MoveSelection(1);
        Assert.Equal(1, list.SelectedIndex);

        list.ActivateSelected();
        Assert.Equal(1, opened);
    }

    [Fact]
    public void SetRowsKeepsSelectedKeyWhenPresent()
    {
        using AssetList list = new();
        list.SetRows([Row("a"), Row("b")], null);
        list.SelectedIndex = 1;

        list.SetRows([Row("a"), Row("b"), Row("c")], "b");

        Assert.Equal(1, list.SelectedIndex);
        Assert.Equal("b", list.SelectedRow?.Key);
    }

    [Fact]
    public void MultiSelectKeepsKeysAcrossSetRows()
    {
        using AssetList list = new();
        list.SetRows([Row("a"), Row("b"), Row("c")], "a", ["a", "c"]);

        Assert.Equal(new[] { "a", "c" }, list.SelectedKeys.OrderBy(key => key).ToArray());
        Assert.Equal("a", list.SelectedRow?.Key);

        list.SetRows([Row("c"), Row("a"), Row("b")], "a", ["a", "c"]);

        Assert.Equal(new[] { "a", "c" }, list.SelectedKeys.OrderBy(key => key).ToArray());
        Assert.Equal("a", list.SelectedRow?.Key);
        Assert.Equal(1, list.SelectedIndex);
    }

    [Fact]
    public void SortPreservesLiveSelectedKeys()
    {
        using AssetList list = new();
        list.SetRows([Row("a"), Row("b"), Row("c")], "a", ["a", "c"]);

        list.SetRows([Row("c"), Row("b"), Row("a")], "a", list.SelectedKeys);

        Assert.Equal(new[] { "a", "c" }, list.SelectedKeys.OrderBy(key => key).ToArray());
        Assert.Equal("a", list.SelectedRow?.Key);
    }

    [Fact]
    public void SelectAllKeepsPreviewOwner()
    {
        using AssetList list = new();
        list.SetRows([Row("a"), Row("b"), Row("c")], "b");

        list.SelectAll();

        Assert.Equal(3, list.SelectedKeys.Count);
        Assert.Equal("b", list.SelectedRow?.Key);
        Assert.Equal("b", list.CurrentKey);
    }

    [Fact]
    public void SelectedIndexUpdatesCurrentKey()
    {
        using AssetList list = new();
        list.SetRows([Row("a"), Row("b"), Row("c")], null);

        list.SelectedIndex = 2;

        Assert.Equal("c", list.CurrentKey);
    }

    [Fact]
    public void ClearingSelectionLeavesNoRowSelected()
    {
        using AssetList list = new();
        list.SetRows([Row("a"), Row("b")], null);
        list.SelectedIndex = 1;

        list.SelectedIndex = -1;

        Assert.Equal(-1, list.SelectedIndex);
        Assert.Null(list.SelectedRow);
        Assert.Null(list.CurrentKey);
        Assert.Empty(list.SelectedKeys);
    }

    [Fact]
    public void TopIndexScrollsWithoutNativeAutoScroll()
    {
        using AssetList list = new();
        List<AssetRow> rows = new();
        for (int index = 0; index < 100; index++)
        {
            rows.Add(Row($"row-{index}"));
        }

        list.SetRows(rows, null);
        list.TopIndex = 10;

        Assert.Equal(10, list.TopIndex);
        Assert.Equal("row-10", list.Rows[list.TopIndex].Key);
    }

    private static AssetRow Row(string key) => new(key, [key], key);
}
