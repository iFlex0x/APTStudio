// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Core.Assets;

namespace AptStudio.Desktop.Browser;

internal static class AnimationText
{
    public static string SourceLabel(AssetEntry animation)
    {
        string source = Path.GetFileName(animation.ArchivePath);
        return animation.BodyArchivePath.Equals(
            animation.ArchivePath, StringComparison.OrdinalIgnoreCase)
            ? source
            : $"{source} · data {Path.GetFileName(animation.BodyArchivePath)}";
    }
}
