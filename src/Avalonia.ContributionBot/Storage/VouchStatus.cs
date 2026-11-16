using System.Text.Json.Serialization;

namespace Avalonia.ContributionBot.Storage;

[JsonConverter(typeof(CamelCaseEnumConverter<VouchStatus>))]
public enum VouchStatus
{
    Vouched,
    Denounced
}
