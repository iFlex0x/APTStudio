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

        if (args.Length > 0 && string.Equals(args[0], "--shot", StringComparison.OrdinalIgnoreCase))
        {
            ScreenshotMode.Run(args);
            return;
        }

        if (args.Length > 0
            && string.Equals(args[0], "--skeletonshot", StringComparison.OrdinalIgnoreCase))
        {
            SkeletonScreenshotMode.Run(args);
            return;
        }

        if (args.Length > 0
            && string.Equals(args[0], "--poseshot", StringComparison.OrdinalIgnoreCase))
        {
            PoseScreenshotMode.Run(args);
            return;
        }

        if (args.Length > 0
            && string.Equals(args[0], "--animskinshot", StringComparison.OrdinalIgnoreCase))
        {
            AnimationSkinsScreenshotMode.Run(args);
            return;
        }

        if (args.Length > 0
            && string.Equals(args[0], "--browser", StringComparison.OrdinalIgnoreCase))
        {
            GameProfile? profile = null;
            if (args.Length > 2
                && string.Equals(args[1], "--game", StringComparison.OrdinalIgnoreCase)
                && GameProfile.TryDetect(args[2], out GameProfile? detected, out _))
            {
                profile = detected;
            }

            Application.Run(new BrowserForm(profile));
            return;
        }

        Application.Run(new MainForm());
    }
}
