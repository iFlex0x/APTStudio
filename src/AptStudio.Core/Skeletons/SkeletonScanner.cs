// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers;
using Microsoft.Win32.SafeHandles;

namespace AptStudio.Core.Skeletons;

public sealed record SkeletonHit(
    long Offset,
    ulong NameHash,
    ulong LinkHash,
    uint BodySize,
    uint StoredSize,
    uint LinkSize,
    ushort Version,
    uint TotalSize);

public static class SkeletonScanner
{
    public const uint ParserVersion = 1;

    private const int ChunkSize = 8 * 1024 * 1024;
    private const int Overlap = 7;
    private static readonly byte[] Magic =
        [0xAC, 0x51, 0x66, 0xBA, 0x8C, 0x96, 0x9F, 0xE7];

    public static IReadOnlyList<SkeletonHit> Scan(
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
            return Array.Empty<SkeletonHit>();
        }

        byte[] buffer = ArrayPool<byte>.Shared.Rent(ChunkSize + Overlap);
        List<SkeletonHit> hits = new();
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
                Collect(handle, length, archivePath, buffer, size, baseOffset, hits);
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
        long baseOffset,
        List<SkeletonHit> hits)
    {
        int start = 0;
        Span<byte> header = stackalloc byte[SkeletonHead.HeaderSize];
        while (start < size)
        {
            int found = buffer.AsSpan(start, size - start).IndexOf(Magic);
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
                    SkeletonHead head = SkeletonHead.Parse(
                        header, $"{Path.GetFileName(archivePath)} 0x{offset:X}");
                    if (head.TotalSize <= uint.MaxValue && offset + head.TotalSize <= length)
                    {
                        hits.Add(new SkeletonHit(
                            offset,
                            head.NameHash,
                            head.LinkHash,
                            head.BodySize,
                            head.StoredSize,
                            head.LinkSize,
                            head.Version,
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
