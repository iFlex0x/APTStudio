using System.Globalization;
using System.Runtime.InteropServices;
using AptStudio.Core.Assets;
using AptStudio.Core.Games;
using AptStudio.Core.Skins;

namespace AptStudio.Desktop;

internal static class PoseScreenshotMode
{
    public static void Run(string[] args)
    {
        string output = Path.GetFullPath(args.Length > 1 ? args[1] : "pose.png");
        string? game = null;
        ulong? skin = null;
        ulong? bone = null;
        float x = 0f;
        float y = 0f;
        float z = 0f;
        float scale = 1f;
        for (int index = 2; index < args.Length; index++)
        {
            if (args[index] == "--game" && index + 1 < args.Length)
            {
                game = args[++index];
            }
            else if (args[index] == "--skin" && index + 1 < args.Length
                && TryHash(args[index + 1], out ulong parsedSkin))
            {
                skin = parsedSkin;
                index++;
            }
            else if (args[index] == "--bone" && index + 1 < args.Length
                && TryHash(args[index + 1], out ulong parsedBone))
            {
                bone = parsedBone;
                index++;
            }
            else if (args[index] == "--x" && index + 1 < args.Length
                && float.TryParse(
                    args[index + 1],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float parsedX))
            {
                x = parsedX;
                index++;
            }
            else if (args[index] == "--y" && index + 1 < args.Length
                && float.TryParse(
                    args[index + 1],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float parsedY))
            {
                y = parsedY;
                index++;
            }
            else if (args[index] == "--z" && index + 1 < args.Length
                && float.TryParse(
                    args[index + 1],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float parsedZ))
            {
                z = parsedZ;
                index++;
            }
            else if (args[index] == "--scale" && index + 1 < args.Length
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
        if (File.Exists(output))
        {
            File.Delete(output);
        }

        if (File.Exists(output + ".log"))
        {
            File.Delete(output + ".log");
        }

        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
        {
            File.WriteAllText(output + ".log", "Angles must be finite." + Environment.NewLine);
            return;
        }

        if (!float.IsFinite(scale) || scale <= 0f)
        {
            File.WriteAllText(output + ".log", "Scale must be a positive number." + Environment.NewLine);
            return;
        }

        game ??= Settings.LoadLastGameRoot();
        if (!GameProfile.TryDetect(game, out GameProfile? profile, out string? error)
            || profile is null)
        {
            File.WriteAllText(output + ".log", (error ?? "No game folder.") + Environment.NewLine);
            return;
        }

        if (skin is null)
        {
            File.WriteAllText(output + ".log", "Pass --skin <hash>." + Environment.NewLine);
            return;
        }

        AssetCatalog assets = AssetCatalog.Load(profile);
        AssetEntry? entry = assets.Entries
            .Where(row => row.ClassHash == AssetClass.Skin && row.NameHash == skin)
            .GroupBy(row => (row.BodyArchivePath, row.Row.BodyOffset))
            .Select(group => group
                .OrderBy(row => row.ArchivePath, StringComparer.OrdinalIgnoreCase)
                .First())
            .FirstOrDefault();
        if (entry is null)
        {
            File.WriteAllText(output + ".log", $"skin 0x{skin:X16} was not found." + Environment.NewLine);
            return;
        }

        ulong root;
        try
        {
            using FileStream stream = new(
                entry.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            root = SkinBinding.Read(stream, entry.BodyArchivePath, entry.Row).SkeletonRoot;
        }
        catch (InvalidDataException ex)
        {
            File.WriteAllText(output + ".log", ex.Message + Environment.NewLine);
            return;
        }

        if (root == 0)
        {
            File.WriteAllText(output + ".log", "Skin has no skeleton." + Environment.NewLine);
            return;
        }

        using PoseForm form = new(profile, assets.Entries, entry, root);
        form.ScaleForShot(scale);
        form.Shown += async (_, _) =>
        {
            try
            {
                await form.LoadForShotAsync();
                if (bone is ulong selected)
                {
                    if (!form.PoseForShot(selected, x, y, z))
                    {
                        File.WriteAllText(
                            output + ".log",
                            $"bone 0x{selected:X16} is not in this skin." + Environment.NewLine);
                        return;
                    }
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

    private static bool TryHash(string text, out ulong hash) =>
        ulong.TryParse(
            text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out hash);
}
