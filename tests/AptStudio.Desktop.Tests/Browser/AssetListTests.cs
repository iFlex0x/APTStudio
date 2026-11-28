using System.Reflection;
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

    [Fact]
    public void ArrowKeysStayWithTheListInsteadOfMovingFocus()
    {
        using Form form = new() { Width = 600, Height = 320, ShowInTaskbar = false };
        using AssetList list = new() { Size = new Size(500, 240) };
        using Button next = new() { Left = 510, Text = "Next" };
        form.Controls.AddRange([list, next]);
        list.SetRows([Row("a"), Row("b"), Row("c")], null);
        list.SelectedIndex = 0;
        form.Show();
        Application.DoEvents();

        Assert.True(list.Focus());
        PostedInput.Key(list, Keys.Down);
        Application.DoEvents();

        Assert.Equal(1, list.SelectedIndex);
        Assert.Same(list, form.ActiveControl);
    }

    [Fact]
    public void ArrowModifiersAreTreatedAsInputKeys()
    {
        using AssetList list = new();

        Assert.True(IsInputKey(list, Keys.Down));
        Assert.True(IsInputKey(list, Keys.Up));
        Assert.True(IsInputKey(list, Keys.Down | Keys.Shift));
        Assert.False(IsInputKey(list, Keys.Tab));
        Assert.False(IsInputKey(list, Keys.Escape));
    }

    [Fact]
    public void ScrollBarTrackClickReachesTheEndOfALargeList()
    {
        using AssetList list = LargeList();
        list.SelectedIndex = 5;

        InvokeMouse(list, "OnMouseDown", 496, 696);

        Assert.Equal(235_394, list.TopIndex);
        Assert.Equal(5, list.SelectedIndex);
    }

    [Fact]
    public void ScrollBarThumbDragUsesSignedLongArithmetic()
    {
        using AssetList list = LargeList();
        list.SelectedIndex = 5;
        list.ScrollOffset = 7_061_840;

        InvokeMouse(list, "OnMouseDown", 496, 680);
        InvokeMouse(list, "OnMouseMove", 496, 280);

        Assert.Equal(2_858_364, list.ScrollOffset);
        Assert.Equal(5, list.SelectedIndex);
        InvokeMouse(list, "OnMouseUp", 496, 280);

        list.TopIndex = 0;
        InvokeMouse(list, "OnMouseDown", 496, 10);
        InvokeMouse(list, "OnMouseMove", 496, 410);

        Assert.Equal(4_203_476, list.ScrollOffset);
        Assert.Equal(5, list.SelectedIndex);
        InvokeMouse(list, "OnMouseUp", 496, 410);
    }

    [Fact]
    public void CaptureLossStopsScrollbarDragging()
    {
        using Form form = new() { Width = 600, Height = 760, ShowInTaskbar = false };
        using AssetList list = LargeList();
        using Button next = new() { Left = 510, Text = "Next" };
        form.Controls.AddRange([list, next]);
        form.Show();
        Application.DoEvents();

        InvokeMouse(list, "OnMouseDown", 496, 680);
        InvokeMouse(list, "OnMouseMove", 496, 600);
        int moved = list.ScrollOffset;
        Assert.NotEqual(0, moved);

        next.Capture = true;
        Application.DoEvents();
        InvokeMouse(list, "OnMouseMove", 496, 100);

        Assert.Equal(moved, list.ScrollOffset);

        next.Capture = false;
        int before = list.ScrollOffset;
        InvokeMouse(list, "OnMouseDown", 496, 300);
        InvokeMouse(list, "OnMouseMove", 496, 320);

        Assert.NotEqual(before, list.ScrollOffset);
        InvokeMouse(list, "OnMouseUp", 496, 320);
    }

    private static AssetList LargeList()
    {
        AssetList list = new() { Size = new Size(500, 700) };
        list.SetRowHeight(30);
        list.SetRows(
            [.. Enumerable.Range(0, 235_418).Select(index => Row($"row-{index}"))],
            null);
        return list;
    }

    private static bool IsInputKey(Control control, Keys key)
    {
        MethodInfo info = typeof(Control).GetMethod(
            "IsInputKey", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("IsInputKey not found.");
        return (bool)info.Invoke(control, [key])!;
    }

    private static void InvokeMouse(Control control, string method, int x, int y)
    {
        MethodInfo info = typeof(Control).GetMethod(
            method, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{method} not found.");
        info.Invoke(control, [new MouseEventArgs(MouseButtons.Left, 1, x, y, 0)]);
    }

    private static AssetRow Row(string key) => new(key, [key], key);
}
