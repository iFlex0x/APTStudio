// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AptStudio.Desktop.Browser;

internal static class BrowserColumns
{
    public static readonly IReadOnlyList<AssetColumn> Textures =
    [
        new("Hash", 160, Mono: true),
        new("Archive", 0, Flexible: true, Muted: true, MiddleEllipsis: true),
        new("Size", 92, Right: true, Muted: true),
        new("Mips", 48, Right: true, Muted: true),
        new("Format", 56, Muted: true),
        new("Storage", 64, Muted: true),
    ];

    public static readonly IReadOnlyList<AssetColumn> NarrowTextures =
    [
        new("Hash", 150, Mono: true),
        new("Archive", 0, Flexible: true, Muted: true, MiddleEllipsis: true),
    ];

    public static readonly IReadOnlyList<AssetColumn> Animations =
    [
        new("Hash", 140, Mono: true),
        new("Archive", 0, Flexible: true, Muted: true, MiddleEllipsis: true),
        new("Occurrences", 78, Right: true, Muted: true),
    ];

    public static readonly IReadOnlyList<AssetColumn> Meshes =
    [
        new("Hash", 160, Mono: true),
        new("Archive", 0, Flexible: true, Muted: true, MiddleEllipsis: true),
    ];

    public static readonly IReadOnlyList<AssetColumn> Skins =
    [
        new("Hash", 160, Mono: true),
        new("Archive", 0, Flexible: true, Muted: true, MiddleEllipsis: true),
    ];

    public static readonly IReadOnlyList<AssetColumn> Skeletons =
    [
        new("Hash", 160, Mono: true),
        new("Archive", 0, Flexible: true, Muted: true, MiddleEllipsis: true),
    ];

    public static readonly IReadOnlyList<AssetColumn> Materials =
    [
        new("Hash", 160, Mono: true),
        new("Code", 190),
        new("Slots", 52, Right: true, Muted: true),
        new("Archive", 0, Flexible: true, Muted: true, MiddleEllipsis: true),
    ];

    public static readonly IReadOnlyList<AssetColumn> Audio =
    [
        new("Pack", 140, Muted: true, MiddleEllipsis: true),
        new("Id", 92, Mono: true),
        new("Name", 0, Flexible: true, MiddleEllipsis: true),
        new("Language", 90, Muted: true),
        new("Length", 72, Right: true, Muted: true),
        new("Ch", 42, Right: true, Muted: true),
        new("Rate", 64, Right: true, Muted: true),
        new("Bank", 76, Right: true, Muted: true),
    ];

    public static readonly IReadOnlyList<AssetColumn> NarrowAudio =
    [
        new("Pack", 64, Muted: true, MiddleEllipsis: true),
        new("Id", 82, Mono: true),
        new("Name", 0, Flexible: true, MiddleEllipsis: true),
        new("Language", 54, Muted: true),
    ];

    public static IReadOnlyList<AssetColumn> For(BrowserView view, int contentWidth) =>
        view switch
        {
            BrowserView.Audio => Fits(Audio, contentWidth, thumbnails: false)
                ? Audio
                : NarrowAudio,
            BrowserView.Textures => Fits(Textures, contentWidth, thumbnails: true)
                ? Textures
                : NarrowTextures,
            BrowserView.Animations => Animations,
            BrowserView.Meshes => Meshes,
            BrowserView.Skins => Skins,
            BrowserView.Skeletons => Skeletons,
            BrowserView.Materials => Materials,
            _ => Textures,
        };

    private static bool Fits(
        IReadOnlyList<AssetColumn> columns,
        int contentWidth,
        bool thumbnails) =>
        contentWidth >= AssetList.MinimumWidth(columns, thumbnails);
}
