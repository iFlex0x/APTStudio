// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Buffers.Binary;
using AptStudio.Core.Materials;

namespace AptStudio.Core.Tests.Materials;

public sealed class MaterialDataTests
{
    [Fact]
    public void ReadsCodeAndSlots()
    {
        byte[] body = MaterialTestData.Body((0, 0xA1), (13, 0xA2), (22, 0xA3));
        byte[] obj = MaterialTestData.Object(body);

        MaterialHead head = MaterialHead.Parse(obj, "TEST");
        MaterialData material = MaterialData.Parse(head, body, "TEST");

        Assert.Equal(5u, material.Version);
        Assert.Equal("CODE_DIFFUSE", material.Code);
        Assert.Equal(MaterialTestData.Name, material.Head.NameHash);
        Assert.Equal(3, material.Slots.Count);
        Assert.Equal([0, 13, 22], material.Slots.Select(slot => slot.Index));
        Assert.Equal("sDiffuse", material.Slots[0].Role.Binding);
        Assert.Equal("sNormalRoughMetal", material.Slots[1].Role.Binding);
    }

    [Fact]
    public void ReadsLz4Object()
    {
        byte[] body = MaterialTestData.Body((0, 0xB1), (2, 0xB2));
        byte[] obj = MaterialTestData.Object(body, lz4: true);
        string path = MaterialTestData.TempFile(obj);
        try
        {
            MaterialHead head = MaterialHead.Parse(obj, "TEST");
            using FileStream stream = File.OpenRead(path);
            MaterialData material = MaterialDecoder.Read(
                stream, "TEST.DPC", new MaterialHit(0, MaterialTestData.Name, MaterialTestData.Link,
                    MaterialHead.MaterialClass, (uint)obj.Length));

            Assert.Equal("CODE_DIFFUSE", material.Code);
            Assert.Equal(2, material.Slots.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadsAnimationClass()
    {
        byte[] body = MaterialTestData.Body((0, 0xC1));
        byte[] obj = MaterialTestData.Object(body, classHash: MaterialHead.AnimClass);

        MaterialHead head = MaterialHead.Parse(obj, "TEST");

        Assert.True(head.IsAnimation);
        Assert.Equal(MaterialHead.AnimClass, head.ClassHash);
    }

    [Fact]
    public void RejectsMaskMissingHashBit()
    {
        byte[] body = MaterialTestData.Body((0, 0xD1));
        int table = 0x190 + MaterialTestData.Code.Length + 1;
        BinaryPrimitives.WriteUInt64LittleEndian(body.AsSpan(table), 0);
        byte[] obj = MaterialTestData.Object(body);
        MaterialHead head = MaterialHead.Parse(obj, "TEST");

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => MaterialData.Parse(head, body, "TEST"));
        Assert.Contains("missing bits", error.Message);
    }

    [Fact]
    public void AcceptsEmptyMaskBit()
    {
        byte[] body = MaterialTestData.Body();
        int table = 0x190 + MaterialTestData.Code.Length + 1;
        BinaryPrimitives.WriteUInt64LittleEndian(body.AsSpan(table), 1);
        byte[] obj = MaterialTestData.Object(body);
        MaterialHead head = MaterialHead.Parse(obj, "TEST");

        MaterialData material = MaterialData.Parse(head, body, "TEST");

        Assert.Empty(material.Slots);
        Assert.Equal(1UL, material.Mask);
    }

    [Fact]
    public void RejectsUnmeasuredVersion()
    {
        byte[] body = MaterialTestData.Body((0, 0xE1));
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(0x130), 4);
        byte[] obj = MaterialTestData.Object(body);
        MaterialHead head = MaterialHead.Parse(obj, "TEST");

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => MaterialData.Parse(head, body, "TEST"));
        Assert.Contains("version 4", error.Message);
    }

    [Fact]
    public void RejectsShortBody()
    {
        byte[] obj = MaterialTestData.Object(MaterialTestData.Body((0, 0xF1)));
        MaterialHead head = MaterialHead.Parse(obj, "TEST");

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => MaterialData.Parse(head, new byte[0x100], "TEST"));
        Assert.Contains("need 876", error.Message);
    }

    [Fact]
    public void RejectsBodySizeMismatch()
    {
        byte[] obj = MaterialTestData.Object(MaterialTestData.Body((0, 0x11)));
        BinaryPrimitives.WriteUInt32LittleEndian(obj.AsSpan(0x20), 1);

        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => MaterialHead.Parse(obj, "TEST"));
        Assert.Contains("body size", error.Message);
    }
}
