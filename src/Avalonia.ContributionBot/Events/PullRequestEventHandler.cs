using Microsoft.Extensions.Logging;

namespace Avalonia.ContributionBot.Events;

/// <summary>
/// Handles <c>pull_request_target</c> events for opened, reopened and synchronized pull requests.
/// </summary>
public sealed partial class PullRequestEventHandler(ILogger<PullRequestEventHandler> logger) : IEventHandler<PullRequestEvent>
{
    public Task HandleAsync(PullRequestEvent payload, CancellationToken cancellationToken)
    {
        switch (payload.Action)
        {
            case "opened":
            case "reopened":
            case "synchronize":
                LogPullRequest(
                    logger,
                    payload.Repository.FullName,
                    payload.Number,
                    payload.PullRequest.User.Login,
                    payload.Action,
                    payload.PullRequest.Head.Sha);
                break;

            default:
                LogIgnoredAction(logger, payload.Action);
                break;
        }

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Pull request {Repository}#{Number} by {Author}: {Action} (head {HeadSha}).")]
    private static partial void LogPullRequest(ILogger logger, string repository, int number, string author, string action, string headSha);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring pull request action '{Action}'.")]
    private static partial void LogIgnoredAction(ILogger logger, string action);
}
