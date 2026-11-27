using System.Net;
using Avalonia.ContributionBot.Comments;
using Avalonia.ContributionBot.Configuration;
using Avalonia.ContributionBot.GitHub;
using Avalonia.ContributionBot.Storage;
using Microsoft.Extensions.Logging;

namespace Avalonia.ContributionBot.Evaluation;

/// <summary>
/// Classifies the author of a pull request and acts on the result.
/// </summary>
public sealed partial class PullRequestEvaluator(
    IGitHubClient github,
    ContributorStore store,
    BotSettings settings,
    ILogger<PullRequestEvaluator> logger)
{
    /// <summary>
    /// Sets the commit status, creates or updates the bot comment, and closes the pull request
    /// when its author is denounced or not vouched for.
    /// A closed pull request whose author is now allowed to contribute is reopened.
    /// A pull request whose author is a repository collaborator gets the team label.
    /// </summary>
    public async Task EvaluateAsync(RepositoryName repository, PullRequest pullRequest, CancellationToken cancellationToken)
    {
        var level = await ClassifyAsync(repository, pullRequest, cancellationToken);
        await SetStatusAsync(repository, pullRequest, level, cancellationToken);

        switch (level)
        {
            case ContributorLevel.Denounced:
                await UpsertCommentAsync(repository, pullRequest, BotCommentTemplate.Denounced, cancellationToken);
                await CloseAsync(repository, pullRequest, cancellationToken);
                return;

            case ContributorLevel.NotVouched:
                await UpsertCommentAsync(repository, pullRequest, BotCommentTemplate.NotVouched, cancellationToken);
                await CloseAsync(repository, pullRequest, cancellationToken);
                return;
        }

        if (pullRequest.State == IssueStates.Closed && !await TryReopenAsync(repository, pullRequest, cancellationToken))
            return;

        switch (level)
        {
            case ContributorLevel.Collaborator:
                await AddTeamLabelAsync(repository, pullRequest, cancellationToken);
                break;

            case ContributorLevel.ClaMissing:
                await UpsertCommentAsync(repository, pullRequest, BotCommentTemplate.ClaRequired, cancellationToken);
                break;

            case ContributorLevel.ClaSigned:
                await UpdateExistingCommentAsync(repository, pullRequest, BotCommentTemplate.ClaAccepted, cancellationToken);
                break;
        }
    }

    /// <summary>
    /// Sets the commit status on the head commit only, without commenting or closing.
    /// </summary>
    public async Task UpdateStatusAsync(RepositoryName repository, PullRequest pullRequest, CancellationToken cancellationToken)
    {
        var level = await ClassifyAsync(repository, pullRequest, cancellationToken);
        await SetStatusAsync(repository, pullRequest, level, cancellationToken);
    }

    private async Task<ContributorLevel> ClassifyAsync(RepositoryName repository, PullRequest pullRequest, CancellationToken cancellationToken)
    {
        var author = pullRequest.User;
        var isBot = author.Type == GitHubUserTypes.Bot;

        var hasWriteAccess = false;
        if (!isBot)
        {
            var permission = await github.GetCollaboratorPermissionAsync(repository, author.Login, cancellationToken);
            hasWriteAccess = permission.HasWriteAccess;
        }

        Contributor? contributor = null;
        if (!isBot && !hasWriteAccess)
        {
            var document = await store.LoadAsync(cancellationToken);
            contributor = document.Find(author.Id);

            if (contributor is not null && contributor.Login != author.Login)
                await RefreshLoginAsync(repository, pullRequest, cancellationToken);
        }

        var level = ContributorClassifier.Classify(isBot, hasWriteAccess, contributor);
        LogClassified(logger, repository, pullRequest.Number, author.Login, level);
        return level;
    }

    /// <summary>
    /// Records the author's current login, which changes when a user renames their account.
    /// </summary>
    private Task<bool> RefreshLoginAsync(RepositoryName repository, PullRequest pullRequest, CancellationToken cancellationToken)
    {
        var author = pullRequest.User;

        return store.UpdateAsync(
            document =>
            {
                var contributor = document.Find(author.Id);
                if (contributor is null || contributor.Login == author.Login)
                    return null;

                var message = $"Rename @{contributor.Login} to @{author.Login} ({author.Id}), seen on {repository}#{pullRequest.Number}";
                contributor.Login = author.Login;
                return message;
            },
            cancellationToken);
    }

    private Task SetStatusAsync(RepositoryName repository, PullRequest pullRequest, ContributorLevel level, CancellationToken cancellationToken)
    {
        var (state, description) = level switch
        {
            ContributorLevel.Bot => (CommitState.Success, "Bot accounts are exempt."),
            ContributorLevel.Collaborator => (CommitState.Success, "Repository collaborators are exempt."),
            ContributorLevel.ClaSigned => (CommitState.Success, "The author has accepted the CLA."),
            ContributorLevel.ClaMissing => (CommitState.Failure, "The author must accept the CLA."),
            ContributorLevel.NotVouched => (CommitState.Failure, "The author must be vouched for."),
            ContributorLevel.Denounced => (CommitState.Failure, "The author is not permitted to contribute."),
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown contributor level.")
        };

        var status = new CommitStatus
        {
            State = state,
            Context = settings.StatusContext,
            Description = description
        };
        return github.CreateCommitStatusAsync(repository, pullRequest.Head.Sha, status, cancellationToken);
    }

    private async Task UpsertCommentAsync(
        RepositoryName repository,
        PullRequest pullRequest,
        BotCommentTemplate template,
        CancellationToken cancellationToken)
    {
        var body = BotComment.Render(template, pullRequest.User.Login, repository, settings);
        var existing = BotComment.Find(await github.GetIssueCommentsAsync(repository, pullRequest.Number, cancellationToken));

        if (existing is null)
        {
            await github.CreateIssueCommentAsync(repository, pullRequest.Number, body, cancellationToken);
            LogCommentCreated(logger, template, repository, pullRequest.Number);
        }
        else if (existing.Body != body)
        {
            await github.UpdateIssueCommentAsync(repository, existing.Id, body, cancellationToken);
            LogCommentUpdated(logger, template, repository, pullRequest.Number);
        }
    }

    private async Task UpdateExistingCommentAsync(
        RepositoryName repository,
        PullRequest pullRequest,
        BotCommentTemplate template,
        CancellationToken cancellationToken)
    {
        var existing = BotComment.Find(await github.GetIssueCommentsAsync(repository, pullRequest.Number, cancellationToken));
        if (existing is null)
            return;

        var body = BotComment.Render(template, pullRequest.User.Login, repository, settings);
        if (existing.Body == body)
            return;

        await github.UpdateIssueCommentAsync(repository, existing.Id, body, cancellationToken);
        LogCommentUpdated(logger, template, repository, pullRequest.Number);
    }

    private async Task AddTeamLabelAsync(RepositoryName repository, PullRequest pullRequest, CancellationToken cancellationToken)
    {
        if (settings.TeamLabel is not { } label)
            return;

        await github.AddLabelAsync(repository, pullRequest.Number, label, cancellationToken);
        LogLabelAdded(logger, label, repository, pullRequest.Number);
    }

    private async Task CloseAsync(RepositoryName repository, PullRequest pullRequest, CancellationToken cancellationToken)
    {
        if (pullRequest.State == IssueStates.Closed)
            return;

        await github.ClosePullRequestAsync(repository, pullRequest.Number, cancellationToken);
        LogClosed(logger, repository, pullRequest.Number);

        if (settings.ClosedLabel is { } label)
        {
            await github.AddLabelAsync(repository, pullRequest.Number, label, cancellationToken);
            LogLabelAdded(logger, label, repository, pullRequest.Number);
        }
    }

    private async Task<bool> TryReopenAsync(RepositoryName repository, PullRequest pullRequest, CancellationToken cancellationToken)
    {
        try
        {
            await github.ReopenPullRequestAsync(repository, pullRequest.Number, cancellationToken);
        }
        catch (GitHubApiException exception)
        {
            LogReopenFailed(logger, repository, pullRequest.Number, exception.Message);
            return false;
        }

        LogReopened(logger, repository, pullRequest.Number);

        if (settings.ClosedLabel is { } label)
        {
            await github.RemoveLabelAsync(repository, pullRequest.Number, label, cancellationToken);
            LogLabelRemoved(logger, label, repository, pullRequest.Number);
        }

        return true;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Pull request {Repository}#{Number} by {Author}: {Level}.")]
    private static partial void LogClassified(ILogger logger, RepositoryName repository, int number, string author, ContributorLevel level);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created the {Template} comment on {Repository}#{Number}.")]
    private static partial void LogCommentCreated(ILogger logger, BotCommentTemplate template, RepositoryName repository, int number);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated the bot comment on {Repository}#{Number} to {Template}.")]
    private static partial void LogCommentUpdated(ILogger logger, BotCommentTemplate template, RepositoryName repository, int number);

    [LoggerMessage(Level = LogLevel.Information, Message = "Closed {Repository}#{Number}.")]
    private static partial void LogClosed(ILogger logger, RepositoryName repository, int number);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reopened {Repository}#{Number}.")]
    private static partial void LogReopened(ILogger logger, RepositoryName repository, int number);

    [LoggerMessage(Level = LogLevel.Information, Message = "Added label '{Label}' to {Repository}#{Number}.")]
    private static partial void LogLabelAdded(ILogger logger, string label, RepositoryName repository, int number);

    [LoggerMessage(Level = LogLevel.Information, Message = "Removed label '{Label}' from {Repository}#{Number}.")]
    private static partial void LogLabelRemoved(ILogger logger, string label, RepositoryName repository, int number);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't reopen {Repository}#{Number}: {Error}")]
    private static partial void LogReopenFailed(ILogger logger, RepositoryName repository, int number, string error);
}
