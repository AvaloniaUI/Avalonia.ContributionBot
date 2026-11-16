using Avalonia.ContributionBot.GitHub;
using Microsoft.Extensions.Logging;

namespace Avalonia.ContributionBot.Storage;

/// <summary>
/// Reads and updates the contributors file in the store repository.
/// </summary>
public sealed partial class ContributorStore(
    IGitHubClient github,
    RepositoryName repository,
    string branch,
    string path,
    ILogger<ContributorStore> logger)
{
    /// <summary>The number of times <see cref="UpdateAsync"/> tries to commit before giving up on conflicts.</summary>
    public const int MaxAttempts = 3;

    public async Task<ContributorsDocument> LoadAsync(CancellationToken cancellationToken)
    {
        var file = await GetFileAsync(cancellationToken);
        return ContributorsDocumentSerializer.Deserialize(file.Content);
    }

    /// <summary>
    /// Applies <paramref name="change"/> to the latest contributors and commits the result.
    /// When the file changes concurrently, it's read again and the change is applied again.
    /// </summary>
    /// <param name="change">
    /// Modifies the contributors and returns the commit message, or returns null when there's nothing to change.
    /// It can run several times, each time on freshly read contributors.
    /// </param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>true if a commit was made; false if <paramref name="change"/> returned null.</returns>
    public async Task<bool> UpdateAsync(Func<ContributorsDocument, string?> change, CancellationToken cancellationToken)
    {
        var attempt = 1;
        while (true)
        {
            var file = await GetFileAsync(cancellationToken);
            var document = ContributorsDocumentSerializer.Deserialize(file.Content);

            var commitMessage = change(document);
            if (commitMessage is null)
                return false;

            try
            {
                await github.PutFileAsync(
                    repository,
                    path,
                    branch,
                    ContributorsDocumentSerializer.Serialize(document),
                    file.Sha,
                    commitMessage,
                    cancellationToken);

                LogCommitted(logger, path, repository, branch, commitMessage);
                return true;
            }
            catch (GitHubConflictException) when (attempt < MaxAttempts)
            {
                LogConflict(logger, path, attempt);
            }

            ++attempt;
        }
    }

    private async Task<RepositoryFile> GetFileAsync(CancellationToken cancellationToken)
        => await github.GetFileAsync(repository, path, branch, cancellationToken)
            ?? throw new InvalidOperationException($"The contributors file '{path}' doesn't exist in {repository}@{branch}.");

    [LoggerMessage(Level = LogLevel.Information, Message = "Committed '{Path}' to {Repository}@{Branch}: {CommitMessage}")]
    private static partial void LogCommitted(ILogger logger, string path, RepositoryName repository, string branch, string commitMessage);

    [LoggerMessage(Level = LogLevel.Warning, Message = "'{Path}' changed concurrently (attempt {Attempt}), retrying.")]
    private static partial void LogConflict(ILogger logger, string path, int attempt);
}
