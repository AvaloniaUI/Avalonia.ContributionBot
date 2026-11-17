using System.Text.Json;
using Avalonia.ContributionBot.Comments;
using Avalonia.ContributionBot.Events;
using Avalonia.ContributionBot.GitHub;
using Avalonia.ContributionBot.Storage;
using Avalonia.ContributionBot.Tests.GitHub;
using Microsoft.Extensions.Logging.Testing;

namespace Avalonia.ContributionBot.Tests.Events;

public sealed class DiscussionCommentEventHandlerTests
{
    private const string DiscussionNodeId = "D_kwDOA";
    private const string CommentNodeId = "DC_kwDOA";
    private const string CommentUrl = "https://github.com/AvaloniaUI/Avalonia/discussions/7#discussioncomment-666";

    private const string AuthorLogin = "newcomer";
    private const long AuthorId = 4004;
    private const string MaintainerLogin = "maintainer";
    private const long MaintainerId = 5005;
    private const string OtherLogin = "someone";
    private const long OtherId = 6006;

    private static readonly DateTimeOffset s_commentDate = new(2026, 11, 17, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeGitHubClient _github = new();
    private readonly FakeLogger<DiscussionCommentEventHandler> _logger = new();

    public DiscussionCommentEventHandlerTests()
    {
        _github.Permissions[MaintainerLogin] = CollaboratorPermissions.Write;
        _github.Users[OtherLogin] = new GitHubUser { Login = OtherLogin, Id = OtherId, Type = GitHubUserTypes.User };
        SetContributors();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Vouches_For_Discussion_Author()
    {
        await HandleAsync("@avalonia-bot vouch");

        var vouch = GetVouch(AuthorId);
        Assert.Equal(VouchStatus.Vouched, vouch.Status);
        Assert.Equal(s_commentDate, vouch.At);
        Assert.NotNull(vouch.By);
        Assert.Equal(MaintainerId, vouch.By.Id);
        Assert.Equal(MaintainerLogin, vouch.By.Login);
        Assert.Null(vouch.Reason);
        Assert.Equal(CommentUrl, vouch.Source);

        Assert.Equal($"Vouch for {AuthorLogin} ({AuthorId}) by {MaintainerLogin} in {CommentUrl}", Assert.Single(_github.CommitMessages));
        AssertReply(DiscussionReply.Vouched, AuthorLogin);
    }

    [Fact]
    public async Task Vouches_For_Mentioned_User_With_Reason()
    {
        await HandleAsync("@avalonia-bot vouch @someone known from the forums");

        var contributor = GetContributor(OtherId);
        Assert.Equal(OtherLogin, contributor.Login);
        Assert.NotNull(contributor.Vouch);
        Assert.Equal("known from the forums", contributor.Vouch.Reason);
        Assert.Null(FindContributor(AuthorId));

        Assert.Equal(
            $"Vouch for {OtherLogin} ({OtherId}) by {MaintainerLogin} in {CommentUrl}\n\nknown from the forums",
            Assert.Single(_github.CommitMessages));
        AssertReply(DiscussionReply.Vouched, OtherLogin);
    }

    [Fact]
    public async Task Vouch_Keeps_Cla_Signature()
    {
        SetContributors(CreateContributor(AuthorId, AuthorLogin, status: null, signed: true));

        await HandleAsync("@avalonia-bot vouch");

        var contributor = GetContributor(AuthorId);
        Assert.NotNull(contributor.Vouch);
        Assert.Equal(VouchStatus.Vouched, contributor.Vouch.Status);
        Assert.NotNull(contributor.Cla);
    }

    [Fact]
    public async Task Vouch_Replaces_Denouncement()
    {
        SetContributors(CreateContributor(AuthorId, AuthorLogin, VouchStatus.Denounced, signed: false));

        await HandleAsync("@avalonia-bot vouch");

        Assert.Equal(VouchStatus.Vouched, GetVouch(AuthorId).Status);
        AssertReply(DiscussionReply.Vouched, AuthorLogin);
    }

    [Fact]
    public async Task Vouch_Leaves_Vouched_User_Unchanged()
    {
        SetContributors(CreateContributor(AuthorId, AuthorLogin, VouchStatus.Vouched, signed: false));

        await HandleAsync("@avalonia-bot vouch new reason");

        Assert.Equal(VouchRecord.ImportedSource, GetVouch(AuthorId).Source);
        Assert.Empty(_github.CommitMessages);
        AssertReply(DiscussionReply.AlreadyVouched, AuthorLogin);
    }

    [Fact]
    public async Task Denounces_And_Refreshes_Login()
    {
        SetContributors(CreateContributor(AuthorId, "old-login", VouchStatus.Vouched, signed: true));

        await HandleAsync("@avalonia-bot denounce spam");

        var contributor = GetContributor(AuthorId);
        Assert.Equal(AuthorLogin, contributor.Login);
        Assert.NotNull(contributor.Vouch);
        Assert.Equal(VouchStatus.Denounced, contributor.Vouch.Status);
        Assert.Equal("spam", contributor.Vouch.Reason);
        Assert.NotNull(contributor.Cla);

        Assert.StartsWith($"Denounce {AuthorLogin} ({AuthorId}) by {MaintainerLogin}", Assert.Single(_github.CommitMessages));
        AssertReply(DiscussionReply.Denounced, AuthorLogin);
    }

    [Fact]
    public async Task Denounce_Leaves_Denounced_User_Unchanged()
    {
        SetContributors(CreateContributor(AuthorId, AuthorLogin, VouchStatus.Denounced, signed: false));

        await HandleAsync("@avalonia-bot denounce");

        Assert.Empty(_github.CommitMessages);
        AssertReply(DiscussionReply.AlreadyDenounced, AuthorLogin);
    }

    [Fact]
    public async Task Unvouch_Keeps_Cla_Signature()
    {
        SetContributors(CreateContributor(OtherId, OtherLogin, VouchStatus.Vouched, signed: true));

        await HandleAsync("@avalonia-bot unvouch @someone");

        var contributor = GetContributor(OtherId);
        Assert.Null(contributor.Vouch);
        Assert.NotNull(contributor.Cla);

        Assert.StartsWith($"Remove the vouch for {OtherLogin} ({OtherId})", Assert.Single(_github.CommitMessages));
        AssertReply(DiscussionReply.Unvouched, OtherLogin);
    }

    [Fact]
    public async Task Unvouch_Removes_Denounced_Entry_Without_Cla()
    {
        SetContributors(CreateContributor(OtherId, OtherLogin, VouchStatus.Denounced, signed: false));

        await HandleAsync("@avalonia-bot unvouch @someone");

        Assert.Null(FindContributor(OtherId));
        Assert.StartsWith($"Remove the denouncement of {OtherLogin} ({OtherId})", Assert.Single(_github.CommitMessages));
        AssertReply(DiscussionReply.Undenounced, OtherLogin);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unvouch_Leaves_User_Without_Vouch_Unchanged(bool signed)
    {
        if (signed)
            SetContributors(CreateContributor(AuthorId, AuthorLogin, status: null, signed: true));

        await HandleAsync("@avalonia-bot unvouch");

        Assert.Empty(_github.CommitMessages);
        AssertReply(DiscussionReply.NothingToRemove, AuthorLogin);
    }

    [Fact]
    public async Task Replies_When_Mentioned_User_Does_Not_Exist()
    {
        await HandleAsync("@avalonia-bot vouch @nobody");

        Assert.Empty(_github.CommitMessages);
        AssertReply(DiscussionReply.UserNotFound, "nobody");
    }

    [Fact]
    public async Task Replies_In_The_Thread_Of_A_Reply()
    {
        _github.DiscussionReplyTo[CommentNodeId] = "DC_parent";

        await HandlePayloadAsync(SamplePayloads.DiscussionComment("@avalonia-bot vouch", parentId: 555));

        var (_, replyTo, _) = Assert.Single(_github.DiscussionComments);
        Assert.Equal("DC_parent", replyTo);
    }

    [Theory]
    [InlineData(CollaboratorPermissions.Read)]
    [InlineData(CollaboratorPermissions.None)]
    public async Task Ignores_Commands_From_Non_Maintainers(string permission)
    {
        _github.Permissions[MaintainerLogin] = permission;

        await HandleAsync("@avalonia-bot vouch");

        Assert.Empty(_github.CommitMessages);
        Assert.Empty(_github.DiscussionComments);
        Assert.Equal(
            "Ignoring command from maintainer: only maintainers with write access to AvaloniaUI/Avalonia can use it.",
            _logger.LatestRecord.Message);
    }

    [Fact]
    public async Task Ignores_Unknown_Commands()
    {
        await HandleAsync("@avalonia-bot agree");

        Assert.Empty(_github.CommitMessages);
        Assert.Empty(_github.DiscussionComments);
        Assert.Equal("Ignoring unknown command 'agree'.", _logger.LatestRecord.Message);
    }

    [Fact]
    public async Task Ignores_Bot_Comments()
    {
        await HandlePayloadAsync(SamplePayloads.DiscussionComment(
            "@avalonia-bot vouch",
            commenterLogin: "avalonia-bot[bot]",
            commenterId: 9000,
            commenterType: GitHubUserTypes.Bot));

        Assert.Empty(_github.DiscussionComments);
        Assert.Equal(
            "Ignoring discussion comment 666: posted by the bot account avalonia-bot[bot].",
            Assert.Single(_logger.Collector.GetSnapshot()).Message);
    }

    [Theory]
    [InlineData("@avalonia-bot vouch", CommentActions.Edited, "Ignoring discussion comment action 'edited'.")]
    [InlineData("Hello, I'd like to contribute!", CommentActions.Created, "Ignoring discussion comment 666: not a bot command.")]
    public async Task Ignores_Irrelevant_Comments(string body, string action, string expectedLog)
    {
        await HandlePayloadAsync(SamplePayloads.DiscussionComment(body, action));

        Assert.Empty(_github.DiscussionComments);
        Assert.Equal(expectedLog, Assert.Single(_logger.Collector.GetSnapshot()).Message);
    }

    private Task HandleAsync(string body)
        => HandlePayloadAsync(SamplePayloads.DiscussionComment(body));

    private async Task HandlePayloadAsync(string json)
    {
        var settings = TestSettings.Default;
        var store = new ContributorStore(
            _github,
            settings.StoreRepository,
            settings.StoreBranch,
            settings.StorePath,
            new FakeLogger<ContributorStore>());

        var handler = new DiscussionCommentEventHandler(_github, store, settings, _logger);
        var payload = Assert.IsType<DiscussionCommentEvent>(
            JsonSerializer.Deserialize(json, typeof(DiscussionCommentEvent), GitHubJsonContext.Default));

        await handler.HandleAsync(payload, Token);
    }

    private void SetContributors(params List<Contributor> contributors)
        => _github.SetContent(
            TestSettings.Default.StorePath,
            ContributorsDocumentSerializer.Serialize(new ContributorsDocument { Contributors = contributors }));

    private Contributor? FindContributor(long id)
        => ContributorsDocumentSerializer.Deserialize(_github.GetContent(TestSettings.Default.StorePath)).Find(id);

    private Contributor GetContributor(long id)
    {
        var contributor = FindContributor(id);
        Assert.NotNull(contributor);
        return contributor;
    }

    private VouchRecord GetVouch(long id)
    {
        var vouch = GetContributor(id).Vouch;
        Assert.NotNull(vouch);
        return vouch;
    }

    private void AssertReply(DiscussionReply reply, string user)
    {
        var (discussionNodeId, replyTo, body) = Assert.Single(_github.DiscussionComments);
        Assert.Equal(DiscussionNodeId, discussionNodeId);
        Assert.Equal(CommentNodeId, replyTo);
        Assert.Equal(reply.Render(user, MaintainerLogin), body);
    }

    private static Contributor CreateContributor(long id, string login, VouchStatus? status, bool signed)
        => new()
        {
            Id = id,
            Login = login,
            Vouch = status is { } vouchStatus ? new VouchRecord { Status = vouchStatus, Source = VouchRecord.ImportedSource } : null,
            Cla = signed ? new ClaSignature { SignedAt = DateTimeOffset.UnixEpoch, Source = "https://example.com" } : null
        };
}
