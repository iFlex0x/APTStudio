using AptStudio.Core.Containers;
using AptStudio.Core.Games;

namespace AptStudio.Core.Textures;

public sealed record CatalogArchive(
    string Path,
    bool IsExternal,
    int TextureCount,
    bool FromCache,
    int ExternalBitmapRows);

public sealed record CatalogProgress(int Index, int Total, string ArchivePath);

public sealed record TextureCatalog(
    IReadOnlyList<CatalogArchive> Archives,
    IReadOnlyList<TextureEntry> Textures,
    IReadOnlyList<string> Failures)
{
    public int PreviewableCount => Textures.Count(TextureDecoder.CanDecodeMip0);

    public int FileCount => Archives.Count + Failures.Count;

    public static TextureCatalog Load(
        GameProfile profile,
        bool rebuildCache = false,
        string? cacheRoot = null,
        IProgress<CatalogProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        string[] files = profile.DataRoots
            .SelectMany(root => Directory.EnumerateFiles(
                Path.Combine(profile.Root, root),
                "*.DPC",
                SearchOption.TopDirectoryOnly))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        List<CatalogArchive> archives = new(files.Length);
        List<TextureEntry> textures = new();
        List<string> failures = new();
        for (int i = 0; i < files.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string file = files[i];
            progress?.Report(new CatalogProgress(i + 1, files.Length, file));
            try
            {
                DpcArchive archive = DpcArchive.Open(file);
                if (archive.BodyLocation != BodyLocation.Local)
                {
                    int externalRows = archive.Entries.Count(
                        entry => entry.ClassHash == BitmapHead.ClassHash);
                    archives.Add(new CatalogArchive(file, true, 0, false, externalRows));
                    continue;
                }

                IReadOnlyList<TextureEntry> found = TextureCache.LoadOrScan(
                    archive, rebuildCache, out bool fromCache, cacheRoot);
                archives.Add(new CatalogArchive(file, false, found.Count, fromCache, 0));
                textures.AddRange(found);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                failures.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        return new TextureCatalog(archives, textures, failures);
    }
}
