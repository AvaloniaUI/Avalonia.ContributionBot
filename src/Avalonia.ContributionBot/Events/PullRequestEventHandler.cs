using Avalonia.ContributionBot.Evaluation;
using Microsoft.Extensions.Logging;

namespace Avalonia.ContributionBot.Events;

/// <summary>
/// Handles <c>pull_request_target</c> events: opened and reopened pull requests get a full evaluation,
/// synchronized ones only get the commit status of their new head commit.
/// </summary>
public sealed partial class PullRequestEventHandler(PullRequestEvaluator evaluator, ILogger<PullRequestEventHandler> logger)
    : IEventHandler<PullRequestEvent>
{
    public Task HandleAsync(PullRequestEvent payload, CancellationToken cancellationToken)
    {
        var repository = payload.Repository.GetName();

        switch (payload.Action)
        {
            case PullRequestActions.Opened:
            case PullRequestActions.Reopened:
                return evaluator.EvaluateAsync(repository, payload.PullRequest, cancellationToken);

            case PullRequestActions.Synchronize:
                return evaluator.UpdateStatusAsync(repository, payload.PullRequest, cancellationToken);

            default:
                LogIgnoredAction(logger, payload.Action);
                return Task.CompletedTask;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring pull request action '{Action}'.")]
    private static partial void LogIgnoredAction(ILogger logger, string action);
}
