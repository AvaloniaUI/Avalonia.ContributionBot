using Microsoft.Extensions.Logging;

namespace Avalonia.ContributionBot.GitHub;

/// <summary>
/// <see cref="IGitHubClient"/> decorator that forwards read operations and logs write operations without performing them.
/// </summary>
public sealed partial class DryRunGitHubClient(IGitHubClient inner, ILogger<DryRunGitHubClient> logger) : IGitHubClient
{
    private static readonly GitHubUser s_dryRunUser = new()
    {
        Login = "dry-run",
        Id = 0,
        Type = GitHubUserTypes.Bot
    };

    public Task<PullRequest> GetPullRequestAsync(RepositoryName repository, int number, CancellationToken cancellationToken)
        => inner.GetPullRequestAsync(repository, number, cancellationToken);

    public Task<GitHubUser?> GetUserAsync(string login, CancellationToken cancellationToken)
        => inner.GetUserAsync(login, cancellationToken);

    public Task<CollaboratorPermission> GetCollaboratorPermissionAsync(RepositoryName repository, string login, CancellationToken cancellationToken)
        => inner.GetCollaboratorPermissionAsync(repository, login, cancellationToken);

    public Task<IReadOnlyList<IssueComment>> GetIssueCommentsAsync(RepositoryName repository, int number, CancellationToken cancellationToken)
        => inner.GetIssueCommentsAsync(repository, number, cancellationToken);

    public Task<GitHubUser?> GetLastClosedByAsync(RepositoryName repository, int number, CancellationToken cancellationToken)
        => inner.GetLastClosedByAsync(repository, number, cancellationToken);

    public Task<RepositoryFile?> GetFileAsync(RepositoryName repository, string path, string branch, CancellationToken cancellationToken)
        => inner.GetFileAsync(repository, path, branch, cancellationToken);

    public Task<IssueComment> CreateIssueCommentAsync(RepositoryName repository, int number, string body, CancellationToken cancellationToken)
    {
        LogComment(logger, repository, number, body);
        return Task.FromResult(CreateComment(0, body));
    }

    public Task<IssueComment> UpdateIssueCommentAsync(RepositoryName repository, long commentId, string body, CancellationToken cancellationToken)
    {
        LogUpdateComment(logger, commentId, repository, body);
        return Task.FromResult(CreateComment(commentId, body));
    }

    public Task ClosePullRequestAsync(RepositoryName repository, int number, CancellationToken cancellationToken)
    {
        LogClosePullRequest(logger, repository, number);
        return Task.CompletedTask;
    }

    public Task ReopenPullRequestAsync(RepositoryName repository, int number, CancellationToken cancellationToken)
    {
        LogReopenPullRequest(logger, repository, number);
        return Task.CompletedTask;
    }

    public Task CreateCommitStatusAsync(RepositoryName repository, string sha, CommitStatus status, CancellationToken cancellationToken)
    {
        LogCommitStatus(logger, status.Context, repository, sha, status.State, status.Description);
        return Task.CompletedTask;
    }

    public Task<string> PutFileAsync(
        RepositoryName repository,
        string path,
        string branch,
        string content,
        string? previousSha,
        string commitMessage,
        CancellationToken cancellationToken)
    {
        LogPutFile(logger, path, repository, branch, commitMessage, content);
        return Task.FromResult("dry-run");
    }

    public Task<string?> GetDiscussionCommentReplyToAsync(string commentNodeId, CancellationToken cancellationToken)
        => inner.GetDiscussionCommentReplyToAsync(commentNodeId, cancellationToken);

    public Task<Uri> AddDiscussionCommentAsync(string discussionNodeId, string? replyToNodeId, string body, CancellationToken cancellationToken)
    {
        LogDiscussionComment(logger, discussionNodeId, replyToNodeId ?? "none", body);
        return Task.FromResult(new Uri("https://dry-run/some-comment/"));
    }

    private static IssueComment CreateComment(long id, string body)
        => new()
        {
            Id = id,
            Body = body,
            User = s_dryRunUser,
            HtmlUrl = string.Empty,
            CreatedAt = DateTimeOffset.UtcNow
        };

    [LoggerMessage(Level = LogLevel.Information, Message = "[dry-run] Would comment on {Repository}#{Number}:\n{Body}")]
    private static partial void LogComment(ILogger logger, RepositoryName repository, int number, string body);

    [LoggerMessage(Level = LogLevel.Information, Message = "[dry-run] Would update comment {CommentId} in {Repository}:\n{Body}")]
    private static partial void LogUpdateComment(ILogger logger, long commentId, RepositoryName repository, string body);

    [LoggerMessage(Level = LogLevel.Information, Message = "[dry-run] Would close pull request {Repository}#{Number}.")]
    private static partial void LogClosePullRequest(ILogger logger, RepositoryName repository, int number);

    [LoggerMessage(Level = LogLevel.Information, Message = "[dry-run] Would reopen pull request {Repository}#{Number}.")]
    private static partial void LogReopenPullRequest(ILogger logger, RepositoryName repository, int number);

    [LoggerMessage(Level = LogLevel.Information, Message = "[dry-run] Would set status '{Context}' on {Repository}@{Sha} to {State}: {Description}")]
    private static partial void LogCommitStatus(ILogger logger, string context, RepositoryName repository, string sha, CommitState state, string description);

    [LoggerMessage(Level = LogLevel.Information, Message = "[dry-run] Would commit '{Path}' to {Repository}@{Branch} with message '{CommitMessage}':\n{Content}")]
    private static partial void LogPutFile(ILogger logger, string path, RepositoryName repository, string branch, string commitMessage, string content);

    [LoggerMessage(Level = LogLevel.Information, Message = "[dry-run] Would comment on discussion {DiscussionId} (reply to: {ReplyToId}):\n{Body}")]
    private static partial void LogDiscussionComment(ILogger logger, string discussionId, string replyToId, string body);
}
