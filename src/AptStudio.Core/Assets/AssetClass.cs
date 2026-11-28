// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Core.Textures;

namespace AptStudio.Core.Assets;

public static class AssetClass
{
    public const ulong Bitmap = BitmapHead.ClassHash;
    public const ulong Mesh = 0x7703A856BA5776DD;
    public const ulong Skin = 0x53E24BDB95306A09;
    public const ulong Animation = 0x622C802CCCFE934F;
    public const ulong Material = 0x310BB7F1CFC1D4FF;

    public static string Name(ulong classHash) => classHash switch
    {
        Bitmap => "Bitmap_Z",
        Mesh => "Mesh_Z",
        Skin => "Skin_Z",
        Animation => "Animation_Z",
        Material => "Material_Z",
        _ => $"0x{classHash:X16}",
    };
}
