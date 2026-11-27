using System.Globalization;
using AptStudio.Core.Audio;

namespace AptStudio.Desktop.Browser;

internal static class AudioText
{
    public static string ShortLanguage(string language)
    {
        int parenthesis = language.IndexOf('(');
        return parenthesis > 0 ? language[..parenthesis] : language;
    }

    public static string Time(double seconds)
    {
        if (seconds <= 0)
        {
            return "0:00";
        }

        TimeSpan value = TimeSpan.FromSeconds(seconds);
        if (value.TotalHours >= 1)
        {
            return value.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture);
        }

        return value.TotalMinutes >= 1
            ? value.ToString(@"m\:ss", CultureInfo.InvariantCulture)
            : value.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + "s";
    }

    public static string Rate(uint sampleRate) =>
        (sampleRate / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + " kHz";

    public static string ExportName(AudioEntry media)
    {
        string stem = media.PackName;
        foreach (string suffix in new[] { ".PC.PCK", ".PCK", ".PC" })
        {
            if (stem.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                stem = stem[..^suffix.Length];
                break;
            }
        }

        string name = media.Name
            ?? $"{stem}_{media.Id:X8}_{ShortLanguage(media.Language)}";
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name + ".ogg";
    }

    public static string SortLabel(AudioSort sort, bool descending)
    {
        string label = sort switch
        {
            AudioSort.Id => "id order",
            AudioSort.Name => "name order",
            AudioSort.Length => "length order",
            AudioSort.Channels => "channel order",
            AudioSort.Rate => "rate order",
            AudioSort.Language => "language order",
            AudioSort.Bank => "bank order",
            _ => "pack order",
        };
        return descending ? $"{label}, descending" : label;
    }
}
