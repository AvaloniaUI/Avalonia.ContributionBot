using Avalonia.ContributionBot.GitHub;
using Avalonia.ContributionBot.Storage;
using Microsoft.Extensions.Logging.Testing;

namespace Avalonia.ContributionBot.Tests.Storage;

public sealed class ContributorStoreTests
{
    private const string Path = "contributors.json";

    private readonly FakeFileGitHubClient _github = new();
    private readonly ContributorStore _store;

    public ContributorStoreTests()
    {
        _store = new ContributorStore(_github, new RepositoryName("ExampleOrg", "Store"), "main", Path, new FakeLogger<ContributorStore>());
        _github.SetContent(Path, Serialize(CreateSigner(1, "first")));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Loads_Contributors()
    {
        var document = await _store.LoadAsync(Token);

        Assert.Equal("first", Assert.Single(document.Contributors).Login);
    }

    [Fact]
    public async Task Throws_When_File_Is_Missing()
    {
        var store = new ContributorStore(_github, new RepositoryName("ExampleOrg", "Store"), "main", "missing.json", new FakeLogger<ContributorStore>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.LoadAsync(Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpdateAsync(_ => "Change", Token));
    }

    [Fact]
    public async Task Commits_Change()
    {
        var committed = await _store.UpdateAsync(
            document =>
            {
                document.Contributors.Add(CreateSigner(2, "second"));
                return "Add second";
            },
            Token);

        Assert.True(committed);
        Assert.Equal(["Add second"], _github.CommitMessages);
        Assert.Equal(Serialize(CreateSigner(1, "first"), CreateSigner(2, "second")), _github.GetContent(Path));
    }

    [Fact]
    public async Task Skips_Commit_When_Nothing_Changes()
    {
        var committed = await _store.UpdateAsync(_ => null, Token);

        Assert.False(committed);
        Assert.Empty(_github.CommitMessages);
    }

    [Fact]
    public async Task Reapplies_Change_After_Concurrent_Update()
    {
        var calls = 0;
        _github.BeforePut = () =>
        {
            _github.BeforePut = null;
            _github.SetContent(Path, Serialize(CreateSigner(1, "first"), CreateSigner(3, "concurrent")));
        };

        var committed = await _store.UpdateAsync(
            document =>
            {
                calls++;
                document.Contributors.Add(CreateSigner(2, "second"));
                return "Add second";
            },
            Token);

        Assert.True(committed);
        Assert.Equal(2, calls);
        Assert.Equal(
            Serialize(CreateSigner(1, "first"), CreateSigner(2, "second"), CreateSigner(3, "concurrent")),
            _github.GetContent(Path));
    }

    [Fact]
    public async Task Gives_Up_After_Max_Attempts()
    {
        var calls = 0;
        _github.BeforePut = () => _github.SetContent(Path, _github.GetContent(Path)!);

        await Assert.ThrowsAsync<GitHubConflictException>(
            () => _store.UpdateAsync(
                _ =>
                {
                    calls++;
                    return "Change";
                },
                Token));

        Assert.Equal(ContributorStore.MaxAttempts, calls);
        Assert.Empty(_github.CommitMessages);
    }

    private static Contributor CreateSigner(long id, string login)
        => new()
        {
            Id = id,
            Login = login,
            Cla = new ClaSignature { SignedAt = DateTimeOffset.UnixEpoch, Source = "https://example.com" }
        };

    private static string Serialize(params Contributor[] contributors)
        => ContributorsDocumentSerializer.Serialize(new ContributorsDocument { Contributors = [.. contributors] });
}
