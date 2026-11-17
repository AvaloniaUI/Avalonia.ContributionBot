using Avalonia.ContributionBot.Comments;

namespace Avalonia.ContributionBot.Tests.Comments;

public sealed class DiscussionReplyTests
{
    public static TheoryData<DiscussionReply> Replies => new(Enum.GetValues<DiscussionReply>());

    [Theory]
    [MemberData(nameof(Replies))]
    public void Renders_Reply_With_No_Placeholders_Left(DiscussionReply reply)
    {
        var body = reply.Render("someone", "maintainer");

        Assert.Contains("someone", body);
        Assert.DoesNotContain('{', body);
        Assert.DoesNotContain('\r', body);
        Assert.False(char.IsWhiteSpace(body[^1]));
    }

    [Fact]
    public void Renders_Maintainer()
    {
        Assert.Equal(
            "@someone has been denounced by @maintainer.",
            DiscussionReply.Denounced.Render("someone", "maintainer"));
    }
}
