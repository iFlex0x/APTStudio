using System.Buffers.Binary;
using System.Numerics;
using AptStudio.Core.Skeletons;

namespace AptStudio.Core.Tests.Skeletons;

public sealed class SkeletonDataTests
{
    [Fact]
    public void ReadsBonesHierarchyAndTail()
    {
        byte[] body = SkeletonTestData.Body(
            tail: 8,
            new TestBone(0x11, Parent: -1, FirstChild: 1, X: 1f, Y: 2f, Z: 3f),
            new TestBone(0x22, Parent: 0, FirstChild: 2, X: 4f, Y: 5f, Z: 6f),
            new TestBone(0x33, Parent: 1, FirstChild: -1, X: 7f, Y: 8f, Z: 9f));
        SkeletonHead head = SkeletonHead.Parse(
            SkeletonTestData.Header(bodySize: (uint)body.Length, storedSize: (uint)body.Length),
            "test");

        SkeletonData data = SkeletonData.Parse(head, body, "test");

        Assert.Equal(3, data.Bones.Count);
        Assert.Equal(0x11UL, data.Bones[0].NameHash);
        Assert.Equal(-1, data.Bones[0].ParentIndex);
        Assert.Equal(1, data.Bones[0].FirstChildIndex);
        Assert.Equal(new Vector3(1f, 2f, 3f), data.Bones[0].Position);
        Assert.Equal(1, data.Bones[2].ParentIndex);
        Assert.Equal(new Vector3(7f, 8f, 9f), data.Bones[2].Position);
        Assert.Equal(4 + 3 * SkeletonData.BoneRecordSize, data.TailOffset);
        Assert.Equal(8, data.TailLength);
        Assert.Equal(4f, data.Bones[1].BindWorld.M41);
        Assert.Equal(5f, data.Bones[1].BindWorld.M42);
        Assert.Equal(6f, data.Bones[1].BindWorld.M43);
    }

    [Fact]
    public void FindsBoneByName()
    {
        byte[] body = SkeletonTestData.Body(
            new TestBone(0x11),
            new TestBone(0x22, Parent: 0));
        SkeletonHead head = SkeletonHead.Parse(
            SkeletonTestData.Header(bodySize: (uint)body.Length, storedSize: (uint)body.Length),
            "test");

        SkeletonData data = SkeletonData.Parse(head, body, "test");

        Assert.Equal(0x22UL, data.Find(0x22)?.NameHash);
        Assert.Null(data.Find(0x44));
    }

    [Fact]
    public void RejectsWrongOwner()
    {
        byte[] body = SkeletonTestData.Body(0x9999, 8, new TestBone(0x11));
        SkeletonHead head = SkeletonHead.Parse(
            SkeletonTestData.Header(bodySize: (uint)body.Length, storedSize: (uint)body.Length),
            "test");

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => SkeletonData.Parse(head, body, "test"));
        Assert.Contains("owner", error.Message);
    }

    [Fact]
    public void RejectsParentOutOfRange()
    {
        byte[] body = SkeletonTestData.Body(new TestBone(0x11, Parent: 5));
        SkeletonHead head = SkeletonHead.Parse(
            SkeletonTestData.Header(bodySize: (uint)body.Length, storedSize: (uint)body.Length),
            "test");

        Assert.Throws<InvalidDataException>(() => SkeletonData.Parse(head, body, "test"));
    }

    [Fact]
    public void RejectsNonAffineMatrix()
    {
        byte[] body = SkeletonTestData.Body(new TestBone(0x11));
        BinaryPrimitives.WriteSingleLittleEndian(
            body.AsSpan(4 + 0xD4 + 60), 2f);
        SkeletonHead head = SkeletonHead.Parse(
            SkeletonTestData.Header(bodySize: (uint)body.Length, storedSize: (uint)body.Length),
            "test");

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => SkeletonData.Parse(head, body, "test"));
        Assert.Contains("affine", error.Message);
    }

    [Fact]
    public void RejectsShortAndEmptyBodies()
    {
        SkeletonHead head = SkeletonHead.Parse(SkeletonTestData.Header(), "test");

        Assert.Throws<InvalidDataException>(
            () => SkeletonData.Parse(head, [0, 0], "test"));
        Assert.Throws<InvalidDataException>(
            () => SkeletonData.Parse(head, [0, 0, 0, 0], "test"));
    }

    [Fact]
    public void DecoderRoundTrips()
    {
        byte[] body = SkeletonTestData.Body(
            new TestBone(0x11, X: 1f),
            new TestBone(0x22, Parent: 0, X: 2f));
        byte[] obj = SkeletonTestData.Object(body);

        SkeletonData data = SkeletonDecoder.Read(
            new MemoryStream(obj), "test", SkeletonTestData.Hit(obj));

        Assert.Equal(SkeletonTestData.Name, data.Head.NameHash);
        Assert.Equal(2, data.Bones.Count);
        Assert.Equal(2f, data.Bones[1].Position.X);
    }
}
