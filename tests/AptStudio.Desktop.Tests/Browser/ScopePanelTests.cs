// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Reflection;
using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class ScopePanelTests
{
    [Fact]
    public void SetTypesDisposesThePreviousRows()
    {
        using ScopePanel panel = new();
        panel.SetTypes([new TypeChoice("Textures", 10)], "Textures");
        ScopeRow[] first = Rows(panel).ToArray();
        Assert.NotEmpty(first);

        panel.SetTypes([new TypeChoice("Meshes", 2)], "Meshes");

        Assert.All(first, row => Assert.True(row.IsDisposed));
        ScopeRow[] remaining = Rows(panel).ToArray();
        Assert.Single(remaining);
        Assert.Equal("Meshes", remaining[0].Text);
    }

    [Fact]
    public void HoverInvalidatesOnlyTheChangedRows()
    {
        using Form form = new() { Width = 420, Height = 900, ShowInTaskbar = false };
        using ScopePanel panel = new() { Dock = DockStyle.Fill };
        form.Controls.Add(panel);
        panel.SetArchives(ManyArchives(220), "");
        form.Show();
        Application.DoEvents();

        ListBox archives = Archives(panel);
        Assert.True(archives.Items.Count > 150);
        Assert.True(archives.ClientSize.Height >= archives.ItemHeight * 4);

        List<Rectangle> invalidated = new();
        void Track(object? sender, InvalidateEventArgs e) => invalidated.Add(e.InvalidRect);
        archives.Invalidated += Track;
        try
        {
            SetHover(panel, 2);
            Assert.Single(invalidated);
            AssertRowRectangle(archives, invalidated[0]);

            invalidated.Clear();
            SetHover(panel, 3);
            Assert.Equal(2, invalidated.Count);
            Assert.All(invalidated, rectangle => AssertRowRectangle(archives, rectangle));

            invalidated.Clear();
            SetHover(panel, 3);
            Assert.Empty(invalidated);

            invalidated.Clear();
            SetHover(panel, 150);
            Assert.Single(invalidated);
            AssertRowRectangle(archives, invalidated[0]);

            invalidated.Clear();
            SetHover(panel, 2);
            Assert.Single(invalidated);
            AssertRowRectangle(archives, invalidated[0]);

            invalidated.Clear();
            SetHover(panel, -1);
            Assert.Single(invalidated);
            AssertRowRectangle(archives, invalidated[0]);

            invalidated.Clear();
            SetHover(panel, -1);
            Assert.Empty(invalidated);

            invalidated.Clear();
            SetHover(panel, 5_000);
            Assert.Empty(invalidated);
        }
        finally
        {
            archives.Invalidated -= Track;
        }
    }

    [Fact]
    public void HoverWritesTheToolTipOnlyWhenTheRowChanges()
    {
        using Form form = new() { Width = 420, Height = 900, ShowInTaskbar = false };
        using ScopePanel panel = new() { Dock = DockStyle.Fill };
        form.Controls.Add(panel);
        panel.SetArchives(
            [
                new ArchiveChoice("", 1_000, "All archives"),
                new ArchiveChoice(@"C:\games\GREECE.DPC", 5_037),
                new ArchiveChoice(@"C:\games\A_LONG_ARCHIVE_NAME_THAT_NEVER_FITS_A_ROW.DPC", 12),
            ],
            "");
        form.Show();
        Application.DoEvents();

        ListBox archives = Archives(panel);
        ToolTip tip = Tip(panel);
        SetHover(panel, -1);
        Assert.Equal(string.Empty, tip.GetToolTip(archives));

        SetHover(panel, 1);
        Assert.Equal("GREECE.DPC", tip.GetToolTip(archives));

        SetHover(panel, 2);
        Assert.Equal(
            "A_LONG_ARCHIVE_NAME_THAT_NEVER_FITS_A_ROW.DPC",
            tip.GetToolTip(archives));

        tip.SetToolTip(archives, "unchanged");
        SetHover(panel, 2);
        Assert.Equal("unchanged", tip.GetToolTip(archives));

        SetHover(panel, -1);
        Assert.Equal(string.Empty, tip.GetToolTip(archives));
    }

    private static ListBox Archives(ScopePanel panel)
    {
        return (ListBox)typeof(ScopePanel)
            .GetField("_archives", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(panel)!;
    }

    private static ToolTip Tip(ScopePanel panel)
    {
        return (ToolTip)typeof(ScopePanel)
            .GetField("_tip", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(panel)!;
    }

    private static void SetHover(ScopePanel panel, int index)
    {
        typeof(ScopePanel)
            .GetMethod("SetHover", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(panel, [index]);
    }

    private static void AssertRowRectangle(ListBox archives, Rectangle rectangle)
    {
        Assert.True(rectangle.Height > 0);
        Assert.True(rectangle.Width > 0);
        Assert.True(
            rectangle.Height <= archives.ItemHeight,
            $"invalidated height {rectangle.Height} exceeds item height {archives.ItemHeight}");
        Assert.True(
            rectangle.Width <= archives.ClientSize.Width,
            $"invalidated width {rectangle.Width} exceeds client width {archives.ClientSize.Width}");
        Assert.True(archives.ClientRectangle.Contains(rectangle));
    }

    private static ArchiveChoice[] ManyArchives(int count)
    {
        ArchiveChoice[] choices = new ArchiveChoice[count];
        for (int index = 0; index < count; index++)
        {
            choices[index] = new ArchiveChoice($@"C:\games\ARCHIVE_{index:D3}.DPC", index * 7);
        }

        choices[0] = new ArchiveChoice("", 1_000, "All archives");
        return choices;
    }

    private static IEnumerable<ScopeRow> Rows(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is ScopeRow row)
            {
                yield return row;
            }

            foreach (ScopeRow nested in Rows(child))
            {
                yield return nested;
            }
        }
    }
}
