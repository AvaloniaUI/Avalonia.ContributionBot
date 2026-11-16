using Avalonia.ContributionBot.GitHub;

namespace Avalonia.ContributionBot.Events;

public sealed class EventRepository
{
    public required string FullName { get; init; }
}

/// <summary>The payload of the <c>pull_request_target</c> event.</summary>
public sealed class PullRequestEvent
{
    public required string Action { get; init; }

    public required int Number { get; init; }

    public required PullRequest PullRequest { get; init; }

    public required EventRepository Repository { get; init; }
}

/// <summary>The payload of the <c>issue_comment</c> event, raised for comments on both issues and pull requests.</summary>
public sealed class IssueCommentEvent
{
    public required string Action { get; init; }

    public required EventIssue Issue { get; init; }

    public required IssueComment Comment { get; init; }

    public required EventRepository Repository { get; init; }
}

public sealed class EventIssue
{
    public required int Number { get; init; }

    /// <summary><c>open</c> or <c>closed</c>.</summary>
    public required string State { get; init; }

    public required GitHubUser User { get; init; }

    public EventIssuePullRequest? PullRequest { get; init; }
}

public sealed class EventIssuePullRequest;

/// <summary>The payload of the <c>discussion_comment</c> event.</summary>
public sealed class DiscussionCommentEvent
{
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

    public long? ParentId { get; init; }
}
