namespace AptStudio.Desktop.Browser;

internal enum BrowserCommand
{
    None,
    OpenGame,
    Export,
    Rescan,
    CancelExport,
}

internal enum BrowserSideButton
{
    None,
    Back,
    Forward,
}

internal static class BrowserShortcuts
{
    public static BrowserCommand Map(Keys key, bool control) => control
        ? key switch
        {
            Keys.O => BrowserCommand.OpenGame,
            Keys.E => BrowserCommand.Export,
            _ => BrowserCommand.None,
        }
        : key switch
        {
            Keys.F5 => BrowserCommand.Rescan,
            Keys.Escape => BrowserCommand.CancelExport,
            _ => BrowserCommand.None,
        };

    public static BrowserSideButton MapSideButton(int button) => button switch
    {
        1 => BrowserSideButton.Back,
        2 => BrowserSideButton.Forward,
        _ => BrowserSideButton.None,
    };
}
