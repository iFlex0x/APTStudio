using System.Security.Cryptography;
using System.Text;
using AptStudio.Core.Containers;

namespace AptStudio.Core.Textures;

public static class TextureCache
{
    private const int Version = 2;
    private const int RecordSize = 74;
    private const long MaxCacheBytes = 64 * 1024 * 1024;
    private static readonly byte[] Magic = "APTEXCACHE"u8.ToArray();

    public static string DefaultRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "APTStudio",
        "cache");

    public static IReadOnlyList<TextureEntry> LoadOrScan(
        string archivePath,
        bool rebuild,
        out bool fromCache,
        string? cacheRoot = null)
    {
        string fullPath = Path.GetFullPath(archivePath);
        string cachePath = CachePath(cacheRoot ?? DefaultRoot, fullPath);
        FileInfo archive = new(fullPath);
        if (!rebuild && TryRead(cachePath, fullPath, archive, out IReadOnlyList<TextureEntry> cached))
        {
            fromCache = true;
            return cached;
        }

        fromCache = false;
        FileInfo before = new(fullPath);
        DpcArchive dpc = DpcArchive.Open(fullPath);
        IReadOnlyList<TextureEntry> textures = TextureScanner.Scan(dpc);
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
            if (!File.Exists(cachePath) || new FileInfo(cachePath).Length > MaxCacheBytes)
            {
                return false;
            }

            using FileStream stream = File.OpenRead(cachePath);
            using BinaryReader reader = new(stream, Encoding.UTF8, leaveOpen: true);
            if (!reader.ReadBytes(Magic.Length).AsSpan().SequenceEqual(Magic))
            {
                return false;
            }

            if (reader.ReadUInt32() != Version)
            {
                return false;
            }

            int pathLength = reader.ReadUInt16();
            if (pathLength > 4096)
            {
                return false;
            }

            string path = Encoding.UTF8.GetString(reader.ReadBytes(pathLength));
            if (!string.Equals(path, archivePath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (reader.ReadInt64() != archive.Length
                || reader.ReadInt64() != archive.LastWriteTimeUtc.Ticks)
            {
                return false;
            }

            uint count = reader.ReadUInt32();
            long remaining = stream.Length - stream.Position;
            if (count > remaining / RecordSize)
            {
                return false;
            }

            List<TextureEntry> list = new((int)count);
            for (int i = 0; i < count; i++)
            {
                list.Add(ReadEntry(reader, archivePath));
            }

            if (stream.Position != stream.Length)
            {
                return false;
            }

            textures = list;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static TextureEntry ReadEntry(BinaryReader reader, string archivePath)
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

        if (kind > BitmapKind.Cube
            || storage is not (BitmapStorage.Raw or BitmapStorage.Lz4)
            || widthRaw is 0 or > int.MaxValue
            || (kind != BitmapKind.Volume && heightRaw is 0 or > int.MaxValue)
            || surfacesRaw is 0 or > int.MaxValue
            || stored == 0
            || mips is < 1 or > 32
            || (ulong)stored + 4 != body
            || (storage == BitmapStorage.Raw && headCompressed != 0)
            || (storage == BitmapStorage.Lz4 && headCompressed <= 8))
        {
            throw new InvalidDataException("cache entry is invalid");
        }

        int width = (int)widthRaw;
        int height = (int)heightRaw;
        int surfaces = (int)surfacesRaw;
        DpcEntry row = new(name, BitmapHead.ClassHash, rowLink, offset, compressed, flags, decompressed, u2, u3);
        BitmapHead head = new(
            name, headLink, kind, width, height, surfaces, mips, storage,
            stored, body, headCompressed, 63, formatKey,
            TextureFormats.FromKey(formatKey), BitmapHead.HeaderSize);
        return new TextureEntry(archivePath, row, head);
    }

    private static void Write(
        string cachePath,
        string archivePath,
        FileInfo archive,
        IReadOnlyList<TextureEntry> textures)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        using MemoryStream buffer = new();
        using (BinaryWriter writer = new(buffer, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(Version);
            byte[] path = Encoding.UTF8.GetBytes(archivePath);
            writer.Write((ushort)path.Length);
            writer.Write(path);
            writer.Write(archive.Length);
            writer.Write(archive.LastWriteTimeUtc.Ticks);
            writer.Write((uint)textures.Count);
            foreach (TextureEntry texture in textures)
            {
                WriteEntry(writer, texture);
            }
        }

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
