using Avalonia.ContributionBot.Events;

namespace Avalonia.ContributionBot.Tests.Events;

public class CommandTextTests
{
    private const string Prefix = "!contribution-bot";

    [Theory]
    [InlineData("!contribution-bot agree", "agree")]
    [InlineData("!Contribution-Bot agree", "agree")]
    [InlineData("  !contribution-bot   vouch @someone  ", "vouch @someone")]
    [InlineData("\n\n!contribution-bot recheck\nsome explanation", "recheck")]
    [InlineData("!contribution-bot\tdenounce @someone spam\r\nmore", "denounce @someone spam")]
    public void Recognizes_Commands(string body, string expected)
    {
        Assert.True(CommandText.TryParse(body, Prefix, out var command));
        Assert.Equal(expected, command);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!contribution-bot")]
    [InlineData("!contribution-bot   ")]
    [InlineData("!contribution-botagree")]
    [InlineData("!contribution-bot-other agree")]
    [InlineData("I agree !contribution-bot")]
    [InlineData("Some text\n!contribution-bot agree")]
    [InlineData("```\n!contribution-bot agree\n```")]
    public void Ignores_Non_Commands(string? body)
    {
        Assert.False(CommandText.TryParse(body, Prefix, out var command));
        Assert.Null(command);
    }
}
