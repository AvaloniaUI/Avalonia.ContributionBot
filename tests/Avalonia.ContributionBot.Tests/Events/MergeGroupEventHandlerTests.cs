using System.Text.Json;
using Avalonia.ContributionBot.Events;
using Avalonia.ContributionBot.GitHub;
using Avalonia.ContributionBot.Tests.GitHub;
using Microsoft.Extensions.Logging.Testing;

namespace Avalonia.ContributionBot.Tests.Events;

public sealed class MergeGroupEventHandlerTests
{
    private readonly FakeGitHubClient _github = new();
    private readonly FakeLogger<MergeGroupEventHandler> _logger = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sets_Success_Status_On_Merge_Group_Commit()
    {
        await HandleAsync(SamplePayloads.MergeGroup(MergeGroupActions.ChecksRequested));

        var (sha, status) = Assert.Single(_github.Statuses);
        Assert.Equal("def456", sha);
        Assert.Equal(CommitState.Success, status.State);
        Assert.Equal(TestSettings.Default.StatusContext, status.Context);
        Assert.Equal("Merge group AvaloniaUI/Avalonia@def456: success.", _logger.LatestRecord.Message);
    }

    [Fact]
    public async Task Ignores_Destroyed_Merge_Group()
    {
        await HandleAsync(SamplePayloads.MergeGroup(MergeGroupActions.Destroyed));

        Assert.Empty(_github.Statuses);
        Assert.Equal("Ignoring merge group action 'destroyed'.", Assert.Single(_logger.Collector.GetSnapshot()).Message);
    }

    private async Task HandleAsync(string json)
    {
        var handler = new MergeGroupEventHandler(_github, TestSettings.Default, _logger);
        var payload = Assert.IsType<MergeGroupEvent>(JsonSerializer.Deserialize(json, typeof(MergeGroupEvent), GitHubRestJsonContext.Default));

        await handler.HandleAsync(payload, Token);
    }
}
