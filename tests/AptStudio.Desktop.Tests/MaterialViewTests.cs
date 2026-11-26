namespace AptStudio.Desktop.Tests;

public sealed class MaterialViewTests
{
    [Fact]
    public void SlotColumnsFitAndTextureUsesMono()
    {
        using MaterialView view = new();
        view.Size = new Size(520, 600);
        view.CreateControl();
        view.PerformLayout();
        DataGridView grid = FindGrid(view)
            ?? throw new InvalidOperationException("No grid in the tree.");

        Assert.Equal(
            [30, 100, 118, 145, 100],
            grid.Columns.Cast<DataGridViewColumn>().Select(column => column.Width).ToArray());
        Font font = Assert.IsType<Font>(grid.Columns[3].DefaultCellStyle.Font);
        Assert.Equal("Consolas", font.Name);
        Size text = TextRenderer.MeasureText("0x838030277F347FF4", font);
        Assert.True(text.Width <= 137, $"{font.Name} {font.Size} is {text.Width} px wide");
        Size archive = TextRenderer.MeasureText("COMMON.DPC", grid.Font);
        Assert.True(archive.Width <= 92, $"archive text is {archive.Width} px wide");
        Assert.True(grid.Columns.Cast<DataGridViewColumn>().Sum(column => column.Width) <= 520);
    }

    [Fact]
    public void SelectSlotRaisesSlotSelected()
    {
        using MaterialView view = new();
        view.SetMaterial(
            "CODE_TEST | Material_Z",
            "0x1 | TEST.DPC | 2 slots",
            [
                new MaterialSlotRow(0, "Texture", "sDiffuse", 0x11, "0x11", "A.DPC"),
                new MaterialSlotRow(2, "Occlusion", "sBlendMask", 0x22, "0x22", "B.DPC"),
            ]);
        List<ulong> seen = new();
        view.SlotSelected += seen.Add;

        view.SelectSlot(1);

        Assert.Equal([0x22UL], seen);
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
