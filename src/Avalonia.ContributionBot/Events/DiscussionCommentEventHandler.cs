using Avalonia.ContributionBot.Configuration;
using Microsoft.Extensions.Logging;

namespace Avalonia.ContributionBot.Events;

/// <summary>
/// Handles <c>discussion_comment</c> events carrying a bot command.
/// </summary>
public sealed partial class DiscussionCommentEventHandler(BotSettings settings, ILogger<DiscussionCommentEventHandler> logger)
    : IEventHandler<DiscussionCommentEvent>
{
    public Task HandleAsync(DiscussionCommentEvent payload, CancellationToken cancellationToken)
    {
        if (payload.Action != "created")
        {
            LogIgnoredAction(logger, payload.Action);
            return Task.CompletedTask;
        }

        if (!CommandText.TryParse(payload.Comment.Body, settings.CommandPrefix, out var command))
        {
            LogIgnoredComment(logger, payload.Comment.Id);
            return Task.CompletedTask;
        }

        LogCommand(logger, command, payload.Comment.User.Login, payload.Repository.FullName, payload.Discussion.Number);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring discussion comment action '{Action}'.")]
    private static partial void LogIgnoredAction(ILogger logger, string action);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring discussion comment {CommentId}: not a bot command.")]
    private static partial void LogIgnoredComment(ILogger logger, long commentId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Command '{Command}' from {Login} on discussion {Repository}#{Number}.")]
    private static partial void LogCommand(ILogger logger, string command, string login, string repository, int number);
}
