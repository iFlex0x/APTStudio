// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using AptStudio.Core.Assets;
using AptStudio.Core.Containers;
using AptStudio.Core.Links;
using AptStudio.Core.Skins;
using AptStudio.Core.Tests.Skins;
using K4os.Compression.LZ4;

namespace AptStudio.Core.Tests.Links;

public sealed class SkinLinkTests
{
    [Fact]
    public void ReadsLinksAndPartCount()
    {
        string path = WriteSkin(
            0x1234,
            new TestPart(2, 12, 0xAA, Array.Empty<ulong>(), SkinTestData.Rigid(2)),
            new TestPart(1, 12, 0xBB, Array.Empty<ulong>(), SkinTestData.Rigid(1)));
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read);
            SkinLinkData data = SkinLinkReader.Read(stream, path, Row(0x1234, 1));

            Assert.Equal(2, data.MeshLinks.Count);
            Assert.Equal(0x1000UL, data.MeshLinks[0]);
            Assert.Equal(0x1001UL, data.MeshLinks[1]);
            Assert.Equal(2, data.PartCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RejectsWrongClass()
    {
        string path = WriteSkin(0x1234, new TestPart(1, 12, 0, Array.Empty<ulong>(), SkinTestData.Rigid(1)));
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read);
            DpcEntry row = Row(0x1234, 1) with { ClassHash = 0x9999 };
            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => SkinLinkReader.Read(stream, path, row));
            Assert.Contains("not Skin_Z", error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RejectsPartCountLargerThanMeshes()
    {
        byte[] body = SkinTestData.Body(
            new TestPart(1, 12, 0, Array.Empty<ulong>(), SkinTestData.Rigid(1)));
        BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(12), 2);
        SkinHead head = SkinHead.Parse(SkinTestData.Header(1), "test");
        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => SkinLinkReader.ParseBody(head, body, "test"));
        Assert.Contains("part count 2", error.Message);
    }

    [Fact]
    public void IndexSharesMeshAcrossSkins()
    {
        string first = WriteSkin(0x100, new TestPart(1, 12, 0, Array.Empty<ulong>(), SkinTestData.Rigid(1)));
        string second = WriteSkin(0x200, new TestPart(1, 12, 0, Array.Empty<ulong>(), SkinTestData.Rigid(1)));
        try
        {
            AssetEntry one = Entry(first, 0x100);
            AssetEntry two = Entry(second, 0x200);
            SkinLinkIndex index = SkinLinkIndex.Build([one, two]);

            Assert.Empty(index.Failures);
            Assert.Equal(2, index.Entries.Count);
            Assert.Single(index.ByMesh);
            Assert.Equal(2, index.ByMesh[0x1000].Count);
        }
        finally
        {
            File.Delete(first);
            File.Delete(second);
        }
    }

    [Fact]
    public void RejectsShortStoredSize()
    {
        byte[] body = SkinTestData.Body(
            new TestPart(1, 12, 0, Array.Empty<ulong>(), SkinTestData.Rigid(1)));
        byte[] header = SkinTestData.Header(0x1234, bodySize: (uint)body.Length, storedSize: 4);
        int payload = 1 * 32 + 0x33 + 12 + 11;
        byte[] file = new byte[payload + 8];
        header.CopyTo(file, 32);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(payload), (uint)body.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(payload + 4), 4);
        string path = Path.Combine(Path.GetTempPath(), $"aptstudio-skin-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, file);
        try
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read);
            InvalidDataException error = Assert.Throws<InvalidDataException>(
                () => SkinLinkReader.Read(stream, path, Row(0x1234, 1)));
            Assert.Contains("body sizes", error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static DpcEntry Row(ulong name, uint offset) => new(
        name, SkinHead.SkinClass, name, offset, 0, 0, 0, 0, 0);

    private static AssetEntry Entry(string path, ulong name) =>
        new(path, path, false, Row(name, 1), null, null);

    private static string WriteSkin(ulong name, params TestPart[] parts)
    {
        byte[] body = SkinTestData.Body(parts);
        byte[] block = new byte[LZ4Codec.MaximumOutputSize(body.Length)];
        int written = LZ4Codec.Encode(body, block);
        uint stored = (uint)(written + 8);
        byte[] header = SkinTestData.Header(name, bodySize: (uint)body.Length, storedSize: stored);
        int payload = 1 * 32 + 0x33 + 12 + 11;
        byte[] file = new byte[payload + stored];
        header.CopyTo(file, 32);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(payload), (uint)body.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(payload + 4), stored);
        block.AsSpan(0, written).CopyTo(file.AsSpan(payload + 8));
        string path = Path.Combine(Path.GetTempPath(), $"aptstudio-skin-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, file);
        return path;
    }
}
