using System.Buffers.Binary;

namespace AptStudio.Core.Skins;

public sealed record SkinPart(
    int Index,
    ulong MeshHash,
    int VertexCount,
    int Stride,
    ulong StreamHash,
    ulong MaterialHash,
    int PacketCount,
    bool PacketsParsed,
    int DataOffset);

public sealed record SkinBone(ulong NameHash, int Index);

public sealed record SkinWeightBuffer(
    int VertexCount,
    int Stride,
    ushort[] Indices,
    ushort[] Weights);

public sealed class SkinData
{
    private const int MaxMeshes = 1000;
    private const int MaxParts = 1000;
    private const int MaxPackets = 64;
    private const int MaxBones = 4000;
    private const int MaxBoneSlot = 4096;
    private const int MaxVertexCount = 500_000;
    private const uint VertexFlags = 0x00200102;

    private readonly byte[] _body;

    private SkinData(
        SkinHead head,
        ulong[] meshLinks,
        SkinPart[] parts,
        SkinBone[] bones,
        byte[] body)
    {
        Head = head;
        MeshLinks = meshLinks;
        Parts = parts;
        Bones = bones;
        _body = body;
    }

    public SkinHead Head { get; }

    public ulong[] MeshLinks { get; }

    public SkinPart[] Parts { get; }

    public SkinBone[] Bones { get; }

    public int SkinnedParts => Parts.Count(part => part.VertexCount > 0);

    public static SkinData Parse(
        SkinHead head,
        byte[] body,
        string source,
        Func<ulong, int?> vertexCount)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(vertexCount);

        if (body.Length < 12)
        {
            throw new InvalidDataException($"{source}: skin body is {body.Length} bytes.");
        }

        int meshCount = BinaryPrimitives.ReadInt32LittleEndian(body);
        if (meshCount is < 1 or > MaxMeshes)
        {
            throw new InvalidDataException($"{source}: mesh count {meshCount} is out of range.");
        }

        long linksEnd = 4L + (long)meshCount * 8;
        if (linksEnd + 4 > body.Length)
        {
            throw new InvalidDataException(
                $"{source}: {meshCount} mesh links do not fit in {body.Length} bytes.");
        }

        ulong[] links = new ulong[meshCount];
        for (int i = 0; i < meshCount; i++)
        {
            links[i] = BinaryPrimitives.ReadUInt64LittleEndian(body.AsSpan(4 + i * 8));
        }

