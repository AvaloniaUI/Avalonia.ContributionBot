using System.Net;

namespace Avalonia.ContributionBot.GitHub;

public sealed class GitHubConflictException(HttpStatusCode statusCode, string request, string? apiMessage)
    : GitHubApiException(statusCode, request, apiMessage);
