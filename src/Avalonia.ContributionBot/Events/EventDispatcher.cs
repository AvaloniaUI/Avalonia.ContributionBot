using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Avalonia.ContributionBot.GitHub;
using Microsoft.Extensions.Logging;

namespace Avalonia.ContributionBot.Events;

/// <summary>
/// Deserializes an event payload according to the event name and passes it to the matching handler.
/// Unsupported events are logged and ignored.
/// </summary>
public sealed partial class EventDispatcher(
    IEventHandler<PullRequestEvent> pullRequestHandler,
    IEventHandler<IssueCommentEvent> issueCommentHandler,
    IEventHandler<DiscussionCommentEvent> discussionCommentHandler,
    ILogger<EventDispatcher> logger)
{
    public async Task DispatchAsync(string eventName, Stream payload, CancellationToken cancellationToken)
    {
        switch (eventName)
        {
            case GitHubEventNames.PullRequestTarget:
                var pullRequestEvent = await DeserializeAsync(payload, GitHubRestJsonContext.Default.PullRequestEvent, cancellationToken);
                await pullRequestHandler.HandleAsync(pullRequestEvent, cancellationToken);
                break;

            case GitHubEventNames.IssueComment:
                var issueCommentEvent = await DeserializeAsync(payload, GitHubRestJsonContext.Default.IssueCommentEvent, cancellationToken);
                await issueCommentHandler.HandleAsync(issueCommentEvent, cancellationToken);
                break;

            case GitHubEventNames.DiscussionComment:
                var discussionCommentEvent = await DeserializeAsync(payload, GitHubRestJsonContext.Default.DiscussionCommentEvent, cancellationToken);
                await discussionCommentHandler.HandleAsync(discussionCommentEvent, cancellationToken);
                break;

            default:
                LogUnsupportedEvent(logger, eventName);
                break;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ignoring unsupported event '{EventName}'.")]
    private static partial void LogUnsupportedEvent(ILogger logger, string eventName);

    private static async Task<T> DeserializeAsync<T>(Stream payload, JsonTypeInfo<T> type, CancellationToken cancellationToken)
        => await JsonSerializer.DeserializeAsync(payload, type, cancellationToken)
            ?? throw new JsonException("The event payload is empty.");
}
