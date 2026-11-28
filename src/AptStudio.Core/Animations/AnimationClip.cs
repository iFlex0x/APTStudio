// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Numerics;
using AptStudio.Core.Skeletons;

namespace AptStudio.Core.Animations;

public sealed record AnimationClipBone(
    ulong NameHash,
    int ValueBits,
    IReadOnlyList<Quaternion> Rotations);

public sealed record AnimationClip(
    float Duration,
    uint KeyCount,
    int GroupCount,
    int FrameCount,
    IReadOnlyList<AnimationClipBone> Bones,
    int UnreadableBones);

public static class AnimationClipReader
{
    private const int BaseBits = 48;
    private const int TrailerBits = 94;
    private const int MaxValueBits = 24;

    public static AnimationClip Read(AnimationData data, byte[] body)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(body);
        if (data.Encoding != AnimationEncoding.Packed || data.PackedStreamOffset < 0)
        {
            throw new InvalidDataException(
                $"animation 0x{data.Head.NameHash:X16}: no packed rotation stream.");
        }

        long keys = data.KeyCount;
        if (keys <= 0 || keys > 100_000)
        {
            throw new InvalidDataException(
                $"animation 0x{data.Head.NameHash:X16}: key count {keys} is out of range.");
        }

        int groups = (int)((keys + 31) / 32);
        int frameCount = (int)(keys - groups + 1);
        if (frameCount <= 0)
        {
            throw new InvalidDataException(
                $"animation 0x{data.Head.NameHash:X16}: {keys} keys in {groups} groups.");
        }

        long streamBit = data.PackedStreamOffset * 8L;
        long streamEnd = streamBit + data.PackedBits;
        long slots = 3L * (keys - groups);
        List<AnimationClipBone> bones = new();
        int unreadable = 0;
        foreach (AnimationBone bone in data.Bones)
        {
            long end = streamBit + bone.BitOffset + bone.BitLength;
            if (end > streamEnd || bone.BitLength < BaseBits)
            {
                unreadable++;
                continue;
            }

            if (bone.BitLength == AnimationData.BaseBlockBits)
            {
                bones.Add(new AnimationClipBone(
                    bone.NameHash,
                    0,
                    [ReadQuat(body, streamBit + bone.BitOffset)]));
                continue;
            }

            long valueBits = bone.BitLength - 48L * groups - TrailerBits;
            if (valueBits < 0 || slots <= 0 || valueBits % slots != 0)
            {
                unreadable++;
                continue;
            }

            int bits = (int)(valueBits / slots);
            if (bits < 2 || bits > MaxValueBits)
            {
                unreadable++;
                continue;
            }

            long at = streamBit + bone.BitOffset;
            int x = (int)ReadBits(body, at, 16);
            int y = (int)ReadBits(body, at + 16, 16);
            int z = (int)ReadBits(body, at + 32, 16);
            List<Quaternion> rotations = new(frameCount) { BuildQuat(x, y, z) };
            long valueAt = at + BaseBits;
            int magnitudeMask = (1 << (bits - 1)) - 1;
            int signBit = 1 << (bits - 1);
            for (long frame = 0; frame < keys - groups; frame++)
            {
                for (int component = 0; component < 3; component++)
                {
                    int field = (int)ReadBits(body, valueAt, bits);
                    valueAt += bits;
                    int delta = (field & signBit) != 0
                        ? -(field & magnitudeMask)
                        : field & magnitudeMask;
                    switch (component)
                    {
                        case 0:
                            x += delta;
                            break;
                        case 1:
                            y += delta;
                            break;
                        default:
                            z += delta;
                            break;
                    }
                }

                rotations.Add(BuildQuat(x, y, z));
            }

            bones.Add(new AnimationClipBone(bone.NameHash, bits, rotations));
        }

        if (bones.Count == 0)
        {
            throw new InvalidDataException(
                $"animation 0x{data.Head.NameHash:X16}: no readable rotation blocks.");
        }

        return new AnimationClip(
            data.Duration,
            data.KeyCount,
            groups,
            frameCount,
            bones,
            unreadable);
    }

    private static Quaternion ReadQuat(byte[] body, long bit)
    {
        int x = (int)ReadBits(body, bit, 16);
        int y = (int)ReadBits(body, bit + 16, 16);
        int z = (int)ReadBits(body, bit + 32, 16);
        return BuildQuat(x, y, z);
    }

    private static Quaternion BuildQuat(int x, int y, int z)
    {
        double cx = x * 2.0 / 65535.0 - 1.0;
        double cy = y * 2.0 / 65535.0 - 1.0;
        double cz = z * 2.0 / 65535.0 - 1.0;
        double w = Math.Sqrt(Math.Abs(1.0 - cx * cx - cy * cy - cz * cz));
        double length = Math.Sqrt(cx * cx + cy * cy + cz * cz + w * w);
        if (length <= 1e-12)
        {
            return Quaternion.Identity;
        }

        return new Quaternion(
            (float)(cx / length),
            (float)(cy / length),
            (float)(cz / length),
            (float)(w / length));
    }

    private static uint ReadBits(byte[] body, long bit, int count)
    {
        ulong value = 0;
        int shift = 0;
        long at = bit >> 3;
        int offset = (int)(bit & 7);
        while (shift < count)
        {
            if (at < 0 || at >= body.Length)
            {
                throw new InvalidDataException("animation bit read is out of range.");
            }

            int take = Math.Min(8 - offset, count - shift);
            int mask = (1 << take) - 1;
            value |= (ulong)((body[at] >> offset) & mask) << shift;
            shift += take;
            at++;
            offset = 0;
        }

        return (uint)value;
    }
}

public sealed class AnimationClipBinding
{
    private readonly AnimationClip _clip;
    private readonly int[] _tracks;

    private AnimationClipBinding(AnimationClip clip, int[] tracks)
    {
        _clip = clip;
        _tracks = tracks;
    }

    public int FrameCount => _clip.FrameCount;

    public int MappedBones => _tracks.Count(track => track >= 0);

    public static AnimationClipBinding Create(AnimationClip clip, SkeletonData skeleton)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(skeleton);
        Dictionary<ulong, int> tracks = new();
        for (int index = 0; index < clip.Bones.Count; index++)
        {
            tracks.TryAdd(clip.Bones[index].NameHash, index);
        }

        int[] mapped = new int[skeleton.Bones.Count];
        Array.Fill(mapped, -1);
        for (int index = 0; index < mapped.Length; index++)
        {
            if (tracks.TryGetValue(skeleton.Bones[index].NameHash, out int track))
            {
                mapped[index] = track;
            }
        }

        return new AnimationClipBinding(clip, mapped);
    }

    public Quaternion[] Sample(int frame)
    {
        int at = Math.Clamp(frame, 0, _clip.FrameCount - 1);
        Quaternion[] rotations = new Quaternion[_tracks.Length];
        for (int index = 0; index < rotations.Length; index++)
        {
            if (_tracks[index] < 0)
            {
                rotations[index] = Quaternion.Identity;
                continue;
            }

            IReadOnlyList<Quaternion> track = _clip.Bones[_tracks[index]].Rotations;
            rotations[index] = track[Math.Min(at, track.Count - 1)];
        }

        return rotations;
    }
}
