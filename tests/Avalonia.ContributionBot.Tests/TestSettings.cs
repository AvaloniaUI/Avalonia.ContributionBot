using Avalonia.ContributionBot.Configuration;
using Avalonia.ContributionBot.GitHub;

namespace Avalonia.ContributionBot.Tests;

internal static class TestSettings
{
    public static readonly BotSettings Default = new()
    {
        EventName = "test",
        EventFile = "event.json",
        CommandPrefix = "!contribution-bot",
        StoreRepository = new RepositoryName("ExampleOrg", "Store"),
        StoreBranch = "main",
        StorePath = "contributors.json",
        ClaUrl = new Uri("https://example.com/CLA.md"),
        IntroductionUrl = new Uri("https://example.com/discussions"),
        StatusContext = "CLA",
        DryRun = false
    };
}
