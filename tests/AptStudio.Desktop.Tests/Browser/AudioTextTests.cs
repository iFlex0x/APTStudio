// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Core.Audio;
using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class AudioTextTests
{
    [Fact]
    public void UnnamedVoiceLineKeepsLanguageAndId()
    {
        AudioEntry media = Entry(null, "VO_MEDIA.PC.PCK", "french(france)", 0x1EEAB9D4);

        Assert.Equal("VO_MEDIA_1EEAB9D4_french.ogg", AudioText.ExportName(media));
    }

    [Fact]
    public void NamedTrackUsesItsName()
    {
        AudioEntry media = Entry("The Great Escape", "MUS_MEDIA.PC.PCK", "sfx", 0x27F2B);

        Assert.Equal("The Great Escape.ogg", AudioText.ExportName(media));
    }

    [Fact]
    public void InvalidFileNameCharactersAreReplaced()
    {
        AudioEntry media = Entry("Name: Part 1", "MUS_MEDIA.PC.PCK", "sfx", 1);

        Assert.Equal("Name_ Part 1.ogg", AudioText.ExportName(media));
    }

    [Fact]
    public void ShortLanguageDropsTheRegion()
    {
        Assert.Equal("french", AudioText.ShortLanguage("french(france)"));
        Assert.Equal("sfx", AudioText.ShortLanguage("sfx"));
    }

    private static AudioEntry Entry(
        string? name,
        string packName,
        string language,
        uint id) =>
        new(
            $@"C:\game\SOUNDBANKS\{packName}",
            packName,
            0,
            id,
            id,
            1000,
            0,
            language,
            new WemInfo(0xFFFF, 1, 48000, 48000, name),
            name);
}
