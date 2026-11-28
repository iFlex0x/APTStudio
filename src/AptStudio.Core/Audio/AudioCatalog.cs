// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Core.Games;
using AptStudio.Core.Textures;

namespace AptStudio.Core.Audio;

public sealed record AudioEntry(
    string PackPath,
    string PackName,
    uint BankId,
    uint Id,
    long Offset,
    uint Size,
    uint LanguageId,
    string Language,
    WemInfo Info,
    string? Name)
{
    public double DurationSeconds => Info.DurationSeconds;

    public string IdText => $"0x{Id:X8}";
}

public sealed record AudioArchive(string Path, int Media);

public sealed record AudioCatalog(
    IReadOnlyList<AudioEntry> Entries,
    IReadOnlyList<AudioArchive> Archives,
    IReadOnlyList<CatalogFailure> Failures,
    int Packs,
    int Previews,
    int Plugins)
{
    /// <summary>
    /// Music previews live in MUS_STRUCTURE with truncated bodies and stale
    /// RIFF sizes; the full tracks are in MUS_MEDIA and MUS_MEDIA_CHUNK0.
    /// </summary>
    private const string PreviewPack = "MUS_STRUCTURE.PC.PCK";

    public static AudioCatalog Load(
        GameProfile profile,
        IProgress<CatalogProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        string root = Path.Combine(profile.Root, "SOUNDBANKS");
        if (!Directory.Exists(root))
        {
            throw new InvalidDataException("SOUNDBANKS was not found in the game folder.");
        }

        List<string> files;
        try
        {
            files = Directory.EnumerateFiles(root, "*.PCK", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"SOUNDBANKS cannot be listed: {ex.Message}", ex);
        }

        List<AudioEntry> entries = new();
        List<AudioArchive> archives = new();
        List<CatalogFailure> failures = new();
        int previews = 0;
        int plugins = 0;
        for (int index = 0; index < files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string file = files[index];
            progress?.Report(new CatalogProgress(index + 1, files.Count, Path.GetFileName(file)));
            try
            {
                AudioPack pack = AudioPack.Read(file);
                if (string.Equals(pack.Name, PreviewPack, StringComparison.OrdinalIgnoreCase))
                {
                    previews += pack.Media.Count;
                    continue;
                }

                if (pack.Media.Count == 0)
                {
                    continue;
                }

                List<AudioEntry> packEntries = new();
                int packPlugins = 0;
                using FileStream stream = new(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                foreach (AudioMediaRef media in pack.Media)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    WemInfo? info = WemInfo.TryRead(
                        stream, media.Offset, media.Size, $"{pack.Name} 0x{media.Id:X8}");
                    if (info is null)
                    {
                        packPlugins++;
                        continue;
                    }

                    packEntries.Add(new AudioEntry(
                        file,
                        pack.Name,
                        media.BankId,
                        media.Id,
                        media.Offset,
                        media.Size,
                        media.LanguageId,
                        pack.LanguageName(media.LanguageId),
                        info,
                        info.Cue));
                }

                entries.AddRange(packEntries);
                plugins += packPlugins;
                if (packEntries.Count > 0)
                {
                    archives.Add(new AudioArchive(file, packEntries.Count));
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                failures.Add(new CatalogFailure(file, ex.Message));
            }
        }

        return new AudioCatalog(entries, archives, failures, files.Count, previews, plugins);
    }
}
