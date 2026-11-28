// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing;

namespace AptStudio.Desktop.Browser;

internal static class BrowserVisual
{
    public static readonly Color Faint = Color.FromArgb(143, 143, 143);
    public static readonly Color BorderStrong = Color.FromArgb(74, 74, 79);
    public static readonly Color SurfaceRaised = Theme.GridHeader;
    public static readonly Color SurfaceHover = Color.FromArgb(51, 51, 55);
    public static readonly Color AccentDim = Color.FromArgb(29, 76, 96);
    public static readonly Color RowBorder = Color.FromArgb(42, 42, 43);
    public static readonly Color PrimaryText = Color.FromArgb(6, 32, 43);
    public static readonly Color Crumb = Color.FromArgb(190, 190, 190);

    public static readonly Font Ui = new("Segoe UI", 9f);
    public static readonly Font UiSmall = new("Segoe UI", 8.25f);
    public static readonly Font Title = new("Segoe UI Semibold", 11.25f);
    public static readonly Font Brand = new("Segoe UI Semibold", 9.75f);
    public static readonly Font MonoStrong = new("Consolas", 10.5f, FontStyle.Bold);
    public static readonly Font Section = new("Segoe UI", 7.5f);
    public static readonly Font Row = new("Segoe UI", 8.5f);
    public static readonly Font RowSmall = new("Segoe UI", 7.875f);

    public const int ThumbnailSize = 34;

    public static void DisposeChildren(Control host)
    {
        Control[] children = new Control[host.Controls.Count];
        host.Controls.CopyTo(children, 0);
        host.Controls.Clear();
        foreach (Control child in children)
        {
            child.Dispose();
        }
    }

    public static Label CreateSectionLabel(string text)
    {
        return new Label
        {
            Text = text,
            Font = Section,
            ForeColor = Faint,
            AutoEllipsis = true,
            AutoSize = false,
            Height = 16,
            Margin = new Padding(6, 0, 0, 4),
        };
    }

    public static void StyleQuietGrid(DataGridView grid)
    {
        grid.BackgroundColor = Theme.Background;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.GridColor = RowBorder;
        grid.RowHeadersVisible = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.ReadOnly = true;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersHeight = 30;
        grid.RowTemplate.Height = 36;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Theme.GridHeader;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Faint;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.GridHeader;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Faint;
        grid.ColumnHeadersDefaultCellStyle.Font = Section;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
        grid.DefaultCellStyle.BackColor = Theme.Background;
        grid.DefaultCellStyle.ForeColor = Theme.Text;
        grid.DefaultCellStyle.SelectionBackColor = Theme.Selection;
        grid.DefaultCellStyle.SelectionForeColor = Theme.Text;
        grid.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Theme.Background;
        grid.AlternatingRowsDefaultCellStyle.ForeColor = Theme.Text;
        grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Theme.Selection;
        grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = Theme.Text;
    }

    public static void Frame(Control control)
    {
        control.Paint += (_, e) =>
        {
            using Pen border = new(BorderStrong);
            e.Graphics.DrawRectangle(
                border, 0, 0, control.ClientSize.Width - 1, control.ClientSize.Height - 1);
        };
    }

    public static string EllipsizeMiddle(string text, Font font, int maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0)
        {
            return text;
        }

        int limit = Math.Max(0, maxWidth - 4);
        if (Measure(text, font) <= limit)
        {
            return text;
        }

        const string dots = "...";
        int dotsWidth = Measure(dots, font);
        if (dotsWidth >= limit)
        {
            return dots;
        }

        int budget = limit - dotsWidth;
        int suffix = TokenSuffix(text, font, Math.Max(3, budget * 2 / 3));
        if (suffix <= 0)
        {
            suffix = LongestSuffix(text, font, Math.Max(3, budget * 2 / 3));
        }
        int prefix = 0;
        while (suffix > 3)
        {
            prefix = LongestPrefix(
                text,
                font,
                budget - Measure(text[^suffix..], font),
                text.Length - suffix);
            if (prefix >= 3)
            {
                break;
            }

            suffix--;
        }

        suffix = Math.Clamp(suffix, 3, text.Length);
        prefix = LongestPrefix(
            text,
            font,
            budget - Measure(text[^suffix..], font),
            text.Length - suffix);
        if (prefix <= 0 || prefix + suffix >= text.Length)
        {
            return dots + text[^Math.Min(suffix, text.Length)..];
        }

        return text[..prefix] + dots + text[^suffix..];
    }

    private static int Measure(string text, Font font) =>
        string.IsNullOrEmpty(text)
            ? 0
            : TextRenderer.MeasureText(
                text,
                font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;

    private static int LongestPrefix(string text, Font font, int width, int limit)
    {
        int low = 0;
        int high = Math.Clamp(limit, 0, text.Length);
        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            if (Measure(text[..middle], font) <= width)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return low;
    }

    private static int TokenSuffix(string text, Font font, int width)
    {
        for (int index = 1; index < text.Length - 3; index++)
        {
            if (text[index - 1] != '_' || text[index] == '_')
            {
                continue;
            }

            string suffix = text[index..];
            if (Measure(suffix, font) <= width)
            {
                return suffix.Length;
            }
        }

        return 0;
    }

    private static int LongestSuffix(string text, Font font, int width)
    {
        int low = 0;
        int high = text.Length;
        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            if (Measure(text[^middle..], font) <= width)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return low;
    }
}
