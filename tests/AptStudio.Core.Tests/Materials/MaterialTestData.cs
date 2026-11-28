// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.Text;
using AptStudio.Core.Materials;
using K4os.Compression.LZ4;

namespace AptStudio.Core.Tests.Materials;

internal static class MaterialTestData
{
    public const ulong Name = 0x1234;
    public const ulong Link = 0x5678;
    public const string Code = "CODE_DIFFUSE";

    public static byte[] Body(params (int Slot, ulong Hash)[] slots)
    {
        byte[] body = new byte[0x380];
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(0x130), 5);
        int codeLength = Code.Length + 1;
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(0x18C), (uint)codeLength);
        Encoding.ASCII.GetBytes(Code).CopyTo(body, 0x190);
        int table = 0x190 + codeLength;
        ulong mask = 0;
        foreach ((int slot, ulong hash) in slots)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(body.AsSpan(table + 12 + slot * 8), hash);
            mask |= 1UL << slot;
        }

        BinaryPrimitives.WriteUInt64LittleEndian(body.AsSpan(table), mask);
        return body;
    }

    public static byte[] Object(
        byte[] body, bool lz4 = false, ulong name = Name, ulong classHash = MaterialHead.MaterialClass)
    {
        const int info = 12;
        const int pad = 9;
        byte[] payload;
        uint unpacked = (uint)body.Length;
        uint stored = 0;
        if (lz4)
        {
            byte[] block = new byte[LZ4Codec.MaximumOutputSize(body.Length)];
            int written = LZ4Codec.Encode(body, block);
            stored = (uint)(written + 8);
            payload = new byte[stored];
            BinaryPrimitives.WriteUInt32LittleEndian(payload, unpacked);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), stored);
            block.AsSpan(0, written).CopyTo(payload.AsSpan(8));
        }
        else
        {
            payload = body;
        }

        uint bodySize = (uint)(info + pad + payload.Length);
        byte[] result = new byte[0x33 + bodySize];
        BinaryPrimitives.WriteUInt64LittleEndian(result, classHash);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(8), name);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(0x10), Link);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(0x18), ulong.MaxValue);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x20), bodySize);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x24), info);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x28), unpacked);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x2C), stored);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x30), pad);
        result[0x32] = lz4 ? (byte)2 : (byte)0;
        payload.CopyTo(result, 0x33 + info + pad);
        return result;
    }

    public static string TempFile(byte[] bytes)
    {
        string path = Path.Combine(Path.GetTempPath(), $"aptstudio-material-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
