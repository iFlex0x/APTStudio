using System.Globalization;
using System.Runtime.InteropServices;
using AptStudio.Core.Assets;
using AptStudio.Core.Games;
using AptStudio.Core.Skins;
using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop;

internal static class PoseScreenshotMode
{
    private const string DefaultOutput = "pose.png";

    public static void Run(string[] args)
    {
        int firstOption = 1;
        string outputArgument = DefaultOutput;
        if (args.Length > 1
            && !string.IsNullOrWhiteSpace(args[1])
            && !args[1].StartsWith("--", StringComparison.Ordinal))
        {
            outputArgument = args[1];
            firstOption = 2;
        }

        string output;
        try
        {
            output = Path.GetFullPath(outputArgument);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException
            or PathTooLongException)
        {
            Console.Error.WriteLine($"Pose shot: {outputArgument} is not a valid output path.");
            return;
        }

        string? game = null;
        ulong? skin = null;
        ulong? bone = null;
        ulong? animation = null;
        int frame = 0;
        float x = 0f;
        float y = 0f;
        float z = 0f;
        float scale = 1f;
        bool load = false;
        for (int index = firstOption; index < args.Length; index++)
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
            else if (args[index] == "--anim" && index + 1 < args.Length
                && TryHash(args[index + 1], out ulong parsedAnimation))
            {
                animation = parsedAnimation;
                index++;
            }
            else if (args[index] == "--frame" && index + 1 < args.Length
                && int.TryParse(
                    args[index + 1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int parsedFrame))
            {
                frame = parsedFrame;
                index++;
            }
            else if (args[index] == "--load")
            {
                load = true;
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

        string? argumentError = null;
        if (!float.IsFinite(x) || !float.IsFinite(y) || !float.IsFinite(z))
        {
            argumentError = "Angles must be finite.";
        }
        else if (!float.IsFinite(scale) || scale <= 0f)
        {
            argumentError = "Scale must be a positive number.";
        }

        game ??= Settings.LoadLastGameRoot();
        if (!GameProfile.TryDetect(game, out GameProfile? profile, out string? error)
            || profile is null)
        {
            Report(output, argumentError ?? error ?? "No game folder.");
            return;
        }

        // the log lives beside the output, so nothing may be written before this check
        if (GameFolderGuard.IsInside(profile.Root, Path.GetDirectoryName(output)!))
        {
            Console.Error.WriteLine(
                $"Pose shot: {output} is inside the game folder. Nothing was written.");
            return;
        }

        if (argumentError is not null)
        {
            Report(output, argumentError);
            return;
        }

        if (skin is null)
        {
            Report(output, "Pass --skin <hash>.");
            return;
        }

        AssetCatalog assets;
        AssetEntry? entry;
        ulong root;
        try
        {
            assets = AssetCatalog.Load(profile);
            entry = assets.Entries
                .Where(row => row.ClassHash == AssetClass.Skin && row.NameHash == skin)
                .GroupBy(row => (row.BodyArchivePath, row.Row.BodyOffset))
                .Select(group => group
                    .OrderBy(row => row.ArchivePath, StringComparer.OrdinalIgnoreCase)
                    .First())
                .FirstOrDefault();
            if (entry is null)
            {
                Report(output, $"skin 0x{skin:X16} was not found.");
                return;
            }

            using FileStream stream = new(
                entry.BodyArchivePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            root = SkinBinding.Read(stream, entry.BodyArchivePath, entry.Row).SkeletonRoot;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException
            or UnauthorizedAccessException)
        {
            Report(output, ex.Message);
            return;
        }

        if (root == 0)
        {
            Report(output, "Skin has no skeleton.");
            return;
        }

        using Form form = new()
        {
            Text = $"Pose 0x{entry.NameHash:X16}",
            Icon = Theme.AppIcon,
            BackColor = Theme.Background,
            ForeColor = Theme.Text,
            StartPosition = FormStartPosition.CenterParent,
            MinimumSize = new Size(900, 560),
            Size = new Size(1120, 720),
            ShowInTaskbar = false,
            AutoScaleDimensions = new SizeF(7F, 15F),
            AutoScaleMode = AutoScaleMode.Font,
        };
        PoseView view = new(
            profile,
            assets.Entries,
            entry,
            root,
            showAnimation: true,
            load ? animation ?? 0 : 0);
        view.Dock = DockStyle.Fill;
        form.Controls.Add(view);
        if (Math.Abs(scale - 1f) > 0.001f)
        {
            form.Scale(new SizeF(scale, scale));
            view.SetDpiScale(scale);
        }

        form.Shown += async (_, _) =>
        {
            try
            {
                await view.LoadAsync();
                if (bone is ulong selected)
                {
                    if (!view.PoseForShot(selected, x, y, z))
                    {
                        Report(output, $"bone 0x{selected:X16} is not in this skin.");
                        return;
                    }
                }

                if (animation is ulong clip && !load)
                {
                    if (!view.AnimationForShot(clip, frame))
                    {
                        Report(output, $"animation 0x{clip:X16} could not drive this skin.");
                        return;
                    }
                }

                await Task.Delay(load ? 600 : 300);
                SaveScreenshot(form, output);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or ArgumentException or InvalidOperationException or InvalidDataException
                or ExternalException)
            {
                Report(output, ex.ToString());
            }
            finally
            {
                if (!form.IsDisposed)
                {
                    form.Close();
                }
            }
        };
        Application.Run(form);
    }

    private static void SaveScreenshot(Form form, string output)
    {
        using Bitmap bitmap = new(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
        string? directory = Path.GetDirectoryName(output);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        string temp = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            bitmap.Save(temp, System.Drawing.Imaging.ImageFormat.Png);
            File.Move(temp, output, overwrite: true);
        }
        finally
        {
            DeleteIfPresent(temp);
        }

        DeleteIfPresent(output + ".log");
    }

    private static void Report(string output, string message)
    {
        if (!TryWriteLog(output, message))
        {
            Console.Error.WriteLine($"Pose shot: {message}");
        }
    }

    private static bool TryWriteLog(string output, string message)
    {
        string? directory = Path.GetDirectoryName(output);
        try
        {
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(output + ".log", message + Environment.NewLine);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void DeleteIfPresent(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static bool TryHash(string text, out ulong hash) =>
        ulong.TryParse(
            text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text,
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out hash);
}
