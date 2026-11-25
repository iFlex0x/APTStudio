using AptStudio.Core.Containers;
using AptStudio.Core.Games;
using AptStudio.Core.Textures;

namespace AptStudio.Core.Assets;

public sealed record AssetArchive(
    string Path,
    bool IsExternal,
    int EntryCount,
    int BitmapCount);

public sealed record AssetClassCount(ulong Hash, string Name, int Count);

public sealed record AssetCatalog(
    IReadOnlyList<AssetArchive> Archives,
    IReadOnlyList<AssetEntry> Entries,
    IReadOnlyList<CatalogFailure> Failures,
    IReadOnlyList<AssetClassCount> Classes,
    int FileCount)
{
    public int BitmapCount { get; } = Entries.Count(entry => entry.IsBitmap);

    public int PreviewableCount { get; } = Entries.Count(entry => entry.IsPreviewable);

    public int BodyErrorCount { get; } = Entries.Count(entry => entry.BodyError is not null);

    public static AssetCatalog Load(
        GameProfile profile,
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
        cancellationToken.ThrowIfCancellationRequested();

        string commonPath = Path.Combine(profile.Root, "DATAS", "COMMON.DPC");
        using FileStream? common = File.Exists(commonPath) ? OpenRead(commonPath) : null;

        List<AssetArchive> archives = new(files.Count);
        List<AssetEntry> entries = new();
        Dictionary<ulong, int> classCounts = new();
        Dictionary<(string Path, uint Offset), BitmapProbe> probes = new();

        for (int i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string file = files[i];
            progress?.Report(new CatalogProgress(i + 1, files.Count, file));
            try
            {
                DpcArchive archive = DpcArchive.Open(file);
                bool external = archive.BodyLocation == BodyLocation.External;
                string? bodyPath = external ? common is null ? null : commonPath : file;
                using FileStream? localBody = external ? null : OpenRead(file);
                FileStream? bodyStream = localBody ?? common;
                int bitmapCount = 0;

                AddRows(archive.Entries, "row");
                AddRows(archive.SecondaryEntries, "resources2 row");

                archives.Add(new AssetArchive(
                    file,
                    external,
                    archive.Entries.Count + archive.SecondaryEntries.Count,
                    bitmapCount));

                void AddRows(IReadOnlyList<DpcEntry> rows, string label)
                {
                    for (int index = 0; index < rows.Count; index++)
                    {
                        DpcEntry row = rows[index];
                        BitmapHead? bitmap = null;
                        string? bodyError = null;
                        if (row.ClassHash == AssetClass.Bitmap)
                        {
                            bitmapCount++;
                            string rowSource =
                                $"{Path.GetFileName(file)} {label} {index} name 0x{row.NameHash:X16}";
                            if (bodyPath is null || bodyStream is null)
                            {
                                bodyError = $"{rowSource}: COMMON.DPC was not found.";
                            }
                            else
                            {
                                (string Path, uint Offset) key = (bodyPath, row.Offset);
                                BitmapProbe probe;
                                if (probes.TryGetValue(key, out BitmapProbe? cached))
                                {
                                    probe = cached;
                                }
                                else
                                {
                                    probe = ReadBitmap(bodyStream, row, rowSource);
                                    probes[key] = probe;
                                }

                                if (probe.Head is not null && probe.Head.NameHash != row.NameHash)
                                {
                                    bodyError =
                                        $"{rowSource}: body name 0x{probe.Head.NameHash:X16} != row name 0x{row.NameHash:X16}.";
                                }
                                else
                                {
                                    bitmap = probe.Head;
                                    bodyError = probe.Error;
                                }
                            }
                        }

                        entries.Add(new AssetEntry(
                            file,
                            bodyPath ?? file,
                            external,
                            row,
                            bitmap,
                            bodyError));
                        classCounts[row.ClassHash] = classCounts.GetValueOrDefault(row.ClassHash) + 1;
                    }
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                failures.Add(new CatalogFailure(file, ex.Message));
            }
        }

        List<AssetClassCount> classes = classCounts
            .Select(pair => new AssetClassCount(pair.Key, AssetClass.Name(pair.Key), pair.Value))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ToList();

        return new AssetCatalog(archives, entries, failures, classes, files.Count);
    }

    private static BitmapProbe ReadBitmap(FileStream stream, DpcEntry row, string source)
    {
        if (row.CompressedSize < BitmapHead.HeaderSize)
        {
            return new BitmapProbe(
                null,
                $"{source}: body is {row.CompressedSize} bytes, need {BitmapHead.HeaderSize}.");
        }

        try
        {
            byte[] header = new byte[BitmapHead.HeaderSize];
            stream.Seek(row.BodyOffset, SeekOrigin.Begin);
            stream.ReadExactly(header);
            BitmapHead head = BitmapHead.Parse(header, source);
            if (head.NameHash != row.NameHash)
            {
                return new BitmapProbe(
                    null,
                    $"{source}: body name 0x{head.NameHash:X16} != row name 0x{row.NameHash:X16}.");
            }

            long payloadEnd = row.BodyOffset + head.PayloadOffset
                + (head.Storage == BitmapStorage.Raw ? head.BodySize : head.CompressedSize);
            long rowEnd = row.BodyOffset + row.CompressedSize;
            if (payloadEnd != rowEnd)
            {
                return new BitmapProbe(
                    null,
                    $"{source}: payload ends at {payloadEnd}, row ends at {rowEnd}.");
            }

            return new BitmapProbe(head, null);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return new BitmapProbe(null, ex.Message);
        }
    }

    private static FileStream OpenRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

    private sealed record BitmapProbe(BitmapHead? Head, string? Error);
}
