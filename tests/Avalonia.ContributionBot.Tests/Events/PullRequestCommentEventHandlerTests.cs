using System.Text.Json;
using Avalonia.ContributionBot.Comments;
using Avalonia.ContributionBot.Evaluation;
using Avalonia.ContributionBot.Events;
using Avalonia.ContributionBot.GitHub;
using Avalonia.ContributionBot.Storage;
using Avalonia.ContributionBot.Tests.GitHub;
using Microsoft.Extensions.Logging.Testing;

namespace Avalonia.ContributionBot.Tests.Events;

public sealed class PullRequestCommentEventHandlerTests
{
    private const int Number = 42;
    private const string HeadSha = "abc123";
    private const string AuthorLogin = "contributor";
    private const long AuthorId = 1001;
    private const string CommentUrl = "https://github.com/AvaloniaUI/Avalonia/pull/42#issuecomment-555";

    private static readonly RepositoryName s_repository = new("AvaloniaUI", "Avalonia");
    private static readonly DateTimeOffset s_commentDate = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeGitHubClient _github = new();
    private readonly FakeLogger<PullRequestCommentEventHandler> _logger = new();

    public PullRequestCommentEventHandlerTests()
        => _github.PullRequests[Number] = new PullRequest
        {
            Number = Number,
            User = new GitHubUser
            {
                Login = AuthorLogin,
                Id = AuthorId,
                Type = GitHubUserTypes.User
            },
            Head = new GitRef
            {
                Sha = HeadSha
            }
        };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Agree_Records_Signature_And_Accepts_Pull_Request()
    {
        SetAuthor(VouchStatus.Vouched, signed: false);
        _github.AddComment(Number, RenderComment(BotCommentTemplate.ClaRequired), FakeGitHubClient.BotUser);

        await HandleAsync(Agree());

        var cla = GetAuthor().Cla;
        Assert.NotNull(cla);
        Assert.Equal(s_commentDate, cla.SignedAt);
        Assert.Equal(CommentUrl, cla.Source);
        Assert.Equal($"CLA accepted by {AuthorLogin} ({AuthorId}) in {CommentUrl}", Assert.Single(_github.CommitMessages));

        AssertStatus(CommitState.Success);
        Assert.Equal(RenderComment(BotCommentTemplate.ClaAccepted), Assert.Single(_github.GetComments(Number)).Body);
        Assert.Empty(_github.ClosedPullRequests);
    }

    [Fact]
    public async Task Agree_Is_Case_Insensitive()
    {
        SetAuthor(VouchStatus.Vouched, signed: false);

        await HandleAsync(Agree("@Avalonia-Bot AGREE"));

        Assert.NotNull(GetAuthor().Cla);
        AssertStatus(CommitState.Success);
    }

    [Fact]
    public async Task Agree_Keeps_Existing_Signature()
    {
        SetAuthor(VouchStatus.Vouched, signed: true);

        await HandleAsync(Agree());

        var cla = GetAuthor().Cla;
        Assert.NotNull(cla);
        Assert.Equal(DateTimeOffset.UnixEpoch, cla.SignedAt);
        Assert.Empty(_github.CommitMessages);
        AssertStatus(CommitState.Success);
    }

    [Fact]
    public async Task Agree_Records_Signature_Of_Author_Not_Vouched_For()
    {
        SetAuthor(status: null, signed: false);

        await HandleAsync(Agree());

        var author = GetAuthor();
        Assert.Null(author.Vouch);
        Assert.NotNull(author.Cla);
        AssertStatus(CommitState.Failure);
    }

    [Fact]
    public async Task Agree_From_Someone_Else_Is_Ignored()
    {
        SetAuthor(VouchStatus.Vouched, signed: false);

        await HandleAsync(SamplePayloads.IssueComment("@avalonia-bot agree"));

        Assert.Null(GetAuthor().Cla);
        Assert.Empty(_github.CommitMessages);
        Assert.Empty(_github.Statuses);
        Assert.Equal(
            "Ignoring agree from commenter on AvaloniaUI/Avalonia#42: only the author contributor can accept the CLA.",
            _logger.LatestRecord.Message);
    }

    [Theory]
    [InlineData(CollaboratorPermissions.Write)]
    [InlineData(CollaboratorPermissions.Admin)]
    public async Task Recheck_From_Collaborator_Evaluates_Pull_Request(string permission)
    {
        SetAuthor(VouchStatus.Vouched, signed: false);
        _github.Permissions["commenter"] = permission;

        await HandleAsync(SamplePayloads.IssueComment("@avalonia-bot recheck"));

        Assert.Empty(_github.CommitMessages);
        AssertStatus(CommitState.Failure);
        Assert.Equal(RenderComment(BotCommentTemplate.ClaRequired), Assert.Single(_github.GetComments(Number)).Body);
    }

