// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AptStudio.Core.Textures;

public enum TextureFamily
{
    Unknown,
    Block8,
    Block16,
    Linear2,
    Linear4,
    Linear8,
    Linear16,
    Volume,
}

public enum TextureCodec
{
    Unknown,
    Bc1,
    Bc3,
}

public sealed record TextureFormat(byte Key, TextureFamily Family, TextureCodec Codec)
{
    public string DisplayName => Codec switch
    {
        TextureCodec.Bc1 => "BC1",
        TextureCodec.Bc3 => "BC3",
        _ => $"0x{Key:X2} {Family}",
    };
}

public static class TextureFormats
{
    // Families come from stored size versus dimensions. Only BC1 and BC3 are visually verified.
    public static TextureFormat FromKey(ushort rawKey)
    {
        byte key = (byte)(rawKey & 0xFF);
        (TextureFamily family, TextureCodec codec) = key switch
        {
            0x06 or 0x15 => (TextureFamily.Linear4, TextureCodec.Unknown),
            0x0C => (TextureFamily.Linear2, TextureCodec.Unknown),
            0x11 => (TextureFamily.Linear8, TextureCodec.Unknown),
            0x19 => (TextureFamily.Linear16, TextureCodec.Unknown),
            0x13 or 0x16 => (TextureFamily.Volume, TextureCodec.Unknown),
            0x1B => (TextureFamily.Block8, TextureCodec.Bc1),
            0x1D => (TextureFamily.Block16, TextureCodec.Bc3),
            0x1E => (TextureFamily.Block8, TextureCodec.Unknown),
            0x20 or 0x21 or 0x23 => (TextureFamily.Block16, TextureCodec.Unknown),
            _ => (TextureFamily.Unknown, TextureCodec.Unknown),
        };

        return new TextureFormat(key, family, codec);
    }

    public static int UnitBytes(TextureFamily family) => family switch
    {
        TextureFamily.Block8 or TextureFamily.Linear8 => 8,
        TextureFamily.Block16 or TextureFamily.Linear16 => 16,
        TextureFamily.Linear2 => 2,
        TextureFamily.Linear4 => 4,
        _ => 0,
    };

    public static bool IsBlock(TextureFamily family) =>
        family is TextureFamily.Block8 or TextureFamily.Block16;

    public static long MipBytes(TextureFamily family, int width, int height)
    {
        int unit = UnitBytes(family);
        return IsBlock(family)
            ? (long)Math.Max(1, (width + 3) / 4) * Math.Max(1, (height + 3) / 4) * unit
            : (long)width * height * unit;
    }
}
