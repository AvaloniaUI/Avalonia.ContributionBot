using System.CommandLine.Help;
using Avalonia.ContributionBot.Configuration;
using Avalonia.ContributionBot.Events;
using Avalonia.ContributionBot.GitHub;
using Avalonia.ContributionBot.Logging;
using Microsoft.Extensions.Logging;

internal static partial class Program
{
    private const int Success = 0;
    private const int Failure = 1;
    private const int InvalidUsage = 2;

    private static bool IsGitHubActions
        => Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";

    public static async Task<int> Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);

            if (IsGitHubActions)
                builder.AddProvider(new GitHubActionsLoggerProvider(Console.Out));
            else
                builder.AddSimpleConsole(options => options.SingleLine = true);
        });

        var logger = loggerFactory.CreateLogger("Avalonia.ContributionBot");
        var commandLine = new BotCommandLine();

        commandLine.RootCommand.SetAction(
            (parseResult, cancellationToken) => RunAsync(commandLine.GetSettings(parseResult), loggerFactory, logger, cancellationToken));

        var parseResult = commandLine.Parse(args);

        if (parseResult.Errors.Count > 0 && parseResult.Action is not HelpAction)
        {
            foreach (var error in parseResult.Errors)
                LogCommandLineError(logger, error.Message);

            LogHelpHint(logger);
            return InvalidUsage;
        }

        return await parseResult.InvokeAsync();
    }

    private static async Task<int> RunAsync(
        BotSettings settings,
        ILoggerFactory loggerFactory,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var token = Environment.GetEnvironmentVariable(BotCommandLine.TokenEnvironmentVariable);
            if (string.IsNullOrEmpty(token))
                token = null;

            if (token is null && !IsGitHubActions)
                token = await GitHubCommandLine.TryGetTokenAsync(cancellationToken);

            if (token is null)
            {
                LogMissingToken(logger, BotCommandLine.TokenEnvironmentVariable);
                return InvalidUsage;
            }

            LogEvent(logger, settings.EventName, settings.EventFile);
            if (settings.DryRun)
                LogDryRun(logger);

            using var http = GitHubClient.CreateHttpClient(token);
            IGitHubClient github = new GitHubClient(http);
            if (settings.DryRun)
                github = new DryRunGitHubClient(github, loggerFactory.CreateLogger<DryRunGitHubClient>());

            var dispatcher = new EventDispatcher(
                new PullRequestEventHandler(loggerFactory.CreateLogger<PullRequestEventHandler>()),
                new IssueCommentEventHandler(settings, loggerFactory.CreateLogger<IssueCommentEventHandler>()),
                new DiscussionCommentEventHandler(settings, loggerFactory.CreateLogger<DiscussionCommentEventHandler>()),
                loggerFactory.CreateLogger<EventDispatcher>());

            await using var payload = File.OpenRead(settings.EventFile);
            await dispatcher.DispatchAsync(settings.EventName, payload, cancellationToken);
            return Success;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            LogCancelled(logger);
            return Failure;
        }
        catch (Exception ex)
        {
            LogFailure(logger, ex);
            return Failure;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "{ErrorMessage}")]
    private static partial void LogCommandLineError(ILogger logger, string errorMessage);

    [LoggerMessage(Level = LogLevel.Information, Message = "Run with --help to list the options.")]
    private static partial void LogHelpHint(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "No GitHub token: set the {VariableName} environment variable.")]
    private static partial void LogMissingToken(ILogger logger, string variableName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Event '{EventName}' from {EventFile}.")]
    private static partial void LogEvent(ILogger logger, string eventName, string eventFile);

    [LoggerMessage(Level = LogLevel.Information, Message = "Dry run: write operations are logged instead of performed.")]
    private static partial void LogDryRun(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Cancelled.")]
    private static partial void LogCancelled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "The run failed.")]
    private static partial void LogFailure(ILogger logger, Exception exception);
}
