// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.ComponentModel;
using AptStudio.Core.Textures;

namespace AptStudio.Desktop.Browser;

// One control so the channel group can never split across a wrapped row.
internal sealed class ChannelSegments : Control
{
    private static readonly (ChannelView View, string Text, int Width)[] Segments =
    [
        (ChannelView.Rgb, "RGB", 40),
        (ChannelView.Alpha, "A", 28),
        (ChannelView.Red, "R", 28),
        (ChannelView.Green, "G", 28),
        (ChannelView.Blue, "B", 28),
    ];

    private ChannelView _selected = ChannelView.Rgb;

    public ChannelSegments()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.UserPaint
            | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable,
            true);
        Height = 24;
        Width = Segments.Sum(segment => segment.Width);
        TabStop = true;
        AccessibleName = "Channel";
        Font = BrowserVisual.RowSmall;
    }

    public event EventHandler<ChannelView>? SelectedChannelChanged;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ChannelView SelectedChannel
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }

            _selected = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using Pen edge = new(BrowserVisual.BorderStrong);
        int[] widths = SegmentWidths();
        int x = 0;
        Rectangle selectedBounds = Rectangle.Empty;
        for (int index = 0; index < Segments.Length; index++)
        {
            (ChannelView view, string text, _) = Segments[index];
            int width = widths[index];
            bool selected = view == _selected;
            if (selected)
            {
                selectedBounds = new Rectangle(x, 0, width, ClientSize.Height - 1);
            }

            Color background = selected && Enabled
                ? BrowserVisual.AccentDim
                : Theme.Surface;
            using SolidBrush brush = new(background);
            e.Graphics.FillRectangle(brush, x, 0, width, ClientSize.Height - 1);
            TextRenderer.DrawText(
                e.Graphics,
                text,
                BrowserVisual.RowSmall,
                new Rectangle(x, 0, width, ClientSize.Height - 1),
                Enabled ? selected ? Theme.Accent : Theme.Muted : BrowserVisual.Faint,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            e.Graphics.DrawLine(edge, x, 0, x, ClientSize.Height - 1);
            x += width;
        }

        e.Graphics.DrawLine(edge, 0, 0, ClientSize.Width - 1, 0);
        e.Graphics.DrawLine(edge, 0, ClientSize.Height - 1, ClientSize.Width - 1, ClientSize.Height - 1);
        e.Graphics.DrawLine(edge, ClientSize.Width - 1, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        if (Enabled && Focused && selectedBounds.Width > 0)
        {
            using Pen focus = new(Theme.Accent);
            e.Graphics.DrawRectangle(
                focus,
                selectedBounds.X + 1,
                1,
                selectedBounds.Width - 3,
                ClientSize.Height - 3);
        }

        base.OnPaint(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (!Enabled)
        {
            base.OnMouseDown(e);
            return;
        }

        Focus();
        int[] widths = SegmentWidths();
        int x = 0;
        for (int index = 0; index < Segments.Length; index++)
        {
            if (e.X >= x && e.X < x + widths[index])
            {
                Select(Segments[index].View);
                break;
            }

            x += widths[index];
        }

        base.OnMouseDown(e);
    }

    private int[] SegmentWidths()
    {
        int[] widths = new int[Segments.Length];
        int baseTotal = Segments.Sum(segment => segment.Width);
        int total = ClientSize.Width;
        if (total <= 0 || baseTotal <= 0)
        {
            return widths;
        }

        int accumulated = 0;
        int previous = 0;
        for (int index = 0; index < Segments.Length; index++)
        {
            accumulated += Segments[index].Width;
            int boundary = index == Segments.Length - 1
                ? total
                : (int)Math.Round((double)accumulated * total / baseTotal);
            widths[index] = Math.Max(0, boundary - previous);
            previous = boundary;
        }

        return widths;
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override bool IsInputKey(Keys keyData)
    {
        return (keyData & Keys.KeyCode) is Keys.Left or Keys.Right
            || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int index = Array.FindIndex(Segments, segment => segment.View == _selected);
        if (e.KeyCode == Keys.Left && index > 0)
        {
            Select(Segments[index - 1].View);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Right && index >= 0 && index < Segments.Length - 1)
        {
            Select(Segments[index + 1].View);
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    private void Select(ChannelView view)
    {
        if (_selected == view)
        {
            return;
        }

        _selected = view;
        Invalidate();
        SelectedChannelChanged?.Invoke(this, view);
    }
}
