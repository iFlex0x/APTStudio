using System.Buffers.Binary;
using AptStudio.Core.Containers;
using AptStudio.Core.Skins;
using K4os.Compression.LZ4;

namespace AptStudio.Core.Links;

public sealed record SkinLinkData(
    SkinHead Head,
    int PartCount,
    IReadOnlyList<ulong> MeshLinks);

/// <summary>
/// Reads only the mesh links of a skin body. The full part parse needs mesh
/// vertex counts; the reverse link index does not.
/// </summary>
public static class SkinLinkReader
{
    private const int MaxMeshes = 1000;

    public static SkinLinkData Read(Stream stream, string archivePath, DpcEntry row)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(row);
        if (row.ClassHash != SkinHead.SkinClass)
        {
            throw new InvalidDataException(
                $"{Path.GetFileName(archivePath)} 0x{row.NameHash:X16}: class is not Skin_Z.");
        }

        string source = $"{Path.GetFileName(archivePath)} 0x{row.NameHash:X16}";
        byte[] header = new byte[SkinHead.HeaderSize];
        stream.Seek(row.BodyOffset, SeekOrigin.Begin);
        stream.ReadExactly(header);
        SkinHead head = SkinHead.Parse(header, source);
        if (head.NameHash != row.NameHash)
        {
            throw new InvalidDataException(
                $"{source}: body name 0x{head.NameHash:X16} != row name 0x{row.NameHash:X16}.");
        }

        long payloadAt = row.BodyOffset + head.PayloadOffset;
        byte[] frame = new byte[8];
        stream.Seek(payloadAt, SeekOrigin.Begin);
        stream.ReadExactly(frame);
        uint unpacked = BinaryPrimitives.ReadUInt32LittleEndian(frame);
        uint stored = BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(4));
        if (unpacked != head.BodySize || stored != head.StoredSize)
        {
            throw new InvalidDataException(
                $"{source}: LZ4 prefix {unpacked}/{stored} "
                + $"!= header {head.BodySize}/{head.StoredSize}.");
        }

        if (unpacked > int.MaxValue || stored > int.MaxValue || stored < 8)
        {
            throw new InvalidDataException($"{source}: body sizes {unpacked}/{stored} are invalid.");
        }

        byte[] block = new byte[stored - 8];
        stream.ReadExactly(block);
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

        return ParseBody(head, body, source);
    }

    public static SkinLinkData ParseBody(SkinHead head, byte[] body, string source)
    {
        ArgumentNullException.ThrowIfNull(head);
        ArgumentNullException.ThrowIfNull(body);
        if (body.Length < 8)
        {
            throw new InvalidDataException($"{source}: skin body is {body.Length} bytes.");
        }

        int count = BinaryPrimitives.ReadInt32LittleEndian(body);
        if (count is < 0 or > MaxMeshes || 4L + count * 8L + 4 > body.Length)
        {
            throw new InvalidDataException($"{source}: mesh count {count} does not fit the body.");
        }

        ulong[] links = new ulong[count];
        for (int i = 0; i < count; i++)
        {
            links[i] = BinaryPrimitives.ReadUInt64LittleEndian(body.AsSpan(4 + i * 8));
        }

        int partCount = BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(4 + count * 8));
        if (partCount is < 0 or > MaxMeshes || partCount > count)
        {
            throw new InvalidDataException(
                $"{source}: part count {partCount} is out of range for {count} meshes.");
        }

        return new SkinLinkData(head, partCount, links);
    }
}
