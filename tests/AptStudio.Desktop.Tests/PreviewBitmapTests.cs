using AptStudio.Core.Textures;

namespace AptStudio.Desktop.Tests;

public sealed class PreviewBitmapTests
{
    [Fact]
    public void CreateWritesBgraForGdiPlus()
    {
        byte[] rgba =
        [
            10, 20, 30, 255,
            200, 150, 100, 128,
        ];
        DecodedTexture image = new(0, 2, 1, rgba);
        using Bitmap bitmap = PreviewBitmap.Create(image);
        Assert.Equal(Color.FromArgb(255, 10, 20, 30), bitmap.GetPixel(0, 0));
        Assert.Equal(Color.FromArgb(128, 200, 150, 100), bitmap.GetPixel(1, 0));
    }

    [Fact]
    public void CreateRejectsShortPixels()
    {
        DecodedTexture image = new(0, 2, 2, new byte[4]);
        Assert.Throws<ArgumentException>(() => PreviewBitmap.Create(image));
    }
}
