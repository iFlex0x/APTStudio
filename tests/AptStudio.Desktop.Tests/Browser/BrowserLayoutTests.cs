using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class BrowserLayoutTests
{
    [Theory]
    [InlineData(1099, true)]
    [InlineData(1100, false)]
    [InlineData(1440, false)]
    public void CollapsesOnlyBelowThreshold(int width, bool expected)
    {
        Assert.Equal(expected, BrowserLayout.ShouldCollapseSidebar(width));
    }

    [Fact]
    public void ClampsSidebarAndInspectorToMinimums()
    {
        Assert.Equal(180, BrowserLayout.ClampSidebar(100, 1440));
        Assert.Equal(320, BrowserLayout.ClampInspector(100, 1440));
        Assert.Equal(680, BrowserLayout.ClampSidebar(900, 1440));
        Assert.Equal(760, BrowserLayout.ClampInspector(900, 1440));
    }
}
