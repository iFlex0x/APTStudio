// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Core.Textures;

namespace AptStudio.Core.Tests.Textures;

public sealed class BitmapHeadTests
{
    [Fact]
    public void ParsesFlatBc1()
    {
        byte[] header = BitmapTestData.Header(512, 512, 10, 174_776, 0x001B);

        BitmapHead head = BitmapHead.Parse(header, "test");

        Assert.Equal(512, head.Width);
        Assert.Equal(512, head.Height);
        Assert.Equal(1, head.Surfaces);
        Assert.Equal(10, head.MipCount);
        Assert.Equal(BitmapKind.Flat, head.Kind);
        Assert.Equal(BitmapStorage.Raw, head.Storage);
        Assert.Equal(TextureCodec.Bc1, head.Format.Codec);
        Assert.Equal(0x78, head.PayloadOffset);
    }

    [Fact]
    public void ParsesSrgbBc3Lz4()
    {
        byte[] header = BitmapTestData.Header(
            2048, 2048, 12, 5_592_432, 0x021D, storage: BitmapStorage.Lz4, compressed: 4_807_234);

        BitmapHead head = BitmapHead.Parse(header, "test");

        Assert.Equal(2048, head.Width);
        Assert.Equal(2048, head.Height);
        Assert.Equal(12, head.MipCount);
        Assert.Equal(BitmapStorage.Lz4, head.Storage);
        Assert.Equal(TextureCodec.Bc3, head.Format.Codec);
    }

    [Fact]
    public void ParsesCube()
    {
        byte[] header = BitmapTestData.Header(
            256, 256, 9, 524_448, 0x0021, kind: BitmapKind.Cube, surfaces: 6);

        BitmapHead head = BitmapHead.Parse(header, "test");

        Assert.Equal(BitmapKind.Cube, head.Kind);
        Assert.Equal(6, head.Surfaces);
        Assert.Equal(TextureFamily.Block16, head.Format.Family);
        Assert.Equal(TextureCodec.Unknown, head.Format.Codec);
    }

    [Fact]
    public void ParsesFlatArray()
    {
        byte[] header = BitmapTestData.Header(4, 4, 1, 32, 0x001B, surfaces: 4);

        BitmapHead head = BitmapHead.Parse(header, "test");

        Assert.Equal(BitmapKind.Flat, head.Kind);
        Assert.Equal(4, head.Surfaces);
    }

    [Fact]
    public void ParsesVolumeWithoutClaimingDepth()
    {
        byte[] header = BitmapTestData.Header(
            17, 0, 1, 144_160, 0x0013, kind: BitmapKind.Volume);

        BitmapHead head = BitmapHead.Parse(header, "test");

        Assert.Equal(BitmapKind.Volume, head.Kind);
        Assert.Equal(17, head.Width);
        Assert.Equal(TextureFamily.Volume, head.Format.Family);
        Assert.Equal(TextureCodec.Unknown, head.Format.Codec);
    }

    [Fact]
    public void RejectsWrongClass()
    {
        byte[] header = BitmapTestData.Header(4, 4, 1, 8, 0x001B, classHash: 0x1234);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            BitmapHead.Parse(header, "test"));

        Assert.Contains("Bitmap_Z", error.Message);
    }

    [Fact]
    public void RejectsWrongVersion()
    {
        byte[] header = BitmapTestData.Header(4, 4, 1, 8, 0x001B, version: 5);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            BitmapHead.Parse(header, "test"));

        Assert.Contains("version 5", error.Message);
    }

    [Fact]
    public void RejectsWrongStorage()
    {
        byte[] header = BitmapTestData.Header(4, 4, 1, 8, 0x001B, storage: (BitmapStorage)1);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            BitmapHead.Parse(header, "test"));

        Assert.Contains("storage 1", error.Message);
    }

    [Fact]
    public void RejectsBodySizeMismatch()
    {
        byte[] header = BitmapTestData.Header(4, 4, 1, 8, 0x001B, bodySize: 13);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            BitmapHead.Parse(header, "test"));

        Assert.Contains("body size 13", error.Message);
    }

    [Fact]
    public void RejectsChainMismatch()
    {
        byte[] header = BitmapTestData.Header(512, 512, 10, 174_775, 0x001B);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            BitmapHead.Parse(header, "test"));

        Assert.Contains("chain", error.Message);
    }

    [Fact]
    public void RejectsVolumeFormatOnFlat()
    {
        byte[] header = BitmapTestData.Header(4, 4, 1, 8, 0x0013);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            BitmapHead.Parse(header, "test"));

        Assert.Contains("volume format", error.Message);
    }

    [Fact]
    public void RejectsZeroWidth()
    {
        byte[] header = BitmapTestData.Header(0, 4, 1, 8, 0x001B);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            BitmapHead.Parse(header, "test"));

        Assert.Contains("width", error.Message);
    }

    [Fact]
    public void RejectsZeroHeight()
    {
        byte[] header = BitmapTestData.Header(4, 0, 1, 8, 0x001B);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            BitmapHead.Parse(header, "test"));

        Assert.Contains("height", error.Message);
    }

    [Fact]
    public void RejectsZeroLinkSize()
    {
        byte[] header = BitmapTestData.Header(4, 4, 1, 8, 0x001B, linkSize: 0);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            BitmapHead.Parse(header, "test"));

        Assert.Contains("link header", error.Message);
    }

    [Fact]
    public void RejectsOtherLinkSize()
    {
        byte[] header = BitmapTestData.Header(4, 4, 1, 8, 0x001B, linkSize: 62);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            BitmapHead.Parse(header, "test"));

        Assert.Contains("link header size 62", error.Message);
    }

    [Fact]
    public void RejectsZeroStored()
    {
        byte[] header = BitmapTestData.Header(4, 4, 1, 0, 0x001B);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            BitmapHead.Parse(header, "test"));

        Assert.Contains("stored size is zero", error.Message);
    }

    [Fact]
    public void RejectsZeroSurfaces()
    {
        byte[] header = BitmapTestData.Header(4, 4, 1, 8, 0x001B, surfaces: 0);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            BitmapHead.Parse(header, "test"));

        Assert.Contains("surface count", error.Message);
    }

    [Fact]
    public void AcceptsUnknownFormat()
    {
        byte[] header = BitmapTestData.Header(4, 4, 1, 8, 0x002A);

        BitmapHead head = BitmapHead.Parse(header, "test");

        Assert.Equal(TextureFamily.Unknown, head.Format.Family);
        Assert.Equal(TextureCodec.Unknown, head.Format.Codec);
    }

    [Fact]
    public void RejectsRawWithCompressedSize()
    {
        byte[] header = BitmapTestData.Header(4, 4, 1, 8, 0x001B, compressed: 10);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            BitmapHead.Parse(header, "test"));

        Assert.Contains("compressed size 10", error.Message);
    }

    [Fact]
    public void RejectsLz4TooSmall()
    {
        byte[] header = BitmapTestData.Header(
            4, 4, 1, 8, 0x001B, storage: BitmapStorage.Lz4, compressed: 8);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            BitmapHead.Parse(header, "test"));

        Assert.Contains("compressed size 8", error.Message);
    }

    [Fact]
    public void RejectsMipCountOver32()
    {
        byte[] header = BitmapTestData.Header(4, 4, 33, 8, 0x001B);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            BitmapHead.Parse(header, "test"));

        Assert.Contains("mip count 33", error.Message);
    }

    [Fact]
    public void RejectsShortHeader()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            BitmapHead.Parse(new byte[0x77], "test"));

        Assert.Contains("need 120", error.Message);
    }
}
