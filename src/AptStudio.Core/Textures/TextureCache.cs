// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using AptStudio.Core.Containers;

namespace AptStudio.Core.Textures;

public static class TextureCache
{
    private const uint FormatVersion = 3;
    private const int RecordSize = 74;
    private const int MaxPathBytes = 4096;
    private const int ChecksumSize = 32;
    private const long MaxCacheBytes = 64 * 1024 * 1024;
    private const uint MaxHeaderDimension = 65535 * 256 + 255;
    private static readonly byte[] Magic = "APTEXCACHE"u8.ToArray();

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

    public static IReadOnlyList<TextureEntry> LoadOrScan(
        string archivePath,
        bool rebuild,
        out bool fromCache,
        string? cacheRoot = null)
    {
        string fullPath = Path.GetFullPath(archivePath);
        string cachePath = CachePath(cacheRoot ?? DefaultRoot, fullPath);
        FileInfo before = new(fullPath);
        if (!rebuild && TryRead(cachePath, fullPath, before, out IReadOnlyList<TextureEntry> cached))
        {
            fromCache = true;
            return cached;
        }

        return ScanAndCache(DpcArchive.Open(fullPath), cachePath, fullPath, before, out fromCache);
    }

    public static IReadOnlyList<TextureEntry> LoadOrScan(
        DpcArchive archive,
        bool rebuild,
        out bool fromCache,
        string? cacheRoot = null)
    {
        string fullPath = Path.GetFullPath(archive.Path);
        FileInfo before = new(fullPath);
        if (before.Length != archive.Length || before.LastWriteTimeUtc != archive.LastWriteTimeUtc)
        {
            return LoadOrScan(fullPath, rebuild, out fromCache, cacheRoot);
        }

        string cachePath = CachePath(cacheRoot ?? DefaultRoot, fullPath);
        if (!rebuild && TryRead(cachePath, fullPath, before, out IReadOnlyList<TextureEntry> cached))
        {
            fromCache = true;
            return cached;
        }

        return ScanAndCache(archive, cachePath, fullPath, before, out fromCache);
    }

    private static IReadOnlyList<TextureEntry> ScanAndCache(
        DpcArchive archive,
        string cachePath,
        string fullPath,
        FileInfo before,
        out bool fromCache)
    {
        fromCache = false;
        IReadOnlyList<TextureEntry> textures = TextureScanner.Scan(archive);
        FileInfo after = new(fullPath);

        // Do not cache a scan that raced a file change.
        if (before.Length == after.Length && before.LastWriteTimeUtc == after.LastWriteTimeUtc)
        {
            // Cache is optional; a read-only disk must not fail the scan.
            try
            {
                Write(cachePath, fullPath, after, textures);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return textures;
    }

    private static string CachePath(string root, string archivePath)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(archivePath));
        string name = Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant() + ".bin";
        return Path.Combine(root, name);
    }

