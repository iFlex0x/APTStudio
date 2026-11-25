using System.Diagnostics;
using System.Globalization;
using AptStudio.Core.Containers;
using AptStudio.Core.Games;
using AptStudio.Core.Images;
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
        Console.Error.WriteLine("  aptstudio export <gameRoot> <nameHash> <outputPath>");
    }

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
                    $"{Path.GetFileName(file),-28} {archive.Header.Kind,-7} {N(archive.Entries.Count),7} rows  {archive.BodyLocation,-8} {N(archive.Length),14} B");
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

                foreach (DpcEntry entry in archive.Entries)
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
        if (!TryFindDpcs(gameRoot, out string[] files))
        {
            return 1;
        }

        Stopwatch watch = Stopwatch.StartNew();
        List<string> failures = new();
        Dictionary<string, (int Count, bool Cached)> byArchive = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> byFormat = new(StringComparer.Ordinal);
        int total = 0;
        int volumes = 0;
        int unknownCodec = 0;
        int skippedExternal = 0;
        int externalBitmaps = 0;
        int cachedArchives = 0;
        int scannedArchives = 0;

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            try
            {
                DpcArchive archive = DpcArchive.Open(file);
                if (archive.BodyLocation != BodyLocation.Local)
                {
                    int rows = archive.Entries.Count(
                        entry => entry.ClassHash == BitmapHead.ClassHash);
                    skippedExternal++;
                    externalBitmaps += rows;
                    if (rows > 0)
                    {
                        Console.WriteLine(
                            $"{name,-28} {N(rows),7} bitmap rows in another archive (not listed)");
                    }

                    continue;
                }

                IReadOnlyList<TextureEntry> textures = TextureCache.LoadOrScan(
                    file, rebuildCache, out bool fromCache);
                if (fromCache)
                {
                    cachedArchives++;
                }
                else
                {
                    scannedArchives++;
                }

                if (textures.Count == 0)
                {
                    continue;
                }

                byArchive[file] = (textures.Count, fromCache);
                foreach (TextureEntry texture in textures)
                {
                    BitmapHead head = texture.Head;
                    total++;
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
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                failures.Add(ex.Message);
                Console.WriteLine($"{name,-28} ERROR");
            }
        }

        watch.Stop();
        Console.WriteLine();
        foreach ((string path, (int count, bool cached)) in byArchive.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                $"{Path.GetFileName(path),-28} {N(count),7} textures {(cached ? "cached" : "scanned")}");
        }

        Console.WriteLine("formats:");
        foreach ((string format, int count) in byFormat.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"  {format,-18} {N(count),7}");
        }

        Console.WriteLine(
            $"textures: {N(total)}, volumes: {N(volumes)}, unknown codec: {N(unknownCodec)}, external archives: {N(skippedExternal)}, external bitmap rows: {N(externalBitmaps)}, cached: {N(cachedArchives)}, scanned: {N(scannedArchives)}, files: {N(files.Length)}, failed: {N(failures.Count)}, {N(watch.ElapsedMilliseconds)} ms");
        foreach (string failure in failures)
        {
            Console.Error.WriteLine(failure);
        }

        return failures.Count == 0 ? 0 : 2;
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
                    if (archive.Entries.Any(entry =>
                        entry.ClassHash == BitmapHead.ClassHash && entry.NameHash == name))
                    {
                        externalMatch ??= archiveName;
                    }

                    continue;
                }

                matches.AddRange(TextureCache.LoadOrScan(file, false, out _)
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
            using FileStream stream = new(
                chosen.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            DecodedTexture image = TextureDecoder.DecodeMip0(stream, chosen);
            byte[] png = PngWriter.EncodeRgba(image.Rgba, image.Width, image.Height);
            string? directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(outputPath, png);
            Console.WriteLine(
                $"{Path.GetFileName(chosen.ArchivePath)} 0x{name:X16} {image.Width}x{image.Height} {chosen.Head.Format.DisplayName} {N(png.Length)} B -> {outputPath}");
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
                    SearchOption.TopDirectoryOnly))
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
