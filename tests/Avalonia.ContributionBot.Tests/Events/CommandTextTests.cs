using Avalonia.ContributionBot.Events;

namespace Avalonia.ContributionBot.Tests.Events;

public class CommandTextTests
{
    private const string Prefix = "@avalonia-bot";

    [Theory]
    [InlineData("@avalonia-bot agree", "agree")]
    [InlineData("@Avalonia-Bot agree", "agree")]
    [InlineData("  @avalonia-bot   vouch @someone  ", "vouch @someone")]
    [InlineData("\n\n@avalonia-bot recheck\nsome explanation", "recheck")]
    [InlineData("@avalonia-bot\tdenounce @someone spam\r\nmore", "denounce @someone spam")]
    public void Recognizes_Commands(string body, string expected)
    {
        Assert.True(CommandText.TryParse(body, Prefix, out var command));
        Assert.Equal(expected, command);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("@avalonia-bot")]
    [InlineData("@avalonia-bot   ")]
    [InlineData("@avalonia-botagree")]
    [InlineData("@avalonia-bot-other agree")]
    [InlineData("I agree @avalonia-bot")]
    [InlineData("Some text\n@avalonia-bot agree")]
    [InlineData("```\n@avalonia-bot agree\n```")]
    public void Ignores_Non_Commands(string? body)
    {
        Assert.False(CommandText.TryParse(body, Prefix, out var command));
        Assert.Null(command);
    }
}
