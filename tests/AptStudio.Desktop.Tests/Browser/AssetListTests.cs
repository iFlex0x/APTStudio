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

    private static AssetRow Row(string key) => new(key, [key], key);
}
