namespace Avalonia.ContributionBot.Storage;

public sealed class Contributor
{
    /// <summary>The GitHub numeric user ID, which identifies the contributor.</summary>
    public required long Id { get; init; }

    /// <summary>The GitHub login, informational and refreshed whenever the bot sees the user.</summary>
    public required string Login { get; set; }

    public VouchRecord? Vouch { get; set; }

    public ClaSignature? Cla { get; set; }
}