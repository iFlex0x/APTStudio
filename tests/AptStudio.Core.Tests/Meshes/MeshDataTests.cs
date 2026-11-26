using System.Buffers.Binary;
using System.Numerics;
using AptStudio.Core.Meshes;

namespace AptStudio.Core.Tests.Meshes;

public sealed class MeshDataTests
{
    [Fact]
    public void ReadsRawPayload()
    {
        byte[] body = MeshTestData.Body(MeshTestData.Payload());
        using MemoryStream stream = new(body);

        MeshData mesh = MeshDecoder.Read(stream, "TEST.DPC", MeshTestData.Row(body));

        Assert.Equal(11, mesh.Head.Version);
        Assert.Equal(MeshStorage.Raw, mesh.Head.Storage);
        MeshStream vertices = Assert.Single(mesh.Streams);
        Assert.Equal(4, vertices.VertexCount);
        Assert.Equal(32, vertices.Stride);
        MeshIndexSet indices = Assert.Single(mesh.IndexSets);
        Assert.Equal(6, indices.IndexCount);
    }

    [Fact]
    public void ReadsLz4Payload()
    {
        byte[] body = MeshTestData.Body(MeshTestData.Payload(), lz4: true);
        using MemoryStream stream = new(body);

        MeshData mesh = MeshDecoder.Read(stream, "TEST.DPC", MeshTestData.Row(body));

        Assert.Equal(MeshStorage.Lz4, mesh.Head.Storage);
        Assert.Equal(4, mesh.Streams[0].VertexCount);
        Assert.Equal(6, mesh.IndexSets[0].IndexCount);
    }

    [Fact]
    public void BuildsGeometryFromFirstStream()
    {
        byte[] body = MeshTestData.Body(MeshTestData.Payload(secondStream: true));
        using MemoryStream stream = new(body);

        MeshData mesh = MeshDecoder.Read(stream, "TEST.DPC", MeshTestData.Row(body));
        MeshGeometry geometry = mesh.ToGeometry();

        Assert.Equal(2, mesh.Streams.Count);
        Assert.Equal(20, mesh.Streams[1].Stride);
        Assert.Equal(4, geometry.Positions.Length);
        Assert.Equal(new Vector3(0, 0, 0), geometry.Positions[0]);
        Assert.Equal(new Vector3(1, 1, 0), geometry.Positions[2]);
        Assert.Equal(new[] { 0, 1, 2, 0, 2, 3 }, geometry.Indices);
        Assert.Equal(Vector3.Zero, geometry.Min);
        Assert.Equal(new Vector3(1, 1, 0), geometry.Max);
    }

