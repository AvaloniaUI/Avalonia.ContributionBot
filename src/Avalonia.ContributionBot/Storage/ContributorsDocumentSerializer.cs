using System.Text.Encodings.Web;
using System.Text.Json;

namespace Avalonia.ContributionBot.Storage;

/// <summary>
/// Reads and writes the contributors file with a stable format, so that its commits have readable diffs:
/// entries sorted by ID, two-space indentation, and LF line endings.
/// </summary>
public static class ContributorsDocumentSerializer
{
    private static readonly ContributorsDocumentJsonContext s_context = new(
        new JsonSerializerOptions(ContributorsDocumentJsonContext.Default.Options)
        {
            // The file is never embedded in HTML, so characters such as '&' or accented letters are written as is.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

    /// <summary>
    /// Serializes the contributors sorted by ID, leaving out entries that have neither a vouch nor a CLA signature.
    /// </summary>
    public static string Serialize(ContributorsDocument document)
    {
        var contributors = new List<Contributor>(document.Contributors);
        contributors.RemoveAll(contributor => contributor.Vouch is null && contributor.Cla is null);
        contributors.Sort((x, y) => x.Id.CompareTo(y.Id));

        return JsonSerializer.Serialize(
            new ContributorsDocument { Contributors = contributors },
            s_context.ContributorsDocument) + "\n";
    }

    /// <summary>
    /// Deserializes the contributors file, rejecting unknown properties and duplicate user IDs.
    /// </summary>
    public static ContributorsDocument Deserialize(string json)
    {
        var document = JsonSerializer.Deserialize(json, s_context.ContributorsDocument)
            ?? throw new JsonException("The contributors file is empty.");

        var ids = new HashSet<long>();
        foreach (var contributor in document.Contributors)
        {
            if (!ids.Add(contributor.Id))
                throw new JsonException($"The contributors file lists user ID {contributor.Id} more than once.");
        }

        return document;
    }
}
