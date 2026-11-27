using Avalonia.ContributionBot.Comments;
using Avalonia.ContributionBot.Configuration;
using Avalonia.ContributionBot.Evaluation;
using Avalonia.ContributionBot.GitHub;
using Avalonia.ContributionBot.Storage;
using Avalonia.ContributionBot.Tests.GitHub;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Avalonia.ContributionBot.Tests.Evaluation;

public sealed class PullRequestEvaluatorTests
{
    private const int Number = 42;
    private const string HeadSha = "abc123";

    private const long SignedId = 1;
    private const long UnsignedId = 2;
    private const long DenouncedId = 3;
    private const long UnknownId = 99;

    private const string ClosedLabel = "not-vouched";
    private const string TeamLabel = "team";

    private static readonly RepositoryName s_repository = new("AvaloniaUI", "Avalonia");

    private readonly FakeGitHubClient _github = new();
    private readonly FakeLogger<PullRequestEvaluator> _logger = new();
    private readonly ContributorStore _store;
    private readonly PullRequestEvaluator _evaluator;

    public PullRequestEvaluatorTests()
    {
        var settings = TestSettings.Default;
        var document = new ContributorsDocument
        {
            Contributors =
            [
                CreateContributor(SignedId, "signed", VouchStatus.Vouched, signed: true),
                CreateContributor(UnsignedId, "unsigned", VouchStatus.Vouched, signed: false),
                CreateContributor(DenouncedId, "denounced", VouchStatus.Denounced, signed: false)
            ]
        };

        _github.SetContent(settings.StorePath, ContributorsDocumentSerializer.Serialize(document));

        _store = new ContributorStore(
            _github,
            settings.StoreRepository,
            settings.StoreBranch,
            settings.StorePath,
            new FakeLogger<ContributorStore>());

        _evaluator = CreateEvaluator(settings);
    }

    private PullRequestEvaluator CreateEvaluator(BotSettings settings)
        => new(_github, _store, settings, _logger);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Exempts_Bots()
    {
        await _evaluator.EvaluateAsync(s_repository, CreatePullRequest(UnknownId, "dependabot[bot]", GitHubUserTypes.Bot), Token);

        AssertStatus(CommitState.Success);
        Assert.Empty(_github.GetComments(Number));
        Assert.Empty(_github.ClosedPullRequests);
    }

    [Theory]
    [InlineData(CollaboratorPermissions.Write)]
    [InlineData(CollaboratorPermissions.Admin)]
    public async Task Exempts_Collaborators(string permission)
    {
        _github.Permissions["maintainer"] = permission;

        await _evaluator.EvaluateAsync(s_repository, CreatePullRequest(UnknownId, "maintainer"), Token);

        AssertStatus(CommitState.Success);
        Assert.Empty(_github.GetComments(Number));
        Assert.Empty(_github.ClosedPullRequests);
    }

    [Theory]
    [InlineData(CollaboratorPermissions.Write)]
    [InlineData(CollaboratorPermissions.Admin)]
    public async Task Adds_Team_Label_To_Pull_Request_Of_Collaborator(string permission)
    {
        _github.Permissions["maintainer"] = permission;
        var evaluator = CreateEvaluator(TestSettings.Create(teamLabel: TeamLabel));

        await evaluator.EvaluateAsync(s_repository, CreatePullRequest(UnknownId, "maintainer"), Token);

        Assert.Equal([TeamLabel], _github.Labels[Number]);
    }

    [Fact]
    public async Task Closes_Pull_Request_Of_Author_Not_Vouched_For()
    {
        await _evaluator.EvaluateAsync(s_repository, CreatePullRequest(UnknownId, "newcomer"), Token);

        AssertStatus(CommitState.Failure);
        AssertSingleComment(BotCommentTemplate.NotVouched, "newcomer");
        Assert.Equal([Number], _github.ClosedPullRequests);
    }

    [Fact]
    public async Task Closes_Pull_Request_Of_Denounced_Author()
    {
        await _evaluator.EvaluateAsync(s_repository, CreatePullRequest(DenouncedId, "denounced"), Token);

        AssertStatus(CommitState.Failure);
        AssertSingleComment(BotCommentTemplate.Denounced, "denounced");
        Assert.Equal([Number], _github.ClosedPullRequests);
    }

