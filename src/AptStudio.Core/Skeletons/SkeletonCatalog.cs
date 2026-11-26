using AptStudio.Core.Games;
using AptStudio.Core.Textures;

namespace AptStudio.Core.Skeletons;

public sealed record SkeletonEntry(string ArchivePath, SkeletonHit Hit)
{
    public ulong NameHash => Hit.NameHash;

    public ulong LinkHash => Hit.LinkHash;

    public long Offset => Hit.Offset;

    public uint TotalSize => Hit.TotalSize;
}

public sealed record SkeletonArchive(string Path, int Count);

public sealed record SkeletonCatalog(
    IReadOnlyList<SkeletonEntry> Entries,
    IReadOnlyList<SkeletonArchive> Archives,
    IReadOnlyList<CatalogFailure> Failures,
    int Scanned,
    int Cached)
{
    public int UniqueNameCount { get; } = Entries.Select(entry => entry.NameHash).Distinct().Count();

    public SkeletonEntry? Find(ulong nameHash, string? preferArchive = null)
    {
        if (preferArchive is not null)
        {
            foreach (SkeletonEntry entry in Entries)
            {
                if (entry.NameHash == nameHash
                    && string.Equals(
                        entry.ArchivePath, preferArchive, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }
        }

        foreach (SkeletonEntry entry in Entries)
        {
            if (entry.NameHash == nameHash)
            {
                return entry;
            }
        }

        return null;
    }

    public static SkeletonCatalog Load(
        GameProfile profile,
        bool rebuild = false,
        IProgress<CatalogProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        List<string> files = new();
        List<CatalogFailure> failures = new();
        foreach (string root in profile.AssetRoots)
        {
            string rootPath = Path.Combine(profile.Root, root);
            if (!Directory.Exists(rootPath))
            {
                continue;
            }

            try
            {
                files.AddRange(Directory.EnumerateFiles(
                    rootPath, "*.DPC", SearchOption.AllDirectories));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failures.Add(new CatalogFailure(rootPath, ex.Message));
            }
        }

        files.Sort(StringComparer.OrdinalIgnoreCase);
        List<SkeletonEntry> entries = new();
        List<SkeletonArchive> archives = new();
        int scanned = 0;
        int cached = 0;
        for (int index = 0; index < files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string file = files[index];
            progress?.Report(new CatalogProgress(index + 1, files.Count, file));
            try
            {
                IReadOnlyList<SkeletonHit> hits = SkeletonCache.LoadOrScan(
                    file, rebuild, out bool fromCache, cancellationToken: cancellationToken);
                if (fromCache)
                {
                    cached++;
                }
                else
                {
                    scanned++;
                }

                if (hits.Count == 0)
                {
                    continue;
                }

                archives.Add(new SkeletonArchive(file, hits.Count));
                foreach (SkeletonHit hit in hits)
                {
                    entries.Add(new SkeletonEntry(file, hit));
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException
                or UnauthorizedAccessException)
            {
                failures.Add(new CatalogFailure(file, ex.Message));
            }
        }

        return new SkeletonCatalog(entries, archives, failures, scanned, cached);
    }
}
