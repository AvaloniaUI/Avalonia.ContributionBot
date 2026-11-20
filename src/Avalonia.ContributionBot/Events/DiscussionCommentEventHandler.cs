using Avalonia.ContributionBot.Comments;
using Avalonia.ContributionBot.Configuration;
using Avalonia.ContributionBot.GitHub;
using Avalonia.ContributionBot.Storage;
using Microsoft.Extensions.Logging;

namespace Avalonia.ContributionBot.Events;

/// <summary>
/// Handles <c>discussion_comment</c> events carrying a <c>vouch</c>, <c>denounce</c> or <c>unvouch</c> command
/// posted by a maintainer, records the change and replies in the discussion.
/// </summary>
public sealed partial class DiscussionCommentEventHandler(
    IGitHubClient github,
    ContributorStore store,
    BotSettings settings,
    ILogger<DiscussionCommentEventHandler> logger)
    : IEventHandler<DiscussionCommentEvent>
{
    public async Task HandleAsync(DiscussionCommentEvent payload, CancellationToken cancellationToken)
    {
        var comment = payload.Comment;

        if (payload.Action != CommentActions.Created)
        {
            LogIgnoredAction(logger, payload.Action);
            return;
        }

        if (comment.User.Type == GitHubUserTypes.Bot)
        {
            LogIgnoredBotComment(logger, comment.Id, comment.User.Login);
            return;
        }

        if (!CommandText.TryParse(comment.Body, settings.CommandPrefix, out var text))
        {
            LogIgnoredComment(logger, comment.Id);
            return;
        }

        var repository = payload.Repository.GetName();
        LogCommand(logger, text, comment.User.Login, repository, payload.Discussion.Number);

        if (!VouchCommand.TryParse(text, out var command))
        {
            LogUnknownCommand(logger, text);
            return;
        }

        var permission = await github.GetCollaboratorPermissionAsync(repository, comment.User.Login, cancellationToken);
        if (!permission.HasWriteAccess)
        {
            LogNotMaintainer(logger, comment.User.Login, repository);
            return;
        }

        var target = payload.Discussion.User;
        if (command.Target is not null)
        {
            var user = await github.GetUserAsync(command.Target, cancellationToken);
            if (user is null)
            {
                LogUserNotFound(logger, command.Target);
                await ReplyAsync(payload, DiscussionReply.UserNotFound.Render(command.Target, comment.User.Login), cancellationToken);
                return;
            }

            target = user;
        }

        var reply = await ApplyAsync(command, target, comment, cancellationToken);
        await ReplyAsync(payload, reply.Render(target.Login, comment.User.Login), cancellationToken);
    }

    /// <summary>
    /// Records the command in the contributors file and returns the reply describing the outcome.
    /// </summary>
    private async Task<DiscussionReply> ApplyAsync(
        VouchCommand command,
        GitHubUser target,
        EventDiscussionComment comment,
        CancellationToken cancellationToken)
    {
        var reply = DiscussionReply.NothingToRemove;

        await store.UpdateAsync(
            document =>
            {
                var change = Apply(document, command, target, comment);
                reply = change.Reply;
                return change.CommitMessage;
            },
            cancellationToken);

        LogApplied(logger, command.Action, target.Login, reply);
        return reply;
    }

    /// <summary>
    /// Applies <paramref name="command"/> to <paramref name="document"/>.
    /// </summary>
    /// <returns>The commit message, or null when nothing changed, and the reply describing the outcome.</returns>
    private static (string? CommitMessage, DiscussionReply Reply) Apply(
        ContributorsDocument document,
        VouchCommand command,
        GitHubUser target,
        EventDiscussionComment comment)
    {
        var contributor = document.Find(target.Id);
        var current = contributor?.Vouch?.Status;

        DiscussionReply reply;
        string verb;

        switch (command.Action)
        {
            case VouchAction.Vouch:
                if (current == VouchStatus.Vouched)
                    return (null, DiscussionReply.AlreadyVouched);

                contributor = GetOrAdd(document, contributor, target);
                contributor.Vouch = CreateRecord(VouchStatus.Vouched, command, comment);
                (reply, verb) = (DiscussionReply.Vouched, "Vouch for");
                break;

            case VouchAction.Denounce:
                if (current == VouchStatus.Denounced)
                    return (null, DiscussionReply.AlreadyDenounced);

                contributor = GetOrAdd(document, contributor, target);
                contributor.Vouch = CreateRecord(VouchStatus.Denounced, command, comment);
                (reply, verb) = (DiscussionReply.Denounced, "Denounce");
                break;

            case VouchAction.Unvouch:
                if (contributor is null || current is null)
                    return (null, DiscussionReply.NothingToRemove);

                contributor.Vouch = null;
                (reply, verb) = current == VouchStatus.Denounced
                    ? (DiscussionReply.Undenounced, "Remove the denouncement of")
                    : (DiscussionReply.Unvouched, "Remove the vouch for");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(command), command.Action, "Unknown vouch action.");
        }

        contributor.Login = target.Login;

        var message = $"{verb} @{target.Login} ({target.Id}) by @{comment.User.Login} in {comment.HtmlUrl}";
        if (command.Reason is not null)
            message += $"\n\n{command.Reason}";

        return (message, reply);
    }

    private static Contributor GetOrAdd(ContributorsDocument document, Contributor? contributor, GitHubUser target)
    {
        if (contributor is not null)
            return contributor;

        contributor = new Contributor
        {
            Id = target.Id,
            Login = target.Login
        };
        document.Contributors.Add(contributor);
        return contributor;
    }

    private static VouchRecord CreateRecord(VouchStatus status, VouchCommand command, EventDiscussionComment comment)
        => new()
        {
            Status = status,
            At = comment.CreatedAt,
            By = new UserReference
            {
                Id = comment.User.Id,
                Login = comment.User.Login
            },
            Reason = command.Reason,
            Source = comment.HtmlUrl
        };

    /// <summary>
    /// Replies in the thread of the command comment.
    /// Discussion threads are one level deep, so a command posted as a reply gets its answer in the same thread.
    /// </summary>
    private async Task ReplyAsync(DiscussionCommentEvent payload, string body, CancellationToken cancellationToken)
    {
        var comment = payload.Comment;
        var replyTo = comment.ParentId is null
            ? comment.NodeId
            : await github.GetDiscussionCommentReplyToAsync(comment.NodeId, cancellationToken) ?? comment.NodeId;

        var url = await github.AddDiscussionCommentAsync(payload.Discussion.NodeId, replyTo, body, cancellationToken);
        LogReplied(logger, url);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring discussion comment action '{Action}'.")]
    private static partial void LogIgnoredAction(ILogger logger, string action);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring discussion comment {CommentId}: posted by the bot account {Login}.")]
    private static partial void LogIgnoredBotComment(ILogger logger, long commentId, string login);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring discussion comment {CommentId}: not a bot command.")]
    private static partial void LogIgnoredComment(ILogger logger, long commentId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Command '{Command}' from {Login} on discussion {Repository}#{Number}.")]
    private static partial void LogCommand(ILogger logger, string command, string login, RepositoryName repository, int number);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring unknown command '{Command}'.")]
    private static partial void LogUnknownCommand(ILogger logger, string command);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring command from {Login}: only maintainers with write access to {Repository} can use it.")]
    private static partial void LogNotMaintainer(ILogger logger, string login, RepositoryName repository);

    [LoggerMessage(Level = LogLevel.Warning, Message = "There is no GitHub user named '{Login}'.")]
    private static partial void LogUserNotFound(ILogger logger, string login);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Action} {Login}: {Reply}.")]
    private static partial void LogApplied(ILogger logger, VouchAction action, string login, DiscussionReply reply);

    [LoggerMessage(Level = LogLevel.Information, Message = "Replied in the discussion: {Url}")]
    private static partial void LogReplied(ILogger logger, Uri url);
}
