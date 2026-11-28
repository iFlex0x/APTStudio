// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class BrowserColumnsTests
{
    [Fact]
    public void TextureColumnsFollowTheActualResultsWidth()
    {
        Assert.Equal(
            ["Hash", "Archive", "Size", "Mips", "Format", "Storage"],
            Headers(BrowserColumns.For(BrowserView.Textures, 606)));
        Assert.Equal(
            ["Hash", "Archive"],
            Headers(BrowserColumns.For(BrowserView.Textures, 605)));
    }

    [Fact]
    public void AudioColumnsKeepTheFullIdAndLanguageInTheCompactSet()
    {
        IReadOnlyList<AssetColumn> compact = BrowserColumns.For(BrowserView.Audio, 332);

        Assert.Equal(["Pack", "Id", "Name", "Language"], Headers(compact));
        Assert.Equal([64, 82, 0, 54], compact.Select(column => column.Width).ToArray());
        Assert.Equal(332, AssetList.MinimumWidth(compact, showThumbnails: false));
        Assert.True(compact[2].Flexible);
    }

    [Fact]
    public void AudioUsesTheWideSetOnlyWhenTheMeasuredWidthFits()
    {
        Assert.Equal(
            ["Pack", "Id", "Name", "Language", "Length", "Ch", "Rate", "Bank"],
            Headers(BrowserColumns.For(BrowserView.Audio, 740)));
        Assert.Equal(
            ["Pack", "Id", "Name", "Language"],
            Headers(BrowserColumns.For(BrowserView.Audio, 739)));
    }

    [Fact]
    public void OtherViewsKeepTheirSingleColumnSet()
    {
        Assert.Equal(
            ["Hash", "Archive", "Occurrences"],
            Headers(BrowserColumns.For(BrowserView.Animations, 100)));
        Assert.Equal(
            ["Hash", "Code", "Slots", "Archive"],
            Headers(BrowserColumns.For(BrowserView.Materials, 100)));
    }

    private static string[] Headers(IReadOnlyList<AssetColumn> columns) =>
        columns.Select(column => column.Header).ToArray();
}
