// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace AptStudio.Core.Skeletons;

public static class SkeletonCache
{
    private const uint FormatVersion = 1;
    private const int RecordSize = 40;
    private const long MaxCacheBytes = 64 * 1024 * 1024;
    private static readonly byte[] Magic = "APTSKELCACHE"u8.ToArray();

    public static string DefaultRoot
    {
        get
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(local))
            {
                local = Path.GetTempPath();
            }

            return Path.Combine(local, "APTStudio", "cache");
        }
    }

    public static IReadOnlyList<SkeletonHit> LoadOrScan(
        string archivePath,
        bool rebuild,
        out bool fromCache,
        string? cacheRoot = null,
        CancellationToken cancellationToken = default)
    {
        string fullPath = Path.GetFullPath(archivePath);
        string cachePath = CachePath(cacheRoot ?? DefaultRoot, fullPath);
        FileInfo before = new(fullPath);
        long beforeLength = before.Length;
        DateTime beforeWrite = before.LastWriteTimeUtc;
        if (!rebuild && TryRead(cachePath, fullPath, before, out IReadOnlyList<SkeletonHit> cached))
        {
            fromCache = true;
            return cached;
        }

        IReadOnlyList<SkeletonHit> hits = SkeletonScanner.Scan(fullPath, cancellationToken);
        FileInfo after = new(fullPath);
        if (beforeLength == after.Length && beforeWrite == after.LastWriteTimeUtc)
        {
            try
            {
                Write(cachePath, after, hits);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        fromCache = false;
        return hits;
    }

    private static string CachePath(string root, string archivePath)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(archivePath));
        string name = Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant() + ".skel";
        return Path.Combine(root, name);
    }

    private static bool TryRead(
        string cachePath,
        string archivePath,
        FileInfo archive,
        out IReadOnlyList<SkeletonHit> hits)
    {
        hits = Array.Empty<SkeletonHit>();
        try
        {
            FileInfo file = new(cachePath);
            if (!file.Exists || file.Length > MaxCacheBytes || file.Length < Magic.Length + 28 + 32)
            {
                return false;
            }

            byte[] bytes = File.ReadAllBytes(cachePath);
            if (!bytes.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            {
                return false;
            }

            int at = Magic.Length;
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at)) != FormatVersion
                || BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at + 4))
                    != SkeletonScanner.ParserVersion)
            {
                return false;
            }

            at += 8;
            long length = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(at));
            long ticks = BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(at + 8));
            if (length != archive.Length || ticks != archive.LastWriteTimeUtc.Ticks)
            {
                return false;
            }

            at += 16;
            int count = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(at));
            at += 4;
            if (count < 0 || (long)count * RecordSize + at + 32 != bytes.Length)
            {
                return false;
            }

            ReadOnlySpan<byte> payload = bytes.AsSpan(at, count * RecordSize);
            byte[] checksum = SHA256.HashData(payload);
            if (!checksum.AsSpan().SequenceEqual(bytes.AsSpan(at + payload.Length, 32)))
            {
                return false;
            }

            List<SkeletonHit> rows = new(count);
            for (int index = 0; index < count; index++)
            {
                ReadOnlySpan<byte> record = payload.Slice(index * RecordSize, RecordSize);
                long offset = BinaryPrimitives.ReadInt64LittleEndian(record);
                uint bodySize = BinaryPrimitives.ReadUInt32LittleEndian(record[24..]);
                uint storedSize = BinaryPrimitives.ReadUInt32LittleEndian(record[28..]);
                uint linkSize = BinaryPrimitives.ReadUInt32LittleEndian(record[32..]);
                long total = SkeletonHead.HeaderSize + (long)linkSize
                    + SkeletonHead.ExtraSize + storedSize;
                if (offset < 0 || bodySize is 0 or > SkeletonHead.MaxBodySize
                    || storedSize <= 8 || linkSize > SkeletonHead.MaxLinkSize
                    || total > uint.MaxValue || offset + total > archive.Length)
                {
                    return false;
                }

                rows.Add(new SkeletonHit(
                    offset,
                    BinaryPrimitives.ReadUInt64LittleEndian(record[8..]),
                    BinaryPrimitives.ReadUInt64LittleEndian(record[16..]),
                    bodySize,
                    storedSize,
                    linkSize,
                    BinaryPrimitives.ReadUInt16LittleEndian(record[36..]),
                    (uint)total));
            }

            hits = rows;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void Write(
        string cachePath,
        FileInfo archive,
        IReadOnlyList<SkeletonHit> hits)
    {
        byte[] payload = new byte[hits.Count * RecordSize];
        for (int index = 0; index < hits.Count; index++)
        {
            SkeletonHit hit = hits[index];
            Span<byte> record = payload.AsSpan(index * RecordSize, RecordSize);
            BinaryPrimitives.WriteInt64LittleEndian(record, hit.Offset);
            BinaryPrimitives.WriteUInt64LittleEndian(record[8..], hit.NameHash);
            BinaryPrimitives.WriteUInt64LittleEndian(record[16..], hit.LinkHash);
            BinaryPrimitives.WriteUInt32LittleEndian(record[24..], hit.BodySize);
            BinaryPrimitives.WriteUInt32LittleEndian(record[28..], hit.StoredSize);
            BinaryPrimitives.WriteUInt32LittleEndian(record[32..], hit.LinkSize);
            BinaryPrimitives.WriteUInt16LittleEndian(record[36..], hit.Version);
        }

        byte[] bytes = new byte[Magic.Length + 28 + payload.Length + 32];
        Magic.CopyTo(bytes, 0);
        int at = Magic.Length;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), FormatVersion);
        BinaryPrimitives.WriteUInt32LittleEndian(
            bytes.AsSpan(at + 4), SkeletonScanner.ParserVersion);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(at + 8), archive.Length);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(at + 16), archive.LastWriteTimeUtc.Ticks);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(at + 24), hits.Count);
        payload.CopyTo(bytes, at + 28);
        SHA256.HashData(payload).CopyTo(bytes, at + 28 + payload.Length);

        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        string temp = cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, cachePath, true);
    }
}
