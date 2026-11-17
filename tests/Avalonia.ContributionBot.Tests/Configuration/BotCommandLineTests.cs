using Avalonia.ContributionBot.Configuration;
using Avalonia.ContributionBot.Events;
using Avalonia.ContributionBot.GitHub;

namespace Avalonia.ContributionBot.Tests.Configuration;

public sealed class BotCommandLineTests
{
    private static readonly string[] s_requiredArgs =
    [
        "--event-name", GitHubEventNames.PullRequestTarget,
        "--event-file", "/tmp/event.json",
        "--store-repository", "ExampleOrg/ExampleRepo",
        "--cla-url", "https://example.com/CLA.md",
        "--introduction-url", "https://example.com/discussions"
    ];

    private static BotSettings GetSettings(params string[] extraArgs)
    {
        var commandLine = new BotCommandLine();
        var parseResult = commandLine.Parse([.. s_requiredArgs, .. extraArgs]);

        Assert.Empty(parseResult.Errors.Select(e => e.Message));
        return commandLine.GetSettings(parseResult);
    }

    [Fact]
    public void Parses_Required_Options_With_Defaults()
    {
        var settings = GetSettings();

        Assert.Equal(GitHubEventNames.PullRequestTarget, settings.EventName);
        Assert.Equal("/tmp/event.json", settings.EventFile);
        Assert.Equal("!contribution-bot", settings.CommandPrefix);
        Assert.Equal(new RepositoryName("ExampleOrg", "ExampleRepo"), settings.StoreRepository);
        Assert.Equal("main", settings.StoreBranch);
        Assert.Equal("contributors.json", settings.StorePath);
        Assert.Equal(new Uri("https://example.com/CLA.md"), settings.ClaUrl);
        Assert.Equal(new Uri("https://example.com/discussions"), settings.IntroductionUrl);
        Assert.Equal("Avalonia Contribution / CLA", settings.StatusContext);
        Assert.False(settings.DryRun);
    }

    [Fact]
    public void Parses_Optional_Options()
    {
        var settings = GetSettings(
            "--command-prefix", "@other-bot",
            "--store-branch", "signatures",
            "--store-path", "data/contributors.json",
            "--status-context", "CLA",
            "--dry-run");

        Assert.Equal("@other-bot", settings.CommandPrefix);
        Assert.Equal("signatures", settings.StoreBranch);
        Assert.Equal("data/contributors.json", settings.StorePath);
        Assert.Equal("CLA", settings.StatusContext);
        Assert.True(settings.DryRun);
    }

    [Theory]
    [InlineData("--cla-url", "not a url")]
    [InlineData("--cla-url", "ftp://example.com/CLA.md")]
    [InlineData("--store-repository", "no-slash")]
    [InlineData("--store-repository", "too/many/parts")]
    [InlineData("--command-prefix", "@bot name")]
    [InlineData("--event-name", "")]
    public void Rejects_Invalid_Values(string option, string value)
    {
        var args = s_requiredArgs.ToList();
        var index = args.IndexOf(option);

        if (index >= 0)
            args[index + 1] = value;
        else
        {
            args.Add(option);
            args.Add(value);
        }

        var error = Assert.Single(new BotCommandLine().Parse(args).Errors);
        Assert.Contains(option, error.Message);
    }

    [Fact]
    public void Get_Settings_Throws_When_Parse_Has_Errors()
    {
        var commandLine = new BotCommandLine();

        Assert.Throws<InvalidOperationException>(() => commandLine.GetSettings(commandLine.Parse([])));
    }
}