    [Theory]
    [InlineData("commenter", 3003)]
    [InlineData(AuthorLogin, AuthorId)]
    public async Task Recheck_From_Non_Collaborator_Is_Ignored(string login, long id)
    {
        SetAuthor(VouchStatus.Vouched, signed: false);

        await HandleAsync(SamplePayloads.IssueComment("@avalonia-bot recheck", commenterLogin: login, commenterId: id));

        Assert.Empty(_github.Statuses);
        Assert.Empty(_github.GetComments(Number));
        Assert.Equal(
            $"Ignoring recheck from {login} on AvaloniaUI/Avalonia#42: only collaborators with write access can request it.",
            _logger.LatestRecord.Message);
    }

    [Fact]
    public async Task Ignores_Unknown_Command()
    {
        SetAuthor(VouchStatus.Vouched, signed: false);

        await HandleAsync(Agree("@avalonia-bot agree please"));

        Assert.Empty(_github.CommitMessages);
        Assert.Empty(_github.Statuses);
        Assert.Equal("Ignoring unknown command 'agree please'.", _logger.LatestRecord.Message);
    }

    [Theory]
    [InlineData("@avalonia-bot agree", CommentActions.Edited, true, IssueStates.Open, "Ignoring issue comment action 'edited'.")]
    [InlineData("@avalonia-bot agree", CommentActions.Created, false, IssueStates.Open, "Ignoring comment on issue #42: not a pull request.")]
    [InlineData("@avalonia-bot agree", CommentActions.Created, true, IssueStates.Closed, "Ignoring comment on pull request #42: the pull request is closed.")]
    [InlineData("I agree", CommentActions.Created, true, IssueStates.Open, "Ignoring comment 555: not a bot command.")]
    public async Task Ignores_Irrelevant_Comments(string body, string action, bool isPullRequest, string state, string expectedLog)
    {
        SetAuthor(VouchStatus.Vouched, signed: false);

        await HandleAsync(SamplePayloads.IssueComment(body, action, isPullRequest, state, AuthorLogin, AuthorId));

        Assert.Empty(_github.Statuses);
        Assert.Equal(expectedLog, Assert.Single(_logger.Collector.GetSnapshot()).Message);
    }

    [Fact]
    public async Task Ignores_Bot_Comments()
    {
        SetAuthor(VouchStatus.Vouched, signed: false);

        await HandleAsync(SamplePayloads.IssueComment(
            "@avalonia-bot recheck",
            commenterLogin: "avalonia-bot[bot]",
            commenterId: 9000,
            commenterType: GitHubUserTypes.Bot));

        Assert.Empty(_github.Statuses);
        Assert.Equal(
            "Ignoring comment 555: posted by the bot account avalonia-bot[bot].",
            Assert.Single(_logger.Collector.GetSnapshot()).Message);
    }

    private static string Agree(string body = "@avalonia-bot agree")
        => SamplePayloads.IssueComment(body, commenterLogin: AuthorLogin, commenterId: AuthorId);

    private static string RenderComment(BotCommentTemplate template)
        => BotComment.Render(template, AuthorLogin, s_repository, TestSettings.Default);

    private async Task HandleAsync(string json)
    {
        var settings = TestSettings.Default;
        var store = new ContributorStore(
            _github,
            settings.StoreRepository,
            settings.StoreBranch,
            settings.StorePath,
            new FakeLogger<ContributorStore>());

        var evaluator = new PullRequestEvaluator(_github, store, settings, new FakeLogger<PullRequestEvaluator>());
        var handler = new PullRequestCommentEventHandler(_github, store, evaluator, settings, _logger);
        var payload = Assert.IsType<IssueCommentEvent>(JsonSerializer.Deserialize(json, typeof(IssueCommentEvent), GitHubRestJsonContext.Default));

        await handler.HandleAsync(payload, Token);
    }

    private void SetAuthor(VouchStatus? status, bool signed)
    {
        List<Contributor> contributors = [];
        if (status is not null || signed)
        {
            contributors.Add(new Contributor
            {
                Id = AuthorId,
                Login = AuthorLogin,
                Vouch = status is { } vouchStatus
                    ? new VouchRecord
                    {
                        Status = vouchStatus,
                        Source = VouchRecord.ImportedSource
                    }
                    : null,
                Cla = signed
                    ? new ClaSignature
                    {
                        SignedAt = DateTimeOffset.UnixEpoch,
                        Source = "https://example.com"
                    }
                    : null
            });
        }

        var document = new ContributorsDocument { Contributors = contributors };
        _github.SetContent(TestSettings.Default.StorePath, ContributorsDocumentSerializer.Serialize(document));
    }

    private Contributor GetAuthor()
    {
        var content = _github.GetContent(TestSettings.Default.StorePath);
        var author = ContributorsDocumentSerializer.Deserialize(content).Find(AuthorId);
        Assert.NotNull(author);
        return author;
    }

    private void AssertStatus(CommitState expected)
    {
        var (sha, status) = Assert.Single(_github.Statuses);
        Assert.Equal(HeadSha, sha);
        Assert.Equal(expected, status.State);
    }
}
