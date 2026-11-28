// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Core.Images;
using AptStudio.Core.IO;

namespace AptStudio.Core.Textures;

public static class TextureExport
{
    public static DecodedTexture ExportPng(TextureEntry texture, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(texture);
        string fullPath = PrepareOutput(texture, outputPath);
        DecodedTexture image = Decode(texture);
        Write(image, fullPath);
        return image;
    }

    public static void WritePng(TextureEntry texture, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(texture);
        string fullPath = PrepareOutput(texture, outputPath);
        Write(Decode(texture), fullPath);
    }

    // Validate and create the destination before reading the source.
    private static string PrepareOutput(TextureEntry texture, string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException(
                $"{texture.ArchivePath}: output path is empty.", nameof(outputPath));
        }

        string fullPath = Path.GetFullPath(outputPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return fullPath;
    }

    private static DecodedTexture Decode(TextureEntry texture)
    {
        using FileStream stream = new(
            texture.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return TextureDecoder.DecodeMip0(stream, texture);
    }

    private static void Write(DecodedTexture image, string fullPath)
    {
        byte[] png = PngWriter.EncodeRgba(image.Rgba, image.Width, image.Height);
        AtomicFile.WriteAllBytes(fullPath, png);
    }
}
