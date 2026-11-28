// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.Numerics;

namespace AptStudio.Core.Skeletons;

public sealed record SkeletonBone(
    ulong NameHash,
    ulong OwnerHash,
    int ParentIndex,
    int FirstChildIndex,
    Matrix4x4 BindWorld)
{
    public Vector3 Position => new(BindWorld.M41, BindWorld.M42, BindWorld.M43);
}

public sealed record SkeletonData(
    SkeletonHead Head,
    IReadOnlyList<SkeletonBone> Bones,
    int TailOffset,
    int TailLength)
{
    public const int BoneRecordSize = 296;
    public const int MaxBones = 4096;

    public static SkeletonData Parse(SkeletonHead head, byte[] body, string source)
    {
        ArgumentNullException.ThrowIfNull(head);
        ArgumentNullException.ThrowIfNull(body);
        if (body.Length < 4)
        {
            throw new InvalidDataException($"{source}: skeleton body is {body.Length} bytes.");
        }

        int count = BinaryPrimitives.ReadInt32LittleEndian(body);
        if (count is < 1 or > MaxBones)
        {
            throw new InvalidDataException($"{source}: bone count {count} is out of range.");
        }

        long recordsEnd = 4L + (long)count * BoneRecordSize;
        if (recordsEnd > body.Length)
        {
            throw new InvalidDataException(
                $"{source}: {count} bone records do not fit in {body.Length} bytes.");
        }

        SkeletonBone[] bones = new SkeletonBone[count];
        for (int index = 0; index < count; index++)
        {
            int at = 4 + index * BoneRecordSize;
            ulong nameHash = BinaryPrimitives.ReadUInt64LittleEndian(body.AsSpan(at + 0xC4));
            ulong ownerHash = BinaryPrimitives.ReadUInt64LittleEndian(body.AsSpan(at + 0xCC));
            int parent = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(at + 0x120));
            int firstChild = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(at + 0x124));
            if (nameHash == 0 || ownerHash != head.NameHash)
            {
                throw new InvalidDataException(
                    $"{source}: bone {index} name or owner does not match the skeleton.");
            }

            if (parent < -1 || parent >= count || firstChild < -1 || firstChild >= count)
            {
                throw new InvalidDataException(
                    $"{source}: bone {index} parent {parent} or first child {firstChild} is out of range.");
            }

            float m00 = BinaryPrimitives.ReadSingleLittleEndian(body.AsSpan(at + 0xD4));
            float m01 = BinaryPrimitives.ReadSingleLittleEndian(body.AsSpan(at + 0xD8));
            float m02 = BinaryPrimitives.ReadSingleLittleEndian(body.AsSpan(at + 0xDC));
            float m03 = BinaryPrimitives.ReadSingleLittleEndian(body.AsSpan(at + 0xE0));
            float m10 = BinaryPrimitives.ReadSingleLittleEndian(body.AsSpan(at + 0xE4));
            float m11 = BinaryPrimitives.ReadSingleLittleEndian(body.AsSpan(at + 0xE8));
            float m12 = BinaryPrimitives.ReadSingleLittleEndian(body.AsSpan(at + 0xEC));
            float m13 = BinaryPrimitives.ReadSingleLittleEndian(body.AsSpan(at + 0xF0));
            float m20 = BinaryPrimitives.ReadSingleLittleEndian(body.AsSpan(at + 0xF4));
            float m21 = BinaryPrimitives.ReadSingleLittleEndian(body.AsSpan(at + 0xF8));
            float m22 = BinaryPrimitives.ReadSingleLittleEndian(body.AsSpan(at + 0xFC));
            float m23 = BinaryPrimitives.ReadSingleLittleEndian(body.AsSpan(at + 0x100));
            float m30 = BinaryPrimitives.ReadSingleLittleEndian(body.AsSpan(at + 0x104));
            float m31 = BinaryPrimitives.ReadSingleLittleEndian(body.AsSpan(at + 0x108));
            float m32 = BinaryPrimitives.ReadSingleLittleEndian(body.AsSpan(at + 0x10C));
            float m33 = BinaryPrimitives.ReadSingleLittleEndian(body.AsSpan(at + 0x110));
            if (Math.Abs(m03) > 1e-3f || Math.Abs(m13) > 1e-3f
                || Math.Abs(m23) > 1e-3f || Math.Abs(m33 - 1f) > 1e-3f)
            {
                throw new InvalidDataException(
                    $"{source}: bone {index} bind matrix is not affine.");
            }

            bones[index] = new SkeletonBone(
                nameHash,
                ownerHash,
                parent,
                firstChild,
                new Matrix4x4(
                    m00, m01, m02, m03,
                    m10, m11, m12, m13,
                    m20, m21, m22, m23,
                    m30, m31, m32, m33));
        }

        return new SkeletonData(
            head,
            bones,
            (int)recordsEnd,
            body.Length - (int)recordsEnd);
    }

    public SkeletonBone? Find(ulong nameHash)
    {
        foreach (SkeletonBone bone in Bones)
        {
            if (bone.NameHash == nameHash)
            {
                return bone;
            }
        }

        return null;
    }
}
