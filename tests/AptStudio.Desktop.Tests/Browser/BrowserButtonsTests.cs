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
}
