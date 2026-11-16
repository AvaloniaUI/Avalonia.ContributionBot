using Avalonia.ContributionBot.Logging;
using Microsoft.Extensions.Logging;

namespace Avalonia.ContributionBot.Tests.Logging;

public sealed class GitHubActionsLoggerProviderTests
{
    private readonly StringWriter _writer = new() { NewLine = "\n" };

    private ILogger CreateLogger()
        => new GitHubActionsLoggerProvider(_writer).CreateLogger("Test");

    [Theory]
    [InlineData(LogLevel.Trace, "::debug::message\n")]
    [InlineData(LogLevel.Debug, "::debug::message\n")]
    [InlineData(LogLevel.Information, "message\n")]
    [InlineData(LogLevel.Warning, "::warning::message\n")]
    [InlineData(LogLevel.Error, "::error::message\n")]
    [InlineData(LogLevel.Critical, "::error::message\n")]
    public void Writes_Level_As_Workflow_Command(LogLevel level, string expected)
    {
        CreateLogger().Log(level, "message");

        Assert.Equal(expected, _writer.ToString());
    }

    [Fact]
    public void Writes_Information_Lines_Unescaped()
    {
        CreateLogger().LogInformation("first\nsecond 100%");

        Assert.Equal("first\nsecond 100%\n", _writer.ToString());
    }

    [Fact]
    public void Escapes_Workflow_Command_Message()
    {
        CreateLogger().LogError("100%\r\nfailed");

        Assert.Equal("::error::100%25%0D%0Afailed\n", _writer.ToString());
    }

    [Fact]
    public void Appends_Exception_To_Message()
    {
        CreateLogger().LogError(new InvalidOperationException("boom"), "failed");

        Assert.Equal("::error::failed%0ASystem.InvalidOperationException: boom\n", _writer.ToString());
    }

    [Fact]
    public void Ignores_None_Level()
    {
        CreateLogger().Log(LogLevel.None, "message");

        Assert.Empty(_writer.ToString());
    }
}
