using AptStudio.Core.Assets;
using AptStudio.Core.Skins;
using AptStudio.Core.Textures;

namespace AptStudio.Core.Links;

public sealed record SkinSkeletonUse(AssetEntry Row, ulong SkeletonRoot)
{
    public ulong NameHash => Row.NameHash;

    public string ArchiveName => Path.GetFileName(Row.ArchivePath);

    public bool HasSkeleton => SkeletonRoot != 0;
}

public sealed record SkinSkeletonIndex(
    IReadOnlyList<SkinSkeletonUse> Entries,
    IReadOnlyDictionary<ulong, IReadOnlyList<SkinSkeletonUse>> BySkeleton,
    IReadOnlyList<CatalogFailure> Failures,
    int NoSkeleton)
{
    public int BoundCount { get; } = Entries.Count(entry => entry.HasSkeleton);

    public static SkinSkeletonIndex Load(
        AssetCatalog catalog,
        IProgress<CatalogProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return Load(catalog.Entries, progress, cancellationToken);
    }

    public static SkinSkeletonIndex Load(
        IEnumerable<AssetEntry> entries,
        IProgress<CatalogProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        List<AssetEntry> rows = entries
            .Where(entry => entry.ClassHash == SkinHead.SkinClass)
            .GroupBy(entry => (entry.BodyArchivePath, entry.Row.BodyOffset))
            .Select(group => group.OrderBy(entry => entry.ArchivePath, StringComparer.OrdinalIgnoreCase)
                .First())
            .ToList();
        return Build(rows, progress, cancellationToken);
    }

    public static SkinSkeletonIndex Build(
        IReadOnlyList<AssetEntry> rows,
        IProgress<CatalogProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        List<SkinSkeletonUse> entries = new(rows.Count);
        List<CatalogFailure> failures = new();
        Dictionary<string, FileStream> streams = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<ulong, List<SkinSkeletonUse>> bySkeleton = new();
        int noSkeleton = 0;
        try
        {
            for (int index = 0; index < rows.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AssetEntry row = rows[index];
                progress?.Report(new CatalogProgress(
                    index + 1, rows.Count, Path.GetFileName(row.ArchivePath)));
                try
                {
                    if (!streams.TryGetValue(row.BodyArchivePath, out FileStream? stream))
                    {
                        stream = new FileStream(
                            row.BodyArchivePath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.ReadWrite);
                        streams.Add(row.BodyArchivePath, stream);
                    }

                    SkinBinding binding = SkinBinding.Read(stream, row.BodyArchivePath, row.Row);
                    SkinSkeletonUse use = new(row, binding.SkeletonRoot);
                    entries.Add(use);
                    if (!binding.HasSkeleton)
                    {
                        noSkeleton++;
                        continue;
                    }

                    if (!bySkeleton.TryGetValue(binding.SkeletonRoot, out List<SkinSkeletonUse>? list))
                    {
                        list = new List<SkinSkeletonUse>();
                        bySkeleton.Add(binding.SkeletonRoot, list);
                    }

                    list.Add(use);
                }
                catch (Exception ex) when (ex is InvalidDataException or IOException
                    or UnauthorizedAccessException)
                {
                    failures.Add(new CatalogFailure(row.BodyArchivePath, ex.Message));
                }
            }
        }
        finally
        {
            foreach (FileStream stream in streams.Values)
            {
                stream.Dispose();
            }
        }

        Dictionary<ulong, IReadOnlyList<SkinSkeletonUse>> rootIndex = bySkeleton.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<SkinSkeletonUse>)pair.Value
                .OrderBy(use => use.NameHash)
                .ThenBy(use => use.ArchiveName, StringComparer.OrdinalIgnoreCase)
                .ToList());
        return new SkinSkeletonIndex(entries, rootIndex, failures, noSkeleton);
    }
}
