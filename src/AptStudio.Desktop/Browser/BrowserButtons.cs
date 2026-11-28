using System.Drawing;

namespace AptStudio.Desktop.Browser;

internal enum BrowserButtonRole
{
    Primary,
    Ordinary,
    Quiet,
}

internal static class BrowserButtons
{
    public const int Height = 26;

    public static void Style(
        Button button,
        BrowserButtonRole role,
        Color background)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.AutoSize = true;
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.Font = BrowserVisual.Ui;
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.Cursor = Cursors.Hand;
        button.Padding = new Padding(8, 0, 8, 0);
        button.Height = Height;
        button.MinimumSize = new Size(0, Height);
        button.MaximumSize = new Size(int.MaxValue, Height);

        void Apply()
        {
            bool enabled = button.Enabled;
            button.BackColor = enabled
                ? role == BrowserButtonRole.Primary ? Theme.Accent : background
                : background;
            button.ForeColor = enabled
                ? role == BrowserButtonRole.Primary
                    ? BrowserVisual.PrimaryText
                    : role == BrowserButtonRole.Quiet ? Theme.Muted : Theme.Text
                : BrowserVisual.Faint;
            button.FlatAppearance.BorderSize =
                enabled && role == BrowserButtonRole.Ordinary ? 1 : 0;
            button.FlatAppearance.BorderColor =
                enabled && role == BrowserButtonRole.Ordinary
                    ? BrowserVisual.BorderStrong
                    : background;
            button.FlatAppearance.MouseOverBackColor =
                role == BrowserButtonRole.Primary
                    ? Color.FromArgb(111, 208, 251)
                    : BrowserVisual.SurfaceHover;
            button.FlatAppearance.MouseDownBackColor =
                role == BrowserButtonRole.Primary
                    ? BrowserVisual.AccentDim
                    : Theme.SelectionInactive;
        }

        button.EnabledChanged += (_, _) => Apply();
        button.Paint -= PaintDisabledCaption;
        button.Paint += PaintDisabledCaption;
        Apply();
    }

    private static void PaintDisabledCaption(object? sender, PaintEventArgs e)
    {
        if (sender is not Button button
            || button.Enabled
            || SystemInformation.HighContrast)
        {
            return;
        }

        Rectangle bounds = button.ClientRectangle;
        using SolidBrush background = new(button.BackColor);
        e.Graphics.FillRectangle(background, bounds);
        TextRenderer.DrawText(
            e.Graphics,
            button.Text,
            button.Font,
            bounds,
            BrowserVisual.Faint,
            TextFormatFlags.HorizontalCenter
            | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPrefix
            | TextFormatFlags.EndEllipsis
            | TextFormatFlags.SingleLine);
    }
}
