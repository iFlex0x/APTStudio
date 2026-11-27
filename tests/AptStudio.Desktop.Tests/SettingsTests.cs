using System.Text.Json;

namespace AptStudio.Desktop.Tests;

public sealed class SettingsTests
{
    [Fact]
    public void SuccessfulSaveReplacesTheFileAndLeavesNoTemp()
    {
        WithFolder(folder =>
        {
            string path = Path.Combine(folder, "settings.json");
            Settings.SaveValues(
                path, new Dictionary<string, string> { ["lastGameRoot"] = @"G:\one" });
            Settings.SaveValues(
                path, new Dictionary<string, string> { ["lastGameRoot"] = @"G:\two" });

            Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
            Dictionary<string, string>? values =
                JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
            Assert.NotNull(values);
            Assert.Equal(@"G:\two", values["lastGameRoot"]);
        });
    }

    [Fact]
    public void FailedSaveDeletesTheTempAndKeepsTheTarget()
    {
        WithFolder(folder =>
        {
            string path = Path.Combine(folder, "settings.json");
            Directory.CreateDirectory(path);
            Settings.SaveValues(
                path, new Dictionary<string, string> { ["lastGameRoot"] = @"G:\one" });

            Assert.True(Directory.Exists(path));
            Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
        });
    }

    private static void WithFolder(Action<string> action)
    {
        string folder = Path.Combine(
            Path.GetTempPath(), "aptstudio-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            action(folder);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
