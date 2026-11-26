using System.Numerics;
using AptStudio.Core.Skeletons;

namespace AptStudio.Desktop.Tests;

public sealed class SkeletonViewTests
{
    [Fact]
    public void ColumnsFitAndBoneUsesMono()
    {
        using SkeletonView view = new();
        view.Size = new Size(520, 600);
        view.CreateControl();
        view.PerformLayout();
        DataGridView grid = FindGrid(view)
            ?? throw new InvalidOperationException("No grid in the tree.");

        Assert.Equal(
            [138, 52, 44, 46, 46, 50],
            grid.Columns.Cast<DataGridViewColumn>().Select(column => column.Width).ToArray());
        Font font = Assert.IsType<Font>(grid.Columns[0].DefaultCellStyle.Font);
        Assert.Equal("Consolas", font.Name);
        Assert.True(grid.Columns.Cast<DataGridViewColumn>().Sum(column => column.Width) <= 520);
        foreach (DataGridViewColumn column in grid.Columns)
        {
            int text = TextRenderer.MeasureText(
                column.HeaderText, grid.ColumnHeadersDefaultCellStyle.Font).Width;
            Assert.True(
                text <= column.Width - 8,
                $"{column.HeaderText} is {text} px, column is {column.Width}");
        }
    }

    [Fact]
    public void SetSkeletonFillsBoneRows()
    {
        using SkeletonView view = new();
        view.SetSkeleton("Skel_Z 0x1", "test", Data());

        DataGridView grid = FindGrid(view)
            ?? throw new InvalidOperationException("No grid in the tree.");
        Assert.Equal(2, grid.Rows.Count);
        Assert.Equal("0000000000000011", grid.Rows[0].Cells[0].Value);
        Assert.Equal("-1", grid.Rows[0].Cells[1].Value);
        Assert.Equal("1.000", grid.Rows[1].Cells[3].Value);
    }

    private static SkeletonData Data()
    {
        SkeletonHead head = new(
            SkeletonHead.SkeletonClass,
            0xABC,
            0,
            20,
            100,
            100,
            1);
        SkeletonBone[] bones =
        [
            new(0x11, 0xABC, -1, 1, Matrix4x4.Identity),
            new(0x22, 0xABC, 0, -1, Matrix4x4.CreateTranslation(1f, 2f, 3f)),
        ];
        return new SkeletonData(
            head,
            bones,
            4 + 2 * SkeletonData.BoneRecordSize,
            0);
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
