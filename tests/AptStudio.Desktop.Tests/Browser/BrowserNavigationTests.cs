using System.Drawing;
using AptStudio.Core.Audio;
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

    private static readonly MaterialOccurrence GreeceEye = new(
        0x011C25CCA6037D8E,
        "GREECE.DPC",
        654321,
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
        Assert.False(navigation.State.MaterialFromList);
        Assert.Null(navigation.State.MaterialError);

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
    public void ChangingLinkedCopyDoesNotAddHistory()
    {
        BrowserNavigation navigation = new(TextureState());
        navigation.OpenChooser();
        navigation.OpenMaterial(ApocalypseEye, 13);
        navigation.OpenLinkedTexture(CommonEye);
        TextureOccurrence other = new(
            0xF037E191BA17A32A,
            "COMMON.DPC",
            "COMMON.DPC",
            256567616,
            Head(256, 256, 9, 0x1D));

        navigation.SetLinkedTextureCopy(other);

        Assert.Equal(other, navigation.State.LinkedTexture);
        Assert.Equal(BrowserView.LinkedTexture, navigation.State.View);

        navigation.Back();
        Assert.Equal(BrowserView.Material, navigation.State.View);
        Assert.Equal(13, navigation.State.MaterialSlot);
    }

    [Fact]
    public void NestedMaterialBackKeepsEachMaterial()
    {
        BrowserNavigation navigation = new(TextureState());
        navigation.OpenChooser();
        navigation.OpenMaterial(ApocalypseEye, 0);
        navigation.OpenLinkedTexture(CommonEye);
        navigation.OpenChooser();
        navigation.OpenMaterial(GreeceEye, 0);

        Assert.Equal(GreeceEye, navigation.State.Material);

        navigation.Back();

        Assert.Equal(BrowserView.LinkedTexture, navigation.State.View);
        Assert.Equal(ApocalypseEye, navigation.State.Material);

        navigation.Back();

        Assert.Equal(BrowserView.Material, navigation.State.View);
        Assert.Equal(ApocalypseEye, navigation.State.Material);

        navigation.Forward();
        Assert.Equal(BrowserView.LinkedTexture, navigation.State.View);

        navigation.Forward();
        Assert.Equal(BrowserView.Material, navigation.State.View);
        Assert.Equal(GreeceEye, navigation.State.Material);
    }

    [Fact]
    public void SortChangesStateWithoutHistoryOrTextureChange()
    {
        BrowserNavigation navigation = new(TextureState());

        navigation.SetSort(TextureSort.Hash, true);

        Assert.Equal(TextureSort.Hash, navigation.State.Sort);
        Assert.True(navigation.State.SortDescending);
        Assert.Equal(Eye, navigation.State.Texture);
        Assert.False(navigation.CanBack);
    }

    [Fact]
    public void AudioOccurrenceSurvivesFilterAndSort()
    {
        AudioEntry entry = new(
            @"C:\game\SOUNDBANKS\VO_MEDIA.PC.PCK",
            "VO_MEDIA.PC.PCK",
            0,
            0x1EEAB9D4,
            2497360640,
            576565,
            4,
            "french(france)",
            new WemInfo(0xFFFF, 1, 48000, 2720160, null),
            null);
        BrowserNavigation navigation = new(TextureState());

        navigation.SelectAudio(entry, 37);
        navigation.SetAudioFilter("VO_MEDIA.PC.PCK", "french(france)");
        navigation.SetAudioSort(AudioSort.Length, true);

        Assert.Same(entry, navigation.State.Audio);
        Assert.Equal(BrowserView.Audio, navigation.State.View);
        Assert.Equal(37, navigation.State.ListIndex);
        Assert.Equal("VO_MEDIA.PC.PCK", navigation.State.AudioPack);
        Assert.Equal("french(france)", navigation.State.AudioLanguage);
        Assert.Equal(AudioSort.Length, navigation.State.AudioSort);
        Assert.True(navigation.State.AudioSortDescending);
        Assert.NotEqual(
            BrowserKeys.Audio(entry),
            BrowserKeys.Audio(entry with { LanguageId = 1 }));

        navigation.ClearAudio();

        Assert.Null(navigation.State.Audio);
    }

    [Fact]
    public void MeshSkinSkeletonRoundTripRestoresCameraPartAndWireframe()
    {
        MeshOccurrence mesh = new(
            0xF626B0795727EAA6,
            @"C:\game\DATAS\SHARED.DPC",
            @"C:\game\DATAS\SHARED.DPC",
            485683);
        SkinOccurrence skin = new(
            0x7D6A523E1499A737,
            @"C:\game\DATAS\SHARED.DPC",
            @"C:\game\DATAS\SHARED.DPC",
            5101973);
        SkeletonOccurrence skeleton = new(
            0x314127CD0F66EADA,
            @"C:\game\DATAS\SHARED.DPC",
            4203747);
        BrowserNavigation navigation = new(TextureState());

        navigation.SelectMesh(mesh, 37);
        MeshCamera camera = new(1.2f, -0.3f, 2.4f, new PointF(11f, -7f));
        navigation.SetMeshView(camera, true);
        navigation.OpenSkin(skin, 7);
        navigation.SetSkinPart(0);
        navigation.OpenSkeleton(skeleton);

        Assert.Equal(BrowserView.Skeleton, navigation.State.View);

        navigation.Back();
        Assert.Equal(BrowserView.Skin, navigation.State.View);
        Assert.Equal(0, navigation.State.SkinPart);
        Assert.Equal(skin, navigation.State.Skin);

        navigation.Back();
        Assert.Equal(BrowserView.Meshes, navigation.State.View);
        Assert.Equal(mesh, navigation.State.Mesh);
        Assert.Equal(camera, navigation.State.MeshCamera);
        Assert.True(navigation.State.MeshWireframe);
        Assert.Equal(37, navigation.State.ListIndex);
    }

    [Fact]
    public void DirectSkinSelectionAndOpenBackRestoreTheSecondRow()
    {
        SkinOccurrence first = new(
            0x84316AA817823056,
            @"C:\game\DATAS\LEVELS\SCENE.DPC",
            @"C:\game\DATAS\COMMON.DPC",
            5101973);
        SkinOccurrence second = new(
            0x84316AA817823056,
            @"C:\game\DATAS\LEVELS\SCENE.DPC",
            @"C:\game\DATAS\COMMON.DPC",
            5102041);
        BrowserNavigation navigation = new(new BrowserState(
            BrowserView.Skins,
            @"C:\game\DATAS\LEVELS\SCENE.DPC",
            "",
            TextureFilter.All,
            null,
            12,
            0,
            ChannelView.Rgb,
            true,
            1f,
            PointF.Empty,
            null,
            0,
            null,
            null));

        navigation.SetSkinListSelection(first, 11);
        navigation.SetSkinListSelection(second, 12);

        Assert.Equal(BrowserView.Skins, navigation.State.View);
        Assert.Equal(second, navigation.State.Skin);
        Assert.False(navigation.CanBack);

        navigation.OpenSkin(second, 0);
        Assert.Equal(BrowserView.Skin, navigation.State.View);
        Assert.Null(navigation.State.Mesh);
        Assert.True(navigation.CanBack);

        navigation.Back();

        Assert.Equal(BrowserView.Skins, navigation.State.View);
        Assert.Equal(second, navigation.State.Skin);
        Assert.Equal(12, navigation.State.ListIndex);
        Assert.False(navigation.CanBack);
    }

    [Fact]
    public void DirectSkeletonSelectionAndOpenBackRestoreTheSecondRow()
    {
        SkeletonOccurrence first = new(
            0xC81A1F5B0C7D7DBB,
            @"C:\game\DATAS\LEVELS\HUG.DPC",
            712345);
        SkeletonOccurrence second = new(
            0xC81A1F5B0C7D7DBB,
            @"C:\game\DATAS\LEVELS\HUG.DPC",
            712410);
        BrowserNavigation navigation = new(new BrowserState(
            BrowserView.Skeletons,
            @"C:\game\DATAS\LEVELS\HUG.DPC",
            "",
            TextureFilter.All,
            null,
            12,
            0,
            ChannelView.Rgb,
            true,
            1f,
            PointF.Empty,
            null,
            0,
            null,
            null));

        navigation.SetSkeletonListSelection(first, 11);
        navigation.SetSkeletonListSelection(second, 12);

        Assert.Equal(BrowserView.Skeletons, navigation.State.View);
        Assert.Equal(second, navigation.State.Skeleton);
        Assert.Null(navigation.State.Skin);
        Assert.False(navigation.CanBack);

        navigation.OpenSkeleton(second);
        Assert.Equal(BrowserView.Skeleton, navigation.State.View);
        Assert.Null(navigation.State.Skin);
        Assert.Null(navigation.State.Mesh);
        Assert.True(navigation.CanBack);

        navigation.Back();

        Assert.Equal(BrowserView.Skeletons, navigation.State.View);
        Assert.Equal(second, navigation.State.Skeleton);
        Assert.Equal(12, navigation.State.ListIndex);
        Assert.False(navigation.CanBack);
    }

    [Fact]
    public void SkinPartMeshBackTwiceRestoresPartAndSourceMesh()
    {
        MeshOccurrence source = new(
            0x2C9F3F3CB3EF2F04,
            @"C:\game\DATAS\LEVELS\GENERIC_KISSING_COUPLE_01_HUG.DPC",
            @"C:\game\DATAS\LEVELS\GENERIC_KISSING_COUPLE_01_HUG.DPC",
            305905920);
        MeshOccurrence torso = new(
            0x38433BB867A98701,
            @"C:\game\DATAS\LEVELS\GENERIC_KISSING_COUPLE_01_HUG.DPC",
            @"C:\game\DATAS\LEVELS\GENERIC_KISSING_COUPLE_01_HUG.DPC",
            306013440);
        SkinOccurrence skin = new(
            0x84316AA817823056,
            @"C:\game\DATAS\LEVELS\GENERIC_KISSING_COUPLE_01_HUG.DPC",
            @"C:\game\DATAS\LEVELS\GENERIC_KISSING_COUPLE_01_HUG.DPC",
            61207488);
        BrowserNavigation navigation = new(TextureState());
        navigation.SelectMesh(source, 15);
        MeshCamera camera = new(0.65f, 0.35f, 1.32f, new PointF(12f, -4f));
        navigation.SetMeshView(camera, true);
        navigation.SetQuery("2c9f3f3c");
        navigation.OpenSkin(skin, 11);
        navigation.SetSkinPart(10);

        navigation.OpenMeshFromSkin(torso, 0);

        Assert.Equal(BrowserView.Meshes, navigation.State.View);
        Assert.Equal(torso, navigation.State.Mesh);
        Assert.Equal(torso.ArchivePath, navigation.State.ArchivePath);
        Assert.Equal("", navigation.State.Query);
        Assert.Null(navigation.State.Skin);
        Assert.True(navigation.CanBack);

        navigation.Back();

        Assert.Equal(BrowserView.Skin, navigation.State.View);
        Assert.Equal(skin, navigation.State.Skin);
        Assert.Equal(10, navigation.State.SkinPart);
        Assert.Equal(source, navigation.State.Mesh);
        Assert.Equal(camera, navigation.State.MeshCamera);
        Assert.True(navigation.State.MeshWireframe);

        navigation.Back();

        Assert.Equal(BrowserView.Meshes, navigation.State.View);
        Assert.Equal(source, navigation.State.Mesh);
        Assert.Equal(camera, navigation.State.MeshCamera);
        Assert.True(navigation.State.MeshWireframe);
        Assert.Equal("2c9f3f3c", navigation.State.Query);
        Assert.Equal(15, navigation.State.ListIndex);
        Assert.False(navigation.CanBack);
    }

    [Fact]
    public void DirectSkeletonUsedBySkinBackTwiceRestoresSkeletonAndList()
    {
        SkeletonOccurrence skeleton = new(
            0xC81A1F5B0C7D7DBB,
            @"C:\game\DATAS\LEVELS\GENERIC_KISSING_COUPLE_01_HUG.DPC",
            294282);
        SkinOccurrence skin = new(
            0x84316AA817823056,
            @"C:\game\DATAS\LEVELS\GENERIC_KISSING_COUPLE_01_HUG.DPC",
            @"C:\game\DATAS\LEVELS\GENERIC_KISSING_COUPLE_01_HUG.DPC",
            61207488);
        BrowserNavigation navigation = new(new BrowserState(
            BrowserView.Skeletons,
            @"C:\game\DATAS\LEVELS\GENERIC_KISSING_COUPLE_01_HUG.DPC",
            "",
            TextureFilter.All,
            null,
            1,
            0,
            ChannelView.Rgb,
            true,
            1f,
            PointF.Empty,
            null,
            0,
            null,
            null));

        navigation.SetSkeletonListSelection(skeleton, 1);
        navigation.OpenSkeleton(skeleton);
        Assert.Equal(BrowserView.Skeleton, navigation.State.View);

        navigation.OpenSkin(skin, 0);
        Assert.Equal(BrowserView.Skin, navigation.State.View);
        Assert.Null(navigation.State.Mesh);

        navigation.Back();
        Assert.Equal(BrowserView.Skeleton, navigation.State.View);
        Assert.Equal(skeleton, navigation.State.Skeleton);

        navigation.Back();
        Assert.Equal(BrowserView.Skeletons, navigation.State.View);
        Assert.Equal(skeleton, navigation.State.Skeleton);
        Assert.Equal(1, navigation.State.ListIndex);
        Assert.False(navigation.CanBack);
    }

    [Fact]
    public void DirectMaterialTextureBackTwiceRestoresSlotAndList()
    {
        MaterialOccurrence material = new(
            0xECE8A8E11B7F30B8,
            @"C:\game\DATAS\VENICE.DPC",
            123456,
            "CODE_EYE");
        TextureOccurrence texture = new(
            0x4B69508121C710A7,
            @"C:\game\DATAS\VENICE.DPC",
            @"C:\game\DATAS\VENICE.DPC",
            654321,
            Head(256, 256, 9, 0x1B));
        BrowserNavigation navigation = new(new BrowserState(
            BrowserView.Materials,
            @"C:\game\DATAS\VENICE.DPC",
            "CODE_EYE",
            TextureFilter.All,
            null,
            0,
            0,
            ChannelView.Rgb,
            true,
            1f,
            PointF.Empty,
            null,
            0,
            null,
            null));

        navigation.SetMaterialListSelection(material, 41, 1234);
        Assert.Equal(BrowserView.Materials, navigation.State.View);
        Assert.Equal(material, navigation.State.Material);
        Assert.True(navigation.State.MaterialFromList);
        Assert.Equal(1234, navigation.State.ListScroll);
        Assert.False(navigation.CanBack);

        navigation.OpenMaterialFromList(material, 3);
        Assert.Equal(BrowserView.Material, navigation.State.View);
        Assert.Equal(3, navigation.State.MaterialSlot);
        Assert.True(navigation.State.MaterialFromList);
        Assert.True(navigation.CanBack);

        navigation.OpenLinkedTexture(texture);
        Assert.Equal(BrowserView.LinkedTexture, navigation.State.View);

        navigation.Back();
        Assert.Equal(BrowserView.Material, navigation.State.View);
        Assert.Equal(3, navigation.State.MaterialSlot);
        Assert.Equal(material, navigation.State.Material);

        navigation.Back();
        Assert.Equal(BrowserView.Materials, navigation.State.View);
        Assert.Equal(material, navigation.State.Material);
        Assert.Equal(41, navigation.State.ListIndex);
        Assert.Equal(1234, navigation.State.ListScroll);
        Assert.True(navigation.State.MaterialFromList);
        Assert.False(navigation.CanBack);
    }

    [Fact]
    public void DirectMaterialErrorOpensWithReadErrorAndBackReturnsList()
    {
        MaterialOccurrence material = new(
            0x2222222222222222,
            @"C:\game\DATAS\VENICE.DPC",
            654321,
            "");
        BrowserNavigation navigation = new(new BrowserState(
            BrowserView.Materials,
            @"C:\game\DATAS\VENICE.DPC",
            "",
            TextureFilter.All,
            null,
            0,
            0,
            ChannelView.Rgb,
            true,
            1f,
            PointF.Empty,
            null,
            0,
            null,
            null));

        navigation.SetMaterialListSelection(material, 8);
        navigation.OpenMaterialFromList(material, -1, "short read at 42");

        Assert.Equal(BrowserView.Material, navigation.State.View);
        Assert.Equal(-1, navigation.State.MaterialSlot);
        Assert.Equal("short read at 42", navigation.State.MaterialError);

        navigation.Back();

        Assert.Equal(BrowserView.Materials, navigation.State.View);
        Assert.Equal(material, navigation.State.Material);
        Assert.Equal(8, navigation.State.ListIndex);
        Assert.Null(navigation.State.MaterialError);
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
