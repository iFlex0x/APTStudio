using System.Buffers.Binary;
using AptStudio.Core.Skins;

namespace AptStudio.Core.Tests.Skins;

public sealed class SkinDataTests
{
    [Fact]
    public void ParsesTwoParts()
    {
        byte[] body = SkinTestData.Body(
            new TestPart(3, 16, 0xAA, new[] { 0x1234UL }, SkinTestData.Weights16(3)),
            new TestPart(2, 12, 0xBB, Array.Empty<ulong>(), SkinTestData.Rigid(2)));
        SkinHead head = SkinHead.Parse(SkinTestData.Header(0x5678), "test");
        SkinData skin = SkinData.Parse(head, body, "test", hash => hash switch
        {
            0x1000 => 3,
            0x1001 => 2,
            _ => null,
        });

        Assert.Equal(2, skin.MeshLinks.Length);
        Assert.Equal(0x1001UL, skin.MeshLinks[1]);
        Assert.Equal(2, skin.Parts.Length);
        Assert.Equal(3, skin.Parts[0].VertexCount);
        Assert.Equal(16, skin.Parts[0].Stride);
        Assert.Equal(0xAAUL, skin.Parts[0].StreamHash);
        Assert.Equal(0x1234UL, skin.Parts[0].MaterialHash);
        Assert.Equal(1, skin.Parts[0].PacketCount);
        Assert.True(skin.Parts[0].PacketsParsed);
        Assert.Equal(0UL, skin.Parts[1].MaterialHash);
        Assert.False(skin.Parts[1].PacketsParsed);
        Assert.Equal(2, skin.Bones.Length);
        Assert.Equal(0x2000UL, skin.Bones[0].NameHash);
        Assert.Equal(1, skin.Bones[1].Index);
    }

    [Fact]
    public void ReadsFourInfluences()
    {
        byte[] body = SkinTestData.Body(
            new TestPart(2, 16, 0xAA, new[] { 0x1234UL }, SkinTestData.Weights16(2, 5)));
        SkinData skin = SkinData.Parse(
            SkinHead.Parse(SkinTestData.Header(1), "test"), body, "test", _ => 2);

        SkinWeightBuffer weights = skin.ReadWeights(skin.Parts[0]);
        Assert.Equal(2, weights.VertexCount);
        Assert.Equal(16, weights.Stride);
        Assert.Equal(new ushort[] { 5, 6, 0, 0 }, weights.Indices.Take(4));
        Assert.Equal(new ushort[] { 0x3FFF, 0xBFFF, 0, 0 }, weights.Weights.Take(4));
    }

    [Fact]
    public void ReadsEightInfluences()
    {
        byte[] body = SkinTestData.Body(
            new TestPart(1, 32, 0xAA, new[] { 0x1234UL }, SkinTestData.Weights32(1, 9)));
        SkinData skin = SkinData.Parse(
            SkinHead.Parse(SkinTestData.Header(1), "test"), body, "test", _ => 1);

        SkinWeightBuffer weights = skin.ReadWeights(skin.Parts[0]);
        Assert.Equal(32, weights.Stride);
        Assert.Equal(8, weights.Indices.Length);
        Assert.Equal(9, weights.Indices[0]);
        Assert.Equal(16, weights.Indices[7]);
        Assert.Equal(8, weights.Weights.Length);
        Assert.All(weights.Weights, value => Assert.Equal(0x2000, value));
    }

    [Fact]
    public void ReadsRigidBinding()
    {
        byte[] body = SkinTestData.Body(
            new TestPart(2, 12, 0xAA, new[] { 0x1234UL }, SkinTestData.Rigid(2, 4)));
        SkinData skin = SkinData.Parse(
            SkinHead.Parse(SkinTestData.Header(1), "test"), body, "test", _ => 2);

        SkinWeightBuffer weights = skin.ReadWeights(skin.Parts[0]);
        Assert.Equal(12, weights.Stride);
        Assert.Equal(new ushort[] { 4, 4 }, weights.Indices);
        Assert.Empty(weights.Weights);
    }

    [Fact]
    public void SkipsPartsWithoutGeometry()
    {
        byte[] body = SkinTestData.Body(
            new TestPart(3, 16, 0xAA, new[] { 1UL }, SkinTestData.Weights16(3)),
            new TestPart(0, 0, 0, Array.Empty<ulong>(), Array.Empty<byte>()),
            new TestPart(2, 16, 0xCC, new[] { 3UL }, SkinTestData.Weights16(2)));
        SkinData skin = SkinData.Parse(
            SkinHead.Parse(SkinTestData.Header(1), "test"), body, "test", hash => hash switch
            {
                0x1000 => 3,
                0x1001 => 0,
                0x1002 => 2,
                _ => null,
            });

        Assert.Equal(3, skin.Parts.Length);
        Assert.Equal(0, skin.Parts[1].VertexCount);
        Assert.Equal(2, skin.Parts[2].VertexCount);
        Assert.Equal(0xCCUL, skin.Parts[2].StreamHash);
    }

    [Fact]
    public void RejectsMissingVertexBlock()
    {
        byte[] body = SkinTestData.Body(
            new TestPart(3, 16, 0xAA, new[] { 1UL }, SkinTestData.Weights16(3)));
        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => SkinData.Parse(
                SkinHead.Parse(SkinTestData.Header(1), "test"), body, "test", _ => 4));
        Assert.Contains("no vertex block with 4 vertices", error.Message);
    }

    [Fact]
    public void RejectsBadMeshCount()
    {
        byte[] body = SkinTestData.Body(
            new TestPart(3, 16, 0xAA, new[] { 1UL }, SkinTestData.Weights16(3)));
        BinaryPrimitives.WriteInt32LittleEndian(body, 0);
        Assert.Throws<InvalidDataException>(
            () => SkinData.Parse(
                SkinHead.Parse(SkinTestData.Header(1), "test"), body, "test", _ => 3));
    }
}
