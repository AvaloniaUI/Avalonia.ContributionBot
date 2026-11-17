using System.Text.Json.Serialization;

namespace Avalonia.ContributionBot.Migration;

[JsonSerializable(typeof(LegacySignatureFile))]
internal sealed partial class LegacyJsonContext : JsonSerializerContext;