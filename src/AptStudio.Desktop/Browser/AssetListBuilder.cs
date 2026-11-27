using System.Globalization;
using AptStudio.Core.Assets;
using AptStudio.Core.Skeletons;

namespace AptStudio.Desktop.Browser;

internal static class AssetListBuilder
{
    public static List<AssetRow> Animations(
        IReadOnlyList<AssetEntry> assets,
        IReadOnlyDictionary<ulong, int> occurrences,
        string archivePath,
        string query,
        CancellationToken cancellationToken = default) =>
        Build(
            assets,
            AssetClass.Animation,
            archivePath,
            query,
            entry =>
            [
                $"0x{entry.NameHash:X16}",
                Path.GetFileName(entry.ArchivePath),
                occurrences.GetValueOrDefault(entry.NameHash)
                    .ToString("N0", CultureInfo.InvariantCulture),
            ],
            entry => $"{entry.ArchivePath}|{entry.BodyArchivePath}|" +
                $"{entry.Row.BodyOffset}|{entry.NameHash:X16}",
            cancellationToken);

    public static List<AssetRow> Meshes(
        IReadOnlyList<AssetEntry> assets,
        string archivePath,
        string query,
        CancellationToken cancellationToken = default) =>
        Build(
            assets,
            AssetClass.Mesh,
            archivePath,
            query,
            entry =>
            [
                $"0x{entry.NameHash:X16}",
                Path.GetFileName(entry.ArchivePath),
            ],
            BrowserKeys.Mesh,
            cancellationToken);

    public static List<AssetRow> Skins(
        IReadOnlyList<AssetEntry> assets,
        string archivePath,
        string query,
        CancellationToken cancellationToken = default) =>
        Build(
            assets,
            AssetClass.Skin,
            archivePath,
            query,
            entry =>
            [
                $"0x{entry.NameHash:X16}",
                Path.GetFileName(entry.ArchivePath),
            ],
            BrowserKeys.Skin,
            cancellationToken);

    public static List<AssetRow> Skeletons(
        IReadOnlyList<SkeletonEntry> entries,
        string archivePath,
        string query,
        CancellationToken cancellationToken = default)
    {
        List<AssetRow> rows = new();
        bool allArchives = archivePath.Length == 0;
        string needle = Needle(query);
        HashSet<string> seen = new(StringComparer.Ordinal);
        int scanned = 0;
        foreach (SkeletonEntry entry in entries)
        {
            if ((++scanned & 1023) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (!allArchives
                && !entry.ArchivePath.Equals(archivePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!seen.Add(BrowserKeys.Skeleton(entry)))
            {
                continue;
            }

            if (needle.Length > 0 && !Matches(entry, needle))
            {
                continue;
            }

            rows.Add(new AssetRow(
                BrowserKeys.Skeleton(entry),
                [
                    $"0x{entry.NameHash:X16}",
                    Path.GetFileName(entry.ArchivePath),
                ],
                entry));
        }

        rows.Sort((left, right) =>
        {
            SkeletonEntry a = (SkeletonEntry)left.Tag!;
            SkeletonEntry b = (SkeletonEntry)right.Tag!;
            int order = a.NameHash.CompareTo(b.NameHash);
            if (order == 0)
            {
                order = string.Compare(
                    a.ArchivePath, b.ArchivePath, StringComparison.OrdinalIgnoreCase);
            }

            return order == 0 ? a.Offset.CompareTo(b.Offset) : order;
        });
        cancellationToken.ThrowIfCancellationRequested();
        return rows;
    }

    private static List<AssetRow> Build(
        IReadOnlyList<AssetEntry> assets,
        ulong classHash,
        string archivePath,
        string query,
        Func<AssetEntry, string[]> cells,
        Func<AssetEntry, string> key,
        CancellationToken cancellationToken)
    {
        List<AssetRow> rows = new();
        bool allArchives = archivePath.Length == 0;
        string needle = Needle(query);
        HashSet<string> seen = new(StringComparer.Ordinal);
        int scanned = 0;
        foreach (AssetEntry entry in assets)
        {
            if ((++scanned & 1023) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (entry.ClassHash != classHash)
            {
                continue;
            }

            if (!allArchives
                && !entry.ArchivePath.Equals(archivePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!seen.Add(key(entry)))
            {
                continue;
            }

            if (needle.Length > 0 && !Matches(entry, needle))
            {
                continue;
            }

            rows.Add(new AssetRow(key(entry), cells(entry), entry));
        }

        rows.Sort((left, right) =>
        {
            AssetEntry a = (AssetEntry)left.Tag!;
            AssetEntry b = (AssetEntry)right.Tag!;
            int order = a.NameHash.CompareTo(b.NameHash);
            if (order == 0)
            {
                order = string.Compare(
                    a.ArchivePath, b.ArchivePath, StringComparison.OrdinalIgnoreCase);
            }

            return order == 0 ? a.Row.BodyOffset.CompareTo(b.Row.BodyOffset) : order;
        });
        cancellationToken.ThrowIfCancellationRequested();
        return rows;
    }

    private static string Needle(string query)
    {
        string trimmed = query.Trim();
        return trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? trimmed[2..]
            : trimmed;
    }

    private static bool Matches(AssetEntry entry, string needle) =>
        entry.NameHash.ToString("X16", CultureInfo.InvariantCulture)
                .Contains(needle, StringComparison.OrdinalIgnoreCase)
            || entry.ArchivePath.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static bool Matches(SkeletonEntry entry, string needle) =>
        entry.NameHash.ToString("X16", CultureInfo.InvariantCulture)
                .Contains(needle, StringComparison.OrdinalIgnoreCase)
            || entry.ArchivePath.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
