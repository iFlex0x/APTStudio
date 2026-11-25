using AptStudio.Core.Containers;

namespace AptStudio.Core.Textures;

public static class TextureScanner
{
    // Bump when row selection or header extraction changes; the cache keys on it.
    public const uint ParserVersion = 1;

    public static IReadOnlyList<TextureEntry> Scan(DpcArchive archive)
    {
        if (archive.BodyLocation != BodyLocation.Local)
        {
            throw new InvalidDataException($"{Path.GetFileName(archive.Path)}: external bitmap bodies are not supported.");
        }

        using FileStream stream = new(archive.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        List<TextureEntry> textures = new();
        for (int i = 0; i < archive.Entries.Count; i++)
        {
            DpcEntry row = archive.Entries[i];
            if (row.ClassHash != BitmapHead.ClassHash)
            {
                continue;
            }

            string source = $"{Path.GetFileName(archive.Path)} row {i} name 0x{row.NameHash:X16}";
            if (row.CompressedSize < BitmapHead.HeaderSize)
            {
                throw new InvalidDataException(
                    $"{source}: body is {row.CompressedSize} bytes, need {BitmapHead.HeaderSize}.");
            }

            byte[] header = new byte[BitmapHead.HeaderSize];
            stream.Seek(row.BodyOffset, SeekOrigin.Begin);
            stream.ReadExactly(header);
            BitmapHead head = BitmapHead.Parse(header, source);
            if (head.NameHash != row.NameHash)
            {
                throw new InvalidDataException(
                    $"{source}: body name 0x{head.NameHash:X16} != row name 0x{row.NameHash:X16}.");
            }

            long payloadEnd = row.BodyOffset + head.PayloadOffset
                + (head.Storage == BitmapStorage.Raw ? head.BodySize : head.CompressedSize);
            long rowEnd = row.BodyOffset + row.CompressedSize;
            if (payloadEnd != rowEnd)
            {
                throw new InvalidDataException(
                    $"{source}: payload ends at {payloadEnd}, row ends at {rowEnd}.");
            }

            textures.Add(new TextureEntry(archive.Path, row, head));
        }

        return textures;
    }
}
