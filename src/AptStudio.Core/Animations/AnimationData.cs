// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;

namespace AptStudio.Core.Animations;

public enum AnimationEncoding
{
    Packed,
    Frames,
}

public sealed record AnimationBone(ulong NameHash, uint BitOffset, uint BitLength);

public sealed record AnimationData(
    AnimationHead Head,
    AnimationEncoding Encoding,
    float Duration,
    uint KeyCount,
    uint PackedBits,
    ushort Flags,
    int PackedStreamOffset,
    IReadOnlyList<AnimationBone> Bones,
    int FrameEntries,
    int FrameTracks,
    float LinkDuration)
{
    public const int FooterSize = 14;

    public int BaseBones => Bones.Count(bone => bone.BitLength == BaseBlockBits);

    public int AnimatedBones => Bones.Count(bone => bone.BitLength > BaseBlockBits);

    public const uint BaseBlockBits = 142;

    public static AnimationData Parse(
        AnimationHead head,
        byte[] body,
        byte[]? linkBlock,
        string source)
    {
        ArgumentNullException.ThrowIfNull(head);
        ArgumentNullException.ThrowIfNull(body);
        if (body.Length < FooterSize)
        {
            throw new InvalidDataException(
                $"{source}: body is {body.Length} bytes, need {FooterSize}.");
        }

        int footer = body.Length - FooterSize;
        uint packedBits = BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(footer));
        float duration = BinaryPrimitives.ReadSingleLittleEndian(body.AsSpan(footer + 4));
        uint keyCount = BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(footer + 8));
        ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(footer + 12));
        float linkDuration = ReadLinkDuration(linkBlock);

        AnimationEncoding encoding = packedBits > 0 ? AnimationEncoding.Packed : AnimationEncoding.Frames;
        if (duration < 0 || keyCount == uint.MaxValue)
        {
            encoding = AnimationEncoding.Frames;
            duration = linkDuration;
        }

        int stream = packedBits > 0 ? FindPackedStream(body, footer, packedBits) : -1;
        IReadOnlyList<AnimationBone> bones = packedBits > 0
            ? ReadBones(body, stream > 0 ? stream : footer, packedBits)
            : Array.Empty<AnimationBone>();
        (int frameEntries, int frameTracks) = encoding == AnimationEncoding.Frames
            ? ReadFrameTable(body, duration)
            : (0, 0);
        if (keyCount == uint.MaxValue)
        {
            keyCount = frameEntries >= frameTracks
                ? (uint)(frameEntries - frameTracks)
                : 0;
        }

        return new AnimationData(
            head,
            encoding,
            duration,
            keyCount,
            packedBits,
            flags,
            stream,
            bones,
            frameEntries,
            frameTracks,
            linkDuration);
    }

    private static float ReadLinkDuration(byte[]? linkBlock)
    {
        if (linkBlock is null || linkBlock.Length < 16)
        {
            return 0f;
        }

        float duration = BinaryPrimitives.ReadSingleLittleEndian(linkBlock.AsSpan(12));
        return float.IsFinite(duration) && duration >= 0 ? duration : 0f;
    }

    private static int FindPackedStream(byte[] body, int footer, uint packedBits)
    {
        long expected = footer - ((packedBits + 7L) / 8);
        return expected >= 0 && expected < footer ? (int)expected : -1;
    }

    private static IReadOnlyList<AnimationBone> ReadBones(byte[] body, int limit, uint packedBits)
    {
        limit = Math.Min(limit, body.Length - FooterSize);
        if (limit < 16)
        {
            return Array.Empty<AnimationBone>();
        }

        int bestAt = -1;
        int bestCount = 0;
        for (int at = 0; at + 4 <= limit; at += 2)
        {
            uint count = BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(at));
            if (count is 0 or > 4096)
            {
                continue;
            }

            long end = at + 4L + count * 12L;
            if (end > limit)
            {
                continue;
            }

            if (BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(at + 12)) != 0)
            {
                continue;
            }

            bool valid = true;
            uint previous = 0;
            for (int i = 0; i < count; i++)
            {
                int record = at + 4 + i * 12;
                ulong hash = BinaryPrimitives.ReadUInt64LittleEndian(body.AsSpan(record));
                uint offset = BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(record + 8));
                if (hash == 0 || offset < previous || offset >= packedBits)
                {
                    valid = false;
                    break;
                }

                previous = offset;
            }

            if (valid && count > bestCount)
            {
                bestAt = at;
                bestCount = (int)count;
            }
        }

        if (bestAt < 0)
        {
            return Array.Empty<AnimationBone>();
        }

        AnimationBone[] bones = new AnimationBone[bestCount];
        for (int i = 0; i < bestCount; i++)
        {
            int record = bestAt + 4 + i * 12;
            ulong hash = BinaryPrimitives.ReadUInt64LittleEndian(body.AsSpan(record));
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(record + 8));
            uint next = i + 1 < bestCount
                ? BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(record + 20))
                : packedBits;
            bones[i] = new AnimationBone(hash, offset, next - offset);
        }

        return bones;
    }

    private static (int Entries, int Tracks) ReadFrameTable(byte[] body, float duration)
    {
        int limit = Math.Min(body.Length - FooterSize, 4096);
        int tries = 0;
        for (int at = 0; at + 4 <= limit && tries < 16; at += 4)
        {
            uint count = BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(at));
            if (count is 0 or > 200_000 || at + 4L + count * 2L > body.Length - FooterSize)
            {
                continue;
            }

            tries++;
            ushort max = 0;
            for (int i = 0; i < count; i++)
            {
                ushort value = BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(at + 4 + i * 2));
                if (value > max)
                {
                    max = value;
                }
            }

            float expected = duration * 60f;
            if (max == 0 || Math.Abs(max - expected) > 2f)
            {
                continue;
            }

            int tracks = 0;
            for (int i = 0; i < count; i++)
            {
                if (BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(at + 4 + i * 2)) == max)
                {
                    tracks++;
                }
            }

            return ((int)count, tracks);
        }

        return (0, 0);
    }
}
