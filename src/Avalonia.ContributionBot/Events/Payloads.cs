using Avalonia.ContributionBot.GitHub;

namespace Avalonia.ContributionBot.Events;

public sealed class EventRepository
{
    public required string FullName { get; init; }

    public RepositoryName GetName()
        => RepositoryName.TryParse(FullName, out var name)
            ? name
            : throw new InvalidOperationException($"Invalid repository name '{FullName}' in the event payload.");
}

/// <summary>
/// The names of the GitHub events handled by the bot.
/// </summary>
public static class GitHubEventNames
{
    public const string PullRequestTarget = "pull_request_target";
    public const string IssueComment = "issue_comment";
    public const string DiscussionComment = "discussion_comment";
}

/// <summary>The payload of the <c>pull_request_target</c> event.</summary>
public sealed class PullRequestEvent
{
    /// <summary>The activity type; the ones handled by the bot are <see cref="PullRequestActions"/> values.</summary>
    public required string Action { get; init; }

    public required int Number { get; init; }

    public required PullRequest PullRequest { get; init; }

    public required EventRepository Repository { get; init; }
}

/// <summary>
/// The values of <see cref="PullRequestEvent.Action"/> handled by the bot.
/// </summary>
public static class PullRequestActions
{
    public const string Opened = "opened";
    public const string Reopened = "reopened";
    public const string Synchronize = "synchronize";
}

/// <summary>The payload of the <c>issue_comment</c> event, raised for comments on both issues and pull requests.</summary>
public sealed class IssueCommentEvent
{
    /// <summary>The activity type, one of the <see cref="CommentActions"/> values.</summary>
    public required string Action { get; init; }

    public required EventIssue Issue { get; init; }

    public required IssueComment Comment { get; init; }

    public required EventRepository Repository { get; init; }
}

/// <summary>
/// The values of <see cref="IssueCommentEvent.Action"/> and <see cref="DiscussionCommentEvent.Action"/>.
/// </summary>
public static class CommentActions
{
    public const string Created = "created";
    public const string Edited = "edited";
    public const string Deleted = "deleted";
}

public sealed class EventIssue
{
    public required int Number { get; init; }

    /// <summary>The state, one of the <see cref="IssueStates"/> values.</summary>
    public required string State { get; init; }

    public required GitHubUser User { get; init; }

    public EventIssuePullRequest? PullRequest { get; init; }
}

public sealed class EventIssuePullRequest;

/// <summary>The payload of the <c>discussion_comment</c> event.</summary>
public sealed class DiscussionCommentEvent
{
    /// <summary>The activity type, one of the <see cref="CommentActions"/> values.</summary>
    public required string Action { get; init; }

    public required EventDiscussion Discussion { get; init; }

    public required EventDiscussionComment Comment { get; init; }

    public required EventRepository Repository { get; init; }
}

public sealed class EventDiscussion
{
    public required int Number { get; init; }

    public required string NodeId { get; init; }

    public required GitHubUser User { get; init; }
}

public sealed class EventDiscussionComment
{
    public required long Id { get; init; }

    public required string NodeId { get; init; }

    public string? Body { get; init; }

    public required GitHubUser User { get; init; }

    public required string HtmlUrl { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public long? ParentId { get; init; }
}
