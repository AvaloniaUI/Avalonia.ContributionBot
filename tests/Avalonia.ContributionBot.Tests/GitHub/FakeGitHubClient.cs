using System.Net;
using Avalonia.ContributionBot.GitHub;

namespace Avalonia.ContributionBot.Tests.GitHub;

/// <summary>
/// A GitHub client keeping a single repository's state in memory and recording the writes made to it.
/// File writes made with an outdated SHA are rejected, like the contents API does.
/// </summary>
internal sealed class FakeGitHubClient : IGitHubClient
{
    public static readonly GitHubUser BotUser = new()
    {
        Login = "avalonia-bot[bot]",
        Id = 9000,
        Type = GitHubUserTypes.Bot
    };

    private readonly Dictionary<string, RepositoryFile> _files = [];
    private readonly Dictionary<int, List<IssueComment>> _comments = [];
    private int _nextSha = 1;
    private long _nextCommentId = 1;

    public List<string> CommitMessages { get; } = [];

    /// <summary>Runs before each file write, e.g. to simulate a concurrent change of the file.</summary>
    public Action? BeforePut { get; set; }

    /// <summary>The collaborator permission of each login; logins not listed have <c>read</c>.</summary>
    public Dictionary<string, string> Permissions { get; } = [];

    public List<long> UpdatedCommentIds { get; } = [];

    public List<int> ClosedPullRequests { get; } = [];

    public Dictionary<int, PullRequest> PullRequests { get; } = [];

    public List<(string Sha, CommitStatus Status)> Statuses { get; } = [];

    public string GetContent(string path)
    {
        Assert.True(_files.TryGetValue(path, out var file), $"The file '{path}' doesn't exist.");
        return file.Content;
    }

    public void SetContent(string path, string content)
        => _files[path] = new RepositoryFile { Content = content, Sha = $"sha{_nextSha++}" };

    public IReadOnlyList<IssueComment> GetComments(int number)
        => _comments.TryGetValue(number, out var comments) ? comments : [];

    public IssueComment AddComment(int number, string body, GitHubUser user)
    {
        var comment = new IssueComment
        {
            Id = _nextCommentId++,
            Body = body,
            User = user,
            HtmlUrl = $"https://github.com/comments/{_nextCommentId}",
            CreatedAt = DateTimeOffset.UnixEpoch
        };

        if (!_comments.TryGetValue(number, out var comments))
            _comments[number] = comments = [];

        comments.Add(comment);
        return comment;
    }

    public Task<RepositoryFile?> GetFileAsync(RepositoryName repository, string path, string branch, CancellationToken cancellationToken)
        => Task.FromResult(_files.GetValueOrDefault(path));

    public Task<string> PutFileAsync(
        RepositoryName repository,
        string path,
        string branch,
        string content,
        string? previousSha,
        string commitMessage,
        CancellationToken cancellationToken)
    {
        BeforePut?.Invoke();

        if (_files.GetValueOrDefault(path)?.Sha != previousSha)
            throw new GitHubConflictException(HttpStatusCode.Conflict, $"PUT {path}", "SHA mismatch");

        SetContent(path, content);
        CommitMessages.Add(commitMessage);
        return Task.FromResult(_files[path].Sha);
    }

    public Task<CollaboratorPermission> GetCollaboratorPermissionAsync(RepositoryName repository, string login, CancellationToken cancellationToken)
        => Task.FromResult(new CollaboratorPermission { Permission = Permissions.GetValueOrDefault(login, CollaboratorPermissions.Read) });

    public Task<IReadOnlyList<IssueComment>> GetIssueCommentsAsync(RepositoryName repository, int number, CancellationToken cancellationToken)
        => Task.FromResult(GetComments(number));

    public Task<IssueComment> CreateIssueCommentAsync(RepositoryName repository, int number, string body, CancellationToken cancellationToken)
        => Task.FromResult(AddComment(number, body, BotUser));

    public Task<IssueComment> UpdateIssueCommentAsync(RepositoryName repository, long commentId, string body, CancellationToken cancellationToken)
    {
        foreach (var comments in _comments.Values)
        {
            for (var i = 0; i < comments.Count; i++)
            {
                if (comments[i].Id != commentId)
                    continue;

                var comment = comments[i];
                comments[i] = new IssueComment
                {
                    Id = comment.Id,
                    Body = body,
                    User = comment.User,
                    HtmlUrl = comment.HtmlUrl,
                    CreatedAt = comment.CreatedAt
                };

                UpdatedCommentIds.Add(commentId);
                return Task.FromResult(comments[i]);
            }
        }

        throw new GitHubApiException(HttpStatusCode.NotFound, $"PATCH comment {commentId}", "Not Found");
    }

    public Task ClosePullRequestAsync(RepositoryName repository, int number, CancellationToken cancellationToken)
    {
        ClosedPullRequests.Add(number);
        return Task.CompletedTask;
    }

    public Task CreateCommitStatusAsync(RepositoryName repository, string sha, CommitStatus status, CancellationToken cancellationToken)
    {
        Statuses.Add((sha, status));
        return Task.CompletedTask;
    }

    public Task<PullRequest> GetPullRequestAsync(RepositoryName repository, int number, CancellationToken cancellationToken)
        => PullRequests.TryGetValue(number, out var pullRequest)
            ? Task.FromResult(pullRequest)
            : throw new GitHubApiException(HttpStatusCode.NotFound, $"GET pull request {number}", "Not Found");

    public Task<Uri> AddDiscussionCommentAsync(string discussionNodeId, string? replyToNodeId, string body, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}
