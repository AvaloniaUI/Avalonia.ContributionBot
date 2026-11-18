using System.Text.Json.Serialization;
using Avalonia.ContributionBot.Events;

namespace Avalonia.ContributionBot.GitHub;

public sealed class GitHubUser
{
    public required string Login { get; init; }

    public required long Id { get; init; }

    /// <summary>The account type, one of the <see cref="GitHubUserTypes"/> values.</summary>
    public required string Type { get; init; }
}

/// <summary>
/// The values of <see cref="GitHubUser.Type"/>.
/// </summary>
public static class GitHubUserTypes
{
    public const string User = "User";
    public const string Bot = "Bot";
    public const string Organization = "Organization";
    public const string Mannequin = "Mannequin";
}

public sealed class GitRef
{
    public required string Sha { get; init; }
}

public sealed class PullRequest
{
    public required int Number { get; init; }

    public required GitHubUser User { get; init; }

    public required GitRef Head { get; init; }

    /// <summary>The state of the pull request, one of the <see cref="IssueStates"/> values.</summary>
    public required string State { get; init; }

    /// <summary>The date the pull request was merged, or null if it isn't merged.</summary>
    public DateTimeOffset? MergedAt { get; init; }
}

public sealed class IssueComment
{
    public required long Id { get; init; }

    public string? Body { get; init; }

    public required GitHubUser User { get; init; }

    public required string HtmlUrl { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

public sealed class CollaboratorPermission
{
    /// <summary>The base permission, one of the <see cref="CollaboratorPermissions"/> values.</summary>
    public required string Permission { get; init; }

    /// <summary>Whether the user can push to the repository, which includes the <c>maintain</c> role.</summary>
    public bool HasWriteAccess
        => Permission is CollaboratorPermissions.Admin or CollaboratorPermissions.Write;
}

/// <summary>
/// The values of <see cref="CollaboratorPermission.Permission"/>.
/// </summary>
public static class CollaboratorPermissions
{
    public const string Admin = "admin";
    public const string Write = "write";
    public const string Read = "read";
    public const string None = "none";
}

/// <summary>
/// The states of issues and pull requests.
/// </summary>
public static class IssueStates
{
    public const string Open = "open";
    public const string Closed = "closed";
}

/// <summary>
/// The text content of a repository file, with the blob SHA needed to update it.
/// </summary>
public sealed class RepositoryFile
{
    public required string Content { get; init; }

    public required string Sha { get; init; }
}

public enum CommitState
{
    Success,
    Failure
}

public sealed class CommitStatus
{
    public required CommitState State { get; init; }

    public required string Context { get; init; }

    public required string Description { get; init; }
}

internal sealed class ContentResponse
{
    /// <summary>The kind of entry, one of the <see cref="ContentTypes"/> values.</summary>
    public required string Type { get; init; }

    public required string Sha { get; init; }

    public string? Content { get; init; }

    /// <summary>The encoding of <see cref="Content"/>, one of the <see cref="ContentEncodings"/> values.</summary>
    public string? Encoding { get; init; }
}

/// <summary>
/// The values of <see cref="ContentResponse.Type"/>.
/// </summary>
internal static class ContentTypes
{
    public const string File = "file";
    public const string Directory = "dir";
    public const string Symlink = "symlink";
    public const string Submodule = "submodule";
}

/// <summary>
/// The values of <see cref="ContentResponse.Encoding"/> and <see cref="BlobResponse.Encoding"/>.
/// <see cref="None"/> means the content is omitted, which happens for files larger than 1 MB.
/// </summary>
internal static class ContentEncodings
{
    public const string Base64 = "base64";
    public const string None = "none";
}

internal sealed class BlobResponse
{
    public required string Sha { get; init; }

    public required string Content { get; init; }

    /// <summary>The encoding of <see cref="Content"/>, one of the <see cref="ContentEncodings"/> values.</summary>
    public required string Encoding { get; init; }
}

internal sealed class PutContentRequest
{
    public required string Message { get; init; }

    public required string Content { get; init; }

    public required string Branch { get; init; }

    public string? Sha { get; init; }
}

internal sealed class PutContentResponse
{
    public required PutContentInfo Content { get; init; }
}

internal sealed class PutContentInfo
{
    public required string Sha { get; init; }
}

internal sealed class CommentRequest
{
    public required string Body { get; init; }
}

internal sealed class UpdatePullRequestRequest
{
    /// <summary>The new state, one of the <see cref="IssueStates"/> values.</summary>
    public required string State { get; init; }
}

internal sealed class AddLabelsRequest
{
    public required List<string> Labels { get; init; }
}

internal sealed class CommitStatusRequest
{
    public required string State { get; init; }

    public required string Context { get; init; }

    public required string Description { get; init; }
}

internal sealed class IssueEvent
{
    /// <summary>The kind of event, such as <see cref="IssueEventTypes.Closed"/>.</summary>
    public required string Event { get; init; }

    /// <summary>The account that performed the event, or null if it has been deleted.</summary>
    public GitHubUser? Actor { get; init; }
}

/// <summary>
/// The values of <see cref="IssueEvent.Event"/> used by the bot.
/// </summary>
internal static class IssueEventTypes
{
    public const string Closed = "closed";
}

internal sealed class ErrorResponse
{
    public string? Message { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AddLabelsRequest))]
[JsonSerializable(typeof(BlobResponse))]
[JsonSerializable(typeof(CollaboratorPermission))]
[JsonSerializable(typeof(CommentRequest))]
[JsonSerializable(typeof(CommitStatusRequest))]
[JsonSerializable(typeof(ContentResponse))]
[JsonSerializable(typeof(DiscussionCommentEvent))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(GitHubUser))]
[JsonSerializable(typeof(IssueComment))]
[JsonSerializable(typeof(IssueCommentEvent))]
[JsonSerializable(typeof(List<IssueComment>))]
[JsonSerializable(typeof(List<IssueEvent>))]
[JsonSerializable(typeof(PullRequest))]
[JsonSerializable(typeof(PullRequestEvent))]
[JsonSerializable(typeof(PutContentRequest))]
[JsonSerializable(typeof(PutContentResponse))]
[JsonSerializable(typeof(UpdatePullRequestRequest))]
internal sealed partial class GitHubRestJsonContext : JsonSerializerContext;
