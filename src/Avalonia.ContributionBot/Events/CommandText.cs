using System.Diagnostics.CodeAnalysis;

namespace Avalonia.ContributionBot.Events;

/// <summary>
/// Recognizes bot commands in comments: the first non-empty line starts with the command prefix
/// (case-insensitive), followed by the command text.
/// </summary>
public static class CommandText
{
    /// <summary>
    /// Extracts the text following the prefix on the first non-empty line of <paramref name="body"/>.
    /// </summary>
    /// <returns>True if the comment starts with the prefix followed by a non-empty command.</returns>
    public static bool TryParse(string? body, string prefix, [NotNullWhen(true)] out string? command)
    {
        command = null;

        var text = body.AsSpan().TrimStart();
        var lineEnd = text.IndexOf('\n');
        var line = lineEnd < 0 ? text : text[..lineEnd];

        if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var rest = line[prefix.Length..];
        if (rest.IsEmpty || !char.IsWhiteSpace(rest[0]))
            return false;

        rest = rest.Trim();
        if (rest.IsEmpty)
            return false;

        command = rest.ToString();
        return true;
    }
}
