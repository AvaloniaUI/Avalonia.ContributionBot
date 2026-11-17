using Avalonia.ContributionBot.Evaluation;
using Avalonia.ContributionBot.Storage;

namespace Avalonia.ContributionBot.Tests.Evaluation;

public sealed class ContributorClassifierTests
{
    [Theory]
    [InlineData(Entry.None, ContributorLevel.NotVouched)]
    [InlineData(Entry.ClaOnly, ContributorLevel.NotVouched)]
    [InlineData(Entry.Vouched, ContributorLevel.ClaMissing)]
    [InlineData(Entry.VouchedWithCla, ContributorLevel.ClaSigned)]
    [InlineData(Entry.Denounced, ContributorLevel.Denounced)]
    [InlineData(Entry.DenouncedWithCla, ContributorLevel.Denounced)]
    public void Classifies_Contributors(Entry entry, ContributorLevel expected)
    {
        var result = ContributorClassifier.Classify(isBot: false, hasWriteAccess: false, CreateContributor(entry));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(Entry.None)]
    [InlineData(Entry.Denounced)]
    public void Exempts_Bots(Entry entry)
    {
        var result = ContributorClassifier.Classify(isBot: true, hasWriteAccess: false, CreateContributor(entry));
        Assert.Equal(ContributorLevel.Bot, result);
    }

    [Theory]
    [InlineData(Entry.None)]
    [InlineData(Entry.Denounced)]
    public void Exempts_Collaborators(Entry entry)
    {
        var result = ContributorClassifier.Classify(isBot: false, hasWriteAccess: true, CreateContributor(entry));
        Assert.Equal(ContributorLevel.Collaborator, result);
    }

    private static Contributor? CreateContributor(Entry entry)
    {
        if (entry == Entry.None)
            return null;

        return new Contributor
        {
            Id = 1,
            Login = "someone",
            Vouch = entry switch
            {
                Entry.Vouched or Entry.VouchedWithCla => new VouchRecord
                {
                    Status = VouchStatus.Vouched,
                    Source = "https://example.com"
                },
                Entry.Denounced or Entry.DenouncedWithCla => new VouchRecord
                {
                    Status = VouchStatus.Denounced,
                    Source = "https://example.com"
                },
                _ => null
            },
            Cla = entry is Entry.ClaOnly or Entry.VouchedWithCla or Entry.DenouncedWithCla
                ? new ClaSignature
                {
                    SignedAt = DateTimeOffset.UnixEpoch,
                    Source = "https://example.com"
                }
                : null
        };
    }

    public enum Entry
    {
        None,
        ClaOnly,
        Vouched,
        VouchedWithCla,
        Denounced,
        DenouncedWithCla
    }
}
