using System.Text.Json.Serialization;

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

internal sealed class CommitStatusRequest
{
    public required string State { get; init; }

    public required string Context { get; init; }

    public required string Description { get; init; }
}

internal sealed class ErrorResponse
{
    public string? Message { get; init; }
}

internal sealed class GraphQLRequest<TVariables>
{
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    [JsonPropertyName("variables")]
    public required TVariables Variables { get; init; }
}

internal sealed class GraphQLResponse<TData>
{
    [JsonPropertyName("data")]
    public TData? Data { get; init; }

    [JsonPropertyName("errors")]
    public List<GraphQLError>? Errors { get; init; }
}

internal sealed class GraphQLError
{
    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

internal sealed class AddDiscussionCommentVariables
{
    [JsonPropertyName("discussionId")]
    public required string DiscussionId { get; init; }

    [JsonPropertyName("replyToId")]
    public string? ReplyToId { get; init; }

    [JsonPropertyName("body")]
    public required string Body { get; init; }
}

internal sealed class AddDiscussionCommentData
{
    [JsonPropertyName("addDiscussionComment")]
    public required AddDiscussionCommentPayload AddDiscussionComment { get; init; }
}

internal sealed class AddDiscussionCommentPayload
{
    [JsonPropertyName("comment")]
    public required DiscussionCommentNode Comment { get; init; }
}

internal sealed class DiscussionCommentNode
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }
}

internal sealed class NodeVariables
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
}

internal sealed class DiscussionCommentNodeData
{
    [JsonPropertyName("node")]
    public DiscussionCommentReplyNode? Node { get; init; }
}

internal sealed class DiscussionCommentReplyNode
{
    [JsonPropertyName("replyTo")]
    public NodeReference? ReplyTo { get; init; }
}

internal sealed class NodeReference
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
}
