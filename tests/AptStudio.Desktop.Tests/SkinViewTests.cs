namespace AptStudio.Desktop.Tests;

public sealed class SkinViewTests
{
    [Fact]
    public void PartColumnsFitAndMeshUsesMono()
    {
        using SkinView view = new();
        view.Size = new Size(520, 600);
        view.CreateControl();
        view.PerformLayout();
        DataGridView grid = FindGrid(view)
            ?? throw new InvalidOperationException("No grid in the tree.");

        Assert.Equal(
            [30, 146, 58, 56, 64, 150],
            grid.Columns.Cast<DataGridViewColumn>().Select(column => column.Width).ToArray());
        Font font = Assert.IsType<Font>(grid.Columns[1].DefaultCellStyle.Font);
        Assert.Equal("Consolas", font.Name);
        Size text = TextRenderer.MeasureText("0x838030277F347FF4", font);
        Assert.True(text.Width <= 138, $"{font.Name} {font.Size} is {text.Width} px wide");
        Assert.True(grid.Columns.Cast<DataGridViewColumn>().Sum(column => column.Width) <= 520);
    }

    [Fact]
    public void SelectPartTracksMeshHash()
    {
        using SkinView view = new();
        view.SetSkin(
            "Skin_Z 0x1",
            "v11 | 2 meshes | 2 parts | 6 bones",
            [
                new SkinPartRow(0, 0x11, 64, 16, 1, true, 0xAA),
                new SkinPartRow(1, 0x22, 0, 0, 0, false, 0),
            ],
            0);

        Assert.Equal(0x11UL, view.SelectedMeshHash);

        view.SelectPart(1);

        Assert.Equal(0x22UL, view.SelectedMeshHash);
    }

    [Fact]
    public void PartChangedReportsTheSelectedPart()
    {
        using SkinView view = new();
        view.SetSkin(
            "Skin_Z 0x1",
            "v11 | 2 meshes | 2 parts | 6 bones",
            [
                new SkinPartRow(0, 0x11, 64, 16, 1, true, 0xAA),
                new SkinPartRow(1, 0x22, 32, 16, 1, true, 0xBB),
            ],
            0);
        List<int> changed = new();
        view.PartChanged += part => changed.Add(part);

        view.SelectPart(1);

        Assert.Equal([1], changed);
    }

    [Fact]
    public void SkeletonButtonFollowsBinding()
    {
        using SkinView view = new();
        view.CreateControl();
        Button button = FindButton(view, "Open Skeleton")
            ?? throw new InvalidOperationException("No skeleton button in the tree.");
        Assert.False(button.Enabled);

        view.SetSkin("Skin_Z 0x1", "no skeleton", [], 0);
        Assert.False(button.Enabled);

        view.SetSkin("Skin_Z 0x1", "skeleton 0x99", [], 0x99);
        Assert.True(button.Enabled);

        ulong? requested = null;
        view.SkeletonRequested += hash => requested = hash;
        button.PerformClick();
        Assert.Equal(0x99UL, requested);
    }

    [Fact]
    public void PoseButtonFollowsBinding()
    {
        using SkinView view = new();
        view.CreateControl();
        Button button = FindButton(view, "Pose")
            ?? throw new InvalidOperationException("No pose button in the tree.");
        Assert.False(button.Enabled);

        view.SetSkin("Skin_Z 0x1", "no skeleton", [], 0);
        Assert.False(button.Enabled);

        view.SetSkin("Skin_Z 0x1", "skeleton 0x99", [], 0x99);
        Assert.True(button.Enabled);

        ulong? requested = null;
        view.PoseRequested += hash => requested = hash;
        button.PerformClick();
        Assert.Equal(0x99UL, requested);
    }

    private static Button? FindButton(Control root, string text)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Button button && button.Text == text)
            {
                return button;
            }

            Button? found = FindButton(child, text);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static DataGridView? FindGrid(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is DataGridView grid)
            {
                return grid;
            }

            DataGridView? found = FindGrid(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
