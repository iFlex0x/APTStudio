// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using AptStudio.Core.Containers;
using K4os.Compression.LZ4;

namespace AptStudio.Core.Meshes;

public static class MeshDecoder
{
    public static MeshData Read(Stream stream, string bodyArchivePath, DpcEntry row)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(row);
        if (row.ClassHash != MeshHead.ClassHash)
        {
            throw new InvalidDataException(
                $"{Path.GetFileName(bodyArchivePath)} 0x{row.NameHash:X16}: class is not Mesh_Z.");
        }

        string source = $"{Path.GetFileName(bodyArchivePath)} 0x{row.NameHash:X16}";
        byte[] header = ReadExactly(stream, row.BodyOffset, MeshHead.HeaderSize, source);
        MeshHead head = MeshHead.Parse(header, source);
        if (head.NameHash != row.NameHash)
        {
            throw new InvalidDataException(
                $"{source}: body name 0x{head.NameHash:X16} != row name 0x{row.NameHash:X16}.");
        }

        long rowEnd = row.BodyOffset + row.CompressedSize;
        long payloadEnd = row.BodyOffset + head.PayloadEnd;
        if (payloadEnd != rowEnd)
        {
            throw new InvalidDataException(
                $"{source}: payload ends at {payloadEnd}, row ends at {rowEnd}.");
        }

        byte[] payload;
        if (head.Storage == MeshStorage.Raw)
        {
            if (head.BodySize > int.MaxValue)
            {
                throw new InvalidDataException($"{source}: body size {head.BodySize} is too large.");
            }

            payload = ReadExactly(
                stream, row.BodyOffset + head.PayloadOffset, (int)head.BodySize, source);
        }
        else
        {
            payload = ReadLz4(stream, row.BodyOffset + head.PayloadOffset, head, source);
        }

        return MeshData.Parse(head, payload, source);
    }

    private static byte[] ReadLz4(
        Stream stream, long payloadOffset, MeshHead head, string source)
    {
        byte[] frame = ReadExactly(stream, payloadOffset, 8, source);
        uint decompressed = BinaryPrimitives.ReadUInt32LittleEndian(frame);
        uint compressed = BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(4));
        if (decompressed != head.BodySize)
        {
            throw new InvalidDataException(
                $"{source}: decompressed {decompressed} != body {head.BodySize}.");
        }

        if (decompressed > int.MaxValue)
        {
            throw new InvalidDataException(
                $"{source}: decompressed size {decompressed} is too large.");
        }

        if (compressed != head.CompressedSize)
        {
            throw new InvalidDataException(
                $"{source}: compressed {compressed} != header {head.CompressedSize}.");
        }

        if (compressed > int.MaxValue)
        {
            throw new InvalidDataException($"{source}: compressed size {compressed} is too large.");
        }

        byte[] block = ReadExactly(stream, payloadOffset + 8, (int)compressed - 8, source);
        byte[] body = new byte[decompressed];
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

        return body;
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
