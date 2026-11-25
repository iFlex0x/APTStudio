using AptStudio.Core.Containers;
using AptStudio.Core.Games;

namespace AptStudio.Core.Textures;

public sealed record CatalogArchive(
    string Path,
    bool IsExternal,
    IReadOnlyList<TextureEntry> Textures,
    bool FromCache,
    int ExternalBitmapReferences)
{
    public int TextureCount => Textures.Count;
}

public sealed record CatalogFailure(string Path, string Message);

public sealed record CatalogProgress(int Index, int Total, string ArchivePath);

public sealed record TextureCatalog(
    IReadOnlyList<CatalogArchive> Archives,
    IReadOnlyList<TextureEntry> Textures,
    IReadOnlyList<CatalogFailure> Failures,
    int FileCount)
{
    public int PreviewableCount => Textures.Count(TextureDecoder.CanDecodeMip0);

    public static TextureCatalog Load(
        GameProfile profile,
        bool rebuildCache = false,
        string? cacheRoot = null,
        IProgress<CatalogProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        List<string> files = new();
        List<CatalogFailure> failures = new();
        foreach (string root in profile.DataRoots)
        {
            string rootPath = Path.Combine(profile.Root, root);
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
        List<CatalogArchive> archives = new(files.Count);
        List<TextureEntry> textures = new();
        for (int i = 0; i < files.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string file = files[i];
            progress?.Report(new CatalogProgress(i + 1, files.Count, file));
            try
            {
                DpcArchive archive = DpcArchive.Open(file);
                if (archive.BodyLocation != BodyLocation.Local)
                {
                    int externalRows = archive.Entries.Count(
                        entry => entry.ClassHash == BitmapHead.ClassHash);
                    externalRows += archive.SecondaryEntries.Count(
                        entry => entry.ClassHash == BitmapHead.ClassHash);
                    archives.Add(new CatalogArchive(
                        file, true, Array.Empty<TextureEntry>(), false, externalRows));
                    continue;
                }

                IReadOnlyList<TextureEntry> found = TextureCache.LoadOrScan(
                    archive, rebuildCache, out bool fromCache, cacheRoot);
                archives.Add(new CatalogArchive(file, false, found, fromCache, 0));
                textures.AddRange(found);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                failures.Add(new CatalogFailure(file, ex.Message));
            }
        }

        return new TextureCatalog(archives, textures, failures, files.Count);
    }
}
