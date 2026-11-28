// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.Numerics;

namespace AptStudio.Core.Meshes;

public sealed record MeshStream(int VertexCount, int Stride, ushort Flags, ulong Hash, int DataOffset);

public sealed record MeshGroup(
    int VertexCount,
    int IndexOffset,
    int FaceCount,
    int VertexOffset,
    uint Flags);

public sealed record MeshIndexSet(
    int IndexCount,
    int IndexSize,
    ushort Flags,
    ulong Hash,
    int DataOffset,
    IReadOnlyList<MeshGroup> Groups);

public sealed record MeshGeometry(Vector3[] Positions, int[] Indices, Vector3 Min, Vector3 Max);

public sealed record MeshData(
    MeshHead Head,
    IReadOnlyList<MeshStream> Streams,
    IReadOnlyList<MeshIndexSet> IndexSets,
    byte[] Payload)
{
    private const int MaxStreams = 8;
    private const int MaxIndexSets = 8;
    private const int MaxGroups = 64;
    private const int GroupRecordSize = 40;
    private const int MinVertexStride = 12;
    private const int MaxVertexStride = 256;

    public static MeshData Parse(MeshHead head, byte[] payload, string source)
    {
        if (payload.Length < 0x18)
        {
            throw new InvalidDataException(
                $"{source}: mesh payload is {payload.Length} bytes, need 24.");
        }

        if (!payload.AsSpan().ContainsAnyExcept((byte)0))
        {
            return new MeshData(head, [], [], payload);
        }

        try
        {
            return ParseStreamed(head, payload, source);
        }
        catch (InvalidDataException)
        {
            MeshData? compact = TryParseCompact(head, payload, source);
            if (compact is not null)
            {
                return compact;
            }

            throw;
        }
    }

    private static MeshData ParseStreamed(MeshHead head, byte[] payload, string source)
    {
        int streamCount = ReadCount(payload, 0x14, "stream count", source);
        if (streamCount is < 1 or > MaxStreams)
        {
            throw new InvalidDataException($"{source}: stream count {streamCount} is out of range.");
        }

        List<MeshStream> streams = new(streamCount);
        int at = 0x18;
        for (int index = 0; index < streamCount; index++)
        {
            if (at + 20 > payload.Length)
            {
                throw new InvalidDataException($"{source}: stream {index} descriptor is truncated.");
            }

            int vertexCount = ReadCount(payload, at, $"stream {index} vertex count", source);
            int stride = ReadCount(payload, at + 4, $"stream {index} stride", source);
            ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(at + 8));
            ulong hash = BinaryPrimitives.ReadUInt64LittleEndian(payload.AsSpan(at + 12));
            at += 20;
            if (vertexCount == 0)
            {
                throw new InvalidDataException($"{source}: stream {index} has no vertices.");
            }

            if (stride is < MinVertexStride or > MaxVertexStride)
            {
                throw new InvalidDataException($"{source}: stream {index} stride {stride} is out of range.");
            }

            long bytes = (long)vertexCount * stride;
            if (bytes > payload.Length - at)
            {
                throw new InvalidDataException(
                    $"{source}: stream {index} needs {bytes} bytes, payload has {payload.Length - at}.");
            }

            streams.Add(new MeshStream(vertexCount, stride, flags, hash, at));
            at += (int)bytes;
        }

        if (streams[0].Stride is not (32 or 36))
        {
            throw new InvalidDataException(
                $"{source}: position stride {streams[0].Stride} is not 32 or 36.");
        }

        if (at + 4 > payload.Length)
        {
            throw new InvalidDataException($"{source}: index set count is truncated.");
        }

        int setCount = ReadCount(payload, at, "index set count", source);
        at += 4;
        if (setCount is < 1 or > MaxIndexSets)
        {
            throw new InvalidDataException($"{source}: index set count {setCount} is out of range.");
        }

        List<IndexDescriptor> descriptors = new(setCount);
        for (int index = 0; index < setCount; index++)
        {
            if (at + 16 > payload.Length)
            {
                throw new InvalidDataException($"{source}: index set {index} descriptor is truncated.");
            }

            int indexCount = ReadCount(payload, at, $"index set {index} count", source);
            ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(at + 4));
            ushort indexFormat = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(at + 6));
            int indexSize = indexFormat switch
            {
                0 => 2,
                8 => 4,
                _ => throw new InvalidDataException(
                    $"{source}: index set {index} format {indexFormat} is not measured."),
            };
            ulong hash = BinaryPrimitives.ReadUInt64LittleEndian(payload.AsSpan(at + 8));
            at += 16;
            if (indexCount == 0 || indexCount % 3 != 0)
            {
                throw new InvalidDataException(
                    $"{source}: index set {index} count {indexCount} is not a triangle list.");
            }

            long bytes = (long)indexCount * indexSize;
            if (bytes > payload.Length - at)
            {
                throw new InvalidDataException(
                    $"{source}: index set {index} needs {bytes} bytes, payload has {payload.Length - at}.");
            }

            descriptors.Add(new IndexDescriptor(indexCount, indexSize, flags, hash, at));
            at += (int)bytes;
        }

        List<MeshGroup>[] groups = ParseGroups(payload, at, streams, descriptors, source);
        List<MeshIndexSet> sets = new(setCount);
        for (int index = 0; index < setCount; index++)
        {
            IndexDescriptor descriptor = descriptors[index];
            sets.Add(new MeshIndexSet(
                descriptor.Count,
                descriptor.IndexSize,
                descriptor.Flags,
                descriptor.Hash,
                descriptor.DataOffset,
                groups[index]));
        }

        for (int setIndex = 0; setIndex < sets.Count; setIndex++)
        {
            MeshStream stream = StreamForSet(streams, setIndex, source);
            MeshIndexSet set = sets[setIndex];
            foreach (MeshGroup group in set.Groups)
            {
                int end = group.IndexOffset + group.FaceCount * 3;
                for (int index = group.IndexOffset; index < end; index++)
                {
                    uint rawValue = ReadIndex(
                        payload, set.DataOffset + index * set.IndexSize, set.IndexSize);
                    if (rawValue > int.MaxValue)
                    {
                        throw new InvalidDataException(
                            $"{source}: index {index} is {rawValue}, too large.");
                    }

                    int value = (int)rawValue;
                    if (value >= group.VertexCount)
                    {
                        throw new InvalidDataException(
                            $"{source}: index {index} is {value}, group vertex count is {group.VertexCount}.");
                    }

                    if (value + group.VertexOffset >= stream.VertexCount)
                    {
                        throw new InvalidDataException(
                            $"{source}: index {index} is {value} + {group.VertexOffset}, stream has {stream.VertexCount}.");
                    }
                }
            }
        }

        return new MeshData(head, streams, sets, payload);
    }

    public MeshGeometry ToGeometry()
    {
        if (Streams.Count == 0)
        {
            throw new InvalidDataException("mesh has no geometry.");
        }

        MeshStream stream = Streams[0];
        MeshIndexSet set = IndexSets[0];
        Vector3[] positions = new Vector3[stream.VertexCount];
        Vector3 min = new(float.PositiveInfinity);
        Vector3 max = new(float.NegativeInfinity);
        for (int index = 0; index < positions.Length; index++)
        {
            int at = stream.DataOffset + index * stream.Stride;
            float x = BinaryPrimitives.ReadSingleLittleEndian(Payload.AsSpan(at));
            float y = BinaryPrimitives.ReadSingleLittleEndian(Payload.AsSpan(at + 4));
            float z = BinaryPrimitives.ReadSingleLittleEndian(Payload.AsSpan(at + 8));
            if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
            {
                throw new InvalidDataException(
                    $"Vertex {index} is not finite ({x}, {y}, {z}).");
            }

            positions[index] = new Vector3(x, y, z);
            min = Vector3.Min(min, positions[index]);
            max = Vector3.Max(max, positions[index]);
        }

        int[] indices = new int[set.IndexCount];
        int cursor = 0;
        foreach (MeshGroup group in set.Groups)
        {
            int end = group.IndexOffset + group.FaceCount * 3;
            for (; cursor < end; cursor++)
            {
                uint value = ReadIndex(
                    Payload, set.DataOffset + cursor * set.IndexSize, set.IndexSize);
                indices[cursor] = (int)value + group.VertexOffset;
            }
        }

        return new MeshGeometry(positions, indices, min, max);
    }

    private static MeshData? TryParseCompact(MeshHead head, byte[] payload, string source)
    {
        if (payload.Length < 12
            || !TryReadCount(payload, 0, out int vertexCount)
            || vertexCount < 3
            || 4L + (long)vertexCount * MinVertexStride > payload.Length)
        {
            return null;
        }

        for (int groupCount = 1; groupCount <= MaxGroups; groupCount++)
        {
            int trailerAt = payload.Length - (32 + groupCount * GroupRecordSize);
            if (trailerAt < 8)
            {
                break;
            }

            if (BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(trailerAt)) != 0
                || BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(trailerAt + 4)) != groupCount)
            {
                continue;
            }

            for (int setAt = trailerAt - 26; setAt >= 4; setAt -= 2)
            {
                if (BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(setAt)) != 1
                    || !TryReadCount(payload, setAt + 4, out int indexCount))
                {
                    continue;
                }

                ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(setAt + 8));
                ushort indexFormat = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(setAt + 10));
                int indexSize = indexFormat switch
                {
                    0 => 2,
                    8 => 4,
                    _ => 0,
                };
                if (indexSize == 0 || indexCount == 0 || indexCount % 3 != 0)
                {
                    continue;
                }

                if ((long)setAt + 20 + (long)indexCount * indexSize != trailerAt)
                {
                    continue;
                }

                List<MeshGroup> groups = new();
                int indexTotal = 0;
                int vertexTotal = 0;
                int recordAt = trailerAt + 8;
                bool valid = true;
                for (int group = 0; group < groupCount; group++)
                {
                    if (!TryReadCount(payload, recordAt + 8, out int groupVertices)
                        || !TryReadCount(payload, recordAt + 12, out int indexOffset)
                        || !TryReadCount(payload, recordAt + 16, out int faceCount)
                        || !TryReadCount(payload, recordAt + 20, out int vertexOffset)
                        || groupVertices == 0 || faceCount == 0
                        || indexOffset != indexTotal || vertexOffset != vertexTotal)
                    {
                        valid = false;
                        break;
                    }

                    groups.Add(new MeshGroup(
                        groupVertices,
                        indexOffset,
                        faceCount,
                        vertexOffset,
                        BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(recordAt))));
                    indexTotal += faceCount * 3;
                    vertexTotal += groupVertices;
                    recordAt += GroupRecordSize;
                }

                if (!valid || indexTotal != indexCount || vertexTotal != vertexCount)
                {
                    continue;
                }

                int dataOffset = setAt + 20;
                foreach (MeshGroup group in groups)
                {
                    int end = group.IndexOffset + group.FaceCount * 3;
                    for (int index = group.IndexOffset; index < end; index++)
                    {
                        uint value = ReadIndex(
                            payload, dataOffset + index * indexSize, indexSize);
                        if (value >= group.VertexCount || value + group.VertexOffset >= vertexCount)
                        {
                            throw new InvalidDataException(
                                $"{source}: compact index {index} is {value}.");
                        }
                    }
                }

                ulong hash = BinaryPrimitives.ReadUInt64LittleEndian(payload.AsSpan(setAt + 12));
                MeshStream stream = new(vertexCount, MinVertexStride, 0, 0, 4);
                MeshIndexSet set = new(
                    indexCount, indexSize, flags, hash, dataOffset, groups);
                return new MeshData(head, [stream], [set], payload);
            }
        }

        return null;
    }

    private static bool TryReadCount(byte[] payload, int at, out int value)
    {
        value = 0;
        if (at < 0 || at + 4 > payload.Length)
        {
            return false;
        }

        uint raw = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(at));
        if (raw > int.MaxValue)
        {
            return false;
        }

        value = (int)raw;
        return true;
    }

    private static List<MeshGroup>[] ParseGroups(
        byte[] payload,
        int at,
        IReadOnlyList<MeshStream> streams,
        IReadOnlyList<IndexDescriptor> sets,
        string source)
    {
        if (at + 8 > payload.Length)
        {
            throw new InvalidDataException($"{source}: group table is truncated.");
        }

        uint header = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(at));
        if (header != 0)
        {
            throw new InvalidDataException($"{source}: group header is {header}, expected 0.");
        }

        int groupCount = ReadCount(payload, at + 4, "group count", source);
        if (groupCount is < 1 or > MaxGroups)
        {
            throw new InvalidDataException($"{source}: group count {groupCount} is out of range.");
        }

        if ((long)at + 8 + (long)groupCount * GroupRecordSize > payload.Length)
        {
            throw new InvalidDataException(
                $"{source}: group table has {groupCount} records, payload is short.");
        }

        List<MeshGroup>[] result = new List<MeshGroup>[sets.Count];
        int recordAt = at + 8;
        int record = 0;
        for (int setIndex = 0; setIndex < sets.Count; setIndex++)
        {
            MeshStream stream = StreamForSet(streams, setIndex, source);
            List<MeshGroup> groups = new();
            int indexTotal = 0;
            int vertexTotal = 0;
            while (indexTotal < sets[setIndex].Count)
            {
                if (record >= groupCount)
                {
                    throw new InvalidDataException(
                        $"{source}: group table ends before index set {setIndex} is covered.");
                }

                uint flags = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(recordAt));
                int vertexCount = ReadCount(payload, recordAt + 8, "group vertex count", source);
                int indexOffset = ReadCount(payload, recordAt + 12, "group index offset", source);
                int faceCount = ReadCount(payload, recordAt + 16, "group face count", source);
                int vertexOffset = ReadCount(payload, recordAt + 20, "group vertex offset", source);
                long groupIndices = (long)faceCount * 3;
                if (vertexCount == 0 || faceCount == 0)
                {
                    throw new InvalidDataException(
                        $"{source}: group {record} has {vertexCount} vertices and {faceCount} faces.");
                }

                if (groupIndices > int.MaxValue || indexOffset != indexTotal)
                {
                    throw new InvalidDataException(
                        $"{source}: group {record} index offset {indexOffset} != {indexTotal}.");
                }

                if (vertexOffset != vertexTotal)
                {
                    throw new InvalidDataException(
                        $"{source}: group {record} vertex offset {vertexOffset} != {vertexTotal}.");
                }

                if (indexTotal + groupIndices > sets[setIndex].Count)
                {
                    throw new InvalidDataException(
                        $"{source}: group {record} overruns index set {setIndex}.");
                }

                groups.Add(new MeshGroup(vertexCount, indexOffset, faceCount, vertexOffset, flags));
                indexTotal += (int)groupIndices;
                vertexTotal += vertexCount;
                record++;
                recordAt += GroupRecordSize;
            }

            if (vertexTotal != stream.VertexCount)
            {
                throw new InvalidDataException(
                    $"{source}: index set {setIndex} groups cover {vertexTotal} vertices, stream has {stream.VertexCount}.");
            }

            result[setIndex] = groups;
        }

        if (record != groupCount)
        {
            throw new InvalidDataException(
                $"{source}: {groupCount - record} group records are left over.");
        }

        return result;
    }

    private static MeshStream StreamForSet(
        IReadOnlyList<MeshStream> streams, int setIndex, string source)
    {
        if (setIndex >= streams.Count)
        {
            throw new InvalidDataException(
                $"{source}: index set {setIndex} has no matching vertex stream.");
        }

        return streams[setIndex];
    }

    private static uint ReadIndex(byte[] payload, int at, int size) =>
        size == 2
            ? BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(at))
            : BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(at));

    private static int ReadCount(byte[] payload, int at, string field, string source)
    {
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(at));
        if (value > int.MaxValue)
        {
            throw new InvalidDataException($"{source}: {field} {value} is too large.");
        }

        return (int)value;
    }

    private sealed record IndexDescriptor(
        int Count, int IndexSize, ushort Flags, ulong Hash, int DataOffset);
}
