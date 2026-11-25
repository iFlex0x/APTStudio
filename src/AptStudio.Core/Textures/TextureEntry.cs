using AptStudio.Core.Containers;

namespace AptStudio.Core.Textures;

public sealed record TextureEntry(string ArchivePath, DpcEntry Row, BitmapHead Head)
{
    public long PayloadOffset => Row.BodyOffset + Head.PayloadOffset;
}
