using Avalonia.ContributionBot.Storage;

namespace Avalonia.ContributionBot.Evaluation;

public static class ContributorClassifier
{
    public static ContributorLevel Classify(bool isBot, bool hasWriteAccess, Contributor? contributor)
    {
        if (isBot)
            return ContributorLevel.Bot;

        if (hasWriteAccess)
            return ContributorLevel.Collaborator;

        if (contributor?.Vouch is not { } vouch)
            return ContributorLevel.NotVouched;

        if (vouch.Status == VouchStatus.Denounced)
            return ContributorLevel.Denounced;

        return contributor.Cla is null ? ContributorLevel.ClaMissing : ContributorLevel.ClaSigned;
    }
}
