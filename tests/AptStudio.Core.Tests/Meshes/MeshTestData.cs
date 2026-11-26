using System.Buffers.Binary;
using AptStudio.Core.Containers;
using AptStudio.Core.Meshes;
using K4os.Compression.LZ4;

namespace AptStudio.Core.Tests.Meshes;

internal static class MeshTestData
{
    public const ulong Name = 0x1111;
    public const ulong Link = 0x2222;
    public const int LinkSize = 13;
    public const int PayloadOffset = 0x48;
    public const int IndexData = 0xC4;

    public static byte[] Payload(bool secondStream = false, int firstStride = 32, int trailer = 68)
    {
        int size = 0x18 + 20 + 4 * firstStride;
        if (secondStream)
        {
            size += 20 + 4 * 20;
        }

        size += 4 + 20 + 6 * 2 + trailer;
        byte[] payload = new byte[size];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0x14), secondStream ? 2u : 1u);
        int at = 0x18;
        WriteStream(payload, ref at, 4, firstStride, 0x0102, 0xA1);
        for (int i = 0; i < 4; i++)
        {
            WriteSingle(payload, at + i * firstStride, i is 1 or 2 ? 1f : 0f);
            WriteSingle(payload, at + i * firstStride + 4, i is 2 or 3 ? 1f : 0f);
        }

        at += 4 * firstStride;
        if (secondStream)
        {
            WriteStream(payload, ref at, 4, 20, 0x0502, 0xA2);
            at += 4 * 20;
        }

        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at), 1);
        at += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at), 6);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(at + 4), 0x0102);
        BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(at + 8), 0xA3);
        at += 20;
        WriteIndex(payload, at, 0);
        WriteIndex(payload, at + 2, 1);
        WriteIndex(payload, at + 4, 2);
        WriteIndex(payload, at + 6, 0);
        WriteIndex(payload, at + 8, 2);
        WriteIndex(payload, at + 10, 3);
        return payload;
    }

    public static byte[] Body(
        byte[] payload,
        ulong name = Name,
        ushort version = 11,
        bool lz4 = false,
        int linkSize = LinkSize)
    {
        int extra = version switch
        {
            9 => 4,
            >= 11 => 8,
            _ => 0,
        };
        int payloadOffset = (0x33 + extra + linkSize + 7) & ~7;
        byte[] block = [];
        int compressedSize = 0;
        if (lz4)
        {
            block = new byte[LZ4Codec.MaximumOutputSize(payload.Length)];
            compressedSize = LZ4Codec.Encode(payload, block) + 8;
        }

        byte[] body = new byte[payloadOffset + (lz4 ? compressedSize : payload.Length)];
        BinaryPrimitives.WriteUInt64LittleEndian(body, MeshHead.ClassHash);
        BinaryPrimitives.WriteUInt64LittleEndian(body.AsSpan(0x08), name);
        BinaryPrimitives.WriteUInt64LittleEndian(body.AsSpan(0x10), Link);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(0x24), (uint)linkSize);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(0x28), (uint)payload.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(0x2C), (uint)compressedSize);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(0x30), version);
        body[0x32] = lz4 ? (byte)2 : (byte)0;
        if (lz4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(payloadOffset), (uint)payload.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(payloadOffset + 4), (uint)compressedSize);
            block.AsSpan(0, compressedSize - 8).CopyTo(body.AsSpan(payloadOffset + 8));
        }
        else
        {
            payload.CopyTo(body, payloadOffset);
        }

        return body;
    }

    public static DpcEntry Row(byte[] body, ulong name = Name) =>
        new(name, MeshHead.ClassHash, Link, 0, (uint)body.Length, 0, 0, 0, 0);

    private static void WriteStream(
        byte[] payload, ref int at, int count, int stride, ushort flags, ulong hash)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at), (uint)count);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at + 4), (uint)stride);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(at + 8), flags);
        BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(at + 12), hash);
        at += 20;
    }

    private static void WriteSingle(byte[] payload, int at, float value) =>
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(at), value);

    private static void WriteIndex(byte[] payload, int at, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(at), value);
}