    [Fact]
    public void AppliesGroupVertexOffsets()
    {
        byte[] body = MeshTestData.Body(MeshTestData.Payload(twoGroups: true));
        using MemoryStream stream = new(body);

        MeshData mesh = MeshDecoder.Read(stream, "TEST.DPC", MeshTestData.Row(body));
        MeshIndexSet set = Assert.Single(mesh.IndexSets);
        Assert.Equal(2, set.Groups.Count);
        MeshGeometry geometry = mesh.ToGeometry();

        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, geometry.Indices);
        Assert.Equal(new Vector3(0, 0, 1), geometry.Positions[3]);
    }

    [Fact]
    public void ReadsWideIndices()
    {
        byte[] body = MeshTestData.Body(
            MeshTestData.Payload(twoGroups: true, wideIndices: true));
        using MemoryStream stream = new(body);

        MeshData mesh = MeshDecoder.Read(stream, "TEST.DPC", MeshTestData.Row(body));
        MeshIndexSet set = Assert.Single(mesh.IndexSets);
        Assert.Equal(4, set.IndexSize);
        Assert.Equal(2, set.Groups.Count);
        MeshGeometry geometry = mesh.ToGeometry();

        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, geometry.Indices);
    }

    [Fact]
    public void RejectsUnknownIndexFormat()
    {
        byte[] payload = MeshTestData.Payload();
        BinaryPrimitives.WriteUInt16LittleEndian(
            payload.AsSpan(MeshTestData.IndexDescriptor + 6), 4);
        byte[] body = MeshTestData.Body(payload);
        using MemoryStream stream = new(body);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => MeshDecoder.Read(stream, "TEST.DPC", MeshTestData.Row(body)));
        Assert.Contains("format 4", error.Message);
    }

    [Fact]
    public void ReadsCompactPayload()
    {
        byte[] body = MeshTestData.Body(MeshTestData.CompactPayload());
        using MemoryStream stream = new(body);

        MeshData mesh = MeshDecoder.Read(stream, "TEST.DPC", MeshTestData.Row(body));

        MeshStream vertices = Assert.Single(mesh.Streams);
        Assert.Equal(4, vertices.VertexCount);
        Assert.Equal(12, vertices.Stride);
        MeshIndexSet set = Assert.Single(mesh.IndexSets);
        Assert.Equal(6, set.IndexCount);
        MeshGeometry geometry = mesh.ToGeometry();
        Assert.Equal(new[] { 0, 1, 2, 0, 2, 3 }, geometry.Indices);
        Assert.Equal(new Vector3(1, 1, 0), geometry.Positions[2]);
    }

    [Fact]
    public void EmptyPayloadHasNoGeometry()
    {
        byte[] body = MeshTestData.Body(new byte[48]);
        using MemoryStream stream = new(body);

        MeshData mesh = MeshDecoder.Read(stream, "TEST.DPC", MeshTestData.Row(body));

        Assert.Empty(mesh.Streams);
        Assert.Empty(mesh.IndexSets);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => mesh.ToGeometry());
        Assert.Contains("no geometry", error.Message);
    }

    [Fact]
    public void RejectsBrokenGroupTable()
    {
        byte[] payload = MeshTestData.Payload();
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(MeshTestData.GroupTable + 16), 5);
        byte[] body = MeshTestData.Body(payload);
        using MemoryStream stream = new(body);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => MeshDecoder.Read(stream, "TEST.DPC", MeshTestData.Row(body)));
        Assert.Contains("group", error.Message);
    }

    [Fact]
    public void RejectsBodyNameMismatch()
    {
        byte[] body = MeshTestData.Body(MeshTestData.Payload(), name: 0x9999);
        using MemoryStream stream = new(body);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => MeshDecoder.Read(stream, "TEST.DPC", MeshTestData.Row(body)));
        Assert.Contains("body name", error.Message);
    }

    [Fact]
    public void RejectsRowEndMismatch()
    {
        byte[] body = MeshTestData.Body(MeshTestData.Payload());
        using MemoryStream stream = new(body);
        var row = MeshTestData.Row(body) with { CompressedSize = (uint)body.Length + 1 };

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => MeshDecoder.Read(stream, "TEST.DPC", row));
        Assert.Contains("payload ends", error.Message);
    }

    [Theory]
    [InlineData((ushort)1)]
    [InlineData((ushort)13)]
    public void ReadsEveryMeasuredVersion(ushort version)
    {
        byte[] body = MeshTestData.Body(MeshTestData.Payload(), version: version);
        using MemoryStream stream = new(body);

        MeshData mesh = MeshDecoder.Read(stream, "TEST.DPC", MeshTestData.Row(body));

        Assert.Equal(version, mesh.Head.Version);
        Assert.Equal(6, mesh.IndexSets[0].IndexCount);
    }

    [Fact]
    public void RejectsUnmeasuredVersion()
    {
        byte[] body = MeshTestData.Body(MeshTestData.Payload(), version: 17);
        using MemoryStream stream = new(body);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => MeshDecoder.Read(stream, "TEST.DPC", MeshTestData.Row(body)));
        Assert.Contains("version 17", error.Message);
    }

    [Fact]
    public void RejectsIndexPastVertexCount()
    {
        byte[] payload = MeshTestData.Payload();
        payload[MeshTestData.IndexData] = 9;
        byte[] body = MeshTestData.Body(payload);
        using MemoryStream stream = new(body);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => MeshDecoder.Read(stream, "TEST.DPC", MeshTestData.Row(body)));
        Assert.Contains("index 0 is 9", error.Message);
    }

    [Fact]
    public void RejectsUnmeasuredPositionStride()
    {
        byte[] body = MeshTestData.Body(MeshTestData.Payload(firstStride: 40));
        using MemoryStream stream = new(body);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => MeshDecoder.Read(stream, "TEST.DPC", MeshTestData.Row(body)));
        Assert.Contains("stride 40", error.Message);
    }

    [Fact]
    public void RejectsTruncatedPayload()
    {
        byte[] payload = MeshTestData.Payload();
        byte[] body = MeshTestData.Body(payload[..^80]);
        using MemoryStream stream = new(body);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => MeshDecoder.Read(stream, "TEST.DPC", MeshTestData.Row(body)));
        Assert.Contains("needs", error.Message);
    }

    [Fact]
    public void RejectsOversizeLz4Body()
    {
        byte[] body = MeshTestData.Body(MeshTestData.Payload(), lz4: true);
        uint huge = (uint)int.MaxValue + 1;
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(0x28), huge);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(MeshTestData.PayloadOffset), huge);
        using MemoryStream stream = new(body);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => MeshDecoder.Read(stream, "TEST.DPC", MeshTestData.Row(body)));
        Assert.Contains("too large", error.Message);
    }

    [Fact]
    public void RejectsMissingIndexSetCount()
    {
        byte[] payload = MeshTestData.Payload()[..(0x18 + 20 + 4 * 32)];
        byte[] body = MeshTestData.Body(payload);
        using MemoryStream stream = new(body);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => MeshDecoder.Read(stream, "TEST.DPC", MeshTestData.Row(body)));
        Assert.Contains("index set count", error.Message);
    }

    [Fact]
    public void RejectsNonMeshRow()
    {
        byte[] body = MeshTestData.Body(MeshTestData.Payload());
        using MemoryStream stream = new(body);
        var row = MeshTestData.Row(body) with { ClassHash = 0x1234 };

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => MeshDecoder.Read(stream, "TEST.DPC", row));
        Assert.Contains("not Mesh_Z", error.Message);
    }
}