        int partCount = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan((int)linksEnd));
        if (partCount is < 1 or > MaxParts || partCount > meshCount)
        {
            throw new InvalidDataException(
                $"{source}: part count {partCount} is out of range for {meshCount} meshes.");
        }

        int cursor = (int)linksEnd + 4;
        SkinPart[] parts = new SkinPart[partCount];
        for (int p = 0; p < partCount; p++)
        {
            ulong link = links[p];
            int? expected = vertexCount(link);
            if (expected == 0)
            {
                parts[p] = new SkinPart(p, link, 0, 0, 0, 0, 0, false, 0);
                continue;
            }

            int block = FindVertexBlock(body, cursor, expected, out int count, out int stride, out ulong stream);
            if (block < 0)
            {
                throw new InvalidDataException(
                    $"{source}: no vertex block with {expected} vertices for mesh 0x{link:X16}.");
            }

            bool packets = TryReadPackets(body, block, out int packetCount, out ulong material);
            parts[p] = new SkinPart(
                p, link, count, stride, stream, material, packetCount, packets, block + 20);
            cursor = block + 20 + count * stride;
        }

        return new SkinData(head, links, parts, ReadBones(body, cursor), body);
    }

    public SkinWeightBuffer ReadWeights(SkinPart part)
    {
        ArgumentNullException.ThrowIfNull(part);
        if (part.DataOffset <= 0 || part.VertexCount <= 0)
        {
            return new SkinWeightBuffer(0, 0, Array.Empty<ushort>(), Array.Empty<ushort>());
        }

        int end = part.DataOffset + part.VertexCount * part.Stride;
        if (end > _body.Length)
        {
            throw new InvalidDataException(
                $"part {part.Index}: vertex data ends at {end}, body is {_body.Length} bytes.");
        }

        if (part.Stride is 16 or 32)
        {
            int influences = part.Stride == 16 ? 4 : 8;
            ushort[] indices = new ushort[part.VertexCount * influences];
            ushort[] weights = new ushort[part.VertexCount * influences];
            for (int v = 0; v < part.VertexCount; v++)
            {
                int at = part.DataOffset + v * part.Stride;
                for (int j = 0; j < influences; j++)
                {
                    indices[v * influences + j] =
                        BinaryPrimitives.ReadUInt16LittleEndian(_body.AsSpan(at + j * 2));
                    weights[v * influences + j] = BinaryPrimitives.ReadUInt16LittleEndian(
                        _body.AsSpan(at + part.Stride / 2 + j * 2));
                }
            }

            return new SkinWeightBuffer(part.VertexCount, part.Stride, indices, weights);
        }

        if (part.Stride == 12)
        {
            ushort[] indices = new ushort[part.VertexCount];
            for (int v = 0; v < part.VertexCount; v++)
            {
                int at = part.DataOffset + v * 12;
                indices[v] = (ushort)BinaryPrimitives.ReadUInt32LittleEndian(_body.AsSpan(at));
            }

            return new SkinWeightBuffer(part.VertexCount, 12, indices, Array.Empty<ushort>());
        }

        throw new InvalidDataException($"part {part.Index}: stride {part.Stride} is not measured.");
    }

    private static int FindVertexBlock(
        byte[] body,
        int from,
        int? expected,
        out int count,
        out int stride,
        out ulong stream)
    {
        for (int flags = from + 8; flags + 12 <= body.Length; flags++)
        {
            if (body[flags] != 0x02 || body[flags + 1] != 0x01
                || body[flags + 2] != 0x20 || body[flags + 3] != 0x00)
            {
                continue;
            }

            int head = flags - 8;
            count = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(head));
            stride = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(head + 4));
            if (count is < 1 or > MaxVertexCount || stride is not (12 or 16 or 32))
            {
                continue;
            }

            long end = head + 20L + (long)count * stride;
            if (end > body.Length)
            {
                continue;
            }

            if (expected is > 0 && count != expected)
            {
                continue;
            }

            if (expected is null && !TryReadPackets(body, head, out _, out _))
            {
                continue;
            }

            stream = BinaryPrimitives.ReadUInt64LittleEndian(body.AsSpan(flags + 4));
            return head;
        }

        count = 0;
        stride = 0;
        stream = 0;
        return -1;
    }

    private static bool TryReadPackets(byte[] body, int block, out int packets, out ulong material)
    {
        if (BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(block - 4)) != 1)
        {
            packets = 0;
            material = 0;
            return false;
        }

        for (int n = 1; n <= MaxPackets; n++)
        {
            int start = block - 8 - n * 16;
            if (start < 0)
            {
                break;
            }

            if (BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(start)) != n)
            {
                continue;
            }

            int at = start + 4;
            bool ok = true;
            for (int k = 0; k < n && ok; k++)
            {
                if (at + 16 > body.Length)
                {
                    ok = false;
                    break;
                }

                // packeted sync/morph tables are not measured, so only zero counts pass
                if (BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(at + 12)) != 0)
                {
                    ok = false;
                    break;
                }

                at += 16;
            }

            if (ok && at == block - 4)
            {
                packets = n;
                material = n > 0
                    ? BinaryPrimitives.ReadUInt64LittleEndian(body.AsSpan(start + 4))
                    : 0;
                return true;
            }
        }

        packets = 0;
        material = 0;
        return false;
    }

    private static SkinBone[] ReadBones(byte[] body, int from)
    {
        // the table ends at the body end; a bone slot can look like a smaller count
        for (int count = MaxBones; count >= 1; count--)
        {
            int start = body.Length - 4 - count * 12;
            if (start < from)
            {
                continue;
            }

            if (BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(start)) != count)
            {
                continue;
            }

            SkinBone[] bones = new SkinBone[count];
            HashSet<int> slots = new(count);
            bool valid = true;
            for (int i = 0; i < count; i++)
            {
                int at = start + 4 + i * 12;
                ulong name = BinaryPrimitives.ReadUInt64LittleEndian(body.AsSpan(at));
                int slot = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(at + 8));
                if (name == 0 || slot < 0 || slot >= MaxBoneSlot || !slots.Add(slot))
                {
                    valid = false;
                    break;
                }

                bones[i] = new SkinBone(name, slot);
            }

            if (valid)
            {
                return bones;
            }
        }

        return Array.Empty<SkinBone>();
    }
}
