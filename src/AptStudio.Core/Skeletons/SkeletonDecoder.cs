// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using K4os.Compression.LZ4;

namespace AptStudio.Core.Skeletons;

public static class SkeletonDecoder
{
    public static SkeletonData Read(Stream stream, string archivePath, SkeletonHit hit)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(hit);
        string source = $"{Path.GetFileName(archivePath)} 0x{hit.NameHash:X16}";
        byte[] header = ReadExactly(stream, hit.Offset, SkeletonHead.HeaderSize, source);
        SkeletonHead head = SkeletonHead.Parse(header, source);
        if (head.NameHash != hit.NameHash
            || head.BodySize != hit.BodySize
            || head.StoredSize != hit.StoredSize
            || head.LinkSize != hit.LinkSize
            || head.Version != hit.Version)
        {
            throw new InvalidDataException(
                $"{source}: header does not match the scan record.");
        }

        byte[] payload = ReadExactly(
            stream, hit.Offset + head.PayloadOffset, (int)head.StoredSize, source);
        if (payload.Length < 8)
        {
            throw new InvalidDataException($"{source}: LZ4 payload is {payload.Length} bytes.");
        }

        uint unpacked = BinaryPrimitives.ReadUInt32LittleEndian(payload);
        uint stored = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(4));
        if (unpacked != head.BodySize || stored != head.StoredSize)
        {
            throw new InvalidDataException(
                $"{source}: LZ4 prefix {unpacked}/{stored} != header {head.BodySize}/{head.StoredSize}.");
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

        return SkeletonData.Parse(head, body, source);
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
