using System.Buffers.Binary;
using System.Text;
using AptStudio.Core.Audio;

namespace AptStudio.Core.Tests.Audio;

public sealed class WemInfoTests
{
    [Fact]
    public void ReadsFormatSamplesAndCue()
    {
        byte[] wem = BuildWem(44100, 2, "mus_test");
        using MemoryStream stream = new(wem);
        WemInfo info = WemInfo.TryRead(stream, 0, wem.Length, "test")!;

        Assert.Equal(0xFFFF, info.FormatTag);
        Assert.Equal("Vorbis", info.Codec);
        Assert.Equal(2, info.Channels);
        Assert.Equal(44100u, info.SampleRate);
        Assert.Equal(44100u, info.SampleCount);
        Assert.Equal(1.0, info.DurationSeconds, 6);
        Assert.Equal("mus_test", info.Cue);
    }

    [Fact]
    public void VersionLabelIsNotAName()
    {
        byte[] wem = BuildWem(48000, 1, "v1.5");
        using MemoryStream stream = new(wem);
        WemInfo info = WemInfo.TryRead(stream, 0, wem.Length, "test")!;
        Assert.Null(info.Cue);
    }

    [Fact]
    public void NonRiffReturnsNull()
    {
        byte[] plugin = new byte[64];
        using MemoryStream stream = new(plugin);
        Assert.Null(WemInfo.TryRead(stream, 0, plugin.Length, "test"));
    }

    [Fact]
    public void RejectsChunkPastBody()
    {
        byte[] wem = BuildWem(48000, 1, null);
        BinaryPrimitives.WriteUInt32LittleEndian(wem.AsSpan(16), 10_000);
        using MemoryStream stream = new(wem);
        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => WemInfo.TryRead(stream, 0, wem.Length, "test"));
        Assert.Contains("runs past the WEM", error.Message);
    }

    [Fact]
    public void RejectsMissingFormat()
    {
        byte[] wem = BuildWem(48000, 1, null);
        Encoding.ASCII.GetBytes("junk").CopyTo(wem, 12);
        using MemoryStream stream = new(wem);
        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => WemInfo.TryRead(stream, 0, wem.Length, "test"));
        Assert.Contains("no measured fmt chunk", error.Message);
    }

    private static byte[] BuildWem(uint rate, ushort channels, string? cue)
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

        if (cue is not null)
        {
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
