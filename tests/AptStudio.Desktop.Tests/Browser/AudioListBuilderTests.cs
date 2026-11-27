using AptStudio.Core.Audio;
using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class AudioListBuilderTests
{
    [Fact]
    public void SameIdInDifferentLanguagesKeepsBothOccurrences()
    {
        AudioEntry french = Entry(0x1EEAB9D4, 4, "french(france)", 56.67);
        AudioEntry english = Entry(0x1EEAB9D4, 1, "english(us)", 12.5);

        List<AssetRow> rows = AudioListBuilder.Build(
            [french, english],
            null,
            null,
            "",
            AudioSort.Pack,
            false,
            CancellationToken.None);

        Assert.Equal(2, rows.Count);
        Assert.NotEqual(rows[0].Key, rows[1].Key);
        Assert.Equal(0x1EEAB9D4u, ((AudioEntry)rows[0].Tag!).Id);
    }

    [Fact]
    public void PackAndLanguageFilterIndependently()
    {
        AudioEntry french = Entry(0x1EEAB9D4, 4, "french(france)", 56.67);
        AudioEntry english = Entry(0x1EEAB9D4, 1, "english(us)", 12.5);
        AudioEntry music = Entry(0x27F2B, 0, "sfx", 240, "MUS_MEDIA.PC.PCK");

        Assert.Equal(
            [french],
            AudioListBuilder.Build(
                [french, english, music],
                "VO_MEDIA.PC.PCK",
                "french(france)",
                "",
                AudioSort.Length,
                true,
                CancellationToken.None).Select(row => (AudioEntry)row.Tag!));
        Assert.Equal(
            [french, english],
            AudioListBuilder.Build(
                [french, english, music],
                "VO_MEDIA.PC.PCK",
                null,
                "",
                AudioSort.Length,
                true,
                CancellationToken.None).Select(row => (AudioEntry)row.Tag!));
        Assert.Equal(
            [music],
            AudioListBuilder.Build(
                [french, english, music],
                null,
                "sfx",
                "",
                AudioSort.Length,
                true,
                CancellationToken.None).Select(row => (AudioEntry)row.Tag!));
    }

    [Fact]
    public void LengthSortReversesWithoutChangingTheOccurrenceKey()
    {
        AudioEntry shortLine = Entry(1, 4, "french(france)", 2);
        AudioEntry longLine = Entry(2, 4, "french(france)", 56);
        string longKey = BrowserKeys.Audio(longLine);

        List<AssetRow> ascending = AudioListBuilder.Build(
            [longLine, shortLine],
            null,
            "french(france)",
            "",
            AudioSort.Length,
            false,
            CancellationToken.None);
        List<AssetRow> descending = AudioListBuilder.Build(
            [longLine, shortLine],
            null,
            "french(france)",
            "",
            AudioSort.Length,
            true,
            CancellationToken.None);

        Assert.Equal(shortLine, ascending[0].Tag);
        Assert.Equal(longLine, descending[0].Tag);
        Assert.Equal(longKey, descending[0].Key);
    }

    [Fact]
    public void SearchMatchesLanguageAndId()
    {
        AudioEntry french = Entry(0x1EEAB9D4, 4, "french(france)", 56.67);

        Assert.Single(AudioListBuilder.Build(
            [french],
            null,
            null,
            "french",
            AudioSort.Pack,
            false,
            CancellationToken.None));
        Assert.Single(AudioListBuilder.Build(
            [french],
            null,
            null,
            "1eeab9d4",
            AudioSort.Pack,
            false,
            CancellationToken.None));
    }

    private static AudioEntry Entry(
        uint id,
        uint languageId,
        string language,
        double seconds,
        string packName = "VO_MEDIA.PC.PCK",
        string? pathName = null)
    {
        string name = pathName ?? packName;
        return new AudioEntry(
            $@"C:\game\SOUNDBANKS\{name}",
            packName,
            0,
            id,
            id,
            1000,
            languageId,
            language,
            new WemInfo(0xFFFF, 1, 48000, (uint)(seconds * 48000), null),
            null);
    }
}
