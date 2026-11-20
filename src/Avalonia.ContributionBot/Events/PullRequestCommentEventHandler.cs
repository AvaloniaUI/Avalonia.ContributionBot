using Avalonia.ContributionBot.Comments;
using Avalonia.ContributionBot.Configuration;
using Avalonia.ContributionBot.Evaluation;
using Avalonia.ContributionBot.GitHub;
using Avalonia.ContributionBot.Storage;
using Microsoft.Extensions.Logging;

namespace Avalonia.ContributionBot.Events;

/// <summary>
/// Handles <c>issue_comment</c> events carrying a bot command on a pull request.
/// </summary>
public sealed partial class PullRequestCommentEventHandler(
    IGitHubClient github,
    ContributorStore store,
    PullRequestEvaluator evaluator,
    BotSettings settings,
    ILogger<PullRequestCommentEventHandler> logger)
    : IEventHandler<IssueCommentEvent>
{
    private const string AgreeCommandName = "agree";
    private const string RecheckCommandName = "recheck";

    public async Task HandleAsync(IssueCommentEvent payload, CancellationToken cancellationToken)
    {
        var issue = payload.Issue;
        var comment = payload.Comment;

        if (payload.Action != CommentActions.Created)
        {
            LogIgnoredAction(logger, payload.Action);
            return;
        }

        if (issue.PullRequest is null)
        {
            LogIgnoredIssue(logger, issue.Number);
            return;
        }

        if (comment.User.Type == GitHubUserTypes.Bot)
        {
            LogIgnoredBotComment(logger, comment.Id, comment.User.Login);
            return;
        }

        if (!CommandText.TryParse(comment.Body, settings.CommandPrefix, out var command))
        {
            LogIgnoredComment(logger, comment.Id);
            return;
        }

        var isRecheck = command.Equals(RecheckCommandName, StringComparison.OrdinalIgnoreCase);
        if (issue.State != IssueStates.Open && !isRecheck)
        {
            LogIgnoredPullRequestState(logger, issue.Number, issue.State);
            return;
        }

        var repository = payload.Repository.GetName();
        LogCommand(logger, command, comment.User.Login, repository, issue.Number);

        if (isRecheck)
            await RecheckAsync(repository, issue, comment, cancellationToken);
        else if (command.Equals(AgreeCommandName, StringComparison.OrdinalIgnoreCase))
            await AgreeAsync(repository, issue, comment, cancellationToken);
        else
            LogUnknownCommand(logger, command);
    }

    /// <summary>
    /// Records the CLA signature of the pull request author, then evaluates the pull request again
    /// so that its status and bot comment reflect the signature.
    /// </summary>
    private async Task AgreeAsync(RepositoryName repository, EventIssue issue, IssueComment comment, CancellationToken cancellationToken)
    {
        if (comment.User.Id != issue.User.Id)
        {
            LogAgreeFromNonAuthor(logger, comment.User.Login, repository, issue.Number, issue.User.Login);
            return;
        }

        var recorded = await store.UpdateAsync(document => RecordSignature(document, comment), cancellationToken);
        if (!recorded)
            LogAlreadySigned(logger, comment.User.Login);

        await EvaluateAsync(repository, issue.Number, cancellationToken);
    }

    /// <summary>
    /// Evaluates the pull request again.
    /// </summary>
    private async Task RecheckAsync(RepositoryName repository, EventIssue issue, IssueComment comment, CancellationToken cancellationToken)
    {
        var permission = await github.GetCollaboratorPermissionAsync(repository, comment.User.Login, cancellationToken);
        if (!permission.HasWriteAccess)
        {
            LogRecheckFromNonCollaborator(logger, comment.User.Login, repository, issue.Number);
            return;
        }

        var pullRequest = await github.GetPullRequestAsync(repository, issue.Number, cancellationToken);
        if (pullRequest.State == IssueStates.Closed && !await IsClosedByBotAsync(repository, pullRequest, cancellationToken))
            return;

        await evaluator.EvaluateAsync(repository, pullRequest, cancellationToken);
    }

    private async Task<bool> IsClosedByBotAsync(RepositoryName repository, PullRequest pullRequest, CancellationToken cancellationToken)
    {
        if (pullRequest.MergedAt is not null)
        {
            LogRecheckOfMergedPullRequest(logger, repository, pullRequest.Number);
            return false;
        }

        var botComment = BotComment.Find(await github.GetIssueCommentsAsync(repository, pullRequest.Number, cancellationToken));
        var closedBy = await github.GetLastClosedByAsync(repository, pullRequest.Number, cancellationToken);

        if (botComment is null || closedBy is null || closedBy.Id != botComment.User.Id)
        {
            LogRecheckOfPullRequestNotClosedByBot(logger, repository, pullRequest.Number);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Adds the CLA signature carried by <paramref name="comment"/> to its author's entry, creating the entry if needed.
    /// </summary>
    /// <returns>The commit message, or null if the author has already accepted the CLA.</returns>
    private static string? RecordSignature(ContributorsDocument document, IssueComment comment)
    {
        var user = comment.User;
        var contributor = document.Find(user.Id);

        if (contributor is null)
        {
            contributor = new Contributor { Id = user.Id, Login = user.Login };
            document.Contributors.Add(contributor);
        }

        if (contributor.Cla is not null)
            return null;

        contributor.Login = user.Login;
        contributor.Cla = new ClaSignature { SignedAt = comment.CreatedAt, Source = comment.HtmlUrl };
        return $"CLA accepted by @{user.Login} ({user.Id}) in {comment.HtmlUrl}";
    }

    private async Task EvaluateAsync(RepositoryName repository, int number, CancellationToken cancellationToken)
    {
        var pullRequest = await github.GetPullRequestAsync(repository, number, cancellationToken);
        await evaluator.EvaluateAsync(repository, pullRequest, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring issue comment action '{Action}'.")]
    private static partial void LogIgnoredAction(ILogger logger, string action);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring comment on issue #{Number}: not a pull request.")]
    private static partial void LogIgnoredIssue(ILogger logger, int number);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring comment on pull request #{Number}: the pull request is {State}.")]
    private static partial void LogIgnoredPullRequestState(ILogger logger, int number, string state);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring comment {CommentId}: posted by the bot account {Login}.")]
    private static partial void LogIgnoredBotComment(ILogger logger, long commentId, string login);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring comment {CommentId}: not a bot command.")]
    private static partial void LogIgnoredComment(ILogger logger, long commentId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Command '{Command}' from {Login} on pull request {Repository}#{Number}.")]
    private static partial void LogCommand(ILogger logger, string command, string login, RepositoryName repository, int number);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring unknown command '{Command}'.")]
    private static partial void LogUnknownCommand(ILogger logger, string command);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring agree from {Login} on {Repository}#{Number}: only the author {Author} can accept the CLA.")]
    private static partial void LogAgreeFromNonAuthor(ILogger logger, string login, RepositoryName repository, int number, string author);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring recheck from {Login} on {Repository}#{Number}: only collaborators with write access can request it.")]
    private static partial void LogRecheckFromNonCollaborator(ILogger logger, string login, RepositoryName repository, int number);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring recheck on {Repository}#{Number}: the pull request is merged.")]
    private static partial void LogRecheckOfMergedPullRequest(ILogger logger, RepositoryName repository, int number);

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring recheck on {Repository}#{Number}: the pull request is closed, and the bot didn't close it.")]
    private static partial void LogRecheckOfPullRequestNotClosedByBot(ILogger logger, RepositoryName repository, int number);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Login} has already accepted the CLA.")]
    private static partial void LogAlreadySigned(ILogger logger, string login);
}
