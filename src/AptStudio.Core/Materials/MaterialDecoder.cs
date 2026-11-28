// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using K4os.Compression.LZ4;

namespace AptStudio.Core.Materials;

public static class MaterialDecoder
{
    public static MaterialData Read(Stream stream, string archivePath, MaterialHit hit)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(hit);
        string source = $"{Path.GetFileName(archivePath)} 0x{hit.NameHash:X16}";
        byte[] header = ReadExactly(stream, hit.Offset, MaterialHead.HeaderSize, source);
        MaterialHead head = MaterialHead.Parse(header, source);
        if (head.NameHash != hit.NameHash)
        {
            throw new InvalidDataException(
                $"{source}: body name 0x{head.NameHash:X16} != scan name 0x{hit.NameHash:X16}.");
        }

        if (head.ClassHash != hit.ClassHash)
        {
            throw new InvalidDataException(
                $"{source}: body class 0x{head.ClassHash:X16} != scan class 0x{hit.ClassHash:X16}.");
        }

        byte[] payload = ReadExactly(
            stream,
            hit.Offset + head.PayloadOffset,
            (int)(head.Storage == MaterialStorage.Raw ? head.UnpackedSize : head.StoredSize),
            source);
        byte[] body = head.Storage == MaterialStorage.Raw
            ? payload
            : Decompress(payload, head, source);
        return MaterialData.Parse(head, body, source);
    }

    private static byte[] Decompress(byte[] payload, MaterialHead head, string source)
    {
        if (payload.Length < 8)
        {
            throw new InvalidDataException($"{source}: LZ4 payload is {payload.Length} bytes.");
        }

        uint unpacked = BinaryPrimitives.ReadUInt32LittleEndian(payload);
        uint stored = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(4));
        if (unpacked != head.UnpackedSize || stored != head.StoredSize)
        {
            throw new InvalidDataException(
                $"{source}: LZ4 prefix {unpacked}/{stored} != header {head.UnpackedSize}/{head.StoredSize}.");
        }

        if (unpacked > int.MaxValue)
        {
            throw new InvalidDataException($"{source}: unpacked size {unpacked} is too large.");
        }

        byte[] body = new byte[unpacked];
        int written;
        try
        {
            written = LZ4Codec.Decode(payload.AsSpan(8), body);
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

        return body;
    }

    private static byte[] ReadExactly(Stream stream, long offset, int count, string source)
    {
        if (count < 0)
        {
            throw new InvalidDataException($"{source}: negative read at {offset}.");
        }

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
