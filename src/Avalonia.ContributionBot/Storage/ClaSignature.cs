namespace Avalonia.ContributionBot.Storage;

public sealed class ClaSignature
{
    public required DateTimeOffset SignedAt { get; init; }

    /// <summary>The URL of the comment in which the contributor accepted the CLA.</summary>
    public required string Source { get; init; }
}