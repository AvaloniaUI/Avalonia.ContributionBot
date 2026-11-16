using System.Text.Json;
using System.Text.Json.Serialization;

namespace Avalonia.ContributionBot.Storage;

/// <summary>
/// Serializes enum values as camel-case strings.
/// </summary>
internal sealed class CamelCaseEnumConverter<TEnum>()
    : JsonStringEnumConverter<TEnum>(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
    where TEnum : struct, Enum;
