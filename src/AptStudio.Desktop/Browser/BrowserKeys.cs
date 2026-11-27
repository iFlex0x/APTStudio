using AptStudio.Core.Assets;

namespace AptStudio.Desktop.Browser;

internal static class BrowserKeys
{
    public static string Texture(AssetEntry entry) =>
        $"{entry.ArchivePath}|{entry.Row.BodyOffset}|{entry.NameHash:X16}";

    public static string Texture(TextureOccurrence occurrence) =>
        $"{occurrence.ArchivePath}|{occurrence.BodyOffset}|{occurrence.Hash:X16}";
}
