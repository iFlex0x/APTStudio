// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using AptStudio.Core.Containers;
using AptStudio.Core.Meshes;
using K4os.Compression.LZ4;

namespace AptStudio.Core.Tests.Meshes;

internal static class MeshTestData
{
    public const ulong Name = 0x1111;
    public const ulong Link = 0x2222;
    public const int LinkSize = 13;
    public const int PayloadOffset = 0x48;
    public const int IndexDescriptor = 0xB0;
    public const int IndexData = 0xC0;
    public const int GroupTable = IndexData + 12;
    public const int WideGroupTable = IndexData + 24;

    public static byte[] Payload(
        bool secondStream = false,
        int firstStride = 32,
        bool twoGroups = false,
        bool wideIndices = false)
    {
        int vertexCount = twoGroups ? 6 : 4;
        int groupCount = twoGroups ? 2 : 1;
        int indexSize = wideIndices ? 4 : 2;
        int size = 0x18 + 20 + vertexCount * firstStride;
        if (secondStream)
        {
            size += 20 + 4 * 20;
        }

        size += 4 + 16 + 6 * indexSize + 8 + groupCount * 40 + 24;
        byte[] payload = new byte[size];
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0x14), secondStream ? 2u : 1u);
        int at = 0x18;
        WriteStream(payload, ref at, vertexCount, firstStride, 0x0102, 0xA1);
        float[][] positions = twoGroups
            ? [[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 0, 1], [1, 0, 1], [1, 1, 1]]
            : [[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0]];
        for (int i = 0; i < positions.Length; i++)
        {
            WriteSingle(payload, at + i * firstStride, positions[i][0]);
            WriteSingle(payload, at + i * firstStride + 4, positions[i][1]);
            WriteSingle(payload, at + i * firstStride + 8, positions[i][2]);
        }

        at += vertexCount * firstStride;
        if (secondStream)
        {
            WriteStream(payload, ref at, 4, 20, 0x0502, 0xA2);
            at += 4 * 20;
        }

        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at), 1);
        at += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at), 6);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(at + 4), 0x0102);
        BinaryPrimitives.WriteUInt16LittleEndian(
            payload.AsSpan(at + 6), wideIndices ? (ushort)8 : (ushort)0);
        BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(at + 8), 0xA3);
        at += 16;
        WriteIndex(payload, at, 0, wideIndices);
        WriteIndex(payload, at + indexSize, 1, wideIndices);
        WriteIndex(payload, at + indexSize * 2, 2, wideIndices);
        WriteIndex(payload, at + indexSize * 3, 0, wideIndices);
        WriteIndex(
            payload, at + indexSize * 4, twoGroups ? (ushort)1 : (ushort)2, wideIndices);
        WriteIndex(
            payload, at + indexSize * 5, twoGroups ? (ushort)2 : (ushort)3, wideIndices);
        at += 6 * indexSize;

        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at + 4), (uint)groupCount);
        at += 8;
        for (int group = 0; group < groupCount; group++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at), 0x00020004);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at + 4), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(
                payload.AsSpan(at + 8), twoGroups ? 3u : 4u);
            BinaryPrimitives.WriteUInt32LittleEndian(
                payload.AsSpan(at + 12), twoGroups ? (uint)(group * 3) : 0);
            BinaryPrimitives.WriteUInt32LittleEndian(
                payload.AsSpan(at + 16), twoGroups ? 1u : 2u);
            BinaryPrimitives.WriteUInt32LittleEndian(
                payload.AsSpan(at + 20), twoGroups ? (uint)(group * 3) : 0);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at + 24), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at + 28), 0xFFFF0020);
            WriteSingle(payload, at + 32, 1f);
            WriteSingle(payload, at + 36, 1f);
            at += 40;
        }

        return payload;
    }

    public static byte[] CompactPayload()
    {
        byte[] payload = new byte[4 + 4 * 12 + 4 + 16 + 12 + 72];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, 4);
        float[][] positions = [[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0]];
        for (int i = 0; i < positions.Length; i++)
        {
            WriteSingle(payload, 4 + i * 12, positions[i][0]);
            WriteSingle(payload, 4 + i * 12 + 4, positions[i][1]);
            WriteSingle(payload, 4 + i * 12 + 8, positions[i][2]);
        }

        int at = 4 + 4 * 12;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at), 1);
        at += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at), 6);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(at + 4), 0x0102);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(at + 6), 0);
        BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(at + 8), 0xA4);
        at += 16;
        WriteIndex(payload, at, 0, false);
        WriteIndex(payload, at + 2, 1, false);
        WriteIndex(payload, at + 4, 2, false);
        WriteIndex(payload, at + 6, 0, false);
        WriteIndex(payload, at + 8, 2, false);
        WriteIndex(payload, at + 10, 3, false);
        at += 12;

        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at + 4), 1);
        at += 8;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at), 0x00000004);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at + 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at + 8), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at + 12), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at + 16), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at + 20), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at + 24), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at + 28), 0x24);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at + 32), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at + 36), 0);
        at += 40;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at + 4), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at + 8), 0xFFFFFFFF);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at + 12), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at + 16), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at + 20), 0);
        return payload;
    }

    public static byte[] Body(
        byte[] payload,
        ulong name = Name,
        ushort version = 11,
        bool lz4 = false,
        int linkSize = LinkSize)
    {
        int extra = version switch
        {
            9 => 4,
            >= 11 => 8,
            _ => 0,
        };
        int payloadOffset = (0x33 + extra + linkSize + 7) & ~7;
        byte[] block = [];
        int compressedSize = 0;
        if (lz4)
        {
            block = new byte[LZ4Codec.MaximumOutputSize(payload.Length)];
            compressedSize = LZ4Codec.Encode(payload, block) + 8;
        }

        byte[] body = new byte[payloadOffset + (lz4 ? compressedSize : payload.Length)];
        BinaryPrimitives.WriteUInt64LittleEndian(body, MeshHead.ClassHash);
        BinaryPrimitives.WriteUInt64LittleEndian(body.AsSpan(0x08), name);
        BinaryPrimitives.WriteUInt64LittleEndian(body.AsSpan(0x10), Link);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(0x24), (uint)linkSize);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(0x28), (uint)payload.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(0x2C), (uint)compressedSize);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(0x30), version);
        body[0x32] = lz4 ? (byte)2 : (byte)0;
        if (lz4)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(payloadOffset), (uint)payload.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(payloadOffset + 4), (uint)compressedSize);
            block.AsSpan(0, compressedSize - 8).CopyTo(body.AsSpan(payloadOffset + 8));
        }
        else
        {
            payload.CopyTo(body, payloadOffset);
        }

        return body;
    }

    public static DpcEntry Row(byte[] body, ulong name = Name) =>
        new(name, MeshHead.ClassHash, Link, 0, (uint)body.Length, 0, 0, 0, 0);

    private static void WriteStream(
        byte[] payload, ref int at, int count, int stride, ushort flags, ulong hash)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at), (uint)count);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at + 4), (uint)stride);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(at + 8), flags);
        BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(at + 12), hash);
        at += 20;
    }

    private static void WriteSingle(byte[] payload, int at, float value) =>
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(at), value);

    private static void WriteIndex(byte[] payload, int at, ushort value, bool wide)
    {
        if (wide)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(at), value);
        }
        else
        {
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(at), value);
        }
    }
}
