// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class SkinChooserTests
{
    [Fact]
    public void MeshChooserKeepsPartsAndOpensTheSelectedOccurrence()
    {
        using SkinChooserView view = new(showParts: true);
        view.Size = new Size(520, 400);
        view.CreateControl();
        view.PerformLayout();
        SkinChoiceRow first = new(0xAAA, "A.DPC", "14", "14 parts", "first");
        SkinChoiceRow second = new(0xBBB, "B.DPC", "9", "9 parts", "second");
        SkinChoiceRow? opened = null;
        view.OpenRequested += (_, row) => opened = row;
        view.SetRows([first, second], "No skin uses this mesh.");

        DataGridView grid = FindGrid(view)
            ?? throw new InvalidOperationException("No grid in the tree.");
        Assert.Equal(3, grid.Columns.Count);
        Assert.Equal(["PARTS"], [grid.Columns[2].HeaderText]);
        Assert.Equal(2, grid.Rows.Count);
        Assert.Equal(0, grid.CurrentRow?.Index);

        Button open = FindButton(view, "Open skin")
            ?? throw new InvalidOperationException("No open button.");
        open.PerformClick();

        Assert.Same(first, opened);
    }

    [Fact]
    public void SkeletonChooserHidesPartsAndRejectsAnEmptyOpen()
    {
        using SkinChooserView view = new(showParts: false);
        SkinChoiceRow? opened = null;
        view.OpenRequested += (_, row) => opened = row;
        view.SetRows([], "No skin is explicitly bound to this skeleton.");

        DataGridView grid = FindGrid(view)
            ?? throw new InvalidOperationException("No grid in the tree.");
        Assert.Equal(2, grid.Columns.Count);
        Assert.Equal(0, grid.Rows.Count);

        Button open = FindButton(view, "Open skin")
            ?? throw new InvalidOperationException("No open button.");
        Assert.False(open.Enabled);
        open.PerformClick();
        Assert.Null(opened);
    }

    [Fact]
    public void EmptyRowsKeepTheCallerMessage()
    {
        using SkinChooserView view = new(showParts: true);

        view.SetRows([], "No skin uses this mesh.");

        Assert.Contains("No skin uses this mesh.", TextValues(view));
    }

    private static IEnumerable<string> TextValues(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child.Text.Length > 0)
            {
                yield return child.Text;
            }

            foreach (string text in TextValues(child))
            {
                yield return text;
            }
        }
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
}
