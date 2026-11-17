using Avalonia.ContributionBot.Configuration;
using Avalonia.ContributionBot.GitHub;

namespace Avalonia.ContributionBot.Comments;

public static class BotComment
{
    /// <summary>The hidden marker starting every pull request comment of the bot.</summary>
    public const string Marker = "<!-- avalonia-contribution-bot -->";

    /// <summary>
    /// Returns the comment body for <paramref name="template"/>, starting with <see cref="Marker"/>.
    /// </summary>
    public static string Render(BotCommentTemplate template, string author, RepositoryName repository, BotSettings settings)
    {
        var text = CommentTemplate.Load(template.ToString())
            .Replace("{author}", author)
            .Replace("{repo}", repository.ToString())
            .Replace("{prefix}", settings.CommandPrefix)
            .Replace("{claUrl}", settings.ClaUrl.AbsoluteUri)
            .Replace("{introductionUrl}", settings.IntroductionUrl.AbsoluteUri);

        return $"{Marker}\n{text}";
    }

    /// <summary>
    /// Returns the first comment posted by a bot account that starts with <see cref="Marker"/>, or null if there is none.
    /// The author check keeps the bot from taking over a user's comment that copies the marker.
    /// </summary>
    public static IssueComment? Find(IReadOnlyList<IssueComment> comments)
    {
        foreach (var comment in comments)
        {
            if (comment.User.Type == GitHubUserTypes.Bot && comment.Body is not null && comment.Body.StartsWith(Marker, StringComparison.Ordinal))
                return comment;
        }

        return null;
    }
}
