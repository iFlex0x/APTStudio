using System.Reflection;
using AptStudio.Core.Containers;
using AptStudio.Core.Textures;
using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class TextureInspectorTests
{
    [Fact]
    public void EmptyStateClearsPreviousDetailsMipAndImageControls()
    {
        using Form form = Show(out TextureInspector inspector);
        using TemporaryTexture file = new(8);
        TextureEntry texture = Entry(file.Path, 0, Head(4, 4, 1, 0x1B));
        TextureOccurrence occurrence = Occurrence(texture);

        inspector.SetTexture(
            occurrence, texture, [occurrence], 0, 0, ChannelView.Rgb, true, 1f, PointF.Empty, false);
        WaitForImage(inspector);

        inspector.ShowEmpty();

        Assert.False(Field<PreviewControl>(inspector, "_preview").HasImage);
        Assert.Empty(Field<ComboBox>(inspector, "_mip").Items.Cast<object>());
        Assert.Equal(-1, Field<ComboBox>(inspector, "_mip").SelectedIndex);
        Assert.Empty(Field<ComboBox>(inspector, "_copies").Items.Cast<object>());
        Assert.Empty(Field<Panel>(inspector, "_detailsPanel").Controls.Cast<Control>());
        Assert.False(Field<Button>(inspector, "_details").Enabled);
        Assert.False(Field<Button>(inspector, "_fit").Enabled);
        Assert.False(Field<Button>(inspector, "_actual").Enabled);
        Assert.False(Field<ChannelSegments>(inspector, "_channelSegments").Enabled);
    }

    [Fact]
    public void SelectingAnotherTextureRemovesThePreviousImageBeforeDecoding()
    {
        using Form form = Show(out TextureInspector inspector);
        using TemporaryTexture firstFile = new(8);
        using TemporaryTexture secondFile = new(40);
        TextureEntry first = Entry(firstFile.Path, 0, Head(4, 4, 1, 0x1B));
        TextureEntry second = Entry(secondFile.Path, 1, Head(4, 4, 1, 0x1B));
        TextureOccurrence firstOccurrence = Occurrence(first);
        TextureOccurrence secondOccurrence = Occurrence(second);

        inspector.SetTexture(
            firstOccurrence, first, [firstOccurrence], 0, 0,
            ChannelView.Rgb, true, 1f, PointF.Empty, false);
        WaitForImage(inspector);
        int published = 0;
        inspector.ViewChanged += (_, _) => published++;

        inspector.SetTexture(
            secondOccurrence, second, [secondOccurrence], 0, 0,
            ChannelView.Rgb, true, 1f, PointF.Empty, false);

        Assert.False(Field<PreviewControl>(inspector, "_preview").HasImage);
        Assert.Equal(0, published);
    }

    [Fact]
    public void SameTextureReapplicationWhileDecodingKeepsTheLatestView()
    {
        using Form form = Show(out TextureInspector inspector);
        const int side = 4096;
        int stored = side * side;
        using TemporaryTexture file = new(stored);
        TextureEntry texture = Entry(
            file.Path, 0, Head(side, side, 1, 0x1D, (uint)stored));
        TextureOccurrence occurrence = Occurrence(texture);

        inspector.SetTexture(
            occurrence, texture, [occurrence], 0, 0,
            ChannelView.Rgb, true, 1f, PointF.Empty, false);
        inspector.SetTexture(
            occurrence, texture, [occurrence], 0, 0,
            ChannelView.Rgb, false, 1.25f, new PointF(20, 30), false);

        PreviewControl preview = Field<PreviewControl>(inspector, "_preview");
        Assert.False(preview.HasImage);
        WaitForImage(inspector);
        PointF expected = new(20, 30);
        for (int attempt = 0; attempt < 200 && preview.Origin != expected; attempt++)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }

        Assert.False(preview.IsFit);
        Assert.Equal(1.25f, preview.Zoom, 3);
        Assert.Equal(expected, preview.Origin);
    }

    [Fact]
    public void UnsupportedTextureKeepsTechnicalDetailsAndDisablesImageOnlyControls()
    {
        using Form form = Show(out TextureInspector inspector);
        using TemporaryTexture file = new(8);
        BitmapHead head = Head(4, 4, 1, 0x20);
        TextureEntry texture = Entry(file.Path, 0, head);
        TextureOccurrence occurrence = Occurrence(texture);

        Assert.False(TextureDecoder.CanDecodeMip0(head));
        inspector.SetTexture(
            occurrence, texture, [occurrence], 0, 0, ChannelView.Rgb, true, 1f, PointF.Empty, false);

        Button details = Field<Button>(inspector, "_details");
        Assert.True(details.Enabled);
        Assert.False(Field<Button>(inspector, "_fit").Enabled);
        Assert.False(Field<Button>(inspector, "_actual").Enabled);
        Assert.False(Field<ChannelSegments>(inspector, "_channelSegments").Enabled);
        Assert.False(Field<ComboBox>(inspector, "_mip").Enabled);
        Assert.False(Field<Button>(inspector, "_export").Enabled);

        details.PerformClick();
        Assert.True(Field<Panel>(inspector, "_detailsPanel").Visible);
        Assert.NotEmpty(Field<Panel>(inspector, "_detailsPanel").Controls.Cast<Control>());
    }

    private static Form Show(out TextureInspector inspector)
    {
        inspector = new TextureInspector { Dock = DockStyle.Fill };
        Form form = new() { Width = 900, Height = 700, ShowInTaskbar = false };
        form.Controls.Add(inspector);
        form.Show();
        Application.DoEvents();
        inspector.ShowEmpty();
        return form;
    }

    private static void WaitForImage(TextureInspector inspector)
    {
        PreviewControl preview = Field<PreviewControl>(inspector, "_preview");
        for (int attempt = 0; attempt < 400 && !preview.HasImage; attempt++)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }

        Assert.True(preview.HasImage);
    }

    private static T Field<T>(object target, string name)
    {
        FieldInfo info = target.GetType().GetField(
            name, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{name} not found.");
        return (T)info.GetValue(target)!;
    }

    private static TextureEntry Entry(string path, uint sectors, BitmapHead head) =>
        new(path, new DpcEntry(0, 0, 0, sectors, 0, 0, 0, 0, 0), head);

    private static TextureOccurrence Occurrence(TextureEntry entry) =>
        new(0, entry.ArchivePath, entry.ArchivePath, entry.Row.BodyOffset, entry.Head);

    private static BitmapHead Head(int width, int height, int mips, ushort key, uint stored = 8)
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
            stored,
            12,
            0,
            BitmapHead.LinkHeaderSize,
            key,
            format,
            0);
    }

    private sealed class TemporaryTexture : IDisposable
    {
        private readonly string _directory;

        public TemporaryTexture(int length)
        {
            _directory = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "aptstudio-inspector",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            Path = System.IO.Path.Combine(_directory, "body.dpc");
            File.WriteAllBytes(Path, new byte[length]);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, true);
            }
            catch (IOException)
            {
            }
        }
    }
}
