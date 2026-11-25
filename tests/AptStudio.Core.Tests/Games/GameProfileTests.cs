using AptStudio.Core.Games;

namespace AptStudio.Core.Tests.Games;

public sealed class GameProfileTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aptstudio-tests", Guid.NewGuid().ToString("N"));

    public GameProfileTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void DetectsResonanceRoot()
    {
        File.WriteAllBytes(Path.Combine(_root, "Resonance.exe"), []);
        Directory.CreateDirectory(Path.Combine(_root, "DATAS"));
        Directory.CreateDirectory(Path.Combine(_root, "FONT"));
        Directory.CreateDirectory(Path.Combine(_root, "LEVELS"));

        bool ok = GameProfile.TryDetect(_root, out GameProfile? profile, out string? error);

        Assert.True(ok, error);
        Assert.NotNull(profile);
        Assert.Equal(GameKind.Resonance, profile.Kind);
        Assert.Equal(Path.GetFullPath(_root), profile.Root);
        Assert.Equal(new[] { "DATAS", "FONT", "LEVELS" }, profile.DataRoots);
    }

    [Fact]
    public void RejectsMissingExecutable()
    {
        Directory.CreateDirectory(Path.Combine(_root, "DATAS"));
        Directory.CreateDirectory(Path.Combine(_root, "FONT"));
        Directory.CreateDirectory(Path.Combine(_root, "LEVELS"));

        Assert.False(GameProfile.TryDetect(_root, out _, out string? error));
        Assert.Contains("Resonance.exe", error);
    }

    [Theory]
    [InlineData("DATAS")]
    [InlineData("FONT")]
    [InlineData("LEVELS")]
    public void RejectsMissingDataFolder(string missing)
    {
        File.WriteAllBytes(Path.Combine(_root, "Resonance.exe"), []);
        foreach (string folder in new[] { "DATAS", "FONT", "LEVELS" })
        {
            if (folder != missing)
            {
                Directory.CreateDirectory(Path.Combine(_root, folder));
            }
        }

        Assert.False(GameProfile.TryDetect(_root, out _, out string? error));
        Assert.Contains(missing, error);
    }

    [Fact]
    public void RejectsRequiemWithClearMessage()
    {
        File.WriteAllBytes(Path.Combine(_root, "APlagueTaleRequiem_x64.exe"), []);
        Directory.CreateDirectory(Path.Combine(_root, "DATAS"));

        Assert.False(GameProfile.TryDetect(_root, out _, out string? error));
        Assert.Contains("Requiem", error);
        Assert.Contains("V1", error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsEmptyRoot(string? root)
    {
        Assert.False(GameProfile.TryDetect(root, out _, out string? error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
