using System.Globalization;
using System.Runtime.InteropServices;
using AptStudio.Core.Assets;
using AptStudio.Core.Games;

namespace AptStudio.Desktop;

internal static class AnimationSkinsScreenshotMode
{
    public static void Run(string[] args)
    {
        string output = Path.GetFullPath(args.Length > 1 ? args[1] : "animskins.png");
        string? game = null;
        ulong? hash = null;
        float scale = 1f;
        for (int index = 2; index < args.Length; index++)
        {
            if (args[index] == "--game" && index + 1 < args.Length)
            {
                game = args[++index];
            }
            else if (args[index] == "--anim"
                && index + 1 < args.Length
                && ulong.TryParse(
                    args[index + 1].StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        ? args[index + 1][2..]
                        : args[index + 1],
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
        }

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        game ??= Settings.LoadLastGameRoot();
        if (!GameProfile.TryDetect(game, out GameProfile? profile, out string? error)
            || profile is null)
        {
            File.WriteAllText(output + ".log", (error ?? "No game folder.") + Environment.NewLine);
            return;
        }

        if (hash is null)
        {
            File.WriteAllText(output + ".log", "Pass --anim <hash>." + Environment.NewLine);
            return;
        }

        AssetCatalog assets = AssetCatalog.Load(profile);
        AssetEntry? animation = assets.Entries
            .Where(row => row.ClassHash == AssetClass.Animation && row.NameHash == hash)
            .GroupBy(row => (row.BodyArchivePath, row.Row.BodyOffset))
            .Select(group => group
                .OrderBy(row => row.ArchivePath, StringComparer.OrdinalIgnoreCase)
                .First())
            .FirstOrDefault();
        if (animation is null)
        {
            File.WriteAllText(output + ".log", $"animation 0x{hash:X16} was not found." + Environment.NewLine);
            return;
        }

        using AnimationSkinsForm form = new(profile, assets.Entries, animation);
        form.ScaleForShot(scale);
        form.Shown += async (_, _) =>
        {
            try
            {
                await form.LoadForShotAsync();
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
