using System.Buffers.Binary;
using System.Numerics;
using AptStudio.Core.Meshes;
using AptStudio.Core.Skeletons;
using AptStudio.Core.Skins;
using AptStudio.Core.Tests.Skeletons;

namespace AptStudio.Core.Tests.Skins;

public sealed class SkinDeformerTests
{
    [Fact]
    public void IdentityPoseKeepsBindPositions()
    {
        SkinDeformer deformer = Build(out MeshGeometry mesh);

        MeshGeometry result = deformer.Deform(null);

        Assert.Equal(new[] { 0, 1 }, deformer.Bones.Select(bone => bone.SkeletonBone));
        Assert.Equal(mesh.Positions.Length, result.Positions.Length);
        Assert.Equal(mesh.Indices, result.Indices);
        AssertVector(new Vector3(0.5f, 0f, 0f), result.Positions[0]);
        AssertVector(new Vector3(2f, 0f, 0f), result.Positions[1]);
    }

    [Fact]
    public void RotatesChildAroundItsOwnOrigin()
    {
        SkinDeformer deformer = Build(out _);
        Dictionary<int, Quaternion> rotations = new()
        {
            [1] = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2f),
        };

        MeshGeometry result = deformer.Deform(rotations);

        AssertVector(new Vector3(0.5f, 0f, 0f), result.Positions[0]);
        AssertVector(new Vector3(1f, 1f, 0f), result.Positions[1]);
    }

    [Fact]
    public void MergesPartsWithIndexOffsets()
    {
        byte[] body = SkinTestData.BodyWithBones(
            [
                new TestPart(1, 12, 0xAA, Array.Empty<ulong>(), Rigid(0)),
                new TestPart(1, 12, 0xBB, Array.Empty<ulong>(), Rigid(1)),
            ],
            [(0x2000, 0), (0x2001, 1)]);
        SkinData skin = SkinData.Parse(
            SkinHead.Parse(SkinTestData.Header(1), "test"),
            body,
            "test",
            _ => 1);
        SkeletonData skeleton = BuildSkeleton();
        Dictionary<ulong, MeshGeometry> meshes = new()
        {
            [0x1000] = Point(new Vector3(0.5f, 0f, 0f)),
            [0x1001] = Point(new Vector3(2f, 0f, 0f)),
        };

        SkinDeformer deformer = SkinDeformer.Create(
            skin, skeleton, hash => meshes.GetValueOrDefault(hash));
        MeshGeometry result = deformer.Deform(null);

        Assert.Equal(2, result.Positions.Length);
        Assert.Equal(new[] { 0, 0, 0, 1, 1, 1 }, result.Indices);
    }

    [Fact]
    public void RejectsSkinWithoutGeometry()
    {
        SkinData skin = BuildSkin(out _);
        SkeletonData skeleton = BuildSkeleton();

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => SkinDeformer.Create(skin, skeleton, _ => null));
        Assert.Contains("no decodable geometry", error.Message);
    }

    private static SkinDeformer Build(out MeshGeometry mesh)
    {
        SkinData skin = BuildSkin(out MeshGeometry geometry);
        SkeletonData skeleton = BuildSkeleton();
        mesh = geometry;
        return SkinDeformer.Create(skin, skeleton, _ => geometry);
    }

    private static SkinData BuildSkin(out MeshGeometry mesh)
    {
        byte[] body = SkinTestData.BodyWithBones(
            [new TestPart(2, 12, 0xAA, Array.Empty<ulong>(), Rigid(0, 1))],
            [(0x2000, 0), (0x2001, 1)]);
        mesh = new MeshGeometry(
            [new Vector3(0.5f, 0f, 0f), new Vector3(2f, 0f, 0f)],
            [0, 1, 0],
            new Vector3(0.5f, 0f, 0f),
            new Vector3(2f, 0f, 0f));
        return SkinData.Parse(
            SkinHead.Parse(SkinTestData.Header(0x5678), "test"),
            body,
            "test",
            _ => 2);
    }

    private static SkeletonData BuildSkeleton()
    {
        byte[] body = SkeletonTestData.Body(
            new TestBone(0x2000),
            new TestBone(0x2001, Parent: 0, X: 1f));
        return SkeletonData.Parse(
            SkeletonHead.Parse(SkeletonTestData.Header(), "test"),
            body,
            "test");
    }

    private static MeshGeometry Point(Vector3 position) =>
        new([position], [0, 0, 0], position, position);

    private static byte[] Rigid(params uint[] slots)
    {
        byte[] data = new byte[slots.Length * 12];
        for (int index = 0; index < slots.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(index * 12), slots[index]);
            BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(index * 12 + 8), 1f);
        }

        return data;
    }

    private static void AssertVector(Vector3 expected, Vector3 actual)
    {
        Assert.True(
            Vector3.Distance(expected, actual) < 1e-5f,
            $"expected {expected}, got {actual}");
    }
}
