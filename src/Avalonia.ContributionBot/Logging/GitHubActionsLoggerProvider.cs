using Microsoft.Extensions.Logging;

namespace Avalonia.ContributionBot.Logging;

/// <summary>
/// Creates loggers writing to the GitHub Actions run log: information messages are written as plain lines,
/// other levels as workflow commands (<c>::debug::</c>, <c>::warning::</c>, <c>::error::</c>),
/// which render as annotations in the run summary.
/// </summary>
public sealed class GitHubActionsLoggerProvider(TextWriter writer) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
        => new GitHubActionsLogger(writer);

    public void Dispose()
    {
    }

    private sealed class GitHubActionsLogger(TextWriter writer) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel)
            => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            if (exception is not null)
                message = $"{message}\n{exception}";

            switch (logLevel)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                    WriteCommand("debug", message);
                    break;

                case LogLevel.Information:
                    writer.WriteLine(message);
                    break;

                case LogLevel.Warning:
                    WriteCommand("warning", message);
                    break;

                case LogLevel.Error:
                case LogLevel.Critical:
                    WriteCommand("error", message);
                    break;
            }
        }

        private void WriteCommand(string command, string message)
            => writer.WriteLine($"::{command}::{message.Replace("%", "%25").Replace("\r", "%0D").Replace("\n", "%0A")}");
    }
}
