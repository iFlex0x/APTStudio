using System.Buffers.Binary;
using System.Text;

namespace AptStudio.Core.Audio;

public sealed record AudioLanguage(uint Id, string Name);

public sealed record AudioBankRef(uint Id, uint BlockSize, uint Size, uint OffsetSectors, uint LanguageId)
{
    public long Offset => (long)OffsetSectors * BlockSize;
}

public sealed record AudioMediaRef(
    uint BankId,
    uint Id,
    long Offset,
    uint Size,
    uint LanguageId);

/// <summary>
/// An AKPK pack. Media rows come from the header sound table (VO and music
/// packs) or from each bank's DIDX table (SFX and character packs).
/// </summary>
public sealed class AudioPack
{
    public const int HeaderSize = 0x1C;
    private const uint Magic = 0x4B504B41;
    private const int MaxRows = 1_000_000;

    private AudioPack(
        string path,
        uint version,
        IReadOnlyList<AudioLanguage> languages,
        IReadOnlyList<AudioBankRef> banks,
        IReadOnlyList<AudioMediaRef> media)
    {
        Path = path;
        Version = version;
        Languages = languages;
        Banks = banks;
        Media = media;
        LanguageNames = languages.ToDictionary(language => language.Id, language => language.Name);
    }

    public string Path { get; }

    public string Name => System.IO.Path.GetFileName(Path);

    public uint Version { get; }

    public IReadOnlyList<AudioLanguage> Languages { get; }

    public IReadOnlyList<AudioBankRef> Banks { get; }

    public IReadOnlyList<AudioMediaRef> Media { get; }

    public IReadOnlyDictionary<uint, string> LanguageNames { get; }

    public string LanguageName(uint id) =>
        LanguageNames.TryGetValue(id, out string? name) ? name : $"lang {id}";

    public static AudioPack Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (stream.Length < HeaderSize)
        {
            throw new InvalidDataException($"{System.IO.Path.GetFileName(path)}: file is {stream.Length} bytes.");
        }

