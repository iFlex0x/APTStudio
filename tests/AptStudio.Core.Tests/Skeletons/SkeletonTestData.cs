using System.Buffers.Binary;
using AptStudio.Core.Skeletons;
using K4os.Compression.LZ4;

namespace AptStudio.Core.Tests.Skeletons;

internal static class SkeletonTestData
{
    public const ulong Name = 0xABCD;
    public const ulong Link = 0x1234;
    public const uint LinkSize = 20;

    public static byte[] Header(
        ulong name = Name,
        uint linkSize = LinkSize,
        uint bodySize = 256,
        uint storedSize = 128,
        ushort version = 1,
        ulong classHash = SkeletonHead.SkeletonClass)
    {
        byte[] header = new byte[SkeletonHead.HeaderSize];
        BinaryPrimitives.WriteUInt64LittleEndian(header, classHash);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(8), name);
        BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(0x10), Link);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x24), linkSize);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x28), bodySize);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x2C), storedSize);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(0x30), version);
        header[0x32] = 2;
        return header;
    }

    public static byte[] Body(params TestBone[] bones) => Body(Name, 8, bones);

    public static byte[] Body(int tail, params TestBone[] bones) => Body(Name, tail, bones);

    public static byte[] Body(ulong owner, int tail, params TestBone[] bones)
    {
        byte[] body = new byte[4 + bones.Length * SkeletonData.BoneRecordSize + tail];
        BinaryPrimitives.WriteInt32LittleEndian(body, bones.Length);
        for (int index = 0; index < bones.Length; index++)
        {
            TestBone bone = bones[index];
            int at = 4 + index * SkeletonData.BoneRecordSize;
            BinaryPrimitives.WriteUInt64LittleEndian(body.AsSpan(at + 0xC4), bone.Name);
            BinaryPrimitives.WriteUInt64LittleEndian(body.AsSpan(at + 0xCC), owner);
            BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(at + 0x120), bone.Parent);
            BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(at + 0x124), bone.FirstChild);
            BinaryPrimitives.WriteSingleLittleEndian(body.AsSpan(at + 0xD4), 1f);
            BinaryPrimitives.WriteSingleLittleEndian(body.AsSpan(at + 0xD4 + 20), 1f);
            BinaryPrimitives.WriteSingleLittleEndian(body.AsSpan(at + 0xD4 + 40), 1f);
            BinaryPrimitives.WriteSingleLittleEndian(body.AsSpan(at + 0xD4 + 48), bone.X);
            BinaryPrimitives.WriteSingleLittleEndian(body.AsSpan(at + 0xD4 + 52), bone.Y);
            BinaryPrimitives.WriteSingleLittleEndian(body.AsSpan(at + 0xD4 + 56), bone.Z);
            BinaryPrimitives.WriteSingleLittleEndian(body.AsSpan(at + 0xD4 + 60), 1f);
        }

        return body;
    }

    public static byte[] Object(byte[] body, ulong name = Name)
    {
        byte[] block = new byte[LZ4Codec.MaximumOutputSize(body.Length)];
        int written = LZ4Codec.Encode(body, block);
        uint stored = (uint)(written + 8);
        byte[] payload = new byte[stored];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, (uint)body.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), stored);
        block.AsSpan(0, written).CopyTo(payload.AsSpan(8));

        byte[] result = new byte[SkeletonHead.HeaderSize + LinkSize + SkeletonHead.ExtraSize + stored];
        Header(name, LinkSize, (uint)body.Length, stored).CopyTo(result, 0);
        payload.CopyTo(result, SkeletonHead.HeaderSize + LinkSize + SkeletonHead.ExtraSize);
        return result;
    }

    public static SkeletonHit Hit(byte[] obj, long offset = 0) =>
        new(
            offset,
            BinaryPrimitives.ReadUInt64LittleEndian(obj.AsSpan(8)),
            BinaryPrimitives.ReadUInt64LittleEndian(obj.AsSpan(0x10)),
            BinaryPrimitives.ReadUInt32LittleEndian(obj.AsSpan(0x28)),
            BinaryPrimitives.ReadUInt32LittleEndian(obj.AsSpan(0x2C)),
            BinaryPrimitives.ReadUInt32LittleEndian(obj.AsSpan(0x24)),
            BinaryPrimitives.ReadUInt16LittleEndian(obj.AsSpan(0x30)),
            (uint)obj.Length);

    public static string TempFile(byte[] bytes)
    {
        string path = Path.Combine(Path.GetTempPath(), $"aptstudio-skeleton-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}

internal sealed record TestBone(
    ulong Name,
    int Parent = -1,
    int FirstChild = -1,
    float X = 0f,
    float Y = 0f,
    float Z = 0f);
