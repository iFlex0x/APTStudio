using System.Buffers;
using Microsoft.Win32.SafeHandles;

namespace AptStudio.Core.Materials;

public sealed record MaterialHit(
    long Offset,
    ulong NameHash,
    ulong LinkHash,
    ulong ClassHash,
    uint TotalSize);

public static class MaterialScanner
{
    public const uint ParserVersion = 1;

    private const int ChunkSize = 8 * 1024 * 1024;
    private const int Overlap = 7;
    private static readonly byte[] Magic = [0xFF, 0xD4, 0xC1, 0xCF, 0xF1, 0xB7, 0x0B, 0x31];
    private static readonly byte[] AnimMagic = [0x65, 0x78, 0x79, 0xB6, 0xCB, 0x11, 0xBB, 0x41];

    public static IReadOnlyList<MaterialHit> Scan(
        string archivePath, CancellationToken cancellationToken = default)
    {
        using SafeFileHandle handle = File.OpenHandle(
            archivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        long length = RandomAccess.GetLength(handle);
        Span<byte> kind = stackalloc byte[1];
        if (length < 0x101
            || RandomAccess.Read(handle, kind, 0x100) != 1
            || kind[0] == 2)
        {
            return Array.Empty<MaterialHit>();
        }

        byte[] buffer = ArrayPool<byte>.Shared.Rent(ChunkSize + Overlap);
        List<MaterialHit> hits = new();
        long position = 0;
        int carried = 0;
        try
        {
            while (position < length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = RandomAccess.Read(
                    handle, buffer.AsSpan(carried, ChunkSize), position);
                if (read <= 0)
                {
                    break;
                }

                int size = carried + read;
                long baseOffset = position - carried;
                Collect(handle, length, archivePath, buffer, size, Magic, baseOffset, hits);
                Collect(handle, length, archivePath, buffer, size, AnimMagic, baseOffset, hits);
                position += read;
                carried = Math.Min(Overlap, size);
                buffer.AsSpan(size - carried, carried).CopyTo(buffer);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        hits.Sort((left, right) => left.Offset.CompareTo(right.Offset));
        return hits;
    }

    private static void Collect(
        SafeFileHandle handle,
        long length,
        string archivePath,
        byte[] buffer,
        int size,
        byte[] magic,
        long baseOffset,
        List<MaterialHit> hits)
    {
        int limit = size;
        int start = 0;
        Span<byte> header = stackalloc byte[MaterialHead.HeaderSize];
        while (start < limit)
        {
            int found = buffer.AsSpan(start, limit - start).IndexOf(magic);
            if (found < 0)
            {
                return;
            }

            int at = start + found;
            long offset = baseOffset + at;
            if (RandomAccess.Read(handle, header, offset) == header.Length)
            {
                try
                {
                    MaterialHead head = MaterialHead.Parse(
                        header, $"{Path.GetFileName(archivePath)} 0x{offset:X}");
                    if (offset + head.TotalSize <= length && head.TotalSize <= uint.MaxValue)
                    {
                        hits.Add(new MaterialHit(
                            offset,
                            head.NameHash,
                            head.LinkHash,
                            head.ClassHash,
                            (uint)head.TotalSize));
                    }
                }
                catch (InvalidDataException)
                {
                }
            }

            start = at + 1;
        }
    }
}
