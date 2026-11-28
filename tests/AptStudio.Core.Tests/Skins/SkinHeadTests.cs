// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Core.Skins;

namespace AptStudio.Core.Tests.Skins;

public sealed class SkinHeadTests
{
    [Theory]
    [InlineData((ushort)3, 3)]
    [InlineData((ushort)11, 11)]
    public void ReadsVersionExtra(ushort version, int extra)
    {
        SkinHead head = SkinHead.Parse(
            SkinTestData.Header(0x1234, linkSize: 146, version: version), "test");
        Assert.Equal(version, head.Version);
        Assert.Equal(0x1234UL, head.NameHash);
        Assert.Equal(0x33 + 146 + extra, head.PayloadOffset);
        Assert.Equal(SkinStorage.Lz4, head.Storage);
    }

    [Fact]
    public void RejectsOtherClasses()
    {
        Assert.Throws<InvalidDataException>(
            () => SkinHead.Parse(SkinTestData.Header(1, classHash: 0x7703A856BA5776DD), "test"));
    }

    [Fact]
    public void RejectsUnmeasuredVersions()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => SkinHead.Parse(SkinTestData.Header(1, version: 5), "test"));
        Assert.Contains("version 5", error.Message);
    }

    [Fact]
    public void RejectsRawStorage()
    {
        byte[] header = SkinTestData.Header(1);
        header[0x32] = 0;
        Assert.Throws<InvalidDataException>(() => SkinHead.Parse(header, "test"));
    }
}
