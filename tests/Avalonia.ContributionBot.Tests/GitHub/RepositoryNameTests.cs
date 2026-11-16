using Avalonia.ContributionBot.GitHub;

namespace Avalonia.ContributionBot.Tests.GitHub;

public sealed class RepositoryNameTests
{
    [Fact]
    public void Parses_Owner_And_Name()
    {
        Assert.True(RepositoryName.TryParse("AvaloniaUI/Avalonia", out var repository));

        Assert.Equal("AvaloniaUI", repository.Owner);
        Assert.Equal("Avalonia", repository.Name);
        Assert.Equal("AvaloniaUI/Avalonia", repository.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Avalonia")]
    [InlineData("/Avalonia")]
    [InlineData("AvaloniaUI/")]
    [InlineData("a/b/c")]
    public void Rejects_Invalid_Names(string? value)
    {
        Assert.False(RepositoryName.TryParse(value, out _));
    }
}
