using Avalonia.ContributionBot.Events;

namespace Avalonia.ContributionBot.Tests.Events;

public sealed class VouchCommandTests
{
    [Theory]
    [InlineData("vouch", VouchAction.Vouch, null, null)]
    [InlineData("VOUCH", VouchAction.Vouch, null, null)]
    [InlineData("vouch @someone", VouchAction.Vouch, "someone", null)]
    [InlineData("vouch @someone knows the codebase", VouchAction.Vouch, "someone", "knows the codebase")]
    [InlineData("vouch great first discussion", VouchAction.Vouch, null, "great first discussion")]
    [InlineData("denounce @spammer\tspam  links", VouchAction.Denounce, "spammer", "spam  links")]
    [InlineData("unvouch @someone", VouchAction.Unvouch, "someone", null)]
    [InlineData("unvouch", VouchAction.Unvouch, null, null)]
    public void Parses_Commands(string text, VouchAction action, string? target, string? reason)
    {
        Assert.True(VouchCommand.TryParse(text, out var command));
        Assert.Equal(action, command.Action);
        Assert.Equal(target, command.Target);
        Assert.Equal(reason, command.Reason);
    }

    [Theory]
    [InlineData("agree")]
    [InlineData("recheck")]
    [InlineData("vouches @someone")]
    [InlineData("vouch@someone")]
    [InlineData("vouch @")]
    [InlineData("vouch @ reason")]
    public void Rejects_Other_Text(string text)
    {
        Assert.False(VouchCommand.TryParse(text, out var command));
        Assert.Null(command);
    }
}
