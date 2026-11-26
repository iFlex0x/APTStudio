using System.Diagnostics;
using System.Globalization;
using AptStudio.Core.Assets;
using AptStudio.Core.Containers;
using AptStudio.Core.Games;
using AptStudio.Core.Meshes;
using AptStudio.Core.Textures;

namespace AptStudio.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 2 && string.Equals(args[0], "scan", StringComparison.OrdinalIgnoreCase))
        {
            return Scan(args[1]);
        }

        if ((args.Length == 2 || args.Length == 3)
            && string.Equals(args[0], "list", StringComparison.OrdinalIgnoreCase))
        {
            bool rebuild = false;
            if (args.Length == 3)
            {
                if (!string.Equals(args[2], "--rebuild", StringComparison.OrdinalIgnoreCase))
                {
                    Usage();
                    return 1;
                }

                rebuild = true;
            }

            return List(args[1], rebuild);
        }

        if (args.Length == 2 && string.Equals(args[0], "assets", StringComparison.OrdinalIgnoreCase))
        {
            return Assets(args[1]);
        }

        if (args.Length == 3 && string.Equals(args[0], "mesh", StringComparison.OrdinalIgnoreCase))
        {
            return Mesh(args[1], args[2]);
        }

        if (args.Length == 4 && string.Equals(args[0], "export", StringComparison.OrdinalIgnoreCase))
        {
            return Export(args[1], args[2], args[3]);
        }

        Usage();
        return 1;
    }

    private static void Usage()
    {
        Console.Error.WriteLine("usage:");
        Console.Error.WriteLine("  aptstudio scan <gameRoot>");
        Console.Error.WriteLine("  aptstudio list <gameRoot> [--rebuild]");
        Console.Error.WriteLine("  aptstudio assets <gameRoot>");
        Console.Error.WriteLine("  aptstudio mesh <gameRoot> <nameHash>");
        Console.Error.WriteLine("  aptstudio export <gameRoot> <nameHash> <outputPath>");
    }

    private static int Assets(string gameRoot)
    {
        if (!GameProfile.TryDetect(gameRoot, out GameProfile? profile, out string? error) || profile is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        Stopwatch watch = Stopwatch.StartNew();
        AssetCatalog catalog;
        try
        {
            catalog = AssetCatalog.Load(profile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        watch.Stop();
        foreach (AssetClassCount group in catalog.Classes)
        {
            Console.WriteLine($"{group.Name,-20} {N(group.Count),8}");
        }

        Console.WriteLine(
            $"assets: {N(catalog.Entries.Count)}, bitmap rows: {N(catalog.BitmapCount)}, previewable rows: {N(catalog.PreviewableCount)}, archives: {N(catalog.Archives.Count)}, files: {N(catalog.FileCount)}, body errors: {N(catalog.BodyErrorCount)}, failed: {N(catalog.Failures.Count)}, {N(watch.ElapsedMilliseconds)} ms");

        foreach (CatalogFailure failure in catalog.Failures)
        {
            Console.Error.WriteLine(failure.Message);
        }

        foreach (AssetEntry entry in catalog.Entries
            .Where(item => item.BodyError is not null)
            .Take(20))
        {
            Console.Error.WriteLine(entry.BodyError);
        }

        return catalog.Failures.Count == 0 && catalog.BodyErrorCount == 0 ? 0 : 2;
    }

    private static int Mesh(string gameRoot, string hashText)
    {
        string text = hashText.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? hashText[2..]
            : hashText;
        if (!ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong nameHash))
        {
            Console.Error.WriteLine($"'{hashText}' is not a 64-bit hex hash.");
            return 1;
        }

        if (!GameProfile.TryDetect(gameRoot, out GameProfile? profile, out string? error) || profile is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        AssetCatalog catalog;
        try
        {
            catalog = AssetCatalog.Load(profile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        List<AssetEntry> matches = catalog.Entries
            .Where(entry => entry.ClassHash == AssetClass.Mesh && entry.NameHash == nameHash)
            .ToList();
        if (matches.Count == 0)
        {
            Console.Error.WriteLine($"no Mesh_Z row named 0x{nameHash:X16}.");
            return 1;
        }

        int failed = 0;
        foreach (AssetEntry entry in matches)
        {
            try
            {
                using FileStream stream = new(
                    entry.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                MeshData mesh = MeshDecoder.Read(stream, entry.BodyArchivePath, entry.Row);
                Console.WriteLine(
                    $"{Path.GetFileName(entry.ArchivePath)} 0x{entry.NameHash:X16} offset {entry.Row.Offset}{(entry.IsExternal ? " body COMMON.DPC" : "")}");
                int groupCount = 0;
                foreach (MeshIndexSet item in mesh.IndexSets)
                {
                    groupCount += item.Groups.Count;
                }

                Console.WriteLine(
                    $"  version {mesh.Head.Version}, {mesh.Head.Storage}, body {N(mesh.Head.BodySize)}, streams {mesh.Streams.Count}, index sets {mesh.IndexSets.Count}, groups {groupCount}");
                for (int index = 0; index < mesh.Streams.Count; index++)
                {
                    MeshStream item = mesh.Streams[index];
                    Console.WriteLine(
                        $"  stream {index}: {N(item.VertexCount)} vertices, stride {item.Stride}, flags 0x{item.Flags:X4}");
                }

                for (int index = 0; index < mesh.IndexSets.Count; index++)
                {
                    MeshIndexSet item = mesh.IndexSets[index];
                    Console.WriteLine(
                        $"  indices {index}: {N(item.IndexCount)} ({N(item.IndexCount / 3)} triangles), u{item.IndexSize * 8}, flags 0x{item.Flags:X4}");
                }

                for (int setIndex = 0; setIndex < mesh.IndexSets.Count; setIndex++)
                {
                    for (int groupIndex = 0; groupIndex < mesh.IndexSets[setIndex].Groups.Count; groupIndex++)
                    {
                        MeshGroup item = mesh.IndexSets[setIndex].Groups[groupIndex];
                        Console.WriteLine(
                            $"  group {setIndex}.{groupIndex}: {N(item.VertexCount)} vertices, {N(item.FaceCount)} triangles, offsets {N(item.VertexOffset)}/{N(item.IndexOffset)}");
                    }
                }

                if (mesh.Streams.Count == 0)
                {
                    Console.WriteLine("  empty mesh (no geometry)");
                    continue;
                }

                MeshGeometry geometry = mesh.ToGeometry();
                Console.WriteLine(
                    $"  bounds min ({F(geometry.Min.X)}, {F(geometry.Min.Y)}, {F(geometry.Min.Z)}) max ({F(geometry.Max.X)}, {F(geometry.Max.Y)}, {F(geometry.Max.Z)})");
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException
                or UnauthorizedAccessException)
            {
                failed++;
                Console.Error.WriteLine($"{Path.GetFileName(entry.ArchivePath)} 0x{entry.NameHash:X16}: {ex.Message}");
            }
        }

        return failed == 0 ? 0 : 2;
    }

    private static string F(float value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private static int Scan(string gameRoot)
    {
        if (!TryFindDpcs(gameRoot, out string[] files))
        {
            return 1;
        }

        Stopwatch watch = Stopwatch.StartNew();
        List<(string Path, DpcArchive Archive)> parsed = new();
        List<string> failures = new();
        foreach (string file in files)
        {
            try
            {
                DpcArchive archive = DpcArchive.Open(file);
                parsed.Add((file, archive));
                Console.WriteLine(
                    $"{Path.GetFileName(file),-28} {archive.Header.Kind,-7} {N(archive.Entries.Count),7} + {N(archive.SecondaryEntries.Count),6} rows  {archive.BodyLocation,-8} {N(archive.Length),14} B");
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                failures.Add(ex.Message);
                Console.WriteLine($"{Path.GetFileName(file),-28} ERROR");
            }
        }

        DpcArchive? common = parsed
            .Where(item => string.Equals(Path.GetFileName(item.Path), "COMMON.DPC", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Archive)
            .FirstOrDefault();

        if (common is not null)
        {
            int external = 0;
            int outside = 0;
            foreach ((string path, DpcArchive archive) in parsed)
            {
                if (archive.BodyLocation != BodyLocation.External)
                {
                    continue;
                }

                foreach (DpcEntry entry in archive.Entries.Concat(archive.SecondaryEntries))
                {
                    external++;
                    if (entry.BodyOffset + entry.CompressedSize > common.Length)
                    {
                        outside++;
                        failures.Add($"{Path.GetFileName(path)}: row name 0x{entry.NameHash:X16} is outside COMMON.");
                    }
                }
            }

            Console.WriteLine(
                $"external extents checked against COMMON: {N(external)}, outside: {N(outside)}");
        }
        else if (parsed.Exists(item => item.Archive.BodyLocation == BodyLocation.External))
        {
            failures.Add("COMMON.DPC not found; external extents were not checked.");
        }

        watch.Stop();
        Console.WriteLine(
            $"files: {N(files.Length)}, parsed: {N(parsed.Count)}, failed: {N(failures.Count)}, {N(watch.ElapsedMilliseconds)} ms");
        foreach (string failure in failures)
        {
            Console.Error.WriteLine(failure);
        }

        return failures.Count == 0 ? 0 : 2;
    }

    private static int List(string gameRoot, bool rebuildCache)
    {
        if (!GameProfile.TryDetect(gameRoot, out GameProfile? profile, out string? error) || profile is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        Stopwatch watch = Stopwatch.StartNew();
        TextureCatalog catalog;
        try
        {
            catalog = TextureCatalog.Load(profile, rebuildCache);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        Dictionary<string, int> byFormat = new(StringComparer.Ordinal);
        int volumes = 0;
        int unknownCodec = 0;
        foreach (CatalogArchive archive in catalog.Archives)
        {
            string name = Path.GetFileName(archive.Path);
            if (archive.IsExternal)
            {
                if (archive.ExternalBitmapReferences > 0)
                {
                    Console.WriteLine(
                        $"{name,-28} {N(archive.ExternalBitmapReferences),7} bitmap refs in another archive (not listed)");
                }

                continue;
            }

            foreach (TextureEntry texture in archive.Textures)
            {
                BitmapHead head = texture.Head;
                string format = head.Format.DisplayName;
                byFormat[format] = byFormat.GetValueOrDefault(format) + 1;
                if (head.Kind == BitmapKind.Volume)
                {
                    volumes++;
                }

                if (head.Format.Codec == TextureCodec.Unknown)
                {
                    unknownCodec++;
                }

                string dimensions = head.Kind == BitmapKind.Volume
                    ? $"volume w={head.Width}"
                    : $"{head.Width}x{head.Height}";
                Console.WriteLine(
                    $"{name,-28} 0x{head.NameHash:X16} {format,-16} {dimensions,-14} {head.MipCount,2} mips {head.Surfaces} surfaces {(head.Storage == BitmapStorage.Raw ? "raw" : "lz4")}");
            }
        }

        watch.Stop();
        Console.WriteLine();
        foreach (CatalogArchive archive in catalog.Archives
            .Where(item => !item.IsExternal && item.TextureCount > 0)
            .OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                $"{Path.GetFileName(archive.Path),-28} {N(archive.TextureCount),7} textures {(archive.FromCache ? "cached" : "scanned")}");
        }

        Console.WriteLine("formats:");
        foreach ((string format, int count) in byFormat.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"  {format,-18} {N(count),7}");
        }

        int cachedArchives = catalog.Archives.Count(
            item => !item.IsExternal && item.TextureCount > 0 && item.FromCache);
        int scannedArchives = catalog.Archives.Count(
            item => !item.IsExternal && item.TextureCount > 0 && !item.FromCache);
        int externalArchives = catalog.Archives.Count(item => item.IsExternal);
        int externalReferences = catalog.Archives.Sum(item => item.ExternalBitmapReferences);
        Console.WriteLine(
            $"textures: {N(catalog.Textures.Count)}, previewable: {N(catalog.PreviewableCount)}, volumes: {N(volumes)}, unknown codec: {N(unknownCodec)}, external archives: {N(externalArchives)}, external bitmap refs: {N(externalReferences)}, cached: {N(cachedArchives)}, scanned: {N(scannedArchives)}, files: {N(catalog.FileCount)}, failed: {N(catalog.Failures.Count)}, {N(watch.ElapsedMilliseconds)} ms");
        foreach (CatalogFailure failure in catalog.Failures)
        {
            Console.Error.WriteLine(failure.Message);
        }

        return catalog.Failures.Count == 0 ? 0 : 2;
    }

    private static int Export(string gameRoot, string hashText, string outputPath)
    {
        string text = hashText.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? hashText[2..]
            : hashText;
        if (!ulong.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong name))
        {
            Console.Error.WriteLine($"invalid texture hash '{hashText}'");
            return 1;
        }

        if (!TryFindDpcs(gameRoot, out string[] files))
        {
            return 1;
        }

        List<TextureEntry> matches = new();
        int failedArchives = 0;
        string? externalMatch = null;
        foreach (string file in files)
        {
            string archiveName = Path.GetFileName(file);
            try
            {
                DpcArchive archive = DpcArchive.Open(file);
                if (archive.BodyLocation != BodyLocation.Local)
                {
                    if (archive.Entries.Concat(archive.SecondaryEntries).Any(entry =>
                        entry.ClassHash == BitmapHead.ClassHash && entry.NameHash == name))
                    {
                        externalMatch ??= archiveName;
                    }

                    continue;
                }

                matches.AddRange(TextureCache.LoadOrScan(archive, false, out _)
                    .Where(texture => texture.Head.NameHash == name));
            }
            catch (InvalidDataException ex)
            {
                failedArchives++;
                Console.Error.WriteLine(ex.Message);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failedArchives++;
                Console.Error.WriteLine($"{archiveName}: {ex.Message}");
            }
        }

        if (matches.Count == 0)
        {
            if (externalMatch is not null)
            {
                Console.Error.WriteLine(
                    $"{externalMatch}: texture 0x{name:X16} resolves into another archive; external bodies are not decoded.");
                return 2;
            }

            Console.Error.WriteLine($"texture 0x{name:X16} not found");
            return failedArchives == 0 ? 1 : 2;
        }

        List<TextureEntry> decodable = matches
            .Where(TextureDecoder.CanDecodeMip0)
            .ToList();
        if (decodable.Count == 0)
        {
            try
            {
                TextureEntry first = matches[0];
                using FileStream stream = new(
                    first.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                TextureDecoder.DecodeMip0(stream, first);
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException
                or IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine(ex.Message);
            }

            return 2;
        }

        TextureEntry chosen = decodable[0];
        if (matches.Count > 1)
        {
            Console.WriteLine(
                $"note: {matches.Count} rows share 0x{name:X16}; exporting {Path.GetFileName(chosen.ArchivePath)} {chosen.Head.Format.DisplayName} {chosen.Head.Width}x{chosen.Head.Height}");
        }

        try
        {
            DecodedTexture image = TextureExport.ExportPng(chosen, outputPath);
            long bytes = new FileInfo(Path.GetFullPath(outputPath)).Length;
            Console.WriteLine(
                $"{Path.GetFileName(chosen.ArchivePath)} 0x{name:X16} {image.Width}x{image.Height} {chosen.Head.Format.DisplayName} {N(bytes)} B -> {outputPath}");
            return 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException or NotSupportedException)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
    }

    private static string N(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static bool TryFindDpcs(string gameRoot, out string[] files)
    {
        files = Array.Empty<string>();
        if (!GameProfile.TryDetect(gameRoot, out GameProfile? profile, out string? error) || profile is null)
        {
            Console.Error.WriteLine(error);
            return false;
        }

        try
        {
            files = profile.DataRoots
                .SelectMany(root => Directory.EnumerateFiles(
                    Path.Combine(profile.Root, root),
                    "*.DPC",
                    SearchOption.AllDirectories))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
            return false;
        }
    }
}
