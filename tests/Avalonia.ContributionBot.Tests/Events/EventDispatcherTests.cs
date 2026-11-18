using System.Text.Json;
using Avalonia.ContributionBot.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Avalonia.ContributionBot.Tests.Events;

public sealed class EventDispatcherTests
{
    private readonly RecordingHandler<PullRequestEvent> _pullRequests = new();
    private readonly RecordingHandler<IssueCommentEvent> _issueComments = new();
    private readonly RecordingHandler<DiscussionCommentEvent> _discussionComments = new();
    private readonly RecordingHandler<MergeGroupEvent> _mergeGroups = new();
    private readonly FakeLogger<EventDispatcher> _logger = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private EventDispatcher CreateDispatcher()
        => new(_pullRequests, _issueComments, _discussionComments, _mergeGroups, _logger);

    [Fact]
    public async Task Dispatches_Pull_Request_Target()
    {
        await CreateDispatcher().DispatchAsync(GitHubEventNames.PullRequestTarget, SamplePayloads.ToStream(SamplePayloads.PullRequest(PullRequestActions.Reopened)), Token);

        var payload = Assert.Single(_pullRequests.Received);
        Assert.Equal(PullRequestActions.Reopened, payload.Action);
        Assert.Equal(42, payload.Number);
        Assert.Equal("contributor", payload.PullRequest.User.Login);
        Assert.Equal("abc123", payload.PullRequest.Head.Sha);
        Assert.Equal("AvaloniaUI/Avalonia", payload.Repository.FullName);
        Assert.Empty(_issueComments.Received);
        Assert.Empty(_discussionComments.Received);
    }

    [Fact]
    public async Task Dispatches_Issue_Comment()
    {
        await CreateDispatcher().DispatchAsync(GitHubEventNames.IssueComment, SamplePayloads.ToStream(SamplePayloads.IssueComment("@bot agree")), Token);

        var payload = Assert.Single(_issueComments.Received);
        Assert.NotNull(payload.Issue.PullRequest);
        Assert.Equal("@bot agree", payload.Comment.Body);
        Assert.Equal(3003, payload.Comment.User.Id);
    }

    [Fact]
    public async Task Dispatches_Issue_Comment_On_Issue()
    {
        await CreateDispatcher().DispatchAsync(
            GitHubEventNames.IssueComment,
            SamplePayloads.ToStream(SamplePayloads.IssueComment("text", isPullRequest: false)),
            Token);

        Assert.Null(Assert.Single(_issueComments.Received).Issue.PullRequest);
    }

    [Fact]
    public async Task Dispatches_Discussion_Comment()
    {
        await CreateDispatcher().DispatchAsync(
            GitHubEventNames.DiscussionComment,
            SamplePayloads.ToStream(SamplePayloads.DiscussionComment("@bot vouch", parentId: 600)),
            Token);

        var payload = Assert.Single(_discussionComments.Received);
        Assert.Equal("D_kwDOA", payload.Discussion.NodeId);
        Assert.Equal("newcomer", payload.Discussion.User.Login);
        Assert.Equal("DC_kwDOA", payload.Comment.NodeId);
        Assert.Equal(600, payload.Comment.ParentId);
    }

    [Fact]
    public async Task Dispatches_Merge_Group()
    {
        await CreateDispatcher().DispatchAsync(
            GitHubEventNames.MergeGroup,
            SamplePayloads.ToStream(SamplePayloads.MergeGroup(MergeGroupActions.ChecksRequested)),
            Token);

        var payload = Assert.Single(_mergeGroups.Received);
        Assert.Equal(MergeGroupActions.ChecksRequested, payload.Action);
        Assert.Equal("def456", payload.MergeGroup.HeadSha);
        Assert.Equal("AvaloniaUI/Avalonia", payload.Repository.FullName);
    }

    [Fact]
    public async Task Ignores_Unsupported_Events()
    {
        await CreateDispatcher().DispatchAsync("push", SamplePayloads.ToStream("{}"), Token);

        Assert.Empty(_pullRequests.Received);
        Assert.Empty(_issueComments.Received);
        Assert.Empty(_discussionComments.Received);
        Assert.Empty(_mergeGroups.Received);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal("Ignoring unsupported event 'push'.", record.Message);
    }

    [Fact]
    public async Task Rejects_Payload_Missing_Required_Fields()
    {
        await Assert.ThrowsAsync<JsonException>(
            () => CreateDispatcher().DispatchAsync(
                GitHubEventNames.PullRequestTarget,
                SamplePayloads.ToStream("""{ "action": "opened" }"""),
                Token));
    }

    [Fact]
    public async Task Rejects_Null_Payload()
    {
        await Assert.ThrowsAsync<JsonException>(
            () => CreateDispatcher().DispatchAsync(
                GitHubEventNames.PullRequestTarget,
                SamplePayloads.ToStream("null"),
                Token));
    }

    private sealed class RecordingHandler<TEvent> : IEventHandler<TEvent>
    {
        public List<TEvent> Received { get; } = [];

        public Task HandleAsync(TEvent payload, CancellationToken cancellationToken)
        {
            Received.Add(payload);
            return Task.CompletedTask;
        }
    }
}