        byte[] header = ReadExactly(stream, 0, HeaderSize, path);
        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != Magic)
        {
            throw new InvalidDataException($"{System.IO.Path.GetFileName(path)}: not an AKPK pack.");
        }

        uint headerSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4));
        uint version = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8));
        uint languageSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x0C));
        uint bankSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x10));
        uint soundSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x14));
        if (headerSize > stream.Length || (long)HeaderSize + languageSize + bankSize + soundSize > stream.Length)
        {
            throw new InvalidDataException(
                $"{System.IO.Path.GetFileName(path)}: header sizes exceed the file.");
        }

        IReadOnlyList<AudioLanguage> languages = ReadLanguages(stream, languageSize, path);
        IReadOnlyList<AudioBankRef> banks = ReadBanks(stream, languageSize, bankSize, path);
        IReadOnlyList<AudioMediaRef> media = ReadMedia(stream, languageSize, bankSize, soundSize, banks, path);
        return new AudioPack(path, version, languages, banks, media);
    }

    private static IReadOnlyList<AudioLanguage> ReadLanguages(
        FileStream stream,
        uint size,
        string path)
    {
        long start = HeaderSize;
        if (size < 4)
        {
            return Array.Empty<AudioLanguage>();
        }

        byte[] section = ReadExactly(stream, start, (int)size, path);
        int count = BinaryPrimitives.ReadInt32LittleEndian(section);
        if (count is < 0 or > 64 || 4 + count * 8 > section.Length)
        {
            throw new InvalidDataException(
                $"{System.IO.Path.GetFileName(path)}: language count {count} does not fit.");
        }

        AudioLanguage[] languages = new AudioLanguage[count];
        for (int i = 0; i < count; i++)
        {
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(section.AsSpan(4 + i * 8));
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(section.AsSpan(8 + i * 8));
            languages[i] = new AudioLanguage(id, ReadUtf16(section, offset, path));
        }

        return languages;
    }

    private static IReadOnlyList<AudioBankRef> ReadBanks(
        FileStream stream,
        uint languageSize,
        uint size,
        string path)
    {
        long start = HeaderSize + languageSize;
        if (size < 4)
        {
            return Array.Empty<AudioBankRef>();
        }

        byte[] section = ReadExactly(stream, start, (int)size, path);
        int count = BinaryPrimitives.ReadInt32LittleEndian(section);
        if (count is < 0 or > MaxRows || 4 + count * 20 > section.Length)
        {
            throw new InvalidDataException(
                $"{System.IO.Path.GetFileName(path)}: bank count {count} does not fit.");
        }

        AudioBankRef[] banks = new AudioBankRef[count];
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> row = section.AsSpan(4 + i * 20, 20);
            banks[i] = new AudioBankRef(
                BinaryPrimitives.ReadUInt32LittleEndian(row),
                BinaryPrimitives.ReadUInt32LittleEndian(row[4..]),
                BinaryPrimitives.ReadUInt32LittleEndian(row[8..]),
                BinaryPrimitives.ReadUInt32LittleEndian(row[12..]),
                BinaryPrimitives.ReadUInt32LittleEndian(row[16..]));
        }

        return banks;
    }

    private static IReadOnlyList<AudioMediaRef> ReadMedia(
        FileStream stream,
        uint languageSize,
        uint bankSize,
        uint soundSize,
        IReadOnlyList<AudioBankRef> banks,
        string path)
    {
        List<AudioMediaRef> media = new();
        long soundStart = HeaderSize + languageSize + bankSize;
        if (soundSize > int.MaxValue)
        {
            throw new InvalidDataException(
                $"{System.IO.Path.GetFileName(path)}: sound table is {soundSize} bytes.");
        }

        if (soundSize >= 4)
        {
            byte[] section = ReadExactly(stream, soundStart, (int)soundSize, path);
            int count = BinaryPrimitives.ReadInt32LittleEndian(section);
            if (count is < 0 or > MaxRows || 4 + count * 20 > section.Length)
            {
                throw new InvalidDataException(
                    $"{System.IO.Path.GetFileName(path)}: sound count {count} does not fit.");
            }

            for (int i = 0; i < count; i++)
            {
                ReadOnlySpan<byte> row = section.AsSpan(4 + i * 20, 20);
                AudioMediaRef entry = ReadMediaRow(row, 0);
                CheckMedia(entry, stream.Length, path);
                media.Add(entry);
            }
        }

        foreach (AudioBankRef bank in banks)
        {
            media.AddRange(ReadBankMedia(stream, bank, path));
        }

        return media;
    }

    private static AudioMediaRef ReadMediaRow(ReadOnlySpan<byte> row, uint bankId) =>
        new(
            bankId,
            BinaryPrimitives.ReadUInt32LittleEndian(row),
            (long)BinaryPrimitives.ReadUInt32LittleEndian(row[12..])
                * BinaryPrimitives.ReadUInt32LittleEndian(row[4..]),
            BinaryPrimitives.ReadUInt32LittleEndian(row[8..]),
            BinaryPrimitives.ReadUInt32LittleEndian(row[16..]));

    private static IEnumerable<AudioMediaRef> ReadBankMedia(
        FileStream stream,
        AudioBankRef bank,
        string path)
    {
        long bankOffset = bank.Offset;
        long bankSize = bank.Size;
        if (bankOffset < 0 || bankSize < 8 || bankOffset + bankSize > stream.Length)
        {
            throw new InvalidDataException(
                $"{System.IO.Path.GetFileName(path)}: bank 0x{bank.Id:X8} lies outside the file.");
        }

        long didx = -1;
        uint didxSize = 0;
        long data = -1;
        uint dataSize = 0;
        long at = bankOffset;
        long end = bankOffset + bankSize;
        byte[] chunk = new byte[8];
        while (at + 8 <= end)
        {
            stream.Seek(at, SeekOrigin.Begin);
            stream.ReadExactly(chunk, 0, 8);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(chunk.AsSpan(4));
            if (at + 8L + size > end)
            {
                throw new InvalidDataException(
                    $"{System.IO.Path.GetFileName(path)}: bank 0x{bank.Id:X8} chunk exceeds the bank.");
            }

            ReadOnlySpan<byte> tag = chunk.AsSpan(0, 4);
            if (tag.SequenceEqual("DIDX"u8))
            {
                didx = at + 8;
                didxSize = size;
            }
            else if (tag.SequenceEqual("DATA"u8))
            {
                data = at + 8;
                dataSize = size;
            }

            at += 8L + size;
        }

        if (didx < 0 || data < 0)
        {
            yield break;
        }

        if (didxSize % 12 != 0)
        {
            throw new InvalidDataException(
                $"{System.IO.Path.GetFileName(path)}: bank 0x{bank.Id:X8} DIDX size {didxSize} is not a row multiple.");
        }

        byte[] rows = ReadExactly(stream, didx, (int)didxSize, path);
        for (int i = 0; i < rows.Length; i += 12)
        {
            AudioMediaRef entry = new(
                bank.Id,
                BinaryPrimitives.ReadUInt32LittleEndian(rows.AsSpan(i)),
                data + BinaryPrimitives.ReadUInt32LittleEndian(rows.AsSpan(i + 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(rows.AsSpan(i + 8)),
                bank.LanguageId);
            if (entry.Size == 0 || entry.Size > dataSize
                || entry.Offset - data + entry.Size > dataSize)
            {
                throw new InvalidDataException(
                    $"{System.IO.Path.GetFileName(path)}: bank 0x{bank.Id:X8} media 0x{entry.Id:X8} lies outside DATA.");
            }

            CheckMedia(entry, stream.Length, path);
            yield return entry;
        }
    }

    private static void CheckMedia(AudioMediaRef entry, long length, string path)
    {
        if (entry.Size == 0 || entry.Offset < 0 || entry.Offset + entry.Size > length)
        {
            throw new InvalidDataException(
                $"{System.IO.Path.GetFileName(path)}: media 0x{entry.Id:X8} lies outside the file.");
        }
    }

    private static string ReadUtf16(ReadOnlySpan<byte> section, uint offset, string path)
    {
        if (offset + 2 > section.Length)
        {
            throw new InvalidDataException(
                $"{System.IO.Path.GetFileName(path)}: language string offset {offset} is outside the table.");
        }

        int end = (int)offset;
        while (end + 2 <= section.Length && (section[end] != 0 || section[end + 1] != 0))
        {
            end += 2;
        }

        return Encoding.Unicode.GetString(section[(int)offset..end]);
    }

    private static byte[] ReadExactly(Stream stream, long offset, int count, string path)
    {
        byte[] buffer = new byte[count];
        stream.Seek(offset, SeekOrigin.Begin);
        int read = 0;
        while (read < count)
        {
            int n = stream.Read(buffer, read, count - read);
            if (n == 0)
            {
                throw new InvalidDataException(
                    $"{System.IO.Path.GetFileName(path)}: short read at {offset}.");
            }

            read += n;
        }

        return buffer;
    }
}
