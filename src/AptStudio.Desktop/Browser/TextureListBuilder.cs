using System.Globalization;
using AptStudio.Core.Assets;
using AptStudio.Core.Textures;

namespace AptStudio.Desktop.Browser;

internal static class TextureListBuilder
{
    public static List<AssetRow> Build(
        IReadOnlyList<AssetEntry> assets,
        BrowserState state,
        CancellationToken cancellationToken,
        IProgress<int>? progress = null)
    {
        List<AssetRow> rows = new();
        if (assets.Count == 0)
        {
            return rows;
        }

        bool allArchives = string.IsNullOrEmpty(state.ArchivePath);
        string query = state.Query.Trim();
        string needle = query.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? query[2..]
            : query;
        HashSet<string> seen = new(StringComparer.Ordinal);
        int reported = 0;
        int scanned = 0;
        foreach (AssetEntry entry in assets)
        {
            if ((++scanned & 1023) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (!entry.IsBitmap || entry.Bitmap is null)
            {
                continue;
            }

            if (!allArchives
                && !entry.ArchivePath.Equals(state.ArchivePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!seen.Add(BrowserKeys.Texture(entry)))
            {
                continue;
            }

            BitmapHead head = entry.Bitmap;
            if (!state.Filter.Matches(head))
            {
                continue;
            }

            if (needle.Length > 0 && !Matches(entry, needle))
            {
                continue;
            }

            rows.Add(new AssetRow(
                BrowserKeys.Texture(entry),
                [
                    $"0x{entry.NameHash:X16}",
                    Path.GetFileName(entry.ArchivePath),
                    $"{head.Width} × {head.Height}",
                    head.MipCount.ToString(CultureInfo.InvariantCulture),
                    head.Format.DisplayName,
                    head.Storage.ToString(),
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
            AssetEntry a = (AssetEntry)left.Tag!;
            AssetEntry b = (AssetEntry)right.Tag!;
            int order = state.Sort switch
            {
                TextureSort.Hash => a.NameHash.CompareTo(b.NameHash),
                TextureSort.Size => ((long)a.Bitmap!.Width * a.Bitmap.Height)
                    .CompareTo((long)b.Bitmap!.Width * b.Bitmap.Height),
                TextureSort.Mips => a.Bitmap!.MipCount.CompareTo(b.Bitmap!.MipCount),
                _ => string.Compare(
                    Path.GetFileName(a.ArchivePath),
                    Path.GetFileName(b.ArchivePath),
                    StringComparison.OrdinalIgnoreCase),
            };
            if (order == 0)
            {
                order = string.Compare(
                    a.ArchivePath, b.ArchivePath, StringComparison.OrdinalIgnoreCase);
            }

            if (order == 0)
            {
                order = a.Row.BodyOffset.CompareTo(b.Row.BodyOffset);
            }

            if (order == 0)
            {
                order = a.NameHash.CompareTo(b.NameHash);
            }

            return state.SortDescending ? -order : order;
        });
        cancellationToken.ThrowIfCancellationRequested();
        return rows;
    }

    private static bool Matches(AssetEntry entry, string needle)
    {
        BitmapHead head = entry.Bitmap!;
        return entry.NameHash.ToString("X16", CultureInfo.InvariantCulture)
                .Contains(needle, StringComparison.OrdinalIgnoreCase)
            || entry.ArchivePath.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || $"{head.Width}x{head.Height}".Contains(needle, StringComparison.OrdinalIgnoreCase)
            || $"{head.Width} × {head.Height}".Contains(needle, StringComparison.OrdinalIgnoreCase)
            || head.Format.DisplayName.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || head.Storage.ToString().Contains(needle, StringComparison.OrdinalIgnoreCase);
    }
}
