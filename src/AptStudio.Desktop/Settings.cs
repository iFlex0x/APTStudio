// Copyright (c) 2026 iFlex0x
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;

namespace AptStudio.Desktop;

internal static class Settings
{
    private const string LastGameRootKey = "lastGameRoot";
    private const string SidebarWidthKey = "browserSidebarWidth";
    private const string InspectorWidthKey = "browserInspectorWidth";

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "APTStudio",
        "settings.json");

    public static string? LoadLastGameRoot()
    {
        Dictionary<string, string> values = LoadValues();
        return values.TryGetValue(LastGameRootKey, out string? root)
            && !string.IsNullOrWhiteSpace(root)
                ? root
                : null;
    }

    public static void SaveLastGameRoot(string root)
    {
        Dictionary<string, string> values = LoadValues();
        values[LastGameRootKey] = root;
        SaveValues(values);
    }

    public static (int Sidebar, int Inspector) LoadBrowserLayout()
    {
        Dictionary<string, string> values = LoadValues();
        return (ReadInt(values, SidebarWidthKey, 216), ReadInt(values, InspectorWidthKey, 420));
    }

    public static void SaveBrowserLayout(int sidebar, int inspector)
    {
        Dictionary<string, string> values = LoadValues();
        values[SidebarWidthKey] = sidebar.ToString(System.Globalization.CultureInfo.InvariantCulture);
        values[InspectorWidthKey] = inspector.ToString(System.Globalization.CultureInfo.InvariantCulture);
        SaveValues(values);
    }

    private static int ReadInt(Dictionary<string, string> values, string key, int fallback) =>
        values.TryGetValue(key, out string? text)
            && int.TryParse(
                text,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out int value)
                ? value
                : fallback;

    private static Dictionary<string, string> LoadValues()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new Dictionary<string, string>();
            }

            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FilePath))
                ?? new Dictionary<string, string>();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private static void SaveValues(Dictionary<string, string> values) =>
        SaveValues(FilePath, values);

    internal static void SaveValues(string path, Dictionary<string, string> values)
    {
        string? temp = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(values));
            File.Move(temp, path, overwrite: true);
            temp = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        finally
        {
            if (temp is not null)
            {
                try
                {
                    File.Delete(temp);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
