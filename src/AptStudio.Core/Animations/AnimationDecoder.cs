using System.Buffers.Binary;
using AptStudio.Core.Containers;
using K4os.Compression.LZ4;

namespace AptStudio.Core.Animations;

public static class AnimationDecoder
{
    public static AnimationData Read(Stream stream, string bodyArchivePath, DpcEntry row)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(row);
        if (row.ClassHash != AnimationHead.AnimationClass)
        {
            throw new InvalidDataException(
                $"{Path.GetFileName(bodyArchivePath)} 0x{row.NameHash:X16}: class is not Animation_Z.");
        }

        string source = $"{Path.GetFileName(bodyArchivePath)} 0x{row.NameHash:X16}";
        byte[] header = ReadExactly(stream, row.BodyOffset, AnimationHead.HeaderSize, source);
        AnimationHead head = AnimationHead.Parse(header, source);
        if (head.NameHash != row.NameHash)
        {
            throw new InvalidDataException(
                $"{source}: body name 0x{head.NameHash:X16} != row name 0x{row.NameHash:X16}.");
        }

        long payloadAt = row.BodyOffset + head.PayloadOffset;
        if (head.LinkSize > int.MaxValue)
        {
            throw new InvalidDataException($"{source}: link block {head.LinkSize} is too large.");
        }

        byte[] body;
        if (head.Storage == AnimationStorage.Lz4)
        {
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
                throw new InvalidDataException($"{source}: body sizes {unpacked}/{stored} are too large.");
            }

            byte[] block = ReadExactly(stream, payloadAt + 8, (int)stored - 8, source);
            body = new byte[unpacked];
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
        }
        else
        {
            if (head.BodySize > int.MaxValue)
            {
                throw new InvalidDataException($"{source}: body size {head.BodySize} is too large.");
            }

            body = ReadExactly(stream, payloadAt, (int)head.BodySize, source);
        }

        byte[] link = ReadExactly(stream, row.BodyOffset + AnimationHead.HeaderSize, (int)head.LinkSize, source);
        return AnimationData.Parse(head, body, link, source);
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
