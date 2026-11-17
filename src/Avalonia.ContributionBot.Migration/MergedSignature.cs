namespace Avalonia.ContributionBot.Migration;

public sealed class MergedSignature
{
    public required long Id { get; init; }

    public required string Login { get; init; }

    public required string KeptSource { get; init; }

    public required string DroppedSource { get; init; }
}