namespace Avalonia.ContributionBot.Storage;

/// <summary>
/// The content of the contributors file, shared by every repository using the bot.
/// </summary>
public sealed class ContributorsDocument
{
    public required List<Contributor> Contributors { get; init; }

    /// <summary>
    /// Returns the contributor with the given GitHub user ID, or null if there is none.
    /// </summary>
    public Contributor? Find(long id)
    {
        foreach (var contributor in Contributors)
        {
            if (contributor.Id == id)
                return contributor;
        }

        return null;
    }
}
