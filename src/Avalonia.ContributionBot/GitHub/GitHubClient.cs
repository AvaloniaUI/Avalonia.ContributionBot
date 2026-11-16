using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Avalonia.ContributionBot.GitHub;

/// <summary>
/// <see cref="IGitHubClient"/> implementation calling the GitHub REST and GraphQL APIs.
/// </summary>
public sealed class GitHubClient(HttpClient http) : IGitHubClient
{
    private const string ApiUrl = "https://api.github.com/";
    private const string GraphQLPath = "graphql";
    private const string ApiVersion = "2022-11-28";
    private const int PageSize = 100;

    public static HttpClient CreateHttpClient(string token, HttpMessageHandler? handler = null)
    {
        var http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.BaseAddress = new Uri(ApiUrl);
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Avalonia.ContributionBot", "1.0"));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", ApiVersion);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    public Task<PullRequest> GetPullRequestAsync(RepositoryName repository, int number, CancellationToken cancellationToken)
        => GetAsync(
            $"{RepositoryPath(repository)}/pulls/{number}",
            GitHubJsonContext.Default.PullRequest,
            cancellationToken);

    public Task<CollaboratorPermission> GetCollaboratorPermissionAsync(RepositoryName repository, string login, CancellationToken cancellationToken)
        => GetAsync(
            $"{RepositoryPath(repository)}/collaborators/{Uri.EscapeDataString(login)}/permission",
            GitHubJsonContext.Default.CollaboratorPermission,
            cancellationToken);

    public async Task<IReadOnlyList<IssueComment>> GetIssueCommentsAsync(RepositoryName repository, int number, CancellationToken cancellationToken)
    {
        var comments = new List<IssueComment>();
        var url = $"{RepositoryPath(repository)}/issues/{number}/comments?per_page={PageSize}";

        while (url is not null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await SendAsync(request, cancellationToken);

            var page = await ReadAsync(response, GitHubJsonContext.Default.ListIssueComment, cancellationToken);
            comments.AddRange(page);
            url = GetNextPageUrl(response);
        }

        return comments;
    }

    public async Task<RepositoryFile?> GetFileAsync(RepositoryName repository, string path, string branch, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{RepositoryPath(repository)}/contents/{EscapePath(path)}?ref={Uri.EscapeDataString(branch)}");

        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(request, response, cancellationToken);

        var content = await ReadAsync(response, GitHubJsonContext.Default.ContentResponse, cancellationToken);
        if (content.Type != "file")
            throw new InvalidOperationException($"'{path}' in {repository}@{branch} is a {content.Type}, not a file.");

        // The contents API omits the content of files larger than 1 MB (encoding "none"); the blob API returns it.
        if (content.Encoding == "base64" && content.Content is not null)
            return new RepositoryFile { Content = DecodeBase64(content.Content), Sha = content.Sha };

        var blob = await GetAsync(
            $"{RepositoryPath(repository)}/git/blobs/{content.Sha}",
            GitHubJsonContext.Default.BlobResponse,
            cancellationToken);

        if (blob.Encoding != "base64")
            throw new InvalidOperationException($"Unexpected blob encoding '{blob.Encoding}' for '{path}'.");

        return new RepositoryFile { Content = DecodeBase64(blob.Content), Sha = blob.Sha };
    }

    public Task<IssueComment> CreateIssueCommentAsync(RepositoryName repository, int number, string body, CancellationToken cancellationToken)
        => SendJsonAsync(
            HttpMethod.Post,
            $"{RepositoryPath(repository)}/issues/{number}/comments",
            new CommentRequest { Body = body },
            GitHubJsonContext.Default.CommentRequest,
            GitHubJsonContext.Default.IssueComment,
            cancellationToken);

    public Task<IssueComment> UpdateIssueCommentAsync(RepositoryName repository, long commentId, string body, CancellationToken cancellationToken)
        => SendJsonAsync(
            HttpMethod.Patch,
            $"{RepositoryPath(repository)}/issues/comments/{commentId}",
            new CommentRequest { Body = body },
            GitHubJsonContext.Default.CommentRequest,
            GitHubJsonContext.Default.IssueComment,
            cancellationToken);

    public Task ClosePullRequestAsync(RepositoryName repository, int number, CancellationToken cancellationToken)
        => SendJsonAsync(
            HttpMethod.Patch,
            $"{RepositoryPath(repository)}/pulls/{number}",
            new UpdatePullRequestRequest { State = "closed" },
            GitHubJsonContext.Default.UpdatePullRequestRequest,
            GitHubJsonContext.Default.PullRequest,
            cancellationToken);

