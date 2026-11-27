using AptStudio.Core.Games;
using AptStudio.Desktop.Browser;

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

        if (args.Length > 0
            && string.Equals(args[0], "--poseshot", StringComparison.OrdinalIgnoreCase))
        {
            PoseScreenshotMode.Run(args);
            return;
        }

        if (args.Length > 0
            && (string.Equals(args[0], "--shot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "--skeletonshot", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[0], "--animskinshot", StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(
                "The legacy screenshot harness was retired with the old browser. " +
                "Open APTStudio and capture the window instead.",
                "APTStudio",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        int index = args.Length > 0
            && string.Equals(args[0], "--browser", StringComparison.OrdinalIgnoreCase)
            ? 1
            : 0;
        GameProfile? profile = null;
        if (args.Length > index + 1
            && string.Equals(args[index], "--game", StringComparison.OrdinalIgnoreCase)
            && GameProfile.TryDetect(args[index + 1], out GameProfile? detected, out _))
        {
            profile = detected;
        }

        Application.Run(new BrowserForm(profile));
    }
}
