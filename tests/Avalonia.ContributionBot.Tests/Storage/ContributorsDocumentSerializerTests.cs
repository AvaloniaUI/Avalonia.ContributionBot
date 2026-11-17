using System.Text.Json;
using Avalonia.ContributionBot.Storage;

namespace Avalonia.ContributionBot.Tests.Storage;

public sealed class ContributorsDocumentSerializerTests
{
    private const string Sample =
        """
        {
          "contributors": [
            {
              "id": 1234567,
              "login": "some-user",
              "vouch": {
                "status": "vouched",
                "at": "2026-11-20T10:00:00+00:00",
                "by": {
                  "id": 7654321,
                  "login": "some-maintainer"
                },
                "source": "https://github.com/AvaloniaUI/Avalonia/discussions/123#discussioncomment-456"
              },
              "cla": {
                "signedAt": "2026-11-21T09:00:00+00:00",
                "source": "https://github.com/AvaloniaUI/Avalonia/pull/789#issuecomment-1011"
              }
            },
            {
              "id": 2345678,
              "login": "spammer",
              "vouch": {
                "status": "denounced",
                "at": "2026-11-22T08:30:00+00:00",
                "by": {
                  "id": 7654321,
                  "login": "some-maintainer"
                },
                "reason": "Spam & self-promotion, déjà vu",
                "source": "https://github.com/AvaloniaUI/Avalonia/discussions/124#discussioncomment-789"
              }
            },
            {
              "id": 3456789,
              "login": "imported-user",
              "vouch": {
                "status": "vouched",
                "source": "imported"
              }
            }
          ]
        }

        """;

    [Fact]
    public void Round_Trips_Without_Changes()
    {
        Assert.Equal(Sample, ContributorsDocumentSerializer.Serialize(ContributorsDocumentSerializer.Deserialize(Sample)));
    }

    [Fact]
    public void Reads_Every_Field()
    {
        var document = ContributorsDocumentSerializer.Deserialize(Sample);

        var user = document.Find(1234567);
        Assert.NotNull(user);
        Assert.Equal("some-user", user.Login);
        Assert.NotNull(user.Vouch);
        Assert.Equal(VouchStatus.Vouched, user.Vouch.Status);
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero), user.Vouch.At);
        Assert.NotNull(user.Vouch.By);
        Assert.Equal(7654321, user.Vouch.By.Id);
        Assert.Equal("some-maintainer", user.Vouch.By.Login);
        Assert.Null(user.Vouch.Reason);
        Assert.NotNull(user.Cla);
        Assert.Equal(new DateTimeOffset(2026, 11, 21, 9, 0, 0, TimeSpan.Zero), user.Cla.SignedAt);
        Assert.Equal("https://github.com/AvaloniaUI/Avalonia/pull/789#issuecomment-1011", user.Cla.Source);

        var spammer = document.Find(2345678);
        Assert.NotNull(spammer);
        Assert.NotNull(spammer.Vouch);
        Assert.Equal(VouchStatus.Denounced, spammer.Vouch.Status);
        Assert.Equal("Spam & self-promotion, déjà vu", spammer.Vouch.Reason);
        Assert.Null(spammer.Cla);

        var imported = document.Find(3456789);
        Assert.NotNull(imported);
        Assert.NotNull(imported.Vouch);
        Assert.Equal(VouchRecord.ImportedSource, imported.Vouch.Source);
        Assert.Null(imported.Vouch.At);
        Assert.Null(imported.Vouch.By);

        Assert.Null(document.Find(42));
    }

    [Fact]
    public void Sorts_By_Id_And_Drops_Empty_Entries()
    {
        var document = new ContributorsDocument
        {
            Contributors =
            [
                CreateSigner(30, "c"),
                new Contributor { Id = 20, Login = "unvouched" },
                CreateSigner(10, "a")
            ]
        };

        var result = ContributorsDocumentSerializer.Deserialize(ContributorsDocumentSerializer.Serialize(document));

        Assert.Equal([10, 30], [result.Contributors[0].Id, result.Contributors[1].Id]);
        Assert.Equal(3, document.Contributors.Count);
    }

    [Theory]
    [InlineData("""{ "contributors": [{ "id": 1, "login": "a" }, { "id": 1, "login": "b" }] }""")]
    [InlineData("""{ "contributors": [{ "id": 1, "login": "a", "unknown": true }] }""")]
    [InlineData("""{ "contributors": [{ "id": 1 }] }""")]
    [InlineData("""{ "contributors": [{ "id": 1, "login": "a", "vouch": { "status": "maybe", "source": "x" } }] }""")]
    [InlineData("""{ "contributors": [{ "id": 1, "login": "a", "vouch": { "status": 0, "source": "x" } }] }""")]
    [InlineData("""{ "signedContributors": [] }""")]
    [InlineData("null")]
    public void Rejects_Invalid_Files(string json)
    {
        Assert.ThrowsAny<JsonException>(() => ContributorsDocumentSerializer.Deserialize(json));
    }

    private static Contributor CreateSigner(long id, string login)
        => new()
        {
            Id = id,
            Login = login,
            Cla = new ClaSignature { SignedAt = DateTimeOffset.UnixEpoch, Source = "https://example.com" }
        };
}
