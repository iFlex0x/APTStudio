// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.Text;
using AptStudio.Core.Containers;
using AptStudio.Core.Textures;

namespace AptStudio.Core.Tests.Textures;

internal static class DpcTestData
{
    private const int FileLength = 10240;
    public const int BodyOffset = 300 * 32;
    public const int SecondaryBodyOffset = 310 * 32;

    private const int BlockTable = 100 * 32;
    private const int ResourcesMap = 128 * 32;
    private const int RowCountOffset = ResourcesMap + 40 + 52 * 28;
    private const int RowsOffset = RowCountOffset + 4;

    public static byte[] Build(
        ulong classHash = BitmapHead.ClassHash,
        uint? rowSize = null,
        uint resourceBlockOffset = 300,
        uint resourceBlockSize = 64,
        BitmapStorage storage = BitmapStorage.Raw,
        uint stored = 8,
        uint compSize = 0,
        ulong name = 0x1111,
        ulong? bodyName = null,
        ushort key = 0x001B,
        int width = 4,
        int height = 4,
        int mips = 1,
        int surfaces = 1,
        BitmapKind kind = BitmapKind.Flat,
        uint? decompressed = null,
        ushort u2 = 0,
        ushort u3 = 0,
        int secondaryCount = 0,
        ulong secondaryName = 0x2222,
        uint secondaryOffset = 310)
    {
        uint payloadSize = storage == BitmapStorage.Raw ? stored + 4 : compSize;
        uint objectSize = (uint)BitmapHead.HeaderSize + payloadSize;
        uint actualRowSize = rowSize ?? objectSize;

        byte[] data = new byte[FileLength];
        byte[] banner = Encoding.ASCII.GetBytes("v2.128.58.19 - test");
        Array.Copy(banner, data, banner.Length);

        int header = DpcHeader.Offset;
        data[header] = 1;
        WriteU32(data, header + 1, 2);
        WriteU32(data, header + 5, 100);
        WriteU32(data, header + 9, resourceBlockSize);
        WriteU32(data, header + 13, resourceBlockOffset);
        WriteU32(data, header + 17, 0);
        WriteU32(data, header + 21, 0);

        WriteU32(data, BlockTable, 1);
        WriteU32(data, BlockTable + 4 + 24, 128);
        WriteU32(data, ResourcesMap, 1);
        WriteU32(data, ResourcesMap + 4, 300);
        WriteU32(data, ResourcesMap + 40, 1);
        WriteU32(data, RowCountOffset, 1);

        int row = RowsOffset;
        WriteU64(data, row, name);
        WriteU64(data, row + 8, classHash);
        WriteU64(data, row + 16, 0x3333);
        WriteU32(data, row + 24, 300);
        WriteU32(data, row + 28, actualRowSize);
        WriteU32(data, row + 32, 0);
        WriteU32(data, row + 36, decompressed ?? stored + 4);
        WriteU16(data, row + 40, u2);
        WriteU16(data, row + 42, u3);

        int rowsEnd = RowsOffset + (int)DpcArchive.NormalEntrySize;
        WriteU64(data, rowsEnd, 0);
        WriteU32(data, rowsEnd + 8, 0);
        WriteU32(data, rowsEnd + 12, 0);
        WriteU32(data, rowsEnd + 16, (uint)secondaryCount);
        for (int i = 0; i < secondaryCount; i++)
        {
            int record = rowsEnd + 20 + i * 45;
            WriteU64(data, record, secondaryName);
            WriteU64(data, record + 8, classHash);
            WriteU64(data, record + 16, 0x4444);
            WriteU32(data, record + 24, secondaryOffset);
            WriteU32(data, record + 28, objectSize);
            WriteU32(data, record + 32, 0);
            WriteU32(data, record + 36, decompressed ?? stored + 4);
            WriteU16(data, record + 40, 0);
            WriteU16(data, record + 42, 0);
        }

        byte[] bitmap = BitmapTestData.Header(
            width, height, mips, stored, key,
            kind: kind, storage: storage, surfaces: surfaces, compressed: compSize, name: bodyName ?? name);
        Array.Copy(bitmap, 0, data, BodyOffset, bitmap.Length);

        int payloadLength = Math.Max(0, (int)(actualRowSize - BitmapHead.HeaderSize));
        for (int i = 0; i < payloadLength && BodyOffset + BitmapHead.HeaderSize + i < data.Length; i++)
        {
            data[BodyOffset + BitmapHead.HeaderSize + i] = 0xAA;
        }

        if (secondaryCount > 0 && secondaryOffset != 300)
        {
            byte[] secondary = BitmapTestData.Header(
                width, height, mips, stored, key,
                kind: kind, storage: storage, surfaces: surfaces, compressed: compSize, name: secondaryName);
            int secondaryBody = (int)secondaryOffset * 32;
            Array.Copy(secondary, 0, data, secondaryBody, secondary.Length);
            int secondaryPayload = Math.Max(0, (int)(objectSize - BitmapHead.HeaderSize));
            for (int i = 0; i < secondaryPayload && secondaryBody + BitmapHead.HeaderSize + i < data.Length; i++)
            {
                data[secondaryBody + BitmapHead.HeaderSize + i] = 0xAA;
            }
        }

        return data;
    }

