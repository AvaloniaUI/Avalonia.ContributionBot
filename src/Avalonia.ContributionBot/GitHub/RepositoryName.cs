using System.Diagnostics.CodeAnalysis;

namespace Avalonia.ContributionBot.GitHub;

/// <summary>
/// A GitHub repository identified by its owner and name, written as <c>owner/name</c>.
/// </summary>
public readonly record struct RepositoryName(string Owner, string Name)
{
    public static bool TryParse([NotNullWhen(true)] string? value, out RepositoryName result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split('/');
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            return false;

        result = new RepositoryName(parts[0], parts[1]);
        return true;
    }

    public override string ToString()
        => $"{Owner}/{Name}";
}
