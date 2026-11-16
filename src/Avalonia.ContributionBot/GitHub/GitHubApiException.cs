using System.Net;

namespace Avalonia.ContributionBot.GitHub;

public class GitHubApiException(HttpStatusCode statusCode, string request, string? apiMessage)
    : Exception($"GitHub API request '{request}' failed with {(int)statusCode} {statusCode}: {apiMessage ?? "no message"}")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
