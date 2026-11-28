// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using System.Text;
using AptStudio.Core.Audio;
using AptStudio.Core.Games;

namespace AptStudio.Core.Tests.Audio;

public sealed class AudioCatalogTests
{
    [Fact]
    public void SkipsPreviewsAndPluginsAndKeepsEntries()
    {
        string root = Directory.CreateTempSubdirectory("aptstudio-audio-").FullName;
        try
        {
            File.WriteAllBytes(Path.Combine(root, "Resonance.exe"), []);
            Directory.CreateDirectory(Path.Combine(root, "DATAS"));
            Directory.CreateDirectory(Path.Combine(root, "FONT"));
            Directory.CreateDirectory(Path.Combine(root, "LEVELS"));
            string soundbanks = Directory.CreateDirectory(Path.Combine(root, "SOUNDBANKS")).FullName;

            WritePack(Path.Combine(soundbanks, "VO_MEDIA.PC.PCK"), BuildWem(48000, 1, "line"), "sfx");
            WritePack(
                Path.Combine(soundbanks, "MUS_STRUCTURE.PC.PCK"), BuildWem(44100, 2, "track"), "sfx");
            WritePack(Path.Combine(soundbanks, "SHARED_MEDIA.PC.PCK"), new byte[64], "sfx");
            File.WriteAllBytes(Path.Combine(soundbanks, "BROKEN.PC.PCK"), [1, 2, 3, 4]);

            Assert.True(GameProfile.TryDetect(root, out GameProfile? profile, out string? error), error);
            AudioCatalog catalog = AudioCatalog.Load(profile!);

            AudioEntry entry = Assert.Single(catalog.Entries);
            Assert.Equal("VO_MEDIA.PC.PCK", entry.PackName);
            Assert.Equal("line", entry.Name);
            Assert.Equal(1, catalog.Previews);
            Assert.Equal(1, catalog.Plugins);
            Assert.Single(catalog.Failures);
            Assert.Equal(4, catalog.Packs);
            Assert.Single(catalog.Archives);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MissingSoundbanksIsNamed()
    {
        string root = Directory.CreateTempSubdirectory("aptstudio-audio-").FullName;
        try
        {
            File.WriteAllBytes(Path.Combine(root, "Resonance.exe"), []);
            Directory.CreateDirectory(Path.Combine(root, "DATAS"));
            Directory.CreateDirectory(Path.Combine(root, "FONT"));
            Directory.CreateDirectory(Path.Combine(root, "LEVELS"));
            Assert.True(GameProfile.TryDetect(root, out GameProfile? profile, out _));

            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => AudioCatalog.Load(profile!));
            Assert.Contains("SOUNDBANKS", error.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void WritePack(string path, byte[] wem, string language)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);
        writer.Write(0x4B504B41u);
        writer.Write(96u);
        writer.Write(1u);
        writer.Write(20u);
        writer.Write(4u);
        writer.Write(24u);
        writer.Write(0u);

        writer.Write(1);
        writer.Write(12u);
        writer.Write(1u);
        writer.Write(Encoding.Unicode.GetBytes(language + "\0"));

        writer.Write(0);

        writer.Write(1);
        writer.Write(0x1234u);
        writer.Write(64u);
        writer.Write((uint)wem.Length);
        writer.Write(4u);
        writer.Write(1u);

        while (stream.Length < 256)
        {
            writer.Write((byte)0);
        }

        writer.Write(wem);
        writer.Flush();
        File.WriteAllBytes(path, stream.ToArray());
    }

    private static byte[] BuildWem(uint rate, ushort channels, string cue)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);
        writer.Write("RIFF"u8);
        writer.Write(0u);
        writer.Write("WAVE"u8);
        writer.Write("fmt "u8);
        writer.Write(66u);
        writer.Write((ushort)0xFFFF);
        writer.Write(channels);
        writer.Write(rate);
        writer.Write(rate * channels * 2);
        writer.Write((ushort)(channels * 2));
        writer.Write((ushort)16);
        writer.Write((ushort)48);
        byte[] extra = new byte[48];
        BinaryPrimitives.WriteUInt32LittleEndian(extra.AsSpan(6), rate);
        writer.Write(extra);

        byte[] text = Encoding.UTF8.GetBytes(cue);
        writer.Write("LIST"u8);
        writer.Write((uint)(4 + 8 + 4 + text.Length + 1 + ((text.Length + 1) & 1)));
        writer.Write("adtl"u8);
        writer.Write("labl"u8);
        writer.Write((uint)(4 + text.Length + 1));
        writer.Write(0u);
        writer.Write(text);
        writer.Write((byte)0);
        if ((text.Length + 1) % 2 != 0)
        {
            writer.Write((byte)0);
        }

        writer.Write("data"u8);
        writer.Write(16u);
        writer.Write(new byte[16]);
        writer.Flush();
        byte[] bytes = stream.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), (uint)bytes.Length - 8);
        return bytes;
    }
}