    [Theory]
    [InlineData(UnknownId, "newcomer")]
    [InlineData(DenouncedId, "denounced")]
    public async Task Adds_Closed_Label_When_Closing(long id, string login)
    {
        var evaluator = CreateEvaluator(TestSettings.Create(ClosedLabel));

        await evaluator.EvaluateAsync(s_repository, CreatePullRequest(id, login), Token);

        Assert.Equal([Number], _github.ClosedPullRequests);
        Assert.Equal([ClosedLabel], _github.Labels[Number]);
    }

    [Fact]
    public async Task Does_Not_Label_When_No_Closed_Label_Is_Set()
    {
        await _evaluator.EvaluateAsync(s_repository, CreatePullRequest(UnknownId, "newcomer"), Token);

        Assert.Equal([Number], _github.ClosedPullRequests);
        Assert.Empty(_github.Labels);
    }

    [Fact]
    public async Task Does_Not_Label_Pull_Request_Already_Closed()
    {
        var evaluator = CreateEvaluator(TestSettings.Create(ClosedLabel));

        await evaluator.EvaluateAsync(s_repository, CreatePullRequest(UnknownId, "newcomer", state: IssueStates.Closed), Token);

        Assert.Empty(_github.ClosedPullRequests);
        Assert.Empty(_github.Labels);
    }

    [Fact]
    public async Task Asks_Vouched_Author_To_Accept_Cla()
    {
        await _evaluator.EvaluateAsync(s_repository, CreatePullRequest(UnsignedId, "unsigned"), Token);

        AssertStatus(CommitState.Failure);
        AssertSingleComment(BotCommentTemplate.ClaRequired, "unsigned");
        Assert.Empty(_github.ClosedPullRequests);
    }

    [Fact]
    public async Task Accepts_Author_Who_Signed_Cla_Without_Commenting()
    {
        await _evaluator.EvaluateAsync(s_repository, CreatePullRequest(SignedId, "signed"), Token);

        AssertStatus(CommitState.Success);
        Assert.Empty(_github.GetComments(Number));
        Assert.Empty(_github.ClosedPullRequests);
    }

    [Fact]
    public async Task Updates_Existing_Comment_When_Cla_Is_Signed()
    {
        _github.AddComment(
            Number,
            BotComment.Render(BotCommentTemplate.ClaRequired, "signed", s_repository, TestSettings.Default),
            FakeGitHubClient.BotUser);

        await _evaluator.EvaluateAsync(s_repository, CreatePullRequest(SignedId, "signed"), Token);

        AssertSingleComment(BotCommentTemplate.ClaAccepted, "signed");
        Assert.Single(_github.UpdatedCommentIds);
    }

    [Fact]
    public async Task Leaves_Unchanged_Comment_Alone()
    {
        var pullRequest = CreatePullRequest(UnsignedId, "unsigned");

        await _evaluator.EvaluateAsync(s_repository, pullRequest, Token);
        await _evaluator.EvaluateAsync(s_repository, pullRequest, Token);

        AssertSingleComment(BotCommentTemplate.ClaRequired, "unsigned");
        Assert.Empty(_github.UpdatedCommentIds);
    }

    [Fact]
    public async Task Ignores_Marker_Copied_By_A_User()
    {
        var user = new GitHubUser { Login = "unsigned", Id = UnsignedId, Type = GitHubUserTypes.User };
        var copied = _github.AddComment(Number, $"{BotComment.Marker}\nCopied", user);

        await _evaluator.EvaluateAsync(s_repository, CreatePullRequest(UnsignedId, "unsigned"), Token);

        var comments = _github.GetComments(Number);
        Assert.Equal(2, comments.Count);
        Assert.Equal(copied.Body, comments[0].Body);
        Assert.Empty(_github.UpdatedCommentIds);
    }

    [Fact]
    public async Task Reopens_Closed_Pull_Request_Of_Author_Allowed_To_Contribute()
    {
        await _evaluator.EvaluateAsync(s_repository, CreatePullRequest(UnsignedId, "unsigned", state: IssueStates.Closed), Token);

        Assert.Equal([Number], _github.ReopenedPullRequests);
        AssertStatus(CommitState.Failure);
        AssertSingleComment(BotCommentTemplate.ClaRequired, "unsigned");
        Assert.Empty(_github.ClosedPullRequests);
    }

    [Fact]
    public async Task Removes_Closed_Label_When_Reopening()
    {
        _github.Labels[Number] = ["bug", ClosedLabel];
        var evaluator = CreateEvaluator(TestSettings.Create(ClosedLabel));

        await evaluator.EvaluateAsync(s_repository, CreatePullRequest(UnsignedId, "unsigned", state: IssueStates.Closed), Token);

        Assert.Equal([Number], _github.ReopenedPullRequests);
        Assert.Equal(["bug"], _github.Labels[Number]);
    }

