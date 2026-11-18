using System.Text;
using System.Text.Json;
using Avalonia.ContributionBot.Events;
using Avalonia.ContributionBot.GitHub;

namespace Avalonia.ContributionBot.Tests.Events;

/// <summary>
/// Event payloads shaped like the ones GitHub sends, reduced to the fields the bot reads plus a few extra ones.
/// </summary>
internal static class SamplePayloads
{
    private const string Repository =
        """
        { "id": 14075717, "name": "Avalonia", "full_name": "AvaloniaUI/Avalonia", "private": false }
        """;

    private const string Sender =
        """
        { "login": "sender", "id": 2002, "type": "User", "site_admin": false }
        """;

    public static string PullRequest(string action = PullRequestActions.Opened)
        => $$"""
           {
             "action": "{{action}}",
             "number": 42,
             "pull_request": {
               "number": 42,
               "state": "open",
               "user": { "login": "contributor", "id": 1001, "type": "User" },
               "head": { "sha": "abc123", "ref": "feature", "label": "contributor:feature" },
               "html_url": "https://github.com/AvaloniaUI/Avalonia/pull/42",
               "draft": false
             },
             "repository": {{Repository}},
             "sender": {{Sender}}
           }
           """;

    public static string IssueComment(
        string body,
        string action = CommentActions.Created,
        bool isPullRequest = true,
        string state = IssueStates.Open,
        string commenterLogin = "commenter",
        long commenterId = 3003,
        string commenterType = GitHubUserTypes.User)
    {
        var pullRequest = isPullRequest
            ? """, "pull_request": { "url": "https://api.github.com/repos/AvaloniaUI/Avalonia/pulls/42" }"""
            : "";

        return $$"""
               {
                 "action": "{{action}}",
                 "issue": {
                   "number": 42,
                   "state": "{{state}}",
                   "user": { "login": "contributor", "id": 1001, "type": "User" }
                   {{pullRequest}}
                 },
                 "comment": {
                   "id": 555,
                   "body": {{JsonSerializer.Serialize(body)}},
                   "user": { "login": "{{commenterLogin}}", "id": {{commenterId}}, "type": "{{commenterType}}" },
                   "html_url": "https://github.com/AvaloniaUI/Avalonia/pull/42#issuecomment-555",
                   "created_at": "2026-11-16T10:00:00Z"
                 },
                 "repository": {{Repository}},
                 "sender": {{Sender}}
               }
               """;
    }

    public static string DiscussionComment(
        string body,
        string action = CommentActions.Created,
        long? parentId = null,
        string commenterLogin = "maintainer",
        long commenterId = 5005,
        string commenterType = GitHubUserTypes.User)
        => $$"""
           {
             "action": "{{action}}",
             "discussion": {
               "number": 7,
               "node_id": "D_kwDOA",
               "user": { "login": "newcomer", "id": 4004, "type": "User" },
               "html_url": "https://github.com/AvaloniaUI/Avalonia/discussions/7"
             },
             "comment": {
               "id": 666,
               "node_id": "DC_kwDOA",
               "body": {{JsonSerializer.Serialize(body)}},
               "user": { "login": "{{commenterLogin}}", "id": {{commenterId}}, "type": "{{commenterType}}" },
               "html_url": "https://github.com/AvaloniaUI/Avalonia/discussions/7#discussioncomment-666",
               "created_at": "2026-11-17T09:00:00Z",
               "parent_id": {{(parentId?.ToString() ?? "null")}}
             },
             "repository": {{Repository}},
             "sender": {{Sender}}
           }
           """;

    public static string MergeGroup(string action)
        => $$"""
           {
             "action": "{{action}}",
             "merge_group": {
               "head_sha": "def456",
               "head_ref": "refs/heads/gh-readonly-queue/main/pr-42-abc123",
               "base_sha": "aaa111",
               "base_ref": "refs/heads/main"
             },
             "repository": {{Repository}},
             "sender": {{Sender}}
           }
           """;

    public static Stream ToStream(string json)
        => new MemoryStream(Encoding.UTF8.GetBytes(json));
}
