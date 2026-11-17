namespace Avalonia.ContributionBot.Evaluation;

/// <summary>
/// The level of a pull request author, which decides what happens to the pull request.
/// </summary>
public enum ContributorLevel
{
    /// <summary>A bot account, exempt from the vouch and CLA checks.</summary>
    Bot,

    /// <summary>A collaborator with write access or higher, exempt from the vouch and CLA checks.</summary>
    Collaborator,

    /// <summary>A denounced contributor: the pull request is closed.</summary>
    Denounced,

    /// <summary>A contributor nobody has vouched for yet: the pull request is closed.</summary>
    NotVouched,

    /// <summary>A vouched contributor who hasn't accepted the CLA: the pull request can't be merged.</summary>
    ClaMissing,

    /// <summary>A vouched contributor who has accepted the CLA: the pull request can proceed.</summary>
    ClaSigned
}
