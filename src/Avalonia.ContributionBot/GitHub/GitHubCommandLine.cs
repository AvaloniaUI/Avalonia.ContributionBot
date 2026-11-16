using System.ComponentModel;
using System.Diagnostics;

namespace Avalonia.ContributionBot.GitHub;

public static class GitHubCommandLine
{
    /// <summary>
    /// Returns the output of <c>gh auth token</c>, or null if the GitHub CLI is missing or not authenticated.
    /// </summary>
    public static async Task<string?> TryGetTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await Process.RunAndCaptureTextAsync("gh", ["auth", "token"], cancellationToken);
            var token = result.StandardOutput.Trim();

            return result.ExitStatus.ExitCode == 0 && token.StartsWith("gh") ? token : null;
        }
        catch (Win32Exception)
        {
            return null;
        }
    }
}
