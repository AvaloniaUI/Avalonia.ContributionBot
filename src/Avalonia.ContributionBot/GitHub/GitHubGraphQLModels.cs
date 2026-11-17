using System.Text.Json.Serialization;

namespace Avalonia.ContributionBot.GitHub;

internal sealed class GraphQLRequest<TVariables>
{
    public required string Query { get; init; }

    public required TVariables Variables { get; init; }
}

internal sealed class GraphQLResponse<TData>
{
    public TData? Data { get; init; }

    public List<GraphQLError>? Errors { get; init; }
}

internal sealed class GraphQLError
{
    public required string Message { get; init; }
}

internal sealed class AddDiscussionCommentVariables
{
    public required string DiscussionId { get; init; }

    public string? ReplyToId { get; init; }

    public required string Body { get; init; }
}

internal sealed class AddDiscussionCommentData
{
    public required AddDiscussionCommentPayload AddDiscussionComment { get; init; }
}

internal sealed class AddDiscussionCommentPayload
{
    public required DiscussionCommentNode Comment { get; init; }
}

internal sealed class DiscussionCommentNode
{
    public required string Url { get; init; }
}

internal sealed class NodeVariables
{
    public required string Id { get; init; }
}

internal sealed class DiscussionCommentNodeData
{
    public DiscussionCommentReplyNode? Node { get; init; }
}

internal sealed class DiscussionCommentReplyNode
{
    public NodeReference? ReplyTo { get; init; }
}

internal sealed class NodeReference
{
    public required string Id { get; init; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GraphQLRequest<AddDiscussionCommentVariables>))]
[JsonSerializable(typeof(GraphQLRequest<NodeVariables>))]
[JsonSerializable(typeof(GraphQLResponse<AddDiscussionCommentData>))]
[JsonSerializable(typeof(GraphQLResponse<DiscussionCommentNodeData>))]
internal sealed partial class GitHubGraphQLJsonContext : JsonSerializerContext;
