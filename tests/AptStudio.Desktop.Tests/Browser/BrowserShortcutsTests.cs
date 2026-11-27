using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class BrowserShortcutsTests
{
    [Fact]
    public void MapsOnlyTheShellShortcuts()
    {
        Assert.Equal(BrowserCommand.OpenGame, BrowserShortcuts.Map(Keys.O, true));
        Assert.Equal(BrowserCommand.Export, BrowserShortcuts.Map(Keys.E, true));
        Assert.Equal(BrowserCommand.Rescan, BrowserShortcuts.Map(Keys.F5, false));
        Assert.Equal(BrowserCommand.CancelExport, BrowserShortcuts.Map(Keys.Escape, false));
        Assert.Equal(BrowserCommand.None, BrowserShortcuts.Map(Keys.O, false));
        Assert.Equal(BrowserCommand.None, BrowserShortcuts.Map(Keys.E, false));
        Assert.Equal(BrowserCommand.None, BrowserShortcuts.Map(Keys.F5, true));
        Assert.Equal(BrowserCommand.None, BrowserShortcuts.Map(Keys.Escape, true));
        Assert.Equal(BrowserCommand.None, BrowserShortcuts.Map(Keys.C, false));
    }
}
