using AptStudio.Core.Materials;
using AptStudio.Core.Textures;

namespace AptStudio.Core.Links;

public sealed record MaterialTextureUse(
    MaterialEntry Material,
    string Code,
    IReadOnlyList<int> Slots)
{
    public ulong NameHash => Material.NameHash;

    public string ArchiveName => Path.GetFileName(Material.ArchivePath);
}

public sealed record MaterialTextureIndex(
    IReadOnlyDictionary<ulong, IReadOnlyList<MaterialTextureUse>> ByTexture,
    IReadOnlyList<CatalogFailure> Failures,
    int Materials,
    int Slots)
{
    public static MaterialTextureIndex Load(
        MaterialCatalog catalog,
        IProgress<CatalogProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return Build(catalog.Entries, progress, cancellationToken);
    }

    public static MaterialTextureIndex Build(
        IReadOnlyList<MaterialEntry> entries,
        IProgress<CatalogProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        List<CatalogFailure> failures = new();
        Dictionary<ulong, Dictionary<MaterialEntry, List<int>>> found = new();
        Dictionary<MaterialEntry, string> codes = new();
        Dictionary<string, FileStream> streams = new(StringComparer.OrdinalIgnoreCase);
        int materials = 0;
        int slots = 0;
        try
        {
            IReadOnlyList<IGrouping<string, MaterialEntry>> archives = entries
                .GroupBy(entry => entry.ArchivePath, StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
            for (int index = 0; index < archives.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IGrouping<string, MaterialEntry> archive = archives[index];
                progress?.Report(new CatalogProgress(
                    index + 1, archives.Count, Path.GetFileName(archive.Key)));
                try
                {
                    FileStream stream = new(
                        archive.Key, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    streams.Add(archive.Key, stream);
                    foreach (MaterialEntry entry in archive)
                    {
                        MaterialData data = MaterialDecoder.Read(stream, archive.Key, entry.Hit);
                        materials++;
                        codes.TryAdd(entry, data.Code);
                        foreach (MaterialSlot slot in data.Slots)
                        {
                            slots++;
                            if (!found.TryGetValue(
                                slot.TextureHash,
                                out Dictionary<MaterialEntry, List<int>>? byMaterial))
                            {
                                byMaterial = new Dictionary<MaterialEntry, List<int>>();
                                found.Add(slot.TextureHash, byMaterial);
                            }

                            if (!byMaterial.TryGetValue(entry, out List<int>? slotList))
                            {
                                slotList = new List<int>();
                                byMaterial.Add(entry, slotList);
                            }

                            slotList.Add(slot.Index);
                        }
                    }
                }
                catch (Exception ex) when (ex is InvalidDataException or IOException
                    or UnauthorizedAccessException)
                {
                    failures.Add(new CatalogFailure(archive.Key, ex.Message));
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

        Dictionary<ulong, IReadOnlyList<MaterialTextureUse>> textureIndex = found.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<MaterialTextureUse>)pair.Value
                .Select(item => new MaterialTextureUse(
                    item.Key,
                    codes[item.Key],
                    (IReadOnlyList<int>)item.Value.OrderBy(slot => slot).ToList()))
                .OrderBy(use => use.NameHash)
                .ThenBy(use => use.ArchiveName, StringComparer.OrdinalIgnoreCase)
                .ToList());
        return new MaterialTextureIndex(textureIndex, failures, materials, slots);
    }
}
