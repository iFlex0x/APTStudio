// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AptStudio.Desktop;

internal static class ExportNames
{
    public static string Unique(string folder, string archivePath, ulong nameHash, ISet<string> used)
    {
        string stem = $"{Path.GetFileNameWithoutExtension(archivePath)}_{nameHash:X16}";
        string name = stem + ".png";
        int suffix = 2;
        while (used.Contains(name) || File.Exists(Path.Combine(folder, name)))
        {
            name = $"{stem}_{suffix++}.png";
        }

        used.Add(name);
        return name;
    }
}
