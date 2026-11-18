using Avalonia.ContributionBot.GitHub;

namespace Avalonia.ContributionBot.Configuration;

/// <summary>
/// The settings of a single bot run, read from the command line.
/// </summary>
public sealed class BotSettings
{
    /// <summary>The name of the GitHub event that triggered the run, e.g. <c>pull_request_target</c>.</summary>
    public required string EventName { get; init; }

    /// <summary>The path to the JSON payload of the event.</summary>
    public required string EventFile { get; init; }

    /// <summary>The prefix that starts every bot command, e.g. <c>!contribution-bot</c>.</summary>
    public required string CommandPrefix { get; init; }

    /// <summary>The repository holding the contributors file.</summary>
    public required RepositoryName StoreRepository { get; init; }

    /// <summary>The branch of <see cref="StoreRepository"/> holding the contributors file.</summary>
    public required string StoreBranch { get; init; }

    /// <summary>The path of the contributors file inside <see cref="StoreRepository"/>.</summary>
    public required string StorePath { get; init; }

    /// <summary>The public URL of the Contributor License Agreement.</summary>
    public required Uri ClaUrl { get; init; }

    /// <summary>The URL where new contributors introduce themselves.</summary>
    public required Uri IntroductionUrl { get; init; }

    /// <summary>The context name of the commit status set on pull requests.</summary>
    public required string StatusContext { get; init; }

    /// <summary>The label added to pull requests closed by the bot, or null to not label pull requests.</summary>
    public string? ClosedLabel { get; init; }

    /// <summary>Whether write operations are logged instead of performed.</summary>
    public required bool DryRun { get; init; }
}
