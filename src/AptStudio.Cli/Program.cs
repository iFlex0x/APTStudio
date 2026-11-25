using System.Diagnostics;
using System.Globalization;
using AptStudio.Core.Containers;
using AptStudio.Core.Games;
using AptStudio.Core.Textures;

namespace AptStudio.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Usage();
            return 1;
        }

        if (string.Equals(args[0], "scan", StringComparison.OrdinalIgnoreCase))
        {
            return Scan(args[1]);
        }

        if (string.Equals(args[0], "list", StringComparison.OrdinalIgnoreCase))
        {
            return List(args[1]);
        }

        Usage();
        return 1;
    }

    private static void Usage()
    {
        Console.Error.WriteLine("usage:");
        Console.Error.WriteLine("  aptstudio scan <gameRoot>");
        Console.Error.WriteLine("  aptstudio list <gameRoot>");
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
                    $"{Path.GetFileName(file),-28} {archive.Header.Kind,-7} {archive.Entries.Count.ToString("N0", CultureInfo.InvariantCulture),7} rows  {archive.BodyLocation,-8} {archive.Length.ToString("N0", CultureInfo.InvariantCulture),14} B");
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
                $"external extents checked against COMMON: {external.ToString("N0", CultureInfo.InvariantCulture)}, outside: {outside.ToString("N0", CultureInfo.InvariantCulture)}");
        }
        else if (parsed.Exists(item => item.Archive.BodyLocation == BodyLocation.External))
        {
            failures.Add("COMMON.DPC not found; external extents were not checked.");
        }

        watch.Stop();
        Console.WriteLine(
            $"files: {files.Length.ToString("N0", CultureInfo.InvariantCulture)}, parsed: {parsed.Count.ToString("N0", CultureInfo.InvariantCulture)}, failed: {failures.Count.ToString("N0", CultureInfo.InvariantCulture)}, {watch.ElapsedMilliseconds.ToString("N0", CultureInfo.InvariantCulture)} ms");
        foreach (string failure in failures)
        {
            Console.Error.WriteLine(failure);
        }

        return failures.Count == 0 ? 0 : 2;
    }

    private static int List(string gameRoot)
    {
        if (!TryFindDpcs(gameRoot, out string[] files))
        {
            return 1;
        }

        Stopwatch watch = Stopwatch.StartNew();
        List<string> failures = new();
        Dictionary<string, int> byArchive = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> byFormat = new(StringComparer.Ordinal);
        int total = 0;
        int volumes = 0;
        int unknownCodec = 0;
        int skippedExternal = 0;

        foreach (string file in files)
        {
            string name = Path.GetFileName(file);
            try
            {
                DpcArchive archive = DpcArchive.Open(file);
                if (archive.BodyLocation != BodyLocation.Local)
                {
                    int externalBitmaps = archive.Entries.Count(
                        entry => entry.ClassHash == BitmapHead.ClassHash);
                    if (externalBitmaps > 0)
                    {
                        failures.Add(
                            $"{name}: {externalBitmaps} bitmap rows resolve into another archive.");
                        Console.WriteLine($"{name,-28} ERROR");
                    }
                    else
                    {
                        skippedExternal++;
                    }

                    continue;
                }

                IReadOnlyList<TextureEntry> textures = TextureScanner.Scan(archive);
                if (textures.Count == 0)
                {
                    continue;
                }

                byArchive[name] = textures.Count;
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
                        ? "volume"
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
        foreach ((string name, int count) in byArchive.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"{name,-28} {count.ToString("N0", CultureInfo.InvariantCulture),7} textures");
        }

        Console.WriteLine("formats:");
        foreach ((string format, int count) in byFormat.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"  {format,-18} {count.ToString("N0", CultureInfo.InvariantCulture),7}");
        }

        Console.WriteLine(
            $"textures: {total.ToString("N0", CultureInfo.InvariantCulture)}, volumes: {volumes.ToString("N0", CultureInfo.InvariantCulture)}, unknown codec: {unknownCodec.ToString("N0", CultureInfo.InvariantCulture)}, skipped external: {skippedExternal.ToString("N0", CultureInfo.InvariantCulture)}, failed: {failures.Count.ToString("N0", CultureInfo.InvariantCulture)}, {watch.ElapsedMilliseconds.ToString("N0", CultureInfo.InvariantCulture)} ms");
        foreach (string failure in failures)
        {
            Console.Error.WriteLine(failure);
        }

        return failures.Count == 0 ? 0 : 2;
    }

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
