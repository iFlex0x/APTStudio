// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using AptStudio.Core.Assets;
using AptStudio.Core.Containers;
using AptStudio.Core.Materials;
using AptStudio.Core.Skeletons;
using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class BrowserKeysTests
{
    [Fact]
    public void HashTextFormatsEveryListTag()
    {
        AssetEntry texture = new(
            "A.DPC",
            "A.DPC",
            false,
            new DpcEntry(0xAAAA, AssetClass.Bitmap, 0, 1, 0, 0, 0, 0, 0),
            null,
            null);
        SkeletonEntry skeleton = new(
            "B.DPC",
            new SkeletonHit(2, 0xBBBB, 0, 0, 0, 0, 1, 0));
        MaterialRow material = new(
            new MaterialEntry(
                @"C:\game\DATAS\C.DPC",
                new MaterialHit(3, 0xCCCC, 0, MaterialHead.MaterialClass, 0)),
            "CODE_EYE",
            2,
            MaterialRowState.Ok,
            "");

        Assert.Equal("0x000000000000AAAA", BrowserKeys.HashText(texture));
        Assert.Equal("0x000000000000BBBB", BrowserKeys.HashText(skeleton));
        Assert.Equal("0x000000000000CCCC", BrowserKeys.HashText(material));
        Assert.Null(BrowserKeys.HashText("C.DPC"));
        Assert.Null(BrowserKeys.HashText(null));
    }

    [Fact]
    public void OccurrenceKeysKeepDuplicatesButShareTheArchiveAndOffsetRule()
    {
        AssetEntry first = new(
            @"C:\game\A.DPC",
            @"C:\game\A.DPC",
            false,
            new DpcEntry(0xAAAA, AssetClass.Bitmap, 10, 1, 0, 0, 0, 0, 0),
            null,
            null);
        AssetEntry second = first with { ArchivePath = @"C:\game\B.DPC" };
        AssetEntry third = first with
        {
            Row = new DpcEntry(0xAAAA, AssetClass.Bitmap, 0, 11, 1, 0, 0, 0, 0),
        };

        TextureOccurrence occurrence = new(
            0xAAAA,
            first.ArchivePath,
            first.BodyArchivePath,
            first.Row.BodyOffset,
            null!);
        MeshOccurrence mesh = new(
            0xAAAA,
            first.ArchivePath,
            first.BodyArchivePath,
            first.Row.BodyOffset);
        SkinOccurrence skin = new(
            0xAAAA,
            first.ArchivePath,
            first.BodyArchivePath,
            first.Row.BodyOffset);

        Assert.Equal(BrowserKeys.Texture(first), BrowserKeys.Mesh(first));
        Assert.Equal(BrowserKeys.Texture(first), BrowserKeys.Skin(first));
        Assert.Equal(BrowserKeys.Texture(first), BrowserKeys.Texture(occurrence));
        Assert.Equal(BrowserKeys.Mesh(first), BrowserKeys.Mesh(mesh));
        Assert.Equal(BrowserKeys.Skin(first), BrowserKeys.Skin(skin));
        Assert.NotEqual(BrowserKeys.Texture(first), BrowserKeys.Texture(second));
        Assert.NotEqual(BrowserKeys.Texture(first), BrowserKeys.Texture(third));
    }
}
