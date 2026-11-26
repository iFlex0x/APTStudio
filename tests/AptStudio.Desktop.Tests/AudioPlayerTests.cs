using AptStudio.Desktop;

namespace AptStudio.Desktop.Tests;

public sealed class AudioPlayerTests
{
    [Fact]
    public void GarbageOggIsANamedError()
    {
        using AudioPlayer player = new();
        InvalidDataException error = Assert.Throws<InvalidDataException>(
            () => player.Play(new byte[32]));
        Assert.Contains("Ogg decode failed", error.Message);
        Assert.False(player.IsPlaying);
    }

    [Fact]
    public void StopOnIdleIsSafe()
    {
        using AudioPlayer player = new();
        player.Stop();
        Assert.False(player.IsPlaying);
        Assert.Null(player.TakeError());
    }
}
