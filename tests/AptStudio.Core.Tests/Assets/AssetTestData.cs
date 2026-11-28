// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.Text;
using AptStudio.Core.Assets;
using AptStudio.Core.Containers;
using AptStudio.Core.Tests.Textures;
using AptStudio.Core.Textures;

namespace AptStudio.Core.Tests.Assets;

internal static class AssetTestData
{
    public const int FileLength = 16384;
    public const uint BitmapSize = 132;
    private const int BlockTable = 100 * 32;
    private const int MapOffset = 128 * 32;
    private const int RowCountOffset = MapOffset + 40 + 52 * 28;

    public static byte[] BuildMany()
    {
        (ulong Name, ulong Class, uint Offset, uint Size)[] rows =
        [
            (0x1111, AssetClass.Bitmap, 300, BitmapSize),
            (0x2222, AssetClass.Mesh, 310, 16),
            (0x3333, AssetClass.Skin, 320, 16),
            (0x4444, AssetClass.Animation, 330, 16),
        ];

        byte[] data = new byte[FileLength];
        WriteBanner(data);
        int header = DpcHeader.Offset;
        data[header] = 1;
        WriteU32(data, header + 1, 2);
        WriteU32(data, header + 5, 100);
        WriteU32(data, header + 9, 64);
        WriteU32(data, header + 13, 300);
        WriteU32(data, header + 17, 0);
        WriteU32(data, header + 21, 0);

        WriteU32(data, BlockTable, 1);
        WriteU32(data, BlockTable + 4 + 24, 128);
        WriteU32(data, MapOffset, 0);
        WriteU32(data, RowCountOffset, (uint)rows.Length);
        int cursor = RowCountOffset + 4;
        foreach ((ulong name, ulong cls, uint offset, uint size) in rows)
        {
            WriteU64(data, cursor, name);
            WriteU64(data, cursor + 8, cls);
            WriteU64(data, cursor + 16, 0x9999);
            WriteU32(data, cursor + 24, offset);
            WriteU32(data, cursor + 28, size);
            WriteU32(data, cursor + 32, 0);
            WriteU32(data, cursor + 36, size);
            WriteU16(data, cursor + 40, 0);
            WriteU16(data, cursor + 42, 0);
            cursor += DpcArchive.NormalEntrySize;
        }

        WriteU64(data, cursor, 0);
        WriteU32(data, cursor + 8, 0);
        WriteU32(data, cursor + 12, 0);
        WriteU32(data, cursor + 16, 0);

        byte[] bitmap = BitmapTestData.Header(4, 4, 1, 8, 0x1B, name: 0x1111);
        Array.Copy(bitmap, 0, data, 300 * 32, bitmap.Length);
        for (int i = 0; i < 12; i++)
        {
            data[300 * 32 + bitmap.Length + i] = 0xAA;
        }

        for (int i = 0; i < 4; i++)
        {
            data[310 * 32 + i] = 1;
            data[320 * 32 + i] = 2;
            data[330 * 32 + i] = 3;
        }

        return data;
    }

    public static byte[] BuildCommon(
        ulong name = 0x1111,
        ulong bodyName = 0x1111,
        uint offset = 300,
        uint size = BitmapSize)
    {
        byte[] data = new byte[FileLength];
        WriteBanner(data);
        int header = DpcHeader.Offset;
        data[header] = 2;
        WriteU32(data, header + 1, 0);
        WriteU32(data, header + 5, 0);
        WriteU32(data, header + 9, 0);
        WriteU32(data, header + 13, 0);
        WriteU32(data, header + 17, 8);
        WriteU32(data, header + 21, 128);

        int map = 128 * 32;
        WriteU32(data, map, 1);
        int record = map + 4;
        WriteU64(data, record, name);
        WriteU64(data, record + 8, name);
        WriteU64(data, record + 16, AssetClass.Bitmap);
        WriteU64(data, record + 24, 0x9999);
        WriteU32(data, record + 32, offset);
        WriteU32(data, record + 36, size);
        WriteU32(data, record + 40, 0);
        WriteU32(data, record + 44, 12);
        WriteU16(data, record + 48, 0);
        WriteU16(data, record + 50, 0);

        byte[] bitmap = BitmapTestData.Header(4, 4, 1, 8, 0x1B, name: bodyName);
        Array.Copy(bitmap, 0, data, offset * 32, bitmap.Length);
        for (int i = 0; i < 12; i++)
        {
            data[offset * 32 + bitmap.Length + i] = 0xAA;
        }

        return data;
    }

    private static void WriteBanner(byte[] data)
    {
        byte[] banner = Encoding.ASCII.GetBytes("v2.128.58.19 - test");
        Array.Copy(banner, data, banner.Length);
    }

    private static void WriteU16(byte[] data, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), value);

    private static void WriteU32(byte[] data, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);

    private static void WriteU64(byte[] data, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(offset), value);
}
