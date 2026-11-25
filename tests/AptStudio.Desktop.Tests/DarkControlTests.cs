using System.Reflection;

namespace AptStudio.Desktop.Tests;

public sealed class DarkControlTests
{
    [Fact]
    public void EnabledMenuTextUsesThemeText()
    {
        using Bitmap bitmap = new(160, 24);
        using Graphics graphics = Graphics.FromImage(bitmap);
        ToolStripMenuItem item = new("Export Selected") { ForeColor = Theme.Text };
        ToolStripItemTextRenderEventArgs args = TextArgs(graphics, item);

        Invoke(new DarkToolStripRenderer(), "OnRenderItemText", args);

        Assert.Equal(Theme.Text, args.TextColor);
    }

    [Fact]
    public void DisabledMenuTextUsesMuted()
    {
        using Bitmap bitmap = new(160, 24);
        using Graphics graphics = Graphics.FromImage(bitmap);
        ToolStripMenuItem item = new("Export Selected") { Enabled = false, ForeColor = Theme.Text };
        ToolStripItemTextRenderEventArgs args = TextArgs(graphics, item);

        Invoke(new DarkToolStripRenderer(), "OnRenderItemText", args);

        Assert.Equal(Theme.Muted, args.TextColor);
    }

    [Fact]
    public void ComboDrawsTextOnSurface()
    {
        using Bitmap bitmap = new(160, 24);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Magenta);
        DarkComboBox combo = new();
        combo.Items.Add("Mip 0 - 512x512");

        Invoke(combo, "OnDrawItem", new DrawItemEventArgs(
            graphics,
            combo.Font,
            new Rectangle(0, 0, 160, 24),
            0,
            DrawItemState.None));

        Assert.Equal(Theme.Surface, bitmap.GetPixel(0, 0));
        Assert.True(CountTextPixels(bitmap) > 20);
    }

    [Fact]
    public void ComboDrawsSelectionOnSelectionColor()
    {
        using Bitmap bitmap = new(160, 24);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Magenta);
        DarkComboBox combo = new();
        combo.Items.Add("Mip 0 - 512x512");

        Invoke(combo, "OnDrawItem", new DrawItemEventArgs(
            graphics,
            combo.Font,
            new Rectangle(0, 0, 160, 24),
            0,
            DrawItemState.Selected));

        Assert.Equal(Theme.Selection, bitmap.GetPixel(0, 0));
        Assert.True(CountTextPixels(bitmap) > 20);
    }

    [Fact]
    public void EmptyComboDrawsOnlyBackground()
    {
        using Bitmap bitmap = new(160, 24);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Magenta);
        DarkComboBox combo = new();

        Invoke(combo, "OnDrawItem", new DrawItemEventArgs(
            graphics,
            combo.Font,
            new Rectangle(0, 0, 160, 24),
            -1,
            DrawItemState.None));

        Assert.Equal(Theme.Surface, bitmap.GetPixel(0, 0));
        Assert.Equal(0, CountTextPixels(bitmap));
    }

    private static ToolStripItemTextRenderEventArgs TextArgs(Graphics graphics, ToolStripMenuItem item)
    {
        item.Size = new Size(160, 24);
        return new ToolStripItemTextRenderEventArgs(
            graphics,
            item,
            item.Text,
            new Rectangle(2, 0, 156, 24),
            Color.Black,
            SystemFonts.MenuFont,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }

    private static int CountTextPixels(Bitmap bitmap)
    {
        int count = 0;
        for (int y = 1; y < bitmap.Height - 1; y++)
        {
            for (int x = 1; x < bitmap.Width - 1; x++)
            {
                Color pixel = bitmap.GetPixel(x, y);
                if (pixel != Theme.Surface && pixel != Theme.Selection)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static void Invoke(object target, string method, object argument)
    {
        MethodInfo info = target.GetType().GetMethod(
            method,
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{method} not found on {target.GetType().Name}.");
        info.Invoke(target, [argument]);
    }
}