    public async Task CreateCommitStatusAsync(RepositoryName repository, string sha, CommitStatus status, CancellationToken cancellationToken)
    {
        var body = new CommitStatusRequest
        {
            State = status.State switch
            {
                CommitState.Success => "success",
                CommitState.Failure => "failure",
                _ => throw new ArgumentOutOfRangeException(nameof(status), status.State, "Unknown commit state.")
            },
            Context = status.Context,
            Description = status.Description
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{RepositoryPath(repository)}/statuses/{sha}")
        {
            Content = JsonContent.Create(body, GitHubJsonContext.Default.CommitStatusRequest)
        };

        using var response = await SendAsync(request, cancellationToken);
    }

    public async Task<string> PutFileAsync(
        RepositoryName repository,
        string path,
        string branch,
        string content,
        string? previousSha,
        string commitMessage,
        CancellationToken cancellationToken)
    {
        var body = new PutContentRequest
        {
            Message = commitMessage,
            Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
            Branch = branch,
            Sha = previousSha
        };

        using var request = new HttpRequestMessage(HttpMethod.Put, $"{RepositoryPath(repository)}/contents/{EscapePath(path)}")
        {
            Content = JsonContent.Create(body, GitHubJsonContext.Default.PutContentRequest)
        };

        using var response = await http.SendAsync(request, cancellationToken);

        // 409: the SHA doesn't match the current file.
        // 422 without a SHA: the file was created in the meantime.
        if (response.StatusCode == HttpStatusCode.Conflict
            || (response.StatusCode == HttpStatusCode.UnprocessableEntity && previousSha is null))
        {
            var message = await ReadErrorMessageAsync(response, cancellationToken);
            throw new GitHubConflictException(response.StatusCode, Describe(request), message);
        }

        await EnsureSuccessAsync(request, response, cancellationToken);

        var result = await ReadAsync(response, GitHubJsonContext.Default.PutContentResponse, cancellationToken);
        return result.Content.Sha;
    }

    public async Task<Uri> AddDiscussionCommentAsync(string discussionNodeId, string? replyToNodeId, string body, CancellationToken cancellationToken)
    {
        var graphRequest = new GraphQLRequest<AddDiscussionCommentVariables>
        {
            Query =
                """
                mutation($discussionId: ID!, $replyToId: ID, $body: String!) {
                  addDiscussionComment(input: { discussionId: $discussionId, replyToId: $replyToId, body: $body }) {
                    comment { url }
                  }
                }
                """,
            Variables = new AddDiscussionCommentVariables
            {
                DiscussionId = discussionNodeId,
                ReplyToId = replyToNodeId,
                Body = body
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, GraphQLPath)
        {
            Content = JsonContent.Create(graphRequest, GitHubJsonContext.Default.GraphQLRequestAddDiscussionCommentVariables)
        };

        using var response = await SendAsync(request, cancellationToken);
        var result = await ReadAsync(response, GitHubJsonContext.Default.GraphQLResponseAddDiscussionCommentData, cancellationToken);

        if (result.Errors is { Count: > 0 } errors)
        {
            var messages = new string[errors.Count];
            for (var i = 0; i < errors.Count; i++)
                messages[i] = errors[i].Message;

            throw new GitHubApiException(response.StatusCode, Describe(request), string.Join("; ", messages));
        }

        var url = result.Data?.AddDiscussionComment.Comment.Url
            ?? throw new GitHubApiException(response.StatusCode, Describe(request), "The response contains no data.");

        return new Uri(url, UriKind.Absolute);
    }

    private async Task<T> GetAsync<T>(string url, JsonTypeInfo<T> responseType, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync(response, responseType, cancellationToken);
    }

    private async Task<TResponse> SendJsonAsync<TRequest, TResponse>(
        HttpMethod method,
        string url,
        TRequest body,
        JsonTypeInfo<TRequest> requestType,
        JsonTypeInfo<TResponse> responseType,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(body, requestType)
        };

        using var response = await SendAsync(request, cancellationToken);
        return await ReadAsync(response, responseType, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await http.SendAsync(request, cancellationToken);

        try
        {
            await EnsureSuccessAsync(request, response, cancellationToken);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private static async Task EnsureSuccessAsync(HttpRequestMessage request, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var message = await ReadErrorMessageAsync(response, cancellationToken);
        throw new GitHubApiException(response.StatusCode, Describe(request), message);
    }

    private static async Task<string?> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            return JsonSerializer.Deserialize(text, GitHubJsonContext.Default.ErrorResponse)?.Message ?? text;
        }
        catch (JsonException)
        {
            return text.Length == 0 ? null : text;
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> responseType, CancellationToken cancellationToken)
        => await response.Content.ReadFromJsonAsync(responseType, cancellationToken)
            ?? throw new JsonException($"The response to {response.RequestMessage?.RequestUri} is empty.");

    /// <summary>
    /// Returns the URL of the next page from the <c>Link</c> response header, or null on the last page.
    /// GitHub formats the header as <c>&lt;url&gt;; rel="next", &lt;url&gt;; rel="last"</c>.
    /// </summary>
    internal static string? GetNextPageUrl(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out var values))
            return null;

        foreach (var value in values)
        {
            foreach (var link in value.Split(',', StringSplitOptions.TrimEntries))
            {
                var separator = link.IndexOf(';');
                if (separator < 0 || link[(separator + 1)..].Trim() != "rel=\"next\"")
                    continue;

                var url = link[..separator].TrimEnd();
                if (url.StartsWith('<') && url.EndsWith('>'))
                    return url[1..^1];
            }
        }

        return null;
    }

    private static string RepositoryPath(RepositoryName repository)
        => $"repos/{Uri.EscapeDataString(repository.Owner)}/{Uri.EscapeDataString(repository.Name)}";

    private static string EscapePath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length; i++)
            segments[i] = Uri.EscapeDataString(segments[i]);

        return string.Join('/', segments);
    }

    private static string DecodeBase64(string value)
        => Encoding.UTF8.GetString(Convert.FromBase64String(value));

    private static string Describe(HttpRequestMessage request)
        => $"{request.Method} {request.RequestUri}";
}
