using Avalonia.ContributionBot.GitHub;
using Avalonia.ContributionBot.Storage;

namespace Avalonia.ContributionBot.Migration;

/// <summary>
/// Converts the legacy CLA signatures into contributors: every signer becomes vouched,
/// and duplicate signatures of a user are merged, keeping the earliest one.
/// </summary>
public static class LegacyMigration
{
    /// <param name="legacy">The legacy signatures.</param>
    /// <param name="repository">The repository in which every legacy signature was made.</param>
    /// <param name="repositoryId">The ID of <paramref name="repository"/>, which every signature must reference.</param>
    /// <exception cref="InvalidDataException">A signature has an invalid user ID or references another repository.</exception>
    public static MigrationResult Migrate(LegacySignatureFile legacy, RepositoryName repository, long repositoryId)
    {
        var signatures = new Dictionary<long, LegacySignature>();
        var merges = new List<MergedSignature>();

        foreach (var signature in legacy.SignedContributors)
        {
            if (signature.Id <= 0)
                throw new InvalidDataException($"The signature of '{signature.Name}' has an invalid user ID {signature.Id}.");

            if (signature.RepoId != repositoryId)
            {
                throw new InvalidDataException(
                    $"The signature of '{signature.Name}' references repository ID {signature.RepoId}, " +
                    $"expected {repositoryId} ({repository}).");
            }

            if (!signatures.TryGetValue(signature.Id, out var existing))
            {
                signatures.Add(signature.Id, signature);
                continue;
            }

            var (kept, dropped) = signature.CreatedAt < existing.CreatedAt ? (signature, existing) : (existing, signature);
            signatures[signature.Id] = kept;
            merges.Add(new MergedSignature
            {
                Id = signature.Id,
                Login = kept.Name,
                KeptSource = GetCommentUrl(repository, kept),
                DroppedSource = GetCommentUrl(repository, dropped)
            });
        }

        var contributors = new List<Contributor>(signatures.Count);
        foreach (var signature in signatures.Values)
        {
            contributors.Add(new Contributor
            {
                Id = signature.Id,
                Login = signature.Name,
                Vouch = new VouchRecord
                {
                    Status = VouchStatus.Vouched,
                    Source = VouchRecord.ImportedSource
                },
                Cla = new ClaSignature
                {
                    SignedAt = signature.CreatedAt,
                    Source = GetCommentUrl(repository, signature)
                }
            });
        }

        return new MigrationResult
        {
            Document = new ContributorsDocument { Contributors = contributors },
            Merges = merges
        };
    }

    private static string GetCommentUrl(RepositoryName repository, LegacySignature signature)
        => $"https://github.com/{repository}/pull/{signature.PullRequestNo}#issuecomment-{signature.CommentId}";
}
