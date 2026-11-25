using AptStudio.Core.Containers;
using AptStudio.Core.Textures;

namespace AptStudio.Core.Assets;

public sealed record AssetEntry(
    string ArchivePath,
    string BodyArchivePath,
    bool IsExternal,
    DpcEntry Row,
    BitmapHead? Bitmap,
    string? BodyError)
{
    public ulong NameHash => Row.NameHash;

    public ulong ClassHash => Row.ClassHash;

    public ulong LinkHash => Row.LinkHash;

    public string ClassName => AssetClass.Name(Row.ClassHash);

    public bool IsBitmap => Row.ClassHash == AssetClass.Bitmap;

    public TextureEntry? Texture =>
        Bitmap is null ? null : new TextureEntry(BodyArchivePath, Row, Bitmap);

    public bool IsPreviewable =>
        Bitmap is not null && TextureDecoder.CanDecodeMip0(Bitmap);
}
