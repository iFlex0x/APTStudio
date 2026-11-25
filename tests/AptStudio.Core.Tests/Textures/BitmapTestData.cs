using System.Buffers.Binary;
using AptStudio.Core.Textures;

namespace AptStudio.Core.Tests.Textures;

internal static class BitmapTestData
{
    public static byte[] Header(
        int width,
        int height,
        int mips,
        uint stored,
        ushort key,
        BitmapKind kind = BitmapKind.Flat,
        BitmapStorage storage = BitmapStorage.Raw,
        int surfaces = 1,
        uint compressed = 0,
        ulong name = 0x1111,
        ulong link = 0x2222,
        ulong classHash = BitmapHead.ClassHash,
        ushort version = 6,
        uint linkSize = 63,
        uint? bodySize = null)
    {
        byte[] header = new byte[BitmapHead.HeaderSize];
        BinaryPrimitives.WriteUInt64LittleEndian(header, classHash);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(0x08), name);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(0x10), link);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x24), linkSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x28), bodySize ?? (stored + 4));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x2C), compressed);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0x30), version);
        header[0x32] = (byte)storage;
        header[0x3F] = (byte)kind;
        header[0x41] = (byte)(width & 0xFF);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0x42), (ushort)(width >> 8));
        header[0x45] = (byte)(height & 0xFF);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0x46), (ushort)(height >> 8));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x48), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x4C), (uint)(surfaces << 8));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x51), stored);
        header[0x61] = (byte)(mips - 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0x64), key);
        return header;
    }
}
