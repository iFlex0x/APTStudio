// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class BrowserVisualTests
{
    [Fact]
    public void ShortTextIsUnchanged()
    {
        Assert.Equal(
            "GREECE.DPC",
            BrowserVisual.EllipsizeMiddle("GREECE.DPC", BrowserVisual.Row, 400));
    }

    [Fact]
    public void LongTextKeepsTheDistinguishingSuffix()
    {
        const string text = "GENERIC_KISSING_COUPLE_00_FLIRT_SHY.DPC";

        string result = BrowserVisual.EllipsizeMiddle(text, BrowserVisual.Row, 180);

        Assert.Contains("...", result);
        Assert.StartsWith("GENERIC", result);
        Assert.EndsWith("FLIRT_SHY.DPC", result);
        Assert.DoesNotContain("...LIRT", result);
    }

    [Fact]
    public void ResultFitsTheRequestedWidth()
    {
        const string text = "GENERIC_KISSING_COUPLE_00_FLIRT_SHY.DPC";
        const int width = 120;

        string result = BrowserVisual.EllipsizeMiddle(text, BrowserVisual.Row, width);
        int measured = TextRenderer.MeasureText(
            result,
            BrowserVisual.Row,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;

        Assert.True(measured <= width, $"{measured} > {width} for '{result}'");
    }

    [Fact]
    public void TinyWidthFallsBackToEllipsis()
    {
        Assert.Equal(
            "...",
            BrowserVisual.EllipsizeMiddle("GENERIC_KISSING", BrowserVisual.Row, 4));
    }
}
