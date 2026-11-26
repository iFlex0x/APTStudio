using System.Globalization;
using System.Runtime.InteropServices;

namespace AptStudio.Desktop;

// dev harness: renders states off-screen so UI checks never steal focus.
internal static class ScreenshotMode
{
    public static void Run(string[] args)
    {
        string output = Path.GetFullPath(args.Length > 1 ? args[1] : "shot.png");
        string? game = null;
        string? filter = null;
        string? exportFolder = null;
        int count = 0;
        int channel = -1;
        int mip = -1;
        int sort = -1;
        int slot = -1;
        float scale = 1f;
        bool previewable = false;
        bool actual = false;
        bool wire = false;
        bool materials = false;
        bool openTexture = false;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--game" && i + 1 < args.Length)
            {
                game = args[++i];
            }
            else if (args[i] == "--filter" && i + 1 < args.Length)
            {
                filter = args[++i];
            }
            else if (args[i] == "--export" && i + 1 < args.Length)
            {
                exportFolder = args[++i];
            }
            else if (args[i] == "--count"
                && i + 1 < args.Length
                && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int countValue))
            {
                count = countValue;
                i++;
            }
            else if (args[i] == "--previewable")
            {
                previewable = true;
            }
            else if (args[i] == "--actual")
            {
                actual = true;
            }
            else if (args[i] == "--wire")
            {
                wire = true;
            }
            else if (args[i] == "--materials")
            {
                materials = true;
            }
            else if (args[i] == "--opentexture")
            {
                openTexture = true;
            }
            else if (args[i] == "--slot"
                && i + 1 < args.Length
                && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int slotValue))
            {
                slot = slotValue;
                i++;
            }
            else if (args[i] == "--sort"
                && i + 1 < args.Length
                && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int sortValue))
            {
                sort = sortValue;
                i++;
            }
            else if (args[i] == "--channel"
                && i + 1 < args.Length
                && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int channelValue))
            {
                channel = channelValue;
                i++;
            }
            else if (args[i] == "--mip"
                && i + 1 < args.Length
                && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int mipValue))
            {
                mip = mipValue;
                i++;
            }
            else if (args[i] == "--scale"
                && i + 1 < args.Length
                && float.TryParse(
                    args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                scale = parsed;
                i++;
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        game ??= Settings.LoadLastGameRoot();
        using MainForm form = new();
        form.EnableScreenshotMode();
        form.ScaleForShot(scale);
        form.Shown += async (_, _) =>
        {
            try
            {
                if (game is not null)
                {
                    await form.LoadForScreenshotAsync(game);
                }

                await form.WaitForPreviewAsync();
                if (filter is not null)
                {
                    form.FilterForShot(filter);
                    await form.WaitForPreviewAsync();
                }

                if (previewable)
                {
                    form.PreviewableForShot();
                    await form.WaitForPreviewAsync();
                }

                if (mip >= 0)
                {
                    form.MipForShot(mip);
                    await form.WaitForPreviewAsync();
                }

                if (channel >= 0)
                {
                    form.ChannelForShot(channel);
                }

                if (actual)
                {
                    form.ActualForShot();
                }

                if (wire)
                {
                    form.WireframeForShot();
                }

                if (materials)
                {
                    await form.MaterialsForShotAsync();
                    await form.WaitForPreviewAsync();
                }

                if (sort >= 0)
                {
                    form.SortForShot(sort);
                }

                if (slot >= 0)
                {
                    form.SlotForShot(slot);
                    await form.WaitForPreviewAsync();
                }

                if (openTexture)
                {
                    form.OpenTextureForShot();
                    await form.WaitForPreviewAsync();
                }

                if (exportFolder is not null)
                {
                    if (count <= 0)
                    {
                        File.WriteAllText(
                            output + ".export.txt",
                            "export skipped: --count must be positive" + Environment.NewLine);
                    }
                    else
                    {
                        Directory.CreateDirectory(exportFolder);
                        await form.ExportForShotAsync(exportFolder, count);
                        File.WriteAllText(
                            output + ".export.txt", form.StatusText + Environment.NewLine);
                    }
                }

                await Task.Delay(300);
                form.ActiveControl = null;
                form.CaptureShot(output);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or ArgumentException or ExternalException)
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
