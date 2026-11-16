using Avalonia.ContributionBot.Configuration;
using Microsoft.Extensions.Logging;

namespace Avalonia.ContributionBot.Events;

/// <summary>
/// Handles <c>issue_comment</c> events carrying a bot command on an open pull request.
/// </summary>
public sealed partial class IssueCommentEventHandler(BotSettings settings, ILogger<IssueCommentEventHandler> logger)
    : IEventHandler<IssueCommentEvent>
{
    public Task HandleAsync(IssueCommentEvent payload, CancellationToken cancellationToken)
    {
        if (payload.Action != "created")
        {
            LogIgnoredAction(logger, payload.Action);
            return Task.CompletedTask;
        }

        if (payload.Issue.PullRequest is null)
        {
            LogIgnoredIssue(logger, payload.Issue.Number);
            return Task.CompletedTask;
        }

        if (payload.Issue.State != "open")
        {
            LogIgnoredPullRequestState(logger, payload.Issue.Number, payload.Issue.State);
            return Task.CompletedTask;
        }

        if (!CommandText.TryParse(payload.Comment.Body, settings.CommandPrefix, out var command))
        {
            LogIgnoredComment(logger, payload.Comment.Id);
            return Task.CompletedTask;
        }

        LogCommand(logger, command, payload.Comment.User.Login, payload.Repository.FullName, payload.Issue.Number);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring issue comment action '{Action}'.")]
    private static partial void LogIgnoredAction(ILogger logger, string action);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring comment on issue #{Number}: not a pull request.")]
    private static partial void LogIgnoredIssue(ILogger logger, int number);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring comment on pull request #{Number}: the pull request is {State}.")]
    private static partial void LogIgnoredPullRequestState(ILogger logger, int number, string state);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring comment {CommentId}: not a bot command.")]
    private static partial void LogIgnoredComment(ILogger logger, long commentId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Command '{Command}' from {Login} on pull request {Repository}#{Number}.")]
    private static partial void LogCommand(ILogger logger, string command, string login, string repository, int number);
}
