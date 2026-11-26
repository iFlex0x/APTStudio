using System.Numerics;
using AptStudio.Core.Meshes;
using AptStudio.Core.Skeletons;

namespace AptStudio.Core.Skins;

public sealed record SkinPaletteBone(int Slot, int SkeletonBone, ulong NameHash);

public sealed record SkinnedPart(SkinPart Part, MeshGeometry Geometry);

public sealed class SkinDeformer
{
    private const int MaxSlots = 4096;

    private readonly SkeletonData _skeleton;
    private readonly SkinnedPart[] _parts;
    private readonly SkinWeightBuffer[] _weights;
    private readonly int[] _slotToBone;
    private readonly Matrix4x4[] _bindInverse;
    private readonly SkinPaletteBone[] _bones;

    private SkinDeformer(
        SkeletonData skeleton,
        SkinnedPart[] parts,
        SkinWeightBuffer[] weights,
        int[] slotToBone,
        Matrix4x4[] bindInverse,
        SkinPaletteBone[] bones)
    {
        _skeleton = skeleton;
        _parts = parts;
        _weights = weights;
        _slotToBone = slotToBone;
        _bindInverse = bindInverse;
        _bones = bones;
    }

    public IReadOnlyList<SkinPaletteBone> Bones => _bones;

    public IReadOnlyList<SkinnedPart> Parts => _parts;

    public static SkinDeformer Create(
        SkinData skin,
        SkeletonData skeleton,
        Func<ulong, MeshGeometry?> geometry)
    {
        ArgumentNullException.ThrowIfNull(skin);
        ArgumentNullException.ThrowIfNull(skeleton);
        ArgumentNullException.ThrowIfNull(geometry);
        if (skin.Bones.Length == 0)
        {
            throw new InvalidDataException(
                $"skin 0x{skin.Head.NameHash:X16} has no bone table.");
        }

        int slots = 0;
        foreach (SkinBone bone in skin.Bones)
        {
            if (bone.Index < 0 || bone.Index >= MaxSlots)
            {
                throw new InvalidDataException(
                    $"skin 0x{skin.Head.NameHash:X16} bone slot {bone.Index} is out of range.");
            }

            slots = Math.Max(slots, bone.Index + 1);
        }

        Dictionary<ulong, int> skeletonBones = new();
        for (int index = 0; index < skeleton.Bones.Count; index++)
        {
            skeletonBones.TryAdd(skeleton.Bones[index].NameHash, index);
        }

        int[] slotToBone = new int[slots];
        Array.Fill(slotToBone, -1);
        List<SkinPaletteBone> bones = new();
        foreach (SkinBone bone in skin.Bones)
        {
            if (!skeletonBones.TryGetValue(bone.NameHash, out int target))
            {
                continue;
            }

            slotToBone[bone.Index] = target;
            bones.Add(new SkinPaletteBone(bone.Index, target, bone.NameHash));
        }

        if (bones.Count == 0)
        {
            throw new InvalidDataException(
                $"skin 0x{skin.Head.NameHash:X16} bones do not match skeleton 0x{skeleton.Head.NameHash:X16}.");
        }

        bones.Sort((left, right) => left.SkeletonBone.CompareTo(right.SkeletonBone));
        List<SkinnedPart> parts = new();
        List<SkinWeightBuffer> weights = new();
        foreach (SkinPart part in skin.Parts)
        {
            if (part.VertexCount == 0)
            {
                continue;
            }

            MeshGeometry? mesh = geometry(part.MeshHash);
            if (mesh is null || mesh.Positions.Length == 0)
            {
                continue;
            }

            if (mesh.Positions.Length != part.VertexCount)
            {
                throw new InvalidDataException(
                    $"skin 0x{skin.Head.NameHash:X16} part {part.Index}: mesh 0x{part.MeshHash:X16} has {mesh.Positions.Length} vertices, expected {part.VertexCount}.");
            }

            parts.Add(new SkinnedPart(part, mesh));
            weights.Add(skin.ReadWeights(part));
        }

        if (parts.Count == 0)
        {
            throw new InvalidDataException(
                $"skin 0x{skin.Head.NameHash:X16} has no decodable geometry.");
        }

        Matrix4x4[] bindInverse = new Matrix4x4[skeleton.Bones.Count];
        for (int index = 0; index < bindInverse.Length; index++)
        {
            if (!Matrix4x4.Invert(skeleton.Bones[index].BindWorld, out Matrix4x4 inverse))
            {
                throw new InvalidDataException(
                    $"skeleton 0x{skeleton.Head.NameHash:X16} bone {index} bind matrix is not invertible.");
            }

            bindInverse[index] = inverse;
        }

        return new SkinDeformer(
            skeleton,
            parts.ToArray(),
            weights.ToArray(),
            slotToBone,
            bindInverse,
            bones.ToArray());
    }

    public MeshGeometry Deform(IReadOnlyDictionary<int, Quaternion>? rotations) =>
        DeformCore(rotations, false);

