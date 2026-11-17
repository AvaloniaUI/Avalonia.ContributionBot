using System.Net;
using System.Text;
using Avalonia.ContributionBot.GitHub;

namespace Avalonia.ContributionBot.Tests.GitHub;

public sealed class GitHubClientTests
{
    private static readonly RepositoryName s_repository = new("AvaloniaUI", "Avalonia");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static GitHubClient CreateClient(FakeHttpHandler handler)
        => new(GitHubClient.CreateHttpClient("test-token", handler));

    private const string PullRequestJson =
        """
        {
          "number": 42,
          "state": "open",
          "user": { "login": "contributor", "id": 1001, "type": "User" },
          "head": { "sha": "abc123", "ref": "feature" },
          "html_url": "https://github.com/AvaloniaUI/Avalonia/pull/42",
          "unknown_field": true
        }
        """;

    private static string CommentJson(long id, string body)
        => $$"""
           {
             "id": {{id}},
             "body": "{{body}}",
             "user": { "login": "contributor", "id": 1001, "type": "User" },
             "html_url": "https://github.com/AvaloniaUI/Avalonia/pull/42#issuecomment-{{id}}",
             "created_at": "2026-11-16T10:00:00Z"
           }
           """;

    [Fact]
    public async Task Sends_Standard_Headers_And_Parses_Pull_Request()
    {
        var handler = new FakeHttpHandler()
            .On(
                HttpMethod.Get,
                "/repos/AvaloniaUI/Avalonia/pulls/42",
                HttpStatusCode.OK,
                PullRequestJson);

        var pullRequest = await CreateClient(handler).GetPullRequestAsync(s_repository, 42, Token);

        Assert.Equal(42, pullRequest.Number);
        Assert.Equal("contributor", pullRequest.User.Login);
        Assert.Equal(1001, pullRequest.User.Id);
        Assert.Equal(GitHubUserTypes.User, pullRequest.User.Type);
        Assert.Equal("abc123", pullRequest.Head.Sha);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(new Uri("https://api.github.com/repos/AvaloniaUI/Avalonia/pulls/42"), request.Uri);
        Assert.Equal("Bearer test-token", request.Headers.Authorization?.ToString());
        Assert.Equal("2022-11-28", Assert.Single(request.Headers.GetValues("X-GitHub-Api-Version")));
        Assert.Contains(request.Headers.Accept, a => a.MediaType == "application/vnd.github+json");
        Assert.NotEmpty(request.Headers.UserAgent);
    }

    [Fact]
    public async Task Throws_Api_Exception_With_Message_On_Error()
    {
        var handler = new FakeHttpHandler()
            .On(
                HttpMethod.Get,
                "/repos/AvaloniaUI/Avalonia/pulls/42",
                HttpStatusCode.NotFound,
                """{ "message": "Not Found" }""");

        var exception = await Assert.ThrowsAsync<GitHubApiException>(
            () => CreateClient(handler).GetPullRequestAsync(s_repository, 42, Token));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Contains("Not Found", exception.Message);
    }

    [Fact]
    public async Task Throws_Api_Exception_With_Raw_Text_On_Non_Json_Error()
    {
        var handler = new FakeHttpHandler()
            .On(
                HttpMethod.Get,
                "/repos/AvaloniaUI/Avalonia/pulls/42",
                HttpStatusCode.BadGateway,
                configure: r => r.Content = new StringContent("Bad gateway"));

        var exception = await Assert.ThrowsAsync<GitHubApiException>(
            () => CreateClient(handler).GetPullRequestAsync(s_repository, 42, Token));

        Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
        Assert.Contains("Bad gateway", exception.Message);
    }

    [Theory]
    [InlineData(CollaboratorPermissions.Admin)]
    [InlineData(CollaboratorPermissions.Write)]
    [InlineData(CollaboratorPermissions.Read)]
    [InlineData(CollaboratorPermissions.None)]
    public async Task Parses_Collaborator_Permission(string permission)
    {
        var handler = new FakeHttpHandler()
            .On(
                HttpMethod.Get,
                "/repos/AvaloniaUI/Avalonia/collaborators/some%20user/permission",
                HttpStatusCode.OK,
                $$"""{ "permission": "{{permission}}", "role_name": "custom" }""");

        var result = await CreateClient(handler).GetCollaboratorPermissionAsync(s_repository, "some user", Token);

        Assert.Equal(permission, result.Permission);
    }

