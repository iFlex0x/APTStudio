namespace AptStudio.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            Application.SetColorMode(SystemColorMode.Dark);
        }
        catch (PlatformNotSupportedException)
        {
        }

        if (args.Length > 0 && string.Equals(args[0], "--shot", StringComparison.OrdinalIgnoreCase))
        {
            ScreenshotMode.Run(args);
            return;
        }

        Application.Run(new MainForm());
    }
}
