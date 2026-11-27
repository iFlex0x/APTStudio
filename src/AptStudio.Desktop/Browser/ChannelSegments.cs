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
        int x = 0;
        foreach ((ChannelView view, string text, int width) in Segments)
        {
            bool selected = view == _selected;
            using SolidBrush background = new(selected ? BrowserVisual.AccentDim : Theme.Surface);
            e.Graphics.FillRectangle(background, x, 0, width, ClientSize.Height - 1);
            TextRenderer.DrawText(
                e.Graphics,
                text,
                BrowserVisual.RowSmall,
                new Rectangle(x, 0, width, ClientSize.Height - 1),
                selected ? Theme.Accent : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            e.Graphics.DrawLine(edge, x, 0, x, ClientSize.Height - 1);
            x += width;
        }

        e.Graphics.DrawLine(edge, 0, 0, ClientSize.Width - 1, 0);
        e.Graphics.DrawLine(edge, 0, ClientSize.Height - 1, ClientSize.Width - 1, ClientSize.Height - 1);
        e.Graphics.DrawLine(edge, ClientSize.Width - 1, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        base.OnPaint(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        int x = 0;
        foreach ((ChannelView view, _, int width) in Segments)
        {
            if (e.X >= x && e.X < x + width)
            {
                Select(view);
                break;
            }

            x += width;
        }

        base.OnMouseDown(e);
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
