using System.Text.Json;

namespace AptStudio.Desktop;

internal static class Settings
{
    private const string LastGameRootKey = "lastGameRoot";

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "APTStudio",
        "settings.json");

    public static string? LoadLastGameRoot()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            Dictionary<string, string>? values =
                JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FilePath));
            return values is not null
                && values.TryGetValue(LastGameRootKey, out string? root)
                && !string.IsNullOrWhiteSpace(root)
                    ? root
                    : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static void SaveLastGameRoot(string root)
    {
        try
        {
            string path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(
                new Dictionary<string, string> { [LastGameRootKey] = root }));
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
