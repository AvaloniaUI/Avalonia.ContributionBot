using System.Net;
using Avalonia.ContributionBot.GitHub;

namespace Avalonia.ContributionBot.Tests.Storage;

/// <summary>
/// A GitHub client holding repository files in memory, rejecting writes made with an outdated SHA like the contents API does.
/// Other operations aren't supported.
/// </summary>
internal sealed class FakeFileGitHubClient : IGitHubClient
{
    private readonly Dictionary<string, RepositoryFile> _files = [];
    private int _nextSha = 1;

    public List<string> CommitMessages { get; } = [];

    /// <summary>Runs before each write, e.g. to simulate a concurrent change of the file.</summary>
    public Action? BeforePut { get; set; }

    public string? GetContent(string path)
        => _files.TryGetValue(path, out var file) ? file.Content : null;

    public void SetContent(string path, string content)
        => _files[path] = new RepositoryFile { Content = content, Sha = $"sha{_nextSha++}" };

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

    public Task<PullRequest> GetPullRequestAsync(RepositoryName repository, int number, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<CollaboratorPermission> GetCollaboratorPermissionAsync(RepositoryName repository, string login, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<IReadOnlyList<IssueComment>> GetIssueCommentsAsync(RepositoryName repository, int number, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<IssueComment> CreateIssueCommentAsync(RepositoryName repository, int number, string body, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<IssueComment> UpdateIssueCommentAsync(RepositoryName repository, long commentId, string body, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task ClosePullRequestAsync(RepositoryName repository, int number, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task CreateCommitStatusAsync(RepositoryName repository, string sha, CommitStatus status, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<Uri> AddDiscussionCommentAsync(string discussionNodeId, string? replyToNodeId, string body, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}