    [Fact]
    public async Task Follows_Pagination_For_Issue_Comments()
    {
        var handler = new FakeHttpHandler()
            .On(
                HttpMethod.Get,
                "/repos/AvaloniaUI/Avalonia/issues/42/comments?per_page=100",
                HttpStatusCode.OK,
                $"[{CommentJson(1, "first")}, {CommentJson(2, "second")}]",
                r => r.Headers.Add(
                    "Link",
                    "<https://api.github.com/repositories/1/issues/42/comments?per_page=100&page=2>; rel=\"next\", " +
                    "<https://api.github.com/repositories/1/issues/42/comments?per_page=100&page=2>; rel=\"last\""))
            .On(
                HttpMethod.Get,
                "/repositories/1/issues/42/comments?per_page=100&page=2",
                HttpStatusCode.OK,
                $"[{CommentJson(3, "third")}]",
                r => r.Headers.Add(
                    "Link",
                    "<https://api.github.com/repositories/1/issues/42/comments?per_page=100&page=1>; rel=\"first\", " +
                    "<https://api.github.com/repositories/1/issues/42/comments?per_page=100&page=1>; rel=\"prev\""));

        var comments = await CreateClient(handler).GetIssueCommentsAsync(s_repository, 42, Token);

        Assert.Equal(["first", "second", "third"], comments.Select(c => c.Body));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Get_File_Returns_Null_When_Missing()
    {
        var handler = new FakeHttpHandler()
            .On(
                HttpMethod.Get,
                "/repos/AvaloniaUI/Avalonia/contents/data/contributors.json?ref=main",
                HttpStatusCode.NotFound,
                """{ "message": "Not Found" }""");

        var file = await CreateClient(handler).GetFileAsync(s_repository, "data/contributors.json", "main", Token);

        Assert.Null(file);
    }

    [Fact]
    public async Task Get_File_Decodes_Base64_Content_With_Line_Breaks()
    {
        var encoded = Convert.ToBase64String("{ \"contributors\": [\"é\"] }"u8.ToArray());
        var wrapped = encoded[..10] + "\\n" + encoded[10..];
        var handler = new FakeHttpHandler()
            .On(
                HttpMethod.Get,
                "/repos/AvaloniaUI/Avalonia/contents/contributors.json?ref=main",
                HttpStatusCode.OK,
                $$"""{ "type": "file", "sha": "blob1", "size": 26, "content": "{{wrapped}}", "encoding": "base64" }""");

        var file = await CreateClient(handler).GetFileAsync(s_repository, "contributors.json", "main", Token);

        Assert.NotNull(file);
        Assert.Equal("{ \"contributors\": [\"é\"] }", file.Content);
        Assert.Equal("blob1", file.Sha);
    }

    [Fact]
    public async Task Get_File_Reads_Large_Files_Through_Blob_Api()
    {
        var encoded = Convert.ToBase64String("large content"u8.ToArray());
        var handler = new FakeHttpHandler()
            .On(
                HttpMethod.Get,
                "/repos/AvaloniaUI/Avalonia/contents/contributors.json?ref=main",
                HttpStatusCode.OK,
                """{ "type": "file", "sha": "blob1", "size": 2000000, "content": "", "encoding": "none" }""")
            .On(
                HttpMethod.Get,
                "/repos/AvaloniaUI/Avalonia/git/blobs/blob1",
                HttpStatusCode.OK,
                $$"""{ "sha": "blob1", "content": "{{encoded}}", "encoding": "base64" }""");

        var file = await CreateClient(handler).GetFileAsync(s_repository, "contributors.json", "main", Token);

        Assert.NotNull(file);
        Assert.Equal("large content", file.Content);
        Assert.Equal("blob1", file.Sha);
    }

    [Fact]
    public async Task Get_File_Rejects_Non_Files()
    {
        var handler = new FakeHttpHandler()
            .On(
                HttpMethod.Get,
                "/repos/AvaloniaUI/Avalonia/contents/data?ref=main",
                HttpStatusCode.OK,
                """{ "type": "dir", "sha": "tree1", "size": 0 }""");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateClient(handler).GetFileAsync(s_repository, "data", "main", Token));
    }

    [Fact]
    public async Task Put_File_Sends_Base64_Content_And_Previous_Sha()
    {
        var handler = new FakeHttpHandler()
            .On(
                HttpMethod.Put,
                "/repos/AvaloniaUI/Avalonia/contents/contributors.json",
                HttpStatusCode.OK,
                """{ "content": { "sha": "blob2" }, "commit": { "sha": "commit1" } }""");

        var sha = await CreateClient(handler).PutFileAsync(
            s_repository, "contributors.json", "main", "new content", "blob1", "Update contributors", Token);

        Assert.Equal("blob2", sha);

        var body = Assert.Single(handler.Requests).GetJsonBody();
        Assert.Equal("Update contributors", body.GetProperty("message").GetString());
        Assert.Equal("new content", Encoding.UTF8.GetString(body.GetProperty("content").GetBytesFromBase64()));
        Assert.Equal("main", body.GetProperty("branch").GetString());
        Assert.Equal("blob1", body.GetProperty("sha").GetString());
    }

