using System.Globalization;
using AptStudio.Core.Audio;

namespace AptStudio.Desktop.Browser;

internal static class AudioListBuilder
{
    public static List<AssetRow> Build(
        IReadOnlyList<AudioEntry> entries,
        string? pack,
        string? language,
        string query,
        AudioSort sort,
        bool descending,
        CancellationToken cancellationToken,
        IProgress<int>? progress = null)
    {
        List<AssetRow> rows = new();
        if (entries.Count == 0)
        {
            return rows;
        }

        string needle = query.Trim();
        if (needle.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            needle = needle[2..];
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        int reported = 0;
        int scanned = 0;
        foreach (AudioEntry entry in entries)
        {
            if ((++scanned & 2047) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (pack is not null && !entry.PackName.Equals(pack, StringComparison.Ordinal))
            {
                continue;
            }

            if (language is not null
                && !entry.Language.Equals(language, StringComparison.Ordinal))
            {
                continue;
            }

            if (!seen.Add(BrowserKeys.Audio(entry)))
            {
                continue;
            }

            if (needle.Length > 0 && !Matches(entry, needle))
            {
                continue;
            }

            rows.Add(new AssetRow(
                BrowserKeys.Audio(entry),
                [
                    entry.PackName,
                    $"0x{entry.Id:X8}",
                    entry.Name ?? "",
                    AudioText.ShortLanguage(entry.Language),
                    AudioText.Time(entry.DurationSeconds),
                    entry.Info.Channels.ToString(CultureInfo.InvariantCulture),
                    AudioText.Rate(entry.Info.SampleRate),
                    entry.BankId == 0 ? "-" : $"0x{entry.BankId:X8}",
                ],
                entry));
            if (progress is not null
                && (rows.Count == 1 || rows.Count - reported >= 10_000))
            {
                reported = rows.Count;
                progress.Report(rows.Count);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        rows.Sort((left, right) =>
        {
            AudioEntry a = (AudioEntry)left.Tag!;
            AudioEntry b = (AudioEntry)right.Tag!;
            int order = sort switch
            {
                AudioSort.Id => a.Id.CompareTo(b.Id),
                AudioSort.Name => string.Compare(
                    a.Name ?? "", b.Name ?? "", StringComparison.OrdinalIgnoreCase),
                AudioSort.Length => a.DurationSeconds.CompareTo(b.DurationSeconds),
                AudioSort.Channels => a.Info.Channels.CompareTo(b.Info.Channels),
                AudioSort.Rate => a.Info.SampleRate.CompareTo(b.Info.SampleRate),
                AudioSort.Language => string.Compare(
                    a.Language, b.Language, StringComparison.OrdinalIgnoreCase),
                AudioSort.Bank => a.BankId.CompareTo(b.BankId),
                _ => string.Compare(
                    a.PackName, b.PackName, StringComparison.OrdinalIgnoreCase),
            };
            if (order == 0)
            {
                order = string.Compare(
                    a.PackPath, b.PackPath, StringComparison.OrdinalIgnoreCase);
            }

            if (order == 0)
            {
                order = a.LanguageId.CompareTo(b.LanguageId);
            }

            if (order == 0)
            {
                order = a.Offset.CompareTo(b.Offset);
            }

            if (order == 0)
            {
                order = a.Size.CompareTo(b.Size);
            }

            if (order == 0)
            {
                order = a.BankId.CompareTo(b.BankId);
            }

            if (order == 0)
            {
                order = a.Id.CompareTo(b.Id);
            }

            return descending ? -order : order;
        });
        cancellationToken.ThrowIfCancellationRequested();
        return rows;
    }

    private static bool Matches(AudioEntry entry, string query) =>
        entry.Id.ToString("X8", CultureInfo.InvariantCulture)
            .Contains(query, StringComparison.OrdinalIgnoreCase)
        || (entry.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
        || entry.PackName.Contains(query, StringComparison.OrdinalIgnoreCase)
        || entry.Language.Contains(query, StringComparison.OrdinalIgnoreCase)
        || entry.Info.Codec.Contains(query, StringComparison.OrdinalIgnoreCase);
}
