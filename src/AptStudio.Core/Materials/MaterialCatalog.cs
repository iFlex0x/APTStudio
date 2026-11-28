// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Core.Games;
using AptStudio.Core.Textures;

namespace AptStudio.Core.Materials;

public sealed record MaterialEntry(string ArchivePath, MaterialHit Hit)
{
    public ulong NameHash => Hit.NameHash;

    public ulong ClassHash => Hit.ClassHash;

    public ulong LinkHash => Hit.LinkHash;

    public long Offset => Hit.Offset;

    public uint TotalSize => Hit.TotalSize;
}

public sealed record MaterialArchive(string Path, int Count);

public sealed record MaterialCatalog(
    IReadOnlyList<MaterialEntry> Entries,
    IReadOnlyList<MaterialArchive> Archives,
    IReadOnlyList<CatalogFailure> Failures,
    int Scanned,
    int Cached)
{
    public static MaterialCatalog Load(
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
        List<MaterialEntry> entries = new();
        List<MaterialArchive> archives = new();
        int scanned = 0;
        int cached = 0;
        for (int index = 0; index < files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string file = files[index];
            progress?.Report(new CatalogProgress(index + 1, files.Count, file));
            try
            {
                IReadOnlyList<MaterialHit> hits = MaterialCache.LoadOrScan(
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

                archives.Add(new MaterialArchive(file, hits.Count));
                foreach (MaterialHit hit in hits)
                {
                    entries.Add(new MaterialEntry(file, hit));
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException
                or UnauthorizedAccessException)
            {
                failures.Add(new CatalogFailure(file, ex.Message));
            }
        }

        return new MaterialCatalog(entries, archives, failures, scanned, cached);
    }
}
