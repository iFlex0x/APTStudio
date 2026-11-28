// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AptStudio.Desktop.Tests;

public sealed class PreviewControlTests
{
    [Fact]
    public void PanningAtActualSizePublishesTheFinalOrigin()
    {
        using Form form = new() { Width = 400, Height = 400, ShowInTaskbar = false };
        using PreviewControl preview = new() { Size = new Size(300, 300) };
        form.Controls.Add(preview);
        form.Show();
        Application.DoEvents();
        preview.SetImage(new Bitmap(50, 50));
        preview.ActualSize();
        Assert.Equal(new PointF(125, 125), preview.Origin);

        List<PointF> views = [];
        preview.ZoomChanged += (_, _) => views.Add(preview.Origin);

        PostedInput.MouseDown(preview, 150, 150);
        PostedInput.MouseMove(preview, 160, 170);
        PostedInput.MouseMove(preview, 180, 190);
        Application.DoEvents();

        Assert.Equal(new PointF(155, 165), preview.Origin);
        Assert.NotEmpty(views);
        Assert.Equal(preview.Origin, views[^1]);
        int settled = views.Count;

        PostedInput.MouseMove(preview, 180, 190);
        Application.DoEvents();
        Assert.Equal(settled, views.Count);

        PostedInput.MouseUp(preview, 180, 190);
        Application.DoEvents();
    }

    [Fact]
    public void CaptureLossStopsPanningAndRestoresTheCursor()
    {
        using Form form = new() { Width = 400, Height = 400, ShowInTaskbar = false };
        using PreviewControl preview = new() { Size = new Size(300, 300) };
        using Button next = new() { Left = 310, Text = "Next" };
        form.Controls.AddRange([preview, next]);
        form.Show();
        Application.DoEvents();
        preview.SetImage(new Bitmap(50, 50));
        preview.ActualSize();

        PostedInput.MouseDown(preview, 150, 150);
        PostedInput.MouseMove(preview, 160, 170);
        Application.DoEvents();
        PointF moved = preview.Origin;
        Assert.NotEqual(new PointF(125, 125), moved);

        next.Capture = true;
        Application.DoEvents();
        Assert.Equal(Cursors.Default, preview.Cursor);

        PostedInput.MouseMove(preview, 190, 210);
        Application.DoEvents();

        Assert.Equal(moved, preview.Origin);

        next.Capture = false;
        PostedInput.MouseDown(preview, 150, 150);
        PostedInput.MouseMove(preview, 170, 180);
        Application.DoEvents();

        Assert.NotEqual(moved, preview.Origin);

        PostedInput.MouseUp(preview, 170, 180);
        Application.DoEvents();
    }
}
