namespace AptStudio.Desktop;

internal sealed record SkinPartRow(
    int Index,
    ulong MeshHash,
    int Vertices,
    int Stride,
    int Packets,
    bool PacketsParsed,
    ulong MaterialHash);
