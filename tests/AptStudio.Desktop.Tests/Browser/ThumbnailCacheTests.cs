// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Drawing;
using AptStudio.Core.Containers;
using AptStudio.Core.Textures;
using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class ThumbnailCacheTests
{
    [Fact]
    public void ThumbnailMipUsesASmallMip()
    {
        Assert.Equal(6, ThumbnailCache.ThumbnailMip(Head(2048, 12)));
        Assert.Equal(1, ThumbnailCache.ThumbnailMip(Head(64, 2)));
        Assert.Equal(0, ThumbnailCache.ThumbnailMip(Head(16, 1)));
    }

    [Fact]
    public void MissingThumbnailReturnsPlaceholderWithoutDecoding()
    {
        using ThumbnailCache cache = new();
        TextureEntry texture = new(
            "missing.DPC",
            new DpcEntry(0, BitmapHead.ClassHash, 0, 0, BitmapHead.HeaderSize, 0, 0, 0, 0),
            Head(512, 10));

        Bitmap image = cache.GetCached(texture);

        Assert.Equal(32, image.Width);
        Assert.Equal(32, image.Height);
    }

    private static BitmapHead Head(int width, int mips)
    {
        ushort key = 0x1B;
        TextureFormat format = TextureFormats.FromKey(key);
        return new BitmapHead(
            0,
            0,
            BitmapKind.Flat,
            width,
            width,
            1,
            mips,
            BitmapStorage.Raw,
            0,
            4,
            0,
            BitmapHead.LinkHeaderSize,
            key,
            format,
            BitmapHead.HeaderSize);
    }
}
