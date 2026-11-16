using System.Text.Json.Serialization;
using Avalonia.ContributionBot.Events;

namespace Avalonia.ContributionBot.GitHub;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BlobResponse))]
[JsonSerializable(typeof(CollaboratorPermission))]
[JsonSerializable(typeof(CommentRequest))]
[JsonSerializable(typeof(CommitStatusRequest))]
[JsonSerializable(typeof(ContentResponse))]
[JsonSerializable(typeof(DiscussionCommentEvent))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(GraphQLRequest<AddDiscussionCommentVariables>))]
[JsonSerializable(typeof(GraphQLResponse<AddDiscussionCommentData>))]
[JsonSerializable(typeof(IssueComment))]
[JsonSerializable(typeof(IssueCommentEvent))]
[JsonSerializable(typeof(List<IssueComment>))]
[JsonSerializable(typeof(PullRequest))]
[JsonSerializable(typeof(PullRequestEvent))]
[JsonSerializable(typeof(PutContentRequest))]
[JsonSerializable(typeof(PutContentResponse))]
[JsonSerializable(typeof(UpdatePullRequestRequest))]
internal sealed partial class GitHubJsonContext : JsonSerializerContext;
