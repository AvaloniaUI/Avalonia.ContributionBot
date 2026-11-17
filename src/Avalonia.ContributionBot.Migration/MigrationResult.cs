using Avalonia.ContributionBot.Storage;

namespace Avalonia.ContributionBot.Migration;

public sealed class MigrationResult
{
    public required ContributorsDocument Document { get; init; }

    /// <summary>The users who signed more than once, whose later signatures were dropped.</summary>
    public required List<MergedSignature> Merges { get; init; }
}