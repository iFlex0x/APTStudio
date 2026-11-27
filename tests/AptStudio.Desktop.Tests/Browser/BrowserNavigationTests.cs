using System.Drawing;
using AptStudio.Core.Textures;
using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class BrowserNavigationTests
{
    private static readonly TextureOccurrence Eye = new(
        0x6B6CD7B43802AB3F,
        "GREECE.DPC",
        "GREECE.DPC",
        655357888,
        Head(512, 512, 10, 0x1B));

    private static readonly TextureOccurrence CommonEye = new(
        0x6B6CD7B43802AB3F,
        "COMMON.DPC",
        "COMMON.DPC",
        266157280,
        Head(512, 512, 10, 0x1B));

    private static readonly MaterialOccurrence ApocalypseEye = new(
        0x025BC135442354FB,
        "APOCALYPSE.DPC",
        123456,
        "CODE_EYE");

    private static BrowserState TextureState() => new(
        BrowserView.Textures,
        "GREECE.DPC",
        "eye",
        new TextureFilter(TextureCodec.Bc1, 512, 512, 10, true),
        Eye,
        412,
        3,
        ChannelView.Rgb,
        true,
        2.5f,
        new PointF(14, -9),
        null,
        0,
        null,
        null);

    [Fact]
    public void ChooserIsTransientAndBackDismissesIt()
    {
        BrowserNavigation navigation = new(TextureState());

        navigation.OpenChooser();

        Assert.True(navigation.IsChooserOpen);
        Assert.True(navigation.CanBack);

        navigation.Back();

        Assert.False(navigation.IsChooserOpen);
        Assert.Equal(Eye, navigation.State.Texture);
        Assert.False(navigation.CanBack);
        Assert.False(navigation.CanForward);
    }

    [Fact]
    public void OpenMaterialPushesSourceAndKeepsExactSlot()
    {
        BrowserNavigation navigation = new(TextureState());
        navigation.OpenChooser();

        navigation.OpenMaterial(ApocalypseEye, 13);

        Assert.Equal(BrowserView.Material, navigation.State.View);
        Assert.Equal(ApocalypseEye, navigation.State.Material);
        Assert.Equal(13, navigation.State.MaterialSlot);

        navigation.Back();

        Assert.Equal(BrowserView.Textures, navigation.State.View);
        Assert.Equal(Eye, navigation.State.Texture);
        Assert.False(navigation.CanBack);
    }

    [Fact]
    public void OpenLinkedTextureAndBackTwice()
    {
        BrowserNavigation navigation = new(TextureState());
        navigation.OpenChooser();
        navigation.OpenMaterial(ApocalypseEye, 13);

        navigation.OpenLinkedTexture(CommonEye);

        Assert.Equal(BrowserView.LinkedTexture, navigation.State.View);
        Assert.Equal(CommonEye, navigation.State.LinkedTexture);

        navigation.Back();

        Assert.Equal(BrowserView.Material, navigation.State.View);
        Assert.Equal(13, navigation.State.MaterialSlot);

        navigation.Back();

        Assert.Equal(BrowserView.Textures, navigation.State.View);
        Assert.Equal(Eye, navigation.State.Texture);
        Assert.Equal("eye", navigation.State.Query);
        Assert.Equal(TextureCodec.Bc1, navigation.State.Filter.Codec);
        Assert.Equal(412, navigation.State.ListIndex);
        Assert.Equal(3, navigation.State.Mip);
        Assert.Equal(2.5f, navigation.State.Zoom);
        Assert.Equal(new PointF(14, -9), navigation.State.Pan);
    }

    [Fact]
    public void RevealAddsOneBackStep()
    {
        BrowserNavigation navigation = new(TextureState());
        navigation.OpenChooser();
        navigation.OpenMaterial(ApocalypseEye, 13);
        navigation.OpenLinkedTexture(CommonEye);

        navigation.RevealLinkedTexture();

        Assert.Equal(BrowserView.Textures, navigation.State.View);
        Assert.Equal("COMMON.DPC", navigation.State.ArchivePath);
        Assert.Equal("", navigation.State.Query);
        Assert.True(navigation.State.Filter.IsAll);
        Assert.Equal(CommonEye, navigation.State.Texture);

        navigation.Back();
        Assert.Equal(BrowserView.LinkedTexture, navigation.State.View);

        navigation.Back();
        Assert.Equal(BrowserView.Material, navigation.State.View);

        navigation.Back();
        Assert.Equal(BrowserView.Textures, navigation.State.View);
        Assert.Equal("GREECE.DPC", navigation.State.ArchivePath);
    }

    [Fact]
    public void SwitchScopeClearsHistory()
    {
        BrowserNavigation navigation = new(TextureState());
        navigation.OpenChooser();
        navigation.OpenMaterial(ApocalypseEye, 13);
        Assert.True(navigation.CanBack);

        navigation.SwitchScope("COMMON.DPC", BrowserView.Textures);

        Assert.False(navigation.CanBack);
        Assert.False(navigation.CanForward);
        Assert.Equal("COMMON.DPC", navigation.State.ArchivePath);
    }

    [Theory]
    [InlineData(TextureCodec.Bc1, 512, 512, 10, true, true)]
    [InlineData(TextureCodec.Bc3, 512, 512, 10, true, false)]
    [InlineData(TextureCodec.Bc1, 256, 512, 10, true, false)]
    [InlineData(TextureCodec.Bc1, 512, 512, 9, true, false)]
    [InlineData(TextureCodec.Bc1, 512, 512, 10, false, true)]
    public void TextureFilterMatchesCodecSizeMipsAndPreviewable(
        TextureCodec codec,
        int width,
        int height,
        int mips,
        bool previewable,
        bool expected)
    {
        TextureFilter filter = new(codec, 512, 512, 10, previewable);
        BitmapHead head = Head(width, height, mips, 0x1B);

        Assert.Equal(expected, filter.Matches(head));
    }

    private static BitmapHead Head(int width, int height, int mips, ushort key)
    {
        TextureFormat format = TextureFormats.FromKey(key);
        return new BitmapHead(
            0,
            0,
            BitmapKind.Flat,
            width,
            height,
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
