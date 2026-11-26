using System.Globalization;
using System.Runtime.InteropServices;
using AptStudio.Core.Assets;
using AptStudio.Core.Games;

namespace AptStudio.Desktop;

internal static class SkeletonScreenshotMode
{
    public static void Run(string[] args)
    {
        string output = Path.GetFullPath(args.Length > 1 ? args[1] : "skeleton.png");
        string? game = null;
        ulong? hash = null;
        float scale = 1f;
        bool usedBy = false;
        string? filter = null;
        for (int index = 2; index < args.Length; index++)
        {
            if (args[index] == "--game" && index + 1 < args.Length)
            {
                game = args[++index];
            }
            else if (args[index] == "--hash"
                && index + 1 < args.Length
                && ulong.TryParse(
                    args[index + 1],
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out ulong parsed))
            {
                hash = parsed;
                index++;
            }
            else if (args[index] == "--scale"
                && index + 1 < args.Length
                && float.TryParse(
                    args[index + 1],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float parsedScale))
            {
                scale = parsedScale;
                index++;
            }
            else if (args[index] == "--usedby")
            {
                usedBy = true;
            }
            else if (args[index] == "--filter" && index + 1 < args.Length)
            {
                filter = args[++index];
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        game ??= Settings.LoadLastGameRoot();
        if (!GameProfile.TryDetect(game, out GameProfile? profile, out string? error)
            || profile is null)
        {
            File.WriteAllText(output + ".log", (error ?? "No game folder.") + Environment.NewLine);
            return;
        }

        AssetCatalog assets = AssetCatalog.Load(profile);
        using SkeletonForm form = new(profile, assets.Entries, hash);
        form.EnableScreenshotMode();
        form.ScaleForShot(scale);
        form.Shown += async (_, _) =>
        {
            try
            {
                await form.LoadForShotAsync(hash);
                if (filter is not null)
                {
                    form.FilterForShot(filter);
                }

                if (hash is ulong selected)
                {
                    form.SelectSkeleton(selected);
                }

                if (usedBy)
                {
                    form.ShowSkinsForShot();
                }

                await Task.Delay(300);
                form.CaptureShot(output);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or ArgumentException or InvalidOperationException or ExternalException)
            {
                File.WriteAllText(output + ".log", ex.ToString());
            }
            finally
            {
                form.Close();
            }
        };
        Application.Run(form);
    }
}
