using System.Buffers.Binary;
using System.Text;
using AptStudio.Core.Audio;

namespace AptStudio.Core.Tests.Audio;

public sealed class AudioPackTests
{
    [Fact]
    public void ReadsLanguagesBanksAndMedia()
    {
        string path = WritePack();
        try
        {
            AudioPack pack = AudioPack.Read(path);
            Assert.Equal(1u, pack.Version);
            AudioLanguage language = Assert.Single(pack.Languages);
            Assert.Equal(1u, language.Id);
            Assert.Equal("sfx", language.Name);
            AudioBankRef bank = Assert.Single(pack.Banks);
            Assert.Equal(0xABCDu, bank.Id);
            Assert.Equal(128, bank.Offset);
            Assert.Equal(90u, bank.Size);

            Assert.Equal(2, pack.Media.Count);
            AudioMediaRef sound = pack.Media[0];
            Assert.Equal(0u, sound.BankId);
            Assert.Equal(0x1234u, sound.Id);
            Assert.Equal(256, sound.Offset);
            Assert.Equal(64u, sound.Size);
            AudioMediaRef embedded = pack.Media[1];
            Assert.Equal(0xABCDu, embedded.BankId);
            Assert.Equal(0x5678u, embedded.Id);
            Assert.Equal(168, embedded.Offset);
            Assert.Equal(50u, embedded.Size);
            Assert.Equal(1u, embedded.LanguageId);
            Assert.Equal("sfx", pack.LanguageName(1));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RejectsOtherMagic()
    {
        string path = WritePack();
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            bytes[0] = (byte)'X';
            File.WriteAllBytes(path, bytes);
            InvalidDataException error = Assert.Throws<InvalidDataException>(() => AudioPack.Read(path));
            Assert.Contains("not an AKPK pack", error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RejectsUnknownVersion()
    {
        string path = WritePack();
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 2u);
            File.WriteAllBytes(path, bytes);
            InvalidDataException error = Assert.Throws<InvalidDataException>(() => AudioPack.Read(path));
            Assert.Contains("version 2", error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RejectsSectionPastFile()
    {
        string path = WritePack();
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x14), 10_000);
            File.WriteAllBytes(path, bytes);
            Assert.Throws<InvalidDataException>(() => AudioPack.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string WritePack()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);
        writer.Write(0x4B504B41u);
        writer.Write(96u);
        writer.Write(1u);
        writer.Write(20u);
        writer.Write(24u);
        writer.Write(24u);
        writer.Write(0u);

        writer.Write(1);
        writer.Write(12u);
        writer.Write(1u);
        writer.Write(Encoding.Unicode.GetBytes("sfx\0"));

        writer.Write(1);
        writer.Write(0xABCDu);
        writer.Write(64u);
        writer.Write(90u);
        writer.Write(2u);
        writer.Write(1u);

        writer.Write(1);
        writer.Write(0x1234u);
        writer.Write(64u);
        writer.Write(64u);
        writer.Write(4u);
        writer.Write(1u);

        while (stream.Length < 128)
        {
            writer.Write((byte)0);
        }

        writer.Write("BKHD"u8);
        writer.Write(4u);
        writer.Write(172u);
        writer.Write("DIDX"u8);
        writer.Write(12u);
        writer.Write(0x5678u);
        writer.Write(0u);
        writer.Write(50u);
        writer.Write("DATA"u8);
        writer.Write(50u);
        writer.Write(new byte[50]);

        while (stream.Length < 256)
        {
            writer.Write((byte)0);
        }

        writer.Write(new byte[64]);
        writer.Flush();
        string path = Path.GetTempFileName();
        File.WriteAllBytes(path, stream.ToArray());
        return path;
    }
}