    public const uint BlockBodySize = 132;

    public static byte[] BuildTwoBlocks(uint duplicateCompressed = BlockBodySize)
    {
        byte[] data = new byte[FileLength];
        byte[] banner = Encoding.ASCII.GetBytes("v2.128.58.19 - test");
        Array.Copy(banner, data, banner.Length);

        int header = DpcHeader.Offset;
        data[header] = 1;
        WriteU32(data, header + 1, 2);
        WriteU32(data, header + 5, 100);
        WriteU32(data, header + 9, 4096);
        WriteU32(data, header + 13, 300);
        WriteU32(data, header + 17, 0);
        WriteU32(data, header + 21, 0);

        WriteU32(data, BlockTable, 2);
        WriteU32(data, BlockTable + 4 + 24, 128);
        WriteU32(data, BlockTable + 4 + 32 + 24, 200);

        WriteMap(
            data,
            128 * 32,
            [(0x1111UL, 300u, BlockBodySize)],
            [(0x2222UL, 305u, BlockBodySize), (0x4444UL, 315u, BlockBodySize)],
            firstCount: 2,
            secondCount: 3);
        WriteMap(
            data,
            200 * 32,
            [(0x3333UL, 310u, BlockBodySize)],
            [(0x1111UL, 300u, duplicateCompressed)]);

        WriteBitmap(data, 300, 0x1111);
        WriteBitmap(data, 305, 0x2222);
        WriteBitmap(data, 310, 0x3333);
        WriteBitmap(data, 315, 0x4444);
        return data;
    }

    private static void WriteMap(
        byte[] data,
        int mapOffset,
        (ulong Name, uint Offset, uint Compressed)[] rows,
        (ulong Name, uint Offset, uint Compressed)[] secondary,
        uint firstCount = 0,
        uint secondCount = 0)
    {
        WriteU32(data, mapOffset, 0);
        WriteU32(data, mapOffset + 1496, (uint)rows.Length);
        int cursor = mapOffset + 1500;
        foreach ((ulong name, uint offset, uint compressed) in rows)
        {
            WriteRow(data, cursor, name, offset, compressed);
            cursor += DpcArchive.NormalEntrySize;
        }

        int rowsEnd = cursor;
        WriteU64(data, rowsEnd, 0);
        cursor = rowsEnd + 8;
        WriteU32(data, cursor, firstCount);
        cursor += 4 + (int)firstCount * 16;
        WriteU32(data, cursor, secondCount);
        cursor += 4 + (int)secondCount * 4;
        WriteU32(data, cursor, (uint)secondary.Length);
        cursor += 4;
        foreach ((ulong name, uint offset, uint compressed) in secondary)
        {
            WriteU64(data, cursor, name);
            WriteU64(data, cursor + 8, BitmapHead.ClassHash);
            WriteU64(data, cursor + 16, 0x4444);
            WriteU32(data, cursor + 24, offset);
            WriteU32(data, cursor + 28, compressed);
            WriteU32(data, cursor + 32, 0);
            WriteU32(data, cursor + 36, 12);
            WriteU16(data, cursor + 40, 0);
            WriteU16(data, cursor + 42, 0);
            cursor += DpcArchive.SecondaryEntrySize;
        }
    }

    private static void WriteRow(byte[] data, int at, ulong name, uint offset, uint compressed)
    {
        WriteU64(data, at, name);
        WriteU64(data, at + 8, BitmapHead.ClassHash);
        WriteU64(data, at + 16, 0x3333);
        WriteU32(data, at + 24, offset);
        WriteU32(data, at + 28, compressed);
        WriteU32(data, at + 32, 0);
        WriteU32(data, at + 36, 12);
        WriteU16(data, at + 40, 0);
        WriteU16(data, at + 42, 0);
    }

    private static void WriteBitmap(byte[] data, uint offset, ulong name)
    {
        int at = (int)offset * 32;
        byte[] bitmap = BitmapTestData.Header(4, 4, 1, 8, 0x001B, name: name);
        Array.Copy(bitmap, 0, data, at, bitmap.Length);
        for (int i = 0; i < 12; i++)
        {
            data[at + BitmapHead.HeaderSize + i] = 0xAA;
        }
    }

    private static void WriteU16(byte[] data, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), value);

    private static void WriteU32(byte[] data, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);

    private static void WriteU64(byte[] data, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(offset), value);
}
