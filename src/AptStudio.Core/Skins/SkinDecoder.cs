// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using AptStudio.Core.Containers;
using K4os.Compression.LZ4;

namespace AptStudio.Core.Skins;

public static class SkinDecoder
{
    public static SkinData Read(
        Stream stream,
        string bodyArchivePath,
        DpcEntry row,
        Func<ulong, int?> vertexCount)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(row);
        if (row.ClassHash != SkinHead.SkinClass)
        {
            throw new InvalidDataException(
                $"{Path.GetFileName(bodyArchivePath)} 0x{row.NameHash:X16}: class is not Skin_Z.");
        }

        string source = $"{Path.GetFileName(bodyArchivePath)} 0x{row.NameHash:X16}";
        byte[] header = ReadExactly(stream, row.BodyOffset, SkinHead.HeaderSize, source);
        SkinHead head = SkinHead.Parse(header, source);
        if (head.NameHash != row.NameHash)
        {
            throw new InvalidDataException(
                $"{source}: body name 0x{head.NameHash:X16} != row name 0x{row.NameHash:X16}.");
        }

        long payloadAt = row.BodyOffset + head.PayloadOffset;
        byte[] frame = ReadExactly(stream, payloadAt, 8, source);
        uint unpacked = BinaryPrimitives.ReadUInt32LittleEndian(frame);
        uint stored = BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(4));
        if (unpacked != head.BodySize || stored != head.StoredSize)
        {
            throw new InvalidDataException(
                $"{source}: LZ4 prefix {unpacked}/{stored} != header {head.BodySize}/{head.StoredSize}.");
        }

        if (unpacked > int.MaxValue || stored > int.MaxValue)
        {
            throw new InvalidDataException(
                $"{source}: body sizes {unpacked}/{stored} are too large.");
        }

        byte[] block = ReadExactly(stream, payloadAt + 8, (int)stored - 8, source);
        byte[] body = new byte[unpacked];
        int written;
        try
        {
            written = LZ4Codec.Decode(block, body);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            throw new InvalidDataException($"{source}: LZ4 block is invalid.", ex);
        }

        if (written != body.Length)
        {
            throw new InvalidDataException(
                $"{source}: LZ4 wrote {written}, expected {body.Length}.");
        }

        return SkinData.Parse(head, body, source, vertexCount);
    }

    private static byte[] ReadExactly(Stream stream, long offset, int count, string source)
    {
        byte[] buffer = new byte[count];
        stream.Seek(offset, SeekOrigin.Begin);
        int read = 0;
        while (read < count)
        {
            int n = stream.Read(buffer, read, count - read);
            if (n == 0)
            {
                throw new InvalidDataException($"{source}: short read at {offset}.");
            }

            read += n;
        }

        return buffer;
    }
}
