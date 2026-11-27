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
}
