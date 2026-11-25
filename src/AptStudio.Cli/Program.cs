using System.Diagnostics;
using System.Globalization;
using AptStudio.Core.Containers;
using AptStudio.Core.Games;

namespace AptStudio.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 2 || !string.Equals(args[0], "scan", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("usage: aptstudio scan <gameRoot>");
            return 1;
        }

        if (!GameProfile.TryDetect(args[1], out GameProfile? profile, out string? error) || profile is null)
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        string[] files;
        try
        {
            files = profile.DataRoots
                .SelectMany(root => Directory.EnumerateFiles(
                    Path.Combine(profile.Root, root),
                    "*.DPC",
                    SearchOption.TopDirectoryOnly))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(ex.Message);
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
}
