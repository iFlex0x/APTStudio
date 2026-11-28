// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Core.Assets;
using AptStudio.Core.Skins;
using AptStudio.Core.Textures;

namespace AptStudio.Core.Links;

public sealed record SkinLinkEntry(AssetEntry Row, SkinLinkData Links)
{
    public ulong NameHash => Row.NameHash;

    public string ArchiveName => Path.GetFileName(Row.ArchivePath);
}

public sealed record SkinLinkIndex(
    IReadOnlyList<SkinLinkEntry> Entries,
    IReadOnlyDictionary<ulong, IReadOnlyList<SkinLinkEntry>> ByMesh,
    IReadOnlyList<CatalogFailure> Failures)
{
    public static SkinLinkIndex Load(
        AssetCatalog catalog,
        IProgress<CatalogProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return Load(catalog.Entries, progress, cancellationToken);
    }

    public static SkinLinkIndex Load(
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

    public static SkinLinkIndex Build(
        IReadOnlyList<AssetEntry> rows,
        IProgress<CatalogProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        List<SkinLinkEntry> entries = new();
        List<CatalogFailure> failures = new();
        Dictionary<string, FileStream> streams = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<ulong, List<SkinLinkEntry>> byMesh = new();
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

                    SkinLinkData links = SkinLinkReader.Read(stream, row.BodyArchivePath, row.Row);
                    SkinLinkEntry entry = new(row, links);
                    entries.Add(entry);
                    foreach (ulong mesh in links.MeshLinks.Distinct())
                    {
                        if (!byMesh.TryGetValue(mesh, out List<SkinLinkEntry>? list))
                        {
                            list = new List<SkinLinkEntry>();
                            byMesh.Add(mesh, list);
                        }

                        list.Add(entry);
                    }
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

        Dictionary<ulong, IReadOnlyList<SkinLinkEntry>> meshIndex = byMesh.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<SkinLinkEntry>)pair.Value
                .OrderBy(entry => entry.NameHash)
                .ThenBy(entry => entry.ArchiveName, StringComparer.OrdinalIgnoreCase)
                .ToList());
        return new SkinLinkIndex(entries, meshIndex, failures);
    }
}