    private static bool TryRead(
        string cachePath,
        string archivePath,
        FileInfo archive,
        out IReadOnlyList<TextureEntry> textures)
    {
        textures = Array.Empty<TextureEntry>();
        try
        {
            FileInfo file = new(cachePath);
            if (!file.Exists || file.Length > MaxCacheBytes)
            {
                return false;
            }

            byte[] bytes = File.ReadAllBytes(cachePath);
            using MemoryStream stream = new(bytes, writable: false);
            using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);
            if (!reader.ReadBytes(Magic.Length).AsSpan().SequenceEqual(Magic)
                || reader.ReadUInt32() != FormatVersion
                || reader.ReadUInt32() != TextureScanner.ParserVersion)
            {
                return false;
            }

            int pathLength = reader.ReadUInt16();
            if (pathLength > MaxPathBytes)
            {
                return false;
            }

            string path = Encoding.UTF8.GetString(reader.ReadBytes(pathLength));
            if (!string.Equals(path, archivePath, StringComparison.OrdinalIgnoreCase)
                || reader.ReadInt64() != archive.Length
                || reader.ReadInt64() != archive.LastWriteTimeUtc.Ticks)
            {
                return false;
            }

            uint count = reader.ReadUInt32();
            long entryBytes = (long)count * RecordSize;
            if (entryBytes != stream.Length - stream.Position - ChecksumSize)
            {
                return false;
            }

            byte[] checksum = reader.ReadBytes(ChecksumSize);
            byte[] entries = reader.ReadBytes((int)entryBytes);
            if (!SHA256.HashData(entries).AsSpan().SequenceEqual(checksum))
            {
                return false;
            }

            using MemoryStream entryStream = new(entries, writable: false);
            using BinaryReader entryReader = new(entryStream, Encoding.UTF8, leaveOpen: true);
            List<TextureEntry> list = new((int)count);
            for (int i = 0; i < count; i++)
            {
                list.Add(ReadEntry(entryReader, archivePath, archive.Length, i));
            }

            // Re-stat so a file replaced during the read is not served from cache.
            FileInfo current = new(archivePath);
            if (current.Length != archive.Length || current.LastWriteTimeUtc != archive.LastWriteTimeUtc)
            {
                return false;
            }

            textures = list;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return false;
        }
    }

    private static TextureEntry ReadEntry(
        BinaryReader reader,
        string archivePath,
        long archiveLength,
        int index)
    {
        ulong name = reader.ReadUInt64();
        ulong rowLink = reader.ReadUInt64();
        ulong headLink = reader.ReadUInt64();
        uint offset = reader.ReadUInt32();
        uint compressed = reader.ReadUInt32();
        uint flags = reader.ReadUInt32();
        uint decompressed = reader.ReadUInt32();
        ushort u2 = reader.ReadUInt16();
        ushort u3 = reader.ReadUInt16();
        BitmapKind kind = (BitmapKind)reader.ReadByte();
        uint widthRaw = reader.ReadUInt32();
        uint heightRaw = reader.ReadUInt32();
        uint surfacesRaw = reader.ReadUInt32();
        ushort mips = reader.ReadUInt16();
        BitmapStorage storage = (BitmapStorage)reader.ReadByte();
        uint stored = reader.ReadUInt32();
        uint body = reader.ReadUInt32();
        uint headCompressed = reader.ReadUInt32();
        ushort formatKey = reader.ReadUInt16();

        TextureFormat format = TextureFormats.FromKey(formatKey);
        long payloadSize = storage == BitmapStorage.Raw ? body : headCompressed;
        if (kind > BitmapKind.Cube
            || storage is not (BitmapStorage.Raw or BitmapStorage.Lz4)
            || widthRaw is 0 or > MaxHeaderDimension
            || (kind != BitmapKind.Volume && (heightRaw is 0 or > MaxHeaderDimension))
            || surfacesRaw is 0 or > MaxHeaderDimension
            || stored == 0
            || mips is < 1 or > 32
            || (ulong)stored + 4 != body
            || (storage == BitmapStorage.Raw && headCompressed != 0)
            || (storage == BitmapStorage.Lz4 && headCompressed <= 8)
            || (kind is BitmapKind.Flat or BitmapKind.Cube && format.Family == TextureFamily.Volume)
            || (long)offset * DpcArchive.SectorSize + compressed > archiveLength
            || BitmapHead.HeaderSize + payloadSize != compressed)
        {
            throw new InvalidDataException(
                $"{Path.GetFileName(archivePath)}: cache entry {index} is invalid.");
        }

        int width = (int)widthRaw;
        int height = (int)heightRaw;
        int surfaces = (int)surfacesRaw;
        DpcEntry row = new(name, BitmapHead.ClassHash, rowLink, offset, compressed, flags, decompressed, u2, u3);
        BitmapHead head = new(
            name, headLink, kind, width, height, surfaces, mips, storage,
            stored, body, headCompressed, BitmapHead.LinkHeaderSize, formatKey,
            format, BitmapHead.HeaderSize);
        return new TextureEntry(archivePath, row, head);
    }

    private static void Write(
        string cachePath,
        string archivePath,
        FileInfo archive,
        IReadOnlyList<TextureEntry> textures)
    {
        byte[] path = Encoding.UTF8.GetBytes(archivePath);
        if (path.Length > MaxPathBytes)
        {
            return;
        }

        byte[] entries;
        using (MemoryStream entryBuffer = new())
        {
            using (BinaryWriter entryWriter = new(entryBuffer, Encoding.UTF8, leaveOpen: true))
            {
                foreach (TextureEntry texture in textures)
                {
                    WriteEntry(entryWriter, texture);
                }
            }

            entries = entryBuffer.ToArray();
        }

        using MemoryStream buffer = new();
        using (BinaryWriter writer = new(buffer, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(FormatVersion);
            writer.Write(TextureScanner.ParserVersion);
            writer.Write((ushort)path.Length);
            writer.Write(path);
            writer.Write(archive.Length);
            writer.Write(archive.LastWriteTimeUtc.Ticks);
            writer.Write((uint)textures.Count);
            writer.Write(SHA256.HashData(entries));
            writer.Write(entries);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        string temp = cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temp, buffer.ToArray());
            File.Move(temp, cachePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    private static void WriteEntry(BinaryWriter writer, TextureEntry texture)
    {
        BitmapHead head = texture.Head;
        writer.Write(head.NameHash);
        writer.Write(texture.Row.LinkHash);
        writer.Write(head.LinkHash);
        writer.Write(texture.Row.Offset);
        writer.Write(texture.Row.CompressedSize);
        writer.Write(texture.Row.Flags);
        writer.Write(texture.Row.DecompressedSize);
        writer.Write(texture.Row.U2);
        writer.Write(texture.Row.U3);
        writer.Write((byte)head.Kind);
        writer.Write((uint)head.Width);
        writer.Write((uint)head.Height);
        writer.Write((uint)head.Surfaces);
        writer.Write((ushort)head.MipCount);
        writer.Write((byte)head.Storage);
        writer.Write(head.StoredSize);
        writer.Write(head.BodySize);
        writer.Write(head.CompressedSize);
        writer.Write(head.FormatKeyRaw);
    }
}
