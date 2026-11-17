using System.Diagnostics.CodeAnalysis;

namespace Avalonia.ContributionBot.Events;

/// <summary>
/// A <c>vouch</c>, <c>denounce</c> or <c>unvouch</c> command posted in a discussion:
/// the command name, an optional <c>@user</c> target, then an optional free-text reason.
/// </summary>
public sealed class VouchCommand
{
    private const string VouchCommandName = "vouch";
    private const string DenounceCommandName = "denounce";
    private const string UnvouchCommandName = "unvouch";

    public required VouchAction Action { get; init; }

    /// <summary>The login of the target user without the leading <c>@</c>, or null to target the discussion author.</summary>
    public string? Target { get; init; }

    public string? Reason { get; init; }

    /// <summary>
    /// Parses the command text following the prefix, as returned by <see cref="CommandText.TryParse"/>.
    /// </summary>
    /// <returns>true if the text starts with a known command name (case-insensitive) and any <c>@</c> target is non-empty.</returns>
    public static bool TryParse(string text, [NotNullWhen(true)] out VouchCommand? command)
    {
        command = null;

        var rest = text.AsSpan().Trim();
        var name = TakeWord(ref rest);

        VouchAction action;
        if (name.Equals(VouchCommandName, StringComparison.OrdinalIgnoreCase))
            action = VouchAction.Vouch;
        else if (name.Equals(DenounceCommandName, StringComparison.OrdinalIgnoreCase))
            action = VouchAction.Denounce;
        else if (name.Equals(UnvouchCommandName, StringComparison.OrdinalIgnoreCase))
            action = VouchAction.Unvouch;
        else
            return false;

        string? target = null;
        if (rest.StartsWith('@'))
        {
            var login = TakeWord(ref rest)[1..];
            if (login.IsEmpty)
                return false;

            target = login.ToString();
        }

        command = new VouchCommand
        {
            Action = action,
            Target = target,
            Reason = rest.IsEmpty ? null : rest.ToString()
        };
        return true;
    }

    /// <summary>
    /// Returns the text up to the first white space, and advances <paramref name="text"/> to the next word.
    /// </summary>
    private static ReadOnlySpan<char> TakeWord(ref ReadOnlySpan<char> text)
    {
        var index = text.IndexOfAnyWhiteSpace();
        if (index < 0)
        {
            var last = text;
            text = [];
            return last;
        }

        var word = text[..index];
        text = text[index..].TrimStart();
        return word;
    }
}
