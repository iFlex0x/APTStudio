using AptStudio.Core.Images;

namespace AptStudio.Core.Textures;

public static class TextureExport
{
    public static DecodedTexture ExportPng(TextureEntry texture, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(texture);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path is empty.", nameof(outputPath));
        }

        using FileStream stream = new(
            texture.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        DecodedTexture image = TextureDecoder.DecodeMip0(stream, texture);
        byte[] png = PngWriter.EncodeRgba(image.Rgba, image.Width, image.Height);
        string fullPath = Path.GetFullPath(outputPath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(fullPath, png);
        return image;
    }
}
