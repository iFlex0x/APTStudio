using System.Buffers.Binary;
using System.Numerics;

namespace AptStudio.Core.Meshes;

public sealed record MeshStream(int VertexCount, int Stride, ushort Flags, ulong Hash, int DataOffset);

public sealed record MeshIndexSet(int IndexCount, ushort Flags, ulong Hash, int DataOffset);

public sealed record MeshGeometry(Vector3[] Positions, int[] Indices, Vector3 Min, Vector3 Max);

public sealed record MeshData(
    MeshHead Head,
    IReadOnlyList<MeshStream> Streams,
    IReadOnlyList<MeshIndexSet> IndexSets,
    byte[] Payload)
{
    private const int MaxStreams = 8;
    private const int MaxIndexSets = 8;
    private const int MinVertexStride = 12;
    private const int MaxVertexStride = 256;

    public static MeshData Parse(MeshHead head, byte[] payload, string source)
    {
        if (payload.Length < 0x18)
        {
            throw new InvalidDataException(
                $"{source}: mesh payload is {payload.Length} bytes, need 24.");
        }

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

        List<MeshIndexSet> sets = new(setCount);
        for (int index = 0; index < setCount; index++)
        {
            if (at + 20 > payload.Length)
            {
                throw new InvalidDataException($"{source}: index set {index} descriptor is truncated.");
            }

            int indexCount = ReadCount(payload, at, $"index set {index} count", source);
            ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(at + 4));
            ulong hash = BinaryPrimitives.ReadUInt64LittleEndian(payload.AsSpan(at + 8));
            at += 20;
            if (indexCount == 0 || indexCount % 3 != 0)
            {
                throw new InvalidDataException(
                    $"{source}: index set {index} count {indexCount} is not a triangle list.");
            }

            long bytes = (long)indexCount * 2;
            if (bytes > payload.Length - at)
            {
                throw new InvalidDataException(
                    $"{source}: index set {index} needs {bytes} bytes, payload has {payload.Length - at}.");
            }

            sets.Add(new MeshIndexSet(indexCount, flags, hash, at));
            at += (int)bytes;
        }

        int firstVertexCount = streams[0].VertexCount;
        for (int index = 0; index < sets[0].IndexCount; index++)
        {
            int value = BinaryPrimitives.ReadUInt16LittleEndian(
                payload.AsSpan(sets[0].DataOffset + index * 2));
            if (value >= firstVertexCount)
            {
                throw new InvalidDataException(
                    $"{source}: index {index} is {value}, vertex count is {firstVertexCount}.");
            }
        }

        return new MeshData(head, streams, sets, payload);
    }

    public MeshGeometry ToGeometry()
    {
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
        for (int index = 0; index < indices.Length; index++)
        {
            indices[index] = BinaryPrimitives.ReadUInt16LittleEndian(
                Payload.AsSpan(set.DataOffset + index * 2));
        }

        return new MeshGeometry(positions, indices, min, max);
    }

    private static int ReadCount(byte[] payload, int at, string field, string source)
    {
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(at));
        if (value > int.MaxValue)
        {
            throw new InvalidDataException($"{source}: {field} {value} is too large.");
        }

        return (int)value;
    }
}
