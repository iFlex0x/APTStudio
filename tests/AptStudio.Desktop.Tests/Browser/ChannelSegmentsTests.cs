// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class ChannelSegmentsTests
{
    [Fact]
    public void ArrowKeysChangeChannelWithoutMovingFocus()
    {
        using Form form = new() { Width = 600, Height = 320, ShowInTaskbar = false };
        using ChannelSegments channels = new() { Left = 10, Top = 10 };
        using Button next = new() { Left = 220, Top = 10, Text = "Next" };
        form.Controls.AddRange([channels, next]);
        form.Show();
        Application.DoEvents();

        Assert.True(channels.Focus());
        PostedInput.Key(channels, Keys.Right);
        Application.DoEvents();

        Assert.Equal(ChannelView.Alpha, channels.SelectedChannel);
        Assert.Same(channels, form.ActiveControl);

        PostedInput.Key(channels, Keys.Left);
        Application.DoEvents();

        Assert.Equal(ChannelView.Rgb, channels.SelectedChannel);
        Assert.Same(channels, form.ActiveControl);
    }

    [Fact]
    public void FocusDrawsAnAccentOutline()
    {
        using Form form = new() { Width = 600, Height = 320, ShowInTaskbar = false };
        using ChannelSegments channels = new() { Left = 10, Top = 10 };
        using Button next = new() { Left = 220, Top = 10, Text = "Next" };
        form.Controls.AddRange([channels, next]);
        form.Show();
        Application.DoEvents();

        Assert.True(next.Focus());
        Application.DoEvents();
        using Bitmap idle = Capture(channels);

        Assert.True(channels.Focus());
        Application.DoEvents();
        using Bitmap active = Capture(channels);

        Assert.True(CountAccent(active) > CountAccent(idle));
    }

    [Fact]
    public void ScaledSegmentsSpreadPaintAndHitTestingAcrossTheClientWidth()
    {
        using Form form = new() { Width = 600, Height = 320, ShowInTaskbar = false };
        using ChannelSegments channels = new() { Left = 10, Top = 10 };
        form.Controls.Add(channels);
        form.Show();
        Application.DoEvents();

        channels.Scale(new SizeF(1.5f, 1.5f));
        Application.DoEvents();

        Assert.Equal(228, channels.ClientSize.Width);
        ClickSegment(channels, 205);
        Assert.Equal(ChannelView.Blue, channels.SelectedChannel);

        using Bitmap painted = Capture(channels);
        Assert.Equal(BrowserVisual.AccentDim, painted.GetPixel(205, 5));
    }

    private static void ClickSegment(Control control, int x)
    {
        System.Reflection.MethodInfo info = typeof(Control).GetMethod(
            "OnMouseDown",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException("OnMouseDown not found.");
        info.Invoke(control, [new MouseEventArgs(MouseButtons.Left, 1, x, 5, 0)]);
    }

    private static Bitmap Capture(Control control)
    {
        Bitmap bitmap = new(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return bitmap;
    }

    private static int CountAccent(Bitmap bitmap)
    {
        int count = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y) == Theme.Accent)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
