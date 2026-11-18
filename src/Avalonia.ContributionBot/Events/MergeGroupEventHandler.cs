using Avalonia.ContributionBot.Configuration;
using Avalonia.ContributionBot.GitHub;
using Microsoft.Extensions.Logging;

namespace Avalonia.ContributionBot.Events;

/// <summary>
/// Handles <c>merge_group</c> events: a merge queue runs the required checks again.
/// </summary>
public sealed partial class MergeGroupEventHandler(
    IGitHubClient github,
    BotSettings settings,
    ILogger<MergeGroupEventHandler> logger)
    : IEventHandler<MergeGroupEvent>
{
    public async Task HandleAsync(MergeGroupEvent payload, CancellationToken cancellationToken)
    {
        if (payload.Action != MergeGroupActions.ChecksRequested)
        {
            LogIgnoredAction(logger, payload.Action);
            return;
        }

        var repository = payload.Repository.GetName();
        var sha = payload.MergeGroup.HeadSha;

        var status = new CommitStatus
        {
            State = CommitState.Success,
            Context = settings.StatusContext,
            Description = "Each pull request was checked before entering the merge queue."
        };

        await github.CreateCommitStatusAsync(repository, sha, status, cancellationToken);
        LogMergeGroupPassed(logger, repository, sha);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring merge group action '{Action}'.")]
    private static partial void LogIgnoredAction(ILogger logger, string action);

    [LoggerMessage(Level = LogLevel.Information, Message = "Merge group {Repository}@{Sha}: success.")]
    private static partial void LogMergeGroupPassed(ILogger logger, RepositoryName repository, string sha);
}
