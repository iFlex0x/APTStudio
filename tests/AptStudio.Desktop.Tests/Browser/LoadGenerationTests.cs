using AptStudio.Desktop.Browser;

namespace AptStudio.Desktop.Tests.Browser;

public sealed class LoadGenerationTests
{
    [Fact]
    public void StaleGenerationDoesNotApply()
    {
        LoadGeneration generation = new();

        int first = generation.Begin();
        int second = generation.Begin();

        Assert.False(generation.IsCurrent(first));
        Assert.True(generation.IsCurrent(second));
    }
}
