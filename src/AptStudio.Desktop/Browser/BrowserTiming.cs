namespace AptStudio.Desktop.Browser;

internal static class BrowserTiming
{
    private static readonly object Gate = new();
    private static readonly bool Enabled =
        Environment.GetEnvironmentVariable("APTSTUDIO_TIMING") == "1";
    private static readonly string FilePath =
        Environment.GetEnvironmentVariable("APTSTUDIO_TIMING_FILE")
        ?? Path.Combine(Path.GetTempPath(), "aptstudio-browser-timing.log");

    public static void Write(string message)
    {
        if (!Enabled)
        {
            return;
        }

        lock (Gate)
        {
            try
            {
                File.AppendAllText(
                    FilePath,
                    $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
