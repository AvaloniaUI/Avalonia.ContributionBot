namespace Avalonia.ContributionBot.Storage;

public sealed class UserReference
{
    public required long Id { get; init; }

    public required string Login { get; init; }
}