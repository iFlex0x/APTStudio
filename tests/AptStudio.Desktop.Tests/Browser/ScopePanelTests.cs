using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class ScopePanelTests
{
    [Fact]
    public void SetTypesDisposesThePreviousRows()
    {
        using ScopePanel panel = new();
        panel.SetTypes([new TypeChoice("Textures", 10)], "Textures");
        ScopeRow[] first = Rows(panel).ToArray();
        Assert.NotEmpty(first);

        panel.SetTypes([new TypeChoice("Meshes", 2)], "Meshes");

        Assert.All(first, row => Assert.True(row.IsDisposed));
        ScopeRow[] remaining = Rows(panel).ToArray();
        Assert.Single(remaining);
        Assert.Equal("Meshes", remaining[0].Text);
    }

    private static IEnumerable<ScopeRow> Rows(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is ScopeRow row)
            {
                yield return row;
            }

            foreach (ScopeRow nested in Rows(child))
            {
                yield return nested;
            }
        }
    }
}
