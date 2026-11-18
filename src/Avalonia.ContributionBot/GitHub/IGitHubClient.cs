namespace Avalonia.ContributionBot.GitHub;

/// <summary>
/// Represents a GitHub client.
/// </summary>
public interface IGitHubClient
{
    Task<PullRequest> GetPullRequestAsync(RepositoryName repository, int number, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the user with the given login, or null if there is none.
    /// </summary>
    Task<GitHubUser?> GetUserAsync(string login, CancellationToken cancellationToken);

    Task<CollaboratorPermission> GetCollaboratorPermissionAsync(RepositoryName repository, string login, CancellationToken cancellationToken);

    Task<IReadOnlyList<IssueComment>> GetIssueCommentsAsync(RepositoryName repository, int number, CancellationToken cancellationToken);

    Task<RepositoryFile?> GetFileAsync(RepositoryName repository, string path, string branch, CancellationToken cancellationToken);

    Task<IssueComment> CreateIssueCommentAsync(RepositoryName repository, int number, string body, CancellationToken cancellationToken);

    Task<IssueComment> UpdateIssueCommentAsync(RepositoryName repository, long commentId, string body, CancellationToken cancellationToken);

    Task<GitHubUser?> GetLastClosedByAsync(RepositoryName repository, int number, CancellationToken cancellationToken);

    Task ClosePullRequestAsync(RepositoryName repository, int number, CancellationToken cancellationToken);

    Task ReopenPullRequestAsync(RepositoryName repository, int number, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a label to an issue or pull request.
    /// </summary>
    Task AddLabelAsync(RepositoryName repository, int number, string label, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a label from an issue or pull request, if it has that label.
    /// </summary>
    Task RemoveLabelAsync(RepositoryName repository, int number, string label, CancellationToken cancellationToken);

    Task CreateCommitStatusAsync(RepositoryName repository, string sha, CommitStatus status, CancellationToken cancellationToken);

    /// <summary>
    /// Creates or updates a text file in a single commit and returns the new blob SHA.
    /// </summary>
    Task<string> PutFileAsync(
        RepositoryName repository,
        string path,
        string branch,
        string content,
        string? previousSha,
        string commitMessage,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the node ID of the top-level comment that the given discussion comment replies to,
    /// or null if the comment is itself a top-level comment.
    /// </summary>
    Task<string?> GetDiscussionCommentReplyToAsync(string commentNodeId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds a comment to a discussion, as a reply to the top-level comment <paramref name="replyToNodeId"/> if not null.
    /// </summary>
    Task<Uri> AddDiscussionCommentAsync(string discussionNodeId, string? replyToNodeId, string body, CancellationToken cancellationToken);
}
