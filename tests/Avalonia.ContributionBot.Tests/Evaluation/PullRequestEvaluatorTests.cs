using Avalonia.ContributionBot.Comments;
using Avalonia.ContributionBot.Evaluation;
using Avalonia.ContributionBot.GitHub;
using Avalonia.ContributionBot.Storage;
using Avalonia.ContributionBot.Tests.GitHub;
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

    private static readonly RepositoryName s_repository = new("AvaloniaUI", "Avalonia");

    private readonly FakeGitHubClient _github = new();
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

        var store = new ContributorStore(
            _github,
            settings.StoreRepository,
            settings.StoreBranch,
            settings.StorePath,
            new FakeLogger<ContributorStore>());

        _evaluator = new PullRequestEvaluator(_github, store, settings, new FakeLogger<PullRequestEvaluator>());
    }

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

        var document = ContributorsDocumentSerializer.Deserialize(_github.GetContent(TestSettings.Default.StorePath)!);
        Assert.Equal("renamed", document.Find(SignedId)!.Login);
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

    private static PullRequest CreatePullRequest(long authorId, string login, string type = GitHubUserTypes.User)
        => new()
        {
            Number = Number,
            User = new GitHubUser { Login = login, Id = authorId, Type = type },
            Head = new GitRef { Sha = HeadSha }
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
