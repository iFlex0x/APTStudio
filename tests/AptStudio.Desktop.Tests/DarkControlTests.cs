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

    private static void Invoke(object target, string method, object argument)
    {
        MethodInfo info = target.GetType().GetMethod(
            method,
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{method} not found on {target.GetType().Name}.");
        info.Invoke(target, [argument]);
    }
}
