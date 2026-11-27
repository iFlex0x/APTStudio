using AptStudio.Core.Assets;
using AptStudio.Core.Audio;
using AptStudio.Core.Materials;
using AptStudio.Core.Skeletons;

namespace AptStudio.Desktop.Browser;

internal static class BrowserKeys
{
    public static string Texture(AssetEntry entry) =>
        $"{entry.ArchivePath}|{entry.Row.BodyOffset}|{entry.NameHash:X16}";

    public static string Texture(TextureOccurrence occurrence) =>
        $"{occurrence.ArchivePath}|{occurrence.BodyOffset}|{occurrence.Hash:X16}";

    public static string Mesh(AssetEntry entry) =>
        $"{entry.ArchivePath}|{entry.Row.BodyOffset}|{entry.NameHash:X16}";

    public static string Mesh(MeshOccurrence occurrence) =>
        $"{occurrence.ArchivePath}|{occurrence.BodyOffset}|{occurrence.Hash:X16}";

    public static string Skin(AssetEntry entry) =>
        $"{entry.ArchivePath}|{entry.Row.BodyOffset}|{entry.NameHash:X16}";

    public static string Skin(SkinOccurrence occurrence) =>
        $"{occurrence.ArchivePath}|{occurrence.BodyOffset}|{occurrence.Hash:X16}";

    public static string Skeleton(SkeletonEntry entry) =>
        $"{entry.ArchivePath}|{entry.Offset}|{entry.NameHash:X16}";

    public static string Skeleton(SkeletonOccurrence occurrence) =>
        $"{occurrence.ArchivePath}|{occurrence.Offset}|{occurrence.Hash:X16}";

    public static string Material(MaterialEntry entry) =>
        $"{entry.ArchivePath}|{entry.Offset}|{entry.NameHash:X16}";

    public static string Material(MaterialOccurrence occurrence) =>
        $"{occurrence.ArchivePath}|{occurrence.BodyOffset}|{occurrence.Hash:X16}";

    public static string Audio(AudioEntry entry) =>
        $"{entry.PackPath}|{entry.Offset}|{entry.Size}|{entry.BankId}|" +
        $"{entry.Id:X8}|{entry.LanguageId}";

    public static string? HashText(object? tag) => tag switch
    {
        AssetEntry entry => $"0x{entry.NameHash:X16}",
        SkeletonEntry entry => $"0x{entry.NameHash:X16}",
        MaterialRow row => $"0x{row.Hash:X16}",
        AudioEntry entry => $"0x{entry.Id:X8}",
        _ => null,
    };
}
