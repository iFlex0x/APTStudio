using AptStudio.Desktop;

namespace AptStudio.Desktop.Tests;

public sealed class LinkViewTests
{
    [Fact]
    public void ShowsRowsAndActivatesFirst()
    {
        using LinkView view = new();
        view.CreateControl();
        LinkRow row = new(LinkKind.Material, 0x1234, "COMMON.DPC", "slots 0");
        LinkRow? activated = null;
        view.LinkActivated += value => activated = value;

        view.SetLinks("Used by 1 materials", "texture", [row]);
        view.ActivateFirst();

        Assert.Equal(1, view.RowCount);
        Assert.Same(row, activated);
    }

    [Fact]
    public void BackRaisesRequest()
    {
        using LinkView view = new();
        view.CreateControl();
        int backs = 0;
        view.BackRequested += () => backs++;

        FindButton(view, "Back").PerformClick();

        Assert.Equal(1, backs);
    }

    [Fact]
    public void ResetClearsRowsAndScalingIsSafe()
    {
        using LinkView view = new();
        view.CreateControl();
        view.SetLinks("title", "subtitle", [new LinkRow(LinkKind.Skin, 1, "a", "b")]);
        view.Reset();
        view.SetDpiScale(1.5f);
        view.SetLinks("title", "subtitle", [new LinkRow(LinkKind.Skin, 1, "a", "b")]);
        view.SetDpiScale(2f);

        Assert.Equal(1, view.RowCount);
    }

    private static Button FindButton(Control root, string text)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Button button && button.Text == text)
            {
                return button;
            }

            if (FindButtonOrNull(child, text) is Button found)
            {
                return found;
            }
        }

        throw new InvalidOperationException($"No button '{text}'.");
    }

    private static Button? FindButtonOrNull(Control root, string text)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Button button && button.Text == text)
            {
                return button;
            }

            if (FindButtonOrNull(child, text) is Button found)
            {
                return found;
            }
        }

        return null;
    }
}
