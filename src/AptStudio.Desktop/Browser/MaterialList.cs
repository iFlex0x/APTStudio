// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using AptStudio.Core.Materials;

namespace AptStudio.Desktop.Browser;

internal enum MaterialRowState
{
    Ok,
    Empty,
    Error,
}

internal sealed record MaterialRow(
    MaterialEntry Entry,
    string Code,
    int SlotCount,
    MaterialRowState State,
    string Error)
{
    public ulong Hash => Entry.NameHash;

    public string ArchiveName => Path.GetFileName(Entry.ArchivePath);

    public string DisplayCode => State == MaterialRowState.Error ? "read error" : Code;

    public string DisplaySlots =>
        State == MaterialRowState.Error ? "-" : SlotCount.ToString(CultureInfo.InvariantCulture);
}

internal sealed record MaterialListResult(IReadOnlyList<MaterialRow> Rows, int DecodeCount);

internal sealed class MaterialSummaryCache
{
    private readonly Dictionary<string, IReadOnlyList<MaterialRow>> _rows =
        new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<MaterialRow>? _allRows;

    public int Generation { get; private set; }

    public int DecodeCount { get; private set; }

    public IReadOnlyList<MaterialRow>? GetRows(string archivePath)
    {
        ArgumentNullException.ThrowIfNull(archivePath);
        return archivePath.Length == 0 ? _allRows : _rows.GetValueOrDefault(archivePath);
    }

    public bool TryStore(int generation, string archivePath, MaterialListResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentNullException.ThrowIfNull(result);
        if (generation != Generation)
        {
            return false;
        }

        _rows[archivePath] = result.Rows;
        DecodeCount += result.DecodeCount;
        return true;
    }

    public bool TryStoreAll(int generation, IReadOnlyList<MaterialRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (generation != Generation)
        {
            return false;
        }

        _allRows = rows;
        return true;
    }

    public void AddDecodes(int count)
    {
        if (count > 0)
        {
            DecodeCount += count;
        }
    }

    public void Reset()
    {
        Generation++;
        _rows.Clear();
        _allRows = null;
        DecodeCount = 0;
    }
}

internal static class MaterialListBuilder
{
    public static MaterialListResult Build(
        IReadOnlyList<MaterialEntry> entries,
        Func<MaterialEntry, (string Code, int SlotCount)> decode,
        CancellationToken cancellationToken = default,
        IProgress<int>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(decode);
        List<MaterialRow> rows = new(entries.Count);
        int decodes = 0;
        for (int index = 0; index < entries.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MaterialEntry entry = entries[index];
            decodes++;
            try
            {
                (string code, int slotCount) = decode(entry);
                rows.Add(new MaterialRow(
                    entry,
                    code,
                    slotCount,
                    slotCount == 0 ? MaterialRowState.Empty : MaterialRowState.Ok,
                    ""));
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException
                or UnauthorizedAccessException)
            {
                rows.Add(new MaterialRow(
                    entry, "", -1, MaterialRowState.Error, ex.Message));
            }

            if ((index + 1) % 1024 == 0 || index + 1 == entries.Count)
            {
                progress?.Report(index + 1);
            }
        }

        rows.Sort((left, right) =>
        {
            int order = left.Hash.CompareTo(right.Hash);
            if (order == 0)
            {
                order = string.Compare(
                    left.Entry.ArchivePath,
                    right.Entry.ArchivePath,
                    StringComparison.OrdinalIgnoreCase);
            }

            return order == 0 ? left.Entry.Offset.CompareTo(right.Entry.Offset) : order;
        });
        return new MaterialListResult(rows, decodes);
    }

    public static IReadOnlyList<MaterialRow> Filter(
        IReadOnlyList<MaterialRow> rows, string query)
    {
        ArgumentNullException.ThrowIfNull(rows);
        string needle = (query ?? "").Trim();
        if (needle.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            needle = needle[2..];
        }

        if (needle.Length == 0)
        {
            return rows;
        }

        List<MaterialRow> matches = new();
        foreach (MaterialRow row in rows)
        {
            if (row.Hash.ToString("X16", CultureInfo.InvariantCulture)
                    .Contains(needle, StringComparison.OrdinalIgnoreCase)
                || row.DisplayCode.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || row.ArchiveName.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(row);
            }
        }

        return matches;
    }
}
