using System.Text.Json;
using System.Text.Json.Serialization;

namespace Avalonia.ContributionBot.Migration;

/// <summary>
/// The content of <c>signee-cla.json</c>, written by the legacy CLA bot.
/// </summary>
public sealed class LegacySignatureFile
{
    [JsonPropertyName("signedContributors")]
    public required List<LegacySignature> SignedContributors { get; init; }

    public static LegacySignatureFile Deserialize(string json)
        => JsonSerializer.Deserialize(json, LegacyJsonContext.Default.LegacySignatureFile)
            ?? throw new JsonException("The legacy signature file is empty.");
}
