namespace AptStudio.Desktop.Tests;

public sealed class AnimationViewTests
{
    [Fact]
    public void SkinsButtonSitsInsideTheHeader()
    {
        using AnimationView view = new();
        view.Size = new Size(600, 400);
        view.CreateControl();
        view.SetAnimation(
            1,
            "Animation_Z",
            "subtitle",
            [new AnimationBoneRow(1, 0, 142)],
            packed: true);
        view.PerformLayout();

        Button skins = Find<Button>(view, "Skins")
            ?? throw new InvalidOperationException("Skins button was not found.");
        Assert.True(skins.Visible);
        Assert.True(
            skins.Right <= view.ClientSize.Width,
            $"button right {skins.Right} exceeds view width {view.ClientSize.Width}.");
        Assert.True(
            skins.Bottom <= 60,
            $"button {skins.Bounds} parent {skins.Parent?.Bounds} grand {skins.Parent?.Parent?.Bounds} view {view.ClientSize}.");
    }

    [Fact]
    public void EmptyAnimationDisablesTheSkinsButton()
    {
        using AnimationView view = new();
        view.SetAnimation(1, "Animation_Z", "error", [], packed: true);

        Button skins = Find<Button>(view, "Skins")
            ?? throw new InvalidOperationException("Skins button was not found.");
        Assert.False(skins.Enabled);
    }

    private static T? Find<T>(Control parent, string text)
        where T : Control
    {
        foreach (Control child in parent.Controls)
        {
            if (child is T match && match.Text == text)
            {
                return match;
            }

            T? nested = Find<T>(child, text);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
