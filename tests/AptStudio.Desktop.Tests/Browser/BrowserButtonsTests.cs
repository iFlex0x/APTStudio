using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class BrowserButtonsTests
{
    [Fact]
    public void RolesMatchFontHeightPaddingAndColors()
    {
        using Button primary = new();
        using Button ordinary = new();
        using Button quiet = new();

        BrowserButtons.Style(primary, BrowserButtonRole.Primary, Theme.Surface);
        BrowserButtons.Style(ordinary, BrowserButtonRole.Ordinary, Theme.Surface);
        BrowserButtons.Style(quiet, BrowserButtonRole.Quiet, Theme.Surface);

        foreach (Button button in new[] { primary, ordinary, quiet })
        {
            Assert.Equal(26, button.Height);
            Assert.Equal(BrowserVisual.Ui, button.Font);
            Assert.Equal(new Padding(8, 0, 8, 0), button.Padding);
        }

        Assert.Equal(BrowserVisual.PrimaryText, primary.ForeColor);
        Assert.Equal(0, primary.FlatAppearance.BorderSize);
        Assert.Equal(Theme.Text, ordinary.ForeColor);
        Assert.Equal(1, ordinary.FlatAppearance.BorderSize);
        Assert.Equal(Theme.Muted, quiet.ForeColor);
        Assert.Equal(0, quiet.FlatAppearance.BorderSize);
    }

    [Fact]
    public void DisabledPrimaryUsesTheSurfaceInsteadOfTheAccent()
    {
        using Button button = new() { Enabled = false };

        BrowserButtons.Style(button, BrowserButtonRole.Primary, Theme.Surface);

        Assert.Equal(Theme.Surface, button.BackColor);
        Assert.Equal(BrowserVisual.Faint, button.ForeColor);
        Assert.Equal(0, button.FlatAppearance.BorderSize);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisabledCaptionRendersLegiblePixels(bool onBackground)
    {
        Color background = onBackground ? Theme.Background : Theme.Surface;
        using Button button = new() { Text = "Export selected" };

        BrowserButtons.Style(button, BrowserButtonRole.Ordinary, background);
        button.Enabled = false;
        button.Size = button.GetPreferredSize(Size.Empty);
        button.CreateControl();
        using Bitmap bitmap = new(button.Width, button.Height);
        button.DrawToBitmap(bitmap, new Rectangle(Point.Empty, button.Size));

        Assert.False(button.Enabled);
        Assert.Equal("Export selected", button.Text);
        Assert.Equal(background, button.BackColor);
        Assert.True(CountLegiblePixels(bitmap) > 20, CaptionSummary(bitmap));
    }

    private static int CountLegiblePixels(Bitmap bitmap)
    {
        int count = 0;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                Color pixel = bitmap.GetPixel(x, y);
                if (pixel.R >= 90 && pixel.G >= 90 && pixel.B >= 90)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static string CaptionSummary(Bitmap bitmap)
    {
        Color brightest = Color.Black;
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                Color pixel = bitmap.GetPixel(x, y);
                if (pixel.GetBrightness() > brightest.GetBrightness())
                {
                    brightest = pixel;
                }
            }
        }

        return $"brightest caption pixel {brightest}";
    }
}
