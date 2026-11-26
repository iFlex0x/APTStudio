using System.Buffers.Binary;
using AptStudio.Core.Animations;

namespace AptStudio.Core.Tests.Animations;

internal static class AnimationTestData
{
    public static byte[] Header(
        ulong nameHash,
        uint linkSize = 26,
        ushort extra = 11,
        uint bodySize = 128,
        uint storedSize = 64,
        ulong classHash = AnimationHead.AnimationClass,
        byte storage = 2)
    {
        byte[] header = new byte[AnimationHead.HeaderSize];
        BinaryPrimitives.WriteUInt64LittleEndian(header, classHash);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(8), nameHash);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(0x10), 0x1122334455667788);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x24), linkSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x28), bodySize);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x2C), storedSize);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0x30), extra);
        header[0x32] = storage;
        return header;
    }

    public static byte[] PackedBody()
    {
        // bone table: 2 bones, offsets 0 and 64; packed stream 128 bits
        byte[] body = new byte[60];
        BinaryPrimitives.WriteUInt32LittleEndian(body, 2);
        BinaryPrimitives.WriteUInt64LittleEndian(body.AsSpan(4), 0xAAAA111122223333);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(12), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(body.AsSpan(16), 0xBBBB444455556666);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(24), 64);
        body[28] = 0;
        body[29] = 4;
        for (int i = 30; i < 46; i++)
        {
            body[i] = (byte)i;
        }

        WriteFooter(body, packedBits: 128, duration: 1f, keyCount: 30);
        return body;
    }

    public static byte[] FrameBody()
    {
        // frame table: 4 entries, two tracks terminated by frame 30
        byte[] body = new byte[26];
        BinaryPrimitives.WriteUInt32LittleEndian(body, 4);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(4), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(6), 15);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(8), 30);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(10), 30);
        WriteFooter(body, packedBits: 0, duration: -1f, keyCount: uint.MaxValue);
        return body;
    }

    public static byte[] LinkBlock(float duration)
    {
        byte[] link = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(link.AsSpan(8), 4);
        BinaryPrimitives.WriteSingleLittleEndian(link.AsSpan(12), duration);
        BinaryPrimitives.WriteSingleLittleEndian(link.AsSpan(16), 1f);
        return link;
    }

    private static void WriteFooter(byte[] body, uint packedBits, float duration, uint keyCount)
    {
        int at = body.Length - AnimationData.FooterSize;
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(at), packedBits);
        BinaryPrimitives.WriteSingleLittleEndian(body.AsSpan(at + 4), duration);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(at + 8), keyCount);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(at + 12), 0);
    }
}
