using Avalonia.ContributionBot.Comments;
using Avalonia.ContributionBot.GitHub;
using Avalonia.ContributionBot.Tests.GitHub;

namespace Avalonia.ContributionBot.Tests.Comments;

public sealed class BotCommentTests
{
    private static readonly RepositoryName s_repository = new("AvaloniaUI", "Avalonia");

    [Theory]
    [InlineData(BotCommentTemplate.NotVouched)]
    [InlineData(BotCommentTemplate.Denounced)]
    [InlineData(BotCommentTemplate.ClaRequired)]
    [InlineData(BotCommentTemplate.ClaAccepted)]
    public void Renders_Template_With_Marker_And_No_Placeholders_Left(BotCommentTemplate template)
    {
        var body = BotComment.Render(template, "someone", s_repository, TestSettings.Default);

        Assert.StartsWith(BotComment.Marker + "\n", body);
        Assert.Contains("@someone", body);
        Assert.DoesNotContain('{', body);
        Assert.DoesNotContain('\r', body);
        Assert.False(char.IsWhiteSpace(body[^1]));
    }

    [Fact]
    public void Renders_Settings_Values()
    {
        var notVouched = BotComment.Render(BotCommentTemplate.NotVouched, "someone", s_repository, TestSettings.Default);
        Assert.Contains("AvaloniaUI/Avalonia", notVouched);
        Assert.Contains("(https://example.com/discussions)", notVouched);

        var claRequired = BotComment.Render(BotCommentTemplate.ClaRequired, "someone", s_repository, TestSettings.Default);
        Assert.Contains("(https://example.com/CLA.md)", claRequired);
        Assert.Contains("!contribution-bot agree", claRequired);
    }

    [Fact]
    public void Finds_Bot_Comment_With_Marker()
    {
        var user = new GitHubUser { Login = "someone", Id = 1, Type = GitHubUserTypes.User };
        var github = new FakeGitHubClient();
        github.AddComment(42, "Hello", user);
        github.AddComment(42, $"{BotComment.Marker}\nCopied by a user", user);
        github.AddComment(42, "Unrelated bot comment", FakeGitHubClient.BotUser);
        var expected = github.AddComment(42, $"{BotComment.Marker}\nBot comment", FakeGitHubClient.BotUser);

        Assert.Same(expected, BotComment.Find(github.GetComments(42)));
    }

    [Fact]
    public void Finds_Nothing_Without_Bot_Comment()
    {
        Assert.Null(BotComment.Find([]));
    }
}