    [Fact]
    public async Task Put_File_Omits_Sha_When_Creating()
    {
        var handler = new FakeHttpHandler()
            .On(
                HttpMethod.Put,
                "/repos/AvaloniaUI/Avalonia/contents/contributors.json",
                HttpStatusCode.Created,
                """{ "content": { "sha": "blob1" } }""");

        await CreateClient(handler).PutFileAsync(s_repository, "contributors.json", "main", "content", null, "Create", Token);

        var body = Assert.Single(handler.Requests).GetJsonBody();
        Assert.False(body.TryGetProperty("sha", out _));
    }

    [Fact]
    public async Task Put_File_Throws_Conflict_On_Sha_Mismatch()
    {
        var handler = new FakeHttpHandler()
            .On(
                HttpMethod.Put,
                "/repos/AvaloniaUI/Avalonia/contents/contributors.json",
                HttpStatusCode.Conflict,
                """{ "message": "contributors.json does not match blob1" }""");

        var exception = await Assert.ThrowsAsync<GitHubConflictException>(
            () => CreateClient(handler).PutFileAsync(s_repository, "contributors.json", "main", "content", "blob1", "Update", Token));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
    }

    [Fact]
    public async Task Put_File_Throws_Conflict_When_Created_Concurrently()
    {
        var handler = new FakeHttpHandler()
            .On(
                HttpMethod.Put,
                "/repos/AvaloniaUI/Avalonia/contents/contributors.json",
                HttpStatusCode.UnprocessableEntity,
                """{ "message": "Invalid request.\n\n\"sha\" wasn't supplied." }""");

        var exception = await Assert.ThrowsAsync<GitHubConflictException>(
            () => CreateClient(handler).PutFileAsync(s_repository, "contributors.json", "main", "content", null, "Create", Token));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, exception.StatusCode);
    }

    [Fact]
    public async Task Put_File_Throws_Api_Exception_On_Other_Validation_Errors()
    {
        var handler = new FakeHttpHandler()
            .On(
                HttpMethod.Put,
                "/repos/AvaloniaUI/Avalonia/contents/contributors.json",
                HttpStatusCode.UnprocessableEntity,
                """{ "message": "Validation Failed" }""");

        var exception = await Assert.ThrowsAsync<GitHubApiException>(
            () => CreateClient(handler).PutFileAsync(s_repository, "contributors.json", "main", "content", "blob1", "Update", Token));

        Assert.IsNotType<GitHubConflictException>(exception);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, exception.StatusCode);
    }

    [Fact]
    public async Task Creates_And_Updates_Issue_Comments()
    {
        var handler = new FakeHttpHandler()
            .On(
                HttpMethod.Post,
                "/repos/AvaloniaUI/Avalonia/issues/42/comments",
                HttpStatusCode.Created,
                CommentJson(7, "created"))
            .On(
                HttpMethod.Patch,
                "/repos/AvaloniaUI/Avalonia/issues/comments/7",
                HttpStatusCode.OK,
                CommentJson(7, "updated"));

        var client = CreateClient(handler);
        var created = await client.CreateIssueCommentAsync(s_repository, 42, "created", Token);
        var updated = await client.UpdateIssueCommentAsync(s_repository, 7, "updated", Token);

        Assert.Equal(7, created.Id);
        Assert.Equal("updated", updated.Body);
        Assert.Equal("created", handler.Requests[0].GetJsonBody().GetProperty("body").GetString());
        Assert.Equal("updated", handler.Requests[1].GetJsonBody().GetProperty("body").GetString());
    }

    [Fact]
    public async Task Closes_Pull_Request()
    {
        var handler = new FakeHttpHandler()
            .On(
                HttpMethod.Patch,
                "/repos/AvaloniaUI/Avalonia/pulls/42",
                HttpStatusCode.OK,
                PullRequestJson);

        await CreateClient(handler).ClosePullRequestAsync(s_repository, 42, Token);

        Assert.Equal(IssueStates.Closed, Assert.Single(handler.Requests).GetJsonBody().GetProperty("state").GetString());
    }

    [Theory]
    [InlineData(CommitState.Success, "success")]
    [InlineData(CommitState.Failure, "failure")]
    public async Task Creates_Commit_Status(CommitState state, string expectedState)
    {
        var handler = new FakeHttpHandler()
            .On(
                HttpMethod.Post,
                "/repos/AvaloniaUI/Avalonia/statuses/abc123",
                HttpStatusCode.Created,
                "{}");

        await CreateClient(handler).CreateCommitStatusAsync(
            s_repository,
            "abc123",
            new CommitStatus { State = state, Context = "Avalonia Contribution / CLA", Description = "Description" },
            Token);

        var body = Assert.Single(handler.Requests).GetJsonBody();
        Assert.Equal(expectedState, body.GetProperty("state").GetString());
        Assert.Equal("Avalonia Contribution / CLA", body.GetProperty("context").GetString());
        Assert.Equal("Description", body.GetProperty("description").GetString());
    }

    [Fact]
    public async Task Adds_Discussion_Comment_Through_GraphQL()
    {
        var handler = new FakeHttpHandler()
            .On(
                HttpMethod.Post,
                "/graphql",
                HttpStatusCode.OK,
                """{ "data": { "addDiscussionComment": { "comment": { "url": "https://github.com/d/1#c" } } } }""");

        var url = await CreateClient(handler).AddDiscussionCommentAsync("D_1", "DC_2", "Hello", Token);

        Assert.Equal(new Uri("https://github.com/d/1#c"), url);

        var body = Assert.Single(handler.Requests).GetJsonBody();
        Assert.Contains("addDiscussionComment", body.GetProperty("query").GetString());

        var variables = body.GetProperty("variables");
        Assert.Equal("D_1", variables.GetProperty("discussionId").GetString());
        Assert.Equal("DC_2", variables.GetProperty("replyToId").GetString());
        Assert.Equal("Hello", variables.GetProperty("body").GetString());
    }

    [Fact]
    public async Task Gets_Reply_To_Of_Discussion_Comment()
    {
        var handler = new FakeHttpHandler()
            .On(
                HttpMethod.Post,
                "/graphql",
                HttpStatusCode.OK,
                """{ "data": { "node": { "replyTo": { "id": "DC_1" } } } }""");

        var replyTo = await CreateClient(handler).GetDiscussionCommentReplyToAsync("DC_2", Token);

        Assert.Equal("DC_1", replyTo);
        Assert.Equal("DC_2", Assert.Single(handler.Requests).GetJsonBody().GetProperty("variables").GetProperty("id").GetString());
    }

    [Fact]
    public async Task Gets_No_Reply_To_For_Top_Level_Discussion_Comment()
    {
        var handler = new FakeHttpHandler()
            .On(
                HttpMethod.Post,
                "/graphql",
                HttpStatusCode.OK,
                """{ "data": { "node": { "replyTo": null } } }""");

        Assert.Null(await CreateClient(handler).GetDiscussionCommentReplyToAsync("DC_1", Token));
    }

    [Fact]
    public async Task Gets_User()
    {
        var handler = new FakeHttpHandler()
            .On(
                HttpMethod.Get,
                "/users/some-user",
                HttpStatusCode.OK,
                """{ "login": "some-user", "id": 1234, "type": "User", "site_admin": false }""");

        var user = await CreateClient(handler).GetUserAsync("some-user", Token);

        Assert.NotNull(user);
        Assert.Equal(1234, user.Id);
        Assert.Equal("some-user", user.Login);
    }

    [Fact]
    public async Task Returns_Null_For_Missing_User()
    {
        var handler = new FakeHttpHandler()
            .On(HttpMethod.Get, "/users/nobody", HttpStatusCode.NotFound, """{ "message": "Not Found" }""");

        Assert.Null(await CreateClient(handler).GetUserAsync("nobody", Token));
    }

    [Fact]
    public async Task Throws_On_GraphQL_Errors()
    {
        var handler = new FakeHttpHandler()
            .On(
                HttpMethod.Post,
                "/graphql",
                HttpStatusCode.OK,
                """{ "data": null, "errors": [{ "message": "Could not resolve to a node" }] }""");

        var exception = await Assert.ThrowsAsync<GitHubApiException>(
            () => CreateClient(handler).AddDiscussionCommentAsync("D_1", null, "Hello", Token));

        Assert.EndsWith("Could not resolve to a node", exception.Message);
    }

    [Theory]
    [InlineData("<https://api.github.com/x?page=2>; rel=\"next\"", "https://api.github.com/x?page=2")]
    [InlineData("<https://api.github.com/x?page=1>; rel=\"prev\", <https://api.github.com/x?page=3>; rel=\"next\"", "https://api.github.com/x?page=3")]
    [InlineData("<https://api.github.com/x?page=1>; rel=\"first\"", null)]
    public void Parses_Next_Page_From_Link_Header(string link, string? expected)
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.Add("Link", link);

        Assert.Equal(expected, GitHubClient.GetNextPageUrl(response));
    }

    [Fact]
    public void Next_Page_Is_Null_Without_Link_Header()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK);

        Assert.Null(GitHubClient.GetNextPageUrl(response));
    }
}
