using System.Text.Json;
using Avalonia.ContributionBot.Evaluation;
using Avalonia.ContributionBot.GitHub;
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
        if (!RepositoryName.TryParse(payload.Repository.FullName, out var repository))
            throw new InvalidOperationException($"Invalid repository name '{payload.Repository.FullName}' in the event payload.");

        switch (payload.Action)
        {
            case "opened":
            case "reopened":
                return evaluator.EvaluateAsync(repository, payload.PullRequest, cancellationToken);

            case "synchronize":
                return evaluator.UpdateStatusAsync(repository, payload.PullRequest, cancellationToken);

            default:
                LogIgnoredAction(logger, payload.Action);
                return Task.CompletedTask;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring pull request action '{Action}'.")]
    private static partial void LogIgnoredAction(ILogger logger, string action);
}
