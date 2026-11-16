using System.Text.Json.Serialization;

namespace Avalonia.ContributionBot.Storage;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = true,
    NewLine = "\n")]
[JsonSerializable(typeof(ContributorsDocument))]
internal sealed partial class ContributorsDocumentJsonContext : JsonSerializerContext;
