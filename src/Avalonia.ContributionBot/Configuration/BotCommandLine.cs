using System.CommandLine;
using System.CommandLine.Parsing;
using Avalonia.ContributionBot.GitHub;

namespace Avalonia.ContributionBot.Configuration;

public sealed class BotCommandLine
{
    public const string TokenEnvironmentVariable = "GITHUB_TOKEN";

    public BotCommandLine()
    {
        RootCommand = new RootCommand(
            "Checks pull request authors against the vouch list and the CLA signatures. " +
            $"The GitHub token is read from the {TokenEnvironmentVariable} environment variable.")
        {
            EventName,
            EventFile,
            CommandPrefix,
            StoreRepository,
            StoreBranch,
            StorePath,
            ClaUrl,
            IntroductionUrl,
            StatusContext,
            DryRun
        };

        CommandPrefix.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<string>().ContainsAnyWhiteSpace())
                result.AddError($"Option {CommandPrefix.Name} must not contain whitespace.");
        });
    }

    public RootCommand RootCommand { get; }

    public Option<string> EventName { get; } = StringOption(
        "--event-name",
        "Name of the triggering GitHub event.");

    public Option<string> EventFile { get; } = StringOption(
        "--event-file",
        "Path to the JSON payload of the event.");

    public Option<string> CommandPrefix { get; } = StringOption(
        "--command-prefix",
        "Prefix starting every bot command.",
        "!contribution-bot");

    public Option<RepositoryName> StoreRepository { get; } = new("--store-repository")
    {
        Description = "Repository holding the contributors file (owner/name).",
        Required = true,
        CustomParser = ParseRepository
    };

    public Option<string> StoreBranch { get; } = StringOption(
        "--store-branch",
        "Branch holding the contributors file.", "main");

    public Option<string> StorePath { get; } = StringOption(
        "--store-path",
        "Path of the contributors file.",
        "contributors.json");

    public Option<Uri> ClaUrl { get; } = UrlOption(
        "--cla-url",
        "Public URL of the Contributor License Agreement.",
        new Uri("https://github.com/AvaloniaUI/Avalonia/blob/main/CLA.md"));

    public Option<Uri> IntroductionUrl { get; } = UrlOption(
        "--introduction-url",
        "URL where new contributors introduce themselves.",
        new Uri("https://github.com/AvaloniaUI/Avalonia/discussions/categories/introductions"));

    public Option<string> StatusContext { get; } = StringOption(
        "--status-context",
        "Context name of the commit status.",
        "Avalonia Contribution / CLA");

    public Option<bool> DryRun { get; } = new("--dry-run")
    {
        Description = "Log write operations instead of performing them."
    };

    public ParseResult Parse(IReadOnlyList<string> args)
        => RootCommand.Parse(args, new ParserConfiguration { ResponseFileTokenReplacer = null });

    public BotSettings GetSettings(ParseResult parseResult)
    {
        if (parseResult.Errors.Count > 0)
            throw new InvalidOperationException("The command line has errors.");

        return new BotSettings
        {
            EventName = parseResult.GetRequiredValue(EventName),
            EventFile = parseResult.GetRequiredValue(EventFile),
            CommandPrefix = parseResult.GetRequiredValue(CommandPrefix),
            StoreRepository = parseResult.GetRequiredValue(StoreRepository),
            StoreBranch = parseResult.GetRequiredValue(StoreBranch),
            StorePath = parseResult.GetRequiredValue(StorePath),
            ClaUrl = parseResult.GetRequiredValue(ClaUrl),
            IntroductionUrl = parseResult.GetRequiredValue(IntroductionUrl),
            StatusContext = parseResult.GetRequiredValue(StatusContext),
            DryRun = parseResult.GetValue(DryRun)
        };
    }

    private static Option<string> StringOption(string name, string description, string? defaultValue = null)
    {
        var option = new Option<string>(name) { Description = description };

        if (defaultValue is null)
            option.Required = true;
        else
            option.DefaultValueFactory = _ => defaultValue;

        option.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<string>().Length == 0)
                result.AddError($"Option {name} must not be empty.");
        });

        return option;
    }

    private static Option<Uri> UrlOption(string name, string description, Uri? defaultValue = null)
    {
        var option = new Option<Uri>(name)
        {
            Description = description,
            CustomParser = result =>
            {
                var value = result.Tokens[0].Value;
                if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
                {
                    return uri;
                }

                result.AddError($"Option '{name}' must be an absolute http(s) URL, got '{value}'.");
                return null;
            }
        };

        if (defaultValue is null)
            option.Required = true;
        else
            option.DefaultValueFactory = _ => defaultValue;

        return option;
    }

    private static RepositoryName ParseRepository(ArgumentResult result)
    {
        var value = result.Tokens[0].Value;
        if (RepositoryName.TryParse(value, out var repository))
            return repository;

        result.AddError($"Option '--store-repository' must be in the 'owner/name' format, got '{value}'.");
        return default;
    }
}
