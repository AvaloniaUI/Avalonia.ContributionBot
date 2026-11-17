using System.Text.Json;
using Avalonia.ContributionBot.Evaluation;
using Avalonia.ContributionBot.Events;
using Avalonia.ContributionBot.GitHub;
using Avalonia.ContributionBot.Storage;
using Avalonia.ContributionBot.Tests.GitHub;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Avalonia.ContributionBot.Tests.Events;

public sealed class EventHandlerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static T Parse<T>(string json)
        => Assert.IsType<T>(JsonSerializer.Deserialize(json, typeof(T), GitHubRestJsonContext.Default));

    private static string GetSingleInformation<T>(FakeLogger<T> logger)
    {
        var record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Information, record.Level);
        return record.Message;
    }

    [Theory]
    [InlineData(PullRequestActions.Opened)]
    [InlineData(PullRequestActions.Reopened)]
    public async Task Pull_Request_Handler_Evaluates_Opened_Pull_Requests(string action)
    {
        var github = new FakeGitHubClient();
        var payload = Parse<PullRequestEvent>(SamplePayloads.PullRequest(action));

        await CreatePullRequestHandler(github).HandleAsync(payload, Token);

        Assert.Single(github.Statuses);
        Assert.Single(github.GetComments(42));
        Assert.Equal([42], github.ClosedPullRequests);
    }

    [Fact]
    public async Task Pull_Request_Handler_Only_Updates_Status_On_Synchronize()
    {
        var github = new FakeGitHubClient();
        var payload = Parse<PullRequestEvent>(SamplePayloads.PullRequest(PullRequestActions.Synchronize));

        await CreatePullRequestHandler(github).HandleAsync(payload, Token);

        Assert.Equal("abc123", Assert.Single(github.Statuses).Sha);
        Assert.Empty(github.GetComments(42));
        Assert.Empty(github.ClosedPullRequests);
    }

    [Theory]
    [InlineData("closed")]
    [InlineData("edited")]
    [InlineData("labeled")]
    public async Task Pull_Request_Handler_Ignores_Other_Actions(string action)
    {
        var github = new FakeGitHubClient();
        var logger = new FakeLogger<PullRequestEventHandler>();
        var payload = Parse<PullRequestEvent>(SamplePayloads.PullRequest(action));

        await CreatePullRequestHandler(github, logger).HandleAsync(payload, Token);

        Assert.Equal($"Ignoring pull request action '{action}'.", GetSingleInformation(logger));
        Assert.Empty(github.Statuses);
    }

    private static PullRequestEventHandler CreatePullRequestHandler(
        FakeGitHubClient github,
        FakeLogger<PullRequestEventHandler>? logger = null)
    {
        var settings = TestSettings.Default;
        github.SetContent(settings.StorePath, ContributorsDocumentSerializer.Serialize(new ContributorsDocument { Contributors = [] }));

        var store = new ContributorStore(
            github,
            settings.StoreRepository,
            settings.StoreBranch,
            settings.StorePath,
            new FakeLogger<ContributorStore>());

        var evaluator = new PullRequestEvaluator(github, store, settings, new FakeLogger<PullRequestEvaluator>());
        return new PullRequestEventHandler(evaluator, logger ?? new FakeLogger<PullRequestEventHandler>());
    }
}
