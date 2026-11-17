using System.Text.Json.Serialization;

namespace Avalonia.ContributionBot.Migration;

public sealed class LegacySignature
{
    /// <summary>The GitHub login at the time of signing.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>The GitHub numeric user ID.</summary>
    [JsonPropertyName("id")]
    public required long Id { get; init; }

    /// <summary>The ID of the pull request comment in which the CLA was accepted.</summary>
    [JsonPropertyName("comment_id")]
    public required long CommentId { get; init; }

    [JsonPropertyName("created_at")]
    public required DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("repoId")]
    public required long RepoId { get; init; }

    [JsonPropertyName("pullRequestNo")]
    public required int PullRequestNo { get; init; }
}