    [Fact]
    public async Task Keeps_Closed_Label_When_Pull_Request_Cannot_Be_Reopened()
    {
        _github.RejectReopen = true;
        _github.Labels[Number] = [ClosedLabel];
        var evaluator = CreateEvaluator(TestSettings.Create(ClosedLabel));

        await evaluator.EvaluateAsync(s_repository, CreatePullRequest(UnsignedId, "unsigned", state: IssueStates.Closed), Token);

        Assert.Equal([ClosedLabel], _github.Labels[Number]);
    }

    [Theory]
    [InlineData(UnknownId, "newcomer", BotCommentTemplate.NotVouched)]
    [InlineData(DenouncedId, "denounced", BotCommentTemplate.Denounced)]
    public async Task Leaves_Closed_Pull_Request_Of_Author_Not_Allowed_To_Contribute_Closed(long id, string login, BotCommentTemplate template)
    {
        await _evaluator.EvaluateAsync(s_repository, CreatePullRequest(id, login, state: IssueStates.Closed), Token);

        AssertStatus(CommitState.Failure);
        AssertSingleComment(template, login);
        Assert.Empty(_github.ReopenedPullRequests);
        Assert.Empty(_github.ClosedPullRequests);
    }

    [Fact]
    public async Task Stops_When_Pull_Request_Cannot_Be_Reopened()
    {
        _github.RejectReopen = true;

        await _evaluator.EvaluateAsync(s_repository, CreatePullRequest(UnsignedId, "unsigned", state: IssueStates.Closed), Token);

        AssertStatus(CommitState.Failure);
        Assert.Empty(_github.GetComments(Number));
        Assert.Equal(LogLevel.Warning, _logger.LatestRecord.Level);
        Assert.StartsWith("Couldn't reopen AvaloniaUI/Avalonia#42: ", _logger.LatestRecord.Message);
    }

    [Fact]
    public async Task Status_Update_Neither_Comments_Nor_Closes()
    {
        await _evaluator.UpdateStatusAsync(s_repository, CreatePullRequest(UnknownId, "newcomer"), Token);

        AssertStatus(CommitState.Failure);
        Assert.Empty(_github.GetComments(Number));
        Assert.Empty(_github.ClosedPullRequests);
    }

    [Fact]
    public async Task Records_Renamed_Login()
    {
        await _evaluator.UpdateStatusAsync(s_repository, CreatePullRequest(SignedId, "renamed"), Token);

        AssertStatus(CommitState.Success);
        Assert.Single(_github.CommitMessages);

        var document = ContributorsDocumentSerializer.Deserialize(_github.GetContent(TestSettings.Default.StorePath));
        var contributor = document.Find(SignedId);
        Assert.NotNull(contributor);
        Assert.Equal("renamed", contributor.Login);
    }

    [Fact]
    public async Task Does_Not_Commit_When_Login_Is_Unchanged()
    {
        await _evaluator.EvaluateAsync(s_repository, CreatePullRequest(SignedId, "signed"), Token);

        Assert.Empty(_github.CommitMessages);
    }

    private void AssertStatus(CommitState expected)
    {
        var (sha, status) = Assert.Single(_github.Statuses);
        Assert.Equal(HeadSha, sha);
        Assert.Equal(expected, status.State);
        Assert.Equal(TestSettings.Default.StatusContext, status.Context);
    }

    private void AssertSingleComment(BotCommentTemplate template, string author)
    {
        var comment = Assert.Single(_github.GetComments(Number));
        Assert.Equal(BotComment.Render(template, author, s_repository, TestSettings.Default), comment.Body);
    }

    private static PullRequest CreatePullRequest(
        long authorId,
        string login,
        string type = GitHubUserTypes.User,
        string state = IssueStates.Open)
        => new()
        {
            Number = Number,
            User = new GitHubUser { Login = login, Id = authorId, Type = type },
            Head = new GitRef { Sha = HeadSha },
            State = state
        };

    private static Contributor CreateContributor(long id, string login, VouchStatus status, bool signed)
        => new()
        {
            Id = id,
            Login = login,
            Vouch = new VouchRecord { Status = status, Source = VouchRecord.ImportedSource },
            Cla = signed ? new ClaSignature { SignedAt = DateTimeOffset.UnixEpoch, Source = "https://example.com" } : null
        };
}