    public MeshGeometry DeformAbsolute(IReadOnlyList<Quaternion>? rotations)
    {
        Dictionary<int, Quaternion>? map = null;
        if (rotations is not null)
        {
            map = new Dictionary<int, Quaternion>();
            for (int index = 0; index < rotations.Count; index++)
            {
                if (rotations[index] != Quaternion.Identity)
                {
                    map[index] = rotations[index];
                }
            }
        }

        return DeformCore(map, true);
    }

    private MeshGeometry DeformCore(
        IReadOnlyDictionary<int, Quaternion>? rotations,
        bool absolute)
    {
        Matrix4x4[] pose = BuildPose(rotations, absolute);
        Matrix4x4[] skinMatrices = new Matrix4x4[pose.Length];
        for (int index = 0; index < pose.Length; index++)
        {
            skinMatrices[index] = _bindInverse[index] * pose[index];
        }

        int vertexCount = 0;
        int indexCount = 0;
        foreach (SkinnedPart part in _parts)
        {
            vertexCount += part.Geometry.Positions.Length;
            indexCount += part.Geometry.Indices.Length;
        }

        Vector3[] positions = new Vector3[vertexCount];
        int[] indices = new int[indexCount];
        Vector3 min = new(float.PositiveInfinity);
        Vector3 max = new(float.NegativeInfinity);
        int vertexOffset = 0;
        int indexOffset = 0;
        for (int partIndex = 0; partIndex < _parts.Length; partIndex++)
        {
            SkinnedPart part = _parts[partIndex];
            SkinWeightBuffer weights = _weights[partIndex];
            int influences = weights.Stride switch
            {
                12 => 1,
                16 => 4,
                32 => 8,
                _ => throw new InvalidDataException(
                    $"part {part.Part.Index}: weight stride {weights.Stride} is not measured."),
            };

            for (int vertex = 0; vertex < part.Geometry.Positions.Length; vertex++)
            {
                Vector3 source = part.Geometry.Positions[vertex];
                Vector3 skinned = Vector3.Zero;
                float total = 0;
                for (int influence = 0; influence < influences; influence++)
                {
                    int at = vertex * influences + influence;
                    int slot = weights.Indices[at];
                    float weight = influences == 1 ? 1f : weights.Weights[at] / 65535f;
                    if (slot >= _slotToBone.Length || weight <= 0)
                    {
                        continue;
                    }

                    int bone = _slotToBone[slot];
                    if (bone < 0)
                    {
                        continue;
                    }

                    skinned += Vector3.Transform(source, skinMatrices[bone]) * weight;
                    total += weight;
                }

                Vector3 result = total > 0 ? skinned / total : source;
                positions[vertexOffset + vertex] = result;
                min = Vector3.Min(min, result);
                max = Vector3.Max(max, result);
            }

            int[] sourceIndices = part.Geometry.Indices;
            for (int index = 0; index < sourceIndices.Length; index++)
            {
                indices[indexOffset + index] = sourceIndices[index] + vertexOffset;
            }

            vertexOffset += part.Geometry.Positions.Length;
            indexOffset += sourceIndices.Length;
        }

        if (vertexCount == 0)
        {
            min = Vector3.Zero;
            max = Vector3.Zero;
        }

        return new MeshGeometry(positions, indices, min, max);
    }

    private Matrix4x4[] BuildPose(
        IReadOnlyDictionary<int, Quaternion>? rotations,
        bool absolute)
    {
        Matrix4x4[] pose = new Matrix4x4[_skeleton.Bones.Count];
        byte[] state = new byte[pose.Length];
        Matrix4x4 World(int index)
        {
            if (state[index] == 2)
            {
                return pose[index];
            }

            if (state[index] == 1)
            {
                throw new InvalidDataException(
                    $"skeleton 0x{_skeleton.Head.NameHash:X16} has a parent cycle at bone {index}.");
            }

            state[index] = 1;
            SkeletonBone bone = _skeleton.Bones[index];
            Matrix4x4 local = bone.ParentIndex < 0
                ? bone.BindWorld
                : bone.BindWorld * _bindInverse[bone.ParentIndex];
            bool hasRotation = false;
            Quaternion rotation = Quaternion.Identity;
            if (rotations is not null
                && rotations.TryGetValue(index, out rotation)
                && rotation != Quaternion.Identity)
            {
                hasRotation = true;
            }
            if (absolute && hasRotation)
            {
                Matrix4x4 replaced = Matrix4x4.CreateFromQuaternion(rotation);
                replaced.M41 = local.M41;
                replaced.M42 = local.M42;
                replaced.M43 = local.M43;
                local = replaced;
            }

            Matrix4x4 world = bone.ParentIndex < 0
                ? local
                : local * World(bone.ParentIndex);
            if (!absolute && hasRotation)
            {
                world = Matrix4x4.CreateFromQuaternion(rotation) * world;
            }

            pose[index] = world;
            state[index] = 2;
            return world;
        }

        for (int index = 0; index < pose.Length; index++)
        {
            World(index);
        }

        return pose;
    }
}
