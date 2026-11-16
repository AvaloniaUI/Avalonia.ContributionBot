using System.Text.Json;
using Avalonia.ContributionBot.Configuration;
using Avalonia.ContributionBot.Events;
using Avalonia.ContributionBot.GitHub;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Avalonia.ContributionBot.Tests.Events;

public sealed class EventHandlerTests
{
    private static readonly BotSettings s_settings = new()
    {
        EventName = "test",
        EventFile = "event.json",
        CommandPrefix = "@avalonia-bot",
        StoreRepository = new RepositoryName("AvaloniaUI", "Store"),
        StoreBranch = "main",
        StorePath = "contributors.json",
        ClaUrl = new Uri("https://example.com/CLA.md"),
        IntroductionUrl = new Uri("https://example.com/discussions"),
        StatusContext = "CLA",
        DryRun = false
    };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static T Parse<T>(string json)
        => (T)JsonSerializer.Deserialize(json, typeof(T), GitHubJsonContext.Default)!;

    private static string GetSingleInformation<T>(FakeLogger<T> logger)
    {
        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Information, record.Level);
        return record.Message;
    }

    [Theory]
    [InlineData("opened")]
    [InlineData("reopened")]
    [InlineData("synchronize")]
    public async Task Pull_Request_Handler_Handles_Supported_Actions(string action)
    {
        var logger = new FakeLogger<PullRequestEventHandler>();

        await new PullRequestEventHandler(logger).HandleAsync(Parse<PullRequestEvent>(SamplePayloads.PullRequest(action)), Token);

        Assert.Equal($"Pull request AvaloniaUI/Avalonia#42 by contributor: {action} (head abc123).", GetSingleInformation(logger));
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("edited")]
    [InlineData("labeled")]
    public async Task Pull_Request_Handler_Ignores_Other_Actions(string action)
    {
        var logger = new FakeLogger<PullRequestEventHandler>();

        await new PullRequestEventHandler(logger).HandleAsync(Parse<PullRequestEvent>(SamplePayloads.PullRequest(action)), Token);

        Assert.Equal($"Ignoring pull request action '{action}'.", GetSingleInformation(logger));
    }

    [Fact]
    public async Task Issue_Comment_Handler_Handles_Command_On_Open_Pull_Request()
    {
        var logger = new FakeLogger<IssueCommentEventHandler>();
        var payload = Parse<IssueCommentEvent>(SamplePayloads.IssueComment("@avalonia-bot agree"));

        await new IssueCommentEventHandler(s_settings, logger).HandleAsync(payload, Token);

        Assert.Equal("Command 'agree' from commenter on pull request AvaloniaUI/Avalonia#42.", GetSingleInformation(logger));
    }

    [Theory]
    [InlineData("@avalonia-bot agree", "edited", true, "open", "Ignoring issue comment action 'edited'.")]
    [InlineData("@avalonia-bot agree", "created", false, "open", "Ignoring comment on issue #42: not a pull request.")]
    [InlineData("@avalonia-bot agree", "created", true, "closed", "Ignoring comment on pull request #42: the pull request is closed.")]
    [InlineData("I agree", "created", true, "open", "Ignoring comment 555: not a bot command.")]
    public async Task Issue_Comment_Handler_Ignores_Irrelevant_Comments(
        string body, string action, bool isPullRequest, string state, string expectedLog)
    {
        var logger = new FakeLogger<IssueCommentEventHandler>();
        var payload = Parse<IssueCommentEvent>(SamplePayloads.IssueComment(body, action, isPullRequest, state));

        await new IssueCommentEventHandler(s_settings, logger).HandleAsync(payload, Token);

        Assert.Equal(expectedLog, GetSingleInformation(logger));
    }

    [Fact]
    public async Task Discussion_Comment_Handler_Handles_Command()
    {
        var logger = new FakeLogger<DiscussionCommentEventHandler>();
        var payload = Parse<DiscussionCommentEvent>(SamplePayloads.DiscussionComment("@avalonia-bot vouch"));

        await new DiscussionCommentEventHandler(s_settings, logger).HandleAsync(payload, Token);

        Assert.Equal("Command 'vouch' from maintainer on discussion AvaloniaUI/Avalonia#7.", GetSingleInformation(logger));
    }

    [Theory]
    [InlineData("@avalonia-bot vouch", "edited", "Ignoring discussion comment action 'edited'.")]
    [InlineData("Hello, I'd like to contribute!", "created", "Ignoring discussion comment 666: not a bot command.")]
    public async Task Discussion_Comment_Handler_Ignores_Irrelevant_Comments(string body, string action, string expectedLog)
    {
        var logger = new FakeLogger<DiscussionCommentEventHandler>();
        var payload = Parse<DiscussionCommentEvent>(SamplePayloads.DiscussionComment(body, action));

        await new DiscussionCommentEventHandler(s_settings, logger).HandleAsync(payload, Token);

        Assert.Equal(expectedLog, GetSingleInformation(logger));
    }
}
