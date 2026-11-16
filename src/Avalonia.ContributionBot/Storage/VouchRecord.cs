namespace Avalonia.ContributionBot.Storage;

public sealed class VouchRecord
{
    public required VouchStatus Status { get; set; }

    /// <summary>When the vouch was recorded, or null for entries imported from the legacy CLA bot.</summary>
    public DateTimeOffset? At { get; set; }

    /// <summary>The maintainer who recorded the vouch, or null for entries imported from the legacy CLA bot.</summary>
    public UserReference? By { get; set; }

    public string? Reason { get; set; }

    /// <summary>The URL of the comment that recorded the vouch, or <see cref="ImportedSource"/>.</summary>
    public required string Source { get; set; }

    /// <summary>The source of vouches imported from the legacy CLA bot.</summary>
    public const string ImportedSource = "imported";
}