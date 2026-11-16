namespace Avalonia.ContributionBot.GitHub;

/// <summary>
/// Represents a GitHub client.
/// </summary>
public interface IGitHubClient
{
    Task<PullRequest> GetPullRequestAsync(RepositoryName repository, int number, CancellationToken cancellationToken);

    Task<CollaboratorPermission> GetCollaboratorPermissionAsync(RepositoryName repository, string login, CancellationToken cancellationToken);

    Task<IReadOnlyList<IssueComment>> GetIssueCommentsAsync(RepositoryName repository, int number, CancellationToken cancellationToken);

    Task<RepositoryFile?> GetFileAsync(RepositoryName repository, string path, string branch, CancellationToken cancellationToken);

    Task<IssueComment> CreateIssueCommentAsync(RepositoryName repository, int number, string body, CancellationToken cancellationToken);

    Task<IssueComment> UpdateIssueCommentAsync(RepositoryName repository, long commentId, string body, CancellationToken cancellationToken);

    Task ClosePullRequestAsync(RepositoryName repository, int number, CancellationToken cancellationToken);

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

    Task<Uri> AddDiscussionCommentAsync(string discussionNodeId, string? replyToNodeId, string body, CancellationToken cancellationToken);
}
