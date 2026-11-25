namespace AptStudio.Core.Containers;

public sealed record DpcEntry(
    ulong NameHash,
    ulong ClassHash,
    ulong LinkHash,
    uint Offset,
    uint CompressedSize,
    uint Flags,
    uint DecompressedSize,
    ushort U2,
    ushort U3)
{
    public long BodyOffset => (long)Offset * 32;
}
